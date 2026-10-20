using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementOrderExecutionRoutes
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record OrderLoad(PurchaseOrder Order, IReadOnlyList<PurchaseOrderLine> Lines,
        Supplier Supplier, IReadOnlyList<PurchaseOrderSourceLine> Sources);
    private sealed class OrderProblem(string code, int status) : Exception(code) { public int Status => status; }
    private static void Missing() => throw new OrderProblem("OrderNotFound", 404);
    private static void Deny() => throw new OrderProblem("OrderAccessDenied", 403);
    private static void Conflict(string code) => throw new OrderProblem(code, 409);
    private static void Enabled(ProcurementOrderExecutionOptions options)
    { if (!options.Enabled || !options.ExecutionDatabaseConfigured) throw new OrderProblem("OrderExecutionUnavailable", 503); }
    private static Guid Key(HttpContext http) => Guid.TryParse(http.Request.Headers["Idempotency-Key"], out var id) && id != Guid.Empty
        ? id : throw new OrderProblem("OrderCommandKeyRequired", 400);
    private static string Fingerprint(object command) => ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(command));
    private static async Task Actor(HrmDbContext core, ProcurementRequestScope scope, HttpContext http,
        IAuthorizationService auth, string permission, CancellationToken ct)
    { if (await ProcurementAuthorityIdentity.ActorAsync(core, scope, http.User, auth, permission, ct) is null) Deny(); }
    private static void Scope(PurchaseOrder order, ProcurementRequestScope scope, bool own)
    { if (scope.IsConfined && order.BranchRef != scope.WorkLocationId || own && order.BuyerSubjectId != scope.SubjectId) Missing(); }
    private static Task LockOrder(ProcurementDbContext db, ProcurementRequestScope scope, Guid id, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.purchase_orders WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE", ct);

    private static async Task<OrderLoad> Load(ProcurementDbContext db, ApprovalDbContext coreCases,
        ProcurementRequestScope scope, Guid id, bool own, CancellationToken ct)
    {
        var sourceIds = await db.PurchaseOrderLines.AsNoTracking().Where(x => x.PurchaseOrderId == id && x.IsActive
            && x.PurchaseRequestLineId != null).Select(x => x.PurchaseRequestLineId!.Value).ToArrayAsync(ct);
        var requestIds = await db.PurchaseRequestLines.AsNoTracking().Where(x => sourceIds.Contains(x.Id))
            .Select(x => x.PurchaseRequestId).Distinct().Order().ToArrayAsync(ct);
        foreach (var requestId in requestIds) await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, requestId, ct);
        await LockOrder(db, scope, id, ct);
        var order = await db.PurchaseOrders.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null) Missing();
        Scope(order!, scope, own);
        var lines = await db.PurchaseOrderLines.Where(x => x.PurchaseOrderId == id && x.IsActive).OrderBy(x => x.LineNumber).ToArrayAsync(ct);
        if (lines.Any(x => x.PurchaseRequestLineId is null)) Conflict("OrderApprovedSourcesRequired");
        var supplier = await db.Suppliers.Include(x => x.Qualifications).SingleOrDefaultAsync(x => x.Id == order!.SupplierId, ct)
            ?? throw new OrderProblem("OrderSupplierUnavailable", 409);
        var sources = new List<PurchaseOrderSourceLine>();
        foreach (var line in lines)
        {
            var requestId = await db.PurchaseRequestLines.Where(x => x.Id == line.PurchaseRequestLineId)
                .Select(x => x.PurchaseRequestId).SingleAsync(ct);
            var source = await Source(db, coreCases, scope, requestId, line.PurchaseRequestLineId!.Value, ct);
            var allocation = source.Allocations.SingleOrDefault(x => x.TargetType == "PurchaseOrder"
                && x.TargetId == id && x.Status != "Released") ?? throw new OrderProblem("OrderSourceAllocationMissing", 409);
            sources.Add(new(line, source, allocation));
        }
        return new(order!, lines, supplier, sources);
    }

    private static async Task<ApprovedPurchaseSource> Source(ProcurementDbContext db, ApprovalDbContext core,
        ProcurementRequestScope scope, Guid requestId, Guid lineId, CancellationToken ct)
    {
        var request = await db.PurchaseRequests.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw new OrderProblem("OrderSourceNotFound", 404);
        if (scope.IsConfined && request.BranchRef != scope.WorkLocationId) Missing();
        var line = request.Lines.SingleOrDefault(x => x.Id == lineId) ?? throw new OrderProblem("OrderSourceNotFound", 404);
        if (!Guid.TryParse(request.ApprovalInstanceRef, out var caseId)) Conflict("OrderSourceNotApproved");
        var pin = await db.ApprovalSubmissions.SingleOrDefaultAsync(x => x.CoreApprovalCaseId == caseId, ct)
            ?? throw new OrderProblem("OrderSourceApprovalEvidenceMissing", 409);
        var outcome = await db.ApprovalOutcomes.SingleOrDefaultAsync(x => x.SubmissionId == pin.Id && x.Outcome == "Approved", ct)
            ?? throw new OrderProblem("OrderSourceNotApproved", 409);
        var workflow = await core.Cases.SingleOrDefaultAsync(x => x.Id == caseId, ct)
            ?? throw new OrderProblem("OrderSourceCaseMissing", 409);
        var decision = await core.Decisions.SingleOrDefaultAsync(x => x.Id == outcome.CoreDecisionId, ct)
            ?? throw new OrderProblem("OrderSourceDecisionMissing", 409);
        var evaluation = await db.PolicyEvaluations.SingleAsync(x => x.Id == pin.PolicyEvaluationId, ct);
        var policy = await db.PolicyVersions.SingleAsync(x => x.Id == pin.PolicyVersionId, ct);
        var finance = await db.BudgetCheckSnapshots.SingleAsync(x => x.Id == pin.FinanceSnapshotId, ct);
        using var json = JsonDocument.Parse(evaluation.DecisionJson);
        var methodCode = json.RootElement.GetProperty("lines").EnumerateArray().Single(x => x.GetProperty("lineId").GetGuid() == lineId)
            .GetProperty("decision").GetProperty("sourcingMethodCode").GetString();
        var method = await db.ProcurementMethods.SingleOrDefaultAsync(x => x.Code == methodCode, ct)
            ?? throw new OrderProblem("OrderSourceMethodUnavailable", 409);
        var allocations = await db.PurchaseRequestAllocations.Where(x => x.PurchaseRequestLineId == lineId).ToListAsync(ct);
        return new(request, line, pin, outcome, workflow, decision, evaluation, policy, finance, method, allocations);
    }

    private static async Task<ProcurementCommercialCommandReceipt?> Receipt(ProcurementDbContext db,
        ProcurementRequestScope scope, string operation, Guid key, string fingerprint, Guid? id, CancellationToken ct)
    {
        var prior = await db.CommercialCommandReceipts.SingleOrDefaultAsync(x => x.Operation == operation && x.CommandKey == key, ct);
        if (prior is not null && (prior.ActorPrincipalId != scope.SubjectId || prior.Fingerprint != fingerprint
            || id is { } target && prior.PurchaseOrderId != target)) Conflict("OrderCommandKeyConflict");
        return prior;
    }
    private static async Task<string> SaveReply(ProcurementDbContext db, ProcurementRequestScope scope,
        PurchaseOrder order, string operation, Guid key, string fingerprint, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        var reply = JsonSerializer.Serialize(new OrderExecutionReply(order.Id, order.Number, order.Status,
            order.Version, order.CurrentRevision, order.ApprovalInstanceRef, Next(order.Status)), Json);
        db.AddScoped(new ProcurementCommercialCommandReceipt { PurchaseOrderId = order.Id, Operation = operation,
            CommandKey = key, ActorPrincipalId = scope.SubjectId, Fingerprint = fingerprint, ResponseJson = reply, CompletedAt = DateTimeOffset.UtcNow });
        return reply;
    }
    private static string Next(string state) => state switch
    { "Draft" => "Review the order and confirm funding before submitting.", "PendingApproval" => "Waiting for the assigned approvers.",
        "Approved" => "The order is approved and ready for issue review.", "IssuePending" => "Delivery is pending; this does not confirm the vendor received it.",
        "Issued" => "The vendor delivery has been confirmed.", "Returned" => "Resolve the funding commitment before editing and resubmitting.",
        "Rejected" => "Resolve the funding commitment before releasing the reserved request quantities.", _ => "Review the recorded next action." };
    private static void Audit(ProcurementDbContext db, ProcurementRequestScope scope, PurchaseOrder order, string action, object fields) =>
        db.AddScoped(new AuditEvent { RecordType = "PurchaseOrder", RecordId = order.Id, Action = action,
            ActorSubjectId = scope.SubjectId, ChangedFieldsJson = JsonSerializer.Serialize(fields), CorrelationId = scope.CorrelationId });
    private static void Event(ProcurementDbContext db, PurchaseOrder order, string type, string effect, object payload) =>
        db.AddScoped(new OutboxEvent { EventType = type, AggregateType = "PurchaseOrder", AggregateId = order.Id,
            IdempotencyKey = effect, PayloadJson = JsonSerializer.Serialize(payload) });
    private static bool Handled(Exception error) => error is OrderProblem or InvalidOperationException or ArgumentException
        or DbUpdateException or NpgsqlException or JsonException or KeyNotFoundException or OverflowException;
    private static IResult Failure(Exception error)
    {
        var code = error is OrderProblem ? error.Message : error is InvalidOperationException && error.Message.StartsWith("Order", StringComparison.Ordinal)
            ? error.Message : error is DbUpdateConcurrencyException ? "OrderVersionChanged" : "OrderOperationUnavailable";
        var status = error is OrderProblem problem ? problem.Status : error is NpgsqlException ? 503 : 409;
        return Results.Problem(statusCode: status, title: status == 503 ? "Order processing is unavailable. Try again shortly."
            : "The order could not be changed. Refresh its details and review the next action.", extensions: new Dictionary<string, object?> { ["code"] = code });
    }
}
