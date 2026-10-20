using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Trusted server-resolved projections, not client declarations. Runtime adapters must
/// resolve current Core authority (SiteIds are active authorized Core sites), verify configured
/// published policy/classification/owner decisions, and lock complete source/effect/consumption sets atomically.</summary>
public sealed record ReceivingSourceProjection(
    PurchaseOrderIssuedVersion Issued, PurchaseOrderSubmission Submission,
    PurchaseOrderDeliveryEvidence Delivery, ReceivingSourcePin Pin,
    Guid BuyerWorkerId, string ClassificationEvidenceJson, bool CurrentIssuedAuthority);

public sealed record ReceivingActorProjection(
    string PrincipalId, Guid WorkerId, bool Active, string TenantId, Guid LegalEntityId,
    Guid? SelectedBranchId, bool OrganisationWide, IReadOnlySet<Guid> BranchIds,
    IReadOnlySet<Guid> SiteIds, IReadOnlySet<string> Permissions);

public sealed record ReceivingPolicyProjection(
    Guid VersionId, string TenantId, Guid LegalEntityId, Guid? BranchId,
    string CategoryCode, ReceivingKind Kind, bool Published,
    DateTimeOffset EffectiveFrom, DateTimeOffset? EffectiveTo,
    bool InspectionRequired, string? ChecklistReference,
    bool AllowRequesterCertification, bool AllowBuyerCertification,
    bool AllowReceiverCertification, bool RequireIndependentInspector,
    IReadOnlySet<Guid> InspectorWorkerIds, IReadOnlySet<Guid> ServiceOwnerWorkerIds,
    IReadOnlySet<Guid> CorrectionWorkerIds, IReadOnlySet<Guid> ReceivingSiteIds);

/// <summary>Complete authoritative ledger for this exact issued PO line, not a page of results.
/// Sequence is checked by the future transactional adapter; pure methods return facts only.</summary>
public sealed record ReceivingLedger(
    ReceivingSourcePin Source, long Sequence, bool Complete,
    IReadOnlyList<GoodsPhysicalObservation> Observations,
    IReadOnlyList<GoodsInspectionEvidence> Inspections,
    IReadOnlyList<ReceivingAcceptanceEffect> Effects);

/// <summary>Evidence from matching/AP and configured Inventory/Asset owners. Unknown is never
/// zero. Owner compensation must refer to this proposed correction, not a generic acknowledgement.</summary>
public sealed record ReceivingConsumptionProjection(
    ReceivingSourcePin Source, Guid AcceptanceRecordId, long AcceptanceSequence,
    string AcceptanceEffectsSha256, bool Complete, string EvidenceReference,
    decimal ConsumedQuantity, decimal ConsumedNetAmount, bool PostingBound,
    ReceivingOwnerCorrectionState OwnerState, Guid CorrectionEffectKey,
    string? OwnerEvidenceReference);

/// <summary>Resolved from the configured correction/return authority (Core case when required).
/// Physical dispatch is independently verified; a requested/approved return is not a dispatch.</summary>
public sealed record ReceivingCorrectionAuthorityProjection(
    ReceivingSourcePin Source, Guid OriginalAcceptanceEffectId, Guid EffectKey,
    AcceptanceEffectKind Kind, bool CurrentAndGranted, decimal AuthorizedQuantity,
    Guid AuthorizerWorkerId, string DecisionReference, bool DispatchVerified,
    string? DispatchEvidenceReference);

/// <summary>Matches InvoiceAcceptedFulfilment semantics: accepted excludes administrative
/// reversals; returned contains dispatched business returns only. Do not subtract either twice.</summary>
public sealed record ReceivingAcceptanceBalance(
    Guid AcceptanceRecordId, long Sequence, decimal AcceptedQuantity, decimal AcceptedNetAmount,
    decimal ReturnedQuantity, decimal ReturnedNetAmount, bool Reversed, string EffectsSha256)
{
    public decimal AvailableQuantity => AcceptedQuantity - ReturnedQuantity;
    public decimal AvailableNetAmount => AcceptedNetAmount - ReturnedNetAmount;
}
