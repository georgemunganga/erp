using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalGoodsReturnRulesTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static (PrincipalGoodsReturnDraft Head, PrincipalGoodsReturnRevision Revision,
        PrincipalGoodsReturnCommandReceipt Receipt) Create()
    {
        var tenant="synthetic-tenant";var company=Guid.NewGuid();var actor=Guid.NewGuid().ToString("D");
        var revisionId=Guid.NewGuid();var key=Guid.NewGuid();
        var head=new PrincipalGoodsReturnDraft{TenantId=tenant,LegalEntityId=company,BranchId=Guid.NewGuid(),
            SourceStateId=Guid.NewGuid(),AcceptanceFactId=Guid.NewGuid(),ReceiptId=Guid.NewGuid(),
            PurchaseOrderId=Guid.NewGuid(),PurchaseOrderLineId=Guid.NewGuid(),IssuedVersionId=Guid.NewGuid(),
            SourceSha256=new string('a',64),AcceptanceSha256=new string('b',64),AcceptedFactQuantity=2m,
            CreatorPrincipalId=actor,CurrentRevisionId=revisionId};
        var revision=new PrincipalGoodsReturnRevision{Id=revisionId,TenantId=tenant,LegalEntityId=company,
            DraftId=head.Id,Revision=1,CommandKey=key,ActorPrincipalId=actor,Quantity=1m,
            Reason="Damaged goods",EvidenceReference="NOTE-1",State="Draft",
            ProposalSha256=PrincipalGoodsReturnRules.Fingerprint(head.Id,1,1m,"Damaged goods","NOTE-1","Draft")};
        var request=JsonSerializer.Serialize(new{operation="Create",input=new{
            acceptanceFactId=head.AcceptanceFactId,quantity="1",reason=revision.Reason,
            evidenceReference=revision.EvidenceReference}},Json);
        return(head,revision,Receipt(head,revision,"Create",request,201));
    }

    private static PrincipalGoodsReturnCommandReceipt Receipt(PrincipalGoodsReturnDraft head,
        PrincipalGoodsReturnRevision revision,string operation,string request,int status)
    {
        var response=PrincipalGoodsReturnRules.ResponseJson(head,revision);
        return new(){TenantId=head.TenantId,LegalEntityId=head.LegalEntityId,DraftId=head.Id,
            BranchId=head.BranchId,CommandKey=revision.CommandKey,Operation=operation,
            ActorPrincipalId=revision.ActorPrincipalId,RequestJson=request,
            RequestSha256=ProcurementEvidenceEncoding.PolicyJsonDigest(request),ResponseJson=response,
            ResponseSha256=ProcurementEvidenceEncoding.Digest(response),ResponseStatus=status,
            ResultRevision=revision.Revision};
    }

    [Fact]
    public void Accepted_goods_proposal_has_exact_actor_owned_receipt_without_business_effect()
    {
        var(head,revision,receipt)=Create();
        Assert.True(PrincipalGoodsReturnRules.MatchesReceipt(head,revision,receipt));
        Assert.True(PrincipalGoodsReturnRules.HistoryValid(head,[revision],[receipt]));
        var response=receipt.ResponseJson;
        Assert.Contains("\"draftOnly\":true",response);
        Assert.Contains("\"stockChanged\":false",response);
        Assert.Contains("\"financeChanged\":false",response);
        Assert.Contains("\"supplierNotified\":false",response);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2.0001")]
    [InlineData("0.00001")]
    [InlineData("not-a-number")]
    public void Proposal_quantity_must_be_positive_exact_and_within_selected_acceptance(string amount)
        =>Assert.ThrowsAny<Exception>(()=>PrincipalGoodsReturnRules.Quantity(amount,2m));

    [Fact]
    public void Fingerprint_rejects_unknown_state_version_and_unrepresentable_quantity()
    {
        var id=Guid.NewGuid();
        Assert.Throws<ArgumentException>(()=>PrincipalGoodsReturnRules.Fingerprint(id,0,1m,"Reason",null,"Draft"));
        Assert.Throws<ArgumentException>(()=>PrincipalGoodsReturnRules.Fingerprint(id,1,1m,"Reason",null,"Approved"));
        Assert.ThrowsAny<Exception>(()=>PrincipalGoodsReturnRules.Fingerprint(id,1,0.00001m,"Reason",null,"Draft"));
    }

    [Fact]
    public void Actor_scope_version_and_response_digest_mutations_cannot_replay()
    {
        var(head,revision,receipt)=Create();
        Assert.False(PrincipalGoodsReturnRules.MatchesReceipt(head,revision,receipt.WithActor(Guid.NewGuid().ToString("D"))));
        Assert.False(PrincipalGoodsReturnRules.MatchesReceipt(head,revision,receipt.WithVersion(2)));
        Assert.False(PrincipalGoodsReturnRules.MatchesReceipt(head,revision,receipt.WithResponse("{}")));
        Assert.False(PrincipalGoodsReturnRules.MatchesReceipt(head,revision,receipt.WithScope(Guid.NewGuid())));
        Assert.False(PrincipalGoodsReturnRules.MatchesReceipt(head,revision,receipt.WithStatus(200)));
    }

    [Fact]
    public void Duplicate_command_and_changed_cancel_quantity_fail_complete_history()
    {
        var(head,first,firstReceipt)=Create();
        var second=new PrincipalGoodsReturnRevision{TenantId=head.TenantId,LegalEntityId=head.LegalEntityId,
            DraftId=head.Id,Revision=2,CommandKey=Guid.NewGuid(),ActorPrincipalId=head.CreatorPrincipalId,
            Quantity=first.Quantity,Reason="Cancelled proposal",EvidenceReference=first.EvidenceReference,State="Cancelled",
            ProposalSha256=PrincipalGoodsReturnRules.Fingerprint(head.Id,2,first.Quantity,"Cancelled proposal",first.EvidenceReference,"Cancelled")};
        head.Revision=2;head.State="Cancelled";head.CurrentRevisionId=second.Id;
        var request=JsonSerializer.Serialize(new{operation="Cancel",id=head.Id,
            input=new{expectedRevision=1L,reason=second.Reason}},Json);
        var secondReceipt=Receipt(head,second,"Cancel",request,200);
        Assert.True(PrincipalGoodsReturnRules.HistoryValid(head,[first,second],[firstReceipt,secondReceipt]));
        var duplicate=new PrincipalGoodsReturnRevision{Id=second.Id,TenantId=second.TenantId,LegalEntityId=second.LegalEntityId,
            DraftId=second.DraftId,Revision=second.Revision,CommandKey=first.CommandKey,
            ActorPrincipalId=second.ActorPrincipalId,Quantity=second.Quantity,Reason=second.Reason,
            EvidenceReference=second.EvidenceReference,State=second.State,ProposalSha256=second.ProposalSha256};
        Assert.False(PrincipalGoodsReturnRules.HistoryValid(head,[first,duplicate],[firstReceipt,secondReceipt]));
        var changedQuantity=new PrincipalGoodsReturnRevision{Id=second.Id,TenantId=second.TenantId,LegalEntityId=second.LegalEntityId,
            DraftId=second.DraftId,Revision=second.Revision,CommandKey=second.CommandKey,
            ActorPrincipalId=second.ActorPrincipalId,Quantity=0.5m,Reason=second.Reason,
            EvidenceReference=second.EvidenceReference,State=second.State,ProposalSha256=second.ProposalSha256};
        Assert.False(PrincipalGoodsReturnRules.HistoryValid(head,[first,changedQuantity],[firstReceipt,secondReceipt]));
    }
}

