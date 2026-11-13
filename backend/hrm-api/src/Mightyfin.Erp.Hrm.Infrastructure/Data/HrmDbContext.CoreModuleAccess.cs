using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Infrastructure.Data;

public sealed partial class HrmDbContext
{
    public DbSet<CoreModuleAccessVersion> ModuleAccessVersions => Set<CoreModuleAccessVersion>();
    public DbSet<CoreModuleAccessLocation> ModuleAccessLocations => Set<CoreModuleAccessLocation>();
    public DbSet<CoreModuleAccessReceipt> ModuleAccessReceipts => Set<CoreModuleAccessReceipt>();

    private void ConfigureModuleAccess(ModelBuilder model)
    {
        // Tenant key only: retain legacy work-location company moves. A scoped
        // grant must recheck current company ownership, never trust this FK alone.
        model.Entity<WorkLocation>().HasAlternateKey(x=>new{x.TenantId,x.Id});
        model.Entity<WorkLocation>().Property(x=>x.TenantId).ValueGeneratedOnAdd()
            .HasValueGenerator((_,_)=>new ScopedTenantKeyGenerator());
        ConfigureEntity<CoreModuleAccessVersion>(model,"core_module_access_versions",e=> {
            e.Property(x=>x.ModuleCode).HasMaxLength(16);
            e.Property(x=>x.State).HasMaxLength(8);e.Property(x=>x.ScopeKind).HasMaxLength(12);
            e.Property(x=>x.RoleKeysCsv).HasMaxLength(2048);e.Property(x=>x.ActionCodesCsv).HasMaxLength(32768);
            e.Property(x=>x.TenantId).ValueGeneratedOnAdd().HasValueGenerator((_,_)=>new ScopedTenantKeyGenerator());
            e.HasAlternateKey(x=>new{x.TenantId,x.LegalEntityId,x.Id});
            e.HasAlternateKey(x=>new{x.TenantId,x.LegalEntityId,x.MembershipId,x.ModuleCode,x.Id,x.Revision});
            e.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.MembershipId,x.ModuleCode,x.Revision}).IsUnique();
            e.HasOne<CoreCompanyMembership>().WithMany().HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.MembershipId})
                .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.Id}).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CoreCompanyMembership>().WithMany().HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.ActingMembershipId})
                .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.Id}).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CoreModuleAccessVersion>().WithMany().HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.MembershipId,x.ModuleCode,x.PreviousVersionId,x.PreviousRevision})
                .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.MembershipId,x.ModuleCode,x.Id,x.Revision}).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("core_module_access_versions","hrm",t=> {
                t.HasCheckConstraint("CK_module_access_version","policy_version = 1 AND membership_revision >= 1 AND acting_membership_revision >= 1 AND revision >= 1 AND previous_revision >= 0 AND CAST(revision AS bigint) = CAST(previous_revision AS bigint) + 1");
                t.HasCheckConstraint("CK_module_access_predecessor","(revision = 1 AND previous_version_id IS NULL) OR (revision > 1 AND previous_version_id IS NOT NULL)");
                t.HasCheckConstraint("CK_module_access_module","module_code IN ('hrm','procurement')");
                t.HasCheckConstraint("CK_module_access_state","(state = 'granted' AND scope_kind IN ('company','locations') AND length(action_codes_csv) > 0) OR (state = 'revoked' AND scope_kind = 'none' AND role_keys_csv = '' AND action_codes_csv = '')");
                t.HasCheckConstraint("CK_module_access_not_archived","NOT is_archived");
            });
        });
        ConfigureEntity<CoreModuleAccessLocation>(model,"core_module_access_locations",e=> {
            e.Property(x=>x.TenantId).ValueGeneratedOnAdd().HasValueGenerator((_,_)=>new ScopedTenantKeyGenerator());
            e.HasIndex(x=>new{x.TenantId,x.GrantVersionId,x.LocationId}).IsUnique();
            e.HasOne<CoreModuleAccessVersion>().WithMany().HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.GrantVersionId})
                .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.Id}).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<WorkLocation>().WithMany().HasForeignKey(x=>new{x.TenantId,x.LocationId})
                .HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("core_module_access_locations","hrm",t=>t.HasCheckConstraint("CK_module_access_location_not_archived","NOT is_archived"));
        });
        ConfigureEntity<CoreModuleAccessReceipt>(model,"core_module_access_receipts",e=> {
            e.Property(x=>x.TenantId).ValueGeneratedOnAdd().HasValueGenerator((_,_)=>new ScopedTenantKeyGenerator());
            e.Property(x=>x.ModuleCode).HasMaxLength(16);e.Property(x=>x.ActorIssuer).HasMaxLength(200);e.Property(x=>x.ActorSubject).HasMaxLength(19);
            e.Property(x=>x.RequestHash).HasMaxLength(64);
            e.HasIndex(x=>new{x.ActorIssuer,x.ActorSubject,x.CommandKey}).IsUnique();
            e.HasOne<CoreCompanyMembership>().WithMany().HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.ActingMembershipId})
                .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.Id}).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CoreModuleAccessVersion>().WithMany().HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.MembershipId,x.ModuleCode,x.ResultVersionId,x.ResultRevision})
                .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.MembershipId,x.ModuleCode,x.Id,x.Revision}).OnDelete(DeleteBehavior.Restrict);
            e.ToTable("core_module_access_receipts","hrm",t=>t.HasCheckConstraint("CK_module_access_receipt","result_revision >= 1 AND length(request_hash) = 64 AND NOT is_archived"));
        });
    }
}
