using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

internal static class ProductProcurementFlow
{
    internal static RouteGroupBuilder ProductProcurement(this RouteGroupBuilder group) =>
        group.WithMetadata(new ProductProcurementRequestPolicy()).AddEndpointFilter(Enforce);
    internal static RouteHandlerBuilder ProductProcurement(this RouteHandlerBuilder endpoint) =>
        endpoint.WithMetadata(new ProductProcurementRequestPolicy()).AddEndpointFilter(Enforce);

    internal static async Task Boundary(HttpContext http, RequestDelegate next)
    {
        if (http.Request.Method is not ("GET" or "POST" or "PUT")) { http.Response.StatusCode = 405; return; }
        string[] rejected = ["Authorization", "X-Tenant", "X-Tenant-Id", "X-Branch-Id", "X-All-Branches"];
        if (rejected.Any(http.Request.Headers.ContainsKey)
            || http.Request.Query.Count > 20 || http.Request.QueryString.Value?.Length > 8192
            || http.Request.Query.Any(x => x.Value.Count != 1)) { http.Response.StatusCode = 400; return; }
        if (HttpMethods.IsGet(http.Request.Method)) { await next(http); return; }
        var browser = http.RequestServices.GetRequiredService<ProductOperationalBrowser>();
        var bound = browser.Read(http);
        var origin = http.RequestServices.GetRequiredService<IConfiguration>()["ERP:ProductIdentity:PublicOrigin"]
            ?? "https://app.kuubuerp.com";
        if (http.Request.Headers.Origin.ToString() != origin || !http.Request.HasJsonContentType()
            || bound is null || !browser.Csrf(http, bound)
            || http.Request.Headers[ProductRequestAuthentication.SessionContextHeader].Count != 1)
        { http.Response.StatusCode = 403; return; }
        const int limit = 1048576;
        if (http.Request.ContentLength > limit) { http.Response.StatusCode = 413; return; }
        var bytes = new byte[limit + 1]; var used = 0;
        while (used < bytes.Length)
        {
            var read = await http.Request.Body.ReadAsync(bytes.AsMemory(used), http.RequestAborted);
            if (read == 0) break;
            used += read;
        }
        if (used > limit) { http.Response.StatusCode = 413; return; }
        var original = http.Request.Body;
        using var body = new MemoryStream(bytes, 0, used, false); http.Request.Body = body;
        try { await next(http); } finally { http.Request.Body = original; }
    }

    internal static async Task<bool> BindScope(HttpContext http, HrmDbContext core, ProcurementRequestScope target)
    {
        var proof = ProductRequestContext.Current(http);
        if (!ProductProcurementAdmission.Allows(proof)) return await Deny(http, 403);
        static bool Header(HttpContext context, string name, out string text)
        {
            text = context.Request.Headers[name].ToString();
            return !context.Request.Headers.ContainsKey(name) || context.Request.Headers[name].Count == 1 && text.Length > 0;
        }
        if (!Header(http, "X-Shell-Entity", out var companyText)
            || !Header(http, "X-Shell-Location", out var branchText)
            || !Header(http, "X-Shell-Branch-Scope", out var allText)) return await Deny(http, 400);
        var company = proof!.CompanyId;
        if (companyText != "" && (!Guid.TryParseExact(companyText, "D", out var requested) || requested != company))
            return await Deny(http, 403);
        Guid? branch = null;
        if (branchText != "")
        {
            if (!Guid.TryParseExact(branchText, "D", out var parsed) || parsed == Guid.Empty) return await Deny(http, 400);
            branch = parsed;
        }
        if (allText != "" && allText != "all") return await Deny(http, 400);
        var all = allText == "all";
        var path = http.Request.Path;
        if (branch is null && allText == "" && HttpMethods.IsGet(http.Request.Method)
            && (path == "/api/procurement/v1/context" || path == "/api/procurement/v1/meta")) all = true;
        if (all && !(ProcurementScopeMiddleware.AllCompanyMasterReadPath(path, http.Request.Method)
            || ProcurementScopeMiddleware.AllOperationalListReadPath(path, http.Request.Method)
            || ProcurementScopeMiddleware.PlanReviewAllReadPath(path, http.Request.Method))) return await Deny(http, 400);
        var active = await core.WorkLocations.AsNoTracking().Where(x => x.TenantId == proof.TenantId
            && x.LegalEntityId == company && !x.IsArchived).Select(x => x.Id).ToArrayAsync(http.RequestAborted);
        if (!ProductProcurementAdmission.SelectionWithin(proof, company, branch, all, active)) return await Deny(http, 403);
        var principal = ProcurementCorePrincipals.Cached(http, proof.TenantId, proof.PrincipalId.ToString("D"));
        if (principal is null) return await Deny(http, 403);
        var permitted = proof.AllCompanyLocations ? active : proof.LocationIds.ToArray();
        target.Set(proof.TenantId, company, proof.PrincipalId.ToString("D"), branch, null,
            !proof.AllCompanyLocations, http.TraceIdentifier, all, permitted);
        target.AssignPrincipal(principal.Id, principal.WorkerId);
        target.AssignProductPrincipal(proof, principal);
        var worker = await ProcurementWorkforce.ResolveActiveWorker(core, target, http.RequestAborted);
        if (worker is { } workerId) target.AssignWorker(workerId);
        return true;
    }

