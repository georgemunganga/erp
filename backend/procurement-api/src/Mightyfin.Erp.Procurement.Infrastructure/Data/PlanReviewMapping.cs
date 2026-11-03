using Microsoft.EntityFrameworkCore;
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
                "submitted_plan_version > 0 AND estimated_amount >= 0 AND branch_ref <> '00000000-0000-0000-0000-000000000000'::uuid AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND state IN ('Pending','Returned','Rejected','Approved','Withdrawn')"));
        });
        model.Entity<PlanReviewCommandReceipt>(b =>
        {
            b.Property(x => x.Operation).HasMaxLength(40);
            b.Property(x => x.ActorSubjectId).HasMaxLength(200);
            b.Property(x => x.RequestSha256).HasMaxLength(64);
            b.Property(x => x.ResponseJson).HasColumnType("text");
            b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CommandKey }).IsUnique();
            b.HasOne<ProcurementPlan>().WithMany()
                .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ProcurementPlanId })
                .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
            b.ToTable(t => t.HasCheckConstraint("ck_plan_review_receipt",
                "command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND operation IN ('ClaimScope','Submit','Decide','Withdraw') AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_status BETWEEN 200 AND 299"));
        });
    }
}
