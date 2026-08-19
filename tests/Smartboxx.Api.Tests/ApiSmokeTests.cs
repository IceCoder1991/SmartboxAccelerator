using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Smartboxx.Api.Tests;

public sealed class ApiSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiSmokeTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_endpoint_is_healthy() =>
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health")).StatusCode);

    [Fact]
    public async Task Module_endpoint_exposes_all_capabilities()
    {
        var modules = await _client.GetStringAsync("/api/modules");
        Assert.All(new[] { "Documents", "Contact Centre", "Software Delivery", "Testing", "API Intelligence", "Value" },
            module => Assert.Contains(module, modules, StringComparison.Ordinal));
    }
}
