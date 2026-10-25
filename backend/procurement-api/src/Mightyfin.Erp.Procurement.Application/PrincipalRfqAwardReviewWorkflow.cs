using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PrincipalRfqAwardReviewOpening(
    PrincipalRfqRecommendationSubmission FrozenRecommendation,
    PrincipalRfqAwardReviewSubmission Submission,
    IReadOnlyList<PrincipalRfqAwardReviewLine> Lines,
    PrincipalRfqAwardReviewReceipt Receipt,
    ApprovalCase CoreCase);

/// <summary>Pure P36 freeze and P38 Core-case construction. This is not an
/// executable approval adapter: persisted source, complete Core history,
/// capacity locks, audit/outbox, grants and schema are future requirements.</summary>
public static class PrincipalRfqAwardReviewWorkflow
{
    public const string SourceType="PrincipalRfqSupplierAward";

    public static void RequireDraftMutationAllowed(
        PrincipalRfqRecommendationDraftHead head,
        PrincipalRfqRecommendationSubmission? latestSubmission,
        PrincipalRfqAwardReviewOutcome? latestOutcome,
        ApprovalCase? latestCoreCase,
        ApprovalDecision? terminalDecision,
        string operation)
    {
        Need(operation is "Revise" or "Cancel" && head.Id!=Guid.Empty,
            "RfqAwardDraftMutationInvalid");
        if(latestSubmission is null)
        {
            Need(latestOutcome is null && latestCoreCase is null && terminalDecision is null,
                "RfqAwardOrphanTerminalHistory");
            return;
        }
        Need(operation=="Revise" && latestSubmission.DraftId==head.Id
            && latestSubmission.AwardReviewSubmissionId!=Guid.Empty
            && latestSubmission.TenantId==head.TenantId
            && latestSubmission.LegalEntityId==head.LegalEntityId
            && latestSubmission.Revision<=head.CurrentRevision
            && (latestSubmission.Revision!=head.CurrentRevision
                || latestSubmission.RevisionId==head.CurrentRevisionId
                    && latestSubmission.RevisionSha256==head.CurrentSnapshotSha256)
            && latestOutcome is not null && latestCoreCase is not null
            && terminalDecision is not null
            && latestOutcome.SourceOutcome=="Returned"
            && latestOutcome.CoreState=="Returned"
            && latestOutcome.TenantId==head.TenantId
            && latestOutcome.LegalEntityId==head.LegalEntityId
            && latestCoreCase.State=="Returned"
            && latestCoreCase.CompletedAt is not null
            && latestOutcome.CoreCaseId==latestCoreCase.Id
            && latestOutcome.SubmissionId==latestSubmission.AwardReviewSubmissionId
            && latestCoreCase.SourceId==latestSubmission.AwardReviewSubmissionId
            && latestOutcome.TerminalDecisionId==terminalDecision.Id
            && terminalDecision.CaseId==latestCoreCase.Id
            && terminalDecision.Action=="Return"
            && terminalDecision.TenantId==head.TenantId
            && terminalDecision.LegalEntityId==head.LegalEntityId
            && terminalDecision.ActorSubjectId!=head.MakerPrincipalId
            && latestCoreCase.Stages.Any(stage=>stage.Id==terminalDecision.StageId
                && stage.State=="Returned"
                && stage.Assignments.Any(assignment=>assignment.Id==terminalDecision.AssignmentId
                    && assignment.State=="Returned"
                    && assignment.AssigneeSubjectId==terminalDecision.ActorSubjectId))
            && latestCoreCase.SourceType==SourceType
            && latestCoreCase.SourceModule=="procurement"
            && latestCoreCase.SourceVersion==latestSubmission.Revision
            && latestCoreCase.MakerSubjectId==head.MakerPrincipalId
            && latestCoreCase.TenantId==head.TenantId
            && latestCoreCase.LegalEntityId==head.LegalEntityId,
            "RfqAwardPendingOrTerminalDraftImmutable");
    }