internal static class PrincipalGoodsReturnReceiptTestChanges
{
    internal static PrincipalGoodsReturnCommandReceipt WithActor(this PrincipalGoodsReturnCommandReceipt row,string actor)
        => Copy(row,actor:actor);
    internal static PrincipalGoodsReturnCommandReceipt WithVersion(this PrincipalGoodsReturnCommandReceipt row,long version)
        => Copy(row,version:version);
    internal static PrincipalGoodsReturnCommandReceipt WithResponse(this PrincipalGoodsReturnCommandReceipt row,string response)
        => Copy(row,response:response);
    internal static PrincipalGoodsReturnCommandReceipt WithScope(this PrincipalGoodsReturnCommandReceipt row,Guid company)
        => Copy(row,company:company);
    internal static PrincipalGoodsReturnCommandReceipt WithStatus(this PrincipalGoodsReturnCommandReceipt row,int status)
        => Copy(row,status:status);
    private static PrincipalGoodsReturnCommandReceipt Copy(PrincipalGoodsReturnCommandReceipt row,string? actor=null,
        long? version=null,string? response=null,Guid? company=null,int? status=null)
        => new(){TenantId=row.TenantId,LegalEntityId=company??row.LegalEntityId,DraftId=row.DraftId,
            BranchId=row.BranchId,CommandKey=row.CommandKey,Operation=row.Operation,
            ActorPrincipalId=actor??row.ActorPrincipalId,RequestJson=row.RequestJson,RequestSha256=row.RequestSha256,
            ResponseJson=response??row.ResponseJson,ResponseSha256=response is null?row.ResponseSha256:ProcurementEvidenceEncoding.Digest(response),
            ResponseStatus=status??row.ResponseStatus,ResultRevision=version??row.ResultRevision};
}
