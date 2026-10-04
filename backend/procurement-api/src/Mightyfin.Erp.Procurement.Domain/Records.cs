namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Every Procurement row is scoped to one tenant and legal entity.</summary>
public abstract class ProcurementRecord
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string TenantId { get; set; } = string.Empty;
    public Guid LegalEntityId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public long Version { get; set; } = 1;
}

public sealed class Supplier : ProcurementRecord
{
    public string Number { get; set; } = null!;
    public string LegalName { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string CountryCode { get; set; } = null!;
    public string? RegistrationNumber { get; set; }
    public string? NormalizedRegistration { get; set; }
    public string Status { get; set; } = "Draft";
    public string? CategoryCode { get; set; }
    public string? FinanceSupplierRef { get; set; }
    public Guid? ProposedByWorkerId { get; set; }
    public ICollection<SupplierContact> Contacts { get; set; } = new List<SupplierContact>();
}

public sealed class SupplierContact : ProcurementRecord
{
    public Guid SupplierId { get; set; }
    public string Name { get; set; } = null!;
    public string? Role { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class CatalogItem : ProcurementRecord
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Kind { get; set; } = null!;
    public string CategoryCode { get; set; } = null!;
    public string UnitCode { get; set; } = null!;
    public string? Description { get; set; }
    public string? TaxCodeRef { get; set; }
    public string Status { get; set; } = "Inactive";
    public Guid? InventoryItemRef { get; set; }
    public Guid? AssetClassRef { get; set; }
}

public sealed class PurchaseRequest : ProcurementRecord
{
    public string Number { get; set; } = null!;
    public Guid RequesterWorkerId { get; set; }
    public Guid? DepartmentRef { get; set; }
    public string? CostCenterRef { get; set; }
    public string? ProjectRef { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string Purpose { get; set; } = null!;
    public string Status { get; set; } = "Draft";
    public DateOnly? NeededBy { get; set; }
    public ICollection<PurchaseRequestLine> Lines { get; set; } = new List<PurchaseRequestLine>();
}

public sealed class PurchaseRequestLine : ProcurementRecord
{
    public Guid PurchaseRequestId { get; set; }
    public int LineNumber { get; set; }
    public Guid? CatalogItemId { get; set; }
    public string Description { get; set; } = null!;
    public string UnitCode { get; set; } = null!;
    public decimal RequestedQuantity { get; set; }
    public decimal EstimatedUnitPrice { get; set; }
    public decimal ApprovedQuantity { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal CancelledQuantity { get; set; }
    public string ApprovalStatus { get; set; } = "Pending";
    public string Route { get; set; } = "Unassigned";
}

/// <summary>Approved, effective-dated configuration; policy values are not seeded here.</summary>
public sealed class PolicyVersion : ProcurementRecord
{
    public string PolicyKey { get; set; } = null!;
    public int PolicyVersionNumber { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string State { get; set; } = "Draft";
    public string RulesJson { get; set; } = "{}";
}

public sealed class ImportBatch : ProcurementRecord
{
    public string RecordType { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string FileSha256 { get; set; } = null!;
    public string State { get; set; } = "Previewed";
    public int TotalRows { get; set; }
    public int AppliedRows { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? ResultJson { get; set; }
}

/// <summary>Append-only business audit metadata. Sensitive field values are not stored here.</summary>
public sealed class AuditEvent : ProcurementRecord
{
    public string RecordType { get; set; } = null!;
    public Guid RecordId { get; set; }
    public string Action { get; set; } = null!;
    public string ActorSubjectId { get; set; } = null!;
    public string? ChangedFieldsJson { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class OutboxEvent : ProcurementRecord
{
    public string EventType { get; set; } = null!;
    public int EventVersion { get; set; } = 1;
    public string AggregateType { get; set; } = null!;
    public Guid AggregateId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string IdempotencyKey { get; set; } = null!;
    public string State { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
