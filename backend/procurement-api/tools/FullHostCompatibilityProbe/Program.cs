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

if (args.SequenceEqual(new[] { "--preflight-core-session-config" }))
{
    CoreLocalSessionConfigurationVerification.Verify();
    Console.WriteLine("PASS_PRIVATE_CORE_SESSION_CONFIG_PREFLIGHT_ONLY");
    return;
}

if (args.SequenceEqual(new[] { "--core-local-session-manifest" }) || args.SequenceEqual(new[] { "--restored-core-local-session" }))
{
    try
    {
        if(args[0]=="--core-local-session-manifest") await RestoredCoreLocalSessionVerification.DescribeAsync();
        else await RestoredCoreLocalSessionVerification.RunAsync();
    }
    catch(Exception)
    {
        Console.Error.WriteLine("Private Core local-session verification failed; protected owner evidence required.");
        Environment.ExitCode=1;
    }
    return;
}

if (args.SequenceEqual(new[] { "--restored-finite-login" }))
{
    try { await RestoredFiniteLoginVerification.RunAsync(); }
    catch (Exception)
    {
        // Never echo connection strings, generated passwords, SQL or provider diagnostics.
        Console.Error.WriteLine("Private restored finite-login verification failed; protected coordinator evidence required.");
        Environment.ExitCode=1;
    }
    return;
}

// Explicit private P25 acceptance is separate from the unchanged read-only smoke mode.
if (args.SequenceEqual(new[] { "--preflight-p30-rfq-fixture" }))
{
    var rules = PrincipalApprovalHttpVerification.BuildP30RfqSourcingRules(
        Guid.Parse("11111111-1111-4111-8111-111111111111"));
    PrincipalApprovalHttpVerification.PreflightP30RfqSourcingRules(rules);
    var branchOnly = rules.Replace("\"branchIds\":[]",
        "\"branchIds\":[\"11111111-1111-4111-8111-111111111111\"]",
        StringComparison.Ordinal);
    if (branchOnly == rules ||
        Mightyfin.Erp.Procurement.Application.SourcingPublicationPolicyRules
            .AnalyzeCoverage(branchOnly).Count == 0)
        throw new InvalidOperationException("Private RFQ fallback regression was not detected.");
    Console.WriteLine("PASS_PRIVATE_P30_RFQ_POLICY_PREFLIGHT_ONLY");
    return;
}
if (args.SequenceEqual(new[] { "--principal-approvals" }))
{
    await PrincipalApprovalHttpVerification.RunAsync();
    return;
}

// This tool never creates a database. The reviewed launcher must create a NEW clone of the
// retained canonical private restored backup and run this binary in --network none.
var connection = Environment.GetEnvironmentVariable("ERP_HOST_SMOKE_CONNECTION")
    ?? throw new InvalidOperationException("Private new-clone connection required.");
var freshHrmOnly = args.SequenceEqual(new[] { "--rehearse-fresh-hrm" });
var ownerUpgradeOnly = args.SequenceEqual(new[] { "--rehearse-payment-owner-upgrade" });
var governanceUpgradeOnly = args.SequenceEqual(new[] { "--rehearse-payment-governance-upgrade" });
var p25OverP28Only = args.SequenceEqual(new[] { "--rehearse-p25-over-p28" });
var p25FreshOnly = args.SequenceEqual(new[] { "--rehearse-p25-fresh-owner" });
var parsed = new NpgsqlConnectionStringBuilder(connection);
if (parsed.Database is not { } name || !(freshHrmOnly
        ? System.Text.RegularExpressions.Regex.IsMatch(name, "^hrm_model_reconcile_20261022_[a-z0-9_]{1,24}$")
        : p25OverP28Only || p25FreshOnly
            ? System.Text.RegularExpressions.Regex.IsMatch(name, "^proc_p25_compat_20261023_[a-z0-9]{6,20}$")
        : governanceUpgradeOnly
            ? System.Text.RegularExpressions.Regex.IsMatch(name, "^proc_payment_http_20261023_[a-z0-9]{6,20}$")
        : ownerUpgradeOnly
            ? System.Text.RegularExpressions.Regex.IsMatch(name, "^proc_release_backup_20261022_core_principal_[a-z0-9_]{1,16}$")
            : name.StartsWith("proc_release_backup_20261022_smoke_", StringComparison.Ordinal))
    || parsed.Host is not { } host || !host.StartsWith("/", StringComparison.Ordinal) || host.Contains(','))
    throw new InvalidOperationException("Only a new bounded smoke clone over a local Unix socket is supported.");
