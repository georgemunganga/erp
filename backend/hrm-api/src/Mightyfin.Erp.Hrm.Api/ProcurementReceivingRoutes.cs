using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
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

public static partial class ProcurementReceivingRoutes
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed class ReceivingProblem(string code, int status) : Exception(code) { public int Status => status; }
    public sealed record ReceiptLineDraft(Guid? Id, Guid PurchaseOrderLineId, string ReceivedQuantity, string RejectedQuantity);
    public sealed record ReceiptDraft(Guid PurchaseOrderId, Guid IssuedVersionId, Guid SiteId, DateTimeOffset ReceivedAt,
        string? DeliveryNoteRef, string? Notes, ReceiptLineDraft[] Lines);
    public sealed record VersionedReceipt(long Version, ReceiptDraft Draft);
    public sealed record RecordReceipt(long Version, string EvidenceReference);
    public sealed record ReceivingAction(long Version, long SourceVersion, string Quantity, string EvidenceReference);
    public sealed record ServiceDraft(Guid PurchaseOrderId, Guid IssuedVersionId, Guid PurchaseOrderLineId,
        string DeliverableReference, DateOnly PeriodFrom, DateOnly PeriodTo, string Quantity, string? Notes);
    public sealed record VersionedService(long Version, ServiceDraft Draft);
    public sealed record CancelDraft(long Version, string Reason);
    public static void MapRoutes(WebApplication app)
    {
        var root = app.MapGroup("/api/procurement/v1");
        root.MapGet("/receiving/sources", Sources).RequireAuthorization("procurement-receipt-view");
        root.MapGet("/receiving/worklist", Worklist).RequireAuthorization("procurement-receipt-view");
        root.MapGet("/receiving/inspection-tasks", InspectionTasks).RequireAuthorization(ControlledReceiving.InspectPermission);
        root.MapGet("/receiving/service-tasks", ServiceTasks).RequireAuthorization(ControlledReceiving.CertifyPermission);
        root.MapGet("/receipts/{id:guid}/inspection-task", InspectionTask).RequireAuthorization(ControlledReceiving.InspectPermission);
        root.MapGet("/service-acceptances/{id:guid}/certification-task", ServiceTask).RequireAuthorization(ControlledReceiving.CertifyPermission);
        root.MapPost("/receipts/drafts", CreateReceipt).RequireAuthorization("procurement-receipt-create");
        root.MapPut("/receipts/{id:guid}/draft", EditReceipt).RequireAuthorization("procurement-receipt-edit");
        root.MapGet("/receipts/{id:guid}", ReceiptDetail).RequireAuthorization("procurement-receipt-view");
        root.MapPost("/receipts/{id:guid}/cancel-draft", CancelReceipt).RequireAuthorization("procurement-receipt-edit");
        root.MapPost("/receipts/{id:guid}/record", RecordGoods).RequireAuthorization(ControlledReceiving.RecordPermission);
        root.MapPost("/receipts/{id:guid}/lines/{lineId:guid}/inspect", InspectGoods).RequireAuthorization(ControlledReceiving.InspectPermission);
        root.MapPost("/receipts/{id:guid}/lines/{lineId:guid}/accept", AcceptGoods).RequireAuthorization(ControlledReceiving.AcceptPermission);
        root.MapPost("/service-acceptances/drafts", CreateService).RequireAuthorization("procurement-receipt-create");
        root.MapPut("/service-acceptances/{id:guid}/draft", EditService).RequireAuthorization("procurement-receipt-edit");
        root.MapGet("/service-acceptances/{id:guid}", ServiceDetail).RequireAuthorization("procurement-receipt-view");
        root.MapPost("/service-acceptances/{id:guid}/cancel-draft", CancelService).RequireAuthorization("procurement-receipt-edit");
        root.MapPost("/service-acceptances/{id:guid}/certify", CertifyService).RequireAuthorization(ControlledReceiving.CertifyPermission);
    }
    private static async Task<IResult> Run(HttpContext http, ProcurementRequestScope scope, bool final,
        Func<ProcurementDbContext, ApprovalDbContext, HrmDbContext, IAuthorizationService, CancellationToken, Task<string>> work, CancellationToken ct)
    {
        try
        {
            var options = http.RequestServices.GetRequiredService<ProcurementReceivingExecutionOptions>();
            if (!options.DraftsEnabled || final && !options.PositiveExecutionEnabled || !options.ExecutionDatabaseConfigured
                || !await http.RequestServices.GetRequiredService<IProcurementReceivingSchemaReadiness>().IsReadyAsync(ct))
                throw new ReceivingProblem("ReceivingExecutionUnavailable", 503);
            var response = await http.RequestServices.GetRequiredService<ProcurementApprovalTransaction>().ExecuteAsync(async (db, cases, token) =>
            {
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                return await work(db, cases, core, http.RequestServices.GetRequiredService<IAuthorizationService>(), token);
            }, ct);
            return Results.Content(response, "application/json");
        }
        catch (Exception ex) when (ex is ReceivingProblem or InvalidOperationException or ArgumentException or UnauthorizedAccessException
            or KeyNotFoundException or JsonException or DbUpdateException or NpgsqlException or OverflowException or FormatException)
        {
            var pg = ex.GetBaseException() as PostgresException;
            var status = ex is ReceivingProblem p ? p.Status : ex is UnauthorizedAccessException ? 403 : ex is KeyNotFoundException ? 404
                : pg?.SqlState is "40001" or "40P01" || ex is DbUpdateConcurrencyException ? 409
                : ex is InvalidOperationException { Message: "ExactDecimalNotRepresentable" } ? 422 : ex is NpgsqlException ? 503 : 409;
            var code = ex is ReceivingProblem ? ex.Message : ex is InvalidOperationException && ex.Message.StartsWith("Receiving", StringComparison.Ordinal)
                ? ex.Message : "ReceivingEvidenceNeedsReview";
            return Results.Json(new { code, message = status == 503 ? "Receiving is not ready. Ask your administrator to check its configuration."
                : status == 422 ? "Check the exact quantities and amounts. No changes were saved." : "This action could not be saved. Refresh the receipt and review its evidence and current access." }, statusCode: status);
        }
    }
    private static Guid Key(HttpContext http) => Guid.TryParse(http.Request.Headers["Idempotency-Key"], out var key) && key != Guid.Empty ? key : throw new ReceivingProblem("ReceivingCommandKeyRequired", 400);
    private static string Fingerprint(object input) => ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(input, Json));
    private static decimal Quantity(string input, bool positive = false)
    {
        decimal value;
        try { value = ExactDecimalJsonCodec.Parse(input, 18, 4, "ReceivingQuantity"); }
        catch (JsonException) { throw new ReceivingProblem("ReceivingQuantityInvalid", 422); }
        if (value < 0 || positive && value <= 0) throw new ReceivingProblem("ReceivingQuantityInvalid", 422);
        return value;
    }
    private static string Exact(decimal value) => ProcurementEvidenceEncoding.Decimal(value);
    private static DateTimeOffset Now() => ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
    private static void Version(long actual, long expected) { if (actual != expected) throw new ReceivingProblem("ReceivingVersionChanged", 409); }
    private static async Task<ProcurementCommercialCommandReceipt?> Replay(ProcurementDbContext db, ProcurementRequestScope scope, string operation, Guid key, string fingerprint, Guid orderId, Guid targetId, CancellationToken ct)
    {
        var receipt = await db.CommercialCommandReceipts.SingleOrDefaultAsync(x => x.Operation == operation && x.CommandKey == key, ct);
        if (receipt is not null)
        {
            using var saved = JsonDocument.Parse(receipt.ResponseJson);
            if (receipt.ActorPrincipalId != scope.SubjectId || receipt.Fingerprint != fingerprint || receipt.PurchaseOrderId != orderId
                || targetId != Guid.Empty && saved.RootElement.GetProperty("id").GetGuid() != targetId) throw new ReceivingProblem("ReceivingCommandKeyConflict", 409);
        }
        return receipt;
    }
    private static async Task<string> Save(ProcurementDbContext db, ProcurementRequestScope scope, Guid orderId, Guid targetId,
        string operation, Guid key, string fingerprint, Func<object> response, CancellationToken ct, string? commandPermission = null)
    {
        await db.SaveChangesAsync(ct);
        var node = JsonSerializer.SerializeToNode(response(), Json)!.AsObject();
        node["commandPermission"] = commandPermission ?? ReceivingOperations[operation]; node["commandSchemaVersion"] = 1;
        var json = node.ToJsonString(Json);
        db.AddScoped(new ProcurementCommercialCommandReceipt { PurchaseOrderId = orderId, ActorPrincipalId = scope.SubjectId,
            Operation = operation, CommandKey = key, Fingerprint = fingerprint, ResponseJson = json, CompletedAt = Now() });
        db.AddScoped(new AuditEvent { RecordType = operation.StartsWith("Service", StringComparison.Ordinal) ? "ServiceAcceptance" : "GoodsReceipt",
            RecordId = targetId, ActorSubjectId = scope.SubjectId, Action = operation, CorrelationId = scope.CorrelationId,
            ChangedFieldsJson = JsonSerializer.Serialize(new { commandKey = key, orderId, response = JsonDocument.Parse(json).RootElement }) });
        return json;
    }
    private static async Task<(Dictionary<Guid, ReceivingSourceState> States, Dictionary<Guid, GoodsInspectionEvidence> Inspections)> ReceiptProjection(
        ProcurementDbContext db, IReadOnlyList<GoodsReceiptLine> lines, CancellationToken ct)
    {
        var stateIds = lines.Where(x => x.ReceivingSourceStateId != null).Select(x => x.ReceivingSourceStateId!.Value).Distinct().ToArray();
        var states = await db.ReceivingSourceStates.Where(x => stateIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        foreach (var entry in db.ChangeTracker.Entries<ReceivingSourceState>().Where(x => x.State == EntityState.Added && stateIds.Contains(x.Entity.Id)))
            states[entry.Entity.Id] = entry.Entity;
        var lineIds = lines.Select(x => x.Id).ToArray();
        var rows = await db.ReceiptInspections.Where(x => lineIds.Contains(x.GoodsReceiptLineId) && x.ExecutionVersion == 1).ToArrayAsync(ct);
        // Before Save the new inspection is only in the tracked graph; keep all other lines' retained facts too.
        var allRows = rows.Concat(db.ChangeTracker.Entries<ReceiptInspection>().Where(x => x.State == EntityState.Added
            && lineIds.Contains(x.Entity.GoodsReceiptLineId) && x.Entity.ExecutionVersion == 1).Select(x => x.Entity));
        var inspections = allRows.ToDictionary(x => x.GoodsReceiptLineId,
            x => ReceivingPersistenceProjection.Inspection(states[x.ReceivingSourceStateId!.Value], x));
        return (states, inspections);
    }
    private static object View(GoodsReceipt header, IEnumerable<GoodsReceiptLine> lines, IReadOnlyDictionary<Guid, ReceivingSourceState>? states = null, IReadOnlyDictionary<Guid, GoodsInspectionEvidence>? inspections = null) => new { header.Id, header.Number, header.Version, header.Status,
        header.ExecutionVersion, header.PurchaseOrderId, header.IssuedVersionId, header.SiteRef, header.ReceivedAt, header.RecordedAt,
        header.DeliveryNoteRef, header.Notes, physicalEvidenceRecorded = header.ExecutionVersion == 1 && header.RecordedAt != null, matchingAuthorityAvailable = false,
        lines = lines.Where(x => x.Status != "Cancelled").OrderBy(x => x.Id).Select(x => new { x.Id, x.Version, x.PurchaseOrderLineId, x.ReceivingSourceStateId,
            sourceVersion = x.ReceivingSourceStateId is { } stateId && states != null && states.TryGetValue(stateId, out var state) ? state.Version : 0,
            x.PhysicalObservationId, x.PhysicalSequence, x.PhysicalPolicyVersionId, x.PhysicalRecordedAt, x.Status,
            receivedQuantity = Exact(x.ReceivedQuantity), initiallyRejectedQuantity = Exact(x.InitiallyRejectedQuantity), rejectedQuantity = Exact(x.RejectedQuantity), acceptedQuantity = Exact(x.AcceptedQuantity),
            inspection = inspections != null && inspections.TryGetValue(x.Id, out var inspection) ? new { inspection.Id, outcome = inspection.Outcome.ToString(),
                qualifiedQuantity = Exact(inspection.QualifiedQuantity), inspectionRejectedQuantity = Exact(ExactDecimalArithmetic.Subtract(inspection.InspectedQuantity, inspection.QualifiedQuantity)),
                inspection.ReceivingPolicyVersionId, inspection.ChecklistReference, inspection.InspectedAt } : null,
            unacceptedHeldQuantity = x.ExecutionVersion == 1 ? Exact(ExactDecimalArithmetic.Subtract(ExactDecimalArithmetic.Subtract(x.ReceivedQuantity, x.RejectedQuantity), x.AcceptedQuantity)) : null }) };
    private static object View(ServiceAcceptance row, ReceivingSourceState? state = null) => new { row.Id, row.Version, row.Status, row.ExecutionVersion, row.PurchaseOrderLineId,
        row.SourcePurchaseOrderId, row.IssuedBindingId, row.ReceivingSourceStateId, sourceVersion = state?.Version ?? 0, row.PeriodFrom, row.PeriodTo, row.DeliverableRef, row.CertifiedAt,
        isCertifiedRecord = row.ExecutionVersion == 1 && row.CertifiedAt != null, matchingAuthorityAvailable = false,
        intendedQuantity = row.CertifiedAt == null ? Exact(row.AcceptedQuantity) : null,
        acceptedQuantity = row.CertifiedAt == null ? "0" : Exact(row.AcceptedQuantity), acceptedNetAmount = row.CertifiedAt == null ? "0" : Exact(row.AcceptedAmount), currentAcceptedQuantity = Exact(row.CurrentAcceptedQuantity),
        currentAcceptedNetAmount = Exact(row.CurrentAcceptedNetAmount), row.CertificationEffectId, row.PolicyVersionId, row.EvidenceRef };
    private static void Event(ProcurementDbContext db, Guid targetId, string type, Guid effectId, object payload)
        => db.AddScoped(new OutboxEvent { AggregateType = "Receiving", AggregateId = targetId, EventType = type,
            IdempotencyKey = effectId.ToString("D"), PayloadJson = JsonSerializer.Serialize(payload, Json) });
}
