namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Authenticated scope used by every repository and audit entry.</summary>
public interface IProcurementScope
{
    string TenantId { get; }
    Guid LegalEntityId { get; }
    Guid? WorkLocationId { get; }
    Guid? OrgUnitId { get; }
    bool IsConfined { get; }
    string SubjectId { get; }
    // Identity comes from Core, never inferred from SubjectId or a role. Older/read-only
    // scopes may omit it; new worker-bound evidence requires a concrete current worker.
    Guid? WorkerId => null;
    string? CorrelationId { get; }
}
