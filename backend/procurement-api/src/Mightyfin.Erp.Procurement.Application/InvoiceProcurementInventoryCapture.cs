namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Server-read rows from one transaction. Raw JSON stays in process for
/// typed classification; only reviewed digests and identifiers are persisted.</summary>
public sealed record InvoiceProcurementCapturedRow(Guid Id, long Version,
    string CanonicalRowSha256, string RowJson);

public sealed record InvoiceProcurementCapturedTable(string TableName,
    IReadOnlyList<InvoiceProcurementCapturedRow> Rows);

public sealed record InvoiceProcurementInventoryCapture(string TenantId,
    Guid LegalEntityId, IReadOnlyList<InvoiceProcurementCapturedTable> Tables);

public sealed record InvoiceProcurementInventorySummary(string TenantId, Guid LegalEntityId,
    string ManifestSha256, IReadOnlyList<InvoiceProcurementBaselineTable> Tables,
    string CompleteSetSha256, int RowCount);
