using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementReceivingRoutes
{
    private static Task<IResult> Sources(HttpContext http, ProcurementRequestScope scope, int? page, int? pageSize, CancellationToken ct)
        => Run(http, scope, false, async (db, cases, core, auth, token) =>
        {
            // Resolve current actor and permitted Core branches once; SQL scope precedes count/page.
            var actor = await Actor(core, scope, http, auth, "procurement-receipt-view", scope.WorkLocationId, null, token, target: false);
            var branches = actor.BranchIds.ToArray(); var number = Math.Clamp(page ?? 1, 1, 100000); var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var query = from order in db.PurchaseOrders.AsNoTracking()
                join issued in db.OrderIssuedVersions.AsNoTracking() on new { order.Id, Revision = order.CurrentRevision } equals new { Id = issued.PurchaseOrderId, Revision = issued.RevisionNumber }
                join line in db.PurchaseOrderLines.AsNoTracking() on order.Id equals line.PurchaseOrderId
                where order.Status == "Issued" && line.IsActive
                    && (scope.WorkLocationId == null || order.BranchRef == scope.WorkLocationId)
                    && (branches.Length == 0 || order.BranchRef != null && branches.Contains(order.BranchRef.Value))
                orderby order.CreatedAt, order.Id, line.LineNumber
                select new { orderId = order.Id, order.Number, issuedId = issued.Id, lineId = line.Id };
            var total = await query.CountAsync(token); var pageRows = await query.Skip((number - 1) * size).Take(size).ToArrayAsync(token);
            var records = new List<object>();
            foreach (var row in pageRows)
            {
                try
                {
                    var source = await Source(db, cases, scope, row.orderId, row.issuedId, row.lineId, token, lockSource: false);
                    ReceivingRule? rule = null; string? blocker = null;
                    try { rule = (await Policy(db, source, null, token)).Rule; }
                    catch (Exception ex) when (ex is ReceivingProblem or InvalidOperationException) { blocker = "Receiving policy or classification needs configuration."; }
                    var state = await db.ReceivingSourceStates.AsNoTracking().SingleOrDefaultAsync(x => x.IssuedVersionId == row.issuedId && x.PurchaseOrderLineId == row.lineId, token);
                    records.Add(new { row.orderId, orderNumber = row.Number, issuedVersionId = row.issuedId, purchaseOrderLineId = row.lineId,
                        source.Line.Description, source.Line.UnitCode, source.Order.CurrencyCode, source.Order.BranchRef,
                        issuedRevision = source.Issued.RevisionNumber, issuedSnapshotSha256 = source.Issued.CommercialSnapshotSha256,
                        orderedQuantity = Exact(source.Line.Quantity), netUnitPrice = Exact(source.Line.UnitPrice), categoryCode = source.Category,
                        purchaseType = source.PurchaseType, kind = rule?.Kind.ToString(), receivingSourceStateId = state?.Id,
                        sourceVersion = state?.Version ?? 0, sourceSequence = state?.Sequence ?? 0,
                        acceptanceHistory = "RequiresCommandVerification", blocker });
                }
                catch (Exception ex) when (ex is ReceivingProblem or InvalidOperationException or JsonException)
                { records.Add(new { row.orderId, orderNumber = row.Number, issuedVersionId = row.issuedId, purchaseOrderLineId = row.lineId,
                    blocker = "The issued source evidence needs review.", acceptanceHistory = "Unverified" }); }
            }
            return JsonSerializer.Serialize(new { items = records, total, page = number, pageSize = size }, Json);
        }, ct);
    private static Task<IResult> Worklist(HttpContext http, ProcurementRequestScope scope, string? view, string? state, int? page, int? pageSize, CancellationToken ct)
        => Run(http, scope, false, async (db, cases, core, auth, token) =>
        {
            if (view is not (null or "receipts" or "services")) throw new ReceivingProblem("ReceivingWorklistViewUnsupported", 422);
            var actor = await Actor(core, scope, http, auth, "procurement-receipt-view", scope.WorkLocationId, null, token, target: false);
            var branches = actor.BranchIds.ToArray(); var number = Math.Clamp(page ?? 1, 1, 100000); var size = Math.Clamp(pageSize ?? 25, 1, 100);
            if (view == "services")
            {
                var query = from service in db.ServiceAcceptances.AsNoTracking()
                    join line in db.PurchaseOrderLines.AsNoTracking() on service.PurchaseOrderLineId equals line.Id
                    join order in db.PurchaseOrders.AsNoTracking() on line.PurchaseOrderId equals order.Id
                    where (scope.WorkLocationId == null || order.BranchRef == scope.WorkLocationId)
                        && (branches.Length == 0 || order.BranchRef != null && branches.Contains(order.BranchRef.Value))
                        && (state == null || service.Status == state)
                    orderby service.CreatedAt descending, service.Id
                    select new { service.Id, service.Status, service.Version, service.ExecutionVersion, service.DeliverableRef,
                        orderId = order.Id, orderNumber = order.Number, service.PeriodFrom, service.PeriodTo, service.CertifiedAt };
                return JsonSerializer.Serialize(new { items = await query.Skip((number - 1) * size).Take(size).ToArrayAsync(token),
                    total = await query.CountAsync(token), page = number, pageSize = size, view = "services" }, Json);
            }
            else
            {
                var query = from receipt in db.GoodsReceipts.AsNoTracking()
                    join order in db.PurchaseOrders.AsNoTracking() on receipt.PurchaseOrderId equals order.Id
                    where (scope.WorkLocationId == null || order.BranchRef == scope.WorkLocationId)
                        && (branches.Length == 0 || order.BranchRef != null && branches.Contains(order.BranchRef.Value))
                        && (state == null || receipt.Status == state)
                    orderby receipt.CreatedAt descending, receipt.Id
                    select new { receipt.Id, receipt.Number, receipt.Status, receipt.Version, receipt.ExecutionVersion,
                        orderId = order.Id, orderNumber = order.Number, receipt.SiteRef, receipt.ReceivedAt, receipt.RecordedAt };
                return JsonSerializer.Serialize(new { items = await query.Skip((number - 1) * size).Take(size).ToArrayAsync(token),
                    total = await query.CountAsync(token), page = number, pageSize = size, view = "receipts" }, Json);
            }
        }, ct);
}
