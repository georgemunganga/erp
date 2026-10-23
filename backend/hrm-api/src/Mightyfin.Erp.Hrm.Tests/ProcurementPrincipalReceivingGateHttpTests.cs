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

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Real mapped closed-gate/authorization proof only. No database, source or
/// current canonical actor is fabricated. Actual positive transactions remain a PG gate.</summary>
public sealed class ProcurementPrincipalReceivingGateHttpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_default_closed_configuration_never_enters_a_database_transaction(bool register)
    {
        await using var app=await App(register);using var client=app.GetTestClient();
        var id=Guid.NewGuid();
        using var read=await client.GetAsync($"/api/procurement/v1/receipts/{id}/principal-draft");
        using var cancel=await client.PostAsJsonAsync($"/api/procurement/v1/receipts/{id}/principal-cancel",new{version=1,reason="Explicit cancellation"});
        using var recover=await client.GetAsync($"/api/procurement/v1/receiving/principal-command-receipts/Record/{Guid.NewGuid()}");
        foreach(var response in new[]{read,cancel,recover})
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
            Assert.Contains("PrincipalReceivingUnavailable",await response.Content.ReadAsStringAsync());
        }
        // No ProcurementApprovalTransaction is registered: crossing the gate is a
        // hard fixture error, not a successful fake persistence call.
    }
    [Fact]
    public async Task Authenticated_employee_cannot_acquire_receiving_or_inspection_from_closed_successor_routes()
    {
        await using var app=await App(false);using var client=app.GetTestClient();client.DefaultRequestHeaders.Add("X-Test-Role","employee");
        using var read=await client.GetAsync($"/api/procurement/v1/receipts/{Guid.NewGuid()}/principal-draft");
        using var inspect=await client.PostAsJsonAsync($"/api/procurement/v1/receipts/{Guid.NewGuid()}/lines/{Guid.NewGuid()}/principal-inspect",
            new{version=1,sourceVersion=1,quantity="1",evidenceReference="Observed evidence"});
        Assert.Equal(HttpStatusCode.Forbidden,read.StatusCode);Assert.Equal(HttpStatusCode.Forbidden,inspect.StatusCode);
    }
    [Fact]
    public async Task Unknown_recovery_operation_returns_404_without_constructing_a_transaction()
    {
        await using var app=await App(false);using var client=app.GetTestClient();
        using var response=await client.GetAsync($"/api/procurement/v1/receiving/principal-command-receipts/Unknown/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
    }
    private static async Task<WebApplication> App(bool register)
    {
        var scope=new ProcurementRequestScope();scope.Set("receiving-principal-gate",Guid.NewGuid(),Guid.NewGuid().ToString("D"),null,null,false,"fixture");
        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseTestServer();builder.Services.AddSingleton(scope);
        if(register)builder.Services.AddSingleton(new ProcurementPrincipalReceivingOptions());
        builder.Services.AddAuthentication("receiving-gate").AddScheme<AuthenticationSchemeOptions,Authentication>("receiving-gate",_=>{});
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        var app=builder.Build();app.UseAuthentication();app.UseAuthorization();ProcurementPrincipalReceivingRoutes.MapRoutes(app);await app.StartAsync();return app;
    }
    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder)
        :AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role=Request.Headers["X-Test-Role"].FirstOrDefault()??"erp_admin";
            var principal=new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub","fixture"),new Claim("tenant","receiving-principal-gate"),new Claim(ClaimTypes.Role,role)],Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal,Scheme.Name)));
        }
    }
}
