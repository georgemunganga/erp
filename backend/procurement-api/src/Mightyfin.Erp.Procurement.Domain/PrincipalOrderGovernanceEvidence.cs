namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Manual external commitment attestation, not a Finance posting or balance.</summary>
public sealed class PrincipalOrderFundingEvidence : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid PurchaseOrderId { get; init; }
    public int CommercialRevision { get; init; }
    public string OriginalDirectSourceSha256 { get; init; } = null!;
    public string CommercialSnapshotJson { get; init; } = null!;
    public string CommercialSnapshotSha256 { get; init; } = null!;
    public string ManualCommitmentReference { get; init; } = null!;
    public string ExternalEvidenceReference { get; init; } = null!;
    public string ApprovedManualPolicyReference { get; init; } = null!;
    public string SourceReservationsJson { get; init; } = null!;
    public decimal AttestedGrossAmount { get; init; }
    public string CurrencyCode { get; init; } = null!;
    public Guid? BuyerWorkerAliasId { get; init; }
    public string RecordedByPrincipalId { get; init; } = null!;
    public Guid? RecordedByWorkerAliasId { get; init; }
    public DateTimeOffset ObservedAt { get; init; }
}

/// <summary>One immutable PO policy/source/funding pin; Core owns the case.</summary>
public sealed class PrincipalOrderSubmission : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid PurchaseOrderId { get; init; }
    public int CommercialRevision { get; init; }
    public long SubmittedRowVersion { get; init; }
    public Guid CoreApprovalCaseId { get; init; }
    public Guid PolicyVersionId { get; init; }
    public int PolicyVersionNumber { get; init; }
    public string PolicyRulesSha256 { get; init; } = null!;
    public string PolicyDecisionJson { get; init; } = null!;
    public Guid FundingEvidenceId { get; init; }
    public string BuyerPrincipalId { get; init; } = null!;
    public Guid? BuyerWorkerAliasId { get; init; }
    public string OriginalDirectSourceSha256 { get; init; } = null!;
    public string CommercialSnapshotSha256 { get; init; } = null!;
    public string CommercialSnapshotJson { get; init; } = null!;
    public decimal LineBaseAmount { get; init; }
    public decimal GrossAmount { get; init; }
    public string CurrencyCode { get; init; } = null!;
    public DateTimeOffset SubmittedAt { get; init; }
}

/// <summary>Exact current PO grant and assigned Core task at opening time.</summary>
public sealed class PrincipalOrderAssignmentEvidence : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid SubmissionId { get; init; }
    public Guid CoreApprovalCaseId { get; init; }
    public Guid CoreStageId { get; init; }
    public Guid CoreAssignmentId { get; init; }
    public Guid GrantId { get; init; }
    public long GrantRevision { get; init; }
    public int AuthorityVersion { get; init; }
    public string PrincipalId { get; init; } = null!;
    public Guid? WorkerAliasId { get; init; }
    public string AuthorityEvidenceSha256 { get; init; } = null!;
}

/// <summary>Append-only mirror of a real Core decision; it grants no separate approval.</summary>
public sealed class PrincipalOrderDecisionEvidence : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid SubmissionId { get; init; }
    public Guid CoreApprovalCaseId { get; init; }
    public Guid CoreAssignmentId { get; init; }
    public Guid CoreDecisionId { get; init; }
    public string ActorPrincipalId { get; init; } = null!;
    public Guid? ActorWorkerAliasId { get; init; }
    public string Action { get; init; } = null!;
    public string? Reason { get; init; }
    public DateTimeOffset DecidedAt { get; init; }
}

/// <summary>Frozen authorized internal version. No supplier send is implied.</summary>
public sealed class PrincipalOrderIssuedVersion : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid PurchaseOrderId { get; init; }
    public Guid SubmissionId { get; init; }
    public Guid FundingEvidenceId { get; init; }
    public Guid CoreApprovalCaseId { get; init; }
    public int CommercialRevision { get; init; }
    public string CommercialSnapshotJson { get; init; } = null!;
    public string CommercialSnapshotSha256 { get; init; } = null!;
    public string OriginalDirectSourceSha256 { get; init; } = null!;
    public string ManualCommitmentReference { get; init; } = null!;
    public string AuthorizedByPrincipalId { get; init; } = null!;
    public Guid? AuthorizedByWorkerAliasId { get; init; }
    public DateTimeOffset AuthorizedAt { get; init; }
}

/// <summary>Manual witness of supplier response to an exact frozen version.</summary>
public sealed class PrincipalOrderAcknowledgment : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid IssuedVersionId { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public string Outcome { get; init; } = null!;
    public string Channel { get; init; } = null!;
    public string SupplierReference { get; init; } = null!;
    public string EvidenceReference { get; init; } = null!;
    public string RecordedByPrincipalId { get; init; } = null!;
    public Guid? RecordedByWorkerAliasId { get; init; }
    public DateTimeOffset ObservedAt { get; init; }
}

/// <summary>A durable hold. Requesting release never changes external Finance
/// state or frees the PR allocation. The original commitment remains unresolved.</summary>
public sealed class PrincipalOrderReleaseRequest : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid PurchaseOrderId { get; init; }
    public Guid FundingEvidenceId { get; init; }
    public string OriginalStatus { get; init; } = null!;
    public int CommercialRevision { get; init; }
    public long RequestedOrderVersion { get; init; }
    public string OriginalDirectSourceSha256 { get; init; } = null!;
    public string Reason { get; init; } = null!;
    public string RequestedByPrincipalId { get; init; } = null!;
    public Guid? RequestedByWorkerAliasId { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
}

/// <summary>Independent witness of an externally completed release, never a
/// Finance ledger posting. Quantity release occurs only with this same save.</summary>
public sealed class PrincipalOrderReleaseConfirmation : ProcurementRecord
{
    public int SchemaVersion { get; init; } = 2;
    public Guid PurchaseOrderId { get; init; }
    public Guid ReleaseRequestId { get; init; }
    public Guid FundingEvidenceId { get; init; }
    public string ManualCommitmentReference { get; init; } = null!;
    public string ExternalReleaseEvidenceReference { get; init; } = null!;
    public string ApprovedManualPolicyReference { get; init; } = null!;
    public string RestoredSourceReservationsJson { get; init; } = null!;
    public decimal ReleasedGrossAmount { get; init; }
    public string CurrencyCode { get; init; } = null!;
    public string ConfirmedByPrincipalId { get; init; } = null!;
    public Guid? ConfirmedByWorkerAliasId { get; init; }
    public DateTimeOffset ConfirmedAt { get; init; }
}
