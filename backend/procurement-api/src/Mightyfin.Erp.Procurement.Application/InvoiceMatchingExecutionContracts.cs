using System.Text.Json.Serialization;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record InvoiceMappingLine(Guid InvoiceLineId, Guid PurchaseOrderLineId,
    Guid? GoodsReceiptLineId, Guid? ServiceAcceptanceId, string InvoiceUnitCode,
    Guid DocumentVersionId, string DocumentLineReference);
public sealed record InvoiceMappingSnapshot(int SchemaVersion, Guid InvoiceId, Guid PurchaseOrderId,
    Guid IssuedVersionId, int Revision, string InvoiceOriginalSha256, IReadOnlyList<InvoiceMappingLine> Lines)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ActorPrincipalId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ActorWorkerId { get; init; }
}
public sealed record InvoiceMappingReview(bool Verified, string State, string EvidenceJson,
    IReadOnlyDictionary<Guid, string> VerifiedUnits)
{
    // Owning typed-v2 proof is distinct from legacy caller-bound JSON; never synthesize a v1 signer.
    public InvoiceRetainedUnitReviewAuthority? RetainedAuthority { get; init; }
    public InvoiceUnitReviewConsumer? CurrentConsumer { get; init; }
    public static InvoiceMappingReview Unavailable(string reason) => new(false, reason, "{}", new Dictionary<Guid, string>());
}
public sealed record InvoiceMappingReviewContext(string TenantId, Guid LegalEntityId, Guid? BranchId,
    string PrincipalId, Guid? WorkerId, Guid PolicyId, string PolicyRulesSha256, DateTimeOffset At,
    IReadOnlyDictionary<Guid, string> DocumentVersionHashes);
public sealed record InvoiceReviewedDocumentLine(Guid InvoiceLineId, Guid DocumentVersionId,
    string DocumentSha256, string DocumentLineReference, string UnitCode);
/// <summary>Human-entered unit attestation. It has no document identity or hash.</summary>
public sealed record InvoiceReviewedManualLine(Guid InvoiceLineId, string UnitCode);
public sealed record InvoiceMappingReviewEnvelope(int SchemaVersion, string TenantId, Guid LegalEntityId,
    Guid? BranchId, Guid InvoiceId, Guid MappingVersionId, string MappingSha256, string InvoiceOriginalSha256,
    Guid PurchaseOrderId, Guid IssuedVersionId, string PrincipalId, Guid WorkerId, Guid PolicyId,
    string PolicyRulesSha256, DateTimeOffset ReviewedAt, IReadOnlyList<InvoiceReviewedDocumentLine> Lines);
public sealed record InvoiceOwnerExposureEnvelope(int SchemaVersion, string TenantId, Guid LegalEntityId,
    Guid? BranchId, Guid PurchaseOrderId, Guid IssuedVersionId, Guid SupplierId, string CurrencyCode,
    string PolicyRulesSha256, string OwnerReference, string Watermark, DateTimeOffset ValidUntil,
    string AllocationsSha256);
/// <summary>The Procurement-owned checkpoint binds one independently reviewed
/// immutable baseline to a complete current inventory and the old-claim
/// partition. Its producer must verify the Core decision and full live set in
/// the comparison's serializable transaction before issuing this envelope.</summary>
public sealed record InvoiceProcurementOwnerExposureEnvelope(int SchemaVersion, string TenantId,
    Guid LegalEntityId, Guid? BranchId, Guid PurchaseOrderId, Guid IssuedVersionId,
    Guid SupplierId, string CurrencyCode, string PolicyRulesSha256, string OwnerReference,
    string Watermark, DateTimeOffset ValidUntil, string AllocationsSha256,
    Guid BaselineId, Guid CoreCaseId, string CatalogManifestSha256,
    string BaselineSetSha256, string CurrentSetSha256, string HistoricalClaimsSha256,
    string LocalClaimsSha256, string UnionClaimsSha256, long ExposureSequence);
public sealed record InvoiceActiveLocalClaim(InvoiceMatchAllocation Allocation, SupplierInvoice Invoice);
public sealed record InvoiceExposureReadContext(string TenantId, Guid LegalEntityId, Guid? BranchId,
    Guid PurchaseOrderId, Guid IssuedVersionId, Guid SupplierId, string CurrencyCode,
    string PolicyRulesSha256, DateTimeOffset At);

/// <summary>Only an authenticated owner adapter can issue a current, complete lease-backed
/// historical checkpoint. No public HTTP input accepts these authority/completeness fields.</summary>
public sealed record InvoiceHistoricalExposure(bool Complete, string State, string? OwnerReference,
    string? Watermark, string? EvidenceJson, string? EvidenceSha256, string? PolicyRulesSha256,
    DateTimeOffset? ValidUntil, IReadOnlyList<InvoiceMatchConsumption> Allocations)
{
    public static InvoiceHistoricalExposure Unavailable(string reason = "FinanceHistoryUnavailable")
        => new(false, reason, null, null, null, null, null, null, []);
}

/// <summary>Reads an already authenticated/reconciled owner checkpoint under the caller's
/// shared transaction/lease. Implementations must not make network calls while source locks
/// are held, or infer completeness from local records. Default implementation is unavailable.</summary>
public interface IInvoiceHistoricalExposureOwner
{
    Task<InvoiceHistoricalExposure> ReadAsync(InvoiceExposureReadContext context, CancellationToken ct);
}
public sealed class UnavailableInvoiceHistoricalExposureOwner : IInvoiceHistoricalExposureOwner
{
    public Task<InvoiceHistoricalExposure> ReadAsync(InvoiceExposureReadContext context, CancellationToken ct)
        => Task.FromResult(InvoiceHistoricalExposure.Unavailable());
}

/// <summary>A configured owning document-review adapter verifies the original invoice version
/// and every declared unit under governed policy. Mapping proposals are not verification.</summary>
public interface IInvoiceMappingEvidenceReviewer
{
    Task<InvoiceMappingReview> ReviewAsync(InvoiceMatchMappingVersion proposal, InvoiceMatchDocument original,
        InvoiceMappingReviewContext context, CancellationToken ct);
}
public sealed class UnavailableInvoiceMappingEvidenceReviewer : IInvoiceMappingEvidenceReviewer
{
    public Task<InvoiceMappingReview> ReviewAsync(InvoiceMatchMappingVersion proposal, InvoiceMatchDocument original,
        InvoiceMappingReviewContext context, CancellationToken ct)
        => Task.FromResult(InvoiceMappingReview.Unavailable("OriginalInvoiceUnitReviewUnavailable"));
}
