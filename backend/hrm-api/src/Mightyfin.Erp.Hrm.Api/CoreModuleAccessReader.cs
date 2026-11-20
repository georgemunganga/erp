using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed class CompanyModuleAccessDecision(Guid company,Guid principal,string tenant,Guid version,int revision,string module,
    string[] roles,string[] actions,bool allCompany,Guid[] locations,OperationalSubjectObservation person,CommercialLicenseLease license)
{
    internal Guid CompanyId{get;}=company;
    internal Guid PrincipalId{get;}=principal;
    internal string TenantId{get;}=tenant;
    internal Guid VersionId{get;}=version;
    internal int Revision{get;}=revision;
    internal string Module{get;}=module;
    internal IReadOnlyList<string> Roles{get;}=Array.AsReadOnly(roles);
    internal IReadOnlyList<string> Actions{get;}=Array.AsReadOnly(actions);
    internal bool AllCompanyLocations{get;}=allCompany;
    internal IReadOnlyList<Guid> LocationIds{get;}=Array.AsReadOnly(locations);
    internal bool IsCurrent=>person.IsCurrent && license.IsCurrent;
    internal bool AllowsAction(string action)=>IsCurrent && Actions.Contains(action,StringComparer.Ordinal);
}
internal sealed record ModuleAccessReadResult(ModuleAccessStatus Status,CompanyModuleAccessDecision? Decision=null);

/// <summary>Fresh explicit current grant only. No role/bootstrap merge or older-version fallback.</summary>
internal sealed class CoreModuleAccessReader(HrmDbContext db,TimeProvider clock)
{
    internal async Task<ModuleAccessReadResult> ReadAsync(OperationalSubjectObservation person,Guid link,Guid company,string module,
        CommercialLicenseLease license,CancellationToken ct=default)
    {
        if(!person.IsCurrent || !license.IsCurrent || module is not ("hrm" or "procurement"))return new(ModuleAccessStatus.Denied);
        try {
            var access=new CoreCompanyAccess(db,clock);
            var candidate=await access.FindCandidateAsync(ProductCommercialLicense.Issuer,person.Subject,link,company,ct);
            var scope=await access.ReadAsync(ProductCommercialLicense.Issuer,person.Subject,link,company,license,ct);
            if(candidate is null || scope is null || !license.AllowsWorkspaceModule(candidate.Slot,module))return new(ModuleAccessStatus.Denied);
            if(license.UserLimit(module) is {} cap && await CoreModuleSeats.CountAsync(db,scope.TenantId,company,module,null,cap+1,ct)>cap)
                return new(ModuleAccessStatus.Denied);
            var member=await db.CompanyMemberships.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==candidate.MembershipId
                && x.TenantId==candidate.TenantId && x.LegalEntityId==company && x.IsActive && !x.IsArchived,ct);
            if(member is null)return new(ModuleAccessStatus.Denied);
            // Include archived/revoked rows in ordering. Corruption/revocation of
            // the newest version must never revive an older permissive version.
            var version=await db.ModuleAccessVersions.IgnoreQueryFilters().AsNoTracking().Where(x=>x.TenantId==scope.TenantId && x.LegalEntityId==company
                && x.MembershipId==member.Id && x.ModuleCode==module).OrderByDescending(x=>x.Revision).FirstOrDefaultAsync(ct);
            if(version is null || version.IsArchived || version.State!="granted" || version.PolicyVersion!=1 || version.MembershipRevision!=member.Revision
                || !CoreModuleAccessCatalog.Canonical(version.RoleKeysCsv) || !CoreModuleAccessCatalog.Canonical(version.ActionCodesCsv))return new(ModuleAccessStatus.Denied);
            var roles=CoreModuleAccessCatalog.Parse(version.RoleKeysCsv);var actions=CoreModuleAccessCatalog.Parse(version.ActionCodesCsv);
            if(!CoreModuleAccessCatalog.Valid(module,roles,actions))return new(ModuleAccessStatus.Denied);
            var entries=await db.ModuleAccessLocations.IgnoreQueryFilters().AsNoTracking().Where(x=>x.GrantVersionId==version.Id).ToArrayAsync(ct);
            if(entries.Length>128 || entries.Any(x=>x.TenantId!=scope.TenantId || x.LegalEntityId!=company || x.IsArchived))return new(ModuleAccessStatus.Denied);
            var locations=entries.Select(x=>x.LocationId).Order().ToArray();
            if(locations.Distinct().Count()!=locations.Length || version.ScopeKind=="company" && locations.Length!=0
                || version.ScopeKind=="locations" && locations.Length==0 || version.ScopeKind is not ("company" or "locations"))return new(ModuleAccessStatus.Denied);
            if(locations.Length>0) {
                var owned=await db.WorkLocations.IgnoreQueryFilters().AsNoTracking().CountAsync(x=>x.TenantId==scope.TenantId && x.LegalEntityId==company
                    && !x.IsArchived && locations.Contains(x.Id),ct);
                if(owned!=locations.Length)return new(ModuleAccessStatus.Denied);
            }
            if(!person.IsCurrent || !license.IsCurrent)return new(ModuleAccessStatus.Denied);
            var latest=await db.ModuleAccessVersions.IgnoreQueryFilters().AsNoTracking().Where(x=>x.TenantId==scope.TenantId && x.LegalEntityId==company
                && x.MembershipId==member.Id && x.ModuleCode==module).OrderByDescending(x=>x.Revision).Select(x=>x.Id).FirstOrDefaultAsync(ct);
            var finalScope=await access.ReadAsync(ProductCommercialLicense.Issuer,person.Subject,link,company,license,ct);
            if(latest!=version.Id || finalScope is null || !await db.CompanyMemberships.IgnoreQueryFilters().AsNoTracking().AnyAsync(x=>x.Id==member.Id
                && x.TenantId==scope.TenantId && x.LegalEntityId==company && x.IsActive && !x.IsArchived && x.Revision==member.Revision,ct)
                || !person.IsCurrent || !license.IsCurrent)return new(ModuleAccessStatus.Denied);
            return new(ModuleAccessStatus.Authorized,new(company,scope.PrincipalId,scope.TenantId,version.Id,version.Revision,module,roles,actions,
                version.ScopeKind=="company",locations,person,license));
        }catch(DbException){return new(ModuleAccessStatus.Unavailable);}
    }
}
