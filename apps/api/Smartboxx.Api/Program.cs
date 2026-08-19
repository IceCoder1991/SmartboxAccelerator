using Smartboxx.Platform.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { name = "Smartboxx API", status = "ready" }));
app.MapHealthChecks("/health");
app.MapGet("/api/modules", () => Results.Ok(new ModuleDescriptor[]
{
    Smartboxx.Modules.Documents.Module.Descriptor,
    Smartboxx.Modules.ContactCentre.Module.Descriptor,
    Smartboxx.Modules.SoftwareDelivery.Module.Descriptor,
    Smartboxx.Modules.Testing.Module.Descriptor,
    Smartboxx.Modules.ApiIntelligence.Module.Descriptor,
    Smartboxx.Modules.Value.Module.Descriptor,
}));

app.Run();

public partial class Program;
