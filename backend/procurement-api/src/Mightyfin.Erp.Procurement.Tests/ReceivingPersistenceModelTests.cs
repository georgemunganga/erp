using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class ReceivingPersistenceModelTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    { public Guid? WorkLocationId => null; public Guid? OrgUnitId => null; public bool IsConfined => false; public string? CorrelationId => "receiving-test"; }
    private static ProcurementDbContext Context() => new(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new Scope("test", Guid.NewGuid(), "receiver"));
    [Fact]
    public void New_evidence_has_only_two_tables_and_keeps_legacy_issued_pin_nullable()
    {
        using var db = Context();
        var header = db.Model.FindEntityType(typeof(GoodsReceipt))!;
        Assert.True(header.FindProperty(nameof(GoodsReceipt.IssuedVersionId))!.IsNullable);
        Assert.False(header.FindProperty(nameof(GoodsReceipt.IssuedBindingId))!.IsNullable);
        Assert.Equal("receiving_source_states", db.Model.FindEntityType(typeof(ReceivingSourceState))!.GetTableName());
        Assert.Equal("receiving_acceptance_effects", db.Model.FindEntityType(typeof(ReceivingAcceptanceEffect))!.GetTableName());
        Assert.Null(db.Model.FindEntityType(typeof(GoodsPhysicalObservation)));
        Assert.Null(db.Model.FindEntityType(typeof(GoodsInspectionEvidence)));
    }
    [Fact]
    public void Header_line_state_foreign_keys_bind_company_PO_issued_and_exact_line()
    {
        using var db = Context();
        var line = db.Model.FindEntityType(typeof(GoodsReceiptLine))!;
        Assert.Contains(line.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(GoodsReceipt)
            && fk.Properties.Select(x => x.Name).SequenceEqual(new[] { "TenantId", "LegalEntityId", "GoodsReceiptId", "SourcePurchaseOrderId", "IssuedBindingId" }));
        Assert.Contains(line.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(ReceivingSourceState)
            && fk.Properties.Select(x => x.Name).SequenceEqual(new[] { "TenantId", "LegalEntityId", "ReceivingSourceStateId", "SourcePurchaseOrderId", "IssuedBindingId", "PurchaseOrderLineId" }));
        var effect = db.Model.FindEntityType(typeof(ReceivingAcceptanceEffect))!;
        Assert.Contains(effect.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(GoodsReceiptLine)
            && fk.Properties.Any(x => x.Name == nameof(ReceivingAcceptanceEffect.PhysicalObservationId))
            && fk.Properties.Any(x => x.Name == nameof(ReceivingAcceptanceEffect.ReceivingSourceStateId)));
    }
    [Fact]
    public async Task Untouched_legacy_draft_stays_missing_evidence_and_cannot_project_as_final()
    {
        await using var db = Context(); var row = new GoodsReceipt { Number = "LEGACY", PurchaseOrderId = Guid.NewGuid(),
            Status = "Draft", ReceiverSubjectId = "receiver", ReceivedAt = DateTimeOffset.UtcNow };
        db.AddScoped(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var restored = await db.GoodsReceipts.SingleAsync();
        Assert.Equal(0, restored.ExecutionVersion); Assert.Equal(Guid.Empty, restored.IssuedBindingId); Assert.Null(restored.IssuedVersionId);
        Assert.Throws<InvalidOperationException>(() => ReceivingPersistenceProjection.VerifyHeader(restored));
    }
    [Fact]
    public async Task Recorded_business_evidence_and_counters_cannot_be_mutated_without_a_fact()
    {
        await using var db = Context();
        var row = new GoodsReceipt { Number = "RECORDED", PurchaseOrderId = Guid.NewGuid(), ReceiverSubjectId = "receiver", RecordedAt = DateTimeOffset.UtcNow };
        db.AddScoped(row); await db.SaveChangesAsync(); row.DeliveryNoteRef = "OVERWRITE";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var line = new GoodsReceiptLine { GoodsReceiptId = row.Id, PurchaseOrderLineId = Guid.NewGuid(), Status = "Draft" };
        db.AddScoped(line); await db.SaveChangesAsync(); line.AcceptedQuantity = 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task A_new_certified_service_cannot_bypass_the_typed_positive_effect()
    {
        await using var db = Context();
        var certificate = new ServiceAcceptance { PurchaseOrderLineId = Guid.NewGuid(), ExecutionVersion = 1,
            SourcePurchaseOrderId = Guid.NewGuid(), IssuedBindingId = Guid.NewGuid(), ReceivingSourceStateId = Guid.NewGuid(),
            SourceStateBindingId = Guid.NewGuid(), CertificationEffectId = Guid.NewGuid(), CertifiedAt = DateTimeOffset.UtcNow,
            OwnerWorkerId = Guid.NewGuid(), AcceptedQuantity = 1, AcceptedAmount = 100, Status = "Accepted" };
        db.AddScoped(certificate);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal("ReceivingServicePositiveEffectRequired", failure.Message);
    }

    [Fact]
    public async Task Service_notes_have_their_own_nullable_2000_character_column_without_legacy_reason_backfill()
    {
        await using var db = Context();
        var service = db.Model.FindEntityType(typeof(ServiceAcceptance))!;
        Assert.True(service.FindProperty(nameof(ServiceAcceptance.Notes))!.IsNullable);
        Assert.Equal(2000, service.FindProperty(nameof(ServiceAcceptance.Notes))!.GetMaxLength());
        Assert.Null(service.FindProperty(nameof(ServiceAcceptance.Reason))!.GetMaxLength()); // Preserve existing text storage; cancellation API remains bounded.
        var row = new ServiceAcceptance { PurchaseOrderLineId = Guid.NewGuid(), OwnerSubjectId = "receiver", Reason = "Retained legacy reason" };
        db.AddScoped(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var restored = await db.ServiceAcceptances.SingleAsync();
        Assert.Null(restored.Notes); Assert.Equal("Retained legacy reason", restored.Reason); Assert.Equal(0, restored.ExecutionVersion);
    }
    [Fact]
    public async Task Final_service_narrative_cannot_be_rewritten_after_certification()
    {
        await using var db = Context();
        var row = new ServiceAcceptance { CertifiedAt = DateTimeOffset.UtcNow, Notes = "Retained final narrative" };
        db.Entry(row).State = EntityState.Unchanged; row.Notes = "Overwritten";
        Assert.Equal("ServiceCertificationImmutable", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
    }

}
