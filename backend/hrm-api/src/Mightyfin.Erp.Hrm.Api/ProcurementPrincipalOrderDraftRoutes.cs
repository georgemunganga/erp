using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>P30 internal direct-purchase draft only. Its independent option and schema
/// inspector remain off until the owning P30 migration and finite-role rehearsal.</summary>
public static partial class ProcurementPrincipalOrderDraftRoutes
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private sealed class Problem(string code,int status):Exception(code){internal int Status=>status;}
    private sealed record Reply(string Body,int Status=200);
    public sealed record CreateInput(OrderDraftInput Draft,IReadOnlyList<OrderSourceSelection> Sources);
    public sealed record CancelInput(long ExpectedOrderVersion,string OriginalDirectSourceSha256,string Reason);
    public sealed record EditInput(long ExpectedOrderVersion,string OriginalDirectSourceSha256,
        string Reason,OrderDraftInput Draft);
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

    public static void MapRoutes(WebApplication app)
    {
        var group=app.MapGroup("/api/procurement/v1/orders");
        group.MapGet("/approved-principal-direct-sources",Sources).RequireAuthorization("procurement-po-convert");
        group.MapGet("/principal-direct-suppliers",EligibleSuppliers).RequireAuthorization("procurement-po-convert");
        group.MapPost("/from-approved-principal-direct-sources",Create).RequireAuthorization("procurement-po-convert");
        group.MapGet("/principal-direct-drafts",List).RequireAuthorization("procurement-po-read");
        group.MapGet("/{id:guid}/principal-direct-draft",Detail).RequireAuthorization("procurement-po-read");
        group.MapGet("/{id:guid}/principal-review/{assignmentId:guid}",Review)
            .RequireAuthorization("procurement-po-approve");
        group.MapGet("/principal-finance-work",FinanceWork)
            .RequireAuthorization("procurement-po-finance-confirm");
        group.MapGet("/{id:guid}/principal-finance-detail",FinanceDetail)
            .RequireAuthorization("procurement-po-finance-confirm");
        group.MapGet("/principal-assigned-reviews",AssignedReviews)
            .RequireAuthorization("procurement-po-approve");
        group.MapGet("/principal-fulfilment-work",FulfilmentWork)
            .RequireAuthorization("procurement-access");
        group.MapGet("/{id:guid}/principal-fulfilment-detail",FulfilmentDetail)
            .RequireAuthorization("procurement-access");
        group.MapPost("/{id:guid}/principal-direct-cancel",Cancel).RequireAuthorization("procurement-po-edit");
        group.MapPost("/{id:guid}/principal-direct-edit",Edit).RequireAuthorization("procurement-po-edit");
        // Recovery is scoped by the original command action inside Replay. A
        // Finance recorder or assigned reviewer need not hold broad PO read.
        group.MapGet("/principal-direct-command-receipts/{operation}/{key:guid}",Recover).RequireAuthorization("procurement-access");
        group.MapPost("/{id:guid}/principal-funding",Funding).RequireAuthorization("procurement-po-finance-confirm");
        group.MapPost("/{id:guid}/principal-submit",Submit).RequireAuthorization("procurement-po-submit");
        group.MapPost("/{id:guid}/principal-decisions/{assignmentId:guid}",Decide).RequireAuthorization("procurement-po-approve");
        group.MapPost("/{id:guid}/principal-issue",Issue).RequireAuthorization("procurement-po-issue");
        group.MapPost("/{id:guid}/principal-acknowledgment",Acknowledgment).RequireAuthorization("procurement-po-dispatch-confirm");
        group.MapPost("/{id:guid}/principal-release-request",RequestRelease).RequireAuthorization("procurement-po-edit");
        group.MapPost("/{id:guid}/principal-release-confirmation",ConfirmRelease).RequireAuthorization("procurement-po-finance-confirm");
    }
    private static async Task<IResult> Run(HttpContext http,ProcurementRequestScope scope,
        Func<ProcurementDbContext,ApprovalDbContext,HrmDbContext,IAuthorizationService,CancellationToken,Task<Reply>> operation,
        CancellationToken ct)
    {
        http.Response.Headers.CacheControl="private, no-store";
        try
        {
            var options=http.RequestServices.GetRequiredService<ProcurementPrincipalOrderDraftOptions>();
            var approvals=http.RequestServices.GetRequiredService<ProcurementApprovalExecutionOptions>();
            var readiness=http.RequestServices.GetRequiredService<IProcurementPrincipalOrderDraftReadiness>();
            if(!options.Enabled || !options.ExecutionDatabaseConfigured || !approvals.Enabled
                || !approvals.PrincipalExecutionEnabled || !approvals.ExecutionDatabaseConfigured
                || !HttpMethods.IsGet(http.Request.Method) && !await readiness.IsReadyAsync(ct))throw new Problem("PrincipalOrderDraftUnavailable",503);
            if(scope.OrgUnitId is not null)throw new Problem("OrderDepartmentScopeUnsupported",503);
            var result=await http.RequestServices.GetRequiredService<ProcurementApprovalTransaction>().ExecuteAsync(async(db,cases,token)=>
            {
                if(!await readiness.IsReadyAsync(db,!HttpMethods.IsGet(http.Request.Method),token))
                    throw new Problem("PrincipalOrderDraftUnavailable",503);
                await using var core=await ProcurementApprovalExecutionLocks.CoreAsync(db,scope,token);
                return await operation(db,cases,core,http.RequestServices.GetRequiredService<IAuthorizationService>(),token);
            },ct);
            return Results.Content(result.Body,"application/json",statusCode:result.Status);
        }
        catch(ApprovedDemandLoadException){return Error("OrderSourceUnavailable",409);}
        catch(Exception error) when(error is Problem or InvalidOperationException or ArgumentException or UnauthorizedAccessException
            or KeyNotFoundException or JsonException or DbUpdateException or NpgsqlException or OverflowException
            || ProcurementPostgresFailures.IsStoreFailure(error))
        {
            var pg=ProcurementPostgresFailures.Postgres(error);
            // Store exception text can contain row values. Keep support diagnostics
            // to metadata only, consistent with the invoice matching endpoint.
            if(error is not Problem && ProcurementPostgresFailures.IsStoreFailure(error))
                http.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Procurement.PrincipalOrders")
                    .LogWarning("Purchase order store operation failed. TraceId: {TraceId}; ExceptionType: {ExceptionType}; SqlState: {SqlState}; Schema: {Schema}; Table: {Table}; Constraint: {Constraint}",
                        http.TraceIdentifier,error.GetType().FullName,pg?.SqlState,pg?.SchemaName,pg?.TableName,pg?.ConstraintName);
            var status=error is Problem p?p.Status:ProcurementPostgresFailures.IsConcurrency(error)||pg?.SqlState=="23505"?409
                :ProcurementPostgresFailures.IsStoreFailure(error)?503:error is UnauthorizedAccessException?403:409;
            return Error(error is Problem?error.Message:status==503?"OrderSaveUnconfirmed":"OrderSourceNeedsReview",status);
        }
    }
    private static IResult Error(string code,int status)=>Results.Json(new{code,
        message=status==503?"Purchase orders are unavailable or this save could not be confirmed. Keep the command reference and check again."
            :"Refresh this order and check your access, sources and remaining quantities."},statusCode:status);
    private static Guid Key(HttpContext http)=>Guid.TryParse(http.Request.Headers["Idempotency-Key"],out var key)&&key!=Guid.Empty
        ?key:throw new Problem("OrderCommandKeyRequired",400);
    private static string Fingerprint(object value)=>ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(value,Json));
    private static string? Clean(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    // The shared source loader predates explicit All and insists on a selected
    // branch for confined users. Resolve and authorize the actual current branch
    // first, then present that branch only to this retained source check.
    private sealed record SourceBranchScope(ProcurementRequestScope Parent,Guid Branch):IProcurementScope
    {
        public string TenantId=>Parent.TenantId;
        public Guid LegalEntityId=>Parent.LegalEntityId;
        public Guid? WorkLocationId=>Branch;
        public Guid? OrgUnitId=>Parent.OrgUnitId;
        public bool IsConfined=>true;
        public string SubjectId=>Parent.SubjectId;
        public Guid? WorkerId=>Parent.WorkerId;
        public Guid? CoreLinkedWorkerId=>Parent.CoreLinkedWorkerId;
        public string? CorrelationId=>Parent.CorrelationId;
    }
    private static async Task<ApprovedPurchaseSource> Source(ProcurementDbContext db,ApprovalDbContext cases,
        HrmDbContext core,ProcurementRequestScope scope,HttpContext http,IAuthorizationService auth,
        string action,Guid requestId,Guid lineId,CancellationToken ct)
    {
        var branch=await db.PurchaseRequests.AsNoTracking().Where(x=>x.Id==requestId)
            .Select(x=>x.BranchRef).SingleOrDefaultAsync(ct);
        if(branch is null || branch==Guid.Empty)throw new Problem("OrderSourceUnavailable",409);
        _=await Actor(core,scope,http,auth,action,branch,true,ct);
        return await ApprovedDemandSourceLoader.LoadAsync(db,cases,new SourceBranchScope(scope,branch.Value),requestId,lineId,ct);
    }
    private static async Task<ProcurementPrincipalPeople.Person> Actor(HrmDbContext core,ProcurementRequestScope scope,
        HttpContext http,IAuthorizationService auth,string action,Guid? branch,bool target,CancellationToken ct)
    {
        if(scope.PrincipalId?.ToString("D")!=scope.SubjectId || scope.OrgUnitId is not null)
            throw new Problem("OrderActionDenied",403);
        var person=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,scope.SubjectId,ct);
        if(person is null || person.CoreLinkedWorkerId!=scope.CoreLinkedWorkerId
            || !person.Permissions.Contains(action)
            || !(await auth.AuthorizeAsync(http.User,action)).Succeeded)throw new Problem("OrderActionDenied",403);
        try{_=await ProcurementExecutionRecordScope.Branches(core,scope,branch,target,ct);}
        catch(KeyNotFoundException){throw new Problem("OrderNotFound",404);}
        return person;
    }
    private static async Task<PurchaseOrder> Owned(ProcurementDbContext db,HrmDbContext core,
        ProcurementRequestScope scope,HttpContext http,IAuthorizationService auth,string action,Guid id,CancellationToken ct)
    {
        var order=await db.PurchaseOrders.SingleOrDefaultAsync(x=>x.Id==id&&x.PrincipalDraftSchemaVersion==2
            &&x.BuyerSubjectId==scope.SubjectId,ct)??throw new Problem("OrderNotFound",404);
        _=await Actor(core,scope,http,auth,action,order.BranchRef,true,ct);
        return order;
    }
    private static async Task<Reply?> Replay(ProcurementDbContext db,HrmDbContext core,ProcurementRequestScope scope,
        HttpContext http,IAuthorizationService auth,string action,string operation,Guid key,string? fingerprint,Guid? target,CancellationToken ct)
    {
        var receipt=await db.PrincipalOrderDraftReceipts.SingleOrDefaultAsync(x=>x.Operation==operation&&x.CommandKey==key,ct);
        if(receipt is null)return null;
        if(receipt.ActorPrincipalId!=scope.SubjectId || target is { } id && receipt.PurchaseOrderId!=id)
            throw new Problem("OrderReceiptNotFound",404);
        var order=await db.PurchaseOrders.SingleOrDefaultAsync(x=>x.Id==receipt.PurchaseOrderId
            &&x.PrincipalDraftSchemaVersion==2,ct)??throw new Problem("OrderReceiptNotFound",404);
        if(operation is "Create" or "Cancel" or "Edit" && order.BuyerSubjectId!=scope.SubjectId)
            throw new Problem("OrderReceiptNotFound",404);
        _=await Actor(core,scope,http,auth,action,order.BranchRef,true,ct);
        if(fingerprint is not null && receipt.CommandSha256!=fingerprint)throw new Problem("OrderCommandKeyConflict",409);
        if(receipt.SchemaVersion!=2 || receipt.ResponseStatus!=ResponseStatus(operation)
            || receipt.ResponseSha256!=ProcurementEvidenceEncoding.Digest(receipt.ResponseJson))
            throw new Problem("OrderReceiptNeedsReview",409);
        return new(receipt.ResponseJson,receipt.ResponseStatus);
    }
    private static Task<Reply> Save(ProcurementDbContext db,ProcurementRequestScope scope,PurchaseOrder order,
        string operation,Guid key,string fingerprint,CancellationToken ct)
    {
        // Create and Cancel share one physical transaction with their receipt. A rollback
        // removes both; an unknown network result is recovered by actor-owned GET.
        db.ChangeTracker.DetectChanges();
        var version=db.Entry(order).State==EntityState.Modified?checked(order.Version+1):order.Version;
        var body=JsonSerializer.Serialize(new{orderId=order.Id,order.Number,status=order.Status,version,
            originalDirectSourceSha256=order.OriginalDirectSourceSha256,commandKey=key,
            recordedHistoricalResult=true,manualFundingOnly=true,financeReservationChanged=false,
            supplierSendPerformed=false},Json);
        db.AddScoped(new PrincipalOrderDraftReceipt{PurchaseOrderId=order.Id,Operation=operation,CommandKey=key,
            ActorPrincipalId=scope.SubjectId,CommandSha256=fingerprint,ResponseJson=body,
            ResponseSha256=ProcurementEvidenceEncoding.Digest(body),ResponseStatus=ResponseStatus(operation),
            ResultVersion=version,CompletedAt=DateTimeOffset.UtcNow});
        return Task.FromResult(new Reply(body,ResponseStatus(operation)));
    }
    private static int ResponseStatus(string operation)=>operation is "Create" or "Submit" or "Issue"?201:200;
    private static void Record(ProcurementDbContext db,ProcurementRequestScope scope,PurchaseOrder order,
        string operation,Guid key,object detail)
    {
        db.AddScoped(new AuditEvent{RecordType="PurchaseOrder",RecordId=order.Id,Action="PrincipalOrder"+operation,
            ActorSubjectId=scope.SubjectId,CorrelationId=scope.CorrelationId,
            ChangedFieldsJson=JsonSerializer.Serialize(new{schemaVersion=2,commandKey=key,detail},Json)});
        db.AddScoped(new OutboxEvent{AggregateType="PurchaseOrder",AggregateId=order.Id,
            EventType=operation switch {"Create"=>"purchase_order.internal_draft_reserved.v2",
                "Cancel"=>"purchase_order.internal_draft_cancelled.v2",
                "Edit"=>"purchase_order.internal_unfunded_draft_revised.v2",
                "Funding"=>"purchase_order.manual_commitment_recorded.v2",
                "Submit"=>"purchase_order.approval_opened.v2",
                "Decide"=>"purchase_order.approval_decided.v2",
                "Issue"=>"purchase_order.internal_version_authorized.v2",
                "Ack"=>"purchase_order.supplier_response_observed.v2",
                "ReleaseRequest"=>"purchase_order.external_release_requested.v2",
                "ReleaseConfirm"=>"purchase_order.external_release_attested.v2",
                _=>throw new InvalidOperationException("OrderEventInvalid")},
            IdempotencyKey=$"po.principal.{operation}.{key:N}.v2",
            PayloadJson=JsonSerializer.Serialize(new{orderId=order.Id,commandKey=key,
                internalVersionAuthorized=operation=="Issue",supplierSendPerformed=false,
                financeReservationChanged=false},Json)});
    }
}

public sealed class ProcurementPrincipalOrderDraftOptions
{
    public bool Enabled {get;set;}
    public bool ExecutionDatabaseConfigured {get;set;}
}
public interface IProcurementPrincipalOrderDraftReadiness
{
    Task<bool> IsReadyAsync(CancellationToken ct);
    Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct);
}
public sealed class UnavailableProcurementPrincipalOrderDraftReadiness:IProcurementPrincipalOrderDraftReadiness
{
    public Task<bool> IsReadyAsync(CancellationToken ct)=>Task.FromResult(false);
    public Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct)=>Task.FromResult(false);
}
