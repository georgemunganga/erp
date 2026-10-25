using Mightyfin.Erp.Hrm.Application;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Core role-editor metadata. This read-only catalog contains no grants,
/// user assignments or permission changes.</summary>
public static class CoreRolePermissionCatalogRoutes
{
    public static void MapRoutes(WebApplication app)
        => app.MapGet("/api/core/v1/role-permissions", (HttpContext http) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { schemaVersion = 1, procurement = ProcurementActionCatalog.AdminReadCatalog() });
        }).RequireAuthorization("hrm-admin");
}
