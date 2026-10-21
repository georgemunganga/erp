using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<PurchaseOrderCommitmentOwnership> OrderCommitmentOwnership => Set<PurchaseOrderCommitmentOwnership>();
    public DbSet<PurchaseOrderFinanceEvidence> OrderFinanceEvidence => Set<PurchaseOrderFinanceEvidence>();
    public DbSet<PurchaseOrderSubmission> OrderSubmissions => Set<PurchaseOrderSubmission>();
    public DbSet<PurchaseOrderAssignmentAuthorityEvidence> OrderAssignmentAuthority => Set<PurchaseOrderAssignmentAuthorityEvidence>();
    public DbSet<PurchaseOrderIssuedVersion> OrderIssuedVersions => Set<PurchaseOrderIssuedVersion>();
    public DbSet<PurchaseOrderFinanceResolution> OrderFinanceResolutions => Set<PurchaseOrderFinanceResolution>();
    public DbSet<PurchaseOrderDeliveryEvidence> OrderDeliveryEvidence => Set<PurchaseOrderDeliveryEvidence>();
    public DbSet<ProcurementCommercialCommandReceipt> CommercialCommandReceipts => Set<ProcurementCommercialCommandReceipt>();

    private void ConfigurePurchaseOrderExecution(ModelBuilder model)
    {
        ConfigureBase<PurchaseOrderCommitmentOwnership>(model, "purchase_order_commitment_ownership");
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
        var ownership = model.Entity<PurchaseOrderCommitmentOwnership>();
        ownership.Property(x => x.CommitmentReference).HasMaxLength(200);
        ownership.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CommitmentReference }).IsUnique();
        ownership.ToTable(t => t.HasCheckConstraint("ck_po_commitment_reference", "length(btrim(commitment_reference)) > 0"));
        Link<PurchaseOrderCommitmentOwnership, PurchaseOrder>(model, nameof(PurchaseOrderCommitmentOwnership.PurchaseOrderId));
        var finance = model.Entity<PurchaseOrderFinanceEvidence>();
        finance.Property(x => x.CommercialSnapshotJson).HasColumnType("text");
        finance.Property(x => x.ParticipantWorkersJson).HasColumnType("text");
        finance.Property(x => x.WorkerIdentitySha256).HasMaxLength(64);
        finance.HasOne<PurchaseOrderCommitmentOwnership>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.CommitmentReference, x.PurchaseOrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.CommitmentReference, x.PurchaseOrderId }).OnDelete(DeleteBehavior.Restrict);
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
            "AND currency_code ~ '^[A-Z]{3}$' AND buyer_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid " +
            "AND worker_identity_sha256 ~ '^[0-9a-f]{64}$' AND length(btrim(commitment_reference)) > 0 " +
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
        resolution.ToTable(t => t.HasCheckConstraint("ck_po_finance_resolution",
            "released_gross_amount >= 0 AND currency_code ~ '^[A-Z]{3}$' " +
            "AND verified_by_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid " +
            "AND length(btrim(evidence_reference)) > 0 AND length(btrim(approved_manual_policy_reference)) > 0 " +
            "AND ((outcome = 'CancelledUnsubmittedOrder' AND submission_id IS NULL) " +
            "OR (outcome IN ('ReopenedDraft','CancelledRejectedOrder') AND submission_id IS NOT NULL))"));
        resolution.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId }).IsUnique();
        resolution.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.EvidenceReference }).IsUnique();
        resolution.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.FinanceEvidenceId }).IsUnique();
        resolution.HasOne<PurchaseOrderFinanceEvidence>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.FinanceEvidenceId, x.PurchaseOrderId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseOrderId }).OnDelete(DeleteBehavior.Restrict);
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
            or PurchaseOrderDeliveryEvidence or PurchaseOrderFinanceResolution or PurchaseOrderCommitmentOwnership or ProcurementCommercialCommandReceipt).ToArray())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Purchase-order commercial authority and dispatch evidence are append-only.");
            if (entry.State != EntityState.Added) continue;
            if (entry.Entity is ProcurementCommercialCommandReceipt receipt && (receipt.ActorPrincipalId != scope.SubjectId
                || receipt.CommandKey == Guid.Empty || receipt.Fingerprint.Length != 64 || receipt.CompletedAt == default
                || receipt.Operation is not ("Convert" or "SourceEdit" or "Finance" or "Submit" or "Decide" or "Issue"
                    or "Delivery" or "FinanceRelease" or "DraftFinanceRelease" or "CancelDraft"
                    or "ReceivingDraft" or "ReceivingRecord" or "ReceivingInspect" or "ReceivingAccept" or "ServiceDraft" or "ServiceCertify" or "ReceivingCancel" or "ServiceCancel")))
                throw new InvalidOperationException("OrderCommandReceiptActorInvalid");
            if (entry.Entity is PurchaseOrderSubmission submitted)
            {
                var order = ApprovalEvidence<PurchaseOrder>(submitted.PurchaseOrderId);
                var fundingPin = ApprovalEvidence<PurchaseOrderFinanceEvidence>(submitted.FinanceEvidenceId);
                if (order is null || fundingPin is null || submitted.BuyerPrincipalId != scope.SubjectId
                    || submitted.BuyerPrincipalId != order.BuyerSubjectId || submitted.BuyerWorkerId == Guid.Empty
                    || submitted.CoreApprovalCaseId == Guid.Empty || submitted.CommercialRevision != order.CurrentRevision
                    || submitted.CurrencyCode != order.CurrencyCode || submitted.AmountBasis != "ApprovedLineBase"
                    || submitted.CommercialSnapshotSha256 != ProcurementEvidenceEncoding.Digest(submitted.CommercialSnapshotJson)
                    || submitted.WorkerIdentitySha256 != PurchaseOrderWorkerIdentity.Digest(order, submitted.CommercialRevision,
                        submitted.CoreApprovalCaseId, submitted.BuyerWorkerId, submitted.ParticipantWorkersJson)
                    || fundingPin.PurchaseOrderId != order.Id || fundingPin.CommercialRevision != submitted.CommercialRevision
                    || fundingPin.CurrencyCode != submitted.CurrencyCode || fundingPin.ConfirmedGrossAmount != submitted.GrossAmount
                    || fundingPin.CommercialSnapshotSha256 != submitted.CommercialSnapshotSha256
                    || !PurchaseOrderWorkerIdentity.IsIndependent(submitted.ParticipantWorkersJson, fundingPin.VerifiedByWorkerId))
                    throw new InvalidOperationException("OrderSubmissionEvidenceInvalid");
            }
            if (entry.Entity is PurchaseOrderAssignmentAuthorityEvidence assignment) GuardOrderAssignment(assignment);
            if (entry.Entity is PurchaseOrderFinanceResolution release && (release.VerifiedByPrincipalId != scope.SubjectId
                || release.VerifiedByWorkerId == Guid.Empty || release.ConfirmedAt == default))
                throw new InvalidOperationException("OrderFinanceReleaseActorInvalid");
            if (entry.Entity is PurchaseOrderFinanceEvidence finance)
            {
                var fundedOrder = ApprovalEvidence<PurchaseOrder>(finance.PurchaseOrderId);
                if (finance.VerifiedByPrincipalId != scope.SubjectId || finance.VerifiedByWorkerId == Guid.Empty
                    || fundedOrder is null || finance.BuyerWorkerId == Guid.Empty
                    || finance.WorkerIdentitySha256 != PurchaseOrderWorkerIdentity.Digest(fundedOrder, finance.CommercialRevision,
                        Guid.Empty, finance.BuyerWorkerId, finance.ParticipantWorkersJson)
                    || !PurchaseOrderWorkerIdentity.IsIndependent(finance.ParticipantWorkersJson, finance.VerifiedByWorkerId)
                    || finance.CommercialSnapshotSha256 != ProcurementEvidenceEncoding.Digest(finance.CommercialSnapshotJson))
                    throw new InvalidOperationException("OrderFinanceVerifierActorInvalid");
            }
            if (entry.Entity is PurchaseOrderIssuedVersion issued)
            {
                var issuedPin = ApprovalEvidence<PurchaseOrderSubmission>(issued.SubmissionId);
                if (issued.IssuedByPrincipalId != scope.SubjectId || issuedPin is null
                    || issued.PurchaseOrderId != issuedPin.PurchaseOrderId || issued.RevisionNumber != issuedPin.CommercialRevision
                    || issued.CommercialSnapshotJson != issuedPin.CommercialSnapshotJson
                    || issued.CommercialSnapshotSha256 != issuedPin.CommercialSnapshotSha256
                    || issued.ParticipantWorkersJson != issuedPin.ParticipantWorkersJson)
                    throw new InvalidOperationException("OrderIssueEvidenceInvalid");
            }
            if (entry.Entity is PurchaseOrderDeliveryEvidence delivery && (delivery.VerifiedByPrincipalId != scope.SubjectId || delivery.VerifiedByWorkerId == Guid.Empty))
                throw new InvalidOperationException("OrderDeliveryActorInvalid");
        }
    }
    private void GuardOrderAssignment(PurchaseOrderAssignmentAuthorityEvidence value)
    {
        var pin = ApprovalEvidence<PurchaseOrderSubmission>(value.SubmissionId);
        var order = pin is null ? null : ApprovalEvidence<PurchaseOrder>(pin.PurchaseOrderId);
        var grant = SpendingAuthorityGrants.Local.SingleOrDefault(x => x.Id == value.GrantId)
            ?? SpendingAuthorityGrants.Include(x => x.Categories).SingleOrDefault(x => x.Id == value.GrantId);
        var retained = PurchaseOrderAssignmentIdentity.ReadVerified(value);
        if (pin is null || order is null || grant is null || retained is null
            || pin.TenantId != scope.TenantId || pin.LegalEntityId != scope.LegalEntityId
            || pin.CoreApprovalCaseId != value.CoreApprovalCaseId || value.CoreStageId == Guid.Empty
            || value.CoreAssignmentId == Guid.Empty || value.AssigneeKind != "ProcurementManager"
            || !PurchaseOrderWorkerIdentity.IsIndependent(pin.ParticipantWorkersJson, value.AssigneeWorkerId)
            || pin.WorkerIdentitySha256 != PurchaseOrderWorkerIdentity.Digest(order, pin.CommercialRevision,
                pin.CoreApprovalCaseId, pin.BuyerWorkerId, pin.ParticipantWorkersJson)
            || value.GrantRevision != grant.Version || value.AuthorityVersion != grant.AuthorityVersion
            || value.PrincipalId != grant.PrincipalId || retained.Authority.EvaluatedAt != pin.SubmittedAt)
            throw new InvalidOperationException("OrderAssignmentAuthorityEvidenceInvalid");
        var lines = PurchaseOrderLines.Where(x => x.PurchaseOrderId == order.Id && x.IsActive).ToList()
            .Concat(PurchaseOrderLines.Local.Where(x => x.PurchaseOrderId == order.Id && x.IsActive))
            .DistinctBy(x => x.Id).ToArray();
        if (lines.Length == 0 || lines.Any(x => x.PurchaseRequestLineId is null)
            || PurchaseOrderCommercialWorkflow.Totals(lines).LineBase != pin.LineBaseAmount)
            throw new InvalidOperationException("OrderAssignmentSourceAmountChanged");
        var categories = new List<string>(); var departments = new List<Guid?>();
        foreach (var line in lines)
        {
            var source = ApprovalEvidence<PurchaseRequestLine>(line.PurchaseRequestLineId!.Value);
            var request = source is null ? null : ApprovalEvidence<PurchaseRequest>(source.PurchaseRequestId);
            if (source is null || request is null || source.TenantId != scope.TenantId || source.LegalEntityId != scope.LegalEntityId
                || string.IsNullOrWhiteSpace(source.CategoryCode)) throw new InvalidOperationException("OrderAssignmentSourceMissing");
            categories.Add(source.CategoryCode); departments.Add(request.DepartmentRef);
        }
        var context = new ProcurementSpendContext(order.Id, order.TenantId, order.LegalEntityId, order.CurrencyCode,
            pin.AmountBasis, pin.LineBaseAmount, order.BranchRef, departments.Distinct().ToArray(),
            categories.Distinct(StringComparer.Ordinal).ToArray());
        var evaluated = ProcurementSpendingAuthority.Evaluate(context, value.PrincipalId, "procurement-po-approve",
            "PurchaseOrder.Approve", pin.SubmittedAt, [grant]);
        if (!evaluated.Eligible || evaluated.Evidence is null
            || JsonSerializer.Serialize(evaluated.Evidence) != JsonSerializer.Serialize(retained.Authority))
            throw new InvalidOperationException("OrderAssignmentAuthorityEvidenceInvalid");
    }

}
