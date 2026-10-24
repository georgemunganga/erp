namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Owned append-only policy authorship for contract review. This separate
/// table leaves the captured P31 operational-policy identity contract unchanged.</summary>
public sealed class ContractReviewPolicyIdentityRecord : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public string PolicyKey { get; init; } = null!;
    public Guid PolicyVersionId { get; init; }
    public int PolicyVersionNumber { get; init; }
    public string Stage { get; init; } = null!;
    public string ActorPrincipalId { get; init; } = null!;
    public Guid? ActorWorkerId { get; init; }
    public long PolicyRecordVersionAtAction { get; init; }
    public string RulesSha256 { get; init; } = null!;
    public DateTimeOffset ActionAt { get; init; }
    public Guid? CreatedEvidenceId { get; init; }
    public string? CreatedEvidenceStage { get; init; }
    public Guid? SubmittedEvidenceId { get; init; }
    public string? SubmittedEvidenceStage { get; init; }
    public string EvidenceSha256 { get; init; } = null!;
}
