using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementApprovalExecutionRoutes
{
    internal static void MapCommandAcknowledgements(WebApplication app) => app.MapGet(
        "/api/procurement/v1/requests/{id:guid}/approval-commands/{operation}/{commandKey:guid}", CommandAcknowledgement)
        .RequireAuthorization("procurement-access");

    private static async Task<IResult> CommandAcknowledgement(Guid id,string operation,Guid commandKey,HttpContext http,
        ProcurementDbContext db,ApprovalDbContext workflow,HrmDbContext core,ProcurementRequestScope scope,
        ProcurementApprovalExecutionOptions options,IAuthorizationService authorization,CancellationToken ct)
    {
        http.Response.Headers.CacheControl="no-store";
        IDbContextTransaction? snapshot=null;ApprovalDbContext? ownedWorkflow=null;HrmDbContext? ownedCore=null;
        try
        {
            if(id==Guid.Empty||commandKey==Guid.Empty||operation is not ("Open" or "Decide"))throw Invalid("ApprovalCommandAcknowledgementInputInvalid");
            // Historical acknowledgement requires read availability, never an
            // execution connection, current spending limit or manual Finance policy.
            if(!options.Enabled||!options.PrincipalExecutionEnabled)throw AcknowledgementUnavailable();
            var permission=operation=="Open"?OpenPermission:DecidePermission;
            _=await Actor(core,scope,http,authorization,permission,ct);
            // Read the actual owning read store, not an execution-role capability or a
            // current Finance/manual-policy decision. Normal registration stays closed.
            if(!await ProcurementPrincipalApprovalSchemaGuard.ReadyAsync(db,http.RequestServices,false,ct))throw AcknowledgementUnavailable();
            if(db.Database.IsRelational())
            {
                if(!db.Database.IsNpgsql()||db.Database.CurrentTransaction is not null)throw AcknowledgementUnavailable();
                snapshot=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
                await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY",ct);
                ownedWorkflow=new ApprovalDbContext(new DbContextOptionsBuilder<ApprovalDbContext>()
                    .UseNpgsql(db.Database.GetDbConnection()).Options,scope);
                await ownedWorkflow.Database.UseTransactionAsync(snapshot.GetDbTransaction(),ct);workflow=ownedWorkflow;
                ownedCore=await ProcurementApprovalExecutionLocks.CoreAsync(db,scope,ct);core=ownedCore;
                _=await Actor(core,scope,http,authorization,permission,ct);
                if(!await ProcurementPrincipalApprovalSchemaGuard.ReadyAsync(db,http.RequestServices,false,ct))throw AcknowledgementUnavailable();
            }
            var request=await ScopedRequests(db,scope).AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw Missing();
            await RecordScope(core,scope,request,ct);
            // Exact actor is in the database predicate. Wrong actor/request/key never
            // exposes another person's acknowledgement or raw stored result.
            var receipt=await db.ApprovalCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x=>x.PurchaseRequestId==id
                &&x.Operation==operation&&x.CommandKey==commandKey&&x.ActorSubjectId==scope.SubjectId,ct)??throw Missing();
            var pin=await db.ApprovalSubmissions.AsNoTracking().SingleOrDefaultAsync(x=>x.PurchaseRequestId==id
                &&x.CoreApprovalCaseId==receipt.CoreApprovalCaseId,ct)??throw AcknowledgementUnavailable();
            var approvalCase=await workflow.Cases.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==receipt.CoreApprovalCaseId,ct)
                ??throw AcknowledgementUnavailable();
            if(!PurchaseRequestPrincipalIdentity.Valid(request)||!PurchaseRequestApprovalPinning.HasRetainedPrincipalIdentity(pin)
                ||pin.RequesterPrincipalId!=request.RequesterPrincipalId||pin.RequesterWorkerId!=request.RequesterCoreWorkerId
                ||pin.RequesterIdentitySha256!=request.RequesterIdentitySha256||pin.MakerSubjectId!=request.CreatedBy
                ||receipt.TenantId!=scope.TenantId||receipt.LegalEntityId!=scope.LegalEntityId||receipt.CompletedAt==default
                ||receipt.CommandFingerprint is not {Length:64} fingerprint||!fingerprint.All(x=>char.IsAsciiDigit(x)||x is >= 'a' and <= 'f')
                ||approvalCase.SourceModule!="procurement"||approvalCase.SourceType!="purchase_request"||approvalCase.SourceId!=id
                ||approvalCase.SourceVersion!=pin.SubmittedBusinessVersion||approvalCase.PolicyVersionId!=pin.PolicyVersionId
                ||approvalCase.PolicyVersionNumber!=pin.PolicyVersionNumber||approvalCase.MakerSubjectId!=pin.MakerSubjectId
                ||receipt.CompletedAt<approvalCase.OpenedAt)throw AcknowledgementUnavailable();
            var originalScope=ReadAcknowledgementSourceScope(pin);
            if(scope.WorkLocationId is {} branch&&originalScope.Branch!=branch
                ||scope.OrgUnitId is {} department&&originalScope.Department!=department)throw Missing();
            await RecordScope(core,scope,new PurchaseRequest {BranchRef=originalScope.Branch},ct);
            var original=ReadAcknowledgementResult(operation,receipt.ResponseJson);
            if(original.CaseId!=approvalCase.Id||original.RequestId!=id||original.SubmittedVersion!=pin.SubmittedBusinessVersion
                ||original.RequestVersion>request.Version||original.CaseVersion>approvalCase.Version)
                throw AcknowledgementUnavailable();
            if(operation=="Open")
            {
                if(receipt.CoreAssignmentId is not null||pin.OpeningCommandKey!=commandKey||original.State!="Pending"
                    ||original.RequestState!="Submitted"||original.ApprovalState!="Pending")throw AcknowledgementUnavailable();
            }
            else
            {
                if(receipt.CoreAssignmentId is not {} assignmentId||assignmentId==Guid.Empty)throw AcknowledgementUnavailable();
                var assignment=await workflow.Assignments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==assignmentId&&x.CaseId==approvalCase.Id,ct)
                    ??throw AcknowledgementUnavailable();
                var bound=await db.AssignmentAuthorityEvidence.AsNoTracking().SingleOrDefaultAsync(x=>x.SubmissionId==pin.Id
                    &&x.CoreApprovalCaseId==approvalCase.Id&&x.CoreAssignmentId==assignmentId,ct)??throw AcknowledgementUnavailable();
                _=await workflow.Stages.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==assignment.StageId&&x.CaseId==approvalCase.Id,ct)
                    ??throw AcknowledgementUnavailable();
                var retained=PurchaseRequestPrincipalAssignmentIdentity.ReadVerified(bound);
                var decision=await workflow.Decisions.AsNoTracking().SingleOrDefaultAsync(x=>x.CaseId==approvalCase.Id
                    &&x.AssignmentId==assignmentId&&x.IdempotencyKey==commandKey.ToString("D")&&x.ActorSubjectId==receipt.ActorSubjectId,ct)
                    ??throw AcknowledgementUnavailable();
                if(original.AssignmentId!=assignmentId||original.DecisionId!=decision.Id
                    ||original.AssignmentVersion is not {} originalAssignmentVersion||originalAssignmentVersion>assignment.Version
                    ||bound.IdentitySchemaVersion!=2||retained is null||bound.PrincipalId!=receipt.ActorSubjectId
                    ||bound.CoreStageId!=assignment.StageId||assignment.AssigneeSubjectId!=receipt.ActorSubjectId
                    ||assignment.PermissionKey!=DecidePermission||decision.StageId!=assignment.StageId
                    ||receipt.CompletedAt<decision.DecidedAt)throw AcknowledgementUnavailable();
                var outcome=await db.ApprovalOutcomes.AsNoTracking().SingleOrDefaultAsync(x=>x.SubmissionId==pin.Id
                    &&x.CoreDecisionId==decision.Id,ct);
                if(original.State=="Pending")
                {
                    if(decision.Action!="Approve"||outcome is not null||original.RequestState!="Submitted")throw AcknowledgementUnavailable();
                }
                else if(outcome is null||outcome.Outcome!=original.State||outcome.PurchaseRequestId!=id
                    ||outcome.CoreApprovalCaseId!=approvalCase.Id||outcome.DecisionCommandKey!=commandKey.ToString("D")
                    ||outcome.ActorSubjectId!=receipt.ActorSubjectId||outcome.SubmittedBusinessVersion!=pin.SubmittedBusinessVersion
                    ||decision.Action!=(original.State=="Approved"?"Approve":original.State=="Rejected"?"Reject":"Return"))
                    throw AcknowledgementUnavailable();
            }
            return Results.Ok(new ApprovalCommandAcknowledgement(1,"Completed",operation,commandKey,id,approvalCase.Id,
                receipt.CoreAssignmentId,receipt.CompletedAt,original));
        }
        catch(Exception error) when(Handled(error)||error is JsonException or FormatException or KeyNotFoundException or DbException)
        {
            if(error.GetBaseException() is DbException||error is JsonException or FormatException or KeyNotFoundException)
                return Failure(AcknowledgementUnavailable());
            return Failure(error);
        }
        finally
        {
            if(ownedCore is not null)await ownedCore.DisposeAsync();
            if(ownedWorkflow is not null)await ownedWorkflow.DisposeAsync();
            if(snapshot is not null)await snapshot.DisposeAsync();
        }
    }

    private static ApprovalHttpError AcknowledgementUnavailable()=>new(503,"ApprovalCommandAcknowledgementUnavailable",
        "The original approval command cannot currently be acknowledged. Keep its original key and check again; absence is not proof that it did not complete.");
    internal sealed record ApprovalCommandAcknowledgement(int AcknowledgementSchemaVersion,string Acknowledgement,string Operation,
        Guid CommandKey,Guid RequestId,Guid CaseId,Guid? AssignmentId,DateTimeOffset CompletedAt,ApprovalCommandOriginalResult Result);
    internal sealed record ApprovalCommandOriginalResult(Guid CaseId,Guid RequestId,long SubmittedVersion,long RequestVersion,
        long CaseVersion,string State,string RequestState,string ApprovalState,
        [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] Guid? AssignmentId=null,
        [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] long? AssignmentVersion=null,
        [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] Guid? DecisionId=null);
    internal static ApprovalCommandOriginalResult ReadAcknowledgementResult(string operation,string json)
    {
        if(operation is not ("Open" or "Decide")||string.IsNullOrEmpty(json)||json.Length>4096)throw AcknowledgementUnavailable();
        using var document=JsonDocument.Parse(json,new JsonDocumentOptions {MaxDepth=4});var root=document.RootElement;
        if(root.ValueKind!=JsonValueKind.Object)throw AcknowledgementUnavailable();
        string[] common=["caseId","requestId","submittedVersion","requestVersion","caseVersion","state","requestState","approvalState"];
        string[] expected=operation=="Open"?common:[..common,"assignmentId","assignmentVersion","decisionId"];
        var names=root.EnumerateObject().Select(x=>x.Name).ToArray();
        if(names.Length!=expected.Length||names.Distinct(StringComparer.Ordinal).Count()!=names.Length
            ||names.Except(expected,StringComparer.Ordinal).Any())throw AcknowledgementUnavailable();
        Guid Identity(string name)=>root.GetProperty(name).ValueKind==JsonValueKind.String&&root.GetProperty(name).TryGetGuid(out var id)&&id!=Guid.Empty?id:throw AcknowledgementUnavailable();
        long Version(string name)=>root.GetProperty(name).ValueKind==JsonValueKind.Number&&root.GetProperty(name).TryGetInt64(out var version)&&version>0?version:throw AcknowledgementUnavailable();
        string Text(string name)=>root.GetProperty(name).ValueKind==JsonValueKind.String?root.GetProperty(name).GetString()!:throw AcknowledgementUnavailable();
        var value=new ApprovalCommandOriginalResult(Identity("caseId"),Identity("requestId"),Version("submittedVersion"),Version("requestVersion"),
            Version("caseVersion"),Text("state"),Text("requestState"),Text("approvalState"),
            operation=="Decide"?Identity("assignmentId"):null,operation=="Decide"?Version("assignmentVersion"):null,
            operation=="Decide"?Identity("decisionId"):null);
        var stateValid=value.State switch {"Pending"=>value.RequestState=="Submitted","Approved"=>value.RequestState=="Approved",
            "Rejected"=>value.RequestState=="Rejected","Returned"=>value.RequestState=="ReturnPendingFinanceRelease",_=>false};
        if(!stateValid||value.ApprovalState!=value.State||value.RequestVersion<value.SubmittedVersion)throw AcknowledgementUnavailable();
        return value;
    }
    private static (Guid? Branch,Guid? Department) ReadAcknowledgementSourceScope(PurchaseRequestApprovalSubmission pin)
    {
        try
        {
            if(pin.RequestSnapshotJson is not {Length:>0 and <=1000000} json||ProcurementEvidenceEncoding.Digest(json)!=pin.RequestSnapshotSha256
                ||ProcurementEvidenceEncoding.PolicyJsonDigest(json).Length!=64)
                throw AcknowledgementUnavailable();
            using var document=JsonDocument.Parse(json);var root=document.RootElement;
            if(root.GetProperty("schemaVersion").GetInt32()!=3
                ||root.GetProperty("RequesterPrincipalId").GetGuid()!=pin.RequesterPrincipalId
                ||root.GetProperty("RequesterIdentitySha256").GetString()!=pin.RequesterIdentitySha256)throw AcknowledgementUnavailable();
            var alias=root.GetProperty("RequesterCoreWorkerId");
            if(alias.ValueKind==JsonValueKind.Null?pin.RequesterWorkerId is not null:
                !alias.TryGetGuid(out var linked)||linked==Guid.Empty||linked!=pin.RequesterWorkerId)throw AcknowledgementUnavailable();
            var source=root.GetProperty("legacyBusiness");
            if(source.GetProperty("schemaVersion").GetInt32()!=1||source.GetProperty("Id").GetGuid()!=pin.PurchaseRequestId
                ||source.GetProperty("TenantId").GetString()!=pin.TenantId||source.GetProperty("LegalEntityId").GetGuid()!=pin.LegalEntityId
                ||source.GetProperty("SubmittedVersion").GetInt64()!=pin.SubmittedBusinessVersion
                ||source.GetProperty("CreatedBy").GetString()!=pin.MakerSubjectId)throw AcknowledgementUnavailable();
            Guid? Optional(string name)=>source.GetProperty(name).ValueKind==JsonValueKind.Null?null:
                source.GetProperty(name).TryGetGuid(out var id)&&id!=Guid.Empty?id:throw AcknowledgementUnavailable();
            return (Optional("BranchRef"),Optional("DepartmentRef"));
        }
        catch(Exception error) when(error is JsonException or FormatException or KeyNotFoundException or InvalidOperationException or ArgumentException)
        { throw AcknowledgementUnavailable(); }
    }
}
