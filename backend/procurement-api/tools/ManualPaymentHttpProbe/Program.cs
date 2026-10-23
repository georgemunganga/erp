using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

// An owner-created clone, its finite runtime accounts and P28 schema must already exist.
// This process never creates a database, grants SQL roles, migrates or overrides readiness.
if (args.Length != 0) throw new ArgumentException("This probe has no mode flags.");
var owner = Required("PROCUREMENT_PAYMENT_HTTP_OWNER_CONNECTION");
var runtime = Required("PROCUREMENT_PAYMENT_HTTP_RUNTIME_CONNECTION");
var execution = Required("PROCUREMENT_PAYMENT_HTTP_EXECUTION_CONNECTION");
var resultPath = Required("PROCUREMENT_PAYMENT_HTTP_RESULT");
if (Environment.GetEnvironmentVariable("PROCUREMENT_PAYMENT_HTTP_EXPLICIT_TEST_OPTIONS") != "enabled-in-private-clone")
    throw new InvalidOperationException("Explicit private payment test options are required.");
ProbeBoundary.Validate(owner, runtime, execution, resultPath);
var tenant = "payment-http-" + Guid.NewGuid().ToString("N")[..16];
var result = new Dictionary<string, object?> { ["status"] = "NOT_STARTED", ["actualProgram"] = false,
    ["normalLocalLogin"] = false, ["paymentRoutesEnabledInProduction"] = false, ["financePostingClaimed"] = false };
var phase = "schema-admission";
try
{
    await ProbeBoundary.RequireAppliedSchemas(owner, tenant);
    phase = "actual-host-admission";
    var assemblyName = typeof(MonthlyLeaveAccrualWorker).Assembly.GetName().Name!;
    Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_" + assemblyName.ToUpperInvariant().Replace('.', '_'),
        AppContext.BaseDirectory);
    // Program consumes its initial configuration before WebApplicationFactory's
    // ConfigureAppConfiguration callback; provide the same private values to it.
    foreach (var setting in PrivatePaymentHost.Settings(runtime, execution, tenant))
        Environment.SetEnvironmentVariable(setting.Key.Replace(":", "__", StringComparison.Ordinal), setting.Value);
    await using var host = new PrivatePaymentHost(runtime, execution, tenant);
    using var anonymous = host.CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    result["actualProgram"] = true;
    // No copied route graph and no synthetic AuthenticationHandler. A missing map or
    // absent real governance service is a BLOCKED gate before fixture writes.
    var routes = host.Services.GetServices<EndpointDataSource>().SelectMany(x => x.Endpoints)
        .OfType<RouteEndpoint>().Select(x => x.RoutePattern.RawText?.TrimEnd('/')).ToHashSet(StringComparer.Ordinal);
    if (!routes.Contains("/api/procurement/v1/manual-payments")
        || !routes.Contains("/api/procurement/v1/manual-payments/commands/{key:guid}"))
        throw new ProbeUnavailable("ManualPaymentRoutesNotComposed");
    using (var serviceScope = host.Services.CreateScope())
    {
        var readiness = serviceScope.ServiceProvider.GetService<IProcurementManualPaymentGovernanceReadiness>();
        if (readiness is null || !await readiness.IsReadyAsync(default))
            throw new ProbeUnavailable("ManualPaymentGovernanceNotReady");
    }
    phase = "private-core-fixture";
    var fixture = await PaymentFixtureStore.Seed(owner, tenant);
    phase = "normal-local-login-and-policy";
    var journey = new PaymentHttpJourney(host, fixture);
    await journey.LoginAndPublishPolicy();
    result["normalLocalLogin"] = true;
    phase = "payment-http-workflows";
    await journey.Verify();
    phase = "owner-evidence-check";
    await PaymentFixtureStore.VerifyOwnerEvidence(owner, fixture);
    result["status"] = "PASS_PRIVATE_CLONE_ONLY";
    result["checks"] = journey.Checks;
    result["manualPaymentStatus"] = "ConfirmedManually";
}
catch (ProbeUnavailable unavailable)
{
    result["status"] = "BLOCKED_NOT_AVAILABLE";
    result["gate"] = unavailable.Code;
    Environment.ExitCode = 2;
}
catch (Exception error)
{
    // Do not record exception text, stack traces, SQL, account IDs or HTTP bodies.
    result["status"] = "FAIL";
    result["phase"] = phase;
    result["exceptionType"] = error.GetType().Name;
    Environment.ExitCode = 1;
}
finally
{
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(resultPath))!);
        await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(result));
        File.SetUnixFileMode(resultPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
    catch { Environment.ExitCode = 1; }
    Console.WriteLine("Private manual payment HTTP probe finished; inspect the owner-private result.");
}

static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
    ? value : throw new InvalidOperationException("A required private probe setting is absent.");

internal sealed class ProbeUnavailable(string code) : Exception(code)
{ internal string Code { get; } = code; }

