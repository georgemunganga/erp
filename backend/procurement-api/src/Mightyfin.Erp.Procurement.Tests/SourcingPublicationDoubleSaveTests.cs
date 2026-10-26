using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class SourcingPublicationDoubleSaveTests
{
    private sealed class Scope : IProcurementScope
    {
        public string TenantId => "m4-double-save";
        public Guid LegalEntityId { get; } = Guid.NewGuid();
        public string SubjectId { get; } = Guid.NewGuid().ToString("D");
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => null;
    }

    [Fact]
    public async Task Retained_approved_shortlist_survives_coordinator_final_save_but_cannot_gain_an_invitation()
    {
        var scope = new Scope();
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ProcurementDbContext(options, scope);
        var supplier = new Supplier { Number = "M4-SUPPLIER", LegalName = "Synthetic supplier",
            DisplayName = "Synthetic supplier", CountryCode = "ZM", Status = "Active",
            IsOrderable = true, EligibleFrom = DateTimeOffset.UtcNow.AddDays(-1) };
        db.AddScoped(supplier);
        await db.SaveChangesAsync();

        var ev = new SourcingEvent { TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            ExecutionVersion = 2, Status = "Published", OwnerSubjectId = Guid.NewGuid().ToString("D"),
            ApprovalInstanceRef = Guid.NewGuid().ToString("D") };
        var opening = new SourcingPublicationOpening { TenantId = scope.TenantId,
            LegalEntityId = scope.LegalEntityId, SourcingEventId = ev.Id,
            CoreApprovalCaseId = Guid.Parse(ev.ApprovalInstanceRef),
            SnapshotJson = $$"""{"supplierIds":["{{supplier.Id:D}}"]}""" };
        var decision = new SourcingPublicationDecision { TenantId = scope.TenantId,
            LegalEntityId = scope.LegalEntityId, SourcingEventId = ev.Id,
            OpeningId = opening.Id, CoreApprovalCaseId = opening.CoreApprovalCaseId,
            CaseState = "Approved", Action = "Approve", ActorPrincipalId = scope.SubjectId,
            DecidedAt = DateTimeOffset.UtcNow };
        var retained = new SourcingInvitation { TenantId = scope.TenantId,
            LegalEntityId = scope.LegalEntityId, SourcingEventId = ev.Id,
            SupplierId = supplier.Id, Status = "ReadyForManualDelivery" };
        // These rows model the tracked state after the command's first successful save.
        db.Attach(ev); db.Attach(opening); db.Attach(decision); db.Attach(retained);
        Assert.Equal(EntityState.Unchanged, db.Entry(decision).State);
        Assert.Equal(0, await db.SaveChangesAsync()); // Coordinator's second save.
        Assert.Equal(0, await db.SaveChangesAsync()); // A further no-op save stays safe.

        db.AddScoped(new SourcingInvitation { SourcingEventId = ev.Id, SupplierId = supplier.Id,
            Status = "ReadyForManualDelivery" });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal("SourcingInvitationRequiresInternalPublication", error.Message);
    }
}
