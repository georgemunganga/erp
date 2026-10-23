namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Append-only exact opening of an internal RFQ publication review.
/// Supplier IDs are pinned in SnapshotJson; no supplier message is implied.</summary>
public sealed class SourcingPublicationOpening : ProcurementRecord
{
    public int SchemaVersion { get; set; } = 2;
    public Guid SourcingEventId { get; set; }
    public long EventVersionAtOpening { get; set; }
    public Guid PolicyVersionId { get; set; }
    public int PolicyVersionNumber { get; set; }
    public string PolicyRulesSha256 { get; set; } = null!;
    public Guid PublishedPolicyEvidenceId { get; set; }
    public Guid CoreApprovalCaseId { get; set; }
    public string MakerPrincipalId { get; set; } = null!;
    public Guid? MakerCoreLinkedWorkerId { get; set; }
    public string OriginalDraftFingerprint { get; set; } = null!;
    public string SnapshotJson { get; set; } = null!;
    public string SnapshotSha256 { get; set; } = null!;
    public DateTimeOffset OpenedAt { get; set; }
}

/// <summary>Append-only Core decision link. An Approved case is an internal
/// publication decision only; supplier delivery stays a separate action.</summary>
public sealed class SourcingPublicationDecision : ProcurementRecord
{
    public int SchemaVersion { get; set; } = 2;
    public Guid SourcingEventId { get; set; }
    public Guid OpeningId { get; set; }
    public Guid CoreApprovalCaseId { get; set; }
    public Guid CoreApprovalDecisionId { get; set; }
    public Guid CoreAssignmentId { get; set; }
    public string Action { get; set; } = null!;
    public string CaseState { get; set; } = null!;
    public string ActorPrincipalId { get; set; } = null!;
    public Guid? ActorCoreLinkedWorkerId { get; set; }
    public long EventVersionAtDecision { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
}

/// <summary>Exact actor-owned command acknowledgment. Replay rechecks current Core
/// action and branch before reading these bytes; this record never sends a notice.</summary>
public sealed class SourcingPublicationReceipt : ProcurementRecord
{
    public int SchemaVersion { get; set; } = 2;
    public Guid SourcingEventId { get; set; }
    public Guid CommandKey { get; set; }
    public string Operation { get; set; } = null!;
    public string ActorPrincipalId { get; set; } = null!;
    public Guid? ActorCoreLinkedWorkerId { get; set; }
    public Guid OpeningId { get; set; }
    public Guid? DecisionId { get; set; }
    public string CommandSha256 { get; set; } = null!;
    public string ResponseJson { get; set; } = null!;
    public string ResponseSha256 { get; set; } = null!;
    public int ResponseStatusCode { get; set; }
    public long ResultVersion { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
}

/// <summary>An employee-recorded external contact. This is evidence of a reported
/// manual delivery, never evidence of email or supplier portal dispatch.</summary>
public sealed class SourcingManualContact : ProcurementRecord
{
    public int SchemaVersion { get; set; } = 2;
    public Guid SourcingEventId { get; set; }
    public Guid InvitationId { get; set; }
    public Guid SupplierId { get; set; }
    public string ActorPrincipalId { get; set; } = null!;
    public Guid? ActorCoreLinkedWorkerId { get; set; }
    public string Channel { get; set; } = null!;
    public string Reference { get; set; } = null!;
    public DateTimeOffset ReportedContactAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public Guid CommandKey { get; set; }
    public string CommandSha256 { get; set; } = null!;
    public string ResponseJson { get; set; } = null!;
    public string ResponseSha256 { get; set; } = null!;
}

/// <summary>Append-only exact state of an internally entered quotation Draft.
/// The original SupplierBid/Line rows may change only alongside a new version.</summary>
public sealed class SourcingManualQuoteVersion : ProcurementRecord
{
    public int SchemaVersion { get; set; } = 2;
    public Guid SourcingEventId { get; set; }
    public Guid SupplierBidId { get; set; }
    public int Revision { get; set; }
    public long BidVersion { get; set; }
    public string Operation { get; set; } = null!;
    public string ActorPrincipalId { get; set; } = null!;
    public Guid? ActorCoreLinkedWorkerId { get; set; }
    public string SnapshotJson { get; set; } = null!;
    public string SnapshotSha256 { get; set; } = null!;
    public Guid CommandKey { get; set; }
    public string CommandSha256 { get; set; } = null!;
    public string ResponseJson { get; set; } = null!;
    public string ResponseSha256 { get; set; } = null!;
    public DateTimeOffset RecordedAt { get; set; }
}
