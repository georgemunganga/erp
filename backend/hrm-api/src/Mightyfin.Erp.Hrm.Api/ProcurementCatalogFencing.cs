using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Hold the same physical catalog session from before owner reads through commit.</summary>
internal static class ProcurementCatalogFencing
{
    internal static ValueTask<object?> Read(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        Execute(context, next, exclusive: false);
    internal static ValueTask<object?> Write(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        HttpMethods.IsGet(context.HttpContext.Request.Method) || HttpMethods.IsHead(context.HttpContext.Request.Method)
            ? next(context) : Execute(context, next, exclusive: true);

    private static async ValueTask<object?> Execute(EndpointFilterInvocationContext context,
        EndpointFilterDelegate next, bool exclusive)
    {
        var services = context.HttpContext.RequestServices;
        var db = services.GetRequiredService<ProcurementDbContext>();
        // TestServer's explicitly nonrelational provider cannot prove locking; actual PG probe owns that gate.
        if (!db.Database.IsRelational()) return await next(context);
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Acquire catalog authority before beginning a transaction.");
        var scope = services.GetRequiredService<ProcurementRequestScope>();
        await db.Database.OpenConnectionAsync(context.HttpContext.RequestAborted);
        try
        {
            var connection = db.Database.GetDbConnection() as NpgsqlConnection
                ?? throw new InvalidOperationException("Catalog authority requires a pinned PostgreSQL session.");
            await using var lease = await ProcurementCatalogAuthorityFence.AcquireAsync(connection,
                scope.TenantId, scope.LegalEntityId, exclusive, context.HttpContext.RequestAborted);
            return await next(context);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
