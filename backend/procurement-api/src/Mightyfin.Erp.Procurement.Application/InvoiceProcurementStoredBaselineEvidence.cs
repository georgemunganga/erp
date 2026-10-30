using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record InvoiceProcurementCurrentInventoryPartition(
    InvoiceProcurementInventorySummary CurrentSummary,
    IReadOnlyList<InvoiceProcurementClassifiedRow> ChangedMutableDraftRows,
    IReadOnlyList<InvoiceProcurementClassifiedRow> AddedRows);

/// <summary>Reconstructs the reviewed set from immutable persisted IDs and
/// digests, then reconciles every old ID against a complete current read in
/// the caller's serializable transaction. This is a prerequisite to, not a
/// substitute for, typed transition and Core decision verification.</summary>
public static class InvoiceProcurementStoredBaselineEvidence
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static InvoiceProcurementInventorySummary VerifyStored(
        InvoiceProcurementOwnerBaseline baseline,
        IReadOnlyList<InvoiceProcurementOwnerBaselineRow> rows)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(rows);
        var names = InvoiceProcurementOwnerTables.Names;
        if (baseline.SchemaVersion != 2 || baseline.Version != 1 || baseline.Id == Guid.Empty
            || string.IsNullOrWhiteSpace(baseline.TenantId) || baseline.LegalEntityId == Guid.Empty
            || baseline.CoreCaseId == Guid.Empty || baseline.CapturedAtUtc == default
            || string.IsNullOrWhiteSpace(baseline.CapturedThroughMigration)
            || !ProcurementPrincipalEvidence.Canonical(baseline.MakerPrincipalId)
            || !ProcurementPrincipalEvidence.OptionalWorker(baseline.MakerWorkerAliasId)
            // CapturedAtUtc is the business snapshot instant. The shared audit
            // pipeline stamps CreatedAt when the batch is saved, which may be later.
            || baseline.CreatedAt == default || baseline.CreatedAt.Offset != TimeSpan.Zero
            || baseline.CapturedAtUtc.Offset != TimeSpan.Zero
            || baseline.CreatedAt < baseline.CapturedAtUtc
            || baseline.CreatedBy != baseline.MakerPrincipalId
            || baseline.UpdatedAt is not null || baseline.UpdatedBy is not null
            || !Sha(baseline.InitialActiveClaimsSha256)
            || !Sha(baseline.InitialExposureSetSha256)
            || baseline.CatalogManifestJson != InvoiceProcurementBaselineEvidence.CatalogManifestJson()
            || baseline.InventoryRowCount != rows.Count || rows.Count > 100_000
            || rows.Any(x => x is null || x.BaselineId != baseline.Id
                || x.TenantId != baseline.TenantId || x.LegalEntityId != baseline.LegalEntityId
                || !names.Contains(x.TableName, StringComparer.Ordinal)
                || x.SourceRowId == Guid.Empty || x.SourceRowVersion < 1
                || x.Version != 1 || x.CreatedAt != baseline.CreatedAt
                || x.CreatedBy != baseline.MakerPrincipalId
                || x.UpdatedAt is not null || x.UpdatedBy is not null
                || x.InitialClassification is not ("ClaimBearing" or "HistoricalOnly" or "MutableDraft" or "BlockedAmbiguity")
                || !Sha(x.OriginalRowSha256) || !Sha(x.ImmutableFieldsSha256))
            || rows.Select(x => (x.TableName, x.SourceRowId)).Distinct().Count() != rows.Count)
            throw new InvalidOperationException("InvoiceOwnerStoredBaselineChanged");
        var manifest = ProcurementEvidenceEncoding.PolicyJsonDigest(baseline.CatalogManifestJson);
        if (manifest != baseline.CatalogManifestSha256)
            throw new InvalidOperationException("InvoiceOwnerStoredBaselineChanged");
        var summaries = new List<InvoiceProcurementBaselineTable>(names.Count);
        foreach (var name in names)
        {
            var ordered = rows.Where(x => x.TableName == name).OrderBy(x => x.SourceRowId).ToArray();
            var ids = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
                { schemaVersion = 1, tableName = name, ids = ordered.Select(x => x.SourceRowId).ToArray() }, Json));
            var rowSet = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
            {
                schemaVersion = 1, tableName = name,
                rows = ordered.Select(x => new
                {
                    Id = x.SourceRowId, Version = x.SourceRowVersion,
                    CanonicalRowSha256 = x.OriginalRowSha256
                }).ToArray()
            }, Json));
            summaries.Add(new(name, ordered.Length, ids, rowSet));
        }
        var complete = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
            { schemaVersion = 1, tables = summaries.ToArray() }, Json));
        if (complete != baseline.InventorySetSha256)
            throw new InvalidOperationException("InvoiceOwnerStoredBaselineChanged");
        return new(baseline.TenantId, baseline.LegalEntityId, manifest, summaries,
            complete, rows.Count);
    }

    public static InvoiceProcurementCurrentInventoryPartition ReconcileCurrent(
        InvoiceProcurementOwnerBaseline baseline,
        IReadOnlyList<InvoiceProcurementOwnerBaselineRow> rows,
        InvoiceProcurementInventoryCapture current)
    {
        _ = VerifyStored(baseline, rows);
        current = InvoiceProcurementIssuedCanonicalRow.ProjectCapture(
            InvoiceProcurementAllocationCanonicalRow.ProjectCapture(current));
        var summary = InvoiceProcurementInventoryEvidence.Summarize(current);
        if (current.TenantId != baseline.TenantId || current.LegalEntityId != baseline.LegalEntityId
            || summary.ManifestSha256 != baseline.CatalogManifestSha256)
            throw new InvalidOperationException("InvoiceOwnerCurrentInventoryChanged");
        var old = rows.ToDictionary(x => (x.TableName, x.SourceRowId));
        var actual = current.Tables.SelectMany(t => t.Rows.Select(r => (t.TableName, Row: r)))
            .ToDictionary(x => (x.TableName, x.Row.Id));
        if (old.Keys.Any(key => !actual.ContainsKey(key)))
            throw new InvalidOperationException("InvoiceOwnerOldRowMissing");
        var changed = new List<InvoiceProcurementClassifiedRow>();
        var added = new List<InvoiceProcurementClassifiedRow>();
        foreach (var (key, item) in actual)
        {
            if (!old.TryGetValue(key, out var stored))
            {
                added.Add(new(item.TableName, item.Row.Id, "PostBaseline", null, null));
                continue;
            }
            var immutable = InvoiceProcurementImmutableFields.Digest(item.TableName, item.Row.RowJson);
            if (immutable != stored.ImmutableFieldsSha256
                || item.Row.Version < stored.SourceRowVersion)
                throw new InvalidOperationException("InvoiceOwnerImmutableRowChanged");
            if (item.Row.CanonicalRowSha256 == stored.OriginalRowSha256)
            {
                if (item.Row.Version != stored.SourceRowVersion)
                    throw new InvalidOperationException("InvoiceOwnerRowVersionWithoutContentChange");
                continue;
            }
            if (stored.InitialClassification != "MutableDraft"
                || stored.TableName is not ("supplier_invoices" or "manual_payment_requests")
                || item.Row.Version <= stored.SourceRowVersion)
                throw new InvalidOperationException("InvoiceOwnerUnprovenHistoricalTransition");
            changed.Add(new(stored.TableName, stored.SourceRowId, stored.InitialClassification,
                stored.SupplierInvoiceId, stored.PurchaseOrderId));
        }
        return new(summary, changed, added);
    }

    /// <summary>Determines only whether the captured old partition can be zero
    /// for this order. Post-baseline rows and current claims require separate
    /// typed verification. Unlinked old credits retain their supplier/currency
    /// reach, so a clean PO is never cleared from IDs alone.</summary>
    public static bool NoRetainedExposureForOrder(
        InvoiceProcurementOwnerBaseline baseline,
        IReadOnlyList<InvoiceProcurementOwnerBaselineRow> rows,
        InvoiceProcurementInventoryCapture current,
        Guid purchaseOrderId, Guid supplierId, string currencyCode)
    {
        if (purchaseOrderId == Guid.Empty || supplierId == Guid.Empty
            || currencyCode is not { Length: 3 }
            || currencyCode.Any(c => c is < 'A' or > 'Z'))
            throw new InvalidOperationException("InvoiceOwnerOrderScopeInvalid");
        _ = ReconcileCurrent(baseline, rows, current);
        InvoiceProcurementBaselineEvidence.RequireResolvedClassificationForOrder(
            baseline, rows, purchaseOrderId);
        var linkedInvoices = rows.Where(x => x.PurchaseOrderId == purchaseOrderId
            && x.SupplierInvoiceId is not null)
            .Select(x => x.SupplierInvoiceId!.Value).ToHashSet();
        // A captured, unprocessed invoice and its original lines have no
        // capacity claim. Their MutableDraft classification was established
        // from the complete opening set; current pointers and reservations
        // still require separate typed replay in the owner factory.
        if (rows.Any(x => (x.PurchaseOrderId == purchaseOrderId
            || x.SupplierInvoiceId is { } invoiceId && linkedInvoices.Contains(invoiceId))
            && !(x.InitialClassification == "MutableDraft"
                && x.TableName is "supplier_invoices" or "supplier_invoice_lines")))
            return false;
        var credits = current.Tables.Single(x => x.TableName == "supplier_credit_note_refs")
            .Rows.ToDictionary(x => x.Id);
        foreach (var row in rows.Where(x => x.TableName == "supplier_credit_note_refs"
            && x.SupplierInvoiceId is null))
        {
            if (!credits.TryGetValue(row.SourceRowId, out var credit)
                || credit.CanonicalRowSha256 != row.OriginalRowSha256
                || credit.Version != row.SourceRowVersion)
                throw new InvalidOperationException("InvoiceOwnerHistoricalCreditChanged");
            using var parsed = JsonDocument.Parse(credit.RowJson);
            var source = parsed.RootElement;
            if (!source.TryGetProperty("supplier_id", out var supplier)
                || !source.TryGetProperty("currency_code", out var currency)
                || supplier.ValueKind != JsonValueKind.String
                || currency.ValueKind != JsonValueKind.String
                || !Guid.TryParse(supplier.GetString(), out var creditSupplier)
                || creditSupplier == Guid.Empty)
                throw new InvalidOperationException("InvoiceOwnerHistoricalCreditUnclassifiable");
            var creditCurrency = currency.GetString();
            if (creditCurrency is not { Length: 3 }
                || creditCurrency.Any(c => c is < 'A' or > 'Z'))
                throw new InvalidOperationException("InvoiceOwnerHistoricalCreditUnclassifiable");
            if (creditSupplier == supplierId
                && creditCurrency == currencyCode)
                return false;
        }
        return true;
    }

    private static bool Sha(string? value) => value is { Length: 64 }
        && value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');
}
