using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<PrincipalInvoiceState> PrincipalInvoiceStates => Set<PrincipalInvoiceState>();
    public DbSet<PrincipalInvoiceMappingVersion> PrincipalInvoiceMappingVersions => Set<PrincipalInvoiceMappingVersion>();
    public DbSet<PrincipalInvoiceMappingLine> PrincipalInvoiceMappingLines => Set<PrincipalInvoiceMappingLine>();
    public DbSet<PrincipalInvoiceUnitReviewSource> PrincipalInvoiceUnitReviewSources => Set<PrincipalInvoiceUnitReviewSource>();
    public DbSet<PrincipalInvoiceUnitReviewAssignment> PrincipalInvoiceUnitReviewAssignments => Set<PrincipalInvoiceUnitReviewAssignment>();
    public DbSet<PrincipalInvoiceUnitReviewAttestation> PrincipalInvoiceUnitReviewAttestations => Set<PrincipalInvoiceUnitReviewAttestation>();
    public DbSet<PrincipalInvoiceUnitReviewOutcome> PrincipalInvoiceUnitReviewOutcomes => Set<PrincipalInvoiceUnitReviewOutcome>();
    public DbSet<PrincipalInvoiceComparison> PrincipalInvoiceComparisons => Set<PrincipalInvoiceComparison>();
    public DbSet<PrincipalInvoiceExposureState> PrincipalInvoiceExposureStates => Set<PrincipalInvoiceExposureState>();
    public DbSet<PrincipalInvoiceClaim> PrincipalInvoiceClaims => Set<PrincipalInvoiceClaim>();
    public DbSet<PrincipalInvoiceCommandReceipt> PrincipalInvoiceCommandReceipts => Set<PrincipalInvoiceCommandReceipt>();
    public DbSet<PrincipalInvoiceDiscrepancy> PrincipalInvoiceDiscrepancies => Set<PrincipalInvoiceDiscrepancy>();
    public DbSet<PrincipalInvoiceDiscrepancyAction> PrincipalInvoiceDiscrepancyActions => Set<PrincipalInvoiceDiscrepancyAction>();

    private void ConfigurePrincipalInvoiceMatching(ModelBuilder model)
    {
        ConfigureBase<PrincipalInvoiceState>(model, "principal_invoice_states");
        ConfigureBase<PrincipalInvoiceMappingVersion>(model, "principal_invoice_mappings");
        ConfigureBase<PrincipalInvoiceMappingLine>(model, "principal_invoice_mapping_lines");
        ConfigureBase<PrincipalInvoiceUnitReviewSource>(model, "principal_invoice_unit_reviews");
        ConfigureBase<PrincipalInvoiceUnitReviewAssignment>(model, "principal_invoice_review_assignments");
        ConfigureBase<PrincipalInvoiceUnitReviewAttestation>(model, "principal_invoice_review_attestations");
        ConfigureBase<PrincipalInvoiceUnitReviewOutcome>(model, "principal_invoice_review_outcomes");
        ConfigureBase<PrincipalInvoiceComparison>(model, "principal_invoice_comparisons");
        ConfigureBase<PrincipalInvoiceExposureState>(model, "principal_invoice_exposure_states");
        ConfigureBase<PrincipalInvoiceClaim>(model, "principal_invoice_claims");
        ConfigureBase<PrincipalInvoiceCommandReceipt>(model, "principal_invoice_receipts");
        ConfigurePrincipalInvoiceDiscrepancies(model);

        const string uuid = "'00000000-0000-0000-0000-000000000000'::uuid";
        const string principal = "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$";
        const string sha = "^[0-9a-f]{64}$";
        var state = model.Entity<PrincipalInvoiceState>();
        state.Property(x => x.CurrencyCode).HasMaxLength(3);
        state.Property(x => x.State).HasMaxLength(40);
        state.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierInvoiceId }).IsUnique().HasDatabaseName("ux_pi_state_invoice");
        Link<PrincipalInvoiceState, SupplierInvoice>(model, nameof(PrincipalInvoiceState.SupplierInvoiceId));
        Link<PrincipalInvoiceState, Supplier>(model, nameof(PrincipalInvoiceState.SupplierId));
        state.ToTable(t => t.HasCheckConstraint("ck_pi_state", $"supplier_invoice_id <> {uuid} AND branch_id <> {uuid} AND supplier_id <> {uuid} AND invoice_version > 0 AND mapping_revision >= 0 AND currency_code ~ '^[A-Z]{{3}}$' AND state IN ('Unmapped','Mapped','ReviewPending','Reviewed','ComparisonBlocked','Compared') AND (mapping_revision=0)=(current_mapping_id IS NULL) AND (active_claim_comparison_id IS NULL OR last_comparison_id=active_claim_comparison_id)"));

        var mapping = model.Entity<PrincipalInvoiceMappingVersion>();
        mapping.Property(x => x.CurrencyCode).HasMaxLength(3);
        mapping.Property(x => x.MappingJson).HasColumnType("text");
        mapping.Property(x => x.MakerPrincipalId).HasMaxLength(36);
        foreach (var name in new[] { nameof(PrincipalInvoiceMappingVersion.OriginalInvoiceSha256), nameof(PrincipalInvoiceMappingVersion.IssuedSnapshotSha256), nameof(PrincipalInvoiceMappingVersion.MappingSha256) }) mapping.Property(name).HasMaxLength(64);
        mapping.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierInvoiceId, x.MappingRevision }).IsUnique().HasDatabaseName("ux_pi_map_revision");
        mapping.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId }).IsUnique().HasDatabaseName("ux_pi_map_invoice_owner");
        mapping.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId, x.PurchaseOrderId, x.PrincipalIssuedVersionId }).IsUnique().HasDatabaseName("ux_pi_map_owner");
        mapping.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId, x.PrincipalIssuedVersionId }).IsUnique().HasDatabaseName("ux_pi_map_review_owner");
        Link<PrincipalInvoiceMappingVersion, SupplierInvoice>(model, nameof(PrincipalInvoiceMappingVersion.SupplierInvoiceId));
        Link<PrincipalInvoiceMappingVersion, PurchaseOrder>(model, nameof(PrincipalInvoiceMappingVersion.PurchaseOrderId));
        mapping.HasOne<PrincipalOrderIssuedVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PrincipalIssuedVersionId, x.PurchaseOrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_mapping_exact_issued_order");
        Link<PrincipalInvoiceMappingVersion, Supplier>(model, nameof(PrincipalInvoiceMappingVersion.SupplierId));
        mapping.ToTable(t => t.HasCheckConstraint("ck_pi_mapping", $"supplier_invoice_id <> {uuid} AND purchase_order_id <> {uuid} AND principal_issued_version_id <> {uuid} AND branch_id <> {uuid} AND supplier_id <> {uuid} AND invoice_version > 0 AND mapping_revision > 0 AND currency_code ~ '^[A-Z]{{3}}$' AND original_invoice_sha256 ~ '{sha}' AND issued_snapshot_sha256 ~ '{sha}' AND mapping_sha256 ~ '{sha}' AND maker_principal_id ~ '{principal}' AND maker_principal_id <> '00000000-0000-0000-0000-000000000000' AND (maker_worker_alias_id IS NULL OR maker_worker_alias_id <> {uuid}) AND octet_length(mapping_json) BETWEEN 1 AND 262144 AND isfinite(mapped_at)"));

        var line = model.Entity<PrincipalInvoiceMappingLine>();
        line.Property(x => x.DocumentLineReference).HasMaxLength(200);
        line.Property(x => x.InvoiceUnitCode).HasMaxLength(20);
        line.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.MappingVersionId, x.SupplierInvoiceLineId }).IsUnique().HasDatabaseName("ux_pi_map_line");
        line.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.MappingVersionId, x.SupplierInvoiceLineId, x.PurchaseOrderLineId, x.SupplierInvoiceId, x.PurchaseOrderId, x.PrincipalIssuedVersionId }).IsUnique().HasDatabaseName("ux_pi_map_line_claim_owner");
        line.HasOne<PrincipalInvoiceMappingVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.MappingVersionId, x.SupplierInvoiceId, x.PurchaseOrderId, x.PrincipalIssuedVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId, x.PurchaseOrderId, x.PrincipalIssuedVersionId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_line_exact_mapping");
        line.HasOne<SupplierInvoiceLine>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierInvoiceLineId, x.SupplierInvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_line_exact_invoice_line");
        line.HasOne<PurchaseOrderLine>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderLineId, x.PurchaseOrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_line_exact_order_line");
        line.HasOne<PrincipalReceivingSourceState>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PrincipalReceivingSourceStateId, x.PurchaseOrderId, x.PrincipalIssuedVersionId, x.PurchaseOrderLineId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId, x.IssuedVersionId, x.PurchaseOrderLineId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_line_source");
        line.HasOne<PrincipalReceivingFact>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PrincipalAcceptanceFactId, x.PrincipalReceivingSourceStateId, x.AcceptanceSequence })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SourceStateId, x.Sequence }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_line_acceptance");
        line.ToTable(t => t.HasCheckConstraint("ck_pi_map_line", $"mapping_version_id <> {uuid} AND supplier_invoice_id <> {uuid} AND supplier_invoice_line_id <> {uuid} AND purchase_order_id <> {uuid} AND principal_issued_version_id <> {uuid} AND purchase_order_line_id <> {uuid} AND document_version_id <> {uuid} AND invoice_unit_code <> '' AND document_line_reference <> '' AND (principal_acceptance_fact_id IS NULL AND principal_receiving_source_state_id IS NULL AND acceptance_sequence IS NULL OR principal_acceptance_fact_id IS NOT NULL AND principal_acceptance_fact_id <> {uuid} AND principal_receiving_source_state_id IS NOT NULL AND principal_receiving_source_state_id <> {uuid} AND acceptance_sequence > 0)"));

        var review = model.Entity<PrincipalInvoiceUnitReviewSource>();
        review.Property(x => x.OriginalUnitsJson).HasColumnType("text");
        review.Property(x => x.MakerPrincipalId).HasMaxLength(36);
        foreach (var name in new[] { nameof(PrincipalInvoiceUnitReviewSource.MappingSha256), nameof(PrincipalInvoiceUnitReviewSource.OriginalInvoiceSha256), nameof(PrincipalInvoiceUnitReviewSource.OriginalUnitsSha256) }) review.Property(name).HasMaxLength(64);
        review.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.MappingVersionId }).IsUnique().HasDatabaseName("ux_pi_review_mapping");
        review.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreCaseId }).IsUnique().HasDatabaseName("ux_pi_review_case");
        review.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Id, x.CoreCaseId }).IsUnique().HasDatabaseName("ux_pi_review_case_owner");
        review.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Id, x.MappingVersionId, x.SupplierInvoiceId, x.PrincipalIssuedVersionId }).IsUnique().HasDatabaseName("ux_pi_review_source_owner");
        review.HasOne<PrincipalInvoiceMappingVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.MappingVersionId, x.SupplierInvoiceId, x.PrincipalIssuedVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId, x.PrincipalIssuedVersionId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_review_exact_mapping");
        Link<PrincipalInvoiceUnitReviewSource, PolicyVersion>(model, nameof(PrincipalInvoiceUnitReviewSource.MatchingPolicyId));
        Link<PrincipalInvoiceUnitReviewSource, PolicyVersion>(model, nameof(PrincipalInvoiceUnitReviewSource.ReviewPolicyId));
        review.ToTable(t => t.HasCheckConstraint("ck_pi_review", $"supplier_invoice_id <> {uuid} AND mapping_version_id <> {uuid} AND principal_issued_version_id <> {uuid} AND branch_id <> {uuid} AND core_case_id <> {uuid} AND matching_policy_id <> review_policy_id AND mapping_sha256 ~ '{sha}' AND original_invoice_sha256 ~ '{sha}' AND original_units_sha256 ~ '{sha}' AND maker_principal_id ~ '{principal}' AND maker_principal_id <> '00000000-0000-0000-0000-000000000000' AND (maker_worker_alias_id IS NULL OR maker_worker_alias_id <> {uuid}) AND octet_length(original_units_json) BETWEEN 1 AND 262144 AND isfinite(opened_at)"));

        var assignment = model.Entity<PrincipalInvoiceUnitReviewAssignment>();
        assignment.Property(x => x.ReviewerPrincipalId).HasMaxLength(36);
        assignment.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreAssignmentId }).IsUnique().HasDatabaseName("ux_pi_review_assignment");
        assignment.HasOne<PrincipalInvoiceUnitReviewSource>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReviewSourceId, x.CoreCaseId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.CoreCaseId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_review_assignment_exact_case");
        assignment.ToTable(t => t.HasCheckConstraint("ck_pi_assignment", $"review_source_id <> {uuid} AND core_case_id <> {uuid} AND core_assignment_id <> {uuid} AND reviewer_principal_id ~ '{principal}' AND reviewer_principal_id <> '00000000-0000-0000-0000-000000000000' AND (reviewer_worker_alias_id IS NULL OR reviewer_worker_alias_id <> {uuid})"));

        var attestation = model.Entity<PrincipalInvoiceUnitReviewAttestation>();
        attestation.Property(x => x.ReviewerPrincipalId).HasMaxLength(36);
        attestation.Property(x => x.OriginalUnitsSha256).HasMaxLength(64);
        attestation.Property(x => x.Statement).HasMaxLength(80);
        attestation.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreDecisionId }).IsUnique().HasDatabaseName("ux_pi_review_attestation_decision");
        attestation.HasOne<PrincipalInvoiceUnitReviewSource>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReviewSourceId, x.CoreCaseId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.CoreCaseId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_attestation_exact_case");
        attestation.HasOne<PrincipalInvoiceUnitReviewAssignment>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.CoreAssignmentId, x.ReviewSourceId, x.CoreCaseId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.CoreAssignmentId, x.ReviewSourceId, x.CoreCaseId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_attestation_exact_assignment");
        attestation.ToTable(t => t.HasCheckConstraint("ck_pi_attestation", $"review_source_id <> {uuid} AND core_case_id <> {uuid} AND core_assignment_id <> {uuid} AND core_decision_id <> {uuid} AND reviewer_principal_id ~ '{principal}' AND reviewer_principal_id <> '00000000-0000-0000-0000-000000000000' AND (reviewer_worker_alias_id IS NULL OR reviewer_worker_alias_id <> {uuid}) AND original_units_sha256 ~ '{sha}' AND statement = 'OriginalDocumentUnitsConfirmed' AND isfinite(attested_at)"));

        var outcome = model.Entity<PrincipalInvoiceUnitReviewOutcome>();
        outcome.Property(x => x.State).HasMaxLength(20); outcome.Property(x => x.EvidenceJson).HasColumnType("text");
        outcome.Property(x => x.EvidenceSha256).HasMaxLength(64); outcome.Property(x => x.ReviewerPrincipalId).HasMaxLength(36);
        outcome.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ReviewSourceId }).IsUnique().HasDatabaseName("ux_pi_review_outcome");
        outcome.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Id, x.ReviewSourceId }).IsUnique().HasDatabaseName("ux_pi_review_outcome_owner");
        outcome.HasOne<PrincipalInvoiceUnitReviewSource>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReviewSourceId, x.CoreCaseId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.CoreCaseId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_review_outcome_exact_case");
        outcome.ToTable(t => t.HasCheckConstraint("ck_pi_outcome", $"review_source_id <> {uuid} AND core_case_id <> {uuid} AND final_decision_id <> {uuid} AND state IN ('Verified','Returned','Rejected') AND evidence_sha256 ~ '{sha}' AND reviewer_principal_id ~ '{principal}' AND reviewer_principal_id <> '00000000-0000-0000-0000-000000000000' AND (reviewer_worker_alias_id IS NULL OR reviewer_worker_alias_id <> {uuid}) AND octet_length(evidence_json) BETWEEN 1 AND 524288 AND isfinite(completed_at)"));

        var exposure = model.Entity<PrincipalInvoiceExposureState>();
        exposure.Property(x => x.CurrencyCode).HasMaxLength(3); exposure.Property(x => x.CoverageState).HasMaxLength(20);
        exposure.Property(x => x.OwnerReference).HasMaxLength(500); exposure.Property(x => x.OwnerWatermark).HasMaxLength(500);
        exposure.Property(x => x.OwnerEvidenceSha256).HasMaxLength(64);
        exposure.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId, x.PrincipalIssuedVersionId }).IsUnique().HasDatabaseName("ux_pi_exposure_source");
        Link<PrincipalInvoiceExposureState, PurchaseOrder>(model, nameof(PrincipalInvoiceExposureState.PurchaseOrderId));
        exposure.HasOne<PrincipalOrderIssuedVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PrincipalIssuedVersionId, x.PurchaseOrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_exposure_exact_issued_order");
        Link<PrincipalInvoiceExposureState, Supplier>(model, nameof(PrincipalInvoiceExposureState.SupplierId));
        exposure.ToTable(t => t.HasCheckConstraint("ck_pi_exposure", $"purchase_order_id <> {uuid} AND principal_issued_version_id <> {uuid} AND supplier_id <> {uuid} AND branch_id <> {uuid} AND currency_code ~ '^[A-Z]{{3}}$' AND sequence >= 0 AND coverage_state IN ('Unknown','Verified','Stale') AND (coverage_state <> 'Verified' OR owner_reference IS NOT NULL AND owner_watermark IS NOT NULL AND owner_evidence_sha256 ~ '{sha}' AND owner_valid_until IS NOT NULL)"));

        var comparison = model.Entity<PrincipalInvoiceComparison>();
        comparison.Property(x => x.Outcome).HasMaxLength(20); comparison.Property(x => x.EvidenceJson).HasColumnType("text");
        comparison.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        comparison.Property(x => x.OwnerReference).HasMaxLength(500);
        comparison.Property(x => x.OwnerWatermark).HasMaxLength(500);
        foreach (var name in new[] { nameof(PrincipalInvoiceComparison.OriginalInvoiceSha256), nameof(PrincipalInvoiceComparison.PolicyRulesSha256), nameof(PrincipalInvoiceComparison.EvidenceSha256) }) comparison.Property(name).HasMaxLength(64);
        comparison.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CommandKey }).IsUnique().HasDatabaseName("ux_pi_compare_command");
        comparison.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId }).IsUnique().HasDatabaseName("ux_pi_compare_invoice_owner");
        comparison.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId, x.PurchaseOrderId, x.PrincipalIssuedVersionId }).IsUnique().HasDatabaseName("ux_pi_compare_claim_owner");
        comparison.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Id, x.MappingVersionId, x.SupplierInvoiceId, x.PurchaseOrderId, x.PrincipalIssuedVersionId }).IsUnique().HasDatabaseName("ux_pi_compare_mapping_claim_owner");
        comparison.HasOne<PrincipalInvoiceMappingVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.MappingVersionId, x.SupplierInvoiceId, x.PurchaseOrderId, x.PrincipalIssuedVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId, x.PurchaseOrderId, x.PrincipalIssuedVersionId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_compare_exact_mapping");
        comparison.HasOne<PrincipalInvoiceUnitReviewSource>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReviewSourceId, x.MappingVersionId, x.SupplierInvoiceId, x.PrincipalIssuedVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.MappingVersionId, x.SupplierInvoiceId, x.PrincipalIssuedVersionId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_compare_exact_review_source");
        comparison.HasOne<PrincipalInvoiceUnitReviewOutcome>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReviewOutcomeId, x.ReviewSourceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.ReviewSourceId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_compare_exact_review_outcome");
        comparison.HasOne<PrincipalInvoiceExposureState>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ExposureStateId, x.PurchaseOrderId, x.PrincipalIssuedVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId, x.PrincipalIssuedVersionId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_compare_exact_exposure");
        Link<PrincipalInvoiceComparison, PolicyVersion>(model, nameof(PrincipalInvoiceComparison.MatchingPolicyId));
        comparison.ToTable(t => t.HasCheckConstraint("ck_pi_comparison", $"supplier_invoice_id <> {uuid} AND mapping_version_id <> {uuid} AND purchase_order_id <> {uuid} AND principal_issued_version_id <> {uuid} AND review_source_id <> {uuid} AND review_outcome_id <> {uuid} AND matching_policy_id <> {uuid} AND exposure_state_id <> {uuid} AND invoice_version > 0 AND original_invoice_sha256 ~ '{sha}' AND policy_rules_sha256 ~ '{sha}' AND evidence_sha256 ~ '{sha}' AND outcome = 'Compared' AND owner_reference <> '' AND owner_watermark <> '' AND command_key <> {uuid} AND actor_principal_id ~ '{principal}' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id <> {uuid}) AND octet_length(evidence_json) BETWEEN 1 AND 524288 AND isfinite(compared_at)"));

        state.HasOne<PrincipalInvoiceMappingVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.CurrentMappingId, x.SupplierInvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_state_current_mapping");
        state.HasOne<PrincipalInvoiceComparison>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.LastComparisonId, x.SupplierInvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_state_last_comparison");
        state.HasOne<PrincipalInvoiceComparison>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ActiveClaimComparisonId, x.SupplierInvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_state_active_claim");

        var claim = model.Entity<PrincipalInvoiceClaim>();
        claim.Property(x => x.Quantity).HasPrecision(18, 4); claim.Property(x => x.NetAmount).HasPrecision(38, 8); claim.Property(x => x.TaxAmount).HasPrecision(38, 8);
        claim.Property(x => x.CurrencyCode).HasMaxLength(3); claim.Property(x => x.UnitCode).HasMaxLength(20);
        claim.Property(x => x.OwnerCoverageReference).HasMaxLength(500);
        claim.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ComparisonId, x.SupplierInvoiceLineId }).IsUnique().HasDatabaseName("ux_pi_claim_line");
        claim.HasOne<PrincipalInvoiceComparison>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ComparisonId, x.MappingVersionId, x.SupplierInvoiceId, x.PurchaseOrderId, x.PrincipalIssuedVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.MappingVersionId, x.SupplierInvoiceId, x.PurchaseOrderId, x.PrincipalIssuedVersionId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_claim_exact_comparison");
        claim.HasOne<PrincipalInvoiceMappingLine>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.MappingVersionId, x.SupplierInvoiceLineId, x.PurchaseOrderLineId, x.SupplierInvoiceId, x.PurchaseOrderId, x.PrincipalIssuedVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.MappingVersionId, x.SupplierInvoiceLineId, x.PurchaseOrderLineId, x.SupplierInvoiceId, x.PurchaseOrderId, x.PrincipalIssuedVersionId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_claim_exact_mapping_line");
        claim.HasOne<PrincipalInvoiceExposureState>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ExposureStateId, x.PurchaseOrderId, x.PrincipalIssuedVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId, x.PrincipalIssuedVersionId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_claim_exact_exposure");
        claim.HasOne<SupplierInvoiceLine>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierInvoiceLineId, x.SupplierInvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SupplierInvoiceId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_claim_exact_invoice_line");
        claim.HasOne<PurchaseOrderLine>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderLineId, x.PurchaseOrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_claim_exact_order_line");
        claim.HasOne<PrincipalReceivingSourceState>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PrincipalReceivingSourceStateId, x.PurchaseOrderId, x.PrincipalIssuedVersionId, x.PurchaseOrderLineId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId, x.IssuedVersionId, x.PurchaseOrderLineId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_claim_source");
        claim.HasOne<PrincipalReceivingFact>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PrincipalAcceptanceFactId, x.PrincipalReceivingSourceStateId, x.AcceptanceSequence })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SourceStateId, x.Sequence }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_claim_acceptance");
        claim.ToTable(t => t.HasCheckConstraint("ck_pi_claim", $"supplier_invoice_id <> {uuid} AND supplier_invoice_line_id <> {uuid} AND mapping_version_id <> {uuid} AND comparison_id <> {uuid} AND exposure_state_id <> {uuid} AND purchase_order_id <> {uuid} AND principal_issued_version_id <> {uuid} AND purchase_order_line_id <> {uuid} AND supplier_id <> {uuid} AND currency_code ~ '^[A-Z]{{3}}$' AND unit_code <> '' AND quantity > 0 AND net_amount >= 0 AND tax_amount >= 0 AND exposure_sequence > 0 AND owner_coverage_reference <> '' AND (principal_receiving_source_state_id IS NULL AND principal_acceptance_fact_id IS NULL AND acceptance_sequence IS NULL OR principal_receiving_source_state_id IS NOT NULL AND principal_receiving_source_state_id <> {uuid} AND principal_acceptance_fact_id IS NOT NULL AND principal_acceptance_fact_id <> {uuid} AND acceptance_sequence > 0)"));

        var receipt = model.Entity<PrincipalInvoiceCommandReceipt>();
        receipt.Property(x => x.Operation).HasMaxLength(30); receipt.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        receipt.Property(x => x.RequestSha256).HasMaxLength(64); receipt.Property(x => x.ResponseSha256).HasMaxLength(64);
        receipt.Property(x => x.ResponseJson).HasColumnType("text");
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Operation, x.CommandKey }).IsUnique().HasDatabaseName("ux_pi_receipt_command");
        Link<PrincipalInvoiceCommandReceipt, SupplierInvoice>(model, nameof(PrincipalInvoiceCommandReceipt.SupplierInvoiceId));
        receipt.ToTable(t => t.HasCheckConstraint("ck_pi_receipt", $"supplier_invoice_id <> {uuid} AND branch_id <> {uuid} AND operation IN ('Map','OpenReview','DecideReview','Compare','IssueAction') AND command_key <> {uuid} AND actor_principal_id ~ '{principal}' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id <> {uuid}) AND request_sha256 ~ '{sha}' AND response_sha256 ~ '{sha}' AND response_status BETWEEN 200 AND 299 AND octet_length(response_json) BETWEEN 1 AND 262144 AND isfinite(completed_at)"));
    }
}
