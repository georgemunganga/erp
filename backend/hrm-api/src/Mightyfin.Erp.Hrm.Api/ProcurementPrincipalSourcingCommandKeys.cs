using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    // One transaction-scoped namespace across the P30 business commands. Hash
    // collisions only serialize extra commands; target+payload are still checked.
    private static async Task LockPrincipalSourcingCommandKey(ProcurementDbContext db,
        Guid key, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key.ToString("D")}, 0))", ct);
    }

    private static async Task RequirePrincipalSourcingKeyUnused(ProcurementDbContext db,
        Guid key, CancellationToken ct)
    {
        if (await db.SourcingPublicationReceipts.AsNoTracking().AnyAsync(x => x.CommandKey == key, ct)
            || await db.SourcingManualContacts.AsNoTracking().AnyAsync(x => x.CommandKey == key, ct)
            || await db.SourcingManualQuoteVersions.AsNoTracking().AnyAsync(x => x.CommandKey == key, ct))
            throw new Problem("SourcingCommandKeyConflict", 409);
    }
}
