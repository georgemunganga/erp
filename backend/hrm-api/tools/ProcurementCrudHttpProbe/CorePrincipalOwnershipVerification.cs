using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ProcurementCrudHttpProbe;
internal static partial class Program
{
    const string CorePrincipalSuffix = "_P22CorePrincipalOwnership";
    const string CorePrincipalDownMessage = "Retained Core request identity prevents destructive downgrade.";
    static string CorePrincipalMigration()
    {
        using var owner = Db();
        var ids = owner.Database.GetMigrations().Where(x => x.EndsWith(CorePrincipalSuffix, StringComparison.Ordinal)).ToArray();
        Check(ids.Length == 1, "Exactly one genuine compiled Core principal migration required.");
        return ids.Single();
    }

    static async Task RehearseUnusedCorePrincipalMigration()
    {
        stage = "unused-Core-principal-P22-Down-Up";
        var id = CorePrincipalMigration(); var legacy = await Legacy(); var original = await LookupFootprint();
        await using (var db = Db())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(id);
            Check(legacy == await Legacy(), "Core principal upgrade changed legacy invoice columns.");
            await migrator.MigrateAsync(CanonicalP21);
        }
        Check(original == await LookupFootprint(), "Unused P22 Down changed retained Core/Procurement rows or migration history.");
        await using (var db = Db()) await db.GetService<IMigrator>().MigrateAsync(id);
        Check(legacy == await Legacy(), "Core principal re-upgrade changed legacy invoice columns.");
    }

    static async Task VerifyCorePrincipalSchema()
    {
        var id = CorePrincipalMigration();
        await using var db = Db();
        Check((await db.Database.GetAppliedMigrationsAsync()).Contains(id), "Real Core principal migration missing.");
        Check(await Scalar("SELECT count(*)::text FROM pg_constraint WHERE connamespace='procurement'::regnamespace AND contype='c' AND convalidated AND conname<>'ck_purchase_request_identity'") == "39",
            "Original canonical 39 validated CHECKs changed.");
        Check(await Scalar("SELECT count(*)::text FROM pg_constraint WHERE conrelid='procurement.purchase_requests'::regclass AND conname='ck_purchase_request_identity' AND contype='c' AND convalidated") == "1",
            "New validated request identity CHECK missing.");
        Check(await Scalar("SELECT count(*)::text FROM pg_constraint WHERE connamespace='procurement'::regnamespace AND contype='c'") == "40",
            "Expected exactly original 39 plus one new request identity CHECK.");
    }

    static async Task VerifyUsedCorePrincipalMigration()
    {
        stage = "used-Core-principal-P22-Down-veto";
        var id = CorePrincipalMigration(); var original = await LookupFootprint();
        await using (var db = Db())
        {
            Check(await db.PurchaseRequests.AnyAsync(x => x.RequesterPrincipalId != null), "Used P22 proof needs a genuinely HTTP-created principal-owned request.");
            var sql = db.GetService<IMigrator>().GenerateScript(id, CanonicalP21);
            Check(sql.Contains(CorePrincipalDownMessage, StringComparison.Ordinal) && !sql.Contains("DROP TABLE", StringComparison.OrdinalIgnoreCase),
                "Expected only guarded P22-to-P21 ownership downgrade.");
            await using var owner = new NpgsqlConnection(connection); await owner.OpenAsync();
            await using var command = new NpgsqlCommand(sql, owner);
            try { await command.ExecuteNonQueryAsync(); throw new ProbeFailure("Used principal evidence allowed destructive Down."); }
            catch (Exception error) when (FindPg(error) is { SqlState: "55000", MessageText: CorePrincipalDownMessage }) { }
        }
        Check(original == await LookupFootprint(), "Denied P22 Down changed complete retained rows/history.");
        foreach (var invalid in new[] { "both-null", "zero-principal", "zero-worker" })
        {
            stage = "request-identity-CHECK-" + invalid;
            await using var owner = new NpgsqlConnection(connection); await owner.OpenAsync();
            await using var transaction = await owner.BeginTransactionAsync();
            await using var command = new NpgsqlCommand("""
                INSERT INTO procurement.purchase_requests
                SELECT (jsonb_populate_record(NULL::procurement.purchase_requests,
                    to_jsonb(source) || jsonb_build_object('id',@id,'number',@number,
                      'requester_principal_id',CASE WHEN @kind='both-null' THEN NULL ELSE @principal END,
                      'requester_worker_id',CASE WHEN @kind='both-null' THEN NULL ELSE @worker END))).*
                FROM procurement.purchase_requests source
                WHERE source.tenant_id=@tenant AND source.legal_entity_id=@company
                ORDER BY source.id LIMIT 1;
                """, owner, transaction);
            command.Parameters.AddWithValue("id", Guid.NewGuid()); command.Parameters.AddWithValue("number", "INVALID-" + Guid.NewGuid().ToString("N"));
            command.Parameters.AddWithValue("kind", invalid); command.Parameters.AddWithValue("principal", invalid == "zero-principal" ? Guid.Empty : Actor);
            command.Parameters.AddWithValue("worker", invalid == "zero-worker" ? Guid.Empty : Worker);
            command.Parameters.AddWithValue("tenant", Tenant); command.Parameters.AddWithValue("company", Company);
            try { await command.ExecuteNonQueryAsync(); throw new ProbeFailure("Invalid request identity passed its CHECK."); }
            catch (PostgresException error) when (error.SqlState == "23514" && error.ConstraintName == "ck_purchase_request_identity") { }
            await transaction.RollbackAsync();
            Check(original == await LookupFootprint(), "Denied raw identity insert changed complete retained rows/history.");
        }
    }
}
