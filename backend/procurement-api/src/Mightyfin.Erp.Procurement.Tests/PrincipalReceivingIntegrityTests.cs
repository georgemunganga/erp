using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class PrincipalControlledReceivingTests
{
    [Fact]
    public void Complete_successor_history_reconciles_two_PO_lines_and_denies_joint_or_partial_counter_tampering()
    {
        var first=Source(ReceivingKind.Service,0.5m,100m);
        var second=Source(ReceivingKind.Service,0.5m,100m);second=second with{Commercial=second.Commercial with{PurchaseRequestLineId=first.Commercial.PurchaseRequestLineId}};
        PrincipalReceivingLedger Accepted(PrincipalReceivingSource source,decimal quantity)
        {var policy=Policy(source);var ledger=Empty(source,policy);return Add(ledger,PrincipalControlledReceiving.CertifyService(ledger,policy,Actor(Inspector),Guid.NewGuid(),Guid.NewGuid(),0,quantity,"Actual milestone",new(2026,10,22),new(2026,10,22),"REC:accepted",At,BusinessDate(source)));}
        var a=Accepted(first,0.5m);var b=Accepted(second,0.25m);
        var request=new PurchaseRequestLine{Id=first.Commercial.PurchaseRequestLineId,TenantId=first.Commercial.TenantId,LegalEntityId=Company,ApprovedQuantity=1,AcceptedQuantity=0.75m};
        PurchaseOrderLine Line(PrincipalReceivingSource source,decimal accepted)=>new(){Id=source.Commercial.PurchaseOrderLineId,
            TenantId=source.Commercial.TenantId,LegalEntityId=Company,PurchaseOrderId=source.Commercial.PurchaseOrderId,
            PurchaseRequestLineId=request.Id,Quantity=source.Commercial.AuthorizedQuantity,AcceptedQuantity=accepted};
        var one=Line(first,0.5m);var two=Line(second,0.25m);
        PrincipalReceivingCounterReconciliation.Verify(request,[one,two],[a,b]);
        request.AcceptedQuantity=0.9m;Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingCounterReconciliation.Verify(request,[one,two],[a,b]));
        one.AcceptedQuantity=0.65m;Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingCounterReconciliation.Verify(request,[one,two],[a,b]));
        one.AcceptedQuantity=0.5m;request.AcceptedQuantity=0.75m;
        var inactive=Line(Source(ReceivingKind.Service),0.1m);inactive.IsActive=false;
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingCounterReconciliation.Verify(request,[one,two,inactive],[a,b]));
        Assert.Equal(50m,PrincipalControlledReceiving.Balance(a).AcceptedNetAmount);Assert.Equal(25m,PrincipalControlledReceiving.Balance(b).AcceptedNetAmount);
    }
    private static (PrincipalReceivingSource Source,PrincipalReceivingFact Fact,PrincipalReceivingCommandReceipt Receipt) OriginalReceiptFixture()
    {
        var source=Source(ReceivingKind.Service);var draft=Guid.NewGuid();var key=Guid.NewGuid();
        var fact=new PrincipalReceivingFact{TenantId=source.Commercial.TenantId,LegalEntityId=Company,BranchId=Branch,DraftId=draft,
            Kind="Acceptance",Operation="Certify",CommandKey=key,ActorPrincipalId=Inspector};
        var result=JsonSerializer.Serialize(new{id=draft,version=2,executionVersion=2,kind="Service",status="Certified",
            purchaseOrderId=source.Commercial.PurchaseOrderId,issuedVersionId=source.Commercial.IssuedVersionId});
        var receipt=new PrincipalReceivingCommandReceipt{TenantId=fact.TenantId,LegalEntityId=Company,DraftId=draft,BranchId=Branch,
            Operation="Certify",ActorPrincipalId=Inspector,CommandKey=key,RequestSha256=new string('a',64),ResponseJson=result,
            ResponseSha256=ProcurementEvidenceEncoding.Digest(result),ResponseStatus=200,ResultRevision=2};
        return(source,fact,receipt);
    }
    [Fact]
    public void Retained_fact_requires_exact_original_same_command_actor_scope_operation_and_result_receipt()
    {
        var fixture=OriginalReceiptFixture();PrincipalReceivingStoredReceipt.Verify(fixture.Fact,fixture.Receipt,fixture.Source);
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingStoredReceipt.Verify(fixture.Fact,null,fixture.Source));
        foreach(var changed in new[]{"actor","scope","operation","key","draft","revision"})
        {
            var (source,fact,original)=OriginalReceiptFixture();
            var receipt=new PrincipalReceivingCommandReceipt{TenantId=original.TenantId,LegalEntityId=original.LegalEntityId,
                DraftId=changed=="draft"?Guid.NewGuid():original.DraftId,BranchId=changed=="scope"?Guid.NewGuid():original.BranchId,
                Operation=changed=="operation"?"Accept":original.Operation,ActorPrincipalId=changed=="actor"?Buyer:original.ActorPrincipalId,
                CommandKey=changed=="key"?Guid.NewGuid():original.CommandKey,RequestSha256=original.RequestSha256,
                ResponseJson=original.ResponseJson,ResponseSha256=original.ResponseSha256,ResponseStatus=200,ResultRevision=changed=="revision"?3:2};
            Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingStoredReceipt.Verify(fact,receipt,source));
        }
    }
    [Fact]
    public void Receipt_with_recomputed_hash_but_wrong_target_or_duplicate_result_fields_never_proves_history()
    {
        foreach(var duplicate in new[]{false,true})
        {
            var (source,fact,original)=OriginalReceiptFixture();
            var response=duplicate?original.ResponseJson.Replace("\"status\":\"Certified\"","\"status\":\"Recorded\",\"status\":\"Certified\"",StringComparison.Ordinal)
                :original.ResponseJson.Replace(fact.DraftId.ToString("D"),Guid.NewGuid().ToString("D"),StringComparison.Ordinal);
            var changed=new PrincipalReceivingCommandReceipt{TenantId=original.TenantId,LegalEntityId=original.LegalEntityId,DraftId=original.DraftId,
                BranchId=original.BranchId,Operation=original.Operation,ActorPrincipalId=original.ActorPrincipalId,CommandKey=original.CommandKey,
                RequestSha256=original.RequestSha256,ResponseJson=response,ResponseSha256=ProcurementEvidenceEncoding.Digest(response),ResponseStatus=200,ResultRevision=2};
            Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingStoredReceipt.Verify(fact,changed,source));
        }
    }
}

