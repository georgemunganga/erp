using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

// Owner-only, new private targets. The retained mode proves P25 can be applied
// after a real P28 clone; the fresh mode runs the genuine Core/HRM/Procurement
// owning migration chain. Neither mode starts Program or provisions runtime roles.
internal static class P25PaymentOwnerCompatibilityRehearsal
{
    private const string P25 = "20261022161353_P25CorePrincipalRequestApproval";
    private const string P27 = "20261023125243_P27ManualPaymentRecords";
    private const string P28 = "20261023153226_P28ManualPaymentGovernance";
    private const string CoreClock = "20261023130726_CoreCompanyTimeZone";
    private const string CoreApproval = "20261019183218_P01CoreApprovalFoundation";
    private static readonly string[] NewColumns =
    [
        "purchase_requests.requester_core_worker_id", "purchase_requests.requester_identity_schema_version",
        "purchase_requests.requester_identity_sha256",
        "purchase_request_approval_submissions.principal_identity_sha256",
        "purchase_request_approval_submissions.requester_identity_sha256",
        "purchase_request_approval_submissions.requester_principal_id",
        "assignment_authority_evidence.identity_schema_version"
    ];
    private static readonly string[] ChangedChecks =
    [
        "ck_authority_identity_evidence", "ck_pr_original_principal_identity",
        "ck_pr_approval_principal_identity", "ck_pr_approval_submission_identity",
        "ck_pr_approval_worker_identity", "ck_pr_finance_identity_version",
        "ck_budget_checker_identity", "ck_pr_assignment_authority_identity"
    ];
    private sealed record Footprint(string Rows, string Shape);

