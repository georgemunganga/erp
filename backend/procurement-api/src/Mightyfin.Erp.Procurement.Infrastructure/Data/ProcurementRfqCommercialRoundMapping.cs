using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>P32 additive owner model. The corresponding generated migration and
/// finite database capture remain a separate release gate.</summary>
public sealed partial class ProcurementDbContext
{
    public DbSet<PrincipalRfqCommercialRoundOpeningRecord> PrincipalRfqCommercialRoundOpenings => Set<PrincipalRfqCommercialRoundOpeningRecord>();
    public DbSet<PrincipalRfqCommercialRoundDecisionRecord> PrincipalRfqCommercialRoundDecisions => Set<PrincipalRfqCommercialRoundDecisionRecord>();
    public DbSet<PrincipalRfqCommercialOfferSubmissionRecord> PrincipalRfqCommercialOfferSubmissions => Set<PrincipalRfqCommercialOfferSubmissionRecord>();
    public DbSet<PrincipalRfqCommercialRoundSealRecord> PrincipalRfqCommercialRoundSeals => Set<PrincipalRfqCommercialRoundSealRecord>();
    public DbSet<PrincipalRfqCommercialCommandReceipt> PrincipalRfqCommercialCommandReceipts => Set<PrincipalRfqCommercialCommandReceipt>();

