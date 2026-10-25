using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<ContractReviewPolicyIdentityRecord> ContractReviewPolicyIdentityRecords => Set<ContractReviewPolicyIdentityRecord>();

    private void ConfigureContractReviewPolicyIdentity(ModelBuilder model)
    {
        ConfigureBase<ContractReviewPolicyIdentityRecord>(model, "contract_review_policy_identity");
        var b = model.Entity<ContractReviewPolicyIdentityRecord>();
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
            .HasName("ak_p35_contract_policy_lineage");
        b.HasOne<PolicyVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey, x.PolicyVersionNumber })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PolicyKey, x.PolicyVersionNumber })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p35_contract_policy_owner");
        b.HasOne<ContractReviewPolicyIdentityRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey, x.CreatedEvidenceId, x.CreatedEvidenceStage })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey, x.Id, x.Stage })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p35_contract_policy_created");
        b.HasOne<ContractReviewPolicyIdentityRecord>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey, x.SubmittedEvidenceId, x.SubmittedEvidenceStage })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey, x.Id, x.Stage })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p35_contract_policy_submitted");
        b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey, x.Stage, x.PolicyRecordVersionAtAction })
            .IsUnique().HasDatabaseName("ux_p35_contract_policy_revision");
        b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey }, "ux_p35_contract_policy_created")
            .IsUnique().HasDatabaseName("ux_p35_contract_policy_created").HasFilter("stage = 'Created'");
        b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey }, "ux_p35_contract_policy_published")
            .IsUnique().HasDatabaseName("ux_p35_contract_policy_published").HasFilter("stage = 'Published'");
        b.ToTable(t => t.HasCheckConstraint("ck_p35_contract_policy_identity", """
            schema_version=2 AND policy_key='SUPPLIER_CONTRACT_REVIEW'
            AND id <> '00000000-0000-0000-0000-000000000000'::uuid
            AND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid
            AND policy_version_number>0 AND policy_record_version_at_action>0
            AND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)
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

    private void GuardContractReviewPolicyIdentity()
    {
        var proofs = ChangeTracker.Entries<ContractReviewPolicyIdentityRecord>().ToArray();
        var owners = ChangeTracker.Entries<PolicyVersion>().ToArray();
        foreach (var entry in proofs)
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("ContractReviewPolicyIdentityAppendOnly");
            if (entry.State != EntityState.Added) continue;
            var row = ContractReviewPolicyIdentityStorage.Read(entry.Entity);
            if (row.TenantId != scope.TenantId || row.LegalEntityId != scope.LegalEntityId
                || row.ActorPrincipalId != scope.SubjectId || row.ActorWorkerId != scope.CoreLinkedWorkerId)
                throw new InvalidOperationException("ContractReviewPolicyIdentityActorMismatch");
            var owner = owners.SingleOrDefault(x => x.Entity.Id == row.PolicyVersionId)
                ?? throw new InvalidOperationException("ContractReviewPolicySameSaveOwnerRequired");
            var policy = owner.Entity;
            var intended = ContractPolicyIntendedVersion(owner);
            if (ProcurementContractReviewPolicyRules.Validate(policy.RulesJson,
                draft: row.Stage == "Created").Count != 0)
                throw new InvalidOperationException("ContractReviewPolicyRulesInvalid");
            if (row.PolicyRecordVersionAtAction != intended || row.RulesSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson))
                throw new InvalidOperationException("ContractReviewPolicyIdentityActionMismatch");
            if (row.Stage == "Created")
            {
                if (owner.State != EntityState.Added || policy.State != "Draft" || policy.Version != 1
                    || policy.CreatedBy != scope.SubjectId || policy.CreatedAt != row.ActionAt
                    || policy.ReviewRequestedAt is not null || policy.PublishedAt is not null)
                    throw new InvalidOperationException("ContractReviewPolicyInitialDraftRequired");
                OperationalPolicyWorkerIdentity.VerifyOrigin(policy, row);
            }
            else
            {
                if (owner.State != EntityState.Modified)
                    throw new InvalidOperationException("ContractReviewPolicySameSaveTransitionRequired");
                var original = (PolicyVersion)owner.OriginalValues.ToObject();
                var created = ContractReviewPolicyIdentityStorage.Read(ContractPolicyRetainedRow(row.CreatedEvidenceId
                    ?? throw new InvalidOperationException("ContractReviewPolicyOriginRequired")));
                if (row.Stage == "Submitted")
                {
                    if (original.State != "Draft" || policy.State != "PendingReview")
                        throw new InvalidOperationException("ContractReviewPolicySubmissionTransitionRequired");
                    OperationalPolicyWorkerIdentity.RequireAuthor(policy, created, scope.SubjectId, scope.CoreLinkedWorkerId);
                    OperationalPolicyWorkerIdentity.VerifySubmission(policy, created, row, intended);
                }
                else if (row.Stage == "Published")
                {
                    if (original.State != "PendingReview" || policy.State != "Published")
                        throw new InvalidOperationException("ContractReviewPolicyPublicationTransitionRequired");
                    var submitted = ContractReviewPolicyIdentityStorage.Read(ContractPolicyRetainedRow(row.SubmittedEvidenceId
                        ?? throw new InvalidOperationException("ContractReviewPolicySubmissionRequired")));
                    if (submitted.PolicyRecordVersionAtAction != original.Version)
                        throw new InvalidOperationException("ContractReviewPolicyCurrentSubmissionRequired");
                    OperationalPolicyWorkerIdentity.VerifyPublication(policy, created, submitted, row,
                        ownerRecordVersion: intended);
                }
                else throw new InvalidOperationException("ContractReviewPolicyIdentityStageInvalid");
            }
        }
        foreach (var owner in owners.Where(x => (x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            && (x.Entity.PolicyKey == ProcurementContractReviewPolicyRules.PolicyKey
                || x.State != EntityState.Added && x.OriginalValues.GetValue<string>(nameof(PolicyVersion.PolicyKey))
                    == ProcurementContractReviewPolicyRules.PolicyKey)))
        {
            var policy = owner.Entity;
            if (policy.PolicyKey != ProcurementContractReviewPolicyRules.PolicyKey)
                throw new InvalidOperationException("ContractReviewPolicyKeyChanged");
            if (owner.State == EntityState.Deleted) throw new InvalidOperationException("ContractReviewPolicyDeleteDenied");
            var original = owner.State == EntityState.Added ? null : (PolicyVersion)owner.OriginalValues.ToObject();
            if (policy.TenantId != scope.TenantId || policy.LegalEntityId != scope.LegalEntityId
                || original is not null && (original.TenantId != policy.TenantId || original.LegalEntityId != policy.LegalEntityId
                    || original.Id != policy.Id || original.PolicyKey != policy.PolicyKey
                    || original.PolicyVersionNumber != policy.PolicyVersionNumber || original.CreatedBy != policy.CreatedBy
                    || original.CreatedAt != policy.CreatedAt))
                throw new InvalidOperationException("ContractReviewPolicyOwnerChanged");
            _ = ContractPolicyIntendedVersion(owner);
            var added = proofs.Where(x => x.State == EntityState.Added && x.Entity.PolicyVersionId == policy.Id)
                .Select(x => x.Entity).ToArray();
            if (owner.State == EntityState.Added)
            {
                if (added.Length != 1 || added[0].Stage != "Created")
                    throw new InvalidOperationException("ContractReviewPolicyOriginRequired");
                continue;
            }
            var origin = ContractReviewPolicyIdentityStorage.Read(ContractPolicyRetainedStage(policy.Id, "Created"));
            OperationalPolicyWorkerIdentity.VerifyOrigin(policy, origin);
            var allowed = new HashSet<string>([nameof(PolicyVersion.Version), nameof(PolicyVersion.UpdatedAt), nameof(PolicyVersion.UpdatedBy)]);
            if (original!.State == "Draft" && policy.State == "Draft" && added.Length == 0)
            {
                OperationalPolicyWorkerIdentity.RequireAuthor(policy, origin, scope.SubjectId, scope.CoreLinkedWorkerId);
                if (ProcurementContractReviewPolicyRules.Validate(policy.RulesJson, draft: true).Count != 0)
                    throw new InvalidOperationException("ContractReviewPolicyRulesInvalid");
                allowed.UnionWith([nameof(PolicyVersion.RulesJson), nameof(PolicyVersion.EffectiveFrom), nameof(PolicyVersion.EffectiveTo)]);
            }
            else if (original.State == "Draft" && policy.State == "Retired" && added.Length == 0)
            {
                OperationalPolicyWorkerIdentity.RequireAuthor(policy, origin, scope.SubjectId, scope.CoreLinkedWorkerId);
                if (ContractReviewPolicyIdentityRecords.Any(x => x.PolicyVersionId == policy.Id && x.Stage != "Created"))
                    throw new InvalidOperationException("ContractReviewPolicyRetainedHistory");
                allowed.Add(nameof(PolicyVersion.State));
            }
            else if (original.State == "Draft" && policy.State == "PendingReview"
                && added.Length == 1 && added[0].Stage == "Submitted")
                allowed.UnionWith([nameof(PolicyVersion.State), nameof(PolicyVersion.ReviewRequestedAt),
                    nameof(PolicyVersion.ReviewRequestedBy), nameof(PolicyVersion.ReviewReason)]);
            else if (original.State == "PendingReview" && policy.State == "Published"
                && added.Length == 1 && added[0].Stage == "Published")
                allowed.UnionWith([nameof(PolicyVersion.State), nameof(PolicyVersion.PublishedAt),
                    nameof(PolicyVersion.PublishedBy), nameof(PolicyVersion.PublicationReason)]);
            else if (original.State == "PendingReview" && policy.State == "Draft" && added.Length == 0)
            {
                var submitted = ContractReviewPolicyIdentityStorage.Read(ContractPolicyRetainedStage(policy.Id,
                    "Submitted", original.Version));
                OperationalPolicyWorkerIdentity.RequireIndependent(original, origin, submitted, scope.SubjectId, scope.CoreLinkedWorkerId);
                allowed.Add(nameof(PolicyVersion.State));
            }
            else if (original.State == "Published" && policy.State == "Published" && added.Length == 0)
            {
                var published = ContractReviewPolicyIdentityStorage.Read(ContractPolicyRetainedStage(policy.Id, "Published"));
                var submitted = ContractReviewPolicyIdentityStorage.Read(ContractPolicyRetainedRow(published.SubmittedEvidenceId
                    ?? throw new InvalidOperationException("ContractReviewPolicySubmissionRequired")));
                OperationalPolicyWorkerIdentity.VerifyPublication(original, origin, submitted, published,
                    OperationalPolicyPublicationMode.RetainedHistorical);
                if (policy.EffectiveTo is not { } end || end <= original.EffectiveFrom
                    || original.EffectiveTo is { } oldEnd && end >= oldEnd)
                    throw new InvalidOperationException("ContractReviewPolicyControlledShorteningRequired");
                if (owners.Count(x => x.State == EntityState.Modified && x.Entity.Id != policy.Id
                    && x.Entity.PolicyKey == policy.PolicyKey && x.Entity.State == "Published"
                    && x.Entity.EffectiveFrom == end
                    && x.OriginalValues.GetValue<string>(nameof(PolicyVersion.State)) == "PendingReview"
                    && proofs.Count(p => p.State == EntityState.Added && p.Entity.PolicyVersionId == x.Entity.Id
                        && p.Entity.Stage == "Published") == 1) != 1)
                    throw new InvalidOperationException("ContractReviewPolicyReplacementRequired");
                allowed.Add(nameof(PolicyVersion.EffectiveTo));
            }
            else throw new InvalidOperationException("ContractReviewPolicyTransitionRequired");
            if (owner.Properties.Any(x => x.IsModified && !allowed.Contains(x.Metadata.Name)))
                throw new InvalidOperationException("ContractReviewPolicyMaterialChangeDenied");
        }
    }

    private static long ContractPolicyIntendedVersion(EntityEntry<PolicyVersion> owner)
    {
        if (owner.State == EntityState.Added) return owner.Entity.Version == 1 ? 1
            : throw new InvalidOperationException("ContractReviewPolicyVersionManagedByStore");
        if (owner.State != EntityState.Modified) throw new InvalidOperationException("ContractReviewPolicySameSaveOwnerRequired");
        var original = owner.OriginalValues.GetValue<long>(nameof(PolicyVersion.Version));
        return original > 0 && original < long.MaxValue && owner.Entity.Version == original ? original + 1
            : throw new InvalidOperationException("ContractReviewPolicyVersionManagedByStore");
    }

    private ContractReviewPolicyIdentityRecord ContractPolicyRetainedRow(Guid id)
    {
        if (ChangeTracker.Entries<ContractReviewPolicyIdentityRecord>().Any(x => x.Entity.Id == id && x.State != EntityState.Unchanged))
            throw new InvalidOperationException("ContractReviewPolicyRetainedLineageRequired");
        return ContractReviewPolicyIdentityRecords.AsNoTracking().SingleOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException("ContractReviewPolicyRetainedLineageRequired");
    }

    private ContractReviewPolicyIdentityRecord ContractPolicyRetainedStage(Guid policyId, string stage, long? version = null)
    {
        var query = ContractReviewPolicyIdentityRecords.AsNoTracking().Where(x => x.PolicyVersionId == policyId && x.Stage == stage);
        if (version is { } v) query = query.Where(x => x.PolicyRecordVersionAtAction == v);
        var rows = query.Take(2).ToArray();
        return rows.Length == 1 ? rows[0] : throw new InvalidOperationException("ContractReviewPolicyHistoricalIdentityMissing");
    }

    private bool PreserveContractReviewPolicyCreationMetadata(ProcurementRecord row) => row is ContractReviewPolicyIdentityRecord
        || row is PolicyVersion policy && policy.PolicyKey == ProcurementContractReviewPolicyRules.PolicyKey
            && ChangeTracker.Entries<ContractReviewPolicyIdentityRecord>().Any(x => x.State == EntityState.Added
                && x.Entity.PolicyVersionId == policy.Id && x.Entity.Stage == "Created");
}
