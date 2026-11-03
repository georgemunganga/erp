using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PlanReviewPrincipal(Guid Id, Guid? LinkedWorkerId, string TenantId,
    Guid LegalEntityId, bool OrganisationWide, IReadOnlySet<Guid> Branches,
    IReadOnlySet<string> Permissions);

/// <summary>Pure Core case graph. The host verifies current Core actors, branch grants,
/// policy publication evidence and store readiness inside the owning transaction.</summary>
public static class PlanReviewCoreWorkflow
{
    public const string SourceType = "procurement_plan";
    public const string ReviewPermission = "procurement-plan-review";
    public const string FinalPermission = "procurement-plan-approve";
    public const string WithdrawPermission = "procurement-plan-withdraw";

    public static ApprovalCase Open(ProcurementPlan plan, PlanReviewSubmission submission,
        PolicyVersion policy, PlanReviewPolicyRules.Definition definition,
        PlanReviewPrincipal maker, IReadOnlyList<PlanReviewPrincipal> assignees, DateTimeOffset now)
    {
        if (plan.BranchRef is not { } branch || branch == Guid.Empty || plan.Status != "Submitted"
            || !PlanReviewSnapshot.Matches(plan, submission)
            || submission.CoreApprovalCaseId == Guid.Empty
            || submission.PolicyVersionId != policy.Id || submission.PolicyVersionNumber != policy.PolicyVersionNumber
            || submission.PolicyRulesSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
            || policy.PolicyKey != PlanReviewPolicyRules.PolicyKey || policy.State != "Published"
            || policy.EffectiveFrom > now || policy.EffectiveTo is { } end && now >= end
            || policy.TenantId != plan.TenantId || policy.LegalEntityId != plan.LegalEntityId
            || submission.MakerSubjectId != plan.OwnerSubjectId
            || maker.Id.ToString("D") != plan.OwnerSubjectId
            || submission.MakerCoreLinkedWorkerId != maker.LinkedWorkerId
            || !Current(maker, plan, branch, "procurement-plan-submit")
            || !SameConfiguration(PlanReviewPolicyRules.Read(policy.RulesJson), definition)
            || definition.Stages.Count != 2 || definition.Stages[0].Purpose != "Review"
            || definition.Stages[1].Purpose != "Final")
            throw new InvalidOperationException("PlanReviewOpeningUnavailable");
        var named = definition.Stages.SelectMany(x => x.AssigneePrincipalIds).ToArray();
        if (named.Length != assignees.Count || named.Length != named.Distinct().Count()
            || assignees.Select(x => x.Id).Distinct().Count() != assignees.Count
            || !named.ToHashSet().SetEquals(assignees.Select(x => x.Id)))
            throw new InvalidOperationException("PlanReviewAssigneeSetChanged");
        foreach (var assignee in assignees)
        {
            var permission = definition.Stages[0].AssigneePrincipalIds.Contains(assignee.Id)
                ? ReviewPermission : FinalPermission;
            if (!Current(assignee, plan, branch, permission)
                || assignee.Id == maker.Id
                || assignee.LinkedWorkerId is { } linked && linked != Guid.Empty
                    && linked == maker.LinkedWorkerId)
                throw new InvalidOperationException("PlanReviewAssigneeUnavailable");
        }
        var approvalCase = new ApprovalCase
        {
            Id = submission.CoreApprovalCaseId,
            TenantId = plan.TenantId, LegalEntityId = plan.LegalEntityId,
            SourceModule = "procurement", SourceType = SourceType, SourceId = plan.Id,
            SourceVersion = submission.SubmittedPlanVersion, PolicyVersionId = policy.Id,
            PolicyVersionNumber = policy.PolicyVersionNumber, MakerSubjectId = plan.OwnerSubjectId,
            OpenedAt = now,
        };
        for (var index = 0; index < 2; index++)
        {
            var configured = definition.Stages[index];
            var stage = new ApprovalStage
            {
                TenantId = plan.TenantId, LegalEntityId = plan.LegalEntityId,
                CaseId = approvalCase.Id, Sequence = index + 1,
                Code = configured.Purpose, Mode = configured.Mode,
                RequiredApprovals = configured.RequiredApprovals,
                State = index == 0 ? "Active" : "Waiting",
            };
            foreach (var id in configured.AssigneePrincipalIds)
                stage.Assignments.Add(new ApprovalAssignment
                {
                    TenantId = plan.TenantId, LegalEntityId = plan.LegalEntityId,
                    CaseId = approvalCase.Id, StageId = stage.Id,
                    AssigneeSubjectId = id.ToString("D"),
                    PermissionKey = index == 0 ? ReviewPermission : FinalPermission,
                });
            approvalCase.Stages.Add(stage);
        }
        ApprovalEngine.ValidateNewCase(approvalCase);
        return approvalCase;
    }

