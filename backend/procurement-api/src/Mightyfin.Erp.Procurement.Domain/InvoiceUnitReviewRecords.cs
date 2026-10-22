namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Immutable owned review proposal. Units are human attestations over exact original
/// document versions, never inferred from PO units or from this record's existence.</summary>
public sealed class InvoiceUnitReviewSource : ProcurementRecord
{
    public Guid SupplierInvoiceId { get; init; }
    public Guid MappingVersionId { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public Guid IssuedVersionId { get; init; }
    public Guid? BranchRef { get; init; }
    public long InvoiceVersionAtOpen { get; init; }
    public int MappingRevision { get; init; }
    public string OriginalSha256 { get; init; } = "";
    public string MappingSha256 { get; init; } = "";
    public Guid MatchingPolicyId { get; init; }
    public Guid ReviewPolicyId { get; init; }
    public Guid CoreCaseId { get; init; }
    public string MakerPrincipalId { get; init; } = "";
    public Guid MakerWorkerId { get; init; }
    public DateTimeOffset OpenedAt { get; init; }
    public string SourceJson { get; init; } = "";
    public string IssuedJson { get; init; } = "";
    public string SourceSha256 { get; init; } = "";
}
public sealed class InvoiceUnitReviewAssignmentEvidence : ProcurementRecord
{
    public Guid ReviewSourceId { get; init; }
    public Guid CoreCaseId { get; init; }
    public Guid CoreAssignmentId { get; init; }
    public string PrincipalId { get; init; } = "";
    public Guid WorkerId { get; init; }
}
public sealed class InvoiceUnitReviewOutcome : ProcurementRecord
{
    public Guid ReviewSourceId { get; init; }
    public Guid CoreCaseId { get; init; }
    public Guid FinalDecisionId { get; init; }
    public string ReviewerPrincipalId { get; init; } = "";
    public Guid ReviewerWorkerId { get; init; }
    public string State { get; init; } = "";
    public int EvidenceSchemaVersion { get; init; }
    public string EvidenceJson { get; init; } = "";
    public string EvidenceSha256 { get; init; } = "";
    public DateTimeOffset CompletedAt { get; init; }
}
public sealed class InvoiceUnitReviewCommandReceipt : ProcurementRecord
{
    public Guid ReviewSourceId { get; init; }
    public string Operation { get; init; } = "";
    public Guid CommandKey { get; init; }
    public string ActorPrincipalId { get; init; } = "";
    public Guid ActorWorkerId { get; init; }
    public string Fingerprint { get; init; } = "";
    public string RequestJson { get; init; } = "";
    public string ResponseJson { get; init; } = "";
    public DateTimeOffset CompletedAt { get; init; }
}
