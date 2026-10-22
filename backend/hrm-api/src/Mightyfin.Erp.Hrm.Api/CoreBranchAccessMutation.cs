using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Core branch writers participate in the same fence as executing module commands.</summary>
internal static class CoreBranchAccessMutation
{
    public static async Task<IResult> ExecuteAsync(HrmDbContext db, string tenantId,
        Func<Task<IResult>> mutation, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Acquire the Core branch fence before starting a transaction.");
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = db.Database.GetDbConnection() as NpgsqlConnection
                ?? throw new InvalidOperationException("Core branch writes require the PostgreSQL authorization fence.");
            await using var fence = await CoreBranchAuthorizationFence.AcquireAsync(connection, tenantId,
                exclusive: true, ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var result = await mutation();
            await transaction.CommitAsync(ct);
            return result;
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