    public static ApprovalDecision Decide(ProcurementPlan plan, PlanReviewSubmission submission,
        ApprovalCase approvalCase, ApprovalAssignment assignment, PlanReviewPrincipal maker,
        PlanReviewPrincipal actor, string action, string commandKey, string? reason,
        ICollection<ApprovalDecision> decisions, DateTimeOffset now)
    {
        if (plan.BranchRef is not { } branch || !PlanReviewSnapshot.Matches(plan, submission)
            || submission.State != "Pending" || approvalCase.State != "Pending"
            || approvalCase.TenantId != plan.TenantId || approvalCase.LegalEntityId != plan.LegalEntityId
            || approvalCase.SourceModule != "procurement" || approvalCase.SourceType != SourceType
            || approvalCase.SourceId != plan.Id || approvalCase.SourceVersion != submission.SubmittedPlanVersion
            || approvalCase.Id != submission.CoreApprovalCaseId || approvalCase.PolicyVersionId != submission.PolicyVersionId
            || approvalCase.PolicyVersionNumber != submission.PolicyVersionNumber
            || approvalCase.MakerSubjectId != plan.OwnerSubjectId || maker.Id.ToString("D") != plan.OwnerSubjectId
            || maker.LinkedWorkerId != submission.MakerCoreLinkedWorkerId
            || assignment.DelegatedFromSubjectId is not null || actor.Id.ToString("D") != assignment.AssigneeSubjectId
            || actor.Id == maker.Id || actor.LinkedWorkerId is { } linked && linked != Guid.Empty
                && linked == maker.LinkedWorkerId
            || action is not ("Approve" or "Return" or "Reject")
            || assignment.PermissionKey is not (ReviewPermission or FinalPermission)
            || !Current(actor, plan, branch, assignment.PermissionKey))
            throw new InvalidOperationException("PlanReviewDecisionUnavailable");
        var stage = approvalCase.Stages.SingleOrDefault(x => x.Id == assignment.StageId)
            ?? throw new InvalidOperationException("PlanReviewStageUnavailable");
        if (stage.Code == "Review" && (plan.Status != "Submitted" || assignment.PermissionKey != ReviewPermission)
            || stage.Code == "Final" && (plan.Status != "Reviewed" || assignment.PermissionKey != FinalPermission)
            || stage.Code is not ("Review" or "Final"))
            throw new InvalidOperationException("PlanReviewStageMismatch");
        var priorCount = decisions.Count;
        var result = ApprovalEngine.Decide(approvalCase, assignment,
            new ApprovalActor(actor.Id.ToString("D"), plan.TenantId, plan.LegalEntityId, actor.Permissions),
            action, commandKey, reason, decisions, now);
        if (!result.Recorded || decisions.Count != priorCount + 1)
            throw new InvalidOperationException("PlanReviewDecisionReceiptRequired");
        var decision = decisions.Last();
        var outcome = PlanReviewLifecycle.Review(plan.Status, action, approvalCase.State, stage.State);
        plan.Status = outcome.PlanState;
        if (outcome.RoundState != "Pending")
        {
            submission.State = outcome.RoundState;
            submission.CompletedAt = now;
        }
        return decision;
    }

    public static ApprovalDecision Withdraw(ProcurementPlan plan, PlanReviewSubmission submission,
        ApprovalCase approvalCase, ApprovalAssignment activeAssignment, PlanReviewPrincipal maker,
        string commandKey, string reason, ICollection<ApprovalDecision> decisions, DateTimeOffset now)
    {
        if (plan.BranchRef is not { } branch || plan.Status is not ("Submitted" or "Reviewed")
            || !PlanReviewSnapshot.Matches(plan, submission) || submission.State != "Pending"
            || approvalCase.Id != submission.CoreApprovalCaseId || approvalCase.SourceId != plan.Id
            || approvalCase.SourceModule != "procurement" || approvalCase.SourceType != SourceType
            || approvalCase.SourceVersion != submission.SubmittedPlanVersion
            || approvalCase.MakerSubjectId != plan.OwnerSubjectId
            || approvalCase.TenantId != plan.TenantId || approvalCase.LegalEntityId != plan.LegalEntityId
            || maker.Id.ToString("D") != plan.OwnerSubjectId
            || !Current(maker, plan, branch, WithdrawPermission))
            throw new InvalidOperationException("PlanReviewWithdrawalUnavailable");
        var priorCount = decisions.Count;
        var result = ApprovalEngine.Cancel(approvalCase, activeAssignment,
            new ApprovalActor(maker.Id.ToString("D"), plan.TenantId, plan.LegalEntityId, maker.Permissions),
            WithdrawPermission, commandKey, reason, decisions, now);
        if (!result.Recorded || decisions.Count != priorCount + 1)
            throw new InvalidOperationException("PlanReviewWithdrawalReceiptRequired");
        var state = PlanReviewLifecycle.Withdraw(plan.Status, approvalCase.State);
        plan.Status = state.PlanState;
        plan.StatusReason = reason.Trim();
        plan.ApprovalInstanceRef = null;
        submission.State = state.RoundState;
        submission.CompletedAt = now;
        return decisions.Last();
    }

    private static bool Current(PlanReviewPrincipal person, ProcurementPlan plan, Guid branch, string action) =>
        person.Id != Guid.Empty && person.TenantId == plan.TenantId
        && person.LegalEntityId == plan.LegalEntityId
        && person.Permissions.Contains("procurement-access")
        && person.Permissions.Contains(action)
        && (person.OrganisationWide || person.Branches.Contains(branch));

    private static bool SameConfiguration(PlanReviewPolicyRules.Definition? policy,
        PlanReviewPolicyRules.Definition given) => policy is not null && policy.Stages.Count == given.Stages.Count
        && policy.Stages.Zip(given.Stages).All(pair =>
            pair.First.Purpose == pair.Second.Purpose && pair.First.Mode == pair.Second.Mode
            && pair.First.RequiredApprovals == pair.Second.RequiredApprovals
            && pair.First.AssigneePrincipalIds.SequenceEqual(pair.Second.AssigneePrincipalIds));
}
