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

public static partial class ProcurementInvoiceUnitReviewRoutes
{
    private static readonly IReadOnlyDictionary<Guid,string> EmptyManualDocumentHashes =
        new Dictionary<Guid,string>();

    private static void MapManualRoutes(WebApplication app)
    {
        var invoices=app.MapGroup("/api/procurement/v1/invoices");
        invoices.MapGet("/{id:guid}/manual-unit-review-readiness",ManualReadiness)
            .RequireAuthorization(InvoiceUnitReviewTransitions.RequestPermission,"procurement-invoice-read");
        invoices.MapGet("/{id:guid}/manual-unit-reviews/commands/{key:guid}",ManualRecoverOpen)
            .RequireAuthorization(InvoiceUnitReviewTransitions.RequestPermission,"procurement-invoice-read");
        invoices.MapPost("/{id:guid}/manual-unit-reviews",ManualOpen)
            .RequireAuthorization(InvoiceUnitReviewTransitions.RequestPermission,"procurement-invoice-read");
        var reviews=app.MapGroup("/api/procurement/v1/manual-invoice-unit-reviews");
        reviews.MapGet("/tasks",ManualTasks).RequireAuthorization(InvoiceRetainedUnitReview.ReviewPermission);
        reviews.MapGet("/{id:guid}",ManualDetail).RequireAuthorization("procurement-access");
        reviews.MapGet("/{id:guid}/commands/{key:guid}",ManualRecover).RequireAuthorization("procurement-access");
        reviews.MapPost("/{id:guid}/assignments/{assignmentId:guid}/decisions",ManualDecide)
            .RequireAuthorization(InvoiceRetainedUnitReview.ReviewPermission);
    }

    internal static bool ManualWriteContext(ProcurementRequestScope scope,bool rawAll) =>
        scope.OrgUnitId is null && scope.PrincipalId?.ToString("D")==scope.SubjectId
        && scope.WorkLocationId is { } branch && branch!=Guid.Empty
        && !scope.IsExplicitAllBranches && !rawAll;

    private static async Task<IResult> RunManual(HttpContext http,ProcurementRequestScope scope,bool write,
        Func<ProcurementDbContext,ApprovalDbContext,HrmDbContext,IAuthorizationService,CancellationToken,Task<string>> work,
        CancellationToken ct)
    {
        http.Response.Headers.CacheControl="no-store";
        try
        {
            var options=http.RequestServices.GetRequiredService<ProcurementInvoiceUnitReviewOptions>();
            if(!options.ExecutionDatabaseConfigured || write && !options.ManualAttestationEnabled)
                throw new ReviewProblem("InvoiceManualUnitReviewUnavailable",503);
            if(write && !ManualWriteContext(scope,http.Request.Headers.ContainsKey("X-Shell-Branch-Scope")))
                throw new ReviewProblem("InvoiceUnitReviewSelectedBranchRequired",422);
            var response=await http.RequestServices.GetRequiredService<ProcurementApprovalTransaction>().ExecuteAsync(async(db,cases,token)=>
            {
                if(!await ProcurementInvoiceManualUnitReviewSchemaCapture.IsInstalledAsync(db,token))
                    throw new ReviewProblem("InvoiceManualUnitReviewSchemaUnavailable",503);
                var ready=http.RequestServices.GetRequiredService<IProcurementInvoiceUnitReviewSchemaReadiness>();
                if(!(write?await ready.CanRecordAsync(db,token):await ready.IsReadyAsync(db,token)))
                    throw new ReviewProblem("InvoiceManualUnitReviewSchemaUnavailable",503);
                await using var core=await ProcurementApprovalExecutionLocks.CoreAsync(db,scope,token);
                return await work(db,cases,core,http.RequestServices.GetRequiredService<IAuthorizationService>(),token);
            },ct);
            return Results.Content(response,"application/json");
        }
        catch(OperationCanceledException){throw;}
        catch(Exception error)when(error is ReviewProblem or InvalidOperationException or ArgumentException
            or UnauthorizedAccessException or KeyNotFoundException or JsonException or System.Data.Common.DbException
            or DbUpdateException or FormatException or OverflowException)
        {
            var status=error is ReviewProblem p?p.Status:error is OperationalPolicyPublicationProblem policy?policy.Status:
                ProcurementPostgresFailures.IsConcurrency(error)||error is DbUpdateConcurrencyException?409:
                ProcurementPostgresFailures.IsStoreFailure(error)?503:error is UnauthorizedAccessException?403:
                error is KeyNotFoundException?404:409;
            return Results.Json(new{code=error is ReviewProblem?error.Message:"InvoiceManualUnitReviewEvidenceUnavailable",
                message="Review the entered invoice units, current independent assignment, policy and company access. No invoice approval or payment was created."},statusCode:status);
        }
    }