internal static class ProbeBoundary
{
    internal static void Validate(string owner, string runtime, string execution, string result)
    {
        if (!OperatingSystem.IsLinux() || File.Exists(result)
            || Path.GetDirectoryName(Path.GetFullPath(result)) is not { } folder
            || !Directory.Exists(folder) || new DirectoryInfo(folder).LinkTarget is not null
            || (File.GetUnixFileMode(folder) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new InvalidOperationException("A new owner-private result path on Linux is required.");
        var connections = new[] { owner, runtime, execution }.Select(x => new NpgsqlConnectionStringBuilder(x)).ToArray();
        var first = connections[0];
        if (first.Database is null || !Regex.IsMatch(first.Database, "^proc_payment_http_[0-9]{8}_[a-z0-9]{6,20}$")
            || first.Host is null || !first.Host.StartsWith("/", StringComparison.Ordinal) || first.Host.Contains(',')
            || connections.Any(x => x.Database != first.Database || x.Host != first.Host || x.Port != first.Port
                || string.IsNullOrWhiteSpace(x.Username))
            || connections.Select(x => x.Username).Distinct(StringComparer.Ordinal).Count() != 3)
            throw new InvalidOperationException("Three distinct identities on one new private clone and Unix socket are required.");
    }

    internal static async Task RequireAppliedSchemas(string owner, string tenant)
    {
        await using var connection = new NpgsqlConnection(owner);
        await connection.OpenAsync();
        const string migration = "20261023153226_P28ManualPaymentGovernance";
        await using (var command = new NpgsqlCommand("SELECT count(*) FROM procurement.__procurement_migrations WHERE \"MigrationId\"=@id", connection))
        {
            command.Parameters.AddWithValue("id", migration);
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1) throw new ProbeUnavailable("P28MigrationNotApplied");
        }
        foreach (var role in new[] { "runtime", "execution" })
        {
            // The runner supplies the finite database identities. This probe verifies,
            // rather than creates or widens, their PostgreSQL authority boundary.
            var value = Environment.GetEnvironmentVariable(role == "runtime"
                ? "PROCUREMENT_PAYMENT_HTTP_RUNTIME_CONNECTION" : "PROCUREMENT_PAYMENT_HTTP_EXECUTION_CONNECTION")
                ?? throw new ProbeUnavailable("FiniteDatabaseRoleRequired");
            var name = new NpgsqlConnectionStringBuilder(value).Username;
            await using var command = new NpgsqlCommand("""
                SELECT count(*) FROM pg_roles r WHERE r.rolname=@name AND r.rolcanlogin AND NOT r.rolsuper
                  AND NOT r.rolcreatedb AND NOT r.rolcreaterole AND NOT r.rolreplication AND NOT r.rolbypassrls
                  AND NOT EXISTS (SELECT 1 FROM pg_auth_members m WHERE m.member=r.oid)
                  AND NOT EXISTS (SELECT 1 FROM pg_namespace n WHERE n.nspowner=r.oid)
                  AND NOT EXISTS (SELECT 1 FROM pg_class c WHERE c.relowner=r.oid)
                  AND NOT EXISTS (SELECT 1 FROM pg_database d WHERE d.datdba=r.oid)
                """, connection);
            command.Parameters.AddWithValue("name", name!);
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
                throw new ProbeUnavailable("FiniteDatabaseRoleRequired");
        }
        await using var hrm = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(owner,
            x => x.MigrationsHistoryTable("__hrm_migrations", "hrm")).Options, new ProbeTenant(tenant));
        await using var procurement = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(owner,
            x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options,
            new ProbeProcurementScope(tenant, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        if ((await hrm.Database.GetPendingMigrationsAsync()).Any() || hrm.Database.HasPendingModelChanges()
            || (await procurement.Database.GetPendingMigrationsAsync()).Any() || procurement.Database.HasPendingModelChanges())
            throw new ProbeUnavailable("OwnerSchemaOrModelNotReady");
    }
}

internal sealed class PrivatePaymentHost(string runtime, string execution, string tenant)
    : WebApplicationFactory<MonthlyLeaveAccrualWorker>
{
    internal static Dictionary<string, string?> Settings(string runtime, string execution, string tenant) => new()
    {
        ["HRM:DefaultTenantId"] = tenant,
        ["ConnectionStrings:Hrm"] = runtime,
        ["ConnectionStrings:Procurement"] = execution,
        ["ConnectionStrings:ProcurementApprovalExecution"] = execution,
        ["ERP:AuthMode"] = "local", ["HRM:AuthMode"] = "local",
        ["Procurement:Approvals:Enabled"] = "false", ["Procurement:Orders:Enabled"] = "false",
        ["Procurement:SourcingDrafts:Enabled"] = "false",
        ["Procurement:Receiving:DraftsEnabled"] = "false",
        ["Procurement:Receiving:PositiveExecutionEnabled"] = "false",
        ["Procurement:InvoiceMatching:Enabled"] = "false",
        ["Procurement:InvoiceUnitReview:Enabled"] = "false",
        ["Procurement:ManualPayments:Enabled"] = "true",
        ["HRM:LocalAdminEmail"] = "", ["HRM:LocalAdminPassword"] = "",
        ["CoreDocuments:Enabled"] = "false",
    };
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(Settings(runtime, execution, tenant)));
        builder.ConfigureTestServices(services =>
        {
            foreach (var registration in services.Where(x => x.ServiceType == typeof(IHostedService)
                && x.ImplementationType == typeof(MonthlyLeaveAccrualWorker)).ToArray()) services.Remove(registration);
            if (services.Any(x => x.ServiceType == typeof(IHostedService)
                && x.ImplementationType?.Assembly == typeof(MonthlyLeaveAccrualWorker).Assembly))
                throw new ProbeUnavailable("UnexpectedERPBackgroundWorker");
            services.RemoveAll<ILoggerProvider>();
            // Deliberately no auth, payment readiness, workflow or schema service replacement.
        });
    }
}

internal sealed class ProbeTenant(string tenant) : ITenantAccessor
{ public string GetTenantId() => tenant; }

internal sealed class ProbeProcurementScope(string tenant, Guid company, Guid branch, Guid actor)
    : Mightyfin.Erp.Procurement.Application.IProcurementScope
{
    public string TenantId => tenant;
    public Guid LegalEntityId => company;
    public Guid? WorkLocationId => branch;
    public Guid? OrgUnitId => null;
    public bool IsConfined => true;
    public string SubjectId => actor.ToString("D");
    public Guid? CoreLinkedWorkerId => null;
    public string? CorrelationId => "private-manual-payment-http-fixture";
}
