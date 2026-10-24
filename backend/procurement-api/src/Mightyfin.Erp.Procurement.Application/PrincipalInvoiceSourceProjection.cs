using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Read-only schema-2 acceptance projection for the existing invoice
/// comparison vocabulary. The caller must have verified the complete retained
/// issued order, owning source, fact receipts, policies and aggregate counters.
/// This projection does not grant comparison, billing capacity or AP authority.</summary>
public static class PrincipalInvoiceSourceProjection
{
    public static IReadOnlyList<InvoiceAcceptedFulfilment> Accepted(
        PrincipalReceivingSourceState state, PrincipalReceivingLedger ledger,
        PurchaseOrderLine orderLine)
    {
        var source = PrincipalReceivingStorageCodec.Source(state);
        var pin = source.Commercial;
        if (source.SchemaVersion != 2 || ledger.Source != source
            || orderLine.Id != pin.PurchaseOrderLineId || orderLine.PurchaseOrderId != pin.PurchaseOrderId
            || orderLine.TenantId != pin.TenantId || orderLine.LegalEntityId != pin.LegalEntityId
            || orderLine.UnitCode != pin.UnitCode || state.BranchId != pin.BranchId
            || state.IssuedVersionId != pin.IssuedVersionId || state.PurchaseOrderLineId != orderLine.Id)
            throw new InvalidOperationException("PrincipalInvoiceAcceptanceSourceChanged");
        var balance = PrincipalControlledReceiving.Balance(ledger);
        if (state.Sequence != ledger.Sequence || state.AcceptedQuantity != balance.AcceptedQuantity
            || state.AcceptedNetAmount != balance.AcceptedNetAmount
            || orderLine.AcceptedQuantity != balance.AcceptedQuantity)
            throw new InvalidOperationException("PrincipalInvoiceAcceptanceCounterChanged");
        var kind = pin.Kind switch
        {
            ReceivingKind.Goods => InvoiceAcceptanceKind.Goods,
            ReceivingKind.Service => InvoiceAcceptanceKind.Service,
            _ => throw new InvalidOperationException("PrincipalInvoiceAcceptanceKindUnknown")
        };
        if (ledger.Acceptances.Select(x => x.Id).Distinct().Count() != ledger.Acceptances.Count)
            throw new InvalidOperationException("PrincipalInvoiceAcceptanceIdentityRepeated");
        return ledger.Acceptances.OrderBy(x => x.Sequence).Select(x =>
        {
            if (x.Source != source || x.Quantity <= 0 || x.NetAmount < 0
                || x.AcceptedAt == default || x.Sequence < 1)
                throw new InvalidOperationException("PrincipalInvoiceAcceptanceFactInvalid");
            return new InvoiceAcceptedFulfilment(x.Id, x.Sequence, pin.TenantId, pin.LegalEntityId,
                pin.BranchId, pin.PurchaseOrderId, pin.IssuedVersionId, pin.PurchaseOrderLineId,
                pin.CurrencyCode, pin.UnitCode, kind, "Accepted",
                ProcurementEvidenceEncoding.Digest(PrincipalReceivingStorageCodec.Encode(x)),
                x.AcceptedAt, false, x.Quantity, 0, x.NetAmount, 0);
        }).ToArray();
    }
}
