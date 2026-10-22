namespace Mightyfin.Erp.Procurement.Domain;

// Commercial records are Procurement-owned. Finance, stock, identity and signed-document
// authorities are referenced by stable IDs rather than copied into this schema.
public sealed class SourcingEvent : ProcurementRecord
{
    public string Number { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Method { get; set; } = null!;
    public string Status { get; set; } = "Draft";
    public string CurrencyCode { get; set; } = null!;
    public string OwnerSubjectId { get; set; } = null!;
    public DateTimeOffset? OpensAt { get; set; }
    public DateTimeOffset? ClosesAt { get; set; }
    public Guid? PolicyVersionId { get; set; }
    public string? ApprovalInstanceRef { get; set; }
    public string? CancellationReason { get; set; }
}

public sealed class SourcingLine : ProcurementRecord
{
    public Guid SourcingEventId { get; set; }
    public int LineNumber { get; set; }
    public string Status { get; set; } = "Active";
    public string? StatusReason { get; set; }
    public Guid? PurchaseRequestLineId { get; set; }
    public string Description { get; set; } = null!;
    public string UnitCode { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string? CategoryCode { get; set; }
    public DateOnly? NeededBy { get; set; }
}

public sealed class SourcingInvitation : ProcurementRecord
{
    public Guid SourcingEventId { get; set; }
    public Guid SupplierId { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? ViewedAt { get; set; }
    public string? DeliveryRef { get; set; }
    public string? FailureReason { get; set; }
}

public sealed class SupplierBid : ProcurementRecord
{
    public Guid SourcingEventId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid? InvitationId { get; set; }
    public int SubmissionVersion { get; set; } = 1;
    public string Status { get; set; } = "Draft";
    public string SourceChannel { get; set; } = "Portal";
    public string CurrencyCode { get; set; } = null!;
    public DateTimeOffset? ReceivedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public string? SurrogateEnteredBy { get; set; }
    public Guid? EvidenceDocumentRefId { get; set; }
    public string? CorrectionReason { get; set; }
}

public sealed class SupplierBidLine : ProcurementRecord
{
    public Guid SupplierBidId { get; set; }
    public Guid SourcingLineId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DeliveryAmount { get; set; }
    public string? LeadTimeText { get; set; }
    public string? Notes { get; set; }
}

public sealed class BidEvaluation : ProcurementRecord
{
    public Guid SupplierBidId { get; set; }
    public string EvaluatorSubjectId { get; set; } = null!;
    public string Stage { get; set; } = null!;
    public string CriterionCode { get; set; } = null!;
    public decimal? Score { get; set; }
    public bool Passed { get; set; }
    public bool ConflictDeclared { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; }
}

public sealed class SourcingAward : ProcurementRecord
{
    public Guid SourcingEventId { get; set; }
    public Guid SupplierId { get; set; }
    public string Status { get; set; } = "Recommended";
    public string Rationale { get; set; } = null!;
    public string? ApprovalInstanceRef { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

public sealed class SourcingAwardLine : ProcurementRecord
{
    public Guid SourcingAwardId { get; set; }
    public Guid SourcingLineId { get; set; }
    public decimal Quantity { get; set; }
    public decimal AwardedAmount { get; set; }
    public string CurrencyCode { get; set; } = null!;
}

public sealed class SupplierContract : ProcurementRecord
{
    public string Number { get; set; } = null!;
    public Guid SupplierId { get; set; }
    public Guid? SourcingAwardId { get; set; }
    public string Title { get; set; } = null!;
    public string ContractType { get; set; } = null!;
    public string Status { get; set; } = "Draft";
    public string OwnerSubjectId { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public decimal CeilingAmount { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public DateOnly? RenewalNoticeOn { get; set; }
    public string? TerminationReason { get; set; }
}

public sealed class SupplierContractVersion : ProcurementRecord
{
    public Guid SupplierContractId { get; set; }
    public int RevisionNumber { get; set; }
    public string Status { get; set; } = "Draft";
    public decimal CeilingAmount { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public string? ApprovalInstanceRef { get; set; }
    public string? SignedDocumentServiceId { get; set; }
    public DateTimeOffset? SignedAt { get; set; }
    public string? AmendmentReason { get; set; }
}

public sealed class ContractRate : ProcurementRecord
{
    public Guid SupplierContractVersionId { get; set; }
    public int LineNumber { get; set; }
    public string Status { get; set; } = "Active";
    public string? StatusReason { get; set; }
    public Guid? CatalogItemId { get; set; }
    public string Description { get; set; } = null!;
    public string UnitCode { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public decimal? MaximumQuantity { get; set; }
}

public sealed class PurchaseOrder : ProcurementRecord
{
    public string Number { get; set; } = null!;
    public Guid SupplierId { get; set; }
    public Guid? SourcingAwardId { get; set; }
    public Guid? SupplierContractVersionId { get; set; }
    public string Status { get; set; } = "Draft";
    public string CurrencyCode { get; set; } = null!;
    public string BuyerSubjectId { get; set; } = null!;
    public Guid? BranchRef { get; set; }
    public string? ShipToRef { get; set; }
    public string? BillToRef { get; set; }
    public string? PaymentTermRef { get; set; }
    public string? FinanceCommitmentRef { get; set; }
    public string? ApprovalInstanceRef { get; set; }
    public DateOnly? ExpectedDeliveryOn { get; set; }
    public DateTimeOffset? IssuedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosureReason { get; set; }
    public int CurrentRevision { get; set; }
}

public sealed class PurchaseOrderLine : ProcurementRecord
{
    public Guid PurchaseOrderId { get; set; }
    public int LineNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? PurchaseRequestLineId { get; set; }
    public Guid? CatalogItemId { get; set; }
    public string Description { get; set; } = null!;
    public string UnitCode { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal InvoicedQuantity { get; set; }
    public string? CostCenterRef { get; set; }
    public string? ProjectRef { get; set; }
    public DateOnly? ExpectedDeliveryOn { get; set; }
}

public sealed class PurchaseOrderRevision : ProcurementRecord
{
    public Guid PurchaseOrderId { get; set; }
    public int RevisionNumber { get; set; }
    public string Status { get; set; } = "PendingApproval";
    public string Reason { get; set; } = null!;
    public string BeforeSnapshotJson { get; set; } = null!;
    public string AfterSnapshotJson { get; set; } = null!;
    public string? ApprovalInstanceRef { get; set; }
    public DateTimeOffset? IssuedAt { get; set; }
}

public sealed class SupplierOrderAcknowledgement : ProcurementRecord
{
    public Guid PurchaseOrderId { get; set; }
    public int RevisionNumber { get; set; }
    public string Decision { get; set; } = null!;
    public string? Reason { get; set; }
    public string SupplierActorRef { get; set; } = null!;
    public DateTimeOffset RespondedAt { get; set; }
}

public sealed class GoodsReceipt : ProcurementRecord
{
    public string Number { get; set; } = null!;
    public Guid PurchaseOrderId { get; set; }
    public string Status { get; set; } = "Draft";
    public Guid? SiteRef { get; set; }
    public string? DeliveryNoteRef { get; set; }
    public string ReceiverSubjectId { get; set; } = null!;
    public DateTimeOffset ReceivedAt { get; set; }
    public string? InventoryHandoffRef { get; set; }
    public string? AssetHandoffRef { get; set; }
}

public sealed class GoodsReceiptLine : ProcurementRecord
{
    public Guid GoodsReceiptId { get; set; }
    public Guid PurchaseOrderLineId { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string Status { get; set; } = "PendingInspection";
    public string? BatchRef { get; set; }
    public string? SerialRef { get; set; }
    public string? RejectionReason { get; set; }
}

public sealed class ReceiptInspection : ProcurementRecord
{
    public Guid GoodsReceiptLineId { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal InspectedQuantity { get; set; }
    public decimal DefectQuantity { get; set; }
    public string InspectorSubjectId { get; set; } = null!;
    public string? ChecklistRef { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? InspectedAt { get; set; }
}

public sealed class ServiceAcceptance : ProcurementRecord
{
    public Guid PurchaseOrderLineId { get; set; }
    public string Status { get; set; } = "PendingCertification";
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal AcceptedAmount { get; set; }
    public string OwnerSubjectId { get; set; } = null!;
    public string? DeliverableRef { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset? CertifiedAt { get; set; }
}

public sealed class SupplierReturn : ProcurementRecord
{
    public Guid GoodsReceiptLineId { get; set; }
    public string Status { get; set; } = "Requested";
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = null!;
    public string? SupplierReturnRef { get; set; }
    public string? InventoryAdjustmentRef { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
}

public sealed class SupplierInvoice : ProcurementRecord
{
    /// <summary>Observed Core branch at intake; null legacy rows remain unverified, never backfilled.</summary>
    public Guid? BranchRef { get; set; }
    public Guid SupplierId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string NormalizedInvoiceNumber { get; set; } = null!;
    public string Status { get; set; } = "Received";
    public string IntakeChannel { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? FinancePostingRef { get; set; }
    public Guid? EvidenceDocumentRefId { get; set; }
    public string? CreditForInvoiceRef { get; set; }
}

public sealed class SupplierInvoiceLine : ProcurementRecord
{
    /// <summary>Original lines are retained when removed from an unprocessed manual intake.</summary>
    public bool IsActive { get; set; } = true;
    public Guid SupplierInvoiceId { get; set; }
    public int LineNumber { get; set; }
    public Guid? PurchaseOrderLineId { get; set; }
    public Guid? GoodsReceiptLineId { get; set; }
    public Guid? ServiceAcceptanceId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public string? TaxCodeRef { get; set; }
    public string? CostCenterRef { get; set; }
}

public sealed class InvoiceMatchResult : ProcurementRecord
{
    public Guid SupplierInvoiceId { get; set; }
    public string MatchType { get; set; } = null!;
    public string Outcome { get; set; } = null!;
    public Guid? PolicyVersionId { get; set; }
    public decimal QuantityVariance { get; set; }
    public decimal PriceVariance { get; set; }
    public decimal TaxVariance { get; set; }
    public DateTimeOffset MatchedAt { get; set; }
}

public sealed class InvoiceException : ProcurementRecord
{
    public Guid SupplierInvoiceId { get; set; }
    public string Type { get; set; } = null!;
    public string Status { get; set; } = "Open";
    public string OwnerSubjectId { get; set; } = null!;
    public string? Resolution { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

public sealed class FinanceInvoiceHandoff : ProcurementRecord
{
    public Guid SupplierInvoiceId { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public string? FinancePostingRef { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
}

/// <summary>Read-only Finance payment outcome; Procurement never executes payment.</summary>
public sealed class FinancePaymentStatusRef : ProcurementRecord
{
    public Guid SupplierInvoiceId { get; set; }
    public string FinancePaymentRef { get; set; } = null!;
    public string Status { get; set; } = null!;
    public decimal PaidAmount { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public DateOnly? PaidOn { get; set; }
    public DateTimeOffset SyncedAt { get; set; }
}

public sealed class SupplierCreditNoteRef : ProcurementRecord
{
    public Guid SupplierId { get; set; }
    public Guid? SupplierInvoiceId { get; set; }
    public Guid? SupplierReturnId { get; set; }
    public string CreditNumber { get; set; } = null!;
    public string Status { get; set; } = "Received";
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string? FinanceCreditRef { get; set; }
    public string Reason { get; set; } = null!;
}
