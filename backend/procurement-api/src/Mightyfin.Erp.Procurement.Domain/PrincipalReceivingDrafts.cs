namespace Mightyfin.Erp.Procurement.Domain;

/// <summary>Owned revision snapshots. Persistence retains each prior revision; a revision
/// is not a physical observation and neither reserves nor accepts quantities.</summary>
public sealed record PrincipalGoodsDraftLine(Guid Id,PrincipalReceivingSource Source,decimal Quantity,decimal RejectedQuantity);
public sealed record PrincipalGoodsDraft(Guid Id,long Version,string TenantId,Guid LegalEntityId,
    Guid PurchaseOrderId,Guid IssuedVersionId,Guid SiteId,PrincipalReceivingPerson Creator,
    DateTimeOffset ObservedAt,string? DeliveryNoteReference,string? Notes,string State,
    IReadOnlyList<PrincipalGoodsDraftLine> Lines,string? CancellationReason=null);
public sealed record PrincipalServiceDraft(Guid Id,long Version,PrincipalReceivingSource Source,
    PrincipalReceivingPerson Creator,string State,string DeliverableReference,DateOnly PeriodFrom,
    DateOnly PeriodTo,decimal Quantity,string? Notes,string? CancellationReason=null);