    internal static async Task RunAsync(string connection, string evidence, bool fresh)
    {
        if (!OperatingSystem.IsLinux() || File.Exists(evidence)
            || Path.GetDirectoryName(Path.GetFullPath(evidence)) is not { } folder || !Directory.Exists(folder)
            || new DirectoryInfo(folder).LinkTarget is not null
            || (File.GetUnixFileMode(folder) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("A new owner-private evidence path is required.");
        var result = new Dictionary<string, object?>
        {
            ["status"] = "NOT_STARTED", ["mode"] = fresh ? "fresh-owner-chain" : "retained-p28-to-p25",
            ["productionChanged"] = false, ["actualProgramStarted"] = false, ["dataPrinted"] = false,
            ["limits"] = "New private target only; no runtime-role, HTTP, downgrade, or production acceptance."
        };
        var phase = "Admission";
        try
        {
            var scope = new ProcurementRequestScope();
            scope.Set("offline-p25-payment-compat", Guid.NewGuid(), "offline-p25-payment-compat",
                null, null, false, "offline-p25-payment-compat");
            await using var hrm = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection,
                x => x.MigrationsHistoryTable("__hrm_migrations", "hrm")).Options, new OfflineTenant());
            await using var approvals = new ApprovalDbContext(new DbContextOptionsBuilder<ApprovalDbContext>().UseNpgsql(connection,
                x => x.MigrationsHistoryTable("__EFMigrationsHistory", "core_approval")).Options, scope);
            await using var procurement = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql(connection, x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, scope);
            Require(!hrm.Database.HasPendingModelChanges() && !approvals.Database.HasPendingModelChanges()
                && !procurement.Database.HasPendingModelChanges());
            await using var sql = new NpgsqlConnection(connection);
            await sql.OpenAsync();
            if (fresh)
            {
                Require(await Scalar<long>(sql, """
                    SELECT (SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                      WHERE n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname NOT LIKE 'pg_toast%'
                        AND n.nspname NOT LIKE 'pg_temp%' AND c.relkind IN ('r','p','v','m','S','f'))
                      + (SELECT count(*) FROM pg_namespace WHERE nspname NOT IN ('pg_catalog','information_schema','public')
                        AND nspname NOT LIKE 'pg_toast%' AND nspname NOT LIKE 'pg_temp%')
                      + (SELECT count(*) FROM pg_extension WHERE extname<>'plpgsql')
                    """) == 0);
                phase = "FreshOwningMigrations";
                await hrm.Database.MigrateAsync();
                await approvals.Database.MigrateAsync();
                await procurement.Database.MigrateAsync();
                await VerifyComplete(hrm, approvals, procurement, sql);
                var first = await Capture(sql, normalizeP25: false);
                phase = "FreshSecondMigrate";
                await hrm.Database.MigrateAsync(); await approvals.Database.MigrateAsync();
                await procurement.Database.MigrateAsync();
                Require(first == await Capture(sql, normalizeP25: false));
                result["freshChainAndRepeatUnchanged"] = true;
            }
            else
            {
                Require(!(await hrm.Database.GetPendingMigrationsAsync()).Any()
                    && (await hrm.Database.GetAppliedMigrationsAsync()).Contains(CoreClock, StringComparer.Ordinal));
                Require(!(await approvals.Database.GetPendingMigrationsAsync()).Any()
                    && (await approvals.Database.GetAppliedMigrationsAsync()).Contains(CoreApproval, StringComparer.Ordinal));
                Require((await procurement.Database.GetPendingMigrationsAsync()).SequenceEqual([P25])
                    && (await procurement.Database.GetAppliedMigrationsAsync()).Contains(P27, StringComparer.Ordinal)
                    && (await procurement.Database.GetAppliedMigrationsAsync()).Contains(P28, StringComparer.Ordinal));
                Require(await Scalar<long>(sql, "SELECT count(*) FROM procurement.manual_payment_requests") > 0
                    && await Scalar<long>(sql, "SELECT count(*) FROM procurement.purchase_requests") > 0);
                await VerifyP25Shape(sql, installed: false);
                var before = await Capture(sql, normalizeP25: true);
                phase = "ApplyOlderPendingP25Only";
                await procurement.Database.MigrateAsync();
                var after = await Capture(sql, normalizeP25: true);
                Require(before == after);
                await VerifyP25Shape(sql, installed: true);
                await VerifyComplete(hrm, approvals, procurement, sql);
                var complete = await Capture(sql, normalizeP25: false);
                phase = "RetainedSecondMigrate";
                await procurement.Database.MigrateAsync();
                Require(complete == await Capture(sql, normalizeP25: false)
                    && before == await Capture(sql, normalizeP25: true));
                result["retainedRowsAndUnrelatedShapeUnchanged"] = true;
                result["repeatMigrationNoFootprintChange"] = true;
            }
            result["procurementMigrationIds"] = new[] { P25, P27, P28 };
            result["status"] = "PASS_PRIVATE_OWNER_ONLY";
        }
        catch (Exception error)
        {
            result["status"] = "FAIL"; result["failurePhase"] = phase;
            result["exceptionType"] = error.GetType().Name;
            if (error.GetBaseException() is PostgresException pg) result["sqlState"] = pg.SqlState;
            Environment.ExitCode = 1;
        }
        try
        {
            await File.WriteAllTextAsync(evidence, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            File.SetUnixFileMode(evidence, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch { Environment.ExitCode = 1; }
        Console.WriteLine("Private P25/P28 owner compatibility rehearsal finished; inspect protected summary only.");
    }

    private static async Task VerifyComplete(HrmDbContext hrm, ApprovalDbContext approvals,
        ProcurementDbContext procurement, NpgsqlConnection sql)
    {
        Require(!(await hrm.Database.GetPendingMigrationsAsync()).Any()
            && !(await approvals.Database.GetPendingMigrationsAsync()).Any()
            && !(await procurement.Database.GetPendingMigrationsAsync()).Any()
            && !hrm.Database.HasPendingModelChanges() && !approvals.Database.HasPendingModelChanges()
            && !procurement.Database.HasPendingModelChanges());
        var applied = (await procurement.Database.GetAppliedMigrationsAsync()).ToArray();
        Require(applied.Contains(P25, StringComparer.Ordinal) && applied.Contains(P27, StringComparer.Ordinal)
            && applied.Contains(P28, StringComparer.Ordinal));
        await VerifyP25Shape(sql, installed: true);
    }

    private static async Task VerifyP25Shape(NpgsqlConnection db, bool installed)
    {
        var columns = await Scalar<long>(db, """
            SELECT count(*) FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid
            JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname='procurement' AND c.relname||'.'||a.attname=ANY(@names)
              AND a.attnum>0 AND NOT a.attisdropped
            """, ("names", NewColumns));
        var checks = await Scalar<long>(db, """
            SELECT count(*) FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid
            JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname='procurement' AND k.conname=ANY(@names) AND k.contype='c'
            """, ("names", ChangedChecks));
        Require(columns == (installed ? NewColumns.Length : 0)
            && checks == (installed ? ChangedChecks.Length : ChangedChecks.Length - 2));
        var nullableWorkers = await Scalar<long>(db, """
            SELECT count(*) FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid
            JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname='procurement' AND c.relname='spending_authority_identity_evidence'
              AND a.attname IN ('actor_worker_id','recipient_worker_id') AND NOT a.attisdropped AND NOT a.attnotnull
            """);
        Require(nullableWorkers == (installed ? 2 : 0));
    }

    private static async Task<Footprint> Capture(NpgsqlConnection db, bool normalizeP25)
    {
        await using var transaction = await db.BeginTransactionAsync();
        await using (var setup = new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL TIME ZONE 'UTC'; SET LOCAL DateStyle='ISO,YMD'", db, transaction))
            await setup.ExecuteNonQueryAsync();
        var tables = new List<(string Schema, string Table)>();
        await using (var catalog = new NpgsqlCommand("""
            SELECT schemaname,tablename FROM pg_tables
            WHERE schemaname NOT IN ('pg_catalog','information_schema')
              AND schemaname NOT LIKE 'pg_toast%' AND schemaname NOT LIKE 'pg_temp%'
            ORDER BY 1,2
            """, db, transaction))
        await using (var reader = await catalog.ExecuteReaderAsync())
            while (await reader.ReadAsync()) tables.Add((reader.GetString(0), reader.GetString(1)));
        var rowParts = new List<string>();
        foreach (var (schema, table) in tables)
        {
            var projection = "to_jsonb(r)";
            if (normalizeP25 && schema == "procurement")
                foreach (var column in NewColumns.Where(x => x.StartsWith(table + ".", StringComparison.Ordinal)))
                    projection += "-'" + column[(table.Length + 1)..] + "'";
            var predicate = normalizeP25 && (schema, table) == ("procurement", "__procurement_migrations")
                ? "WHERE \"MigrationId\"<>@p25" : "";
            await using var command = new NpgsqlCommand($"""
                SELECT count(*)::text||':'||encode(sha256(convert_to(coalesce(string_agg(h,'' ORDER BY h),''),'UTF8')),'hex')
                FROM (SELECT encode(sha256(convert_to(({projection})::text,'UTF8')),'hex') h
                      FROM {Quote(schema)}.{Quote(table)} r {predicate}) q
                """, db, transaction);
            command.Parameters.AddWithValue("p25", P25);
            rowParts.Add(schema + '.' + table + ':' + (string)(await command.ExecuteScalarAsync())!);
        }
        await using var shape = new NpgsqlCommand("""
            SELECT coalesce(string_agg(s,E'\n' ORDER BY s),'') FROM (
              SELECT n.nspname||'.'||c.relname||':C:'||a.attname||':'||format_type(a.atttypid,a.atttypmod)||':'||
                     CASE WHEN @normalize AND n.nspname='procurement' AND c.relname='spending_authority_identity_evidence'
                                AND a.attname IN ('actor_worker_id','recipient_worker_id')
                          THEN 'reviewed-nullability' ELSE a.attnotnull::text END||':'||coalesce(pg_get_expr(d.adbin,d.adrelid),'') s
              FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
              JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped
              LEFT JOIN pg_attrdef d ON d.adrelid=c.oid AND d.adnum=a.attnum
              WHERE c.relkind IN ('r','p') AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
                AND (NOT @normalize OR NOT(n.nspname='procurement' AND c.relname||'.'||a.attname=ANY(@new_columns)))
              UNION ALL SELECT n.nspname||'.'||c.relname||':K:'||k.conname||':'||pg_get_constraintdef(k.oid)
              FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace
              WHERE n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
                AND (NOT @normalize OR NOT(n.nspname='procurement' AND k.conname=ANY(@changed_checks)))
              UNION ALL SELECT schemaname||'.'||tablename||':I:'||indexname||':'||indexdef
              FROM pg_indexes WHERE schemaname NOT LIKE 'pg_%' AND schemaname<>'information_schema'
              UNION ALL SELECT n.nspname||'.'||c.relname||':T:'||t.tgname||':'||pg_get_triggerdef(t.oid)
              FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace
              WHERE NOT t.tgisinternal AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
              UNION ALL SELECT n.nspname||':F:'||p.proname||':'||pg_get_function_identity_arguments(p.oid)||':'||pg_get_functiondef(p.oid)
              FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
              WHERE n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema' AND p.prokind IN ('f','p')
            ) q
            """, db, transaction);
        shape.Parameters.AddWithValue("normalize", normalizeP25);
        shape.Parameters.AddWithValue("new_columns", NewColumns);
        shape.Parameters.AddWithValue("changed_checks", ChangedChecks);
        var shapeText = (string)(await shape.ExecuteScalarAsync())!;
        await transaction.RollbackAsync();
        return new(Hash(string.Join('\n', rowParts)), Hash(shapeText));
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Quote(string value) => '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';

    private static void Require(bool valid)
    { if (!valid) throw new InvalidOperationException("Private P25/P28 owner compatibility invariant failed."); }
    private static async Task<T> Scalar<T>(NpgsqlConnection db, string commandText,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(commandText, db);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return (T)(await command.ExecuteScalarAsync())!;
    }
    private sealed class OfflineTenant : ITenantAccessor
    { public string GetTenantId() => "offline-p25-payment-compat"; }
}
