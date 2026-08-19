using System.Security.Claims;

namespace Smartboxx.Platform.Contracts;

public static class SmartboxxPermissions
{
    public static readonly string[] All =
    [
        "documents.view", "documents.upload", "documents.configure", "documents.review", "documents.admin",
        "contactCentre.view", "contactCentre.ingest", "contactCentre.review", "softwareDelivery.view", "softwareDelivery.ingest",
        "testing.view", "testing.generate", "testing.approve", "testing.execute", "apiGovernance.view", "apiGovernance.ingest", "apiGovernance.admin", "value.view", "value.configure",
        "platform.admin"
    ];
}

public interface ICurrentUser
{
    string UserId { get; }
    string ClientId { get; }
    string DisplayName { get; }
    string CorrelationId { get; }
    IReadOnlyCollection<Claim> Claims { get; }
}

public interface IIdentityProvider
{
    ValueTask<ICurrentUser> GetCurrentAsync(CancellationToken cancellationToken = default);
}

public interface IAuthorisationService
{
    bool HasPermission(string permission);
    IReadOnlySet<string> Permissions { get; }
}

public interface IFeatureService
{
    bool IsEnabled(string flag);
}

/// <summary>Resolves secrets from the deployment's secret store without exposing their values in configuration.</summary>
public interface ISecretProvider
{
    ValueTask<string> GetRequiredAsync(string name, CancellationToken cancellationToken = default);
}

/// <summary>Provides the tenant selected by the authenticated request boundary.</summary>
public interface ITenantContextAccessor
{
    string TenantId { get; }
}

public interface IAuditService
{
    ValueTask WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}

public sealed record AuditEvent(string Action, string Capability, string? RecordType = null, string? RecordId = null,
    IReadOnlyDictionary<string, object?>? SafeValues = null, string? Result = null);

public interface IObjectStorage
{
    Task<ObjectMetadata> PutAsync(string tenantId, string objectName, Stream content, string contentType,
        CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string tenantId, string objectName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string tenantId, string objectName, CancellationToken cancellationToken = default);
}

public sealed record ObjectMetadata(string ObjectName, string ContentType, long Size, string Sha256);

public enum JobState { Scheduled, Running, Succeeded, Failed, DeadLettered, Cancelled }
public sealed record JobEnvelope(Guid Id, string TenantId, string Type, string Payload, string IdempotencyKey,
    string TraceId, JobState State, int Attempts, DateTimeOffset ScheduledAt);

public interface IJobScheduler
{
    ValueTask<Guid> ScheduleAsync(string type, string payload, string idempotencyKey,
        DateTimeOffset? runAt = null, CancellationToken cancellationToken = default);
}

public sealed record AiChatRequest(string Purpose, string PromptVersion, IReadOnlyList<AiMessage> Messages,
    TimeSpan? Timeout = null);
public sealed record AiMessage(string Role, string Content);
public sealed record AiChatResult(string Content, string ModelAlias, int? InputTokens, int? OutputTokens);
public interface IAiClient
{
    Task<AiChatResult> ChatAsync(AiChatRequest request, CancellationToken cancellationToken = default);
}

public sealed record ValueEvent(Guid Id, string TenantId, string Type, int Version,
    IReadOnlyDictionary<string, decimal> Inputs, DateTimeOffset OccurredAt);

public interface IValueEventSink
{
    ValueTask EmitAsync(ValueEvent valueEvent, CancellationToken cancellationToken = default);
    IReadOnlyList<ValueEvent> Read(string tenantId);
}
