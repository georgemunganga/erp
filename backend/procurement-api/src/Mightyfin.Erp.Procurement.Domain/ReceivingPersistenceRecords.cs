namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Concurrency/projection state for one sealed issued line; not a receipt or stock master.</summary>
public sealed class ReceivingSourceState : ProcurementRecord
{
    public int ExecutionVersion { get; init; } = 1;
    public Guid PurchaseOrderId { get; init; }
    public Guid PurchaseOrderLineId { get; init; }
    public Guid SubmissionId { get; init; }
    public Guid IssuedVersionId { get; init; }
    public Guid DeliveryEvidenceId { get; init; }
    public ReceivingSourcePin Source { get; init; } = null!;
    public string SourceSha256 { get; init; } = null!;
    public string ClassificationJson { get; init; } = null!;
    public Guid ClassificationPolicyVersionId { get; init; }
    public long Sequence { get; set; }
    public decimal CurrentAcceptedQuantity { get; set; }
    public decimal CurrentAcceptedNetAmount { get; set; }
}

public sealed partial class GoodsReceipt
{
    public int ExecutionVersion { get; set; }
    public Guid? IssuedVersionId { get; set; }
    public Guid? SubmissionId { get; set; }
    // Guid.Empty is legacy *missing* identity, not a guessed issued version. It permits
    // a nonnullable composite header/line key without retrofitting nullable legacy pins.
    public Guid IssuedBindingId { get; set; }
    public Guid? BranchRef { get; set; }
    public Guid? ReceiverWorkerId { get; set; }
    public DateTimeOffset? RecordedAt { get; set; }
    public string? Notes { get; set; }
}
public sealed partial class GoodsReceiptLine
{
    public int ExecutionVersion { get; set; }
    public Guid? ReceivingSourceStateId { get; set; }
    public Guid SourceStateBindingId { get; set; }
    public Guid? SourcePurchaseOrderId { get; set; }
    public Guid IssuedBindingId { get; set; }
    public decimal InitiallyRejectedQuantity { get; set; }
    public Guid? PhysicalObservationId { get; set; }
    public Guid PhysicalBindingId { get; set; }
    public Guid? PhysicalPolicyVersionId { get; set; }
    public string? PhysicalEvidenceRef { get; set; }
    public long? PhysicalSequence { get; set; }
    public Guid? PhysicalEffectKey { get; set; }
    public DateTimeOffset? PhysicalRecordedAt { get; set; }
}
public sealed partial class ReceiptInspection
{
    public Guid PhysicalBindingId { get; set; }
    public int ExecutionVersion { get; set; }
    public Guid? PhysicalObservationId { get; set; }
    public Guid? ReceivingSourceStateId { get; set; }
    public Guid SourceStateBindingId { get; set; }
    public decimal QualifiedQuantity { get; set; }
    public Guid? InspectorWorkerId { get; set; }
    public Guid? PolicyVersionId { get; set; }
    public string? EvidenceRef { get; set; }
    public long? LedgerSequence { get; set; }
    public Guid? EffectKey { get; set; }
}
public sealed partial class ServiceAcceptance
{
    public int ExecutionVersion { get; set; }
    public Guid? ReceivingSourceStateId { get; set; }
    public Guid SourceStateBindingId { get; set; }
    public Guid? SourcePurchaseOrderId { get; set; }
    public Guid IssuedBindingId { get; set; }
    public Guid? OwnerWorkerId { get; set; }
    public Guid? PolicyVersionId { get; set; }
    public string? EvidenceRef { get; set; }
    public Guid? CertificationEffectId { get; set; }
    public decimal CurrentAcceptedQuantity { get; set; }
    public decimal CurrentAcceptedNetAmount { get; set; }
}
public sealed partial class ReceivingAcceptanceEffect
{
    public Guid ReceivingSourceStateId { get; init; }
    public Guid? GoodsReceiptLineId { get; init; }
    public Guid? ServiceAcceptanceId { get; init; }
}
