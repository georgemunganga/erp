namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>One immutable copy of the verified, original P30 commercial state.
/// Existing P30 orders acquire this only on their first controlled edit.</summary>
public sealed class PrincipalOrderCommercialOrigin : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid PurchaseOrderId { get; init; }
    public string OriginalDirectSourceSha256 { get; init; } = null!;
    public string SnapshotJson { get; init; } = null!;
    public string SnapshotSha256 { get; init; } = null!;
    public string CapturedByPrincipalId { get; init; } = null!;
    public DateTimeOffset CapturedAt { get; init; }
    public string CaptureMode { get; init; } = null!; // Create or VerifiedFirstEdit.
}

/// <summary>Append-only effective commercial state after one unfunded Draft edit.</summary>
public sealed class PrincipalOrderDraftCommercialRevision : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid PurchaseOrderId { get; init; }
    public Guid OriginId { get; init; }
    public int RevisionNumber { get; init; }
    public Guid? PreviousRevisionId { get; init; }
    public string? PreviousSnapshotSha256 { get; init; }
    public string OriginalDirectSourceSha256 { get; init; } = null!;
    public string SnapshotJson { get; init; } = null!;
    public string SnapshotSha256 { get; init; } = null!;
    public string Reason { get; init; } = null!;
    public string ActorPrincipalId { get; init; } = null!;
    public Guid CommandKey { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}

/// <summary>The effective source quantity/value for one unchanged original binding.
/// Binding identity and its original amount are never edited.</summary>
public sealed class PrincipalOrderEffectiveSourceSlice : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid PurchaseOrderId { get; init; }
    public Guid RevisionId { get; init; }
    public Guid SourceBindingId { get; init; }
    public Guid PurchaseOrderLineId { get; init; }
    public Guid AllocationId { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal EstimatedValue { get; init; }
    public string CurrencyCode { get; init; } = null!;
}
