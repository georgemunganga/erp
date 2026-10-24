using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalGoodsReturnDraftRulesTests
{
    [Fact]
    public void Draft_quantity_is_exact_and_bounded_by_selected_fact_not_a_return_balance()
    {
        Assert.Equal(0.0001m, PrincipalGoodsReturnRules.Quantity("0.0001", 0.0001m));
        Assert.Equal(99999999999999.9999m,
            PrincipalGoodsReturnRules.Quantity("99999999999999.9999", 99999999999999.9999m));
        Assert.Throws<ArgumentException>(() => PrincipalGoodsReturnRules.Quantity("0", 1m));
        Assert.Throws<ArgumentException>(() => PrincipalGoodsReturnRules.Quantity("1.0001", 1m));
        Assert.ThrowsAny<Exception>(() => PrincipalGoodsReturnRules.Quantity("0.00001", 1m));
        Assert.ThrowsAny<Exception>(() => PrincipalGoodsReturnRules.Quantity("100000000000000.0000", 100000000000000m));
    }

    [Fact]
    public void Revision_fingerprint_binds_quantity_reason_reference_and_state()
    {
        var id = Guid.NewGuid();
        var original = PrincipalGoodsReturnRules.Fingerprint(id, 1, 0.0001m, "Damaged", "PHOTO-1", "Draft");
        Assert.Equal(original, PrincipalGoodsReturnRules.Fingerprint(id, 1, 0.0001m, "Damaged", "PHOTO-1", "Draft"));
        Assert.NotEqual(original, PrincipalGoodsReturnRules.Fingerprint(id, 1, 0.0002m, "Damaged", "PHOTO-1", "Draft"));
        Assert.NotEqual(original, PrincipalGoodsReturnRules.Fingerprint(id, 1, 0.0001m, "Wrong item", "PHOTO-1", "Draft"));
        Assert.NotEqual(original, PrincipalGoodsReturnRules.Fingerprint(id, 1, 0.0001m, "Damaged", "PHOTO-2", "Draft"));
        Assert.NotEqual(original, PrincipalGoodsReturnRules.Fingerprint(id, 2, 0.0001m, "Damaged", "PHOTO-1", "Cancelled"));
        Assert.Throws<ArgumentException>(() => PrincipalGoodsReturnRules.Fingerprint(id, 1, 1m, "", null, "Draft"));
        Assert.Throws<ArgumentException>(() => PrincipalGoodsReturnRules.Fingerprint(id, 1, 1m, "Damaged", null, "Approved"));
    }

    [Fact]
    public void Draft_response_states_no_downstream_effect_and_never_uses_binary_float()
    {
        var actor = Guid.NewGuid().ToString("D");
        var head = new PrincipalGoodsReturnDraft { CreatorPrincipalId = actor, AcceptedFactQuantity = 0.0001m };
        var revision = new PrincipalGoodsReturnRevision { DraftId = head.Id, ActorPrincipalId = actor,
            Quantity = 0.0001m, Reason = "Damaged" };
        var json = PrincipalGoodsReturnRules.ResponseJson(head, revision);
        Assert.Contains("\"acceptedFactQuantityExact\":\"0.0001\"", json);
        Assert.Contains("\"quantityExact\":\"0.0001\"", json);
        Assert.Contains("\"draftOnly\":true", json);
        Assert.Contains("\"stockChanged\":false", json);
        Assert.Contains("\"financeChanged\":false", json);
        Assert.DoesNotContain("returnedQuantity", json);
    }
}
