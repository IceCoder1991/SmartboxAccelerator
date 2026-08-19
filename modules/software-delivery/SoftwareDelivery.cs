using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Smartboxx.Platform.Contracts;

namespace Smartboxx.Modules.SoftwareDelivery;
public interface ISourceControlProvider { Task<RepositorySnapshot> ReadAsync(RepositoryRequest request, CancellationToken cancellationToken = default); }
public interface IWorkItemProvider { Task<IReadOnlyList<WorkItem>> GetAsync(string repositoryId, CancellationToken cancellationToken = default); }
public interface ICodeAnalysisProvider { RepositoryAnalysis Analyse(RepositorySnapshot snapshot); }
public sealed record RepositoryRequest(string RepositoryId, string Branch, string Commit, int MaxFiles = 500, long MaxBytes = 5_000_000);
public sealed record SourceFile(string Path, string Content);
public sealed record RepositorySnapshot(string Id, string Branch, string Commit, IReadOnlyList<SourceFile> Files);
public sealed record WorkItem(string Id, string Title);
public sealed record Citation(string Path, int StartLine, int EndLine, string? Symbol = null);
public sealed record RepositoryAnalysis(string Architecture, IReadOnlyList<string> Inventory, IReadOnlyList<string> SuggestedIssues, IReadOnlyList<Citation> Citations);

public sealed class SyntheticSourceControlProvider : ISourceControlProvider
{
    public Task<RepositorySnapshot> ReadAsync(RepositoryRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new RepositorySnapshot(request.RepositoryId, request.Branch, request.Commit,
        [new("src/Orders.cs", "namespace Demo;\npublic sealed class Orders { public void Create() { } }"), new("README.md", "# Demonstration repository")]));
}
public sealed class LocalRepositoryProvider(string allowedRoot) : ISourceControlProvider
{
    private static readonly string[] Excluded = [".git", "node_modules", "bin", "obj"];
    private static readonly Regex Sensitive = new("(?i)(password|secret|api[_-]?key|email)", RegexOptions.Compiled);
    public async Task<RepositorySnapshot> ReadAsync(RepositoryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Commit)) throw new ArgumentException("A pinned commit is required.");
        var root = Path.GetFullPath(Path.Combine(allowedRoot, request.RepositoryId));
        if (!root.StartsWith(Path.GetFullPath(allowedRoot), StringComparison.Ordinal) || !Directory.Exists(root)) throw new ArgumentException("Repository is outside the permitted root.");
        var files = new List<SourceFile>(); long bytes = 0;
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(p => !Excluded.Any(e => p.Split(Path.DirectorySeparatorChar).Contains(e))).Take(request.MaxFiles + 1))
        {
            if (files.Count == request.MaxFiles) throw new InvalidOperationException("Repository file limit exceeded.");
            var info = new FileInfo(path); bytes += info.Length; if (bytes > request.MaxBytes) throw new InvalidOperationException("Repository size limit exceeded.");
            var content = await File.ReadAllTextAsync(path, cancellationToken); if (Sensitive.IsMatch(content) || Sensitive.IsMatch(path)) continue;
            files.Add(new(Path.GetRelativePath(root, path), content));
        }
        return new(request.RepositoryId, request.Branch, request.Commit, files);
    }
}
public sealed class DeterministicCodeAnalysisProvider : ICodeAnalysisProvider
{
    public RepositoryAnalysis Analyse(RepositorySnapshot snapshot)
    {
        var inventory = snapshot.Files.Select(f => f.Path).Order().ToArray();
        var citations = snapshot.Files.Select(f => new Citation(f.Path, 1, Math.Max(1, f.Content.Count(c => c == '\n') + 1))).ToArray();
        return new($"Pinned snapshot {snapshot.Commit} contains {inventory.Length} analysable files.", inventory, ["Add tests for public application services.", "Document external dependencies."], citations);
    }
}
public interface IRepositoryContextReader { RepositoryContext? Get(string tenantId, string snapshotId); }
public sealed record RepositoryContext(string SnapshotId, string Commit, RepositoryAnalysis Analysis);
public sealed class SoftwareDeliveryService(ISourceControlProvider source, ICodeAnalysisProvider analysis, IValueEventSink values) : IRepositoryContextReader
{
    private readonly ConcurrentDictionary<string, RepositoryContext> contexts = new();
    public async Task<RepositoryContext> AnalyseAsync(string tenantId, RepositoryRequest request, CancellationToken ct = default)
    { var snapshot = await source.ReadAsync(request, ct); var context = new RepositoryContext(snapshot.Id, snapshot.Commit, analysis.Analyse(snapshot)); contexts[$"{tenantId}:{snapshot.Id}"] = context; await values.EmitAsync(new(Guid.NewGuid(), tenantId, "software-delivery.snapshot-analysed", 1, new Dictionary<string, decimal>{{"files", snapshot.Files.Count},{"estimatedMinutesSaved", snapshot.Files.Count * 2}}, DateTimeOffset.UtcNow), ct); return context; }
    public RepositoryContext? Get(string tenantId, string snapshotId) => contexts.GetValueOrDefault($"{tenantId}:{snapshotId}");
}