    internal static bool IsManualPolicy(InvoiceUnitReviewPolicyRules.Document policy) =>
        policy.SchemaVersion==2 && policy.EvidenceMode=="ManualAttestation";
    private static void RequireManualPolicy(PolicyVersion policy)
    {
        if(!IsManualPolicy(InvoiceUnitReviewPolicyRules.Parse(policy.RulesJson)))
            throw new ReviewProblem("InvoiceManualAttestationPolicyRequired",409);
    }
    private static InvoiceUnitReviewTransitions.Source RequireManualSource(InvoiceUnitReviewSource source)
    {
        var stored=InvoiceUnitReviewStorage.Read(source);
        if(stored.SourceSchemaVersion!=4 || stored.ReviewPolicy.SchemaVersion!=2
            || stored.ReviewPolicy.EvidenceMode!="ManualAttestation")throw new KeyNotFoundException();
        return stored;
    }
    private static void RequireManualMapping(InvoiceMatchMappingVersion mapping,SupplierInvoice invoice,
        IReadOnlyList<SupplierInvoiceLine> originals)
    {
        if(mapping.SourceSchemaVersion!=3)throw new ReviewProblem("PrincipalInvoiceMappingRequired",409);
        var snapshot=InvoiceMappingReviewSourceDispatch.Verify(mapping,invoice,originals);
        if(snapshot.Lines.Any(x=>x.DocumentVersionId!=Guid.Empty || !string.IsNullOrWhiteSpace(x.DocumentLineReference)
            || string.IsNullOrWhiteSpace(x.InvoiceUnitCode) || x.InvoiceUnitCode.Length>20
            || x.InvoiceUnitCode!=x.InvoiceUnitCode.Trim()))
            throw new ReviewProblem("InvoiceManualMappingWithoutDocumentPinsRequired",409);
    }
    private static async Task<string?> ReplayManual(ProcurementDbContext db,ProcurementRequestScope scope,
        InvoiceUnitReviewRequest request,SupplierInvoice invoice,CancellationToken ct)
    {
        var receipt=await db.InvoiceUnitReviewReceipts.SingleOrDefaultAsync(x=>x.Operation==request.Operation
            && x.CommandKey==request.CommandKey && x.ActorPrincipalId==scope.SubjectId,ct);
        if(receipt is null)return null;
        if(receipt.Fingerprint!=ProcurementEvidenceEncoding.PolicyJsonDigest(InvoiceUnitReviewCommands.Serialize(request)))
            throw new ReviewProblem("InvoiceUnitReviewCommandKeyConflict",409);
        var source=await db.InvoiceUnitReviewSources.SingleAsync(x=>x.Id==receipt.ReviewSourceId,ct);
        RequireManualSource(source);
        if(source.BranchRef!=invoice.BranchRef || source.SupplierInvoiceId!=invoice.Id
            || request.InvoiceId!=invoice.Id || request.ReviewId is { } id && source.Id!=id)
            throw new ReviewProblem("InvoiceUnitReviewCommandKeyConflict",409);
        _=InvoiceUnitReviewCommands.ReadHistorical(receipt,source);
        return receipt.ResponseJson;
    }
}
