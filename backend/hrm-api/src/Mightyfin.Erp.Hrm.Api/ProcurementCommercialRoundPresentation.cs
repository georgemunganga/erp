using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal static class ProcurementCommercialRoundActions
{
    internal static bool IsEditableRoute(long? requestedVersion)=>requestedVersion is null;
    // Presentation hints only. Every command rechecks current authority/state under owning locks.
    internal static string[] ForRecord(bool recordReady,bool explicitBranch,bool sourceDraft,bool currentView,
        bool enrolled,string? planStatus,bool untouched,bool verifiedHolds,bool manage,bool cancel)
    {
        if (!recordReady||!explicitBranch||!sourceDraft||!currentView||!untouched) return [];
        if (!enrolled) return manage&&verifiedHolds ? ["Create"] : [];
        if (planStatus=="Rejected") return cancel ? ["Cancel"] : [];
        if (planStatus!="Draft") return [];
        var actions=new List<string>();
        if (manage&&verifiedHolds) actions.Add("Revise");
        if (cancel) actions.Add("Cancel");
        return actions.ToArray();
    }
}
public static partial class ProcurementSourcingExecutionRoutes
{
    internal sealed record RoundPolicyPin(Guid PolicyVersionId, int PolicyVersionNumber);
    private static object RoundSourceView(SourcingEvent source,PrincipalRetainedSource retained)
    {
        var method=retained.Snapshot.Lines.Select(x=>new {id=x.MethodId,revision=x.MethodRevision,code=x.MethodCode}).Distinct().Single();
        return new {source.Id,source.Number,source.Title,source.CurrencyCode,branchId=source.BranchRef,
            source.Version,source.Status,method=new {method.id,method.revision,method.code,label=source.Method},
            lines=PrincipalRetainedLineViews(retained),supplierFacingWorkflowAvailable=false};
    }
    private static async Task<(string[] Actions, RoundPolicyPin? Policy)> RoundActions(HttpContext http,ProcurementDbContext db,ApprovalDbContext cases,
        HrmDbContext core,ProcurementRequestScope scope,IAuthorizationService auth,SourcingEvent source,
        CommercialRoundPlan? plan,bool currentView,CancellationToken ct,RoundLoadedGraph? graph=null)
    {
        var explicitBranch=!scope.IsExplicitAllBranches&&scope.WorkLocationId is { } selected&&selected==source.BranchRef
            && !http.Request.Headers["X-Shell-Branch-Scope"].Any(x=>string.Equals(x,"all",StringComparison.OrdinalIgnoreCase));
        if (!explicitBranch||!currentView||source.Status!="Draft"
            || !http.RequestServices.GetRequiredService<ProcurementCommercialRoundOptions>().RecordEnabled
            || !await http.RequestServices.GetRequiredService<IProcurementCommercialRoundReadiness>().ReadyAsync(db,true,ct)) return ([],null);
        bool untouched=!await RoundExposedSourceIds(db).ContainsAsync(source.Id,ct);
        if (!untouched) return ([],null);
        async Task<bool> Allowed(string permission)
        {
            try { _=await PrincipalActor(core,scope,http,auth,permission,source.BranchRef,true,ct);return true; }
            catch (Problem error) when(error.Status is 403 or 404) {return false;}
        }
        var manage=await Allowed("procurement-round-plan-manage");
        var cancel=plan is not null&&await Allowed("procurement-round-plan-cancel");
        var submit=plan?.Status=="Draft"&&await Allowed(CommercialRoundReviewCoreWorkflow.SubmitPermission);
        var held=false;
        if (manage||submit)
        {
            try {_=await RoundVerifyHeldSource(db,cases,scope,source,ct);held=true;}
            catch(Exception error) when(IsDiscoveryEvidenceFailure(error)||error is JsonException or InvalidOperationException) { }
        }
        var actions=ProcurementCommercialRoundActions.ForRecord(true,explicitBranch,true,currentView,plan is not null,
            plan?.Status,untouched,held,manage,cancel).ToList();
        var review=http.RequestServices.GetRequiredService<IProcurementCommercialRoundReviewReadiness>();
        var options=http.RequestServices.GetRequiredService<ProcurementCommercialRoundOptions>();
        var reviewReady=false;
        RoundPolicyPin? policyPin=null;
        if(submit&&held&&options.ReviewEnabled&&review.HasCapturedContract)
            try { reviewReady=await review.ReadyAsync(db,true,ct); }
            catch(Exception error) when(error is not OperationCanceledException) { reviewReady=false; }
        if(reviewReady)
        {
            var now=ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            var categories=await db.SourcingLines.AsNoTracking().Where(x=>x.SourcingEventId==source.Id&&x.Status=="Active")
                .Select(x=>x.CategoryCode).ToArrayAsync(ct);
            var applicable=await RoundApplicableReviewPolicy(db,scope,source,categories,now,ct);
            if(applicable is { } selectedPolicy&& !selectedPolicy.Rules.DocumentsRequired)
            {
                policyPin=new(selectedPolicy.Policy.Id,selectedPolicy.Policy.PolicyVersionNumber);
                if(graph is not null)
                {
                    var draft=CommercialRoundPlanDraft.Restore(graph.Revision,graph.Criteria,graph.Participants);
                    if(RoundReviewComplete(draft,selectedPolicy.Rules,now))
                        try { await RoundParticipants(core,scope,graph.Plan.BranchId,draft,ct);actions.Add("Submit"); }
                        catch(Exception error) when(error is not OperationCanceledException) { }
                }
            }
        }
        return (actions.ToArray(),policyPin);
    }
    private static Task<IResult> RoundSources(int? page,int? pageSize,string? q,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        => RunRound(http,scope,async(db,cases,core,auth,token)=> {
            var paging=ProcurementSourcingDiscoveryQueries.Paging(page,pageSize,q);
            var actor=await PrincipalActor(core,scope,http,auth,"procurement-round-plan-read",null,false,token);
            var exposed=RoundExposedSourceIds(db);
            var query=QuotationOwnedSources(db).AsNoTracking().Where(x=>x.Status=="Draft"&&x.BranchRef!=null
                &&x.ApprovalInstanceRef==null&&x.OpensAt==null&&x.ClosesAt==null
                &&!db.CommercialRoundEnrollments.Any(e=>e.SourcingEventId==x.Id)&&!exposed.Contains(x.Id));
            if (scope.WorkLocationId is { } selected) query=query.Where(x=>x.BranchRef==selected);
            if (!actor.OrganisationWide) {var branches=actor.Branches.ToArray();query=query.Where(x=>branches.Contains(x.BranchRef!.Value));}
            if (paging.Search.Length>0) query=query.Where(x=>EF.Functions.ILike(x.Number,paging.Pattern,"\\")||EF.Functions.ILike(x.Title,paging.Pattern,"\\"));
            var total=await query.CountAsync(token);
            var items=await query.OrderBy(x=>x.Id).Skip(paging.Offset).Take(paging.Size)
                .Select(x=>new {x.Id,x.Number,x.Title,x.CurrencyCode,branchId=x.BranchRef,x.Status,x.Version,
                    enrollmentCandidate=true,eligibilityIsProvisional=true}).ToArrayAsync(token);
            ReadOnly(db,cases,core);
            return new(JsonSerializer.Serialize(new{items,total,page=paging.Number,pageSize=paging.Size},RoundJson));
        },ct);
    internal sealed record RoundPersonCandidate(Guid Id,string DisplayName);
    internal static IQueryable<RoundPersonCandidate> RoundPeopleProjection(HrmDbContext core,ProcurementRequestScope scope,
        DateTimeOffset now,Guid branch,Guid? after,ProcurementSourcingDiscoveryQueries.Page paging)
    {
        // Core Resolve currently defines OrganisationWide as no declared branch assignments.
        // Candidates preserves that no-declared OR target-company/branch predicate before paging.
        // Future explicit organisation grants must be added in Core, not parsed again here.
        var query=ProcurementPrincipalPeople.Candidates(core,scope,now,branch);
        // Npgsql's documented row-value comparison provides stable keyset paging.
        if(after is {} cursor)query=query.Where(x=>EF.Functions.GreaterThan(ValueTuple.Create(x.Id),ValueTuple.Create(cursor)));
        if(paging.Search.Length>0)query=query.Where(x=>EF.Functions.ILike(x.DisplayName,paging.Pattern,"\\"));
        return query.OrderBy(x=>x.Id).Select(x=>new RoundPersonCandidate(x.Id,x.DisplayName));
    }
    private static Task<IResult> RoundPeople(Guid id,Guid? after,int? limit,string? q,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        => RunRound(http,scope,async(db,cases,core,auth,token)=> {
            var paging=ProcurementSourcingDiscoveryQueries.Paging(1,limit,q);
            if(after==Guid.Empty)throw new Problem("RoundPlanPeopleCursorInvalid",422);
            var source=await RoundSource(db,core,scope,http,auth,"procurement-round-plan-read",id,token);
            // Narrow projection only; never materialize LocalUser/password/payroll entities.
            var candidates=await RoundPeopleProjection(core,scope,DateTimeOffset.UtcNow,source.BranchRef!.Value,after,paging)
                .Take(101).ToArrayAsync(token);
            var items=new List<object>();Guid? last=null;var scanned=0;
            foreach(var candidate in candidates.Take(100))
            {
                last=candidate.Id;scanned++;
                var person=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,candidate.Id.ToString("D"),token);
                if(person is not null&&(person.OrganisationWide||person.AllowedBranches.Contains(source.BranchRef!.Value)))
                    items.Add(new{principalId=candidate.Id.ToString("D"),candidate.DisplayName,branchId=source.BranchRef});
                if(items.Count==paging.Size)break;
            }
            var nextCursor=scanned<candidates.Length?last:null;
            ReadOnly(db,cases,core);
            return new(JsonSerializer.Serialize(new{items,nextCursor},RoundJson));
        },ct);
}
