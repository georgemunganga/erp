using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalOrderDraftPinsTests
{
    [Fact]
    public void Original_pin_survives_status_change_but_detects_commercial_or_source_tampering()
    {
        var branch=Guid.NewGuid();var source=Guid.NewGuid();
        var order=new PurchaseOrder{TenantId="test",LegalEntityId=Guid.NewGuid(),
            PrincipalDraftSchemaVersion=2,SupplierId=Guid.NewGuid(),
            BuyerSubjectId=Guid.NewGuid().ToString("D"),BranchRef=branch,CurrencyCode="ZMW",Status="Draft"};
        var line=new PurchaseOrderLine{PurchaseOrderId=order.Id,PurchaseRequestLineId=source,
            LineNumber=1,Description="Laptops",UnitCode="EA",Quantity=2m,UnitPrice=5.1234m,TaxAmount=0m};
        var pin=new PrincipalOrderSourceBinding{TenantId=order.TenantId,LegalEntityId=order.LegalEntityId,
            PurchaseOrderId=order.Id,PurchaseOrderLineId=line.Id,
            AllocationId=Guid.NewGuid(),PurchaseRequestId=Guid.NewGuid(),PurchaseRequestLineId=source,
            RequestVersion=4,SubmissionId=Guid.NewGuid(),SubmittedBusinessVersion=3,
            ApprovalOutcomeId=Guid.NewGuid(),CoreApprovalCaseId=Guid.NewGuid(),CoreDecisionId=Guid.NewGuid(),
            FinanceSnapshotId=Guid.NewGuid(),MethodId=Guid.NewGuid(),MethodRevision=1,MethodCode="DIRECT",
            RequestSnapshotSha256=Hash(),PolicyDecisionSha256=Hash(),PolicyRulesSha256=Hash(),
            FinanceSnapshotSha256=Hash(),PrincipalIdentitySha256=Hash(),BuyerPrincipalId=order.BuyerSubjectId,
            RequesterPrincipalId=Guid.NewGuid(),BranchId=branch,Quantity=2m,UnitEstimate=5.0000m,
            ProposedUnitPrice=5.1234m,EstimatedValue=10.2468m,CurrencyCode="ZMW"};
        order.OriginalDirectSourceSha256=PrincipalOrderDraftPins.Digest(order,[line],[pin]);
        pin.OriginalDirectSourceSha256=order.OriginalDirectSourceSha256;
        PrincipalOrderDraftPins.Verify(order,[line],[pin]);
        order.Status="Cancelled";
        PrincipalOrderDraftPins.Verify(order,[line],[pin]);
        line.UnitPrice=5.1235m;
        Assert.Throws<InvalidOperationException>(()=>PrincipalOrderDraftPins.Verify(order,[line],[pin]));
        line.UnitPrice=5.1234m;
        order.SupplierId=Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(()=>PrincipalOrderDraftPins.Verify(order,[line],[pin]));
    }

    [Fact]
    public void A_duplicate_source_or_unlinked_line_cannot_get_an_original_digest()
    {
        var order=new PurchaseOrder{TenantId="test",LegalEntityId=Guid.NewGuid(),
            PrincipalDraftSchemaVersion=2,BranchRef=Guid.NewGuid(),
            BuyerSubjectId=Guid.NewGuid().ToString("D"),CurrencyCode="ZMW"};
        var line=new PurchaseOrderLine{PurchaseOrderId=order.Id,PurchaseRequestLineId=Guid.NewGuid(),
            Quantity=1m,UnitPrice=1m,Description="One",UnitCode="EA"};
        var pin=new PrincipalOrderSourceBinding{TenantId=order.TenantId,LegalEntityId=order.LegalEntityId,
            PurchaseOrderId=order.Id,PurchaseOrderLineId=line.Id,
            PurchaseRequestLineId=Guid.NewGuid(),BuyerPrincipalId=order.BuyerSubjectId,
            BranchId=order.BranchRef!.Value,CurrencyCode="ZMW",Quantity=1m,ProposedUnitPrice=1m,EstimatedValue=1m};
        Assert.Throws<InvalidOperationException>(()=>PrincipalOrderDraftPins.Digest(order,[line],[pin]));
    }
    private static string Hash()=>new('a',64);
}
