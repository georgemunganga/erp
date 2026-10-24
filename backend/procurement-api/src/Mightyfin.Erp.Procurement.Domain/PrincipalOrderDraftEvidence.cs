namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Immutable original identity and source pin for one internal direct PO line.</summary>
public sealed class PrincipalOrderSourceBinding : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid PurchaseOrderId { get; init; }
    public Guid PurchaseOrderLineId { get; init; }
    public Guid AllocationId { get; init; }
    public Guid PurchaseRequestId { get; init; }
    public Guid PurchaseRequestLineId { get; init; }
    public long RequestVersion { get; init; }
    public Guid SubmissionId { get; init; }
    public long SubmittedBusinessVersion { get; init; }
    public Guid ApprovalOutcomeId { get; init; }
    public Guid CoreApprovalCaseId { get; init; }
    public Guid CoreDecisionId { get; init; }
    public Guid FinanceSnapshotId { get; init; }
    public Guid MethodId { get; init; }
    public int MethodRevision { get; init; }
    public string MethodCode { get; init; } = null!;
    public string RequestSnapshotSha256 { get; init; } = null!;
    public string PolicyDecisionSha256 { get; init; } = null!;
    public string PolicyRulesSha256 { get; init; } = null!;
    public string FinanceSnapshotSha256 { get; init; } = null!;
    public string PrincipalIdentitySha256 { get; init; } = null!;
    public string BuyerPrincipalId { get; init; } = null!;
    public Guid? BuyerWorkerAliasId { get; init; }
    public Guid RequesterPrincipalId { get; init; }
    public Guid? RequesterWorkerAliasId { get; init; }
    public Guid BranchId { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitEstimate { get; init; }
    public decimal ProposedUnitPrice { get; init; }
    public decimal EstimatedValue { get; init; }
    public string CurrencyCode { get; init; } = null!;
    public string OriginalDirectSourceSha256 { get; set; } = null!;
}

/// <summary>Append-only response to one actor-owned Create or Cancel command.</summary>
public sealed class PrincipalOrderDraftReceipt : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid PurchaseOrderId { get; init; }
    public string Operation { get; init; } = null!;
    public Guid CommandKey { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public string CommandSha256 { get; init; } = null!;
    public string ResponseJson { get; init; } = null!;
    public string ResponseSha256 { get; init; } = null!;
    public int ResponseStatus { get; init; }
    public long ResultVersion { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
}
