using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

/// <summary>One explicit P27→P28 owning migration rehearsal on a new private clone.
/// The older CoreClock/P27 upgrade profile remains unchanged.</summary>
internal static class PaymentGovernanceUpgradeRehearsal
{
    private const string CoreClock = "20261023130726_CoreCompanyTimeZone";
    private const string P27 = "20261023125243_P27ManualPaymentRecords";
    private const string P28 = "20261023153226_P28ManualPaymentGovernance";
    private static readonly string[] NewTables = ["manual_payment_workflow_cases", "manual_payment_core_decisions", "manual_payment_date_observations"];
    private static readonly string[] NewFunctions = ["guard_manual_payment_title", "require_manual_payment_governance_companions",
        "manual_payment_policy_digest", "require_manual_payment_core_case", "require_manual_payment_core_decision",
        "require_manual_payment_company_date"];
    private sealed record Footprint(string Rows, string Shape);

    internal static async Task RunAsync(string connection, string evidence)
    {
        // Reject unsafe evidence destinations before any connection or owner mutation;
        // a failed admission must never fall through to a write at that path.
        if (!OperatingSystem.IsLinux() || File.Exists(evidence)
            || Path.GetDirectoryName(Path.GetFullPath(evidence)) is not { } folder || !Directory.Exists(folder)
            || new DirectoryInfo(folder).LinkTarget is not null
            || (File.GetUnixFileMode(folder) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("New owner-private evidence path required.");
        var result = new Dictionary<string, object?> { ["status"] = "NOT_STARTED", ["actualProgramStarted"] = false,
            ["dataPrinted"] = false, ["productionChanged"] = false,
            ["limits"] = "Private new clone P27 to P28 owner migration only; no runtime-role or payment API claim." };
        var phase = "OwnerMigrationAdmission";
        try
        {
            var scope = new ProcurementRequestScope();
            scope.Set("offline-governance-upgrade", Guid.NewGuid(), "offline-governance-upgrade", null, null, false,
                "offline-governance-upgrade");
            await using var core = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection,
                x => x.MigrationsHistoryTable("__hrm_migrations", "hrm")).Options, new OwnerTenant());
            await using var procurement = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection,
                x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, scope);
            Require(!core.Database.HasPendingModelChanges() && !procurement.Database.HasPendingModelChanges());
            Require(!(await core.Database.GetPendingMigrationsAsync()).Any()
                && (await core.Database.GetAppliedMigrationsAsync()).Contains(CoreClock, StringComparer.Ordinal));
            Require((await procurement.Database.GetPendingMigrationsAsync()).SequenceEqual([P28])
                && (await procurement.Database.GetAppliedMigrationsAsync()).Contains(P27, StringComparer.Ordinal));
            await using var sql = new NpgsqlConnection(connection); await sql.OpenAsync();
            Require(await Scalar<long>(sql, "SELECT count(*) FROM pg_tables WHERE schemaname='procurement' AND tablename=ANY(@names)",
                ("names", NewTables)) == 0);
            Require(await Scalar<long>(sql, "SELECT count(*) FROM hrm.legal_entities WHERE time_zone_id IS NULL OR length(btrim(time_zone_id))=0") == 0);
            var before = await Capture(sql, existingOnly: true);
            phase = "ApplyProcurementP28Only";
            await procurement.Database.MigrateAsync();
            var after = await Capture(sql, existingOnly: true);
            Require(before == after); // All previous rows/columns/checks/indexes/triggers/functions except reviewed P28 changes.
            Require(await Scalar<long>(sql, "SELECT count(*) FROM pg_tables WHERE schemaname='procurement' AND tablename=ANY(@names)",
                ("names", NewTables)) == 3);
            foreach (var name in NewTables)
                Require(await Scalar<long>(sql, $"SELECT count(*) FROM procurement.{name}") == 0);
            Require(await Scalar<long>(sql, "SELECT count(*) FROM procurement.manual_payment_requests WHERE title IS NOT NULL") == 0);
            Require(await Scalar<long>(sql, """
                SELECT count(*) FROM pg_attribute WHERE attrelid='procurement.manual_payment_requests'::regclass
                 AND attname='title' AND NOT attisdropped AND NOT attnotnull AND format_type(atttypid,atttypmod)='character varying(200)'
                """) == 1);
            Require(await Scalar<long>(sql, """
                SELECT count(*) FROM pg_constraint WHERE conrelid='procurement.manual_payment_decisions'::regclass
                 AND conname='ak_manual_payment_decision_governance' AND contype='u'
                """) == 1);
            var operational = await Scalar<string>(sql, """
                SELECT pg_get_constraintdef(oid) FROM pg_constraint
                 WHERE conrelid='procurement.operational_policy_identity_evidence'::regclass
                   AND conname='ck_operational_policy_identity'
                """);
            Require(operational.Contains("MANUAL_PAYMENT_REVIEW", StringComparison.Ordinal)
                && operational.Contains("schema_version", StringComparison.Ordinal));
            Require(!(await core.Database.GetPendingMigrationsAsync()).Any()
                && !(await procurement.Database.GetPendingMigrationsAsync()).Any()
                && !core.Database.HasPendingModelChanges() && !procurement.Database.HasPendingModelChanges());
            var complete = await Capture(sql, existingOnly: false);
            phase = "SecondProcurementMigrateIdempotency";
            await procurement.Database.MigrateAsync();
            Require(complete == await Capture(sql, existingOnly: false));
            Require(before == await Capture(sql, existingOnly: true));
            result["existingRowsAndUnrelatedShapeUnchanged"] = true;
            result["approvedP28AdditionsPresent"] = true;
            result["repeatMigrationNoFootprintChange"] = true;
            result["coreMigration"] = CoreClock; result["priorProcurementMigration"] = P27;
            result["procurementMigration"] = P28;
            result["status"] = "PASS_PRIVATE_CLONE_ONLY";
        }
        catch (Exception error)
        {
            // The clone can contain retained records: never serialize SQL, row values or exception text.
            result["status"] = "FAIL"; result["failurePhase"] = phase; result["exceptionType"] = error.GetType().Name;
            if (error.GetBaseException() is PostgresException postgres) result["sqlState"] = postgres.SqlState;
            Environment.ExitCode = 1;
        }
        try
        {
            await File.WriteAllTextAsync(evidence, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            File.SetUnixFileMode(evidence, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch { Environment.ExitCode = 1; }
        Console.WriteLine("Private P28 owner upgrade rehearsal finished; inspect protected summary only.");
    }

    private static async Task<Footprint> Capture(NpgsqlConnection db, bool existingOnly)
    {
        await using var tx = await db.BeginTransactionAsync();
        await using (var setup = new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL TIME ZONE 'UTC'; SET LOCAL DateStyle='ISO,YMD'", db, tx))
            await setup.ExecuteNonQueryAsync();
        var tables = new List<(string Schema, string Table)>();
        await using (var catalog = new NpgsqlCommand("""
            SELECT schemaname,tablename FROM pg_tables
             WHERE schemaname NOT IN ('pg_catalog','information_schema')
               AND (NOT @existing OR NOT(schemaname='procurement' AND tablename=ANY(@new_tables))) ORDER BY 1,2
            """, db, tx))
        {
            catalog.Parameters.AddWithValue("existing", existingOnly); catalog.Parameters.AddWithValue("new_tables", NewTables);
            await using var reader = await catalog.ExecuteReaderAsync();
            while (await reader.ReadAsync()) tables.Add((reader.GetString(0), reader.GetString(1)));
        }
        var rows = new List<string>();
        foreach (var (schema, table) in tables)
        {
            var projection = existingOnly && (schema, table) == ("procurement", "manual_payment_requests")
                ? "to_jsonb(r)-'title'" : "to_jsonb(r)";
            var predicate = existingOnly && (schema, table) == ("procurement", "__procurement_migrations")
                ? "WHERE \"MigrationId\"<>@p28" : "";
            await using var query = new NpgsqlCommand($"SELECT count(*)::text||':'||encode(sha256(convert_to(coalesce(string_agg(h,'' ORDER BY h),''),'UTF8')),'hex') FROM (SELECT encode(sha256(convert_to(({projection})::text,'UTF8')),'hex') h FROM {Quote(schema)}.{Quote(table)} r {predicate}) q", db, tx);
            query.Parameters.AddWithValue("p28", P28);
            rows.Add(schema + '.' + table + ':' + (string)(await query.ExecuteScalarAsync())!);
        }
        await using var shape = new NpgsqlCommand("""
            SELECT coalesce(string_agg(s,E'\n' ORDER BY s),'') FROM (
              SELECT n.nspname||'.'||c.relname||':C:'||a.attnum||':'||a.attname||':'||format_type(a.atttypid,a.atttypmod)||':'||a.attnotnull::text||':'||coalesce(pg_get_expr(d.adbin,d.adrelid),'') s
              FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped
              LEFT JOIN pg_attrdef d ON d.adrelid=c.oid AND d.adnum=a.attnum
              WHERE c.relkind IN ('r','p') AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
                AND (NOT @existing OR NOT(n.nspname='procurement' AND c.relname=ANY(@new_tables)))
                AND (NOT @existing OR NOT(n.nspname='procurement' AND c.relname='manual_payment_requests' AND a.attname='title'))
              UNION ALL SELECT n.nspname||'.'||c.relname||':K:'||k.conname||':'||pg_get_constraintdef(k.oid)
              FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace
              WHERE n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
                AND (NOT @existing OR NOT(n.nspname='procurement' AND c.relname=ANY(@new_tables)))
                AND (NOT @existing OR NOT(n.nspname='procurement' AND
                  (c.relname='operational_policy_identity_evidence' AND k.conname='ck_operational_policy_identity'
                   OR c.relname='manual_payment_decisions' AND k.conname='ak_manual_payment_decision_governance')))
              UNION ALL SELECT schemaname||'.'||tablename||':I:'||indexname||':'||indexdef
              FROM pg_indexes WHERE schemaname NOT LIKE 'pg_%' AND schemaname<>'information_schema'
                AND (NOT @existing OR NOT(schemaname='procurement' AND tablename=ANY(@new_tables)))
                AND (NOT @existing OR indexname<>'ak_manual_payment_decision_governance')
              UNION ALL SELECT n.nspname||'.'||c.relname||':T:'||t.tgname||':'||pg_get_triggerdef(t.oid)
              FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace
              WHERE NOT t.tgisinternal AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
                AND (NOT @existing OR NOT(n.nspname='procurement' AND c.relname=ANY(@new_tables)))
                AND (NOT @existing OR t.tgname NOT IN ('manual_payment_title_immutable','manual_payment_governance_bundle'))
              UNION ALL SELECT n.nspname||':F:'||p.proname||':'||pg_get_function_identity_arguments(p.oid)||':'||pg_get_functiondef(p.oid)
              FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
              WHERE n.nspname='procurement' AND p.prokind IN ('f','p')
                AND (NOT @existing OR p.proname<>ALL(@new_functions))
            ) q
            """, db, tx);
        shape.Parameters.AddWithValue("existing", existingOnly);
        shape.Parameters.AddWithValue("new_tables", NewTables);
        shape.Parameters.AddWithValue("new_functions", NewFunctions);
        var schemaText = (string)(await shape.ExecuteScalarAsync())!;
        await tx.RollbackAsync();
        return new(Hash(string.Join('\n', rows)), Hash(schemaText));
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Quote(string value) => '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    private static void Require(bool valid) { if (!valid) throw new InvalidOperationException("Private P28 upgrade invariant failed."); }
    private static async Task<T> Scalar<T>(NpgsqlConnection db, string sql, params (string Name, object Value)[] values)
    {
        await using var command = new NpgsqlCommand(sql, db);
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        return (T)(await command.ExecuteScalarAsync())!;
    }
    private sealed class OwnerTenant : ITenantAccessor { public string GetTenantId() => "offline-governance-upgrade"; }
}
