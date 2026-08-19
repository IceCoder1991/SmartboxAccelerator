using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Smartboxx.Api.Tests;

public sealed class ApiSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public ApiSmokeTests(WebApplicationFactory<Program> factory) => _factory = factory.WithWebHostBuilder(b => b.UseEnvironment("Development"));

    private HttpClient Client(string tenant = "acme-demo", string roles = "viewer")
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Smartboxx-Tenant", tenant);
        client.DefaultRequestHeaders.Add("X-Dev-Roles", roles);
        return client;
    }

    [Fact] public async Task Health_endpoint_is_healthy() => Assert.Equal(HttpStatusCode.OK, (await Client().GetAsync("/health")).StatusCode);

    [Fact]
    public async Task Bootstrap_is_tenant_scoped_and_contains_only_authorised_navigation()
    {
        var acme = JsonDocument.Parse(await Client().GetStringAsync("/api/platform/bootstrap")).RootElement;
        var northwind = JsonDocument.Parse(await Client("northwind-demo").GetStringAsync("/api/platform/bootstrap")).RootElement;
        Assert.Equal("Acme Demo", acme.GetProperty("clientName").GetString());
        Assert.Equal("Northwind Synthetic", northwind.GetProperty("clientName").GetString());
        Assert.Equal("Documents", acme.GetProperty("navigation")[0].GetProperty("label").GetString());
        Assert.Equal("Contact Centre", northwind.GetProperty("navigation")[0].GetProperty("label").GetString());
        Assert.False(acme.TryGetProperty("storage", out _));
        Assert.False(acme.TryGetProperty("aiRouting", out _));
    }

    [Fact] public async Task Unknown_tenant_is_strictly_rejected() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Client("not-a-client").GetAsync("/api/platform/bootstrap")).StatusCode);

    [Fact]
    public async Task Api_authorisation_does_not_rely_on_hidden_navigation()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(roles: "viewer").GetAsync("/api/platform/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client(roles: "admin").GetAsync("/api/platform/admin")).StatusCode);
    }

    [Fact]
    public async Task Local_authentication_cannot_activate_outside_development()
    {
        await using var production = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Production"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await production.CreateClient().GetAsync("/api/platform/bootstrap")).StatusCode);
    }

    [Fact]
    public void Invalid_configuration_fails_startup_with_actionable_error()
    {
        using var invalid = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Smartboxx:DefaultTenant"] = "missing" })));
        var error = Assert.ThrowsAny<Exception>(() => invalid.CreateClient());
        Assert.Contains("DefaultTenant", error.ToString(), StringComparison.Ordinal);
    }
}
