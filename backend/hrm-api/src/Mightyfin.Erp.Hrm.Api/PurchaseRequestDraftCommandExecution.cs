using System.Buffers.Binary;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Core.Persistence.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Unregistered P26 building block. Mandatory owning callbacks must be
/// connected to accepted readiness and transaction-neutral ORIGINAL route actions.
/// No route mapping, business-validator copy, Finance/Core workflow or activation.</summary>
internal sealed class PurchaseRequestDraftCommandExecution(string connectionString)
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);

    internal async Task<PurchaseRequestDraftCommandReply> ExecuteAsync<T>(HttpContext http,ProcurementRequestScope original,
        IAuthorizationService authorization,EncodedPurchaseRequestDraftCommand<T> command,
        Func<ProcurementDbContext,CancellationToken,Task> requireWriteReady,
        Func<ProcurementDbContext,HrmDbContext,ProcurementRequestScope,T,CancellationToken,Task<Guid>> originalBusinessAction,
        Func<ProcurementDbContext,Guid,CancellationToken,Task<string>> serializeActualPersistedDetail,
        CancellationToken ct=default) where T:class
    {
        ArgumentNullException.ThrowIfNull(requireWriteReady);ArgumentNullException.ThrowIfNull(originalBusinessAction);
        ArgumentNullException.ThrowIfNull(serializeActualPersistedDetail);command=SnapshotEncoding(original,command);
        for(var attempt=1;;attempt++)
        {
            try
            {
                return await ExecuteAttemptAsync(http,original,authorization,command,requireWriteReady,originalBusinessAction,serializeActualPersistedDetail,ct);
            }
            catch(Exception error) when(attempt<3&&ConfirmedAbort(error))
            {await Task.Delay(TimeSpan.FromMilliseconds(40*attempt),ct);}
            // No unknown commit/network/unique violation retry. A key unique race
            // returns retained conflict; a later explicit GET/same-key call owns recovery.
            catch(Exception error) when(ProcurementPostgresFailures.Postgres(error) is
                {SqlState:PostgresErrorCodes.UniqueViolation,ConstraintName:"ux_pr_draft_receipt_command"})
            {throw new PurchaseRequestDraftCommandError(409,"RequestDraftCommandConflict");}
        }
    }
    private async Task<PurchaseRequestDraftCommandReply> ExecuteAttemptAsync<T>(HttpContext http,ProcurementRequestScope original,
        IAuthorizationService authorization,EncodedPurchaseRequestDraftCommand<T> command,
        Func<ProcurementDbContext,CancellationToken,Task> requireReady,
        Func<ProcurementDbContext,HrmDbContext,ProcurementRequestScope,T,CancellationToken,Task<Guid>> business,
        Func<ProcurementDbContext,Guid,CancellationToken,Task<string>> serialize,CancellationToken ct) where T:class
    {
        // Every retry uses a NEW connection and contexts. No borrowed endpoint
        // catalog filter/outer transaction is allowed to surround this adapter.
        await using var connection=new NpgsqlConnection(connectionString);await connection.OpenAsync(ct);
        await using var branch=await CoreBranchAuthorizationFence.AcquireAsync(connection,original.TenantId,false,ct);
        await using var catalog=await ProcurementCatalogAuthorityFence.AcquireAsync(connection,original.TenantId,original.LegalEntityId,false,ct);
        await using var commandLease=await PostgresSessionAdvisoryLease.AcquireAsync(connection,
            CommandLeaseKey(original.TenantId,original.LegalEntityId,command.Intent.Operation,command.CommandKey),true,ct);
        await using var transaction=await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var scope=CopyContext(original);
        await using var db=Store(connection,scope);await db.Database.UseTransactionAsync(transaction,ct);
        await using var core=await ProcurementApprovalExecutionLocks.CoreAsync(db,scope,ct);
        await RequireCurrentAsync(core,http,scope,original,authorization,command.Intent.Operation,ct);
        await requireReady(db,ct);
        var prior=await db.PurchaseRequestDraftCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x=>x.Operation==command.Intent.Operation
            &&x.CommandKey==command.CommandKey,ct); // actor deliberately NOT in lookup/unique family
        if(prior is not null)
        {
            var replay=MatchCompleted(prior,scope,command.Intent.Operation,command.CommandKey,command.Fingerprint,command.Intent.TargetId);
            _=await OwnedParentAsync(db,scope,prior.PurchaseRequestId,ct);
            await transaction.CommitAsync(ct);return replay;
        }
        if(command.Intent.TargetId is {} target)
        {
            await ProcurementApprovalExecutionLocks.LockRequestAsync(db,scope,target,ct);
            _=await OwnedParentAsync(db,scope,target,ct);
        }
        // Only NOW may current catalog/default/CAS/mutability validation execute.
        // Callback must throw/reject on failure, not return an IResult early or
        // begin/commit its own transaction. Existing Update retains UpdatedAt bump.
        var id=await business(db,core,scope,command.AcceptedInput,ct);
        Require(id!=Guid.Empty&&(command.Intent.TargetId is null||id==command.Intent.TargetId),"RequestDraftCommandTargetInvalid");
        await db.SaveChangesAsync(ct);
        Require(!core.ChangeTracker.HasChanges(),"RequestDraftCommandCoreWriteUnsupported");
        var parent=await OwnedParentAsync(db,scope,id,ct);
        // Owning ToDetail adapter must re-read final rows/versions/timestamps, then
        // serialize ONCE with the SAME options as the original successful route.
        var raw=await serialize(db,id,ct);
        Require(!db.ChangeTracker.HasChanges()&&!core.ChangeTracker.HasChanges(),"RequestDraftCommandSerializationMustReadOnly");
        var metadata=new PurchaseRequestDraftCommandMetadata(scope.TenantId,scope.LegalEntityId,command.Intent.Operation,command.CommandKey,
            scope.PrincipalId!.Value,id,scope.WorkLocationId,scope.OrgUnitId,parent.BranchRef,parent.DepartmentRef,
            command.Intent.ExpectedVersion,parent.Version,command.Fingerprint,DateTimeOffset.UtcNow);
        var receipt=PurchaseRequestDraftCommandReceipts.Complete(metadata,raw);db.AddScoped(receipt);
        await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
        return Reply(receipt);
    }

    /// <summary>Consistent historic read. No execution/write flag, Finance/default,
    /// catalog, mutable-state/CAS or business callback participates in acknowledgement.</summary>
    internal async Task<PurchaseRequestDraftCommandAcknowledgement> ReadAsync(HttpContext http,ProcurementRequestScope original,
        IAuthorizationService authorization,string operation,string rawKey,
        Func<ProcurementDbContext,CancellationToken,Task> requireReadReady,CancellationToken ct=default)
    {
        ArgumentNullException.ThrowIfNull(requireReadReady);
        Require(PurchaseRequestDraftCommandReceipts.OperationSupported(operation)
            &&PurchaseRequestDraftCommandReceipts.TryCommandKey(rawKey,out _),"RequestDraftCommandLocatorInvalid");
        PurchaseRequestDraftCommandReceipts.TryCommandKey(rawKey,out var key);
        await using var connection=new NpgsqlConnection(connectionString);await connection.OpenAsync(ct);
        await using var branch=await CoreBranchAuthorizationFence.AcquireAsync(connection,original.TenantId,false,ct);
        await using var transaction=await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
        await using(var readOnly=new NpgsqlCommand("SET TRANSACTION READ ONLY",connection,transaction))await readOnly.ExecuteNonQueryAsync(ct);
        var scope=CopyContext(original);await using var db=Store(connection,scope);await db.Database.UseTransactionAsync(transaction,ct);
        await using var core=await ProcurementApprovalExecutionLocks.CoreAsync(db,scope,ct);
        await RequireCurrentAsync(core,http,scope,original,authorization,operation,ct);await requireReadReady(db,ct);
        var receipt=await db.PurchaseRequestDraftCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x=>x.Operation==operation&&x.CommandKey==key,ct)
            ??throw new PurchaseRequestDraftCommandError(404,"RequestDraftCommandNotFound");
        _=MatchCompleted(receipt,scope,operation,key,null,null);
        _=await OwnedParentAsync(db,scope,receipt.PurchaseRequestId,ct); // ownership/scope ONLY, never today's status/version
        await transaction.CommitAsync(ct);
        return new(1,"Completed",receipt.Operation,receipt.CommandKey,receipt.ActorPrincipalId,receipt.PurchaseRequestId,
            receipt.ContextBranchRef,receipt.ContextDepartmentRef,receipt.OriginalBranchRef,receipt.OriginalDepartmentRef,
            receipt.CommandFingerprint,receipt.CompletedAt,receipt.ResponseStatusCode,receipt.ResponseSchemaVersion,receipt.ResponseSha256,receipt.ResponseJson);
        // Missing404 is not evidence that the original command did not commit.
    }

    internal static long CommandLeaseKey(string tenant,Guid company,string operation,Guid key)
    {
        Require(!string.IsNullOrWhiteSpace(tenant)&&!tenant.Contains('\0')&&company!=Guid.Empty&&key!=Guid.Empty
            &&PurchaseRequestDraftCommandReceipts.OperationSupported(operation),"RequestDraftCommandLeaseIdentityInvalid");
        var identity="greenwebb.procurement.pr-draft-command.v1\0"+tenant+"\0"+company.ToString("D")+"\0"+operation+"\0"+key.ToString("D");
        return BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }
    internal static bool ConfirmedAbort(Exception error)=>ProcurementPostgresFailures.IsConcurrency(error);
    internal static string Permission(string operation)=>operation switch
    {"Create"=>"procurement-request-create","Update"=>"procurement-request-edit","Submit"=>"procurement-request-submit",
        _=>throw new PurchaseRequestDraftCommandError(400,"RequestDraftCommandOperationInvalid")};

    internal static PurchaseRequestDraftCommandReply MatchCompleted(PurchaseRequestDraftCommandReceipt receipt,ProcurementRequestScope scope,
        string operation,Guid key,string? fingerprint,Guid? target)
    {
        if(receipt.TenantId!=scope.TenantId||receipt.LegalEntityId!=scope.LegalEntityId||receipt.Operation!=operation||receipt.CommandKey!=key
            ||scope.PrincipalId!=receipt.ActorPrincipalId||receipt.ContextBranchRef!=scope.WorkLocationId||receipt.ContextDepartmentRef!=scope.OrgUnitId)
            throw new PurchaseRequestDraftCommandError(404,"RequestDraftCommandNotFound");
        PurchaseRequestDraftCommandReceipts.ValidateCompleted(receipt);
        Require(receipt.Version==1&&receipt.CreatedBy==receipt.ActorPrincipalId.ToString("D")&&receipt.CreatedAt>=receipt.CompletedAt
            &&receipt.UpdatedAt is null&&receipt.UpdatedBy is null,"RequestDraftCommandStoredResultInvalid");
        if(fingerprint is not null&&receipt.CommandFingerprint!=fingerprint||target is not null&&receipt.PurchaseRequestId!=target)
            throw new PurchaseRequestDraftCommandError(409,"RequestDraftCommandConflict");
        return Reply(receipt);
    }
    private static PurchaseRequestDraftCommandReply Reply(PurchaseRequestDraftCommandReceipt value)=>new(value.ResponseStatusCode,value.ResponseJson,
        value.Operation=="Create"?"/api/procurement/v1/requests/"+value.PurchaseRequestId.ToString("D"):null,value.CommandKey,value.CommandFingerprint,value.ResponseSha256);
    private static ProcurementDbContext Store(NpgsqlConnection connection,ProcurementRequestScope scope)=>new(
        new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection,n=>n.MigrationsHistoryTable("__procurement_migrations","procurement")).Options,scope);
    private static ProcurementRequestScope CopyContext(ProcurementRequestScope original)
    {
        Require(original.PrincipalId is {} id&&id!=Guid.Empty&&original.SubjectId==id.ToString("D")
            &&ProcurementPrincipalEvidence.Canonical(original.SubjectId)&&original.WorkLocationId!=Guid.Empty&&original.OrgUnitId!=Guid.Empty
            &&(!original.IsConfined||original.WorkLocationId is not null),"RequestDraftCommandScopeInvalid");
        var fresh=new ProcurementRequestScope();fresh.Set(original.TenantId,original.LegalEntityId,original.SubjectId,
            original.WorkLocationId,original.OrgUnitId,original.IsConfined,original.CorrelationId??"pr-draft-command");return fresh;
    }
    private static async Task RequireCurrentAsync(HrmDbContext core,HttpContext http,ProcurementRequestScope scope,ProcurementRequestScope original,
        IAuthorizationService authorization,string operation,CancellationToken ct)
    {
        var subject=http.User.FindFirstValue("sub")??http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var person=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,scope.SubjectId,ct);
        var permission=Permission(operation);
        if(http.User.Identity?.IsAuthenticated!=true||subject!=scope.SubjectId||person is null||person.PrincipalId!=scope.SubjectId
            ||!person.Permissions.Contains("procurement-access")||!person.Permissions.Contains(permission)
            ||!person.OrganisationWide&&(scope.WorkLocationId is not {} branch||!person.AllowedBranches.Contains(branch))
            ||!(await authorization.AuthorizeAsync(http.User,"procurement-access")).Succeeded
            ||!(await authorization.AuthorizeAsync(http.User,permission)).Succeeded)
            throw new PurchaseRequestDraftCommandError(403,"RequestDraftCommandPermissionUnavailable");
        // Revalidate effective owning Core objects AFTER the branch lease. The
        // earlier HTTP middleware scope can predate a concurrent branch change.
        if(scope.WorkLocationId is {} location&&!await core.WorkLocations.AsNoTracking().AnyAsync(x=>x.TenantId==scope.TenantId
            &&x.LegalEntityId==scope.LegalEntityId&&x.Id==location&&!x.IsArchived,ct)
            ||scope.OrgUnitId is {} department&&(!person.OrganisationWide||!await core.OrgUnits.AsNoTracking().AnyAsync(x=>x.TenantId==scope.TenantId
                &&x.LegalEntityId==scope.LegalEntityId&&x.Id==department&&!x.IsArchived&&x.Status=="active",ct)))
            throw new PurchaseRequestDraftCommandError(403,"RequestDraftCommandScopeUnavailable");
        scope.AssignPrincipal(original.PrincipalId!.Value,person.CoreLinkedWorkerId);
        if(person.WorkerId is {} worker)scope.AssignWorker(worker); // metadata only, never a prerequisite
    }
    private static async Task<PurchaseRequest> OwnedParentAsync(ProcurementDbContext db,ProcurementRequestScope scope,Guid id,CancellationToken ct)
    {
        // Same existing PurchaseRequestOwnership and route effective branch/department
        // boundaries; no role label/employee projection confers record ownership.
        var query=db.PurchaseRequests.AsNoTracking().Where(PurchaseRequestOwnership.OwnedBy(scope.PrincipalId));
        if(scope.WorkLocationId.HasValue||scope.IsConfined)query=query.Where(x=>scope.WorkLocationId!=null&&x.BranchRef==scope.WorkLocationId);
        if(scope.OrgUnitId.HasValue)query=query.Where(x=>x.DepartmentRef==scope.OrgUnitId);
        return await query.SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new PurchaseRequestDraftCommandError(404,"RequestDraftCommandNotFound");
    }
    internal static void ValidateEncoding<T>(ProcurementRequestScope scope,EncodedPurchaseRequestDraftCommand<T> command) where T:class
        =>_=SnapshotEncoding(scope,command);
    internal static EncodedPurchaseRequestDraftCommand<T> SnapshotEncoding<T>(ProcurementRequestScope scope,EncodedPurchaseRequestDraftCommand<T> command) where T:class
    {
        // Re-encode and DETACH accepted DTO/list before any await. External caller
        // mutations while waiting on leases cannot change the business input.
        // All operations reuse the exact same host allowlist/normalizer.
        var body=JsonSerializer.SerializeToElement(command.AcceptedInput,Json);var key=command.CommandKey.ToString("D");
        object snapshot=command.AcceptedInput switch
        {
            ProcurementRequestRoutes.UpdateRequest=>PurchaseRequestDraftCommandIntents.Update(scope,command.Intent.TargetId??Guid.Empty,key,body),
            ProcurementRequestRoutes.NewRequest=>PurchaseRequestDraftCommandIntents.Create(scope,key,body),
            ProcurementRequestRoutes.VersionCommand=>PurchaseRequestDraftCommandIntents.Submit(scope,command.Intent.TargetId??Guid.Empty,key,body),
            _=>throw new InvalidOperationException("RequestDraftCommandInputUnsupported")
        };
        var frozen=(EncodedPurchaseRequestDraftCommand<T>)snapshot;
        Require(frozen.Fingerprint==command.Fingerprint&&JsonSerializer.Serialize(command.Intent,Json)==command.IntentJson
            &&PurchaseRequestDraftCommandReceipts.Fingerprint(command.IntentJson)==command.Fingerprint,"RequestDraftCommandIntentChanged");
        return frozen;
    }
    private static void Require(bool ok,string code){if(!ok)throw new InvalidOperationException(code);}
}
internal sealed record PurchaseRequestDraftCommandReply(int StatusCode,string RawJson,string? Location,Guid CommandKey,string CommandFingerprint,string ResponseSha256);
internal sealed record PurchaseRequestDraftCommandAcknowledgement(int AcknowledgementSchemaVersion,string Acknowledgement,string Operation,Guid CommandKey,
    Guid ActorPrincipalId,Guid RequestId,Guid? ContextBranchRef,Guid? ContextDepartmentRef,Guid? OriginalBranchRef,Guid? OriginalDepartmentRef,
    string CommandFingerprint,DateTimeOffset CompletedAt,int OriginalStatusCode,int ResponseSchemaVersion,string ResponseSha256,string OriginalResultJson);
internal sealed class PurchaseRequestDraftCommandError(int status,string code):Exception(code)
{internal int Status{get;}=status;internal string Code{get;}=code;}
