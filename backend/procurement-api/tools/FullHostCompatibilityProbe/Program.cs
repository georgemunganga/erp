using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

// This tool never creates a database. The reviewed launcher must create a NEW clone of the
// retained canonical private restored backup and run this binary in --network none.
var connection = Environment.GetEnvironmentVariable("ERP_HOST_SMOKE_CONNECTION")
    ?? throw new InvalidOperationException("Private new-clone connection required.");
var parsed = new NpgsqlConnectionStringBuilder(connection);
if (parsed.Database is not { } name || !name.StartsWith("proc_release_backup_20261022_smoke_", StringComparison.Ordinal)
    || !parsed.Host.StartsWith("/", StringComparison.Ordinal) || parsed.Host.Contains(','))
    throw new InvalidOperationException("Only a new bounded smoke clone over a local Unix socket is supported.");
if (args.Length != 0) throw new InvalidOperationException("No migration/publisher mode or custom host arguments are permitted.");
var evidence = Environment.GetEnvironmentVariable("ERP_HOST_SMOKE_RESULT")
    ?? throw new InvalidOperationException("Private result path required.");
if (File.Exists(evidence)) throw new InvalidOperationException("Do not reuse evidence paths.");
var scope = new ProcurementRequestScope();
scope.Set("offline-smoke", Guid.NewGuid(), "offline-smoke", null, null, false, "offline-smoke");
var result = new Dictionary<string, object?> { ["status"] = "NOT_STARTED", ["dataPrinted"] = false,
    ["limits"] = "Private clone only; no runtime-role, authenticated business workflow or deployment proof." };
var before = await Footprint(connection);
try
{
    await using var hrm = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>()
        .UseNpgsql(connection, n => n.MigrationsHistoryTable("__hrm_migrations", "hrm")).Options, new OfflineTenant());
    await using var procurement = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection, n => n.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, scope);
    var hrmPending = (await hrm.Database.GetPendingMigrationsAsync()).Count();
    var hrmDrift = hrm.Database.HasPendingModelChanges();
    var procPending = (await procurement.Database.GetPendingMigrationsAsync()).Count();
    var procDrift = procurement.Database.HasPendingModelChanges();
    result["hrmPendingMigrations"] = hrmPending; result["hrmPendingModelChanges"] = hrmDrift;
    result["procurementPendingMigrations"] = procPending; result["procurementPendingModelChanges"] = procDrift;
    // Actual Program calls Hrm Migrate at startup and ignores its pending-model warning.
    // Do not let that implicit behavior hide a compatibility blocker or apply unknown DDL.
    if (hrmPending != 0 || hrmDrift || procPending != 0 || procDrift)
    {
        result["status"] = "BLOCKED_OWNER_MIGRATION_OR_MODEL";
        Environment.ExitCode = 2;
    }
    else
    {
        // WebApplicationFactory uses the actual API assembly entry point, not copied routes.
        // Its default content-root search needs an explicit root when no solution file exists.
        var key = "TEST_CONTENTROOT_" + typeof(MonthlyLeaveAccrualWorker).Assembly.GetName().Name!.ToUpperInvariant().Replace('.', '_');
        Environment.SetEnvironmentVariable(key, AppContext.BaseDirectory);
        await using var factory = new OfflineHost(connection);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (factory.Services.GetServices<IHostedService>().Any(x => x is MonthlyLeaveAccrualWorker))
            throw new InvalidOperationException("Leave accrual worker must not be registered in this smoke harness.");
        var config = factory.Services.GetRequiredService<IConfiguration>();
        if (!string.IsNullOrEmpty(config["HRM:LocalAdminEmail"]) || !string.IsNullOrEmpty(config["HRM:LocalAdminPassword"]))
            throw new InvalidOperationException("Bootstrap must be disabled.");
        using var welcome = await client.GetAsync("/");
        using var health = await client.GetAsync("/health/ready");
        if (welcome.StatusCode != HttpStatusCode.OK || health.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException("Actual Program startup/root/database health failed.");
        result["status"] = "PASS"; result["actualProgramStarted"] = true;
        result["leaveWorkerRemovedTestOnly"] = true; result["bootstrapDisabled"] = true;
        result["outboxPublisherMode"] = false;
    }
}
catch (Exception error)
{
    // Actual clone may contain private business records. Never serialize exception messages,
    // SQL, connection strings, HTTP bodies, configuration or row data to any console/log.
    result["status"] = "FAIL"; result["exceptionType"] = error.GetType().Name; Environment.ExitCode = 1;
}
finally
{
    var after = await Footprint(connection);
    result["allTableValuesAndSchemasUnchanged"] = before == after;
    if (before != after) { result["status"] = "FAIL_CLONE_CHANGED"; Environment.ExitCode = 1; }
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(evidence))!);
    await File.WriteAllTextAsync(evidence, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Private full Program compatibility probe finished; consult protected summary only.");
}

