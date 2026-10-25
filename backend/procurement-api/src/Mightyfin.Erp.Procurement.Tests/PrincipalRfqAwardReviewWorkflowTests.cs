using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Xunit;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqAwardReviewWorkflowTests
{
    private static readonly Guid Company=Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Branch=Guid.Parse("22222222-2222-4222-8222-222222222222");
    private const string Maker="33333333-3333-4333-8333-333333333333";
    private const string Reviewer="44444444-4444-4444-8444-444444444444";
    private static readonly DateTimeOffset Now=new(2026,10,25,5,0,0,TimeSpan.Zero);
    private static string Rules =>
        "{\"schemaVersion\":1,\"transactionType\":\"SupplierAward\",\"allocationMode\":\"FullOfferLineOnly\",\"reviewBranchId\":\"22222222-2222-4222-8222-222222222222\",\"currencyCode\":\"ZMW\",\"maximumAmountExact\":\"100\",\"stages\":[{\"code\":\"REVIEW\",\"mode\":\"All\",\"principalIds\":[\""+Reviewer+"\"]}]}";

    private sealed record Fixture(PrincipalRfqAwardCandidate Candidate,
        PrincipalRfqAwardReviewSourceGraph Source,
        PrincipalRfqRecommendationDraftHead Head,
        PrincipalRfqRecommendationDraftRevision Revision,
        PolicyVersion Policy,
        PrincipalRfqAwardFoundation.CurrentPerson[] Reviewers);

    private static Fixture Valid(int revisionNo=1, Fixture? previous=null)
    {
        var head=previous?.Head??new PrincipalRfqRecommendationDraftHead
        {
            TenantId="tenant-a",LegalEntityId=Company,
            RoundId=Guid.CreateVersion7(),UnsealingId=Guid.CreateVersion7(),
            SealId=Guid.CreateVersion7(),SourcingEventId=Guid.CreateVersion7(),
            BranchId=Branch,PreparedPlanId=Guid.CreateVersion7(),
            EvaluationPlanRevisionId=Guid.CreateVersion7(),
            ApprovedRoundSha256=new string('a',64),OpeningSha256=new string('b',64),
            OfferSetSha256=new string('c',64),EvaluationDefinitionSha256=new string('d',64),
            MakerPrincipalId=Maker,
        };
        var revision=new PrincipalRfqRecommendationDraftRevision
        {
            TenantId=head.TenantId,LegalEntityId=head.LegalEntityId,
            DraftId=head.Id,RoundId=head.RoundId,UnsealingId=head.UnsealingId,
            SourcingEventId=head.SourcingEventId,BranchId=head.BranchId,
            Revision=revisionNo,ActorPrincipalId=Maker,RecordedAt=Now,
            PreviousRevisionId=previous?.Revision.Id,
            PreviousSnapshotSha256=previous?.Revision.SnapshotSha256,
            CommandKey=Guid.CreateVersion7(),RequestSha256=new string('f',64),
        };
        var offerId=Guid.CreateVersion7();var supplierId=Guid.CreateVersion7();
        var sourceLineId=Guid.CreateVersion7();var offerSha=new string('e',64);
        var opened=new[]{new PrincipalRfqRecommendationDraft.OfferLine(offerId,offerSha,
            supplierId,sourceLineId,"1","ZMW")};
        var approved=new[]{new PrincipalRfqRecommendationDraft.SourceLine(sourceLineId,"1")};
        var selected=new PrincipalRfqRecommendationDraft.Selection(offerId,sourceLineId,
            "1","5.1234","Reviewed source");
        var proposed=PrincipalRfqRecommendationDraft.Validate([selected],opened,approved,"ZMW");
        revision.SnapshotJson=PrincipalRfqRecommendationDraft.Snapshot(head,revision,proposed);
        revision.SnapshotSha256=ProcurementEvidenceEncoding.Digest(revision.SnapshotJson);
        head.CurrentRevision=revisionNo;head.CurrentRevisionId=revision.Id;
        head.CurrentSnapshotSha256=revision.SnapshotSha256;
        var currentLine=new PrincipalRfqRecommendationDraftLine
        {
            TenantId=head.TenantId,LegalEntityId=head.LegalEntityId,DraftId=head.Id,
            RevisionId=revision.Id,RoundId=head.RoundId,UnsealingId=head.UnsealingId,
            SourcingEventId=head.SourcingEventId,BranchId=head.BranchId,
            OfferId=offerId,OfferSha256=offerSha,SupplierId=supplierId,
            SourcingLineId=sourceLineId,QuantityExact="1",ProposedValueExact="5.1234",
            CurrencyCode="ZMW",Rationale="Reviewed source",
        };
        var policy=new PolicyVersion
        {
            TenantId=head.TenantId,LegalEntityId=head.LegalEntityId,
            PolicyKey=PrincipalRfqAwardFoundation.PolicyKey,PolicyVersionNumber=1,
            State="Published",RulesJson=Rules,EffectiveFrom=Now.AddDays(-1),
        };
        var reviewers=new[]{new PrincipalRfqAwardFoundation.CurrentPerson(Reviewer,null,false,
            new HashSet<Guid>{Branch},new HashSet<string>{"procurement-access",
                PrincipalRfqAwardFoundation.ReviewAction})};
        var maker=new PrincipalRfqAwardFoundation.CurrentPerson(Maker,null,false,
            new HashSet<Guid>{Branch},new HashSet<string>{"procurement-access",
                PrincipalRfqAwardFoundation.SubmitAction});
        var history=previous is null ? new[]{revision} : new[]{previous.Revision,revision};
        var source=new PrincipalRfqAwardReviewSourceGraph(history,[currentLine],opened,approved,
            [new PrincipalRfqAwardFoundation.RetainedLine(offerId,offerSha,supplierId,
                sourceLineId,"1","1","ZMW","5.1234")],maker,[]);
        var verified=PrincipalRfqAwardSourceQualification.VerifyCurrent(head,revision,history,
            [currentLine],opened,approved,"ZMW");
        var body=PrincipalRfqAwardFoundation.CommandBody(verified,policy.Id,Branch);
        var candidate=PrincipalRfqAwardFoundation.Qualify(verified,source.RetainedLines,
            policy,maker,reviewers,[],Guid.CreateVersion7(),head.TenantId,head.LegalEntityId,
            Branch,Guid.CreateVersion7(),body,ProcurementEvidenceEncoding.Digest(body),
            revisionNo,Now);
        return new(candidate,source,head,revision,policy,reviewers);
    }

    private static PrincipalRfqAwardReviewOpening Open(Fixture f,
        PrincipalRfqRecommendationSubmission? previous=null,
        PrincipalRfqAwardReviewOutcome? outcome=null,
        ApprovalCase? priorCase=null,ApprovalDecision? priorDecision=null)
        =>PrincipalRfqAwardReviewWorkflow.Prepare(f.Candidate,f.Source,f.Head,f.Revision,f.Policy,
            f.Reviewers,previous,outcome,priorCase,priorDecision,Now);

    [Fact]
    public void Exact_source_constructs_distinct_p36_freeze_and_real_Core_named_case_without_award_effect()
    {
        var f=Valid();var prepared=Open(f);
        Assert.Equal(f.Revision.Id,prepared.FrozenRecommendation.RevisionId);
        Assert.Equal(f.Revision.SnapshotSha256,prepared.FrozenRecommendation.RevisionSha256);
        Assert.Equal(prepared.FrozenRecommendation.Id,prepared.Submission.RecommendationSubmissionId);
        Assert.Equal(prepared.Submission.Id,prepared.CoreCase.SourceId);
        Assert.Equal(PrincipalRfqAwardReviewWorkflow.SourceType,prepared.CoreCase.SourceType);
        Assert.Equal("Pending",prepared.CoreCase.State);
        var stage=Assert.Single(prepared.CoreCase.Stages);
        Assert.Equal("All",stage.Mode);
        Assert.Equal(Reviewer,Assert.Single(stage.Assignments).AssigneeSubjectId);
        Assert.Equal(PrincipalRfqAwardFoundation.ReviewAction,stage.Assignments.Single().PermissionKey);
        Assert.Single(prepared.Lines);
        Assert.Contains("\"awardCreatedByThisCommand\":false",prepared.Receipt.ResponseJson);
        Assert.Equal(ProcurementEvidenceEncoding.Digest(prepared.Receipt.ResponseJson),
            prepared.Receipt.ResponseSha256);
    }

    [Fact]
    public void Current_revision_policy_scope_or_reviewer_alias_change_denies_before_Core_case()
    {
        var f=Valid();
        Assert.Throws<InvalidOperationException>(()=>Open(f with
            {Candidate=f.Candidate with {ExpectedRecommendationRevision=2}}));
        Assert.Throws<InvalidOperationException>(()=>Open(f with
            {Candidate=f.Candidate with {CompanyId=Guid.CreateVersion7()}}));
        Assert.Throws<InvalidOperationException>(()=>Open(f with
            {Policy=new PolicyVersion {TenantId="tenant-a",LegalEntityId=Company,
                PolicyKey=PrincipalRfqAwardFoundation.PolicyKey,PolicyVersionNumber=1,
                State="Draft",RulesJson=Rules,EffectiveFrom=Now.AddDays(-1)}}));
        Assert.Throws<InvalidOperationException>(()=>Open(f with
            {Reviewers=[f.Reviewers[0] with {WorkerAliasId=Guid.CreateVersion7(),
                AllowedBranches=new HashSet<Guid>{Guid.CreateVersion7()}}]}));
    }

    [Fact]
    public void Copied_candidate_cannot_substitute_selected_quantity_quote_or_total()
    {
        var f=Valid();
        Assert.Equal("RfqAwardQualifiedCandidateChanged",Assert.Throws<InvalidOperationException>(
            ()=>Open(f with {Candidate=f.Candidate with
                {Lines=[f.Candidate.Lines[0] with {QuantityExact="2"}]}})).Message);
        Assert.Equal("RfqAwardQualifiedCandidateChanged",Assert.Throws<InvalidOperationException>(
            ()=>Open(f with {Candidate=f.Candidate with
                {Lines=[f.Candidate.Lines[0] with {RetainedQuoteTotalExact="6"}],
                    ProposedTotalExact="6"}})).Message);
        Assert.Equal("RfqAwardQualifiedCandidateChanged",Assert.Throws<InvalidOperationException>(
            ()=>Open(f with {Candidate=f.Candidate with {Lines=[]}})).Message);
        Assert.Throws<InvalidOperationException>(()=>Open(f with {Source=f.Source with
            {RetainedLines=[f.Source.RetainedLines[0] with {RemainingHeldQuantityExact="0.5"}]}}));
    }

    [Fact]
    public void Pending_submission_blocks_revision_and_cancel_even_for_own_Draft()
    {
        var f=Valid();var opened=Open(f);
        Assert.Throws<InvalidOperationException>(()=>
            PrincipalRfqAwardReviewWorkflow.RequireDraftMutationAllowed(f.Head,
                opened.FrozenRecommendation,null,opened.CoreCase,null,"Revise"));
        Assert.Throws<InvalidOperationException>(()=>
            PrincipalRfqAwardReviewWorkflow.RequireDraftMutationAllowed(f.Head,
                opened.FrozenRecommendation,null,opened.CoreCase,null,"Cancel"));
    }

    [Fact]
    public void Returned_Core_fact_allows_next_revision_but_same_revision_and_Approved_deny()
    {
        var first=Valid();var opened=Open(first);
        var stage=Assert.Single(opened.CoreCase.Stages);
        var assignment=Assert.Single(stage.Assignments);
        var decisions=new List<ApprovalDecision>();
        ApprovalEngine.Decide(opened.CoreCase,assignment,
            new ApprovalActor(Reviewer,"tenant-a",Company,
                new HashSet<string>{PrincipalRfqAwardFoundation.ReviewAction}),
            "Return",Guid.CreateVersion7().ToString("D"),"Correct the source",
            decisions,Now.AddMinutes(1));
        var decision=Assert.Single(decisions);
        var returned=PrincipalRfqAwardCoreHistory.VerifyTerminal(opened.Submission,
            opened.CoreCase,first.Policy,decisions);
        Assert.Equal("Returned",returned.SourceOutcome);
        PrincipalRfqAwardReviewWorkflow.RequireDraftMutationAllowed(first.Head,
            opened.FrozenRecommendation,returned,opened.CoreCase,decision,"Revise");
        Assert.Throws<InvalidOperationException>(()=>Open(first,opened.FrozenRecommendation,
            returned,opened.CoreCase,decision));
        var second=Valid(2,first);
        var resubmitted=Open(second,opened.FrozenRecommendation,returned,opened.CoreCase,decision);
        Assert.Equal(2,resubmitted.CoreCase.SourceVersion);
        Assert.NotEqual(opened.CoreCase.Id,resubmitted.CoreCase.Id);
        var approved=new PrincipalRfqAwardReviewOutcome
        {
            TenantId="tenant-a",LegalEntityId=Company,
            SubmissionId=opened.Submission.Id,CoreCaseId=opened.CoreCase.Id,
            TerminalDecisionId=decision.Id,CoreState="Approved",SourceOutcome="Approved",
            RecordedAt=Now,
        };
        Assert.Throws<InvalidOperationException>(()=>
            PrincipalRfqAwardReviewWorkflow.RequireDraftMutationAllowed(second.Head,
                opened.FrozenRecommendation,approved,
                opened.CoreCase,decision,"Revise"));
    }

    [Fact]
    public void Core_approval_replay_records_only_pending_source_effect_not_a_fabricated_award()
    {
        var f=Valid();var opened=Open(f);
        var assignment=Assert.Single(Assert.Single(opened.CoreCase.Stages).Assignments);
        var decisions=new List<ApprovalDecision>();
        ApprovalEngine.Decide(opened.CoreCase,assignment,
            new ApprovalActor(Reviewer,"tenant-a",Company,
                new HashSet<string>{PrincipalRfqAwardFoundation.ReviewAction}),
            "Approve",Guid.CreateVersion7().ToString("D"),null,decisions,Now.AddMinutes(1));
        var fact=PrincipalRfqAwardCoreHistory.VerifyTerminal(opened.Submission,
            opened.CoreCase,f.Policy,decisions);
        Assert.Equal("Approved",fact.CoreState);
        Assert.Equal("CoreApprovedPendingAward",fact.SourceOutcome);
        var originalPins=opened.Submission.AssignmentPinsJson;
        opened.Submission.AssignmentPinsJson="{}";
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqAwardCoreHistory.VerifyTerminal(
            opened.Submission,opened.CoreCase,f.Policy,decisions));
        opened.Submission.AssignmentPinsJson=originalPins;
        opened.CoreCase.State="Returned";
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqAwardCoreHistory.VerifyTerminal(
            opened.Submission,opened.CoreCase,f.Policy,decisions));
        opened.CoreCase.State="Approved";
        decisions[0].ActorSubjectId=Maker;
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqAwardCoreHistory.VerifyTerminal(
            opened.Submission,opened.CoreCase,f.Policy,decisions));
    }
}
