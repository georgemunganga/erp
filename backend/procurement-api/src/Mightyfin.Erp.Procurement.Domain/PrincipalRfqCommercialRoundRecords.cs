namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Immutable opening of a distinct commercial Core case. Approval of the
/// sourcing publication or preparation plan is never commercial approval.</summary>
public sealed class PrincipalRfqCommercialRoundOpeningRecord : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid SourcingEventId { get; init; }
    public Guid PreparedPlanId { get; init; }
    public Guid CommercialPolicyVersionId { get; init; }
    public string CommercialPolicyKey { get; init; } = "SOURCING_COMMERCIAL_ROUND";
    public Guid PublishedPolicyIdentityId { get; init; }
    public string PublishedPolicyIdentityStage { get; init; } = "Published";
    public Guid CoreCaseId { get; init; }
    public Guid BranchId { get; init; }
    public long EventVersion { get; init; }
    public string PreparedPlanSha256 { get; init; } = null!;
    public string PolicyRulesSha256 { get; init; } = null!;
    public string ActorPrincipalId { get; init; } = null!;
    public Guid? ActorWorkerAliasId { get; init; }
    public string ClosingOfficerPrincipalId { get; init; } = null!;
    public Guid? ClosingOfficerWorkerAliasId { get; init; }
    public DateTimeOffset OpensAt { get; init; }
    public DateTimeOffset ClosesAt { get; init; }
    public string SnapshotJson { get; init; } = null!;
    public string SnapshotSha256 { get; init; } = null!;
}

/// <summary>One retained Core vote, including a Pending One/All-stage vote. The
/// separate command receipt contains the original actor-owned HTTP result.</summary>
public sealed class PrincipalRfqCommercialRoundVoteRecord : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid OpeningId { get; init; }
    public Guid SourcingEventId { get; init; }
    public Guid BranchId { get; init; }
    public Guid CoreCaseId { get; init; }
    public Guid CoreDecisionId { get; init; }
    public Guid CoreAssignmentId { get; init; }
    public string Action { get; init; } = null!;
    public string CaseState { get; init; } = null!;
    public long CaseVersion { get; init; }
    public long AssignmentVersion { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public Guid? ActorWorkerAliasId { get; init; }
    public DateTimeOffset DecidedAt { get; init; }
}

/// <summary>One retained terminal Core decision. Approved is the sole state that
/// may make an offer window available; a bare state string grants nothing.</summary>
public sealed class PrincipalRfqCommercialRoundDecisionRecord : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid OpeningId { get; init; }
    public Guid SourcingEventId { get; init; }
    public Guid CoreCaseId { get; init; }
    public Guid CoreDecisionId { get; init; }
    public Guid TerminalVoteId { get; init; }
    public Guid CoreAssignmentId { get; init; }
    public Guid BranchId { get; init; }
    public string State { get; init; } = null!;
    public string ActorPrincipalId { get; init; } = null!;
    public Guid? ActorWorkerAliasId { get; init; }
    public string OpeningSha256 { get; init; } = null!;
    public string CoreOutcomeSha256 { get; init; } = null!;
    public DateTimeOffset DecidedAt { get; init; }
}

/// <summary>Append-only surrogate offer. Recorder is an internal Core account,
/// never a claim that the supplier authenticated or accepted a portal invitation.</summary>
public sealed class PrincipalRfqCommercialOfferSubmissionRecord : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid OpeningId { get; init; }
    public Guid ApprovedDecisionId { get; init; }
    public Guid SourcingEventId { get; init; }
    public Guid BranchId { get; init; }
    public Guid SupplierBidId { get; init; }
    public Guid InvitationId { get; init; }
    public Guid SupplierId { get; init; }
    public int DraftRevision { get; init; }
    public long BidVersion { get; init; }
    public Guid OriginalDocumentVersionId { get; init; }
    public string OriginalDocumentSha256 { get; init; } = null!;
    public string RecorderPrincipalId { get; init; } = null!;
    public Guid? RecorderWorkerAliasId { get; init; }
    public DateTimeOffset ReportedReceivedAt { get; init; }
    public DateTimeOffset SubmittedAt { get; init; }
    public string SnapshotJson { get; init; } = null!;
    public string SnapshotSha256 { get; init; } = null!;
}

/// <summary>Explicit manual close; supplier offers remain sealed evidence, not award.</summary>
public sealed class PrincipalRfqCommercialRoundSealRecord : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid OpeningId { get; init; }
    public Guid ApprovedDecisionId { get; init; }
    public Guid SourcingEventId { get; init; }
    public Guid BranchId { get; init; }
    public string OfficerPrincipalId { get; init; } = null!;
    public Guid? OfficerWorkerAliasId { get; init; }
    public DateTimeOffset ClosedAt { get; init; }
    public int SubmissionCount { get; init; }
    public string SubmissionSetSha256 { get; init; } = null!;
}

/// <summary>One actor-owned command acknowledgment, used only after rechecking
/// current Core account, source scope, operation and retained command fingerprint.</summary>
public sealed class PrincipalRfqCommercialCommandReceipt : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid OpeningId { get; init; }
    public Guid SourcingEventId { get; init; }
    public Guid BranchId { get; init; }
    public string Operation { get; init; } = null!;
    public Guid CommandKey { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public Guid? TargetId { get; init; }
    public Guid? OpeningTargetId { get; init; }
    public Guid? VoteTargetId { get; init; }
    public Guid? DecisionTargetId { get; init; }
    public Guid? OfferTargetId { get; init; }
    public Guid? SealTargetId { get; init; }
    public string RequestSha256 { get; init; } = null!;
    public string ResponseJson { get; init; } = null!;
    public string ResponseSha256 { get; init; } = null!;
    public int ResponseStatus { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
}
