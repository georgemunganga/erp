using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalOrderDraftGateHttpTests
{
    [Fact]
    public async Task Convert_only_actor_can_reach_read_only_discovery_but_cannot_read_or_cancel_a_draft()
    {
        await using var app=await App(["procurement-po-convert"]);
        using var client=app.GetTestClient();
        using var suppliers=await client.GetAsync("/api/procurement/v1/orders/principal-direct-suppliers");
        using var sources=await client.GetAsync("/api/procurement/v1/orders/approved-principal-direct-sources");
        using var list=await client.GetAsync("/api/procurement/v1/orders/principal-direct-drafts");
        using var cancel=await client.PostAsJsonAsync($"/api/procurement/v1/orders/{Guid.NewGuid()}/principal-direct-cancel",
            new{expectedOrderVersion=1,originalDirectSourceSha256=new string('a',64),reason="fixture"});
        Assert.Equal(HttpStatusCode.ServiceUnavailable,suppliers.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,sources.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,cancel.StatusCode);
    }

    [Fact]
    public async Task Unknown_or_ungranted_actor_cannot_enter_any_principal_draft_route()
    {
        await using var app=await App([]);
        using var client=app.GetTestClient();
        using var supplier=await client.GetAsync("/api/procurement/v1/orders/principal-direct-suppliers");
        using var create=await client.PostAsJsonAsync("/api/procurement/v1/orders/from-approved-principal-direct-sources",new{});
        using var list=await client.GetAsync("/api/procurement/v1/orders/principal-direct-drafts");
        using var receipt=await client.GetAsync($"/api/procurement/v1/orders/principal-direct-command-receipts/Create/{Guid.NewGuid()}");
        Assert.All(new[]{supplier,create,list,receipt},x=>Assert.Equal(HttpStatusCode.Forbidden,x.StatusCode));
    }

    private static async Task<WebApplication> App(string[] actions)
    {
        var scope=new ProcurementRequestScope();
        scope.Set("m3b-http-gate",Guid.NewGuid(),Guid.NewGuid().ToString("D"),null,null,false,"fixture");
        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(scope);
        builder.Services.AddSingleton(new ProcurementPrincipalOrderDraftOptions()); // default off
        builder.Services.AddSingleton(new ProcurementApprovalExecutionOptions());
        builder.Services.AddSingleton(new AllowedActions(actions));
        builder.Services.AddSingleton<IProcurementPrincipalOrderDraftReadiness,UnavailableProcurementPrincipalOrderDraftReadiness>();
        // A route accidentally entering the transaction would try this unreachable host.
        builder.Services.AddSingleton(new ProcurementApprovalTransaction(
            "Host=127.0.0.1;Port=1;Database=never_open;Username=fixture;Timeout=1",scope,scope));
        builder.Services.AddAuthentication("m3b-gate").AddScheme<AuthenticationSchemeOptions,FixtureAuth>("m3b-gate",_=>{});
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        var app=builder.Build();app.UseAuthentication();app.UseAuthorization();
        ProcurementPrincipalOrderDraftRoutes.MapRoutes(app);
        await app.StartAsync();return app;
    }

    private sealed record AllowedActions(string[] Values);
    private sealed class FixtureAuth(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,
        UrlEncoder encoder,AllowedActions allowed):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims=new List<Claim>{new("sub",Guid.NewGuid().ToString("D")),new("tenant","m3b-http-gate"),
                new(ProcurementPermissions.ConfiguredClaim,"true"),
                new(ProcurementPermissions.ActionClaim,"procurement-access")};
            claims.AddRange(allowed.Values.Select(x=>new Claim(ProcurementPermissions.ActionClaim,x)));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims,Scheme.Name)),Scheme.Name)));
        }
    }
}
