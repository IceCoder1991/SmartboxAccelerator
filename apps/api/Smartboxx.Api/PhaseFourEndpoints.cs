using Smartboxx.Modules.ApiIntelligence;
using Smartboxx.Modules.ContactCentre;
using Smartboxx.Modules.SoftwareDelivery;
using Smartboxx.Modules.Testing;
using Smartboxx.Modules.Value;
using Smartboxx.Platform.Contracts;

namespace Smartboxx.Api;
public static class PhaseFourEndpoints
{
    public static IEndpointRouteBuilder MapPhaseFourApis(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/contact-centre/calls", async (CallSource call, TenantAccessor tenant, ContactCentreService service, IAuditService audit, CancellationToken ct) =>
        { var result = await service.IngestAsync(tenant.Current!.Id, call, cancellationToken: ct); await audit.WriteAsync(new("call.ingested", "contact-centre", "call", result.Id.ToString(), Result: "succeeded"), ct); return Results.Accepted($"/api/contact-centre/calls/{result.Id}", result); }).RequireAuthorization("contactCentre.ingest");
        app.MapGet("/api/contact-centre/calls", (string? query, ContactCentreService service) => service.Search(query ?? "")).RequireAuthorization("contactCentre.view");

        app.MapPost("/api/software-delivery/snapshots", async (RepositoryRequest request, TenantAccessor tenant, SoftwareDeliveryService service, IAuditService audit, CancellationToken ct) =>
        { var result = await service.AnalyseAsync(tenant.Current!.Id, request, ct); await audit.WriteAsync(new("snapshot.analysed", "software-delivery", "repository", request.RepositoryId, new Dictionary<string, object?>{{"commit", request.Commit}}, "succeeded"), ct); return Results.Ok(result); }).RequireAuthorization("softwareDelivery.ingest");
        app.MapGet("/api/software-delivery/snapshots/{id}", (string id, TenantAccessor tenant, IRepositoryContextReader reader) => reader.Get(tenant.Current!.Id, id) is { } value ? Results.Ok(value) : Results.NotFound()).RequireAuthorization("softwareDelivery.view");

        app.MapPost("/api/testing/suites", (TestSuiteRequest request, TenantAccessor tenant, TestingService service) => Results.Ok(service.Generate(tenant.Current!.Id, request.SnapshotId, request.Requirements))).RequireAuthorization("testing.generate");
        app.MapPost("/api/testing/suites/{id:guid}/approval", (Guid id, TenantAccessor tenant, TestingService service) => Results.Ok(service.Approve(tenant.Current!.Id, id))).RequireAuthorization("testing.approve");

        app.MapPost("/api/api-intelligence/contracts", async (ApiContractRequest request, TenantAccessor tenant, ApiIntelligenceService service, IAuditService audit, CancellationToken ct) =>
        { var result = await service.IngestAsync(tenant.Current!.Id, request.Name, request.Specification, request.Rules, ct); await audit.WriteAsync(new("contract.analysed", "api-intelligence", "contract", result.Id.ToString(), Result: "succeeded"), ct); return Results.Ok(result); }).RequireAuthorization("apiGovernance.ingest");
        app.MapGet("/api/api-intelligence/contracts", (string? query, ApiIntelligenceService service) => service.Search(query ?? "")).RequireAuthorization("apiGovernance.view");
        app.MapPost("/api/api-intelligence/compare", (ApiComparisonRequest request) => ApiIntelligenceService.Compare(request.Baseline, request.Candidate)).RequireAuthorization("apiGovernance.view");

        app.MapGet("/api/value/report", (int? modelVersion, TenantAccessor tenant, InMemoryValueService service) => service.Calculate(tenant.Current!.Id, modelVersion)).RequireAuthorization("value.view");
        app.MapGet("/api/value/export", (TenantAccessor tenant, InMemoryValueService service) => Results.Text(System.Text.Json.JsonSerializer.Serialize(service.Calculate(tenant.Current!.Id)), "application/json")).RequireAuthorization("value.view");
        return app;
    }
}
public sealed record TestSuiteRequest(string SnapshotId, IReadOnlyList<Requirement> Requirements);
public sealed record ApiContractRequest(string Name, string Specification, IReadOnlyList<GovernanceRule>? Rules);
public sealed record ApiComparisonRequest(ApiContract Baseline, ApiContract Candidate);
