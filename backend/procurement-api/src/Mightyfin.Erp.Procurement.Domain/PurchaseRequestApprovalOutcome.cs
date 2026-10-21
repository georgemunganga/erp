namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Append-only Procurement source effect; Core retains the underlying decision.</summary>
public sealed class PurchaseRequestApprovalOutcome : ProcurementRecord
{
    public Guid SubmissionId { get; init; }
    public Guid PurchaseRequestId { get; init; }
    public long SubmittedBusinessVersion { get; init; }
    public Guid CoreApprovalCaseId { get; init; }
    public Guid CoreDecisionId { get; init; }
    public string DecisionCommandKey { get; init; } = null!;
    public string Outcome { get; init; } = null!;
    public string EffectKey { get; init; } = null!;
    public string ActorSubjectId { get; init; } = null!;
    public DateTimeOffset AppliedAt { get; init; }
}

/// <summary>A durable request to Finance, never a budget release or ledger entry itself.</summary>
public sealed class PurchaseRequestFinanceReleaseRequest : ProcurementRecord
{
    public Guid ApprovalOutcomeId { get; init; }
    public string Outcome { get; init; } = null!;
    public Guid SubmissionId { get; init; }
    public Guid PurchaseRequestId { get; init; }
    public long SubmittedBusinessVersion { get; init; }
    public Guid CoreApprovalCaseId { get; init; }
    public Guid FinanceSnapshotId { get; init; }
    public Guid? BranchRef { get; init; }
    public string ReservationRef { get; init; } = null!;
    public decimal ReservedAmount { get; init; }
    public string CurrencyCode { get; init; } = null!;
    public string EffectKey { get; init; } = null!;
    // Queue ownership is explicit; dispatch attempts/failures are tracked by the shared outbox.
    // Pending is derived until a matching append-only confirmation exists.
    public string OwnerQueue { get; init; } = "FinanceReservationRelease";
    public string RequestedBySubjectId { get; init; } = null!;
    public DateTimeOffset RequestedAt { get; init; }
}

/// <summary>Append-only resolved evidence. Absence means pending/unknown, never released.</summary>
public sealed class PurchaseRequestFinanceReleaseConfirmation : ProcurementRecord
{
    public Guid ReleaseRequestId { get; init; }
    public string ReservationRef { get; init; } = null!;
    public decimal ReleasedAmount { get; init; }
    public string CurrencyCode { get; init; } = null!;
    public string EvidenceRef { get; init; } = null!;
    public string EvidenceSource { get; init; } = null!;
    public string VerifiedBySubjectId { get; init; } = null!;
    public DateTimeOffset FinanceConfirmedAt { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public string EffectKey { get; init; } = null!;
}
