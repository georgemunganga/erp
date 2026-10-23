using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;

namespace Mightyfin.Erp.Hrm.Api;

public interface IProcurementApprovalSchemaReadiness
{
    Task<bool> IsReadyAsync(CancellationToken ct);
}

/// <summary>Read-only schema/access discovery using the dedicated execution connection.</summary>
public sealed class ProcurementApprovalSchemaReadiness(IConfiguration configuration, IApprovalScope scope,
    Mightyfin.Erp.Procurement.Application.IProcurementScope procurementScope,
    ProcurementPrincipalApprovalStoreReadiness p25PhysicalStore)
    : IProcurementApprovalSchemaReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        var connection = configuration.GetConnectionString("ProcurementApprovalExecution");
        if (string.IsNullOrWhiteSpace(connection)) return false;
        try
        {
            await using var db = new ApprovalDbContext(new DbContextOptionsBuilder<ApprovalDbContext>()
                .UseNpgsql(connection, provider => provider.MigrationsHistoryTable("__EFMigrationsHistory", "core_approval"))
                .Options, scope);
            if (!await db.Database.CanConnectAsync(ct)
                || (await db.Database.GetPendingMigrationsAsync(ct)).Any()) return false;
            // History alone is not evidence that tables still exist or the runtime role
            // can read them. These bounded selects perform no test writes.
            _ = await db.Cases.AsNoTracking().Select(x => x.Id).Take(1).ToArrayAsync(ct);
            _ = await db.Stages.AsNoTracking().Select(x => x.Id).Take(1).ToArrayAsync(ct);
            _ = await db.Assignments.AsNoTracking().Select(x => x.Id).Take(1).ToArrayAsync(ct);
            _ = await db.Decisions.AsNoTracking().Select(x => x.Id).Take(1).ToArrayAsync(ct);
            _ = await db.Delegations.AsNoTracking().Select(x => x.Id).Take(1).ToArrayAsync(ct);
            await using var procurement = new Mightyfin.Erp.Procurement.Infrastructure.Data.ProcurementDbContext(
                new DbContextOptionsBuilder<Mightyfin.Erp.Procurement.Infrastructure.Data.ProcurementDbContext>()
                    .UseNpgsql(connection, x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, procurementScope);
            return await new ProcurementFinanceCheckerSchemaReadiness().IsReadyAsync(procurement, false, ct)
                || await p25PhysicalStore.IsReadyAsync(procurement,ct);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
    }
}
