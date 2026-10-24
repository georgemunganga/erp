using Mightyfin.Erp.Hrm.Api;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class PrincipalOrderRevisionPhysicalCompatibilityTests
{
    [Fact]
    public void AbsentP33GraphRetainsTheOriginalP30ReadPath()
    {
        // An absent graph cannot be treated as an empty installed revision set.
        // The caller still requires the P30 original pins and Create receipt.
        Assert.False(ProcurementPrincipalOrderDraftHistory.Classify(0, false, false));
    }

    [Theory]
    [InlineData(1, false, false)]
    [InlineData(2, false, false)]
    [InlineData(3, false, false)]
    [InlineData(3, true, false)]
    public void PartialOrUncapturedP33GraphCannotFallBackToOriginalPins(
        int tableCount, bool migrationApplied, bool catalogMatches)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ProcurementPrincipalOrderDraftHistory.Classify(tableCount, migrationApplied, catalogMatches));
    }

    [Fact]
    public void CompleteCapturedP33GraphRequiresFullHistoryVerifier()
    {
        Assert.True(ProcurementPrincipalOrderDraftHistory.Classify(3, true, true));
    }
}
