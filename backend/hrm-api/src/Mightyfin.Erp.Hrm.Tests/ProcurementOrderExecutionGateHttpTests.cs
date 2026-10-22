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
/// commercial transaction, isolation, rollback and replay still require the PostgreSQL probe.</summary>
public sealed class ProcurementOrderExecutionGateHttpTests
{
    [Theory]
    [InlineData(false, true, false, "OrderExecutionUnavailable", 0)]
    [InlineData(true, false, false, "OrderExecutionUnavailable", 0)]
    [InlineData(true, true, false, "OrderExecutionSchemaUnavailable", 2)]
    public async Task Closed_gate_blocks_reads_and_mutations_before_database_execution(bool enabled,
        bool connectionConfigured, bool ready, string expectedCode, int readinessCalls)
    {
        var options = new ProcurementOrderExecutionOptions { Enabled = enabled, ExecutionDatabaseConfigured = connectionConfigured };
        var readiness = new Readiness(ready);
        await using var app = await App(options, readiness);
        using var client = app.GetTestClient();
        using var read = await client.GetAsync("/api/procurement/v1/orders/approved-sources");
        using var submit = await client.PostAsJsonAsync($"/api/procurement/v1/orders/{Guid.NewGuid()}/submit", new OrderVersionInput(1));
        foreach (var response in new[] { read, submit })
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(expectedCode, body.GetProperty("code").GetString());
        }
        Assert.Equal(readinessCalls, readiness.Calls);
    }

    [Fact]
    public async Task Employee_does_not_acquire_PO_buying_permission_from_a_valid_session()
    {
        var readiness = new Readiness(false);
        await using var app = await App(new(), readiness);
        using var client = app.GetTestClient(); client.DefaultRequestHeaders.Add("X-Test-Role", "employee");
        using var response = await client.GetAsync("/api/procurement/v1/orders/approved-sources");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Equal(0, readiness.Calls);
    }

    private static async Task<WebApplication> App(ProcurementOrderExecutionOptions options, Readiness readiness)
    {
        var scope = new ProcurementRequestScope();
        scope.Set("po-gate-test", Guid.NewGuid(), Guid.NewGuid().ToString("D"), null, null, false, "fixture");
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(scope); builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IProcurementOrderSchemaReadiness>(readiness);
        // Port 1 is deliberately unusable. A gate regression cannot silently pass by
        // mutating an in-memory stand-in for the real shared PostgreSQL transaction.
        builder.Services.AddSingleton(new ProcurementApprovalTransaction(
            "Host=127.0.0.1;Port=1;Database=never_open;Username=fixture;Timeout=1", scope, scope));
        builder.Services.AddAuthentication("po-gate")
            .AddScheme<AuthenticationSchemeOptions, Authentication>("po-gate", _ => { });
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
        ProcurementOrderExecutionRoutes.MapRoutes(app); await app.StartAsync(); return app;
    }
    private sealed class Readiness(bool ready) : IProcurementOrderSchemaReadiness
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
