using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record ProductHrmWorkspacePolicy;

/// <summary>Compatibility admission for the existing company-wide HR administrator
/// workflows. A selected branch grant is never translated into legacy global access.
/// Until a workflow has a narrower contract, every stored company location must be
/// covered, and Core must prove this is the single-company provisioned tenant.</summary>
internal static class ProductHrmWorkspace
{
    private static readonly object AdmissionKey = new();
    internal static bool HasAdmission(HttpContext http) => http.Items.ContainsKey(AdmissionKey);
    internal static void MarkAdmission(HttpContext http) => http.Items[AdmissionKey] = true;
    internal static bool Enabled(IConfiguration config) => ProductRequestAuthentication.Enabled(config)
        && config.GetValue<bool>("ERP:ProductHrmWorkspace:Enabled");
    internal static bool IsEndpoint(HttpContext http, IConfiguration config) => Enabled(config)
        && http.GetEndpoint()?.Metadata.GetMetadata<ProductHrmWorkspacePolicy>() is not null;
    internal static bool RoleAllows(CoreErpRequestScope? scope) => scope is { IsCurrent: true, Module: "hrm" }
        && scope.AllowsAction("hrm-access") && scope.AllowsAction("hrm-admin")
        && scope.Roles.Contains("hr_admin", StringComparer.Ordinal);

