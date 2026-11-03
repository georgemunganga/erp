namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Explicit immutable opt-in. Cancellation never removes this publication barrier.</summary>
public sealed class CommercialRoundEnrollment : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid SourcingEventId { get; init; }
    public Guid BranchId { get; init; }
    public Guid SourceEvidenceId { get; init; }
    public long SourceEventVersion { get; init; }
    public string SourceSha256 { get; init; } = "";
    public string OriginalMakerPrincipalId { get; init; } = "";
    public string EnrolledByPrincipalId { get; init; } = "";
    public Guid? EnrolledByWorkerAlias { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}

public sealed class CommercialRoundPlan : ProcurementRecord
{
    public Guid EnrollmentId { get; init; }
    public Guid SourcingEventId { get; init; }
    public Guid BranchId { get; init; }
    public string MakerPrincipalId { get; init; } = "";
    public string Status { get; set; } = "Draft";
    // Current revision is the unique (PlanId, ResultVersion == Version) row.
    // No mutable-version FK or circular initial-insert pointer is introduced.
}

public sealed class CommercialRoundPlanRevision : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid PlanId { get; init; }
    public Guid SourcingEventId { get; init; }
    public Guid BranchId { get; init; }
    public Guid? PreviousRevisionId { get; init; }
    public long ExpectedVersion { get; init; }
    public long ResultVersion { get; init; }
    public string Operation { get; init; } = "";
    public string ResultStatus { get; init; } = "";
    public string ActorPrincipalId { get; init; } = "";
    public Guid? ActorWorkerAlias { get; init; }
    public string CompanyTimeZoneId { get; init; } = "";
    public string? Requirements { get; init; }
    public string? ProposedPolicyReference { get; init; }
    public string? DisclosureProposal { get; init; }
    public string? DocumentRequirements { get; init; }
    public DateTimeOffset? OpensAt { get; init; }
    public DateTimeOffset? ClosesAt { get; init; }
    public DateTimeOffset? TechnicalReleaseAt { get; init; }
    public DateTimeOffset? CommercialReleaseAt { get; init; }
    public string? Reason { get; init; }
    public int CriterionCount { get; init; }
    public int ParticipantCount { get; init; }
    public string ContentJson { get; init; } = "";
    public string ContentSha256 { get; init; } = "";
    public DateTimeOffset RecordedAt { get; init; }
}

public sealed class CommercialRoundCriterion : ProcurementRecord
{
    public Guid PlanId { get; init; }
    public Guid RevisionId { get; init; }
    public Guid LogicalId { get; init; }
    public int Order { get; init; }
    public string Code { get; init; } = "";
    public string Stage { get; init; } = "";
    public string Kind { get; init; } = "";
    public string? Description { get; init; }
    public string? Rubric { get; init; }
    public decimal? Weight { get; init; }
    public decimal? MaximumScore { get; init; }
    public decimal? MinimumScore { get; init; }
}

public sealed class CommercialRoundParticipant : ProcurementRecord
{
    public Guid PlanId { get; init; }
    public Guid RevisionId { get; init; }
    public Guid LogicalId { get; init; }
    public int Order { get; init; }
    public string PrincipalId { get; init; } = "";
    public string Duty { get; init; } = "";
    // Planner-reported proposed information, not a participant's verified declaration.
    public string? ConflictNote { get; init; }
}

public sealed class CommercialRoundPlanReceipt : ProcurementRecord
{
    public Guid SourcingEventId { get; init; }
    public Guid PlanId { get; init; }
    public Guid BranchId { get; init; }
    public Guid RevisionId { get; init; }
    public long ResultVersion { get; init; }
    public string Operation { get; init; } = "";
    public string ActorPrincipalId { get; init; } = "";
    public Guid CommandKey { get; init; }
    public string RequestSha256 { get; init; } = "";
    public string ResponseJson { get; init; } = "";
    public string ResponseSha256 { get; init; } = "";
    public int ResponseStatusCode { get; init; }
}
