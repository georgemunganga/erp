using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Api;
public static partial class ProcurementReceivingRoutes
{
    /// <summary>Read-only immutable issued authority for invoice unit review. Intentionally
    /// no upstream row locks, acceptance/capacity projection or receiving-state creation.
    /// The caller already owns invoice→case ordering under the shared branch fence.</summary>
    internal static async Task<InvoiceIssuedOrder> InvoiceReviewIssuedOrder(ProcurementDbContext db, ApprovalDbContext cases,
        ProcurementRequestScope scope, Guid orderId, Guid issuedId, CancellationToken ct)
    {
        var ids = await db.PurchaseOrderLines.AsNoTracking().Where(x => x.PurchaseOrderId == orderId && x.IsActive).OrderBy(x => x.LineNumber).Select(x => x.Id).ToArrayAsync(ct);
        if (ids.Length is < 1 or > 200) throw new InvalidOperationException("InvoiceUnitReviewIssuedLinesRequired");
        var loads = new List<SourceLoad>();
        foreach (var id in ids) loads.Add(await Source(db, cases, scope, orderId, issuedId, id, ct, false));
        var first = loads[0];
        return new(orderId, issuedId, first.Issued.RevisionNumber, first.Order.Version, first.Order.TenantId, first.Order.LegalEntityId,
            first.Order.BranchRef, first.Order.SupplierId, first.Order.CurrencyCode, "Issued", first.Issued.CommercialSnapshotSha256,
            first.Delivery.ObservedAt, loads.Select(x => new InvoiceIssuedOrderLine(x.Line.Id, x.Line.Version, x.Category, x.Line.UnitCode, x.Line.Quantity, x.Line.UnitPrice, x.Line.TaxAmount)).ToArray());
    }
}
