using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Draft-only exact quantity and immutable revision digest. Not return approval authority.</summary>
public static class PrincipalGoodsReturnRules
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static decimal Quantity(string value, decimal acceptedFactQuantity)
    {
        var parsed = ExactDecimalJsonCodec.Parse(value, 18, 4, "ReturnQuantity");
        if (parsed <= 0 || acceptedFactQuantity <= 0 || parsed > acceptedFactQuantity)
            throw new ArgumentException("Return quantity must be positive and no greater than the selected accepted goods fact.");
        return parsed;
    }

    public static string Fingerprint(Guid draftId, long revision, decimal quantity,
        string reason, string? evidenceReference, string state)
    {
        if (draftId == Guid.Empty || revision < 1 || quantity <= 0 ||
            state is not ("Draft" or "Cancelled") || string.IsNullOrWhiteSpace(reason) || reason != reason.Trim() ||
            reason.Length > 2000 || evidenceReference?.Length > 300)
            throw new ArgumentException("Return draft revision is invalid.");
        return ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, draftId, revision, quantity = quantity.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
            reason, evidenceReference, state
        }, Json));
    }

    public static string ResponseJson(PrincipalGoodsReturnDraft head, PrincipalGoodsReturnRevision revision)
        => JsonSerializer.Serialize(new
        {
            id = head.Id, status = head.State, version = head.Revision, branchId = head.BranchId,
            receiptId = head.ReceiptId, acceptanceFactId = head.AcceptanceFactId,
            purchaseOrderId = head.PurchaseOrderId, purchaseOrderLineId = head.PurchaseOrderLineId,
            acceptedFactQuantityExact = head.AcceptedFactQuantity.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
            quantityExact = revision.Quantity.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
            revision.Reason, revision.EvidenceReference,
            draftOnly = true, stockChanged = false, financeChanged = false, supplierNotified = false
        }, Json);
}
