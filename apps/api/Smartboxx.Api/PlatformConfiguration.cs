using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Smartboxx.Api;

public sealed class PlatformOptions
{
    public const string SectionName = "Smartboxx";
    [Required] public string DefaultTenant { get; init; } = "";
    [MinLength(1)] public Dictionary<string, ClientOptions> Clients { get; init; } = [];
    public string IsolationModel { get; init; } = "shared-store-tenant-key";
}

public sealed class ClientOptions
{
    [Required, RegularExpression("^[a-z0-9-]+$")] public string Id { get; init; } = "";
    [Required] public string ClientName { get; init; } = "";
    [Required] public string ProductName { get; init; } = "";
    public Dictionary<string, string> Terminology { get; init; } = [];
    public Dictionary<string, bool> Features { get; init; } = [];
    public Dictionary<string, string> AiRouting { get; init; } = [];
    [Required] public IdentityOptions Identity { get; init; } = new();
    [Required] public ProviderOptions Storage { get; init; } = new();
    [Required] public ProviderOptions VectorStore { get; init; } = new();
    [Range(1, 3650)] public int RetentionDays { get; init; } = 365;
    [Required] public BrandingOptions Branding { get; init; } = new();
    public Dictionary<string, string[]> Roles { get; init; } = [];
    public NavigationItem[] Navigation { get; init; } = [];
}

public sealed class IdentityOptions
{
    [Required] public string Mode { get; init; } = "Oidc";
    public string? Authority { get; init; }
    public string? Audience { get; init; }
}
public sealed class ProviderOptions { [Required] public string Provider { get; init; } = "FileSystem"; public string? Endpoint { get; init; } }
public sealed class BrandingOptions
{
    [Required] public string Logo { get; init; } = "";
    [Required] public string Favicon { get; init; } = "";
    public Dictionary<string, string> Theme { get; init; } = [];
}
public sealed record NavigationItem(string Label, string Route, string Feature, string Permission);

public sealed class PlatformOptionsValidator : IValidateOptions<PlatformOptions>
{
    public ValidateOptionsResult Validate(string? name, PlatformOptions options)
    {
        var errors = new List<string>();
        if (!options.Clients.ContainsKey(options.DefaultTenant)) errors.Add("Smartboxx:DefaultTenant must identify a configured client.");
        foreach (var (key, client) in options.Clients)
        {
            if (key != client.Id) errors.Add($"Smartboxx:Clients:{key}:Id must equal its configuration key.");
            if (client.Identity.Mode is not ("Local" or "Oidc")) errors.Add($"Client '{key}' Identity:Mode must be Local or Oidc.");
            if (client.Identity.Mode == "Oidc" && string.IsNullOrWhiteSpace(client.Identity.Authority)) errors.Add($"Client '{key}' OIDC Authority is required.");
            foreach (var permission in client.Roles.SelectMany(x => x.Value).Distinct())
                if (!Smartboxx.Platform.Contracts.SmartboxxPermissions.All.Contains(permission)) errors.Add($"Client '{key}' maps unknown permission '{permission}'.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
