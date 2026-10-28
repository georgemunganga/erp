using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class PrincipalControlledReceivingTests
{
    private sealed record PrincipalInvoiceFixture(
        PrincipalReceivingSource Source, PolicyVersion Policy, PrincipalReceivingSourceState State,
        PrincipalReceivingFact Fact, PrincipalReceivingDraftLineBinding Binding,
        PrincipalReceivingCommandReceipt Receipt);

    private static PrincipalInvoiceFixture PrincipalInvoiceServiceFixture()
    {
        var source=Source(ReceivingKind.Service,1m,0.0001m);
        var policy=Policy(source);
        var draftId=Guid.NewGuid();
        var acceptance=PrincipalControlledReceiving.CertifyService(Empty(source,policy),policy,
            Actor(Inspector),draftId,Guid.NewGuid(),0,1m,"Completed service",
            new DateOnly(2026,10,23),new DateOnly(2026,10,23),"REC:service",At,BusinessDate(source));
        var state=State(source,1,acceptance.Quantity,acceptance.NetAmount);
        var bindingId=Guid.NewGuid();
        var payload=PrincipalReceivingStorageCodec.Encode(acceptance);
        var row=new PrincipalReceivingFact
        {
            Id=acceptance.Id,TenantId=state.TenantId,LegalEntityId=state.LegalEntityId,
            SourceStateId=state.Id,DraftLineBindingId=bindingId,DraftId=draftId,
            BranchId=state.BranchId,Kind="Acceptance",Operation="Certify",Sequence=1,
            CommandKey=acceptance.CommandKey,ActorPrincipalId=acceptance.Actor.PrincipalId,
            ActorWorkerAliasId=acceptance.Actor.WorkerAliasId,PolicyVersionId=policy.Id,
            Quantity=acceptance.Quantity,NetAmount=acceptance.NetAmount,
            PayloadJson=payload,PayloadSha256=ProcurementEvidenceEncoding.Digest(payload)
        };
        var binding=new PrincipalReceivingDraftLineBinding
        {
            Id=bindingId,TenantId=state.TenantId,LegalEntityId=state.LegalEntityId,
            DraftId=draftId,DraftRevisionId=Guid.NewGuid(),LineId=draftId,
            SourceStateId=state.Id,BranchId=state.BranchId,
            PurchaseOrderId=state.PurchaseOrderId,IssuedVersionId=state.IssuedVersionId,
            PurchaseOrderLineId=state.PurchaseOrderLineId,Quantity=1m
        };
        var response=JsonSerializer.Serialize(new
        {
            id=draftId,version=1,executionVersion=2,kind="Service",
            purchaseOrderId=state.PurchaseOrderId,issuedVersionId=state.IssuedVersionId,status="Certified"
        });
        var receipt=new PrincipalReceivingCommandReceipt
        {
            TenantId=state.TenantId,LegalEntityId=state.LegalEntityId,
            DraftId=draftId,BranchId=state.BranchId,Operation="Certify",CommandKey=row.CommandKey,
            ActorPrincipalId=row.ActorPrincipalId,RequestSha256=new string('a',64),
            ResponseJson=response,ResponseSha256=ProcurementEvidenceEncoding.Digest(response),
            ResponseStatus=200,ResultRevision=1
        };
        return new(source,policy,state,row,binding,receipt);
    }

    [Fact]
    public void Principal_fact_projection_requires_full_retained_graph_and_remains_historical_only()
    {
        var f=PrincipalInvoiceServiceFixture();
        var result=PrincipalInvoiceAcceptedSourceProjection.Verify(f.Source,f.State,[f.Fact],[f.Binding],[f.Receipt],[f.Policy]);
        var accepted=Assert.Single(result.Facts);
        Assert.Equal(f.State.Id,result.SourceStateId);
        Assert.Equal(f.State.SourceSha256,result.SourceSha256);
        Assert.Equal(1,result.SourceSequence);
        Assert.Equal(f.Fact.Id,accepted.FactId);
        Assert.Equal(f.State.PurchaseOrderLineId,accepted.PurchaseOrderLineId);
        Assert.Equal(f.Fact.PayloadSha256,accepted.PayloadSha256);
        Assert.Equal(1m,accepted.AcceptedQuantity);
        Assert.Equal(0.0001m,accepted.AcceptedNetAmount);
        Assert.Equal(ReceivingKind.Service,accepted.Kind);
    }

    [Fact]
    public void Missing_or_foreign_owner_edges_cannot_become_accepted_invoice_evidence()
    {
        var f=PrincipalInvoiceServiceFixture();
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceAcceptedSourceProjection.Verify(f.Source,f.State,[f.Fact],[],[f.Receipt],[f.Policy]));
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceAcceptedSourceProjection.Verify(f.Source,f.State,[f.Fact],[f.Binding],[],[f.Policy]));
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceAcceptedSourceProjection.Verify(f.Source,f.State,[f.Fact],[f.Binding,f.Binding],[f.Receipt],[f.Policy]));
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceAcceptedSourceProjection.Verify(f.Source with { SupplierAcknowledgmentId=Guid.NewGuid() },f.State,[f.Fact],[f.Binding],[f.Receipt],[f.Policy]));
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceAcceptedSourceProjection.Verify(f.Source,f.State,[f.Fact],[f.Binding],[f.Receipt],[]));
        f.State.AcceptedQuantity=2m;
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceAcceptedSourceProjection.Verify(f.Source,f.State,[f.Fact],[f.Binding],[f.Receipt],[f.Policy]));
    }
}
