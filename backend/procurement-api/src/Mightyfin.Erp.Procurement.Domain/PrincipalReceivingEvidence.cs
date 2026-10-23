namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Core principal is authoritative; the optional retained worker is denial-only alias evidence.</summary>
public sealed record PrincipalReceivingPerson(string PrincipalId,Guid? WorkerAliasId);

/// <summary>Versioned retained commercial facts. An Accepted supplier acknowledgment is
/// a witnessed response, never physical delivery or acceptance. Legacy receiving pins are unchanged.</summary>
public sealed record PrincipalReceivingSource(
    int SchemaVersion,ReceivingSourcePin Commercial,Guid SubmissionId,Guid FundingEvidenceId,
    Guid CoreApprovalCaseId,Guid SupplierAcknowledgmentId,string OriginalDirectSourceSha256,
    PrincipalReceivingPerson Buyer,PrincipalReceivingPerson Requester,
    DateTimeOffset IssuedAuthorizedAt,DateTimeOffset SupplierAcknowledgedAt);

/// <summary>Actual physical fact. It records excess truthfully; no acceptance or stock/Finance effect follows implicitly.</summary>
public sealed record PrincipalGoodsObservation(
    Guid Id,Guid ReceiptId,Guid ReceiptLineId,PrincipalReceivingSource Source,
    Guid SiteId,decimal ReceivedQuantity,decimal InitiallyRejectedQuantity,
    PrincipalReceivingPerson Receiver,string DeliveryNoteReference,string EvidenceReference,
    Guid PolicyVersionId,string PolicyRulesSha256,Guid CommandKey,long Sequence,
    DateTimeOffset ObservedAt,DateTimeOffset RecordedAt);

/// <summary>Final disposition over one exact physical fragment; reinspection needs a later explicit correction contract.</summary>
public sealed record PrincipalGoodsInspection(
    Guid Id,Guid ObservationId,PrincipalReceivingSource Source,PrincipalReceivingPerson Inspector,
    decimal InspectedQuantity,decimal QualifiedQuantity,ReceivingInspectionOutcome Outcome,
    string ChecklistReference,string EvidenceReference,Guid PolicyVersionId,string PolicyRulesSha256,
    Guid CommandKey,long Sequence,DateTimeOffset InspectedAt);

/// <summary>Positive Procurement acceptance only. No posting, stock, asset, return or available-balance claim.</summary>
public sealed record PrincipalReceivingAcceptance(
    Guid Id,Guid AcceptanceRecordId,PrincipalReceivingSource Source,Guid? ObservationId,Guid? InspectionId,
    decimal Quantity,decimal NetAmount,PrincipalReceivingPerson Actor,Guid PolicyVersionId,string PolicyRulesSha256,
    string EvidenceReference,string? DeliverableReference,DateOnly? PeriodFrom,DateOnly? PeriodTo,
    Guid CommandKey,long Sequence,DateTimeOffset AcceptedAt);
