namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Append-only response recovery for an exactly fingerprinted approval command.</summary>
public sealed class ProcurementApprovalCommandReceipt : ProcurementRecord
{
    public string Operation { get; init; } = null!;
    public Guid CommandKey { get; init; }
    public string ActorSubjectId { get; init; } = null!;
    public Guid PurchaseRequestId { get; init; }
    public Guid CoreApprovalCaseId { get; init; }
    public Guid? CoreAssignmentId { get; init; }
    public string CommandFingerprint { get; init; } = null!;
    public string ResponseJson { get; init; } = null!;
    public DateTimeOffset CompletedAt { get; init; }
}

/// <summary>Procurement-owned purchasing eligibility pinned to a Core assignment, not a second task ledger.</summary>
public sealed class PurchaseRequestAssignmentAuthorityEvidence : ProcurementRecord
{
    public Guid SubmissionId { get; init; }
    public Guid CoreApprovalCaseId { get; init; }
    public Guid CoreStageId { get; init; }
    public Guid CoreAssignmentId { get; init; }
    public Guid? AssigneeWorkerId { get; init; }
    public string PrincipalId { get; init; } = null!;
    public string AssigneeKind { get; init; } = null!;
    public Guid GrantId { get; init; }
    public long GrantRevision { get; init; }
    public int AuthorityVersion { get; init; }
    public string EvidenceJson { get; init; } = null!;
    public string EvidenceSha256 { get; init; } = null!;
}
