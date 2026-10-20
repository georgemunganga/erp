using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;

namespace Mightyfin.Erp.Core.Approvals.Tests;

public sealed class ApprovalDbContextTests
{
    private sealed record TestScope(string TenantId, Guid LegalEntityId, string SubjectId) : IApprovalScope;

    [Fact]
    public async Task Case_queries_are_scoped_to_tenant_and_company()
    {
        var entity = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApprovalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using (var own = new ApprovalDbContext(options, new TestScope("tenant-a", entity, "maker")))
        {
            own.AddCase(CreateCase("tenant-a", entity));
            await own.SaveChangesAsync();
        }
        await using (var otherTenant = new ApprovalDbContext(options, new TestScope("tenant-b", entity, "reader")))
            Assert.Empty(await otherTenant.Cases.ToListAsync());
        await using (var otherEntity = new ApprovalDbContext(options, new TestScope("tenant-a", Guid.NewGuid(), "reader")))
            Assert.Empty(await otherEntity.Cases.ToListAsync());
    }

    [Fact]
    public async Task Decision_history_cannot_be_edited_or_deleted()
    {
        var entity = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApprovalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApprovalDbContext(options, new TestScope("tenant-a", entity, "manager"));
        var seed = CreateCase("tenant-a", entity);
        db.AddCase(seed);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var approvalCase = await db.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments).SingleAsync();
        var decisions = new List<ApprovalDecision>();
        ApprovalEngine.Decide(approvalCase, approvalCase.Stages.Single().Assignments.Single(),
            Actor("manager", entity), "Approve", "key", null, decisions, DateTimeOffset.UtcNow);
        var decision = Assert.Single(decisions);
        db.Decisions.Add(decision);
        await db.SaveChangesAsync();
        decision.Reason = "rewritten";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(decision).State = EntityState.Deleted;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private static ApprovalActor Actor(string subject, Guid entity) =>
        new(subject, "tenant-a", entity, new HashSet<string> { "procurement.purchase_request.approve" });

