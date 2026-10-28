using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Resolves current Core authority inside the receiving transaction.
/// An employee alias is optional; identity, permission and branch scope are mandatory.</summary>
internal static class ProcurementPrincipalReceivingActor
{
    internal static async Task<PrincipalReceivingActor> CurrentAsync(HrmDbContext core,
        ProcurementRequestScope scope,HttpContext http,IAuthorizationService authorization,
        string action,CancellationToken ct)
    {
        if(scope.PrincipalId?.ToString("D")!=scope.SubjectId || scope.OrgUnitId is not null
            || scope.IsConfined&&scope.WorkLocationId is null&&!scope.IsExplicitAllBranches)
            throw new UnauthorizedAccessException("PrincipalReceivingScopeUnsupported");
        var person=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,scope.SubjectId,ct);
        if(person is null || person.CoreLinkedWorkerId!=scope.CoreLinkedWorkerId
            || !person.Permissions.Contains(action)
            || !(await authorization.AuthorizeAsync(http.User,action)).Succeeded)
            throw new UnauthorizedAccessException("PrincipalReceivingActionDenied");
        var allowed=person.AllowedBranches.ToArray();
        var branches=await core.WorkLocations.AsNoTracking().Where(site=>site.TenantId==scope.TenantId
            &&site.LegalEntityId==scope.LegalEntityId&&!site.IsArchived
            &&(person.OrganisationWide||allowed.Contains(site.Id))
            &&(scope.WorkLocationId==null||site.Id==scope.WorkLocationId)).Select(site=>site.Id).ToArrayAsync(ct);
        if(branches.Length==0)throw new UnauthorizedAccessException("PrincipalReceivingActionDenied");
        return new(person.PrincipalId,person.CoreLinkedWorkerId,scope.TenantId,scope.LegalEntityId,
            branches.ToHashSet(),person.Permissions);
    }

    internal static async Task RequireIndependentAsync(HrmDbContext core,PrincipalReceivingActor actor,
        IEnumerable<PrincipalReceivingPerson> retainedPeople,CancellationToken ct)
    {
        foreach(var original in retainedPeople)
            if(!await ProcurementPrincipalPeople.IndependentNow(core,actor.TenantId,original.PrincipalId,
                original.WorkerAliasId,actor.PrincipalId,actor.WorkerAliasId,ct))
                throw new UnauthorizedAccessException("PrincipalReceivingIndependentActorRequired");
    }
}
