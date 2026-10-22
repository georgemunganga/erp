using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

// Fixture-only actual owning EF migration path. Never used by the application startup/DI.
internal static class HrmFreshRehearsal
{
    internal static async Task RunAsync(string connection, string evidence)
    {
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        if (parsed.Database is not { } database
            || !System.Text.RegularExpressions.Regex.IsMatch(database, "^hrm_model_reconcile_20261022_[a-z0-9_]{1,24}$")
            || parsed.Host is not { } host || !host.StartsWith("/", StringComparison.Ordinal) || host.Contains(','))
            throw new InvalidOperationException("Only a new private fresh HRM rehearsal database over Unix is supported.");
        var result = new Dictionary<string, object?>
        {
            ["status"] = "NOT_STARTED", ["dataPrinted"] = false, ["actualProgramStarted"] = false,
            ["limits"] = "New disposable database, actual EF migration engine only; no runtime-role or production proof."
        };
        var phase = "OwnerModel";
        try
        {
            await using var db = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>()
                .UseNpgsql(connection, n => n.MigrationsHistoryTable("__hrm_migrations", "hrm")).Options, new FreshTenant());
            if (db.Database.HasPendingModelChanges())
                throw new InvalidOperationException("Real owner model must match its snapshot before migration.");
            phase = "EmptyTargetCheck";
            await using (var target = new NpgsqlConnection(connection))
            {
                await target.OpenAsync();
                await using var empty = new NpgsqlCommand("""
                    SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                    WHERE n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema';
                    """, target);
                if ((long)(await empty.ExecuteScalarAsync())! != 0)
                    throw new InvalidOperationException("Fresh rehearsal refuses any existing user object; never reset or reuse.");
            }
            if ((await db.Database.GetAppliedMigrationsAsync()).Any())
                throw new InvalidOperationException("Fresh rehearsal refuses existing migration history.");
            var source = new NpgsqlConnectionStringBuilder(connection)
                { Database = "proc_release_backup_20261022_canonical_0619" }.ConnectionString;
            phase = "RetainedReadOnlyCatalog";
            var retainedBefore = await CatalogAsync(source);
            phase = "MigrateFirst";
            await db.Database.MigrateAsync();
            var first = await CatalogAsync(connection);
            var firstData = await DataDigestAsync(connection);
            phase = "MigrateSecond";
            await db.Database.MigrateAsync();
            var second = await CatalogAsync(connection);
            var secondData = await DataDigestAsync(connection);
            var retainedAfter = await CatalogAsync(source);
            result["realPendingModelChanges"] = db.Database.HasPendingModelChanges();
            result["pendingMigrations"] = (await db.Database.GetPendingMigrationsAsync()).Count();
            result["secondMigrationSchemaHistoryAndDataUnchanged"] = first == second && firstData == secondData;
            result["retainedCatalogUnchanged"] = retainedBefore == retainedAfter;
            result["freshCatalogHistoryMatchesRetained"] = first == retainedBefore;
            if (first != second || firstData != secondData || retainedBefore != retainedAfter || first != retainedBefore
                || db.Database.HasPendingModelChanges() || (await db.Database.GetPendingMigrationsAsync()).Any())
                throw new InvalidOperationException("Actual fresh and retained owner contracts must match without repair.");
            result["status"] = "PASS";
        }
        catch (Exception error)
        {
            result["status"] = "FAIL"; result["failurePhase"] = phase;
            result["exceptionType"] = error.GetType().Name;
            result["failureMethods"] = new System.Diagnostics.StackTrace(error, false).GetFrames()
                .Select(frame => frame.GetMethod()).Where(method => method is not null).Take(12)
                .Select(method => method!.DeclaringType?.FullName + "." + method.Name).ToArray();
            if (error.GetBaseException() is PostgresException postgres)
            {
                result["sqlState"] = postgres.SqlState;
                // Schema metadata only, never Detail/MessageText/SQL/parameters.
                result["constraintName"] = postgres.ConstraintName;
            }
            Environment.ExitCode = 1;
        }
        await File.WriteAllTextAsync(evidence, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Private fresh HRM migration rehearsal finished; consult protected summary only.");
    }

    private static async Task<string> CatalogAsync(string connection)
    {
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();
        await using (var setup = new NpgsqlCommand("SET TRANSACTION READ ONLY", db, tx)) await setup.ExecuteNonQueryAsync();
        await using var query = new NpgsqlCommand("""
            SELECT coalesce(string_agg(s,E'\n' ORDER BY s),'') FROM (
              SELECT c.relname||':'||a.attname||':'||format_type(a.atttypid,a.atttypmod)||':'||a.attnotnull::text||':'||coalesce(pg_get_expr(d.adbin,d.adrelid),'') s
              FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
              JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped
              LEFT JOIN pg_attrdef d ON d.adrelid=c.oid AND d.adnum=a.attnum
              WHERE n.nspname='hrm' AND c.relkind IN ('r','p')
              UNION ALL SELECT c.relname||':'||k.conname||':'||pg_get_constraintdef(k.oid)
              FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='hrm'
              UNION ALL SELECT tablename||':'||indexname||':'||indexdef FROM pg_indexes WHERE schemaname='hrm'
              UNION ALL SELECT 'history:'||"MigrationId" FROM hrm.__hrm_migrations
            ) q
            """, db, tx);
        var text = (string)(await query.ExecuteScalarAsync())!;
        await tx.RollbackAsync();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static async Task<string> DataDigestAsync(string connection)
    {
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();
        await using (var setup = new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL TIME ZONE 'UTC'; SET LOCAL DateStyle='ISO,YMD'", db, tx)) await setup.ExecuteNonQueryAsync();
        var names = new List<string>();
        await using (var query = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname='hrm' ORDER BY tablename", db, tx))
        await using (var rows = await query.ExecuteReaderAsync()) while (await rows.ReadAsync()) names.Add(rows.GetString(0));
        var parts = new List<string>();
        foreach (var name in names)
        {
            var quoted = '"' + name.Replace("\"", "\"\"") + '"';
            await using var query = new NpgsqlCommand($"SELECT count(*)::text||':'||encode(sha256(convert_to(coalesce(string_agg(h,'' ORDER BY h),''),'UTF8')),'hex') FROM (SELECT encode(sha256(convert_to(to_jsonb(r)::text,'UTF8')),'hex') h FROM hrm.{quoted} r)s", db, tx);
            parts.Add(name + ':' + (string)(await query.ExecuteScalarAsync())!);
        }
        await tx.RollbackAsync();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts))));
    }

    private sealed class FreshTenant : ITenantAccessor { public string GetTenantId() => "offline-fresh-rehearsal"; }
}
