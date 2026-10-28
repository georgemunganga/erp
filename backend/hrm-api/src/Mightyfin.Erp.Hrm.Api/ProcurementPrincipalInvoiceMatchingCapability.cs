using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Configured UI availability only. Every route still authorizes the
/// actor, source record, current policy, approved owner and command separately.</summary>
internal static class ProcurementPrincipalInvoiceMatchingCapability
{
    internal sealed record Availability(bool Read,bool NewWork);

    internal static bool ReadContext(ProcurementRequestScope scope) =>
        scope.OrgUnitId is null && scope.PrincipalId?.ToString("D") == scope.SubjectId
        && (!scope.IsConfined || scope.WorkLocationId is not null || scope.IsExplicitAllBranches);

    internal static bool WriteContext(ProcurementRequestScope scope,bool rawAll) =>
        ReadContext(scope) && scope.WorkLocationId is { } branch && branch!=Guid.Empty
        && !scope.IsExplicitAllBranches && !rawAll;

    internal static async Task<Availability> ReadyAsync(IServiceProvider services,
        ProcurementRequestScope scope,HttpContext http,bool pending,CancellationToken ct)
    {
        var unavailable=new Availability(false,false);
        var options=services.GetService<ProcurementInvoiceMatchingExecutionOptions>();
        var legacy=services.GetService<IProcurementInvoiceMatchingSchemaReadiness>();
        var baseline=services.GetService<IProcurementInvoiceOwnerBaselineReadiness>();
        var coordinator=services.GetService<ProcurementApprovalTransaction>();
        var authorization=services.GetService<IAuthorizationService>();
        if(pending || !ReadContext(scope)
            || options is not { Enabled:true,ExecutionDatabaseConfigured:true }
            || legacy is null || baseline is null || coordinator is null || authorization is null)
            return unavailable;
        try
        {
            if(!await legacy.IsReadyAsync(ct)
                || !(await authorization.AuthorizeAsync(http.User,"procurement-invoice-read")).Succeeded)
                return unavailable;
            var rawAll=http.Request.Headers.ContainsKey("X-Shell-Branch-Scope");
            var writeConfigured=options.PrincipalSourcesEnabled && WriteContext(scope,rawAll)
                && (await authorization.AuthorizeAsync(http.User,"procurement-invoice-capture")).Succeeded
                && (await authorization.AuthorizeAsync(http.User,"procurement-invoice-match")).Succeeded;
            return await coordinator.ExecuteAsync(async(db,cases,token)=>
            {
                await using var core=await ProcurementApprovalExecutionLocks.CoreAsync(db,scope,token);
                var person=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,
                    scope.LegalEntityId,scope.SubjectId,token);
                if(person is null || person.PrincipalId!=scope.SubjectId
                    || !person.Permissions.Contains("procurement-invoice-read"))return unavailable;
                var allowed=person.AllowedBranches.ToArray();
                var activeBranch=await core.WorkLocations.AsNoTracking().AnyAsync(site=>
                    site.TenantId==scope.TenantId && site.LegalEntityId==scope.LegalEntityId
                    && !site.IsArchived && (person.OrganisationWide || allowed.Contains(site.Id))
                    && (scope.WorkLocationId==null || site.Id==scope.WorkLocationId),token);
                if(!activeBranch || !await baseline.CanReadAsync(db,token)
                    || !await ProcurementPrincipalInvoiceMatchingReadiness.IsReadyAsync(db,cases,false,
                        services.GetRequiredService<IConfiguration>(),token))
                    return unavailable;
                var writeActor=writeConfigured && person.Permissions.Contains("procurement-invoice-capture")
                    && person.Permissions.Contains("procurement-invoice-match")
                    && (person.OrganisationWide || allowed.Contains(scope.WorkLocationId!.Value));
                var write=writeActor
                    && await ProcurementPrincipalInvoiceMatchingReadiness.IsReadyAsync(db,cases,true,
                        services.GetRequiredService<IConfiguration>(),token);
                return new Availability(true,write);
            },ct);
        }
        catch(Exception error)when(error is not OperationCanceledException){return unavailable;}
    }
}