var diagnosticOnly = args.SequenceEqual(new[] { "--diagnose-hrm-model" });
if (args.Length != 0 && !diagnosticOnly && !freshHrmOnly && !ownerUpgradeOnly && !governanceUpgradeOnly
    && !p25OverP28Only && !p25FreshOnly) throw new InvalidOperationException("Only explicit diagnostic or owning migration rehearsal flags are supported; no publisher arguments.");
var evidence = Environment.GetEnvironmentVariable("ERP_HOST_SMOKE_RESULT")
    ?? throw new InvalidOperationException("Private result path required.");
if (File.Exists(evidence)) throw new InvalidOperationException("Do not reuse evidence paths.");
if (p25OverP28Only || p25FreshOnly)
{
    await P25PaymentOwnerCompatibilityRehearsal.RunAsync(connection, evidence, p25FreshOnly);
    return;
}
if (ownerUpgradeOnly)
{
    await PaymentOwnerUpgradeRehearsal.RunAsync(connection, evidence);
    return;
}
if (governanceUpgradeOnly)
{
    await PaymentGovernanceUpgradeRehearsal.RunAsync(connection, evidence);
    return;
}
if (freshHrmOnly)
{
    await HrmFreshRehearsal.RunAsync(connection, evidence);
    return;
}
var defaultTenant = diagnosticOnly ? "offline-diagnostic" : Environment.GetEnvironmentVariable("ERP_HOST_SMOKE_DEFAULT_TENANT");
if (string.IsNullOrWhiteSpace(defaultTenant) || defaultTenant != defaultTenant.Trim()
    || defaultTenant.Length > 200 || defaultTenant.Any(char.IsControl))
    throw new InvalidOperationException("Require explicitly verified deployment tenant for actual host composition.");
var scope = new ProcurementRequestScope();
scope.Set("offline-smoke", Guid.NewGuid(), "offline-smoke", null, null, false, "offline-smoke");
var result = new Dictionary<string, object?> { ["status"] = "NOT_STARTED", ["dataPrinted"] = false,
    ["limits"] = "Private clone only; no runtime-role, authenticated business workflow or deployment proof." };
