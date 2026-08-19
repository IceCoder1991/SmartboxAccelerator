using System.Collections.Concurrent;
using System.Text.Json;
using Smartboxx.Platform.Contracts;

namespace Smartboxx.Modules.ApiIntelligence;
public sealed record GovernanceRule(string Id, int Version, string Description, string JsonProperty, string Severity);
public sealed record ApiEvidence(string RuleId, string JsonPointer, string Message, string Severity, string ReviewState = "open");
public sealed record ApiContract(Guid Id, string Name, string Version, string RawSpecification, IReadOnlyList<string> Operations, IReadOnlyList<ApiEvidence> Findings);
public sealed record BreakingChange(string JsonPointer, string Description);
public sealed class ApiIntelligenceService(IValueEventSink values)
{
    private readonly ConcurrentDictionary<Guid, ApiContract> contracts = new();
    public async Task<ApiContract> IngestAsync(string tenantId, string name, string specification, IReadOnlyList<GovernanceRule>? rules = null, CancellationToken ct = default)
    {
        using var document = JsonDocument.Parse(specification); var root = document.RootElement;
        if (!root.TryGetProperty("openapi", out var version)) throw new ArgumentException("A valid OpenAPI version is required.");
        var paths = root.TryGetProperty("paths", out var pathNode) ? pathNode.EnumerateObject().ToArray() : [];
        var operations = paths.SelectMany(p => p.Value.EnumerateObject().Where(m => new[]{"get","put","post","delete","patch"}.Contains(m.Name)).Select(m => $"{m.Name.ToUpperInvariant()} {p.Name}")).ToArray();
        var findings = new List<ApiEvidence>();
        foreach (var path in paths)
        foreach (var operation in path.Value.EnumerateObject().Where(m => new[]{"get","put","post","delete","patch"}.Contains(m.Name)))
        { if (!operation.Value.TryGetProperty("operationId", out _)) findings.Add(new("operation-id", $"/paths/{Escape(path.Name)}/{operation.Name}", "Operation should define operationId.", "warning")); if (!operation.Value.TryGetProperty("security", out _) && !root.TryGetProperty("security", out _)) findings.Add(new("security", $"/paths/{Escape(path.Name)}/{operation.Name}", "No security requirement is declared in the supplied contract.", "high")); }
        foreach (var rule in rules ?? []) if (!root.TryGetProperty(rule.JsonProperty, out _)) findings.Add(new(rule.Id, "/", rule.Description, rule.Severity));
        var contract = new ApiContract(Guid.NewGuid(), name, version.GetString()!, specification, operations, findings); contracts[contract.Id] = contract;
        await values.EmitAsync(new(Guid.NewGuid(), tenantId, "api-intelligence.contract-analysed", 1, new Dictionary<string, decimal>{{"operations", operations.Length},{"findings", findings.Count}}, DateTimeOffset.UtcNow), ct); return contract;
    }
    public IReadOnlyList<ApiContract> Search(string query) => contracts.Values.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || c.Operations.Any(o => o.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
    public ApiContract Review(Guid id, string ruleId, bool falsePositive) { var c = contracts[id]; c = c with { Findings = c.Findings.Select(f => f.RuleId == ruleId ? f with { ReviewState = falsePositive ? "false-positive" : "confirmed" } : f).ToArray() }; contracts[id] = c; return c; }
    public static IReadOnlyList<BreakingChange> Compare(ApiContract baseline, ApiContract candidate) => baseline.Operations.Except(candidate.Operations).Select(o => new BreakingChange("/paths", $"Removed operation {o}")).ToArray();
    private static string Escape(string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}