public sealed partial class PrincipalControlledReceivingTests
{
    [Fact]
    public async Task Append_denies_diverged_owning_counters_before_adding_a_fact_or_advancing_the_cursor()
    {
        var source=Source(ReceivingKind.Service);var policy=Policy(source);
        var scope=new ReceivingStoreScope(source.Commercial.TenantId,Company,Inspector);
        var options=new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db=new ProcurementDbContext(options,scope);
        // Deliberately incomplete inherited rows. This is a negative store boundary,
        // not a valid PR/PO/approval producer or relational fixture.
        db.AddScoped(new PurchaseRequestLine{Id=source.Commercial.PurchaseRequestLineId,PurchaseRequestId=Guid.NewGuid(),
            Description="Inherited demand",UnitCode="each",RequestedQuantity=1,ApprovedQuantity=1,AcceptedQuantity=0.25m});
        db.AddScoped(new PurchaseOrderLine{Id=source.Commercial.PurchaseOrderLineId,PurchaseOrderId=source.Commercial.PurchaseOrderId,
            PurchaseRequestLineId=source.Commercial.PurchaseRequestLineId,Description="Inherited order",UnitCode="each",Quantity=1,AcceptedQuantity=0.25m});
        await db.SaveChangesAsync();var priorAudit=await db.AuditEvents.CountAsync();
        var state=State(source);var fact=PrincipalControlledReceiving.CertifyService(Empty(source,policy),policy,Actor(Inspector),Guid.NewGuid(),Guid.NewGuid(),0,
            0.1m,"Attempt on changed counter",new(2026,10,22),new(2026,10,22),"REC:changed",At,BusinessDate(source));
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>new PrincipalReceivingStore(db).AppendAsync(state,fact.AcceptanceRecordId,fact,default));
        Assert.Equal("PrincipalReceivingOrderCounterDiverged",error.Message);Assert.Equal(0,state.Sequence);Assert.Equal(0,state.AcceptedQuantity);
        Assert.Empty(db.PrincipalReceivingFacts.Local);Assert.Empty(db.PrincipalReceivingCommandReceipts.Local);Assert.Equal(priorAudit,await db.AuditEvents.CountAsync());
    }
}
