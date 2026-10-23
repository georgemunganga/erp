using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class GovernedPrincipalRfqSourceTests
{
    private static (RfqPrincipalDraft Draft,SourcingEvent Event,SourcingDraftEvidence Evidence,SourcingDemandBinding Binding) Original()
    {
        var tenant="tenant";var company=Guid.NewGuid();var buyer=Guid.NewGuid().ToString("D");
        var maker=Guid.NewGuid().ToString("D");var requestId=Guid.NewGuid();var lineId=Guid.NewGuid();
        var eventId=Guid.NewGuid();var commandKey=Guid.NewGuid();
        var pin=new RfqPrincipalDemandPin(requestId,lineId,4,Guid.NewGuid(),3,Guid.NewGuid(),
            Guid.NewGuid(),Guid.NewGuid(),new string('a',64),new string('b',64),new string('c',64),
            Guid.NewGuid(),new string('d',64),Guid.NewGuid(),2,"RFQ",3,maker,maker,null,null,
            new string('e',64),Guid.NewGuid(),Guid.NewGuid(),0.0001m,0.0001m,0.00000001m,
            "EA","OFFICE",null);
        var draft=GovernedPrincipalRfqDraft.NewDraft(eventId,tenant,company,null,"ZMW","Laptop quotes",
            buyer,null,"Draft",[pin]);
        var(payload,sha)=GovernedPrincipalRfqRestoration.Capture(draft,commandKey);
        var evidence=new SourcingDraftEvidence{SchemaVersion=2,SourcingEventId=eventId,
            TenantId=tenant,LegalEntityId=company,MakerSubjectId=buyer,MakerPrincipalId=buyer,
            MakerWorkerId=null,CreateCommandKey=commandKey,OriginalDraftFingerprint=draft.OriginalFingerprint,
            PayloadJson=payload,PayloadSha256=sha};
        var ev=new SourcingEvent{Id=eventId,TenantId=tenant,LegalEntityId=company,ExecutionVersion=2,
            Status="Draft",Number="RF-TEST",Title=draft.Title,CurrencyCode="ZMW",OwnerSubjectId=buyer,
            Method=pin.MethodCode,MethodId=pin.MethodId,MethodRevision=pin.MethodRevision,
            MakerWorkerId=null,SourceEvidenceId=evidence.Id,OriginalDraftFingerprint=draft.OriginalFingerprint};
        var binding=new SourcingDemandBinding{TenantId=tenant,LegalEntityId=company,IdentitySchemaVersion=2,
            SourcingEventId=eventId,SourceEvidenceId=evidence.Id,SourcingLineId=pin.EventLineId,
            PurchaseRequestId=requestId,PurchaseRequestLineId=lineId,RequestVersion=pin.RequestVersion,
            SubmissionId=pin.SubmissionId,SubmittedVersion=pin.SubmittedVersion,CoreApprovalCaseId=pin.CaseId,
            ApprovalOutcomeId=pin.OutcomeId,CoreDecisionId=pin.DecisionId,RequestSha=pin.RequestSha,
            PolicyDecisionSha=pin.PolicyDecisionSha,PolicyRulesSha=pin.PolicyRulesSha,FinanceSnapshotId=pin.FinanceId,
            FinanceSha=pin.FinanceSha,MethodId=pin.MethodId,MethodRevision=pin.MethodRevision,
            MethodCode=pin.MethodCode,MinimumQuoteCount=pin.MinimumQuoteCount,
            MakerPrincipalId=pin.MakerPrincipalId,RequesterPrincipalId=pin.RequesterPrincipalId,
            MakerWorkerId=null,RequesterWorkerId=null,PrincipalIdentitySha256=pin.PrincipalIdentitySha256,
            AllocationId=pin.AllocationId,Quantity=pin.Quantity,UnitEstimate=pin.UnitEstimate,
            EstimatedValue=pin.EstimatedValue,UnitCode=pin.UnitCode,CategoryCode=pin.CategoryCode};
        return(draft,ev,evidence,binding);
    }

    [Fact]
    public void Workerless_principal_evidence_round_trips_without_a_synthetic_employee()
    {
        var(draft,ev,evidence,binding)=Original();
        var restored=GovernedPrincipalRfqRestoration.RestoreVerified(evidence,ev,[binding]);
        Assert.Equal(draft.OriginalFingerprint,restored.OriginalFingerprint);
        Assert.Null(restored.MakerWorkerAlias);Assert.Null(Assert.Single(restored.Lines).RequesterWorkerAlias);
        Assert.Equal(0.00000001m,Assert.Single(restored.Lines).EstimatedValue);
        Assert.Equal(2,evidence.SchemaVersion);Assert.Equal(2,binding.IdentitySchemaVersion);
    }

    [Fact]
    public void Changed_principal_binding_or_payload_fails_before_cancellation()
    {
        var(_,ev,evidence,binding)=Original();
        Assert.Throws<InvalidOperationException>(()=>GovernedPrincipalRfqRestoration.RestoreVerified(evidence,ev,
            [new SourcingDemandBinding{IdentitySchemaVersion=1}]));
        evidence=new SourcingDraftEvidence{SchemaVersion=2,SourcingEventId=evidence.SourcingEventId,
            TenantId=evidence.TenantId,LegalEntityId=evidence.LegalEntityId,MakerSubjectId=evidence.MakerSubjectId,
            MakerPrincipalId=evidence.MakerPrincipalId,CreateCommandKey=evidence.CreateCommandKey,
            OriginalDraftFingerprint=evidence.OriginalDraftFingerprint,
            PayloadJson=evidence.PayloadJson.Replace("Laptop quotes","Changed quotes",StringComparison.Ordinal),
            PayloadSha256=evidence.PayloadSha256};
        Assert.Throws<InvalidOperationException>(()=>GovernedPrincipalRfqRestoration.RestoreVerified(evidence,ev,[binding]));
    }

    [Fact]
    public void Duplicate_json_field_is_rejected_even_when_the_payload_digest_is_recomputed()
    {
        var(_,ev,evidence,binding)=Original();
        var payload=evidence.PayloadJson.Replace("{\"SchemaVersion\":2,", "{\"SchemaVersion\":2,\"SchemaVersion\":2,",StringComparison.Ordinal);
        Assert.NotEqual(evidence.PayloadJson,payload);
        var changed=new SourcingDraftEvidence{SchemaVersion=2,SourcingEventId=evidence.SourcingEventId,
            TenantId=evidence.TenantId,LegalEntityId=evidence.LegalEntityId,
            MakerSubjectId=evidence.MakerSubjectId,MakerPrincipalId=evidence.MakerPrincipalId,
            CreateCommandKey=evidence.CreateCommandKey,OriginalDraftFingerprint=evidence.OriginalDraftFingerprint,
            PayloadJson=payload,PayloadSha256=ProcurementEvidenceEncoding.Digest(payload)};
        Assert.Throws<InvalidOperationException>(()=>GovernedPrincipalRfqRestoration.RestoreVerified(changed,ev,[binding]));
    }

    [Fact]
    public void Principal_draft_rejects_noncanonical_identity_and_zero_worker_alias()
    {
        var(draft,_,_,_)=Original();
        Assert.Throws<InvalidOperationException>(()=>GovernedPrincipalRfqDraft.NewDraft(draft.Id,draft.TenantId,
            draft.CompanyId,draft.BranchId,draft.CurrencyCode,draft.Title,"not-an-account",null,"Draft",draft.Lines));
        Assert.Throws<InvalidOperationException>(()=>GovernedPrincipalRfqDraft.NewDraft(draft.Id,draft.TenantId,
            draft.CompanyId,draft.BranchId,draft.CurrencyCode,draft.Title,draft.MakerPrincipalId,Guid.Empty,"Draft",draft.Lines));
    }

    [Fact]
    public void Cancellation_plans_only_the_original_hold_and_preserves_other_allocations()
    {
        var f=new PurchaseOrderSourceAllocationTests.Fixture(executionRoute:"CompetitiveSourcing",minimumQuoteCount:2);
        var eventId=Guid.NewGuid();var eventLineId=Guid.NewGuid();var allocationId=Guid.NewGuid();
        var buyer=Guid.NewGuid().ToString("D");var requester=Guid.NewGuid().ToString("D");
        var original=f.Source.Submission;
        var pin=new RfqPrincipalDemandPin(f.Request.Id,f.Line.Id,f.Request.Version,original.Id,
            original.SubmittedBusinessVersion,f.Case.Id,f.Source.Outcome.Id,f.Source.FinalDecision.Id,
            original.RequestSnapshotSha256,original.PolicyDecisionSha256,original.PolicyRulesSha256,
            original.FinanceSnapshotId,original.FinanceSnapshotSha256,f.Method.Id,f.Method.Version,
            f.Method.Code,2,requester,requester,null,null,new string('a',64),allocationId,
            eventLineId,4m,10m,40m,f.Line.UnitCode,f.Line.CategoryCode,f.Request.DepartmentRef);
        var draft=GovernedPrincipalRfqDraft.NewDraft(eventId,f.Request.TenantId,f.Company,f.Request.BranchRef,
            f.Request.CurrencyCode,"Quotes",buyer,null,"Draft",[pin]);
        var eventRow=new SourcingEvent{Id=eventId,TenantId=f.Request.TenantId,LegalEntityId=f.Company};
        var lineRow=new SourcingLine{Id=eventLineId,TenantId=f.Request.TenantId,LegalEntityId=f.Company,
            SourcingEventId=eventId,PurchaseRequestLineId=f.Line.Id};
        f.Owners.Add(PurchaseDemandAllocationOwner.FromSourcing(eventRow,lineRow));
        var hold=new PurchaseRequestAllocation{Id=allocationId,TenantId=f.Request.TenantId,LegalEntityId=f.Company,
            PurchaseRequestId=f.Request.Id,PurchaseRequestLineId=f.Line.Id,TargetType="SourcingEvent",
            TargetId=eventId,Status="Reserved",Quantity=4m,EstimatedValue=40m,CurrencyCode="ZMW",
            Method=f.Method.Code};
        f.Ledger.Add(hold);f.Line.AllocatedQuantity=4m;
        var actor=new RfqPrincipalActor(buyer,null,f.Request.TenantId,f.Company,null,new HashSet<Guid>(),
            true,new HashSet<string>{"procurement-sourcing-cancel"});
        var plan=GovernedPrincipalRfqDraft.Cancel(actor,draft,Guid.NewGuid(),draft.OriginalFingerprint,"No need",[f.Source]);
        var release=Assert.Single(plan.Effects);
        Assert.Equal("Release",release.Kind);Assert.Equal(allocationId,release.AllocationId);
        Assert.Equal(0m,release.ResultingAllocatedQuantity);Assert.Equal("Reserved",hold.Status);
        Assert.Equal(4m,f.Line.AllocatedQuantity);
        Assert.Throws<InvalidOperationException>(()=>GovernedPrincipalRfqDraft.Cancel(actor,draft,
            Guid.NewGuid(),"wrong fingerprint","No need",[f.Source]));
        Assert.Throws<InvalidOperationException>(()=>GovernedPrincipalRfqDraft.Cancel(actor with
        { PrincipalId=Guid.NewGuid().ToString("D") },draft,Guid.NewGuid(),draft.OriginalFingerprint,"No need",[f.Source]));
        Assert.Throws<InvalidOperationException>(()=>GovernedPrincipalRfqDraft.Cancel(actor with
        { Permissions=new HashSet<string>() },draft,Guid.NewGuid(),draft.OriginalFingerprint,"No need",[f.Source]));
        hold.Quantity=3m;
        Assert.Throws<InvalidOperationException>(()=>GovernedPrincipalRfqDraft.Cancel(actor,draft,
            Guid.NewGuid(),draft.OriginalFingerprint,"No need",[f.Source]));
        Assert.Equal(4m,f.Line.AllocatedQuantity);
    }
}
