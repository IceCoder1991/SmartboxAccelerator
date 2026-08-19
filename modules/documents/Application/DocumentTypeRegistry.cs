using System.Collections.Concurrent;
using System.Diagnostics;
using Smartboxx.Modules.Documents.Domain;
using Smartboxx.Platform.Contracts;

namespace Smartboxx.Modules.Documents.Application;

public sealed record SaveDocumentTypeRequest(string TechnicalName, string DisplayName, string? Description,
    string ClassificationInstructions, ExtractionSchema ExtractionSchema, string Prompt,
    ValidationRuleSet ValidationRules, ReviewPolicy ReviewPolicy, ModelConfiguration ModelConfiguration,
    string? EvaluationDatasetReference, OutputMapping OutputMapping);

public sealed class RegistryValidationException(IReadOnlyDictionary<string, string[]> errors) : Exception("Document type validation failed.")
{ public IReadOnlyDictionary<string, string[]> Errors { get; } = errors; }
public sealed class RegistryConflictException(string message) : Exception(message);

public interface IDocumentTypeRepository
{
    Task<IReadOnlyList<DocumentTypeDefinition>> ListAsync(string tenantId, CancellationToken cancellationToken);
    Task<DocumentTypeDefinition?> GetAsync(string tenantId, Guid id, int? version, CancellationToken cancellationToken);
    Task AddAsync(DocumentTypeDefinition definition, CancellationToken cancellationToken);
    Task ReplaceAsync(DocumentTypeDefinition definition, CancellationToken cancellationToken);
}

public sealed class InMemoryDocumentTypeRepository : IDocumentTypeRepository
{
    private readonly ConcurrentDictionary<(string Tenant, Guid Id, int Version), DocumentTypeDefinition> _items = new();
    public Task<IReadOnlyList<DocumentTypeDefinition>> ListAsync(string tenantId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DocumentTypeDefinition>>(_items.Values.Where(x => x.TenantId == tenantId).OrderBy(x => x.TechnicalName).ThenByDescending(x => x.Version).ToArray());
    public Task<DocumentTypeDefinition?> GetAsync(string tenantId, Guid id, int? version, CancellationToken cancellationToken) =>
        Task.FromResult(_items.Values.Where(x => x.TenantId == tenantId && x.Id == id && (version is null || x.Version == version)).OrderByDescending(x => x.Version).FirstOrDefault());
    public Task AddAsync(DocumentTypeDefinition definition, CancellationToken cancellationToken)
    { if (!_items.TryAdd((definition.TenantId, definition.Id, definition.Version), definition)) throw new RegistryConflictException("The document type version already exists."); return Task.CompletedTask; }
    public Task ReplaceAsync(DocumentTypeDefinition definition, CancellationToken cancellationToken)
    { _items[(definition.TenantId, definition.Id, definition.Version)] = definition; return Task.CompletedTask; }
}

public sealed class DocumentTypeRegistry(IDocumentTypeRepository repository, ICurrentUser user, IAuditService audit)
{
    private static readonly ActivitySource Telemetry = new("Smartboxx.Documents.Registry");
    public Task<IReadOnlyList<DocumentTypeDefinition>> ListAsync(CancellationToken ct) => repository.ListAsync(user.ClientId, ct);
    public Task<DocumentTypeDefinition?> GetAsync(Guid id, int? version, CancellationToken ct) => repository.GetAsync(user.ClientId, id, version, ct);

