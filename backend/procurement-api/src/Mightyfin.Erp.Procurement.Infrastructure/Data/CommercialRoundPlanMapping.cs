using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<CommercialRoundEnrollment> CommercialRoundEnrollments => Set<CommercialRoundEnrollment>();
    public DbSet<CommercialRoundPlan> CommercialRoundPlans => Set<CommercialRoundPlan>();
    public DbSet<CommercialRoundPlanRevision> CommercialRoundPlanRevisions => Set<CommercialRoundPlanRevision>();
    public DbSet<CommercialRoundCriterion> CommercialRoundCriteria => Set<CommercialRoundCriterion>();
    public DbSet<CommercialRoundParticipant> CommercialRoundParticipants => Set<CommercialRoundParticipant>();
    public DbSet<CommercialRoundPlanReceipt> CommercialRoundPlanReceipts => Set<CommercialRoundPlanReceipt>();

    private void ConfigureCommercialRoundPlans(ModelBuilder model)
    {
        ConfigureBase<CommercialRoundEnrollment>(model, "commercial_round_enrollments");
        ConfigureBase<CommercialRoundPlan>(model, "commercial_round_plans");
        ConfigureBase<CommercialRoundPlanRevision>(model, "commercial_round_plan_revisions");
        ConfigureBase<CommercialRoundCriterion>(model, "commercial_round_criteria");
        ConfigureBase<CommercialRoundParticipant>(model, "commercial_round_participants");
        ConfigureBase<CommercialRoundPlanReceipt>(model, "commercial_round_plan_receipts");
        var enrollment = model.Entity<CommercialRoundEnrollment>(); var plan = model.Entity<CommercialRoundPlan>();
        var revision = model.Entity<CommercialRoundPlanRevision>(); var criterion = model.Entity<CommercialRoundCriterion>();
        var participant = model.Entity<CommercialRoundParticipant>(); var receipt = model.Entity<CommercialRoundPlanReceipt>();
        Link<CommercialRoundEnrollment, SourcingEvent>(model, nameof(CommercialRoundEnrollment.SourcingEventId));
        model.Entity<SourcingDraftEvidence>().HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.Id }).HasName("ak_round_original_source");
        enrollment.HasOne<SourcingDraftEvidence>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.SourceEvidenceId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_round_original_source");
        enrollment.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.BranchId, x.Id }).HasName("ak_round_enrollment_source");
        enrollment.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId }).IsUnique().HasDatabaseName("ux_round_enrollment_event");
        plan.HasOne<CommercialRoundEnrollment>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.BranchId, x.EnrollmentId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_round_plan_enrollment");
        plan.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.EnrollmentId }).IsUnique().HasDatabaseName("ux_round_plan_enrollment");
        plan.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.BranchId, x.Id }).HasName("ak_round_plan_source");
        revision.HasOne<CommercialRoundPlan>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.BranchId, x.PlanId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_round_revision_source");
        revision.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.Id }).HasName("ak_round_revision_plan");
        revision.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.Id, x.ResultVersion }).HasName("ak_round_revision_result");
        revision.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.ResultVersion }).IsUnique().HasDatabaseName("ux_round_revision_version");
        revision.HasOne<CommercialRoundPlanRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.PreviousRevisionId, x.ExpectedVersion })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.Id, ExpectedVersion = x.ResultVersion }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_round_revision_previous");
        criterion.HasOne<CommercialRoundPlanRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.RevisionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_round_criterion_revision");
        participant.HasOne<CommercialRoundPlanRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.RevisionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_round_participant_revision");
        receipt.HasOne<CommercialRoundPlanRevision>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.RevisionId, x.ResultVersion })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.PlanId, RevisionId = x.Id, x.ResultVersion }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_round_receipt_revision");
        criterion.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.RevisionId, x.LogicalId }).IsUnique().HasDatabaseName("ux_round_criterion_logical");
        criterion.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.RevisionId, x.Order }).IsUnique().HasDatabaseName("ux_round_criterion_order");
        criterion.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.RevisionId, x.Code }).IsUnique().HasDatabaseName("ux_round_criterion_code");
        participant.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.RevisionId, x.LogicalId }).IsUnique().HasDatabaseName("ux_round_participant_logical");
        participant.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.RevisionId, x.Order }).IsUnique().HasDatabaseName("ux_round_participant_order");
        participant.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.RevisionId, x.PrincipalId, x.Duty }).IsUnique().HasDatabaseName("ux_round_participant_duty");
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ActorPrincipalId, x.CommandKey }).IsUnique().HasDatabaseName("ux_round_actor_command");
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PlanId, x.ResultVersion }).IsUnique().HasDatabaseName("ux_round_result_receipt");
        foreach (var type in new[] { typeof(CommercialRoundEnrollment), typeof(CommercialRoundPlan), typeof(CommercialRoundPlanRevision), typeof(CommercialRoundCriterion), typeof(CommercialRoundParticipant), typeof(CommercialRoundPlanReceipt) })
            foreach (var property in model.Entity(type).Metadata.GetProperties())
            {
                if (property.ClrType == typeof(string) && property.Name.EndsWith("PrincipalId", StringComparison.Ordinal)) property.SetMaxLength(36);
                if (property.ClrType == typeof(string) && property.Name.EndsWith("Sha256", StringComparison.Ordinal)) property.SetMaxLength(64);
            }
        plan.Property(x => x.Status).HasMaxLength(20); revision.Property(x => x.Operation).HasMaxLength(20); revision.Property(x => x.ResultStatus).HasMaxLength(20);
        revision.Property(x => x.CompanyTimeZoneId).HasMaxLength(100); revision.Property(x => x.ProposedPolicyReference).HasMaxLength(200);
        revision.Property(x => x.Requirements).HasMaxLength(10000); revision.Property(x => x.DisclosureProposal).HasMaxLength(10000);
        revision.Property(x => x.DocumentRequirements).HasMaxLength(10000); revision.Property(x => x.Reason).HasMaxLength(2000);
        revision.Property(x => x.ContentJson).HasColumnType("text"); receipt.Property(x => x.ResponseJson).HasColumnType("text"); receipt.Property(x => x.Operation).HasMaxLength(20);
        criterion.Property(x => x.Code).HasMaxLength(80); criterion.Property(x => x.Stage).HasMaxLength(20); criterion.Property(x => x.Kind).HasMaxLength(20);
        criterion.Property(x => x.Description).HasMaxLength(2000); criterion.Property(x => x.Rubric).HasMaxLength(10000);
        criterion.Property(x => x.Weight).HasPrecision(18, 4); criterion.Property(x => x.MaximumScore).HasPrecision(18, 4); criterion.Property(x => x.MinimumScore).HasPrecision(18, 4);
        participant.Property(x => x.Duty).HasMaxLength(30); participant.Property(x => x.ConflictNote).HasMaxLength(2000);
        // All domain identities are real nonzero keys. Scope FKs are retained,
        // never re-parented to make an incomplete draft appear complete.
        foreach (var type in new[] { typeof(CommercialRoundEnrollment), typeof(CommercialRoundPlan), typeof(CommercialRoundPlanRevision), typeof(CommercialRoundCriterion), typeof(CommercialRoundParticipant), typeof(CommercialRoundPlanReceipt) })
        {
            var entity = model.Entity(type);
            static string Column(string name) => "\"" + System.Text.RegularExpressions.Regex.Replace(name,"([a-z0-9])([A-Z])","$1_$2").ToLowerInvariant() + "\"";
            var predicates = entity.Metadata.GetProperties().Where(p => p.ClrType == typeof(Guid) || p.ClrType == typeof(Guid?))
                .Select(p => (p.ClrType == typeof(Guid?) ? "(" + Column(p.Name) + " IS NULL OR " : "") + Column(p.Name)
                    + " <> '00000000-0000-0000-0000-000000000000'::uuid" + (p.ClrType == typeof(Guid?) ? ")" : ""));
            entity.ToTable(t => t.HasCheckConstraint("ck_round_nonzero_" + entity.Metadata.GetTableName(), string.Join(" AND ", predicates)));
            var principals = entity.Metadata.GetProperties().Where(p => p.ClrType == typeof(string) && (p.Name.EndsWith("PrincipalId",StringComparison.Ordinal)))
                .Select(p => Column(p.Name) + " <> '00000000-0000-0000-0000-000000000000'").ToArray();
            if(principals.Length>0) entity.ToTable(t=>t.HasCheckConstraint("ck_round_principals_"+entity.Metadata.GetTableName(),string.Join(" AND ",principals)));

        }
        const string principal = " ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'";
        const string immutable = "version=1 AND updated_at IS NULL AND updated_by IS NULL";
        enrollment.ToTable(t => t.HasCheckConstraint("ck_round_enrollment_v1", "schema_version=1 AND source_event_version>0 AND source_sha256 ~ '^[0-9a-f]{64}$' AND original_maker_principal_id" + principal + " AND enrolled_by_principal_id" + principal + " AND isfinite(recorded_at) AND " + immutable));
        plan.ToTable(t => t.HasCheckConstraint("ck_round_plan_draft", "version>0 AND status IN ('Draft','InReview','Rejected','Published','Cancelled') AND maker_principal_id" + principal));
        revision.ToTable(t => t.HasCheckConstraint("ck_round_revision_v1", "schema_version=1 AND expected_version>=0 AND result_version=expected_version+1 AND operation IN ('Create','Revise','Cancel','Submit','Return','Reject','Approve') AND ((operation='Create' AND expected_version=0 AND previous_revision_id IS NULL) OR (operation<>'Create' AND expected_version>0 AND previous_revision_id IS NOT NULL)) AND ((operation IN ('Create','Revise','Return') AND result_status='Draft') OR (operation='Cancel' AND result_status='Cancelled') OR (operation='Submit' AND result_status='InReview') OR (operation='Reject' AND result_status='Rejected') OR (operation='Approve' AND result_status IN ('InReview','Published'))) AND ((operation IN ('Cancel','Return','Reject') AND char_length(btrim(reason)) BETWEEN 1 AND 2000) OR (operation NOT IN ('Cancel','Return','Reject') AND reason IS NULL)) AND criterion_count BETWEEN 0 AND 100 AND participant_count BETWEEN 0 AND 100 AND octet_length(content_json) BETWEEN 2 AND 2000000 AND content_sha256 ~ '^[0-9a-f]{64}$' AND actor_principal_id" + principal + " AND isfinite(recorded_at) AND " + immutable));
        criterion.ToTable(t => t.HasCheckConstraint("ck_round_criterion_v1", "\"order\" BETWEEN 1 AND 100 AND stage IN ('Mandatory','Technical','Commercial') AND kind IN ('PassFail','Weighted') AND char_length(btrim(code)) BETWEEN 1 AND 80 AND (weight IS NULL OR weight>=0) AND (maximum_score IS NULL OR maximum_score>=0) AND (minimum_score IS NULL OR minimum_score>=0) AND (minimum_score IS NULL OR maximum_score IS NULL OR minimum_score<=maximum_score) AND (kind<>'PassFail' OR (weight IS NULL AND maximum_score IS NULL AND minimum_score IS NULL)) AND " + immutable));
        participant.ToTable(t => t.HasCheckConstraint("ck_round_participant_v1", "\"order\" BETWEEN 1 AND 100 AND principal_id" + principal + " AND duty IN ('Planner','Reviewer','OpeningOfficer','TechnicalEvaluator','CommercialEvaluator','ConflictReviewer') AND " + immutable));
        receipt.ToTable(t => t.HasCheckConstraint("ck_round_receipt_v1", "result_version>0 AND operation IN ('Create','Revise','Cancel','Submit','Return','Reject','Approve') AND response_status_code=CASE WHEN operation IN ('Create','Submit') THEN 201 ELSE 200 END AND octet_length(response_json) BETWEEN 2 AND 65536 AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND actor_principal_id" + principal + " AND " + immutable));
    }
}
