namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>A planning schedule only. It never creates an AP bill, liability, or payment.</summary>
public sealed class RecurringPurchaseCommitment : ProcurementRecord
{
    public string Number { get; set; } = null!;
    public string Title { get; set; } = null!;
    public Guid SupplierId { get; set; }
    public Guid? SupplierContractId { get; set; }
    public Guid OwnerWorkerId { get; set; }
    public string OwnerSubjectId { get; set; } = null!;
    public Guid? WorkLocationId { get; set; }
    public string Frequency { get; set; } = null!;
    public DateOnly FirstCycleOn { get; set; }
    public DateOnly? LastCycleOn { get; set; }
    public DateOnly NextCycleOn { get; set; }
    public decimal ExpectedAmount { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string Status { get; set; } = "Draft";
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosedReason { get; set; }
}
