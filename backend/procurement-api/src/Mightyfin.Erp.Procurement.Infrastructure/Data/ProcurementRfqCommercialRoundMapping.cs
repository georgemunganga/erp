using Microsoft.EntityFrameworkCore;
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
        ConfigureBase<PrincipalRfqCommercialRoundOpeningRecord>(model, "principal_rfq_commercial_round_openings");
        ConfigureBase<PrincipalRfqCommercialRoundDecisionRecord>(model, "principal_rfq_commercial_round_decisions");
        ConfigureBase<PrincipalRfqCommercialOfferSubmissionRecord>(model, "principal_rfq_commercial_offer_submissions");
        ConfigureBase<PrincipalRfqCommercialRoundSealRecord>(model, "principal_rfq_commercial_round_seals");
        ConfigureBase<PrincipalRfqCommercialCommandReceipt>(model, "principal_rfq_commercial_command_receipts");
        var openings = model.Entity<PrincipalRfqCommercialRoundOpeningRecord>();
        var decisions = model.Entity<PrincipalRfqCommercialRoundDecisionRecord>();
        var offers = model.Entity<PrincipalRfqCommercialOfferSubmissionRecord>();
        var seals = model.Entity<PrincipalRfqCommercialRoundSealRecord>();
        var receipts = model.Entity<PrincipalRfqCommercialCommandReceipt>();
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

        Link<PrincipalRfqCommercialRoundOpeningRecord, SourcingEvent>(model, nameof(PrincipalRfqCommercialRoundOpeningRecord.SourcingEventId));
        Link<PrincipalRfqCommercialRoundOpeningRecord, PrincipalRfqPreparedPlan>(model, nameof(PrincipalRfqCommercialRoundOpeningRecord.PreparedPlanId));
        Link<PrincipalRfqCommercialRoundOpeningRecord, PolicyVersion>(model, nameof(PrincipalRfqCommercialRoundOpeningRecord.CommercialPolicyVersionId));
        Link<PrincipalRfqCommercialRoundOpeningRecord, OperationalPolicyIdentityRecord>(model, nameof(PrincipalRfqCommercialRoundOpeningRecord.PublishedPolicyIdentityId));
        Link<PrincipalRfqCommercialRoundDecisionRecord, SourcingEvent>(model, nameof(PrincipalRfqCommercialRoundDecisionRecord.SourcingEventId));
        Link<PrincipalRfqCommercialOfferSubmissionRecord, SupplierBid>(model, nameof(PrincipalRfqCommercialOfferSubmissionRecord.SupplierBidId));
        Link<PrincipalRfqCommercialOfferSubmissionRecord, SourcingInvitation>(model, nameof(PrincipalRfqCommercialOfferSubmissionRecord.InvitationId));
        Link<PrincipalRfqCommercialOfferSubmissionRecord, Supplier>(model, nameof(PrincipalRfqCommercialOfferSubmissionRecord.SupplierId));

        // The redundant event and branch references must agree with the SAME
        // opening/decision. Separate independent FKs would permit mixed graphs.
        openings.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SourcingEventId, x.BranchId });
        decisions.HasOne<PrincipalRfqCommercialRoundOpeningRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SourcingEventId, x.BranchId })
            .OnDelete(DeleteBehavior.Restrict);
        decisions.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.OpeningId, x.SourcingEventId, x.BranchId });
        offers.HasOne<PrincipalRfqCommercialRoundDecisionRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ApprovedDecisionId, x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.OpeningId, x.SourcingEventId, x.BranchId })
            .OnDelete(DeleteBehavior.Restrict);
        seals.HasOne<PrincipalRfqCommercialRoundDecisionRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ApprovedDecisionId, x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.OpeningId, x.SourcingEventId, x.BranchId })
            .OnDelete(DeleteBehavior.Restrict);
        receipts.HasOne<PrincipalRfqCommercialRoundOpeningRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.OpeningId, x.SourcingEventId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SourcingEventId, x.BranchId })
            .OnDelete(DeleteBehavior.Restrict);
        openings.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PreparedPlanId }).IsUnique();
        openings.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreCaseId }).IsUnique();
        decisions.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningId }).IsUnique();
        decisions.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreDecisionId }).IsUnique();
        offers.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningId, x.SupplierBidId }).IsUnique();
        offers.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningId, x.SupplierId }).IsUnique();
        seals.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningId }).IsUnique();
        receipts.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CommandKey }).IsUnique();
        receipts.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Operation, x.TargetId }).IsUnique();
        const string canonical = " ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'";
        const string nz = " <> '00000000-0000-0000-0000-000000000000'::uuid";
        const string hash = " ~ '^[0-9a-f]{64}$'";
        openings.ToTable(t => t.HasCheckConstraint("ck_principal_rfq_commercial_opening", "schema_version=1 AND event_version>0 AND sourcing_event_id"+nz+" AND prepared_plan_id"+nz+" AND commercial_policy_version_id"+nz+" AND published_policy_identity_id"+nz+" AND core_case_id"+nz+" AND branch_id"+nz+" AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id"+canonical+" AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id"+nz+") AND prepared_plan_sha256 IS NOT NULL AND prepared_plan_sha256"+hash+" AND policy_rules_sha256 IS NOT NULL AND policy_rules_sha256"+hash+" AND snapshot_sha256 IS NOT NULL AND snapshot_sha256"+hash+" AND opens_at<closes_at AND isfinite(opens_at) AND isfinite(closes_at)"));
        decisions.ToTable(t => t.HasCheckConstraint("ck_principal_rfq_commercial_decision", "schema_version=1 AND state IN ('Approved','Returned','Rejected') AND core_case_id"+nz+" AND core_decision_id"+nz+" AND core_assignment_id"+nz+" AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id"+canonical+" AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id"+nz+") AND opening_sha256 IS NOT NULL AND opening_sha256"+hash+" AND core_outcome_sha256 IS NOT NULL AND core_outcome_sha256"+hash+" AND isfinite(decided_at)"));
        offers.ToTable(t => t.HasCheckConstraint("ck_principal_rfq_commercial_offer", "schema_version=1 AND supplier_bid_id"+nz+" AND invitation_id"+nz+" AND supplier_id"+nz+" AND approved_decision_id"+nz+" AND draft_revision>0 AND bid_version>0 AND original_document_version_id"+nz+" AND original_document_sha256 IS NOT NULL AND original_document_sha256"+hash+" AND recorder_principal_id IS NOT NULL AND recorder_principal_id <> '00000000-0000-0000-0000-000000000000' AND recorder_principal_id"+canonical+" AND (recorder_worker_alias_id IS NULL OR recorder_worker_alias_id"+nz+") AND snapshot_sha256 IS NOT NULL AND snapshot_sha256"+hash+" AND reported_received_at<=submitted_at AND isfinite(reported_received_at) AND isfinite(submitted_at)"));
        seals.ToTable(t => t.HasCheckConstraint("ck_principal_rfq_commercial_seal", "schema_version=1 AND approved_decision_id"+nz+" AND officer_principal_id IS NOT NULL AND officer_principal_id <> '00000000-0000-0000-0000-000000000000' AND officer_principal_id"+canonical+" AND (officer_worker_alias_id IS NULL OR officer_worker_alias_id"+nz+") AND submission_count BETWEEN 0 AND 500 AND submission_set_sha256 IS NOT NULL AND submission_set_sha256"+hash+" AND isfinite(closed_at)"));
        receipts.ToTable(t => t.HasCheckConstraint("ck_principal_rfq_commercial_receipt", "schema_version=1 AND operation IN ('Open','Decide','SubmitOffer','Close') AND command_key"+nz+" AND target_id IS NOT NULL AND target_id"+nz+" AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id"+canonical+" AND request_sha256 IS NOT NULL AND request_sha256"+hash+" AND response_sha256 IS NOT NULL AND response_sha256"+hash+" AND response_status IN (200,201) AND isfinite(completed_at)"));
    }

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
            }
            else if (addedReceipts.Count(receipt => receipt.TargetId == entry.Entity.Id
                && receipt.ActorPrincipalId == actor && receipt.BranchId == branch) != 1)
                throw new InvalidOperationException("RfqCommercialSameSaveReceiptRequired");
            if (entry.Entity is PrincipalRfqCommercialOfferSubmissionRecord offer)
            {
                var decision = PrincipalRfqCommercialRoundDecisions.Local.SingleOrDefault(x => x.Id == offer.ApprovedDecisionId)
                    ?? PrincipalRfqCommercialRoundDecisions.AsNoTracking().SingleOrDefault(x => x.Id == offer.ApprovedDecisionId);
                if (decision?.State != "Approved" || decision.OpeningId != offer.OpeningId
                    || decision.SourcingEventId != offer.SourcingEventId || decision.BranchId != offer.BranchId
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
}
