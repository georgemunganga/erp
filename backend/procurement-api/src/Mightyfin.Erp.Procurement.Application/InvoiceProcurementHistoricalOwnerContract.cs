namespace Mightyfin.Erp.Procurement.Application;

/// <summary>One table's complete, approved baseline set. Row digests include
/// original IDs and canonical values; a zero count is an explicit captured fact.</summary>
public sealed record InvoiceProcurementBaselineTable(string TableName, long RowCount,
    string IdSetSha256, string RowSetSha256);

/// <summary>Proposed Procurement owner schema, distinct from the installed v1
/// owner envelope. This DTO alone grants no completeness: an owning database
/// adapter must validate the reviewed table manifest and every live row under
/// the matching transaction before it may return Complete=true.</summary>
public sealed record InvoiceProcurementExposureOwnerEnvelope(
    int SchemaVersion, string TenantId, Guid LegalEntityId, Guid? BranchId,
    Guid PurchaseOrderId, Guid IssuedVersionId, Guid SupplierId, string CurrencyCode,
    string PolicyRulesSha256, string OwnerReference, string Watermark,
    Guid BaselineId, string BaselineManifestSha256,
    IReadOnlyList<InvoiceProcurementBaselineTable> BaselineTables,
    string BaselineSetSha256, string CurrentCompleteSetSha256,
    string ActiveBaselineClaimsSha256, string ActiveLocalClaimsSha256,
    string UnionClaimsSha256, long ExposureSequence,
    DateTimeOffset ObservedAt, DateTimeOffset ValidUntil,
    string AllocationsSha256);
