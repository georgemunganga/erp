using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementSourcingDraftRulesTests
{
    [Fact]
    public void Sourcing_draft_requires_a_valid_window_and_lines()
    {
        var line = new SourcingLineInput(null, null, "Ten laptops", "pcs", 10, "IT", null);
        var valid = new SourcingDraftInput("Laptops", "RFQ", "ZMW",
            new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 14, 17, 0, 0, TimeSpan.Zero), [line]);
        Assert.Null(ProcurementSourcingDraftRules.Validate(valid));
        Assert.NotNull(ProcurementSourcingDraftRules.Validate(valid with { ClosesAt = valid.OpensAt }));
        Assert.NotNull(ProcurementSourcingDraftRules.Validate(valid with { Lines = [] }));
        Assert.NotNull(ProcurementSourcingDraftRules.Validate(valid with
        {
            Lines = [line with { PurchaseRequestLineId = Guid.NewGuid() }],
        }));
        Assert.NotNull(ProcurementSourcingDraftRules.Validate(valid with
        {
            Lines = [line with { Quantity = 0 }],
        }));
    }
}
