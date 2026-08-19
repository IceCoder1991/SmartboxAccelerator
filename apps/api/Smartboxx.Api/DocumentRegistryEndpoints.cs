using Smartboxx.Modules.Documents.Application;
using Smartboxx.Platform.Contracts;

namespace Smartboxx.Api;

public sealed class AuditLogger(ILogger<AuditLogger> logger, ICurrentUser user) : IAuditService
{
    public ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    { logger.LogInformation("Audit {Action} by {UserId} for tenant {TenantId}, record {RecordType}/{RecordId}, result {Result}", auditEvent.Action, user.UserId, user.ClientId, auditEvent.RecordType, auditEvent.RecordId, auditEvent.Result); return ValueTask.CompletedTask; }
}

public static class DocumentRegistryEndpoints
{
    public static IEndpointRouteBuilder MapDocumentRegistry(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/document-types").RequireAuthorization("documents.view");
        group.MapGet("/", async (DocumentTypeRegistry registry, CancellationToken ct) => Results.Ok(new { items = await registry.ListAsync(ct) }));
        group.MapGet("/{id:guid}", async (Guid id, int? version, DocumentTypeRegistry registry, CancellationToken ct) =>
            await registry.GetAsync(id, version, ct) is { } value ? Results.Ok(value) : Results.NotFound()).WithName("GetDocumentType");
        group.MapPost("/", async (SaveDocumentTypeRequest request, DocumentTypeRegistry registry, CancellationToken ct) =>
        { var value = await registry.CreateAsync(request, ct); return Results.CreatedAtRoute("GetDocumentType", new { id = value.Id, version = value.Version }, value); }).RequireAuthorization("documents.configure");
        group.MapPut("/{id:guid}", async (Guid id, SaveDocumentTypeRequest request, DocumentTypeRegistry registry, CancellationToken ct) => Results.Ok(await registry.UpdateAsync(id, request, ct))).RequireAuthorization("documents.configure");
        group.MapPost("/{id:guid}/publish", async (Guid id, DocumentTypeRegistry registry, CancellationToken ct) => Results.Ok(await registry.PublishAsync(id, ct))).RequireAuthorization("documents.configure");
        group.MapPost("/{id:guid}/versions", async (Guid id, DocumentTypeRegistry registry, CancellationToken ct) => Results.Ok(await registry.CloneDraftAsync(id, ct))).RequireAuthorization("documents.configure");
        group.MapPost("/{id:guid}/archive", async (Guid id, DocumentTypeRegistry registry, CancellationToken ct) => Results.Ok(await registry.ArchiveAsync(id, ct))).RequireAuthorization("documents.configure");
        return endpoints;
    }
}
