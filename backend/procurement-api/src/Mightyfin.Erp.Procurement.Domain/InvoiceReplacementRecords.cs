namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>An append-only link between two distinct supplier-issued invoice
/// identities. The source invoice and its recorded comparison are never edited.</summary>
public sealed class InvoiceReplacementLink : ProcurementRecord
{
    public Guid SourceInvoiceId { get; init; }
    public Guid SuccessorInvoiceId { get; init; }
    public long SourceInvoiceVersion { get; init; }
    public string SourceOriginalSha256 { get; init; } = "";
    public Guid SourceMappingVersionId { get; init; }
    public int SourceMappingRevision { get; init; }
    public Guid BlockedResultId { get; init; }
    public Guid ReviewSourceId { get; init; }
    public Guid ReviewOutcomeId { get; init; }
    public string NoClaimProofSha256 { get; init; } = "";
    public Guid SupplierId { get; init; }
    public string CurrencyCode { get; init; } = "";
    public Guid PurchaseOrderId { get; init; }
    public Guid IssuedVersionId { get; init; }
    public Guid BranchId { get; init; }
    public Guid CalculationId { get; init; }
    public Guid CaptureReceiptId { get; init; }
    public Guid CaptureCommandKey { get; init; }
    public string MakerPrincipalId { get; init; } = "";
    public string Reason { get; init; } = "";
    public Guid CommandKey { get; init; }
    public DateTimeOffset RecordedAtUtc { get; init; }
}

/// <summary>Exact saved replacement response. The v2 capture receipt remains
/// separately required and retains the internal derived capture command key.</summary>
public sealed class InvoiceReplacementCommandReceipt : ProcurementRecord
{
    public Guid LinkId { get; init; }
    public Guid SourceInvoiceId { get; init; }
    public Guid SuccessorInvoiceId { get; init; }
    public string Operation { get; init; } = "InvoiceReplaceV2";
    public Guid CommandKey { get; init; }
    public string ActorPrincipalId { get; init; } = "";
    public string RequestSha256 { get; init; } = "";
    public string ResponseJson { get; init; } = "";
    public string ResponseSha256 { get; init; } = "";
    public int ResponseStatus { get; init; } = 201;
    public DateTimeOffset CompletedAtUtc { get; init; }
}
