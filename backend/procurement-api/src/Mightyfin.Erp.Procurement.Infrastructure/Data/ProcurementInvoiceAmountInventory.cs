using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>Consumed Core reference requirements, never an authority assertion.
/// Host resolves these exact retained values through the actual enlisted Core owner.</summary>
public sealed record InvoiceAmountCoreReferenceRequirement(
    string TenantId, Guid LegalEntityId, Guid InvoiceId, Guid CalculationId,
    Guid ReferenceId, int Revision, string CurrencyCode, int MinorUnits,
    InvoiceMoneyRounding Rounding, string SourcePayloadSha256,
    DateTimeOffset PublishedAtUtc, DateTimeOffset ObservedAtUtc);

/// <summary>Separate supplemental set. This is never part of the frozen M6A manifest.</summary>
public sealed record InvoiceAmountInventorySupplement(int SchemaVersion, int CalculationCount,
    string SetSha256, IReadOnlyList<InvoiceAmountCoreReferenceRequirement> CoreRequirements,
    bool CoreReferencesVerified);

public static class InvoiceAmountInventoryEvidence
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Verifies only the complete stored Procurement graph. A nonempty
    /// result still requires authoritative Core history verification by Host.</summary>
    public static InvoiceAmountInventorySupplement VerifyStoredGraph(
        InvoiceProcurementInventoryCapture legacyCapture,
        IReadOnlyList<InvoiceAmountCalculation> calculations,
        IReadOnlyList<AuditEvent> captureAudits, IReadOnlyList<OutboxEvent> captureEvents,
        IReadOnlyList<SupplierInvoice> invoices, IReadOnlyList<SupplierInvoiceLine> originalLines,
        IReadOnlyList<InvoiceIntakeCommandReceipt> receipts)
    {
        if (legacyCapture is null || string.IsNullOrWhiteSpace(legacyCapture.TenantId)
            || legacyCapture.LegalEntityId == Guid.Empty || calculations is null || captureAudits is null
            || captureEvents is null || invoices is null || originalLines is null || receipts is null
            || calculations.Count > 100_000 || captureAudits.Count > 100_000 || captureEvents.Count > 100_000
            || receipts.Count > 100_000)
            throw new InvalidOperationException("InvoiceAmountInventoryUnavailable");
        bool Scoped(ProcurementRecord row) => row.TenantId == legacyCapture.TenantId
            && row.LegalEntityId == legacyCapture.LegalEntityId && row.Id != Guid.Empty;
        if (calculations.Any(x => x is null || !Scoped(x))
            || captureAudits.Any(x => x is null || !Scoped(x))
            || captureEvents.Any(x => x is null || !Scoped(x))
            || receipts.Any(x => x is null || !Scoped(x))
            || calculations.Select(x => x.Id).Distinct().Count() != calculations.Count
            || calculations.Select(x => x.SupplierInvoiceId).Distinct().Count() != calculations.Count
            || captureAudits.Select(x => x.Id).Distinct().Count() != captureAudits.Count
            || captureEvents.Select(x => x.Id).Distinct().Count() != captureEvents.Count
            || captureAudits.Count != calculations.Count || captureEvents.Count != calculations.Count
            || receipts.Count != calculations.Count)
            throw new InvalidOperationException("InvoiceAmountInventoryMarkerSetChanged");
        if (calculations.Count == 0)
            return new(1, 0, Digest(Array.Empty<object>()), [], true);
        // Complete owning reads are tied to the already-complete frozen capture's
        // IDs. No caller-selected invoice/line subset proves company inventory.
        bool SameIds(string table, IEnumerable<ProcurementRecord> actual)
        {
            var rows = legacyCapture.Tables.Single(x => x.TableName == table).Rows;
            var values = actual.ToArray();
            return values.All(Scoped) && values.Select(x => x.Id).Distinct().Count() == values.Length
                && rows.Select(x => x.Id).Order().SequenceEqual(values.Select(x => x.Id).Order());
        }
        if (!SameIds("supplier_invoices", invoices) || !SameIds("supplier_invoice_lines", originalLines))
            throw new InvalidOperationException("InvoiceAmountInventoryOriginalSetIncomplete");
        InvoiceIntakeCommandReceiptEvidence.VerifySet(receipts, calculations, invoices, originalLines);
        var receiptByCalculation = receipts.ToDictionary(x => x.CalculationId);
        var invoiceById = invoices.ToDictionary(x => x.Id);
        var requirements = new List<InvoiceAmountCoreReferenceRequirement>();
        var proofs = new List<object>();
        long totalBytes = 0;
        foreach (var row in calculations.OrderBy(x => x.Id))
        {
            if (!invoiceById.TryGetValue(row.SupplierInvoiceId, out var invoice)
                || invoice.CreatedAt != row.CreatedAt)
                throw new InvalidOperationException("InvoiceAmountInventoryParentChanged");
            totalBytes += Encoding.UTF8.GetByteCount(row.CalculationJson ?? "");
            if (totalBytes > 100_000_000)
                throw new InvalidOperationException("InvoiceAmountInventoryBoundExceeded");
            // Include inactive originals too. An unexpected inactive original is
            // denied by the codec; it cannot be omitted to manufacture parity.
            var originals = originalLines.Where(x => x.SupplierInvoiceId == invoice.Id).ToArray();
            _ = InvoiceAmountCalculationEvidence.Verify(row, invoice, originals);
            var marker = InvoiceAmountCalculationEvidence.MarkerJson(row);
            var audits = captureAudits.Where(x => x.RecordId == row.Id).ToArray();
            var events = captureEvents.Where(x => x.AggregateId == invoice.Id).ToArray();
            if (audits.Length != 1 || events.Length != 1)
                throw new InvalidOperationException("InvoiceAmountInventoryMarkersRequired");
            var audit = audits[0]; var message = events[0];
            // PostgreSQL jsonb may reorder properties and normalize whitespace.
            // Compare the entire typed marker object, including every key and value;
            // empty/invalid JSON never proves a retained marker.
            var markerDigest = ProcurementEvidenceEncoding.PolicyJsonDigest(marker);
            bool SameMarker(string? stored) => !string.IsNullOrWhiteSpace(stored)
                && markerDigest.Length == 64
                && ProcurementEvidenceEncoding.PolicyJsonDigest(stored) == markerDigest;
            if (audit.Action != InvoiceAmountCalculationEvidence.AuditAction
                || audit.RecordType != nameof(InvoiceAmountCalculation)
                || audit.ActorSubjectId != row.CreatedBy || audit.CreatedBy != row.CreatedBy
                || audit.CreatedAt != row.CreatedAt || audit.Version != 1
                || audit.UpdatedAt is not null || audit.UpdatedBy is not null
                || !SameMarker(audit.ChangedFieldsJson)
                || message.EventType != InvoiceAmountCalculationEvidence.EventType
                || message.EventVersion != 2 || message.AggregateType != nameof(SupplierInvoice)
                || message.CreatedBy != row.CreatedBy || message.CreatedAt != row.CreatedAt
                || message.Version < 1 || !SameMarker(message.PayloadJson)
                || message.IdempotencyKey != "invoice-amount-calculation:" + row.Id.ToString("D"))
                throw new InvalidOperationException("InvoiceAmountInventoryMarkerEvidenceChanged");
            // Enum syntax was checked by the codec; no invented fallback mode.
            var rounding = Enum.Parse<InvoiceMoneyRounding>(row.RoundingMode);
            requirements.Add(new(row.TenantId, row.LegalEntityId, invoice.Id, row.Id,
                row.CoreCurrencyReferenceId, row.CoreCurrencyReferenceRevision, row.CurrencyCode,
                row.MinorUnits, rounding, row.CoreSourcePayloadSha256,
                row.CorePublishedAtUtc, row.ObservedAtUtc));
            proofs.Add(new
            {
                calculationId = row.Id, invoiceId = invoice.Id,
                rowSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(row, Json)),
                auditId = audit.Id, outboxId = message.Id,
                markerSha256 = ProcurementEvidenceEncoding.Digest(marker),
                intakeReceiptId = receiptByCalculation[row.Id].Id,
                intakeReceiptSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(
                    JsonSerializer.Serialize(receiptByCalculation[row.Id], Json))
            });
        }
        return new(1, calculations.Count, Digest(proofs), requirements.AsReadOnly(), false);
    }

    public static async Task<InvoiceAmountInventorySupplement> VerifyCoreHistoryAsync(
        InvoiceAmountInventorySupplement supplement,
        Func<InvoiceAmountCoreReferenceRequirement, CancellationToken, Task<bool>>? verifyCore,
        CancellationToken ct)
    {
        if (supplement is null || supplement.SchemaVersion != 1
            || supplement.CalculationCount < 0 || supplement.CoreRequirements is null
            || supplement.CoreRequirements.Count != supplement.CalculationCount
            || supplement.SetSha256 is not { Length: 64 })
            throw new InvalidOperationException("InvoiceAmountCoreRequirementSetChanged");
        if (supplement.CalculationCount == 0) return supplement;
        if (verifyCore is null)
            throw new InvalidOperationException("InvoiceAmountAuthoritativeCoreHistoryRequired");
        foreach (var requirement in supplement.CoreRequirements)
            if (!await verifyCore(requirement, ct))
                throw new InvalidOperationException("InvoiceAmountCoreReferenceHistoryChanged");
        return supplement with { CoreReferencesVerified = true };
    }

    private static string Digest(IEnumerable<object> rows) => ProcurementEvidenceEncoding.PolicyJsonDigest(
        JsonSerializer.Serialize(new { schemaVersion = 1, rows = rows.ToArray() }, Json));
}

