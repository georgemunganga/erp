using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementOrderExecutionRoutes
{
    private static async Task<IResult> ApprovedSources(HttpContext http, ProcurementRequestScope scope,
        ProcurementOrderExecutionOptions options, IProcurementOrderSchemaReadiness readiness, ProcurementApprovalTransaction transaction,
        IAuthorizationService auth, int? page, int? pageSize, CancellationToken ct)
    {
        try
        {
            await EnabledAsync(options, readiness, ct); var number = Math.Clamp(page ?? 1, 1, 100000); var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var response = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                await Actor(core, scope, http, auth, "procurement-po-convert", token);
                var allowedBranches = await CurrentRecordScope(core, scope, null, false, token);
                var query = from line in db.PurchaseRequestLines.AsNoTracking()
                    join request in db.PurchaseRequests.AsNoTracking() on line.PurchaseRequestId equals request.Id
                    where request.ApprovalState == "Approved" && line.ApprovalStatus == "Approved"
                        && (scope.WorkLocationId == null || request.BranchRef == scope.WorkLocationId)
                        && (allowedBranches.Length == 0 || request.BranchRef != null && allowedBranches.Contains(request.BranchRef.Value))
                        && (line.Route == "Unassigned" || line.Route == "DirectPurchase")
                    select new { RequestId = request.Id, LineId = line.Id, request.Number, request.CreatedAt, line.LineNumber };
                var total = await query.CountAsync(token);
                var candidates = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.RequestId).ThenBy(x => x.LineNumber)
                    .Skip((number - 1) * size).Take(size).ToArrayAsync(token);
                var rows = new List<object>();
                foreach (var candidate in candidates)
                {
                    try
                    {
                        var source = await Source(db, cases, scope, candidate.RequestId, candidate.LineId, token);
                        if (source.Submission.FinanceIdentitySchemaVersion != 1)
                            throw new OrderProblem("OrderSourceFinanceCheckerNeedsReview", 409);
                        await CatalogClassification(db, [source.Line], token);
                        var availability = PurchaseOrderSourceAllocation.Check(source, scope.TenantId, scope.LegalEntityId, DateTimeOffset.UtcNow);
                        rows.Add(new { requestId = source.Request.Id, requestNumber = source.Request.Number,
                            lineId = source.Line.Id, requestVersion = source.Request.Version, source.Line.Description,
                            source.Line.UnitCode, source.Line.CategoryCode, source.Line.PurchaseType, source.Request.BranchRef,
                            availability.AvailableQuantity, availability.ApprovedUnitPrice,
                            availableQuantityExact = ProcurementEvidenceEncoding.Decimal(availability.AvailableQuantity),
                            approvedUnitPriceExact = ProcurementEvidenceEncoding.Decimal(availability.ApprovedUnitPrice), availability.CurrencyCode,
                            eligibility = availability.AvailableQuantity > 0 ? "Available" : "FullyAllocated", blocker = (string?)null });
                    }
                    catch (Exception error) when (error is InvalidOperationException or OrderProblem)
                    {
                        // A buyer sees why an otherwise approved source cannot be converted;
                        // an incomplete authority record never becomes an eligible source.
                        rows.Add(new { requestId = candidate.RequestId, requestNumber = candidate.Number, lineId = candidate.LineId,
                            eligibility = "NeedsReview", blocker = error.Message.StartsWith("Order", StringComparison.Ordinal)
                                ? error.Message : "OrderSourceEvidenceUnavailable" });
                    }
                }
                return new { items = rows, total, page = number, pageSize = size };
            }, ct);
            return Results.Ok(response);
        }
        catch (Exception error) when (Handled(error)) { return Failure(error); }
    }

    private static async Task<IResult> AssignedOrders(HttpContext http, ProcurementRequestScope scope,
        ProcurementOrderExecutionOptions options, IProcurementOrderSchemaReadiness readiness, ProcurementApprovalTransaction transaction,
        IAuthorizationService auth, int? page, int? pageSize, CancellationToken ct)
    {
        try
        {
            await EnabledAsync(options, readiness, ct); var number = Math.Clamp(page ?? 1, 1, 100000); var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var response = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                await Actor(core, scope, http, auth, "procurement-po-approve", token);
                var allowedBranches = await CurrentRecordScope(core, scope, null, false, token);
                var visibleAssignments = await (from pin in db.OrderAssignmentAuthority.AsNoTracking()
                    join submission in db.OrderSubmissions.AsNoTracking() on pin.SubmissionId equals submission.Id
                    join order in db.PurchaseOrders.AsNoTracking() on submission.PurchaseOrderId equals order.Id
                    where pin.PrincipalId == scope.SubjectId && order.Status == "PendingApproval"
                        && (scope.WorkLocationId == null || order.BranchRef == scope.WorkLocationId)
                        && (allowedBranches.Length == 0 || order.BranchRef != null && allowedBranches.Contains(order.BranchRef.Value))
                    select pin.CoreAssignmentId).ToArrayAsync(token);
                var query = from assignment in cases.Assignments.AsNoTracking()
                    join workflow in cases.Cases.AsNoTracking() on assignment.CaseId equals workflow.Id
                    join stage in cases.Stages.AsNoTracking() on assignment.StageId equals stage.Id
                    where visibleAssignments.Contains(assignment.Id) && assignment.AssigneeSubjectId == scope.SubjectId && assignment.PermissionKey == "procurement-po-approve"
                        && assignment.State == "Pending" && stage.State == "Active" && workflow.State == "Pending"
                        && workflow.SourceModule == "procurement" && workflow.SourceType == "purchase_order"
                    orderby workflow.OpenedAt, assignment.Id
                    select new { assignmentId = assignment.Id, caseId = workflow.Id, orderId = workflow.SourceId,
                        assignmentVersion = assignment.Version, caseVersion = workflow.Version, stage.Code, workflow.OpenedAt };
                var total = await query.CountAsync(token);
                var tasks = await query.Skip((number - 1) * size).Take(size).ToArrayAsync(token);
                var ids = tasks.Select(x => x.orderId).ToArray();
                var orders = await db.PurchaseOrders.AsNoTracking().Where(x => ids.Contains(x.Id)
                    && (scope.WorkLocationId == null || x.BranchRef == scope.WorkLocationId)).ToDictionaryAsync(x => x.Id, token);
                var rows = tasks.Where(x => orders.ContainsKey(x.orderId)).Select(x => new { x.assignmentId, x.caseId, x.orderId,
                    x.assignmentVersion, x.caseVersion, x.Code, x.OpenedAt, orders[x.orderId].Number,
                    orderVersion = orders[x.orderId].Version, orders[x.orderId].Status, orders[x.orderId].CurrencyCode });
                // Counts describe the current user's Core assignments; inaccessible source
                // records are omitted and their content is never returned.
                return new { items = rows, total, page = number, pageSize = size };
            }, ct);
            return Results.Ok(response);
        }
        catch (Exception error) when (Handled(error)) { return Failure(error); }
    }

    private static async Task<IResult> OrderCommercialHistory(Guid id, HttpContext http, ProcurementRequestScope scope,
        ProcurementOrderExecutionOptions options, IProcurementOrderSchemaReadiness readiness, ProcurementApprovalTransaction transaction,
        IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            await EnabledAsync(options, readiness, ct);
            var response = await transaction.ExecuteAsync(async (db, cases, token) =>
            {
                var order = await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token)
                    ?? throw new OrderProblem("OrderNotFound", 404);
                if (order.PrincipalDraftSchemaVersion == 2) Missing();
                Scope(order, scope, false);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                await Actor(core, scope, http, auth, "procurement-po-read", token, order.BranchRef, true);
                var workflows = await cases.Cases.AsNoTracking().Include(x => x.Stages).ThenInclude(x => x.Assignments)
                    .Where(x => x.SourceModule == "procurement" && x.SourceType == "purchase_order" && x.SourceId == id)
                    .OrderBy(x => x.SourceVersion).ToArrayAsync(token);
                if (order.BuyerSubjectId != scope.SubjectId && !workflows.Any(x => x.Stages.SelectMany(s => s.Assignments)
                    .Any(a => a.AssigneeSubjectId == scope.SubjectId))) Missing();
                var caseIds = workflows.Select(x => x.Id).ToArray();
                var decisions = await cases.Decisions.AsNoTracking().Where(x => caseIds.Contains(x.CaseId))
                    .OrderBy(x => x.DecidedAt).Select(x => new { x.Id, x.CaseId, x.AssignmentId, x.Action, x.Reason,
                        x.ActorSubjectId, x.DecidedAt }).ToArrayAsync(token);
                var lines = await db.PurchaseOrderLines.AsNoTracking().Where(x => x.PurchaseOrderId == id && x.IsActive)
                    .OrderBy(x => x.LineNumber).ToArrayAsync(token);
                var issued = await db.OrderIssuedVersions.AsNoTracking().Where(x => x.PurchaseOrderId == id)
                    .OrderBy(x => x.RevisionNumber).Select(x => new { x.Id, x.RevisionNumber, x.AuthorizedAt,
                        x.CommercialSnapshotSha256 }).ToArrayAsync(token);
                return new { order = ProcurementOrderDraftRoutes.Detail(order, lines, null),
                    cases = workflows.Select(x => new { x.Id, x.SourceVersion, x.State, x.Version,
                        stages = x.Stages.Select(s => new { s.Id, s.Code, s.Sequence, s.State,
                            assignments = s.Assignments.Select(a => new { a.Id, a.AssigneeSubjectId, a.State, a.Version }) }) }),
                    decisions, issued };
            }, ct);
            return Results.Ok(response);
        }
        catch (Exception error) when (Handled(error)) { return Failure(error); }
    }
}
