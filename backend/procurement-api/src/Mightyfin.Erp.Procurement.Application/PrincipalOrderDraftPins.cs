using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Version 2 internal draft identity. The digest covers only original, immutable
/// commercial/source facts. Cancel changes status and releases the reservation, never this pin.</summary>
public static class PrincipalOrderDraftPins
{
    public static string Digest(PurchaseOrder order,IReadOnlyCollection<PurchaseOrderLine> lines,
        IReadOnlyCollection<PrincipalOrderSourceBinding> bindings)
    {
        if(order.PrincipalDraftSchemaVersion!=2 || lines.Count is <1 or >100 || bindings.Count!=lines.Count
            || order.BranchRef is null || order.BranchRef==Guid.Empty
            || lines.Select(x=>x.Id).Distinct().Count()!=lines.Count
            || bindings.Select(x=>x.PurchaseRequestLineId).Distinct().Count()!=bindings.Count
            || bindings.Select(x=>x.AllocationId).Distinct().Count()!=bindings.Count)
            throw new InvalidOperationException("PrincipalOrderOriginalSourceInvalid");
        var items=bindings.OrderBy(x=>x.PurchaseOrderLineId).Select(pin=>
        {
            var line=lines.SingleOrDefault(x=>x.Id==pin.PurchaseOrderLineId);
            if(line is null || line.PurchaseOrderId!=order.Id || line.PurchaseRequestLineId!=pin.PurchaseRequestLineId
                || pin.PurchaseOrderId!=order.Id || pin.BuyerPrincipalId!=order.BuyerSubjectId
                || pin.TenantId!=order.TenantId || pin.LegalEntityId!=order.LegalEntityId
                || pin.BranchId!=order.BranchRef || pin.CurrencyCode!=order.CurrencyCode
                || pin.Quantity!=line.Quantity || pin.ProposedUnitPrice!=line.UnitPrice
                || pin.EstimatedValue!=ExactDecimalArithmetic.Multiply(pin.Quantity,line.UnitPrice)
                || !line.IsActive || pin.SchemaVersion!=2)
                throw new InvalidOperationException("PrincipalOrderOriginalSourceInvalid");
            return new{pin.TenantId,pin.LegalEntityId,pin.PurchaseOrderLineId,pin.AllocationId,
                pin.PurchaseRequestId,pin.PurchaseRequestLineId,
                pin.RequestVersion,pin.SubmissionId,pin.SubmittedBusinessVersion,pin.ApprovalOutcomeId,
                pin.CoreApprovalCaseId,pin.CoreDecisionId,pin.FinanceSnapshotId,pin.MethodId,pin.MethodRevision,
                pin.MethodCode,pin.RequestSnapshotSha256,pin.PolicyDecisionSha256,pin.PolicyRulesSha256,
                pin.FinanceSnapshotSha256,pin.PrincipalIdentitySha256,pin.BuyerPrincipalId,
                pin.BuyerWorkerAliasId,pin.RequesterPrincipalId,pin.RequesterWorkerAliasId,pin.BranchId,
                quantity=ProcurementEvidenceEncoding.Decimal(pin.Quantity),
                unitEstimate=ProcurementEvidenceEncoding.Decimal(pin.UnitEstimate),
                proposedUnitPrice=ProcurementEvidenceEncoding.Decimal(pin.ProposedUnitPrice),
                estimatedValue=ProcurementEvidenceEncoding.Decimal(pin.EstimatedValue),
                pin.CurrencyCode,line.LineNumber,line.Description,line.UnitCode,
                unitPrice=ProcurementEvidenceEncoding.Decimal(line.UnitPrice),
                taxAmount=ProcurementEvidenceEncoding.Decimal(line.TaxAmount),
                line.CostCenterRef,line.ProjectRef,line.ExpectedDeliveryOn};
        }).ToArray();
        return ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new{schemaVersion=2,
            order.Id,order.SupplierId,order.CurrencyCode,order.BuyerSubjectId,order.BranchRef,
            order.ShipToRef,order.BillToRef,order.PaymentTermRef,order.ExpectedDeliveryOn,items}));
    }

    public static void Verify(PurchaseOrder order,IReadOnlyCollection<PurchaseOrderLine> lines,
        IReadOnlyCollection<PrincipalOrderSourceBinding> bindings)
    {
        if(order.OriginalDirectSourceSha256!=Digest(order,lines,bindings)
            || bindings.Any(x=>x.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256))
            throw new InvalidOperationException("PrincipalOrderOriginalSourceChanged");
    }
}
