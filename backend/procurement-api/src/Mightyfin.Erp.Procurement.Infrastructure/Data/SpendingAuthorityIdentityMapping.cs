using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<SpendingAuthorityIdentityRecord> SpendingAuthorityIdentityRecords => Set<SpendingAuthorityIdentityRecord>();

    private void ConfigureSpendingAuthorityIdentity(ModelBuilder model)
    {
        ConfigureBase<SpendingAuthorityIdentityRecord>(model, "spending_authority_identity_evidence");
        var b = model.Entity<SpendingAuthorityIdentityRecord>();
        b.Property(x => x.AuthorityReference).HasMaxLength(100).IsRequired();
        b.Property(x => x.Permission).HasMaxLength(100).IsRequired();
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.ConfigurationOwnerPrincipalId).HasMaxLength(200).IsRequired();
        b.Property(x => x.RecipientPrincipalId).HasMaxLength(200).IsRequired();
        b.Property(x => x.ActorPrincipalId).HasMaxLength(200).IsRequired();
        b.Property(x => x.Stage).HasMaxLength(20).IsRequired();
        b.Property(x => x.BusinessFingerprint).HasMaxLength(64).IsRequired();
        b.Property(x => x.EvidenceSha256).HasMaxLength(64).IsRequired();
        b.Property(x => x.CreatedEvidenceStage).HasMaxLength(20);
        b.Property(x => x.SubmittedEvidenceStage).HasMaxLength(20);
        b.Property(x => x.GrantId).ValueGeneratedNever();
        b.Property(x => x.ActorWorkerId).ValueGeneratedNever();
        b.Property(x => x.RecipientWorkerId).ValueGeneratedNever();
        b.Property(x => x.CreatedEvidenceId).ValueGeneratedNever();
        b.Property(x => x.SubmittedEvidenceId).ValueGeneratedNever();
        b.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.GrantId, x.Id, x.Stage })
            .HasName("ak_authority_identity_lineage");
        b.HasOne<ProcurementSpendingAuthorityGrant>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GrantId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_authority_identity_grant");
        b.HasOne<SpendingAuthorityIdentityRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GrantId, x.CreatedEvidenceId, x.CreatedEvidenceStage })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.GrantId, x.Id, x.Stage })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_authority_identity_created");
        b.HasOne<SpendingAuthorityIdentityRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GrantId, x.SubmittedEvidenceId, x.SubmittedEvidenceStage })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.GrantId, x.Id, x.Stage })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_authority_identity_submitted");
        b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.GrantId, x.Stage, x.GrantRecordVersionAtAction })
            .IsUnique().HasDatabaseName("ux_authority_identity_action_revision");
        b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.GrantId }, "ux_authority_identity_origin")
            .IsUnique().HasDatabaseName("ux_authority_identity_origin").HasFilter("stage = 'Created'");
        b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.GrantId }, "ux_authority_identity_activation")
            .IsUnique().HasDatabaseName("ux_authority_identity_activation").HasFilter("stage = 'Activated'");
        b.ToTable(t => t.HasCheckConstraint("ck_authority_identity_evidence", """
            schema_version IN (1,2) AND (schema_version=2 OR (actor_worker_id IS NOT NULL AND recipient_worker_id IS NOT NULL)) AND id <> '00000000-0000-0000-0000-000000000000'::uuid
            AND grant_id <> '00000000-0000-0000-0000-000000000000'::uuid
            AND (schema_version=1 OR ((action='PurchaseRequest.Approve' AND permission='procurement-approval-decide')
                OR (action='SourcingEvent.Publish' AND permission='procurement-sourcing-publish')
                OR (action='PurchaseOrder.Approve' AND permission='procurement-po-approve')))
            AND authority_version>0 AND grant_record_version_at_action>0
            AND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)
            AND (recipient_worker_id IS NULL OR recipient_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)
            AND length(btrim(authority_reference))>0 AND length(btrim(permission))>0 AND length(btrim(action))>0
            AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
            AND actor_principal_id <> '00000000-0000-0000-0000-000000000000'
            AND recipient_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
            AND recipient_principal_id <> '00000000-0000-0000-0000-000000000000'
            AND configuration_owner_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
            AND configuration_owner_principal_id <> '00000000-0000-0000-0000-000000000000'
            AND business_fingerprint IS NOT NULL AND business_fingerprint ~ '^[0-9a-fA-F]{64}$'
            AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'
            AND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id
            AND version=1 AND updated_at IS NULL AND updated_by IS NULL
            AND ((stage='Created' AND grant_record_version_at_action=1
                AND created_evidence_id IS NULL AND created_evidence_stage IS NULL
                AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)
            OR (stage='Submitted' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id
                AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'
                AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)
            OR (stage='Activated' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id
                AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'
                AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id
                AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))
            """));
    }

    private void GuardSpendingAuthorityIdentity()
    {
        var entries = ChangeTracker.Entries<SpendingAuthorityIdentityRecord>().ToArray();
        foreach (var entry in entries)
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("AuthorityIdentityAppendOnly");
            if (entry.State != EntityState.Added) continue;
            var row = SpendingAuthorityIdentityStorage.Read(entry.Entity);
            if (row.TenantId != scope.TenantId || row.LegalEntityId != scope.LegalEntityId
                || row.ActorPrincipalId != scope.SubjectId
                || (row.SchemaVersion==1 ? scope.WorkerId is null || scope.WorkerId==Guid.Empty || row.ActorWorkerId!=scope.WorkerId
                    : row.ActorWorkerId!=scope.CoreLinkedWorkerId))
                throw new InvalidOperationException("AuthorityIdentityActorMismatch");
            if (entries.Count(x => x.State == EntityState.Added && x.Entity.GrantId == row.GrantId) != 1)
                throw new InvalidOperationException("AuthorityIdentityOneActionPerSaveRequired");
            var owner = ChangeTracker.Entries<ProcurementSpendingAuthorityGrant>()
                .SingleOrDefault(x => x.Entity.Id == row.GrantId)
                ?? throw new InvalidOperationException("AuthorityIdentitySameSaveOwnerRequired");
            var grant = owner.Entity;
            var expectedVersion = owner.State == EntityState.Added ? 1
                : checked(owner.OriginalValues.GetValue<long>(nameof(ProcurementRecord.Version)) + 1);
            if (grant.Version != expectedVersion || row.GrantRecordVersionAtAction != expectedVersion
                || row.BusinessFingerprint != SpendingAuthorityConfiguration.Fingerprint(grant))
                throw new InvalidOperationException("AuthorityIdentityOwnerVersionOrFingerprintMismatch");
            if (row.Stage == "Created")
            {
                if (owner.State != EntityState.Added || grant.State != "Draft" || grant.Version != 1
                    || grant.CreatedBy != scope.SubjectId || grant.CreatedAt == default
                    || grant.CreatedAt != ProcurementEvidenceEncoding.DatabaseTimestamp(grant.CreatedAt)
                    || grant.UpdatedAt is not null || grant.UpdatedBy is not null
                    || grant.SubmittedAt is not null || grant.SubmittedByPrincipalId is not null
                    || grant.SubmittedBusinessFingerprint is not null || grant.ReviewReturnReason is not null
                    || grant.ActivatedAt is not null || grant.ActivatedByPrincipalId is not null
                    || grant.ActivationReason is not null || grant.PublishedBusinessFingerprint is not null
                    || grant.RevokedAt is not null || grant.RevokedByPrincipalId is not null
                    || grant.RevocationReason is not null || grant.SupersededAt is not null || grant.SupersededByGrantId is not null)
                    throw new InvalidOperationException("AuthorityIdentityInitialDraftRequired");
                SpendingAuthorityIdentity.VerifyOrigin(grant, row);
                continue;
            }
            if (owner.State != EntityState.Modified)
                throw new InvalidOperationException("AuthorityIdentitySameSaveTransitionRequired");
            var originalState = owner.OriginalValues.GetValue<string>(nameof(ProcurementSpendingAuthorityGrant.State));
            var created = SpendingAuthorityIdentityStorage.Read(AuthorityIdentityRow(row.CreatedEvidenceId!.Value));
            if (row.Stage == "Submitted")
            {
                if (originalState != "Draft" || grant.State != "PendingReview")
                    throw new InvalidOperationException("AuthorityIdentitySubmissionTransitionRequired");
                SpendingAuthorityIdentity.VerifySubmission(grant, created, row);
            }
            else
            {
                if (originalState != "PendingReview" || grant.State != "Active")
                    throw new InvalidOperationException("AuthorityIdentityActivationTransitionRequired");
                var submitted = SpendingAuthorityIdentityStorage.Read(AuthorityIdentityRow(row.SubmittedEvidenceId!.Value));
                SpendingAuthorityIdentity.VerifyActivation(grant, created, submitted, row);
            }
        }
    }

    private SpendingAuthorityIdentityRecord AuthorityIdentityRow(Guid id)
    {
        var tracked = ChangeTracker.Entries<SpendingAuthorityIdentityRecord>().SingleOrDefault(x => x.Entity.Id == id);
        // Lineage must already be retained; never adopt a newly supplied historical envelope.
        if (tracked is not null && tracked.State != EntityState.Unchanged)
            throw new InvalidOperationException("AuthorityIdentityRetainedLineageRequired");
        return SpendingAuthorityIdentityRecords.AsNoTracking().SingleOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException("AuthorityIdentityRetainedLineageRequired");
    }

    private bool PreserveAuthorityCreationMetadata(ProcurementRecord row) => row is SpendingAuthorityIdentityRecord
        || row is ProcurementSpendingAuthorityGrant grant && ChangeTracker.Entries<SpendingAuthorityIdentityRecord>()
            .Any(x => x.State == EntityState.Added && x.Entity.GrantId == grant.Id && x.Entity.Stage == "Created");
}
