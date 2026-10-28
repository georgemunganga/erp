using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>One table's complete, approved baseline set. Row digests include
/// original IDs and canonical values; a zero count is an explicit captured fact.</summary>
public sealed record InvoiceProcurementBaselineTable(string TableName, long RowCount,
    string IdSetSha256, string RowSetSha256);

/// <summary>Every retained allocation, including inactive history, in one
/// exposure's owning row set. Active must come from the current invoice pointer,
/// not from the caller or this DTO.</summary>
public sealed record InvoiceProcurementClaimCursor(Guid AllocationId, long ExposureSequence, bool Active);

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

public static class InvoiceProcurementHistoricalOwnerContract
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Check a producer's typed lease and disjoint active-claim partition.
    /// The producer must separately prove that the reviewed table manifest and
    /// complete row sets came from the owning database transaction. Passing this
    /// pure check is never authorization to set InvoiceHistoricalExposure.Complete.</summary>
    public static void VerifyPartition(InvoiceProcurementExposureOwnerEnvelope envelope,
        InvoiceExposureReadContext context, InvoiceMatchExposureState exposure,
        IReadOnlyList<string> approvedTableNames,
        IReadOnlyList<InvoiceMatchConsumption> activeBaselineClaims,
        IReadOnlyList<InvoiceMatchConsumption> activeLocalClaims)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(exposure);
        ArgumentNullException.ThrowIfNull(approvedTableNames);
        ArgumentNullException.ThrowIfNull(activeBaselineClaims);
        ArgumentNullException.ThrowIfNull(activeLocalClaims);
        var tables=envelope.BaselineTables;
        var names=approvedTableNames.Order(StringComparer.Ordinal).ToArray();
        if (envelope.SchemaVersion != 2 || envelope.TenantId != context.TenantId
            || envelope.LegalEntityId != context.LegalEntityId || envelope.BranchId != context.BranchId
            || envelope.PurchaseOrderId != context.PurchaseOrderId || envelope.IssuedVersionId != context.IssuedVersionId
            || envelope.SupplierId != context.SupplierId || envelope.CurrencyCode != context.CurrencyCode
            || envelope.PolicyRulesSha256 != context.PolicyRulesSha256
            || exposure.TenantId != context.TenantId || exposure.LegalEntityId != context.LegalEntityId
            || exposure.PurchaseOrderId != context.PurchaseOrderId || exposure.IssuedVersionId != context.IssuedVersionId
            || exposure.SupplierId != context.SupplierId || exposure.CurrencyCode != context.CurrencyCode
            || envelope.BaselineId == Guid.Empty || envelope.ExposureSequence != exposure.Sequence
            || envelope.ExposureSequence < 0 || envelope.ObservedAt > context.At
            || envelope.ValidUntil <= context.At || envelope.ValidUntil <= envelope.ObservedAt
            || string.IsNullOrWhiteSpace(envelope.OwnerReference) || string.IsNullOrWhiteSpace(envelope.Watermark)
            || tables is null || names.Length == 0 || names.Length != names.Distinct(StringComparer.Ordinal).Count()
            || tables.Count != names.Length || tables.Any(x => x is null)
            || !tables.Select(x => x.TableName).Order(StringComparer.Ordinal).SequenceEqual(names)
            || tables.Any(x => x.RowCount < 0 || !Sha(x.IdSetSha256) || !Sha(x.RowSetSha256))
            || !Sha(envelope.CurrentCompleteSetSha256)
            || !Sha(envelope.BaselineManifestSha256)
            || !Sha(envelope.BaselineSetSha256)
            || !Sha(envelope.ActiveBaselineClaimsSha256)
            || !Sha(envelope.ActiveLocalClaimsSha256)
            || !Sha(envelope.UnionClaimsSha256)
            || !Sha(envelope.AllocationsSha256))
            throw new InvalidOperationException("InvoiceProcurementOwnerEnvelopeInvalid");
        var manifest=ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
        { schemaVersion=1, tableNames=names },Json));
        var baseline=ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
        { schemaVersion=1, tables=tables.OrderBy(x=>x.TableName,StringComparer.Ordinal).ToArray() },Json));
        var union=activeBaselineClaims.Concat(activeLocalClaims).ToArray();
        var allocationTable=tables.SingleOrDefault(x=>x.TableName=="invoice_match_allocations");
        if (union.Any(x => x is null || x.Kind != InvoiceConsumptionKind.Debit
                || x.TenantId != context.TenantId || x.LegalEntityId != context.LegalEntityId
                || x.BranchRef != context.BranchId || x.PurchaseOrderId != context.PurchaseOrderId
                || x.RevisionId != context.IssuedVersionId || x.SupplierId != context.SupplierId
                || x.CurrencyCode != context.CurrencyCode)
            || union.Select(x=>x.Id).Distinct().Count()!=union.Length
            || union.Select(x=>(x.InvoiceId,x.InvoiceLineId)).Distinct().Count()!=union.Length
            || allocationTable is null || allocationTable.RowCount < activeBaselineClaims.Count)
            throw new InvalidOperationException("InvoiceProcurementOwnerClaimOverlap");
        if (envelope.BaselineManifestSha256 != manifest || envelope.BaselineSetSha256 != baseline
            || envelope.ActiveBaselineClaimsSha256 != InvoiceMatchExecutionEvidence.AllocationsDigest(activeBaselineClaims)
            || envelope.ActiveLocalClaimsSha256 != InvoiceMatchExecutionEvidence.AllocationsDigest(activeLocalClaims)
            || envelope.AllocationsSha256 != envelope.ActiveBaselineClaimsSha256
            || envelope.UnionClaimsSha256 != InvoiceMatchExecutionEvidence.AllocationsDigest(union))
            throw new InvalidOperationException("InvoiceProcurementOwnerSetDigestChanged");
    }

    /// <summary>Detect a lost or unexplained exposure increment. Multiple invoice
    /// lines may share one sequence; every sequence must have at least one row.
    /// The owning adapter must derive Active from retained invoice pointers.</summary>
    public static void VerifyHistoricalSequence(long exposureSequence,
        IReadOnlyList<InvoiceProcurementClaimCursor> allRetainedRows,
        IReadOnlyList<InvoiceMatchConsumption> activeClaims)
    {
        ArgumentNullException.ThrowIfNull(allRetainedRows);
        ArgumentNullException.ThrowIfNull(activeClaims);
        if (exposureSequence < 0 || allRetainedRows.Count > 100_000
            || allRetainedRows.Any(x => x is null || x.AllocationId == Guid.Empty
                || x.ExposureSequence < 1 || x.ExposureSequence > exposureSequence)
            || allRetainedRows.Select(x => x.AllocationId).Distinct().Count() != allRetainedRows.Count
            || activeClaims.Any(x => x is null || x.Id == Guid.Empty)
            || activeClaims.Select(x => x.Id).Distinct().Count() != activeClaims.Count
            || !allRetainedRows.Where(x => x.Active).Select(x => x.AllocationId).Order()
                .SequenceEqual(activeClaims.Select(x => x.Id).Order())
            || allRetainedRows.Select(x => x.ExposureSequence).Distinct().Order()
                .Where((sequence,index) => sequence != index + 1).Any()
            || allRetainedRows.Select(x => x.ExposureSequence).Distinct().LongCount() != exposureSequence)
            throw new InvalidOperationException("InvoiceProcurementExposureSequenceIncomplete");
    }

    private static bool Sha(string? value) => value is {Length:64}
        && value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');
}
