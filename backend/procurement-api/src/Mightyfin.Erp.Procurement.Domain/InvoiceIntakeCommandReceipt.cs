namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Exact, actor-owned recovery for one new v2 invoice capture.
/// ProcurementCommercialCommandReceipt remains bound to a purchase order.</summary>
public sealed class InvoiceIntakeCommandReceipt : ProcurementRecord
{
    public Guid SupplierInvoiceId { get; init; }
    public Guid CalculationId { get; init; }
    public string Operation { get; init; } = "InvoiceCaptureV2";
    public Guid CommandKey { get; init; }
    public string ActorPrincipalId { get; init; } = "";
    public string RequestSha256 { get; init; } = "";
    public string ResponseJson { get; init; } = "";
    public string ResponseSha256 { get; init; } = "";
    public int ResponseStatus { get; init; } = 201;
    public DateTimeOffset CompletedAtUtc { get; init; }
}
