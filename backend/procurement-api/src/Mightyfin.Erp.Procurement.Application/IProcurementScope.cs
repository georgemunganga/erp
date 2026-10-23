namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Authenticated scope used by every repository and audit entry.</summary>
public interface IProcurementScope
{
    string TenantId { get; }
    Guid LegalEntityId { get; }
    Guid? WorkLocationId { get; }
    Guid? OrgUnitId { get; }
    bool IsConfined { get; }
    // An authenticated host may explicitly validate All branches for a named
    // workflow. Missing scope and all other implementations remain fail-closed.
    bool IsExplicitAllBranches => false;
    string SubjectId { get; }
    // Identity comes from Core, never inferred from SubjectId or a role. Older/read-only
    // scopes may omit it; new worker-bound evidence requires a concrete current worker.
    Guid? WorkerId => null;
    // New principal evidence may retain this genuine Core link solely as known-person
    // alias metadata. It is not active employment or a permission/scope grant. Host
    // overrides with its validated account link, including authoritative null. The
    // default preserves existing explicit owning/test scope contracts only.
    Guid? CoreLinkedWorkerId => WorkerId;
    string? CorrelationId { get; }
}
