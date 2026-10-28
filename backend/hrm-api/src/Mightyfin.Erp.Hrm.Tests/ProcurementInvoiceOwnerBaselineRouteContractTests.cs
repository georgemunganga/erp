using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementInvoiceOwnerBaselineRouteContractTests
{
    [Fact]
    public void All_branch_post_cannot_gain_write_authority_from_middleware_branch_fallback()
    {
        Assert.True(ProcurementInvoiceOwnerBaselineRoutes.WriteContextAllowed(false,false,false));
        Assert.False(ProcurementInvoiceOwnerBaselineRoutes.WriteContextAllowed(false,false,true));
        Assert.False(ProcurementInvoiceOwnerBaselineRoutes.WriteContextAllowed(false,true,false));
        Assert.False(ProcurementInvoiceOwnerBaselineRoutes.WriteContextAllowed(true,false,false));
    }
    [Fact]
    public void Original_decision_key_binds_exact_baseline_assignment_and_expected_versions()
    {
        var key = Guid.NewGuid();
        var baseline = Guid.NewGuid();
        var assignment = Guid.NewGuid();
        var body = new ProcurementInvoiceOwnerBaselineRoutes.DecisionInput(2, 1, "Approve", null);
        var original = ProcurementInvoiceOwnerBaselineRoutes.Fingerprint(
            "Decide", key, baseline, assignment, body);

        Assert.NotEqual(original, ProcurementInvoiceOwnerBaselineRoutes.Fingerprint(
            "Decide", key, baseline, Guid.NewGuid(), body));
        Assert.NotEqual(original, ProcurementInvoiceOwnerBaselineRoutes.Fingerprint(
            "Decide", key, Guid.NewGuid(), assignment, body));
        Assert.NotEqual(original, ProcurementInvoiceOwnerBaselineRoutes.Fingerprint(
            "Decide", key, baseline, assignment, body with { ExpectedCaseVersion = 3 }));
        Assert.NotEqual(original, ProcurementInvoiceOwnerBaselineRoutes.Fingerprint(
            "Decide", key, baseline, assignment, body with { Action = "Return", Reason = "Correction" }));
        Assert.Equal(original, ProcurementInvoiceOwnerBaselineRoutes.Fingerprint(
            "Decide", key, baseline, assignment, body));
    }
}
