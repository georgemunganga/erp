using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private void ConfigurePrincipalInvoiceDiscrepancies(ModelBuilder model)
    {
        ConfigureBase<PrincipalInvoiceDiscrepancy>(model, "principal_invoice_discrepancies");
        ConfigureBase<PrincipalInvoiceDiscrepancyAction>(model, "principal_invoice_discrepancy_actions");
        const string uuid = "'00000000-0000-0000-0000-000000000000'::uuid";
        const string principal = "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$";
        const string sha = "^[0-9a-f]{64}$";

        var issue = model.Entity<PrincipalInvoiceDiscrepancy>();
        issue.Property(x => x.Code).HasMaxLength(100);
        issue.Property(x => x.OwnerPrincipalId).HasMaxLength(36);
        issue.Property(x => x.State).HasMaxLength(32);
        issue.Property(x => x.OriginalInvoiceSha256).HasMaxLength(64);
        issue.Property(x => x.IssueKeySha256).HasMaxLength(64);
        issue.Property(x => x.ResolutionReference).HasMaxLength(200);
        issue.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SupplierInvoiceId,
            x.MappingVersionId, x.CommandKey, x.IssueKeySha256 }).IsUnique()
            .HasDatabaseName("ux_pi_discrepancy_attempt_issue");
        issue.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Id,
            x.SupplierInvoiceId, x.MappingVersionId }).IsUnique()
            .HasDatabaseName("ux_pi_discrepancy_owner");
        issue.HasOne<PrincipalInvoiceMappingVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.MappingVersionId,
                x.SupplierInvoiceId, x.PurchaseOrderId, x.PrincipalIssuedVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.SupplierInvoiceId, x.PurchaseOrderId, x.PrincipalIssuedVersionId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_discrepancy_exact_mapping");
        issue.HasOne<SupplierInvoiceLine>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierInvoiceLineId,
                x.SupplierInvoiceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.SupplierInvoiceId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_discrepancy_invoice_line");
        issue.ToTable(t => t.HasCheckConstraint("ck_pi_discrepancy", $"supplier_invoice_id <> {uuid} AND mapping_version_id <> {uuid} AND purchase_order_id <> {uuid} AND principal_issued_version_id <> {uuid} AND branch_id <> {uuid} AND command_key <> {uuid} AND (supplier_invoice_line_id IS NULL OR supplier_invoice_line_id <> {uuid}) AND original_invoice_sha256 ~ '{sha}' AND issue_key_sha256 ~ '{sha}' AND code <> '' AND owner_principal_id ~ '{principal}' AND owner_principal_id <> '00000000-0000-0000-0000-000000000000' AND (owner_worker_alias_id IS NULL OR owner_worker_alias_id <> {uuid}) AND state IN ('Open','InReview','AwaitingCorrection','ReadyToRetry','Resolved','CancelledWithReason') AND isfinite(opened_at) AND (due_at IS NULL OR isfinite(due_at)) AND (resolved_at IS NULL OR isfinite(resolved_at)) AND (state IN ('Resolved','CancelledWithReason'))=(resolved_at IS NOT NULL)"));

        var action = model.Entity<PrincipalInvoiceDiscrepancyAction>();
        action.Property(x => x.Action).HasMaxLength(40);
        action.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        action.Property(x => x.Reason).HasMaxLength(2000);
        action.Property(x => x.EvidenceReference).HasMaxLength(200);
        action.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.DiscrepancyId, x.CommandKey }).IsUnique()
            .HasDatabaseName("ux_pi_discrepancy_action_command");
        action.HasOne<PrincipalInvoiceDiscrepancy>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.DiscrepancyId,
                x.SupplierInvoiceId, x.MappingVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.SupplierInvoiceId, x.MappingVersionId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_discrepancy_action_exact_issue");
        action.HasOne<PrincipalInvoiceComparison>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ResolvingComparisonId,
                x.SupplierInvoiceId, x.MappingVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.SupplierInvoiceId, x.MappingVersionId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pi_discrepancy_action_exact_comparison");
        action.ToTable(t => t.HasCheckConstraint("ck_pi_discrepancy_action", $"discrepancy_id <> {uuid} AND supplier_invoice_id <> {uuid} AND mapping_version_id <> {uuid} AND command_key <> {uuid} AND (resolving_comparison_id IS NULL OR resolving_comparison_id <> {uuid}) AND action IN ('Opened','Assigned','Commented','CorrectionRequested','ReadyToRetry','Resolved','CancelledWithReason') AND actor_principal_id ~ '{principal}' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id <> {uuid}) AND reason <> '' AND isfinite(at)"));
    }
}
