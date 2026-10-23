using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<ManualPaymentWorkflowCaseLink> ManualPaymentWorkflowCases => Set<ManualPaymentWorkflowCaseLink>();
    public DbSet<ManualPaymentCoreDecisionLink> ManualPaymentCoreDecisions => Set<ManualPaymentCoreDecisionLink>();
    public DbSet<ManualPaymentBusinessDateObservation> ManualPaymentDateObservations => Set<ManualPaymentBusinessDateObservation>();

    private void ConfigureManualPaymentGovernance(ModelBuilder model)
    {
        ConfigureBase<ManualPaymentWorkflowCaseLink>(model, "manual_payment_workflow_cases");
        ConfigureBase<ManualPaymentCoreDecisionLink>(model, "manual_payment_core_decisions");
        ConfigureBase<ManualPaymentBusinessDateObservation>(model, "manual_payment_date_observations");
        var cases = model.Entity<ManualPaymentWorkflowCaseLink>();
        var decisions = model.Entity<ManualPaymentCoreDecisionLink>();
        var dates = model.Entity<ManualPaymentBusinessDateObservation>();
        model.Entity<ManualPaymentDecision>().HasAlternateKey(x => new
            { x.TenantId, x.LegalEntityId, x.Id, x.ManualPaymentRequestId, x.BranchId, x.ToVersion })
            .HasName("ak_manual_payment_decision_governance");
        cases.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.ManualPaymentRequestId, x.BranchId })
            .HasName("ak_manual_payment_case_source");
        cases.HasOne<ManualPaymentDecision>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.OpeningManualDecisionId, x.ManualPaymentRequestId, x.BranchId, x.OpenedRequestVersion })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.ManualPaymentRequestId, x.BranchId, x.ToVersion }).OnDelete(DeleteBehavior.Restrict);
        decisions.HasOne<ManualPaymentDecision>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ManualPaymentDecisionId, x.ManualPaymentRequestId, x.BranchId, x.ToRequestVersion })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.ManualPaymentRequestId, x.BranchId, x.ToVersion }).OnDelete(DeleteBehavior.Restrict);
        dates.HasOne<ManualPaymentDecision>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ManualPaymentDecisionId, x.ManualPaymentRequestId, x.BranchId, x.RequestVersion })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.ManualPaymentRequestId, x.BranchId, x.ToVersion }).OnDelete(DeleteBehavior.Restrict);
        decisions.HasOne<ManualPaymentWorkflowCaseLink>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.WorkflowCaseLinkId, x.ManualPaymentRequestId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.ManualPaymentRequestId, x.BranchId }).OnDelete(DeleteBehavior.Restrict);
        Link<ManualPaymentWorkflowCaseLink, PolicyVersion>(model, nameof(ManualPaymentWorkflowCaseLink.PolicyVersionId));
        cases.Property(x => x.Purpose).HasMaxLength(20);
        cases.Property(x => x.MakerPrincipalId).HasMaxLength(36);
        cases.Property(x => x.AssigneePrincipalId).HasMaxLength(36);
        cases.Property(x => x.PolicyRulesSha256).HasMaxLength(64);
        cases.Property(x => x.InvoiceGrossAmountAtOpening).HasPrecision(18, 4);
        cases.Property(x => x.SnapshotJson).HasColumnType("text");
        cases.Property(x => x.SnapshotSha256).HasMaxLength(64);
        decisions.Property(x => x.ManualAction).HasMaxLength(20);
        decisions.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        dates.Property(x => x.Action).HasMaxLength(20);
        dates.Property(x => x.CompanyTimeZoneId).HasMaxLength(100);
        cases.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ManualPaymentRequestId, x.Purpose, x.OpenedRequestVersion }).IsUnique();
        cases.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreApprovalCaseId }).IsUnique();
        cases.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningManualDecisionId }).IsUnique();
        decisions.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ManualPaymentDecisionId }).IsUnique();
        decisions.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreApprovalDecisionId }).IsUnique();
        dates.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ManualPaymentDecisionId }).IsUnique();
        cases.HasQueryFilter(x => x.TenantId == CurrentTenantId && x.LegalEntityId == CurrentLegalEntityId
            && (scope.WorkLocationId == null || x.BranchId == scope.WorkLocationId)
            && (!scope.IsConfined || scope.WorkLocationId != null) && scope.OrgUnitId == null);
        decisions.HasQueryFilter(x => x.TenantId == CurrentTenantId && x.LegalEntityId == CurrentLegalEntityId
            && (scope.WorkLocationId == null || x.BranchId == scope.WorkLocationId)
            && (!scope.IsConfined || scope.WorkLocationId != null) && scope.OrgUnitId == null);
        dates.HasQueryFilter(x => x.TenantId == CurrentTenantId && x.LegalEntityId == CurrentLegalEntityId
            && (scope.WorkLocationId == null || x.BranchId == scope.WorkLocationId)
            && (!scope.IsConfined || scope.WorkLocationId != null) && scope.OrgUnitId == null);
        const string notEmpty = "'00000000-0000-0000-0000-000000000000'::uuid";
        cases.ToTable(t => t.HasCheckConstraint("ck_manual_payment_case_source",
            $"id <> {notEmpty} AND branch_id <> {notEmpty} AND version = 1 AND purpose IN ('Approval','Confirmation') AND opened_request_version > 0 AND invoice_version_at_opening > 0 AND invoice_gross_amount_at_opening > 0 AND invoice_gross_amount_at_opening <> 'NaN'::numeric "
            + $"AND core_approval_case_id <> {notEmpty} AND core_approval_stage_id <> {notEmpty} AND core_approval_assignment_id <> {notEmpty} AND policy_version_number > 0 "
            + $"AND {CanonicalPrincipal("maker_principal_id")} AND {CanonicalPrincipal("assignee_principal_id")} AND maker_principal_id <> assignee_principal_id "
            + $"AND (maker_core_linked_worker_id IS NULL OR maker_core_linked_worker_id <> {notEmpty}) AND (assignee_core_linked_worker_id IS NULL OR assignee_core_linked_worker_id <> {notEmpty}) "
            + "AND (maker_core_linked_worker_id IS NULL OR assignee_core_linked_worker_id IS NULL OR maker_core_linked_worker_id <> assignee_core_linked_worker_id) "
            + "AND length(policy_rules_sha256)=64 AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND length(snapshot_sha256)=64 AND snapshot_sha256 ~ '^[0-9a-f]{64}$' "
            + "AND octet_length(snapshot_json) BETWEEN 1 AND 65536 AND isfinite(opened_at)"));
        decisions.ToTable(t => t.HasCheckConstraint("ck_manual_payment_core_decision",
            $"id <> {notEmpty} AND branch_id <> {notEmpty} AND version = 1 AND core_approval_decision_id <> {notEmpty} AND from_request_version > 0 AND to_request_version = from_request_version + 1 "
            + $"AND {CanonicalPrincipal("actor_principal_id")} AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> {notEmpty}) "
            + "AND manual_action IN ('Approve','Return','Reject','Confirm','Dispute','Cancel') AND isfinite(decided_at)"));
        dates.ToTable(t => t.HasCheckConstraint("ck_manual_payment_company_date",
            $"id <> {notEmpty} AND branch_id <> {notEmpty} AND version = 1 AND request_version > 0 AND action IN ('Record','Confirm') "
            + "AND company_business_date > DATE '0001-01-01' AND length(btrim(company_time_zone_id)) BETWEEN 1 AND 100 AND isfinite(observed_at_utc)"));
    }
}
