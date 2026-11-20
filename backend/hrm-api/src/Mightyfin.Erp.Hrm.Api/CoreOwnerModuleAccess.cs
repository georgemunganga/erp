using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Company owners administer enabled modules across their company. Persist the
/// decision through the ordinary versioned command; never invent session authority.</summary>
internal sealed class CoreOwnerModuleAccess(Func<string,HrmDbContext> contexts,TimeProvider clock)
{
    internal async Task<ModuleAccessStatus> EnsureAsync(OperationalSubjectObservation person,CompanyTeamActor actor,
        Guid company,string module,CommercialLicenseLease license,CancellationToken ct=default)
    {
        if(!person.IsCurrent || !license.IsCurrent || person.Subject!=actor.Subject
            || actor.Issuer!=ProductCommercialLicense.Issuer || module is not ("hrm" or "procurement"))return ModuleAccessStatus.Denied;
        await using var lookup=contexts("untrusted-owner-default-lookup");
        var access=new CoreCompanyAccess(lookup,clock);
        var candidate=await access.FindCandidateAsync(actor.Issuer,actor.Subject,actor.LinkId,company,ct);
        var scope=await access.ReadAsync(actor.Issuer,actor.Subject,actor.LinkId,company,license,ct);
        if(candidate is null || scope is null)return ModuleAccessStatus.Denied;
        // Delegated users retain their explicit grants, including branch restrictions.
        if(scope.Role!=CoreCompanyRoles.Owner)return ModuleAccessStatus.Unchanged;
        if(!license.AllowsWorkspaceModule(candidate.Slot,module))return ModuleAccessStatus.Denied;
        await using var db=contexts(scope.TenantId);
        var member=await db.CompanyMemberships.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==candidate.MembershipId
            && x.LegalEntityId==company && x.IsActive && !x.IsArchived,ct);
        if(member is null || member.Role!=CoreCompanyRoles.Owner)return ModuleAccessStatus.Denied;
        var prior=await db.ModuleAccessVersions.IgnoreQueryFilters().AsNoTracking().Where(x=>x.TenantId==scope.TenantId
            && x.LegalEntityId==company && x.MembershipId==member.Id && x.ModuleCode==module)
            .OrderByDescending(x=>x.Revision).FirstOrDefaultAsync(ct);
        if(prior is {IsArchived:true} || prior is not null && prior.PolicyVersion!=1)return ModuleAccessStatus.Unavailable;
        // Employee self-service remains a separate, explicitly assigned employee link/role.
        var roles=CoreModuleAccessCatalog.Roles(module).Where(x=>x!="employee")
            .Concat(prior is {State:"granted"} && CoreModuleAccessCatalog.Parse(prior.RoleKeysCsv).Contains("employee")?["employee"]:[])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var actions=CoreModuleAccessCatalog.Actions(module).Order(StringComparer.Ordinal).ToArray();
        if(prior is {State:"granted",ScopeKind:"company"} && prior.MembershipRevision==member.Revision
            && prior.RoleKeysCsv==string.Join(',',roles) && prior.ActionCodesCsv==string.Join(',',actions))return ModuleAccessStatus.Unchanged;
        // Expected version plus stable key gives concurrent first opens one auditable outcome.
        var bytes=SHA256.HashData(Encoding.UTF8.GetBytes($"core.owner.module.defaults.v1\n{company:D}\n{member.Id:D}\n{member.Revision}\n{module}\n{prior?.Revision??0}"));
        var command=new ModuleAccessCommand(company,member.Id,member.Revision,module,prior?.Revision??0,new Guid(bytes.AsSpan(0,16)),
            false,roles,actions,"company",[],true,true);
        return (await new CoreModuleAccessCommands(contexts,clock).ExecuteAsync(person,actor,command,license,ct)).Status;
    }
}
