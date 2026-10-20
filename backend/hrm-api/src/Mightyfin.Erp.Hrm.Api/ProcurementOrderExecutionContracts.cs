namespace Mightyfin.Erp.Hrm.Api;

public sealed record OrderSourceSelection(int DraftLineIndex, Guid PurchaseRequestId,
    Guid PurchaseRequestLineId, long ExpectedRequestVersion);
public sealed record ConvertApprovedOrderInput(OrderDraftInput Draft, IReadOnlyList<OrderSourceSelection> Sources);
public sealed record OrderVersionInput(long ExpectedOrderVersion);
public sealed record OrderSourceDraftInput(long ExpectedOrderVersion, string Reason, OrderDraftInput Draft);
public sealed record OrderDecisionInput(string Action, string? Reason, long ExpectedOrderVersion,
    long ExpectedCaseVersion, long ExpectedAssignmentVersion);
public sealed record OrderFinanceInput(long ExpectedOrderVersion, int CommercialRevision,
    string CommitmentReference, string EvidenceReference, string CurrencyCode, decimal ConfirmedGrossAmount,
    IReadOnlyList<string> SourceReservationReferences, DateTimeOffset ConfirmedAt,
    string? ReplacesCommitmentReference = null);
public sealed record OrderDeliveryInput(long ExpectedOrderVersion, Guid IssuedVersionId,
    string DeliveryReference, string Outcome, string Channel, string EvidenceReference, DateTimeOffset ObservedAt);
public sealed record OrderExecutionReply(Guid Id, string Number, string Status, long Version,
    int Revision, string? ApprovalInstanceRef, string NextStep);
public sealed record OrderFinanceResolutionInput(long ExpectedOrderVersion, string EvidenceReference,
    decimal ReleasedGrossAmount, string CurrencyCode, IReadOnlyList<string> RestoredReservationReferences,
    string Reason, DateTimeOffset ConfirmedAt);
public sealed record OrderSourceCancelInput(long ExpectedOrderVersion, string Reason);
