using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class SourcingPublicationGuardTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    {
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "publication-guard";
    }

    [Fact]
    public async Task Principal_draft_cannot_be_edited_directly_into_published_state()
    {
        var actor = Guid.NewGuid().ToString("D");
        var scope = new Scope("tenant", Guid.NewGuid(), actor);
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ProcurementDbContext(options, scope);
        var ev = new SourcingEvent
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            ExecutionVersion = 2, SourceEvidenceId = Guid.NewGuid(),
            MethodId = Guid.NewGuid(), MethodRevision = 1,
            OriginalDraftFingerprint = new string('a', 64),
            OwnerSubjectId = actor, Status = "Draft", BranchRef = null,
        };
        db.Attach(ev);
        ev.Status = "Published";
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal("RfqMaterialEvidenceLocked", error.Message);
    }

    [Fact]
    public async Task Principal_shortlist_cannot_be_inserted_without_approved_core_decision()
    {
        var actor = Guid.NewGuid().ToString("D");
        var scope = new Scope("tenant", Guid.NewGuid(), actor);
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ProcurementDbContext(options, scope);
        var ev = new SourcingEvent
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            ExecutionVersion = 2, Status = "Published", OwnerSubjectId = actor,
        };
        db.Attach(ev);
        db.AddScoped(new SourcingInvitation
        {
            SourcingEventId = ev.Id, SupplierId = Guid.NewGuid(),
            Status = "ReadyForManualDelivery", SentAt = null,
        });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal("SourcingInvitationRequiresInternalPublication", error.Message);
    }

    [Fact]
    public async Task Manual_supplier_contact_cannot_be_claimed_without_append_only_contact_evidence()
    {
        var scope = new Scope("tenant", Guid.NewGuid(), Guid.NewGuid().ToString("D"));
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ProcurementDbContext(options, scope);
        var ev = new SourcingEvent { TenantId = scope.TenantId,
            LegalEntityId = scope.LegalEntityId, ExecutionVersion = 2, Status = "Published" };
        var invitation = new SourcingInvitation { TenantId = scope.TenantId,
            LegalEntityId = scope.LegalEntityId, SourcingEventId = ev.Id,
            SupplierId = Guid.NewGuid(), Status = "ReadyForManualDelivery" };
        db.Attach(ev); db.Attach(invitation);
        invitation.Status = "ManualContactRecorded";
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal("SourcingInvitationContactInvalid", error.Message);
    }

    [Fact]
    public async Task Principal_manual_quote_cannot_be_submitted_or_mutated_without_draft_revision()
    {
        var scope = new Scope("tenant", Guid.NewGuid(), Guid.NewGuid().ToString("D"));
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ProcurementDbContext(options, scope);
        var ev = new SourcingEvent { TenantId = scope.TenantId,
            LegalEntityId = scope.LegalEntityId, ExecutionVersion = 2, Status = "Published" };
        var bid = new SupplierBid { TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            SourcingEventId = ev.Id, SupplierId = Guid.NewGuid(), SourceChannel = "Manual",
            Status = "Draft", SurrogateEnteredBy = scope.SubjectId, DraftRevision = 1 };
        db.Attach(ev); db.Attach(bid);
        bid.Status = "Submitted";
        bid.SubmittedAt = DateTimeOffset.UtcNow;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal("SourcingManualQuoteVersionRequired", error.Message);
    }
}
