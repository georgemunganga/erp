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
    public string ApproverPrincipalId { get; set; } = null!;
    public string RecorderPrincipalId { get; set; } = null!;
    public string ConfirmerPrincipalId { get; set; } = null!;
    public string? Purpose { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string? ApprovedByPrincipalId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateOnly? PaidOn { get; set; }
    public string? ExternalMethod { get; set; }
    public string? ExternalReference { get; set; }
    public decimal? RecordedAmount { get; set; }
    public string? RecordedByPrincipalId { get; set; }
    public DateTimeOffset? RecordedAt { get; set; }
    public string? ConfirmedByPrincipalId { get; set; }
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
    public DateTimeOffset OccurredAt { get; set; }
    public string? Reason { get; set; }
    public decimal? PreviousAmount { get; set; }
    public decimal? NewAmount { get; set; }
    public decimal? RecordedAmount { get; set; }
    public string? PreviousApproverPrincipalId { get; set; }
    public string? NewApproverPrincipalId { get; set; }
    public string? PreviousRecorderPrincipalId { get; set; }
    public string? NewRecorderPrincipalId { get; set; }
    public string? PreviousConfirmerPrincipalId { get; set; }
    public string? NewConfirmerPrincipalId { get; set; }
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