    public static PrincipalRfqAwardReviewOpening Prepare(
        PrincipalRfqAwardCandidate candidate,
        PrincipalRfqRecommendationDraftHead head,
        PrincipalRfqRecommendationDraftRevision revision,
        PolicyVersion policy,
        IReadOnlyList<PrincipalRfqAwardFoundation.CurrentPerson> currentReviewers,
        PrincipalRfqRecommendationSubmission? previousSubmission,
        PrincipalRfqAwardReviewOutcome? previousOutcome,
        ApprovalCase? previousCoreCase,
        ApprovalDecision? previousTerminalDecision,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(currentReviewers);
        Need(now!=default && candidate.SubmissionId!=Guid.Empty
            && candidate.TenantId==head.TenantId
            && candidate.CompanyId==head.LegalEntityId
            && candidate.DraftId==head.Id
            && candidate.RecommendationRevisionId==revision.Id
            && candidate.RecommendationSha256==revision.SnapshotSha256
            && revision.SnapshotSha256==ProcurementEvidenceEncoding.Digest(revision.SnapshotJson)
            && revision.DraftId==head.Id
            && revision.Revision==head.CurrentRevision
            && revision.Id==head.CurrentRevisionId
            && revision.SnapshotSha256==head.CurrentSnapshotSha256
            && candidate.ExpectedRecommendationRevision==revision.Revision
            && candidate.RoundId==head.RoundId
            && candidate.UnsealingId==head.UnsealingId
            && candidate.ReviewBranchId==head.BranchId
            && candidate.MakerPrincipalId==head.MakerPrincipalId
            && candidate.MakerWorkerAliasId==head.MakerWorkerAliasId
            && candidate.PolicyVersionId==policy.Id
            && candidate.PolicyRulesSha256==ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
            && candidate.CommandKey!=Guid.Empty
            && candidate.RequestSha256==ProcurementEvidenceEncoding.Digest(
                PrincipalRfqAwardFoundation.CommandBody(
                    head.Id,revision.Id,revision.SnapshotSha256,revision.Revision,
                    policy.Id,head.BranchId))
            && policy.TenantId==head.TenantId
            && policy.LegalEntityId==head.LegalEntityId
            && policy.PolicyKey==PrincipalRfqAwardFoundation.PolicyKey
            && policy.State=="Published"
            && policy.EffectiveFrom<=now
            && !(policy.EffectiveTo is { } until && until<=now)
            && candidate.Lines.Count is >0 and <=500,
            "RfqAwardFrozenSourceChanged");
        if(previousSubmission is not null)
        {
            RequireDraftMutationAllowed(head,previousSubmission,previousOutcome,
                previousCoreCase,previousTerminalDecision,"Revise");
            Need(previousSubmission.Revision<revision.Revision,
                "RfqAwardReturnedRevisionMustAdvance");
        }
        else Need(previousOutcome is null && previousCoreCase is null
                && previousTerminalDecision is null,"RfqAwardOrphanTerminalHistory");
        var rules=PrincipalRfqAwardFoundation.ParseRules(policy.RulesJson);
        Need(rules.ReviewBranchId==head.BranchId
            && rules.CurrencyCode==candidate.CurrencyCode
            && currentReviewers.Count==rules.Stages.Sum(x=>x.PrincipalIds.Count)
            && currentReviewers.Select(x=>x.PrincipalId).ToHashSet(StringComparer.Ordinal)
                .SetEquals(rules.Stages.SelectMany(x=>x.PrincipalIds)),
            "RfqAwardNamedReviewersChanged");
        foreach(var person in currentReviewers)
            Need(ProcurementPrincipalEvidence.Canonical(person.PrincipalId)
                && ProcurementPrincipalEvidence.OptionalWorker(person.WorkerAliasId)
                && person.Actions.Contains("procurement-access")
                && person.Actions.Contains(PrincipalRfqAwardFoundation.ReviewAction)
                && (person.OrganisationWide || person.AllowedBranches.Contains(head.BranchId))
                && ProcurementPrincipalEvidence.Independent(head.MakerPrincipalId,
                    head.MakerWorkerAliasId,person.PrincipalId,person.WorkerAliasId),
                "RfqAwardCurrentReviewerUnavailable");
        for(var i=0;i<currentReviewers.Count;i++)
            for(var j=i+1;j<currentReviewers.Count;j++)
                Need(ProcurementPrincipalEvidence.Independent(currentReviewers[i].PrincipalId,
                    currentReviewers[i].WorkerAliasId,currentReviewers[j].PrincipalId,
                    currentReviewers[j].WorkerAliasId),"RfqAwardReviewersNotIndependent");
        var at=ProcurementEvidenceEncoding.DatabaseTimestamp(now)!.Value;
        var frozen=new PrincipalRfqRecommendationSubmission
        {
            TenantId=head.TenantId,LegalEntityId=head.LegalEntityId,
            DraftId=head.Id,AwardReviewSubmissionId=candidate.SubmissionId,
            RevisionId=revision.Id,Revision=revision.Revision,
            RevisionSha256=revision.SnapshotSha256,RoundId=head.RoundId,
            UnsealingId=head.UnsealingId,SourcingEventId=head.SourcingEventId,
            BranchId=head.BranchId,MakerPrincipalId=head.MakerPrincipalId,
            MakerWorkerAliasId=head.MakerWorkerAliasId,CommandKey=candidate.CommandKey,
            RequestSha256=candidate.RequestSha256,SubmittedAt=at,
            CreatedAt=at,CreatedBy=head.MakerPrincipalId,
            SnapshotJson=revision.SnapshotJson,
            SnapshotSha256=revision.SnapshotSha256,
        };
        var coreId=Guid.CreateVersion7();
        var row=new PrincipalRfqAwardReviewSubmission
        {
            Id=candidate.SubmissionId,TenantId=head.TenantId,
            LegalEntityId=head.LegalEntityId,
            RecommendationSubmissionId=frozen.Id,DraftId=head.Id,
            RecommendationRevisionId=revision.Id,
            RecommendationRevision=revision.Revision,
            RecommendationSha256=revision.SnapshotSha256,
            RoundId=head.RoundId,UnsealingId=head.UnsealingId,
            SourcingEventId=head.SourcingEventId,BranchId=head.BranchId,CoreCaseId=coreId,
            PolicyVersionId=policy.Id,PolicyVersionNumber=policy.PolicyVersionNumber,
            PolicyKey=PrincipalRfqAwardFoundation.PolicyKey,
            PolicyRulesSha256=candidate.PolicyRulesSha256,
            MakerPrincipalId=head.MakerPrincipalId,
            MakerWorkerAliasId=head.MakerWorkerAliasId,
            CommandKey=candidate.CommandKey,RequestSha256=candidate.RequestSha256,
            CurrencyCode=candidate.CurrencyCode,
            ProposedTotalExact=candidate.ProposedTotalExact,
            SubmittedAt=at,CreatedAt=at,CreatedBy=head.MakerPrincipalId,
        };
        var core=new ApprovalCase
        {
            Id=coreId,TenantId=head.TenantId,LegalEntityId=head.LegalEntityId,
            SourceModule="procurement",SourceType=SourceType,SourceId=row.Id,
            SourceVersion=revision.Revision,PolicyVersionId=policy.Id,
            PolicyVersionNumber=policy.PolicyVersionNumber,
            MakerSubjectId=head.MakerPrincipalId,
            OpenedAt=at,CreatedAt=at,UpdatedAt=at,
        };
        var sequence=0;
        foreach(var configured in rules.Stages)
        {
            var stage=new ApprovalStage
            {
                TenantId=head.TenantId,LegalEntityId=head.LegalEntityId,
                CaseId=core.Id,Sequence=++sequence,Code=configured.Code,
                Mode=configured.Mode=="One"?"Any":"All",
                RequiredApprovals=configured.Mode=="One"?1:configured.PrincipalIds.Count,
                State=sequence==1?"Active":"Waiting",
                CreatedAt=at,UpdatedAt=at,
            };
            foreach(var principal in configured.PrincipalIds)
                stage.Assignments.Add(new ApprovalAssignment
                {
                    TenantId=head.TenantId,LegalEntityId=head.LegalEntityId,
                    CaseId=core.Id,StageId=stage.Id,
                    AssigneeSubjectId=principal,
                    PermissionKey=PrincipalRfqAwardFoundation.ReviewAction,
                    CreatedAt=at,UpdatedAt=at,
                });
            core.Stages.Add(stage);
        }
        ApprovalEngine.ValidateNewCase(core);
        row.AssignmentPinsJson=JsonSerializer.Serialize(new
        {
            schemaVersion=1,caseId=core.Id,submissionId=row.Id,
            stages=core.Stages.OrderBy(x=>x.Sequence).Select(x=>new
            {
                x.Id,x.Sequence,x.Code,x.Mode,x.RequiredApprovals,
                assignments=x.Assignments.OrderBy(a=>a.AssigneeSubjectId,StringComparer.Ordinal)
                    .Select(a=>new {a.Id,a.AssigneeSubjectId,
                        workerAliasId=currentReviewers.Single(p=>p.PrincipalId==a.AssigneeSubjectId).WorkerAliasId})
                    .ToArray(),
            }).ToArray(),
        });
        row.AssignmentPinsSha256=ProcurementEvidenceEncoding.Digest(row.AssignmentPinsJson);
        var lines=candidate.Lines.Select(x=>new PrincipalRfqAwardReviewLine
        {
            TenantId=head.TenantId,LegalEntityId=head.LegalEntityId,
            SubmissionId=row.Id,RecommendationSubmissionId=frozen.Id,
            RoundId=head.RoundId,UnsealingId=head.UnsealingId,SourcingEventId=head.SourcingEventId,BranchId=head.BranchId,
            OfferId=x.OfferId,OfferSha256=x.OfferSha256,SupplierId=x.SupplierId,
            SourcingLineId=x.SourcingLineId,QuantityExact=x.QuantityExact,
            CurrencyCode=x.CurrencyCode,RetainedQuoteTotalExact=x.RetainedQuoteTotalExact,
            CreatedAt=at,CreatedBy=head.MakerPrincipalId,
        }).ToArray();
        var response=ResponseFor(row);
        var receipt=new PrincipalRfqAwardReviewReceipt
        {
            TenantId=head.TenantId,LegalEntityId=head.LegalEntityId,
            SubmissionId=row.Id,RecommendationSubmissionId=frozen.Id,
            RoundId=head.RoundId,SourcingEventId=head.SourcingEventId,BranchId=head.BranchId,
            ActorPrincipalId=head.MakerPrincipalId,CommandKey=candidate.CommandKey,
            RequestSha256=candidate.RequestSha256,
            ResponseJson=response,ResponseSha256=ProcurementEvidenceEncoding.Digest(response),
            CreatedAt=at,CreatedBy=head.MakerPrincipalId,
        };
        return new(frozen,row,Array.AsReadOnly(lines),receipt,core);
    }

    public static string ResponseFor(PrincipalRfqAwardReviewSubmission row) => JsonSerializer.Serialize(new
        {
            schemaVersion=1,submissionId=row.Id,recommendationSubmissionId=row.RecommendationSubmissionId,
            coreCaseId=row.CoreCaseId,state="PendingReview",policyVersionId=row.PolicyVersionId,
            recommendationSha256=row.RecommendationSha256,
            awardCreatedByThisCommand=false,purchaseOrderCreatedByThisCommand=false,
        });

    private static void Need(bool valid,string code)
    {if(!valid)throw new InvalidOperationException(code);}
}
