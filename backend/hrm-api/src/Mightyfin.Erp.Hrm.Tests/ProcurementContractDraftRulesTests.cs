using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementContractDraftRulesTests
{
    [Fact]
    public void Contract_draft_requires_supplier_terms_and_valid_dates()
    {
        var valid = new ContractDraftInput(Guid.NewGuid(), "Office supplies", "Framework",
            "ZMW", 120_000m, new DateOnly(2026, 11, 1), new DateOnly(2027, 10, 31),
            new DateOnly(2027, 8, 1));
        Assert.Null(ProcurementContractDraftRules.Validate(valid));
        Assert.NotNull(ProcurementContractDraftRules.Validate(valid with { SupplierId = Guid.Empty }));
        Assert.NotNull(ProcurementContractDraftRules.Validate(valid with { CeilingAmount = -1 }));
        Assert.NotNull(ProcurementContractDraftRules.Validate(valid with { ExpiresOn = new DateOnly(2026, 10, 1) }));
        Assert.NotNull(ProcurementContractDraftRules.Validate(valid with { RenewalNoticeOn = new DateOnly(2027, 11, 1) }));
    }
}
