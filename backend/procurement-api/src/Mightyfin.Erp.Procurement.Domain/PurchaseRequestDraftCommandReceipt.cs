namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Original completed Create/Update/Submit result owned by Procurement.
/// Payload fields are init-only. Persistence must additionally protect inherited record
/// scope and append-only storage; this type does not itself register a table or authority.
/// Optional worker provenance remains only in the original request/result.</summary>
public sealed class PurchaseRequestDraftCommandReceipt : ProcurementRecord
{
    public string Operation { get; init; } = "";
    public Guid CommandKey { get; init; }
    public Guid ActorPrincipalId { get; init; }
    public Guid PurchaseRequestId { get; init; }
    public Guid? ContextBranchRef { get; init; }
    public Guid? ContextDepartmentRef { get; init; }
    public Guid? OriginalBranchRef { get; init; }
    public Guid? OriginalDepartmentRef { get; init; }
    public long? ExpectedVersion { get; init; }
    public int FingerprintSchemaVersion { get; init; }
    public string CommandFingerprint { get; init; } = "";
    public int ResponseSchemaVersion { get; init; }
    public int ResponseStatusCode { get; init; }
    public long ResultVersion { get; init; }
    public string ResponseJson { get; init; } = "";
    public string ResponseSha256 { get; init; } = "";
    public DateTimeOffset CompletedAt { get; init; }
}
