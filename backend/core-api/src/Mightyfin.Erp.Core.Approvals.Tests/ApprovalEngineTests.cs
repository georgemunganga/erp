using Mightyfin.Erp.Core.Approvals.Domain;

namespace Mightyfin.Erp.Core.Approvals.Tests;

public sealed class ApprovalEngineTests
{
    private const string Permission = "procurement.purchase_request.approve";
    private static readonly Guid Entity = Guid.NewGuid();

    private static (ApprovalCase Case, ApprovalAssignment First, ApprovalAssignment Second,
        List<ApprovalDecision> Decisions) CreateCase(int quorum = 2)
    {
        var approvalCase = new ApprovalCase
        {
            TenantId = "tenant-a", LegalEntityId = Entity, MakerSubjectId = "maker",
            SourceModule = "procurement", SourceType = "purchase_request", SourceId = Guid.NewGuid(),
            SourceVersion = 3, PolicyVersionId = Guid.NewGuid(), PolicyVersionNumber = 2,
        };
        var stage = new ApprovalStage
        {
            TenantId = "tenant-a", LegalEntityId = Entity, CaseId = approvalCase.Id,
            Sequence = 1, Code = "manager", State = "Active", RequiredApprovals = quorum,
        };
        var first = Assignment(approvalCase, stage, "manager-a");
        var second = Assignment(approvalCase, stage, "manager-b");
        stage.Assignments = [first, second];
        approvalCase.Stages = [stage];
        ApprovalEngine.ValidateNewCase(approvalCase);
        return (approvalCase, first, second, []);
    }

    private static ApprovalAssignment Assignment(ApprovalCase parent, ApprovalStage stage, string assignee) => new()
    {
        TenantId = parent.TenantId, LegalEntityId = parent.LegalEntityId,
        CaseId = parent.Id, StageId = stage.Id, AssigneeSubjectId = assignee,
        PermissionKey = Permission,
    };

    private static ApprovalActor Actor(string subject, string tenant = "tenant-a", Guid? entity = null,
        bool permission = true) => new(subject, tenant, entity ?? Entity,
        permission ? new HashSet<string> { Permission } : new HashSet<string>());

    [Fact]
    public void Quorum_requires_distinct_assignees_and_duplicate_retry_does_not_count_twice()
    {
        var (approvalCase, first, second, decisions) = CreateCase();
        var now = DateTimeOffset.UtcNow;
        var initial = ApprovalEngine.Decide(approvalCase, first, Actor("manager-a"), "Approve", "key-1", null, decisions, now);
        Assert.False(initial.CaseCompleted);
        var replay = ApprovalEngine.Decide(approvalCase, first, Actor("manager-a"), "Approve", "key-1", null, decisions, now);
        Assert.False(replay.Recorded);
        Assert.Throws<UnauthorizedAccessException>(() => ApprovalEngine.Decide(approvalCase, first,
            Actor("manager-a", permission: false), "Approve", "key-1", null, decisions, now));
        var final = ApprovalEngine.Decide(approvalCase, second, Actor("manager-b"), "Approve", "key-2", null, decisions, now);
        Assert.True(final.CaseCompleted);
        Assert.Equal("Approved", approvalCase.State);
        Assert.Equal(2, decisions.Count);
    }

    [Fact]
    public void Maker_cross_scope_unassigned_and_missing_permission_are_denied()
    {
        var (approvalCase, first, _, decisions) = CreateCase();
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<UnauthorizedAccessException>(() => ApprovalEngine.Decide(approvalCase, first, Actor("maker"), "Approve", "a", null, decisions, now));
        Assert.Throws<UnauthorizedAccessException>(() => ApprovalEngine.Decide(approvalCase, first, Actor("manager-a", "other"), "Approve", "b", null, decisions, now));
        Assert.Throws<UnauthorizedAccessException>(() => ApprovalEngine.Decide(approvalCase, first, Actor("manager-a", entity: Guid.NewGuid()), "Approve", "c", null, decisions, now));
        Assert.Throws<UnauthorizedAccessException>(() => ApprovalEngine.Decide(approvalCase, first, Actor("manager-a", permission: false), "Approve", "d", null, decisions, now));
        Assert.Empty(decisions);
    }

