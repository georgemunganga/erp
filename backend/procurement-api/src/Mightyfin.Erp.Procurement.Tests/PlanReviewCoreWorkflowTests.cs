using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Xunit;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PlanReviewCoreWorkflowTests
{
    private static readonly Guid Company = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid Branch = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid Maker = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Reviewer = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Approver = Guid.Parse("33333333-3333-4333-8333-333333333333");

    [Fact]
    public void Builds_two_named_Core_stages_with_no_default_or_worker_requirement()
    {
        var (plan, submission, policy, definition, maker, assignees) = Ready();
        var workflow = PlanReviewCoreWorkflow.Open(plan, submission, policy, definition, maker, assignees,
            DateTimeOffset.UtcNow);
        Assert.Equal(plan.Id, workflow.SourceId);
        Assert.Equal(submission.SubmittedPlanVersion, workflow.SourceVersion);
        Assert.Equal(new[] { "Review", "Final" }, workflow.Stages.OrderBy(x => x.Sequence).Select(x => x.Code));
        Assert.All(workflow.Stages.SelectMany(x => x.Assignments), assignment =>
            Assert.Null(assignment.DelegatedFromSubjectId));
    }

    [Fact]
    public void Denies_cross_branch_and_known_person_alias_of_maker()
    {
        var (plan, submission, policy, definition, maker, assignees) = Ready();
        var wrongBranch = assignees.ToArray();
        wrongBranch[0] = wrongBranch[0] with { OrganisationWide = false, Branches = new HashSet<Guid>() };
        Assert.Throws<InvalidOperationException>(() => PlanReviewCoreWorkflow.Open(plan, submission,
            policy, definition, maker, wrongBranch, DateTimeOffset.UtcNow));

        var knownAlias = Guid.NewGuid();
        maker = maker with { LinkedWorkerId = knownAlias };
        var samePerson = assignees.ToArray();
        samePerson[1] = samePerson[1] with { LinkedWorkerId = knownAlias };
        Assert.Throws<InvalidOperationException>(() => PlanReviewCoreWorkflow.Open(plan, submission,
            policy, definition, maker, samePerson, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Shared_decisions_move_plan_to_reviewed_then_active_once()
    {
        var (plan, submission, policy, definition, maker, assignees) = Ready();
        var now = DateTimeOffset.UtcNow;
        var workflow = PlanReviewCoreWorkflow.Open(plan, submission, policy, definition, maker, assignees, now);
        var decisions = new List<Mightyfin.Erp.Core.Approvals.Domain.ApprovalDecision>();
        var review = workflow.Stages.Single(x => x.Code == "Review").Assignments.Single();
        _ = PlanReviewCoreWorkflow.Decide(plan, submission, workflow, review, maker, assignees[0],
            "Approve", Guid.NewGuid().ToString("D"), null, decisions, now);
        Assert.Equal("Reviewed", plan.Status);
        Assert.Equal("Pending", submission.State);
        var final = workflow.Stages.Single(x => x.Code == "Final").Assignments.Single();
        _ = PlanReviewCoreWorkflow.Decide(plan, submission, workflow, final, maker, assignees[1],
            "Approve", Guid.NewGuid().ToString("D"), null, decisions, now);
        Assert.Equal("Active", plan.Status);
        Assert.Equal("Approved", submission.State);
        Assert.Equal(2, decisions.Count);
        Assert.Throws<InvalidOperationException>(() => PlanReviewCoreWorkflow.Decide(plan, submission,
            workflow, final, maker, assignees[1], "Approve", Guid.NewGuid().ToString("D"), null, decisions, now));
    }

    [Fact]
    public void Current_action_revocation_blocks_decision_without_changing_case()
    {
        var (plan, submission, policy, definition, maker, assignees) = Ready();
        var workflow = PlanReviewCoreWorkflow.Open(plan, submission, policy, definition, maker, assignees,
            DateTimeOffset.UtcNow);
        var reviewer = assignees[0] with { Permissions = new HashSet<string> { "procurement-access" } };
        var decisions = new List<Mightyfin.Erp.Core.Approvals.Domain.ApprovalDecision>();
        Assert.Throws<InvalidOperationException>(() => PlanReviewCoreWorkflow.Decide(plan, submission,
            workflow, workflow.Stages.Single(x => x.Code == "Review").Assignments.Single(), maker, reviewer,
            "Approve", Guid.NewGuid().ToString("D"), null, decisions, DateTimeOffset.UtcNow));
        Assert.Empty(decisions);
        Assert.Equal("Submitted", plan.Status);
    }

    [Fact]
    public void Maker_withdraws_open_round_and_can_edit_draft_for_new_submission()
    {
        var (plan, submission, policy, definition, maker, assignees) = Ready();
        maker = maker with { Permissions = new HashSet<string>(maker.Permissions)
        { PlanReviewCoreWorkflow.WithdrawPermission } };
        var now = DateTimeOffset.UtcNow;
        var workflow = PlanReviewCoreWorkflow.Open(plan, submission, policy, definition, maker, assignees, now);
        plan.ApprovalInstanceRef = workflow.Id.ToString("D");
        var decisions = new List<Mightyfin.Erp.Core.Approvals.Domain.ApprovalDecision>();
        var active = workflow.Stages.Single(x => x.Code == "Review").Assignments.Single();
        _ = PlanReviewCoreWorkflow.Withdraw(plan, submission, workflow, active, maker,
            Guid.NewGuid().ToString("D"), "Need to revise forecast", decisions, now);
        Assert.Equal("Draft", plan.Status);
        Assert.Null(plan.ApprovalInstanceRef);
        Assert.Equal("Withdrawn", submission.State);
        Assert.Equal("Cancelled", workflow.State);
        Assert.Equal("Cancel", Assert.Single(decisions).Action);
    }

    private static (ProcurementPlan Plan, PlanReviewSubmission Submission, PolicyVersion Policy,
        PlanReviewPolicyRules.Definition Definition, PlanReviewPrincipal Maker,
        IReadOnlyList<PlanReviewPrincipal> Assignees) Ready()
    {
        var plan = new ProcurementPlan
        {
            TenantId = "tenant", LegalEntityId = Company, BranchRef = Branch,
            Number = "PLAN-1", FinancialPeriodRef = "FY-2027", CurrencyCode = "ZMW",
            OwnerSubjectId = Maker.ToString("D"), Status = "Submitted", Version = 2,
        };
        plan.Lines.Add(new ProcurementPlanLine
        {
            TenantId = "tenant", LegalEntityId = Company, ProcurementPlanId = plan.Id,
            LineNumber = 1, Description = "Supplies", UnitCode = "EA",
            EstimatedQuantity = 1, EstimatedAmount = 10,
        });
        var snapshot = PlanReviewSnapshot.Create(plan, 2);
        var policy = new PolicyVersion
        {
            TenantId = "tenant", LegalEntityId = Company,
            PolicyKey = PlanReviewPolicyRules.PolicyKey, PolicyVersionNumber = 1,
            State = "Published", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
            RulesJson = JsonSerializer.Serialize(new { schemaVersion = 2, stages = new object[]
            {
                new { purpose = "Review", mode = "Any", requiredApprovals = 1,
                    assigneePrincipalIds = new[] { Reviewer.ToString("D") } },
                new { purpose = "Final", mode = "Any", requiredApprovals = 1,
                    assigneePrincipalIds = new[] { Approver.ToString("D") } },
            } }),
        };
        var submission = new PlanReviewSubmission
        {
            TenantId = "tenant", LegalEntityId = Company, ProcurementPlanId = plan.Id,
            BranchRef = Branch, SubmittedPlanVersion = 2, CoreApprovalCaseId = Guid.NewGuid(),
            PolicyVersionId = policy.Id, PolicyVersionNumber = 1,
            PolicyRulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
            MakerSubjectId = Maker.ToString("D"), CurrencyCode = "ZMW",
            EstimatedAmount = snapshot.EstimatedAmount,
            SnapshotJson = snapshot.Json, SnapshotSha256 = snapshot.Sha256,
        };
        var definition = new PlanReviewPolicyRules.Definition([
            new("Review", "Any", 1, [Reviewer]), new("Final", "Any", 1, [Approver])]);
        PlanReviewPrincipal person(Guid id, string action) => new(id, null, "tenant", Company,
            false, new HashSet<Guid> { Branch }, new HashSet<string> { "procurement-access", action });
        return (plan, submission, policy, definition,
            person(Maker, "procurement-plan-submit"),
            [person(Reviewer, PlanReviewCoreWorkflow.ReviewPermission),
                person(Approver, PlanReviewCoreWorkflow.FinalPermission)]);
    }
}
