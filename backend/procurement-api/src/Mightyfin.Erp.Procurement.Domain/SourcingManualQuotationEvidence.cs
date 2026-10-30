namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Proposed append-only principal capture contract, not yet EF-registered.
/// Records a buyer's report of contact outside the ERP; it never sends a message.</summary>
public sealed class SourcingManualContact : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid SourcingEventId { get; init; }
    public Guid InvitationId { get; init; }
    public Guid SupplierId { get; init; }
    public Guid BranchId { get; init; }
    public Guid PublicationOpeningId { get; init; }
    public Guid PublicationDecisionId { get; init; }
    public long EventVersionAtCapture { get; init; }
    public string ActorPrincipalId { get; init; } = "";
    public Guid? ActorCoreLinkedWorkerId { get; init; }
    public string Channel { get; init; } = "";
    public string Reference { get; init; } = "";
    public DateTimeOffset ReportedContactAt { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public Guid CommandKey { get; init; }
    public string CommandSha256 { get; init; } = "";
    public string ResponseJson { get; init; } = "";
    public string ResponseSha256 { get; init; } = "";
    public int ResponseStatusCode { get; init; } = 201;
}

/// <summary>Proposed append-only exact quotation version AND owned command receipt.
/// The existing SupplierBid/Line remain the commercial records; BidVersion pins
/// their existing optimistic version. This does not prove supplier submission.</summary>
public sealed class SourcingManualQuoteVersion : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid SourcingEventId { get; init; }
    public Guid InvitationId { get; init; }
    public Guid SupplierId { get; init; }
    public Guid SupplierBidId { get; init; }
    public Guid BranchId { get; init; }
    public Guid ManualContactId { get; init; }
    public Guid? PreviousVersionId { get; init; }
    public long BidVersion { get; init; }
    public string Operation { get; init; } = "";
    public string ActorPrincipalId { get; init; } = "";
    public Guid? ActorCoreLinkedWorkerId { get; init; }
    public string SnapshotJson { get; init; } = "";
    public string SnapshotSha256 { get; init; } = "";
    public Guid CommandKey { get; init; }
    public string CommandSha256 { get; init; } = "";
    public string ResponseJson { get; init; } = "";
    public string ResponseSha256 { get; init; } = "";
    public int ResponseStatusCode { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
}
