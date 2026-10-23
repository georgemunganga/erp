using Microsoft.EntityFrameworkCore;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>Compatibility guard for existing invoice/credit CRUD during the additive P27 rollout.
/// Not an endpoint activation/readiness contract: runtime payment activation still requires
/// the exact migration, constraints, Core policies and acceptance evidence.</summary>
internal static class ProcurementManualPaymentSchema
{
    private const string CountSql = """
        SELECT count(*)::integer AS "Value" FROM pg_tables WHERE schemaname='procurement'
        AND tablename IN ('manual_payment_requests','manual_payment_decisions','manual_payment_receipts')
        """;
    internal static bool Installed(ProcurementDbContext db) => db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL"
        || Complete(db.Database.SqlQueryRaw<int>(CountSql).Single());
    internal static async Task<bool> InstalledAsync(ProcurementDbContext db, CancellationToken ct) =>
        db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL"
        || Complete(await db.Database.SqlQueryRaw<int>(CountSql).SingleAsync(ct));
    // P27 synthetic/legacy stores deliberately lack these tables. Every real P28 store
    // enforces governance automatically, even when a caller chose the generic coordinator.
    internal static bool GovernanceInstalled(ProcurementDbContext db) => db.Database.IsNpgsql()
        && Complete(db.Database.SqlQueryRaw<int>("""
            SELECT count(*)::integer AS "Value" FROM pg_tables WHERE schemaname='procurement'
            AND tablename IN ('manual_payment_workflow_cases','manual_payment_core_decisions','manual_payment_date_observations')
            """).Single());
    private static bool Complete(int count) => count switch
    { 0 => false, 3 => true, _ => throw new InvalidOperationException("ManualPaymentSchemaIncomplete") };
}
