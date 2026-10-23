using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record RfqPublicationPreparation(string DraftFingerprint, string SnapshotSha256,
    IReadOnlyList<Guid> SupplierIds, int RequiredQuoteCount, decimal SelectedOriginalAmount,
    IReadOnlyList<string> OutstandingControls)
{
    // No proposal is an approval, published RFQ, invitation delivery or supplier response.
    public bool CanPublish => false;
}

/// <summary>Pure bounded preparation for a future principal/Core publication transaction.
/// The adapter must reread suppliers with current qualifications, the published SOURCING policy, source holds and
/// current named Core reviewers under one owner transaction before persisting a snapshot.</summary>
public static class PrincipalRfqPublicationPreparation
{
    public static RfqPublicationPreparation Prepare(RfqPrincipalDraft draft,
        SourcingPublicationControls controls, IReadOnlyList<Supplier> candidates,
        DateTimeOffset observedAt)
    {
        if (draft is null || draft.State != "Draft" || draft.Id == Guid.Empty
            || string.IsNullOrWhiteSpace(draft.TenantId) || draft.CompanyId == Guid.Empty
            || !ProcurementPrincipalEvidence.Canonical(draft.MakerPrincipalId)
            || draft.OriginalFingerprint is not { Length: 64 }
            || draft.Lines is null || draft.Lines.Count is < 1 or > 200
            || observedAt == default || controls is null
            || controls.MinimumQuoteCount is < 0 or > 20
            || controls.MinimumInvitedSuppliers is < 0 or > 100
            || candidates is null || candidates.Count is < 1 or > 100)
            throw new InvalidOperationException("RfqPublicationPreparationInvalid");
        var ids = candidates.Select(x => x.Id).ToArray();
        if (ids.Distinct().Count() != ids.Length || ids.Any(x => x == Guid.Empty)
            || candidates.Count < controls.MinimumInvitedSuppliers
            || candidates.Count < controls.MinimumQuoteCount
            || candidates.Any(x => x.TenantId != draft.TenantId || x.LegalEntityId != draft.CompanyId
                || !SupplierEligibility.CanReceiveNewOrders(x, observedAt)))
            throw new InvalidOperationException("RfqSupplierShortlistUnavailable");
        var lines = draft.Lines.OrderBy(x => x.EventLineId).ToArray();
        if (lines.Select(x => x.EventLineId).Distinct().Count() != lines.Length
            || lines.Any(x => x.EventLineId == Guid.Empty || x.AllocationId == Guid.Empty
                || x.Quantity <= 0 || x.UnitEstimate < 0
                || !ExactDecimalJsonCodec.IsRepresentable(x.Quantity, 18, 4)
                || !ExactDecimalJsonCodec.IsRepresentable(x.UnitEstimate, 18, 4)
                || x.EstimatedValue != ExactDecimalArithmetic.Multiply(x.Quantity, x.UnitEstimate)))
            throw new InvalidOperationException("RfqPublicationSourceInvalid");
        var amount = ExactDecimalArithmetic.Sum(lines.Select(x => x.EstimatedValue));
        if (amount <= 0 || !ExactDecimalJsonCodec.IsRepresentable(amount, 38, 8))
            throw new InvalidOperationException("RfqPublicationAmountInvalid");
        var sortedIds = ids.Order().ToArray();
        var snapshotSha = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, draft.Id, draft.TenantId, draft.CompanyId, draft.BranchId,
            draft.CurrencyCode, draft.OriginalFingerprint,
            selectedOriginalAmount = ProcurementEvidenceEncoding.Decimal(amount),
            minimumQuoteCount = controls.MinimumQuoteCount,
            minimumInvitedSuppliers = controls.MinimumInvitedSuppliers,
            supplierIds = sortedIds,
            sourceLines = lines.Select(x => new { x.EventLineId, x.LineId, x.AllocationId,
                quantity = ProcurementEvidenceEncoding.Decimal(x.Quantity),
                estimatedValue = ProcurementEvidenceEncoding.Decimal(x.EstimatedValue) })
        }));
        return new(draft.OriginalFingerprint, snapshotSha, Array.AsReadOnly(sortedIds),
            controls.MinimumQuoteCount, amount,
            ["PublishedSourcingPolicyAndIdentityRequired", "IndependentCoreReviewRequired",
                "CurrentSourceAndSupplierRecheckRequired", "PublicationSnapshotPersistenceRequired",
                "ManualSupplierContactIsNotDelivery"]);
    }
}

