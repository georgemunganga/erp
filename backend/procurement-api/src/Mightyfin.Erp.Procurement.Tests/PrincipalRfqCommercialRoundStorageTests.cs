using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqCommercialRoundStorageTests
{
    private sealed class Scope : IProcurementScope
    {
        public string TenantId => "fixture";
        public Guid LegalEntityId { get; } = Guid.NewGuid();
        public string SubjectId { get; } = Guid.NewGuid().ToString("D");
        public Guid? WorkLocationId { get; init; }
        public Guid? OrgUnitId => null;
        public bool IsConfined => WorkLocationId is not null;
        public string? CorrelationId => null;
    }

    [Fact]
    public void Retained_round_evidence_is_append_only_even_with_same_scope()
    {
        var scope = new Scope();
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var opening = new PrincipalRfqCommercialRoundOpeningRecord
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            ActorPrincipalId = scope.SubjectId, BranchId = Guid.NewGuid(),
        };
        db.Attach(opening);
        db.Entry(opening).Property(x => x.SnapshotSha256).CurrentValue = new string('a', 64);
        Assert.Equal("RfqCommercialEvidenceAppendOnly",
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
    }

    [Fact]
    public void Bare_opening_or_receipt_cannot_be_saved_as_complete_business_evidence()
    {
        var scope = new Scope();
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var opening = new PrincipalRfqCommercialRoundOpeningRecord
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            ActorPrincipalId = scope.SubjectId, BranchId = Guid.NewGuid(),
        };
        db.Add(opening);
        Assert.Equal("RfqCommercialSameSaveReceiptRequired",
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
        db.ChangeTracker.Clear();
        db.Add(new PrincipalRfqCommercialCommandReceipt
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            ActorPrincipalId = scope.SubjectId, BranchId = opening.BranchId,
            Operation = "Open", CommandKey = Guid.NewGuid(), TargetId = opening.Id,
            RequestSha256 = new string('a', 64), ResponseJson = "{}",
            ResponseSha256 = ProcurementEvidenceEncoding.Digest("{}"),
        });
        Assert.Equal("RfqCommercialReceiptTargetRequired",
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges()).Message);
    }
}
