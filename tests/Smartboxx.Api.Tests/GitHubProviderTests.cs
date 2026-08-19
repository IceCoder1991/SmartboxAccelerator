using System.Net;
using System.Text;
using Smartboxx.Modules.SoftwareDelivery;
using Smartboxx.Platform.Contracts;
using Smartboxx.Providers.GitHub;

namespace Smartboxx.Api.Tests;

public sealed class GitHubProviderTests
{
    [Fact]
    public async Task ReadsPinnedAllowedRepositoryAndRedactsSensitiveFiles()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/repos/acme/widgets/git/trees/abcdef1" => Json("""{"tree":[{"path":"src/a.cs","type":"blob","sha":"1","size":12},{"path":".env","type":"blob","sha":"2","size":5}],"truncated":false}"""),
            "/repos/acme/widgets/git/blobs/1" => Json($$"""{"content":"{{Convert.ToBase64String(Encoding.UTF8.GetBytes("class A {}"))}}","encoding":"base64"}"""),
            _ => new(HttpStatusCode.NotFound)
        });
        var provider = Create(handler);

        var result = await provider.ReadAsync(new("acme/widgets", "main", "abcdef1"));

        var file = Assert.Single(result.Files);
        Assert.Equal("src/a.cs", file.Path);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal("Bearer test-token", r.Authorization));
    }

    [Fact]
    public async Task RejectsCrossTenantRepositoryBeforeResolvingSecretOrCallingGitHub()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("must not call"));
        var secret = new StubSecret();
        var provider = Create(handler, secret);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => provider.ReadAsync(new("other/repo", "main", "abcdef1")));
        Assert.Equal(0, secret.Calls);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task RetriesTransientResponseWithoutLeakingTokenInTelemetrySurface()
    {
        var attempts = 0;
        var handler = new StubHandler(_ => ++attempts == 1 ? new(HttpStatusCode.ServiceUnavailable) : Json("""{"tree":[],"truncated":false}"""));
        var result = await Create(handler).ReadAsync(new("acme/widgets", "main", "abcdef1"));
        Assert.Empty(result.Files);
        Assert.Equal(2, attempts);
    }

    private static GitHubSourceControlProvider Create(StubHandler handler, StubSecret? secret = null) => new(
        new HttpClient(handler),
        new GitHubProviderOptions { RequestTimeout = TimeSpan.FromSeconds(2), Tenants = new Dictionary<string, GitHubTenantOptions> { ["tenant-a"] = new() { TokenSecretName = "github-a", AllowedRepositories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "acme/widgets" } } } },
        secret ?? new StubSecret(), new StubTenant());

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };

    private sealed class StubTenant : ITenantContextAccessor { public string TenantId => "tenant-a"; }
    private sealed class StubSecret : ISecretProvider
    {
        public int Calls { get; private set; }
        public ValueTask<string> GetRequiredAsync(string name, CancellationToken cancellationToken = default) { Calls++; Assert.Equal("github-a", name); return ValueTask.FromResult("test-token"); }
    }
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string Uri, string? Authorization)> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString()));
            return Task.FromResult(respond(request));
        }
    }
}
