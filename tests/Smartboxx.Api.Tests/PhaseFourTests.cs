using Smartboxx.Modules.ApiIntelligence;
using Smartboxx.Modules.ContactCentre;
using Smartboxx.Modules.SoftwareDelivery;
using Smartboxx.Modules.Testing;
using Smartboxx.Modules.Value;

namespace Smartboxx.Api.Tests;

public sealed class PhaseFourTests
{
    [Fact]
    public async Task Contact_centre_produces_grounded_quality_evidence()
    {
        var values = new InMemoryValueService();
        var service = new ContactCentreService(new DemoRecordingProvider(), new DemoSpeechToTextProvider(), new DeterministicQualityEvaluator(), values);
        var result = await service.IngestAsync("tenant", new("call", "Alex", "demo://call", DateTimeOffset.UnixEpoch));
        Assert.Equal(100, result.Score);
        Assert.All(result.Evidence, evidence => Assert.True(evidence.Passed));
        Assert.Contains("indicative", result.SentimentCaveat);
    }

    [Fact]
    public async Task Testing_reuses_the_approved_repository_context()
    {
        var values = new InMemoryValueService();
        var repositories = new SoftwareDeliveryService(new SyntheticSourceControlProvider(), new DeterministicCodeAnalysisProvider(), values);
        await repositories.AnalyseAsync("tenant", new("orders", "main", "abc123"));
        var testing = new TestingService(repositories, values);
        var suite = testing.Generate("tenant", "orders", [new("R1", "Update an order", ["The update is persisted"])]);
        Assert.Equal("awaiting-approval", suite.State);
        Assert.True(suite.Tests.Single().Generated);
        Assert.Equal("approved-for-export", testing.Approve("tenant", suite.Id).State);
    }

    [Fact]
    public async Task Api_governance_reports_contract_evidence_and_breaks()
    {
        var service = new ApiIntelligenceService(new InMemoryValueService());
        const string baselineJson = """{"openapi":"3.1.0","paths":{"/orders":{"get":{"responses":{"200":{"description":"ok"}}}}}}""";
        const string candidateJson = """{"openapi":"3.1.0","paths":{}}""";
        var baseline = await service.IngestAsync("tenant", "orders-v1", baselineJson);
        var candidate = await service.IngestAsync("tenant", "orders-v2", candidateJson);
        Assert.Contains(baseline.Findings, finding => finding.RuleId == "security" && finding.JsonPointer.Contains("orders"));
        Assert.Single(ApiIntelligenceService.Compare(baseline, candidate));
    }

    [Fact]
    public async Task Value_report_separates_observed_from_estimated_metrics()
    {
        var values = new InMemoryValueService();
        await values.EmitAsync(new(Guid.NewGuid(), "tenant", "test", 1, new Dictionary<string, decimal>{{"tests", 2}, {"estimatedMinutesSaved", 10}}, DateTimeOffset.UtcNow));
        var report = values.Calculate("tenant");
        Assert.Contains(report.Metrics, metric => metric.Classification == "observed");
        Assert.Contains(report.Metrics, metric => metric.Classification == "estimated");
        Assert.Contains("not realised", report.Disclaimer);
    }
}
