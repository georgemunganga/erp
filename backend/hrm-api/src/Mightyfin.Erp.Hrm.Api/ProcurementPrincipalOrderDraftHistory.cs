using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Shared retained reader for original P30 orders and P33 edited Drafts.
/// It never infers a missing origin or a missing revision from current values.</summary>
internal static class ProcurementPrincipalOrderDraftHistory
{
    internal sealed record Effective(decimal Quantity, decimal UnitPrice, decimal TaxAmount, decimal EstimatedValue);

    internal static async Task<IReadOnlyDictionary<Guid,Effective>> VerifyAsync(ProcurementDbContext db,
        PurchaseOrder order, IReadOnlyCollection<PurchaseOrderLine> lines,
        IReadOnlyCollection<PrincipalOrderSourceBinding> bindings, CancellationToken ct)
    {
        var origin=await db.PrincipalOrderCommercialOrigins.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.PurchaseOrderId==order.Id,ct);
        var revisions=await db.PrincipalOrderDraftCommercialRevisions.AsNoTracking()
            .Where(x=>x.PurchaseOrderId==order.Id).OrderBy(x=>x.RevisionNumber).ToArrayAsync(ct);
        var receipts=await db.PrincipalOrderDraftReceipts.AsNoTracking()
            .Where(x=>x.PurchaseOrderId==order.Id&&x.Operation=="Edit").ToArrayAsync(ct);
        PrincipalOrderCommercialStates.VerifyReceipts(order,origin,revisions,receipts);
        if(origin is null)
        {
            if(revisions.Length!=0)throw new InvalidOperationException("PrincipalOrderDraftOriginMissing");
            PrincipalOrderDraftPins.Verify(order,lines,bindings);
            return bindings.ToDictionary(x=>x.Id,x=>new Effective(x.Quantity,x.ProposedUnitPrice,
                lines.Single(line=>line.Id==x.PurchaseOrderLineId).TaxAmount,x.EstimatedValue));
        }
        PrincipalOrderCommercialStates.VerifyOrigin(origin,order,bindings);
        if(revisions.Length==0)
        {
            PrincipalOrderDraftPins.Verify(order,lines,bindings);
            return bindings.ToDictionary(x=>x.Id,x=>new Effective(x.Quantity,x.ProposedUnitPrice,
                lines.Single(line=>line.Id==x.PurchaseOrderLineId).TaxAmount,x.EstimatedValue));
        }
        var slices=await db.PrincipalOrderEffectiveSourceSlices.AsNoTracking()
            .Where(x=>x.PurchaseOrderId==order.Id).ToArrayAsync(ct);
        var allocationIds=bindings.Select(x=>x.AllocationId).ToArray();
        var allocations=await db.PurchaseRequestAllocations.AsNoTracking()
            .Where(x=>allocationIds.Contains(x.Id)).ToArrayAsync(ct);
        PrincipalOrderCommercialStates.VerifyRevision(order,lines,origin,revisions,slices,bindings,allocations);
        var latest=revisions[^1];
        return slices.Where(x=>x.RevisionId==latest.Id).ToDictionary(x=>x.SourceBindingId,
            x=>new Effective(x.Quantity,x.UnitPrice,x.TaxAmount,x.EstimatedValue));
    }
}
