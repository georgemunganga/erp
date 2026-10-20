using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Acceptance completeness after the adapter loads ALL linked PO lines (including
/// inactive history), all legacy receipt/certificate rows and verified controlled ledgers.
/// Draft-intended service quantity is not accepted evidence. No missing binding means zero.</summary>
public static class ReceivingAcceptanceHistory
{
    public static bool IsVerified(IReadOnlyList<PurchaseOrderLine> allOrderLines, IReadOnlyList<GoodsReceiptLine> allReceiptLines,
        IReadOnlyList<ServiceAcceptance> allServices, IReadOnlyDictionary<Guid, decimal> provenQuantities,
        IReadOnlySet<Guid> verifiedStateIds, decimal requestAcceptedQuantity)
    {
        var lineIds = allOrderLines.Select(x => x.Id).ToHashSet();
        if (allReceiptLines.Any(row => !lineIds.Contains(row.PurchaseOrderLineId)) || allServices.Any(row => !lineIds.Contains(row.PurchaseOrderLineId))) return false;
        foreach (var row in allReceiptLines)
        {
            var consequential = row.AcceptedQuantity != 0 || row.PhysicalObservationId != null || row.Status is not ("Draft" or "Cancelled");
            if (row.ExecutionVersion != 1 && (row.ReceivedQuantity != 0 || consequential)) return false;
            if (row.ExecutionVersion == 1 && consequential && (row.ReceivingSourceStateId is not { } state || state == Guid.Empty
                || row.SourceStateBindingId != state || !verifiedStateIds.Contains(state))) return false;
        }
        foreach (var row in allServices)
        {
            if (row.ExecutionVersion != 1 && (row.AcceptedQuantity != 0 || row.AcceptedAmount != 0 || row.CertifiedAt != null
                || row.Status is not ("PendingCertification" or "Draft" or "Cancelled"))) return false;
            var consequential = row.CertifiedAt != null || row.CertificationEffectId != null || row.CurrentAcceptedQuantity != 0
                || row.CurrentAcceptedNetAmount != 0 || row.Status is not ("PendingCertification" or "Cancelled");
            if (row.ExecutionVersion == 1 && consequential && (row.ReceivingSourceStateId is not { } state || state == Guid.Empty
                || row.SourceStateBindingId != state || !verifiedStateIds.Contains(state))) return false;
        }
        return allOrderLines.All(row => row.AcceptedQuantity == provenQuantities.GetValueOrDefault(row.Id))
            && requestAcceptedQuantity == ExactDecimalArithmetic.Sum(allOrderLines.Select(x => x.AcceptedQuantity));
    }
}
