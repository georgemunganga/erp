using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Reconcile complete retained successor histories to owning counters.
/// Callers load every PO line, including inactive lines, reject unknown legacy
/// history and verify original source/receipt authority before this calculation.</summary>
public static class PrincipalReceivingCounterReconciliation
{
    public static void Verify(PurchaseRequestLine request,IReadOnlyList<PurchaseOrderLine> lines,IReadOnlyList<PrincipalReceivingLedger> ledgers)
    {
        if(request.Id==Guid.Empty||lines.Any(x=>x.PurchaseRequestLineId!=request.Id||x.TenantId!=request.TenantId||x.LegalEntityId!=request.LegalEntityId)
            ||lines.Select(x=>x.Id).Distinct().Count()!=lines.Count
            ||ledgers.Select(x=>x.Source.Commercial.PurchaseOrderLineId).Distinct().Count()!=ledgers.Count)
            throw new InvalidOperationException("PrincipalReceivingAggregateSourceUnknown");
        var accepted=new Dictionary<Guid,decimal>();
        foreach(var ledger in ledgers)
        {
            var source=ledger.Source.Commercial;var line=lines.SingleOrDefault(x=>x.Id==source.PurchaseOrderLineId);
            if(source.PurchaseRequestLineId!=request.Id||source.TenantId!=request.TenantId||source.LegalEntityId!=request.LegalEntityId
                ||line is null||line.PurchaseOrderId!=source.PurchaseOrderId)
                throw new InvalidOperationException("PrincipalReceivingAggregateSourceUnknown");
            accepted.Add(line.Id,PrincipalControlledReceiving.Balance(ledger).AcceptedQuantity);
        }
        foreach(var line in lines)
            if(line.AcceptedQuantity!=accepted.GetValueOrDefault(line.Id))
                throw new InvalidOperationException("PrincipalReceivingOrderCounterDiverged");
        var aggregate=ExactDecimalArithmetic.Sum(accepted.Values);
        if(request.AcceptedQuantity!=aggregate||aggregate<0||aggregate>request.ApprovedQuantity)
            throw new InvalidOperationException("PrincipalReceivingRequestCounterDiverged");
    }
}
