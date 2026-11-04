using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<CommercialRoundReviewSubmission> CommercialRoundReviewSubmissions => Set<CommercialRoundReviewSubmission>();
    public DbSet<CommercialRoundReviewOutcome> CommercialRoundReviewOutcomes => Set<CommercialRoundReviewOutcome>();

    private void ConfigureCommercialRoundReview(ModelBuilder model)
    {
        ConfigureBase<CommercialRoundReviewSubmission>(model, "commercial_round_review_submissions");
        ConfigureBase<CommercialRoundReviewOutcome>(model, "commercial_round_review_outcomes");
        var submission = model.Entity<CommercialRoundReviewSubmission>();
        var outcome = model.Entity<CommercialRoundReviewOutcome>();
        submission.HasOne<CommercialRoundPlan>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.BranchId, x.PlanId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.BranchId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_round_review_plan");
        submission.HasOne<CommercialRoundPlanRevision>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.SubmittedRevisionId, x.SubmittedPlanVersion })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, RevisionId = x.Id, x.ResultVersion })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_round_review_submitted_revision");
        submission.HasOne<PolicyVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_round_review_policy");
        submission.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.Id })
            .HasName("ak_round_review_submission_plan");
        submission.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.SubmittedPlanVersion })
            .IsUnique().HasDatabaseName("ux_round_review_submission_version");
        submission.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreCaseId })
            .IsUnique().HasDatabaseName("ux_round_review_core_case");
        outcome.HasOne<CommercialRoundReviewSubmission>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.SubmissionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_round_review_outcome_submission");
        outcome.HasOne<CommercialRoundPlanRevision>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.ResultRevisionId, x.ResultPlanVersion })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, RevisionId = x.Id, x.ResultVersion })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_round_review_outcome_revision");
        outcome.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreDecisionId })
            .IsUnique().HasDatabaseName("ux_round_review_core_decision");
        outcome.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.ResultPlanVersion })
            .IsUnique().HasDatabaseName("ux_round_review_result_version");
        submission.Property(x => x.SubmittedContentSha256).HasMaxLength(64);
        submission.Property(x => x.PolicyRulesSha256).HasMaxLength(64);
        submission.Property(x => x.MakerPrincipalId).HasMaxLength(36);
        outcome.Property(x => x.ResultStatus).HasMaxLength(20);
        outcome.Property(x => x.Action).HasMaxLength(20);
        outcome.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        outcome.Property(x => x.Reason).HasMaxLength(2000);
        const string hash = " ~ '^[0-9a-f]{64}$'";
        const string principal = " ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'";
        submission.ToTable(t => t.HasCheckConstraint("ck_round_review_submission",
            "submitted_plan_version>0 AND policy_version_number>0 AND submitted_content_sha256" + hash
            + " AND policy_rules_sha256" + hash + " AND maker_principal_id" + principal
            + " AND submitted_revision_id<>'00000000-0000-0000-0000-000000000000'::uuid"
            + " AND core_case_id<>'00000000-0000-0000-0000-000000000000'::uuid"
            + " AND isfinite(submitted_at) AND version=1 AND updated_at IS NULL AND updated_by IS NULL"));
        outcome.ToTable(t => t.HasCheckConstraint("ck_round_review_outcome",
            "result_plan_version>0 AND action IN ('Approve','Return','Reject')"
            + " AND ((action='Approve' AND result_status IN ('InReview','Published') AND reason IS NULL)"
            + " OR (action='Return' AND result_status='Draft' AND char_length(btrim(reason)) BETWEEN 1 AND 2000)"
            + " OR (action='Reject' AND result_status='Rejected' AND char_length(btrim(reason)) BETWEEN 1 AND 2000))"
            + " AND actor_principal_id" + principal
            + " AND isfinite(recorded_at) AND version=1 AND updated_at IS NULL AND updated_by IS NULL"));
    }

    private void GuardCommercialRoundReview()
    {
        foreach (var change in stagedRoundPlans.Where(x => x.Revision.Operation is "Submit" or "Approve" or "Return" or "Reject"))
        {
            if (change.Revision.Operation == "Submit"
                && !ChangeTracker.Entries<CommercialRoundReviewSubmission>().Any(x => x.State == EntityState.Added
                    && x.Entity.PlanId == change.Plan.Id && x.Entity.SubmittedRevisionId == change.Revision.Id)
                || change.Revision.Operation != "Submit"
                && !ChangeTracker.Entries<CommercialRoundReviewOutcome>().Any(x => x.State == EntityState.Added
                    && x.Entity.PlanId == change.Plan.Id && x.Entity.ResultRevisionId == change.Revision.Id))
                throw new InvalidOperationException("RoundReviewOwnedGraphRequired");
        }
        foreach (var entry in ChangeTracker.Entries<CommercialRoundReviewSubmission>())
        {
            if (entry.State == EntityState.Unchanged) continue;
            var row = entry.Entity;
            var staged = stagedRoundPlans.SingleOrDefault(x => x.Plan.Id == row.PlanId
                && x.Revision.Id == row.SubmittedRevisionId && x.Revision.Operation == "Submit");
            using var submissionResponse = staged is null ? null : System.Text.Json.JsonDocument.Parse(staged.Receipt.ResponseJson);
            if (entry.State != EntityState.Added || staged is null
                || submissionResponse is null
                || submissionResponse.RootElement.GetProperty("submissionId").GetGuid() != row.Id
                || submissionResponse.RootElement.GetProperty("caseId").GetGuid() != row.CoreCaseId
                || row.TenantId != staged.Plan.TenantId || row.LegalEntityId != staged.Plan.LegalEntityId
                || row.SourcingEventId != staged.Plan.SourcingEventId || row.BranchId != staged.Plan.BranchId
                || row.SubmittedPlanVersion != staged.Revision.ResultVersion
                || row.SubmittedContentSha256 != staged.Revision.ContentSha256
                || row.PolicyVersionId == Guid.Empty || row.PolicyVersionNumber < 1
                || row.PolicyRulesSha256 is not { Length: 64 } || row.CoreCaseId == Guid.Empty
                || row.MakerPrincipalId != staged.Revision.ActorPrincipalId
                || row.MakerWorkerAlias != staged.Revision.ActorWorkerAlias
                || row.SubmittedAt != staged.Revision.RecordedAt)
                throw new InvalidOperationException("RoundReviewOpeningEvidenceInvalid");
        }
        foreach (var entry in ChangeTracker.Entries<CommercialRoundReviewOutcome>())
        {
            if (entry.State == EntityState.Unchanged) continue;
            var row = entry.Entity;
            var staged = stagedRoundPlans.SingleOrDefault(x => x.Plan.Id == row.PlanId
                && x.Revision.Id == row.ResultRevisionId && x.Revision.Operation == row.Action);
            using var decisionResponse = staged is null ? null : System.Text.Json.JsonDocument.Parse(staged.Receipt.ResponseJson);
            if (entry.State != EntityState.Added || staged is null
                || decisionResponse is null
                || decisionResponse.RootElement.GetProperty("submissionId").GetGuid() != row.SubmissionId
                || decisionResponse.RootElement.GetProperty("caseId").GetGuid() != row.CoreCaseId
                || decisionResponse.RootElement.GetProperty("assignmentId").GetGuid() != row.CoreAssignmentId
                || decisionResponse.RootElement.GetProperty("decisionId").GetGuid() != row.CoreDecisionId
                || row.TenantId != staged.Plan.TenantId || row.LegalEntityId != staged.Plan.LegalEntityId
                || row.SourcingEventId != staged.Plan.SourcingEventId || row.BranchId != staged.Plan.BranchId
                || row.ResultPlanVersion != staged.Revision.ResultVersion
                || row.ResultStatus != staged.Revision.ResultStatus
                || row.ActorPrincipalId != staged.Revision.ActorPrincipalId
                || row.ActorWorkerAlias != staged.Revision.ActorWorkerAlias
                || row.Reason != staged.Revision.Reason
                || row.CoreCaseId == Guid.Empty || row.CoreStageId == Guid.Empty
                || row.CoreAssignmentId == Guid.Empty || row.CoreDecisionId == Guid.Empty
                || row.SubmissionId == Guid.Empty || row.RecordedAt != staged.Revision.RecordedAt)
                throw new InvalidOperationException("RoundReviewDecisionEvidenceInvalid");
        }
    }
}
