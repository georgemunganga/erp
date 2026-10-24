namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Proposal only. This record never changes received, accepted, stock or payable balances.</summary>
public sealed class PrincipalGoodsReturnDraft : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid BranchId { get; init; }
    public Guid SourceStateId { get; init; }
    public Guid AcceptanceFactId { get; init; }
    public Guid ReceiptId { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public Guid PurchaseOrderLineId { get; init; }
    public Guid IssuedVersionId { get; init; }
    public string SourceSha256 { get; init; } = null!;
    public string AcceptanceSha256 { get; init; } = null!;
    public decimal AcceptedFactQuantity { get; init; }
    public string CreatorPrincipalId { get; init; } = null!;
    public Guid? CreatorWorkerAliasId { get; init; }
    public long Revision { get; set; } = 1;
    public string State { get; set; } = "Draft";
    public Guid CurrentRevisionId { get; set; }
}

/// <summary>Immutable exact proposal revision; cancellations retain the last proposed quantity.</summary>
public sealed class PrincipalGoodsReturnRevision : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid DraftId { get; init; }
    public long Revision { get; init; }
    public Guid CommandKey { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public decimal Quantity { get; init; }
    public string Reason { get; init; } = null!;
    public string? EvidenceReference { get; init; }
    public string State { get; init; } = "Draft";
    public string ProposalSha256 { get; init; } = null!;
}

/// <summary>Actor-owned durable result for lost response recovery. Never an approval receipt.</summary>
public sealed class PrincipalGoodsReturnCommandReceipt : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid DraftId { get; init; }
    public Guid BranchId { get; init; }
    public Guid CommandKey { get; init; }
    public string Operation { get; init; } = null!;
    public string ActorPrincipalId { get; init; } = null!;
    public string RequestSha256 { get; init; } = null!;
    public string ResponseJson { get; init; } = null!;
    public string ResponseSha256 { get; init; } = null!;
    public int ResponseStatus { get; init; }
    public long ResultRevision { get; init; }
}
