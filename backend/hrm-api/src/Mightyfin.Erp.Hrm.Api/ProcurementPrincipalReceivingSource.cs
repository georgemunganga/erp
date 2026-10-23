using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Project only a graph verified by the shared retained-issued2 owner loader.
/// It neither loads a second PO graph nor confuses manual acknowledgment with delivery.</summary>
internal static class ProcurementPrincipalReceivingSource
{
    internal static PrincipalReceivingSource Project(ProcurementPrincipalOrderRetainedIssued.Result retained,PrincipalReceivingRule configuredRule)
    {
        var selected=retained.SelectedLine??throw new InvalidOperationException("PrincipalReceivingLineRequired");
        if(configuredRule.CategoryCode!=selected.OriginalCategoryCode||configuredRule.PurchaseType!=selected.OriginalPurchaseType)
            throw new InvalidOperationException("PrincipalReceivingClassificationNotConfigured");
        return ProjectRetained(retained,configuredRule.Kind);
    }
    // Historical kind is a retained source pin, never inferred from today's policy.
    internal static PrincipalReceivingSource ProjectRetained(ProcurementPrincipalOrderRetainedIssued.Result retained,ReceivingKind kind)
    {
        var selected=retained.SelectedLine??throw new InvalidOperationException("PrincipalReceivingLineRequired");
        var category=selected.OriginalCategoryCode;var purchaseType=selected.OriginalPurchaseType;
        if(string.IsNullOrWhiteSpace(category)||string.IsNullOrWhiteSpace(purchaseType))
            throw new InvalidOperationException("PrincipalReceivingOriginalClassificationRequired");
        var classification=JsonSerializer.Serialize(new{schemaVersion=2,category,purchaseType,
            selected.Binding.RequestSnapshotSha256,selected.Binding.PolicyDecisionSha256,
            selected.Binding.PolicyRulesSha256});
        var order=retained.Order;var line=selected.OrderLine;var pin=selected.Binding;
        if(kind is not (ReceivingKind.Goods or ReceivingKind.Service))throw new InvalidOperationException("PrincipalReceivingKindInvalid");
        var commercial=new ReceivingSourcePin(order.TenantId,order.LegalEntityId,order.BranchRef,order.Id,
            retained.Issued.Id,line.Id,pin.PurchaseRequestLineId,retained.Issued.CommercialRevision,
            retained.Issued.CommercialSnapshotSha256,pin.OriginalDirectSourceSha256,
            $"PrincipalIssued2:{retained.Submission.Id:D}:{line.Id:D}",
            ProcurementEvidenceEncoding.Digest(classification),kind,category,purchaseType,
            order.CurrencyCode,line.UnitCode,line.Quantity,line.UnitPrice,ControlledReceiving.NetPriceBasis);
        return new(2,commercial,retained.Submission.Id,retained.Funding.Id,retained.CoreCase.Id,
            retained.Acknowledgment.Id,retained.Issued.OriginalDirectSourceSha256,
            new(retained.Submission.BuyerPrincipalId,retained.Submission.BuyerWorkerAliasId),
            new(pin.RequesterPrincipalId.ToString("D"),pin.RequesterWorkerAliasId),retained.Issued.AuthorizedAt,
            retained.Acknowledgment.ObservedAt,pin.Id);
    }
}
