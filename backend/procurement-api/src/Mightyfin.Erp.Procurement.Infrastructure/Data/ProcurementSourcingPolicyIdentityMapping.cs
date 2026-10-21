using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<SourcingPolicyIdentityEvidence> SourcingPolicyIdentityEvidenceRecords => Set<SourcingPolicyIdentityEvidence>();
    private void ConfigureSourcingPolicyIdentity(ModelBuilder model)
    {
        ConfigureBase<SourcingPolicyIdentityEvidence>(model, "sourcing_policy_identity_evidence");
        var b = model.Entity<SourcingPolicyIdentityEvidence>();
        b.Property(x => x.Stage).HasMaxLength(20).IsRequired(); b.Property(x => x.ActorPrincipalId).HasMaxLength(200).IsRequired();
        b.Property(x => x.RulesSha256).HasMaxLength(64).IsRequired(); b.Property(x => x.EvidenceSha256).HasMaxLength(64).IsRequired();
        b.Property(x => x.CreatedEvidenceStage).HasMaxLength(20); b.Property(x => x.SubmittedEvidenceStage).HasMaxLength(20);
        b.Property(x => x.CreatedEvidenceId).ValueGeneratedNever(); b.Property(x => x.SubmittedEvidenceId).ValueGeneratedNever();
        b.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.Id, x.Stage }).HasName("ak_source_policy_identity_parent");
        b.HasOne<PolicyVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyVersionNumber })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PolicyVersionNumber }).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_source_policy_identity_owner");
        b.HasOne<SourcingPolicyIdentityEvidence>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.CreatedEvidenceId, x.CreatedEvidenceStage })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.Id, x.Stage }).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_source_policy_identity_created");
        b.HasOne<SourcingPolicyIdentityEvidence>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.SubmittedEvidenceId, x.SubmittedEvidenceStage })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.Id, x.Stage }).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_source_policy_identity_submitted");
        b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.Stage, x.PolicyRecordVersionAtAction })
            .IsUnique().HasDatabaseName("ux_source_policy_identity_revision");
        b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId }, "ux_source_policy_identity_created").IsUnique().HasDatabaseName("ux_source_policy_identity_created")
            .HasFilter("stage = 'Created'");
        b.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId }, "ux_source_policy_identity_published").IsUnique().HasDatabaseName("ux_source_policy_identity_published")
            .HasFilter("stage = 'Published'");
        b.ToTable(t => t.HasCheckConstraint("ck_source_policy_identity", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_number>0 AND policy_record_version_at_action>0 AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(actor_principal_id))>0 AND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$' AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND isfinite(action_at) AND ((stage='Created' AND policy_record_version_at_action=1 AND created_evidence_id IS NULL AND created_evidence_stage IS NULL AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL) OR (stage='Submitted' AND created_evidence_id IS NOT NULL AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created' AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL) OR (stage='Published' AND created_evidence_id IS NOT NULL AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created' AND submitted_evidence_id IS NOT NULL AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))"));
    }

    private void GuardSourcingPolicyIdentity()
    {
        foreach (var entry in ChangeTracker.Entries<SourcingPolicyIdentityEvidence>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("SourcingPolicyIdentityAppendOnly");
            if (entry.State != EntityState.Added) continue;
            var row = entry.Entity; SourcingPolicyIdentity.Verify(row);
            var policy = IdentityPolicy(row.PolicyVersionId) ?? throw new InvalidOperationException("SourcingPolicyIdentityOwnerRequired");
            var owner = Entry(policy);
            if (row.TenantId != scope.TenantId || row.LegalEntityId != scope.LegalEntityId || row.ActorPrincipalId != scope.SubjectId
                || policy.PolicyKey != SourcingPublicationPolicyRules.PolicyKey || row.PolicyVersionNumber != policy.PolicyVersionNumber
                || row.RulesSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
                || row.PolicyRecordVersionAtAction != (owner.State == EntityState.Added ? 1 : checked(owner.OriginalValues.GetValue<long>(nameof(PolicyVersion.Version)) + 1)))
                throw new InvalidOperationException("SourcingPolicyIdentityActionMismatch");
            if (row.Stage == "Created")
            {
                if (owner.State != EntityState.Added || policy.State != "Draft") throw new InvalidOperationException("SourcingPolicyIdentityCreationRequired");
            }
            else
            {
                if (owner.State != EntityState.Modified) throw new InvalidOperationException("SourcingPolicyIdentityTransitionRequired");
                var origin = IdentityRow(row.CreatedEvidenceId!.Value) ?? throw new InvalidOperationException("SourcingPolicyOriginalMakerRequired");
                SourcingPolicyIdentity.VerifyOrigin(policy, origin);
                var oldState = owner.OriginalValues.GetValue<string>(nameof(PolicyVersion.State));
                if (row.Stage == "Submitted")
                {
                    if (oldState != "Draft" || policy.State != "PendingReview" || row.ActorPrincipalId != origin.ActorPrincipalId
                        || row.ActorWorkerId != origin.ActorWorkerId || policy.ReviewRequestedBy != row.ActorPrincipalId
                        || ProcurementEvidenceEncoding.DatabaseTimestamp(policy.ReviewRequestedAt) != row.ActionAt)
                        throw new InvalidOperationException("SourcingPolicySubmittedIdentityMismatch");
                }
                else
                {
                    var submission = IdentityRow(row.SubmittedEvidenceId!.Value) ?? throw new InvalidOperationException("SourcingPolicySubmittedIdentityRequired");
                    SourcingPolicyIdentity.VerifySubmission(policy, origin, submission, false);
                    if (oldState != "PendingReview" || policy.State != "Published" || row.ActorPrincipalId == origin.ActorPrincipalId
                        || row.ActorPrincipalId == submission.ActorPrincipalId || row.ActorWorkerId == origin.ActorWorkerId
                        || row.ActorWorkerId == submission.ActorWorkerId || policy.PublishedBy != row.ActorPrincipalId
                        || ProcurementEvidenceEncoding.DatabaseTimestamp(policy.PublishedAt) != row.ActionAt)
                        throw new InvalidOperationException("SourcingPolicyIndependentReviewerRequired");
                }
            }
        }
        foreach (var entry in ChangeTracker.Entries<PolicyVersion>().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            var policy = entry.Entity;
            if (entry.State == EntityState.Modified && entry.OriginalValues.GetValue<string>(nameof(PolicyVersion.PolicyKey)) != policy.PolicyKey)
            {
                if (policy.PolicyKey == "SOURCING" || entry.OriginalValues.GetValue<string>(nameof(PolicyVersion.PolicyKey)) == "SOURCING")
                    throw new InvalidOperationException("SourcingPolicyKeyImmutable");
            }
            if (policy.PolicyKey != "SOURCING") continue;
            var added = ChangeTracker.Entries<SourcingPolicyIdentityEvidence>().Where(x => x.State == EntityState.Added && x.Entity.PolicyVersionId == policy.Id).Select(x => x.Entity).ToArray();
            if (entry.State == EntityState.Added)
            {
                if (added.Count(x => x.Stage == "Created") != 1 || added.Length != 1) throw new InvalidOperationException("SourcingPolicyOriginalMakerRequired");
                continue;
            }
            var origin = SourcingPolicyIdentityEvidenceRecords.Local.SingleOrDefault(x => x.PolicyVersionId == policy.Id && x.Stage == "Created")
                ?? SourcingPolicyIdentityEvidenceRecords.SingleOrDefault(x => x.PolicyVersionId == policy.Id && x.Stage == "Created")
                ?? throw new InvalidOperationException("SourcingPolicyLegacyIdentityUnavailable");
            SourcingPolicyIdentity.VerifyOrigin(policy, origin);
            var oldState = entry.OriginalValues.GetValue<string>(nameof(PolicyVersion.State));
            var allowed = new HashSet<string>([nameof(PolicyVersion.Version), nameof(PolicyVersion.UpdatedAt), nameof(PolicyVersion.UpdatedBy)]);
            if (oldState == "Draft" && policy.State == "Draft")
            {
                if (scope.SubjectId != origin.ActorPrincipalId || added.Length != 0) throw new InvalidOperationException("SourcingPolicyOriginalAuthorRequired");
                allowed.UnionWith([nameof(PolicyVersion.RulesJson), nameof(PolicyVersion.EffectiveFrom), nameof(PolicyVersion.EffectiveTo)]);
            }
            else if (oldState == "Draft" && policy.State == "PendingReview" && added.Length == 1 && added[0].Stage == "Submitted")
                allowed.UnionWith([nameof(PolicyVersion.State), nameof(PolicyVersion.ReviewRequestedAt), nameof(PolicyVersion.ReviewRequestedBy), nameof(PolicyVersion.ReviewReason)]);
            else if (oldState == "PendingReview" && policy.State == "Published" && added.Length == 1 && added[0].Stage == "Published")
                allowed.UnionWith([nameof(PolicyVersion.State), nameof(PolicyVersion.PublishedAt), nameof(PolicyVersion.PublishedBy), nameof(PolicyVersion.PublicationReason)]);
            else if (oldState == "PendingReview" && policy.State == "Draft" && added.Length == 0 && scope.SubjectId != origin.ActorPrincipalId)
            {
                var submitted = SourcingPolicyIdentityEvidenceRecords.SingleOrDefault(x => x.PolicyVersionId == policy.Id && x.Stage == "Submitted" && x.PolicyRecordVersionAtAction == policy.Version)
                    ?? throw new InvalidOperationException("SourcingPolicySubmittedIdentityRequired");
                SourcingPolicyIdentity.VerifySubmission(policy, origin, submitted, false); allowed.Add(nameof(PolicyVersion.State));
            }
            else if (oldState == "Published" && policy.State == "Published" && added.Length == 0)
            {
                var published = SourcingPolicyIdentityEvidenceRecords.SingleOrDefault(x => x.PolicyVersionId == policy.Id && x.Stage == "Published")
                    ?? throw new InvalidOperationException("SourcingPolicyPublicationIdentityRequired");
                var submitted = IdentityRow(published.SubmittedEvidenceId!.Value) ?? throw new InvalidOperationException("SourcingPolicySubmittedIdentityRequired");
                SourcingPolicyIdentity.VerifyPublication(policy, origin, submitted, published);
                allowed.Add(nameof(PolicyVersion.EffectiveTo)); // Existing controlled supersession, not a new publication.
            }
            else throw new InvalidOperationException("SourcingPolicyIdentityTransitionRequired");
            if (entry.Properties.Any(x => x.IsModified && !allowed.Contains(x.Metadata.Name))) throw new InvalidOperationException("SourcingPolicyIdentityMaterialChangeDenied");
        }
    }
    private PolicyVersion? IdentityPolicy(Guid id) => PolicyVersions.Local.SingleOrDefault(x => x.Id == id) ?? PolicyVersions.SingleOrDefault(x => x.Id == id);
    private SourcingPolicyIdentityEvidence? IdentityRow(Guid id) => SourcingPolicyIdentityEvidenceRecords.Local.SingleOrDefault(x => x.Id == id)
        ?? SourcingPolicyIdentityEvidenceRecords.SingleOrDefault(x => x.Id == id);
}
