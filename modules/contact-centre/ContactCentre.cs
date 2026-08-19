using System.Collections.Concurrent;
using Smartboxx.Platform.Contracts;

namespace Smartboxx.Modules.ContactCentre;

public interface ICallProvider { IAsyncEnumerable<CallSource> GetCallsAsync(CancellationToken cancellationToken = default); }
public interface ICallRecordingProvider { Task<Stream> OpenAsync(string recordingReference, CancellationToken cancellationToken = default); }
public interface ISpeechToTextProvider { Task<Transcript> TranscribeAsync(Stream audio, CancellationToken cancellationToken = default); }
public interface IQualityEvaluator { QualityResult Evaluate(Transcript transcript, IReadOnlyList<QualityRule> rules); }
public sealed record CallSource(string ExternalId, string Agent, string RecordingReference, DateTimeOffset StartedAt);
public sealed record Transcript(string Text, IReadOnlyList<TranscriptSegment> Segments);
public sealed record TranscriptSegment(TimeSpan Start, TimeSpan End, string Speaker, string Text);
public sealed record QualityRule(string Id, string Description, string RequiredPhrase, int Weight, int Version = 1);
public sealed record QualityEvidence(string RuleId, bool Passed, string Evidence, TimeSpan? At);
public sealed record QualityResult(int Score, IReadOnlyList<QualityEvidence> Evidence);
public sealed record CallInsight(Guid Id, string ExternalId, string Agent, string Transcript, string Summary,
    string Intent, string Sentiment, string SentimentCaveat, int Score, IReadOnlyList<QualityEvidence> Evidence,
    string ReviewState, DateTimeOffset StartedAt);

public sealed class DemoCallProvider : ICallProvider
{
    public async IAsyncEnumerable<CallSource> GetCallsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    { await Task.Yield(); cancellationToken.ThrowIfCancellationRequested(); yield return new("demo-001", "Alex", "demo://call-001", DateTimeOffset.Parse("2025-01-02T10:00:00Z")); }
}
public sealed class DemoRecordingProvider : ICallRecordingProvider
{ public Task<Stream> OpenAsync(string recordingReference, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream("demo"u8.ToArray())); }
public sealed class DemoSpeechToTextProvider : ISpeechToTextProvider
{
    public Task<Transcript> TranscribeAsync(Stream audio, CancellationToken cancellationToken = default) => Task.FromResult(new Transcript(
        "Hello, how can I help? I need to change my address. I can help with that. Thank you.",
        [new(TimeSpan.Zero, TimeSpan.FromSeconds(2), "agent", "Hello, how can I help?"), new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), "customer", "I need to change my address."), new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(8), "agent", "I can help with that. Thank you.")]));
}
public sealed class DeterministicQualityEvaluator : IQualityEvaluator
{
    public QualityResult Evaluate(Transcript transcript, IReadOnlyList<QualityRule> rules)
    {
        var evidence = rules.Select(rule => { var segment = transcript.Segments.FirstOrDefault(s => s.Text.Contains(rule.RequiredPhrase, StringComparison.OrdinalIgnoreCase)); return new QualityEvidence(rule.Id, segment is not null, segment?.Text ?? "Phrase not found", segment?.Start); }).ToArray();
        var possible = Math.Max(1, rules.Sum(r => r.Weight));
        var score = rules.Zip(evidence).Where(pair => pair.Second.Passed).Sum(pair => pair.First.Weight) * 100 / possible;
        return new(score, evidence);
    }
}

public sealed class ContactCentreService(ICallRecordingProvider recordings, ISpeechToTextProvider speech, IQualityEvaluator evaluator, IValueEventSink values)
{
    private readonly ConcurrentDictionary<Guid, CallInsight> insights = new();
    public async Task<CallInsight> IngestAsync(string tenantId, CallSource call, IReadOnlyList<QualityRule>? rules = null, CancellationToken cancellationToken = default)
    {
        await using var audio = await recordings.OpenAsync(call.RecordingReference, cancellationToken);
        var transcript = await speech.TranscribeAsync(audio, cancellationToken);
        var result = evaluator.Evaluate(transcript, rules ?? [new("greeting", "Agent offers help", "how can I help", 50), new("courtesy", "Agent closes courteously", "thank you", 50)]);
        var intent = transcript.Text.Contains("address", StringComparison.OrdinalIgnoreCase) ? "account.update-address" : "unknown";
        var sentiment = transcript.Text.Contains("thank", StringComparison.OrdinalIgnoreCase) ? "positive" : "neutral";
        var insight = new CallInsight(Guid.NewGuid(), call.ExternalId, call.Agent, transcript.Text, "Customer requested an address change.", intent, sentiment,
            "Automated sentiment is indicative only and must not be used for employment decisions.", result.Score, result.Evidence, "pending", call.StartedAt);
        insights[insight.Id] = insight;
        await values.EmitAsync(new(Guid.NewGuid(), tenantId, "contact-centre.call-processed", 1, new Dictionary<string, decimal> { ["calls"] = 1, ["estimatedMinutesSaved"] = 6 }, DateTimeOffset.UtcNow), cancellationToken);
        return insight;
    }
    public IReadOnlyList<CallInsight> Search(string query) => insights.Values.Where(x => string.IsNullOrWhiteSpace(query) || (x.Transcript + x.Summary + x.Agent).Contains(query, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.StartedAt).ToArray();
    public CallInsight Review(Guid id, string state) { var item = insights[id]; var reviewed = item with { ReviewState = state }; insights[id] = reviewed; return reviewed; }
}
