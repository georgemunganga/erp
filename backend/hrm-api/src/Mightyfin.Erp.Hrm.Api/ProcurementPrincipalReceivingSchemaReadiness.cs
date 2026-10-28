using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Receiving admission composes retained M5 read proof with the new owning
/// physical contract. It never requires authority to create or issue another order.</summary>
public sealed class ProcurementPrincipalReceivingSchemaReadiness(
    IConfiguration configuration, IProcurementScope scope,
    IProcurementPrincipalOrderGovernanceReadiness orders) : IProcurementPrincipalReceivingReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        if (!ProcurementPrincipalOrderDraftSchemaReadiness.TryExecutionConnection(configuration, out _)) return false;
        try
        {
            await using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql(configuration.GetConnectionString("ProcurementApprovalExecution"),
                    x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, scope);
            return await IsReadyAsync(owner, false, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
    }

    public async Task<bool> IsReadyAsync(ProcurementDbContext owner, bool write, CancellationToken ct)
    {
        if (!ProcurementPrincipalOrderDraftSchemaReadiness.TryExecutionConnection(configuration, out var role)
            || !owner.Database.IsNpgsql()) return false;
        return await orders.IsReadyAsync(owner, false, ct)
            && await ProcurementPrincipalReceivingPhysicalSchema.InspectAsync(owner, write, role, ct);
    }
}
