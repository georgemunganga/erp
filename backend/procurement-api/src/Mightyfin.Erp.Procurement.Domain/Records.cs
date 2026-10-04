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
    public string? TradingName { get; set; }
    public string? Website { get; set; }
    public string? CompanyIdType { get; set; }
    public string? TaxIdentifierRef { get; set; }
    public string? SupplierType { get; set; }
    public bool IsLocal { get; set; }
    public bool IsPreferred { get; set; }
    public bool IsStrategic { get; set; }
    public string? DefaultCurrencyCode { get; set; }
    public string? PaymentTermRef { get; set; }
    public string? TaxCategoryRef { get; set; }
    public string? LanguageCode { get; set; }
    public string? RiskTier { get; set; }
    public bool IsOrderable { get; set; }
    public DateTimeOffset? EligibleFrom { get; set; }
    public DateTimeOffset? EligibleUntil { get; set; }
    public string? HoldReason { get; set; }
    public string? StatusReason { get; set; }
    public string? StatusDecidedBy { get; set; }
    public DateTimeOffset? StatusDecidedAt { get; set; }
    public Guid? StatusPolicyVersionId { get; set; }
    public string? PaymentVerificationStatus { get; set; }
    public string? PaymentVerificationToken { get; set; }
    public string? PaymentVerificationCallbackRef { get; set; }
    public string? PortalAccessState { get; set; }
    public string? Remarks { get; set; }
    public ICollection<SupplierContact> Contacts { get; set; } = new List<SupplierContact>();
    public ICollection<SupplierSite> Sites { get; set; } = new List<SupplierSite>();
    public ICollection<SupplierQualification> Qualifications { get; set; } = new List<SupplierQualification>();
    public ICollection<SupplierAttribute> Attributes { get; set; } = new List<SupplierAttribute>();
    public ICollection<SupplierCategory> Categories { get; set; } = new List<SupplierCategory>();
    public ICollection<SupplierDecision> Decisions { get; set; } = new List<SupplierDecision>();
}

