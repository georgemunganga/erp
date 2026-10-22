using System.Text.Json;
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

/// <summary>Owned invoice mapping/comparison, not invoice approval or AP authority.</summary>
public static partial class ProcurementInvoiceMatchingRoutes
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private sealed class MatchingProblem(string code,int status) : Exception(code) { public int Status=>status; }
    public sealed record MappingInput(long Version,Guid PurchaseOrderId,Guid IssuedVersionId,InvoiceMappingLine[] Lines);
    public sealed record CompareInput(long Version,Guid MappingVersionId,string? Reason);
    public static void MapRoutes(WebApplication app)
    {
        var group=app.MapGroup("/api/procurement/v1/invoices");
        group.MapPost("/{id:guid}/matching/mappings",Map).RequireAuthorization("procurement-invoice-capture");
        group.MapPost("/{id:guid}/matching/compare",Compare).RequireAuthorization("procurement-invoice-match");
        group.MapGet("/{id:guid}/matching/sources",Sources).RequireAuthorization("procurement-invoice-match","procurement-invoice-read");
        group.MapGet("/{id:guid}/matching/sources/{orderId:guid}/lines/{lineId:guid}/acceptances",Acceptances).RequireAuthorization("procurement-invoice-match","procurement-invoice-read");
        group.MapGet("/{id:guid}/matching",Read).RequireAuthorization("procurement-invoice-read");
        group.MapGet("/{id:guid}/matching/commands/{key:guid}",Recover).RequireAuthorization("procurement-access");
    }
    private static async Task<IResult> Run(HttpContext http,ProcurementRequestScope scope,
        Func<ProcurementDbContext,ApprovalDbContext,HrmDbContext,IAuthorizationService,CancellationToken,Task<string>> work,CancellationToken ct)
    {
        try
        {
            var options=http.RequestServices.GetRequiredService<ProcurementInvoiceMatchingExecutionOptions>();
            if(!options.Enabled || !options.ExecutionDatabaseConfigured || !await http.RequestServices.GetRequiredService<IProcurementInvoiceMatchingSchemaReadiness>().IsReadyAsync(ct))
                throw new MatchingProblem("InvoiceMatchingUnavailable",503);
            var json=await http.RequestServices.GetRequiredService<ProcurementApprovalTransaction>().ExecuteAsync(async(db,cases,token)=>
            {
                await using var core=await ProcurementApprovalExecutionLocks.CoreAsync(db,scope,token);
                return await work(db,cases,core,http.RequestServices.GetRequiredService<IAuthorizationService>(),token);
            },ct);
            return Results.Content(json,"application/json");
        }
        catch(Exception error) when(error is MatchingProblem or InvalidOperationException or ArgumentException or UnauthorizedAccessException
            or KeyNotFoundException or JsonException or DbUpdateException or NpgsqlException or OverflowException or FormatException)
        {
            var pg=error.GetBaseException() as PostgresException;
            // Exception messages/details may contain database row values. Log only
            // diagnostic metadata; never pass the exception, body or headers to the sink.
            if (error is not MatchingProblem)
                http.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Procurement.InvoiceMatching")
                    .LogWarning("Invoice matching command rejected. TraceId: {TraceId}; ExceptionType: {ExceptionType}; FailureSite: {FailureSite}; SqlState: {SqlState}; Constraint: {Constraint}",
                        http.TraceIdentifier, error.GetType().FullName,
                        error.TargetSite?.DeclaringType?.FullName + "." + error.TargetSite?.Name,
                        pg?.SqlState, pg?.ConstraintName);
            var status=error is MatchingProblem p ? p.Status : error is UnauthorizedAccessException ? 403 : error is KeyNotFoundException ? 404
                : pg?.SqlState is "40001" or "40P01" || error is DbUpdateConcurrencyException ? 409 : error is NpgsqlException ? 503 : 409;
            return Results.Json(new { code=error is MatchingProblem ? error.Message : "InvoiceMatchingEvidenceNeedsReview",
                message=status==503 ? "Invoice matching is not ready. Ask your administrator to check its configuration."
                    : "Refresh the invoice and review its original documents, current access and source evidence. No new payment was created." },statusCode:status);
        }
    }
    private static async Task<Guid?> Actor(HrmDbContext core,ProcurementRequestScope scope,HttpContext http,
        IAuthorizationService auth,string permission,Guid? branch,CancellationToken ct)
    {
        if (!(await auth.AuthorizeAsync(http.User, permission)).Succeeded) throw new MatchingProblem("InvoiceMatchingAccessDenied",403);
        var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct);
        if (actor is null || scope.PrincipalId is not { } principal || actor.PrincipalId != principal.ToString("D")
            || !actor.Permissions.Contains(permission)) throw new MatchingProblem("InvoiceMatchingAccessDenied",403);
        _=await ProcurementExecutionRecordScope.Branches(core,scope,branch,true,ct);
        return actor.WorkerId;
    }
    private static async Task<SupplierInvoice> Invoice(ProcurementDbContext db,ProcurementRequestScope scope,Guid id,CancellationToken ct,bool locked=false)
    {
        if(locked) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.supplier_invoices WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE",ct);
        return await db.SupplierInvoices.SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw new MatchingProblem("InvoiceMatchingInvoiceNotFound",404);
    }
    private static async Task<PurchaseOrder> Target(ProcurementDbContext db,HrmDbContext core,ProcurementRequestScope scope,
        HttpContext http,IAuthorizationService auth,string permission,SupplierInvoice invoice,Guid orderId,CancellationToken ct)
    {
        var order=await db.PurchaseOrders.SingleOrDefaultAsync(x=>x.Id==orderId,ct) ?? throw new MatchingProblem("InvoiceMatchingOrderNotFound",404);
        _=await Actor(core,scope,http,auth,permission,order.BranchRef,ct);
        if(order.BranchRef!=invoice.BranchRef || order.SupplierId!=invoice.SupplierId || order.CurrencyCode!=invoice.CurrencyCode
            || invoice.PurchaseOrderId is { } originalOrder && originalOrder!=order.Id) throw new MatchingProblem("InvoiceMatchingOrderBindingInvalid",409);
        return order;
    }
    private static Guid Key(HttpContext http) => Guid.TryParse(http.Request.Headers["Idempotency-Key"],out var key) && key!=Guid.Empty ? key : throw new MatchingProblem("InvoiceMatchingCommandKeyRequired",400);
    private static string Fingerprint(object value)=>ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(value,Json));
    private static DateTimeOffset Now()=>ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
    private static async Task<string?> Replay(ProcurementDbContext db,ProcurementRequestScope scope,string operation,Guid key,
        string fingerprint,Guid invoiceId,Guid orderId,CancellationToken ct)
    {
        var receipt=await db.CommercialCommandReceipts.SingleOrDefaultAsync(x=>x.Operation==operation && x.CommandKey==key,ct);
        if(receipt is null) return null;
        using var body=JsonDocument.Parse(receipt.ResponseJson);
        if(receipt.ActorPrincipalId!=scope.SubjectId || receipt.Fingerprint!=fingerprint || receipt.PurchaseOrderId!=orderId
            || body.RootElement.GetProperty("invoiceId").GetGuid()!=invoiceId) throw new MatchingProblem("InvoiceMatchingCommandKeyConflict",409);
        return receipt.ResponseJson;
    }
    private static async Task<string> Save(ProcurementDbContext db,ProcurementRequestScope scope,Guid invoiceId,Guid orderId,
        string operation,string permission,Guid key,string fingerprint,Func<object> view,CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        var response=JsonSerializer.Serialize(new { invoiceId,commandKey=key,commandPermission=permission,commandSchemaVersion=1,
            recordedHistoricalResult=true,isCurrentApproval=false,canPostToFinance=false,result=view() },Json);
        db.AddScoped(new ProcurementCommercialCommandReceipt { PurchaseOrderId=orderId,ActorPrincipalId=scope.SubjectId,Operation=operation,
            CommandKey=key,Fingerprint=fingerprint,ResponseJson=response,CompletedAt=Now() });
        return response;
    }
    private static Task<IResult> Map(Guid id,MappingInput input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var key=Key(http); var fingerprint=Fingerprint(new { schemaVersion=1,id,input });
            if(input.Lines is not { Length: >0 and <=200 }) throw new MatchingProblem("InvoiceMappingLinesRequired",422);
            var invoice=await Invoice(db,scope,id,token); var worker=await Actor(core,scope,http,auth,"procurement-invoice-capture",invoice.BranchRef,token);
            _=await Target(db,core,scope,http,auth,"procurement-invoice-capture",invoice,input.PurchaseOrderId,token);
            // Exact recovery rechecks both the actual invoice and target PO's current scope first.
            if(await Replay(db,scope,"InvoiceMapping",key,fingerprint,id,input.PurchaseOrderId,token) is { } replay) return replay;
            _=await ProcurementReceivingRoutes.InvoiceEvidence(db,cases,scope,input.PurchaseOrderId,input.IssuedVersionId,input.Lines,token,false);
            invoice=await Invoice(db,scope,id,token,true);
            if(invoice.Version!=input.Version || invoice.ActiveMatchClaimResultId is not null) throw new MatchingProblem("InvoiceMappingVersionOrActiveClaimChanged",409);
            var lines=await db.SupplierInvoiceLines.Where(x=>x.SupplierInvoiceId==id && x.IsActive).OrderBy(x=>x.LineNumber).ToArrayAsync(token);
            var mapping=InvoiceMappingEvidence.Create(invoice,lines,input.PurchaseOrderId,input.IssuedVersionId,input.Lines,scope.SubjectId,worker,Now(), identitySchemaVersion: 2);
            db.AddScoped(mapping); invoice.CurrentMappingVersionId=mapping.Id; invoice.MappingRevision=mapping.MappingRevision; invoice.MatchingState="MappingUnverified";
            return await Save(db,scope,id,input.PurchaseOrderId,"InvoiceMapping","procurement-invoice-capture",key,fingerprint,
                ()=>new { mapping.Id,mapping.MappingRevision,mapping.MappingSha256,mapping.VerificationState,invoice.Version },token);
        },ct);
}
