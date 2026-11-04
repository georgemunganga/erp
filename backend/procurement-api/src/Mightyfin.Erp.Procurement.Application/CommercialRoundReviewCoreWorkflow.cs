using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Source-owned state validation around the shared Core approval engine.</summary>
public static class CommercialRoundReviewCoreWorkflow
{
    public const string SourceType = "commercial_round_plan";
    public const string SubmitPermission = "procurement-round-plan-submit";
    public const string ReviewPermission = "procurement-round-plan-review";

    public static ApprovalCase Open(CommercialRoundPlan plan, CommercialRoundPlanRevision submitted,
        CommercialRoundReviewSubmission round, PolicyVersion policy,
        CommercialRoundReviewPolicyRules.Definition rules, PlanReviewPrincipal maker,
        IReadOnlyList<PlanReviewPrincipal> reviewers, RoundPlanActor planMaker,
        RoundPlanActor materialAuthor, DateTimeOffset now)
    {
        if (plan.Status != "InReview" || submitted.Operation != "Submit" || submitted.ResultStatus != "InReview"
            || submitted.Id != round.SubmittedRevisionId || submitted.ResultVersion != round.SubmittedPlanVersion
            || submitted.ActorPrincipalId != round.MakerPrincipalId
            || submitted.ActorWorkerAlias != round.MakerWorkerAlias
            || submitted.ContentSha256 != round.SubmittedContentSha256
            || plan.Version == long.MaxValue || plan.Version + 1 != submitted.ResultVersion
            || round.PlanId != plan.Id || round.SourcingEventId != plan.SourcingEventId || round.BranchId != plan.BranchId
            || round.CoreCaseId == Guid.Empty || round.PolicyVersionId != policy.Id
            || round.PolicyVersionNumber != policy.PolicyVersionNumber
            || round.PolicyRulesSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
            || policy.PolicyKey != CommercialRoundReviewPolicyRules.PolicyKey || policy.State != "Published"
            || policy.EffectiveFrom > now || policy.EffectiveTo is { } end && now >= end
            || policy.TenantId != plan.TenantId || policy.LegalEntityId != plan.LegalEntityId
            || maker.Id.ToString("D") != round.MakerPrincipalId || maker.LinkedWorkerId != round.MakerWorkerAlias
            || planMaker.PrincipalId != plan.MakerPrincipalId
            || !Current(maker, plan, SubmitPermission)
            || reviewers.Count != rules.AssigneePrincipalIds.Count
            || !rules.AssigneePrincipalIds.ToHashSet().SetEquals(reviewers.Select(x => x.Id)))
            throw new InvalidOperationException("RoundReviewOpeningUnavailable");
        foreach (var reviewer in reviewers)
            if (!Current(reviewer, plan, ReviewPermission) || !Independent(reviewer, planMaker)
                || !Independent(reviewer, materialAuthor) || reviewer.Id == maker.Id
                || reviewer.LinkedWorkerId is { } alias && alias != Guid.Empty && alias == maker.LinkedWorkerId)
                throw new InvalidOperationException("RoundReviewerUnavailable");
        var approvalCase = new ApprovalCase
        {
            Id = round.CoreCaseId, TenantId = plan.TenantId, LegalEntityId = plan.LegalEntityId,
            SourceModule = "procurement", SourceType = SourceType, SourceId = plan.Id,
            SourceVersion = submitted.ResultVersion, PolicyVersionId = policy.Id,
            PolicyVersionNumber = policy.PolicyVersionNumber, MakerSubjectId = round.MakerPrincipalId,
            OpenedAt = now,
        };
        var stage = new ApprovalStage
        {
            TenantId = plan.TenantId, LegalEntityId = plan.LegalEntityId,
            CaseId = approvalCase.Id, Sequence = 1, Code = "RoundPlanReview",
            Mode = rules.Mode, RequiredApprovals = rules.RequiredApprovals, State = "Active",
        };
        foreach (var id in rules.AssigneePrincipalIds)
            stage.Assignments.Add(new ApprovalAssignment
            {
                TenantId = plan.TenantId, LegalEntityId = plan.LegalEntityId,
                CaseId = approvalCase.Id, StageId = stage.Id,
                AssigneeSubjectId = id.ToString("D"), PermissionKey = ReviewPermission,
            });
        approvalCase.Stages.Add(stage);
        ApprovalEngine.ValidateNewCase(approvalCase);
        return approvalCase;
    }

