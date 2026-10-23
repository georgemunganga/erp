using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<ManualPaymentRequest> ManualPaymentRequests => Set<ManualPaymentRequest>();
    public DbSet<ManualPaymentDecision> ManualPaymentDecisions => Set<ManualPaymentDecision>();
    public DbSet<ManualPaymentCommandReceipt> ManualPaymentReceipts => Set<ManualPaymentCommandReceipt>();

    private void ConfigureManualPayments(ModelBuilder model)
    {
        ConfigureLifecycle<ManualPaymentRequest>(model, "manual_payment_requests");
        ConfigureLifecycle<ManualPaymentDecision>(model, "manual_payment_decisions");
        ConfigureBase<ManualPaymentCommandReceipt>(model, "manual_payment_receipts");
        var request = model.Entity<ManualPaymentRequest>();
        var decision = model.Entity<ManualPaymentDecision>();
        var receipt = model.Entity<ManualPaymentCommandReceipt>();
        // Optional employee aliases are retained UUID evidence, never foreign keys to HR.
        foreach (var entity in new[] { request.Metadata, decision.Metadata, receipt.Metadata })
            foreach (var property in entity.GetProperties().Where(p => p.Name.EndsWith("PrincipalId", StringComparison.Ordinal)))
                property.SetMaxLength(36);
        request.Property(x => x.Purpose).HasMaxLength(1000);
        request.Property(x => x.ClosureReason).HasMaxLength(1000);
        request.Property(x => x.ExternalMethod).HasMaxLength(100);
        request.Property(x => x.ExternalReference).HasMaxLength(200);
        decision.Property(x => x.Action).HasMaxLength(20);
        decision.Property(x => x.FromStatus).HasMaxLength(40);
        decision.Property(x => x.ToStatus).HasMaxLength(40);
        decision.Property(x => x.Reason).HasMaxLength(1000);
        decision.Property(x => x.ExternalMethod).HasMaxLength(100);
        decision.Property(x => x.ExternalReference).HasMaxLength(200);
        receipt.Property(x => x.Action).HasMaxLength(20);
        receipt.Property(x => x.RequestSha256).HasMaxLength(64);
        receipt.Property(x => x.ResponseJson).HasColumnType("text"); // Exact replay bytes, not normalized JSONB.
        request.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.BranchId })
            .HasName("ak_manual_payment_request_branch");
        decision.HasOne<ManualPaymentRequest>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ManualPaymentRequestId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.BranchId }).OnDelete(DeleteBehavior.Restrict);
        receipt.HasOne<ManualPaymentRequest>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ManualPaymentRequestId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.BranchId }).OnDelete(DeleteBehavior.Restrict);
        Link<ManualPaymentRequest, Supplier>(model, nameof(ManualPaymentRequest.SupplierId));
        Link<ManualPaymentRequest, SupplierInvoice>(model, nameof(ManualPaymentRequest.SupplierInvoiceId));
        request.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.BranchId, x.Status });
        request.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierInvoiceId });
        request.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CreatorPrincipalId, x.CreatedAt });
        decision.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.ManualPaymentRequestId, x.ToVersion })
            .HasName("ak_manual_payment_transition_version");
        receipt.HasOne<ManualPaymentDecision>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ManualPaymentRequestId, x.ResultVersion })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.ManualPaymentRequestId, x.ToVersion }).OnDelete(DeleteBehavior.Restrict);
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ActorPrincipalId, x.CommandKey })
            .IsUnique().HasDatabaseName("ux_manual_payment_actor_command");
        // No record is made visible through a mismatching selected branch. Named API reads
        // must also intersect with current Core branch grants before paging/counting.
        request.HasQueryFilter(x => x.TenantId == CurrentTenantId && x.LegalEntityId == CurrentLegalEntityId
            && (scope.WorkLocationId == null || x.BranchId == scope.WorkLocationId)
            && (!scope.IsConfined || scope.WorkLocationId != null) && scope.OrgUnitId == null);
        decision.HasQueryFilter(x => x.TenantId == CurrentTenantId && x.LegalEntityId == CurrentLegalEntityId
            && (scope.WorkLocationId == null || x.BranchId == scope.WorkLocationId)
            && (!scope.IsConfined || scope.WorkLocationId != null) && scope.OrgUnitId == null);
        receipt.HasQueryFilter(x => x.TenantId == CurrentTenantId && x.LegalEntityId == CurrentLegalEntityId
            && (scope.WorkLocationId == null || x.BranchId == scope.WorkLocationId)
            && (!scope.IsConfined || scope.WorkLocationId != null) && scope.OrgUnitId == null);
        const string nonempty = "'00000000-0000-0000-0000-000000000000'::uuid";
        const string actions = "('Create','Revise','Submit','Approve','Return','Reject','Record','Confirm','Cancel','Dispute')";
        var principals = new[] { "creator_principal_id", "approver_principal_id", "recorder_principal_id", "confirmer_principal_id" };
        var aliases = new[] { "creator_core_linked_worker_id", "approver_core_linked_worker_id", "recorder_core_linked_worker_id",
            "confirmer_core_linked_worker_id", "approved_by_core_linked_worker_id", "recorded_by_core_linked_worker_id", "confirmed_by_core_linked_worker_id" };
        var identity = string.Join(" AND ", principals.Select(CanonicalPrincipal)) + " AND "
            + string.Join(" AND ", aliases.Select(name => $"({name} IS NULL OR {name} <> {nonempty})"))
            + " AND creator_principal_id <> approver_principal_id AND recorder_principal_id <> confirmer_principal_id"
            + " AND (creator_core_linked_worker_id IS NULL OR approver_core_linked_worker_id IS NULL OR creator_core_linked_worker_id <> approver_core_linked_worker_id)"
            + " AND (recorder_core_linked_worker_id IS NULL OR confirmer_core_linked_worker_id IS NULL OR recorder_core_linked_worker_id <> confirmer_core_linked_worker_id)";
        request.ToTable(t => {
            t.HasCheckConstraint("ck_manual_payment_identity", identity);
            t.HasCheckConstraint("ck_manual_payment_values", $"id <> {nonempty} AND branch_id <> {nonempty} AND amount > 0 AND version > 0 AND currency_code ~ '^[A-Z]{{3}}$' "
                + "AND status IN ('Draft','PendingApproval','Approved','PaymentRecorded','ConfirmedManually','Rejected','Cancelled','Disputed')");
            t.HasCheckConstraint("ck_manual_payment_approval", "(approved_amount IS NULL AND approved_by_principal_id IS NULL AND approved_at IS NULL AND approved_by_core_linked_worker_id IS NULL) OR "
                + "(approved_amount IS NOT NULL AND approved_by_principal_id IS NOT NULL AND approved_amount = amount AND approved_by_principal_id = approver_principal_id AND approved_at IS NOT NULL AND approved_at >= created_at)");
            t.HasCheckConstraint("ck_manual_payment_recording", "(recorded_amount IS NULL AND recorded_by_principal_id IS NULL AND recorded_at IS NULL AND recorded_by_core_linked_worker_id IS NULL AND paid_on IS NULL AND external_method IS NULL AND external_reference IS NULL) OR "
                + "(recorded_amount IS NOT NULL AND recorded_by_principal_id IS NOT NULL AND external_method IS NOT NULL AND external_reference IS NOT NULL AND recorded_amount = amount AND recorded_by_principal_id = recorder_principal_id AND recorded_at IS NOT NULL AND approved_at IS NOT NULL AND recorded_at >= approved_at "
                + "AND paid_on IS NOT NULL AND length(btrim(external_method)) > 0 AND length(btrim(external_reference)) > 0)");
            t.HasCheckConstraint("ck_manual_payment_confirmation", "(confirmed_by_principal_id IS NULL AND confirmed_at IS NULL AND confirmed_by_core_linked_worker_id IS NULL) OR "
                + "(confirmed_by_principal_id IS NOT NULL AND confirmed_by_principal_id = confirmer_principal_id AND confirmed_at IS NOT NULL AND recorded_at IS NOT NULL AND confirmed_at >= recorded_at)");
            t.HasCheckConstraint("ck_manual_payment_state_evidence", "(status NOT IN ('Approved','PaymentRecorded','ConfirmedManually','Disputed') OR approved_at IS NOT NULL) "
                + "AND (status NOT IN ('PaymentRecorded','ConfirmedManually','Disputed') OR recorded_at IS NOT NULL) "
                + "AND ((status = 'ConfirmedManually') = (confirmed_at IS NOT NULL)) "
                + "AND (status IN ('PaymentRecorded','ConfirmedManually','Disputed') OR recorded_at IS NULL) "
                + "AND (status NOT IN ('Draft','PendingApproval','Rejected') OR approved_at IS NULL) "
                + "AND (status NOT IN ('Rejected','Cancelled','Disputed') OR (closure_reason IS NOT NULL AND length(btrim(closure_reason)) > 0))");
        });
        decision.ToTable(t => t.HasCheckConstraint("ck_manual_payment_transition", $"branch_id <> {nonempty} AND version = 1 AND from_version >= 0 AND to_version = from_version + 1 "
            + $"AND action IN {actions} AND {CanonicalPrincipal("actor_principal_id")} "
            + $"AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> {nonempty})"));
        decision.ToTable(t => t.HasCheckConstraint("ck_manual_payment_action_states",
            "(action = 'Create' AND from_status = 'None' AND to_status = 'Draft' AND from_version = 0) OR "
            + "(from_version > 0 AND ((action = 'Revise' AND from_status = 'Draft' AND to_status = 'Draft') OR "
            + "(action = 'Submit' AND from_status = 'Draft' AND to_status = 'PendingApproval') OR "
            + "(action = 'Approve' AND from_status = 'PendingApproval' AND to_status = 'Approved') OR "
            + "(action = 'Return' AND from_status = 'PendingApproval' AND to_status = 'Draft') OR "
            + "(action = 'Reject' AND from_status = 'PendingApproval' AND to_status = 'Rejected') OR "
            + "(action = 'Record' AND from_status = 'Approved' AND to_status = 'PaymentRecorded') OR "
            + "(action = 'Confirm' AND from_status = 'PaymentRecorded' AND to_status = 'ConfirmedManually') OR "
            + "(action = 'Cancel' AND from_status IN ('Draft','PendingApproval','Approved') AND to_status = 'Cancelled') OR "
            + "(action = 'Dispute' AND from_status = 'PaymentRecorded' AND to_status = 'Disputed')))"));
        receipt.ToTable(t => t.HasCheckConstraint("ck_manual_payment_receipt", $"branch_id <> {nonempty} AND command_key <> {nonempty} AND version = 1 AND result_version > 0 "
            + $"AND action IN {actions} AND {CanonicalPrincipal("actor_principal_id")} AND request_sha256 ~ '^[0-9a-f]{{64}}$' "
            + "AND jsonb_typeof(response_json::jsonb) = 'object'"));
    }

    private static string CanonicalPrincipal(string column) =>
        $"{column} ~ '^[0-9a-f]{{8}}-[0-9a-f]{{4}}-[0-9a-f]{{4}}-[0-9a-f]{{4}}-[0-9a-f]{{12}}$' AND {column} <> '00000000-0000-0000-0000-000000000000'";
}
