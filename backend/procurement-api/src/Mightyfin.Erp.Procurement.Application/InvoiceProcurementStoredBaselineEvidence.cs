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
            || baseline.CreatedAt != baseline.CapturedAtUtc
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
                || x.Version != 1 || x.CreatedAt != baseline.CapturedAtUtc
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
                || stored.TableName != "supplier_invoices"
                || item.Row.Version <= stored.SourceRowVersion)
                throw new InvalidOperationException("InvoiceOwnerUnprovenHistoricalTransition");
            changed.Add(new(stored.TableName, stored.SourceRowId, stored.InitialClassification,
                stored.SupplierInvoiceId, stored.PurchaseOrderId));
        }
        return new(summary, changed, added);
    }

    private static bool Sha(string? value) => value is { Length: 64 }
        && value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');
}
