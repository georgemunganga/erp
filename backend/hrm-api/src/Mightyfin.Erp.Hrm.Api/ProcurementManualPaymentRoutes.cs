using System.Data.Common;
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

/// <summary>Unregistered, read-only manual payment evidence. Finance remains the payment owner.
/// Composition must wait for P27/P28 governance readiness and explicit role grants.</summary>
public static partial class ProcurementManualPaymentRoutes
{
    private sealed class ReadProblem(string code, int status) : Exception(code)
    { internal int Status => status; }

    public static void MapReadRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/manual-payments");
        group.MapGet("", List).RequireAuthorization("procurement-payment-read");
        group.MapGet("/lookups/assignees", AssigneeLookup).RequireAuthorization("procurement-payment-read");
        group.MapGet("/lookups/invoices", InvoiceLookup).RequireAuthorization("procurement-payment-read");
        group.MapGet("/lookups/branches", BranchLookup).RequireAuthorization("procurement-payment-read");
        group.MapGet("/commands/{key:guid}", ReceiptByKey).RequireAuthorization("procurement-payment-read");
        group.MapGet("/{id:guid}", Detail).RequireAuthorization("procurement-payment-read");
        group.MapGet("/{id:guid}/commands/{key:guid}", Receipt).RequireAuthorization("procurement-payment-read");
    }

    private static async Task<IResult> Run(HttpContext http, ProcurementRequestScope scope,
        ProcurementApprovalTransaction transaction, IAuthorizationService authorization,
        Func<ProcurementDbContext, ApprovalDbContext, HrmDbContext, ProcurementPrincipalPeople.Person,
            Guid[], CancellationToken, Task<object>> read, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        try
        {
            var readiness = http.RequestServices.GetService<IProcurementManualPaymentGovernanceReadiness>();
            var options = http.RequestServices.GetService<ProcurementManualPaymentOptions>();
            if (options is not { Enabled: true, ExecutionDatabaseConfigured: true }
                || readiness is null || !await readiness.IsReadyAsync(ct))
                throw new ReadProblem("ManualPaymentGovernanceUnavailable", 503);
            var result = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                var person = await Actor(core, scope, http, authorization, "procurement-payment-read", token);
                var branches = await ProcurementExecutionRecordScope.Branches(core, scope, null, false, token);
                return await read(db, cases, core, person, branches, token);
            }, ct);
            return Results.Ok(result);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is ReadProblem or UnauthorizedAccessException or KeyNotFoundException
            or InvalidOperationException or ArgumentException or JsonException or FormatException or OverflowException
            or DbException or DbUpdateException)
        {
            var status = error is ReadProblem problem ? problem.Status
                : error is UnauthorizedAccessException ? 403 : error is KeyNotFoundException ? 404
                : error is ArgumentException ? 400 : ProcurementPostgresFailures.IsConcurrency(error) ? 409 : 503;
            return Results.Json(new { code = error is ReadProblem ? error.Message : "ManualPaymentReadUnavailable",
                message = error.Message switch {
                    "ManualPaymentAssigneeLookupTooLarge" => "Enter a name to narrow the list of people.",
                    "ManualPaymentAssigneeCursorChanged" => "The list of people changed. Search again.",
                    "ManualPaymentGovernanceUnavailable" => "Manual payments are not ready in this workspace yet.",
                    _ => status == 404 ? "Payment request not found." : "Payment information is unavailable. Check your access and try again."
                } }, statusCode: status);
        }
    }

    private static async Task<ProcurementPrincipalPeople.Person> Actor(HrmDbContext core, ProcurementRequestScope scope,
        HttpContext http, IAuthorizationService authorization, string action, CancellationToken ct)
    {
        var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct);
        if (person is null || person.PrincipalId != scope.SubjectId
            || scope.PrincipalId?.ToString("D") != person.PrincipalId
            || !person.Permissions.Contains(action)
            || !(await authorization.AuthorizeAsync(http.User, null, action)).Succeeded)
            throw new UnauthorizedAccessException("ManualPaymentCurrentActionRequired");
        return person;
    }

    private static IQueryable<ManualPaymentRequest> Visible(ProcurementDbContext db, ProcurementRequestScope scope,
        ProcurementPrincipalPeople.Person person, Guid[] branches)
    {
        if (scope.OrgUnitId is not null || scope.IsConfined && scope.WorkLocationId is null)
            throw new UnauthorizedAccessException("ManualPaymentCurrentScopeRequired");
        var query = db.ManualPaymentRequests.AsNoTracking();
        if (scope.WorkLocationId is { } selected) query = query.Where(x => x.BranchId == selected);
        // Core account grants are applied before count/search/page. An account with no
        // branch assignments has the existing organisation-wide Core read scope.
        if (!person.OrganisationWide)
        {
            if (branches.Length == 0) throw new UnauthorizedAccessException("ManualPaymentCurrentScopeRequired");
            var allowed = person.AllowedBranches.Intersect(branches).ToArray();
            if (allowed.Length == 0) throw new UnauthorizedAccessException("ManualPaymentCurrentScopeRequired");
            query = query.Where(x => allowed.Contains(x.BranchId));
        }
        return query;
    }

    private static async Task<IResult> List(string? q, string? status, Guid? invoiceId, Guid? supplierId, int? page, int? pageSize,
        HttpContext http, ProcurementRequestScope scope, ProcurementApprovalTransaction transaction,
        IAuthorizationService authorization, CancellationToken ct)
        => await Run(http, scope, transaction, authorization, async (db, cases, core, person, branches, token) =>
        {
            var number = page ?? 1; var size = pageSize ?? 25; var search = q?.Trim() ?? "";
            if (number is < 1 or > 100000 || size is < 1 or > 100 || search.Length > 100 || search.Any(char.IsControl)
                || status is { Length: > 40 } || status is not null && status.Any(char.IsControl)
                || invoiceId == Guid.Empty || supplierId == Guid.Empty)
                throw new ArgumentException("ManualPaymentQueryInvalid");
            var query = Visible(db, scope, person, branches);
            if (status is not null) query = query.Where(x => x.Status == status);
            if (invoiceId is { } invoice) query = query.Where(x => x.SupplierInvoiceId == invoice);
            if (supplierId is { } supplier) query = query.Where(x => x.SupplierId == supplier);
            if (search.Length > 0)
            {
                var pattern = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
                query = query.Where(x => EF.Functions.ILike(x.Title ?? "", pattern, "\\")
                    || EF.Functions.ILike(x.Purpose ?? "", pattern, "\\"));
            }
            var total = await query.CountAsync(token);
            var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((number - 1) * size).Take(size).ToArrayAsync(token);
            var labels = await PaymentInvoiceLabels(db, scope, rows, token);
            return new { items = rows.Select(x => Summary(x, labels[x.SupplierInvoiceId])).ToArray(), total, page = number, pageSize = size };
        }, ct);

    private static async Task<IResult> Detail(Guid id, HttpContext http, ProcurementRequestScope scope,
        ProcurementApprovalTransaction transaction, IAuthorizationService authorization, CancellationToken ct)
        => await Run(http, scope, transaction, authorization, async (db, cases, core, person, branches, token) =>
        {
            var request = await Visible(db, scope, person, branches).SingleOrDefaultAsync(x => x.Id == id, token)
                ?? throw new KeyNotFoundException();
            var invoiceLabels = await PaymentInvoiceLabels(db, scope, [request], token);
            var invoiceLabel = invoiceLabels[request.SupplierInvoiceId];
            var decisions = await db.ManualPaymentDecisions.AsNoTracking()
                .Where(x => x.ManualPaymentRequestId == id && x.BranchId == request.BranchId)
                .OrderBy(x => x.ToVersion).ToArrayAsync(token);
            var links = await db.ManualPaymentWorkflowCases.AsNoTracking()
                .Where(x => x.ManualPaymentRequestId == id && x.BranchId == request.BranchId)
                .OrderBy(x => x.OpenedRequestVersion).ToArrayAsync(token);
            var linkIds = links.Select(x => x.Id).ToArray();
            var caseIds = links.Select(x => x.CoreApprovalCaseId).ToArray();
            var workflows = await cases.Cases.AsNoTracking()
                .Where(x => caseIds.Contains(x.Id) && x.TenantId == scope.TenantId
                    && x.LegalEntityId == scope.LegalEntityId && x.SourceModule == "procurement"
                    && x.SourceId == id && (x.SourceType == "manual_payment_request" || x.SourceType == "manual_payment_confirmation"))
                .ToArrayAsync(token);
            if (workflows.Length != links.Length) throw new ReadProblem("ManualPaymentCoreHistoryUnavailable", 503);
            var workflowById = workflows.ToDictionary(x => x.Id);
            if (links.Any(x => x.Purpose is not ("Approval" or "Confirmation")
                || !workflowById.TryGetValue(x.CoreApprovalCaseId, out var workflow)
                || workflow.SourceVersion != x.OpenedRequestVersion || workflow.PolicyVersionId != x.PolicyVersionId
                || workflow.PolicyVersionNumber != x.PolicyVersionNumber || workflow.MakerSubjectId != x.MakerPrincipalId
                || workflow.SourceType != (x.Purpose == "Approval" ? "manual_payment_request" : "manual_payment_confirmation")))
                throw new ReadProblem("ManualPaymentCoreHistoryUnavailable", 503);
            var linkedDecisions = await db.ManualPaymentCoreDecisions.AsNoTracking()
                .Where(x => x.ManualPaymentRequestId == id && x.BranchId == request.BranchId
                    && linkIds.Contains(x.WorkflowCaseLinkId))
                .OrderBy(x => x.ToRequestVersion).ToArrayAsync(token);
            var coreDecisionIds = linkedDecisions.Select(x => x.CoreApprovalDecisionId).ToArray();
            var coreDecisions = await cases.Decisions.AsNoTracking()
                .Where(x => coreDecisionIds.Contains(x.Id) && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId)
                .ToArrayAsync(token);
            if (coreDecisions.Length != linkedDecisions.Length) throw new ReadProblem("ManualPaymentCoreHistoryUnavailable", 503);
            var coreDecisionById = coreDecisions.ToDictionary(x => x.Id);
            if (linkedDecisions.Any(x => x.ManualAction is not ("Approve" or "Confirm" or "Reject" or "Dispute" or "Return" or "Cancel")
                || !coreDecisionById.TryGetValue(x.CoreApprovalDecisionId, out var coreDecision)
                || !workflowById.ContainsKey(links.Single(y => y.Id == x.WorkflowCaseLinkId).CoreApprovalCaseId)
                || coreDecision.CaseId != links.Single(y => y.Id == x.WorkflowCaseLinkId).CoreApprovalCaseId
                || coreDecision.ActorSubjectId != x.ActorPrincipalId
                || coreDecision.Action != (x.ManualAction is "Approve" or "Confirm" ? "Approve"
                    : x.ManualAction is "Reject" or "Dispute" ? "Reject" : x.ManualAction == "Return" ? "Return" : "Cancel")))
                throw new ReadProblem("ManualPaymentCoreHistoryUnavailable", 503);
            var people = await DetailPeople(core, scope, request, decisions, links, linkedDecisions, token);
            // This is a display hint from the current Core company setting. Record and
            // Confirm obtain a fresh independent observation in their command transaction.
            var observedClock = await new CoreCompanyBusinessDateProvider(core,
                http.RequestServices.GetRequiredService<TimeProvider>())
                .GetAsync(scope.TenantId, scope.LegalEntityId, token);
            return new
            {
                request = new { request.Id, request.Version, request.Status, request.Title, request.Purpose,
                    request.BranchId, request.SupplierInvoiceId, request.SupplierId, request.CurrencyCode,
                    invoiceLabel.InvoiceNumber, invoiceLabel.SupplierDisplayName,
                    amountExact = ProcurementEvidenceEncoding.Decimal(request.Amount),
                    approvedAmountExact = Exact(request.ApprovedAmount), recordedAmountExact = Exact(request.RecordedAmount),
                    request.CreatorPrincipalId, request.ApproverPrincipalId, request.RecorderPrincipalId,
                    request.ConfirmerPrincipalId, request.ApprovedByPrincipalId, request.ApprovedAt,
                    request.PaidOn, request.ExternalMethod, request.ExternalReference,
                    request.RecordedByPrincipalId, request.RecordedAt, request.ConfirmedByPrincipalId,
                    request.ConfirmedAt, request.EvidenceDocumentRefId, request.ClosureReason,
                    request.CreatedAt, request.CreatedBy, request.UpdatedAt, request.UpdatedBy },
                transitions = decisions.Select(x => new { x.Id, x.Action, x.FromStatus, x.ToStatus,
                    x.FromVersion, x.ToVersion, x.ActorPrincipalId, x.OccurredAt, x.Reason,
                    previousAmountExact = Exact(x.PreviousAmount), newAmountExact = Exact(x.NewAmount),
                    recordedAmountExact = Exact(x.RecordedAmount), x.PaidOn, x.ExternalMethod, x.ExternalReference,
                    x.EvidenceDocumentRefId }).ToArray(),
                coreCases = links.Select(x => new { x.Id, x.Purpose, x.OpenedRequestVersion,
                    x.CoreApprovalCaseId, x.CoreApprovalStageId, x.CoreApprovalAssignmentId,
                    x.AssigneePrincipalId, x.PolicyVersionId, x.PolicyVersionNumber,
                    x.OpenedAt, state = workflowById[x.CoreApprovalCaseId].State,
                    version = workflowById[x.CoreApprovalCaseId].Version }).ToArray(),
                coreDecisions = linkedDecisions.Select(x => new { x.Id, x.ManualPaymentDecisionId,
                    x.CoreApprovalDecisionId, x.ManualAction, x.ActorPrincipalId, x.DecidedAt,
                    coreAction = coreDecisionById[x.CoreApprovalDecisionId].Action }).ToArray(),
                people,
                companyClock = new { date = observedClock.Date, timeZoneId = observedClock.TimeZoneId,
                    observedAtUtc = observedClock.ObservedAtUtc },
                sourceInvoice = new { invoiceId = invoiceLabel.InvoiceId, invoiceLabel.InvoiceNumber,
                    invoiceLabel.SupplierId, displayName = invoiceLabel.SupplierDisplayName,
                    invoiceLabel.CurrencyCode, grossExact = ProcurementEvidenceEncoding.Decimal(invoiceLabel.GrossAmount),
                    invoiceLabel.InvoiceDate, branchId = request.BranchId }
            };
        }, ct);

    private static Task<IResult> Receipt(Guid id, Guid key, HttpContext http, ProcurementRequestScope scope,
        ProcurementApprovalTransaction transaction, IAuthorizationService authorization, CancellationToken ct)
        => Recover(id, key, http, scope, transaction, authorization, ct);

    private static Task<IResult> ReceiptByKey(Guid key, HttpContext http, ProcurementRequestScope scope,
        ProcurementApprovalTransaction transaction, IAuthorizationService authorization, CancellationToken ct)
        => Recover(null, key, http, scope, transaction, authorization, ct);

    private static async Task<IResult> Recover(Guid? id, Guid key, HttpContext http, ProcurementRequestScope scope,
        ProcurementApprovalTransaction transaction, IAuthorizationService authorization, CancellationToken ct)
        => await Run(http, scope, transaction, authorization, async (db, cases, core, person, branches, token) =>
        {
            if (key == Guid.Empty) throw new ArgumentException("ManualPaymentCommandKeyInvalid");
            var visible = Visible(db, scope, person, branches);
            var found = await (from candidateReceipt in db.ManualPaymentReceipts.AsNoTracking()
                join candidateRequest in visible on candidateReceipt.ManualPaymentRequestId equals candidateRequest.Id
                where candidateReceipt.CommandKey == key && candidateReceipt.ActorPrincipalId == scope.SubjectId
                    && candidateReceipt.BranchId == candidateRequest.BranchId && (id == null || candidateRequest.Id == id)
                select new { receipt = candidateReceipt, request = candidateRequest }).SingleOrDefaultAsync(token) ?? throw new KeyNotFoundException();
            var receipt = found.receipt;
            var request = found.request;
            var action = CommandAction(receipt.Action);
            await Actor(core, scope, http, authorization, action, token);
            var decision = await db.ManualPaymentDecisions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.ManualPaymentRequestId == request.Id && x.BranchId == request.BranchId
                && x.ToVersion == receipt.ResultVersion && x.Action == receipt.Action
                && x.ActorPrincipalId == scope.SubjectId, token)
                ?? throw new ReadProblem("ManualPaymentReceiptEvidenceUnavailable", 503);
            using var document = JsonDocument.Parse(receipt.ResponseJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("id", out var responseId)
                || responseId.GetGuid() != request.Id || !root.TryGetProperty("version", out var responseVersion)
                || responseVersion.GetInt64() != receipt.ResultVersion
                || !root.TryGetProperty("status", out var responseStatus)
                || responseStatus.GetString() != decision.ToStatus)
                throw new ReadProblem("ManualPaymentReceiptEvidenceUnavailable", 503);
            // Deliberately project only the recorded result. Never replay request JSON,
            // a body hash, bank details, or a later current request state.
            return new { id = request.Id, version = receipt.ResultVersion, status = decision.ToStatus,
                action = receipt.Action, commandKey = key };
        }, ct);

    private static object Summary(ManualPaymentRequest x, PaymentInvoiceLabel label) => new
    {
        x.Id, x.Version, x.Status, x.Title, x.BranchId, x.SupplierInvoiceId,
        x.SupplierId, label.InvoiceNumber, label.SupplierDisplayName,
        x.CurrencyCode, amountExact = ProcurementEvidenceEncoding.Decimal(x.Amount),
        x.CreatorPrincipalId, x.ApproverPrincipalId, x.RecorderPrincipalId, x.ConfirmerPrincipalId,
        x.CreatedAt, x.UpdatedAt
    };

    private static string? Exact(decimal? value) => value is { } amount ? ProcurementEvidenceEncoding.Decimal(amount) : null;

    private static string CommandAction(string action) => action switch
    {
        "Create" => "procurement-payment-create",
        "Revise" => "procurement-payment-edit",
        "Submit" => "procurement-payment-submit",
        "Approve" or "Return" or "Reject" => "procurement-payment-approve",
        "Record" => "procurement-payment-record",
        "Confirm" or "Dispute" => "procurement-payment-confirm",
        "Cancel" => "procurement-payment-cancel",
        _ => throw new ReadProblem("ManualPaymentReceiptActionInvalid", 503)
    };
}
