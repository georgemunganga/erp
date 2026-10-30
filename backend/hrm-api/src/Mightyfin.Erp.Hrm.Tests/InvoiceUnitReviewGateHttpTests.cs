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
    [Fact]
    public async Task Manual_routes_are_closed_without_configuration_and_never_require_a_document_provider()
    {
        await using var app=await App();using var client=app.GetTestClient();var id=Guid.NewGuid();
        foreach(var path in new[]{ $"/invoices/{id}/manual-unit-review-readiness", $"/invoices/{id}/manual-unit-reviews/commands/{Guid.NewGuid()}", "/manual-invoice-unit-reviews/tasks",
            $"/manual-invoice-unit-reviews/{id}", $"/manual-invoice-unit-reviews/{id}/commands/{Guid.NewGuid()}?operation=Open"})
        {
            using var response=await client.GetAsync("/api/procurement/v1"+path);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
            Assert.Equal("InvoiceManualUnitReviewUnavailable",(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        using var opened=await client.PostAsJsonAsync($"/api/procurement/v1/invoices/{id}/manual-unit-reviews",
            new ProcurementInvoiceUnitReviewRoutes.OpenInput(1,1,"Entered units checked"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable,opened.StatusCode);
        using var decided=await client.PostAsJsonAsync($"/api/procurement/v1/manual-invoice-unit-reviews/{id}/assignments/{Guid.NewGuid()}/decisions",
            new ProcurementInvoiceUnitReviewRoutes.DecisionInput(1,1,1,"Approve","Independently checked","ManuallyEnteredUnitsConfirmed"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable,decided.StatusCode);
        Assert.False(new ProcurementInvoiceUnitReviewOptions().ManualAttestationEnabled);
    }
    [Theory]
    [InlineData(1,"DocumentReview",false)]
    [InlineData(1,"ManualAttestation",false)]
    [InlineData(2,"DocumentReview",false)]
    [InlineData(2,"ManualAttestation",true)]
    [InlineData(3,"ManualAttestation",false)]
    public void Manual_policy_requires_both_explicit_schema_and_mode(int schema,string mode,bool expected)
    {
        var rules=new Mightyfin.Erp.Procurement.Application.InvoiceUnitReviewPolicyRules.Document([])
            {SchemaVersion=schema,EvidenceMode=mode};
        Assert.Equal(expected,ProcurementInvoiceUnitReviewRoutes.IsManualPolicy(rules));
    }
    [Fact]
    public void Manual_write_requires_selected_Core_principal_and_rejects_raw_All_fallback()
    {
        var principal=Guid.NewGuid();var branch=Guid.NewGuid();var scope=new ProcurementRequestScope();
        scope.Set("manual-test",Guid.NewGuid(),principal.ToString("D"),branch,null,true,"test");
        scope.AssignPrincipal(principal);
        Assert.True(ProcurementInvoiceUnitReviewRoutes.ManualWriteContext(scope,false));
        Assert.False(ProcurementInvoiceUnitReviewRoutes.ManualWriteContext(scope,true));
        var all=new ProcurementRequestScope();
        all.Set("manual-test",Guid.NewGuid(),principal.ToString("D"),null,null,true,"test",true,[branch]);
        all.AssignPrincipal(principal);
        Assert.False(ProcurementInvoiceUnitReviewRoutes.ManualWriteContext(all,false));
        var noPrincipal=new ProcurementRequestScope();
        noPrincipal.Set("manual-test",Guid.NewGuid(),principal.ToString("D"),branch,null,true,"test");
        Assert.False(ProcurementInvoiceUnitReviewRoutes.ManualWriteContext(noPrincipal,false));
    }
    [Theory]
    [InlineData("actor")][InlineData("key")][InlineData("operation")]
    [InlineData("tenant")][InlineData("company")][InlineData("branch")][InlineData("invoice")]
    public void Manual_recorded_value_rejects_foreign_actor_key_operation_and_scope_before_payload(string fault)
    {
        var principal=Guid.NewGuid().ToString("D");var company=Guid.NewGuid();var branch=Guid.NewGuid();var key=Guid.NewGuid();
        var scope=new ProcurementRequestScope();scope.Set("manual-recovery-test",company,principal,branch,null,true,"test");
        var invoice=new Mightyfin.Erp.Procurement.Domain.SupplierInvoice{
            TenantId="manual-recovery-test",LegalEntityId=company,BranchRef=branch};
        var source=new Mightyfin.Erp.Procurement.Domain.InvoiceUnitReviewSource{
            TenantId=fault=="tenant"?"other":"manual-recovery-test",LegalEntityId=fault=="company"?Guid.NewGuid():company,
            SupplierInvoiceId=fault=="invoice"?Guid.NewGuid():invoice.Id,BranchRef=fault=="branch"?Guid.NewGuid():branch};
        var receipt=new Mightyfin.Erp.Procurement.Domain.InvoiceUnitReviewCommandReceipt{
            TenantId="manual-recovery-test",LegalEntityId=company,ReviewSourceId=source.Id,
            ActorPrincipalId=fault=="actor"?Guid.NewGuid().ToString("D"):principal,
            Operation=fault=="operation"?"Decision":"Open",CommandKey=fault=="key"?Guid.NewGuid():key};
        Assert.Throws<KeyNotFoundException>(()=>ProcurementInvoiceUnitReviewRoutes.ManualRecordedValue(
            receipt,source,invoice,scope,"Open",key));
    }
    [Fact]
    public void Unknown_native_schema_pins_cannot_admit_any_check()
    {
        Assert.False(ProcurementInvoiceManualUnitReviewSchemaCapture.MatchesOutcome(""));
        Assert.False(ProcurementInvoiceManualUnitReviewSchemaCapture.MatchesOutcome(
            ProcurementInvoiceUnitReviewSchemaReadiness.CheckDefinitions["ck_inv_unit_outcome"]));
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
