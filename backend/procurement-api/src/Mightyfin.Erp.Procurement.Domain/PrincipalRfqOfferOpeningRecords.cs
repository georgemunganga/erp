namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Append-only release of one exact sealed offer set for named purpose
/// readers. This does not evaluate, recommend, award, dispatch or create a PO.</summary>
public sealed class PrincipalRfqOfferOpeningRecord : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid OpeningId { get; init; }
    public Guid SealId { get; init; }
    public Guid SourcingEventId { get; init; }
    public Guid BranchId { get; init; }
    public Guid PolicyVersionId { get; init; }
    public Guid PublishedPolicyIdentityId { get; init; }
    public string PolicyRulesSha256 { get; init; } = null!;
    public string ApprovedRoundSha256 { get; init; } = null!;
    public string OfficerPrincipalId { get; init; } = null!;
    public Guid? OfficerWorkerAliasId { get; init; }
    public Guid CommandKey { get; init; }
    public string RequestSha256 { get; init; } = null!;
    public DateTimeOffset OpenedAt { get; init; }
    public int OfferCount { get; init; }
    public string OfferSetSha256 { get; init; } = null!;
    public string Reason { get; init; } = null!;
    public string Reference { get; init; } = null!;
    public string SnapshotJson { get; init; } = null!;
    public string SnapshotSha256 { get; init; } = null!;
}

/// <summary>Actor-owned original acknowledgment. It contains no sealed price or
/// offer body; historical GET recovery does not require today's policy eligibility.</summary>
public sealed class PrincipalRfqOfferOpeningReceipt : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid UnsealingId { get; init; }
    public Guid OpeningId { get; init; }
    public Guid SourcingEventId { get; init; }
    public Guid BranchId { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public Guid CommandKey { get; init; }
    public string RequestSha256 { get; init; } = null!;
    public string ResponseJson { get; init; } = null!;
    public string ResponseSha256 { get; init; } = null!;
    public int ResponseStatus { get; init; } = 200;
}
