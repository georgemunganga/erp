using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
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

/// <summary>Real mapped routing/authentication/activation gates without a database. Full
/// matching transaction, isolation, rollback and replay still require the PostgreSQL probe.</summary>
public sealed class ProcurementInvoiceMatchingGateHttpTests
{
    [Theory]
    [InlineData(false,true,false,0)]
    [InlineData(true,false,false,0)]
    [InlineData(true,true,false,4)]
    public async Task Closed_gates_block_mapping_comparison_read_and_recovery_before_any_database_use(bool enabled,bool configured,bool ready,int readinessCalls)
    {
        var readiness=new Readiness(ready);
        await using var app=await App(new() { Enabled=enabled,ExecutionDatabaseConfigured=configured },readiness);
        using var client=app.GetTestClient(); var id=Guid.NewGuid();
        var responses=new[] { await client.GetAsync($"/api/procurement/v1/invoices/{id}/matching"),
            await client.GetAsync($"/api/procurement/v1/invoices/{id}/matching/commands/{Guid.NewGuid()}?operation=InvoiceCompare"),
            await client.PostAsJsonAsync($"/api/procurement/v1/invoices/{id}/matching/mappings",new ProcurementInvoiceMatchingRoutes.MappingInput(1,Guid.NewGuid(),Guid.NewGuid(),[])),
            await client.PostAsJsonAsync($"/api/procurement/v1/invoices/{id}/matching/compare",new ProcurementInvoiceMatchingRoutes.CompareInput(1,Guid.NewGuid(),null)) };
        foreach(var response in responses)
        {
            using(response)
            {
                Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
                var body=await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("InvoiceMatchingUnavailable",body.GetProperty("code").GetString());
            }
        }
        Assert.Equal(readinessCalls,readiness.Calls);
    }
    [Fact]
    public async Task Invoice_capture_role_does_not_implicitly_gain_new_qualified_matching_permission()
    {
        var readiness=new Readiness(false); await using var app=await App(new(),readiness);
        using var client=app.GetTestClient(); client.DefaultRequestHeaders.Add("X-Test-Role","ap_processor");
        using var response=await client.PostAsJsonAsync($"/api/procurement/v1/invoices/{Guid.NewGuid()}/matching/compare",new ProcurementInvoiceMatchingRoutes.CompareInput(1,Guid.NewGuid(),null));
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode); Assert.Equal(0,readiness.Calls);
    }
    private static async Task<WebApplication> App(ProcurementInvoiceMatchingExecutionOptions options,Readiness readiness)
    {
        var scope=new ProcurementRequestScope(); scope.Set("matching-gate",Guid.NewGuid(),Guid.NewGuid().ToString("D"),null,null,false,"fixture");
        var builder=WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(scope); builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IProcurementInvoiceMatchingSchemaReadiness>(readiness);
        builder.Services.AddSingleton(new ProcurementApprovalTransaction("Host=127.0.0.1;Port=1;Database=never_open;Username=fixture;Timeout=1",scope,scope));
        builder.Services.AddAuthentication("matching-gate").AddScheme<AuthenticationSchemeOptions,Authentication>("matching-gate",_=>{});
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        var app=builder.Build(); app.UseAuthentication(); app.UseAuthorization(); ProcurementInvoiceMatchingRoutes.MapRoutes(app); await app.StartAsync(); return app;
    }
    private sealed class Readiness(bool ready) : IProcurementInvoiceMatchingSchemaReadiness
    {
        public int Calls { get; private set; }
        public Task<bool> IsReadyAsync(CancellationToken ct) { Calls++; return Task.FromResult(ready); }
    }
    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].FirstOrDefault() ?? "erp_admin";
            var identity = new ClaimsIdentity(new[] { new Claim("sub", "fixture"), new Claim("tenant", "capability-test"), new Claim(ClaimTypes.Role, role) }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
