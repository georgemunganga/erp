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
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalInvoiceIssueOwnerLookupTests
{
    [Fact]
    public async Task Held_lookup_cannot_open_or_disclose_names_without_real_readiness()
    {
        await using var app = await App();
        using var client = app.GetTestClient();
        var path = $"/api/procurement/v1/invoices/{Guid.NewGuid()}/principal-matching/exceptions/{Guid.NewGuid()}/eligible-owners";
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PrincipalInvoiceExceptionsUnavailable", body.GetProperty("code").GetString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "ap_processor");
        using var denied = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Theory]
    [InlineData("?limit=51")]
    [InlineData("?cursor=00000000-0000-0000-0000-000000000000")]
    [InlineData("?q=%0A")]
    public async Task Malformed_lookup_target_never_reaches_a_business_transaction(string query)
    {
        // Synthetic readiness tests only request validation, never a positive
        // owner lookup. No Procurement transaction/database is registered.
        await using var app = await App(syntheticallyReady: true);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(
            $"/api/procurement/v1/invoices/{Guid.NewGuid()}/principal-matching/exceptions/{Guid.NewGuid()}/eligible-owners{query}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public void Only_current_matchers_reaching_the_issue_branch_can_be_selected()
    {
        var branch = Guid.NewGuid();
        var other = Guid.NewGuid();
        var owner = Guid.NewGuid().ToString("D");
        ProcurementPrincipalPeople.Person Person(string id, bool wide, Guid[] branches, params string[] actions)
            => new(id, null, actions.ToHashSet(StringComparer.Ordinal), wide, branches.ToHashSet());

        Assert.False(ProcurementPrincipalInvoiceDiscrepancyRoutes.EligibleIssueOwner(null, branch, owner));
        Assert.False(ProcurementPrincipalInvoiceDiscrepancyRoutes.EligibleIssueOwner(
            Person(owner, true, [], "procurement-invoice-read", "procurement-invoice-match"), branch, owner));
        Assert.False(ProcurementPrincipalInvoiceDiscrepancyRoutes.EligibleIssueOwner(
            Person(Guid.NewGuid().ToString("D"), true, []), branch, owner));
        Assert.False(ProcurementPrincipalInvoiceDiscrepancyRoutes.EligibleIssueOwner(
            Person(Guid.NewGuid().ToString("D"), true, [], "procurement-invoice-match"), branch, owner));
        Assert.False(ProcurementPrincipalInvoiceDiscrepancyRoutes.EligibleIssueOwner(
            Person(Guid.NewGuid().ToString("D"), false, [other], "procurement-invoice-read", "procurement-invoice-match"), branch, owner));
        Assert.True(ProcurementPrincipalInvoiceDiscrepancyRoutes.EligibleIssueOwner(
            Person(Guid.NewGuid().ToString("D"), false, [branch], "procurement-invoice-read", "procurement-invoice-match"), branch, owner));
        Assert.True(ProcurementPrincipalInvoiceDiscrepancyRoutes.EligibleIssueOwner(
            Person(Guid.NewGuid().ToString("D"), true, [], "procurement-invoice-read", "procurement-invoice-match"), branch, owner));
    }

    private static async Task<WebApplication> App(bool syntheticallyReady = false)
    {
        var scope = new ProcurementRequestScope();
        scope.Set("principal-issue-gate", Guid.NewGuid(), Guid.NewGuid().ToString("D"), null, null, false, "fixture");
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(scope);
        if (syntheticallyReady)
        {
            builder.Services.AddSingleton(new ProcurementPrincipalInvoiceMatchingOptions { Enabled = true });
            builder.Services.AddSingleton<IProcurementPrincipalInvoiceSchemaReadiness, ValidationOnlyReadiness>();
        }
        builder.Services.AddAuthentication("principal-issue-gate")
            .AddScheme<AuthenticationSchemeOptions, Authentication>("principal-issue-gate", _ => { });
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
        ProcurementPrincipalInvoiceDiscrepancyRoutes.MapRoutes(app); await app.StartAsync(); return app;
    }

    private sealed class ValidationOnlyReadiness : IProcurementPrincipalInvoiceSchemaReadiness
    {
        public Task<bool> IsReadyAsync(CancellationToken ct) => Task.FromResult(true);
        public Task<bool> IsReadReadyAsync(CancellationToken ct) => Task.FromResult(true);
        public Task<bool> IsDownloadReadyAsync(CancellationToken ct) => Task.FromResult(false);
        public Task<bool> IsReadyAsync(ProcurementDbContext db, CancellationToken ct) => throw new InvalidOperationException("Business transaction reached");
        public Task<bool> IsReadReadyAsync(ProcurementDbContext db, CancellationToken ct) => throw new InvalidOperationException("Business transaction reached");
        public Task<bool> IsDownloadReadyAsync(ProcurementDbContext db, CancellationToken ct) => throw new InvalidOperationException("Business transaction reached");
    }

    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].FirstOrDefault() ?? "erp_admin";
            var principal = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", "fixture"), new Claim("tenant", "principal-issue-gate"),
                new Claim(ClaimTypes.Role, role)], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
