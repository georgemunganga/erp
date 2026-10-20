using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
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
    public void Final_physical_and_inspection_checks_reject_missing_nullable_sequences_without_requiring_them_on_legacy_or_drafts()
    {
        using var db = Context(); var model = db.GetService<IDesignTimeModel>().Model;
        var line = model.FindEntityType(typeof(GoodsReceiptLine))!;
        var inspection = model.FindEntityType(typeof(ReceiptInspection))!;
        // These columns intentionally stay nullable for v0 and unrecorded drafts.
        Assert.True(line.FindProperty(nameof(GoodsReceiptLine.PhysicalSequence))!.IsNullable);
        Assert.True(inspection.FindProperty(nameof(ReceiptInspection.LedgerSequence))!.IsNullable);
        var physicalSql = line.GetCheckConstraints().Single(x => x.Name == "ck_receiving_line_execution").Sql;
        var inspectionSql = inspection.GetCheckConstraints().Single(x => x.Name == "ck_receiving_inspection_execution").Sql;
        Assert.StartsWith("execution_version = 0 OR", physicalSql);
        Assert.Contains("physical_observation_id IS NULL OR", physicalSql);
        Assert.Contains("physical_sequence IS NOT NULL AND physical_sequence > 0", physicalSql);
        Assert.StartsWith("execution_version = 0 OR", inspectionSql);
        Assert.Contains("ledger_sequence IS NOT NULL AND ledger_sequence > 0", inspectionSql);
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
            && fk.Properties.Select(x => x.Name).SequenceEqual(new[] { "TenantId", "LegalEntityId", "GoodsReceiptLineId" }));
        Assert.Contains(line.GetIndexes(), index => index.IsUnique
            && index.GetDatabaseName() == "ux_receiving_goods_physical_binding"
            && index.Properties.Select(x => x.Name).SequenceEqual(new[] { "TenantId", "LegalEntityId", "Id", "SourceStateBindingId", "PhysicalBindingId" }));
    }
    [Fact]
    public void Mutable_fact_bindings_are_not_EF_identity_keys_or_generated_authority()
    {
        using var db = Context();
        foreach (var (type, names) in new[] {
            (typeof(GoodsReceiptLine), new[] { "SourceStateBindingId", "PhysicalBindingId" }),
            (typeof(ServiceAcceptance), new[] { "SourceStateBindingId" }) })
        {
            var entity = db.Model.FindEntityType(type)!;
            foreach (var name in names)
            {
                Assert.DoesNotContain(entity.GetKeys(), key => key.Properties.Any(x => x.Name == name));
                Assert.Equal(ValueGenerated.Never, entity.FindProperty(name)!.ValueGenerated);
            }
        }
        foreach (var (type, names) in new[] {
            (typeof(GoodsReceipt), new[] { "IssuedBindingId" }),
            (typeof(GoodsReceiptLine), new[] { "SourceStateBindingId", "PhysicalBindingId", "IssuedBindingId" }),
            (typeof(ReceiptInspection), new[] { "SourceStateBindingId", "PhysicalBindingId" }),
            (typeof(ServiceAcceptance), new[] { "SourceStateBindingId", "IssuedBindingId" }) })
            foreach (var name in names)
                Assert.Equal(ValueGenerated.Never, db.Model.FindEntityType(type)!.FindProperty(name)!.ValueGenerated);
        var sql = ReceivingOwnershipConstraintSql.Install;
        Assert.Equal(3, sql.Split("DEFERRABLE INITIALLY DEFERRED").Length - 1);
        Assert.Contains("REFERENCES procurement.goods_receipt_lines (tenant_id, legal_entity_id, id, source_state_binding_id, physical_binding_id)", sql);
        Assert.Contains("REFERENCES procurement.service_acceptances (tenant_id, legal_entity_id, id, source_state_binding_id)", sql);
        Assert.DoesNotContain("NOT VALID", sql);
        Assert.Contains("c.convalidated AND c.condeferrable AND c.condeferred", ReceivingOwnershipConstraintSql.ManifestQuery);
        Assert.Contains("c.conrelid=to_regclass(e.child) AND c.confrelid=to_regclass(e.parent)", ReceivingOwnershipConstraintSql.ManifestQuery);
        Assert.Contains("i.indisunique AND i.indisvalid AND i.indimmediate", ReceivingOwnershipConstraintSql.ManifestQuery);
    }

    [Fact]
    public async Task Persisted_goods_draft_can_record_fact_without_mutating_an_EF_key_or_fabricating_draft_pins()
    {
        await using var db = Context();
        var row = new GoodsReceiptLine { GoodsReceiptId = Guid.NewGuid(), PurchaseOrderLineId = Guid.NewGuid(),
            ExecutionVersion = 1, SourcePurchaseOrderId = Guid.NewGuid(), IssuedBindingId = Guid.NewGuid(), ReceivedQuantity = 1, Status = "Draft" };
        db.AddScoped(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        row = await db.GoodsReceiptLines.SingleAsync();
        Assert.Equal(Guid.Empty, row.SourceStateBindingId); Assert.Equal(Guid.Empty, row.PhysicalBindingId);
        var state = StateFor(row.TenantId, row.LegalEntityId, row.SourcePurchaseOrderId!.Value,
            row.IssuedBindingId, row.PurchaseOrderLineId, ReceivingKind.Goods);
        state.Sequence = 1; db.AddScoped(state);
        row.ReceivingSourceStateId = state.Id; row.SourceStateBindingId = state.Id;
        row.PhysicalObservationId = Guid.NewGuid(); row.PhysicalBindingId = row.PhysicalObservationId.Value;
        row.PhysicalPolicyVersionId = state.ClassificationPolicyVersionId; row.PhysicalSequence = 1;
        row.PhysicalRecordedAt = DateTimeOffset.UtcNow; row.PhysicalEvidenceRef = "RECORD"; row.PhysicalEffectKey = Guid.NewGuid(); row.Status = "Held";
        db.ChangeTracker.DetectChanges();
        Assert.True(db.Entry(row).Property(x => x.PhysicalBindingId).IsModified);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var saved = await db.GoodsReceiptLines.SingleAsync();
        Assert.Equal(saved.PhysicalObservationId, saved.PhysicalBindingId);
        Assert.Equal(1, (await db.ReceivingSourceStates.SingleAsync()).Sequence);
        saved.PhysicalEvidenceRef = "OVERWRITE";
        Assert.Equal("PhysicalReceiptEvidenceImmutable", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
    }

    [Fact]
    public async Task Persisted_service_draft_can_certify_with_exact_same_save_positive_effect()
    {
        await using var db = Context();
        var row = new ServiceAcceptance { ExecutionVersion = 1, SourcePurchaseOrderId = Guid.NewGuid(),
            IssuedBindingId = Guid.NewGuid(), PurchaseOrderLineId = Guid.NewGuid(), OwnerSubjectId = "receiver", Status = "Draft", Notes = "Preserved" };
        db.AddScoped(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        row = await db.ServiceAcceptances.SingleAsync(); Assert.Equal(Guid.Empty, row.SourceStateBindingId);
        var state = StateFor(row.TenantId, row.LegalEntityId, row.SourcePurchaseOrderId!.Value,
            row.IssuedBindingId, row.PurchaseOrderLineId, ReceivingKind.Service);
        state.Sequence = 1; state.CurrentAcceptedQuantity = 1; state.CurrentAcceptedNetAmount = 10; db.AddScoped(state);
        var actor = Guid.NewGuid();
        var effect = new ReceivingAcceptanceEffect { Source = state.Source, ReceivingSourceStateId = state.Id,
            ServiceAcceptanceId = row.Id, AcceptanceRecordId = row.Id, CreatedBy = "receiver", Kind = AcceptanceEffectKind.Acceptance,
            QuantityDelta = 1, NetAmountDelta = 10, ActorWorkerId = actor, ReceivingPolicyVersionId = state.ClassificationPolicyVersionId,
            LedgerSequence = 1, EffectKey = Guid.NewGuid(), EvidenceReference = "CERTIFY", OccurredAt = DateTimeOffset.UtcNow };
        db.AddScoped(effect);
        row.ReceivingSourceStateId = state.Id; row.SourceStateBindingId = state.Id; row.CertificationEffectId = effect.Id;
        row.CertifiedAt = effect.OccurredAt; row.OwnerWorkerId = actor; row.AcceptedQuantity = 1; row.AcceptedAmount = 10;
        row.CurrentAcceptedQuantity = 1; row.CurrentAcceptedNetAmount = 10; row.Status = "Accepted";
        db.ChangeTracker.DetectChanges(); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var saved = await db.ServiceAcceptances.SingleAsync();
        Assert.Equal(state.Id, saved.SourceStateBindingId); Assert.Equal("Preserved", saved.Notes);
        Assert.Single(await db.ReceivingAcceptanceEffects.ToArrayAsync());
        saved.Notes = "OVERWRITE";
        Assert.Equal("ServiceCertificationImmutable", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
    }

    private static ReceivingSourceState StateFor(string tenant, Guid company, Guid order, Guid issued, Guid line, ReceivingKind kind)
    {
        const string classification = "{}";
        var pin = new ReceivingSourcePin(tenant, company, Guid.NewGuid(), order, issued, line, Guid.NewGuid(), 1,
            new string('a', 64), new string('b', 64), "FIXTURE", ProcurementEvidenceEncoding.PolicyJsonDigest(classification),
            kind, "IT", kind == ReceivingKind.Goods ? "GOODS" : "SERVICE", "ZMW", "EA", 1, 10, ControlledReceiving.NetPriceBasis);
        return new() { TenantId = tenant, LegalEntityId = company, PurchaseOrderId = order, IssuedVersionId = issued,
            PurchaseOrderLineId = line, SubmissionId = Guid.NewGuid(), DeliveryEvidenceId = Guid.NewGuid(), Source = pin,
            SourceSha256 = ReceivingPersistenceProjection.PinDigest(pin), ClassificationJson = classification, ClassificationPolicyVersionId = Guid.NewGuid() };
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
