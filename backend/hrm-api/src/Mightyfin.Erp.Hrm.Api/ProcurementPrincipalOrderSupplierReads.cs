using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalOrderDraftRoutes
{
    /// <summary>Purpose-only supplier picker. It exposes no contact, bank, tax,
    /// qualification or general vendor profile fields. Create rechecks eligibility.</summary>
    private static Task<IResult> EligibleSuppliers(int? page,int? pageSize,string? q,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)=>Run(http,scope,async(db,cases,core,auth,token)=>
    {
        var paging=ProcurementSourcingDiscoveryQueries.Paging(page,pageSize,q);
        var actor=await Actor(core,scope,http,auth,"procurement-po-convert",null,false,token);
        var branches=await ProcurementExecutionRecordScope.Branches(core,scope,null,false,token);
        if(!actor.OrganisationWide && branches.Length==0)
            throw new Problem("OrderBranchUnavailable",403);
        var now=DateTimeOffset.UtcNow;
        var query=db.Suppliers.AsNoTracking().Where(s=>s.Status=="Active"&&s.IsOrderable
            &&(s.EligibleFrom==null||s.EligibleFrom<=now)
            &&(s.EligibleUntil==null||s.EligibleUntil>=now)
            &&(s.PaymentVerificationToken==null||s.PaymentVerificationStatus=="Verified"));
        if(paging.Search.Length>0)query=query.Where(s=>EF.Functions.ILike(s.DisplayName,paging.Pattern,"\\")
            ||EF.Functions.ILike(s.Number,paging.Pattern,"\\"));
        // The current qualification winner is checked by the same application
        // rule used at Create. Candidate paging can be sparse; candidateTotal is
        // explicitly not the count of currently eligible suppliers.
        var candidateTotal=await query.CountAsync(token);
        var pageRows=await query.OrderBy(s=>s.DisplayName).ThenBy(s=>s.Id)
            .Skip(paging.Offset).Take(paging.Size).Include(s=>s.Qualifications).ToArrayAsync(token);
        var items=pageRows.Where(s=>SupplierEligibility.CanReceiveNewOrders(s,now))
            .Select(s=>new{s.Id,s.DisplayName,s.Number}).ToArray();
        return new Reply(JsonSerializer.Serialize(new{items,candidateTotal,page=paging.Number,
            pageSize=paging.Size,nextPage=paging.Offset+paging.Size<candidateTotal?(int?)paging.Number+1:null,
            previewOnly=true,eligibilityMeaning="CurrentPreviewOnly"},Json));
    },ct);
}
