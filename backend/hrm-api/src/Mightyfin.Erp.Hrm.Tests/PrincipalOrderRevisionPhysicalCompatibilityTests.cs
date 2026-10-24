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
        Assert.False(ProcurementPrincipalOrderDraftHistory.Classify(0, 0, false, false));
    }

    [Theory]
    [InlineData(0, 0, true, false)] // Retained migration history cannot lose its tables.
    [InlineData(1, 1, false, false)]
    [InlineData(2, 2, false, false)]
    [InlineData(3, 2, true, true)] // One relation replaced by a view or other kind.
    [InlineData(3, 3, false, false)]
    [InlineData(3, 3, true, false)]
    public void PartialOrUncapturedP33GraphCannotFallBackToOriginalPins(
        int tableCount, int regularCount, bool migrationApplied, bool catalogMatches)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ProcurementPrincipalOrderDraftHistory.Classify(tableCount, regularCount, migrationApplied, catalogMatches));
    }

    [Fact]
    public void CompleteCapturedP33GraphRequiresFullHistoryVerifier()
    {
        Assert.True(ProcurementPrincipalOrderDraftHistory.Classify(3, 3, true, true));
    }
}
