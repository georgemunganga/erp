namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Append-only Procurement evidence of canonical Core people observed at a grant action.
/// Principal/worker masters remain Core-owned. Record versions are snapshots, never FK keys.</summary>
public sealed class SpendingAuthorityIdentityRecord : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid GrantId { get; init; }
    public string AuthorityReference { get; init; } = null!;
    public int AuthorityVersion { get; init; }
    public string Permission { get; init; } = null!;
    public string Action { get; init; } = null!;
    public string ConfigurationOwnerPrincipalId { get; init; } = null!;
    public string RecipientPrincipalId { get; init; } = null!;
    public Guid? RecipientWorkerId { get; init; }
    public string Stage { get; init; } = null!;
    public string ActorPrincipalId { get; init; } = null!;
    public Guid? ActorWorkerId { get; init; }
    public long GrantRecordVersionAtAction { get; init; }
    public string BusinessFingerprint { get; init; } = null!;
    public DateTimeOffset ActionAt { get; init; }
    public Guid? CreatedEvidenceId { get; init; }
    public string? CreatedEvidenceStage { get; init; }
    public Guid? SubmittedEvidenceId { get; init; }
    public string? SubmittedEvidenceStage { get; init; }
    public string EvidenceSha256 { get; init; } = null!;
}
