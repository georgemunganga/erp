using Mightyfin.Erp.Hrm.Api;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductProcurementAdmissionTests
{
    [Fact]
    public void Product_metadata_preserves_draft_gates_and_never_activates_unqualified_workflows()
    {
        var original = TypedResults.Ok(new { schemaReady = true, capabilities = new {
            vendorDraft = true, itemDraft = false, requestDraft = true, orders = true,
            invoiceValidation = true, financeHandoff = true, supplierPortal = true } });
        var narrowed = Assert.IsAssignableFrom<IValueHttpResult>(ProductProcurementFlow.Metadata(original));
        var data = JsonSerializer.SerializeToElement(narrowed.Value);
        var capabilities = data.GetProperty("capabilities");
        Assert.True(capabilities.GetProperty("vendorDraft").GetBoolean());
        Assert.False(capabilities.GetProperty("itemDraft").GetBoolean());
        Assert.True(capabilities.GetProperty("requestDraft").GetBoolean());
        foreach (var key in new[] { "orders", "invoiceValidation", "financeHandoff", "supplierPortal" })
            Assert.False(capabilities.GetProperty(key).GetBoolean());
        Assert.True(original.Value!.capabilities.orders);
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.Parse("2026-11-12T08:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
        public override long GetTimestamp() => Now.UtcTicks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    }
    private static CoreErpRequestScope Scope(Clock clock, Guid company, Guid branch,
        string module = "procurement", bool all = false, string[]? actions = null)
    {
        var person = new OperationalSubjectObservation("42", "test-client", clock.Now.AddMinutes(1), clock,
            clock.GetTimestamp(), TimeSpan.FromMinutes(1));
        var license = new CommercialLicenseLease("01arz3ndektsv4rrffq69g5fav", [module], 1,
            clock.Now.AddMinutes(1), clock, clock.GetTimestamp(), TimeSpan.FromMinutes(1));
        var grant = new CompanyModuleAccessDecision(company, Guid.NewGuid(), "company-tenant", Guid.NewGuid(),
            1, module, ["procurement_manager"], actions ?? ["procurement-access"], all, all ? [] : [branch], person, license);
        return new(grant, all, all ? [] : [branch]);
    }

    [Fact]
    public void Entry_action_never_expands_role_to_business_permissions()
    {
        var scope = Scope(new(), Guid.NewGuid(), Guid.NewGuid());
        var principal = Assert.IsType<ProcurementCorePrincipal>(ProductProcurementAdmission.Principal(scope, null));
        var claims = principal.Claims(scope.TenantId);
        Assert.True(ProcurementPermissions.Allows("procurement-access", claims));
        Assert.False(ProcurementPermissions.Allows("procurement-vendor-create", claims));
        Assert.Equal(["procurement-access"], principal.Actions);
        Assert.Null(principal.WorkerId);
    }

    [Fact]
    public void Exact_company_and_current_owned_branch_are_required_even_with_no_legacy_assignments()
    {
        var company = Guid.NewGuid(); var branch = Guid.NewGuid(); var other = Guid.NewGuid();
        var scope = Scope(new(), company, branch);
        Assert.True(ProductProcurementAdmission.SelectionWithin(scope, company, branch, false, [branch, other]));
        Assert.True(ProductProcurementAdmission.SelectionWithin(scope, company, null, true, [branch, other]));
        Assert.False(ProductProcurementAdmission.SelectionWithin(scope, company, other, false, [branch, other]));
        Assert.False(ProductProcurementAdmission.SelectionWithin(scope, other, branch, false, [branch]));
        Assert.False(ProductProcurementAdmission.SelectionWithin(scope, company, null, true, []));
        Assert.False(ProductProcurementAdmission.SelectionWithin(scope, company, branch, true, [branch]));
        Assert.False(ProductProcurementAdmission.SelectionWithin(scope, company, null, false, [branch]));
    }

    [Fact]
    public void Company_wide_is_only_this_company_and_never_an_unowned_branch()
    {
        var company = Guid.NewGuid(); var branch = Guid.NewGuid(); var foreign = Guid.NewGuid();
        var scope = Scope(new(), company, branch, all: true);
        Assert.True(ProductProcurementAdmission.SelectionWithin(scope, company, null, true, [branch]));
        Assert.True(ProductProcurementAdmission.SelectionWithin(scope, company, branch, false, [branch]));
        Assert.False(ProductProcurementAdmission.SelectionWithin(scope, company, foreign, false, [branch]));
        Assert.False(ProductProcurementAdmission.SelectionWithin(scope, foreign, null, true, [branch]));
    }

    [Fact]
    public void Missing_wrong_module_expired_or_entryless_proof_is_denied()
    {
        var clock = new Clock(); var company = Guid.NewGuid(); var branch = Guid.NewGuid();
        Assert.Null(ProductProcurementAdmission.Principal(null, null));
        Assert.Null(ProductProcurementAdmission.Principal(Scope(clock, company, branch, module: "hrm"), null));
        Assert.Null(ProductProcurementAdmission.Principal(Scope(clock, company, branch, actions: ["procurement-vendor-read"]), null));
        var expires = Scope(clock, company, branch); clock.Now = clock.Now.AddMinutes(2);
        Assert.Null(ProductProcurementAdmission.Principal(expires, null));
        Assert.False(ProductProcurementAdmission.SelectionWithin(expires, company, branch, false, [branch]));
    }
}
