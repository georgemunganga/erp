namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Procurement evidence of Finance's confirmed commitment; never a financial balance or posting.</summary>
public sealed class PurchaseOrderFinanceEvidence : ProcurementRecord
{
    public Guid PurchaseOrderId { get; init; }
    public Guid SupplierId { get; init; }
    public int CommercialRevision { get; init; }
    public string CommitmentReference { get; init; } = null!;
    public string EvidenceReference { get; init; } = null!;
    public string EvidenceMode { get; init; } = null!;
    public string ApprovedManualPolicyReference { get; init; } = null!;
    public string Status { get; init; } = null!;
    public string CurrencyCode { get; init; } = null!;
    public decimal ConfirmedGrossAmount { get; init; }
    public string SourceReservationsJson { get; init; } = null!;
    public string CommercialSnapshotSha256 { get; init; } = null!;
    public string CommercialSnapshotJson { get; init; } = null!;
    public Guid BuyerWorkerId { get; init; }
    public string ParticipantWorkersJson { get; init; } = null!;
    public string WorkerIdentitySha256 { get; init; } = null!;
    public string VerifiedByPrincipalId { get; init; } = null!;
    public Guid VerifiedByWorkerId { get; init; }
    public DateTimeOffset ConfirmedAt { get; init; }
    public string? ReplacesCommitmentReference { get; init; }
}

/// <summary>Immutable commercial authority and input pin; Core owns the tasks and decisions.</summary>
public sealed class PurchaseOrderSubmission : ProcurementRecord
{
    public Guid PurchaseOrderId { get; init; }
    public int CommercialRevision { get; init; }
    public long SubmittedRowVersion { get; init; }
    public Guid CoreApprovalCaseId { get; init; }
    public Guid PolicyVersionId { get; init; }
    public int PolicyVersionNumber { get; init; }
    public Guid FinanceEvidenceId { get; init; }
    public string BuyerPrincipalId { get; init; } = null!;
    public Guid BuyerWorkerId { get; init; }
    public string ParticipantWorkersJson { get; init; } = null!;
    public string WorkerIdentitySha256 { get; init; } = null!;
    public string CurrencyCode { get; init; } = null!;
    public string AmountBasis { get; init; } = null!;
    public decimal LineBaseAmount { get; init; }
    public decimal GrossAmount { get; init; }
    public string CommercialSnapshotJson { get; init; } = null!;
    public string CommercialSnapshotSha256 { get; init; } = null!;
    public string SourceEvidenceJson { get; init; } = null!;
    public string PolicyRulesSha256 { get; init; } = null!;
    public string PolicyDecisionJson { get; init; } = null!;
    public DateTimeOffset SubmittedAt { get; init; }
}

public sealed class PurchaseOrderAssignmentAuthorityEvidence : ProcurementRecord
{
    public Guid SubmissionId { get; init; }
    public Guid CoreApprovalCaseId { get; init; }
    public Guid CoreAssignmentId { get; init; }
    public Guid CoreStageId { get; init; }
    public Guid GrantId { get; init; }
    public long GrantRevision { get; init; }
    public int AuthorityVersion { get; init; }
    public string PrincipalId { get; init; } = null!;
    public Guid AssigneeWorkerId { get; init; }
    public string AssigneeKind { get; init; } = null!;
    public string AuthorityEvidenceJson { get; init; } = null!;
    public string AuthorityEvidenceSha256 { get; init; } = null!;
}

/// <summary>One immutable supplier-facing commercial version; an outbox item alone is not successful delivery.</summary>
public sealed class PurchaseOrderIssuedVersion : ProcurementRecord
{
    public Guid PurchaseOrderId { get; init; }
    public Guid SubmissionId { get; init; }
    public int RevisionNumber { get; init; }
    public string CommercialSnapshotJson { get; init; } = null!;
    public string CommercialSnapshotSha256 { get; init; } = null!;
    public string FinanceCommitmentReference { get; init; } = null!;
    public string IssueEffectKey { get; init; } = null!;
    public string IssuedByPrincipalId { get; init; } = null!;
    public string ParticipantWorkersJson { get; init; } = null!;
    public DateTimeOffset AuthorizedAt { get; init; }
}

public sealed class PurchaseOrderDeliveryEvidence : ProcurementRecord
{
    public Guid IssuedVersionId { get; init; }
    public string DeliveryReference { get; init; } = null!;
    public string Outcome { get; init; } = null!;
    public string Channel { get; init; } = null!;
    public string EvidenceReference { get; init; } = null!;
    public string VerifiedByPrincipalId { get; init; } = null!;
    public Guid VerifiedByWorkerId { get; init; }
    public DateTimeOffset ObservedAt { get; init; }
}

/// <summary>Retry recovery for Procurement commercial commands; not a second approval/task ledger.</summary>
public sealed class ProcurementCommercialCommandReceipt : ProcurementRecord
{
    public Guid PurchaseOrderId { get; init; }
    public string Operation { get; init; } = null!;
    public Guid CommandKey { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public string Fingerprint { get; init; } = null!;
    public string ResponseJson { get; init; } = null!;
    public DateTimeOffset CompletedAt { get; init; }
}

/// <summary>Finance attests release of the PO commitment and restoration of the original PR
/// reservations. Procurement records evidence only; no balance is changed here.</summary>
public sealed class PurchaseOrderFinanceResolution : ProcurementRecord
{
    public Guid PurchaseOrderId { get; init; }
    public Guid? SubmissionId { get; init; }
    public Guid FinanceEvidenceId { get; init; }
    public string CommitmentReference { get; init; } = null!;
    public string EvidenceReference { get; init; } = null!;
    public string ApprovedManualPolicyReference { get; init; } = null!;
    public string SourceReservationsJson { get; init; } = null!;
    public decimal ReleasedGrossAmount { get; init; }
    public string CurrencyCode { get; init; } = null!;
    public string Outcome { get; init; } = null!;
    public string Reason { get; init; } = null!;
    public string VerifiedByPrincipalId { get; init; } = null!;
    public Guid VerifiedByWorkerId { get; init; }
    public DateTimeOffset ConfirmedAt { get; init; }
}

/// <summary>One upstream Finance commitment reference belongs to one PO for its entire history.
/// This claim is not a balance, reservation, posting or payment.</summary>
public sealed class PurchaseOrderCommitmentOwnership : ProcurementRecord
{
    public Guid PurchaseOrderId { get; init; }
    public string CommitmentReference { get; init; } = null!;
}
