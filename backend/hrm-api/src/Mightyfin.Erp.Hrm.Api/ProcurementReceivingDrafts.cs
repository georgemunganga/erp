using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementReceivingRoutes
{
    private static Task<IResult> CreateReceipt(ReceiptDraft input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => SaveReceipt(null, null, input, http, scope, ct);
    private static Task<IResult> EditReceipt(Guid id, VersionedReceipt input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => SaveReceipt(id, input.Version, input.Draft, http, scope, ct);
    private static Task<IResult> SaveReceipt(Guid? id, long? version, ReceiptDraft input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, false, async (db, cases, core, auth, token) =>
        {
            if (input.Lines is not { Length: > 0 and <= 100 } || input.Lines.Select(x => x.PurchaseOrderLineId).Distinct().Count() != input.Lines.Length
                || input.ReceivedAt == default || input.ReceivedAt > DateTimeOffset.UtcNow || input.Notes?.Length > 2000 || input.DeliveryNoteRef?.Length > 200)
                throw new ReceivingProblem("ReceivingDraftInvalid", 422);
            var permission = id.HasValue ? "procurement-receipt-edit" : "procurement-receipt-create";
            var sources = new List<SourceLoad>();
            foreach (var line in input.Lines.OrderBy(x => x.PurchaseOrderLineId))
            {
                var source = await Source(db, cases, scope, input.PurchaseOrderId, input.IssuedVersionId, line.PurchaseOrderLineId, token);
                await Actor(core, scope, http, auth, permission, source.Order.BranchRef, input.SiteId, token);
                if (Quantity(line.RejectedQuantity) > Quantity(line.ReceivedQuantity)) throw new ReceivingProblem("ReceivingDraftDispositionInvalid", 422);
                sources.Add(source);
            }
            var key = Key(http); var fingerprint = Fingerprint(new { operation = "ReceivingDraft", id, version, input });
            if (id is { } targetId)
            {
                var target = await db.GoodsReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == targetId, token);
                if (target is null || target.CreatedBy != scope.SubjectId || target.PurchaseOrderId != input.PurchaseOrderId
                    || target.IssuedVersionId != input.IssuedVersionId) throw new ReceivingProblem("ReceivingDraftTargetUnavailable", 404);
            }
            var prior = await Replay(db, scope, "ReceivingDraft", key, fingerprint, input.PurchaseOrderId, id ?? Guid.Empty, token);
            if (prior is not null) return prior.ResponseJson;
            GoodsReceipt header; GoodsReceiptLine[] rows;
            if (id is { } existing)
            {
                await LockReceipt(db, scope, existing, token);
                header = await db.GoodsReceipts.SingleOrDefaultAsync(x => x.Id == existing, token) ?? throw new ReceivingProblem("ReceivingReceiptNotFound", 404);
                Version(header.Version, version!.Value);
                if (header.CreatedBy != scope.SubjectId || header.Status != "Draft" || header.RecordedAt != null || header.ExecutionVersion != 1
                    || header.PurchaseOrderId != input.PurchaseOrderId || header.IssuedVersionId != input.IssuedVersionId)
                    throw new ReceivingProblem("ReceivingDraftLocked", 409);
                rows = await db.GoodsReceiptLines.Where(x => x.GoodsReceiptId == existing).ToArrayAsync(token);
                if (rows.Any(x => x.PhysicalObservationId != null || x.AcceptedQuantity != 0)) throw new ReceivingProblem("ReceivingDraftLocked", 409);
                foreach (var row in rows) row.Status = "Cancelled";
            }
            else
            {
                header = new() { Number = "GRN-" + Guid.NewGuid().ToString("N"), PurchaseOrderId = input.PurchaseOrderId, ExecutionVersion = 1,
                    IssuedVersionId = input.IssuedVersionId, IssuedBindingId = input.IssuedVersionId, SubmissionId = sources[0].Submission.Id,
                    BranchRef = sources[0].Order.BranchRef, ReceiverSubjectId = scope.SubjectId, Status = "Draft" };
                db.AddScoped(header); rows = [];
            }
            header.SiteRef = input.SiteId; header.ReceivedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(input.ReceivedAt)!.Value;
            header.Notes = input.Notes?.Trim(); header.DeliveryNoteRef = input.DeliveryNoteRef?.Trim();
            var result = new List<GoodsReceiptLine>();
            foreach (var inputLine in input.Lines)
            {
                var row = inputLine.Id is { } lineId ? rows.SingleOrDefault(x => x.Id == lineId && x.PurchaseOrderLineId == inputLine.PurchaseOrderLineId)
                    ?? throw new ReceivingProblem("ReceivingDraftLineNotFound", 404) : new GoodsReceiptLine { GoodsReceiptId = header.Id,
                        PurchaseOrderLineId = inputLine.PurchaseOrderLineId, ExecutionVersion = 1, SourcePurchaseOrderId = header.PurchaseOrderId, IssuedBindingId = input.IssuedVersionId };
                if (inputLine.Id is null) db.AddScoped(row);
                row.ReceivedQuantity = Quantity(inputLine.ReceivedQuantity); row.InitiallyRejectedQuantity = Quantity(inputLine.RejectedQuantity);
                row.RejectedQuantity = row.InitiallyRejectedQuantity; row.Status = "Draft"; result.Add(row);
            }
            return await Save(db, scope, header.PurchaseOrderId, header.Id, "ReceivingDraft", key, fingerprint, () => View(header, result), token, permission);
        }, ct);
    private static Task LockReceipt(ProcurementDbContext db, ProcurementRequestScope scope, Guid id, CancellationToken ct)
        => db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.goods_receipts WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE", ct);
    private static async Task<(GoodsReceipt Header, GoodsReceiptLine[] Lines)> LoadReceipt(ProcurementDbContext db, Guid id, CancellationToken ct)
    {
        var header = await db.GoodsReceipts.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ReceivingProblem("ReceivingReceiptNotFound", 404);
        var lines = await db.GoodsReceiptLines.Where(x => x.GoodsReceiptId == id && x.Status != "Cancelled").OrderBy(x => x.PurchaseOrderLineId).ToArrayAsync(ct);
        return (header, lines);
    }
    private static Task<IResult> ReceiptDetail(Guid id, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, false, async (db, cases, core, auth, token) =>
        {
            var (header, lines) = await LoadReceipt(db, id, token);
            var branch = await db.PurchaseOrders.Where(x => x.Id == header.PurchaseOrderId).Select(x => x.BranchRef).SingleAsync(token);
            await Actor(core, scope, http, auth, "procurement-receipt-view", branch, null, token);
            if (header.ExecutionVersion == 1 && (header.BranchRef != branch || !await BoundIssuedOrders(db, scope).AnyAsync(x => x.Id == header.PurchaseOrderId, token))) throw new ReceivingProblem("ReceivingCurrentIssuedAuthorityChanged", 409);
            return await ReceiptRead(db, cases, core, auth, scope, http, header, lines, token);
        }, ct);
    private static Task<IResult> CancelReceipt(Guid id, CancelDraft input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, false, async (db, cases, core, auth, token) =>
        {
            var (header, lines) = await LoadReceipt(db, id, token);
            foreach (var line in lines) await Source(db, cases, scope, header.PurchaseOrderId, header.IssuedVersionId ?? Guid.Empty, line.PurchaseOrderLineId, token);
            await Actor(core, scope, http, auth, "procurement-receipt-edit", header.BranchRef, header.SiteRef, token);
            if (header.CreatedBy != scope.SubjectId) throw new ReceivingProblem("ReceivingDraftTargetUnavailable", 404);
            var key = Key(http); var fingerprint = Fingerprint(new { id, input });
            var prior = await Replay(db, scope, "ReceivingCancel", key, fingerprint, header.PurchaseOrderId, id, token);
            if (prior is not null) return prior.ResponseJson;
            await LockReceipt(db, scope, id, token); Version(header.Version, input.Version);
            if (header.CreatedBy != scope.SubjectId || header.Status != "Draft" || header.RecordedAt != null || lines.Any(x => x.PhysicalObservationId != null)
                || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 1000) throw new ReceivingProblem("ReceivingDraftCancellationInvalid", 409);
            header.Status = "Cancelled";
            return await Save(db, scope, header.PurchaseOrderId, id, "ReceivingCancel", key, fingerprint, () => View(header, lines), token);
        }, ct);
    private static Task<IResult> CreateService(ServiceDraft input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => SaveService(null, null, input, http, scope, ct);
    private static Task<IResult> EditService(Guid id, VersionedService input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => SaveService(id, input.Version, input.Draft, http, scope, ct);
    private static Task<IResult> SaveService(Guid? id, long? version, ServiceDraft input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, false, async (db, cases, core, auth, token) =>
        {
            var source = await Source(db, cases, scope, input.PurchaseOrderId, input.IssuedVersionId, input.PurchaseOrderLineId, token);
            await Actor(core, scope, http, auth, id.HasValue ? "procurement-receipt-edit" : "procurement-receipt-create", source.Order.BranchRef, source.Order.BranchRef, token);
            if (string.IsNullOrWhiteSpace(input.DeliverableReference) || input.DeliverableReference.Trim().Length > 200
                || input.PeriodFrom > input.PeriodTo || input.Notes?.Length > 2000) throw new ReceivingProblem("ReceivingServiceDraftInvalid", 422);
            var quantity = Quantity(input.Quantity); var amount = ExactDecimalArithmetic.Multiply(quantity, source.Line.UnitPrice);
            var key = Key(http); var fingerprint = Fingerprint(new { operation = "ServiceDraft", id, version, input });
            if (id is { } targetId)
            {
                var target = await db.ServiceAcceptances.AsNoTracking().SingleOrDefaultAsync(x => x.Id == targetId, token);
                if (target is null || target.CreatedBy != scope.SubjectId || target.SourcePurchaseOrderId != input.PurchaseOrderId
                    || target.IssuedBindingId != input.IssuedVersionId || target.PurchaseOrderLineId != input.PurchaseOrderLineId)
                    throw new ReceivingProblem("ReceivingDraftTargetUnavailable", 404);
            }
            var prior = await Replay(db, scope, "ServiceDraft", key, fingerprint, source.Order.Id, id ?? Guid.Empty, token);
            if (prior is not null) return prior.ResponseJson;
            ServiceAcceptance row;
            if (id is { } existing)
            {
                row = await db.ServiceAcceptances.SingleOrDefaultAsync(x => x.Id == existing, token) ?? throw new ReceivingProblem("ReceivingServiceNotFound", 404);
                Version(row.Version, version!.Value);
                if (row.CreatedBy != scope.SubjectId || row.CertifiedAt != null || row.ExecutionVersion != 1 || row.Status != "PendingCertification"
                    || row.SourcePurchaseOrderId != input.PurchaseOrderId || row.IssuedBindingId != input.IssuedVersionId || row.PurchaseOrderLineId != input.PurchaseOrderLineId)
                    throw new ReceivingProblem("ReceivingServiceDraftLocked", 409);
            }
            else { row = new() { ExecutionVersion = 1, PurchaseOrderLineId = input.PurchaseOrderLineId, SourcePurchaseOrderId = input.PurchaseOrderId,
                IssuedBindingId = input.IssuedVersionId, OwnerSubjectId = scope.SubjectId }; db.AddScoped(row); }
            row.DeliverableRef = input.DeliverableReference.Trim(); row.PeriodFrom = input.PeriodFrom; row.PeriodTo = input.PeriodTo;
            row.AcceptedQuantity = quantity; row.AcceptedAmount = amount; row.Reason = input.Notes?.Trim();
            return await Save(db, scope, source.Order.Id, row.Id, "ServiceDraft", key, fingerprint, () => View(row), token, id.HasValue ? "procurement-receipt-edit" : "procurement-receipt-create");
        }, ct);
    private static Task<IResult> CancelService(Guid id, CancelDraft input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, false, async (db, cases, core, auth, token) =>
        {
            var row = await db.ServiceAcceptances.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new ReceivingProblem("ReceivingServiceNotFound", 404);
            if (row.ExecutionVersion != 1 || row.SourcePurchaseOrderId is null || row.CreatedBy != scope.SubjectId)
                throw new ReceivingProblem("ReceivingDraftTargetUnavailable", 404);
            var source = await Source(db, cases, scope, row.SourcePurchaseOrderId.Value, row.IssuedBindingId, row.PurchaseOrderLineId, token);
            await Actor(core, scope, http, auth, "procurement-receipt-edit", source.Order.BranchRef, source.Order.BranchRef, token);
            var key = Key(http); var fingerprint = Fingerprint(new { id, input });
            var prior = await Replay(db, scope, "ServiceCancel", key, fingerprint, source.Order.Id, id, token);
            if (prior is not null) return prior.ResponseJson;
            Version(row.Version, input.Version);
            if (row.CertifiedAt != null || row.Status != "PendingCertification" || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 1000)
                throw new ReceivingProblem("ReceivingServiceDraftLocked", 409);
            row.Status = "Cancelled"; row.Reason = input.Reason.Trim();
            return await Save(db, scope, source.Order.Id, id, "ServiceCancel", key, fingerprint, () => View(row), token);
        }, ct);
    private static Task<IResult> ServiceDetail(Guid id, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, false, async (db, cases, core, auth, token) =>
        {
            var row = await db.ServiceAcceptances.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new ReceivingProblem("ReceivingServiceNotFound", 404);
            var orderId = row.SourcePurchaseOrderId ?? await db.PurchaseOrderLines.Where(x => x.Id == row.PurchaseOrderLineId).Select(x => x.PurchaseOrderId).SingleAsync(token);
            var branch = await db.PurchaseOrders.Where(x => x.Id == orderId).Select(x => x.BranchRef).SingleAsync(token);
            await Actor(core, scope, http, auth, "procurement-receipt-view", branch, null, token);
            if (row.ExecutionVersion == 1 && !await BoundIssuedOrders(db, scope).AnyAsync(x => x.Id == orderId, token))
                throw new ReceivingProblem("ReceivingCurrentIssuedAuthorityChanged", 409);
            return await ServiceRead(db, cases, core, auth, scope, http, row, token);
        }, ct);
}
