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

/// <summary>Mapped route and default-off proof only. Positive source/PG claims
/// require a later real finite-role and owner-schema rehearsal.</summary>
public sealed class ProcurementPrincipalInvoiceSourceGateHttpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closed_source_capability_never_opens_a_transaction(bool registered)
    {
        await using var app = await App(registered);
        using var client = app.GetTestClient();
        var invoice = Guid.NewGuid(); var order = Guid.NewGuid(); var line = Guid.NewGuid();
        foreach (var path in new[]
        {
            $"/api/procurement/v1/invoices/{invoice}/principal-matching/sources",
            $"/api/procurement/v1/invoices/{invoice}/principal-matching/sources/{order}/lines/{line}/acceptances"
        })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("PrincipalInvoiceSourceUnavailable", body.GetProperty("code").GetString());
        }
        // No transaction service is installed; reaching it would fail this test.
    }

    [Fact]
    public async Task Capture_only_role_cannot_read_the_principal_matching_source_directory()
    {
        await using var app = await App(false);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "ap_processor");
        using var response = await client.GetAsync($"/api/procurement/v1/invoices/{Guid.NewGuid()}/principal-matching/sources");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<WebApplication> App(bool register)
    {
        var scope = new ProcurementRequestScope();
        scope.Set("principal-invoice-gate", Guid.NewGuid(), Guid.NewGuid().ToString("D"), null, null, false, "fixture");
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(scope);
        if (register) builder.Services.AddSingleton(new ProcurementPrincipalInvoiceSourceOptions());
        builder.Services.AddAuthentication("principal-invoice-gate")
            .AddScheme<AuthenticationSchemeOptions, Authentication>("principal-invoice-gate", _ => { });
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
        ProcurementPrincipalInvoiceSourceRoutes.MapRoutes(app); await app.StartAsync(); return app;
    }

    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].FirstOrDefault() ?? "erp_admin";
            var principal = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", "fixture"), new Claim("tenant", "principal-invoice-gate"),
                new Claim(ClaimTypes.Role, role)], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
