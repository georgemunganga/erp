using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<PurchaseOrderFinanceEvidence> OrderFinanceEvidence => Set<PurchaseOrderFinanceEvidence>();
    public DbSet<PurchaseOrderSubmission> OrderSubmissions => Set<PurchaseOrderSubmission>();
    public DbSet<PurchaseOrderAssignmentAuthorityEvidence> OrderAssignmentAuthority => Set<PurchaseOrderAssignmentAuthorityEvidence>();
    public DbSet<PurchaseOrderIssuedVersion> OrderIssuedVersions => Set<PurchaseOrderIssuedVersion>();
    public DbSet<PurchaseOrderFinanceResolution> OrderFinanceResolutions => Set<PurchaseOrderFinanceResolution>();
    public DbSet<PurchaseOrderDeliveryEvidence> OrderDeliveryEvidence => Set<PurchaseOrderDeliveryEvidence>();
    public DbSet<ProcurementCommercialCommandReceipt> CommercialCommandReceipts => Set<ProcurementCommercialCommandReceipt>();

    private void ConfigurePurchaseOrderExecution(ModelBuilder model)
    {
        ConfigureBase<PurchaseOrderFinanceEvidence>(model, "purchase_order_finance_evidence");
        ConfigureBase<PurchaseOrderFinanceResolution>(model, "purchase_order_finance_resolutions");
        ConfigureBase<PurchaseOrderSubmission>(model, "purchase_order_submissions");
        ConfigureBase<PurchaseOrderAssignmentAuthorityEvidence>(model, "purchase_order_assignment_authority");
        ConfigureBase<PurchaseOrderIssuedVersion>(model, "purchase_order_issued_versions");
        ConfigureBase<PurchaseOrderDeliveryEvidence>(model, "purchase_order_delivery_evidence");
        ConfigureBase<ProcurementCommercialCommandReceipt>(model, "commercial_command_receipts");
        model.Entity<PurchaseOrder>().Property(x => x.ReturnReason).HasMaxLength(2000);
        model.Entity<ProcurementMethod>().Property(x => x.ExecutionRoute).HasMaxLength(50).HasDefaultValue("Unconfigured");
        model.Entity<ProcurementMethod>().ToTable(t => t.HasCheckConstraint("ck_procurement_method_execution_route",
            "execution_route IN ('Unconfigured','DirectPurchase','CompetitiveSourcing','ContractRelease')"));
        model.Entity<PurchaseRequestAllocation>().Property(x => x.EstimatedValue).HasPrecision(38, 8);
        var finance = model.Entity<PurchaseOrderFinanceEvidence>();
        finance.Property(x => x.CommitmentReference).HasMaxLength(200);
        finance.Property(x => x.EvidenceReference).HasMaxLength(500);
        finance.Property(x => x.EvidenceMode).HasMaxLength(40);
        finance.Property(x => x.ApprovedManualPolicyReference).HasMaxLength(500);
        finance.Property(x => x.CurrencyCode).HasMaxLength(3);
        finance.Property(x => x.CommercialSnapshotSha256).HasMaxLength(64);
        finance.Property(x => x.ConfirmedGrossAmount).HasPrecision(38, 8);
        finance.Property(x => x.VerifiedByPrincipalId).HasMaxLength(200);
        finance.Property(x => x.Status).HasMaxLength(40);
        finance.Property(x => x.SourceReservationsJson).HasColumnType("text");
        finance.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.EvidenceReference }).IsUnique();
        finance.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId, x.CommercialRevision, x.CreatedAt });
        Link<PurchaseOrderFinanceEvidence, PurchaseOrder>(model, nameof(PurchaseOrderFinanceEvidence.PurchaseOrderId));
        Link<PurchaseOrderFinanceEvidence, Supplier>(model, nameof(PurchaseOrderFinanceEvidence.SupplierId));
        finance.ToTable(t => t.HasCheckConstraint("ck_order_finance_evidence",
            "commercial_revision > 0 AND status IN ('Confirmed','Revoked') AND confirmed_gross_amount >= 0 " +
            "AND currency_code ~ '^[A-Z]{3}$' AND length(btrim(commitment_reference)) > 0 " +
            "AND length(btrim(evidence_reference)) > 0 AND evidence_mode = 'ApprovedManualEvidence' " +
            "AND length(btrim(approved_manual_policy_reference)) > 0 AND length(btrim(verified_by_principal_id)) > 0 AND verified_by_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid"));
        var submission = model.Entity<PurchaseOrderSubmission>();
        submission.Property(x => x.CommercialSnapshotJson).HasColumnType("text");
        submission.Property(x => x.ParticipantWorkersJson).HasColumnType("text");
        submission.Property(x => x.WorkerIdentitySha256).HasMaxLength(64);
        submission.Property(x => x.SourceEvidenceJson).HasColumnType("text");
        submission.Property(x => x.PolicyDecisionJson).HasColumnType("text");
        submission.Property(x => x.CommercialSnapshotSha256).HasMaxLength(64);
        submission.Property(x => x.PolicyRulesSha256).HasMaxLength(64);
        submission.Property(x => x.BuyerPrincipalId).HasMaxLength(200);
        submission.Property(x => x.AmountBasis).HasMaxLength(40);
        submission.Property(x => x.CurrencyCode).HasMaxLength(3);
        submission.Property(x => x.LineBaseAmount).HasPrecision(38, 8);
        submission.Property(x => x.GrossAmount).HasPrecision(38, 8);
        submission.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId, x.CommercialRevision }).IsUnique();
        submission.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreApprovalCaseId }).IsUnique();
        Link<PurchaseOrderSubmission, PurchaseOrder>(model, nameof(PurchaseOrderSubmission.PurchaseOrderId));
        submission.HasOne<PolicyVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyVersionNumber })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PolicyVersionNumber }).OnDelete(DeleteBehavior.Restrict);
        submission.HasOne<PurchaseOrderFinanceEvidence>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.FinanceEvidenceId, x.PurchaseOrderId, x.CommercialRevision, x.CurrencyCode, x.CommercialSnapshotSha256 })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId, x.CommercialRevision, x.CurrencyCode, x.CommercialSnapshotSha256 })
            .OnDelete(DeleteBehavior.Restrict);
        submission.ToTable(t => t.HasCheckConstraint("ck_order_submission_evidence",
            "commercial_revision > 0 AND submitted_row_version > 0 AND amount_basis = 'ApprovedLineBase' " +
            "AND line_base_amount >= 0 AND gross_amount >= line_base_amount " +
            "AND buyer_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND worker_identity_sha256 ~ '^[0-9a-f]{64}$' AND commercial_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 ~ '^[0-9a-f]{64}$'"));
        var authority = model.Entity<PurchaseOrderAssignmentAuthorityEvidence>();
        authority.Property(x => x.PrincipalId).HasMaxLength(200);
        authority.Property(x => x.AssigneeKind).HasMaxLength(50);
        authority.Property(x => x.AuthorityEvidenceJson).HasColumnType("text");
        authority.Property(x => x.AuthorityEvidenceSha256).HasMaxLength(64);
        authority.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreAssignmentId }).IsUnique();
        authority.HasOne<PurchaseOrderSubmission>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId, x.CoreApprovalCaseId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.CoreApprovalCaseId }).OnDelete(DeleteBehavior.Restrict);
        authority.HasOne<ProcurementSpendingAuthorityGrant>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GrantId, x.PrincipalId, x.AuthorityVersion })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PrincipalId, x.AuthorityVersion }).OnDelete(DeleteBehavior.Restrict);
        authority.ToTable(t => t.HasCheckConstraint("ck_po_assignment_identity",
            "core_stage_id <> '00000000-0000-0000-0000-000000000000'::uuid " +
            "AND core_assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid " +
            "AND assignee_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid " +
            "AND grant_revision > 0 AND authority_version > 0 AND assignee_kind = 'ProcurementManager' " +
            "AND authority_evidence_sha256 ~ '^[0-9a-f]{64}$'"));
        var issued = model.Entity<PurchaseOrderIssuedVersion>();
        issued.Property(x => x.ParticipantWorkersJson).HasColumnType("text");
        issued.Property(x => x.CommercialSnapshotJson).HasColumnType("text");
        issued.Property(x => x.CommercialSnapshotSha256).HasMaxLength(64);
        issued.Property(x => x.IssueEffectKey).HasMaxLength(150);
        issued.Property(x => x.FinanceCommitmentReference).HasMaxLength(200);
        issued.Property(x => x.IssuedByPrincipalId).HasMaxLength(200);
        issued.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PurchaseOrderId, x.RevisionNumber }).IsUnique();
        issued.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId }).IsUnique();
        issued.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.IssueEffectKey }).IsUnique();
        issued.HasOne<PurchaseOrderSubmission>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId, x.PurchaseOrderId, x.RevisionNumber })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId, x.CommercialRevision })
            .OnDelete(DeleteBehavior.Restrict);
        var delivery = model.Entity<PurchaseOrderDeliveryEvidence>();
        delivery.Property(x => x.DeliveryReference).HasMaxLength(200);
        delivery.Property(x => x.Outcome).HasMaxLength(30);
        delivery.Property(x => x.Channel).HasMaxLength(50);
        delivery.Property(x => x.EvidenceReference).HasMaxLength(500);
        delivery.Property(x => x.VerifiedByPrincipalId).HasMaxLength(200);
        delivery.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.IssuedVersionId, x.DeliveryReference }).IsUnique();
        Link<PurchaseOrderDeliveryEvidence, PurchaseOrderIssuedVersion>(model, nameof(PurchaseOrderDeliveryEvidence.IssuedVersionId));
        var resolution = model.Entity<PurchaseOrderFinanceResolution>();
        resolution.Property(x => x.CommitmentReference).HasMaxLength(200);
        resolution.Property(x => x.EvidenceReference).HasMaxLength(500);
        resolution.Property(x => x.ApprovedManualPolicyReference).HasMaxLength(500);
        resolution.Property(x => x.SourceReservationsJson).HasColumnType("text");
        resolution.Property(x => x.ReleasedGrossAmount).HasPrecision(38, 8);
        resolution.Property(x => x.CurrencyCode).HasMaxLength(3);
        resolution.Property(x => x.Outcome).HasMaxLength(50);
        resolution.Property(x => x.Reason).HasMaxLength(2000);
        resolution.Property(x => x.VerifiedByPrincipalId).HasMaxLength(200);
        resolution.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId }).IsUnique();
        resolution.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.EvidenceReference }).IsUnique();
        resolution.HasOne<PurchaseOrderSubmission>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId, x.PurchaseOrderId, x.FinanceEvidenceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId, x.FinanceEvidenceId }).OnDelete(DeleteBehavior.Restrict);
        var receipt = model.Entity<ProcurementCommercialCommandReceipt>();
        receipt.Property(x => x.Operation).HasMaxLength(40);
        receipt.Property(x => x.ActorPrincipalId).HasMaxLength(200);
        receipt.Property(x => x.Fingerprint).HasMaxLength(64);
        receipt.Property(x => x.ResponseJson).HasColumnType("text");
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Operation, x.CommandKey }).IsUnique();
        Link<ProcurementCommercialCommandReceipt, PurchaseOrder>(model, nameof(ProcurementCommercialCommandReceipt.PurchaseOrderId));
    }

    private void GuardPurchaseOrderExecution()
    {
        foreach (var entry in ChangeTracker.Entries().Where(x => x.Entity is PurchaseOrderFinanceEvidence
            or PurchaseOrderSubmission or PurchaseOrderAssignmentAuthorityEvidence or PurchaseOrderIssuedVersion
            or PurchaseOrderDeliveryEvidence or PurchaseOrderFinanceResolution or ProcurementCommercialCommandReceipt))
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Purchase-order commercial authority and dispatch evidence are append-only.");
            if (entry.State != EntityState.Added) continue;
            if (entry.Entity is ProcurementCommercialCommandReceipt receipt && (receipt.ActorPrincipalId != scope.SubjectId
                || receipt.CommandKey == Guid.Empty || receipt.Fingerprint.Length != 64 || receipt.CompletedAt == default))
                throw new InvalidOperationException("OrderCommandReceiptActorInvalid");
            if (entry.Entity is PurchaseOrderAssignmentAuthorityEvidence assignment
                && Mightyfin.Erp.Procurement.Application.PurchaseOrderAssignmentIdentity.ReadVerified(assignment) is null)
                throw new InvalidOperationException("OrderAssignmentWorkerEvidenceInvalid");
            if (entry.Entity is PurchaseOrderFinanceResolution release && (release.VerifiedByPrincipalId != scope.SubjectId
                || release.VerifiedByWorkerId == Guid.Empty || release.ConfirmedAt == default))
                throw new InvalidOperationException("OrderFinanceReleaseActorInvalid");
            if (entry.Entity is PurchaseOrderFinanceEvidence finance && (finance.VerifiedByPrincipalId != scope.SubjectId || finance.VerifiedByWorkerId == Guid.Empty))
                throw new InvalidOperationException("OrderFinanceVerifierActorInvalid");
            if (entry.Entity is PurchaseOrderIssuedVersion issued && issued.IssuedByPrincipalId != scope.SubjectId)
                throw new InvalidOperationException("OrderIssueActorInvalid");
            if (entry.Entity is PurchaseOrderDeliveryEvidence delivery && (delivery.VerifiedByPrincipalId != scope.SubjectId || delivery.VerifiedByWorkerId == Guid.Empty))
                throw new InvalidOperationException("OrderDeliveryActorInvalid");
        }
    }
}
