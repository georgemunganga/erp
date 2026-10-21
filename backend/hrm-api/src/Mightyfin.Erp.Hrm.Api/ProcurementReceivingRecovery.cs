using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementReceivingRoutes
{
    private static readonly IReadOnlyDictionary<string, string> ReceivingOperations = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ReceivingDraft"] = "procurement-receipt-create", ["ServiceDraft"] = "procurement-receipt-create",
        ["ReceivingRecord"] = "procurement-receipt-record", ["ReceivingInspect"] = "procurement-receipt-inspect",
        ["ReceivingAccept"] = "procurement-receipt-accept", ["ServiceCertify"] = "procurement-service-certify",
        ["ReceivingCancel"] = "procurement-receipt-edit", ["ServiceCancel"] = "procurement-receipt-edit",
    };
    internal static bool SupportsRecovery(string operation) => ReceivingOperations.ContainsKey(operation);
    internal static Task<IResult> Recover(string operation, Guid key, Guid? orderId, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, false, async (db, cases, core, auth, token) =>
        {
            if (key == Guid.Empty || !ReceivingOperations.TryGetValue(operation, out var defaultPermission)) throw new ReceivingProblem("ReceivingRecoveryInvalid", 400);
            var receipt = await db.CommercialCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Operation == operation && x.CommandKey == key
                && x.ActorPrincipalId == scope.SubjectId && (orderId == null || x.PurchaseOrderId == orderId), token)
                ?? throw new ReceivingProblem("ReceivingRecordedResultNotFound", 404);
            using var saved = JsonDocument.Parse(receipt.ResponseJson); var result = saved.RootElement;
            if (result.GetProperty("commandSchemaVersion").GetInt32() != 1) throw new ReceivingProblem("ReceivingRecoveryEvidenceInvalid", 409);
            var permission = result.GetProperty("commandPermission").GetString();
            if (permission != defaultPermission && !((operation is "ReceivingDraft" or "ServiceDraft") && permission == "procurement-receipt-edit"))
                throw new ReceivingProblem("ReceivingRecoveryEvidenceInvalid", 409);
            var order = await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == receipt.PurchaseOrderId, token)
                ?? throw new ReceivingProblem("ReceivingOrderNotFound", 404);
            await Actor(core, scope, http, auth, permission!, order.BranchRef, null, token);
            var target = result.GetProperty("id").GetGuid(); long currentVersion; string currentStatus;
            if (operation.StartsWith("Service", StringComparison.Ordinal))
            {
                var row = await db.ServiceAcceptances.AsNoTracking().SingleOrDefaultAsync(x => x.Id == target, token)
                    ?? throw new ReceivingProblem("ReceivingTargetNotFound", 404);
                if (row.SourcePurchaseOrderId != order.Id) throw new ReceivingProblem("ReceivingTargetNotFound", 404);
                currentVersion = row.Version; currentStatus = row.Status;
            }
            else
            {
                var row = await db.GoodsReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == target && x.PurchaseOrderId == order.Id, token)
                    ?? throw new ReceivingProblem("ReceivingTargetNotFound", 404);
                currentVersion = row.Version; currentStatus = row.Status;
            }
            return JsonSerializer.Serialize(new { state = "RecordedHistoricalOutcome", receipt.PurchaseOrderId, receipt.Operation,
                receipt.CommandKey, receipt.CompletedAt, recordedResult = result.Clone(), currentTargetVersion = currentVersion,
                currentTargetStatus = currentStatus, replayAuthorized = false }, Json);
        }, ct);
}