    private static async Task<ApprovalCase> LoadCase(ApprovalDbContext db) =>
        await db.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments).SingleAsync();

    private static async Task<DbContextOptions<ApprovalDbContext>> SeedQuorumCase(Guid entity)
    {
        var options = new DbContextOptionsBuilder<ApprovalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var seed = new ApprovalDbContext(options, new TestScope("tenant-a", entity, "maker"));
        var approvalCase = CreateCase("tenant-a", entity);
        var stage = approvalCase.Stages.Single();
        stage.RequiredApprovals = 2;
        stage.Assignments.Add(new ApprovalAssignment
        {
            TenantId = "tenant-a", LegalEntityId = entity, CaseId = approvalCase.Id,
            StageId = stage.Id, AssigneeSubjectId = "second",
            PermissionKey = "procurement.purchase_request.approve",
        });
        seed.AddCase(approvalCase);
        await seed.SaveChangesAsync();
        return options;
    }

    [Fact]
    public async Task Intermediate_vote_advances_case_once_and_replay_does_not_advance_it()
    {
        var entity = Guid.NewGuid();
        var options = await SeedQuorumCase(entity);
        await using var db = new ApprovalDbContext(options, new TestScope("tenant-a", entity, "manager"));
        var approvalCase = await LoadCase(db);
        var original = approvalCase.Version;
        var decisions = new List<ApprovalDecision>();
        var assignment = approvalCase.Stages.Single().Assignments.Single(x => x.AssigneeSubjectId == "manager");
        Assert.True(ApprovalEngine.Decide(approvalCase, assignment, Actor("manager", entity),
            "Approve", "first", "Checked", decisions, DateTimeOffset.UtcNow).Recorded);
        db.Decisions.AddRange(decisions);
        await db.SaveChangesAsync();
        Assert.Equal("Pending", approvalCase.State);
        Assert.Equal(original + 1, approvalCase.Version);
        Assert.False(ApprovalEngine.Decide(approvalCase, assignment, Actor("manager", entity),
            "Approve", "first", " Checked ", decisions, DateTimeOffset.UtcNow).Recorded);
        await db.SaveChangesAsync();
        Assert.Equal(original + 1, approvalCase.Version);
        Assert.Single(await db.Decisions.ToListAsync());
    }

    [Fact]
    public async Task Simultaneous_votes_on_different_assignments_conflict_on_case_revision()
    {
        var entity = Guid.NewGuid();
        var options = await SeedQuorumCase(entity);
        await using var left = new ApprovalDbContext(options, new TestScope("tenant-a", entity, "manager"));
        await using var right = new ApprovalDbContext(options, new TestScope("tenant-a", entity, "second"));
        var firstCase = await LoadCase(left);
        var staleCase = await LoadCase(right);
        var firstVotes = new List<ApprovalDecision>();
        var secondVotes = new List<ApprovalDecision>();
        ApprovalEngine.Decide(firstCase, firstCase.Stages.Single().Assignments.Single(x => x.AssigneeSubjectId == "manager"),
            Actor("manager", entity), "Approve", "first", null, firstVotes, DateTimeOffset.UtcNow);
        ApprovalEngine.Decide(staleCase, staleCase.Stages.Single().Assignments.Single(x => x.AssigneeSubjectId == "second"),
            Actor("second", entity), "Approve", "second", null, secondVotes, DateTimeOffset.UtcNow);
        left.Decisions.AddRange(firstVotes);
        right.Decisions.AddRange(secondVotes);
        await left.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => right.SaveChangesAsync());
    }

    [Fact]
    public async Task Final_vote_changes_state_and_advances_case_only_once()
    {
        var entity = Guid.NewGuid();
        var options = await SeedQuorumCase(entity);
        foreach (var subject in new[] { "manager", "second" })
        {
            await using var db = new ApprovalDbContext(options, new TestScope("tenant-a", entity, subject));
            var approvalCase = await LoadCase(db);
            var before = approvalCase.Version;
            var decisions = new List<ApprovalDecision>();
            ApprovalEngine.Decide(approvalCase, approvalCase.Stages.Single().Assignments.Single(x => x.AssigneeSubjectId == subject),
                Actor(subject, entity), "Approve", subject, null, decisions, DateTimeOffset.UtcNow);
            db.Decisions.AddRange(decisions);
            await db.SaveChangesAsync();
            Assert.Equal(before + 1, approvalCase.Version);
            Assert.Equal(subject == "second" ? "Approved" : "Pending", approvalCase.State);
        }
    }

    [Fact]
    public async Task Decision_actor_cannot_be_forged_through_direct_context_insert()
    {
        var entity = Guid.NewGuid();
        var options = await SeedQuorumCase(entity);
        await using var db = new ApprovalDbContext(options, new TestScope("tenant-a", entity, "manager"));
        var approvalCase = await LoadCase(db);
        var stage = approvalCase.Stages.Single();
        var assignment = stage.Assignments.Single(x => x.AssigneeSubjectId == "second");
        db.Decisions.Add(new ApprovalDecision
        {
            TenantId = "tenant-a", LegalEntityId = entity, CaseId = approvalCase.Id,
            StageId = stage.Id, AssignmentId = assignment.Id, ActorSubjectId = "second",
            Action = "Approve", IdempotencyKey = "forged",
        });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Decision_requires_complete_tracked_case_even_using_boolean_save_overload(bool loadHeaderOnly)
    {
        var entity = Guid.NewGuid();
        var options = await SeedQuorumCase(entity);
        await using var db = new ApprovalDbContext(options, new TestScope("tenant-a", entity, "manager"));
        if (loadHeaderOnly) await db.Cases.SingleAsync();
        db.Decisions.Add(new ApprovalDecision
        {
            TenantId = "tenant-a", LegalEntityId = entity,
            CaseId = loadHeaderOnly ? db.Cases.Local.Single().Id : Guid.NewGuid(),
            StageId = Guid.NewGuid(), AssignmentId = Guid.NewGuid(), ActorSubjectId = "manager",
            Action = "Approve", IdempotencyKey = "orphan",
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(true));
    }

    private static ApprovalCase CreateCase(string tenant, Guid entity)
    {
        var approvalCase = new ApprovalCase
        {
            TenantId = tenant, LegalEntityId = entity, MakerSubjectId = "maker",
            SourceModule = "procurement", SourceType = "purchase_request",
            SourceId = Guid.NewGuid(), SourceVersion = 1,
            PolicyVersionId = Guid.NewGuid(), PolicyVersionNumber = 1,
        };
        var stage = new ApprovalStage
        {
            TenantId = tenant, LegalEntityId = entity, CaseId = approvalCase.Id,
            Sequence = 1, Code = "manager", State = "Active",
        };
        stage.Assignments.Add(new ApprovalAssignment
        {
            TenantId = tenant, LegalEntityId = entity, CaseId = approvalCase.Id,
            StageId = stage.Id, AssigneeSubjectId = "manager",
            PermissionKey = "procurement.purchase_request.approve",
        });
        approvalCase.Stages.Add(stage);
        return approvalCase;
    }
}
