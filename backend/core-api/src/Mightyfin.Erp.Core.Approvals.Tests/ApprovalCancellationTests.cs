using Mightyfin.Erp.Core.Approvals.Domain;

namespace Mightyfin.Erp.Core.Approvals.Tests;

public sealed class ApprovalCancellationTests
{
    [Fact]
    public void Assigned_checker_can_cancel_but_a_reused_key_cannot_claim_another_actor_assignment_or_action()
    {
        var (workflow, assignment, maker) = Fixture(); var decisions = new List<ApprovalDecision>();
        var checker = maker with { SubjectId = "checker", Permissions = new HashSet<string> { "source-cancel", "source-approve" } };
        Assert.True(ApprovalEngine.Cancel(workflow, assignment, checker, "source-cancel", "key", "Withdrawn", decisions, Now).Recorded);
        Assert.Throws<InvalidOperationException>(() => ApprovalEngine.Cancel(workflow, assignment, maker,
            "source-cancel", "key", "Withdrawn", decisions, Now));
        var other = workflow.Stages.Last().Assignments.Single();
        Assert.Throws<InvalidOperationException>(() => ApprovalEngine.Cancel(workflow, other, checker,
            "source-cancel", "key", "Withdrawn", decisions, Now));
        Assert.Throws<InvalidOperationException>(() => ApprovalEngine.Decide(workflow, assignment, checker,
            "Approve", "key", "Withdrawn", decisions, Now));
        Assert.Single(decisions);
    }
    [Fact]
    public void Maker_with_explicit_permission_cancels_pending_tasks_without_approving_their_own_case()
    {
        var (workflow, assignment, actor) = Fixture(); var decisions = new List<ApprovalDecision>();
        var result = ApprovalEngine.Cancel(workflow, assignment, actor, "source-cancel", "cancel-1", "No longer needed", decisions, Now);
        Assert.True(result.Recorded); Assert.False(result.CaseCompleted); Assert.Equal("Cancelled", workflow.State);
        Assert.All(workflow.Stages, x => Assert.Equal("Cancelled", x.State));
        Assert.All(workflow.Stages.SelectMany(x => x.Assignments), x => Assert.Equal("Cancelled", x.State));
        Assert.Equal("Cancel", Assert.Single(decisions).Action); Assert.Equal("maker", decisions[0].ActorSubjectId);
        Assert.False(ApprovalEngine.Cancel(workflow, assignment, actor, "source-cancel", "cancel-1", "No longer needed", decisions, Now).Recorded);
        Assert.Single(decisions);
        Assert.Throws<InvalidOperationException>(() => ApprovalEngine.Cancel(workflow, assignment, actor,
            "source-cancel", "cancel-1", "Changed reason", decisions, Now));
    }

    [Theory]
    [InlineData("permission")][InlineData("tenant")][InlineData("company")][InlineData("stranger")]
    public void Cancellation_requires_current_permission_actor_and_scope(string denied)
    {
        var (workflow, assignment, actor) = Fixture();
        actor = denied switch {
            "permission" => actor with { Permissions = new HashSet<string>() },
            "tenant" => actor with { TenantId = "foreign" },
            "company" => actor with { LegalEntityId = Guid.NewGuid() },
            _ => actor with { SubjectId = "stranger" }
        };
        var decisions = new List<ApprovalDecision>();
        Assert.Throws<UnauthorizedAccessException>(() => ApprovalEngine.Cancel(workflow, assignment, actor,
            "source-cancel", "key", "Reason", decisions, Now));
        Assert.Equal("Pending", workflow.State); Assert.Empty(decisions);
    }

    [Fact]
    public void Cancellation_needs_a_reason_and_cannot_reverse_a_completed_approval()
    {
        var (workflow, assignment, actor) = Fixture(); var decisions = new List<ApprovalDecision>();
        Assert.Throws<ArgumentException>(() => ApprovalEngine.Cancel(workflow, assignment, actor,
            "source-cancel", "key", "  ", decisions, Now));
        workflow.State = "Approved";
        Assert.Throws<InvalidOperationException>(() => ApprovalEngine.Cancel(workflow, assignment, actor,
            "source-cancel", "key", "Reason", decisions, Now));
        Assert.Equal("Approved", workflow.State); Assert.Empty(decisions);
    }

    private static readonly DateTimeOffset Now = new(2026,10,23,12,0,0,TimeSpan.Zero);
    private static (ApprovalCase Case, ApprovalAssignment Assignment, ApprovalActor Maker) Fixture()
    {
        var company = Guid.NewGuid();
        var workflow = new ApprovalCase { TenantId = "scope", LegalEntityId = company, SourceModule = "test",
            SourceType = "request", SourceId = Guid.NewGuid(), SourceVersion = 1, PolicyVersionId = Guid.NewGuid(),
            PolicyVersionNumber = 1, MakerSubjectId = "maker", OpenedAt = Now };
        ApprovalStage Stage(int sequence, string state)
        {
            var stage = new ApprovalStage { TenantId = "scope", LegalEntityId = company, CaseId = workflow.Id,
                Sequence = sequence, Code = "review", Mode = "All", RequiredApprovals = 1, State = state };
            stage.Assignments.Add(new ApprovalAssignment { TenantId = "scope", LegalEntityId = company,
                CaseId = workflow.Id, StageId = stage.Id, AssigneeSubjectId = "checker", PermissionKey = "source-approve" });
            return stage;
        }
        workflow.Stages.Add(Stage(1, "Active")); workflow.Stages.Add(Stage(2, "Waiting"));
        ApprovalEngine.ValidateNewCase(workflow);
        return (workflow, workflow.Stages.First().Assignments.Single(), new("maker", "scope", company, new HashSet<string> { "source-cancel" }));
    }
}
