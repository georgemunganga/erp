using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<InvoiceMatchMappingVersion> InvoiceMatchMappingVersions => Set<InvoiceMatchMappingVersion>();
    public DbSet<InvoiceMatchAllocation> InvoiceMatchAllocations => Set<InvoiceMatchAllocation>();
    public DbSet<InvoiceMatchExposureState> InvoiceMatchExposureStates => Set<InvoiceMatchExposureState>();
    public DbSet<InvoiceProcurementOwnerBaseline> InvoiceProcurementOwnerBaselines => Set<InvoiceProcurementOwnerBaseline>();
    public DbSet<InvoiceProcurementOwnerBaselineRow> InvoiceProcurementOwnerBaselineRows => Set<InvoiceProcurementOwnerBaselineRow>();

    private void ConfigureInvoiceMatchingExecution(ModelBuilder model)
    {
        // A short explicit name avoids renaming P24/P25's retained five-column key
        // when PostgreSQL's identifier limit truncates the new four-column key.
        model.Entity<PurchaseOrderIssuedVersion>()
            .HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId })
            .HasName("ak_invoice_issued_order_binding");
        ConfigureBase<InvoiceMatchMappingVersion>(model, "invoice_match_mapping_versions");
        ConfigureBase<InvoiceMatchAllocation>(model, "invoice_match_allocations");
        ConfigureBase<InvoiceMatchExposureState>(model, "invoice_match_exposure_states");
        ConfigureBase<InvoiceProcurementOwnerBaseline>(model, "invoice_owner_baselines");
        ConfigureBase<InvoiceProcurementOwnerBaselineRow>(model, "invoice_owner_baseline_rows");
        var ownerBaseline=model.Entity<InvoiceProcurementOwnerBaseline>();
        ownerBaseline.Property(x=>x.CapturedThroughMigration).HasMaxLength(150);
        ownerBaseline.Property(x=>x.CatalogManifestJson).HasColumnType("text");
        ownerBaseline.Property(x=>x.MakerPrincipalId).HasMaxLength(200);
        foreach(var name in new[]{nameof(InvoiceProcurementOwnerBaseline.CatalogManifestSha256),nameof(InvoiceProcurementOwnerBaseline.InventorySetSha256),nameof(InvoiceProcurementOwnerBaseline.InitialActiveClaimsSha256),nameof(InvoiceProcurementOwnerBaseline.InitialExposureSetSha256)})
            ownerBaseline.Property(name).HasMaxLength(64);
        // Retain rejected/stale captures. The qualified owner must choose one
        // approved current capture under the serializable exposure lock.
        ownerBaseline.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.CapturedAtUtc});
        ownerBaseline.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.CoreCaseId}).IsUnique().HasDatabaseName("ux_invoice_owner_baseline_case");
        ownerBaseline.ToTable(t=>t.HasCheckConstraint("ck_invoice_owner_baseline",
            "schema_version=2 AND inventory_row_count>=0 AND core_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND catalog_manifest_sha256 ~ '^[0-9a-f]{64}$' AND inventory_set_sha256 ~ '^[0-9a-f]{64}$' AND initial_active_claims_sha256 ~ '^[0-9a-f]{64}$' AND initial_exposure_set_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(catalog_manifest_json) BETWEEN 1 AND 262144 AND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND isfinite(captured_at_utc)"));
        var ownerRow=model.Entity<InvoiceProcurementOwnerBaselineRow>();
        ownerRow.Property(x=>x.TableName).HasMaxLength(100);
        ownerRow.Property(x=>x.InitialClassification).HasMaxLength(50);
        ownerRow.Property(x=>x.OriginalRowSha256).HasMaxLength(64);
        ownerRow.Property(x=>x.ImmutableFieldsSha256).HasMaxLength(64);
        ownerRow.HasOne<InvoiceProcurementOwnerBaseline>().WithMany().HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.BaselineId})
            .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        ownerRow.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.BaselineId,x.TableName,x.SourceRowId})
            .IsUnique().HasDatabaseName("ux_invoice_owner_original_row");
        ownerRow.ToTable(t=>t.HasCheckConstraint("ck_invoice_owner_baseline_row",
            "source_row_id <> '00000000-0000-0000-0000-000000000000'::uuid AND source_row_version>0 AND table_name ~ '^[a-z][a-z0-9_]{0,99}$' AND initial_classification IN ('ClaimBearing','HistoricalOnly','MutableDraft','BlockedAmbiguity') AND original_row_sha256 ~ '^[0-9a-f]{64}$' AND immutable_fields_sha256 ~ '^[0-9a-f]{64}$'"));
        var mapping = model.Entity<InvoiceMatchMappingVersion>();
        mapping.Property(x => x.MappingJson).HasColumnType("text"); mapping.Property(x => x.VerificationEvidenceJson).HasColumnType("text");
        mapping.Property(x => x.MappingSha256).HasMaxLength(64); mapping.Property(x => x.InvoiceOriginalSha256).HasMaxLength(64);
        mapping.Property(x => x.IdentitySchemaVersion).HasDefaultValue(1);
        mapping.Property(x => x.SourceSchemaVersion).HasColumnName("source_schema_version");
        mapping.Property(x => x.ActorPrincipalId).HasMaxLength(200); mapping.Property(x => x.VerificationState).HasMaxLength(100);
        mapping.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierInvoiceId, x.MappingRevision }).IsUnique();
        Link<InvoiceMatchMappingVersion, SupplierInvoice>(model, nameof(InvoiceMatchMappingVersion.SupplierInvoiceId));
        mapping.HasOne<PurchaseOrderIssuedVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.IssuedVersionId, x.PurchaseOrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId }).OnDelete(DeleteBehavior.Restrict);
        mapping.ToTable(t => t.HasCheckConstraint("ck_invoice_mapping_evidence", "mapping_revision > 0 AND mapping_sha256 ~ '^[0-9a-f]{64}$' AND invoice_original_sha256 ~ '^[0-9a-f]{64}$' AND identity_schema_version IN (1,2) AND (source_schema_version IS NULL OR (source_schema_version=3 AND identity_schema_version=2)) AND (identity_schema_version=2 OR actor_worker_id IS NOT NULL) AND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND actor_principal_id IS NOT NULL AND (identity_schema_version=1 OR (actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000'))"));
        var exposure = model.Entity<InvoiceMatchExposureState>();
        exposure.Property(x => x.CurrencyCode).HasMaxLength(3); exposure.Property(x => x.CoverageState).HasMaxLength(30);
        exposure.Property(x => x.OwnerReference).HasMaxLength(500); exposure.Property(x => x.OwnerWatermark).HasMaxLength(500);
        exposure.Property(x => x.OwnerEvidenceSha256).HasMaxLength(64);
        exposure.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId, x.IssuedVersionId }).IsUnique();
        exposure.HasOne<PurchaseOrderIssuedVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.IssuedVersionId, x.PurchaseOrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId }).OnDelete(DeleteBehavior.Restrict);
        Link<InvoiceMatchExposureState, Supplier>(model, nameof(InvoiceMatchExposureState.SupplierId));
        exposure.ToTable(t => t.HasCheckConstraint("ck_invoice_exposure_state", "sequence >= 0 AND coverage_state IN ('Unknown','Verified','Stale') AND currency_code ~ '^[A-Z]{3}$' AND (coverage_state <> 'Verified' OR (owner_reference IS NOT NULL AND owner_watermark IS NOT NULL AND owner_evidence_sha256 IS NOT NULL AND owner_evidence_sha256 ~ '^[0-9a-f]{64}$' AND owner_valid_until IS NOT NULL))"));
        var result = model.Entity<InvoiceMatchResult>();
        result.Property(x => x.ExecutionVersion).HasDefaultValue(0);
        result.Property(x => x.EvidenceJson).HasColumnType("text"); result.Property(x => x.SourceWatermarksJson).HasColumnType("text");
        foreach (var name in new[] { nameof(InvoiceMatchResult.InvoiceOriginalSha256), nameof(InvoiceMatchResult.InputFingerprint), nameof(InvoiceMatchResult.EvidenceSha256), nameof(InvoiceMatchResult.PolicyRulesSha256) })
            result.Property(name).HasMaxLength(64);
        result.Property(x => x.ActorPrincipalId).HasMaxLength(200);
        result.Property(x => x.QuantityVariance).HasPrecision(18,4); result.Property(x => x.PriceVariance).HasPrecision(18,4); result.Property(x => x.TaxVariance).HasPrecision(38,8);
        result.HasOne<InvoiceMatchMappingVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.MappingVersionId, x.SupplierInvoiceId, x.PurchaseOrderId, x.IssuedVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId, x.PurchaseOrderId, x.IssuedVersionId }).OnDelete(DeleteBehavior.Restrict);
        result.HasOne<PurchaseOrderIssuedVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.IssuedVersionId, x.PurchaseOrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId }).OnDelete(DeleteBehavior.Restrict);
        result.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CommandKey }).IsUnique().HasFilter("command_key IS NOT NULL");
        result.ToTable(t => t.HasCheckConstraint("ck_invoice_match_execution", "execution_version = 0 OR (execution_version IN (1,2) AND mapping_version_id IS NOT NULL AND purchase_order_id IS NOT NULL AND issued_version_id IS NOT NULL AND invoice_version > 0 AND command_key IS NOT NULL AND (execution_version=2 OR actor_worker_id IS NOT NULL) AND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND actor_principal_id IS NOT NULL AND (execution_version=1 OR (actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000')) AND invoice_original_sha256 IS NOT NULL AND invoice_original_sha256 ~ '^[0-9a-f]{64}$' AND input_fingerprint IS NOT NULL AND input_fingerprint ~ '^[0-9a-f]{64}$' AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 IS NOT NULL AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND evidence_json IS NOT NULL AND source_watermarks_json IS NOT NULL AND outcome IN ('Compared','Blocked'))"));
        var invoice = model.Entity<SupplierInvoice>(); invoice.Property(x => x.MatchingState).HasMaxLength(50).HasDefaultValue("NotCompared");
        invoice.HasOne<InvoiceMatchMappingVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.CurrentMappingVersionId, x.Id })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId }).OnDelete(DeleteBehavior.Restrict);
        foreach (var pointer in new[] { nameof(SupplierInvoice.LastMatchResultId), nameof(SupplierInvoice.ActiveMatchClaimResultId) })
            invoice.HasOne<InvoiceMatchResult>().WithMany().HasForeignKey(nameof(ProcurementRecord.TenantId), nameof(ProcurementRecord.LegalEntityId), pointer, nameof(ProcurementRecord.Id))
                .HasPrincipalKey(nameof(ProcurementRecord.TenantId), nameof(ProcurementRecord.LegalEntityId), nameof(ProcurementRecord.Id), nameof(InvoiceMatchResult.SupplierInvoiceId)).OnDelete(DeleteBehavior.Restrict);
        var allocation = model.Entity<InvoiceMatchAllocation>();
        allocation.Property(x => x.Quantity).HasPrecision(18,4); allocation.Property(x => x.NetAmount).HasPrecision(38,8); allocation.Property(x => x.TaxAmount).HasPrecision(38,8);
        allocation.Property(x => x.Kind).HasMaxLength(20); allocation.Property(x => x.CurrencyCode).HasMaxLength(3); allocation.Property(x => x.UnitCode).HasMaxLength(20);
        allocation.Property(x => x.ValidationReference).HasMaxLength(200); allocation.Property(x => x.OwnerCoverageReference).HasMaxLength(500);
        allocation.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.MatchResultId, x.SupplierInvoiceLineId }).IsUnique();
        allocation.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ExposureStateId, x.ExposureSequence, x.SupplierInvoiceLineId }).IsUnique();
        allocation.HasOne<SupplierInvoiceLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierInvoiceLineId, x.SupplierInvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId }).OnDelete(DeleteBehavior.Restrict);
        allocation.HasOne<InvoiceMatchResult>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.MatchResultId, x.SupplierInvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId }).OnDelete(DeleteBehavior.Restrict);
        allocation.HasOne<InvoiceMatchExposureState>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ExposureStateId, x.PurchaseOrderId, x.IssuedVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId, x.IssuedVersionId }).OnDelete(DeleteBehavior.Restrict);
        allocation.HasOne<PurchaseOrderLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderLineId, x.PurchaseOrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId }).OnDelete(DeleteBehavior.Restrict);
        allocation.HasOne<ReceivingSourceState>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReceivingSourceStateId, x.PurchaseOrderId, x.IssuedVersionId, x.PurchaseOrderLineId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId, x.IssuedVersionId, x.PurchaseOrderLineId }).OnDelete(DeleteBehavior.Restrict);
        // Mutable receiving completion pins must never become EF principal keys.
        // The migration-owned deferred overlay below binds the exact line/state tuple.
        allocation.HasOne<GoodsReceiptLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GoodsReceiptLineId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        allocation.HasOne<ServiceAcceptance>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ServiceAcceptanceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<GoodsReceiptLine>().HasIndex(x => new { x.TenantId,x.LegalEntityId,x.Id,x.PurchaseOrderLineId,x.SourceStateBindingId })
            .IsUnique().HasDatabaseName("ux_invoice_goods_exact_source_binding");
        model.Entity<ServiceAcceptance>().HasIndex(x => new { x.TenantId,x.LegalEntityId,x.Id,x.PurchaseOrderLineId,x.SourceStateBindingId })
            .IsUnique().HasDatabaseName("ux_invoice_service_exact_source_binding");
        Link<InvoiceMatchAllocation, Supplier>(model, nameof(InvoiceMatchAllocation.SupplierId));
        Link<InvoiceMatchAllocation, InvoiceMatchAllocation>(model, nameof(InvoiceMatchAllocation.OriginalDebitId));
        allocation.ToTable(t => t.HasCheckConstraint("ck_invoice_match_allocation", "kind = 'Debit' AND original_debit_id IS NULL AND quantity > 0 AND net_amount >= 0 AND tax_amount >= 0 AND exposure_sequence > 0 AND (goods_receipt_line_id IS NULL OR service_acceptance_id IS NULL) AND ((goods_receipt_line_id IS NULL AND service_acceptance_id IS NULL AND receiving_source_state_id IS NULL) OR (receiving_source_state_id IS NOT NULL AND (goods_receipt_line_id IS NOT NULL OR service_acceptance_id IS NOT NULL)))"));
        var exception = model.Entity<InvoiceException>();
        foreach (var pointer in new[] { nameof(InvoiceException.MatchResultId), nameof(InvoiceException.ResolvingMatchResultId) })
            exception.HasOne<InvoiceMatchResult>().WithMany().HasForeignKey(nameof(ProcurementRecord.TenantId), nameof(ProcurementRecord.LegalEntityId), pointer, nameof(InvoiceException.SupplierInvoiceId))
                .HasPrincipalKey(nameof(ProcurementRecord.TenantId), nameof(ProcurementRecord.LegalEntityId), nameof(ProcurementRecord.Id), nameof(InvoiceMatchResult.SupplierInvoiceId)).OnDelete(DeleteBehavior.Restrict);
        exception.HasOne<SupplierInvoiceLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierInvoiceLineId, x.SupplierInvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<InvoiceException>().Property(x => x.DiscrepancyKey).HasMaxLength(150);
    }

    private void GuardInvoiceMatchingExecution()
    {
        foreach (var entry in ChangeTracker.Entries<InvoiceMatchMappingVersion>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("InvoiceMappingAppendOnly");
            if (entry.State == EntityState.Added)
            {
                var invoice = ApprovalEvidence<SupplierInvoice>(entry.Entity.SupplierInvoiceId) ?? throw new InvalidOperationException("InvoiceMappingSourceMissing");
                var lines = SupplierInvoiceLines.Where(x => x.SupplierInvoiceId == invoice.Id && x.IsActive).ToArray();
                _ = InvoiceMappingReviewSourceDispatch.Verify(entry.Entity, invoice, lines);
                if (entry.Entity.ActorPrincipalId != scope.SubjectId || !ProcurementPrincipalEvidence.OptionalWorker(entry.Entity.ActorWorkerId)
                    || entry.Entity.IdentitySchemaVersion == 2 && entry.Entity.ActorWorkerId != scope.CoreLinkedWorkerId) throw new InvalidOperationException("InvoiceMappingActorInvalid");
            }
        }
        foreach (var entry in ChangeTracker.Entries<InvoiceMatchAllocation>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("InvoiceAllocationAppendOnly");
            if (entry.State == EntityState.Added)
            {
                var row = entry.Entity; var result = ApprovalEvidence<InvoiceMatchResult>(row.MatchResultId);
                var exposure = ApprovalEvidence<InvoiceMatchExposureState>(row.ExposureStateId);
                var invoice = ApprovalEvidence<SupplierInvoice>(row.SupplierInvoiceId);
                var mapping = result?.MappingVersionId is { } mappingId ? ApprovalEvidence<InvoiceMatchMappingVersion>(mappingId) : null;
                var exposureEntry = exposure is null ? null : Entry(exposure);
                var stateAdvanced = exposureEntry is not null && (exposureEntry.State == EntityState.Added ? exposure!.Sequence == 1
                    : exposureEntry.State == EntityState.Modified && exposureEntry.Property(x => x.Sequence).IsModified
                        && exposure!.Sequence == checked(exposureEntry.OriginalValues.GetValue<long>(nameof(InvoiceMatchExposureState.Sequence)) + 1));
                var batch = ChangeTracker.Entries<InvoiceMatchAllocation>().Where(x => x.State == EntityState.Added && x.Entity.ExposureStateId == row.ExposureStateId).Select(x => x.Entity).ToArray();
                if (!stateAdvanced || batch.Select(x => (x.MatchResultId,x.SupplierInvoiceId)).Distinct().Count() != 1
                    || row.Kind != "Debit" || row.OriginalDebitId != null || result is null || result.Outcome != "Compared" || result.ExecutionVersion is not (1 or 2)
                    || result.SupplierInvoiceId != row.SupplierInvoiceId || exposure is null || exposure.CoverageState != "Verified"
                    || exposure.OwnerReference != row.OwnerCoverageReference || exposure.Sequence != row.ExposureSequence
                    || exposure.OwnerValidUntil is null || exposure.OwnerValidUntil <= result.MatchedAt
                    || invoice is null || mapping is null || invoice.ActiveMatchClaimResultId != result.Id
                    || invoice.CurrentMappingVersionId != mapping.Id || mapping.SupplierInvoiceId != invoice.Id
                    || result.InvoiceOriginalSha256 != mapping.InvoiceOriginalSha256 || result.InvoiceVersion != invoice.Version
                    || result.PurchaseOrderId != row.PurchaseOrderId || result.IssuedVersionId != row.IssuedVersionId
                    || mapping.PurchaseOrderId != row.PurchaseOrderId || mapping.IssuedVersionId != row.IssuedVersionId
                    || exposure.PurchaseOrderId != row.PurchaseOrderId || exposure.IssuedVersionId != row.IssuedVersionId
                    || exposure.SupplierId != row.SupplierId || exposure.CurrencyCode != row.CurrencyCode
                    || invoice.SupplierId != row.SupplierId || invoice.CurrencyCode != row.CurrencyCode
                    || row.NetAmount < 0 || row.TaxAmount < 0 || string.IsNullOrWhiteSpace(row.UnitCode)
                    || row.Quantity <= 0 || !ExactDecimalJsonCodec.IsRepresentable(row.Quantity,18,4)
                    || !ExactDecimalJsonCodec.IsRepresentable(row.NetAmount,38,8) || !ExactDecimalJsonCodec.IsRepresentable(row.TaxAmount,38,8))
                    throw new InvalidOperationException("InvoiceAllocationAuthorityRequired");
            }
        }
        foreach (var entry in ChangeTracker.Entries<InvoiceMatchResult>())
            if (entry.State == EntityState.Added && entry.Entity.ExecutionVersion is 1 or 2)
            {
                var row = entry.Entity;
                var invoice = ApprovalEvidence<SupplierInvoice>(row.SupplierInvoiceId);
                var mapping = row.MappingVersionId is { } mappingId ? ApprovalEvidence<InvoiceMatchMappingVersion>(mappingId) : null;
                if (invoice is null || mapping is null || invoice.CurrentMappingVersionId != mapping.Id || invoice.MappingRevision != mapping.MappingRevision
                    || mapping.SupplierInvoiceId != invoice.Id || mapping.PurchaseOrderId != row.PurchaseOrderId || mapping.IssuedVersionId != row.IssuedVersionId
                    || row.InvoiceVersion != invoice.Version || row.InvoiceOriginalSha256 != mapping.InvoiceOriginalSha256
                    || row.InvoiceOriginalSha256 != InvoiceMappingEvidence.OriginalDigest(invoice, SupplierInvoiceLines.Where(x => x.SupplierInvoiceId == invoice.Id && x.IsActive).ToArray()))
                    throw new InvalidOperationException("InvoiceMatchCurrentSourceRequired");
                if (row.ActorPrincipalId != scope.SubjectId || !ProcurementPrincipalEvidence.OptionalWorker(row.ActorWorkerId)
                    || row.ExecutionVersion == 1 && row.ActorWorkerId is null
                    || row.ExecutionVersion == 2 && (!ProcurementPrincipalEvidence.Canonical(row.ActorPrincipalId) || row.ActorWorkerId != scope.CoreLinkedWorkerId) || row.CommandKey is null || row.CommandKey == Guid.Empty
                    || row.EvidenceJson is null || row.EvidenceSha256 != ProcurementEvidenceEncoding.Digest(row.EvidenceJson)
                    || row.Outcome is not ("Compared" or "Blocked")) throw new InvalidOperationException("InvoiceMatchEvidenceInvalid");
            }
        foreach (var entry in ChangeTracker.Entries<InvoiceMatchExposureState>())
        {
            if (entry.State == EntityState.Modified && entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not
                ("Sequence" or "CoverageState" or "OwnerReference" or "OwnerWatermark" or "OwnerEvidenceSha256" or "OwnerValidUntil" or "Version" or "UpdatedAt" or "UpdatedBy")))
                throw new InvalidOperationException("InvoiceExposureSourceImmutable");
            if (entry.State == EntityState.Modified && entry.Property(x => x.Sequence).IsModified)
            {
                var rows = ChangeTracker.Entries<InvoiceMatchAllocation>().Where(x => x.State == EntityState.Added && x.Entity.ExposureStateId == entry.Entity.Id).Select(x => x.Entity).ToArray();
                if (rows.Length == 0 || entry.Entity.Sequence != checked(entry.OriginalValues.GetValue<long>(nameof(InvoiceMatchExposureState.Sequence)) + 1)
                    || rows.Any(x => x.ExposureSequence != entry.Entity.Sequence)) throw new InvalidOperationException("InvoiceExposureClaimBatchRequired");
            }
        }
        foreach (var entry in ChangeTracker.Entries<SupplierInvoiceLine>())
        {
            if (entry.State == EntityState.Modified && (!MayMaintainManualInvoice(entry.Entity.SupplierInvoiceId)
                || entry.Properties.Any(property => property.IsModified && property.Metadata.Name is
                    (nameof(SupplierInvoiceLine.SupplierInvoiceId) or nameof(SupplierInvoiceLine.LineNumber)
                    or nameof(ProcurementRecord.TenantId) or nameof(ProcurementRecord.LegalEntityId)
                    or nameof(ProcurementRecord.CreatedAt) or nameof(ProcurementRecord.CreatedBy)))))
                throw new InvalidOperationException("OriginalInvoiceLineImmutable");
            if (entry.State == EntityState.Added && !ChangeTracker.Entries<SupplierInvoice>().Any(x => x.State == EntityState.Added
                && x.Entity.Id == entry.Entity.SupplierInvoiceId && x.Entity.TenantId == entry.Entity.TenantId && x.Entity.LegalEntityId == entry.Entity.LegalEntityId))
                if (!MayMaintainManualInvoice(entry.Entity.SupplierInvoiceId))
                    throw new InvalidOperationException("OriginalInvoiceLineSetClosed");
        }
        foreach (var entry in ChangeTracker.Entries<SupplierInvoice>())
        {
            if (entry.State != EntityState.Modified) continue;
            if (entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not
                ("MappingRevision" or "CurrentMappingVersionId" or "LastMatchResultId" or "ActiveMatchClaimResultId" or "MatchingState" or "MatchRevision" or "Version" or "UpdatedAt" or "UpdatedBy")))
                if (!MayMaintainManualInvoice(entry.Entity.Id))
                    throw new InvalidOperationException("OriginalInvoiceImmutable");
            if (entry.Property(x => x.CurrentMappingVersionId).IsModified || entry.Property(x => x.MappingRevision).IsModified)
            {
                var mapping = ChangeTracker.Entries<InvoiceMatchMappingVersion>().Where(x => x.State == EntityState.Added && x.Entity.Id == entry.Entity.CurrentMappingVersionId).Select(x => x.Entity).SingleOrDefault();
                if (mapping is null || mapping.SupplierInvoiceId != entry.Entity.Id || mapping.MappingRevision != entry.Entity.MappingRevision
                    || entry.Entity.MappingRevision != checked(entry.OriginalValues.GetValue<int>(nameof(SupplierInvoice.MappingRevision)) + 1))
                    throw new InvalidOperationException("InvoiceMappingPointerFactRequired");
            }
            if (entry.Property(x => x.LastMatchResultId).IsModified || entry.Property(x => x.MatchRevision).IsModified)
            {
                var result = ChangeTracker.Entries<InvoiceMatchResult>().Where(x => x.State == EntityState.Added && x.Entity.Id == entry.Entity.LastMatchResultId).Select(x => x.Entity).SingleOrDefault();
                if (result is null || result.SupplierInvoiceId != entry.Entity.Id || result.InvoiceVersion != entry.Entity.Version
                    || entry.Entity.MatchRevision != checked(entry.OriginalValues.GetValue<long>(nameof(SupplierInvoice.MatchRevision)) + 1))
                    throw new InvalidOperationException("InvoiceComparisonPointerFactRequired");
            }
            if (entry.Property(x => x.ActiveMatchClaimResultId).IsModified)
            {
                var previous = entry.OriginalValues.GetValue<Guid?>(nameof(SupplierInvoice.ActiveMatchClaimResultId));
                var rows = ChangeTracker.Entries<InvoiceMatchAllocation>().Where(x => x.State == EntityState.Added && x.Entity.SupplierInvoiceId == entry.Entity.Id).Select(x => x.Entity).ToArray();
                if (previous is not null || entry.Entity.ActiveMatchClaimResultId is null || rows.Length == 0
                    || rows.Any(x => x.MatchResultId != entry.Entity.ActiveMatchClaimResultId)) throw new InvalidOperationException("InvoiceClaimReleaseRequiresOwnerResolution");
            }
        }
    }
}