    public async Task<DocumentTypeDefinition> CreateAsync(SaveDocumentTypeRequest request, CancellationToken ct)
    {
        Validate(request); using var activity = Telemetry.StartActivity("document_type.create");
        var now = DateTimeOffset.UtcNow;
        var value = Build(Guid.NewGuid(), 1, RegistryLifecycle.Draft, request, now, now);
        await repository.AddAsync(value, ct); await Audit("document_type.created", value, ct); return value;
    }
    public async Task<DocumentTypeDefinition> UpdateAsync(Guid id, SaveDocumentTypeRequest request, CancellationToken ct)
    {
        Validate(request); using var activity = Telemetry.StartActivity("document_type.update");
        var current = await repository.GetAsync(user.ClientId, id, null, ct) ?? throw new KeyNotFoundException();
        if (!current.IsMutable) throw new RegistryConflictException("Published and archived versions are immutable; create a new draft version.");
        var value = Build(id, current.Version, RegistryLifecycle.Draft, request, current.CreatedAt, DateTimeOffset.UtcNow);
        await repository.ReplaceAsync(value, ct); await Audit("document_type.updated", value, ct); return value;
    }
    public async Task<DocumentTypeDefinition> PublishAsync(Guid id, CancellationToken ct)
    {
        using var activity = Telemetry.StartActivity("document_type.publish");
        var current = await repository.GetAsync(user.ClientId, id, null, ct) ?? throw new KeyNotFoundException();
        if (!current.IsMutable) throw new RegistryConflictException("Only a draft can be published.");
        var value = current with { Lifecycle = RegistryLifecycle.Published, UpdatedAt = DateTimeOffset.UtcNow, UpdatedBy = user.UserId };
        await repository.ReplaceAsync(value, ct); await Audit("document_type.published", value, ct); return value;
    }
    public async Task<DocumentTypeDefinition> CloneDraftAsync(Guid id, CancellationToken ct)
    {
        var current = await repository.GetAsync(user.ClientId, id, null, ct) ?? throw new KeyNotFoundException();
        if (current.Lifecycle != RegistryLifecycle.Published) throw new RegistryConflictException("Only a published version can be cloned.");
        var now = DateTimeOffset.UtcNow; var value = current with { Version = current.Version + 1, Lifecycle = RegistryLifecycle.Draft, CreatedAt = now, UpdatedAt = now, UpdatedBy = user.UserId };
        await repository.AddAsync(value, ct); await Audit("document_type.draft_created", value, ct); return value;
    }
    public async Task<DocumentTypeDefinition> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var current = await repository.GetAsync(user.ClientId, id, null, ct) ?? throw new KeyNotFoundException();
        if (current.Lifecycle != RegistryLifecycle.Published) throw new RegistryConflictException("Only a published version can be archived.");
        var value = current with { Lifecycle = RegistryLifecycle.Archived, UpdatedAt = DateTimeOffset.UtcNow, UpdatedBy = user.UserId };
        await repository.ReplaceAsync(value, ct); await Audit("document_type.archived", value, ct); return value;
    }
    private DocumentTypeDefinition Build(Guid id, int version, RegistryLifecycle lifecycle, SaveDocumentTypeRequest x, DateTimeOffset created, DateTimeOffset updated) => new(id, user.ClientId, x.TechnicalName.Trim().ToLowerInvariant(), version, lifecycle, x.DisplayName.Trim(), x.Description?.Trim(), x.ClassificationInstructions.Trim(), x.ExtractionSchema, x.Prompt.Trim(), x.ValidationRules, x.ReviewPolicy, x.ModelConfiguration, x.EvaluationDatasetReference, x.OutputMapping, created, updated, user.UserId);
    private static void Validate(SaveDocumentTypeRequest x)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(x.TechnicalName) || !System.Text.RegularExpressions.Regex.IsMatch(x.TechnicalName, "^[a-z][a-z0-9_-]{1,63}$")) errors["technicalName"] = ["Use 2-64 lowercase letters, numbers, underscores or hyphens."];
        if (string.IsNullOrWhiteSpace(x.DisplayName)) errors["displayName"] = ["Display name is required."];
        if (string.IsNullOrWhiteSpace(x.ClassificationInstructions)) errors["classificationInstructions"] = ["Classification instructions are required."];
        if (string.IsNullOrWhiteSpace(x.ExtractionSchema.Reference) || x.ExtractionSchema.Version < 1) errors["extractionSchema"] = ["A versioned extraction schema is required."];
        if (x.ReviewPolicy.AutoApproveThreshold is < 0 or > 1 || x.ReviewPolicy.ClassificationThreshold is < 0 or > 1 || x.ReviewPolicy.DefaultFieldThreshold is < 0 or > 1 || x.ReviewPolicy.FieldThresholds.Values.Any(v => v is < 0 or > 1)) errors["reviewPolicy"] = ["Confidence thresholds must be between 0 and 1."];
        if (errors.Count > 0) throw new RegistryValidationException(errors);
    }
    private ValueTask Audit(string action, DocumentTypeDefinition value, CancellationToken ct) => audit.WriteAsync(new(action, "Documents", "DocumentTypeDefinition", value.Id.ToString(), new Dictionary<string, object?> { ["version"] = value.Version, ["lifecycle"] = value.Lifecycle.ToString() }, "success"), ct);
}
