using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private static Task<IResult> PrincipalSources(int? page,int? pageSize,string? q,
        HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
    {
        if(!PrincipalSourceAvailable(http))return Task.FromResult(Error("PrincipalSourcingSourceUnavailable",503));
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var paging=ProcurementSourcingDiscoveryQueries.Paging(page,pageSize,q);
            var actor=await PrincipalActor(core,scope,http,auth,"procurement-sourcing-convert",null,false,token);
            var requests=db.PurchaseRequests.AsNoTracking();
            if(actor.SelectedBranchId is { } selected)requests=requests.Where(x=>x.BranchRef==selected);
            if(!actor.OrganisationWide)
            {
                var branches=actor.Branches.ToArray();
                requests=requests.Where(x=>x.BranchRef!=null && branches.Contains(x.BranchRef.Value));
            }
            var candidates=from line in ProcurementSourcingDiscoveryQueries.Candidates(db,scope)
                join request in requests on line.PurchaseRequestId equals request.Id
                where db.ApprovalSubmissions.Any(x=>x.PurchaseRequestId==request.Id
                    && x.SnapshotSchemaVersion==3)
                select new{line.Id,line.PurchaseRequestId,request.Number,line.LineNumber,line.Description};
            if(paging.Search.Length>0)candidates=candidates.Where(x=>EF.Functions.ILike(x.Number,paging.Pattern,"\\")
                || EF.Functions.ILike(x.Description,paging.Pattern,"\\"));
            var count=await candidates.CountAsync(token);
            var rows=await candidates.OrderBy(x=>x.Number).ThenBy(x=>x.PurchaseRequestId)
                .ThenBy(x=>x.LineNumber).ThenBy(x=>x.Id).Skip(paging.Offset).Take(paging.Size).ToArrayAsync(token);
            var items=new List<object>();var now=DateTimeOffset.UtcNow;
            foreach(var row in rows)
            {
                try
                {
                    var(source,available)=await PrincipalEligible(db,cases,core,scope,http,auth,
                        row.PurchaseRequestId,row.Id,now,token);
                    items.Add(new{purchaseRequestId=row.PurchaseRequestId,purchaseRequestLineId=row.Id,
                        requestNumber=row.Number,branchRef=source.Request.BranchRef,source.Line.Description,
                        source.Line.UnitCode,source.Line.CategoryCode,expectedRequestVersion=source.Request.Version,
                        sourceLineVersion=source.Line.Version,
                        availableQuantity=ProcurementEvidenceEncoding.Decimal(available.AvailableQuantity),
                        approvedUnitEstimate=ProcurementEvidenceEncoding.Decimal(available.ApprovedUnitPrice),
                        available.CurrencyCode,available.MinimumQuoteCount,available.SourceSubmissionId,
                        available.SourceCaseId,methodId=available.MethodId,methodRevision=available.MethodRevision,
                        state=available.AvailableQuantity>0?"Available":"FullyAllocated",
                        canSelect=available.AvailableQuantity>0,canPublish=false});
                }
                catch(Exception error) when(IsDiscoveryEvidenceFailure(error))
                {
                    items.Add(new{purchaseRequestId=row.PurchaseRequestId,purchaseRequestLineId=row.Id,
                        requestNumber=row.Number,state="Unavailable",blocker="SourcingSourceNeedsReview",
                        canSelect=false,canPublish=false});
                }
            }
            ReadOnly(db,cases,core);
            return new Reply(JsonSerializer.Serialize(new{items,candidateTotal=count,page=paging.Number,
                pageSize=paging.Size,eligibilityMeaning="CurrentPreviewOnly",canPublish=false},Json));
        },ct);
    }
    private static Task<IResult> PrincipalDrafts(int? page,int? pageSize,string? q,string? state,
        HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
    {
        if(!PrincipalSourceAvailable(http))return Task.FromResult(Error("PrincipalSourcingSourceUnavailable",503));
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var paging=ProcurementSourcingDiscoveryQueries.Paging(page,pageSize,q);
            if(state is not (null or "Draft" or "Cancelled"))throw new ArgumentException("SourcingDiscoveryStateInvalid");
            var actor=await PrincipalReadActor(core,scope,http,auth,token);
            var query=PrincipalOwned(db,actor.Actor).AsNoTracking().Where(x=>x.Status=="Draft"||x.Status=="Cancelled");
            if(actor.Actor.SelectedBranchId is { } selected)query=query.Where(x=>x.BranchRef==selected);
            if(!actor.Actor.OrganisationWide)
            {
                var branches=actor.Actor.Branches.ToArray();
                query=query.Where(x=>x.BranchRef!=null&&branches.Contains(x.BranchRef.Value));
            }
            if(state is not null)query=query.Where(x=>x.Status==state);
            if(paging.Search.Length>0)query=query.Where(x=>EF.Functions.ILike(x.Number,paging.Pattern,"\\")
                || EF.Functions.ILike(x.Title,paging.Pattern,"\\"));
            var count=await query.CountAsync(token);
            var rows=await query.OrderByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id)
                .Skip(paging.Offset).Take(paging.Size).ToArrayAsync(token);
            var items=new List<object>();
            foreach(var ev in rows)
            {
                try
                {
                    _=await PrincipalReadOwned(db,ev,token);
                    items.Add(new{ev.Id,ev.Number,ev.Title,ev.CurrencyCode,ev.Status,ev.Version,
                        originalDraftFingerprint=ev.OriginalDraftFingerprint,state="VerifiedRetainedDraft",canPublish=false});
                }
                catch(Exception error) when(IsDiscoveryEvidenceFailure(error))
                {items.Add(new{ev.Id,ev.Number,state="Unavailable",blocker="SourcingOriginalEvidenceUnavailable",canPublish=false});}
            }
            ReadOnly(db,cases,core);
            return new Reply(JsonSerializer.Serialize(new{items,total=count,page=paging.Number,
                pageSize=paging.Size,canPublish=false},Json));
        },ct);
    }
    private static async Task<(RfqPrincipalActor Actor,string Permission)> PrincipalReadActor(
        Mightyfin.Erp.Hrm.Infrastructure.Data.HrmDbContext core,ProcurementRequestScope scope,
        HttpContext http,Microsoft.AspNetCore.Authorization.IAuthorizationService auth,CancellationToken ct)
    {
        foreach(var action in new[]{"procurement-sourcing-convert","procurement-sourcing-cancel"})
        {
            try{return(await PrincipalActor(core,scope,http,auth,action,null,false,ct),action);}
            catch(Problem error) when(error.Status==403) { }
        }
        throw new Problem("SourcingActionDenied",403);
    }
    private static Task<IResult> PrincipalRead(Guid id,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
    {
        if(!PrincipalSourceAvailable(http))return Task.FromResult(Error("PrincipalSourcingSourceUnavailable",503));
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var actor=await PrincipalReadActor(core,scope,http,auth,token);
            var ev=await PrincipalOwnedEvent(db,core,scope,http,auth,actor.Actor,actor.Permission,id,token);
            return await PrincipalReadOwned(db,ev,token);
        },ct);
    }
    private static async Task<Reply> PrincipalReadOwned(ProcurementDbContext db,SourcingEvent ev,CancellationToken ct)
    {
        var evidence=await db.SourcingDraftEvidenceRecords.AsNoTracking().SingleAsync(x=>x.Id==ev.SourceEvidenceId,ct);
        var bindings=await db.SourcingDemandBindings.AsNoTracking().Where(x=>x.SourcingEventId==ev.Id)
            .OrderBy(x=>x.PurchaseRequestLineId).Take(201).ToArrayAsync(ct);
        var lines=await db.SourcingLines.AsNoTracking().Where(x=>x.SourcingEventId==ev.Id).Take(201).ToArrayAsync(ct);
        if(bindings.Length is <1 or >200 || lines.Length!=bindings.Length)
            throw new Problem("SourcingOriginalEvidenceUnavailable",409);
        _=GovernedPrincipalRfqRestoration.RestoreVerified(evidence,ev,bindings);
        var ids=bindings.Select(x=>x.AllocationId).ToArray();
        var allocations=await db.PurchaseRequestAllocations.AsNoTracking().Where(x=>ids.Contains(x.Id)).ToArrayAsync(ct);
        if(allocations.Length!=bindings.Length)throw new Problem("SourcingOriginalEvidenceUnavailable",409);
        foreach(var binding in bindings)
        {
            var line=lines.SingleOrDefault(x=>x.Id==binding.SourcingLineId);
            var allocation=allocations.SingleOrDefault(x=>x.Id==binding.AllocationId);
            if(line is null || line.PurchaseRequestLineId!=binding.PurchaseRequestLineId
                || line.Quantity!=binding.Quantity || line.UnitCode!=binding.UnitCode
                || line.CategoryCode!=binding.CategoryCode || allocation is null
                || allocation.TargetType!="SourcingEvent" || allocation.TargetId!=ev.Id
                || allocation.Quantity!=binding.Quantity || allocation.EstimatedValue!=binding.EstimatedValue
                || allocation.CurrencyCode!=ev.CurrencyCode
                || (ev.Status=="Draft"?line.Status!="Active"||allocation.Status!="Reserved"
                    :ev.Status!="Cancelled"||line.Status!="Cancelled"||allocation.Status!="Released"))
                throw new Problem("SourcingOriginalEvidenceUnavailable",409);
        }
        return new Reply(JsonSerializer.Serialize(new{ev.Id,ev.Number,ev.Title,ev.Method,ev.CurrencyCode,
            ev.Status,ev.Version,originalDraftFingerprint=ev.OriginalDraftFingerprint,ev.CancellationReason,
            canPublish=false,supplierDeliveryState="NotStarted",financeReservationChanged=false,
            lines=bindings.Select(binding=>new{id=binding.SourcingLineId,binding.PurchaseRequestId,
                binding.PurchaseRequestLineId,description=lines.Single(x=>x.Id==binding.SourcingLineId).Description,
                binding.UnitCode,binding.CategoryCode,quantity=ProcurementEvidenceEncoding.Decimal(binding.Quantity),
                approvedUnitEstimate=ProcurementEvidenceEncoding.Decimal(binding.UnitEstimate),
                estimatedValue=ProcurementEvidenceEncoding.Decimal(binding.EstimatedValue),
                allocationState=allocations.Single(x=>x.Id==binding.AllocationId).Status}).ToArray()},Json));
    }
    private static Task<IResult> PrincipalRecover(string operation,Guid key,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)
    {
        if(!PrincipalSourceAvailable(http))return Task.FromResult(Error("PrincipalSourcingSourceUnavailable",503));
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            if(operation is not ("Create" or "Cancel") || key==Guid.Empty)
                throw new Problem("SourcingReceiptNotFound",404);
            var permission=operation=="Create"?"procurement-sourcing-convert":"procurement-sourcing-cancel";
            var actor=await PrincipalActor(core,scope,http,auth,permission,null,false,token);
            var original=await PrincipalReplay(db,core,scope,http,auth,actor,permission,operation,key,null,null,token)
                ??throw new Problem("SourcingReceiptNotFound",404);
            return new Reply(JsonSerializer.Serialize(new{operation,commandKey=key,originalStatusCode=original.Status,
                responseJson=original.Body,recordedHistoricalResult=true,canPublish=false},Json));
        },ct);
    }
}
