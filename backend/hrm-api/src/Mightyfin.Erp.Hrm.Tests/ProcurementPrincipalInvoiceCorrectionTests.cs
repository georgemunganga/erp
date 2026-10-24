using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalInvoiceCorrectionTests
{
    [Theory]
    [InlineData("Verified", "Approved")]
    [InlineData("Returned", "Returned")]
    [InlineData("Rejected", "Rejected")]
    public void Corrected_mapping_uses_the_real_completed_core_case_state(string reviewOutcome, string coreState)
    {
        Assert.Equal(coreState, ProcurementPrincipalInvoiceMappingRoutes.PreviousCoreState(reviewOutcome));
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Approved")]
    public void No_other_review_state_can_authorize_a_replacement_mapping(string reviewOutcome)
    {
        Assert.Throws<InvalidOperationException>(() => ProcurementPrincipalInvoiceMappingRoutes.PreviousCoreState(reviewOutcome));
    }
}
