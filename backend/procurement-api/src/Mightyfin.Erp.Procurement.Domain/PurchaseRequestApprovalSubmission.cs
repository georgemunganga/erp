using System.Text.Json.Serialization;

namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>
/// Procurement's immutable business evidence for one submitted request version.
/// Core owns execution records; this record grants no approval or spending authority.
/// Persistence must enforce append-only history and scoped unique source/case keys.
/// </summary>
public sealed class PurchaseRequestApprovalSubmission : ProcurementRecord
{
    public int SnapshotSchemaVersion { get; init; } = 1;
    public Guid PurchaseRequestId { get; init; }
    public long SubmittedBusinessVersion { get; init; }
    public Guid CoreApprovalCaseId { get; init; }
    public Guid OpeningCommandKey { get; init; }
    // Nullable only for unchanged pre-identity-evidence v1 history. Execution rejects missing values.
    public Guid? MakerWorkerId { get; init; }
    public Guid? RequesterWorkerId { get; init; }
    public string? WorkerIdentitySha256 { get; init; }
    public string MakerSubjectId { get; init; } = null!;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? RequesterPrincipalId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? RequesterIdentitySha256 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PrincipalIdentitySha256 { get; init; }
    public string RequestSnapshotJson { get; init; } = null!;
    public string RequestSnapshotSha256 { get; init; } = null!;
    public Guid PolicyEvaluationId { get; init; }
    public Guid PolicyVersionId { get; init; }
    public int PolicyVersionNumber { get; init; }
    public string PolicyDecisionSha256 { get; init; } = null!;
    public string PolicyRulesSha256 { get; init; } = null!;
    // Independent of maker/requester SnapshotSchemaVersion. Zero preserves original Finance bytes.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FinanceIdentitySchemaVersion { get; init; }
    public Guid FinanceSnapshotId { get; init; }
    public string FinanceSnapshotJson { get; init; } = null!;
    public string FinanceSnapshotSha256 { get; init; } = null!;
    public string FinanceEvidenceMode { get; init; } = null!;
    public string? ManualEvidenceAuthorityRef { get; init; }
    public string AmountBasis { get; init; } = null!;
    public decimal EstimatedAmount { get; init; }
    public string CurrencyCode { get; init; } = null!;
    public DateTimeOffset PinnedAt { get; init; }
}
