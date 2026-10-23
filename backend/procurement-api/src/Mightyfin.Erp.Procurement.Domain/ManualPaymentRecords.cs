namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>
/// Procurement evidence of an externally executed payment. It is neither a payable,
/// a bank instruction nor a FinancePaymentStatusRef. Persistence is not registered yet.
/// </summary>
public sealed class ManualPaymentRequest : ProcurementRecord
{
    public Guid BranchId { get; set; }
    public Guid SupplierInvoiceId { get; set; }
    public Guid SupplierId { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Draft";
    public string CreatorPrincipalId { get; set; } = null!;
    public Guid? CreatorCoreLinkedWorkerId { get; set; }
    public string ApproverPrincipalId { get; set; } = null!;
    public Guid? ApproverCoreLinkedWorkerId { get; set; }
    public string RecorderPrincipalId { get; set; } = null!;
    public Guid? RecorderCoreLinkedWorkerId { get; set; }
    public string ConfirmerPrincipalId { get; set; } = null!;
    public Guid? ConfirmerCoreLinkedWorkerId { get; set; }
    public string? Purpose { get; set; }
    public string? Title { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string? ApprovedByPrincipalId { get; set; }
    public Guid? ApprovedByCoreLinkedWorkerId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateOnly? PaidOn { get; set; }
    public string? ExternalMethod { get; set; }
    public string? ExternalReference { get; set; }
    public decimal? RecordedAmount { get; set; }
    public string? RecordedByPrincipalId { get; set; }
    public Guid? RecordedByCoreLinkedWorkerId { get; set; }
    public DateTimeOffset? RecordedAt { get; set; }
    public string? ConfirmedByPrincipalId { get; set; }
    public Guid? ConfirmedByCoreLinkedWorkerId { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public Guid? EvidenceDocumentRefId { get; set; }
    public string? ClosureReason { get; set; }
}

/// <summary>Append-only business transition to persist atomically with the request and Core audit.</summary>
public sealed class ManualPaymentDecision : ProcurementRecord
{
    public Guid ManualPaymentRequestId { get; set; }
    public Guid BranchId { get; set; }
    public string Action { get; set; } = null!;
    public string FromStatus { get; set; } = null!;
    public string ToStatus { get; set; } = null!;
    public long FromVersion { get; set; }
    public long ToVersion { get; set; }
    public string ActorPrincipalId { get; set; } = null!;
    public Guid? ActorCoreLinkedWorkerId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? Reason { get; set; }
    public decimal? PreviousAmount { get; set; }
    public decimal? NewAmount { get; set; }
    public decimal? RecordedAmount { get; set; }
    public string? PreviousApproverPrincipalId { get; set; }
    public string? NewApproverPrincipalId { get; set; }
    public Guid? PreviousApproverCoreLinkedWorkerId { get; set; }
    public Guid? NewApproverCoreLinkedWorkerId { get; set; }
    public string? PreviousRecorderPrincipalId { get; set; }
    public string? NewRecorderPrincipalId { get; set; }
    public Guid? PreviousRecorderCoreLinkedWorkerId { get; set; }
    public Guid? NewRecorderCoreLinkedWorkerId { get; set; }
    public string? PreviousConfirmerPrincipalId { get; set; }
    public string? NewConfirmerPrincipalId { get; set; }
    public Guid? PreviousConfirmerCoreLinkedWorkerId { get; set; }
    public Guid? NewConfirmerCoreLinkedWorkerId { get; set; }
    public DateOnly? PaidOn { get; set; }
    public string? ExternalMethod { get; set; }
    public string? ExternalReference { get; set; }
    public Guid? EvidenceDocumentRefId { get; set; }
}

/// <summary>
/// Actor-scoped command receipt. The future owning store must enforce a unique
/// (tenant, company, actor, command key) constraint and retain the exact response.
/// </summary>
public sealed class ManualPaymentCommandReceipt : ProcurementRecord
{
    public Guid ManualPaymentRequestId { get; set; }
    public Guid BranchId { get; set; }
    public string ActorPrincipalId { get; set; } = null!;
    public Guid CommandKey { get; set; }
    public string Action { get; set; } = null!;
    public string RequestSha256 { get; set; } = null!;
    public string ResponseJson { get; set; } = null!;
    public long ResultVersion { get; set; }
}
