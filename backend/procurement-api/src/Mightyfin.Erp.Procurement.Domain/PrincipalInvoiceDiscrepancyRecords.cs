namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>A known blocked schema-2 comparison issue. It is not a legacy
/// InvoiceMatchResult, a capacity claim, AP posting or payment instruction.</summary>
public sealed class PrincipalInvoiceDiscrepancy : ProcurementRecord
{
    public Guid SupplierInvoiceId { get; init; }
    public Guid MappingVersionId { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public Guid PrincipalIssuedVersionId { get; init; }
    public Guid BranchId { get; init; }
    public Guid? SupplierInvoiceLineId { get; init; }
    public Guid CommandKey { get; init; }
    public string OriginalInvoiceSha256 { get; init; } = null!;
    public string Code { get; init; } = null!;
    public string IssueKeySha256 { get; init; } = null!;
    public string OwnerPrincipalId { get; set; } = null!;
    public Guid? OwnerWorkerAliasId { get; set; }
    public string State { get; set; } = "Open";
    public string? ResolutionReference { get; set; }
    public DateTimeOffset OpenedAt { get; init; }
    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

/// <summary>Append-only explanation/assignment/correction decision. A note
/// alone cannot make a blocked invoice comparable or release a claim.</summary>
public sealed class PrincipalInvoiceDiscrepancyAction : ProcurementRecord
{
    public Guid DiscrepancyId { get; init; }
    public Guid SupplierInvoiceId { get; init; }
    public Guid MappingVersionId { get; init; }
    public string Action { get; init; } = null!;
    public string ActorPrincipalId { get; init; } = null!;
    public Guid? ActorWorkerAliasId { get; init; }
    public string Reason { get; init; } = null!;
    public string? EvidenceReference { get; init; }
    public Guid? ResolvingComparisonId { get; init; }
    public Guid CommandKey { get; init; }
    public DateTimeOffset At { get; init; }
}
