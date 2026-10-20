using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementReceivingRoutes
{
    // The owner SQL fence must run before selector counts/pages, not only in row verification.
    internal static IQueryable<PurchaseOrder> InvoiceBoundIssuedOrders(ProcurementDbContext db,ProcurementRequestScope scope)
        => BoundIssuedOrders(db,scope);
    internal sealed record InvoiceSelectorSource(string UnitCode,string Quantity,string UnitPrice,string TaxAmount,string CategoryCode);
    internal static async Task<InvoiceSelectorSource> InvoiceSelectorLine(ProcurementDbContext db,ApprovalDbContext cases,
        ProcurementRequestScope scope,Guid orderId,Guid issuedId,Guid lineId,CancellationToken ct)
    {
        if(await db.PurchaseOrderLines.Where(x=>x.PurchaseOrderId==orderId && x.IsActive).Take(201).CountAsync(ct)>200)
            throw new InvalidOperationException("InvoiceSourceSelectorBoundExceeded");
        try
        {
            var source=await Source(db,cases,scope,orderId,issuedId,lineId,ct,lockSource:false);
            return new(source.Line.UnitCode,Exact(source.Line.Quantity),Exact(source.Line.UnitPrice),Exact(source.Line.TaxAmount),source.Category);
        }
        catch(ReceivingProblem error) { throw new InvalidOperationException("InvoiceSourceEvidenceUnavailable",error); }
    }
    internal static async Task RequireInvoiceSelectorHistoryBound(ProcurementDbContext db,Guid orderId,CancellationToken ct)
    {
        var requestLines=db.PurchaseOrderLines.Where(x=>x.PurchaseOrderId==orderId && x.IsActive && x.PurchaseRequestLineId!=null).Select(x=>x.PurchaseRequestLineId!.Value);
        var related=db.PurchaseOrderLines.Where(x=>x.PurchaseRequestLineId!=null && requestLines.Contains(x.PurchaseRequestLineId.Value)).Select(x=>x.Id);
        if(await related.Take(501).CountAsync(ct)>500) throw new InvalidOperationException("InvoiceHistorySelectorBoundExceeded");
        var states=db.ReceivingSourceStates.Where(x=>related.Contains(x.PurchaseOrderLineId)).Select(x=>x.Id);
        if(await states.Take(501).CountAsync(ct)>500
            || await db.ReceivingAcceptanceEffects.Where(x=>states.Contains(x.ReceivingSourceStateId)).Take(2001).CountAsync(ct)>2000
            || await db.GoodsReceiptLines.Where(x=>related.Contains(x.PurchaseOrderLineId)).Take(2001).CountAsync(ct)>2000
            || await db.ServiceAcceptances.Where(x=>related.Contains(x.PurchaseOrderLineId)).Take(2001).CountAsync(ct)>2000)
            throw new InvalidOperationException("InvoiceHistorySelectorBoundExceeded");
    }
}
