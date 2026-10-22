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
/// <summary>Mapped closed-gate/action-policy tests only. No synthetic ready provider,
/// database connection, transaction or Core document authority is installed.</summary>
public sealed class InvoiceUnitReviewGateHttpTests
{
    [Fact]
    public async Task Default_configuration_refuses_all_review_routes_before_any_store_or_case_creation()
    {
        await using var app = await App(); using var client = app.GetTestClient(); var id = Guid.NewGuid();
        var paths = new[] { $"/invoices/{id}/unit-review-readiness", "/invoice-unit-reviews/tasks", $"/invoice-unit-reviews/{id}",
            $"/invoice-unit-reviews/{id}/commands/{Guid.NewGuid()}", $"/invoice-unit-reviews/{id}/documents", $"/invoice-unit-reviews/{id}/documents/{Guid.NewGuid()}/download" };
        foreach (var path in paths)
        {
            using var response = await client.GetAsync("/api/procurement/v1" + path);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("InvoiceUnitReviewUnavailable", body.GetProperty("code").GetString());
        }
        using var opened = await client.PostAsJsonAsync($"/api/procurement/v1/invoices/{id}/unit-reviews", new ProcurementInvoiceUnitReviewRoutes.OpenInput(1,1,null));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, opened.StatusCode);
        using var decision = await client.PostAsJsonAsync($"/api/procurement/v1/invoice-unit-reviews/{id}/assignments/{Guid.NewGuid()}/decisions",
            new ProcurementInvoiceUnitReviewRoutes.DecisionInput(1,1,1,"Approve",null,"OriginalDocumentUnitsConfirmed"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, decision.StatusCode);
        // No coordinator/readiness/context is registered: invoking any store would fail this test.
    }
    [Theory]
    [InlineData("ap_processor")][InlineData("procurement_manager")][InlineData("procurement_buyer")]
    public async Task Existing_roles_do_not_gain_new_request_or_original_unit_vote_actions(string role)
    {
        await using var app = await App(); using var client = app.GetTestClient(); client.DefaultRequestHeaders.Add("X-Review-Test-Role", role);
        using var open = await client.PostAsJsonAsync($"/api/procurement/v1/invoices/{Guid.NewGuid()}/unit-reviews", new ProcurementInvoiceUnitReviewRoutes.OpenInput(1,1,null));
        Assert.Equal(HttpStatusCode.Forbidden, open.StatusCode);
        using var vote = await client.PostAsJsonAsync($"/api/procurement/v1/invoice-unit-reviews/{Guid.NewGuid()}/assignments/{Guid.NewGuid()}/decisions",
            new ProcurementInvoiceUnitReviewRoutes.DecisionInput(1,1,1,"Approve",null,"OriginalDocumentUnitsConfirmed"));
        Assert.Equal(HttpStatusCode.Forbidden, vote.StatusCode);
    }
    private static async Task<WebApplication> App()
    {
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new ProcurementRequestScope()); builder.Services.AddSingleton(new ProcurementInvoiceUnitReviewOptions());
        builder.Services.AddAuthentication("unit-review-test").AddScheme<AuthenticationSchemeOptions, Authentication>("unit-review-test", _ => { });
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); ProcurementInvoiceUnitReviewRoutes.MapRoutes(app); await app.StartAsync(); return app;
    }
    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(new[] { new Claim("sub", "synthetic-unit-review-test"), new Claim("tenant", "synthetic"),
                new Claim(ClaimTypes.Role, Request.Headers["X-Review-Test-Role"].FirstOrDefault() ?? "erp_admin") }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
