namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Immutable complete line mapping proposal/review. Original invoice amounts are never rewritten.</summary>
public sealed class InvoiceMatchMappingVersion : ProcurementRecord
{
    public Guid SupplierInvoiceId { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public Guid IssuedVersionId { get; init; }
    public int MappingRevision { get; init; }
    public string InvoiceOriginalSha256 { get; init; } = null!;
    public string MappingJson { get; init; } = null!;
    public string MappingSha256 { get; init; } = null!;
    public string VerificationState { get; init; } = "Unverified";
    public string VerificationEvidenceJson { get; init; } = "{}";
    public string ActorPrincipalId { get; init; } = null!;
    public int IdentitySchemaVersion { get; init; } = 1;
    /// <summary>Null on retained v1/v2 rows. Version 3 is the separate principal
    /// receiving source payload; it does not change Core identity schema 2.</summary>
    public int? SourceSchemaVersion { get; init; }
    public Guid? ActorWorkerId { get; init; }
    public DateTimeOffset ReviewedAt { get; init; }
}

/// <summary>Procurement billing-capacity claim, not a Finance liability or posting.
/// Immutable rows remain after supersession; SupplierInvoice.ActiveMatchClaimResultId selects live claims.</summary>
public sealed class InvoiceMatchAllocation : ProcurementRecord
{
    public Guid SupplierInvoiceId { get; init; }
    public Guid SupplierInvoiceLineId { get; init; }
    public Guid MatchResultId { get; init; }
    public Guid ExposureStateId { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public Guid IssuedVersionId { get; init; }
    public Guid PurchaseOrderLineId { get; init; }
    public Guid SupplierId { get; init; }
    public Guid? ReceivingSourceStateId { get; init; }
    public Guid? GoodsReceiptLineId { get; init; }
    public Guid? ServiceAcceptanceId { get; init; }
    public Guid? OriginalDebitId { get; init; }
    public string Kind { get; init; } = "Debit";
    public string CurrencyCode { get; init; } = null!;
    public string UnitCode { get; init; } = null!;
    public decimal Quantity { get; init; }
    public decimal NetAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public long ExposureSequence { get; init; }
    public string ValidationReference { get; init; } = null!;
    public string OwnerCoverageReference { get; init; } = null!;
}

/// <summary>One serialized source exposure checkpoint. Unknown is a real unavailable state;
/// a local empty allocation table never establishes complete Finance/AP history.</summary>
public sealed class InvoiceMatchExposureState : ProcurementRecord
{
    public Guid PurchaseOrderId { get; init; }
    public Guid IssuedVersionId { get; init; }
    public Guid SupplierId { get; init; }
    public string CurrencyCode { get; init; } = null!;
    public long Sequence { get; set; }
    public string CoverageState { get; set; } = "Unknown";
    public string? OwnerReference { get; set; }
    public string? OwnerWatermark { get; set; }
    public string? OwnerEvidenceSha256 { get; set; }
    public DateTimeOffset? OwnerValidUntil { get; set; }
}
