namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Immutable company inventory opened as the Core case SourceId.
/// Core, rather than fields on this header, records approval or rejection.</summary>
public sealed class InvoiceProcurementOwnerBaseline : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public string CapturedThroughMigration { get; init; } = "";
    public DateTimeOffset CapturedAtUtc { get; init; }
    public string CatalogManifestJson { get; init; } = "";
    public string CatalogManifestSha256 { get; init; } = "";
    public string InventorySetSha256 { get; init; } = "";
    public string InitialActiveClaimsSha256 { get; init; } = "";
    public long InitialExposureSequence { get; init; }
    public int InventoryRowCount { get; init; }
    public Guid CoreCaseId { get; init; }
    public string MakerPrincipalId { get; init; } = "";
    public Guid? MakerWorkerAliasId { get; init; }
}

/// <summary>Immutable inventory for one old row. A mutable Draft may later
/// transition if the owner validates the complete typed transition chain.</summary>
public sealed class InvoiceProcurementOwnerBaselineRow : ProcurementRecord
{
    public Guid BaselineId { get; init; }
    public string TableName { get; init; } = "";
    public Guid SourceRowId { get; init; }
    public long SourceRowVersion { get; init; }
    public string OriginalRowSha256 { get; init; } = "";
    public string ImmutableFieldsSha256 { get; init; } = "";
    public string InitialClassification { get; init; } = "";
    public Guid? SupplierInvoiceId { get; init; }
    public Guid? PurchaseOrderId { get; init; }
}
