using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Internal P25 principal demand to governed RFQ draft. No publication or supplier workflow.</summary>
public static partial class ProcurementSourcingExecutionRoutes
{
    private static void MapPrincipalSourceRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/approved-principal-sources",PrincipalSources).RequireAuthorization("procurement-sourcing-convert");
        group.MapPost("/from-approved-principal-sources",PrincipalCreate).RequireAuthorization("procurement-sourcing-convert");
        group.MapGet("/principal-source-drafts",PrincipalDrafts).RequireAuthorization("procurement-access");
        group.MapGet("/{id:guid}/principal-source-draft",PrincipalRead).RequireAuthorization("procurement-access");
        group.MapPost("/{id:guid}/principal-source-cancel",PrincipalCancel).RequireAuthorization("procurement-sourcing-cancel");
        group.MapGet("/principal-command-receipts/{operation}/{key:guid}",PrincipalRecover).RequireAuthorization("procurement-access");
    }

    private static bool PrincipalSourceAvailable(HttpContext http)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        var sourcing=http.RequestServices.GetRequiredService<ProcurementSourcingDraftExecutionOptions>();
        var approvals=http.RequestServices.GetRequiredService<ProcurementApprovalExecutionOptions>();
        return sourcing.PrincipalSourceEnabled && approvals.Enabled && approvals.PrincipalExecutionEnabled
            && approvals.ExecutionDatabaseConfigured;
    }
    private static async Task<RfqPrincipalActor> PrincipalActor(HrmDbContext core,ProcurementRequestScope scope,
        HttpContext http,IAuthorizationService auth,string permission,Guid? branch,bool target,CancellationToken ct)
    {
        if(scope.OrgUnitId is not null)throw new Problem("RfqDepartmentScopeUnsupported",503);
        var person=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,scope.SubjectId,ct);
        if(person is null || scope.PrincipalId?.ToString("D")!=person.PrincipalId
            || !person.Permissions.Contains(permission)
            || !(await auth.AuthorizeAsync(http.User,permission)).Succeeded)
            throw new Problem("SourcingActionDenied",403);
        Guid[] branches;
        try{branches=await ProcurementExecutionRecordScope.Branches(core,scope,branch,target,ct);}
        catch(KeyNotFoundException){throw new Problem("SourcingNotFound",404);}
        return new(person.PrincipalId,person.CoreLinkedWorkerId,scope.TenantId,scope.LegalEntityId,
            scope.WorkLocationId,branches.ToHashSet(),branches.Length==0&&!scope.IsConfined,
            new HashSet<string>([permission],StringComparer.Ordinal));
    }
    private static IQueryable<SourcingEvent> PrincipalOwned(ProcurementDbContext db,RfqPrincipalActor actor)
        => from ev in db.SourcingEvents
           join evidence in db.SourcingDraftEvidenceRecords on ev.SourceEvidenceId equals (Guid?)evidence.Id
           where ev.ExecutionVersion==2 && evidence.SchemaVersion==2
               && ev.OwnerSubjectId==actor.PrincipalId && evidence.MakerPrincipalId==actor.PrincipalId
               && evidence.MakerSubjectId==actor.PrincipalId && evidence.MakerWorkerId==ev.MakerWorkerId
               && evidence.SourcingEventId==ev.Id && evidence.BranchRef==ev.BranchRef
               && evidence.OriginalDraftFingerprint==ev.OriginalDraftFingerprint
           select ev;
    private static async Task<SourcingEvent> PrincipalOwnedEvent(ProcurementDbContext db,HrmDbContext core,
        ProcurementRequestScope scope,HttpContext http,IAuthorizationService auth,RfqPrincipalActor actor,
        string permission,Guid id,CancellationToken ct)
    {
        var ev=await PrincipalOwned(db,actor).SingleOrDefaultAsync(x=>x.Id==id,ct)
            ??throw new Problem("SourcingNotFound",404);
        _=await PrincipalActor(core,scope,http,auth,permission,ev.BranchRef,true,ct);
        return ev;
    }
    private static async Task<Reply?> PrincipalReplay(ProcurementDbContext db,HrmDbContext core,
        ProcurementRequestScope scope,HttpContext http,IAuthorizationService auth,RfqPrincipalActor actor,
        string permission,string operation,Guid key,string? fingerprint,Guid? target,CancellationToken ct)
    {
        var receipt=await db.SourcingCommandReceipts.SingleOrDefaultAsync(x=>x.Operation==operation&&x.CommandKey==key,ct);
        if(receipt is null)return null;
        if(receipt.ActorPrincipalId!=actor.PrincipalId || receipt.ResponseSchemaVersion!=2)
            throw new Problem("SourcingReceiptNotFound",404);
        var ev=await PrincipalOwnedEvent(db,core,scope,http,auth,actor,permission,receipt.SourcingEventId,ct);
        if(receipt.ActorWorkerId!=ev.MakerWorkerId || fingerprint is not null && receipt.CommandFingerprint!=fingerprint
            || target is { } id && receipt.SourcingEventId!=id)
            throw new Problem("SourcingCommandKeyConflict",409);
        if(receipt.ResponseSha256!=ProcurementEvidenceEncoding.Digest(receipt.ResponseJson)
            || receipt.ResponseStatusCode!=(operation=="Create"?201:200))
            throw new Problem("SourcingReceiptNeedsReview",409);
        return new(receipt.ResponseJson,receipt.ResponseStatusCode);
    }
    private static async Task<Reply> PrincipalSave(ProcurementDbContext db,ProcurementRequestScope scope,
        SourcingEvent ev,string operation,Guid key,string fingerprint,CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        var body=JsonSerializer.Serialize(new{eventId=ev.Id,ev.Number,status=ev.Status,version=ev.Version,
            originalDraftFingerprint=ev.OriginalDraftFingerprint,commandKey=key,recordedHistoricalResult=true,
            canPublish=false,supplierDeliveryState="NotStarted",financeReservationChanged=false},Json);
        db.AddScoped(new SourcingCommandReceipt{SourcingEventId=ev.Id,Operation=operation,CommandKey=key,
            ActorPrincipalId=scope.SubjectId,ActorWorkerId=ev.MakerWorkerId,ResponseSchemaVersion=2,
            ResultVersion=ev.Version,ResponseStatusCode=operation=="Create"?201:200,
            CommandFingerprint=fingerprint,ResponseJson=body,
            ResponseSha256=ProcurementEvidenceEncoding.Digest(body),CompletedAt=DateTimeOffset.UtcNow});
        return new(body,operation=="Create"?201:200);
    }
    private static async Task<(ApprovedPurchaseSource Source,CompetitiveSourceAvailability Available)> PrincipalEligible(
        ProcurementDbContext db,Mightyfin.Erp.Core.Approvals.Infrastructure.ApprovalDbContext cases,HrmDbContext core,
        ProcurementRequestScope scope,HttpContext http,IAuthorizationService auth,Guid requestId,Guid lineId,
        DateTimeOffset now,CancellationToken ct)
    {
        var source=await ApprovedDemandSourceLoader.LoadAsync(db,cases,scope,requestId,lineId,ct);
        _=await PrincipalActor(core,scope,http,auth,"procurement-sourcing-convert",source.Request.BranchRef,true,ct);
        if(source.Submission.SnapshotSchemaVersion!=3 || source.Submission.FinanceIdentitySchemaVersion!=2)
            throw new Problem("SourcingPrincipalSourceRequired",409);
        if(await ProcurementCatalogClassificationBinding.StoredIssue(db,[source.Line],ct) is not null)
            throw new Problem("SourcingCatalogSourceUnavailable",409);
        var finance=await ProcurementApprovalFinanceEligibility.CheckPrincipalAsync(source.Request,source.Finance,
            http.RequestServices.GetRequiredService<ProcurementApprovalExecutionOptions>(),core,db,scope,now,ct);
        if(source.Submission.FinanceEvidenceMode!=finance.Mode
            || source.Submission.ManualEvidenceAuthorityRef!=finance.Reference)
            throw new Problem("SourcingFinanceAuthorityChanged",409);
        return(source,CompetitiveSourcingSource.Check(source,scope.TenantId,scope.LegalEntityId,now));
    }
    private static SourcingDemandBinding PrincipalBinding(Guid eventId,Guid evidenceId,RfqPrincipalDemandPin p)=>new()
    {
        IdentitySchemaVersion=2,SourcingEventId=eventId,SourceEvidenceId=evidenceId,SourcingLineId=p.EventLineId,
        PurchaseRequestId=p.RequestId,PurchaseRequestLineId=p.LineId,RequestVersion=p.RequestVersion,
        SubmissionId=p.SubmissionId,SubmittedVersion=p.SubmittedVersion,CoreApprovalCaseId=p.CaseId,
        ApprovalOutcomeId=p.OutcomeId,CoreDecisionId=p.DecisionId,RequestSha=p.RequestSha,
        PolicyDecisionSha=p.PolicyDecisionSha,PolicyRulesSha=p.PolicyRulesSha,FinanceSnapshotId=p.FinanceId,
        FinanceSha=p.FinanceSha,MethodId=p.MethodId,MethodRevision=p.MethodRevision,MethodCode=p.MethodCode,
        MinimumQuoteCount=p.MinimumQuoteCount,MakerPrincipalId=p.MakerPrincipalId,
        RequesterPrincipalId=p.RequesterPrincipalId,MakerWorkerId=p.MakerWorkerAlias,
        RequesterWorkerId=p.RequesterWorkerAlias,PrincipalIdentitySha256=p.PrincipalIdentitySha256,
        AllocationId=p.AllocationId,Quantity=p.Quantity,UnitEstimate=p.UnitEstimate,
        EstimatedValue=p.EstimatedValue,UnitCode=p.UnitCode,CategoryCode=p.CategoryCode,DepartmentRef=p.DepartmentId,
    };
}
