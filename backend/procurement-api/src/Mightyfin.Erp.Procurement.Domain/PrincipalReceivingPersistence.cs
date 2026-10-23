namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Immutable source envelope with a concurrency-controlled ledger cursor.
/// This is Procurement-owned acceptance history, never an inventory or Finance ledger.</summary>
public sealed class PrincipalReceivingSourceState : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid BranchId { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public Guid PurchaseOrderLineId { get; init; }
    public Guid IssuedVersionId { get; init; }
    public Guid SubmissionId { get; init; }
    public Guid FundingEvidenceId { get; init; }
    public Guid AcknowledgmentId { get; init; }
    public Guid PurchaseRequestLineId { get; init; }
    public Guid SourceBindingId { get; init; }
    public string OriginalSourceSha256 { get; init; } = null!;
    public string IssuedSnapshotSha256 { get; init; } = null!;
    public string CategoryCode { get; init; } = null!;
    public string PurchaseType { get; init; } = null!;
    public string Kind { get; init; } = null!;
    public string CurrencyCode { get; init; } = null!;
    public string SourceJson { get; init; } = null!;
    public string SourceSha256 { get; init; } = null!;
    public long Sequence { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal AcceptedNetAmount { get; set; }
}

/// <summary>Append-only normalized fact. Payload retains the complete typed original
/// observation/inspection/acceptance, including optional historical worker aliases.</summary>
public sealed class PrincipalReceivingFact : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid SourceStateId { get; init; }
    public Guid DraftLineBindingId { get; init; }
    public Guid DraftId { get; init; }
    public Guid BranchId { get; init; }
    public string Kind { get; init; } = null!;
    public long Sequence { get; init; }
    public Guid CommandKey { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public Guid? ActorWorkerAliasId { get; init; }
    public Guid PolicyVersionId { get; init; }
    public decimal Quantity { get; init; }
    public decimal NetAmount { get; init; }
    public string PayloadJson { get; init; } = null!;
    public string PayloadSha256 { get; init; } = null!;
}

/// <summary>Small CAS header; each complete draft revision is separately retained.</summary>
public sealed class PrincipalReceivingDraftHead : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid BranchId { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public Guid IssuedVersionId { get; init; }
    public string Kind { get; init; } = null!;
    public string CreatorPrincipalId { get; init; } = null!;
    public Guid? CreatorWorkerAliasId { get; init; }
    public long Revision { get; set; } = 1;
    public string State { get; set; } = "Draft";
    public string SnapshotSha256 { get; set; } = null!;
}

public sealed class PrincipalReceivingDraftRevision : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid DraftId { get; init; }
    public long Revision { get; init; }
    public Guid CommandKey { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public string SnapshotJson { get; init; } = null!;
    public string SnapshotSha256 { get; init; } = null!;
}

/// <summary>RFQ/PO receipts are not reused: this owned receipt always references a real
/// receiving draft. Exact original response bytes survive policy/account changes.</summary>
public sealed class PrincipalReceivingCommandReceipt : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid DraftId { get; init; }
    public Guid BranchId { get; init; }
    public string Operation { get; init; } = null!;
    public Guid CommandKey { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public string RequestSha256 { get; init; } = null!;
    public string ResponseJson { get; init; } = null!;
    public string ResponseSha256 { get; init; } = null!;
    public int ResponseStatus { get; init; }
    public long ResultRevision { get; init; }
}

/// <summary>Append-only normalized membership for one immutable revision. The logical
/// line survives revisions; EF relationships use the stable row UUID, never the CAS revision.</summary>
public sealed class PrincipalReceivingDraftLineBinding : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid DraftId { get; init; }
    public Guid DraftRevisionId { get; init; }
    public Guid LineId { get; init; }
    public Guid SourceStateId { get; init; }
    public Guid BranchId { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public Guid IssuedVersionId { get; init; }
    public Guid PurchaseOrderLineId { get; init; }
    public decimal Quantity { get; init; }
    public decimal RejectedQuantity { get; init; }
}