static async Task<string> Footprint(string connection)
{
    await using var db = new NpgsqlConnection(connection); await db.OpenAsync();
    await using var tx = await db.BeginTransactionAsync();
    await using (var setup = new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL TIME ZONE 'UTC'; SET LOCAL DateStyle='ISO,YMD';", db, tx))
        await setup.ExecuteNonQueryAsync();
    var tables = new List<(string Schema, string Name)>();
    await using (var catalog = new NpgsqlCommand("SELECT n.nspname,c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind IN ('r','p') AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema' ORDER BY 1,2", db, tx))
    await using (var rows = await catalog.ExecuteReaderAsync()) while (await rows.ReadAsync()) tables.Add((rows.GetString(0), rows.GetString(1)));
    string Quote(string s) => '"' + s.Replace("\"", "\"\"") + '"';
    var parts = new List<string>();
    foreach (var table in tables)
    {
        await using var hash = new NpgsqlCommand($"SELECT count(*)::text || ':' || encode(sha256(convert_to(coalesce(string_agg(h,'' ORDER BY h),''),'UTF8')),'hex') FROM (SELECT encode(sha256(convert_to(to_jsonb(r)::text,'UTF8')),'hex') h FROM {Quote(table.Schema)}.{Quote(table.Name)} r)s", db, tx);
        parts.Add(table.Schema + "." + table.Name + ":" + (string)(await hash.ExecuteScalarAsync())!);
    }
    // Include all column/constraint/index definitions so startup cannot silently alter schema.
    await using var shape = new NpgsqlCommand("""
        SELECT coalesce(string_agg(s,E'\n' ORDER BY s),'') FROM (
          SELECT n.nspname||'.'||c.relname||':'||a.attnum||':'||a.attname||':'||format_type(a.atttypid,a.atttypmod)||':'||a.attnotnull::text||':'||coalesce(pg_get_expr(d.adbin,d.adrelid),'') s
          FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped LEFT JOIN pg_attrdef d ON d.adrelid=c.oid AND d.adnum=a.attnum
          WHERE c.relkind IN ('r','p') AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
          UNION ALL SELECT n.nspname||'.'||c.relname||':'||k.conname||':'||pg_get_constraintdef(k.oid) FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'
          UNION ALL SELECT schemaname||'.'||tablename||':'||indexname||':'||indexdef FROM pg_indexes WHERE schemaname NOT LIKE 'pg_%' AND schemaname<>'information_schema'
        )q
        """, db, tx);
    parts.Add((string)(await shape.ExecuteScalarAsync())!);
    await tx.RollbackAsync();
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts))));
}

sealed class OfflineTenant : ITenantAccessor { public string GetTenantId() => "offline-smoke"; }
sealed class OfflineHost(string connection) : WebApplicationFactory<MonthlyLeaveAccrualWorker>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Hrm"] = connection, ["ConnectionStrings:Procurement"] = connection,
            ["ConnectionStrings:ProcurementApprovalExecution"] = null,
            ["ERP:AuthMode"] = "local", ["HRM:AuthMode"] = "local",
            ["Procurement:Approvals:Enabled"] = "false", ["Procurement:Orders:Enabled"] = "false",
            ["Procurement:SourcingDrafts:Enabled"] = "false", ["Procurement:Receiving:DraftsEnabled"] = "false",
            ["Procurement:Receiving:PositiveExecutionEnabled"] = "false", ["Procurement:InvoiceMatching:Enabled"] = "false",
            ["CoreDocuments:Enabled"] = "false",
            ["HRM:LocalAdminEmail"] = "", ["HRM:LocalAdminPassword"] = "",
        }));
        builder.ConfigureTestServices(services =>
        {
            foreach (var registration in services.Where(x => x.ServiceType == typeof(IHostedService)
                && x.ImplementationType == typeof(MonthlyLeaveAccrualWorker)).ToArray()) services.Remove(registration);
            if (services.Any(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType?.Assembly == typeof(MonthlyLeaveAccrualWorker).Assembly))
                throw new InvalidOperationException("Unexpected application background worker; refuse offline startup.");
            services.RemoveAll<ILoggerProvider>();
        });
    }
}
