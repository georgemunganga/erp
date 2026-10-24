using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<PrincipalRfqOfferOpeningRecord> PrincipalRfqOfferOpenings => Set<PrincipalRfqOfferOpeningRecord>();
    public DbSet<PrincipalRfqOfferOpeningReceipt> PrincipalRfqOfferOpeningReceipts => Set<PrincipalRfqOfferOpeningReceipt>();

    private void ConfigureRfqOfferOpening(ModelBuilder model)
    {
        ConfigureP32Base<PrincipalRfqOfferOpeningRecord>(model, "principal_rfq_commercial_offer_openings", "unseal");
        ConfigureP32Base<PrincipalRfqOfferOpeningReceipt>(model, "principal_rfq_commercial_offer_opening_receipts", "unseal_receipt");
        var row = model.Entity<PrincipalRfqOfferOpeningRecord>();
        var receipt = model.Entity<PrincipalRfqOfferOpeningReceipt>();
        row.Property(x => x.Id).ValueGeneratedNever(); receipt.Property(x => x.Id).ValueGeneratedNever();
        row.Property(x => x.OfficerPrincipalId).HasMaxLength(36);
        receipt.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        row.Property(x => x.Reason).HasMaxLength(1000); row.Property(x => x.Reference).HasMaxLength(200);
        row.Property(x => x.SnapshotJson).HasColumnType("text"); receipt.Property(x => x.ResponseJson).HasColumnType("text");
        foreach (var name in new[] { "PolicyRulesSha256", "ApprovedRoundSha256", "RequestSha256", "OfferSetSha256", "SnapshotSha256" })
            row.Property<string>(name).HasMaxLength(64);
        receipt.Property(x => x.RequestSha256).HasMaxLength(64); receipt.Property(x => x.ResponseSha256).HasMaxLength(64);
        model.Entity<PrincipalRfqCommercialRoundOpeningRecord>().HasAlternateKey(x => new
        { x.TenantId, x.LegalEntityId, x.Id, x.SourcingEventId, x.BranchId,
            x.CommercialPolicyVersionId, x.PublishedPolicyIdentityId, x.PolicyRulesSha256, x.SnapshotSha256 })
            .HasName("ak_p32_round_open_policy");
        row.HasOne<PrincipalRfqCommercialRoundOpeningRecord>().WithMany().HasForeignKey(x => new
        { x.TenantId, x.LegalEntityId, x.OpeningId, x.SourcingEventId, x.BranchId,
            x.PolicyVersionId, x.PublishedPolicyIdentityId, x.PolicyRulesSha256, x.ApprovedRoundSha256 })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SourcingEventId, x.BranchId,
                x.CommercialPolicyVersionId, x.PublishedPolicyIdentityId, x.PolicyRulesSha256, x.SnapshotSha256 })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_unseal_round_policy");
        model.Entity<PrincipalRfqCommercialRoundSealRecord>().HasAlternateKey(x => new
        { x.TenantId, x.LegalEntityId, x.Id, x.OpeningId, x.SourcingEventId, x.BranchId,
            x.SubmissionCount, x.SubmissionSetSha256 }).HasName("ak_p32_seal_set");
        row.HasOne<PrincipalRfqCommercialRoundSealRecord>().WithMany().HasForeignKey(x => new
        { x.TenantId, x.LegalEntityId, x.SealId, x.OpeningId, x.SourcingEventId, x.BranchId,
            x.OfferCount, x.OfferSetSha256 }).HasPrincipalKey(x => new
        { x.TenantId, x.LegalEntityId, x.Id, x.OpeningId, x.SourcingEventId, x.BranchId,
            x.SubmissionCount, x.SubmissionSetSha256 })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_unseal_exact_seal");
        row.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.OpeningId,
            x.SourcingEventId, x.BranchId, x.OfficerPrincipalId, x.CommandKey, x.RequestSha256 })
            .HasName("ak_p32_unseal_command");
        receipt.HasOne<PrincipalRfqOfferOpeningRecord>().WithMany().HasForeignKey(x => new
        { x.TenantId, x.LegalEntityId, x.UnsealingId, x.OpeningId, x.SourcingEventId, x.BranchId,
            x.ActorPrincipalId, x.CommandKey, x.RequestSha256 }).HasPrincipalKey(x => new
        { x.TenantId, x.LegalEntityId, x.Id, x.OpeningId, x.SourcingEventId, x.BranchId,
            x.OfficerPrincipalId, x.CommandKey, x.RequestSha256 })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p32_unseal_receipt_command");
        row.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningId, x.SourcingEventId, x.BranchId,
            x.PolicyVersionId, x.PublishedPolicyIdentityId, x.PolicyRulesSha256, x.ApprovedRoundSha256 })
            .HasDatabaseName("ix_p32_unseal_round_policy");
        row.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SealId, x.OpeningId, x.SourcingEventId,
            x.BranchId, x.OfferCount, x.OfferSetSha256 }).HasDatabaseName("ix_p32_unseal_exact_seal");
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.UnsealingId, x.OpeningId,
            x.SourcingEventId, x.BranchId, x.ActorPrincipalId, x.CommandKey, x.RequestSha256 })
            .HasDatabaseName("ix_p32_unseal_receipt_command");
        row.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.OpeningId }).IsUnique().HasDatabaseName("ux_p32_unseal_round");
        row.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CommandKey }).IsUnique().HasDatabaseName("ux_p32_unseal_key");
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.UnsealingId }).IsUnique().HasDatabaseName("ux_p32_unseal_receipt_target");
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CommandKey }).IsUnique().HasDatabaseName("ux_p32_unseal_receipt_key");
        const string nz = " <> '00000000-0000-0000-0000-000000000000'::uuid";
        const string actor = " IS NOT NULL AND ";
        const string canonical = " ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'";
        const string sha = " ~ '^[0-9a-f]{64}$'";
        row.ToTable(t => t.HasCheckConstraint("ck_p32_unseal", "schema_version=1 AND id" + nz
            + " AND opening_id" + nz + " AND seal_id" + nz + " AND sourcing_event_id" + nz + " AND branch_id" + nz
            + " AND policy_version_id" + nz + " AND published_policy_identity_id" + nz + " AND command_key" + nz
            + " AND officer_principal_id" + actor + "officer_principal_id" + canonical
            + " AND officer_principal_id<>'00000000-0000-0000-0000-000000000000'"
            + " AND (officer_worker_alias_id IS NULL OR officer_worker_alias_id" + nz + ") AND offer_count BETWEEN 0 AND 500"
            + " AND policy_rules_sha256 IS NOT NULL AND policy_rules_sha256" + sha
            + " AND approved_round_sha256 IS NOT NULL AND approved_round_sha256" + sha
            + " AND request_sha256 IS NOT NULL AND request_sha256" + sha
            + " AND offer_set_sha256 IS NOT NULL AND offer_set_sha256" + sha
            + " AND snapshot_sha256 IS NOT NULL AND snapshot_sha256" + sha
            + " AND snapshot_json IS NOT NULL AND snapshot_sha256=encode(sha256(convert_to(snapshot_json,'UTF8')),'hex')"
            + " AND reason IS NOT NULL AND reference IS NOT NULL AND isfinite(opened_at)"));
        receipt.ToTable(t => t.HasCheckConstraint("ck_p32_unseal_receipt", "schema_version=1 AND id" + nz
            + " AND unsealing_id" + nz + " AND opening_id" + nz + " AND sourcing_event_id" + nz + " AND branch_id" + nz
            + " AND command_key" + nz + " AND response_status=200 AND actor_principal_id IS NOT NULL"
            + " AND actor_principal_id" + canonical + " AND actor_principal_id<>'00000000-0000-0000-0000-000000000000'"
            + " AND request_sha256 IS NOT NULL AND request_sha256" + sha
            + " AND response_sha256 IS NOT NULL AND response_sha256" + sha
            + " AND response_json IS NOT NULL AND response_sha256=encode(sha256(convert_to(response_json,'UTF8')),'hex')"));
    }

    private void GuardRfqOfferOpening()
    {
        var changed = ChangeTracker.Entries<ProcurementRecord>().Where(x => x.Entity is
            PrincipalRfqOfferOpeningRecord or PrincipalRfqOfferOpeningReceipt).ToArray();
        foreach (var entry in changed)
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached) continue;
            if (entry.Entity.TenantId != scope.TenantId || entry.Entity.LegalEntityId != scope.LegalEntityId)
                throw new InvalidOperationException("RfqOfferOpeningScopeMismatch");
            if (entry.State != EntityState.Added)
                throw new InvalidOperationException("RfqOfferOpeningEvidenceAppendOnly");
        }
        var rows = changed.Where(x => x.State == EntityState.Added).Select(x => x.Entity)
            .OfType<PrincipalRfqOfferOpeningRecord>().ToArray();
        var receipts = changed.Where(x => x.State == EntityState.Added).Select(x => x.Entity)
            .OfType<PrincipalRfqOfferOpeningReceipt>().ToArray();
        if (rows.Length == 0 && receipts.Length == 0) return;
        if (Database.IsNpgsql() && Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("RfqOfferOpeningOwnerTransactionRequired");
        if (rows.Length != receipts.Length) throw new InvalidOperationException("RfqOfferOpeningSameSaveReceiptRequired");
        foreach (var row in rows)
        {
            if (row.OfficerPrincipalId != scope.SubjectId || row.OfficerWorkerAliasId != scope.CoreLinkedWorkerId
                || receipts.Count(x => x.UnsealingId == row.Id) != 1)
                throw new InvalidOperationException("RfqOfferOpeningSameSaveActorRequired");
            if (ChangeTracker.Entries<PrincipalRfqCommercialOfferSubmissionRecord>().Any(x =>
                    x.Entity.OpeningId == row.OpeningId && x.State != EntityState.Unchanged)
                || ChangeTracker.Entries<PrincipalRfqCommercialRoundSealRecord>().Any(x =>
                    x.Entity.OpeningId == row.OpeningId && x.State != EntityState.Unchanged))
                throw new InvalidOperationException("RfqOfferOpeningCommittedSealRequired");
            var round = PrincipalRfqCommercialRoundOpenings.AsNoTracking().Single(x => x.Id == row.OpeningId);
            var seal = PrincipalRfqCommercialRoundSeals.AsNoTracking().Single(x => x.Id == row.SealId);
            var offers = PrincipalRfqCommercialOfferSubmissions.AsNoTracking()
                .Where(x => x.OpeningId == row.OpeningId).OrderBy(x => x.SupplierBidId).ToArray();
            var exact = System.Text.Json.JsonSerializer.Serialize(offers.Select(x => new
            { BidId = x.SupplierBidId, x.SupplierId, x.SnapshotSha256 }));
            if (offers.Length != row.OfferCount || offers.Any(x => x.ApprovedDecisionId != seal.ApprovedDecisionId
                    || x.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(x.SnapshotJson))
                || ProcurementEvidenceEncoding.Digest(exact) != row.OfferSetSha256)
                throw new InvalidOperationException("RfqOfferOpeningCommittedSetChanged");
            PrincipalRfqOfferOpeningStorage.Verify(row, round, seal);
            PrincipalRfqOfferOpeningStorage.VerifyReceipt(row, receipts.Single(x => x.UnsealingId == row.Id));
            if (ChangeTracker.Entries<AuditEvent>().Count(x => x.State == EntityState.Added
                    && x.Entity.RecordType == nameof(PrincipalRfqOfferOpeningRecord) && x.Entity.RecordId == row.Id
                    && x.Entity.Action == "OpenOffers" && x.Entity.ActorSubjectId == scope.SubjectId
                    && x.Entity.ChangedFieldsJson == PrincipalRfqOfferOpeningStorage.AuditPayload(row)) != 1
                || ChangeTracker.Entries<OutboxEvent>().Count(x => x.State == EntityState.Added
                    && x.Entity.EventType == "sourcing.commercial_offers_opened.v1"
                    && x.Entity.AggregateType == "RfqCommercialRound" && x.Entity.AggregateId == row.OpeningId
                    && x.Entity.IdempotencyKey == row.CommandKey.ToString("D")
                    && x.Entity.PayloadJson == PrincipalRfqOfferOpeningStorage.EventPayload(row)) != 1)
                throw new InvalidOperationException("RfqOfferOpeningSameSaveEffectsRequired");
        }
    }
}