public sealed class SupplierContact : ProcurementRecord
{
    public Guid SupplierId { get; set; }
    public string Name { get; set; } = null!;
    public string? Role { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
    public string? Salutation { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? WorkPhone { get; set; }
    public string? MobilePhone { get; set; }
    public string? LanguageCode { get; set; }
}

public sealed class SupplierSite : ProcurementRecord
{
    public Guid SupplierId { get; set; }
    public string Kind { get; set; } = "Physical";
    public string? Label { get; set; }
    public string AddressLine1 { get; set; } = null!;
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? PostalCode { get; set; }
    public string CountryCode { get; set; } = null!;
    public bool IsPrimary { get; set; }
    public bool IsServiceSite { get; set; }
}

public sealed class SupplierQualification : ProcurementRecord
{
    public Guid SupplierId { get; set; }
    public string QualificationType { get; set; } = null!;
    public string? Issuer { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public string ReviewStatus { get; set; } = "Pending";
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? EvidenceDocumentRefId { get; set; }
}

public sealed class SupplierAttribute : ProcurementRecord
{
    public Guid SupplierId { get; set; }
    public string Kind { get; set; } = null!; // CustomField or ReportingTag
    public string FieldKey { get; set; } = null!;
    public string FieldValue { get; set; } = null!;
}

public sealed class SupplierCategory : ProcurementRecord
{
    public Guid SupplierId { get; set; }
    public string CategoryCode { get; set; } = null!;
    public bool IsPrimary { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

public sealed class SupplierDecision : ProcurementRecord
{
    public Guid SupplierId { get; set; }
    public string FromStatus { get; set; } = null!;
    public string ToStatus { get; set; } = null!;
    public string ActorSubjectId { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public Guid? PolicyVersionId { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
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
    public string? PurchaseUnitCode { get; set; }
    public decimal? PurchaseUnitConversion { get; set; }
    public bool IsPurchasable { get; set; }
    public string? Brand { get; set; }
    public string? Manufacturer { get; set; }
    public string? ManufacturerPartNumber { get; set; }
    public int? LeadTimeDays { get; set; }
    public decimal? MinimumOrderQuantity { get; set; }
    public Guid? SpecificationDocumentRefId { get; set; }
    public Guid? ImageDocumentRefId { get; set; }
    public string? ExpenseAccountRef { get; set; }
    public bool IsStockItem { get; set; }
    public bool IsAsset { get; set; }
    public bool InspectionRequired { get; set; }
    public string? SourceVersion { get; set; }
    public ICollection<CatalogEntry> Entries { get; set; } = new List<CatalogEntry>();
}

public sealed class CatalogEntry : ProcurementRecord
{
    public Guid CatalogItemId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? SiteRef { get; set; }
    public Guid? ContractRef { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string UnitCode { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public string? TaxBasis { get; set; }
    public decimal? MinimumQuantity { get; set; }
    public decimal? MaximumQuantity { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public string Status { get; set; } = "Draft";
    public int PublishedVersion { get; set; }
    public string? AudienceCode { get; set; }
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
    public Guid? BranchRef { get; set; }
    public Guid? SiteRef { get; set; }
    public string? DeliveryLocationRef { get; set; }
    public string? DeliveryAddressText { get; set; }
    public string? OnBehalfOfSubjectId { get; set; }
    public string? PlanRef { get; set; }
    public string? FundingSourceRef { get; set; }
    public string? NotesToApprover { get; set; }
    public string? SourceChannel { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public long? SubmittedVersion { get; set; }
    public string? ApprovalInstanceRef { get; set; }
    public string? ApprovalState { get; set; }
    public ICollection<PurchaseRequestLine> Lines { get; set; } = new List<PurchaseRequestLine>();
    public ICollection<PurchaseRequestAllocation> Allocations { get; set; } = new List<PurchaseRequestAllocation>();
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
    public string? PurchaseType { get; set; }
    public string? CategoryCode { get; set; }
    public DateOnly? NeededBy { get; set; }
    public Guid? PreferredSupplierId { get; set; }
    public string? EstimatedCurrencyCode { get; set; }
    public decimal? DiscountPercent { get; set; }
    public string? TaxCodeRef { get; set; }
    public string? CostCenterRef { get; set; }
    public string? ProjectRef { get; set; }
    public Guid? SpecificationDocumentRefId { get; set; }
}

public sealed class PurchaseRequestAllocation : ProcurementRecord
{
    public Guid PurchaseRequestId { get; set; }
    public Guid PurchaseRequestLineId { get; set; }
    public string Method { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }
    public decimal Quantity { get; set; }
    public decimal EstimatedValue { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string? DecidedBy { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string IdempotencyKey { get; set; } = null!;
}

public sealed class PolicyEvaluation : ProcurementRecord
{
    public Guid PurchaseRequestId { get; set; }
    public Guid PolicyVersionId { get; set; }
    public string Outcome { get; set; } = null!;
    public string? ResultCode { get; set; }
    public string? Summary { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; }
}

public sealed class BudgetCheckSnapshot : ProcurementRecord
{
    public Guid PurchaseRequestId { get; set; }
    public string FinanceCheckRef { get; set; } = null!;
    public string Status { get; set; } = null!;
    public decimal CheckedAmount { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public DateTimeOffset CheckedAt { get; set; }
    public string? ReservationRef { get; set; }
    public string? ReservationStatus { get; set; }
    public string? ExchangeRateSource { get; set; }
}

public sealed class ProcurementAttachmentRef : ProcurementRecord
{
    public string OwnerType { get; set; } = null!;
    public Guid OwnerId { get; set; }
    public string DocumentServiceId { get; set; } = null!;
    public int DocumentVersion { get; set; }
    public string FileName { get; set; } = null!;
    public string? Classification { get; set; }
    public string? Sha256 { get; set; }
    public string ScanStatus { get; set; } = "Pending";
    public DateOnly? ExpiresOn { get; set; }
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
