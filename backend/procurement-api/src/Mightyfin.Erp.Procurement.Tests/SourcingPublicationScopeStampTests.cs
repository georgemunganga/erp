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

    [Fact]
    public void A_previously_saved_publication_does_not_recreate_invitations_on_a_second_save()
    {
        var scope = new Scope();
        using var db = Db(scope);
        var opening = (SourcingPublicationOpening)Record("Opening", scope.TenantId, scope.LegalEntityId, scope.SubjectId);
        opening.SnapshotJson = System.Text.Json.JsonSerializer.Serialize(new
            { supplierIds = new[] { Guid.NewGuid(), Guid.NewGuid() } });
        var decision = (SourcingPublicationDecision)Record("Decision", scope.TenantId, scope.LegalEntityId, scope.SubjectId);
        decision.OpeningId = opening.Id;
        decision.SourcingEventId = opening.SourcingEventId;
        decision.CoreApprovalCaseId = opening.CoreApprovalCaseId;
        // These represent already persisted records when the transaction
        // coordinator performs its final save. No new decision is submitted.
        db.Attach(opening);
        db.Attach(decision);

        Assert.Equal(0, db.SaveChanges());
        Assert.All(db.ChangeTracker.Entries(), entry => Assert.Equal(EntityState.Unchanged, entry.State));
        Assert.Empty(db.SourcingInvitations.Local);
        Assert.Empty(db.AuditEvents.Local);
        Assert.Empty(db.OutboxEvents.Local);
    }

    [Fact]
    public void Historical_approval_cannot_recreate_the_complete_supplier_invitation_batch()
    {
        var scope = new Scope();
        using var db = Db(scope);
        var suppliers = Enumerable.Range(1, 2).Select(n => new Supplier
        { Number = "SUP-" + n, LegalName = "Supplier " + n, DisplayName = "Supplier " + n,
            CountryCode = "ZM", Status = "Active", IsOrderable = true }).ToArray();
        foreach (var supplier in suppliers) db.AddScoped(supplier);
        db.SaveChanges();
        var auditBefore = db.AuditEvents.Count();
        var outboxBefore = db.OutboxEvents.Count();
        db.ChangeTracker.Clear();

        var opening = (SourcingPublicationOpening)Record("Opening", scope.TenantId, scope.LegalEntityId, scope.SubjectId);
        opening.SnapshotJson = System.Text.Json.JsonSerializer.Serialize(new
            { supplierIds = suppliers.Select(x => x.Id).ToArray() });
        var decision = (SourcingPublicationDecision)Record("Decision", scope.TenantId, scope.LegalEntityId, scope.SubjectId);
        decision.OpeningId = opening.Id; decision.SourcingEventId = opening.SourcingEventId;
        decision.CoreApprovalCaseId = opening.CoreApprovalCaseId;
        var sourcing = new SourcingEvent { Id = opening.SourcingEventId, TenantId = scope.TenantId,
            LegalEntityId = scope.LegalEntityId, Number = "RFQ-1", Title = "Existing approved quote request",
            ExecutionVersion = 2, Status = "Published", CurrencyCode = "ZMW", Method = "RFQ",
            SourceEvidenceId = Guid.NewGuid(), MethodId = Guid.NewGuid(), MethodRevision = 1,
            OwnerSubjectId = scope.SubjectId, OriginalDraftFingerprint = new string('a', 64),
            ApprovalInstanceRef = opening.CoreApprovalCaseId.ToString("D"), Version = 3 };
        db.Attach(sourcing); db.Attach(opening); db.Attach(decision);
        foreach (var supplier in suppliers)
            db.AddScoped(new SourcingInvitation { SourcingEventId = sourcing.Id, SupplierId = supplier.Id,
                Status = "ReadyForManualDelivery" });

        var error = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Equal("SourcingInvitationRequiresInternalPublication", error.Message);
        Assert.Empty(db.SourcingInvitations.AsNoTracking());
        Assert.Equal(auditBefore, db.AuditEvents.Count());
        Assert.Equal(outboxBefore, db.OutboxEvents.Count());
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
