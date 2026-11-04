namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Frozen content and authentic Core case opened by one Submit command.</summary>
public sealed class CommercialRoundReviewSubmission : ProcurementRecord
{
    public Guid SourcingEventId { get; init; }
    public Guid BranchId { get; init; }
    public Guid PlanId { get; init; }
    public Guid SubmittedRevisionId { get; init; }
    public long SubmittedPlanVersion { get; init; }
    public string SubmittedContentSha256 { get; init; } = "";
    public Guid PolicyVersionId { get; init; }
    public long PolicyVersionNumber { get; init; }
    public string PolicyRulesSha256 { get; init; } = "";
    public Guid CoreCaseId { get; init; }
    public string MakerPrincipalId { get; init; } = "";
    public Guid? MakerWorkerAlias { get; init; }
    public DateTimeOffset SubmittedAt { get; init; }
}

/// <summary>One Core decision bound to an immutable plan result revision.</summary>
public sealed class CommercialRoundReviewOutcome : ProcurementRecord
{
    public Guid SourcingEventId { get; init; }
    public Guid BranchId { get; init; }
    public Guid PlanId { get; init; }
    public Guid SubmissionId { get; init; }
    public Guid CoreCaseId { get; init; }
    public Guid CoreStageId { get; init; }
    public Guid CoreAssignmentId { get; init; }
    public Guid CoreDecisionId { get; init; }
    public Guid ResultRevisionId { get; init; }
    public long ResultPlanVersion { get; init; }
    public string ResultStatus { get; init; } = "";
    public string Action { get; init; } = "";
    public string ActorPrincipalId { get; init; } = "";
    public Guid? ActorWorkerAlias { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}
