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

/// <summary>Mapped route and action gates only. The retained evidence, SQL
/// scope-before-count, and role isolation still need the actual P30 PostgreSQL
/// and normal-cookie cohort before activation.</summary>
public sealed class ProcurementPrincipalOrderTaskGateHttpTests
{
    [Fact]
    public async Task New_task_reads_remain_unavailable_before_P30_activation()
    {
        await using var app=await App();using var client=app.GetTestClient();
        foreach(var path in new[]{"/api/procurement/v1/orders/principal-direct-suppliers?page=1&pageSize=25",
            "/api/procurement/v1/orders/principal-assigned-reviews",
            $"/api/procurement/v1/orders/{Guid.NewGuid()}/principal-finance-detail",
            "/api/procurement/v1/orders/principal-fulfilment-work?view=issue",
            $"/api/procurement/v1/orders/{Guid.NewGuid()}/principal-fulfilment-detail?view=ack"})
        {
            using var response=await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
            var body=await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("PrincipalOrderDraftUnavailable",body.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task Employee_cannot_call_either_task_read_without_current_action()
    {
        await using var app=await App();using var client=app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role","employee");
        foreach(var path in new[]{"/api/procurement/v1/orders/principal-direct-suppliers?page=1&pageSize=25",
            "/api/procurement/v1/orders/principal-assigned-reviews",
            $"/api/procurement/v1/orders/{Guid.NewGuid()}/principal-finance-detail",
            "/api/procurement/v1/orders/principal-fulfilment-work?view=issue",
            $"/api/procurement/v1/orders/{Guid.NewGuid()}/principal-fulfilment-detail?view=ack"})
        {
            using var response=await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        }
    }

    private static async Task<WebApplication> App()
    {
        var scope=new ProcurementRequestScope();
        scope.Set("po-task-gate",Guid.NewGuid(),Guid.NewGuid().ToString("D"),null,null,false,"fixture");
        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(scope);
        builder.Services.AddSingleton(new ProcurementPrincipalOrderDraftOptions());
        builder.Services.AddSingleton(new ProcurementOrderExecutionOptions{
            PrincipalExecutionEnabled=true,ExecutionDatabaseConfigured=true});
        builder.Services.AddSingleton(new ProcurementApprovalExecutionOptions());
        builder.Services.AddSingleton<IProcurementPrincipalOrderDraftReadiness,
            UnavailableProcurementPrincipalOrderDraftReadiness>();
        builder.Services.AddSingleton(new ProcurementApprovalTransaction(
            "Host=127.0.0.1;Port=1;Database=never_open;Username=fixture;Timeout=1",scope,scope));
        builder.Services.AddAuthentication("po-task-gate")
            .AddScheme<AuthenticationSchemeOptions,Authentication>("po-task-gate",_=>{});
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        var app=builder.Build();app.UseAuthentication();app.UseAuthorization();
        ProcurementPrincipalOrderDraftRoutes.MapRoutes(app);await app.StartAsync();return app;
    }

    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,UrlEncoder encoder)
        :AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role=Request.Headers["X-Test-Role"].FirstOrDefault()??"erp_admin";
            var identity=new ClaimsIdentity(new[]{new Claim("sub","fixture"),
                new Claim("tenant","po-task-gate"),new Claim(ClaimTypes.Role,role)},Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity),Scheme.Name)));
        }
    }
}