    internal static async Task<bool> CoversCompanyAsync(HttpContext http, HrmDbContext db, CancellationToken ct)
    {
        var scope = ProductRequestContext.Current(http);
        if (!RoleAllows(scope)) return false;
        var companies = await db.LegalEntities.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == scope!.TenantId).Select(x => x.Id).ToArrayAsync(ct);
        if (companies.Length != 1 || companies[0] != scope!.CompanyId) return false;
        if (!await db.WorkspaceBindings.IgnoreQueryFilters().AnyAsync(x => x.TenantId == scope.TenantId
                && x.LegalEntityId == scope.CompanyId && !x.IsArchived, ct)) return false;
        // Include archived locations: historical records must not leak through a
        // company-wide legacy report when only a current branch is assigned.
        var locations = await db.WorkLocations.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId).Select(x => new { x.Id, x.LegalEntityId }).ToArrayAsync(ct);
        if (!locations.All(x => x.LegalEntityId == scope.CompanyId
            && (scope.AllCompanyLocations || scope.LocationIds.Contains(x.Id)))) return false;
        return scope.AllCompanyLocations || !await db.Workers.IgnoreQueryFilters().AnyAsync(x => x.TenantId == scope.TenantId
            && (!x.LocationId.HasValue || !scope.LocationIds.Contains(x.LocationId.Value)), ct);
    }

    internal static bool PathAllowed(PathString path, string method)
    {
        var text = (path.Value ?? "").ToLowerInvariant();
        var prefix = text.StartsWith("/api/v1/hrm/", StringComparison.Ordinal) ? "/api/v1/hrm/" : "/api/hrm/";
        if (!text.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var relative = text[prefix.Length..].TrimEnd('/');
        // Login, credential and product/team grants remain Core/Kuubu-owned.
        string[] blocked = ["auth", "identity", "admin/branch-access", "admin/users", "admin/roles",
            "admin/permissions", "admin/auth", "admin/identity", "integrations", "security", "setup/reset"];
        if (blocked.Any(p => relative == p || relative.StartsWith(p + "/", StringComparison.Ordinal))) return false;
        if (relative == "admin/legal-entities" && method != "GET") return false;
        return true;
    }

    internal static async Task Boundary(HttpContext http, RequestDelegate next)
    {
        if (!PathAllowed(http.Request.Path, http.Request.Method)
            || http.Request.Headers.ContainsKey("Authorization") || http.Request.Headers.ContainsKey("X-Tenant")
            || http.Request.Headers.ContainsKey("X-Tenant-Id") || http.Request.Headers.ContainsKey("HRM-Default-TenantId")
            || http.Request.Headers.ContainsKey("X-All-Branches") || http.Request.Headers.ContainsKey("X-Branch-Id"))
        { http.Response.StatusCode = 403; return; }
        if (http.Request.Headers.TryGetValue("X-Shell-Entity", out var company) && (company.Count != 1 || !Guid.TryParse(company, out _))
            || http.Request.Headers.TryGetValue("X-Shell-Location", out var location) && (location.Count != 1 || !Guid.TryParse(location, out _)))
        { http.Response.StatusCode = 400; return; }
        if (http.Request.Method is not ("GET" or "HEAD"))
        {
            var browser = http.RequestServices.GetRequiredService<ProductOperationalBrowser>();
            var bound = browser.Read(http);
            var origin = http.RequestServices.GetRequiredService<IConfiguration>()["ERP:ProductIdentity:PublicOrigin"];
            if (http.Request.Headers.Origin.ToString() != origin || bound is null || !browser.Csrf(http, bound)
                || !(http.Request.HasJsonContentType() || http.Request.HasFormContentType))
            { http.Response.StatusCode = 403; return; }
            const int limit = 16 * 1024 * 1024;
            if (http.Request.ContentLength is null or > limit) { http.Response.StatusCode = 413; return; }
        }
        if (http.GetEndpoint()?.Metadata.GetMetadata<ProductWorkerRequestPolicy>() is { } worker)
        { await ProductWorkerFlow.CheckBoundary(http, worker, next); return; }
        await next(http);
    }

    public static RouteGroupBuilder ProductHrm(this RouteGroupBuilder group)
        => group.WithMetadata(new ProductHrmWorkspacePolicy()).AddEndpointFilter(Enforce);
    public static RouteHandlerBuilder ProductHrm(this RouteHandlerBuilder endpoint)
        => endpoint.WithMetadata(new ProductHrmWorkspacePolicy()).AddEndpointFilter(Enforce);

    private static async ValueTask<object?> Enforce(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        var http = invocation.HttpContext;
        var config = http.RequestServices.GetRequiredService<IConfiguration>();
        if (!ProductRequestAuthentication.Enabled(config)) return await next(invocation);
        var scope = ProductRequestContext.Current(http);
        if (http.GetEndpoint()?.Metadata.GetMetadata<ProductWorkerRequestPolicy>() is not null
            && (!Enabled(config) || !RoleAllows(scope))) return await next(invocation);
        if (!Enabled(config) || !PathAllowed(http.Request.Path, http.Request.Method)) return Results.StatusCode(403);
        var db = http.RequestServices.GetRequiredService<HrmDbContext>();
        if (!await CoversCompanyAsync(http, db, http.RequestAborted))
        {
            // Preserve the separately qualified bounded employee directory for
            // narrower grants. Its own filter still validates every operation.
            if (http.GetEndpoint()?.Metadata.GetMetadata<ProductWorkerRequestPolicy>() is not null) return await next(invocation);
            return Results.Json(new { code = "hrm-workspace-scope", message = "This HR workflow requires access to all company locations. Review location permissions in Kuubu." }, statusCode: 403);
        }
        if (http.Request.Headers.TryGetValue("X-Shell-Entity", out var entity) && entity.ToString() != scope!.CompanyId.ToString("D")) return Results.StatusCode(403);
        if (http.Request.Headers.TryGetValue("X-Shell-Location", out var branch)
            && (!Guid.TryParse(branch, out var id) || !await db.WorkLocations.AnyAsync(x => x.Id == id
                && x.LegalEntityId == scope!.CompanyId && (scope.AllCompanyLocations || scope.LocationIds.Contains(id)), http.RequestAborted)))
            return Results.StatusCode(403);
        MarkAdmission(http);
        NpgsqlConnection? fence = null;
        var key = "core.company.team.v1\n" + scope!.TenantId + "\n" + scope.CompanyId.ToString("D");
        try
        {
            // Some existing GET handlers initialize company settings lazily.
            // Fence them too, and require a real transaction for every EF save.
            if (db.Database.IsNpgsql() && http.GetEndpoint()?.Metadata.GetMetadata<ProductWorkerRequestPolicy>()?.Writes != true)
            {
                fence = new NpgsqlConnection(db.Database.GetConnectionString()); await fence.OpenAsync(http.RequestAborted);
                await using var command = new NpgsqlCommand("SELECT pg_advisory_lock_shared(hashtextextended(@key,0))", fence);
                command.Parameters.AddWithValue("key", key); await command.ExecuteNonQueryAsync(http.RequestAborted);
                db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
            }
            else if (!db.Database.IsNpgsql() && http.Request.Method is not ("GET" or "HEAD")) return Results.StatusCode(503);
            var before = await ProductRequestContext.RecheckAsync(http, http.RequestAborted);
            if (before != ModuleAccessStatus.Authorized) return Results.StatusCode(before == ModuleAccessStatus.Unavailable ? 503 : 403);
            var result = await next(invocation);
            var after = await ProductRequestContext.RecheckAsync(http, http.RequestAborted);
            return after == ModuleAccessStatus.Authorized && await CoversCompanyAsync(http, db, http.RequestAborted)
                ? result : Results.StatusCode(after == ModuleAccessStatus.Unavailable ? 503 : 403);
        }
        catch (ProductProcurementAuthorityException error) { return Results.StatusCode(error.Status); }
        finally
        {
            if (fence is not null)
            {
                // Never return a pooled connection with a session-level fence.
                try { await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock_shared(hashtextextended(@key,0))", fence);
                    release.Parameters.AddWithValue("key", key); await release.ExecuteNonQueryAsync(CancellationToken.None); }
                finally { await fence.DisposeAsync(); }
            }
        }
    }
}

internal sealed class ProductHrmCommitGuard(IHttpContextAccessor accessor, IConfiguration config) : DbTransactionInterceptor
{
    private HttpContext? Request(TransactionEventData data) => data.Context is HrmDbContext && accessor.HttpContext is { } http
        && ProductHrmWorkspace.IsEndpoint(http, config) && ProductHrmWorkspace.HasAdmission(http) ? http : null;
    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData data, InterceptionResult result, CancellationToken ct = default)
    {
        if (Request(data) is { } http)
        {
            var access = await ProductRequestContext.RecheckAsync(http, ct);
            if (access != ModuleAccessStatus.Authorized || !await ProductHrmWorkspace.CoversCompanyAsync(http, (HrmDbContext)data.Context!, ct))
                throw new ProductProcurementAuthorityException(access == ModuleAccessStatus.Unavailable ? 503 : 403);
        }
        return result;
    }
    public override InterceptionResult TransactionCommitting(DbTransaction transaction, TransactionEventData data, InterceptionResult result)
    { if (Request(data) is not null) throw new ProductProcurementAuthorityException(503); return result; }
}
