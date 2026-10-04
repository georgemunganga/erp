namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Authenticated scope used by every repository and audit entry.</summary>
public interface IProcurementScope
{
    string TenantId { get; }
    Guid LegalEntityId { get; }
    string SubjectId { get; }
    string? CorrelationId { get; }
}