/// <summary>Pure quotation DRAFT shape over existing SupplierBid/SupplierBidLine records.
/// There is deliberately no Submit, evaluation, award or PO operation here.</summary>
public static class PrincipalManualQuotationDraft
{
    public static void Validate(SourcingEvent sourcing, SourcingInvitation invitation,
        SupplierBid bid, IReadOnlyList<SourcingLine> sourceLines,
        IReadOnlyList<SupplierBidLine> quoteLines, string actorPrincipalId)
    {
        if (!ProcurementPrincipalEvidence.Canonical(actorPrincipalId)
            || sourcing is null || sourcing.ExecutionVersion != 2 || sourcing.Status != "Open"
            || invitation is null || invitation.SourcingEventId != sourcing.Id
            || invitation.SupplierId == Guid.Empty || invitation.Status != "ManualContactRecorded"
            || bid is null || bid.Status != "Draft" || bid.SourceChannel != "Manual"
            || bid.SubmissionVersion != 1 || bid.Version < 1
            || bid.SubmittedAt is not null || bid.SurrogateEnteredBy != actorPrincipalId
            || bid.SourcingEventId != sourcing.Id || bid.SupplierId != invitation.SupplierId
            || bid.InvitationId != invitation.Id || bid.CurrencyCode != sourcing.CurrencyCode
            || bid.TenantId != sourcing.TenantId || bid.LegalEntityId != sourcing.LegalEntityId
            || invitation.TenantId != sourcing.TenantId || invitation.LegalEntityId != sourcing.LegalEntityId
            || sourceLines is null || sourceLines.Count is < 1 or > 200
            || quoteLines is null || quoteLines.Count is < 1 or > 200
            || quoteLines.Select(x => x.SourcingLineId).Distinct().Count() != quoteLines.Count)
            throw new InvalidOperationException("ManualQuotationDraftInvalid");
        var available = sourceLines.Where(x => x.SourcingEventId == sourcing.Id && x.Status == "Active"
            && x.TenantId == sourcing.TenantId && x.LegalEntityId == sourcing.LegalEntityId)
            .ToDictionary(x => x.Id);
        foreach (var line in quoteLines)
        {
            if (!available.TryGetValue(line.SourcingLineId, out var source)
                || line.SupplierBidId != bid.Id || line.TenantId != bid.TenantId
                || line.LegalEntityId != bid.LegalEntityId || line.Quantity <= 0
                || line.Quantity > source.Quantity || line.UnitPrice < 0 || line.TaxAmount < 0
                || line.DeliveryAmount < 0
                || !ExactDecimalJsonCodec.IsRepresentable(line.Quantity, 18, 4)
                || !ExactDecimalJsonCodec.IsRepresentable(line.UnitPrice, 18, 4)
                || !ExactDecimalJsonCodec.IsRepresentable(line.TaxAmount, 18, 4)
                || !ExactDecimalJsonCodec.IsRepresentable(line.DeliveryAmount, 18, 4))
                throw new InvalidOperationException("ManualQuotationLineInvalid");
        }
        var total = ExactDecimalArithmetic.Sum(quoteLines.Select(x =>
            ExactDecimalArithmetic.Add(ExactDecimalArithmetic.Add(
                ExactDecimalArithmetic.Multiply(x.Quantity, x.UnitPrice), x.TaxAmount), x.DeliveryAmount)));
        if (!ExactDecimalJsonCodec.IsRepresentable(total, 38, 8))
            throw new InvalidOperationException("ManualQuotationTotalInvalid");
    }
}
