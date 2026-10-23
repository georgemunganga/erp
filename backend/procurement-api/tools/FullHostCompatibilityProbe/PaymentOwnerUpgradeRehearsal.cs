using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

// Private, new canonical clone only. This is not an application startup or migration adapter.
internal static class PaymentOwnerUpgradeRehearsal
{
    private const string CoreMigration = "20261023130726_CoreCompanyTimeZone";
    private const string PaymentMigration = "20261023125243_P27ManualPaymentRecords";
    private static readonly string[] PaymentTables = ["manual_payment_requests", "manual_payment_decisions", "manual_payment_receipts"];

    internal static async Task RunAsync(string connection, string evidence)
    {
        var result = new Dictionary<string, object?> { ["status"] = "NOT_STARTED", ["actualProgramStarted"] = false,
            ["dataPrinted"] = false, ["productionChanged"] = false,
            ["limits"] = "Owner credentials on a new private retained-data clone; no runtime authorization or deployment proof." };
        var phase = "OwnerMigrationAdmission";
        try
        {
            var scope = new ProcurementRequestScope();
            scope.Set("offline-owner-upgrade", Guid.NewGuid(), "offline-owner-upgrade", null, null, false, "offline-owner-upgrade");
            await using var core = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection,
                n => n.MigrationsHistoryTable("__hrm_migrations", "hrm")).Options, new OwnerTenant());
            await using var procurement = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection,
                n => n.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, scope);
            Require(!core.Database.HasPendingModelChanges() && !procurement.Database.HasPendingModelChanges());
            Require((await core.Database.GetPendingMigrationsAsync()).SequenceEqual([CoreMigration]));
            Require((await procurement.Database.GetPendingMigrationsAsync()).SequenceEqual([PaymentMigration]));
            await using var sql = new NpgsqlConnection(connection); await sql.OpenAsync();
            Require(await Scalar<long>(sql, "SELECT count(*) FROM hrm.legal_entities") > 0);
            Require(await Scalar<long>(sql, "SELECT count(*) FROM pg_tables WHERE schemaname='procurement' AND tablename=ANY(@tables)", ("tables", PaymentTables)) == 0);
            var before = await ExistingFootprint(sql);
            phase = "CoreAndProcurementOwningMigrations";
            await core.Database.MigrateAsync();
            await procurement.Database.MigrateAsync();
            var first = await ExistingFootprint(sql);
            Require(before == first);
            Require(await Scalar<long>(sql, "SELECT count(*) FROM hrm.legal_entities WHERE time_zone_id IS DISTINCT FROM 'UTC'") == 0);
            Require(await Scalar<long>(sql, "SELECT count(*) FROM pg_tables WHERE schemaname='procurement' AND tablename=ANY(@tables)", ("tables", PaymentTables)) == 3);
            foreach (var name in PaymentTables)
                Require(await Scalar<long>(sql, $"SELECT count(*) FROM procurement.{name}") == 0);
            phase = "SecondMigrationIdempotency";
            await core.Database.MigrateAsync(); await procurement.Database.MigrateAsync();
            Require(first == await ExistingFootprint(sql));
            Require(!(await core.Database.GetPendingMigrationsAsync()).Any() && !(await procurement.Database.GetPendingMigrationsAsync()).Any());
            Require(!core.Database.HasPendingModelChanges() && !procurement.Database.HasPendingModelChanges());
            phase = "PersistedCompanyDateProvider";
            string tenant; Guid company;
            await using (var cmd = new NpgsqlCommand("SELECT tenant_id,id FROM hrm.legal_entities WHERE NOT is_archived ORDER BY id LIMIT 1", sql))
            await using (var rows = await cmd.ExecuteReaderAsync())
            { Require(await rows.ReadAsync()); tenant = rows.GetString(0); company = rows.GetGuid(1); }
            var instant = new DateTimeOffset(2026, 10, 23, 23, 30, 0, TimeSpan.Zero);
            var observation = await new CoreCompanyBusinessDateProvider(core, new FixedClock(instant)).GetAsync(tenant, company, default);
            Require(observation.Date == new DateOnly(2026, 10, 23) && observation.TimeZoneId == "UTC" && observation.ObservedAtUtc == instant);
            Require(first == await ExistingFootprint(sql));
            result["existingRowsAndUnrelatedSchemaUnchanged"] = true;
            result["secondMigrationSchemaHistoryAndDataUnchanged"] = true;
            result["coreCompanyDateReadFromPersistedSetting"] = true;
            result["coreMigration"] = CoreMigration; result["procurementMigration"] = PaymentMigration;
            result["status"] = "PASS";
        }
        catch (Exception error)
        {
            result["status"] = "FAIL"; result["failurePhase"] = phase; result["exceptionType"] = error.GetType().Name;
            if (error.GetBaseException() is PostgresException pg) result["sqlState"] = pg.SqlState;
            Environment.ExitCode = 1;
        }
        await File.WriteAllTextAsync(evidence, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Private owner upgrade rehearsal finished; consult protected summary only.");
    }

    private static void Require(bool valid) { if (!valid) throw new InvalidOperationException("Private owner upgrade invariant failed."); }
    private static async Task<T> Scalar<T>(NpgsqlConnection db, string sql, params (string Name, object Value)[] values)
    {
        await using var command = new NpgsqlCommand(sql, db);
        foreach (var value in values) command.Parameters.AddWithValue(value.Name, value.Value);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    // Excludes only the explicitly reviewed additions, not existing business records or old history.
    // All existing tables, including imported archives, remain in this digest. No row data is output.
    private static async Task<string> ExistingFootprint(NpgsqlConnection db)
    {
        await using var tx = await db.BeginTransactionAsync();
        await using (var setup = new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL TIME ZONE 'UTC'; SET LOCAL DateStyle='ISO,YMD'", db, tx))
            await setup.ExecuteNonQueryAsync();
        var tables = new List<(string Schema, string Table)>();
        await using (var command = new NpgsqlCommand("""
            SELECT schemaname,tablename FROM pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema')
            AND NOT (schemaname='procurement' AND tablename=ANY(@tables)) ORDER BY 1,2
            """, db, tx))
        {
            command.Parameters.AddWithValue("tables", PaymentTables);
            await using var rows = await command.ExecuteReaderAsync();
            while (await rows.ReadAsync()) tables.Add((rows.GetString(0), rows.GetString(1)));
        }
        var parts = new List<string>();
        foreach (var table in tables)
        {
            static string Quote(string name) => '"' + name.Replace("\"", "\"\"") + '"';
            var projection = table == ("hrm", "legal_entities") ? "to_jsonb(r)-'time_zone_id'" : "to_jsonb(r)";
            var predicate = table == ("hrm", "__hrm_migrations") ? "WHERE \"MigrationId\"<>@core" :
                table == ("procurement", "__procurement_migrations") ? "WHERE \"MigrationId\"<>@payment" : "";
            await using var command = new NpgsqlCommand($"SELECT count(*)::text||':'||encode(sha256(convert_to(coalesce(string_agg(h,'' ORDER BY h),''),'UTF8')),'hex') FROM (SELECT encode(sha256(convert_to(({projection})::text,'UTF8')),'hex') h FROM {Quote(table.Schema)}.{Quote(table.Table)} r {predicate}) q", db, tx);
            command.Parameters.AddWithValue("core", CoreMigration); command.Parameters.AddWithValue("payment", PaymentMigration);
            parts.Add(table.Schema + '.' + table.Table + ':' + (string)(await command.ExecuteScalarAsync())!);
        }
        await using (var command = new NpgsqlCommand("""
            SELECT coalesce(string_agg(s,E'\n' ORDER BY s),'') FROM (
              SELECT n.nspname||'.'||c.relname||':'||a.attnum||':'||a.attname||':'||format_type(a.atttypid,a.atttypmod)||':'||a.attnotnull::text||':'||coalesce(pg_get_expr(d.adbin,d.adrelid),'') s
              FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
              JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped
              LEFT JOIN pg_attrdef d ON d.adrelid=c.oid AND d.adnum=a.attnum
              WHERE c.relkind IN ('r','p') AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
              AND NOT(n.nspname='procurement' AND c.relname=ANY(@tables))
              AND NOT(n.nspname='hrm' AND c.relname='legal_entities' AND a.attname='time_zone_id')
              UNION ALL SELECT n.nspname||'.'||c.relname||':'||k.conname||':'||pg_get_constraintdef(k.oid)
              FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace
              WHERE n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema' AND NOT(n.nspname='procurement' AND c.relname=ANY(@tables))
              UNION ALL SELECT schemaname||'.'||tablename||':'||indexname||':'||indexdef FROM pg_indexes
              WHERE schemaname NOT LIKE 'pg_%' AND schemaname<>'information_schema' AND NOT(schemaname='procurement' AND tablename=ANY(@tables))
            ) q
            """, db, tx))
        {
            command.Parameters.AddWithValue("tables", PaymentTables);
            parts.Add((string)(await command.ExecuteScalarAsync())!);
        }
        await tx.RollbackAsync();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts))));
    }
    private sealed class OwnerTenant : ITenantAccessor { public string GetTenantId() => "offline-owner-upgrade"; }
    private sealed class FixedClock(DateTimeOffset instant) : TimeProvider { public override DateTimeOffset GetUtcNow() => instant; }
}
