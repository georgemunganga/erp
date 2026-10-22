using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<ReceivingSourceState> ReceivingSourceStates => Set<ReceivingSourceState>();
    public DbSet<ReceivingAcceptanceEffect> ReceivingAcceptanceEffects => Set<ReceivingAcceptanceEffect>();
    private void ConfigureReceivingExecution(ModelBuilder model)
    {
        ConfigureBase<ReceivingSourceState>(model, "receiving_source_states");
        ConfigureBase<ReceivingAcceptanceEffect>(model, "receiving_acceptance_effects");
        var state = model.Entity<ReceivingSourceState>();
        state.Property(x => x.Source).HasConversion(x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
            x => JsonSerializer.Deserialize<ReceivingSourcePin>(x, (JsonSerializerOptions?)null)!).HasColumnType("text");
        state.Property(x => x.SourceSha256).HasMaxLength(64); state.Property(x => x.ClassificationJson).HasColumnType("text");
        state.Property(x => x.CurrentAcceptedQuantity).HasPrecision(18, 4); state.Property(x => x.CurrentAcceptedNetAmount).HasPrecision(38, 8);
        state.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.IssuedVersionId, x.PurchaseOrderLineId }).IsUnique();
        state.HasOne<PurchaseOrderLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderLineId, x.PurchaseOrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId }).OnDelete(DeleteBehavior.Restrict);
        state.HasOne<PurchaseOrderIssuedVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.IssuedVersionId, x.PurchaseOrderId, x.SubmissionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId, x.SubmissionId }).OnDelete(DeleteBehavior.Restrict);
        state.HasOne<PurchaseOrderDeliveryEvidence>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.DeliveryEvidenceId, x.IssuedVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.IssuedVersionId }).OnDelete(DeleteBehavior.Restrict);
        Link<ReceivingSourceState, PolicyVersion>(model, nameof(ReceivingSourceState.ClassificationPolicyVersionId));
        state.ToTable(t => t.HasCheckConstraint("ck_receiving_source_state", "execution_version = 1 AND sequence >= 0 AND current_accepted_quantity >= 0 AND current_accepted_net_amount >= 0 AND source_sha256 ~ '^[a-f0-9]{64}$'"));
        var header = model.Entity<GoodsReceipt>();
        header.Property(x => x.ExecutionVersion).HasDefaultValue(0); header.Property(x => x.IssuedBindingId).HasDefaultValue(Guid.Empty).ValueGeneratedNever();
        header.Property(x => x.Notes).HasMaxLength(2000);
        header.HasOne<PurchaseOrderIssuedVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.IssuedVersionId, x.PurchaseOrderId, x.SubmissionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId, x.SubmissionId }).OnDelete(DeleteBehavior.Restrict);
        header.ToTable(t => t.HasCheckConstraint("ck_receiving_header_execution", "execution_version = 0 OR (execution_version = 1 AND issued_version_id IS NOT NULL AND submission_id IS NOT NULL AND issued_binding_id = issued_version_id AND issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (recorded_at IS NULL OR (receiver_worker_id IS NOT NULL AND receiver_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)))"));
        var line = model.Entity<GoodsReceiptLine>();
        line.Property(x => x.ExecutionVersion).HasDefaultValue(0);
        line.Property(x => x.IssuedBindingId).HasDefaultValue(Guid.Empty).ValueGeneratedNever(); line.Property(x => x.SourceStateBindingId).HasDefaultValue(Guid.Empty).ValueGeneratedNever();
        line.Property(x => x.PhysicalBindingId).HasDefaultValue(Guid.Empty).ValueGeneratedNever(); line.Property(x => x.InitiallyRejectedQuantity).HasPrecision(18, 4);
        line.Property(x => x.PhysicalEvidenceRef).HasMaxLength(500);
        line.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PhysicalObservationId }).IsUnique().HasFilter("physical_observation_id IS NOT NULL");
        line.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ReceivingSourceStateId, x.PhysicalSequence }).IsUnique().HasFilter("physical_sequence IS NOT NULL");
        line.HasOne<GoodsReceipt>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptId, x.SourcePurchaseOrderId, x.IssuedBindingId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId, x.IssuedBindingId }).OnDelete(DeleteBehavior.Restrict);
        line.HasOne<ReceivingSourceState>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReceivingSourceStateId, x.SourcePurchaseOrderId, x.IssuedBindingId, x.PurchaseOrderLineId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId, x.IssuedVersionId, x.PurchaseOrderLineId }).OnDelete(DeleteBehavior.Restrict);
        Link<GoodsReceiptLine, PolicyVersion>(model, nameof(GoodsReceiptLine.PhysicalPolicyVersionId));
        line.ToTable(t => t.HasCheckConstraint("ck_receiving_line_execution", "execution_version = 0 OR (execution_version = 1 AND source_purchase_order_id IS NOT NULL AND issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (receiving_source_state_id IS NULL OR source_state_binding_id = receiving_source_state_id) AND (physical_observation_id IS NULL OR (receiving_source_state_id IS NOT NULL AND physical_binding_id = physical_observation_id AND physical_sequence IS NOT NULL AND physical_sequence > 0 AND physical_policy_version_id IS NOT NULL AND physical_effect_key IS NOT NULL AND physical_recorded_at IS NOT NULL AND received_quantity > 0 AND initially_rejected_quantity >= 0 AND initially_rejected_quantity <= received_quantity)))"));
        var inspection = model.Entity<ReceiptInspection>();
        inspection.Property(x => x.PhysicalBindingId).HasDefaultValue(Guid.Empty).ValueGeneratedNever(); inspection.Property(x => x.ExecutionVersion).HasDefaultValue(0); inspection.Property(x => x.SourceStateBindingId).HasDefaultValue(Guid.Empty).ValueGeneratedNever();
        inspection.Property(x => x.QualifiedQuantity).HasPrecision(18, 4); inspection.Property(x => x.EvidenceRef).HasMaxLength(500);
        inspection.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PhysicalObservationId }).IsUnique().HasFilter("physical_observation_id IS NOT NULL");
        inspection.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ReceivingSourceStateId, x.LedgerSequence }).IsUnique().HasFilter("ledger_sequence IS NOT NULL");
        // EF keys are immutable; draft-to-fact binding fields must remain ordinary properties.
        // The reviewed SQL overlay retains the full exact ownership FK at transaction commit.
        line.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SourceStateBindingId, x.PhysicalBindingId })
            .IsUnique().HasDatabaseName("ux_receiving_goods_physical_binding");
        inspection.HasOne<GoodsReceiptLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptLineId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        Link<ReceiptInspection, PolicyVersion>(model, nameof(ReceiptInspection.PolicyVersionId));
        inspection.ToTable(t => t.HasCheckConstraint("ck_receiving_inspection_execution", "execution_version = 0 OR (execution_version = 1 AND receiving_source_state_id IS NOT NULL AND physical_observation_id IS NOT NULL AND physical_binding_id = physical_observation_id AND source_state_binding_id = receiving_source_state_id AND inspector_worker_id IS NOT NULL AND policy_version_id IS NOT NULL AND ledger_sequence IS NOT NULL AND ledger_sequence > 0 AND effect_key IS NOT NULL AND inspected_at IS NOT NULL AND qualified_quantity >= 0 AND qualified_quantity <= inspected_quantity)"));
        var service = model.Entity<ServiceAcceptance>();
        service.Property(x => x.Notes).HasMaxLength(2000);
        service.Property(x => x.ExecutionVersion).HasDefaultValue(0); service.Property(x => x.SourceStateBindingId).HasDefaultValue(Guid.Empty).ValueGeneratedNever();
        service.Property(x => x.IssuedBindingId).HasDefaultValue(Guid.Empty).ValueGeneratedNever(); service.Property(x => x.EvidenceRef).HasMaxLength(500);
        service.Property(x => x.AcceptedAmount).HasPrecision(38, 8); service.Property(x => x.CurrentAcceptedQuantity).HasPrecision(18, 4);
        service.Property(x => x.CurrentAcceptedNetAmount).HasPrecision(38, 8);
        service.HasOne<ReceivingSourceState>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReceivingSourceStateId, x.SourcePurchaseOrderId, x.IssuedBindingId, x.PurchaseOrderLineId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId, x.IssuedVersionId, x.PurchaseOrderLineId }).OnDelete(DeleteBehavior.Restrict);
        Link<ServiceAcceptance, PolicyVersion>(model, nameof(ServiceAcceptance.PolicyVersionId));
        service.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ReceivingSourceStateId, x.DeliverableRef }).IsUnique().HasFilter("certified_at IS NOT NULL AND execution_version = 1");
        service.ToTable(t => t.HasCheckConstraint("ck_service_execution", "execution_version = 0 OR (execution_version = 1 AND source_purchase_order_id IS NOT NULL AND issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (receiving_source_state_id IS NULL OR source_state_binding_id = receiving_source_state_id) AND (certified_at IS NULL OR (receiving_source_state_id IS NOT NULL AND certification_effect_id IS NOT NULL AND owner_worker_id IS NOT NULL AND policy_version_id IS NOT NULL AND accepted_quantity > 0 AND accepted_amount >= 0)))"));
        var effect = model.Entity<ReceivingAcceptanceEffect>();
        effect.Property(x => x.Source).HasConversion(x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
            x => JsonSerializer.Deserialize<ReceivingSourcePin>(x, (JsonSerializerOptions?)null)!).HasColumnType("text");
        effect.Property(x => x.CorrectionEvidence).HasConversion(x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
            x => JsonSerializer.Deserialize<ReceivingCorrectionPin>(x, (JsonSerializerOptions?)null)).HasColumnType("text");
        effect.Property(x => x.QuantityDelta).HasPrecision(18, 4); effect.Property(x => x.NetAmountDelta).HasPrecision(38, 8);
        effect.Property(x => x.EvidenceReference).HasMaxLength(500); effect.Property(x => x.DeliverableReference).HasMaxLength(200); effect.Property(x => x.Reason).HasMaxLength(2000);
        effect.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.EffectKey }).IsUnique();
        effect.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ReceivingSourceStateId, x.LedgerSequence }).IsUnique();
        Link<ReceivingAcceptanceEffect, ReceivingSourceState>(model, nameof(ReceivingAcceptanceEffect.ReceivingSourceStateId));
        Link<ReceivingAcceptanceEffect, PolicyVersion>(model, nameof(ReceivingAcceptanceEffect.ReceivingPolicyVersionId));
        effect.HasOne<GoodsReceiptLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptLineId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        service.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SourceStateBindingId })
            .IsUnique().HasDatabaseName("ux_receiving_service_state_binding");
        effect.HasOne<ServiceAcceptance>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ServiceAcceptanceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        effect.HasOne<ReceiptInspection>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.InspectionEvidenceId, x.GoodsReceiptLineId, x.ReceivingSourceStateId, x.PhysicalObservationId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.GoodsReceiptLineId, x.SourceStateBindingId, x.PhysicalBindingId }).OnDelete(DeleteBehavior.Restrict);
        effect.ToTable(t => t.HasCheckConstraint("ck_receiving_positive_effect", "kind = 1 AND quantity_delta > 0 AND net_amount_delta >= 0 AND ledger_sequence > 0 AND original_acceptance_effect_id IS NULL AND correction_evidence IS NULL AND ((goods_receipt_line_id IS NOT NULL AND service_acceptance_id IS NULL AND acceptance_record_id = goods_receipt_line_id AND physical_observation_id IS NOT NULL) OR (service_acceptance_id IS NOT NULL AND goods_receipt_line_id IS NULL AND acceptance_record_id = service_acceptance_id AND physical_observation_id IS NULL AND inspection_evidence_id IS NULL))"));
    }
    private void GuardReceivingExecution()
    {
        foreach (var entry in ChangeTracker.Entries<ReceivingAcceptanceEffect>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("ReceivingEffectsAppendOnly");
            if (entry.State == EntityState.Added && (entry.Entity.Kind != AcceptanceEffectKind.Acceptance || entry.Entity.Source.TenantId != scope.TenantId
                || entry.Entity.Source.LegalEntityId != scope.LegalEntityId || entry.Entity.ReceivingSourceStateId == Guid.Empty))
                throw new InvalidOperationException("ReceivingPositiveEvidenceRequired");
            if (entry.State == EntityState.Added)
            {
                var effect = entry.Entity; var state = ApprovalEvidence<ReceivingSourceState>(effect.ReceivingSourceStateId);
                if (state is null || Entry(state).State is not (EntityState.Added or EntityState.Modified) || state.Sequence != effect.LedgerSequence
                    || effect.Source != state.Source || effect.ActorWorkerId == Guid.Empty
                    || effect.CreatedBy != scope.SubjectId || effect.QuantityDelta <= 0
                    || effect.NetAmountDelta != ExactDecimalArithmetic.Multiply(effect.QuantityDelta, effect.Source.NetUnitPrice)
                    || effect.Source.Kind == ReceivingKind.Goods && (effect.GoodsReceiptLineId != effect.AcceptanceRecordId || effect.ServiceAcceptanceId != null)
                    || effect.Source.Kind == ReceivingKind.Service && (effect.ServiceAcceptanceId != effect.AcceptanceRecordId || effect.GoodsReceiptLineId != null))
                    throw new InvalidOperationException("ReceivingPositiveEvidenceBindingInvalid");
            }
        }
        foreach (var entry in ChangeTracker.Entries<ReceivingSourceState>())
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified && entry.Properties.Any(x => x.IsModified
                && x.Metadata.Name is not ("Sequence" or "CurrentAcceptedQuantity" or "CurrentAcceptedNetAmount" or "Version" or "UpdatedAt" or "UpdatedBy")))
                throw new InvalidOperationException("ReceivingSourcePinImmutable");
            if (entry.State is EntityState.Added or EntityState.Modified) ReceivingPersistenceProjection.VerifyState(entry.Entity);
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                var before = entry.State == EntityState.Added ? 0 : entry.OriginalValues.GetValue<long>(nameof(ReceivingSourceState.Sequence));
                var physical = ChangeTracker.Entries<GoodsReceiptLine>().Where(x => x.Entity.ReceivingSourceStateId == entry.Entity.Id
                    && x.Entity.PhysicalSequence != null && (x.State == EntityState.Added || x.State == EntityState.Modified
                        && x.OriginalValues.GetValue<Guid?>(nameof(GoodsReceiptLine.PhysicalObservationId)) == null)).Select(x => x.Entity.PhysicalSequence!.Value);
                var inspections = ChangeTracker.Entries<ReceiptInspection>().Where(x => x.State == EntityState.Added && x.Entity.ReceivingSourceStateId == entry.Entity.Id).Select(x => x.Entity.LedgerSequence!.Value);
                var effects = ChangeTracker.Entries<ReceivingAcceptanceEffect>().Where(x => x.State == EntityState.Added && x.Entity.ReceivingSourceStateId == entry.Entity.Id).Select(x => x.Entity.LedgerSequence);
                var sequence = physical.Concat(inspections).Concat(effects).Order().ToArray();
                if (entry.Entity.Sequence != before + sequence.Length || sequence.Where((value, index) => value != before + index + 1).Any()
                    || entry.State == EntityState.Added && sequence.Length == 0)
                    throw new InvalidOperationException("ReceivingSequenceFactRequired");
            }
            if (entry.State == EntityState.Modified && (entry.Property(x => x.CurrentAcceptedQuantity).IsModified || entry.Property(x => x.CurrentAcceptedNetAmount).IsModified))
            {
                var effects = ChangeTracker.Entries<ReceivingAcceptanceEffect>().Where(x => x.State == EntityState.Added && x.Entity.ReceivingSourceStateId == entry.Entity.Id).Select(x => x.Entity).ToArray();
                if (effects.Length == 0 || entry.Entity.CurrentAcceptedQuantity != ExactDecimalArithmetic.Add(entry.OriginalValues.GetValue<decimal>(nameof(ReceivingSourceState.CurrentAcceptedQuantity)), ExactDecimalArithmetic.Sum(effects.Select(x => x.QuantityDelta)))
                    || entry.Entity.CurrentAcceptedNetAmount != ExactDecimalArithmetic.Add(entry.OriginalValues.GetValue<decimal>(nameof(ReceivingSourceState.CurrentAcceptedNetAmount)), ExactDecimalArithmetic.Sum(effects.Select(x => x.NetAmountDelta))))
                    throw new InvalidOperationException("ReceivingCounterEffectRequired");
            }
        }
        foreach (var entry in ChangeTracker.Entries<GoodsReceipt>())
        {
            if (entry.State == EntityState.Modified && entry.OriginalValues.GetValue<DateTimeOffset?>(nameof(GoodsReceipt.RecordedAt)) is not null
                && entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not ("Status" or "Version" or "UpdatedAt" or "UpdatedBy")))
                throw new InvalidOperationException("RecordedReceiptImmutable");
        }
        foreach (var entry in ChangeTracker.Entries<GoodsReceiptLine>())
            if (entry.State == EntityState.Modified && entry.OriginalValues.GetValue<Guid?>(nameof(GoodsReceiptLine.PhysicalObservationId)) is not null
                && entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not ("AcceptedQuantity" or "RejectedQuantity" or "Status" or "Version" or "UpdatedAt" or "UpdatedBy")))
                throw new InvalidOperationException("PhysicalReceiptEvidenceImmutable");
        foreach (var entry in ChangeTracker.Entries<GoodsReceiptLine>())
            if (entry.State == EntityState.Modified && entry.Property(x => x.AcceptedQuantity).IsModified)
            {
                var effects = ChangeTracker.Entries<ReceivingAcceptanceEffect>().Where(x => x.State == EntityState.Added && x.Entity.GoodsReceiptLineId == entry.Entity.Id).Select(x => x.Entity).ToArray();
                if (effects.Length == 0 || entry.Entity.AcceptedQuantity != ExactDecimalArithmetic.Add(entry.OriginalValues.GetValue<decimal>(nameof(GoodsReceiptLine.AcceptedQuantity)), ExactDecimalArithmetic.Sum(effects.Select(x => x.QuantityDelta))))
                    throw new InvalidOperationException("ReceivingCounterEffectRequired");
            }
        foreach (var entry in ChangeTracker.Entries<GoodsReceiptLine>())
            if (entry.State == EntityState.Modified && entry.Property(x => x.RejectedQuantity).IsModified
                && entry.OriginalValues.GetValue<Guid?>(nameof(GoodsReceiptLine.PhysicalObservationId)) is not null)
            {
                var disposition = ChangeTracker.Entries<ReceiptInspection>().Where(x => x.State == EntityState.Added && x.Entity.GoodsReceiptLineId == entry.Entity.Id).Select(x => x.Entity).SingleOrDefault();
                if (disposition is null || entry.Entity.RejectedQuantity != ExactDecimalArithmetic.Add(entry.Entity.InitiallyRejectedQuantity,
                    ExactDecimalArithmetic.Subtract(disposition.InspectedQuantity, disposition.QualifiedQuantity)))
                    throw new InvalidOperationException("ReceivingInspectionDispositionRequired");
            }
        foreach (var entry in ChangeTracker.Entries<PurchaseOrderLine>())
            if (entry.State == EntityState.Modified && entry.Property(x => x.AcceptedQuantity).IsModified)
            {
                var deltas = ChangeTracker.Entries<ReceivingAcceptanceEffect>().Where(x => x.State == EntityState.Added && x.Entity.Source.PurchaseOrderLineId == entry.Entity.Id).Select(x => x.Entity.QuantityDelta).ToArray();
                if (deltas.Length == 0 || entry.Entity.AcceptedQuantity != ExactDecimalArithmetic.Add(entry.OriginalValues.GetValue<decimal>(nameof(PurchaseOrderLine.AcceptedQuantity)), ExactDecimalArithmetic.Sum(deltas)))
                    throw new InvalidOperationException("ReceivingCounterEffectRequired");
            }
        foreach (var entry in ChangeTracker.Entries<PurchaseRequestLine>())
            if (entry.State == EntityState.Modified && entry.Property(x => x.AcceptedQuantity).IsModified)
            {
                var deltas = ChangeTracker.Entries<ReceivingAcceptanceEffect>().Where(x => x.State == EntityState.Added && x.Entity.Source.PurchaseRequestLineId == entry.Entity.Id).Select(x => x.Entity.QuantityDelta).ToArray();
                if (deltas.Length == 0 || entry.Entity.AcceptedQuantity != ExactDecimalArithmetic.Add(entry.OriginalValues.GetValue<decimal>(nameof(PurchaseRequestLine.AcceptedQuantity)), ExactDecimalArithmetic.Sum(deltas)))
                    throw new InvalidOperationException("ReceivingCounterEffectRequired");
            }
        foreach (var entry in ChangeTracker.Entries<ServiceAcceptance>())
            if (entry.State == EntityState.Modified && (entry.Property(x => x.CurrentAcceptedQuantity).IsModified || entry.Property(x => x.CurrentAcceptedNetAmount).IsModified))
            {
                var effects = ChangeTracker.Entries<ReceivingAcceptanceEffect>().Where(x => x.State == EntityState.Added && x.Entity.ServiceAcceptanceId == entry.Entity.Id).Select(x => x.Entity).ToArray();
                if (effects.Length == 0 || entry.Entity.CurrentAcceptedQuantity != ExactDecimalArithmetic.Add(entry.OriginalValues.GetValue<decimal>(nameof(ServiceAcceptance.CurrentAcceptedQuantity)), ExactDecimalArithmetic.Sum(effects.Select(x => x.QuantityDelta)))
                    || entry.Entity.CurrentAcceptedNetAmount != ExactDecimalArithmetic.Add(entry.OriginalValues.GetValue<decimal>(nameof(ServiceAcceptance.CurrentAcceptedNetAmount)), ExactDecimalArithmetic.Sum(effects.Select(x => x.NetAmountDelta))))
                    throw new InvalidOperationException("ReceivingCounterEffectRequired");
            }
        foreach (var entry in ChangeTracker.Entries<ReceiptInspection>())
            if (entry.State == EntityState.Modified && entry.OriginalValues.GetValue<int>(nameof(ReceiptInspection.ExecutionVersion)) == 1)
                throw new InvalidOperationException("InspectionEvidenceImmutable");
        foreach (var entry in ChangeTracker.Entries<ServiceAcceptance>())
            if ((entry.State is EntityState.Added or EntityState.Modified) && entry.Entity.ExecutionVersion == 1 && entry.Entity.CertifiedAt != null
                && (entry.State == EntityState.Added || entry.OriginalValues.GetValue<DateTimeOffset?>(nameof(ServiceAcceptance.CertifiedAt)) == null))
            {
                var effect = ChangeTracker.Entries<ReceivingAcceptanceEffect>().Where(x => x.State == EntityState.Added && x.Entity.Id == entry.Entity.CertificationEffectId
                    && x.Entity.ServiceAcceptanceId == entry.Entity.Id).Select(x => x.Entity).SingleOrDefault();
                if (effect is null || entry.Entity.ReceivingSourceStateId != effect.ReceivingSourceStateId
                    || entry.Entity.SourceStateBindingId != effect.ReceivingSourceStateId || entry.Entity.AcceptedQuantity != effect.QuantityDelta
                    || entry.Entity.AcceptedAmount != effect.NetAmountDelta || entry.Entity.OwnerWorkerId != effect.ActorWorkerId)
                    throw new InvalidOperationException("ReceivingServicePositiveEffectRequired");
            }
        foreach (var entry in ChangeTracker.Entries<ServiceAcceptance>())
            if (entry.State == EntityState.Modified && entry.OriginalValues.GetValue<DateTimeOffset?>(nameof(ServiceAcceptance.CertifiedAt)) is not null
                && entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not ("CurrentAcceptedQuantity" or "CurrentAcceptedNetAmount" or "Status" or "Version" or "UpdatedAt" or "UpdatedBy")))
                throw new InvalidOperationException("ServiceCertificationImmutable");
    }
}
