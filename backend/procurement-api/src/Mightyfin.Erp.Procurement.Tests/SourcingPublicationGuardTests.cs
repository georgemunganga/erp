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
}