    private static async Task<bool> Deny(HttpContext http, int status)
    {
        http.Response.StatusCode = status;
        http.Response.ContentType = "application/json";
        await http.Response.WriteAsync("{\"error\":\"workspace_access_unavailable\"}", http.RequestAborted);
        return false;
    }

    private static async ValueTask<object?> Enforce(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        var http = invocation.HttpContext;
        var configuration = http.RequestServices.GetRequiredService<IConfiguration>();
        if (!ProductRequestAuthentication.Enabled(configuration)) return await next(invocation);
        var proof = ProductRequestContext.Current(http);
        if (!ProductProcurementAdmission.Enabled(configuration) || !ProductProcurementAdmission.Allows(proof))
            return Results.StatusCode(403);
        NpgsqlConnection? fence = null;
        var key = "core.company.team.v1\n" + proof!.TenantId + "\n" + proof.CompanyId.ToString("D");
        try
        {
            if (!HttpMethods.IsGet(http.Request.Method))
            {
                var connection = configuration.GetConnectionString("Hrm");
                if (string.IsNullOrWhiteSpace(connection)) return Results.StatusCode(503);
                var normalized = connection.StartsWith("postgres", StringComparison.OrdinalIgnoreCase)
                    ? Routing.NpgsqlConnectionStringNormalizer.Normalize(connection) : connection;
                fence = new NpgsqlConnection(normalized);
                await fence.OpenAsync(http.RequestAborted);
                await using var command = new NpgsqlCommand("SELECT pg_advisory_lock_shared(hashtextextended(@key,0))", fence);
                command.Parameters.AddWithValue("key", key); await command.ExecuteNonQueryAsync(http.RequestAborted);
                // EF must use a real owning transaction even for a one-statement SaveChanges.
                var data = http.RequestServices.GetRequiredService<ProcurementDbContext>();
                data.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
            }
            var before = await ProductRequestContext.RecheckAsync(http, http.RequestAborted);
            if (before != ModuleAccessStatus.Authorized) return Results.StatusCode(before == ModuleAccessStatus.Unavailable ? 503 : 403);
            var result = await next(invocation);
            if (http.Request.Path == "/api/procurement/v1/meta") result = Metadata(result);
            var after = await ProductRequestContext.RecheckAsync(http, http.RequestAborted);
            return after == ModuleAccessStatus.Authorized ? result
                : Results.StatusCode(after == ModuleAccessStatus.Unavailable ? 503 : 403);
        }
        catch (ProductProcurementAuthorityException denied) { return Results.StatusCode(denied.Status); }
        catch (DbException) { return Results.StatusCode(503); }
        finally
        {
            if (fence is not null)
            {
                try
                {
                    if (fence.State == System.Data.ConnectionState.Open)
                    {
                        await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock_shared(hashtextextended(@key,0))", fence);
                        release.Parameters.AddWithValue("key", key); await release.ExecuteNonQueryAsync(CancellationToken.None);
                    }
                }
                finally { await fence.DisposeAsync(); }
            }
        }
    }

    internal static object? Metadata(object? result)
    {
        if (result is IStatusCodeHttpResult status && status.StatusCode >= 400) return result;
        if (result is not IValueHttpResult { Value: { } value }) return result;
        var payload = JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (!payload.TryGetProperty("capabilities", out var capabilities)) return result;
        string[] admitted = ["vendorDraft", "itemDraft", "requestDraft"];
        var narrowed = capabilities.EnumerateObject().ToDictionary(x => x.Name,
            x => admitted.Contains(x.Name, StringComparer.Ordinal) ? x.Value : JsonSerializer.SerializeToElement(false));
        var body = payload.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);
        body["capabilities"] = JsonSerializer.SerializeToElement(narrowed);
        return Results.Json(body);
    }
}
