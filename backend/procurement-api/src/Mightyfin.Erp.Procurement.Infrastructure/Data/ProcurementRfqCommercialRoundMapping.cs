using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>P32 additive owner model. The corresponding generated migration and
/// finite database capture remain a separate release gate.</summary>
public sealed partial class ProcurementDbContext
{
    public DbSet<PrincipalRfqCommercialRoundOpeningRecord> PrincipalRfqCommercialRoundOpenings => Set<PrincipalRfqCommercialRoundOpeningRecord>();
    public DbSet<PrincipalRfqCommercialRoundVoteRecord> PrincipalRfqCommercialRoundVotes => Set<PrincipalRfqCommercialRoundVoteRecord>();
    public DbSet<PrincipalRfqCommercialRoundDecisionRecord> PrincipalRfqCommercialRoundDecisions => Set<PrincipalRfqCommercialRoundDecisionRecord>();
    public DbSet<PrincipalRfqCommercialOfferSubmissionRecord> PrincipalRfqCommercialOfferSubmissions => Set<PrincipalRfqCommercialOfferSubmissionRecord>();
    public DbSet<PrincipalRfqCommercialRoundSealRecord> PrincipalRfqCommercialRoundSeals => Set<PrincipalRfqCommercialRoundSealRecord>();
    public DbSet<PrincipalRfqCommercialCommandReceipt> PrincipalRfqCommercialCommandReceipts => Set<PrincipalRfqCommercialCommandReceipt>();

