using Smartboxx.Modules.SoftwareDelivery;
using Smartboxx.Platform.Contracts;
using Smartboxx.Providers.GitHub;

namespace Smartboxx.Api.Tests;

public sealed class GitHubProviderIntegrationTests
{
    [Fact]
    public async Task ReadsExplicitlyConfiguredLiveRepository()
    {
        if (Environment.GetEnvironmentVariable("SMARTBOXX_GITHUB_INTEGRATION") != "1") return;
        var token = Require("SMARTBOXX_GITHUB_TOKEN");
        var repository = Require("SMARTBOXX_GITHUB_REPOSITORY");
        var commit = Require("SMARTBOXX_GITHUB_COMMIT");
        var provider = new GitHubSourceControlProvider(new HttpClient(), new GitHubProviderOptions
        {
            Tenants = new Dictionary<string, GitHubTenantOptions> { ["integration"] = new() { TokenSecretName = "integration-token", AllowedRepositories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { repository } } }
        }, new ExplicitSecret(token), new ExplicitTenant());
        var snapshot = await provider.ReadAsync(new RepositoryRequest(repository, "integration", commit, 100, 1_000_000));
        Assert.Equal(commit, snapshot.Commit);
    }

    private static string Require(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"{name} is required when live integration testing is enabled.");
    private sealed class ExplicitTenant : ITenantContextAccessor { public string TenantId => "integration"; }
    private sealed class ExplicitSecret(string value) : ISecretProvider { public ValueTask<string> GetRequiredAsync(string name, CancellationToken cancellationToken = default) => ValueTask.FromResult(value); }
}
