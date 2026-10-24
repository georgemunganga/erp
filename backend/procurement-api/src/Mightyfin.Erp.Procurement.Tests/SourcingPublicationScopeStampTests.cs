using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class SourcingPublicationScopeStampTests
{
    private sealed class Scope : IProcurementScope
    {
        public string TenantId => "publication-scope";
        public Guid LegalEntityId { get; } = Guid.NewGuid();
        public string SubjectId { get; } = Guid.NewGuid().ToString("D");
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => null;
    }

    [Theory]
    [InlineData("Opening")]
    [InlineData("Decision")]
    [InlineData("Receipt")]
    public void Source_bound_publication_evidence_can_be_attached_without_relabeling(string kind)
    {
        var scope = new Scope();
        using var db = Db(scope);
        var row = Record(kind, scope.TenantId, scope.LegalEntityId, scope.SubjectId);
        Attach(db, row);
        Assert.Equal(scope.TenantId, row.TenantId);
        Assert.Equal(scope.LegalEntityId, row.LegalEntityId);
        Assert.Equal(EntityState.Added, db.Entry(row).State);
    }

    [Theory]
    [InlineData("Opening", "missing")]
    [InlineData("Opening", "tenant")]
    [InlineData("Opening", "company")]
    [InlineData("Decision", "missing")]
    [InlineData("Decision", "tenant")]
    [InlineData("Decision", "company")]
    [InlineData("Receipt", "missing")]
    [InlineData("Receipt", "tenant")]
    [InlineData("Receipt", "company")]
    public void Missing_or_foreign_publication_scope_is_rejected_before_tracking(string kind, string mismatch)
    {
        var scope = new Scope();
        using var db = Db(scope);
        var row = Record(kind, mismatch == "missing" ? "" : mismatch == "tenant" ? "another-tenant" : scope.TenantId,
            mismatch is "missing" or "company" ? Guid.NewGuid() : scope.LegalEntityId, scope.SubjectId);
        var before = (row.TenantId, row.LegalEntityId);
        Assert.Throws<InvalidOperationException>(() => Attach(db, row));
        Assert.Equal(before, (row.TenantId, row.LegalEntityId));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    private static ProcurementDbContext Db(Scope scope) => new(
        new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        scope);

    private static ProcurementRecord Record(string kind, string tenant, Guid company, string actor)
    {
        var digest = new string('a', 64);
        ProcurementRecord row = kind switch
        {
            "Opening" => new SourcingPublicationOpening { SourcingEventId = Guid.NewGuid(), EventVersionAtOpening = 2,
                PolicyVersionId = Guid.NewGuid(), PolicyVersionNumber = 1, PolicyRulesSha256 = digest,
                PublishedPolicyEvidenceId = Guid.NewGuid(), CoreApprovalCaseId = Guid.NewGuid(),
                MakerPrincipalId = actor, OriginalDraftFingerprint = digest, SnapshotJson = "{}",
                SnapshotSha256 = digest, OpenedAt = DateTimeOffset.UtcNow },
            "Decision" => new SourcingPublicationDecision { SourcingEventId = Guid.NewGuid(), OpeningId = Guid.NewGuid(),
                CoreApprovalCaseId = Guid.NewGuid(), CoreApprovalDecisionId = Guid.NewGuid(), CoreAssignmentId = Guid.NewGuid(),
                ActorPrincipalId = actor, Action = "Approve", CaseState = "Approved", EventVersionAtDecision = 3,
                DecidedAt = DateTimeOffset.UtcNow },
            "Receipt" => new SourcingPublicationReceipt { SourcingEventId = Guid.NewGuid(), OpeningId = Guid.NewGuid(),
                ActorPrincipalId = actor, CommandKey = Guid.NewGuid(), Operation = "Open", CommandSha256 = digest,
                ResponseJson = "{}", ResponseSha256 = digest, ResponseStatusCode = 201, ResultVersion = 2,
                CompletedAt = DateTimeOffset.UtcNow },
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        row.TenantId = tenant; row.LegalEntityId = company;
        return row;
    }

    private static void Attach(ProcurementDbContext db, ProcurementRecord row)
    {
        switch (row)
        {
            case SourcingPublicationOpening opening: db.AddScoped(opening); break;
            case SourcingPublicationDecision decision: db.AddScoped(decision); break;
            case SourcingPublicationReceipt receipt: db.AddScoped(receipt); break;
            default: throw new ArgumentOutOfRangeException(nameof(row));
        }
    }
}
