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
        var decision = new ApprovalDecision
        {
            TenantId = "tenant-a", LegalEntityId = entity, CaseId = Guid.NewGuid(),
            StageId = Guid.NewGuid(), AssignmentId = Guid.NewGuid(),
            ActorSubjectId = "manager", Action = "Approve", IdempotencyKey = "key",
        };
        db.Decisions.Add(decision);
        await db.SaveChangesAsync();
        decision.Reason = "rewritten";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(decision).State = EntityState.Deleted;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
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
