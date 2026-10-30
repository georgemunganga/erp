using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>
/// Pure input validation for a buyer-entered quotation draft. This is not Core
/// authorization, retained publication/contact verification or supplier submission.
/// The future owning transaction must independently load and lock that evidence.
/// No employee link is required and no record is changed by these checks.
/// </summary>
public static class PrincipalManualQuotationDraft
{
    public static void Validate(SourcingEvent? sourcing, SourcingInvitation? invitation,
        SupplierBid? bid, IReadOnlyList<SourcingLine>? sourceLines,
        IReadOnlyList<SupplierBidLine>? quoteLines, string? actorPrincipalId)
    {
        if (!ProcurementPrincipalEvidence.Canonical(actorPrincipalId)
            || sourcing is null || sourcing.Id == Guid.Empty || sourcing.ExecutionVersion != 2
            || sourcing.Status != "Published" || !Scope(sourcing)
            || !Currency(sourcing.CurrencyCode)
            || !ProcurementPrincipalEvidence.Canonical(sourcing.OwnerSubjectId)
            || !ProcurementPrincipalEvidence.OptionalWorker(sourcing.MakerWorkerId)
            || invitation is null || invitation.Id == Guid.Empty || invitation.SupplierId == Guid.Empty
            || invitation.SourcingEventId != sourcing.Id || invitation.Status != "ManualContactRecorded"
            || !SameScope(invitation, sourcing)
            || bid is null || bid.Status != "Draft" || bid.SurrogateEnteredBy != actorPrincipalId
            || bid.SourcingEventId != sourcing.Id || bid.SupplierId != invitation.SupplierId
            || bid.InvitationId != invitation.Id || bid.CurrencyCode != sourcing.CurrencyCode
            || !SameScope(bid, sourcing))
            throw new InvalidOperationException("ManualQuotationDraftInvalid");

        ValidateShape(bid, quoteLines, allowCancelled: false);
        // Validate every supplied source before dictionary construction. Do not
        // silently drop foreign/inactive rows or dereference a null element.
        if (sourceLines is null || sourceLines.Count is < 1 or > 200
            || sourceLines.Any(line => line is null)
            || sourceLines.Any(line => line.Id == Guid.Empty || line.SourcingEventId != sourcing.Id
                || !SameScope(line, sourcing) || line.Status != "Active" || line.Quantity <= 0
                || !ExactDecimalJsonCodec.IsRepresentable(line.Quantity, 18, 4))
            || sourceLines.Select(line => line.Id).Distinct().Count() != sourceLines.Count)
            throw new InvalidOperationException("ManualQuotationSourceInvalid");

        var available = sourceLines.ToDictionary(line => line.Id);
        foreach (var line in quoteLines!)
        {
            if (!available.TryGetValue(line.SourcingLineId, out var source)
                || line.Quantity > source.Quantity)
                throw new InvalidOperationException("ManualQuotationLineInvalid");
        }
    }

    // Reusable structural checks for an already retained Draft/Cancelled
    // snapshot. They confer no legacy-row provenance or transition authority.
    internal static void ValidateShape(SupplierBid? bid, IReadOnlyList<SupplierBidLine>? lines,
        bool allowCancelled)
    {
        if (bid is null || !Scope(bid) || bid.Id == Guid.Empty
            || bid.SourcingEventId == Guid.Empty || bid.SupplierId == Guid.Empty
            || bid.InvitationId is null || bid.InvitationId == Guid.Empty
            || bid.Status != "Draft" && !(allowCancelled && bid.Status == "Cancelled")
            || bid.SourceChannel != "Manual" || bid.SubmissionVersion != 1 || bid.Version < 1
            || bid.SubmittedAt is not null || !Currency(bid.CurrencyCode)
            || !ProcurementPrincipalEvidence.Canonical(bid.SurrogateEnteredBy)
            || bid.EvidenceDocumentRefId == Guid.Empty
            // Optional observed receipt time is separate from the external
            // contact record; absence is valid, an explicit zero date is not.
            || bid.ReceivedAt == default(DateTimeOffset)
            || lines is null || lines.Count is < 1 or > 200 || lines.Any(line => line is null))
            throw new InvalidOperationException("ManualQuotationDraftInvalid");

        if (lines.Select(line => line.Id).Distinct().Count() != lines.Count
            || lines.Select(line => line.SourcingLineId).Distinct().Count() != lines.Count
            || lines.Any(line => line.Id == Guid.Empty || line.SourcingLineId == Guid.Empty
                || line.SupplierBidId != bid.Id || !SameScope(line, bid)
                || line.Quantity <= 0 || line.UnitPrice < 0 || line.TaxAmount < 0 || line.DeliveryAmount < 0
                || !ExactDecimalJsonCodec.IsRepresentable(line.Quantity, 18, 4)
                || !ExactDecimalJsonCodec.IsRepresentable(line.UnitPrice, 18, 4)
                || !ExactDecimalJsonCodec.IsRepresentable(line.TaxAmount, 18, 4)
                || !ExactDecimalJsonCodec.IsRepresentable(line.DeliveryAmount, 18, 4)))
            throw new InvalidOperationException("ManualQuotationLineInvalid");

        try
        {
            // Use shared scaled-integer arithmetic, never rounded CLR decimal
            // multiplication. Its exact CLR subset does not widen numeric(38,8).
            var total = ExactDecimalArithmetic.Sum(lines.Select(line =>
                ExactDecimalArithmetic.Add(ExactDecimalArithmetic.Add(
                    ExactDecimalArithmetic.Multiply(line.Quantity, line.UnitPrice), line.TaxAmount),
                    line.DeliveryAmount)));
            if (!ExactDecimalJsonCodec.IsRepresentable(total, 38, 8))
                throw new InvalidOperationException("ManualQuotationTotalInvalid");
        }
        catch (InvalidOperationException error) when (error.Message == "ExactDecimalNotRepresentable")
        {
            throw new InvalidOperationException("ManualQuotationTotalInvalid", error);
        }
    }

    private static bool Scope(ProcurementRecord record) =>
        !string.IsNullOrWhiteSpace(record.TenantId) && record.TenantId.Length <= 128
        && record.LegalEntityId != Guid.Empty;

    private static bool SameScope(ProcurementRecord record, ProcurementRecord owner) =>
        record.TenantId == owner.TenantId && record.LegalEntityId == owner.LegalEntityId;

    private static bool Currency(string? code) => code is { Length: 3 }
        && code.All(character => character is >= 'A' and <= 'Z');
}
