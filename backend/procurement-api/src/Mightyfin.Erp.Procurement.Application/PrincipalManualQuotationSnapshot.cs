using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>
/// Deterministic commercial shape of a buyer-entered Draft/Cancelled quotation.
/// This does not establish principal provenance, external contact, a supplier's
/// submission or authority to persist a version. No revision model is added here.
/// </summary>
public static class PrincipalManualQuotationSnapshot
{
    public static string Build(SupplierBid? bid, IReadOnlyList<SupplierBidLine>? lines)
    {
        PrincipalManualQuotationDraft.ValidateShape(bid, lines, allowCancelled: true);
        var quotation = bid!;
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 2,
            quotation.Id, quotation.TenantId, quotation.LegalEntityId, quotation.SourcingEventId,
            quotation.SupplierId, quotation.InvitationId, quotation.Status, quotation.SourceChannel,
            quotation.CurrencyCode, quotation.SurrogateEnteredBy, quotation.SubmissionVersion,
            receivedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(quotation.ReceivedAt),
            quotation.EvidenceDocumentRefId, quotation.CorrectionReason,
            lines = lines!.OrderBy(line => line.SourcingLineId).Select(line => new
            {
                line.Id, line.SourcingLineId,
                quantityExact = ProcurementEvidenceEncoding.Decimal(line.Quantity),
                unitPriceExact = ProcurementEvidenceEncoding.Decimal(line.UnitPrice),
                taxAmountExact = ProcurementEvidenceEncoding.Decimal(line.TaxAmount),
                deliveryAmountExact = ProcurementEvidenceEncoding.Decimal(line.DeliveryAmount),
                line.LeadTimeText, line.Notes,
            }),
        });
    }
}
