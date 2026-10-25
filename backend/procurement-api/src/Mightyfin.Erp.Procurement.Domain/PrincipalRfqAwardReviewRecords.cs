namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Append-only P36 freeze of one exact Draft revision. A Draft is never
/// itself a submitted recommendation or an award.</summary>
public sealed class PrincipalRfqRecommendationSubmission : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid DraftId { get; init; }
    public Guid AwardReviewSubmissionId { get; init; }
    public Guid RevisionId { get; init; }
    public int Revision { get; init; }
    public string RevisionSha256 { get; init; } = null!;
    public Guid RoundId { get; init; }
    public Guid UnsealingId { get; init; }
    public Guid SourcingEventId { get; init; }
    public Guid BranchId { get; init; }
    public string MakerPrincipalId { get; init; } = null!;
    public Guid? MakerWorkerAliasId { get; init; }
    public Guid CommandKey { get; init; }
    public string RequestSha256 { get; init; } = null!;
    public DateTimeOffset SubmittedAt { get; init; }
    public string SnapshotJson { get; init; } = null!;
    public string SnapshotSha256 { get; init; } = null!;
}

/// <summary>Procurement's immutable source for one Core approval case. The
/// Core case owns task state; this row does not assert an approved award.</summary>
public sealed class PrincipalRfqAwardReviewSubmission : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid RecommendationSubmissionId { get; init; }
    public Guid DraftId { get; init; }
    public Guid RecommendationRevisionId { get; init; }
    public string RecommendationSha256 { get; init; } = null!;
    public Guid RoundId { get; init; }
    public Guid UnsealingId { get; init; }
    public Guid BranchId { get; init; }
    public Guid CoreCaseId { get; init; }
    public Guid PolicyVersionId { get; init; }
    public int PolicyVersionNumber { get; init; }
    public string PolicyRulesSha256 { get; init; } = null!;
    public string MakerPrincipalId { get; init; } = null!;
    public Guid? MakerWorkerAliasId { get; init; }
    public Guid CommandKey { get; init; }
    public string RequestSha256 { get; init; } = null!;
    public string CurrencyCode { get; init; } = null!;
    public string ProposedTotalExact { get; init; } = null!;
    public DateTimeOffset SubmittedAt { get; init; }
    public string AssignmentPinsJson { get; set; } = null!;
    public string AssignmentPinsSha256 { get; set; } = null!;
}

public sealed class PrincipalRfqAwardReviewLine : ProcurementRecord
{
    public Guid SubmissionId { get; init; }
    public Guid RecommendationSubmissionId { get; init; }
    public Guid RoundId { get; init; }
    public Guid UnsealingId { get; init; }
    public Guid BranchId { get; init; }
    public Guid OfferId { get; init; }
    public string OfferSha256 { get; init; } = null!;
    public Guid SupplierId { get; init; }
    public Guid SourcingLineId { get; init; }
    public string QuantityExact { get; init; } = null!;
    public string CurrencyCode { get; init; } = null!;
    public string RetainedQuoteTotalExact { get; init; } = null!;
}

/// <summary>Original-key acknowledgement bytes. Recovery must still check the
/// current named actor, scope and stored response self-digest.</summary>
public sealed class PrincipalRfqAwardReviewReceipt : ProcurementRecord
{
    public Guid SubmissionId { get; init; }
    public Guid RecommendationSubmissionId { get; init; }
    public Guid RoundId { get; init; }
    public Guid BranchId { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public Guid CommandKey { get; init; }
    public string RequestSha256 { get; init; } = null!;
    public string ResponseJson { get; init; } = null!;
    public string ResponseSha256 { get; init; } = null!;
}

/// <summary>A terminal fact can be appended only after the owning adapter
/// verifies actual Core case, stages, assignments and decisions in one tx.</summary>
public sealed class PrincipalRfqAwardReviewOutcome : ProcurementRecord
{
    public Guid SubmissionId { get; init; }
    public Guid CoreCaseId { get; init; }
    public Guid TerminalDecisionId { get; init; }
    public string CoreState { get; init; } = null!;
    public string SourceOutcome { get; init; } = null!;
    public DateTimeOffset RecordedAt { get; init; }
}
