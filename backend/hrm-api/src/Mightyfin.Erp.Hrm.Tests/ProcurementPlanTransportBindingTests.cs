using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPlanTransportBindingTests
{
    [Fact]
    public async Task Plan_endpoints_build_without_inferring_unregistered_services_as_request_bodies()
    {
        // A minimal host deliberately omits Plan services. Endpoint construction
        // must succeed; a request still requires the real registered services.
        // No fake readiness/options or successful workflow is supplied here.
        var builder = WebApplication.CreateBuilder();
        await using var app = builder.Build();
        ProcurementPlanRoutes.MapRoutes(app);
        ProcurementPlanReviewRoutes.MapRoutes(app);
        ProcurementPlanReviewReads.MapRoutes(app);
        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(x => x.Endpoints).OfType<RouteEndpoint>().ToArray();
        Assert.NotEmpty(endpoints);
        var gets = endpoints.Where(x => x.Metadata.GetMetadata<HttpMethodMetadata>()!
            .HttpMethods.Contains(HttpMethods.Get)).ToArray();
        Assert.NotEmpty(gets);
        Assert.All(gets, endpoint => Assert.Null(endpoint.Metadata.GetMetadata<IAcceptsMetadata>()));
        Assert.Contains(endpoints, endpoint => endpoint.RoutePattern.RawText!.EndsWith("/review-submissions", StringComparison.Ordinal)
            && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(HttpMethods.Post));
    }
}
