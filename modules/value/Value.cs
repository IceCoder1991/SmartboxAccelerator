using System.Collections.Concurrent;
using Smartboxx.Platform.Contracts;

namespace Smartboxx.Modules.Value;
public sealed record CalculationModel(int Version, decimal HourlyCost, decimal AiCostPerEvent, DateTimeOffset EffectiveFrom);
public sealed record ValueMetric(string Name, decimal Value, string Unit, string Classification);
public sealed record ValueReport(string TenantId, int ModelVersion, IReadOnlyList<ValueMetric> Metrics, IReadOnlyList<string> DataQualityWarnings, string Disclaimer);
public sealed class InMemoryValueService : IValueEventSink
{
    private readonly ConcurrentQueue<ValueEvent> events = new();
    private readonly ConcurrentQueue<CalculationModel> models = new();
    public InMemoryValueService() => models.Enqueue(new(1, 30m, .02m, DateTimeOffset.UnixEpoch));
    public ValueTask EmitAsync(ValueEvent valueEvent, CancellationToken cancellationToken = default) { events.Enqueue(valueEvent); return ValueTask.CompletedTask; }
    public IReadOnlyList<ValueEvent> Read(string tenantId) => events.Where(e => e.TenantId == tenantId).OrderBy(e => e.OccurredAt).ToArray();
    public void Configure(CalculationModel model) => models.Enqueue(model);
    public ValueReport Calculate(string tenantId, int? version = null)
    {
        var model = version is null ? models.MaxBy(m => m.Version)! : models.Single(m => m.Version == version);
        var source = Read(tenantId); var minutes = source.Sum(e => e.Inputs.GetValueOrDefault("estimatedMinutesSaved")); var aiCost = source.Count * model.AiCostPerEvent;
        var observed = source.Sum(e => e.Inputs.GetValueOrDefault("calls") + e.Inputs.GetValueOrDefault("files") + e.Inputs.GetValueOrDefault("tests") + e.Inputs.GetValueOrDefault("operations"));
        return new(tenantId, model.Version, [new("activity", observed, "items", "observed"), new("time saved", minutes, "minutes", "estimated"), new("cost saved", minutes / 60 * model.HourlyCost, "currency", "estimated"), new("AI cost", aiCost, "currency", "estimated")], source.Count == 0 ? ["No observed events are available for this scenario."] : [], "Estimates are scenario projections, not realised financial savings.");
    }
}
