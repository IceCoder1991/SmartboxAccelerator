using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Smartboxx.Platform.Contracts;

namespace Smartboxx.Api;

public sealed class TenantContext { public required string Id { get; init; } public required ClientOptions Client { get; init; } }
public sealed class TenantAccessor { public TenantContext? Current { get; set; } }

public sealed class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IOptions<PlatformOptions> options, TenantAccessor accessor)
    {
        var id = context.Request.Headers["X-Smartboxx-Tenant"].FirstOrDefault() ?? options.Value.DefaultTenant;
        if (!options.Value.Clients.TryGetValue(id, out var client))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { code = "unknown_tenant", message = "The requested client is not configured." });
            return;
        }
        accessor.Current = new TenantContext { Id = id, Client = client };
        context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
        await next(context);
    }
}

public sealed class CurrentUser(IHttpContextAccessor http, TenantAccessor tenant) : ICurrentUser
{
    private ClaimsPrincipal Principal => http.HttpContext?.User ?? new();
    public string UserId => Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
    public string ClientId => tenant.Current?.Id ?? throw new InvalidOperationException("Tenant context is unavailable.");
    public string DisplayName => Principal.Identity?.Name ?? "Anonymous";
    public string CorrelationId => http.HttpContext?.TraceIdentifier ?? "";
    public IReadOnlyCollection<Claim> Claims => Principal.Claims.ToArray();
}

public sealed class HttpIdentityProvider(ICurrentUser user) : IIdentityProvider
{ public ValueTask<ICurrentUser> GetCurrentAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(user); }

public sealed class AuthorisationService(ICurrentUser user, TenantAccessor tenant) : IAuthorisationService
{
    public IReadOnlySet<string> Permissions
    {
        get
        {
            var roles = user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value);
            return roles.SelectMany(role => tenant.Current!.Client.Roles.GetValueOrDefault(role) ?? []).ToHashSet(StringComparer.Ordinal);
        }
    }
    public bool HasPermission(string permission) => Permissions.Contains(permission);
}
public sealed class FeatureService(TenantAccessor tenant) : IFeatureService
{ public bool IsEnabled(string flag) => tenant.Current!.Client.Features.GetValueOrDefault(flag, false); }

public sealed class LocalDevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IWebHostEnvironment environment) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!environment.IsDevelopment()) return Task.FromResult(AuthenticateResult.Fail("Local authentication is restricted to Development."));
        var userId = Request.Headers["X-Dev-User"].FirstOrDefault() ?? "developer";
        var roles = (Request.Headers["X-Dev-Roles"].FirstOrDefault() ?? "viewer").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId), new(ClaimTypes.Name, userId) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
    }
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement { public string Permission { get; } = permission; }
public sealed class PermissionHandler(IAuthorisationService authorisation) : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    { if (authorisation.HasPermission(requirement.Permission)) context.Succeed(requirement); return Task.CompletedTask; }
}
