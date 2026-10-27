using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalOrderFinanceTaskReadTests
{
    [Theory]
    [InlineData("Draft", false, false, true)]
    [InlineData("Draft", true, false, true)]
    [InlineData("Draft", false, true, false)]
    [InlineData("Draft", true, true, false)]
    [InlineData("ReleasePending", false, false, false)]
    [InlineData("ReleasePending", false, true, false)]
    [InlineData("ReleasePending", true, false, false)]
    [InlineData("ReleasePending", true, true, true)]
    [InlineData("Issued", true, false, false)]
    [InlineData("Cancelled", true, true, false)]
    public void Finance_read_preserves_funded_Draft_and_requires_both_release_records(
        string state, bool funding, bool release, bool readable)
    {
        Assert.Equal(readable,
            ProcurementPrincipalOrderDraftRoutes.FinanceTaskEvidenceMatchesState(state, funding, release));
    }
}
