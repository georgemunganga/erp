namespace Mightyfin.Erp.Procurement.Domain;

public enum ReceivingKind { Goods = 1, Service = 2 }
public enum AcceptanceEffectKind { Acceptance = 1, AdministrativeReversal = 2, ReturnDispatched = 3 }
public enum ReceivingOwnerCorrectionState { Unknown = 0, Unprocessed = 1, CompensationConfirmed = 2, Blocked = 3 }

public sealed record ReceivingCorrectionPin(
    long AcceptanceSequence, string AcceptanceEffectsSha256, string ConsumptionEvidenceReference,
    decimal ConsumedQuantity, decimal ConsumedNetAmount, ReceivingOwnerCorrectionState OwnerState,
    Guid CorrectionEffectKey, string OwnerEvidenceReference, Guid RequesterWorkerId, Guid AuthorizerWorkerId,
    string AuthorityDecisionReference, decimal AuthorizedQuantity, string? DispatchEvidenceReference);

public enum ReceivingInspectionOutcome { Passed = 1, PartiallyPassed = 2, Rejected = 3 }

/// <summary>Immutable business pin. The resolver proves delivery, classification and source lineage;
/// this is not a new stock, asset, identity or Finance master.</summary>
public sealed record ReceivingSourcePin(
    string TenantId, Guid LegalEntityId, Guid? BranchId, Guid PurchaseOrderId, Guid IssuedVersionId,
    Guid PurchaseOrderLineId, Guid PurchaseRequestLineId, int IssuedRevision,
    string IssuedSnapshotSha256, string SourceEvidenceSha256, string ClassificationReference,
    string ClassificationSha256, ReceivingKind Kind, string CategoryCode, string PurchaseType,
    string CurrencyCode, string UnitCode, decimal AuthorizedQuantity, decimal NetUnitPrice,
    string NetPriceBasis);

/// <summary>Final physical observation. Receipt does not imply acceptance or downstream posting.
/// Future persistence must enforce append-only alongside the existing receipt draft records.</summary>
public sealed class GoodsPhysicalObservation : ProcurementRecord
{
    public Guid GoodsReceiptId { get; init; }
    public Guid GoodsReceiptLineId { get; init; }
    public ReceivingSourcePin Source { get; init; } = null!;
    public Guid SiteId { get; init; }
    public decimal ReceivedQuantity { get; init; }
    public decimal InitiallyRejectedQuantity { get; init; }
    public Guid ReceiverWorkerId { get; init; }
    public Guid ReceivingPolicyVersionId { get; init; }
    public string EvidenceReference { get; init; } = null!;
    public long LedgerSequence { get; init; }
    public Guid EffectKey { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}

/// <summary>One final disposition per physical observation in the initial bounded contract.
/// Reinspection/correction of disposition requires a later explicit compensating contract.</summary>
public sealed class GoodsInspectionEvidence : ProcurementRecord
{
    public Guid PhysicalObservationId { get; init; }
    public Guid GoodsReceiptLineId { get; init; }
    public ReceivingSourcePin Source { get; init; } = null!;
    public Guid InspectorWorkerId { get; init; }
    public Guid ReceivingPolicyVersionId { get; init; }
    public string ChecklistReference { get; init; } = null!;
    public ReceivingInspectionOutcome Outcome { get; init; }
    public decimal InspectedQuantity { get; init; }
    public decimal QualifiedQuantity { get; init; }
    public string EvidenceReference { get; init; } = null!;
    public long LedgerSequence { get; init; }
    public Guid EffectKey { get; init; }
    public DateTimeOffset InspectedAt { get; init; }
}

/// <summary>Signed entitlement effect; negatives link one exact positive fragment. No effect for
/// a pending return. Existing receipt-line/service-certificate IDs remain matcher identities.</summary>
public sealed partial class ReceivingAcceptanceEffect : ProcurementRecord
{
    public ReceivingSourcePin Source { get; init; } = null!;
    public Guid AcceptanceRecordId { get; init; }
    public Guid? PhysicalObservationId { get; init; }
    public Guid? InspectionEvidenceId { get; init; }
    public Guid? OriginalAcceptanceEffectId { get; init; }
    public ReceivingCorrectionPin? CorrectionEvidence { get; init; }
    public AcceptanceEffectKind Kind { get; init; }
    public decimal QuantityDelta { get; init; }
    public decimal NetAmountDelta { get; init; }
    public Guid ActorWorkerId { get; init; }
    public Guid ReceivingPolicyVersionId { get; init; }
    public string EvidenceReference { get; init; } = null!;
    public string? Reason { get; init; }
    public string? DeliverableReference { get; init; }
    public DateOnly? PeriodFrom { get; init; }
    public DateOnly? PeriodTo { get; init; }
    public long LedgerSequence { get; init; }
    public Guid EffectKey { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}
