using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPlanDetailActionTests
{
    [Theory]
    [InlineData("Returned")]
    [InlineData("Rejected")]
    [InlineData("Approved")]
    [InlineData("Withdrawn")]
    public void Old_closed_round_never_advertises_actions_after_plan_is_submitted_again(string state)
    {
        var (plan, round, approvalCase) = Current();
        round.State = state;
        round.CompletedAt = DateTimeOffset.UtcNow;
        approvalCase.State = state == "Withdrawn" ? "Cancelled" : state;
        approvalCase.CompletedAt = DateTimeOffset.UtcNow;
        plan.ApprovalInstanceRef = Guid.NewGuid().ToString("D");
        Assert.False(ProcurementPlanReviewReads.CurrentDetailRound(plan, round, approvalCase));
        Assert.True(PlanReviewSnapshot.TryReadRetained(round, out _));
    }

    [Fact]
    public void Pending_round_must_be_the_current_case_with_a_live_stage_assignment()
    {
        var (plan, round, approvalCase) = Current();
        Assert.True(ProcurementPlanReviewReads.CurrentDetailRound(plan, round, approvalCase));
        plan.ApprovalInstanceRef = Guid.NewGuid().ToString("D");
        Assert.False(ProcurementPlanReviewReads.CurrentDetailRound(plan, round, approvalCase));
        plan.ApprovalInstanceRef = approvalCase.Id.ToString("D");
        var assignment = approvalCase.Stages.Single().Assignments.Single();
        assignment.State = "Approved";
        Assert.False(ProcurementPlanReviewReads.CurrentDetailRound(plan, round, approvalCase));
        assignment.State = "Pending";
        approvalCase.State = "Cancelled";
        Assert.False(ProcurementPlanReviewReads.CurrentDetailRound(plan, round, approvalCase));
    }

    [Fact]
    public void Assignment_hints_require_named_independent_actor_current_stage_and_supported_permission()
    {
        var (plan, _, approvalCase) = Current();
        var stage = approvalCase.Stages.Single();
        var assignment = stage.Assignments.Single();
        bool Hint(string actor) => ProcurementPlanReviewReads.CanDecideDetailAssignment(plan, approvalCase, stage, assignment, actor);
        Assert.True(Hint("reviewer"));
        Assert.False(Hint("other"));
        assignment.AssigneeSubjectId = plan.OwnerSubjectId;
        Assert.False(Hint(plan.OwnerSubjectId));
        assignment.AssigneeSubjectId = "reviewer";
        assignment.DelegatedFromSubjectId = "delegator";
        Assert.False(Hint("reviewer"));
        assignment.DelegatedFromSubjectId = null;
        assignment.PermissionKey = PlanReviewCoreWorkflow.FinalPermission;
        Assert.False(Hint("reviewer"));
        plan.Status = "Reviewed";
        Assert.False(Hint("reviewer"));
        stage.Code = "Final";
        Assert.True(Hint("reviewer"));
        stage.State = "Completed";
        Assert.False(Hint("reviewer"));
    }

    private static (ProcurementPlan Plan, PlanReviewSubmission Round, ApprovalCase Case) Current()
    {
        var plan = new ProcurementPlan { TenantId = "tenant", LegalEntityId = Guid.NewGuid(),
            BranchRef = Guid.NewGuid(), OwnerSubjectId = "maker", CurrencyCode = "ZMW",
            FinancialPeriodRef = "FY2027", Number = "PLAN-1", Status = "Submitted", Version = 2 };
        plan.Lines.Add(new ProcurementPlanLine { ProcurementPlanId = plan.Id, LineNumber = 1,
            Description = "Forecast", UnitCode = "EA", EstimatedQuantity = 1m, EstimatedAmount = 2m });
        var snapshot = PlanReviewSnapshot.Create(plan, 2);
        var approvalCase = new ApprovalCase { TenantId = plan.TenantId, LegalEntityId = plan.LegalEntityId,
            SourceModule = "procurement", SourceType = PlanReviewCoreWorkflow.SourceType,
            SourceId = plan.Id, SourceVersion = 2, MakerSubjectId = plan.OwnerSubjectId,
            PolicyVersionId = Guid.NewGuid(), PolicyVersionNumber = 1 };
        var stage = new ApprovalStage { CaseId = approvalCase.Id, Code = "Review", State = "Active" };
        stage.Assignments.Add(new ApprovalAssignment { CaseId = approvalCase.Id, StageId = stage.Id,
            AssigneeSubjectId = "reviewer", PermissionKey = PlanReviewCoreWorkflow.ReviewPermission });
        approvalCase.Stages.Add(stage);
        plan.ApprovalInstanceRef = approvalCase.Id.ToString("D");
        var round = new PlanReviewSubmission { TenantId = plan.TenantId, LegalEntityId = plan.LegalEntityId,
            ProcurementPlanId = plan.Id, BranchRef = plan.BranchRef!.Value, SubmittedPlanVersion = 2,
            CoreApprovalCaseId = approvalCase.Id, PolicyVersionId = approvalCase.PolicyVersionId,
            PolicyVersionNumber = 1, MakerSubjectId = plan.OwnerSubjectId, CurrencyCode = plan.CurrencyCode,
            EstimatedAmount = snapshot.EstimatedAmount, SnapshotJson = snapshot.Json, SnapshotSha256 = snapshot.Sha256 };
        return (plan, round, approvalCase);
    }
}
