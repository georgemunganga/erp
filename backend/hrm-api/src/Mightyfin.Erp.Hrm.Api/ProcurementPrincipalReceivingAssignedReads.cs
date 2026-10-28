using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalReceivingRoutes
{
    internal sealed class AssignedCandidate
    {
        public PrincipalReceivingDraftHead Head{get;init;}=null!;
        public PrincipalReceivingDraftLineBinding Line{get;init;}=null!;
        public PrincipalReceivingSourceState Source{get;init;}=null!;
    }
    internal static IQueryable<AssignedCandidate> BoundDraftLines(ProcurementDbContext db,ProcurementRequestScope scope,PrincipalReceivingActor actor)
    {
        var branches=actor.AllowedBranches.ToArray();
        var issued=ProcurementPrincipalOrderRetainedIssued.QueryBoundIssued(db,scope,actor.AllowedBranches);
        return from head in db.PrincipalReceivingDraftHeads.AsNoTracking()
            join revision in db.PrincipalReceivingDraftRevisions.AsNoTracking()
                on new{DraftId=head.Id,head.Revision} equals new{revision.DraftId,revision.Revision}
            join line in db.PrincipalReceivingDraftLineBindings.AsNoTracking() on revision.Id equals line.DraftRevisionId
            join source in db.PrincipalReceivingSourceStates.AsNoTracking() on line.SourceStateId equals source.Id
            join order in issued on head.PurchaseOrderId equals order.Id
            where branches.Contains(head.BranchId)&&head.BranchId==source.BranchId&&line.BranchId==head.BranchId
                &&line.DraftId==head.Id&&line.PurchaseOrderId==head.PurchaseOrderId&&source.PurchaseOrderId==head.PurchaseOrderId
                &&line.IssuedVersionId==head.IssuedVersionId&&source.IssuedVersionId==head.IssuedVersionId
                &&line.PurchaseOrderLineId==source.PurchaseOrderLineId&&head.BranchId==order.BranchRef
            select new AssignedCandidate{Head=head,Line=line,Source=source};
    }
    internal static Expression<Func<AssignedCandidate,bool>> AssignedRulePredicate(IReadOnlyList<PrincipalReceivingRule> rules,string principal,string task)
    {
        var row=Expression.Parameter(typeof(AssignedCandidate),"row");
        var source=Expression.Property(row,nameof(AssignedCandidate.Source));Expression match=Expression.Constant(false);
        foreach(var rule in rules)
        {
            var named=task=="Inspection"?rule.InspectionRequired&&rule.InspectorPrincipalIds.Contains(principal)
                :task=="Acceptance"?rule.AcceptorPrincipalIds.Contains(principal):rule.ServiceOwnerPrincipalIds.Contains(principal);
            if(!named)continue;
            var category=Expression.Equal(Expression.Property(source,nameof(PrincipalReceivingSourceState.CategoryCode)),Expression.Constant(rule.CategoryCode));
            var type=Expression.Equal(Expression.Property(source,nameof(PrincipalReceivingSourceState.PurchaseType)),Expression.Constant(rule.PurchaseType));
            var kind=Expression.Equal(Expression.Property(source,nameof(PrincipalReceivingSourceState.Kind)),Expression.Constant(rule.Kind.ToString()));
            var sites=Expression.Call(typeof(Enumerable),nameof(Enumerable.Contains),[typeof(Guid)],Expression.Constant(rule.SiteIds.ToArray()),Expression.Property(source,nameof(PrincipalReceivingSourceState.BranchId)));
            match=Expression.OrElse(match,Expression.AndAlso(Expression.AndAlso(category,type),Expression.AndAlso(kind,sites)));
        }
        return Expression.Lambda<Func<AssignedCandidate,bool>>(match,row);
    }
    private static string TaskAction(string task)=>task=="Inspection"?ControlledReceiving.InspectPermission:task=="Acceptance"?ControlledReceiving.AcceptPermission:ControlledReceiving.CertifyPermission;
    private static Task<IResult> InspectionTasks(int? offset,int? limit,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)=>Tasks("Inspection",null,offset,limit,http,scope,ct);
    private static Task<IResult> AcceptanceTasks(int? offset,int? limit,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)=>Tasks("Acceptance",null,offset,limit,http,scope,ct);
    private static Task<IResult> ServiceTasks(int? offset,int? limit,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)=>Tasks("Service",null,offset,limit,http,scope,ct);
    private static Task<IResult> InspectionTask(Guid id,int? offset,int? limit,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)=>Tasks("Inspection",id,offset,limit,http,scope,ct);
    private static Task<IResult> AcceptanceTask(Guid id,int? offset,int? limit,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)=>Tasks("Acceptance",id,offset,limit,http,scope,ct);
    private static Task<IResult> ServiceTask(Guid id,int? offset,int? limit,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)=>Tasks("Service",id,offset,limit,http,scope,ct);
    private static Task<IResult> Tasks(string task,Guid? id,int? offset,int? limit,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,TaskAction(task),async(db,cases,core,actor,token)=>
        {
            var start=Math.Clamp(offset??0,0,100_000);var size=Math.Clamp(limit??25,1,25);
            var policy=await ProcurementPrincipalReceivingPolicy.CurrentRecordAsync(db,scope,http.RequestServices,token);
            var document=PrincipalReceivingPolicyRules.Parse(policy.RulesJson);
            var query=BoundDraftLines(db,scope,actor).Where(x=>x.Source.CurrencyCode==document.CurrencyCode)
                .Where(AssignedRulePredicate(document.Rules,actor.PrincipalId,task));
            query=task=="Service"?query.Where(x=>x.Head.Kind=="Service"&&x.Head.State=="Draft")
                :query.Where(x=>x.Head.Kind=="Goods"&&x.Head.State=="Recorded");
            if(id is {} target)query=query.Where(x=>x.Head.Id==target);
            // Current actor/action/site, exact current named policy and retained
            // issued scope all constrain the count, not an after-page name filter.
            var total=await query.CountAsync(token);
            if(id.HasValue&&total==0)throw new Problem("ReceivingTaskNotFound",404);
            var page=await query.OrderBy(x=>x.Head.CreatedAt).ThenBy(x=>x.Head.Id).ThenBy(x=>x.Line.LineId)
                .Skip(start).Take(size).ToArrayAsync(token);var records=new List<object>();
            foreach(var candidate in page)
            {
                var head=await Head(db,actor,candidate.Head.Id,task=="Service"?"Service":"Goods",false,token);
                var source=PrincipalReceivingStorageCodec.Source(candidate.Source);
                var loaded=await StoredSource(db,cases,scope,actor,source,false,token);
                var selected=PrincipalReceivingPolicyRules.Select(policy,source,DateTimeOffset.UtcNow);
                var blockers=new List<string>();var actions=new List<string>();
                object projection;
                if(task=="Service")
                {
                    var draft=PrincipalReceivingDraftSnapshot.Service(Revision(db,head));
                    if(draft.Source!=source||candidate.Line.LineId!=head.Id)throw new Problem("ReceivingTaskNeedsReview",409);
                    projection=new{draft.DeliverableReference,draft.PeriodFrom,draft.PeriodTo,quantity=Exact(draft.Quantity)};
                    if(draft.Quantity>PrincipalControlledReceiving.Balance(loaded.Ledger).RemainingQuantity)blockers.Add("IssuedCapacityExceeded");
                    var independent=new List<PrincipalReceivingPerson>();if(!selected.Rule.AllowBuyerCertification)independent.Add(source.Buyer);
                    if(!selected.Rule.AllowRequesterCertification)independent.Add(source.Requester);
                    try{await ProcurementPrincipalReceivingActor.RequireIndependentAsync(core,actor,independent,token);}
                    catch(UnauthorizedAccessException){blockers.Add("IndependentActorRequired");}
                    if(blockers.Count==0)actions.Add("Certify");
                }
                else
                {
                    var observed=loaded.Ledger.Observations.SingleOrDefault(x=>x.ReceiptId==head.Id&&x.ReceiptLineId==candidate.Line.LineId)
                        ??throw new Problem("ReceivingPhysicalObservationRequired",409);
                    var inspected=loaded.Ledger.Inspections.SingleOrDefault(x=>x.ObservationId==observed.Id);
                    if(observed.PolicyVersionId!=policy.Id||observed.PolicyRulesSha256!=selected.RulesSha256)blockers.Add("ReceivingPolicyChanged");
                    if(task=="Inspection"&&inspected is not null)blockers.Add("InspectionAlreadyRecorded");
                    if(task=="Acceptance"&&selected.Rule.InspectionRequired&&inspected is null)blockers.Add("InspectionRequired");
                    var accepted=ExactDecimalArithmetic.Sum(loaded.Ledger.Acceptances.Where(x=>x.ObservationId==observed.Id).Select(x=>x.Quantity));
                    var qualified=inspected?.QualifiedQuantity??ExactDecimalArithmetic.Subtract(observed.ReceivedQuantity,observed.InitiallyRejectedQuantity);
                    var available=ExactDecimalArithmetic.Subtract(qualified,accepted);
                    if(task=="Acceptance"&&available<=0)blockers.Add("NoQualifiedQuantity");
                    if(task=="Acceptance"&&PrincipalControlledReceiving.Balance(loaded.Ledger).RemainingQuantity<=0)blockers.Add("IssuedCapacityExceeded");
                    var independent=new List<PrincipalReceivingPerson>();
                    if(task=="Inspection"&&selected.Rule.RequireIndependentInspector)independent.Add(observed.Receiver);
                    if(task=="Acceptance")
                    {
                        if(!selected.Rule.AllowBuyerCertification)independent.Add(source.Buyer);
                        if(!selected.Rule.AllowRequesterCertification)independent.Add(source.Requester);
                        if(!selected.Rule.AllowReceiverCertification)independent.Add(observed.Receiver);
                    }
                    try{await ProcurementPrincipalReceivingActor.RequireIndependentAsync(core,actor,independent,token);}
                    catch(UnauthorizedAccessException){blockers.Add("IndependentActorRequired");}
                    projection=new{observed.ObservedAt,observed.DeliveryNoteReference,physicalQuantity=Exact(observed.ReceivedQuantity),
                        initiallyRejectedQuantity=Exact(observed.InitiallyRejectedQuantity),
                        rejectedQuantity=Exact(inspected is null?observed.InitiallyRejectedQuantity:ExactDecimalArithmetic.Add(observed.InitiallyRejectedQuantity,ExactDecimalArithmetic.Subtract(inspected.InspectedQuantity,inspected.QualifiedQuantity))),qualifiedQuantity=inspected is null?null:Exact(inspected.QualifiedQuantity),
                        acceptedQuantity=Exact(accepted),unacceptedPhysicalQuantity=Exact(available),inspectionOutcome=inspected?.Outcome.ToString(),
                        inspectionRejectedQuantity=inspected is null?null:Exact(ExactDecimalArithmetic.Subtract(inspected.InspectedQuantity,inspected.QualifiedQuantity)),
                        checklistReference=selected.Rule.ChecklistReference};
                    if(blockers.Count==0)actions.Add(task=="Inspection"?"Inspect":"Accept");
                }
                // Purpose projection omits notes, maker identity, prices, commercial
                // snapshots and unrestricted document history. Commands revalidate.
                records.Add(new{id=head.Id,lineId=candidate.Line.LineId,version=head.Revision,sourceVersion=loaded.State.Sequence,
                    taskKind=task,status=head.State,purchaseOrderLineId=source.Commercial.PurchaseOrderLineId,purchaseOrderId=head.PurchaseOrderId,issuedVersionId=head.IssuedVersionId,
                    siteId=head.BranchId,projection,actionCandidates=actions,blockers,isAuthority=false});
            }
            return new Reply(200,System.Text.Json.JsonSerializer.Serialize(new{records,total,offset=start,limit=size,purposeOnly=true,executionVersion=2,isAuthority=false},Json));
        },ct);
    private static Task<IResult> Worklist(string? kind,string? status,int? offset,int? limit,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,"procurement-receipt-view",async(db,cases,_,actor,token)=>
        {
            if(kind is not(null or "Goods" or "Service") || status is not(null or "Draft" or "Recorded" or "Certified" or "Cancelled"))
                throw new Problem("ReceivingFilterUnsupported",422);
            var start=Math.Clamp(offset??0,0,100_000);var size=Math.Clamp(limit??10,1,10);
            var query=BoundDraftLines(db,scope,actor);
            if(kind is not null)query=query.Where(x=>x.Head.Kind==kind);
            if(status is not null)query=query.Where(x=>x.Head.State==status);
            var headers=query.Select(x=>x.Head).Distinct();var total=await headers.CountAsync(token);
            var page=await headers.OrderByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id).Skip(start).Take(size).ToArrayAsync(token);
            var records=new List<object>();
            foreach(var candidate in page)
            {
                var head=await Head(db,actor,candidate.Id,candidate.Kind,false,token);
                var revision=Revision(db,head);var sources=PrincipalReceivingDraftSnapshot.Sources(head.Kind,revision);
                var retained=await ProcurementPrincipalOrderRetainedIssued.LoadAsync(db,cases,scope,actor.AllowedBranches,
                    head.PurchaseOrderId,head.IssuedVersionId,null,false,token);
                foreach(var source in sources)
                {
                    var original=retained.Sources.SingleOrDefault(x=>x.OrderLine.Id==source.Commercial.PurchaseOrderLineId)
                        ??throw new Problem("ReceivingSourceNeedsReview",409);
                    if(ProcurementPrincipalReceivingSource.ProjectRetained(retained with{SelectedLine=original},source.Commercial.Kind)!=source)
                        throw new Problem("ReceivingSourceNeedsReview",409);
                }
                // Historical directory contains no current readiness/action/balance
                // claim and does not require today's RECEIVING policy publication.
                records.Add(new{id=head.Id,version=head.Revision,status=head.State,kind=head.Kind,head.CreatedAt,
                    purchaseOrderId=head.PurchaseOrderId,purchaseOrderNumber=retained.Order.Number,issuedVersionId=head.IssuedVersionId,
                    siteId=head.BranchId,lineCount=sources.Count,executionVersion=2,isAuthority=false});
            }
            return new Reply(200,System.Text.Json.JsonSerializer.Serialize(new{records,total,offset=start,limit=size,executionVersion=2},Json));
        },ct);

}
