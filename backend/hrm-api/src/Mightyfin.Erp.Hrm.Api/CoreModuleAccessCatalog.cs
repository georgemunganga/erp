using Mightyfin.Erp.Hrm.Application;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Explicit module snapshots only. Never calls the legacy role-action bootstrap resolver.</summary>
internal static class CoreModuleAccessCatalog
{
    internal static string[] Roles(string module)=>module switch {
        "hrm"=>HrmStaffAccess.Roles.Where(x=>x!="erp_admin").Order(StringComparer.Ordinal).ToArray(),
        "procurement"=>ProcurementActionCatalog.BootstrapGrants.Values.SelectMany(x=>x).Where(x=>x!="erp_admin").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
        _=>[]
    };
    internal static string[] Actions(string module)=>module switch {
        "hrm"=>["hrm-access","hrm-admin"],
        "procurement"=>ProcurementActionCatalog.BootstrapGrants.Keys.Order(StringComparer.Ordinal).ToArray(),
        _=>[]
    };
    internal static bool Valid(string module,string[] roles,string[] actions)
    {
        var entry=module switch{"hrm"=>"hrm-access","procurement"=>"procurement-access",_=>""};
        return entry!="" && roles.Length<=16 && actions.Length is >0 and <=256
            && roles.Distinct(StringComparer.Ordinal).Count()==roles.Length && actions.Distinct(StringComparer.Ordinal).Count()==actions.Length
            && roles.All(Roles(module).Contains) && actions.All(Actions(module).Contains) && actions.Contains(entry,StringComparer.Ordinal)
            && (module!="hrm" || roles.Length>0 && (!actions.Contains("hrm-admin",StringComparer.Ordinal) || roles.Contains("hr_admin",StringComparer.Ordinal)));
    }
    internal static string[] Parse(string csv)=>csv==""?[]:csv.Split(',');
    internal static bool Canonical(string csv)=>string.Join(',',Parse(csv).Order(StringComparer.Ordinal))==csv
        && Parse(csv).Distinct(StringComparer.Ordinal).Count()==Parse(csv).Length;
}

internal enum ModuleAccessStatus { Changed,Unchanged,Existing,Authorized,Denied,Invalid,Conflict,Unavailable }
internal sealed record ModuleAccessResult(ModuleAccessStatus Status,Guid? VersionId=null,int Revision=0,string? State=null);
internal sealed record ModuleAccessCommand(Guid CompanyId,Guid MembershipId,int MembershipRevision,string ModuleCode,int ExpectedRevision,
    Guid CommandKey,bool Revoke,string[] RoleKeys,string[] Actions,string ScopeKind,Guid[] LocationIds,bool AllCompanyLocationsConfirmed,bool Confirm);

internal sealed record NormalizedModuleAccess(string[] Roles,string[] Actions,Guid[] Locations)
{
    internal static NormalizedModuleAccess? Read(ModuleAccessCommand command)
    {
        if(command.CompanyId==Guid.Empty || command.MembershipId==Guid.Empty || command.CommandKey==Guid.Empty || !command.Confirm
            || command.MembershipRevision<1 || command.ExpectedRevision<0 || command.RoleKeys is null || command.Actions is null || command.LocationIds is null
            || command.RoleKeys.Length>16 || command.Actions.Length>256 || command.LocationIds.Length>128 || command.ModuleCode is not ("hrm" or "procurement"))return null;
        if(command.Revoke)return command.RoleKeys.Length==0 && command.Actions.Length==0 && command.LocationIds.Length==0
            && command.ScopeKind=="none" && !command.AllCompanyLocationsConfirmed && command.ExpectedRevision>0?new([],[],[]):null;
        if(!CoreModuleAccessCatalog.Valid(command.ModuleCode,command.RoleKeys,command.Actions)
            || command.LocationIds.Distinct().Count()!=command.LocationIds.Length || command.LocationIds.Contains(Guid.Empty)
            || !(command.ScopeKind=="company" && command.AllCompanyLocationsConfirmed && command.LocationIds.Length==0
                || command.ScopeKind=="locations" && !command.AllCompanyLocationsConfirmed && command.LocationIds.Length>0))return null;
        return new(command.RoleKeys.Order(StringComparer.Ordinal).ToArray(),command.Actions.Order(StringComparer.Ordinal).ToArray(),command.LocationIds.Order().ToArray());
    }
}
