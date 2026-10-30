using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Availability only; source, current policy and independent assignment
/// remain command-owned checks. Historical reads do not depend on Core Documents.</summary>
internal static class ProcurementInvoiceManualUnitReviewCapability
{
    internal sealed record Availability(bool Read,bool NewWork);
    internal static async Task<Availability> ReadyAsync(IServiceProvider services,
        ProcurementRequestScope scope,HttpContext http,bool pending,CancellationToken ct)
    {
        var unavailable=new Availability(false,false);
        var options=services.GetService<ProcurementInvoiceUnitReviewOptions>();
        var schema=services.GetService<IProcurementInvoiceUnitReviewSchemaReadiness>();
        var coordinator=services.GetService<ProcurementApprovalTransaction>();
        var auth=services.GetService<IAuthorizationService>();
        if(pending || options is not {ExecutionDatabaseConfigured:true} || schema is null
            || coordinator is null || auth is null
            || !ProcurementPrincipalInvoiceMatchingCapability.ReadContext(scope))return unavailable;
        try
        {
            return await coordinator.ExecuteAsync(async(db,cases,token)=>
            {
                if(!await ProcurementInvoiceManualUnitReviewSchemaCapture.IsInstalledAsync(db,token)
                    || !await schema.IsReadyAsync(db,token))return unavailable;
                await using var core=await ProcurementApprovalExecutionLocks.CoreAsync(db,scope,token);
                var actor=await ProcurementInvoiceUnitReviewPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,scope.SubjectId,token);
                if(actor is null || actor.PrincipalId!=scope.PrincipalId?.ToString("D")
                    || !actor.Permissions.Contains("procurement-access"))return unavailable;
                var actions=new[]{InvoiceUnitReviewTransitions.RequestPermission,
                    InvoiceRetainedUnitReview.ReviewPermission,InvoiceUnitReviewTransitions.ReadPermission};
                var permitted=false;
                foreach(var action in actions)
                    permitted|=actor.Permissions.Contains(action)
                        && (await auth.AuthorizeAsync(http.User,null,action)).Succeeded
                        && (action==InvoiceRetainedUnitReview.ReviewPermission
                            || actor.Permissions.Contains("procurement-invoice-read")
                            && (await auth.AuthorizeAsync(http.User,null,"procurement-invoice-read")).Succeeded);
                if(!permitted)return unavailable;
                _=await ProcurementExecutionRecordScope.Branches(core,scope,null,false,token);
                var write=options.ManualAttestationEnabled
                    && ProcurementInvoiceUnitReviewRoutes.ManualWriteContext(scope,http.Request.Headers.ContainsKey("X-Shell-Branch-Scope"))
                    && await schema.CanRecordAsync(db,token);
                return new Availability(true,write);
            },ct);
        }
        catch(Exception error)when(error is not OperationCanceledException){return unavailable;}
    }
}
