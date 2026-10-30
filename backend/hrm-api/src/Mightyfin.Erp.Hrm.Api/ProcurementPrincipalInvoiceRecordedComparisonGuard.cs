using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal static class ProcurementPrincipalInvoiceRecordedComparisonGuard
{
    internal const string Code = "PrincipalInvoiceRecordedComparisonRequiresCorrection";

    // All principal writers take the same invoice row lock before this read. A
    // receipt is PO-bound, so its saved invoice identity must be checked; a
    // sibling invoice's result on the same PO cannot close this invoice.
    internal static async Task<bool> IsLockedAsync(ProcurementDbContext db, SupplierInvoice invoice,
        bool currentPrincipalMapping, CancellationToken ct, Guid? requestedOrderId = null,
        bool principalOnly = false)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("PrincipalInvoiceSharedTransactionRequired");
        var results = await db.InvoiceMatchResults.AsNoTracking().Where(x =>
            x.TenantId == invoice.TenantId && x.LegalEntityId == invoice.LegalEntityId
            && x.SupplierInvoiceId == invoice.Id).ToArrayAsync(ct);
        var mappedOrders = await db.InvoiceMatchMappingVersions.AsNoTracking().Where(x =>
            x.TenantId == invoice.TenantId && x.LegalEntityId == invoice.LegalEntityId
            && x.SupplierInvoiceId == invoice.Id).Select(x => x.PurchaseOrderId).ToArrayAsync(ct);
        var candidateOrders = mappedOrders.Concat(results.Where(x => x.PurchaseOrderId.HasValue)
            .Select(x => x.PurchaseOrderId!.Value)).Concat(invoice.PurchaseOrderId is { } header ? [header] : [])
            .Concat(requestedOrderId is { } requested ? [requested] : []).Distinct().ToArray();
        var invoiceId = invoice.Id.ToString("D");
        var receipts = await db.CommercialCommandReceipts.AsNoTracking().Where(x =>
            x.TenantId == invoice.TenantId && x.LegalEntityId == invoice.LegalEntityId
            && x.Operation == "InvoiceCompare" && (candidateOrders.Contains(x.PurchaseOrderId)
                || x.ResponseJson.Contains(invoiceId)))
            .Take(1025).ToArrayAsync(ct);
        return receipts.Length > 1024 || IsLocked(invoice, currentPrincipalMapping, results, receipts, principalOnly);
    }

    internal static bool IsLocked(SupplierInvoice invoice, bool currentPrincipalMapping,
        IReadOnlyList<InvoiceMatchResult> results, IReadOnlyList<ProcurementCommercialCommandReceipt> compareReceipts,
        bool principalOnly = false)
    {
        var ownResults = results.Where(x => x.SupplierInvoiceId == invoice.Id && x.TenantId == invoice.TenantId
            && x.LegalEntityId == invoice.LegalEntityId).ToArray();
        if (ownResults.Any(x => x.SourceSchemaVersion == 3)) return true;
        if (invoice.MatchRevision != ownResults.Length
            || (invoice.LastMatchResultId is null) != (ownResults.Length == 0)
            || invoice.LastMatchResultId is { } last && ownResults.Count(x => x.Id == last) != 1
            || invoice.ActiveMatchClaimResultId is { } claim && ownResults.Count(x => x.Id == claim) != 1)
            return true;
        // The source-3 mapping family cannot treat a missing/contradictory
        // result pointer as an empty comparison history.
        if (currentPrincipalMapping && ownResults.Length != 0) return true;
        foreach (var receipt in compareReceipts)
        {
            try
            {
                if (receipt.TenantId != invoice.TenantId || receipt.LegalEntityId != invoice.LegalEntityId
                    || receipt.Operation != "InvoiceCompare" || receipt.PurchaseOrderId == Guid.Empty
                    || !Guid.TryParseExact(receipt.ActorPrincipalId, "D", out var actorId) || actorId == Guid.Empty
                    || receipt.CommandKey == Guid.Empty || receipt.CompletedAt == default
                    || receipt.Fingerprint?.Length != 64
                    || receipt.Fingerprint.Any(x => !((x >= '0' && x <= '9') || (x >= 'a' && x <= 'f')))
                    || ProcurementEvidenceEncoding.PolicyJsonDigest(receipt.ResponseJson).Length != 64) return true;
                using var body = JsonDocument.Parse(receipt.ResponseJson);
                var root = body.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !TypedCompareResponse(root, receipt)) return true;
                var identities = root.EnumerateObject().Where(x => x.NameEquals("invoiceId")).ToArray();
                if (identities.Length != 1 || identities[0].Value.ValueKind != JsonValueKind.String
                    || !Guid.TryParseExact(identities[0].Value.GetString(), "D", out var savedId))
                    return true;
                if (savedId != invoice.Id) continue;
                // The legacy Map entry must preserve genuine source-1 histories.
                // Its extra admission fence only closes principal comparisons;
                // malformed candidate receipts still fail before this branch.
                if (principalOnly && root.GetProperty("commandSchemaVersion").GetInt32() == 1) continue;
                // A target receipt without its result is still recorded work.
                // Its shape may be damaged, but it cannot authorize a new key.
                return true;
            }
            catch (JsonException) { return true; }
        }
        return false;
    }

    private static bool TypedCompareResponse(JsonElement root, ProcurementCommercialCommandReceipt receipt)
    {
        if (!root.TryGetProperty("commandSchemaVersion", out var schema)
            || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version)
            || version is not (1 or 3)) return false;
        var rootNames = version == 3
            ? new[] { "invoiceId", "commandKey", "commandPermission", "commandSchemaVersion", "sourceSchemaVersion",
                "recordedHistoricalResult", "isCurrentApproval", "canPostToFinance", "result" }
            : ["invoiceId", "commandKey", "commandPermission", "commandSchemaVersion",
                "recordedHistoricalResult", "isCurrentApproval", "canPostToFinance", "result"];
        if (!ExactNames(root, rootNames)
            || !root.TryGetProperty("commandKey", out var key) || key.ValueKind != JsonValueKind.String
            || !Guid.TryParseExact(key.GetString(), "D", out var savedKey) || savedKey != receipt.CommandKey
            || !String(root, "commandPermission", "procurement-invoice-match")
            || !Boolean(root, "recordedHistoricalResult", true)
            || !Boolean(root, "isCurrentApproval", false) || !Boolean(root, "canPostToFinance", false)
            || version == 3 && (!root.TryGetProperty("sourceSchemaVersion", out var source)
                || source.ValueKind != JsonValueKind.Number || !source.TryGetInt32(out var sourceVersion)
                || sourceVersion != 3)) return false;
        var result = root.GetProperty("result");
        if (result.ValueKind != JsonValueKind.Object
            || !ExactNames(result, ["id", "outcome", "version", "matchingState", "hasCapacityClaim",
                "canPostToFinance", "discrepancies", "sourceBlockers"])
            || result.GetProperty("outcome").ValueKind != JsonValueKind.String
            || result.GetProperty("matchingState").ValueKind != JsonValueKind.String) return false;
        var outcome = result.GetProperty("outcome").GetString();
        var state = result.GetProperty("matchingState").GetString();
        return ((outcome == "Blocked" && state == "ComparisonBlocked")
                || (outcome == "Compared" && state == "ComparisonPassed"))
            && result.GetProperty("id").ValueKind == JsonValueKind.String
            && Guid.TryParseExact(result.GetProperty("id").GetString(), "D", out var resultId)
            && resultId != Guid.Empty
            && result.GetProperty("outcome").ValueKind == JsonValueKind.String
            && result.GetProperty("version").ValueKind == JsonValueKind.Number
            && result.GetProperty("version").TryGetInt64(out var invoiceVersion) && invoiceVersion > 0
            && result.GetProperty("matchingState").ValueKind == JsonValueKind.String
            && result.GetProperty("hasCapacityClaim").ValueKind is JsonValueKind.True or JsonValueKind.False
            && result.GetProperty("hasCapacityClaim").GetBoolean() == (outcome == "Compared")
            && Boolean(result, "canPostToFinance", false)
            && result.GetProperty("discrepancies").ValueKind == JsonValueKind.Array
            && result.GetProperty("sourceBlockers").ValueKind == JsonValueKind.Array;
    }

    private static bool ExactNames(JsonElement value, IReadOnlyCollection<string> names)
        => value.EnumerateObject().Select(x => x.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(names.Order(StringComparer.Ordinal));
    private static bool String(JsonElement value, string name, string expected)
        => value.GetProperty(name).ValueKind == JsonValueKind.String
            && value.GetProperty(name).GetString() == expected;
    private static bool Boolean(JsonElement value, string name, bool expected)
        => expected ? value.GetProperty(name).ValueKind == JsonValueKind.True
            : value.GetProperty(name).ValueKind == JsonValueKind.False;
}
