using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<OperationalPolicyIdentityRecord> OperationalPolicyIdentityRecords => Set<OperationalPolicyIdentityRecord>();

    private void ConfigureOperationalPolicyIdentity(ModelBuilder model)
    {
        ConfigureBase<OperationalPolicyIdentityRecord>(model, "operational_policy_identity_evidence");
        model.Entity<PolicyVersion>().HasAlternateKey(x => new
            { x.TenantId, x.LegalEntityId, x.Id, x.PolicyKey, x.PolicyVersionNumber })
            .HasName("ak_operational_policy_identity_owner"); // P29's existing four-column key is unchanged.
        var b = model.Entity<OperationalPolicyIdentityRecord>();
        b.Property(x => x.PolicyKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.Stage).HasMaxLength(20).IsRequired();
        b.Property(x => x.ActorPrincipalId).HasMaxLength(200).IsRequired();
        b.Property(x => x.RulesSha256).HasMaxLength(64).IsRequired();
        b.Property(x => x.EvidenceSha256).HasMaxLength(64).IsRequired();
        b.Property(x => x.CreatedEvidenceStage).HasMaxLength(20);
        b.Property(x => x.SubmittedEvidenceStage).HasMaxLength(20);
        b.Property(x => x.PolicyVersionId).ValueGeneratedNever();
        b.Property(x => x.ActorWorkerId).ValueGeneratedNever();
        b.Property(x => x.CreatedEvidenceId).ValueGeneratedNever();
        b.Property(x => x.SubmittedEvidenceId).ValueGeneratedNever();
        b.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey, x.Id, x.Stage })
            .HasName("ak_operational_policy_identity_lineage");
        b.HasOne<PolicyVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey, x.PolicyVersionNumber })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PolicyKey, x.PolicyVersionNumber })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_operational_policy_identity_owner");
        b.HasOne<OperationalPolicyIdentityRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey, x.CreatedEvidenceId, x.CreatedEvidenceStage })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey, x.Id, x.Stage })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_operational_policy_identity_created");
        b.HasOne<OperationalPolicyIdentityRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey, x.SubmittedEvidenceId, x.SubmittedEvidenceStage })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey, x.Id, x.Stage })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_operational_policy_identity_submitted");
        b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey, x.Stage, x.PolicyRecordVersionAtAction })
            .IsUnique().HasDatabaseName("ux_operational_policy_identity_revision");
        b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey }, "ux_operational_policy_identity_created")
            .IsUnique().HasDatabaseName("ux_operational_policy_identity_created").HasFilter("stage = 'Created'");
        b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey }, "ux_operational_policy_identity_published")
            .IsUnique().HasDatabaseName("ux_operational_policy_identity_published").HasFilter("stage = 'Published'");
        b.ToTable(t => t.HasCheckConstraint("ck_operational_policy_identity", """
            schema_version IN (1,2) AND policy_key IN ('RECEIVING','INVOICE_MATCHING','INVOICE_UNIT_REVIEW','MANUAL_PAYMENT_REVIEW','SOURCING_COMMERCIAL_ROUND') AND (policy_key NOT IN ('MANUAL_PAYMENT_REVIEW','SOURCING_COMMERCIAL_ROUND') OR schema_version=2)
            AND id <> '00000000-0000-0000-0000-000000000000'::uuid
            AND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid
            AND policy_version_number>0 AND policy_record_version_at_action>0
            AND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)
            AND (schema_version=2 OR actor_worker_id IS NOT NULL)
            AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
            AND actor_principal_id <> '00000000-0000-0000-0000-000000000000'
            AND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$'
            AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'
            AND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id
            AND version=1 AND updated_at IS NULL AND updated_by IS NULL
            AND ((stage='Created' AND policy_record_version_at_action=1
                AND created_evidence_id IS NULL AND created_evidence_stage IS NULL
                AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)
            OR (stage='Submitted' AND policy_record_version_at_action>1
                AND created_evidence_id IS NOT NULL AND created_evidence_id<>id
                AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'
                AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)
            OR (stage='Published' AND policy_record_version_at_action>2
                AND created_evidence_id IS NOT NULL AND created_evidence_id<>id
                AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'
                AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id
                AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))
            """));
    }

    private void GuardOperationalPolicyIdentity()
    {
        var proofs = ChangeTracker.Entries<OperationalPolicyIdentityRecord>().ToArray();
        var owners = ChangeTracker.Entries<PolicyVersion>().ToArray();
        foreach (var entry in proofs)
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("OperationalPolicyIdentityAppendOnly");
            if (entry.State != EntityState.Added) continue;
            var row = OperationalPolicyIdentityStorage.Read(entry.Entity);
            var actorWorker = OperationalIdentityActorWorker(row.SchemaVersion);
            if (row.TenantId != scope.TenantId || row.LegalEntityId != scope.LegalEntityId
                || row.ActorPrincipalId != scope.SubjectId || row.ActorWorkerId != actorWorker)
                throw new InvalidOperationException("OperationalPolicyIdentityActorMismatch");
            var owner = owners.SingleOrDefault(x => x.Entity.Id == row.PolicyVersionId)
                ?? throw new InvalidOperationException("OperationalPolicyIdentitySameSaveOwnerRequired");
            var policy = owner.Entity;
            var intendedVersion = OperationalIdentityIntendedVersion(owner);
            if (row.PolicyRecordVersionAtAction != intendedVersion
                || row.RulesSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson))
                throw new InvalidOperationException("OperationalPolicyIdentityActionMismatch");
            if (row.Stage == "Created")
            {
                if (owner.State != EntityState.Added || policy.State != "Draft" || policy.Version != 1
                    || policy.CreatedBy != scope.SubjectId || policy.CreatedAt == default
                    || policy.CreatedAt != ProcurementEvidenceEncoding.DatabaseTimestamp(policy.CreatedAt)
                    || policy.UpdatedAt is not null || policy.UpdatedBy is not null
                    || policy.ReviewRequestedAt is not null || policy.ReviewRequestedBy is not null || policy.ReviewReason is not null
                    || policy.PublishedAt is not null || policy.PublishedBy is not null || policy.PublicationReason is not null)
                    throw new InvalidOperationException("OperationalPolicyIdentityInitialDraftRequired");
                OperationalPolicyWorkerIdentity.VerifyOrigin(policy, row);
            }
            else
            {
                if (owner.State != EntityState.Modified)
                    throw new InvalidOperationException("OperationalPolicyIdentitySameSaveTransitionRequired");
                var original = (PolicyVersion)owner.OriginalValues.ToObject();
                var created = OperationalPolicyIdentityStorage.Read(OperationalIdentityRetainedRow(row.CreatedEvidenceId ?? throw new InvalidOperationException("OperationalPolicyOriginalMakerRequired")));
                if (row.Stage == "Submitted")
                {
                    if (original.State != "Draft" || policy.State != "PendingReview")
                        throw new InvalidOperationException("OperationalPolicyIdentitySubmissionTransitionRequired");
                    OperationalPolicyWorkerIdentity.RequireAuthor(policy, created, scope.SubjectId, actorWorker);
                    OperationalPolicyWorkerIdentity.VerifySubmission(policy, created, row, intendedVersion);
                }
                else
                {
                    if (original.State != "PendingReview" || policy.State != "Published")
                        throw new InvalidOperationException("OperationalPolicyIdentityPublicationTransitionRequired");
                    var submitted = OperationalPolicyIdentityStorage.Read(OperationalIdentityRetainedRow(row.SubmittedEvidenceId ?? throw new InvalidOperationException("OperationalPolicySubmittedIdentityRequired")));
                    if (submitted.PolicyRecordVersionAtAction != original.Version)
                        throw new InvalidOperationException("OperationalPolicyIdentityCurrentSubmissionRequired");
                    OperationalPolicyWorkerIdentity.VerifyPublication(policy, created, submitted, row,
                        ownerRecordVersion: intendedVersion);
                }
            }
        }
        foreach (var owner in owners.Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var policy = owner.Entity;
            var original = owner.State == EntityState.Added ? null : (PolicyVersion)owner.OriginalValues.ToObject();
            if (!OperationalIdentityPolicyKey(policy.PolicyKey) && (original is null || !OperationalIdentityPolicyKey(original.PolicyKey))) continue;
            if (owner.State == EntityState.Deleted) throw new InvalidOperationException("OperationalPolicyDeletionDenied");
            _ = OperationalIdentityIntendedVersion(owner);
            if (policy.TenantId != scope.TenantId || policy.LegalEntityId != scope.LegalEntityId
                || original is not null && (original.TenantId != policy.TenantId || original.LegalEntityId != policy.LegalEntityId
                    || original.Id != policy.Id || original.PolicyKey != policy.PolicyKey || original.PolicyVersionNumber != policy.PolicyVersionNumber
                    || original.CreatedBy != policy.CreatedBy || original.CreatedAt != policy.CreatedAt))
                throw new InvalidOperationException("OperationalPolicyIdentityOwnerImmutable");
            var added = proofs.Where(x => x.State == EntityState.Added && x.Entity.PolicyVersionId == policy.Id).Select(x => x.Entity).ToArray();
            if (owner.State == EntityState.Added)
            {
                if (added.Length != 1 || added[0].Stage != "Created")
                    throw new InvalidOperationException("OperationalPolicyOriginalMakerRequired");
                continue;
            }
            if (original is null) throw new InvalidOperationException("OperationalPolicyIdentitySameSaveOwnerRequired");
            var createdRow = OperationalIdentityRetainedStage(policy.Id, "Created");
            var origin = OperationalPolicyIdentityStorage.Read(createdRow);
            OperationalPolicyWorkerIdentity.VerifyOrigin(policy, origin);
            var allowed = new HashSet<string>([nameof(PolicyVersion.Version), nameof(PolicyVersion.UpdatedAt), nameof(PolicyVersion.UpdatedBy)]);
            if (original.State == "Draft" && policy.State == "Draft" && added.Length == 0)
            {
                var actorWorker = OperationalIdentityActorWorker(origin.SchemaVersion);
                OperationalPolicyWorkerIdentity.RequireAuthor(policy, origin, scope.SubjectId, actorWorker);
                allowed.UnionWith([nameof(PolicyVersion.RulesJson), nameof(PolicyVersion.EffectiveFrom), nameof(PolicyVersion.EffectiveTo)]);
            }
            else if (original.State == "Draft" && policy.State == "Retired" && added.Length == 0)
            {
                OperationalPolicyWorkerIdentity.RequireAuthor(policy, origin, scope.SubjectId, OperationalIdentityActorWorker(origin.SchemaVersion));
                if (policy.ReviewRequestedAt is not null || policy.ReviewRequestedBy is not null || policy.ReviewReason is not null
                    || policy.PublishedAt is not null || policy.PublishedBy is not null || policy.PublicationReason is not null
                    || OperationalPolicyIdentityRecords.Any(x => x.PolicyVersionId == policy.Id && x.Stage != "Created"))
                    throw new InvalidOperationException("OperationalPolicyRetirementHistoryDenied");
                allowed.Add(nameof(PolicyVersion.State));
            }
            else if (original.State == "Draft" && policy.State == "PendingReview" && added.Length == 1 && added[0].Stage == "Submitted")
                allowed.UnionWith([nameof(PolicyVersion.State), nameof(PolicyVersion.ReviewRequestedAt), nameof(PolicyVersion.ReviewRequestedBy), nameof(PolicyVersion.ReviewReason)]);
            else if (original.State == "PendingReview" && policy.State == "Published" && added.Length == 1 && added[0].Stage == "Published")
                allowed.UnionWith([nameof(PolicyVersion.State), nameof(PolicyVersion.PublishedAt), nameof(PolicyVersion.PublishedBy), nameof(PolicyVersion.PublicationReason)]);
            else if (original.State == "PendingReview" && policy.State == "Draft" && added.Length == 0)
            {
                var actorWorker = OperationalIdentityActorWorker(origin.SchemaVersion);
                var submitted = OperationalPolicyIdentityStorage.Read(OperationalIdentityRetainedStage(policy.Id, "Submitted", original.Version));
                OperationalPolicyWorkerIdentity.RequireIndependent(original, origin, submitted, scope.SubjectId, actorWorker);
                allowed.Add(nameof(PolicyVersion.State));
            }
            else if (original.State == "Published" && policy.State == "Published" && added.Length == 0)
            {
                _ = OperationalIdentityActorWorker(origin.SchemaVersion);
                var published = OperationalPolicyIdentityStorage.Read(OperationalIdentityRetainedStage(policy.Id, "Published"));
                var submitted = OperationalPolicyIdentityStorage.Read(OperationalIdentityRetainedRow(published.SubmittedEvidenceId ?? throw new InvalidOperationException("OperationalPolicySubmittedIdentityRequired")));
                OperationalPolicyWorkerIdentity.VerifyPublication(original, origin, submitted, published,
                    OperationalPolicyPublicationMode.RetainedHistorical);
                if (policy.EffectiveTo is not { } end || end <= original.EffectiveFrom
                    || original.EffectiveTo is { } oldEnd && end >= oldEnd)
                    throw new InvalidOperationException("OperationalPolicyControlledShorteningRequired");
                var replacement = owners.Where(x => x.State == EntityState.Modified && x.Entity.Id != policy.Id
                    && x.Entity.TenantId == policy.TenantId && x.Entity.LegalEntityId == policy.LegalEntityId
                    && x.Entity.PolicyKey == policy.PolicyKey && x.Entity.State == "Published" && x.Entity.EffectiveFrom == end
                    && x.OriginalValues.GetValue<string>(nameof(PolicyVersion.State)) == "PendingReview"
                    && proofs.Count(p => p.State == EntityState.Added && p.Entity.PolicyVersionId == x.Entity.Id && p.Entity.Stage == "Published") == 1).ToArray();
                if (replacement.Length != 1) throw new InvalidOperationException("OperationalPolicyPublishedReplacementRequired");
                allowed.Add(nameof(PolicyVersion.EffectiveTo));
            }
            else throw new InvalidOperationException("OperationalPolicyIdentityTransitionRequired");
            if (owner.Properties.Any(x => x.IsModified && !allowed.Contains(x.Metadata.Name)))
                throw new InvalidOperationException("OperationalPolicyIdentityMaterialChangeDenied");
        }
    }

    private static long OperationalIdentityIntendedVersion(EntityEntry<PolicyVersion> owner)
    {
        if (owner.State == EntityState.Added)
        {
            if (owner.Entity.Version != 1) throw new InvalidOperationException("OperationalPolicyOwnerVersionManagedByStore");
            return 1;
        }
        if (owner.State != EntityState.Modified)
            throw new InvalidOperationException("OperationalPolicyIdentitySameSaveTransitionRequired");
        var original = owner.OriginalValues.GetValue<long>(nameof(PolicyVersion.Version));
        if (original <= 0 || original == long.MaxValue || owner.Entity.Version != original)
            throw new InvalidOperationException("OperationalPolicyOwnerVersionManagedByStore");
        return original + 1;
    }
    private Guid? OperationalIdentityActorWorker(int schema)
    {
        if (!ProcurementPrincipalEvidence.Canonical(scope.SubjectId) || scope.WorkerId == Guid.Empty)
            throw new InvalidOperationException("OperationalPolicyIdentityActorMismatch");
        return schema switch { 1 => scope.WorkerId, 2 => scope.CoreLinkedWorkerId,
            _ => throw new InvalidOperationException("OperationalPolicyIdentitySchemaUnsupported") };
    }
    private OperationalPolicyIdentityRecord OperationalIdentityRetainedRow(Guid id)
    {
        if (ChangeTracker.Entries<OperationalPolicyIdentityRecord>().Any(x => x.Entity.Id == id && x.State != EntityState.Unchanged))
            throw new InvalidOperationException("OperationalPolicyIdentityRetainedLineageRequired");
        return OperationalPolicyIdentityRecords.AsNoTracking().SingleOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException("OperationalPolicyIdentityRetainedLineageRequired");
    }
    private OperationalPolicyIdentityRecord OperationalIdentityRetainedStage(Guid policyId, string stage, long? revision = null)
    {
        var query = OperationalPolicyIdentityRecords.AsNoTracking().Where(x => x.PolicyVersionId == policyId && x.Stage == stage);
        if (revision.HasValue) query = query.Where(x => x.PolicyRecordVersionAtAction == revision.Value);
        var rows = query.Take(2).ToArray();
        if (rows.Length != 1) throw new InvalidOperationException("OperationalPolicyHistoricalIdentityMissing");
        return rows[0];
    }
    private static bool OperationalIdentityPolicyKey(string key) => key is "RECEIVING" or "INVOICE_MATCHING"
        or "INVOICE_UNIT_REVIEW" or "MANUAL_PAYMENT_REVIEW" or PrincipalRfqCommercialRoundPolicyRules.PolicyKey;
    private bool PreserveOperationalPolicyCreationMetadata(ProcurementRecord row) => row is OperationalPolicyIdentityRecord
        || row is PolicyVersion policy && OperationalIdentityPolicyKey(policy.PolicyKey)
            && ChangeTracker.Entries<OperationalPolicyIdentityRecord>().Any(x => x.State == EntityState.Added
                && x.Entity.PolicyVersionId == policy.Id && x.Entity.Stage == "Created");
}
