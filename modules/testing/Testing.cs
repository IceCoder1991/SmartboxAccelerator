using System.Collections.Concurrent;
using Smartboxx.Modules.SoftwareDelivery;
using Smartboxx.Platform.Contracts;

namespace Smartboxx.Modules.Testing;
public sealed record Requirement(string Id, string Text, IReadOnlyList<string> AcceptanceCriteria);
public sealed record GeneratedTest(string Id, string RequirementId, string Title, IReadOnlyList<string> Steps, string Expected, bool Generated = true);
public sealed record TestSuite(Guid Id, string SnapshotId, IReadOnlyList<GeneratedTest> Tests, IReadOnlyDictionary<string, IReadOnlyList<string>> Traceability, string State);
public sealed record TestResult(string TestId, bool Passed, string Details);
public sealed record TestResultSummary(int Passed, int Failed, IReadOnlyList<string> Defects);

public sealed class TestingService(IRepositoryContextReader repositories, IValueEventSink values)
{
    private readonly ConcurrentDictionary<Guid, TestSuite> suites = new();
    public TestSuite Generate(string tenantId, string snapshotId, IReadOnlyList<Requirement> requirements)
    {
        _ = repositories.Get(tenantId, snapshotId) ?? throw new KeyNotFoundException("Approved shared repository context was not found; re-ingestion is not performed.");
        var tests = requirements.SelectMany(r => r.AcceptanceCriteria.Select((criterion, i) => new GeneratedTest($"{r.Id}-{i + 1}", r.Id, criterion, ["Arrange approved test data", "Perform the described action", "Observe the response"], criterion))).ToArray();
        var suite = new TestSuite(Guid.NewGuid(), snapshotId, tests, requirements.ToDictionary(r => r.Id, r => (IReadOnlyList<string>)tests.Where(t => t.RequirementId == r.Id).Select(t => t.Id).ToArray()), "awaiting-approval");
        suites[suite.Id] = suite; return suite;
    }
    public TestSuite Approve(string tenantId, Guid id)
    { var suite = suites[id] with { State = "approved-for-export" }; suites[id] = suite; values.EmitAsync(new(Guid.NewGuid(), tenantId, "testing.tests-approved", 1, new Dictionary<string, decimal>{{"tests", suite.Tests.Count},{"estimatedMinutesSaved", suite.Tests.Count * 5}}, DateTimeOffset.UtcNow)).GetAwaiter().GetResult(); return suite; }
    public TestResultSummary ImportResults(IReadOnlyList<TestResult> results) => new(results.Count(r => r.Passed), results.Count(r => !r.Passed), results.Where(r => !r.Passed).Select(r => $"{r.TestId}: {r.Details}").ToArray());
    public string GenerateApiTest(string openApiJson) => openApiJson.Contains("openapi", StringComparison.OrdinalIgnoreCase) ? "GENERATED - validate status code and response schema; approval required before execution." : throw new ArgumentException("An OpenAPI contract is required.");
}
