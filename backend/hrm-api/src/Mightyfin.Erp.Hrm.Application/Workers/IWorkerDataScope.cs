namespace Mightyfin.Erp.Hrm.Application.Workers;

/// <summary>Server-owned per-request authority, never a browser filter or role bootstrap.</summary>
public interface IWorkerDataScope
{
    bool Enabled {get;}
    bool IsCurrent {get;}
    string TenantId {get;}
    Guid CompanyId {get;}
    bool AllLocations {get;}
    Guid[] LocationIds {get;}
}
