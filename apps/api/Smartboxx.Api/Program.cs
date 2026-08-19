using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Smartboxx.Api;
using Smartboxx.Platform.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<PlatformOptions>()
    .Bind(builder.Configuration.GetSection(PlatformOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<PlatformOptions>, PlatformOptionsValidator>();
builder.Services.AddHealthChecks();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantAccessor>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IIdentityProvider, HttpIdentityProvider>();
builder.Services.AddScoped<IAuthorisationService, AuthorisationService>();
builder.Services.AddScoped<IFeatureService, FeatureService>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddAuthentication("Smartboxx")
    .AddScheme<AuthenticationSchemeOptions, LocalDevelopmentAuthenticationHandler>("Smartboxx", _ => { });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("documents.view", policy => policy.AddRequirements(new PermissionRequirement("documents.view")))
    .AddPolicy("platform.admin", policy => policy.AddRequirements(new PermissionRequirement("platform.admin")));

var app = builder.Build();
app.UseExceptionHandler(exception => exception.Run(async context =>
{
    context.Response.StatusCode = 500;
    await context.Response.WriteAsJsonAsync(new { code = "unexpected_error", traceId = context.TraceIdentifier });
}));
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseMiddleware<TenantMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { name = "Smartboxx API", status = "ready" }));
app.MapHealthChecks("/health");
app.MapGet("/api/platform/bootstrap", (TenantAccessor tenant, ICurrentUser user,
    IAuthorisationService authorization, IFeatureService features) =>
{
    var client = tenant.Current!.Client;
    var navigation = client.Navigation.Where(item => features.IsEnabled(item.Feature) && authorization.HasPermission(item.Permission));
    return Results.Ok(new
    {
        clientName = client.ClientName,
        productName = client.ProductName,
        terminology = client.Terminology,
        navigation,
        featureFlags = client.Features,
        themeTokens = client.Branding.Theme,
        logo = client.Branding.Logo,
        favicon = client.Branding.Favicon,
        currentUser = new { id = user.UserId, displayName = user.DisplayName, permissions = authorization.Permissions }
    });
}).RequireAuthorization();

app.MapGet("/api/documents", () => Results.Ok(new { items = Array.Empty<object>() }))
    .RequireAuthorization("documents.view");
app.MapGet("/api/platform/admin", () => Results.Ok(new { status = "authorised" }))
    .RequireAuthorization("platform.admin");
app.MapGet("/api/modules", (TenantAccessor tenant) => Results.Ok(new ModuleDescriptor[]
{
    Smartboxx.Modules.Documents.Module.Descriptor, Smartboxx.Modules.ContactCentre.Module.Descriptor,
    Smartboxx.Modules.SoftwareDelivery.Module.Descriptor, Smartboxx.Modules.Testing.Module.Descriptor,
    Smartboxx.Modules.ApiIntelligence.Module.Descriptor, Smartboxx.Modules.Value.Module.Descriptor,
}.Where(module => tenant.Current!.Client.Features.GetValueOrDefault(module.Id, false))));

app.Run();
public partial class Program;
