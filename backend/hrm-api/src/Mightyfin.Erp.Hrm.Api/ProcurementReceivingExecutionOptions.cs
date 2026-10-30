using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public sealed class ProcurementReceivingExecutionOptions
{
    public bool DraftsEnabled { get; set; }
    public bool PositiveExecutionEnabled { get; set; }
    public bool ExecutionDatabaseConfigured { get; set; }
    // No correction switch: absence of an authoritative consumption adapter cannot be configured away.
}
public interface IProcurementReceivingSchemaReadiness { Task<bool> IsReadyAsync(CancellationToken ct); }
public sealed class ProcurementReceivingSchemaReadiness(IConfiguration config, IProcurementScope scope,
    IProcurementOrderSchemaReadiness orders) : IProcurementReceivingSchemaReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        var connection = config.GetConnectionString("ProcurementApprovalExecution");
        if (string.IsNullOrWhiteSpace(connection) || !await orders.IsReadyAsync(ct)) return false;
        try
        {
            await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection,
                p => p.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, scope);
            if (ProcurementCompiledModelParity.HasPendingModelChanges(db) || (await db.Database.GetPendingMigrationsAsync(ct)).Any()) return false;
            _ = await db.ReceivingSourceStates.Select(x => new { x.Id, x.SourceSha256, x.Sequence }).Take(1).ToArrayAsync(ct);
            _ = await db.ReceivingAcceptanceEffects.Select(x => new { x.Id, x.GoodsReceiptLineId, x.ServiceAcceptanceId }).Take(1).ToArrayAsync(ct);
            _ = await db.GoodsReceipts.Select(x => new { x.Id, x.ExecutionVersion, x.IssuedBindingId }).Take(1).ToArrayAsync(ct);
            _ = await db.GoodsReceiptLines.Select(x => new { x.Id, x.PhysicalBindingId, x.SourceStateBindingId }).Take(1).ToArrayAsync(ct);
            _ = await db.ReceiptInspections.Select(x => new { x.Id, x.QualifiedQuantity, x.PhysicalObservationId }).Take(1).ToArrayAsync(ct);
            _ = await db.ServiceAcceptances.Select(x => new { x.Id, x.CurrentAcceptedNetAmount, x.CertificationEffectId }).Take(1).ToArrayAsync(ct);
            await db.Database.OpenConnectionAsync(ct);
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT bool_and(has_table_privilege(current_user, relation, privilege)) FROM (VALUES
                ('procurement.receiving_source_states','SELECT,INSERT,UPDATE'),
                ('procurement.receiving_acceptance_effects','SELECT,INSERT'),
                ('procurement.goods_receipts','SELECT,INSERT,UPDATE'), ('procurement.goods_receipt_lines','SELECT,INSERT,UPDATE'),
                ('procurement.receipt_inspections','SELECT,INSERT'), ('procurement.service_acceptances','SELECT,INSERT,UPDATE')) AS required(relation, privileges)
                CROSS JOIN LATERAL string_to_table(privileges, ',') AS grants(privilege)
                """;
            if (await command.ExecuteScalarAsync(ct) is not true) return false;
            // Explicit physical-schema contract: EF drift/columns alone cannot prove DB-only ownership FKs.
            command.CommandText = ReceivingOwnershipConstraintSql.ManifestQuery;
            return await command.ExecuteScalarAsync(ct) is true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return false; }
    }
}
