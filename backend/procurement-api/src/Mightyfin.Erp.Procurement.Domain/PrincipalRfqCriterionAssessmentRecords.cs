namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>One immutable evaluator observation. A correction appends a successor
/// revision; it never changes the former score or pass/fail observation.</summary>
public sealed class PrincipalRfqCriterionAssessmentRecord : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid RoundId { get; init; }
    public Guid UnsealingId { get; init; }
    public Guid OfferId { get; init; }
    public Guid EventId { get; init; }
    public Guid BranchId { get; init; }
    public Guid PreparedPlanId { get; init; }
    public Guid EvaluationPlanRevisionId { get; init; }
    public string EvaluationDefinitionSha256 { get; init; } = null!;
    public string ApprovedRoundSha256 { get; set; } = null!;
    public string OpeningSha256 { get; set; } = null!;
    public string OfferSha256 { get; init; } = null!;
    public string CriterionCode { get; init; } = null!;
    public string Kind { get; init; } = null!;
    public string EvaluatorPrincipalId { get; init; } = null!;
    public Guid? EvaluatorWorkerAliasId { get; init; }
    public int Revision { get; init; }
    public Guid? PreviousAssessmentId { get; init; }
    public int? PreviousRevision { get; init; }
    public decimal? Score { get; set; }
    public bool? Passed { get; init; }
    public string Rationale { get; set; } = null!;
    public Guid OriginalDocumentVersionId { get; init; }
    public string OriginalDocumentSha256 { get; init; } = null!;
    public Guid CommandKey { get; init; }
    public string RequestSha256 { get; init; } = null!;
    public DateTimeOffset RecordedAt { get; set; }
    public string SnapshotJson { get; set; } = null!;
    public string SnapshotSha256 { get; set; } = null!;
}

/// <summary>Actor-owned original acknowledgment, without any other evaluator's
/// score or supplier offer body.</summary>
public sealed class PrincipalRfqCriterionAssessmentReceipt : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid AssessmentId { get; init; }
    public Guid RoundId { get; init; }
    public Guid UnsealingId { get; init; }
    public Guid OfferId { get; init; }
    public Guid EventId { get; init; }
    public Guid BranchId { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public Guid CommandKey { get; init; }
    public string RequestSha256 { get; init; } = null!;
    public string ResponseJson { get; init; } = null!;
    public string ResponseSha256 { get; init; } = null!;
}