    public static (ApprovalDecision Decision, string ResultStatus) Decide(
        CommercialRoundPlan plan, CommercialRoundReviewSubmission round, ApprovalCase approvalCase,
        ApprovalAssignment assignment, PlanReviewPrincipal actor, RoundPlanActor planMaker,
        RoundPlanActor materialAuthor, string action, string key,
        string? reason, ICollection<ApprovalDecision> decisions, DateTimeOffset now)
    {
        if (plan.Status != "InReview" || round.PlanId != plan.Id || plan.Version < round.SubmittedPlanVersion
            || round.SubmittedContentSha256.Length != 64 || approvalCase.State != "Pending"
            || approvalCase.Id != round.CoreCaseId || approvalCase.TenantId != plan.TenantId
            || approvalCase.LegalEntityId != plan.LegalEntityId || approvalCase.SourceModule != "procurement"
            || approvalCase.SourceType != SourceType || approvalCase.SourceId != plan.Id
            || approvalCase.SourceVersion != round.SubmittedPlanVersion
            || approvalCase.PolicyVersionId != round.PolicyVersionId
            || approvalCase.PolicyVersionNumber != round.PolicyVersionNumber
            || approvalCase.MakerSubjectId != round.MakerPrincipalId
            || planMaker.PrincipalId != plan.MakerPrincipalId
            || assignment.AssigneeSubjectId != actor.Id.ToString("D")
            || assignment.DelegatedFromSubjectId is not null
            || actor.Id.ToString("D") == round.MakerPrincipalId
            || !Independent(actor, planMaker) || !Independent(actor, materialAuthor)
            || actor.LinkedWorkerId is { } linked && linked != Guid.Empty && linked == round.MakerWorkerAlias
            || !Current(actor, plan, ReviewPermission) || assignment.PermissionKey != ReviewPermission
            || action is not ("Approve" or "Return" or "Reject"))
            throw new InvalidOperationException("RoundReviewDecisionUnavailable");
        var stage = approvalCase.Stages.SingleOrDefault(x => x.Id == assignment.StageId);
        if (stage is null || stage.Code != "RoundPlanReview" || stage.State != "Active")
            throw new InvalidOperationException("RoundReviewStageInvalid");
        var before = decisions.Count;
        var result = ApprovalEngine.Decide(approvalCase, assignment,
            new ApprovalActor(actor.Id.ToString("D"), plan.TenantId, plan.LegalEntityId, actor.Permissions),
            action, key, reason, decisions, now);
        if (!result.Recorded || decisions.Count != before + 1)
            throw new InvalidOperationException("RoundReviewDecisionReceiptRequired");
        var status = action switch
        {
            "Return" => "Draft",
            "Reject" => "Rejected",
            "Approve" when result.CaseCompleted && result.State == "Approved" => "Published",
            "Approve" when result.State == "Pending" => "InReview",
            _ => throw new InvalidOperationException("RoundReviewCaseStateInvalid"),
        };
        return (decisions.Last(), status);
    }

    private static bool Current(PlanReviewPrincipal person, CommercialRoundPlan plan, string action) =>
        person.Id != Guid.Empty && person.TenantId == plan.TenantId
        && person.LegalEntityId == plan.LegalEntityId
        && person.Permissions.Contains("procurement-access") && person.Permissions.Contains(action)
        && (person.OrganisationWide || person.Branches.Contains(plan.BranchId));
    public static bool PendingTask(ApprovalCase approvalCase, ApprovalStage stage,
        ApprovalAssignment assignment, CommercialRoundReviewSubmission round,
        CommercialRoundPlan plan, long latestSubmittedVersion) =>
        approvalCase.State == "Pending" && stage.State == "Active"
        && stage.Code == "RoundPlanReview" && stage.Sequence == 1
        && assignment.State == "Pending" && assignment.PermissionKey == ReviewPermission
        && assignment.DelegatedFromSubjectId is null
        && stage.CaseId == approvalCase.Id && assignment.CaseId == approvalCase.Id
        && assignment.StageId == stage.Id && approvalCase.Id == round.CoreCaseId
        && approvalCase.TenantId == round.TenantId && approvalCase.LegalEntityId == round.LegalEntityId
        && approvalCase.SourceModule == "procurement" && approvalCase.SourceType == SourceType
        && approvalCase.SourceId == plan.Id && approvalCase.SourceVersion == round.SubmittedPlanVersion
        && approvalCase.PolicyVersionId == round.PolicyVersionId
        && approvalCase.PolicyVersionNumber == round.PolicyVersionNumber
        && plan.TenantId == round.TenantId && plan.LegalEntityId == round.LegalEntityId
        && plan.SourcingEventId == round.SourcingEventId && plan.BranchId == round.BranchId
        && plan.Status == "InReview" && plan.Version >= round.SubmittedPlanVersion
        && round.SubmittedPlanVersion == latestSubmittedVersion;
    private static bool Independent(PlanReviewPrincipal reviewer, RoundPlanActor other) =>
        reviewer.Id.ToString("D") != other.PrincipalId
        && (reviewer.LinkedWorkerId is null || other.WorkerAlias is null
            || reviewer.LinkedWorkerId == Guid.Empty || other.WorkerAlias == Guid.Empty
            || reviewer.LinkedWorkerId != other.WorkerAlias);
}
