namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Procurement category reference; policy decisions live in PolicyVersion.</summary>
public sealed class ProcurementCategory : ProcurementRecord
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Status { get; set; } = "Draft";
    public string? ParentCode { get; set; }
    public string? ExpenseAccountRef { get; set; }
}

public sealed class ProcurementMethod : ProcurementRecord
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Status { get; set; } = "Draft";
    public string? Description { get; set; }
    public Guid? PolicyVersionId { get; set; }
}

public sealed class ProcurementPlan : ProcurementRecord
{
    public string Number { get; set; } = null!;
    public string FinancialPeriodRef { get; set; } = null!;
    public Guid? DepartmentRef { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string Status { get; set; } = "Draft";
    public string OwnerSubjectId { get; set; } = null!;
    public int RevisionNumber { get; set; } = 1;
    public string? FundingSourceRef { get; set; }
    public string? ApprovalInstanceRef { get; set; }
}

public sealed class ProcurementPlanLine : ProcurementRecord
{
    public Guid ProcurementPlanId { get; set; }
    public int LineNumber { get; set; }
    public Guid? CatalogItemId { get; set; }
    public string Description { get; set; } = null!;
    public string? CategoryCode { get; set; }
    public string UnitCode { get; set; } = null!;
    public decimal EstimatedQuantity { get; set; }
    public decimal EstimatedAmount { get; set; }
    public DateOnly? ExpectedOn { get; set; }
    public string? FundingSourceRef { get; set; }
}

/// <summary>Membership only. Identity, login, MFA and sessions remain Core-owned.</summary>
public sealed class SupplierPortalMembership : ProcurementRecord
{
    public Guid SupplierId { get; set; }
    public string IdentitySubjectId { get; set; } = null!;
    public string Status { get; set; } = "Invited";
    public string? InvitationRef { get; set; }
    public DateTimeOffset? InvitationExpiresAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class SupplierPerformanceReview : ProcurementRecord
{
    public Guid SupplierId { get; set; }
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public string Status { get; set; } = "Draft";
    public decimal? TotalScore { get; set; }
    public string ReviewerSubjectId { get; set; } = null!;
    public string? DisputeReason { get; set; }
    public string? CorrectivePlanRef { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

public sealed class SupplierPerformanceCriterion : ProcurementRecord
{
    public Guid SupplierPerformanceReviewId { get; set; }
    public string CriterionCode { get; set; } = null!;
    public decimal Weight { get; set; }
    public decimal? Score { get; set; }
    public string? SourceRecordType { get; set; }
    public Guid? SourceRecordId { get; set; }
    public string? Comment { get; set; }
}

/// <summary>Record-level collaboration; supplier-visible comments require API audience checks.</summary>
public sealed class ProcurementComment : ProcurementRecord
{
    public string OwnerType { get; set; } = null!;
    public Guid OwnerId { get; set; }
    public string Audience { get; set; } = "Internal";
    public string Body { get; set; } = null!;
    public string AuthorSubjectId { get; set; } = null!;
}

/// <summary>Server-evaluated setup evidence, not a user-ticked readiness flag.</summary>
public sealed class ProcurementSetupAssessment : ProcurementRecord
{
    public string CapabilityKey { get; set; } = null!;
    public string Status { get; set; } = "Unknown";
    public string? EvidenceRef { get; set; }
    public string? EvaluatedBy { get; set; }
    public DateTimeOffset? EvaluatedAt { get; set; }
    public string? FailureCode { get; set; }
}
