using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Canonical retained buyer Draft, including every quoted line and exact
/// decimal text. This is not an external bid submission or supplier acceptance.</summary>
public static class PrincipalManualQuotationSnapshot
{
    public static string Build(SupplierBid bid, IReadOnlyList<SupplierBidLine> lines)
    {
        if (bid is null || lines is null || bid.SubmissionVersion != 1
            || bid.Status is not ("Draft" or "Cancelled") || bid.SourceChannel != "Manual"
            || lines.Count is < 1 or > 200 || lines.Select(x => x.SourcingLineId).Distinct().Count() != lines.Count)
            throw new InvalidOperationException("ManualQuotationSnapshotInvalid");
        // PostgreSQL numeric(18,4) restores trailing zeroes that the input
        // codec removes. Evidence must bind the exact value, not its scale.
        static string Exact(decimal value) => ProcurementEvidenceEncoding.Decimal(value);
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 2, bid.Id, bid.SourcingEventId, bid.SupplierId, bid.InvitationId,
            bid.Status, bid.SourceChannel, bid.CurrencyCode, bid.SurrogateEnteredBy,
            lines = lines.OrderBy(x => x.SourcingLineId).Select(x => new
            {
                x.SourcingLineId,
                quantityExact = Exact(x.Quantity), unitPriceExact = Exact(x.UnitPrice),
                taxAmountExact = Exact(x.TaxAmount), deliveryAmountExact = Exact(x.DeliveryAmount),
                x.LeadTimeText, x.Notes,
            }),
        });
    }
}
