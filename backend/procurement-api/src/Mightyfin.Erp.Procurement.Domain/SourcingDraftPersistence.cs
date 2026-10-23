namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Original immutable schema1 RFQ draft, not publication/approval evidence.</summary>
public sealed class SourcingDraftEvidence : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid SourcingEventId { get; init; }
    public Guid? BranchRef { get; init; }
    public string MakerSubjectId { get; init; } = null!;
    public Guid? MakerWorkerId { get; init; }
    // Schema 2 preserves a Core principal even when no employee exists.
    public string? MakerPrincipalId { get; init; }
    public Guid CreateCommandKey { get; init; }
    public string OriginalDraftFingerprint { get; init; } = null!;
    public string PayloadJson { get; init; } = null!;
    public string PayloadSha256 { get; init; } = null!;
}

/// <summary>Immutable relationship/evidence projection. The existing PR allocation is the only hold.</summary>
public sealed class SourcingDemandBinding : ProcurementRecord
{
    public Guid SourcingEventId { get; init; }
    public Guid SourceEvidenceId { get; init; }
    public Guid SourcingLineId { get; init; }
    public Guid PurchaseRequestId { get; init; }
    public Guid PurchaseRequestLineId { get; init; }
    public long RequestVersion { get; init; }
    public Guid SubmissionId { get; init; }
    public long SubmittedVersion { get; init; }
    public Guid CoreApprovalCaseId { get; init; }
    public Guid ApprovalOutcomeId { get; init; }
    public Guid CoreDecisionId { get; init; }
    public string RequestSha { get; init; } = null!;
    public string PolicyDecisionSha { get; init; } = null!;
    public string PolicyRulesSha { get; init; } = null!;
    public Guid FinanceSnapshotId { get; init; }
    public string FinanceSha { get; init; } = null!;
    public Guid MethodId { get; init; }
    public long MethodRevision { get; init; }
    public string MethodCode { get; init; } = null!;
    public int MinimumQuoteCount { get; init; }
    public int IdentitySchemaVersion { get; init; } = 1;
    public Guid? MakerWorkerId { get; init; }
    public Guid? RequesterWorkerId { get; init; }
    public string? MakerPrincipalId { get; init; }
    public string? RequesterPrincipalId { get; init; }
    public string? PrincipalIdentitySha256 { get; init; }
    public Guid AllocationId { get; init; }
    public string TargetType { get; init; } = "SourcingEvent";
    public decimal Quantity { get; init; }
    public decimal UnitEstimate { get; init; }
    public decimal EstimatedValue { get; init; }
    public string UnitCode { get; init; } = null!;
    public string? CategoryCode { get; init; }
    public Guid? DepartmentRef { get; init; }
}

/// <summary>RFQ-owned durable original-command recovery; never backed by a placeholder PO.</summary>
public sealed class SourcingCommandReceipt : ProcurementRecord
{
    public Guid SourcingEventId { get; init; }
    public string Operation { get; init; } = null!;
    public Guid CommandKey { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public Guid? ActorWorkerId { get; init; }
    public int ResponseSchemaVersion { get; init; } = 1;
    public int ResponseStatusCode { get; init; }
    public long ResultVersion { get; init; }
    public string CommandFingerprint { get; init; } = null!;
    public string ResponseJson { get; init; } = null!;
    public string ResponseSha256 { get; init; } = null!;
    public DateTimeOffset CompletedAt { get; init; }
}
