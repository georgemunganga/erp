using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private void ConfigurePlanReview(ModelBuilder model)
    {
        ConfigureBase<PlanReviewSubmission>(model, "plan_review_submissions");
        ConfigureBase<PlanReviewCommandReceipt>(model, "plan_review_command_receipts");
        model.Entity<PlanReviewSubmission>(b =>
        {
            b.Property(x => x.MakerSubjectId).HasMaxLength(200);
            b.Property(x => x.CurrencyCode).HasMaxLength(3);
            b.Property(x => x.EstimatedAmount).HasPrecision(18, 4);
            b.Property(x => x.SnapshotJson).HasColumnType("text");
            b.Property(x => x.SnapshotSha256).HasMaxLength(64);
            b.Property(x => x.PolicyRulesSha256).HasMaxLength(64);
            b.Property(x => x.State).HasMaxLength(30);
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreApprovalCaseId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ProcurementPlanId, x.SubmittedPlanVersion }).IsUnique();
            b.HasOne<ProcurementPlan>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ProcurementPlanId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PolicyVersion>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("ck_plan_review_submission",
                "submitted_plan_version > 0 AND estimated_amount >= 0 AND branch_ref <> '00000000-0000-0000-0000-000000000000'::uuid AND (maker_core_linked_worker_id IS NULL OR maker_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND state IN ('Pending','Returned','Rejected','Approved','Withdrawn')"));
        });
        model.Entity<PlanReviewCommandReceipt>(b =>
        {
            b.Property(x => x.Operation).HasMaxLength(40);
            b.Property(x => x.ActorSubjectId).HasMaxLength(200);
            b.Property(x => x.RequestSha256).HasMaxLength(64);
            b.Property(x => x.ResponseJson).HasColumnType("text");
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CommandKey }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.DecisionId });
            b.HasOne<ProcurementPlan>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ProcurementPlanId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("ck_plan_review_receipt",
                "command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND operation IN ('ClaimScope','Submit','Decide','Withdraw') AND (decision_id IS NULL OR decision_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((operation IN ('Decide','Withdraw') AND decision_id IS NOT NULL) OR (operation IN ('ClaimScope','Submit') AND decision_id IS NULL)) AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_status BETWEEN 200 AND 299"));
        });
    }

    private void GuardPlanReview()
    {
        foreach (var entry in ChangeTracker.Entries<PlanReviewSubmission>())
        {
            var row = entry.Entity;
            if (entry.State == EntityState.Added)
            {
                if (row.State != "Pending" || row.CompletedAt is not null
                    || row.SubmittedPlanVersion < 1 || row.CoreApprovalCaseId == Guid.Empty
                    || row.MakerCoreLinkedWorkerId == Guid.Empty
                    || ProcurementEvidenceEncoding.Digest(row.SnapshotJson) != row.SnapshotSha256
                    || row.PolicyRulesSha256 is not { Length: 64 })
                    throw new InvalidOperationException("PlanReviewOpeningEvidenceInvalid");
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entry.Property(x => x.ProcurementPlanId).IsModified
                    || entry.Property(x => x.BranchRef).IsModified
                    || entry.Property(x => x.SubmittedPlanVersion).IsModified
                    || entry.Property(x => x.CoreApprovalCaseId).IsModified
                    || entry.Property(x => x.PolicyVersionId).IsModified
                    || entry.Property(x => x.PolicyVersionNumber).IsModified
                    || entry.Property(x => x.PolicyRulesSha256).IsModified
                    || entry.Property(x => x.MakerSubjectId).IsModified
                    || entry.Property(x => x.MakerCoreLinkedWorkerId).IsModified
                    || entry.Property(x => x.CurrencyCode).IsModified
                    || entry.Property(x => x.EstimatedAmount).IsModified
                    || entry.Property(x => x.SnapshotJson).IsModified
                    || entry.Property(x => x.SnapshotSha256).IsModified
                    || entry.Property(x => x.SubmittedAt).IsModified
                    || entry.Property(x => x.State).OriginalValue != "Pending"
                    || row.State is not ("Approved" or "Returned" or "Rejected" or "Withdrawn")
                    || row.CompletedAt is null)
                    throw new InvalidOperationException("PlanReviewPinnedEvidenceImmutable");
            }
        }
    }
}
