using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<ProcurementSpendingAuthorityGrant> SpendingAuthorityGrants => Set<ProcurementSpendingAuthorityGrant>();
    public DbSet<ProcurementSpendingAuthorityCategory> SpendingAuthorityCategories => Set<ProcurementSpendingAuthorityCategory>();

    private void ConfigureSpendingAuthority(ModelBuilder model)
    {
        ConfigureBase<ProcurementSpendingAuthorityGrant>(model, "spending_authority_grants");
        ConfigureBase<ProcurementSpendingAuthorityCategory>(model, "spending_authority_categories");
        var grants = model.Entity<ProcurementSpendingAuthorityGrant>();
        grants.Property(x => x.AuthorityReference).HasMaxLength(100);
        grants.Property(x => x.PrincipalId).HasMaxLength(200);
        grants.Property(x => x.Permission).HasMaxLength(100);
        grants.Property(x => x.Action).HasMaxLength(100);
        grants.Property(x => x.State).HasMaxLength(30);
        grants.Property(x => x.ConfigurationOwnerPrincipalId).HasMaxLength(200);
        grants.Property(x => x.SubmittedByPrincipalId).HasMaxLength(200);
        grants.Property(x => x.ActivatedByPrincipalId).HasMaxLength(200);
        grants.Property(x => x.RevokedByPrincipalId).HasMaxLength(200);
        grants.Property(x => x.PublishedBusinessFingerprint).HasMaxLength(64);
        grants.Property(x => x.SubmittedBusinessFingerprint).HasMaxLength(64);
        grants.Property(x => x.ReviewReturnReason).HasMaxLength(2000);
        grants.Property(x => x.ActivationReason).HasMaxLength(2000);
        grants.Property(x => x.RevocationReason).HasMaxLength(2000);
        grants.Property(x => x.CurrencyCode).HasMaxLength(3);
        grants.Property(x => x.AmountBasis).HasMaxLength(100);
        grants.Property(x => x.MaximumAmount).HasPrecision(18, 4);
        grants.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PrincipalId, x.Permission,
            x.Action, x.AuthorityReference, x.AuthorityVersion }).IsUnique();
        // One Active row per logical authority, including future-effective rows. Older
        // versions are explicitly terminal before a replacement can publish.
        grants.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.PrincipalId, x.Permission,
            x.Action, x.AuthorityReference }).IsUnique().HasFilter("state = 'Active'");
        grants.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.BranchRef, x.DepartmentRef, x.State });
        grants.HasMany(x => x.Categories).WithOne().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GrantId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var categories = model.Entity<ProcurementSpendingAuthorityCategory>();
        categories.Property(x => x.CategoryCode).HasMaxLength(100);
        categories.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.GrantId, x.CategoryCode }).IsUnique();
        grants.HasOne<ProcurementSpendingAuthorityGrant>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SupersededByGrantId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }

    private void GuardSpendingAuthorityChanges()
    {
        var grants = ChangeTracker.Entries<ProcurementSpendingAuthorityGrant>().ToArray();
        foreach (var entry in grants.Where(x => x.State == EntityState.Added))
            if (entry.Entity.State != "Draft" || entry.Entity.PublishedBusinessFingerprint is not null)
                throw new InvalidOperationException("AuthorityMustStartAsDraft");
        foreach (var entry in grants.Where(x => x.State == EntityState.Modified))
        {
            var original = (ProcurementSpendingAuthorityGrant)entry.OriginalValues.ToObject();
            if (original.TenantId != entry.Entity.TenantId || original.LegalEntityId != entry.Entity.LegalEntityId
                || original.PrincipalId != entry.Entity.PrincipalId || original.Permission != entry.Entity.Permission
                || original.Action != entry.Entity.Action || original.AuthorityReference != entry.Entity.AuthorityReference
                || original.AuthorityVersion != entry.Entity.AuthorityVersion
                || original.ConfigurationOwnerPrincipalId != entry.Entity.ConfigurationOwnerPrincipalId
                || original.CreatedBy != entry.Entity.CreatedBy || original.CreatedAt != entry.Entity.CreatedAt)
                throw new InvalidOperationException("AuthorityLogicalKeyIsImmutable");
            if (original.State == "Draft" && entry.Entity.State is not ("Draft" or "PendingReview"))
                throw new InvalidOperationException("AuthorityTransitionInvalid");
            if (original.State == "PendingReview" && entry.Entity.State is not ("Draft" or "Active"))
                throw new InvalidOperationException("AuthorityTransitionInvalid");
            if (entry.Entity.State == "PendingReview" && (entry.Entity.SubmittedByPrincipalId != scope.SubjectId
                || entry.Entity.SubmittedAt is null || entry.Entity.SubmittedBusinessFingerprint
                    != SpendingAuthorityConfiguration.Fingerprint(entry.Entity)))
                throw new InvalidOperationException("SubmittedAuthorityChanged");
            if (original.State == "PendingReview" && entry.Entity.State == "Draft"
                && (string.IsNullOrWhiteSpace(entry.Entity.ReviewReturnReason)
                    || entry.Entity.SubmittedBusinessFingerprint is not null))
                throw new InvalidOperationException("AuthorityReviewReturnEvidenceRequired");
            if (original.PublishedBusinessFingerprint is not null)
            {
                original.Categories = SpendingAuthorityCategories.AsNoTracking().Where(x => x.GrantId == original.Id).ToList();
                SpendingAuthorityConfiguration.AssertPublishedBusinessUnchanged(original, entry.Entity);
                if (original.SubmittedAt != entry.Entity.SubmittedAt
                    || original.SubmittedByPrincipalId != entry.Entity.SubmittedByPrincipalId
                    || original.SubmittedBusinessFingerprint != entry.Entity.SubmittedBusinessFingerprint
                    || original.ActivatedAt != entry.Entity.ActivatedAt
                    || original.ActivatedByPrincipalId != entry.Entity.ActivatedByPrincipalId
                    || original.ActivationReason != entry.Entity.ActivationReason)
                    throw new InvalidOperationException("AuthorityReviewHistoryIsImmutable");
            }
            if (entry.Entity.State == "Active" && original.State == "PendingReview"
                && (original.SubmittedBusinessFingerprint != SpendingAuthorityConfiguration.Fingerprint(entry.Entity)
                    || original.SubmittedBusinessFingerprint != entry.Entity.SubmittedBusinessFingerprint
                    || original.SubmittedByPrincipalId != entry.Entity.SubmittedByPrincipalId
                    || original.SubmittedAt != entry.Entity.SubmittedAt
                    || entry.Entity.ActivatedByPrincipalId != scope.SubjectId || entry.Entity.ActivatedAt is null
                    || string.IsNullOrWhiteSpace(entry.Entity.ActivationReason)
                    || entry.Entity.PublishedBusinessFingerprint != SpendingAuthorityConfiguration.Fingerprint(entry.Entity)
                    || entry.Entity.ActivatedByPrincipalId == entry.Entity.PrincipalId
                    || entry.Entity.ActivatedByPrincipalId == entry.Entity.CreatedBy
                    || entry.Entity.ActivatedByPrincipalId == entry.Entity.ConfigurationOwnerPrincipalId
                    || entry.Entity.ActivatedByPrincipalId == entry.Entity.SubmittedByPrincipalId))
                throw new InvalidOperationException("IndependentAuthorityReviewerRequired");
            if (entry.Entity.State == "Revoked" && (entry.Entity.RevokedByPrincipalId != scope.SubjectId
                || entry.Entity.RevokedAt is null || string.IsNullOrWhiteSpace(entry.Entity.RevocationReason)))
                throw new InvalidOperationException("AuthorityRevocationEvidenceRequired");
            if (entry.Entity.State == "Superseded" && (entry.Entity.SupersededByGrantId is null
                || entry.Entity.SupersededAt is null))
                throw new InvalidOperationException("AuthoritySupersessionEvidenceRequired");
            if (original.State is "Revoked" or "Superseded")
                throw new InvalidOperationException("TerminalAuthorityIsImmutable");
        }
        foreach (var entry in ChangeTracker.Entries<ProcurementSpendingAuthorityCategory>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var originalParentId = entry.State == EntityState.Added ? entry.Entity.GrantId
                : entry.OriginalValues.GetValue<Guid>(nameof(ProcurementSpendingAuthorityCategory.GrantId));
            if (originalParentId != entry.Entity.GrantId)
                throw new InvalidOperationException("AuthorityCategoryParentIsImmutable");
            var newParent = grants.SingleOrDefault(x => x.Entity.Id == originalParentId && x.State == EntityState.Added);
            var parent = newParent?.Entity ?? SpendingAuthorityGrants.AsNoTracking()
                .SingleOrDefault(x => x.Id == originalParentId);
            if (parent is null || parent.State != "Draft" || parent.PublishedBusinessFingerprint is not null)
                throw new InvalidOperationException("PublishedAuthorityCategoryIsImmutable");
        }
    }
}
