using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    /// <summary>Future actual P28 restricted-role gate; parent calls only after real P28
    /// migration/PG CHECK literal freeze and RFQ fixture privilege provisioning. Unrun source.</summary>
    public async Task VerifySourcingOriginalRequestUpdatePrivilege()
    {
        Require(OrderDistinctRuntime, "RFQ privilege proof requires genuinely separate finite runtime/bootstrap identities.");
        var actorScope = new FinanceSchemaScope(OrderCompany, OrderBuyer);
        var reader = new ProcurementSourcingDraftSchemaReadiness(new ConfigurationBuilder().Build(), actorScope,
            new ProcurementFinanceCheckerSchemaReadiness());
        async Task<bool> Runtime(bool write)
        {
            await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql(runtimeConnection, n => n.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, actorScope);
            return await reader.IsReadyAsync(db, write, default);
        }
        // Never accept a vacuous negative because P28, CHECK freeze or some other permission
        // is missing. Original fully configured finite identity must genuinely pass first.
        Require(await Runtime(false) && await Runtime(true), "RFQ owning schema/runtime grant baseline unavailable; no privilege proof claimed.");
        var role = new NpgsqlCommandBuilder().QuoteIdentifier(new NpgsqlConnectionStringBuilder(runtimeConnection).Username!);
        await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
        await using var transaction = await owner.BeginTransactionAsync();
        try
        {
            await using (var revoke = new NpgsqlCommand($"REVOKE UPDATE ON procurement.purchase_requests FROM {role}; SET LOCAL ROLE {role}", owner, transaction))
                await revoke.ExecuteNonQueryAsync();
            await using (var premise = new NpgsqlCommand("SELECT has_table_privilege(current_user,'procurement.purchase_requests','SELECT') AND NOT has_table_privilege(current_user,'procurement.purchase_requests','UPDATE')", owner, transaction))
                Require(await premise.ExecuteScalarAsync() is true, "Synthetic SELECT-only PR grant premise not established.");
            await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql(owner, n => n.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, actorScope);
            await db.Database.UseTransactionAsync(transaction);
            Require(await reader.IsReadyAsync(db, false, default), "Removing command-only PR UPDATE incorrectly removed historical read availability.");
            Require(!await reader.IsReadyAsync(db, true, default), "RFQ command readiness advertised true with only original-request SELECT.");
            Require(!db.ChangeTracker.HasChanges(), "RFQ readiness privilege check tracked mutations.");
        }
        finally { await transaction.RollbackAsync(); }
        Require(await Runtime(false) && await Runtime(true), "Rollback did not restore RFQ original request grant.");
        Console.WriteLine("PASS RFQ original-request UPDATE required for command readiness; SELECT-only historical read remains available, rollback restored finite grant. No RFQ effects created.");
    }
}
