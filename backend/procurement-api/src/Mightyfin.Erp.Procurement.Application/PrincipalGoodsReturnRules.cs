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
            reason.Length > 2000 || evidenceReference?.Length > 300 ||
            !ExactDecimalJsonCodec.IsRepresentable(quantity, 18, 4))
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
            id = head.Id, status = revision.State, version = revision.Revision, branchId = head.BranchId,
            receiptId = head.ReceiptId, acceptanceFactId = head.AcceptanceFactId,
            purchaseOrderId = head.PurchaseOrderId, purchaseOrderLineId = head.PurchaseOrderLineId,
            acceptedFactQuantityExact = head.AcceptedFactQuantity.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
            quantityExact = revision.Quantity.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
            revision.Reason, revision.EvidenceReference,
            draftOnly = true, stockChanged = false, financeChanged = false, supplierNotified = false
        }, Json);

    /// <summary>Revalidates the complete original acknowledgement, including its
    /// transport status; a self-hashed body alone is not the original receipt.</summary>
    public static bool MatchesReceipt(PrincipalGoodsReturnDraft head,
        PrincipalGoodsReturnRevision revision, PrincipalGoodsReturnCommandReceipt receipt)
    {
        var operation = revision.Revision == 1 ? "Create" :
            revision.State == "Cancelled" ? "Cancel" : "Revise";
        return revision.DraftId == head.Id && revision.TenantId == head.TenantId &&
            revision.LegalEntityId == head.LegalEntityId && revision.ActorPrincipalId == head.CreatorPrincipalId &&
            receipt.TenantId == head.TenantId && receipt.LegalEntityId == head.LegalEntityId &&
            receipt.DraftId == head.Id && receipt.BranchId == head.BranchId &&
            receipt.CommandKey == revision.CommandKey && receipt.ResultRevision == revision.Revision &&
            receipt.ActorPrincipalId == revision.ActorPrincipalId && receipt.Operation == operation &&
            receipt.ResponseStatus == (operation == "Create" ? 201 : 200) &&
            receipt.ResponseJson == ResponseJson(head, revision) &&
            receipt.ResponseSha256 == ProcurementEvidenceEncoding.Digest(receipt.ResponseJson) &&
            MatchesRequest(head, revision, receipt);
    }

    public static bool MatchesRequest(PrincipalGoodsReturnDraft head, PrincipalGoodsReturnRevision revision,
        PrincipalGoodsReturnCommandReceipt receipt)
    {
        try
        {
            using var document = JsonDocument.Parse(receipt.RequestJson);
            var root = document.RootElement;
            if (root.GetProperty("operation").GetString() != receipt.Operation ||
                receipt.RequestSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(receipt.RequestJson)) return false;
            if (receipt.Operation != "Create" && root.GetProperty("id").GetGuid() != head.Id) return false;
            var input = root.GetProperty("input");
            if (receipt.Operation == "Create" && input.GetProperty("acceptanceFactId").GetGuid() != head.AcceptanceFactId ||
                receipt.Operation != "Create" && input.GetProperty("expectedRevision").GetInt64() != revision.Revision - 1)
                return false;
            if (input.GetProperty("reason").GetString()?.Trim() != revision.Reason) return false;
            if (receipt.Operation == "Cancel") return revision.State == "Cancelled";
            return Quantity(input.GetProperty("quantity").GetString()!, head.AcceptedFactQuantity) == revision.Quantity &&
                (input.GetProperty("evidenceReference").ValueKind == JsonValueKind.Null ? null :
                    input.GetProperty("evidenceReference").GetString()?.Trim()) == revision.EvidenceReference;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or
            ArgumentException or OverflowException) { return false; }
    }

    public static bool CancellationPreservesPrior(PrincipalGoodsReturnRevision prior,
        PrincipalGoodsReturnRevision cancellation) => prior.DraftId == cancellation.DraftId &&
        prior.Revision + 1 == cancellation.Revision && prior.State == "Draft" &&
        cancellation.State == "Cancelled" && prior.Quantity == cancellation.Quantity &&
        prior.EvidenceReference == cancellation.EvidenceReference;

    /// <summary>One complete proposal history. No return approval or dispatch is inferred.</summary>
    public static bool HistoryValid(PrincipalGoodsReturnDraft head,
        IReadOnlyList<PrincipalGoodsReturnRevision> revisions,
        IReadOnlyList<PrincipalGoodsReturnCommandReceipt> receipts)
    {
        if (head.Revision < 1 || head.State is not ("Draft" or "Cancelled") ||
            revisions.Count != head.Revision || receipts.Count != revisions.Count ||
            revisions.Select(x => x.Id).Distinct().Count() != revisions.Count ||
            revisions.Select(x => x.CommandKey).Distinct().Count() != revisions.Count ||
            receipts.Select(x => x.CommandKey).Distinct().Count() != receipts.Count) return false;
        var ordered = revisions.OrderBy(x => x.Revision).ToArray();
        if (ordered[^1].Id != head.CurrentRevisionId || ordered[^1].State != head.State) return false;
        try
        {
            for (var index = 0; index < ordered.Length; index++)
            {
                var revision = ordered[index];
                if (revision.Revision != index + 1 || index < ordered.Length - 1 && revision.State != "Draft" ||
                    revision.Quantity > head.AcceptedFactQuantity ||
                    revision.ProposalSha256 != Fingerprint(revision.DraftId, revision.Revision,
                        revision.Quantity, revision.Reason, revision.EvidenceReference, revision.State) ||
                    index > 0 && revision.State == "Cancelled" && !CancellationPreservesPrior(ordered[index - 1], revision)) return false;
                var receipt = receipts.SingleOrDefault(x => x.CommandKey == revision.CommandKey);
                if (receipt is null || !MatchesReceipt(head, revision, receipt)) return false;
            }
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException) { return false; }
        return true;
    }

    public static string OutboxPayloadJson(Guid draftId, string operation, Guid commandKey,
        long revision, string actorPrincipalId) => JsonSerializer.Serialize(new
        {
            id = draftId, operation, commandKey, revision, actorPrincipalId,
            approved = false, dispatched = false, stockChanged = false, financeChanged = false
        }, Json);
}
