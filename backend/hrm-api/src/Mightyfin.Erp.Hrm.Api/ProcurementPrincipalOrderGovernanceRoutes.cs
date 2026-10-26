using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalOrderDraftRoutes
{
    public sealed record FundingInput(long ExpectedOrderVersion,string ManualCommitmentReference,
        string ExternalEvidenceReference,string CurrencyCode,
        [property:JsonConverter(typeof(CommercialGrossDecimalJsonConverter))] decimal AttestedGrossAmount,
        IReadOnlyList<string> SourceReservationReferences,DateTimeOffset ObservedAt);
    public sealed record DecisionInput(string Action,string? Reason,long ExpectedOrderVersion,
        long ExpectedCaseVersion,long ExpectedAssignmentVersion);
    public sealed record VersionInput(long ExpectedOrderVersion);
    public sealed record AcknowledgmentInput(long ExpectedOrderVersion,Guid IssuedVersionId,
        string Outcome,string Channel,string SupplierReference,string EvidenceReference,DateTimeOffset ObservedAt);
    public sealed record ReleaseRequestInput(long ExpectedOrderVersion,string OriginalDirectSourceSha256,string Reason);
    public sealed record ReleaseConfirmationInput(long ExpectedOrderVersion,Guid ReleaseRequestId,
        string ExternalReleaseEvidenceReference,string CurrencyCode,
        [property:JsonConverter(typeof(CommercialGrossDecimalJsonConverter))] decimal ReleasedGrossAmount,
        IReadOnlyList<string> RestoredSourceReservationReferences,DateTimeOffset ConfirmedAt);

    private static void MapGovernanceRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/principal-governed-orders",GovernedBuyerList).RequireAuthorization("procurement-po-read");
        group.MapGet("/{id:guid}/principal-governed-detail",GovernedBuyerDetail).RequireAuthorization("procurement-po-read");
        // Purpose-only action reads enforce the selected operation's permission
        // inside the current company/branch transaction, without buyer-wide read.
        group.MapGet("/principal-operation-work",GovernedOperationWork).RequireAuthorization("procurement-access");
        group.MapGet("/{id:guid}/principal-operation-detail",GovernedOperationDetail).RequireAuthorization("procurement-access");
        group.MapGet("/{id:guid}/principal-review/{assignmentId:guid}",Review).RequireAuthorization("procurement-po-approve");
        group.MapGet("/principal-finance-work",FinanceWork).RequireAuthorization("procurement-po-finance-confirm");
        group.MapGet("/{id:guid}/principal-finance-detail",FinanceDetail).RequireAuthorization("procurement-po-finance-confirm");
        group.MapGet("/principal-assigned-reviews",AssignedReviews).RequireAuthorization("procurement-po-approve");
        group.MapGet("/principal-governance-command-receipts/{operation}/{key:guid}",RecoverGoverned)
            .RequireAuthorization("procurement-access");
        group.MapPost("/{id:guid}/principal-funding",Funding).RequireAuthorization("procurement-po-finance-confirm");
        group.MapPost("/{id:guid}/principal-submit",Submit).RequireAuthorization("procurement-po-submit");
        group.MapPost("/{id:guid}/principal-decisions/{assignmentId:guid}",Decide).RequireAuthorization("procurement-po-approve");
        group.MapPost("/{id:guid}/principal-issue",Issue).RequireAuthorization("procurement-po-issue");
        group.MapPost("/{id:guid}/principal-acknowledgment",Acknowledgment).RequireAuthorization("procurement-po-dispatch-confirm");
        group.MapPost("/{id:guid}/principal-release-request",RequestRelease).RequireAuthorization("procurement-po-edit");
        group.MapPost("/{id:guid}/principal-release-confirmation",ConfirmRelease).RequireAuthorization("procurement-po-finance-confirm");
    }

    private static async Task<IResult> RunGoverned(HttpContext http,ProcurementRequestScope scope,
        Func<ProcurementDbContext,ApprovalDbContext,HrmDbContext,IAuthorizationService,CancellationToken,Task<Reply>> operation,
        CancellationToken ct)
    {
        if (!GovernedContextAllowed(HttpMethods.IsGet(http.Request.Method), scope.WorkLocationId,
                scope.IsExplicitAllBranches))
            return Error("OrderBranchRequired",422);
        var options=http.RequestServices.GetService<ProcurementOrderExecutionOptions>();
        var physical=http.RequestServices.GetService<IProcurementPrincipalOrderGovernanceReadiness>();
        // Historical reads do not acquire new-work authority. The inherited
        // draft/approval transaction gates and current record scope still apply.
        if(options is not {ExecutionDatabaseConfigured:true} || physical is null
            || !HttpMethods.IsGet(http.Request.Method) && !options.PrincipalExecutionEnabled
            || !await physical.IsReadyAsync(ct))
            return Error("PrincipalOrderGovernanceUnavailable",503);
        return await Run(http,scope,async(db,cases,core,auth,token)=>
        {
            if(!await physical.IsReadyAsync(db,!HttpMethods.IsGet(http.Request.Method),token))
                throw new Problem("PrincipalOrderGovernanceUnavailable",503);
            return await operation(db,cases,core,auth,token);
        },ct);
    }

    internal static bool GovernedContextAllowed(bool read,Guid? branch,bool explicitAll) =>
        branch is { } id && id!=Guid.Empty && !explicitAll
        || read && branch is null && explicitAll;

    private static bool GovernedWritesAvailable(HttpContext http,ProcurementRequestScope scope) =>
        http.RequestServices.GetRequiredService<ProcurementOrderExecutionOptions>().PrincipalExecutionEnabled
        && GovernedContextAllowed(false,scope.WorkLocationId,scope.IsExplicitAllBranches);

    private static string GovernanceAction(string operation)=>operation switch
    {
        "Funding" or "ReleaseConfirm"=>"procurement-po-finance-confirm",
        "Submit"=>"procurement-po-submit", "Decide"=>"procurement-po-approve",
        "Issue"=>"procurement-po-issue", "Ack"=>"procurement-po-dispatch-confirm",
        "ReleaseRequest"=>"procurement-po-edit", _=>throw new Problem("OrderReceiptNotFound",404)
    };
    private static int GovernanceResponseStatus(string operation)=>PrincipalOrderGovernanceReceiptEncoding.StatusCode(operation);

    private static Task<IResult> RecoverGoverned(string operation,Guid key,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)=>RunGoverned(http,scope,async(db,cases,core,auth,token)=>
        {
            if(key==Guid.Empty)throw new Problem("OrderReceiptNotFound",404);
            var receipt=await ReplayGoverned(db,cases,core,scope,http,auth,GovernanceAction(operation),operation,key,null,null,token)
                ??throw new Problem("OrderReceiptNotFound",404);
            return new Reply(JsonSerializer.Serialize(new{operation,commandKey=key,
                originalStatusCode=receipt.Status,responseJson=receipt.Body,recordedHistoricalResult=true,
                historicalOnly=true,financeReservationChanged=false,supplierSendPerformed=false},Json));
        },ct);

    private static async Task<Reply?> ReplayGoverned(ProcurementDbContext db,ApprovalDbContext cases,HrmDbContext core,
        ProcurementRequestScope scope,HttpContext http,IAuthorizationService auth,string action,
        string operation,Guid key,string? fingerprint,Guid? target,CancellationToken ct)
    {
        if(action!=GovernanceAction(operation))throw new Problem("OrderReceiptNotFound",404);
        var receipt=await db.PrincipalOrderDraftReceipts.SingleOrDefaultAsync(x=>x.Operation==operation&&x.CommandKey==key,ct);
        if(receipt is null)return null;
        if(receipt.ActorPrincipalId!=scope.SubjectId || target is {} id&&receipt.PurchaseOrderId!=id)
            throw new Problem("OrderReceiptNotFound",404);
        var order=await db.PurchaseOrders.SingleOrDefaultAsync(x=>x.Id==receipt.PurchaseOrderId&&x.PrincipalDraftSchemaVersion==2,ct)
            ??throw new Problem("OrderReceiptNotFound",404);
        _=await Actor(core,scope,http,auth,action,order.BranchRef,true,ct);
        if(fingerprint is not null&&receipt.CommandSha256!=fingerprint)throw new Problem("OrderCommandKeyConflict",409);
        var result=PrincipalOrderGovernanceReceiptEncoding.Verify(receipt,order,operation,key);
        if(!await GovernanceEvidenceMatches(db,cases,order,receipt,result,ct))
            throw new Problem("OrderReceiptNeedsReview",409);
        return new(receipt.ResponseJson,receipt.ResponseStatus);
    }

    private static Task<Reply> SaveGoverned(ProcurementDbContext db,ProcurementRequestScope scope,
        PurchaseOrder order,string operation,Guid key,string fingerprint,CancellationToken ct)
    {
        _=GovernanceAction(operation);
        db.ChangeTracker.DetectChanges();
        var version=db.Entry(order).State==EntityState.Modified?checked(order.Version+1):order.Version;
        var evidence=AddedGovernanceEvidenceId(db,order.Id,operation,key);
        var body=PrincipalOrderGovernanceReceiptEncoding.Encode(order,operation,key,version,evidence);
        db.AddScoped(new PrincipalOrderDraftReceipt{PurchaseOrderId=order.Id,Operation=operation,CommandKey=key,
            ActorPrincipalId=scope.SubjectId,CommandSha256=fingerprint,ResponseJson=body,
            ResponseSha256=ProcurementEvidenceEncoding.Digest(body),ResponseStatus=GovernanceResponseStatus(operation),
            ResultVersion=version,CompletedAt=DateTimeOffset.UtcNow});
        return Task.FromResult(new Reply(body,GovernanceResponseStatus(operation)));
    }

    private static void RecordGoverned(ProcurementDbContext db,ProcurementRequestScope scope,
        PurchaseOrder order,string operation,Guid key,object detail)
    {
        var eventType=operation switch
        {
            "Funding"=>"purchase_order.manual_commitment_recorded.v2",
            "Submit"=>"purchase_order.approval_opened.v2", "Decide"=>"purchase_order.approval_decided.v2",
            "Issue"=>"purchase_order.internal_version_authorized.v2", "Ack"=>"purchase_order.supplier_response_observed.v2",
            "ReleaseRequest"=>"purchase_order.external_release_requested.v2",
            "ReleaseConfirm"=>"purchase_order.external_release_attested.v2", _=>throw new Problem("OrderOperationInvalid",422)
        };
        db.AddScoped(new AuditEvent{RecordType="PurchaseOrder",RecordId=order.Id,Action="PrincipalOrder"+operation,
            ActorSubjectId=scope.SubjectId,CorrelationId=scope.CorrelationId,
            ChangedFieldsJson=JsonSerializer.Serialize(new{schemaVersion=2,commandKey=key,detail},Json)});
        db.AddScoped(new OutboxEvent{AggregateType="PurchaseOrder",AggregateId=order.Id,EventType=eventType,
            IdempotencyKey=$"po.principal.{operation}.{key:N}.v2",
            PayloadJson=JsonSerializer.Serialize(new{orderId=order.Id,commandKey=key,
                internalVersionAuthorized=operation=="Issue",supplierSendPerformed=false,financeReservationChanged=false},Json)});
    }
}

public interface IProcurementPrincipalOrderGovernanceReadiness
{
    Task<bool> IsReadyAsync(CancellationToken ct);
    Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct);
}
