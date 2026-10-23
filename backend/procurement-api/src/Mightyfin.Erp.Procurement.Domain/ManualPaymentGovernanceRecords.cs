namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>
/// Immutable source-module link to the real shared Core approval case, not a second
/// approval engine. Approval opens at Submit; Confirmation opens at Record. Core
/// continues to own cases/stages/assignments/decisions and Procurement owns this pin.
/// </summary>
public sealed class ManualPaymentWorkflowCaseLink : ProcurementRecord
{
    public Guid ManualPaymentRequestId { get; init; }
    public Guid BranchId { get; init; }
    public string Purpose { get; init; } = null!;
    public long OpenedRequestVersion { get; init; }
    public Guid OpeningManualDecisionId { get; init; }
    public Guid PolicyVersionId { get; init; }
    public int PolicyVersionNumber { get; init; }
    public string PolicyRulesSha256 { get; init; } = null!;
    public Guid CoreApprovalCaseId { get; init; }
    public Guid CoreApprovalStageId { get; init; }
    public Guid CoreApprovalAssignmentId { get; init; }
    public string MakerPrincipalId { get; init; } = null!;
    public Guid? MakerCoreLinkedWorkerId { get; init; }
    public string AssigneePrincipalId { get; init; } = null!;
    public Guid? AssigneeCoreLinkedWorkerId { get; init; }
    public long InvoiceVersionAtOpening { get; init; }
    public decimal InvoiceGrossAmountAtOpening { get; init; }
    public string SnapshotJson { get; init; } = null!;
    public string SnapshotSha256 { get; init; } = null!;
    public DateTimeOffset OpenedAt { get; init; }
}

/// <summary>Append-only exact association between a source transition and its Core decision.
/// An approval/return/rejection or confirmation/dispute must never claim an unrelated task.</summary>
public sealed class ManualPaymentCoreDecisionLink : ProcurementRecord
{
    public Guid ManualPaymentRequestId { get; init; }
    public Guid BranchId { get; init; }
    public Guid WorkflowCaseLinkId { get; init; }
    public Guid ManualPaymentDecisionId { get; init; }
    public Guid CoreApprovalDecisionId { get; init; }
    public string ManualAction { get; init; } = null!;
    public string ActorPrincipalId { get; init; } = null!;
    public Guid? ActorCoreLinkedWorkerId { get; init; }
    public long FromRequestVersion { get; init; }
    public long ToRequestVersion { get; init; }
    public DateTimeOffset DecidedAt { get; init; }
}

/// <summary>Server-observed company date used for an exact Record/Confirm transition.
/// Core owns the company/timezone master. This is retained decision evidence only,
/// so a later Core timezone change cannot rewrite the meaning of historical checks.</summary>
public sealed class ManualPaymentBusinessDateObservation : ProcurementRecord
{
    public Guid ManualPaymentRequestId { get; init; }
    public Guid BranchId { get; init; }
    public Guid ManualPaymentDecisionId { get; init; }
    public long RequestVersion { get; init; }
    public string Action { get; init; } = null!;
    public DateOnly CompanyBusinessDate { get; init; }
    public string CompanyTimeZoneId { get; init; } = null!;
    public DateTimeOffset ObservedAtUtc { get; init; }
}