public sealed partial class ProcurementDbContext
{
    /// <summary>Root calls inside common inventory capture in the actual shared
    /// Serializable transaction. Explicit callback must resolve retained Core history
    /// through that same transaction; there is no default permissive authority.</summary>
    public async Task<InvoiceAmountInventorySupplement> VerifyInvoiceAmountInventoryAsync(
        InvoiceProcurementInventoryCapture legacyCapture, bool openingNewBaseline,
        Func<InvoiceAmountCoreReferenceRequirement, CancellationToken, Task<bool>>? verifyCore,
        CancellationToken ct)
    {
        if (!Database.IsNpgsql() || Database.CurrentTransaction is null
            || Database.CurrentTransaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable
            || legacyCapture.TenantId != scope.TenantId || legacyCapture.LegalEntityId != scope.LegalEntityId)
            throw new InvalidOperationException("InvoiceAmountInventorySerializableRequired");
        await Database.ExecuteSqlRawAsync("SET LOCAL row_security = off", ct);
        long evidenceBytes = 0;
        async Task<T[]> ReadBounded<T>(IQueryable<T> query, Func<T, string?> payload)
        {
            var items = new List<T>();
            await foreach (var item in query.Take(100_001).AsAsyncEnumerable().WithCancellation(ct))
            {
                var bytes = Encoding.UTF8.GetByteCount(payload(item) ?? "");
                evidenceBytes += bytes;
                if (items.Count >= 100_000 || bytes > InvoiceAmountCalculationEvidence.MaximumJsonBytes
                    || evidenceBytes > 100_000_000)
                    throw new InvalidOperationException("InvoiceAmountInventoryBoundExceeded");
                items.Add(item);
            }
            return items.ToArray();
        }
        var audits = await ReadBounded(AuditEvents.AsNoTracking()
            .Where(x => x.Action == InvoiceAmountCalculationEvidence.AuditAction), x => x.ChangedFieldsJson);
        var messages = await ReadBounded(OutboxEvents.AsNoTracking()
            .Where(x => x.EventType == InvoiceAmountCalculationEvidence.EventType), x => x.PayloadJson);
        var rows = InvoiceAmountCalculationRelationAvailable()
            ? await ReadBounded(InvoiceAmountCalculations.AsNoTracking(), x => x.CalculationJson)
            : Array.Empty<InvoiceAmountCalculation>();
        // A missing fourth relation may coexist with a true legacy-empty DB.
        // It can never excuse a calculation whose original 201 receipt is absent.
        await using var receiptTable = Database.GetDbConnection().CreateCommand();
        receiptTable.Transaction = Database.CurrentTransaction.GetDbTransaction();
        receiptTable.CommandTimeout = 15;
        receiptTable.CommandText = "SELECT CASE WHEN to_regclass('procurement.invoice_intake_command_receipts') IS NULL THEN 0 "
            + "WHEN EXISTS(SELECT 1 FROM pg_class WHERE oid=to_regclass('procurement.invoice_intake_command_receipts') "
            + "AND relkind='r' AND relpersistence='p' AND NOT relrowsecurity AND NOT relforcerowsecurity) THEN 1 ELSE -1 END";
        var receiptRelation = Convert.ToInt32(await receiptTable.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
        if (receiptRelation is not (0 or 1)) throw new InvalidOperationException("InvoiceIntakeReceiptPhysicalRelationRequired");
        var receipts = receiptRelation == 1
            ? await ReadBounded(InvoiceIntakeCommandReceipts.AsNoTracking(), x => x.ResponseJson)
            : Array.Empty<InvoiceIntakeCommandReceipt>();
        if (openingNewBaseline && (rows.Length != 0 || audits.Length != 0 || messages.Length != 0 || receipts.Length != 0))
            throw new InvalidOperationException("InvoiceAmountBaselineSupplementApprovalRequired");
        var invoices = rows.Length == 0 ? Array.Empty<SupplierInvoice>()
            : await SupplierInvoices.AsNoTracking().ToArrayAsync(ct);
        var lines = rows.Length == 0 ? Array.Empty<SupplierInvoiceLine>()
            : await SupplierInvoiceLines.AsNoTracking().ToArrayAsync(ct);
        var supplement = InvoiceAmountInventoryEvidence.VerifyStoredGraph(legacyCapture, rows,
            audits, messages, invoices, lines, receipts);
        return await InvoiceAmountInventoryEvidence.VerifyCoreHistoryAsync(supplement, verifyCore, ct);
    }
}
