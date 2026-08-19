using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Smartboxx.Api;
using Smartboxx.Platform.Contracts;
using Smartboxx.Modules.Documents.Application;
using Smartboxx.Modules.ContactCentre;
using Smartboxx.Modules.SoftwareDelivery;
using Smartboxx.Modules.Testing;
using Smartboxx.Modules.ApiIntelligence;
using Smartboxx.Modules.Value;

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
builder.Services.AddSingleton<IDocumentTypeRepository, InMemoryDocumentTypeRepository>();
builder.Services.AddScoped<DocumentTypeRegistry>();
builder.Services.AddScoped<IAuditService, AuditLogger>();
builder.Services.AddSingleton<InMemoryValueService>();
builder.Services.AddSingleton<IValueEventSink>(sp => sp.GetRequiredService<InMemoryValueService>());
builder.Services.AddSingleton<ICallProvider, DemoCallProvider>();
builder.Services.AddSingleton<ICallRecordingProvider, DemoRecordingProvider>();
builder.Services.AddSingleton<ISpeechToTextProvider, DemoSpeechToTextProvider>();
builder.Services.AddSingleton<IQualityEvaluator, DeterministicQualityEvaluator>();
builder.Services.AddSingleton<ContactCentreService>();
builder.Services.AddSingleton<ISourceControlProvider, SyntheticSourceControlProvider>();
builder.Services.AddSingleton<ICodeAnalysisProvider, DeterministicCodeAnalysisProvider>();
builder.Services.AddSingleton<SoftwareDeliveryService>();
builder.Services.AddSingleton<IRepositoryContextReader>(sp => sp.GetRequiredService<SoftwareDeliveryService>());
builder.Services.AddSingleton<TestingService>();
builder.Services.AddSingleton<ApiIntelligenceService>();
builder.Services.AddAuthentication("Smartboxx")
    .AddScheme<AuthenticationSchemeOptions, LocalDevelopmentAuthenticationHandler>("Smartboxx", _ => { });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("documents.view", policy => policy.AddRequirements(new PermissionRequirement("documents.view")))
    .AddPolicy("documents.configure", policy => policy.AddRequirements(new PermissionRequirement("documents.configure")))
    .AddPolicy("contactCentre.view", p => p.AddRequirements(new PermissionRequirement("contactCentre.view")))
    .AddPolicy("contactCentre.ingest", p => p.AddRequirements(new PermissionRequirement("contactCentre.ingest")))
    .AddPolicy("softwareDelivery.view", p => p.AddRequirements(new PermissionRequirement("softwareDelivery.view")))
    .AddPolicy("softwareDelivery.ingest", p => p.AddRequirements(new PermissionRequirement("softwareDelivery.ingest")))
    .AddPolicy("testing.generate", p => p.AddRequirements(new PermissionRequirement("testing.generate")))
    .AddPolicy("testing.approve", p => p.AddRequirements(new PermissionRequirement("testing.approve")))
    .AddPolicy("apiGovernance.view", p => p.AddRequirements(new PermissionRequirement("apiGovernance.view")))
    .AddPolicy("apiGovernance.ingest", p => p.AddRequirements(new PermissionRequirement("apiGovernance.ingest")))
    .AddPolicy("value.view", p => p.AddRequirements(new PermissionRequirement("value.view")))
    .AddPolicy("platform.admin", policy => policy.AddRequirements(new PermissionRequirement("platform.admin")));

var app = builder.Build();
app.UseExceptionHandler(exception => exception.Run(async context =>
{
    var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    context.Response.StatusCode = error switch
    {
        RegistryValidationException => StatusCodes.Status400BadRequest,
        RegistryConflictException => StatusCodes.Status409Conflict,
        KeyNotFoundException => StatusCodes.Status404NotFound,
        _ => StatusCodes.Status500InternalServerError
    };
    if (error is RegistryValidationException validation)
        await context.Response.WriteAsJsonAsync(new { code = "validation_failed", errors = validation.Errors, traceId = context.TraceIdentifier });
    else
        await context.Response.WriteAsJsonAsync(new { code = error is RegistryConflictException ? "registry_conflict" : error is KeyNotFoundException ? "not_found" : "unexpected_error", traceId = context.TraceIdentifier });
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

app.MapDocumentRegistry();
app.MapGet("/api/documents", () => Results.Ok(new { items = Array.Empty<object>() }))
    .RequireAuthorization("documents.view");
app.MapGet("/api/platform/admin", () => Results.Ok(new { status = "authorised" }))
    .RequireAuthorization("platform.admin");
app.MapPhaseFourApis();
app.MapGet("/api/modules", (TenantAccessor tenant) => Results.Ok(new ModuleDescriptor[]
{
    Smartboxx.Modules.Documents.Module.Descriptor, Smartboxx.Modules.ContactCentre.Module.Descriptor,
    Smartboxx.Modules.SoftwareDelivery.Module.Descriptor, Smartboxx.Modules.Testing.Module.Descriptor,
    Smartboxx.Modules.ApiIntelligence.Module.Descriptor, Smartboxx.Modules.Value.Module.Descriptor,
}.Where(module => tenant.Current!.Client.Features.GetValueOrDefault(module.Id, false))));

app.Run();
public partial class Program;
