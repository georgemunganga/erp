namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Append-only observed people behind RECEIVING and INVOICE_MATCHING policy actions.
/// Core owns identity masters. Stable policy ownership and typed stage lineage belong here;
/// mutable owner revisions are retained values, never foreign-key identities.</summary>
public sealed class OperationalPolicyIdentityRecord : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public string PolicyKey { get; init; } = null!;
    public Guid PolicyVersionId { get; init; }
    public int PolicyVersionNumber { get; init; }
    public string Stage { get; init; } = null!;
    public string ActorPrincipalId { get; init; } = null!;
    public Guid ActorWorkerId { get; init; }
    public long PolicyRecordVersionAtAction { get; init; }
    public string RulesSha256 { get; init; } = null!;
    public DateTimeOffset ActionAt { get; init; }
    public Guid? CreatedEvidenceId { get; init; }
    public string? CreatedEvidenceStage { get; init; }
    public Guid? SubmittedEvidenceId { get; init; }
    public string? SubmittedEvidenceStage { get; init; }
    public string EvidenceSha256 { get; init; } = null!;
}
