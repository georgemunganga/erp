using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<PrincipalRfqRecommendationSubmission> PrincipalRfqRecommendationSubmissions => Set<PrincipalRfqRecommendationSubmission>();
    public DbSet<PrincipalRfqAwardReviewSubmission> PrincipalRfqAwardReviewSubmissions => Set<PrincipalRfqAwardReviewSubmission>();
    public DbSet<PrincipalRfqAwardReviewLine> PrincipalRfqAwardReviewLines => Set<PrincipalRfqAwardReviewLine>();
    public DbSet<PrincipalRfqAwardReviewReceipt> PrincipalRfqAwardReviewReceipts => Set<PrincipalRfqAwardReviewReceipt>();
    public DbSet<PrincipalRfqAwardReviewOutcome> PrincipalRfqAwardReviewOutcomes => Set<PrincipalRfqAwardReviewOutcome>();

    private void ConfigureRfqAwardReview(ModelBuilder model)
    {
        ConfigureBase<PrincipalRfqRecommendationSubmission>(model, "principal_rfq_recommendation_submissions");
        ConfigureBase<PrincipalRfqAwardReviewSubmission>(model, "principal_rfq_award_review_submissions");
        ConfigureBase<PrincipalRfqAwardReviewLine>(model, "principal_rfq_award_review_lines");
        ConfigureBase<PrincipalRfqAwardReviewReceipt>(model, "principal_rfq_award_review_receipts");
        ConfigureBase<PrincipalRfqAwardReviewOutcome>(model, "principal_rfq_award_review_outcomes");
        var freeze = model.Entity<PrincipalRfqRecommendationSubmission>();
        var review = model.Entity<PrincipalRfqAwardReviewSubmission>();
        var line = model.Entity<PrincipalRfqAwardReviewLine>();
        var receipt = model.Entity<PrincipalRfqAwardReviewReceipt>();
        var outcome = model.Entity<PrincipalRfqAwardReviewOutcome>();
        freeze.HasKey(x => x.Id).HasName("pk_p38_rec_submit");
        review.HasKey(x => x.Id).HasName("pk_p38_review_submit");
        line.HasKey(x => x.Id).HasName("pk_p38_review_line");
        receipt.HasKey(x => x.Id).HasName("pk_p38_review_receipt");
        outcome.HasKey(x => x.Id).HasName("pk_p38_review_outcome");
        freeze.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
            .HasName("ak_p38_rec_sub_scope");
        review.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
            .HasName("ak_p38_review_sub_scope");
        line.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
            .HasName("ak_p38_line_scope");
        receipt.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
            .HasName("ak_p38_receipt_scope");
        outcome.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
            .HasName("ak_p38_outcome_scope");
        freeze.Property(x => x.Id).ValueGeneratedNever();
        review.Property(x => x.Id).ValueGeneratedNever();
        line.Property(x => x.Id).ValueGeneratedNever();
        receipt.Property(x => x.Id).ValueGeneratedNever();
        outcome.Property(x => x.Id).ValueGeneratedNever();

        freeze.Property(x => x.MakerPrincipalId).HasMaxLength(36);
        freeze.Property(x => x.RevisionSha256).HasMaxLength(64);
        freeze.Property(x => x.RequestSha256).HasMaxLength(64);
        freeze.Property(x => x.SnapshotSha256).HasMaxLength(64);
        freeze.Property(x => x.SnapshotJson).HasColumnType("text");
        review.Property(x => x.PolicyKey).HasMaxLength(100);
        review.Property(x => x.MakerPrincipalId).HasMaxLength(36);
        review.Property(x => x.RecommendationSha256).HasMaxLength(64);
        review.Property(x => x.PolicyRulesSha256).HasMaxLength(64);
        review.Property(x => x.RequestSha256).HasMaxLength(64);
        review.Property(x => x.AssignmentPinsSha256).HasMaxLength(64);
        review.Property(x => x.AssignmentPinsJson).HasColumnType("text");
        review.Property(x => x.CurrencyCode).HasMaxLength(3);
        review.Property(x => x.ProposedTotalExact).HasMaxLength(40);
        line.Property(x => x.OfferSha256).HasMaxLength(64);
        line.Property(x => x.QuantityExact).HasMaxLength(20);
        line.Property(x => x.CurrencyCode).HasMaxLength(3);
        line.Property(x => x.RetainedQuoteTotalExact).HasMaxLength(40);
        receipt.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        receipt.Property(x => x.RequestSha256).HasMaxLength(64);
        receipt.Property(x => x.ResponseSha256).HasMaxLength(64);
        receipt.Property(x => x.ResponseJson).HasColumnType("text");
        outcome.Property(x => x.CoreState).HasMaxLength(40);
        outcome.Property(x => x.SourceOutcome).HasMaxLength(40);

        // Both existing P36 revision keys identify the same revision: scope/source
        // tuple and immutable digest. No new key is added to an installed family.
        freeze.HasOne<PrincipalRfqRecommendationDraftRevision>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.RevisionId, x.DraftId,
                x.RoundId, x.UnsealingId, x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.DraftId,
                x.RoundId, x.UnsealingId, x.SourcingEventId, x.BranchId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p38_rec_revision_scope");
        freeze.HasOne<PrincipalRfqRecommendationDraftRevision>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.RevisionId, x.DraftId, x.RevisionSha256 })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.DraftId, x.SnapshotSha256 })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p38_rec_revision_hash");
        freeze.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.DraftId,
            x.AwardReviewSubmissionId, x.RevisionId, x.RoundId, x.UnsealingId,
            x.SourcingEventId, x.BranchId, x.RevisionSha256 }).HasName("ak_p38_rec_owner");
        freeze.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.DraftId, x.RevisionId })
            .IsUnique().HasDatabaseName("ux_p38_rec_revision");
        freeze.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.MakerPrincipalId, x.CommandKey })
            .IsUnique().HasDatabaseName("ux_p38_rec_actor_key");

        review.HasOne<PrincipalRfqRecommendationSubmission>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.RecommendationSubmissionId, x.DraftId,
                x.Id, x.RecommendationRevisionId, x.RoundId, x.UnsealingId,
                x.SourcingEventId, x.BranchId, x.RecommendationSha256 })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.DraftId,
                x.AwardReviewSubmissionId, x.RevisionId, x.RoundId, x.UnsealingId,
                x.SourcingEventId, x.BranchId, x.RevisionSha256 })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p38_review_rec");
        review.HasOne<PolicyVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId,
                x.PolicyKey, x.PolicyVersionNumber })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.PolicyKey, x.PolicyVersionNumber })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p38_review_policy");
        review.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
            x.RecommendationSubmissionId, x.RoundId, x.UnsealingId, x.SourcingEventId, x.BranchId })
            .HasName("ak_p38_review_line_owner");
        review.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
            x.RecommendationSubmissionId, x.RoundId, x.SourcingEventId, x.BranchId,
            x.MakerPrincipalId, x.CommandKey, x.RequestSha256 })
            .HasName("ak_p38_review_receipt_owner");
        review.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.CoreCaseId })
            .HasName("ak_p38_review_core_owner");
        review.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.RecommendationSubmissionId })
            .IsUnique().HasDatabaseName("ux_p38_review_rec");
        review.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreCaseId })
            .IsUnique().HasDatabaseName("ux_p38_review_core");
        review.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.MakerPrincipalId, x.CommandKey })
            .IsUnique().HasDatabaseName("ux_p38_review_actor_key");

        line.HasOne<PrincipalRfqAwardReviewSubmission>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId,
                x.RecommendationSubmissionId, x.RoundId, x.UnsealingId, x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.RecommendationSubmissionId, x.RoundId, x.UnsealingId, x.SourcingEventId, x.BranchId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p38_line_review");
        line.HasOne<PrincipalRfqCommercialOfferSubmissionRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.OfferId, x.RoundId,
                x.SourcingEventId, x.BranchId, x.SupplierId, x.OfferSha256 })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.OpeningId,
                x.SourcingEventId, x.BranchId, x.SupplierId, x.SnapshotSha256 })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p38_line_offer");
        line.HasOne<SourcingLine>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SourcingLineId, x.SourcingEventId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SourcingEventId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p38_line_source");
        line.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId, x.OfferId, x.SourcingLineId })
            .IsUnique().HasDatabaseName("ux_p38_line_choice");

        receipt.HasOne<PrincipalRfqAwardReviewSubmission>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId,
                x.RecommendationSubmissionId, x.RoundId, x.SourcingEventId, x.BranchId,
                x.ActorPrincipalId, x.CommandKey, x.RequestSha256 })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.RecommendationSubmissionId, x.RoundId, x.SourcingEventId, x.BranchId,
                x.MakerPrincipalId, x.CommandKey, x.RequestSha256 })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p38_receipt_review");
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ActorPrincipalId, x.CommandKey })
            .IsUnique().HasDatabaseName("ux_p38_receipt_actor_key");
        outcome.HasOne<PrincipalRfqAwardReviewSubmission>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId, x.CoreCaseId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.CoreCaseId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p38_outcome_review");
        outcome.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId })
            .IsUnique().HasDatabaseName("ux_p38_outcome_submission");
        outcome.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.TerminalDecisionId })
            .IsUnique().HasDatabaseName("ux_p38_outcome_core_decision");
        const string present = "<>'00000000-0000-0000-0000-000000000000'::uuid";
        const string digest = " ~ '^[a-f0-9]{64}$'";
        freeze.ToTable(t => t.HasCheckConstraint("ck_p38_rec_submit",
            "schema_version=1 AND id"+present+" AND draft_id"+present+" AND award_review_submission_id"+present
            +" AND revision_id"+present+" AND revision>0 AND round_id"+present+" AND unsealing_id"+present
            +" AND sourcing_event_id"+present+" AND branch_id"+present+" AND command_key"+present
            +" AND revision_sha256"+digest+" AND request_sha256"+digest
            +" AND snapshot_sha256"+digest+" AND snapshot_sha256=encode(sha256(convert_to(snapshot_json,'UTF8')),'hex')"));
        review.ToTable(t => t.HasCheckConstraint("ck_p38_review_submit",
            "schema_version=1 AND id"+present+" AND recommendation_submission_id"+present
            +" AND draft_id"+present+" AND recommendation_revision_id"+present
            +" AND recommendation_revision>0 AND round_id"+present+" AND unsealing_id"+present
            +" AND sourcing_event_id"+present+" AND branch_id"+present+" AND core_case_id"+present
            +" AND policy_version_id"+present+" AND policy_version_number>0"
            +" AND policy_key='SOURCING_SUPPLIER_AWARD' AND command_key"+present
            +" AND recommendation_sha256"+digest+" AND policy_rules_sha256"+digest
            +" AND request_sha256"+digest+" AND assignment_pins_sha256"+digest
            +" AND assignment_pins_sha256=encode(sha256(convert_to(assignment_pins_json,'UTF8')),'hex')"
            +" AND currency_code ~ '^[A-Z]{3}$'"
            +" AND proposed_total_exact ~ '^(0|[1-9][0-9]{0,29})(\\.[0-9]{1,8})?$'"));
        line.ToTable(t => t.HasCheckConstraint("ck_p38_review_line",
            "id"+present+" AND submission_id"+present+" AND recommendation_submission_id"+present
            +" AND round_id"+present+" AND unsealing_id"+present+" AND sourcing_event_id"+present
            +" AND branch_id"+present+" AND offer_id"+present+" AND supplier_id"+present
            +" AND sourcing_line_id"+present+" AND offer_sha256"+digest
            +" AND quantity_exact ~ '^(0|[1-9][0-9]{0,13})(\\.[0-9]{1,4})?$'"
            +" AND quantity_exact !~ '^0(\\.0+)?$' AND currency_code ~ '^[A-Z]{3}$'"
            +" AND retained_quote_total_exact ~ '^(0|[1-9][0-9]{0,29})(\\.[0-9]{1,8})?$'"));
        receipt.ToTable(t => t.HasCheckConstraint("ck_p38_review_receipt",
            "id"+present+" AND submission_id"+present+" AND recommendation_submission_id"+present
            +" AND round_id"+present+" AND sourcing_event_id"+present+" AND branch_id"+present
            +" AND command_key"+present+" AND request_sha256"+digest+" AND response_sha256"+digest
            +" AND response_sha256=encode(sha256(convert_to(response_json,'UTF8')),'hex')"));
        outcome.ToTable(t => t.HasCheckConstraint("ck_p38_review_outcome",
            "id"+present+" AND submission_id"+present+" AND core_case_id"+present
            +" AND terminal_decision_id"+present
            +" AND ((core_state='Approved' AND source_outcome='CoreApprovedPendingAward')"
            +" OR (core_state='Returned' AND source_outcome='Returned')"
            +" OR (core_state='Rejected' AND source_outcome='Rejected'))"));
    }

    private void GuardRfqAwardReview()
    {
        var freezes = ChangeTracker.Entries<PrincipalRfqRecommendationSubmission>().ToArray();
        var reviews = ChangeTracker.Entries<PrincipalRfqAwardReviewSubmission>().ToArray();
        var lines = ChangeTracker.Entries<PrincipalRfqAwardReviewLine>().ToArray();
        var receipts = ChangeTracker.Entries<PrincipalRfqAwardReviewReceipt>().ToArray();
        var outcomes = ChangeTracker.Entries<PrincipalRfqAwardReviewOutcome>().ToArray();
        foreach (var entry in freezes.Cast<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry>()
            .Concat(reviews).Concat(lines).Concat(receipts).Concat(outcomes))
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("RfqAwardEvidenceAppendOnly");
        // The terminal Core loader and same-transaction decision proof are not
        // installed yet. An outcome cannot be smuggled into the source context.
        if (outcomes.Any(x => x.State == EntityState.Added))
            throw new InvalidOperationException("RfqAwardCoreOutcomeAdapterUnavailable");
        var addedFreezes = freezes.Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        var addedReviews = reviews.Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        var addedLines = lines.Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        var addedReceipts = receipts.Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        var audits = ChangeTracker.Entries<AuditEvent>().Where(x => x.State == EntityState.Added)
            .Select(x => x.Entity).Where(x => x.RecordType == nameof(PrincipalRfqAwardReviewSubmission)).ToArray();
        var events = ChangeTracker.Entries<OutboxEvent>().Where(x => x.State == EntityState.Added)
            .Select(x => x.Entity).Where(x => x.EventType == PrincipalRfqAwardReviewEffects.EventType).ToArray();
        if (addedFreezes.Length == 0 && addedReviews.Length == 0 && addedLines.Length == 0 && addedReceipts.Length == 0)
            return; // Existing P36 Draft CRUD never queries an uninstalled P38 table.
        if (addedFreezes.Length != 1 || addedReviews.Length != 1 || addedReceipts.Length != 1 || addedLines.Length == 0)
            throw new InvalidOperationException("RfqAwardSubmissionBatchIncomplete");
        var frozen = addedFreezes[0];
        var review = addedReviews[0];
        var receipt = addedReceipts[0];
        if (new ProcurementRecord[] { frozen, review, receipt }.Concat(addedLines)
            .Any(x => x.Id == Guid.Empty || x.TenantId != scope.TenantId || x.LegalEntityId != scope.LegalEntityId
                || x.CreatedBy != scope.SubjectId || x.CreatedAt == default || x.Version != 1))
            throw new InvalidOperationException("RfqAwardAuthenticatedScopeRequired");
        if (frozen.SchemaVersion != 1 || review.SchemaVersion != 1
            || frozen.AwardReviewSubmissionId != review.Id || review.RecommendationSubmissionId != frozen.Id
            || frozen.DraftId == Guid.Empty || frozen.DraftId != review.DraftId
            || frozen.RevisionId == Guid.Empty || frozen.RevisionId != review.RecommendationRevisionId
            || frozen.Revision != review.RecommendationRevision || frozen.Revision < 1
            || frozen.RevisionSha256 != review.RecommendationSha256
            || frozen.RoundId == Guid.Empty || frozen.RoundId != review.RoundId
            || frozen.UnsealingId == Guid.Empty || frozen.UnsealingId != review.UnsealingId
            || frozen.SourcingEventId == Guid.Empty || frozen.SourcingEventId != review.SourcingEventId
            || frozen.BranchId == Guid.Empty || frozen.BranchId != review.BranchId
            || frozen.MakerPrincipalId != scope.SubjectId || review.MakerPrincipalId != scope.SubjectId
            || frozen.MakerWorkerAliasId != review.MakerWorkerAliasId
            || frozen.MakerWorkerAliasId != scope.CoreLinkedWorkerId
            || frozen.CommandKey == Guid.Empty || frozen.CommandKey != review.CommandKey
            || frozen.RequestSha256 != review.RequestSha256
            || frozen.SnapshotSha256 != frozen.RevisionSha256
            || frozen.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(frozen.SnapshotJson)
            || review.CoreCaseId == Guid.Empty || review.PolicyVersionId == Guid.Empty
            || review.PolicyKey != PrincipalRfqAwardFoundation.PolicyKey || review.PolicyVersionNumber < 1
            || review.AssignmentPinsSha256 != ProcurementEvidenceEncoding.Digest(review.AssignmentPinsJson)
            || frozen.SubmittedAt == default || frozen.SubmittedAt != review.SubmittedAt
            || frozen.CreatedAt != frozen.SubmittedAt || review.CreatedAt != review.SubmittedAt
            || ProcurementEvidenceEncoding.DatabaseTimestamp(frozen.SubmittedAt) != frozen.SubmittedAt)
            throw new InvalidOperationException("RfqAwardFrozenSourceMismatch");
        var head = ChangeTracker.Entries<PrincipalRfqRecommendationDraftHead>()
            .SingleOrDefault(x => x.Entity.Id == frozen.DraftId);
        var revision = ChangeTracker.Entries<PrincipalRfqRecommendationDraftRevision>()
            .SingleOrDefault(x => x.Entity.Id == frozen.RevisionId);
        if (head is null || revision is null
            || head.State != EntityState.Unchanged || revision.State != EntityState.Unchanged
            || head.Entity.CurrentRevisionId != frozen.RevisionId
            || head.Entity.CurrentRevision != frozen.Revision
            || head.Entity.CurrentSnapshotSha256 != frozen.RevisionSha256
            || head.Entity.RoundId != frozen.RoundId || head.Entity.UnsealingId != frozen.UnsealingId
            || head.Entity.SourcingEventId != frozen.SourcingEventId || head.Entity.BranchId != frozen.BranchId
            || head.Entity.MakerPrincipalId != frozen.MakerPrincipalId
            || revision.Entity.DraftId != frozen.DraftId || revision.Entity.Revision != frozen.Revision
            || revision.Entity.SnapshotSha256 != frozen.RevisionSha256
            || revision.Entity.SnapshotJson != frozen.SnapshotJson)
            throw new InvalidOperationException("RfqAwardCurrentRecommendationRequired");
        if (receipt.SubmissionId != review.Id || receipt.RecommendationSubmissionId != frozen.Id
            || receipt.RoundId != review.RoundId || receipt.SourcingEventId != review.SourcingEventId
            || receipt.BranchId != review.BranchId || receipt.ActorPrincipalId != scope.SubjectId
            || receipt.CommandKey != review.CommandKey || receipt.RequestSha256 != review.RequestSha256
            || receipt.CreatedAt != review.SubmittedAt
            || receipt.ResponseJson != PrincipalRfqAwardReviewWorkflow.ResponseFor(review)
            || receipt.ResponseSha256 != ProcurementEvidenceEncoding.Digest(receipt.ResponseJson))
            throw new InvalidOperationException("RfqAwardReceiptMismatch");
        var payload = PrincipalRfqAwardReviewEffects.Payload(review);
        if (audits.Length != 1 || audits[0].RecordId != review.Id
            || audits[0].ActorSubjectId != scope.SubjectId
            || audits[0].Action != PrincipalRfqAwardReviewEffects.AuditAction
            || audits[0].ChangedFieldsJson != payload
            || events.Length != 1 || events[0].AggregateType != nameof(PrincipalRfqAwardReviewSubmission)
            || events[0].AggregateId != review.Id || events[0].EventVersion != 1
            || events[0].PayloadJson != payload || events[0].IdempotencyKey != review.CommandKey.ToString("D"))
            throw new InvalidOperationException("RfqAwardSameSaveEffectsRequired");
        if (addedLines.Any(x => x.SubmissionId != review.Id || x.RecommendationSubmissionId != frozen.Id
            || x.RoundId != review.RoundId || x.UnsealingId != review.UnsealingId
            || x.SourcingEventId != review.SourcingEventId || x.BranchId != review.BranchId
            || x.OfferId == Guid.Empty || x.SupplierId == Guid.Empty || x.SourcingLineId == Guid.Empty
            || x.CreatedAt != review.SubmittedAt)
            || addedLines.Select(x => (x.OfferId, x.SourcingLineId)).Distinct().Count() != addedLines.Length)
            throw new InvalidOperationException("RfqAwardLineSourceMismatch");
    }
}
