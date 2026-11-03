namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Frozen Procurement source paired with one shared Core approval case.</summary>
public sealed class PlanReviewSubmission : ProcurementRecord
{
    public Guid ProcurementPlanId { get; set; }
    public Guid BranchRef { get; set; }
    public long SubmittedPlanVersion { get; set; }
    public Guid CoreApprovalCaseId { get; set; }
    public Guid PolicyVersionId { get; set; }
    public int PolicyVersionNumber { get; set; }
    public string PolicyRulesSha256 { get; set; } = null!;
    public string MakerSubjectId { get; set; } = null!;
    /// <summary>Known Core account link at submission; nullable for workerless principals.</summary>
    public Guid? MakerCoreLinkedWorkerId { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal EstimatedAmount { get; set; }
    public string SnapshotJson { get; set; } = null!;
    public string SnapshotSha256 { get; set; } = null!;
    public string State { get; set; } = "Pending";
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>Actor-bound original-key response; a retry cannot silently create another round.</summary>
public sealed class PlanReviewCommandReceipt : ProcurementRecord
{
    public Guid ProcurementPlanId { get; set; }
    public Guid CommandKey { get; set; }
    public Guid? DecisionId { get; set; }
    public Guid? ActorCoreLinkedWorkerId { get; set; }
    public string Operation { get; set; } = null!;
    public string ActorSubjectId { get; set; } = null!;
    public string RequestSha256 { get; set; } = null!;
    public string ResponseJson { get; set; } = null!;
    public int ResponseStatus { get; set; }
}
