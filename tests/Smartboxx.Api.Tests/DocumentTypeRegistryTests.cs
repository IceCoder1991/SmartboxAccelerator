using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Smartboxx.Api.Tests;

public sealed class DocumentTypeRegistryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public DocumentTypeRegistryTests(WebApplicationFactory<Program> factory) => _factory = factory.WithWebHostBuilder(x => x.UseEnvironment("Development"));
    private HttpClient Client(string tenant = "acme-demo", string role = "admin")
    { var c = _factory.CreateClient(); c.DefaultRequestHeaders.Add("X-Smartboxx-Tenant", tenant); c.DefaultRequestHeaders.Add("X-Dev-Roles", role); return c; }
    private static object Valid(string displayName = "Invoice") => new
    {
        technicalName = "invoice", displayName, description = "Synthetic invoices", classificationInstructions = "Classify invoice headings.",
        extractionSchema = new { reference = "invoice-schema", version = 1 }, prompt = "Extract invoice fields.",
        validationRules = new { reference = "invoice-rules", version = 1 },
        reviewPolicy = new { classificationThreshold = .8m, defaultFieldThreshold = .75m, autoApproveThreshold = .95m, fieldThresholds = new Dictionary<string, decimal> { ["total"] = .9m }, mandatoryReviewFields = new[] { "bankAccount" } },
        modelConfiguration = new { purposeAlias = "document-extraction", strategy = "hybrid", parameters = new Dictionary<string, string>() },
        evaluationDatasetReference = "synthetic-invoices-v1", outputMapping = new { fields = new Dictionary<string, string> { ["total"] = "invoiceTotal" } }
    };

    [Fact]
    public async Task Registry_enforces_permissions_tenant_isolation_and_published_immutability()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(role: "viewer").PostAsJsonAsync("/api/document-types", Valid())).StatusCode);
        var created = await Client().PostAsJsonAsync("/api/document-types", Valid());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement;
        var id = item.GetProperty("id").GetGuid();
        Assert.Empty(JsonDocument.Parse(await Client("northwind-demo").GetStringAsync("/api/document-types")).RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await Client().PostAsync($"/api/document-types/{id}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client().PutAsJsonAsync($"/api/document-types/{id}", Valid("Changed"))).StatusCode);
        var clone = await Client().PostAsync($"/api/document-types/{id}/versions", null);
        Assert.Equal(HttpStatusCode.OK, clone.StatusCode);
        Assert.Equal(2, JsonDocument.Parse(await clone.Content.ReadAsStringAsync()).RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Invalid_registry_input_returns_validation_problem()
    {
        var invalid = await Client().PostAsJsonAsync("/api/document-types", new { technicalName = "INVALID" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}