    private void ConfigureRfqCommercialRound(ModelBuilder model)
    {
        ConfigureRfqOfferOpening(model);
        ConfigureRfqCriterionAssessment(model);
        ConfigureRfqRecommendationDraft(model);
        ConfigureP32Base<PrincipalRfqCommercialRoundOpeningRecord>(model, "principal_rfq_commercial_round_openings", "round");
        ConfigureP32Base<PrincipalRfqCommercialRoundVoteRecord>(model, "principal_rfq_commercial_round_votes", "vote");
        ConfigureP32Base<PrincipalRfqCommercialRoundDecisionRecord>(model, "principal_rfq_commercial_round_decisions", "decision");
        ConfigureP32Base<PrincipalRfqCommercialOfferSubmissionRecord>(model, "principal_rfq_commercial_offer_submissions", "offer");
        ConfigureP32Base<PrincipalRfqCommercialRoundSealRecord>(model, "principal_rfq_commercial_round_seals", "seal");
        ConfigureP32Base<PrincipalRfqCommercialCommandReceipt>(model, "principal_rfq_commercial_command_receipts", "receipt");
        var openings = model.Entity<PrincipalRfqCommercialRoundOpeningRecord>();
        var votes = model.Entity<PrincipalRfqCommercialRoundVoteRecord>();
        var decisions = model.Entity<PrincipalRfqCommercialRoundDecisionRecord>();
        var offers = model.Entity<PrincipalRfqCommercialOfferSubmissionRecord>();
        var seals = model.Entity<PrincipalRfqCommercialRoundSealRecord>();
        var receipts = model.Entity<PrincipalRfqCommercialCommandReceipt>();
        openings.Property(x => x.Id).ValueGeneratedNever();
        votes.Property(x => x.Id).ValueGeneratedNever();
        decisions.Property(x => x.Id).ValueGeneratedNever();
        offers.Property(x => x.Id).ValueGeneratedNever();
        seals.Property(x => x.Id).ValueGeneratedNever();
        receipts.Property(x => x.Id).ValueGeneratedNever();
        openings.Property(x => x.PublishedPolicyIdentityStage).HasMaxLength(20);
        openings.Property(x => x.CommercialPolicyKey).HasMaxLength(60);
        openings.Property(x => x.SnapshotJson).HasColumnType("text");
        openings.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        openings.Property(x => x.ClosingOfficerPrincipalId).HasMaxLength(36);
        openings.Property(x => x.PreparedPlanSha256).HasMaxLength(64);
        openings.Property(x => x.PolicyRulesSha256).HasMaxLength(64);
        openings.Property(x => x.SnapshotSha256).HasMaxLength(64);
        votes.Property(x => x.Action).HasMaxLength(20);
        votes.Property(x => x.CaseState).HasMaxLength(20);
        votes.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        decisions.Property(x => x.State).HasMaxLength(20);
        decisions.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        decisions.Property(x => x.OpeningSha256).HasMaxLength(64);
        decisions.Property(x => x.CoreOutcomeSha256).HasMaxLength(64);
        offers.Property(x => x.RecorderPrincipalId).HasMaxLength(36);
        offers.Property(x => x.OriginalDocumentSha256).HasMaxLength(64);
        offers.Property(x => x.OriginalDocumentSelectionDigest).HasMaxLength(64);
        offers.Property(x => x.SnapshotJson).HasColumnType("text");
        offers.Property(x => x.SnapshotSha256).HasMaxLength(64);
        seals.Property(x => x.OfficerPrincipalId).HasMaxLength(36);
        seals.Property(x => x.SubmissionSetSha256).HasMaxLength(64);
        receipts.Property(x => x.Operation).HasMaxLength(24);
        receipts.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        receipts.Property(x => x.RequestSha256).HasMaxLength(64);
        receipts.Property(x => x.ResponseJson).HasColumnType("text");
        receipts.Property(x => x.ResponseSha256).HasMaxLength(64);

        LinkP32<PrincipalRfqCommercialRoundOpeningRecord, SourcingEvent>(model, nameof(PrincipalRfqCommercialRoundOpeningRecord.SourcingEventId), "fk_p32_round_event");
        LinkP32<PrincipalRfqCommercialRoundOpeningRecord, PolicyVersion>(model, nameof(PrincipalRfqCommercialRoundOpeningRecord.CommercialPolicyVersionId), "fk_p32_round_policy");
        LinkP32<PrincipalRfqCommercialRoundDecisionRecord, SourcingEvent>(model, nameof(PrincipalRfqCommercialRoundDecisionRecord.SourcingEventId), "fk_p32_decision_event");
        LinkP32<PrincipalRfqCommercialOfferSubmissionRecord, Supplier>(model, nameof(PrincipalRfqCommercialOfferSubmissionRecord.SupplierId), "fk_p32_offer_supplier");

        model.Entity<PrincipalRfqPreparedPlan>()
            .HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.SourcingEventId, x.BranchId, x.SnapshotSha256 })
            .HasName("ak_p32_prepared_round_source");
        model.Entity<OperationalPolicyIdentityRecord>()
            .HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.PolicyVersionId, x.PolicyKey, x.Stage, x.RulesSha256 })
            .HasName("ak_p32_policy_round_identity");
        openings.HasOne<PrincipalRfqPreparedPlan>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PreparedPlanId,
                x.SourcingEventId, x.BranchId, x.PreparedPlanSha256 })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.SourcingEventId, x.BranchId, x.SnapshotSha256 })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_round_prepared");
        openings.HasOne<OperationalPolicyIdentityRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId,
                x.PublishedPolicyIdentityId, x.CommercialPolicyVersionId,
                x.CommercialPolicyKey, x.PublishedPolicyIdentityStage, x.PolicyRulesSha256 })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.PolicyVersionId, x.PolicyKey, x.Stage, x.RulesSha256 })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_round_policy_identity");
        model.Entity<SupplierBid>()
            .HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.SourcingEventId, x.SupplierId }).HasName("ak_p32_bid_event_supplier");
        // InvitationId is nullable on retained legacy bids. A nonpartial unique
        // index preserves those rows; the authored P32 FK accepts only a new
        // offer's nonnull six-column exact match, never a legacy NULL match.
        model.Entity<SupplierBid>()
            .HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.SourcingEventId, x.SupplierId, x.InvitationId })
            .IsUnique().HasDatabaseName("ux_p32_bid_exact_invitation");
        model.Entity<SourcingInvitation>()
            .HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.SourcingEventId, x.SupplierId }).HasName("ak_p32_invite_event_supplier");
        offers.HasOne<SupplierBid>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierBidId,
                x.SourcingEventId, x.SupplierId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.SourcingEventId, x.SupplierId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_offer_bid_owner");
        offers.HasOne<SourcingInvitation>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.InvitationId,
                x.SourcingEventId, x.SupplierId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.SourcingEventId, x.SupplierId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_offer_invite_owner");

        // The redundant event and branch references must agree with the SAME
        // opening/decision. Separate independent FKs would permit mixed graphs.
        openings.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SourcingEventId, x.BranchId })
            .HasName("ak_p32_round_owner");
        decisions.HasOne<PrincipalRfqCommercialRoundOpeningRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SourcingEventId, x.BranchId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_decision_round_owner");
        votes.HasOne<PrincipalRfqCommercialRoundOpeningRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SourcingEventId, x.BranchId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_vote_round_owner");
        votes.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
            x.OpeningId, x.SourcingEventId, x.BranchId }).HasName("ak_p32_vote_owner");
        votes.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.OpeningId,
            x.SourcingEventId, x.BranchId, x.CoreCaseId, x.CoreDecisionId,
            x.CoreAssignmentId, x.ActorPrincipalId }).HasName("ak_p32_vote_terminal_owner");
        decisions.HasOne<PrincipalRfqCommercialRoundVoteRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.TerminalVoteId, x.OpeningId,
                x.SourcingEventId, x.BranchId, x.CoreCaseId, x.CoreDecisionId,
                x.CoreAssignmentId, x.ActorPrincipalId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.OpeningId,
                x.SourcingEventId, x.BranchId, x.CoreCaseId, x.CoreDecisionId,
                x.CoreAssignmentId, x.ActorPrincipalId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_terminal_vote_owner");
        decisions.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasName("ak_p32_decision_owner");
        decisions.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
            x.TerminalVoteId, x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasName("ak_p32_decision_vote_owner");
        offers.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
            x.OpeningId, x.SourcingEventId, x.BranchId }).HasName("ak_p32_offer_owner");
        seals.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
            x.OpeningId, x.SourcingEventId, x.BranchId }).HasName("ak_p32_seal_owner");
        offers.HasOne<PrincipalRfqCommercialRoundDecisionRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ApprovedDecisionId, x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.OpeningId, x.SourcingEventId, x.BranchId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_offer_approved_decision");
        seals.HasOne<PrincipalRfqCommercialRoundDecisionRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ApprovedDecisionId, x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.OpeningId, x.SourcingEventId, x.BranchId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_seal_approved_decision");
        receipts.HasOne<PrincipalRfqCommercialRoundOpeningRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SourcingEventId, x.BranchId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_receipt_round_owner");
        receipts.HasOne<PrincipalRfqCommercialRoundOpeningRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.OpeningTargetId,
                x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.SourcingEventId, x.BranchId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_receipt_open_target");
        receipts.HasOne<PrincipalRfqCommercialRoundDecisionRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.DecisionTargetId,
                x.VoteTargetId, x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.TerminalVoteId, x.OpeningId, x.SourcingEventId, x.BranchId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_receipt_decision_vote");
        receipts.HasOne<PrincipalRfqCommercialRoundVoteRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.VoteTargetId,
                x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.OpeningId, x.SourcingEventId, x.BranchId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_receipt_vote_target");
        receipts.HasOne<PrincipalRfqCommercialOfferSubmissionRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.OfferTargetId,
                x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.OpeningId, x.SourcingEventId, x.BranchId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_receipt_offer_target");
        receipts.HasOne<PrincipalRfqCommercialRoundSealRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SealTargetId,
                x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.OpeningId, x.SourcingEventId, x.BranchId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_receipt_seal_target");
        openings.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PreparedPlanId }).IsUnique().HasDatabaseName("ux_p32_round_plan");
        openings.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreCaseId }).IsUnique().HasDatabaseName("ux_p32_round_core_case");
        decisions.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningId }).IsUnique().HasDatabaseName("ux_p32_decision_round");
        votes.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreDecisionId }).IsUnique().HasDatabaseName("ux_p32_vote_core_decision");
        votes.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningId, x.CoreAssignmentId }).IsUnique().HasDatabaseName("ux_p32_vote_assignment");
        decisions.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreDecisionId }).IsUnique().HasDatabaseName("ux_p32_decision_core");
        offers.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningId, x.SupplierBidId }).IsUnique().HasDatabaseName("ux_p32_offer_bid");
        offers.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningId, x.SupplierId }).IsUnique().HasDatabaseName("ux_p32_offer_supplier");
        seals.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningId }).IsUnique().HasDatabaseName("ux_p32_seal_round");
        receipts.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CommandKey }).IsUnique().HasDatabaseName("ux_p32_receipt_key");
        receipts.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Operation, x.TargetId }).IsUnique().HasDatabaseName("ux_p32_receipt_target");
        const string canonical = " ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'";
        const string nz = " <> '00000000-0000-0000-0000-000000000000'::uuid";
        const string hash = " ~ '^[0-9a-f]{64}$'";
        openings.ToTable(t => t.HasCheckConstraint("ck_principal_rfq_commercial_opening", "schema_version=1 AND id"+nz+" AND commercial_policy_key='SOURCING_COMMERCIAL_ROUND' AND published_policy_identity_stage='Published' AND core_case_id=id AND event_version>0 AND sourcing_event_id"+nz+" AND prepared_plan_id"+nz+" AND commercial_policy_version_id"+nz+" AND published_policy_identity_id"+nz+" AND core_case_id"+nz+" AND branch_id"+nz+" AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id"+canonical+" AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id"+nz+") AND closing_officer_principal_id IS NOT NULL AND closing_officer_principal_id <> '00000000-0000-0000-0000-000000000000' AND closing_officer_principal_id"+canonical+" AND (closing_officer_worker_alias_id IS NULL OR closing_officer_worker_alias_id"+nz+") AND prepared_plan_sha256 IS NOT NULL AND prepared_plan_sha256"+hash+" AND policy_rules_sha256 IS NOT NULL AND policy_rules_sha256"+hash+" AND snapshot_sha256 IS NOT NULL AND snapshot_sha256"+hash+" AND opens_at<closes_at AND isfinite(opens_at) AND isfinite(closes_at)"));
        votes.ToTable(t => t.HasCheckConstraint("ck_principal_rfq_commercial_vote", "schema_version=1 AND id"+nz+" AND opening_id"+nz+" AND sourcing_event_id"+nz+" AND branch_id"+nz+" AND core_case_id=opening_id AND core_decision_id"+nz+" AND core_assignment_id"+nz+" AND action IN ('Approve','Return','Reject') AND case_state IN ('Pending','Approved','Returned','Rejected') AND case_version>0 AND assignment_version>0 AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id"+canonical+" AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id"+nz+") AND isfinite(decided_at)"));
        decisions.ToTable(t => t.HasCheckConstraint("ck_principal_rfq_commercial_decision", "schema_version=1 AND id"+nz+" AND opening_id"+nz+" AND sourcing_event_id"+nz+" AND branch_id"+nz+" AND terminal_vote_id"+nz+" AND state IN ('Approved','Returned','Rejected') AND core_case_id"+nz+" AND core_decision_id"+nz+" AND core_assignment_id"+nz+" AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id"+canonical+" AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id"+nz+") AND opening_sha256 IS NOT NULL AND opening_sha256"+hash+" AND core_outcome_sha256 IS NOT NULL AND core_outcome_sha256"+hash+" AND isfinite(decided_at)"));
        offers.ToTable(t => t.HasCheckConstraint("ck_principal_rfq_commercial_offer", "schema_version=1 AND id"+nz+" AND opening_id"+nz+" AND sourcing_event_id"+nz+" AND branch_id"+nz+" AND supplier_bid_id"+nz+" AND invitation_id"+nz+" AND supplier_id"+nz+" AND approved_decision_id"+nz+" AND draft_revision>0 AND bid_version>0 AND original_document_version_id"+nz+" AND original_document_sha256 IS NOT NULL AND original_document_sha256"+hash+" AND original_document_use_set_id"+nz+" AND original_document_selection_digest IS NOT NULL AND original_document_selection_digest"+hash+" AND recorder_principal_id IS NOT NULL AND recorder_principal_id <> '00000000-0000-0000-0000-000000000000' AND recorder_principal_id"+canonical+" AND (recorder_worker_alias_id IS NULL OR recorder_worker_alias_id"+nz+") AND snapshot_sha256 IS NOT NULL AND snapshot_sha256"+hash+" AND reported_received_at<=submitted_at AND isfinite(reported_received_at) AND isfinite(submitted_at)"));
        seals.ToTable(t => t.HasCheckConstraint("ck_principal_rfq_commercial_seal", "schema_version=1 AND id"+nz+" AND opening_id"+nz+" AND sourcing_event_id"+nz+" AND branch_id"+nz+" AND approved_decision_id"+nz+" AND officer_principal_id IS NOT NULL AND officer_principal_id <> '00000000-0000-0000-0000-000000000000' AND officer_principal_id"+canonical+" AND (officer_worker_alias_id IS NULL OR officer_worker_alias_id"+nz+") AND submission_count BETWEEN 0 AND 500 AND submission_set_sha256 IS NOT NULL AND submission_set_sha256"+hash+" AND isfinite(closed_at)"));
        receipts.ToTable(t => t.HasCheckConstraint("ck_principal_rfq_commercial_receipt",
            "schema_version=1 AND id"+nz+" AND opening_id"+nz+" AND sourcing_event_id"+nz
            +" AND branch_id"+nz+" AND command_key"+nz+" AND target_id IS NOT NULL AND target_id"+nz
            +" AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000'"
            +" AND actor_principal_id"+canonical+" AND request_sha256 IS NOT NULL AND request_sha256"+hash
            +" AND response_sha256 IS NOT NULL AND response_sha256"+hash+" AND isfinite(completed_at)"
            +" AND ((operation='Open' AND opening_target_id IS NOT NULL AND opening_target_id=target_id AND vote_target_id IS NULL"
            +" AND decision_target_id IS NULL AND offer_target_id IS NULL AND seal_target_id IS NULL AND response_status=201)"
            +" OR (operation='Decide' AND vote_target_id IS NOT NULL AND vote_target_id=target_id AND opening_target_id IS NULL"
            +" AND offer_target_id IS NULL AND seal_target_id IS NULL AND response_status=200)"
            +" OR (operation='SubmitOffer' AND offer_target_id IS NOT NULL AND offer_target_id=target_id AND opening_target_id IS NULL"
            +" AND vote_target_id IS NULL AND decision_target_id IS NULL AND seal_target_id IS NULL AND response_status=201)"
            +" OR (operation='Close' AND seal_target_id IS NOT NULL AND seal_target_id=target_id AND opening_target_id IS NULL"
            +" AND vote_target_id IS NULL AND decision_target_id IS NULL AND offer_target_id IS NULL AND response_status=200))"));
    }

    private void ConfigureP32Base<T>(ModelBuilder model, string table, string shortName)
        where T : ProcurementRecord
    {
        ConfigureBase<T>(model, table);
        model.Entity<T>().HasKey(x => x.Id).HasName("pk_p32_" + shortName);
        model.Entity<T>().HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
            .HasName("ak_p32_" + shortName + "_scope");
    }

    private static void LinkP32<TChild, TParent>(ModelBuilder model, string foreignId,
        string constraint) where TChild : ProcurementRecord where TParent : ProcurementRecord =>
        model.Entity<TChild>().HasOne<TParent>().WithMany()
            .HasForeignKey(nameof(ProcurementRecord.TenantId), nameof(ProcurementRecord.LegalEntityId), foreignId)
            .HasPrincipalKey(nameof(ProcurementRecord.TenantId), nameof(ProcurementRecord.LegalEntityId), nameof(ProcurementRecord.Id))
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName(constraint);

    private void GuardRfqCommercialRound()
    {
        GuardRfqOfferOpening();
        GuardRfqCriterionAssessment();
        GuardRfqRecommendationDraft();
        var addedReceipts = ChangeTracker.Entries<PrincipalRfqCommercialCommandReceipt>()
            .Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        var addedTargets = ChangeTracker.Entries<ProcurementRecord>()
            .Where(x => x.State == EntityState.Added && x.Entity is
                PrincipalRfqCommercialRoundOpeningRecord or PrincipalRfqCommercialRoundVoteRecord or
                PrincipalRfqCommercialRoundDecisionRecord or
                PrincipalRfqCommercialOfferSubmissionRecord or PrincipalRfqCommercialRoundSealRecord)
            .Select(x => x.Entity).ToArray();
        if (Database.IsNpgsql() && (addedTargets.Length > 0 || addedReceipts.Length > 0)
            && Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("RfqCommercialSerializableOwnerRequired");
        if (addedTargets.OfType<PrincipalRfqCommercialRoundSealRecord>().Any(seal =>
            addedTargets.OfType<PrincipalRfqCommercialOfferSubmissionRecord>()
                .Any(offer => offer.OpeningId == seal.OpeningId)))
            throw new InvalidOperationException("RfqCommercialOfferSetNotCommitted");
        foreach (var entry in ChangeTracker.Entries<ProcurementRecord>().Where(x => x.Entity is
            PrincipalRfqCommercialRoundOpeningRecord or PrincipalRfqCommercialRoundVoteRecord or
            PrincipalRfqCommercialRoundDecisionRecord or
            PrincipalRfqCommercialOfferSubmissionRecord or PrincipalRfqCommercialRoundSealRecord or
            PrincipalRfqCommercialCommandReceipt))
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("RfqCommercialEvidenceAppendOnly");
            if (entry.State != EntityState.Added) continue;
            var actor = entry.Entity switch
            {
                PrincipalRfqCommercialRoundOpeningRecord row => row.ActorPrincipalId,
                PrincipalRfqCommercialRoundVoteRecord row => row.ActorPrincipalId,
                PrincipalRfqCommercialRoundDecisionRecord row => row.ActorPrincipalId,
                PrincipalRfqCommercialOfferSubmissionRecord row => row.RecorderPrincipalId,
                PrincipalRfqCommercialRoundSealRecord row => row.OfficerPrincipalId,
                PrincipalRfqCommercialCommandReceipt row => row.ActorPrincipalId,
                _ => string.Empty,
            };
            var branch = entry.Entity switch
            {
                PrincipalRfqCommercialRoundOpeningRecord row => row.BranchId,
                PrincipalRfqCommercialRoundVoteRecord row => row.BranchId,
                PrincipalRfqCommercialRoundDecisionRecord row => row.BranchId,
                PrincipalRfqCommercialOfferSubmissionRecord row => row.BranchId,
                PrincipalRfqCommercialRoundSealRecord row => row.BranchId,
                PrincipalRfqCommercialCommandReceipt row => row.BranchId,
                _ => Guid.Empty,
            };
            if (entry.Entity.TenantId != scope.TenantId || entry.Entity.LegalEntityId != scope.LegalEntityId
                || actor != scope.SubjectId || scope.OrgUnitId is not null
                || branch == Guid.Empty || scope.WorkLocationId is { } selected && selected != branch
                || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches)
                throw new UnauthorizedAccessException("RfqCommercialEvidenceScopeMismatch");
            if (entry.Entity is PrincipalRfqCommercialCommandReceipt receipt)
            {
                if (receipt.CommandKey == Guid.Empty || receipt.TargetId is not { } targetId
                    || receipt.RequestSha256 is not { Length: 64 }
                    || receipt.ResponseSha256 != ProcurementEvidenceEncoding.Digest(receipt.ResponseJson)
                    || addedTargets.Count(target => target.Id == targetId
                        && receipt.Operation == (target switch
                        {
                            PrincipalRfqCommercialRoundOpeningRecord => "Open",
                            PrincipalRfqCommercialRoundVoteRecord => "Decide",
                            PrincipalRfqCommercialOfferSubmissionRecord => "SubmitOffer",
                            PrincipalRfqCommercialRoundSealRecord => "Close",
                            _ => string.Empty,
                        })) != 1)
                    throw new InvalidOperationException("RfqCommercialReceiptTargetRequired");
                var target = addedTargets.Single(x => x.Id == targetId);
                var targetOpening = target switch
                {
                    PrincipalRfqCommercialRoundOpeningRecord row => row.Id,
                    PrincipalRfqCommercialRoundVoteRecord row => row.OpeningId,
                    PrincipalRfqCommercialRoundDecisionRecord row => row.OpeningId,
                    PrincipalRfqCommercialOfferSubmissionRecord row => row.OpeningId,
                    PrincipalRfqCommercialRoundSealRecord row => row.OpeningId,
                    _ => Guid.Empty,
                };
                var targetEvent = target switch
                {
                    PrincipalRfqCommercialRoundOpeningRecord row => row.SourcingEventId,
                    PrincipalRfqCommercialRoundVoteRecord row => row.SourcingEventId,
                    PrincipalRfqCommercialRoundDecisionRecord row => row.SourcingEventId,
                    PrincipalRfqCommercialOfferSubmissionRecord row => row.SourcingEventId,
                    PrincipalRfqCommercialRoundSealRecord row => row.SourcingEventId,
                    _ => Guid.Empty,
                };
                var targetBranch = target switch
                {
                    PrincipalRfqCommercialRoundOpeningRecord row => row.BranchId,
                    PrincipalRfqCommercialRoundVoteRecord row => row.BranchId,
                    PrincipalRfqCommercialRoundDecisionRecord row => row.BranchId,
                    PrincipalRfqCommercialOfferSubmissionRecord row => row.BranchId,
                    PrincipalRfqCommercialRoundSealRecord row => row.BranchId,
                    _ => Guid.Empty,
                };
                if (receipt.OpeningId != targetOpening || receipt.SourcingEventId != targetEvent
                    || receipt.BranchId != targetBranch
                    || !ReceiptPinsMatch(receipt, target)
                    || (receipt.Operation switch
                    {
                        "Open" => receipt.OpeningTargetId != targetId || receipt.VoteTargetId is not null
                            || receipt.DecisionTargetId is not null
                            || receipt.OfferTargetId is not null || receipt.SealTargetId is not null,
                        "Decide" => receipt.VoteTargetId != targetId || receipt.OpeningTargetId is not null
                            || receipt.OfferTargetId is not null || receipt.SealTargetId is not null,
                        "SubmitOffer" => receipt.OfferTargetId != targetId || receipt.OpeningTargetId is not null
                            || receipt.VoteTargetId is not null || receipt.DecisionTargetId is not null
                            || receipt.SealTargetId is not null,
                        "Close" => receipt.SealTargetId != targetId || receipt.OpeningTargetId is not null
                            || receipt.VoteTargetId is not null || receipt.DecisionTargetId is not null
                            || receipt.OfferTargetId is not null,
                        _ => true,
                    }))
                    throw new InvalidOperationException("RfqCommercialReceiptTargetRequired");
                if (target is PrincipalRfqCommercialRoundVoteRecord vote)
                {
                    var terminal = addedTargets.OfType<PrincipalRfqCommercialRoundDecisionRecord>()
                        .SingleOrDefault(x => x.TerminalVoteId == vote.Id);
                    if (vote.CaseState == "Pending"
                        ? terminal is not null || receipt.DecisionTargetId is not null
                        : terminal is null || receipt.DecisionTargetId != terminal.Id
                            || terminal.State != vote.CaseState
                            || terminal.CoreDecisionId != vote.CoreDecisionId)
                        throw new InvalidOperationException("RfqCommercialVoteTerminalBindingChanged");
                }
            }
            if (entry.Entity is not (PrincipalRfqCommercialCommandReceipt or PrincipalRfqCommercialRoundDecisionRecord)
                && addedReceipts.Count(receipt => receipt.TargetId == entry.Entity.Id
                    && receipt.ActorPrincipalId == actor && receipt.BranchId == branch) != 1)
                throw new InvalidOperationException("RfqCommercialSameSaveReceiptRequired");
            if (entry.Entity is PrincipalRfqCommercialRoundDecisionRecord decision)
            {
                var vote = PrincipalRfqCommercialRoundVotes.Local.SingleOrDefault(x => x.Id == decision.TerminalVoteId);
                if (vote is null || Entry(vote).State != EntityState.Added
                    || vote.OpeningId != decision.OpeningId || vote.SourcingEventId != decision.SourcingEventId
                    || vote.BranchId != decision.BranchId || vote.CoreCaseId != decision.CoreCaseId
                    || vote.CoreDecisionId != decision.CoreDecisionId || vote.CoreAssignmentId != decision.CoreAssignmentId
                    || vote.ActorPrincipalId != decision.ActorPrincipalId || vote.CaseState != decision.State
                    || vote.DecidedAt != decision.DecidedAt
                    || addedReceipts.Count(x => x.VoteTargetId == vote.Id
                        && x.DecisionTargetId == decision.Id && x.CommandKey != Guid.Empty) != 1)
                    throw new InvalidOperationException("RfqCommercialTerminalVoteRequired");
                var decisionOpening = PrincipalRfqCommercialRoundOpenings.Local.SingleOrDefault(x => x.Id == decision.OpeningId)
                    ?? PrincipalRfqCommercialRoundOpenings.AsNoTracking().SingleOrDefault(x => x.Id == decision.OpeningId);
                if (decisionOpening is null || decisionOpening.SourcingEventId != decision.SourcingEventId
                    || decisionOpening.BranchId != decision.BranchId
                    || decisionOpening.CoreCaseId != decision.CoreCaseId
                    || decisionOpening.SnapshotSha256 != decision.OpeningSha256)
                    throw new InvalidOperationException("RfqCommercialDecisionOpeningChanged");
            }
            if (entry.Entity is PrincipalRfqCommercialRoundVoteRecord voteRecord)
            {
                var voteOpening = PrincipalRfqCommercialRoundOpenings.Local.SingleOrDefault(x => x.Id == voteRecord.OpeningId)
                    ?? PrincipalRfqCommercialRoundOpenings.AsNoTracking().SingleOrDefault(x => x.Id == voteRecord.OpeningId);
                var voteReceipt = addedReceipts.SingleOrDefault(x => x.VoteTargetId == voteRecord.Id);
                if (voteOpening is null || voteOpening.SourcingEventId != voteRecord.SourcingEventId
                    || voteOpening.BranchId != voteRecord.BranchId || voteOpening.CoreCaseId != voteRecord.CoreCaseId
                    || voteReceipt is null || voteReceipt.CommandKey == Guid.Empty
                    || sharedApprovalCore?.Database.CurrentTransaction is null
                    || sharedApprovalTransaction is null
                    || !ReferenceEquals(sharedApprovalTransaction, Database.CurrentTransaction?.GetDbTransaction())
                    || !ReferenceEquals(sharedApprovalConnection, Database.GetDbConnection())
                    || !ReferenceEquals(sharedApprovalCore.Database.GetDbConnection(), Database.GetDbConnection())
                    || !ReferenceEquals(sharedApprovalCore.Database.CurrentTransaction.GetDbTransaction(),
                        Database.CurrentTransaction?.GetDbTransaction())
                    || !ReferenceEquals(sharedApprovalCore.Database.CurrentTransaction.GetDbTransaction().Connection,
                        Database.GetDbConnection())
                    || !sharedApprovalInsertedDecisions.Contains(voteRecord.CoreDecisionId))
                    throw new InvalidOperationException("RfqCommercialCoreVoteNotEnlisted");
                // Read the saved Core CAS result, never the caller's tracked graph.
                // This also rejects an unsaved Local mutation after Core Save.
                var (coreCase, coreDecision, assignment) = CommittedCommercialVote(
                    sharedApprovalCore, voteRecord.CoreCaseId,
                    voteRecord.CoreDecisionId, voteRecord.CoreAssignmentId);
                var prepared = PrincipalRfqPreparedPlans.AsNoTracking()
                    .SingleOrDefault(x => x.Id == voteOpening.PreparedPlanId);
                if (coreDecision is null || coreCase is null || assignment is null || prepared is null
                    || coreCase.TenantId != scope.TenantId || coreCase.LegalEntityId != scope.LegalEntityId
                    || coreCase.SourceModule != "procurement"
                    || coreCase.SourceType != PrincipalRfqCommercialRoundCoreWorkflow.SourceType
                    || coreCase.SourceId != voteOpening.PreparedPlanId || coreCase.Id != voteOpening.Id
                    || coreCase.SourceVersion != prepared.DraftRevision
                    || prepared.SnapshotSha256 != voteOpening.PreparedPlanSha256
                    || coreCase.PolicyVersionId != voteOpening.CommercialPolicyVersionId
                    || coreCase.MakerSubjectId != voteOpening.ActorPrincipalId
                    || coreCase.State != voteRecord.CaseState || coreCase.Version != voteRecord.CaseVersion
                    || coreDecision.CaseId != coreCase.Id || coreDecision.AssignmentId != assignment.Id
                    || coreDecision.StageId != assignment.StageId
                    || coreDecision.TenantId != scope.TenantId || coreDecision.LegalEntityId != scope.LegalEntityId
                    || coreDecision.ActorSubjectId != voteRecord.ActorPrincipalId
                    || coreDecision.Action != voteRecord.Action
                    || coreDecision.IdempotencyKey != voteReceipt.CommandKey.ToString("D")
                    || ProcurementEvidenceEncoding.DatabaseTimestamp(coreDecision.DecidedAt) != voteRecord.DecidedAt
                    || assignment.AssigneeSubjectId != voteRecord.ActorPrincipalId
                    || assignment.CaseId != coreCase.Id
                    || assignment.PermissionKey != PrincipalRfqCommercialRoundPolicyRules.ApprovalAction
                    || assignment.Version != voteRecord.AssignmentVersion
                    || (voteRecord.CaseState == "Pending"
                        ? voteRecord.Action != "Approve" || assignment.State != "Approved"
                        : voteRecord.Action != (voteRecord.CaseState switch
                            { "Approved" => "Approve", "Returned" => "Return", "Rejected" => "Reject", _ => "Invalid" })))
                    throw new InvalidOperationException("RfqCommercialCoreVoteChanged");
            }
            if (entry.Entity is PrincipalRfqCommercialRoundOpeningRecord opening)
            {
                var prepared = PrincipalRfqPreparedPlans.AsNoTracking()
                    .SingleOrDefault(x => x.Id == opening.PreparedPlanId);
                var source = SourcingEvents.AsNoTracking()
                    .SingleOrDefault(x => x.Id == opening.SourcingEventId);
                var identity = OperationalPolicyIdentityRecords.AsNoTracking()
                    .SingleOrDefault(x => x.Id == opening.PublishedPolicyIdentityId);
                var policy = PolicyVersions.AsNoTracking()
                    .SingleOrDefault(x => x.Id == opening.CommercialPolicyVersionId);
                if (prepared is null || source is null || identity is null || policy is null
                    || prepared.SourcingEventId != opening.SourcingEventId
                    || prepared.BranchId != opening.BranchId
                    || prepared.SnapshotSha256 != opening.PreparedPlanSha256
                    || prepared.ActorPrincipalId != opening.ActorPrincipalId
                    || source.Status != "Published" || source.ExecutionVersion != 2
                    || source.BranchRef != opening.BranchId || source.Version != opening.EventVersion
                    || identity.PolicyVersionId != policy.Id || identity.Stage != "Published"
                    || opening.CommercialPolicyKey != PrincipalRfqCommercialRoundPolicyRules.PolicyKey
                    || opening.PublishedPolicyIdentityStage != "Published"
                    || identity.PolicyKey != PrincipalRfqCommercialRoundPolicyRules.PolicyKey
                    || identity.RulesSha256 != opening.PolicyRulesSha256
                    || policy.State != "Published" || policy.PolicyKey != identity.PolicyKey
                    || ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson) != opening.PolicyRulesSha256
                    || !OpeningPinsMatch(opening))
                    throw new InvalidOperationException("RfqCommercialOpeningOwnershipChanged");
                if (sharedApprovalCore?.Database.CurrentTransaction is null
                    || sharedApprovalTransaction is null
                    || !ReferenceEquals(sharedApprovalTransaction, Database.CurrentTransaction?.GetDbTransaction())
                    || !ReferenceEquals(sharedApprovalTransaction,
                        sharedApprovalCore.Database.CurrentTransaction.GetDbTransaction())
                    || !ReferenceEquals(sharedApprovalConnection, Database.GetDbConnection())
                    || !ReferenceEquals(sharedApprovalConnection, sharedApprovalCore.Database.GetDbConnection()))
                    throw new InvalidOperationException("RfqCommercialCoreOpeningNotEnlisted");
                sharedApprovalCore.ChangeTracker.DetectChanges();
                if (sharedApprovalCore.ChangeTracker.HasChanges())
                    throw new InvalidOperationException("RfqCommercialCoreOpeningNotCommitted");
                var coreCase = sharedApprovalCore.Cases.AsNoTracking()
                    .Include(x => x.Stages).ThenInclude(x => x.Assignments)
                    .SingleOrDefault(x => x.Id == opening.CoreCaseId);
                if (coreCase is null || coreCase.TenantId != opening.TenantId
                    || coreCase.LegalEntityId != opening.LegalEntityId
                    || coreCase.SourceModule != "procurement"
                    || coreCase.SourceType != PrincipalRfqCommercialRoundCoreWorkflow.SourceType
                    || coreCase.SourceId != opening.PreparedPlanId
                    || coreCase.SourceVersion != prepared.DraftRevision
                    || coreCase.PolicyVersionId != policy.Id
                    || coreCase.PolicyVersionNumber != policy.PolicyVersionNumber
                    || coreCase.MakerSubjectId != opening.ActorPrincipalId
                    || coreCase.State != "Pending" || coreCase.Stages.Count == 0)
                    throw new InvalidOperationException("RfqCommercialCoreOpeningChanged");
                using var snapshot = JsonDocument.Parse(opening.SnapshotJson);
                var pinnedSuppliers = snapshot.RootElement.GetProperty("supplierIds")
                    .EnumerateArray().Select(x => x.GetGuid()).ToArray();
                var invitations = SourcingInvitations.AsNoTracking()
                    .Where(x => x.SourcingEventId == opening.SourcingEventId)
                    .Select(x => new { x.SupplierId, x.Status }).ToArray();
                if (pinnedSuppliers.Length is < 1 or > 100
                    || pinnedSuppliers.Distinct().Count() != pinnedSuppliers.Length
                    || invitations.Length != pinnedSuppliers.Length
                    || invitations.Select(x => x.SupplierId).Distinct().Count() != invitations.Length
                    || !invitations.Select(x => x.SupplierId).ToHashSet().SetEquals(pinnedSuppliers)
                    || invitations.Any(x => x.Status is not ("ReadyForManualDelivery" or "ManualContactRecorded")))
                    throw new InvalidOperationException("RfqCommercialSupplierAudienceChanged");
                var pinnedStages = snapshot.RootElement.GetProperty("stages")
                    .EnumerateArray().ToArray();
                var currentStages = coreCase.Stages.OrderBy(x => x.Sequence).ToArray();
                if (pinnedStages.Length != currentStages.Length
                    || pinnedStages.Zip(currentStages).Any(pair =>
                    {
                        var pin = pair.First;
                        var stage = pair.Second;
                        var pins = pin.GetProperty("assignments").EnumerateArray().ToArray();
                        return pin.GetProperty("Id").GetGuid() != stage.Id
                            || pin.GetProperty("Sequence").GetInt32() != stage.Sequence
                            || pin.GetProperty("Code").GetString() != stage.Code
                            || pin.GetProperty("Mode").GetString() != stage.Mode
                            || pin.GetProperty("RequiredApprovals").GetInt32() != stage.RequiredApprovals
                            || stage.State != (stage.Sequence == 1 ? "Active" : "Waiting")
                            || pins.Length != stage.Assignments.Count
                            || pins.Any(a => !stage.Assignments.Any(current =>
                                current.Id == a.GetProperty("Id").GetGuid()
                                && current.AssigneeSubjectId == a.GetProperty("AssigneeSubjectId").GetString()
                                && current.PermissionKey == a.GetProperty("PermissionKey").GetString()
                                && current.State == "Pending"));
                    }))
                    throw new InvalidOperationException("RfqCommercialCoreOpeningChanged");
            }
            if (entry.Entity is PrincipalRfqCommercialOfferSubmissionRecord offer)
            {
                if (PrincipalRfqCommercialRoundSeals.AsNoTracking()
                    .Any(x => x.OpeningId == offer.OpeningId))
                    throw new InvalidOperationException("RfqCommercialRoundAlreadyClosed");
                var approvedOfferDecision = PrincipalRfqCommercialRoundDecisions.Local.SingleOrDefault(x => x.Id == offer.ApprovedDecisionId)
                    ?? PrincipalRfqCommercialRoundDecisions.AsNoTracking().SingleOrDefault(x => x.Id == offer.ApprovedDecisionId);
                var bid = SupplierBids.AsNoTracking().SingleOrDefault(x => x.Id == offer.SupplierBidId);
                var invitation = SourcingInvitations.AsNoTracking().SingleOrDefault(x => x.Id == offer.InvitationId);
                if (approvedOfferDecision?.State != "Approved" || approvedOfferDecision.OpeningId != offer.OpeningId
                    || approvedOfferDecision.SourcingEventId != offer.SourcingEventId || approvedOfferDecision.BranchId != offer.BranchId
                    || bid is null || bid.SourcingEventId != offer.SourcingEventId
                    || bid.SupplierId != offer.SupplierId || bid.InvitationId != offer.InvitationId
                    || invitation is null || invitation.SourcingEventId != offer.SourcingEventId
                    || invitation.SupplierId != offer.SupplierId
                    || offer.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(offer.SnapshotJson))
                    throw new InvalidOperationException("RfqCommercialApprovedRoundRequired");
                var documents = rfqDocumentOwner;
                if (documents?.Database.CurrentTransaction is null
                    || !ReferenceEquals(documents.Database.GetDbConnection(), Database.GetDbConnection())
                    || !ReferenceEquals(documents.Database.CurrentTransaction.GetDbTransaction(),
                        Database.CurrentTransaction?.GetDbTransaction())
                    || !rfqInsertedUseSets.Contains(offer.OriginalDocumentUseSetId))
                    throw new InvalidOperationException("RfqCommercialOriginalDocumentUseRequired");
                var set = documents.UseSets.Local.SingleOrDefault(x =>
                    x.Id == offer.OriginalDocumentUseSetId);
                var pins = documents.UsePins.Local.Where(x =>
                    x.UseSetId == offer.OriginalDocumentUseSetId).ToArray();
                var originalReceipt = addedReceipts.SingleOrDefault(x =>
                    x.TargetId == offer.Id && x.Operation == "SubmitOffer");
                if (set is null || originalReceipt is null || pins.Length != 1
                    || set.TenantId != scope.TenantId || set.LegalEntityId != scope.LegalEntityId
                    || set.OwnerModule != "procurement" || set.OwnerType != "sourcing_event"
                    || set.OwnerId != offer.SourcingEventId
                    || set.SourceModule != "procurement"
                    || set.SourceRecordType != "sourcing_commercial_offer"
                    || set.SourceRecordId != offer.SupplierBidId
                    || set.SourceRevision != offer.DraftRevision
                    || set.Kind != Mightyfin.Erp.Core.Documents.Domain.DocumentUseKind.Publication
                    || set.UseKey != originalReceipt.CommandKey
                    || set.CreatedBy != offer.RecorderPrincipalId
                    || set.SelectionDigest != offer.OriginalDocumentSelectionDigest
                    || set.SelectionCount != 1
                    || pins[0].DocumentId == Guid.Empty
                    || pins[0].VersionId != offer.OriginalDocumentVersionId
                    || pins[0].Sha256 != offer.OriginalDocumentSha256)
                    throw new InvalidOperationException("RfqCommercialOriginalDocumentUseChanged");
            }
            if (entry.Entity is PrincipalRfqCommercialRoundSealRecord seal)
            {
                var approvedSealDecision = PrincipalRfqCommercialRoundDecisions.Local.SingleOrDefault(x => x.Id == seal.ApprovedDecisionId)
                    ?? PrincipalRfqCommercialRoundDecisions.AsNoTracking().SingleOrDefault(x => x.Id == seal.ApprovedDecisionId);
                if (approvedSealDecision?.State != "Approved" || approvedSealDecision.OpeningId != seal.OpeningId
                    || approvedSealDecision.SourcingEventId != seal.SourcingEventId || approvedSealDecision.BranchId != seal.BranchId)
                    throw new InvalidOperationException("RfqCommercialApprovedRoundRequired");
                var offers = PrincipalRfqCommercialOfferSubmissions.AsNoTracking()
                    .Where(x => x.OpeningId == seal.OpeningId).OrderBy(x => x.SupplierBidId).ToArray();
                var exactSet = JsonSerializer.Serialize(offers.Select(x => new
                { BidId = x.SupplierBidId, x.SupplierId, x.SnapshotSha256 }));
                if (offers.Length != seal.SubmissionCount
                    || offers.Any(x => x.ApprovedDecisionId != approvedSealDecision.Id
                        || x.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(x.SnapshotJson))
                    || seal.SubmissionSetSha256 != ProcurementEvidenceEncoding.Digest(exactSet))
                    throw new InvalidOperationException("RfqCommercialSubmissionSetChanged");
            }
        }
    }

    private static bool OpeningPinsMatch(PrincipalRfqCommercialRoundOpeningRecord opening)
    {
        try
        {
            if (opening.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(opening.SnapshotJson)
                || ProcurementEvidenceEncoding.PolicyJsonDigest(opening.SnapshotJson).Length != 64)
                return false;
            using var document = JsonDocument.Parse(opening.SnapshotJson);
            var pin = document.RootElement;
            return pin.GetProperty("schemaVersion").GetInt32() == 1
                && pin.GetProperty("eventId").GetGuid() == opening.SourcingEventId
                && pin.GetProperty("preparedPlanId").GetGuid() == opening.PreparedPlanId
                && pin.GetProperty("preparedPlanSha256").GetString() == opening.PreparedPlanSha256
                && pin.GetProperty("tenantId").GetString() == opening.TenantId
                && pin.GetProperty("companyId").GetGuid() == opening.LegalEntityId
                && pin.GetProperty("branchId").GetGuid() == opening.BranchId
                && pin.GetProperty("eventVersion").GetInt64() == opening.EventVersion
                && pin.GetProperty("policyVersionId").GetGuid() == opening.CommercialPolicyVersionId
                && pin.GetProperty("policyKey").GetString() == opening.CommercialPolicyKey
                && pin.GetProperty("policyRulesSha256").GetString() == opening.PolicyRulesSha256
                && pin.GetProperty("closerPrincipalId").GetString() == opening.ClosingOfficerPrincipalId
                && (pin.GetProperty("closerWorkerAliasId").ValueKind == JsonValueKind.Null
                    ? opening.ClosingOfficerWorkerAliasId is null
                    : pin.GetProperty("closerWorkerAliasId").GetGuid() == opening.ClosingOfficerWorkerAliasId)
                && pin.GetProperty("coreCaseId").GetGuid() == opening.CoreCaseId
                && pin.GetProperty("opensAt").GetDateTimeOffset() == opening.OpensAt
                && pin.GetProperty("closesAt").GetDateTimeOffset() == opening.ClosesAt;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException) { return false; }
    }

    private static bool ReceiptPinsMatch(PrincipalRfqCommercialCommandReceipt receipt,
        ProcurementRecord target)
    {
        try
        {
            using var document = JsonDocument.Parse(receipt.ResponseJson);
            var pin = document.RootElement;
            if (pin.GetProperty("schemaVersion").GetInt32() != 1
                || pin.GetProperty("operation").GetString() != receipt.Operation
                || pin.GetProperty("commandKey").GetGuid() != receipt.CommandKey
                || pin.GetProperty("openingId").GetGuid() != receipt.OpeningId
                || pin.GetProperty("eventId").GetGuid() != receipt.SourcingEventId
                || pin.GetProperty("branchId").GetGuid() != receipt.BranchId
                || pin.GetProperty("targetId").GetGuid() != receipt.TargetId
                || pin.GetProperty("responseStatus").GetInt32() != receipt.ResponseStatus)
                return false;
            var result = pin.GetProperty("result");
            if (result.GetProperty("roundId").GetGuid() != receipt.OpeningId) return false;
            return target switch
            {
                PrincipalRfqCommercialRoundOpeningRecord opening =>
                    result.GetProperty("eventId").GetGuid() == opening.SourcingEventId
                    && result.GetProperty("coreCaseId").GetGuid() == opening.CoreCaseId
                    && result.GetProperty("snapshotSha256").GetString() == opening.SnapshotSha256
                    && result.GetProperty("state").GetString() == "PendingCommercialReview",
                PrincipalRfqCommercialRoundVoteRecord vote =>
                    result.GetProperty("voteId").GetGuid() == vote.Id
                    && result.GetProperty("coreDecisionId").GetGuid() == vote.CoreDecisionId
                    && result.GetProperty("caseState").GetString() == vote.CaseState
                    && result.GetProperty("caseVersion").GetInt64() == vote.CaseVersion
                    && result.GetProperty("assignmentVersion").GetInt64() == vote.AssignmentVersion
                    && (result.GetProperty("terminalDecisionId").ValueKind == JsonValueKind.Null
                        ? receipt.DecisionTargetId is null
                        : result.GetProperty("terminalDecisionId").GetGuid() == receipt.DecisionTargetId),
                PrincipalRfqCommercialRoundDecisionRecord decision =>
                    result.GetProperty("decisionId").GetGuid() == decision.Id
                    && result.GetProperty("coreDecisionId").GetGuid() == decision.CoreDecisionId
                    && result.GetProperty("outcomeSha256").GetString() == decision.CoreOutcomeSha256
                    && result.GetProperty("state").GetString() == decision.State,
                PrincipalRfqCommercialOfferSubmissionRecord offer =>
                    result.GetProperty("offerId").GetGuid() == offer.Id
                    && result.GetProperty("supplierId").GetGuid() == offer.SupplierId
                    && result.GetProperty("submittedAt").GetDateTimeOffset() == offer.SubmittedAt
                    && result.GetProperty("snapshotSha256").GetString() == offer.SnapshotSha256,
                PrincipalRfqCommercialRoundSealRecord seal =>
                    result.GetProperty("sealId").GetGuid() == seal.Id
                    && result.GetProperty("state").GetString() == "Closed"
                    && result.GetProperty("offerCount").GetInt32() == seal.SubmissionCount
                    && result.GetProperty("submissionSetSha256").GetString() == seal.SubmissionSetSha256,
                _ => false,
            };
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException) { return false; }
    }
}
