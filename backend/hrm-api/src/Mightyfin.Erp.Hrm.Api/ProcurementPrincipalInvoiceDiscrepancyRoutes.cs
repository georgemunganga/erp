using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Held typed P31 discrepancy worklist and corrective notes. A note or
/// reassignment cannot create a claim, approve an invoice or post to Finance.</summary>
public static class ProcurementPrincipalInvoiceDiscrepancyRoutes
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public sealed record ActionInput(long ExpectedVersion, string Action,
        string Reason, string? EvidenceReference, string? NewOwnerPrincipalId);

    public static void MapRoutes(WebApplication app)
    {
        var root = app.MapGroup("/api/procurement/v1/invoices/{id:guid}/principal-matching/exceptions")
            .RequireAuthorization("procurement-invoice-read", "procurement-invoice-match");
        root.MapGet("", List);
        root.MapPost("/{issueId:guid}/actions", Action);
        root.MapGet("/commands/{key:guid}", Recover);
    }

    private static bool Available(HttpContext http) =>
        http.RequestServices.GetService<ProcurementPrincipalInvoiceMatchingOptions>() is { Enabled: true }
        && http.RequestServices.GetService<IProcurementPrincipalInvoiceSchemaReadiness>() is not null;

    private static async Task<IResult> List(Guid id, int? page, int? pageSize,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (!Available(http) || !await http.RequestServices
                .GetRequiredService<IProcurementPrincipalInvoiceSchemaReadiness>().IsReadReadyAsync(ct))
            return Results.Json(new { code = "PrincipalInvoiceExceptionsUnavailable" }, statusCode: 503);
        if (page is < 1 or > 100000 || pageSize is < 1 or > 50)
            return Results.Json(new { code = "PrincipalInvoiceExceptionsPageInvalid" }, statusCode: 422);
        return await ProcurementPrincipalInvoiceSourceRoutes.Run(http, scope,
            async (db, unusedCases, core, auth, token) =>
            {
                var ready = http.RequestServices.GetRequiredService<IProcurementPrincipalInvoiceSchemaReadiness>();
                if (!await ready.IsReadReadyAsync(db, token))
                    throw new InvalidOperationException("PrincipalInvoiceExceptionsUnavailable");
                var (invoice, _) = await ProcurementPrincipalInvoiceSourceRoutes.Current(
                    db, core, scope, http, auth, id, token);
                var query = db.PrincipalInvoiceDiscrepancies.AsNoTracking()
                    .Where(x => x.SupplierInvoiceId == id && x.BranchId == invoice.BranchRef);
                var total = await query.CountAsync(token);
                var requested = page ?? 1; var size = pageSize ?? 20;
                var rows = await query.OrderByDescending(x => x.OpenedAt).ThenBy(x => x.Id)
                    .Skip((requested - 1) * size).Take(size).ToArrayAsync(token);
                var issueIds = rows.Select(x => x.Id).ToArray();
                var actions = await db.PrincipalInvoiceDiscrepancyActions.AsNoTracking()
                    .Where(x => issueIds.Contains(x.DiscrepancyId))
                    .OrderBy(x => x.At).ThenBy(x => x.Id).ToArrayAsync(token);
                return new
                {
                    invoiceId = id, invoiceVersion = invoice.Version, page = requested,
                    pageSize = size, total, sourceSchemaVersion = 2,
                    items = rows.Select(x => new
                    {
                        issueId = x.Id, x.SupplierInvoiceLineId, x.MappingVersionId,
                        x.Code, x.State, x.OwnerPrincipalId, x.Version,
                        x.OpenedAt, x.DueAt, x.ResolvedAt, x.ResolutionReference,
                        history = actions.Where(a => a.DiscrepancyId == x.Id)
                            .Select(a => new { actionId = a.Id, a.Action, a.ActorPrincipalId,
                                a.Reason, a.EvidenceReference, a.At }).ToArray()
                    }).ToArray(),
                    canPostToFinance = false
                };
            }, ct);
    }

    private static async Task<IResult> Action(Guid id, Guid issueId, ActionInput? input,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (!Available(http) || !await http.RequestServices
                .GetRequiredService<IProcurementPrincipalInvoiceSchemaReadiness>().IsReadyAsync(ct))
            return Results.Json(new { code = "PrincipalInvoiceExceptionsUnavailable" }, statusCode: 503);
        if (input is null || input.ExpectedVersion < 1 || input.Reason is not { Length: > 0 and <= 2000 }
            || string.IsNullOrWhiteSpace(input.Reason) || input.EvidenceReference?.Length > 200
            || input.Action is not ("Assign" or "Comment" or "CorrectionRequested" or "ReadyToRetry")
            || input.Action == "Assign" && (!Guid.TryParseExact(input.NewOwnerPrincipalId, "D", out var owner)
                || owner == Guid.Empty)
            || input.Action != "Assign" && input.NewOwnerPrincipalId is not null
            || !Guid.TryParseExact(http.Request.Headers["Idempotency-Key"], "D", out var key)
            || key == Guid.Empty)
            return Results.Json(new { code = "PrincipalInvoiceExceptionActionInvalid" }, statusCode: 422);
        var requestSha = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(
            new { schemaVersion = 2, invoiceId = id, issueId, input }, Json));
        return await ProcurementPrincipalInvoiceSourceRoutes.Run(http, scope,
            async (db, unusedCases, core, auth, token) =>
            {
                var ready = http.RequestServices.GetRequiredService<IProcurementPrincipalInvoiceSchemaReadiness>();
                if (!await ready.IsReadyAsync(db, token))
                    throw new InvalidOperationException("PrincipalInvoiceExceptionsUnavailable");
                var (invoice, _) = await ProcurementPrincipalInvoiceSourceRoutes.Current(
                    db, core, scope, http, auth, id, token);
                var receipt = await db.PrincipalInvoiceCommandReceipts.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.SupplierInvoiceId == id
                        && x.Operation == "IssueAction" && x.CommandKey == key, token);
                if (receipt is not null)
                {
                    if (receipt.ActorPrincipalId != scope.SubjectId
                        || receipt.ActorWorkerAliasId != scope.CoreLinkedWorkerId
                        || receipt.BranchId != invoice.BranchRef
                        || receipt.RequestSha256 != requestSha
                        || receipt.ResponseSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(receipt.ResponseJson))
                        throw new InvalidOperationException("PrincipalInvoiceExceptionCommandConflict");
                    return JsonSerializer.Deserialize<JsonElement>(receipt.ResponseJson, Json);
                }
                var state = await db.PrincipalInvoiceStates.SingleAsync(x =>
                    x.SupplierInvoiceId == id && x.State == "ComparisonBlocked", token);
                var issue = await db.PrincipalInvoiceDiscrepancies.SingleOrDefaultAsync(x =>
                    x.Id == issueId && x.SupplierInvoiceId == id
                        && x.BranchId == invoice.BranchRef && x.MappingVersionId == state.CurrentMappingId, token)
                    ?? throw new KeyNotFoundException();
                if (issue.Version != input.ExpectedVersion
                    || issue.State is "Resolved" or "CancelledWithReason")
                    throw new InvalidOperationException("PrincipalInvoiceExceptionChanged");
                // Reassignment is an action on the currently assigned issue, too.
                // A general matcher may inspect the invoice but cannot take over
                // another owner's correction queue.
                if (issue.OwnerPrincipalId != scope.SubjectId)
                    throw new UnauthorizedAccessException();
                var target = issue.OwnerPrincipalId;
                if (input.Action == "Assign")
                {
                    var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                        scope.LegalEntityId, input.NewOwnerPrincipalId!, token)
                        ?? throw new InvalidOperationException("PrincipalInvoiceExceptionOwnerUnavailable");
                    if (!person.Permissions.Contains("procurement-invoice-match")
                        || !person.OrganisationWide && !person.AllowedBranches.Contains(issue.BranchId)
                        || person.PrincipalId == issue.OwnerPrincipalId)
                        throw new InvalidOperationException("PrincipalInvoiceExceptionOwnerUnavailable");
                    target = person.PrincipalId;
                    issue.OwnerPrincipalId = person.PrincipalId;
                    issue.OwnerWorkerAliasId = person.CoreLinkedWorkerId;
                    issue.State = "InReview";
                }
                else
                {
                    if (input.Action == "CorrectionRequested")
                    {
                        if (issue.State == "AwaitingCorrection")
                            throw new InvalidOperationException("PrincipalInvoiceCorrectionAlreadyRequested");
                        issue.State = "AwaitingCorrection";
                    }
                    if (input.Action == "ReadyToRetry")
                    {
                        if (issue.State == "ReadyToRetry")
                            throw new InvalidOperationException("PrincipalInvoiceIssueAlreadyReadyToRetry");
                        issue.State = "ReadyToRetry";
                    }
                }
                var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
                var action = new PrincipalInvoiceDiscrepancyAction
                {
                    TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                    DiscrepancyId = issue.Id, SupplierInvoiceId = id,
                    MappingVersionId = issue.MappingVersionId,
                    Action = input.Action == "Assign" ? "Assigned" : input.Action == "Comment"
                        ? "Commented" : input.Action, ActorPrincipalId = scope.SubjectId,
                    ActorWorkerAliasId = scope.CoreLinkedWorkerId,
                    Reason = input.Reason.Trim(), EvidenceReference = input.EvidenceReference,
                    CommandKey = key, At = now
                };
                db.AddScoped(action);
                var response = JsonSerializer.Serialize(new
                {
                    invoiceId = id, invoiceVersion = invoice.Version,
                    issueId = issue.Id, actionId = action.Id,
                    commandKey = key, ownerPrincipalId = target,
                    issueState = issue.State,
                    issueVersion = input.Action == "Comment" ? issue.Version : checked(issue.Version + 1),
                    sourceSchemaVersion = 2, canPostToFinance = false
                }, Json);
                db.AddScoped(new PrincipalInvoiceCommandReceipt
                {
                    TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                    SupplierInvoiceId = id, Operation = "IssueAction", CommandKey = key,
                    ActorPrincipalId = scope.SubjectId,
                    ActorWorkerAliasId = scope.CoreLinkedWorkerId,
                    BranchId = invoice.BranchRef!.Value, RequestSha256 = requestSha,
                    ResponseJson = response,
                    ResponseSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(response),
                    ResponseStatus = 200, CompletedAt = now
                });
                db.AddScoped(new AuditEvent
                {
                    RecordType = nameof(PrincipalInvoiceDiscrepancy), RecordId = issue.Id,
                    ActorSubjectId = scope.SubjectId, Action = "PrincipalInvoiceIssueAction",
                    ChangedFieldsJson = JsonSerializer.Serialize(new
                    {
                        invoiceId = id, issueId = issue.Id, action = input.Action,
                        commandKey = key, issueState = issue.State,
                        canPostToFinance = false
                    }, Json)
                });
                await db.SaveChangesAsync(token);
                return JsonSerializer.Deserialize<JsonElement>(response, Json);
            }, ct);
    }

    private static async Task<IResult> Recover(Guid id, Guid key, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (!Available(http) || !await http.RequestServices
                .GetRequiredService<IProcurementPrincipalInvoiceSchemaReadiness>().IsReadReadyAsync(ct))
            return Results.Json(new { code = "PrincipalInvoiceExceptionsUnavailable" }, statusCode: 503);
        return await ProcurementPrincipalInvoiceSourceRoutes.Run(http, scope,
            async (db, unusedCases, core, auth, token) =>
            {
                var (invoice, _) = await ProcurementPrincipalInvoiceSourceRoutes.Current(
                    db, core, scope, http, auth, id, token);
                var receipt = await db.PrincipalInvoiceCommandReceipts.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.SupplierInvoiceId == id
                        && x.Operation == "IssueAction" && x.CommandKey == key, token)
                    ?? throw new KeyNotFoundException();
                if (receipt.ActorPrincipalId != scope.SubjectId
                    || receipt.ActorWorkerAliasId != scope.CoreLinkedWorkerId
                    || receipt.BranchId != invoice.BranchRef
                    || receipt.ResponseSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(receipt.ResponseJson))
                    throw new KeyNotFoundException();
                using var parsed = JsonDocument.Parse(receipt.ResponseJson);
                var row = parsed.RootElement;
                var action = await db.PrincipalInvoiceDiscrepancyActions.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == row.GetProperty("actionId").GetGuid()
                        && x.SupplierInvoiceId == id && x.CommandKey == key
                        && x.ActorPrincipalId == scope.SubjectId, token)
                    ?? throw new InvalidOperationException("PrincipalInvoiceExceptionReceiptWithoutAction");
                if (row.GetProperty("invoiceId").GetGuid() != id
                    || row.GetProperty("issueId").GetGuid() != action.DiscrepancyId
                    || row.GetProperty("commandKey").GetGuid() != key
                    || row.GetProperty("sourceSchemaVersion").GetInt32() != 2
                    || row.GetProperty("canPostToFinance").GetBoolean())
                    throw new InvalidOperationException("PrincipalInvoiceExceptionReceiptChanged");
                return JsonSerializer.Deserialize<JsonElement>(receipt.ResponseJson, Json);
            }, ct);
    }
}
