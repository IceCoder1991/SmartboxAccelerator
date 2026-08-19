namespace Smartboxx.Providers.GitHub;

public sealed class GitHubProviderOptions
{
    public Uri ApiBaseAddress { get; init; } = new("https://api.github.com/");
    public int MaxRetries { get; init; } = 3;
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public IReadOnlyDictionary<string, GitHubTenantOptions> Tenants { get; init; } = new Dictionary<string, GitHubTenantOptions>();
}

public sealed class GitHubTenantOptions
{
    public required string TokenSecretName { get; init; }
    public IReadOnlySet<string> AllowedRepositories { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
