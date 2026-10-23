namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>One invoice's schema-2 commercial workflow pointer. The original
/// SupplierInvoice remains the only invoice master and its legacy mapping/result
/// pointers are never overloaded with principal PO identifiers.</summary>
public sealed class PrincipalInvoiceState : ProcurementRecord
{
    public Guid SupplierInvoiceId { get; init; }
    public Guid BranchId { get; init; }
    public Guid SupplierId { get; init; }
    public string CurrencyCode { get; init; } = null!;
    public long InvoiceVersion { get; set; }
    public int MappingRevision { get; set; }
    public Guid? CurrentMappingId { get; set; }
    public Guid? LastComparisonId { get; set; }
    public Guid? ActiveClaimComparisonId { get; set; }
    public string State { get; set; } = "Unmapped";
}

/// <summary>Append-only exact invoice line/document to principal issued PO2 and
/// optional positive receiving fact mapping. JSON retains complete ordered
/// membership; normalized parent columns independently bind the source owner.</summary>
public sealed class PrincipalInvoiceMappingVersion : ProcurementRecord
{
    public Guid SupplierInvoiceId { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public Guid PrincipalIssuedVersionId { get; init; }
    public Guid BranchId { get; init; }
    public Guid SupplierId { get; init; }
    public string CurrencyCode { get; init; } = null!;
    public long InvoiceVersion { get; init; }
    public int MappingRevision { get; init; }
    public string OriginalInvoiceSha256 { get; init; } = null!;
    public string IssuedSnapshotSha256 { get; init; } = null!;
    public string MappingJson { get; init; } = null!;
    public string MappingSha256 { get; init; } = null!;
    public string MakerPrincipalId { get; init; } = null!;
    public Guid? MakerWorkerAliasId { get; init; }
    public DateTimeOffset MappedAt { get; init; }
}

/// <summary>Normalized immutable membership for one complete mapping version.
/// These stable UUIDs give database ownership checks a real target; the JSON
/// digest alone is not relational authority.</summary>
public sealed class PrincipalInvoiceMappingLine : ProcurementRecord
{
    public Guid MappingVersionId { get; init; }
    public Guid SupplierInvoiceId { get; init; }
    public Guid SupplierInvoiceLineId { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public Guid PrincipalIssuedVersionId { get; init; }
    public Guid PurchaseOrderLineId { get; init; }
    public Guid? PrincipalReceivingSourceStateId { get; init; }
    public Guid? PrincipalAcceptanceFactId { get; init; }
    public long? AcceptanceSequence { get; init; }
    public Guid DocumentVersionId { get; init; }
    public string DocumentLineReference { get; init; } = null!;
    public string InvoiceUnitCode { get; init; } = null!;
}

/// <summary>One Core case opened against this exact principal mapping and
/// retained original invoice/document units, never an HR employee approval.</summary>
public sealed class PrincipalInvoiceUnitReviewSource : ProcurementRecord
{
    public Guid SupplierInvoiceId { get; init; }
    public Guid MappingVersionId { get; init; }
    public Guid PrincipalIssuedVersionId { get; init; }
    public Guid BranchId { get; init; }
    public Guid CoreCaseId { get; init; }
    public Guid MatchingPolicyId { get; init; }
    public Guid ReviewPolicyId { get; init; }
    public string MappingSha256 { get; init; } = null!;
    public string OriginalInvoiceSha256 { get; init; } = null!;
    public string OriginalUnitsJson { get; init; } = null!;
    public string OriginalUnitsSha256 { get; init; } = null!;
    public string MakerPrincipalId { get; init; } = null!;
    public Guid? MakerWorkerAliasId { get; init; }
    public DateTimeOffset OpenedAt { get; init; }
}

public sealed class PrincipalInvoiceUnitReviewAssignment : ProcurementRecord
{
    public Guid ReviewSourceId { get; init; }
    public Guid CoreCaseId { get; init; }
    public Guid CoreAssignmentId { get; init; }
    public string ReviewerPrincipalId { get; init; } = null!;
    public Guid? ReviewerWorkerAliasId { get; init; }
}

public sealed class PrincipalInvoiceUnitReviewOutcome : ProcurementRecord
{
    public Guid ReviewSourceId { get; init; }
    public Guid CoreCaseId { get; init; }
    public Guid FinalDecisionId { get; init; }
    public string State { get; init; } = null!;
    public string EvidenceJson { get; init; } = null!;
    public string EvidenceSha256 { get; init; } = null!;
    public string ReviewerPrincipalId { get; init; } = null!;
    public Guid? ReviewerWorkerAliasId { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
}

/// <summary>Procurement-only comparison, never AP posting or payment authority.</summary>
public sealed class PrincipalInvoiceComparison : ProcurementRecord
{
    public Guid SupplierInvoiceId { get; init; }
    public Guid MappingVersionId { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public Guid PrincipalIssuedVersionId { get; init; }
    public Guid ReviewSourceId { get; init; }
    public Guid ReviewOutcomeId { get; init; }
    public Guid MatchingPolicyId { get; init; }
    public Guid ExposureStateId { get; init; }
    public long InvoiceVersion { get; init; }
    public string OriginalInvoiceSha256 { get; init; } = null!;
    public string PolicyRulesSha256 { get; init; } = null!;
    public string Outcome { get; init; } = null!;
    public string EvidenceJson { get; init; } = null!;
    public string EvidenceSha256 { get; init; } = null!;
    public string OwnerReference { get; init; } = null!;
    public string OwnerWatermark { get; init; } = null!;
    public string ActorPrincipalId { get; init; } = null!;
    public Guid? ActorWorkerAliasId { get; init; }
    public Guid CommandKey { get; init; }
    public DateTimeOffset ComparedAt { get; init; }
}

/// <summary>Serialized owner coverage cursor for one principal issued source.
/// Unknown is not zero consumption. Only an authenticated owner may attest
/// complete previous billing/credit history.</summary>
public sealed class PrincipalInvoiceExposureState : ProcurementRecord
{
    public Guid PurchaseOrderId { get; init; }
    public Guid PrincipalIssuedVersionId { get; init; }
    public Guid SupplierId { get; init; }
    public Guid BranchId { get; init; }
    public string CurrencyCode { get; init; } = null!;
    public long Sequence { get; set; }
    public string CoverageState { get; set; } = "Unknown";
    public string? OwnerReference { get; set; }
    public string? OwnerWatermark { get; set; }
    public string? OwnerEvidenceSha256 { get; set; }
    public DateTimeOffset? OwnerValidUntil { get; set; }
}

/// <summary>Append-only qualified capacity debit against one invoice line and,
/// for a permitted three-way category, one actual positive receiving fact.
/// This is not the stock balance, payable or settled amount.</summary>
public sealed class PrincipalInvoiceClaim : ProcurementRecord
{
    public Guid SupplierInvoiceId { get; init; }
    public Guid SupplierInvoiceLineId { get; init; }
    public Guid MappingVersionId { get; init; }
    public Guid ComparisonId { get; init; }
    public Guid ExposureStateId { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public Guid PrincipalIssuedVersionId { get; init; }
    public Guid PurchaseOrderLineId { get; init; }
    public Guid? PrincipalReceivingSourceStateId { get; init; }
    public Guid? PrincipalAcceptanceFactId { get; init; }
    public long? AcceptanceSequence { get; init; }
    public Guid SupplierId { get; init; }
    public string CurrencyCode { get; init; } = null!;
    public string UnitCode { get; init; } = null!;
    public decimal Quantity { get; init; }
    public decimal NetAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public long ExposureSequence { get; init; }
    public string OwnerCoverageReference { get; init; } = null!;
}

/// <summary>Exact actor-owned response, in the same transaction as its one
/// application fact. No command payload or sensitive document text is needed
/// for GET-only recovery.</summary>
public sealed class PrincipalInvoiceCommandReceipt : ProcurementRecord
{
    public Guid SupplierInvoiceId { get; init; }
    public string Operation { get; init; } = null!;
    public Guid CommandKey { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public Guid? ActorWorkerAliasId { get; init; }
    public Guid BranchId { get; init; }
    public string RequestSha256 { get; init; } = null!;
    public string ResponseJson { get; init; } = null!;
    public string ResponseSha256 { get; init; } = null!;
    public int ResponseStatus { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
}
