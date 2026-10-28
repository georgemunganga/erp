using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Principal-scoped adapters over the imported receiving DTOs.
/// Physical readiness protects reads; the separate new-work switch protects writes.</summary>
public static partial class ProcurementPrincipalReceivingRoutes
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private sealed record Reply(int Status,string Body);
    private sealed class Problem(string code,int status):Exception(code){internal int Status=>status;}
    public static void MapRoutes(WebApplication app)
    {
        var group=app.MapGroup("/api/procurement/v1");
        group.MapGet("/receiving/principal-sources",Sources).RequireAuthorization("procurement-receipt-create");
        group.MapGet("/receiving/principal-worklist",Worklist).RequireAuthorization("procurement-receipt-view");
        group.MapGet("/receiving/principal-inspection-tasks",InspectionTasks).RequireAuthorization(ControlledReceiving.InspectPermission);
        group.MapGet("/receiving/principal-acceptance-tasks",AcceptanceTasks).RequireAuthorization(ControlledReceiving.AcceptPermission);
        group.MapGet("/receiving/principal-service-tasks",ServiceTasks).RequireAuthorization(ControlledReceiving.CertifyPermission);
        group.MapGet("/receipts/{id:guid}/principal-inspection-task",InspectionTask).RequireAuthorization(ControlledReceiving.InspectPermission);
        group.MapGet("/receipts/{id:guid}/principal-acceptance-task",AcceptanceTask).RequireAuthorization(ControlledReceiving.AcceptPermission);
        group.MapGet("/service-acceptances/{id:guid}/principal-certification-task",ServiceTask).RequireAuthorization(ControlledReceiving.CertifyPermission);
        group.MapPost("/receipts/principal-drafts",CreateGoods).RequireAuthorization("procurement-receipt-create");
        group.MapPut("/receipts/{id:guid}/principal-draft",ReviseGoods).RequireAuthorization("procurement-receipt-edit");
        group.MapGet("/receipts/{id:guid}/principal-draft",GoodsDetail).RequireAuthorization("procurement-receipt-view");
        group.MapPost("/receipts/{id:guid}/principal-cancel",CancelGoods).RequireAuthorization("procurement-receipt-edit");
        group.MapPost("/receipts/{id:guid}/principal-record",Record).RequireAuthorization(ControlledReceiving.RecordPermission);
        group.MapPost("/receipts/{id:guid}/lines/{lineId:guid}/principal-inspect",Inspect).RequireAuthorization(ControlledReceiving.InspectPermission);
        group.MapPost("/receipts/{id:guid}/lines/{lineId:guid}/principal-accept",Accept).RequireAuthorization(ControlledReceiving.AcceptPermission);
        group.MapPost("/service-acceptances/principal-drafts",CreateService).RequireAuthorization("procurement-receipt-create");
        group.MapPut("/service-acceptances/{id:guid}/principal-draft",ReviseService).RequireAuthorization("procurement-receipt-edit");
        group.MapGet("/service-acceptances/{id:guid}/principal-draft",ServiceDetail).RequireAuthorization("procurement-receipt-view");
        group.MapPost("/service-acceptances/{id:guid}/principal-cancel",CancelService).RequireAuthorization("procurement-receipt-edit");
        group.MapPost("/service-acceptances/{id:guid}/principal-certify",Certify).RequireAuthorization(ControlledReceiving.CertifyPermission);
        group.MapGet("/receiving/principal-command-receipts/{operation}/{key:guid}",Recover).RequireAuthorization("procurement-access");
    }
    private static async Task<IResult> Run(HttpContext http,ProcurementRequestScope scope,string action,
        Func<ProcurementDbContext,ApprovalDbContext,HrmDbContext,PrincipalReceivingActor,CancellationToken,Task<Reply>> operation,CancellationToken ct)
    {
        http.Response.Headers.CacheControl="private, no-store";
        try
        {
            var options=http.RequestServices.GetService<ProcurementPrincipalReceivingOptions>();
            var ready=http.RequestServices.GetService<IProcurementPrincipalReceivingReadiness>();
            var write = !HttpMethods.IsGet(http.Request.Method);
            if(!ProcurementPrincipalReceivingCapability.Configured(options, write) || ready is null
                || !await ready.IsReadyAsync(ct))
                throw new Problem("PrincipalReceivingUnavailable",503);
            if(write && (scope.WorkLocationId is null || scope.IsExplicitAllBranches))
                throw new Problem("ReceivingBranchRequired",400);
            var result=await http.RequestServices.GetRequiredService<ProcurementApprovalTransaction>().ExecuteAsync(async(db,cases,token)=>
            {
                if(!await ready.IsReadyAsync(db,write,token))throw new Problem("PrincipalReceivingUnavailable",503);
                await using var core=await ProcurementApprovalExecutionLocks.CoreAsync(db,scope,token);
                var actor=await ProcurementPrincipalReceivingActor.CurrentAsync(core,scope,http,
                    http.RequestServices.GetRequiredService<IAuthorizationService>(),action,token);
                return await operation(db,cases,core,actor,token);
            },ct);
            return Results.Content(result.Body,"application/json",statusCode:result.Status);
        }
        catch(OperationalPolicyPublicationProblem error){return Error(error.Code,error.Status);}
        catch(Exception error) when(error is Problem or InvalidOperationException or UnauthorizedAccessException or KeyNotFoundException
            or ArgumentException or JsonException or DbUpdateException or OverflowException || ProcurementPostgresFailures.IsStoreFailure(error))
        {
            var status=error is Problem problem?problem.Status:ProcurementPostgresFailures.IsConcurrency(error)?409:
                ProcurementPostgresFailures.IsStoreFailure(error)?503:error is UnauthorizedAccessException?403:409;
            return Error(error is Problem?error.Message:status==503?"PrincipalReceivingSaveUnconfirmed":"PrincipalReceivingNeedsReview",status);
        }
    }
    private static IResult Error(string code,int status)=>Results.Json(new{code,message=status==503?
        "Receiving is unavailable or this save could not be confirmed. Keep the command reference and check again.":
        "Refresh this record and check your access, source versions and remaining quantities."},statusCode:status);
    private static Guid Key(HttpContext http)=>Guid.TryParse(http.Request.Headers["Idempotency-Key"],out var key)&&key!=Guid.Empty?key:throw new Problem("ReceivingCommandKeyRequired",400);
    private static string Fingerprint(object value)=>ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(value,Json));
    private static string Exact(decimal value)=>value.ToString("0.############################",CultureInfo.InvariantCulture);
    private static decimal Quantity(string value)
    {
        try{var parsed=ExactDecimalJsonCodec.Parse(value,18,4,"ReceivingQuantity");return parsed>=0?parsed:throw new Problem("ReceivingQuantityInvalid",422);}
        catch(JsonException){throw new Problem("ReceivingQuantityInvalid",422);}
    }
    private static void Version(PrincipalReceivingDraftHead head,long expected){if(head.Revision!=expected)throw new Problem("PrincipalReceivingDraftChanged",409);}
    private static string Action(string operation)=>operation switch
    {"Create"=>"procurement-receipt-create","Revise" or "Cancel"=>"procurement-receipt-edit","Record"=>ControlledReceiving.RecordPermission,
        "Inspect"=>ControlledReceiving.InspectPermission,"Accept"=>ControlledReceiving.AcceptPermission,"Certify"=>ControlledReceiving.CertifyPermission,
        _=>throw new Problem("ReceivingCommandNotFound",404)};
    private static async Task<PrincipalReceivingDraftHead> Head(ProcurementDbContext db,PrincipalReceivingActor actor,Guid id,string kind,bool owner,CancellationToken ct)
    {
        var branches=actor.AllowedBranches.ToArray();
        var head=await db.PrincipalReceivingDraftHeads.SingleOrDefaultAsync(x=>x.Id==id&&x.Kind==kind&&branches.Contains(x.BranchId),ct)
            ??throw new Problem("ReceivingNotFound",404);
        if(owner&&head.CreatorPrincipalId!=actor.PrincipalId)throw new Problem("ReceivingNotFound",404);
        await db.PrincipalReceivingDraftRevisions.Where(x=>x.DraftId==id&&x.Revision==head.Revision).LoadAsync(ct);
        var revision=db.PrincipalReceivingDraftRevisions.Local.Single(x=>x.DraftId==id&&x.Revision==head.Revision);
        _=PrincipalReceivingDraftSnapshot.Validate(head,revision);
        await db.PrincipalReceivingDraftLineBindings.Where(x=>x.DraftRevisionId==revision.Id).LoadAsync(ct);
        return head;
    }
    private static PrincipalReceivingDraftRevision Revision(ProcurementDbContext db,PrincipalReceivingDraftHead head)
        =>db.PrincipalReceivingDraftRevisions.Local.Single(x=>x.DraftId==head.Id&&x.Revision==head.Revision);
    private static async Task<Reply?> Replay(ProcurementDbContext db,PrincipalReceivingActor actor,string operation,Guid key,string? fingerprint,CancellationToken ct)
    {
        var receipt=await new PrincipalReceivingStore(db).RecoverAsync(key,actor.PrincipalId,operation,fingerprint,ct);
        if(receipt is null)return null;
        var branches=actor.AllowedBranches.ToArray();
        var head=await db.PrincipalReceivingDraftHeads.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==receipt.DraftId
            &&branches.Contains(x.BranchId)&&x.BranchId==receipt.BranchId,ct)??throw new Problem("ReceivingCommandNotFound",404);
        if(operation is "Create" or "Revise" or "Cancel" or "Record"&&head.CreatorPrincipalId!=actor.PrincipalId)
            throw new Problem("ReceivingCommandNotFound",404);
        return new(receipt.ResponseStatus,receipt.ResponseJson);
    }
    private static async Task<(PrincipalReceivingSourceState State,PrincipalReceivingLedger Ledger)> StoredSource(ProcurementDbContext db,
        ApprovalDbContext cases,ProcurementRequestScope scope,PrincipalReceivingActor actor,PrincipalReceivingSource source,bool locks,CancellationToken ct)
    {
        var result=await SourceHistory(db,cases,scope,actor,source,locks,ct);
        await ReconcileRequest(db,cases,scope,actor,source.Commercial.PurchaseRequestLineId,ct);
        return result;
    }
    private static async Task ReconcileRequest(ProcurementDbContext db,ApprovalDbContext cases,ProcurementRequestScope scope,
        PrincipalReceivingActor actor,Guid requestLineId,CancellationToken ct)
    {
        var states=await db.PrincipalReceivingSourceStates.AsNoTracking().Where(x=>x.PurchaseRequestLineId==requestLineId).ToArrayAsync(ct);
        foreach(var state in states)
        {
            // Historical immutable proof only: never acquire upstream request/PO
            // locks here after the target's lock sequence. Mutating callers already
            // hold the shared original request lock; SERIALIZABLE protects reads.
            var source=PrincipalReceivingStorageCodec.Source(state);
            _=await SourceHistory(db,cases,scope,actor,source,false,ct);
        }
        await new PrincipalReceivingStore(db).ReconcileCountersAsync(requestLineId,ct);
    }
    private static async Task<(PrincipalReceivingSourceState State,PrincipalReceivingLedger Ledger)> SourceHistory(ProcurementDbContext db,
        ApprovalDbContext cases,ProcurementRequestScope scope,PrincipalReceivingActor actor,PrincipalReceivingSource source,bool locks,CancellationToken ct)
    {
        var retained=await ProcurementPrincipalOrderRetainedIssued.LoadAsync(db,cases,scope,actor.AllowedBranches,
            source.Commercial.PurchaseOrderId,source.Commercial.IssuedVersionId,source.Commercial.PurchaseOrderLineId,locks,ct);
        // Reconstruct original pins using retained classification/kind, without current
        // policy/catalog lookup. The full owning verifier already proves issued history.
        if(ProcurementPrincipalReceivingSource.ProjectRetained(retained,source.Commercial.Kind)!=source)throw new Problem("PrincipalReceivingOriginalSourceChanged",409);
        if(!await db.PrincipalReceivingSourceStates.AnyAsync(x=>x.IssuedVersionId==source.Commercial.IssuedVersionId
            &&x.PurchaseOrderLineId==source.Commercial.PurchaseOrderLineId,ct))throw new Problem("PrincipalReceivingHistoryUnknown",409);
        var store=new PrincipalReceivingStore(db);var state=await store.SourceAsync(source,ct);var ledger=await store.LedgerAsync(state,ct);
        foreach(var policy in ledger.RetainedPolicies)
            _=await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db,scope,policy,ct,OperationalPolicyPublicationMode.RetainedHistorical);
        return(state,ledger);
    }
    private static async Task<PrincipalReceivingSource> NewSource(ProcurementDbContext db,ApprovalDbContext cases,
        ProcurementRequestScope scope,HttpContext http,PrincipalReceivingActor actor,Guid order,Guid issued,Guid line,CancellationToken ct,bool lockSources=true)
    {
        var retained=await ProcurementPrincipalOrderRetainedIssued.LoadAsync(db,cases,scope,actor.AllowedBranches,order,issued,line,lockSources,ct);
        var selected=retained.SelectedLine??throw new Problem("PrincipalReceivingLineRequired",422);
        var rule=await ProcurementPrincipalReceivingPolicy.RuleForNewSourceAsync(db,scope,http.RequestServices,
            selected.OriginalCategoryCode??"",selected.OriginalPurchaseType??"",retained.Order.BranchRef!.Value,ct);
        var source=ProcurementPrincipalReceivingSource.Project(retained,rule);
        _=await ProcurementPrincipalReceivingPolicy.CurrentAsync(db,scope,http.RequestServices,source,ct);
        return source;
    }
    private static Reply Complete(ProcurementDbContext db,PrincipalReceivingDraftHead head,string operation,Guid key,string actor,string hash,int status=200)
    {
        var body=JsonSerializer.Serialize(View(db,head),Json);new PrincipalReceivingStore(db).Complete(head,operation,key,actor,hash,body,status);
        db.AddScoped(new AuditEvent{RecordType="PrincipalReceiving",RecordId=head.Id,
            Action="PrincipalReceiving"+operation,ActorSubjectId=actor,
            ChangedFieldsJson=JsonSerializer.Serialize(new{schemaVersion=2,commandKey=key,
                revision=head.Revision,head.Kind,head.State},Json)});
        // Every handoff is explicitly Procurement-owned. No ledger, stock or Finance posting.
        db.AddScoped(new OutboxEvent{AggregateType="PrincipalReceiving",AggregateId=head.Id,
            EventType=operation is "Accept" or "Certify"?"procurement.fulfilment.accepted.v2":"procurement.receiving.changed.v2",
            IdempotencyKey=$"receiving.principal.{key:N}.v2",PayloadJson=JsonSerializer.Serialize(new{head.Id,operation,commandKey=key,
                revision=head.Revision,financePostingPerformed=false,stockPostingPerformed=false},Json)});
        return new(status,body);
    }
    private static object DraftView(PrincipalReceivingDraftHead head,PrincipalReceivingDraftRevision revision)
    {
        if(head.Kind=="Goods")
        {
            var draft=PrincipalReceivingDraftSnapshot.Goods(revision);
            return new{draft.Id,version=draft.Version,status=draft.State,draft.PurchaseOrderId,draft.IssuedVersionId,draft.SiteId,
                receivedAt=draft.ObservedAt,deliveryNoteRef=draft.DeliveryNoteReference,draft.Notes,draft.CancellationReason,
                lines=draft.Lines.Select(line=>new{line.Id,purchaseOrderLineId=line.Source.Commercial.PurchaseOrderLineId,
                    receivedQuantity=Exact(line.Quantity),rejectedQuantity=Exact(line.RejectedQuantity)})};
        }
        var service=PrincipalReceivingDraftSnapshot.Service(revision);
        return new{service.Id,version=service.Version,status=service.State,purchaseOrderId=service.Source.Commercial.PurchaseOrderId,
            issuedVersionId=service.Source.Commercial.IssuedVersionId,purchaseOrderLineId=service.Source.Commercial.PurchaseOrderLineId,
            service.DeliverableReference,service.PeriodFrom,service.PeriodTo,quantity=Exact(service.Quantity),service.Notes,service.CancellationReason};
    }
    private static Task<IResult> Recover(string operation,Guid key,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
    {
        string action;
        try{action=Action(operation);}
        catch(Problem){return Task.FromResult(Error("ReceivingCommandNotFound",404));}
        return Run(http,scope,action,async(db,_,_,actor,token)=>
        {
            var recorded=await Replay(db,actor,operation,key,null,token)
                ??throw new Problem("ReceivingCommandNotFound",404);
            // GET acknowledges an existing result; the original body stays
            // byte-identical. POST same-key replay retains its stored status.
            return new Reply(StatusCodes.Status200OK,recorded.Body);
        },ct);
    }
}

public sealed class ProcurementPrincipalReceivingOptions
{
    public bool Enabled{get;set;}
    public bool ExecutionDatabaseConfigured{get;set;}
}
public interface IProcurementPrincipalReceivingReadiness
{
    Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct);
    Task<bool> IsReadyAsync(CancellationToken ct)=>Task.FromResult(false);
}
public sealed class UnavailableProcurementPrincipalReceivingReadiness:IProcurementPrincipalReceivingReadiness
{
    public Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct)=>Task.FromResult(false);
}