    [Fact]
    public void Rejection_requires_reason_and_closes_case_without_approval()
    {
        var (approvalCase, first, _, decisions) = CreateCase();
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => ApprovalEngine.Decide(approvalCase, first, Actor("manager-a"), "Reject", "a", null, decisions, now));
        var result = ApprovalEngine.Decide(approvalCase, first, Actor("manager-a"), "Reject", "b", "No budget", decisions, now);
        Assert.False(result.CaseCompleted);
        Assert.Equal("Rejected", approvalCase.State);
        Assert.Single(decisions);
    }

    [Fact]
    public void Key_reuse_for_another_action_is_denied()
    {
        var (approvalCase, first, second, decisions) = CreateCase();
        var now = DateTimeOffset.UtcNow;
        ApprovalEngine.Decide(approvalCase, first, Actor("manager-a"), "Approve", "same", null, decisions, now);
        Assert.Throws<InvalidOperationException>(() => ApprovalEngine.Decide(approvalCase, second, Actor("manager-b"), "Approve", "same", null, decisions, now));
    }

    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    [InlineData("Return")]
    public void Replay_compares_normalized_reason_and_key_without_mutating_decisions(string action)
    {
        var (approvalCase, first, _, decisions) = CreateCase();
        var now = DateTimeOffset.UtcNow;
        ApprovalEngine.Decide(approvalCase, first, Actor("manager-a"), action, " key ",
            "  Checked\t delivery\n details  ", decisions, now);
        var stored = Assert.Single(decisions);
        Assert.Equal("key", stored.IdempotencyKey);
        Assert.Equal("Checked delivery details", stored.Reason);
        Assert.False(ApprovalEngine.Decide(approvalCase, first, Actor("manager-a"), action, "key",
            "Checked  delivery details", decisions, now).Recorded);
        Assert.Throws<InvalidOperationException>(() => ApprovalEngine.Decide(approvalCase, first,
            Actor("manager-a"), action, "key", "Different reason", decisions, now));
        Assert.Single(decisions);
    }

    [Fact]
    public void Empty_approval_reason_and_whitespace_are_the_same_replay()
    {
        var (approvalCase, first, _, decisions) = CreateCase();
        var now = DateTimeOffset.UtcNow;
        ApprovalEngine.Decide(approvalCase, first, Actor("manager-a"), "Approve", "key", null, decisions, now);
        Assert.False(ApprovalEngine.Decide(approvalCase, first, Actor("manager-a"), "Approve", "key",
            " \t\n ", decisions, now).Recorded);
    }

    [Theory]
    [InlineData(151, 0)]
    [InlineData(1, 2001)]
    public void Oversized_decision_inputs_are_rejected_before_mutation(int keyLength, int reasonLength)
    {
        var (approvalCase, first, _, decisions) = CreateCase();
        Assert.Throws<ArgumentException>(() => ApprovalEngine.Decide(approvalCase, first, Actor("manager-a"),
            "Approve", new string('k', keyLength), new string('r', reasonLength), decisions, DateTimeOffset.UtcNow));
        Assert.Empty(decisions);
        Assert.Equal("Pending", first.State);
    }

    [Fact]
    public void Invalid_quorum_and_maker_assignment_cannot_open_case()
    {
        var (approvalCase, first, _, _) = CreateCase();
        var stage = approvalCase.Stages.Single();
        stage.RequiredApprovals = 3;
        Assert.Throws<ArgumentException>(() => ApprovalEngine.ValidateNewCase(approvalCase));
        stage.RequiredApprovals = 1;
        first.AssigneeSubjectId = "maker";
        Assert.Throws<ArgumentException>(() => ApprovalEngine.ValidateNewCase(approvalCase));
    }

    [Fact]
    public void Later_stage_activates_only_after_first_stage_quorum()
    {
        var (approvalCase, first, second, decisions) = CreateCase();
        var secondStage = new ApprovalStage
        {
            TenantId = approvalCase.TenantId, LegalEntityId = Entity, CaseId = approvalCase.Id,
            Sequence = 2, Code = "finance", Mode = "Any", RequiredApprovals = 1,
        };
        var finance = Assignment(approvalCase, secondStage, "finance");
        secondStage.Assignments = [finance];
        approvalCase.Stages.Add(secondStage);
        ApprovalEngine.ValidateNewCase(approvalCase);
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<InvalidOperationException>(() => ApprovalEngine.Decide(approvalCase, finance, Actor("finance"), "Approve", "early", null, decisions, now));
        ApprovalEngine.Decide(approvalCase, first, Actor("manager-a"), "Approve", "first", null, decisions, now);
        ApprovalEngine.Decide(approvalCase, second, Actor("manager-b"), "Approve", "second", null, decisions, now);
        Assert.Equal("Active", secondStage.State);
        Assert.Equal("Pending", approvalCase.State);
        var result = ApprovalEngine.Decide(approvalCase, finance, Actor("finance"), "Approve", "final", null, decisions, now);
        Assert.True(result.CaseCompleted);
    }
}
