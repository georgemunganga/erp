namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>A buyer's proposal only. This record is never an award, ranking,
/// supplier notification, allocation, or authority to make a purchase order.</summary>
public sealed class PrincipalRfqRecommendationDraftHead : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid RoundId { get; init; }
    public Guid UnsealingId { get; init; }
    public Guid SealId { get; init; }
    public Guid SourcingEventId { get; init; }
    public Guid BranchId { get; init; }
    public string ApprovedRoundSha256 { get; init; } = null!;
    public string OpeningSha256 { get; init; } = null!;
    public string OfferSetSha256 { get; init; } = null!;
    public Guid EvaluationPlanRevisionId { get; init; }
    public string EvaluationDefinitionSha256 { get; init; } = null!;
    public string MakerPrincipalId { get; init; } = null!;
    public Guid? MakerWorkerAliasId { get; init; }
    public int CurrentRevision { get; set; }
    public Guid CurrentRevisionId { get; set; }
    public string CurrentSnapshotSha256 { get; set; } = null!;
}

/// <summary>Append-only full replacement of the proposed selection. An empty
/// line set removes all proposed lines without deleting earlier history.</summary>
public sealed class PrincipalRfqRecommendationDraftRevision : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid DraftId { get; init; }
    public Guid RoundId { get; init; }
    public Guid UnsealingId { get; init; }
    public Guid SourcingEventId { get; init; }
    public Guid BranchId { get; init; }
    public int Revision { get; init; }
    public Guid? PreviousRevisionId { get; init; }
    public string? PreviousSnapshotSha256 { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public Guid? ActorWorkerAliasId { get; init; }
    public Guid CommandKey { get; init; }
    public string RequestSha256 { get; init; } = null!;
    public DateTimeOffset RecordedAt { get; init; }
    public string SnapshotJson { get; init; } = null!;
    public string SnapshotSha256 { get; init; } = null!;
}

public sealed class PrincipalRfqRecommendationDraftLine : ProcurementRecord
{
    public Guid DraftId { get; init; }
    public Guid RevisionId { get; init; }
    public Guid RoundId { get; init; }
    public Guid UnsealingId { get; init; }
    public Guid SourcingEventId { get; init; }
    public Guid BranchId { get; init; }
    public Guid OfferId { get; init; }
    public string OfferSha256 { get; init; } = null!;
    public Guid SupplierId { get; init; }
    public Guid SourcingLineId { get; init; }
    public string QuantityExact { get; init; } = null!;
    public string ProposedValueExact { get; init; } = null!;
    public string CurrencyCode { get; init; } = null!;
    public string Rationale { get; init; } = null!;
}

/// <summary>Original actor-owned acknowledgement; no competitor offer body or
/// other evaluator's assessment is copied into recovery.</summary>
public sealed class PrincipalRfqRecommendationDraftReceipt : ProcurementRecord
{
    public Guid DraftId { get; init; }
    public Guid RevisionId { get; init; }
    public Guid RoundId { get; init; }
    public Guid SourcingEventId { get; init; }
    public Guid BranchId { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public Guid CommandKey { get; init; }
    public string RequestSha256 { get; init; } = null!;
    public string ResponseJson { get; init; } = null!;
    public string ResponseSha256 { get; init; } = null!;
}
