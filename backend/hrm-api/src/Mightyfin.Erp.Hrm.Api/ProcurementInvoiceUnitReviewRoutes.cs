using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Api;
public static partial class ProcurementInvoiceUnitReviewRoutes
{
    private sealed class ReviewProblem(string code, int status) : Exception(code) { internal int Status => status; }
    public sealed record OpenInput(long InvoiceVersion, int MappingRevision, string? Reason);
    public sealed record DecisionInput(long InvoiceVersion, long CaseVersion, long AssignmentVersion, string Action, string? Reason, string? Attestation);
    public static void MapRoutes(WebApplication app)
    {
        var invoices = app.MapGroup("/api/procurement/v1/invoices");
        invoices.MapGet("/{id:guid}/unit-review-readiness", Readiness).RequireAuthorization(InvoiceUnitReviewTransitions.RequestPermission, "procurement-invoice-read");
        invoices.MapPost("/{id:guid}/unit-reviews", Open).RequireAuthorization(InvoiceUnitReviewTransitions.RequestPermission, "procurement-invoice-read");
        var reviews = app.MapGroup("/api/procurement/v1/invoice-unit-reviews");
        reviews.MapGet("/tasks", Tasks).RequireAuthorization(InvoiceRetainedUnitReview.ReviewPermission);
        reviews.MapGet("/{id:guid}", Detail).RequireAuthorization("procurement-access");
        reviews.MapGet("/{id:guid}/commands/{key:guid}", Recover).RequireAuthorization("procurement-access");
        reviews.MapPost("/{id:guid}/assignments/{assignmentId:guid}/decisions", Decide).RequireAuthorization(InvoiceRetainedUnitReview.ReviewPermission);
        reviews.MapGet("/{id:guid}/documents", Documents).RequireAuthorization("procurement-access");
        reviews.MapGet("/{id:guid}/documents/{versionId:guid}/download", Download).RequireAuthorization("procurement-access");
    }
    private static Task<IResult> Run(HttpContext http, ProcurementRequestScope scope, bool write,
        Func<ProcurementDbContext, ApprovalDbContext, HrmDbContext, IAuthorizationService, CancellationToken, Task<string>> work, CancellationToken ct)
        => RunResult(http, scope, write, async (db, cases, core, auth, token) => Results.Content(await work(db, cases, core, auth, token), "application/json"), ct);
    private static async Task<IResult> RunResult(HttpContext http, ProcurementRequestScope scope, bool write,
        Func<ProcurementDbContext, ApprovalDbContext, HrmDbContext, IAuthorizationService, CancellationToken, Task<IResult>> work, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        try
        {
            var options = http.RequestServices.GetRequiredService<ProcurementInvoiceUnitReviewOptions>();
            if (!options.ExecutionDatabaseConfigured || write && !options.Enabled) throw new ReviewProblem("InvoiceUnitReviewUnavailable", 503);
            return await http.RequestServices.GetRequiredService<ProcurementApprovalTransaction>().ExecuteAsync(async (db, cases, token) =>
            {
                var ready = http.RequestServices.GetRequiredService<IProcurementInvoiceUnitReviewSchemaReadiness>();
                if (!(write ? await ready.CanRecordAsync(db, token) : await ready.IsReadyAsync(db, token))) throw new ReviewProblem("InvoiceUnitReviewSchemaUnavailable", 503);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                return await work(db, cases, core, http.RequestServices.GetRequiredService<IAuthorizationService>(), token);
            }, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is ReviewProblem or InvalidOperationException or ArgumentException or UnauthorizedAccessException
            or KeyNotFoundException or JsonException or Mightyfin.Erp.Core.Documents.Application.DocumentProblemException or System.Data.Common.DbException or DbUpdateException or FormatException or OverflowException)
        {
            var status = error is ReviewProblem problem ? problem.Status : error is Mightyfin.Erp.Core.Documents.Application.DocumentProblemException documentProblem ? documentProblem.Code == "document-not-found" ? 404 : 409 : error is OperationalPolicyPublicationProblem policyProblem ? policyProblem.Status : ProcurementPostgresFailures.IsConcurrency(error) || error is DbUpdateConcurrencyException ? 409
                : ProcurementPostgresFailures.IsStoreFailure(error) ? 503 : error is UnauthorizedAccessException ? 403 : error is KeyNotFoundException ? 404 : 409;
            return Results.Json(new { code = error is ReviewProblem ? error.Message : "InvoiceUnitReviewEvidenceUnavailable",
                message = "Review the current invoice, original documents, reviewer assignment and company access before continuing. No invoice approval or Finance posting was created." }, statusCode: status);
        }
    }
    private static async Task<InvoiceUnitReviewPolicyRules.Person> Actor(HrmDbContext core, ProcurementRequestScope scope,
        HttpContext http, IAuthorizationService auth, Guid? branch, string[] actions, CancellationToken ct)
    {
        var person = await ProcurementInvoiceUnitReviewPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct)
            ?? throw new UnauthorizedAccessException();
        if (scope.PrincipalId?.ToString("D") != person.PrincipalId || !person.Permissions.Contains("procurement-access")) throw new UnauthorizedAccessException();
        var allowed = false;
        foreach (var action in actions)
            if (person.Permissions.Contains(action) && (await auth.AuthorizeAsync(http.User, null, action)).Succeeded) { allowed = true; break; }
        if (!allowed) throw new UnauthorizedAccessException();
        _ = await ProcurementExecutionRecordScope.Branches(core, scope, branch, true, ct);
        // The canonical people projection excludes archived/foreign company locations.
        // A retained raw assignment to such a location cannot grant document/history access.
        if (!person.OrganisationWide && (branch is not { } target || !person.AllowedBranches.Contains(target))) throw new KeyNotFoundException();
        return person;
    }
    private static async Task<SupplierInvoice> Invoice(ProcurementDbContext db, ProcurementRequestScope scope, Guid id, CancellationToken ct, bool locked)
    {
        if (locked) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.supplier_invoices WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE", ct);
        return await db.SupplierInvoices.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
    }
    private static async Task<(InvoiceUnitReviewSource Source, SupplierInvoice Invoice)> Source(ProcurementDbContext db, ProcurementRequestScope scope, Guid id, bool locked, CancellationToken ct)
    {
        var source = await db.InvoiceUnitReviewSources.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
        var invoice = await Invoice(db, scope, source.SupplierInvoiceId, ct, locked);
        if (source.BranchRef != invoice.BranchRef) throw new KeyNotFoundException();
        _ = InvoiceUnitReviewStorage.Read(source); return (source, invoice);
    }
    private static async Task<ApprovalCase> Workflow(ApprovalDbContext cases, ProcurementRequestScope scope, InvoiceUnitReviewSource source, bool locked, CancellationToken ct)
    {
        if (locked) await cases.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM core_approval.cases WHERE \"TenantId\"={scope.TenantId} AND \"LegalEntityId\"={scope.LegalEntityId} AND \"Id\"={source.CoreCaseId} FOR UPDATE", ct);
        return await cases.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments).SingleOrDefaultAsync(x => x.Id == source.CoreCaseId
            && x.SourceModule == "procurement" && x.SourceType == InvoiceRetainedUnitReview.ReviewSourceType && x.SourceId == source.Id && x.SourceVersion == 1, ct) ?? throw new KeyNotFoundException();
    }
    private static Guid Key(HttpContext http) => Guid.TryParseExact(http.Request.Headers["Idempotency-Key"], "D", out var key) && key != Guid.Empty ? key : throw new ReviewProblem("InvoiceUnitReviewCommandKeyRequired", 400);
    private static DateTimeOffset Now() => ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
    private static string? Reason(string? value)
    {
        var reason = string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (reason.Length > 2000) throw new ReviewProblem("InvoiceUnitReviewReasonInvalid", 422); return reason.Length == 0 ? null : reason;
    }
    private static async Task<PolicyVersion> Policy(ProcurementDbContext db, ProcurementRequestScope scope, HttpContext http, string key, CancellationToken ct)
    {
        var now = Now(); var rows = await db.PolicyVersions.Where(x => x.PolicyKey == key && x.State == "Published" && x.EffectiveFrom <= now && (x.EffectiveTo == null || now < x.EffectiveTo)).Take(2).ToArrayAsync(ct);
        if (rows.Length != 1) throw new ReviewProblem("InvoiceUnitReviewPublishedPolicyUnavailable", 409);
        await ProcurementOperationalPolicyCurrentPublication.RequireAsync(db, scope, http.RequestServices, rows[0], ct); return rows[0];
    }
    private static async Task<InvoiceMatchMappingVersion> Mapping(ProcurementDbContext db, SupplierInvoice invoice, CancellationToken ct)
        => await db.InvoiceMatchMappingVersions.SingleOrDefaultAsync(x => x.Id == invoice.CurrentMappingVersionId && x.SupplierInvoiceId == invoice.Id && x.MappingRevision == invoice.MappingRevision, ct)
            ?? throw new ReviewProblem("InvoiceUnitReviewCurrentMappingRequired", 409);
    private static Task<SupplierInvoiceLine[]> Originals(ProcurementDbContext db, Guid id, CancellationToken ct)
        => db.SupplierInvoiceLines.Where(x => x.SupplierInvoiceId == id && x.IsActive).OrderBy(x => x.LineNumber).ToArrayAsync(ct);
    private static async Task<string?> Replay(ProcurementDbContext db, ProcurementRequestScope scope, InvoiceUnitReviewRequest request, SupplierInvoice invoice, Guid? worker, CancellationToken ct)
    {
        var receipt = await db.InvoiceUnitReviewReceipts.SingleOrDefaultAsync(x => x.Operation == request.Operation && x.CommandKey == request.CommandKey, ct);
        if (receipt is null) return null;
        if (receipt.ActorPrincipalId != scope.SubjectId || receipt.Fingerprint != ProcurementEvidenceEncoding.PolicyJsonDigest(InvoiceUnitReviewCommands.Serialize(request)))
            throw new ReviewProblem("InvoiceUnitReviewCommandKeyConflict", 409);
        var source = await db.InvoiceUnitReviewSources.SingleAsync(x => x.Id == receipt.ReviewSourceId, ct);
        if (source.BranchRef != invoice.BranchRef || source.SupplierInvoiceId != invoice.Id || source.SupplierInvoiceId != request.InvoiceId || request.ReviewId is { } id && source.Id != id) throw new ReviewProblem("InvoiceUnitReviewCommandKeyConflict", 409);
        return receipt.ResponseJson;
    }
    private static void Receipt(ProcurementDbContext db, ProcurementRequestScope scope, InvoiceUnitReviewSource source,
        InvoiceUnitReviewRequest request, InvoiceUnitReviewResponse response, Guid? worker)
    {
        var json = InvoiceUnitReviewCommands.Serialize(request);
        db.AddScoped(new InvoiceUnitReviewCommandReceipt { ReviewSourceId = source.Id, Operation = request.Operation, CommandKey = request.CommandKey,
            ActorPrincipalId = scope.SubjectId, ActorWorkerId = worker, Fingerprint = ProcurementEvidenceEncoding.PolicyJsonDigest(json), RequestJson = json,
            ResponseJson = InvoiceUnitReviewCommands.Serialize(response), CompletedAt = response.CompletedAt });
    }
}
