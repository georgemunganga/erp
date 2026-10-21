namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Immutable observed Core identity references for new SOURCING policy actions.
/// Worker/account masters remain Core-owned. Record revision is evidence, never a mutable FK key.</summary>
public sealed class SourcingPolicyIdentityEvidence : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
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
    public string EvidenceSha256 { get; set; } = null!;
}
