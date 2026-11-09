using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record ModuleAccessLocation(Guid Id,string? Name,bool Available);
internal sealed record ModuleAccessLocations(ModuleAccessStatus Status,ModuleAccessLocation[]? Items=null,string? Next=null);
internal sealed record ModuleAccessMembers(ModuleAccessStatus Status,CompanyTeamMember[]? Items=null,string? Next=null);
internal sealed record ModuleAccessSnapshot(Guid VersionId,int Revision,int MembershipRevision,string State,
    string[] Roles,string[] Actions,string Scope,ModuleAccessLocation[] Locations,bool RequiresReconfirmation);
internal sealed record ModuleAccessReview(ModuleAccessStatus Status,CompanyTeamMember? Member=null,string[]? Roles=null,
    string[]? Actions=null,ModuleAccessSnapshot? Grant=null);

/// <summary>Owner-only review/write orchestration. Enrollment selectors never come from the browser.</summary>
internal sealed class CoreModuleAccessApi(Func<string,HrmDbContext> contexts,
    Func<string,CancellationToken,Task<CommercialLicenseResult>> readLicense,TimeProvider clock)
{
    private sealed record Ready(ModuleAccessStatus Status,CoreCompanyAccessDecision? Scope=null,CommercialLicenseLease? License=null);
    private async Task<Ready> Resolve(OperationalSubjectObservation person,CompanyTeamActor actor,Guid company,string? module,CancellationToken ct)
    {
        if(!person.IsCurrent || person.Subject!=actor.Subject || actor.Issuer!=ProductCommercialLicense.Issuer)return new(ModuleAccessStatus.Denied);
        await using var db=contexts("untrusted-module-review");var access=new CoreCompanyAccess(db,clock);
        var candidate=await access.FindCandidateAsync(actor.Issuer,actor.Subject,actor.LinkId,company,ct);
        if(candidate is null || candidate.Role!=CoreCompanyRoles.Owner)return new(ModuleAccessStatus.Denied);
        var license=await readLicense(candidate.EnrollmentId,ct);
        if(!person.IsCurrent)return new(ModuleAccessStatus.Denied);
        if(license.Status!=CommercialLicenseStatus.Authorized || license.Lease is null)
            return new(license.Status==CommercialLicenseStatus.Denied?ModuleAccessStatus.Denied:ModuleAccessStatus.Unavailable);
        var scope=await access.ReadAsync(actor.Issuer,actor.Subject,actor.LinkId,company,license.Lease,ct);
        return scope is not null && scope.Role==CoreCompanyRoles.Owner && person.IsCurrent && license.Lease.IsCurrent
            && (module is null || license.Lease.AllowsWorkspaceModule(candidate.Slot,module))
            ?new(ModuleAccessStatus.Authorized,scope,license.Lease):new(ModuleAccessStatus.Denied);
    }
    private async Task<bool> Current(HrmDbContext db,Ready ready,OperationalSubjectObservation person,CompanyTeamActor actor,Guid company,CancellationToken ct)
    {
        var scope=await new CoreCompanyAccess(db,clock).ReadAsync(actor.Issuer,actor.Subject,actor.LinkId,company,ready.License!,ct);
        return scope is not null && scope.Role==CoreCompanyRoles.Owner && person.IsCurrent && ready.License!.IsCurrent;
    }
    internal async Task<ModuleAccessMembers> MembersAsync(OperationalSubjectObservation person,CompanyTeamActor actor,Guid company,Guid? after,CancellationToken ct=default)
    {
        try {
            var ready=await Resolve(person,actor,company,null,ct);if(ready.Status!=ModuleAccessStatus.Authorized)return new(ready.Status);
            await using var db=contexts(ready.Scope!.TenantId);
            var query=Members(db,actor.Issuer,company);
            if(after is {} cursor){var text=cursor.ToString("D");query=query.Where(x=>string.Compare(x.MembershipId.ToString().ToLower(),text)>0);}
            var scanned=await query.OrderBy(x=>x.MembershipId.ToString().ToLower()).Take(21).ToArrayAsync(ct);
            if(!await Current(db,ready,person,actor,company,ct))return new(ModuleAccessStatus.Denied);
            var page=scanned.Take(20).ToArray();return new(ModuleAccessStatus.Authorized,page.Select(x=>x.Public()).ToArray(),scanned.Length>20?page[^1].MembershipId.ToString("D"):null);
        }catch(DbException){return new(ModuleAccessStatus.Unavailable);}
    }
    private sealed class MemberRow
    {
        public Guid MembershipId{get;set;}
        public string DisplayName{get;set;}="";
        public string Role{get;set;}="";
        public bool MembershipActive{get;set;}
        public bool IdentityAvailable{get;set;}
        public int Revision{get;set;}
        internal CompanyTeamMember Public()=>new(MembershipId,DisplayName,Role,MembershipActive,IdentityAvailable,Revision);
    }
    private IQueryable<MemberRow> Members(HrmDbContext db,string issuer,Guid company)=>
        from member in db.CompanyMemberships.AsNoTracking()
        join user in db.LocalUsers.AsNoTracking() on new{member.TenantId,Id=member.LocalUserId} equals new{user.TenantId,user.Id}
        where member.LegalEntityId==company && !member.IsArchived
        select new MemberRow{MembershipId=member.Id,DisplayName=user.DisplayName,Role=member.Role,MembershipActive=member.IsActive,
            IdentityAvailable=user.IsActive && !user.IsArchived && user.CredentialMode==LocalCredentialModes.Federated
            && (user.LockedUntil==null || user.LockedUntil<=clock.GetUtcNow())
            && db.ExternalIdentityLinks.Any(link=>link.LocalUserId==user.Id && link.Issuer==issuer && link.IsActive && !link.IsArchived),Revision=member.Revision};

    internal async Task<ModuleAccessLocations> LocationsAsync(OperationalSubjectObservation person,CompanyTeamActor actor,Guid company,Guid? after,CancellationToken ct=default)
    {
        try {
            var ready=await Resolve(person,actor,company,null,ct);if(ready.Status!=ModuleAccessStatus.Authorized)return new(ready.Status);
            await using var db=contexts(ready.Scope!.TenantId);
            var query=db.WorkLocations.AsNoTracking().Where(x=>x.LegalEntityId==company && !x.IsArchived);
            if(after is {} cursor){var text=cursor.ToString("D");query=query.Where(x=>string.Compare(x.Id.ToString().ToLower(),text)>0);}
            var scanned=await query.OrderBy(x=>x.Id.ToString().ToLower()).Take(21).Select(x=>new ModuleAccessLocation(x.Id,x.Name,true)).ToArrayAsync(ct);
            if(!await Current(db,ready,person,actor,company,ct))return new(ModuleAccessStatus.Denied);
            var page=scanned.Take(20).ToArray();return new(ModuleAccessStatus.Authorized,page,scanned.Length>20?page[^1].Id.ToString("D"):null);
        }catch(DbException){return new(ModuleAccessStatus.Unavailable);}
    }
    internal async Task<ModuleAccessReview> ReviewAsync(OperationalSubjectObservation person,CompanyTeamActor actor,Guid company,Guid membership,string module,CancellationToken ct=default)
    {
        if(module is not ("hrm" or "procurement"))return new(ModuleAccessStatus.Invalid);
        try {
            var ready=await Resolve(person,actor,company,module,ct);if(ready.Status!=ModuleAccessStatus.Authorized)return new(ready.Status);
            await using var db=contexts(ready.Scope!.TenantId);
            var member=await Members(db,actor.Issuer,company).SingleOrDefaultAsync(x=>x.MembershipId==membership,ct);
            if(member is null)return new(ModuleAccessStatus.Denied);
            var versions=db.ModuleAccessVersions.IgnoreQueryFilters().AsNoTracking().Where(x=>x.TenantId==ready.Scope.TenantId
                && x.LegalEntityId==company && x.MembershipId==membership && x.ModuleCode==module).OrderByDescending(x=>x.Revision);
            var version=await versions.FirstOrDefaultAsync(ct);ModuleAccessSnapshot? snapshot=null;
            if(version is not null) {
                if(version.IsArchived || version.PolicyVersion!=1 || version.State is not ("granted" or "revoked"))return new(ModuleAccessStatus.Unavailable);
                var entries=await db.ModuleAccessLocations.IgnoreQueryFilters().AsNoTracking().Where(x=>x.GrantVersionId==version.Id).Take(129).ToArrayAsync(ct);
                if(entries.Length>128 || entries.Any(x=>x.IsArchived || x.TenantId!=ready.Scope.TenantId || x.LegalEntityId!=company))return new(ModuleAccessStatus.Unavailable);
                var ids=entries.Select(x=>x.LocationId).ToArray();
                var owned=await db.WorkLocations.AsNoTracking().Where(x=>x.LegalEntityId==company && !x.IsArchived && ids.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,x=>x.Name,ct);
                var locations=ids.Order().Select(id=>new ModuleAccessLocation(id,owned.GetValueOrDefault(id),owned.ContainsKey(id))).ToArray();
                var roles=CoreModuleAccessCatalog.Parse(version.RoleKeysCsv);var actions=CoreModuleAccessCatalog.Parse(version.ActionCodesCsv);
                var stale=version.MembershipRevision!=member.Revision || !member.MembershipActive || !member.IdentityAvailable
                    || locations.Any(x=>!x.Available) || !CoreModuleAccessCatalog.Canonical(version.RoleKeysCsv) || !CoreModuleAccessCatalog.Canonical(version.ActionCodesCsv)
                    || version.State=="granted" && (!CoreModuleAccessCatalog.Valid(module,roles,actions)
                        || !(version.ScopeKind=="company" && ids.Length==0 || version.ScopeKind=="locations" && ids.Length>0));
                snapshot=new(version.Id,version.Revision,version.MembershipRevision,version.State,roles,actions,version.ScopeKind,locations,stale);
            }
            var latest=await versions.Select(x=>x.Id).FirstOrDefaultAsync(ct);
            if(latest!=(version?.Id??Guid.Empty) || !await Members(db,actor.Issuer,company).AnyAsync(x=>x.MembershipId==membership && x.Revision==member.Revision,ct))return new(ModuleAccessStatus.Conflict);
            if(!await Current(db,ready,person,actor,company,ct))return new(ModuleAccessStatus.Denied);
            return new(ModuleAccessStatus.Authorized,member.Public(),CoreModuleAccessCatalog.Roles(module),CoreModuleAccessCatalog.Actions(module),snapshot);
        }catch(DbException){return new(ModuleAccessStatus.Unavailable);}
    }
    internal async Task<ModuleAccessResult> ExecuteAsync(OperationalSubjectObservation person,CompanyTeamActor actor,ModuleAccessCommand command,CancellationToken ct=default)
    {
        if(NormalizedModuleAccess.Read(command) is null)return new(ModuleAccessStatus.Invalid);
        try {
            var ready=await Resolve(person,actor,command.CompanyId,command.ModuleCode,ct);
            return ready.Status==ModuleAccessStatus.Authorized
                ?await new CoreModuleAccessCommands(contexts,clock).ExecuteAsync(person,actor,command,ready.License!,ct):new(ready.Status);
        }catch(DbException){return new(ModuleAccessStatus.Unavailable);}
    }
}
