using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Smartboxx.Modules.SoftwareDelivery;
using Smartboxx.Platform.Contracts;

namespace Smartboxx.Providers.GitHub;

public sealed class GitHubSourceControlProvider(
    HttpClient http, GitHubProviderOptions options, ISecretProvider secrets, ITenantContextAccessor tenant) : ISourceControlProvider
{
    private static readonly ActivitySource Activities = new("Smartboxx.Providers.GitHub");
    private static readonly Meter Meter = new("Smartboxx.Providers.GitHub");
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("smartboxx.github.requests");
    private static readonly Regex SensitivePath = new("(^|/)(\\.env|id_rsa|id_ed25519|.*\\.(pfx|p12|key))$|secret|credential", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SensitiveContent = new("(?im)^\\s*(password|secret|api[_-]?key|private[_-]?key|token)\\s*[:=]", RegexOptions.Compiled);

    public async Task<RepositorySnapshot> ReadAsync(RepositoryRequest request, CancellationToken cancellationToken = default)
    {
        var config = GetTenant();
        var repository = NormalizeRepository(request.RepositoryId);
        if (!config.AllowedRepositories.Contains(repository)) throw new UnauthorizedAccessException("Repository is not allowed for this tenant.");
        if (string.IsNullOrWhiteSpace(request.Commit)) throw new ArgumentException("A pinned commit SHA is required.", nameof(request));
        if (!request.Commit.All(Uri.IsHexDigit) || request.Commit.Length < 7) throw new ArgumentException("Commit must be a hexadecimal SHA.", nameof(request));

        using var activity = Activities.StartActivity("github.read_snapshot", ActivityKind.Client);
        activity?.SetTag("tenant.id", TenantTag(tenant.TenantId));
        activity?.SetTag("repository", repository);
        activity?.SetTag("commit", request.Commit);
        var token = await secrets.GetRequiredAsync(config.TokenSecretName, cancellationToken);
        var tree = await GetJsonAsync<TreeResponse>($"repos/{repository}/git/trees/{request.Commit}?recursive=1", token, cancellationToken);
        if (tree.Truncated) throw new InvalidOperationException("GitHub truncated the repository tree; reduce repository scope before ingestion.");

        var blobs = tree.Tree.Where(x => x.Type == "blob" && !SensitivePath.IsMatch(x.Path)).ToArray();
        if (blobs.Length > request.MaxFiles) throw new InvalidOperationException("Repository file limit exceeded.");
        if (blobs.Sum(x => x.Size) > request.MaxBytes) throw new InvalidOperationException("Repository size limit exceeded.");

        var files = new List<SourceFile>(blobs.Length);
        long bytes = 0;
        foreach (var item in blobs)
        {
            var blob = await GetJsonAsync<BlobResponse>($"repos/{repository}/git/blobs/{item.Sha}", token, cancellationToken);
            if (!string.Equals(blob.Encoding, "base64", StringComparison.OrdinalIgnoreCase)) continue;
            byte[] raw;
            try { raw = Convert.FromBase64String(blob.Content.Replace("\n", "", StringComparison.Ordinal)); }
            catch (FormatException) { continue; }
            bytes += raw.LongLength;
            if (bytes > request.MaxBytes) throw new InvalidOperationException("Repository size limit exceeded.");
            var content = Encoding.UTF8.GetString(raw);
            if (!content.Contains('\0') && !SensitiveContent.IsMatch(content)) files.Add(new SourceFile(item.Path, content));
        }
        return new RepositorySnapshot(repository, request.Branch, request.Commit, files);
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var config = GetTenant();
        var token = await secrets.GetRequiredAsync(config.TokenSecretName, cancellationToken);
        using var response = await SendAsync("rate_limit", token, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private GitHubTenantOptions GetTenant() => options.Tenants.TryGetValue(tenant.TenantId, out var value)
        ? value : throw new UnauthorizedAccessException("GitHub is not configured for this tenant.");

    private async Task<T> GetJsonAsync<T>(string path, string token, CancellationToken ct)
    {
        using var response = await SendAsync(path, token, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new KeyNotFoundException("The requested GitHub resource was not found.");
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web), ct)
            ?? throw new InvalidOperationException("GitHub returned an empty response.");
    }

    private async Task<HttpResponseMessage> SendAsync(string path, string token, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(options.ApiBaseAddress, path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd("Smartboxx/1.0");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            Requests.Add(1, new KeyValuePair<string, object?>("attempt", attempt + 1));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(options.RequestTimeout);
            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            }
            catch (Exception exception) when (attempt < options.MaxRetries &&
                (exception is HttpRequestException || (exception is OperationCanceledException && !ct.IsCancellationRequested)))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)), ct);
                continue;
            }
            if (!ShouldRetry(response, attempt)) return response;
            var delay = RetryDelay(response, attempt);
            response.Dispose();
            await Task.Delay(delay, ct);
        }
    }

    private bool ShouldRetry(HttpResponseMessage response, int attempt) => attempt < options.MaxRetries &&
        (response.StatusCode == HttpStatusCode.RequestTimeout || (int)response.StatusCode == 429 || (int)response.StatusCode >= 500 ||
         (response.StatusCode == HttpStatusCode.Forbidden && response.Headers.TryGetValues("X-RateLimit-Remaining", out var values) && values.Contains("0")));

    private static TimeSpan RetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta) return Min(delta, TimeSpan.FromSeconds(60));
        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var values) && long.TryParse(values.FirstOrDefault(), out var epoch))
            return Min(TimeSpan.FromSeconds(Math.Max(1, epoch - DateTimeOffset.UtcNow.ToUnixTimeSeconds())), TimeSpan.FromSeconds(60));
        return TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt));
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;
    private static string NormalizeRepository(string value)
    {
        var normalized = value.Trim().Trim('/');
        if (normalized.Split('/').Length != 2 || normalized.Any(c => !(char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or '/')))
            throw new ArgumentException("RepositoryId must be in owner/repository form.");
        return normalized;
    }
    private static string TenantTag(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12];

    private sealed record TreeResponse(IReadOnlyList<TreeItem> Tree, bool Truncated);
    private sealed record TreeItem(string Path, string Type, string Sha, long Size);
    private sealed record BlobResponse(string Content, string Encoding);
}