string? before = null;
var phase = "InitialFootprint";
try
{
    before = await Footprint(connection);
    phase = "OwnerModels";
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
    if (diagnosticOnly)
    {
        result["hrmModelDiagnostic"] = await HrmModelDiagnostic.DescribeAsync(hrm, hrmDrift);
        result["status"] = "OWNER_MODEL_DIAGNOSTIC_ONLY_PROGRAM_NOT_STARTED";
        result["actualProgramStarted"] = false;
        if (hrmPending != 0 || hrmDrift || procPending != 0 || procDrift) Environment.ExitCode = 2;
    }
    else if (hrmPending != 0 || hrmDrift || procPending != 0 || procDrift)
    {
        result["status"] = "BLOCKED_OWNER_MIGRATION_OR_MODEL";
        Environment.ExitCode = 2;
    }
    else
    {
        // WebApplicationFactory uses the actual API assembly entry point, not copied routes.
        // Its default content-root search needs an explicit root when no solution file exists.
        var key = "TEST_CONTENTROOT_" + typeof(MonthlyLeaveAccrualWorker).Assembly.GetName().Name!.ToUpperInvariant().Replace('.', '_');
        // WebHostBuilder imports ASPNETCORE_* environment settings before WAF searches
        // for a solution file. A raw process variable is not an IWebHostBuilder setting.
        Environment.SetEnvironmentVariable("ASPNETCORE_" + key, AppContext.BaseDirectory);
        // Minimal Program reads connection/auth settings before builder.Build, while
        // WAF web configuration callbacks are deferred until that build. Supply the
        // same isolated process settings before executing the real entry point.
        foreach (var setting in OfflineHost.Settings(connection, defaultTenant))
            Environment.SetEnvironmentVariable(setting.Key.Replace(":", "__", StringComparison.Ordinal), setting.Value);
        phase = "ActualProgramCreateClient";
        await using var factory = new OfflineHost(connection, defaultTenant);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        phase = "OfflineServicesCheck";
        var accrualServicePresent = factory.Services.GetServices<IHostedService>().Any(x => x is MonthlyLeaveAccrualWorker);
        result["accrualServicePresent"] = accrualServicePresent;
        if (accrualServicePresent)
            throw new InvalidOperationException("Leave accrual worker must not be registered in this smoke harness.");
        var config = factory.Services.GetRequiredService<IConfiguration>();
        var bootstrapConfigured = !string.IsNullOrEmpty(config["HRM:LocalAdminEmail"]) || !string.IsNullOrEmpty(config["HRM:LocalAdminPassword"]);
        result["bootstrapConfigured"] = bootstrapConfigured;
        if (bootstrapConfigured)
            throw new InvalidOperationException("Bootstrap must be disabled.");
        phase = "RootAndReadinessHttp";
        using var welcome = await client.GetAsync("/");
        using var health = await client.GetAsync("/health/ready");
        result["welcomeStatus"] = (int)welcome.StatusCode; result["healthStatus"] = (int)health.StatusCode;
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
    result["status"] = "FAIL"; result["exceptionType"] = error.GetType().Name;
    result["failurePhase"] = phase;
    result["failureMethods"] = new System.Diagnostics.StackTrace(error, false).GetFrames()
        .Select(frame => frame.GetMethod())
        .Where(method => method is not null)
        .Take(16)
        .Select(method => method!.DeclaringType?.FullName + "." + method.Name).ToArray();
    // Fixed diagnostic classification only, never emit private exception text.
    result["solutionRootDiscoveryFailure"] = error is InvalidOperationException
        && error.StackTrace?.Contains("UseSolutionRelativeContentRoot", StringComparison.Ordinal) == true;
    Environment.ExitCode = 1;
}
finally
{
    try
    {
        var after = await Footprint(connection);
        result["allTableValuesAndSchemasUnchanged"] = before is not null && before == after;
        if (before is not null && before != after) { result["status"] = "FAIL_CLONE_CHANGED"; Environment.ExitCode = 1; }
    }
    catch (Exception error)
    {
        result["status"] = "FAIL_FOOTPRINT"; result["footprintExceptionType"] = error.GetType().Name;
        result["allTableValuesAndSchemasUnchanged"] = false; Environment.ExitCode = 1;
    }
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(evidence))!);
        await File.WriteAllTextAsync(evidence, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }
    catch { Environment.ExitCode = 1; }
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
sealed class OfflineHost(string connection, string defaultTenant) : WebApplicationFactory<MonthlyLeaveAccrualWorker>
{
    internal static Dictionary<string, string?> Settings(string connection, string defaultTenant) => new()
    {
        ["HRM:DefaultTenantId"] = defaultTenant,
        ["ConnectionStrings:Hrm"] = connection, ["ConnectionStrings:Procurement"] = connection,
        ["ConnectionStrings:ProcurementApprovalExecution"] = null,
        ["ERP:AuthMode"] = "local", ["HRM:AuthMode"] = "local",
        ["Procurement:Approvals:Enabled"] = "false", ["Procurement:Orders:Enabled"] = "false",
        ["Procurement:SourcingDrafts:Enabled"] = "false", ["Procurement:Receiving:DraftsEnabled"] = "false",
        ["Procurement:Receiving:PositiveExecutionEnabled"] = "false", ["Procurement:InvoiceMatching:Enabled"] = "false",
        ["CoreDocuments:Enabled"] = "false",
        ["HRM:LocalAdminEmail"] = "", ["HRM:LocalAdminPassword"] = "",
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(Settings(connection, defaultTenant)));
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
