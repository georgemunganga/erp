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

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Actual mapped fail-closed routes; no database, coordinator or fake
/// successful quotation response is registered by this test host.</summary>
public sealed class ProcurementPrincipalQuotationGateHttpTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task All_routes_fail_closed_without_captured_read_contract_before_any_store(bool configured, bool provider)
    {
        await using var app = await Host(configured, provider); using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Quotation-Test-Reader", "yes");
        foreach (var (method, path) in Paths())
        {
            using var response = method == "GET" ? await client.GetAsync(path) : await client.PostAsJsonAsync(path, new { });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("QuotationStoreUnavailable", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
    }

    [Fact]
    public async Task ERP_admin_without_explicit_quotation_read_cannot_enter_any_new_route()
    {
        await using var app = await Host(true, true); using var client = app.GetTestClient();
        foreach (var (method, path) in Paths())
        {
            using var response = method == "GET" ? await client.GetAsync(path) : await client.PostAsJsonAsync(path, new { });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    private static (string, string)[] Paths()
    {
        var id = Guid.NewGuid(); var invitation = Guid.NewGuid(); var bid = Guid.NewGuid();
        var prefix = "/api/procurement/v1/sourcing";
        return [("GET", prefix + "/quotation-sources"), ("GET", $"{prefix}/{id}/quotation-drafts"),
            ("GET", $"{prefix}/{id}/quotation-drafts/{bid}"), ("GET", $"{prefix}/{id}/quotation-drafts/{bid}/versions/1"), ("GET", $"{prefix}/quotation-commands/{Guid.NewGuid()}"),
            ("POST", $"{prefix}/{id}/invitations/{invitation}/manual-contact"),
            ("POST", $"{prefix}/{id}/invitations/{invitation}/quotation-drafts"),
            ("POST", $"{prefix}/{id}/quotation-drafts/{bid}/revise"), ("POST", $"{prefix}/{id}/quotation-drafts/{bid}/cancel")];
    }
    private static async Task<WebApplication> Host(bool configured, bool provider)
    {
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
        var scope = new ProcurementRequestScope(); scope.Set("quotation-gate", Guid.NewGuid(), Actor,
            null, null, false, "quotation-gate"); scope.AssignPrincipal(Guid.Parse(Actor));
        builder.Services.AddSingleton(scope);
        builder.Services.AddSingleton(new ProcurementSourcingDraftExecutionOptions { ExecutionDatabaseConfigured = configured });
        if (provider) builder.Services.AddSingleton<IProcurementManualQuotationReadiness, UnavailableProcurementManualQuotationReadiness>();
        builder.Services.AddAuthentication("quotation-gate").AddScheme<AuthenticationSchemeOptions, Authentication>("quotation-gate", _ => { });
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
        ProcurementSourcingExecutionRoutes.MapRoutes(app); await app.StartAsync(); return app;
    }
    private const string Actor = "ce705463-b4e2-4e38-bcb5-dbf7e42c360e";
    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Actor), new(ClaimTypes.Role, "erp_admin") };
            if (Request.Headers["X-Quotation-Test-Reader"] == "yes") claims.AddRange([
                new(ProcurementPermissions.ConfiguredClaim, "true"),
                new(ProcurementPermissions.ActionClaim, "procurement-access"),
                new(ProcurementPermissions.ActionClaim, "procurement-quotation-read")]);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
}