    private void ConfigureRfqCommercialRound(ModelBuilder model)
    {
        ConfigureP32Base<PrincipalRfqCommercialRoundOpeningRecord>(model, "principal_rfq_commercial_round_openings", "round");
        ConfigureP32Base<PrincipalRfqCommercialRoundDecisionRecord>(model, "principal_rfq_commercial_round_decisions", "decision");
        ConfigureP32Base<PrincipalRfqCommercialOfferSubmissionRecord>(model, "principal_rfq_commercial_offer_submissions", "offer");
        ConfigureP32Base<PrincipalRfqCommercialRoundSealRecord>(model, "principal_rfq_commercial_round_seals", "seal");
        ConfigureP32Base<PrincipalRfqCommercialCommandReceipt>(model, "principal_rfq_commercial_command_receipts", "receipt");
        var openings = model.Entity<PrincipalRfqCommercialRoundOpeningRecord>();
        var decisions = model.Entity<PrincipalRfqCommercialRoundDecisionRecord>();
        var offers = model.Entity<PrincipalRfqCommercialOfferSubmissionRecord>();
        var seals = model.Entity<PrincipalRfqCommercialRoundSealRecord>();
        var receipts = model.Entity<PrincipalRfqCommercialCommandReceipt>();
        openings.Property(x => x.Id).ValueGeneratedNever();
        decisions.Property(x => x.Id).ValueGeneratedNever();
        offers.Property(x => x.Id).ValueGeneratedNever();
        seals.Property(x => x.Id).ValueGeneratedNever();
        receipts.Property(x => x.Id).ValueGeneratedNever();
        openings.Property(x => x.PublishedPolicyIdentityStage).HasMaxLength(20);
        openings.Property(x => x.CommercialPolicyKey).HasMaxLength(60);
        openings.Property(x => x.SnapshotJson).HasColumnType("text");
        openings.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        openings.Property(x => x.PreparedPlanSha256).HasMaxLength(64);
        openings.Property(x => x.PolicyRulesSha256).HasMaxLength(64);
        openings.Property(x => x.SnapshotSha256).HasMaxLength(64);
        decisions.Property(x => x.State).HasMaxLength(20);
        decisions.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        decisions.Property(x => x.OpeningSha256).HasMaxLength(64);
        decisions.Property(x => x.CoreOutcomeSha256).HasMaxLength(64);
        offers.Property(x => x.RecorderPrincipalId).HasMaxLength(36);
        offers.Property(x => x.OriginalDocumentSha256).HasMaxLength(64);
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
        decisions.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasName("ak_p32_decision_owner");
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
                x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.OpeningId, x.SourcingEventId, x.BranchId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_receipt_decision_target");
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
        decisions.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreDecisionId }).IsUnique().HasDatabaseName("ux_p32_decision_core");
        offers.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningId, x.SupplierBidId }).IsUnique().HasDatabaseName("ux_p32_offer_bid");
        offers.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningId, x.SupplierId }).IsUnique().HasDatabaseName("ux_p32_offer_supplier");
        seals.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningId }).IsUnique().HasDatabaseName("ux_p32_seal_round");
        receipts.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CommandKey }).IsUnique().HasDatabaseName("ux_p32_receipt_key");
        receipts.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Operation, x.TargetId }).IsUnique().HasDatabaseName("ux_p32_receipt_target");
        const string canonical = " ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'";
        const string nz = " <> '00000000-0000-0000-0000-000000000000'::uuid";
        const string hash = " ~ '^[0-9a-f]{64}$'";
        openings.ToTable(t => t.HasCheckConstraint("ck_principal_rfq_commercial_opening", "schema_version=1 AND id"+nz+" AND commercial_policy_key='SOURCING_COMMERCIAL_ROUND' AND published_policy_identity_stage='Published' AND core_case_id=id AND event_version>0 AND sourcing_event_id"+nz+" AND prepared_plan_id"+nz+" AND commercial_policy_version_id"+nz+" AND published_policy_identity_id"+nz+" AND core_case_id"+nz+" AND branch_id"+nz+" AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id"+canonical+" AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id"+nz+") AND prepared_plan_sha256 IS NOT NULL AND prepared_plan_sha256"+hash+" AND policy_rules_sha256 IS NOT NULL AND policy_rules_sha256"+hash+" AND snapshot_sha256 IS NOT NULL AND snapshot_sha256"+hash+" AND opens_at<closes_at AND isfinite(opens_at) AND isfinite(closes_at)"));
        decisions.ToTable(t => t.HasCheckConstraint("ck_principal_rfq_commercial_decision", "schema_version=1 AND id"+nz+" AND opening_id"+nz+" AND sourcing_event_id"+nz+" AND branch_id"+nz+" AND state IN ('Approved','Returned','Rejected') AND core_case_id"+nz+" AND core_decision_id"+nz+" AND core_assignment_id"+nz+" AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id"+canonical+" AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id"+nz+") AND opening_sha256 IS NOT NULL AND opening_sha256"+hash+" AND core_outcome_sha256 IS NOT NULL AND core_outcome_sha256"+hash+" AND isfinite(decided_at)"));
        offers.ToTable(t => t.HasCheckConstraint("ck_principal_rfq_commercial_offer", "schema_version=1 AND id"+nz+" AND opening_id"+nz+" AND sourcing_event_id"+nz+" AND branch_id"+nz+" AND supplier_bid_id"+nz+" AND invitation_id"+nz+" AND supplier_id"+nz+" AND approved_decision_id"+nz+" AND draft_revision>0 AND bid_version>0 AND original_document_version_id"+nz+" AND original_document_sha256 IS NOT NULL AND original_document_sha256"+hash+" AND recorder_principal_id IS NOT NULL AND recorder_principal_id <> '00000000-0000-0000-0000-000000000000' AND recorder_principal_id"+canonical+" AND (recorder_worker_alias_id IS NULL OR recorder_worker_alias_id"+nz+") AND snapshot_sha256 IS NOT NULL AND snapshot_sha256"+hash+" AND reported_received_at<=submitted_at AND isfinite(reported_received_at) AND isfinite(submitted_at)"));
        seals.ToTable(t => t.HasCheckConstraint("ck_principal_rfq_commercial_seal", "schema_version=1 AND id"+nz+" AND opening_id"+nz+" AND sourcing_event_id"+nz+" AND branch_id"+nz+" AND approved_decision_id"+nz+" AND officer_principal_id IS NOT NULL AND officer_principal_id <> '00000000-0000-0000-0000-000000000000' AND officer_principal_id"+canonical+" AND (officer_worker_alias_id IS NULL OR officer_worker_alias_id"+nz+") AND submission_count BETWEEN 0 AND 500 AND submission_set_sha256 IS NOT NULL AND submission_set_sha256"+hash+" AND isfinite(closed_at)"));
        receipts.ToTable(t => t.HasCheckConstraint("ck_principal_rfq_commercial_receipt", "schema_version=1 AND id"+nz+" AND opening_id"+nz+" AND sourcing_event_id"+nz+" AND branch_id"+nz+" AND command_key"+nz+" AND target_id IS NOT NULL AND target_id"+nz+" AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id"+canonical+" AND request_sha256 IS NOT NULL AND request_sha256"+hash+" AND response_sha256 IS NOT NULL AND response_sha256"+hash+" AND isfinite(completed_at) AND ((operation='Open' AND opening_target_id IS NOT NULL AND opening_target_id=target_id AND decision_target_id IS NULL AND offer_target_id IS NULL AND seal_target_id IS NULL AND response_status=201) OR (operation='Decide' AND decision_target_id IS NOT NULL AND decision_target_id=target_id AND opening_target_id IS NULL AND offer_target_id IS NULL AND seal_target_id IS NULL AND response_status=200) OR (operation='SubmitOffer' AND offer_target_id IS NOT NULL AND offer_target_id=target_id AND opening_target_id IS NULL AND decision_target_id IS NULL AND seal_target_id IS NULL AND response_status=201) OR (operation='Close' AND seal_target_id IS NOT NULL AND seal_target_id=target_id AND opening_target_id IS NULL AND decision_target_id IS NULL AND offer_target_id IS NULL AND response_status=200))"));
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
        var addedReceipts = ChangeTracker.Entries<PrincipalRfqCommercialCommandReceipt>()
            .Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        var addedTargets = ChangeTracker.Entries<ProcurementRecord>()
            .Where(x => x.State == EntityState.Added && x.Entity is
                PrincipalRfqCommercialRoundOpeningRecord or PrincipalRfqCommercialRoundDecisionRecord or
                PrincipalRfqCommercialOfferSubmissionRecord or PrincipalRfqCommercialRoundSealRecord)
            .Select(x => x.Entity).ToArray();
        foreach (var entry in ChangeTracker.Entries<ProcurementRecord>().Where(x => x.Entity is
            PrincipalRfqCommercialRoundOpeningRecord or PrincipalRfqCommercialRoundDecisionRecord or
            PrincipalRfqCommercialOfferSubmissionRecord or PrincipalRfqCommercialRoundSealRecord or
            PrincipalRfqCommercialCommandReceipt))
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("RfqCommercialEvidenceAppendOnly");
            if (entry.State != EntityState.Added) continue;
            var actor = entry.Entity switch
            {
                PrincipalRfqCommercialRoundOpeningRecord row => row.ActorPrincipalId,
                PrincipalRfqCommercialRoundDecisionRecord row => row.ActorPrincipalId,
                PrincipalRfqCommercialOfferSubmissionRecord row => row.RecorderPrincipalId,
                PrincipalRfqCommercialRoundSealRecord row => row.OfficerPrincipalId,
                PrincipalRfqCommercialCommandReceipt row => row.ActorPrincipalId,
                _ => string.Empty,
            };
            var branch = entry.Entity switch
            {
                PrincipalRfqCommercialRoundOpeningRecord row => row.BranchId,
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
                            PrincipalRfqCommercialRoundDecisionRecord => "Decide",
                            PrincipalRfqCommercialOfferSubmissionRecord => "SubmitOffer",
                            PrincipalRfqCommercialRoundSealRecord => "Close",
                            _ => string.Empty,
                        })) != 1)
                    throw new InvalidOperationException("RfqCommercialReceiptTargetRequired");
                var target = addedTargets.Single(x => x.Id == targetId);
                var targetOpening = target switch
                {
                    PrincipalRfqCommercialRoundOpeningRecord row => row.Id,
                    PrincipalRfqCommercialRoundDecisionRecord row => row.OpeningId,
                    PrincipalRfqCommercialOfferSubmissionRecord row => row.OpeningId,
                    PrincipalRfqCommercialRoundSealRecord row => row.OpeningId,
                    _ => Guid.Empty,
                };
                var targetEvent = target switch
                {
                    PrincipalRfqCommercialRoundOpeningRecord row => row.SourcingEventId,
                    PrincipalRfqCommercialRoundDecisionRecord row => row.SourcingEventId,
                    PrincipalRfqCommercialOfferSubmissionRecord row => row.SourcingEventId,
                    PrincipalRfqCommercialRoundSealRecord row => row.SourcingEventId,
                    _ => Guid.Empty,
                };
                var targetBranch = target switch
                {
                    PrincipalRfqCommercialRoundOpeningRecord row => row.BranchId,
                    PrincipalRfqCommercialRoundDecisionRecord row => row.BranchId,
                    PrincipalRfqCommercialOfferSubmissionRecord row => row.BranchId,
                    PrincipalRfqCommercialRoundSealRecord row => row.BranchId,
                    _ => Guid.Empty,
                };
                if (receipt.OpeningId != targetOpening || receipt.SourcingEventId != targetEvent
                    || receipt.BranchId != targetBranch
                    || (receipt.Operation switch
                    {
                        "Open" => receipt.OpeningTargetId != targetId || receipt.DecisionTargetId is not null
                            || receipt.OfferTargetId is not null || receipt.SealTargetId is not null,
                        "Decide" => receipt.DecisionTargetId != targetId || receipt.OpeningTargetId is not null
                            || receipt.OfferTargetId is not null || receipt.SealTargetId is not null,
                        "SubmitOffer" => receipt.OfferTargetId != targetId || receipt.OpeningTargetId is not null
                            || receipt.DecisionTargetId is not null || receipt.SealTargetId is not null,
                        "Close" => receipt.SealTargetId != targetId || receipt.OpeningTargetId is not null
                            || receipt.DecisionTargetId is not null || receipt.OfferTargetId is not null,
                        _ => true,
                    }))
                    throw new InvalidOperationException("RfqCommercialReceiptTargetRequired");
            }
            else if (addedReceipts.Count(receipt => receipt.TargetId == entry.Entity.Id
                && receipt.ActorPrincipalId == actor && receipt.BranchId == branch) != 1)
                throw new InvalidOperationException("RfqCommercialSameSaveReceiptRequired");
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
            }
            if (entry.Entity is PrincipalRfqCommercialOfferSubmissionRecord offer)
            {
                var decision = PrincipalRfqCommercialRoundDecisions.Local.SingleOrDefault(x => x.Id == offer.ApprovedDecisionId)
                    ?? PrincipalRfqCommercialRoundDecisions.AsNoTracking().SingleOrDefault(x => x.Id == offer.ApprovedDecisionId);
                var bid = SupplierBids.AsNoTracking().SingleOrDefault(x => x.Id == offer.SupplierBidId);
                var invitation = SourcingInvitations.AsNoTracking().SingleOrDefault(x => x.Id == offer.InvitationId);
                if (decision?.State != "Approved" || decision.OpeningId != offer.OpeningId
                    || decision.SourcingEventId != offer.SourcingEventId || decision.BranchId != offer.BranchId
                    || bid is null || bid.SourcingEventId != offer.SourcingEventId
                    || bid.SupplierId != offer.SupplierId || bid.InvitationId != offer.InvitationId
                    || invitation is null || invitation.SourcingEventId != offer.SourcingEventId
                    || invitation.SupplierId != offer.SupplierId
                    || offer.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(offer.SnapshotJson))
                    throw new InvalidOperationException("RfqCommercialApprovedRoundRequired");
            }
            if (entry.Entity is PrincipalRfqCommercialRoundSealRecord seal)
            {
                var decision = PrincipalRfqCommercialRoundDecisions.Local.SingleOrDefault(x => x.Id == seal.ApprovedDecisionId)
                    ?? PrincipalRfqCommercialRoundDecisions.AsNoTracking().SingleOrDefault(x => x.Id == seal.ApprovedDecisionId);
                if (decision?.State != "Approved" || decision.OpeningId != seal.OpeningId
                    || decision.SourcingEventId != seal.SourcingEventId || decision.BranchId != seal.BranchId)
                    throw new InvalidOperationException("RfqCommercialApprovedRoundRequired");
            }
        }
    }

    private static bool OpeningPinsMatch(PrincipalRfqCommercialRoundOpeningRecord opening)
    {
        try
        {
            if (opening.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(opening.SnapshotJson))
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
                && pin.GetProperty("coreCaseId").GetGuid() == opening.CoreCaseId
                && pin.GetProperty("opensAt").GetDateTimeOffset() == opening.OpensAt
                && pin.GetProperty("closesAt").GetDateTimeOffset() == opening.ClosesAt;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException) { return false; }
    }
}
