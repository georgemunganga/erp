namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Append-only intake-v2 calculation. Core owns the referenced precision;
/// this Procurement child retains the exact consumed snapshot and arithmetic.</summary>
public sealed class InvoiceAmountCalculation : ProcurementRecord
{
    public Guid SupplierInvoiceId { get; set; }
    public int SchemaVersion { get; set; } = 2;
    public Guid CoreCurrencyReferenceId { get; set; }
    public int CoreCurrencyReferenceRevision { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public int MinorUnits { get; set; }
    public string RoundingMode { get; set; } = null!;
    public string CoreSourcePayloadSha256 { get; set; } = null!;
    public DateTimeOffset CorePublishedAtUtc { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
    public string InvoiceOriginalSha256 { get; set; } = null!;
    public string CalculationJson { get; set; } = null!;
    public string CalculationPolicySha256 { get; set; } = null!;
    public string CalculationSha256 { get; set; } = null!;
}
