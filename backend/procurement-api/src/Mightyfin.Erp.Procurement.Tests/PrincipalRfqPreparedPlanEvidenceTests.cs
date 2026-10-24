using System.Text.Json;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Procurement.Tests;
public sealed partial class PrincipalRfqPrepublicationPlanTests
{
    // Synthetic pure owned shapes, NOT actual saved Core document or SQL evidence.
    private static (Fixture F,PrincipalRfqCommercialDraftHead Head,PrincipalRfqCommercialDraftRevision Row,
        PrincipalRfqCommercialDraftReceipt Command,PrincipalRfqPrepublicationPlan Candidate) PreparedShape()
    {
        var f=Original();var actor=CommercialActor(f);
        var(head,first)=PrincipalRfqCommercialDraft.Create(actor,Guid.NewGuid(),Guid.NewGuid(),f.Event,f.Evidence,[f.Binding],EmptyDraft());
        var definition=JsonSerializer.Serialize(new{schemaVersion=1,transactionType="SourcingEvaluationDefinition",normalizationTotal="1",
            criteria=new[]{new{code="TECH",phase="Technical",kind="Scored",mandatory=true,maximumScore="10",weight="1",evaluatorPrincipalIds=new[]{f.Evaluator.PrincipalId}}}});
        var input=new PrincipalRfqCommercialDraftInput(definition,f.At.AddDays(1),f.At.AddDays(2),"ZMW","SameCurrencyOnly","OriginalSeparateComponents",[]);
        var row=PrincipalRfqCommercialDraft.Revise(actor,head,first,1,Guid.NewGuid(),f.Event,f.Evidence,[f.Binding],input);
        var candidate=PrincipalRfqPrepublicationPlan.Prepare(f.Event,f.Evidence,[f.Binding],PrincipalRfqEvaluationPlanDefinition.Parse(definition),f.Policy,[f.Evaluator],[],
            f.Event.Version,row.Revision,new(row.Id,"procurement",PrincipalRfqPrepublicationPlan.DocumentSourceType,f.Event.Id,row.Revision,DocumentUseKind.Review),
            input.OpensAt.Value,input.ClosesAt.Value,f.At,"ZMW","SameCurrencyOnly","OriginalSeparateComponents");
        return(f,head,row,DraftReceipt(head,row),candidate);
    }
    private static PrincipalRfqPreparationReceipt PreparationReceipt(PrincipalRfqPreparedPlan plan,PrincipalRfqCommercialDraftReceipt command)
    {
        var response=PrincipalRfqPreparedPlanEvidence.Response(plan,command.CommandKey);
        return new(){TenantId=plan.TenantId,LegalEntityId=plan.LegalEntityId,PreparedPlanId=plan.Id,DraftRevisionId=plan.DraftRevisionId,
            DraftCommandReceiptId=command.Id,SourcingEventId=plan.SourcingEventId,BranchId=plan.BranchId,CommandKey=command.CommandKey,
            ActorPrincipalId=plan.ActorPrincipalId,RequestSha256=command.RequestSha256,SnapshotSha256=plan.SnapshotSha256,
            ResponseJson=response,ResponseSha256=ProcurementEvidenceEncoding.Digest(response)};
    }
    [Fact]
    public void Prepared_candidate_and_original_receipt_bind_same_revision_policy_source_without_publication()
    {
        var f=PreparedShape();var plan=PrincipalRfqPreparedPlanEvidence.Create(f.Candidate,f.F.Policy,f.Head,f.Row,CommercialActor(f.F),f.F.At,0,null,null);
        var receipt=PreparationReceipt(plan,f.Command);
        PrincipalRfqPreparedPlanEvidence.Verify(plan,f.Head,f.Row);PrincipalRfqPreparedPlanEvidence.VerifyReceipt(plan,receipt,f.Command);
        Assert.Equal(f.Row.Id,plan.DocumentUseKey);Assert.Equal(0,plan.DocumentCount);Assert.Null(plan.DocumentUseSetId);
        Assert.Equal(f.F.Policy.PublishedIdentityId,plan.PublishedPolicyIdentityId);
        Assert.Contains("PreparedCandidate",receipt.ResponseJson);Assert.Contains("\"canPublish\":false",receipt.ResponseJson);
    }
    [Fact]
    public void Prepared_original_acknowledgement_survives_later_draft_revision_without_rewriting_bytes()
    {
        var f=PreparedShape();var plan=PrincipalRfqPreparedPlanEvidence.Create(f.Candidate,f.F.Policy,f.Head,f.Row,CommercialActor(f.F),f.F.At,0,null,null);
        var receipt=PreparationReceipt(plan,f.Command);var original=receipt.ResponseJson;
        _=PrincipalRfqCommercialDraft.Revise(CommercialActor(f.F),f.Head,f.Row,2,Guid.NewGuid(),f.F.Event,f.F.Evidence,[f.F.Binding],EmptyDraft());
        _=PrincipalRfqCommercialDraft.RestoreHistorical(f.Head,f.Row,f.Command);
        PrincipalRfqPreparedPlanEvidence.Verify(plan,f.Head,f.Row);PrincipalRfqPreparedPlanEvidence.VerifyReceipt(plan,receipt,f.Command);
        Assert.Equal(original,PrincipalRfqPreparedPlanEvidence.Response(plan,receipt.CommandKey));
    }
    [Fact]
    public void Preparation_cannot_fake_verified_empty_documents_or_change_source_policy_revision_binding()
    {
        var f=PreparedShape();var plan=PrincipalRfqPreparedPlanEvidence.Create(f.Candidate,f.F.Policy,f.Head,f.Row,CommercialActor(f.F),f.F.At,0,null,null);
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqPreparedPlanEvidence.Create(f.Candidate,f.F.Policy,f.Head,f.Row,CommercialActor(f.F),f.F.At,0,Guid.NewGuid(),new string('a',64)));
        foreach(var changed in new[]{Changed(plan,x=>x["DraftRevisionId"]=Guid.NewGuid()),Changed(plan,x=>x["PublishedPolicyIdentityId"]=Guid.NewGuid()),
            Changed(plan,x=>x["DocumentCount"]=1),Changed(plan,x=>x["ActorPrincipalId"]=Guid.NewGuid().ToString("D")),Changed(plan,x=>x["PolicyRulesSha256"]=new string('b',64))})
            Assert.Throws<InvalidOperationException>(()=>PrincipalRfqPreparedPlanEvidence.Verify(changed,f.Head,f.Row));
    }
    [Fact]
    public void Preparation_original_command_actor_body_and_response_cannot_be_rebound()
    {
        var f=PreparedShape();var plan=PrincipalRfqPreparedPlanEvidence.Create(f.Candidate,f.F.Policy,f.Head,f.Row,CommercialActor(f.F),f.F.At,0,null,null);
        var receipt=PreparationReceipt(plan,f.Command);
        foreach(var changed in new[]{Changed(receipt,x=>x["CommandKey"]=Guid.NewGuid()),Changed(receipt,x=>x["ActorPrincipalId"]=Guid.NewGuid().ToString("D")),
            Changed(receipt,x=>x["RequestSha256"]=new string('b',64)),Changed(receipt,x=>x["ResponseJson"]="{\"canPublish\":true}")})
            Assert.Throws<InvalidOperationException>(()=>PrincipalRfqPreparedPlanEvidence.VerifyReceipt(plan,changed,f.Command));
    }
}
