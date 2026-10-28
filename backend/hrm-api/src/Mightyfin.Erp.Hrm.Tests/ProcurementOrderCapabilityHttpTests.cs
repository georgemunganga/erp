using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Actual mapped metadata route; availability only, not workflow or PostgreSQL migration proof.</summary>
public sealed class ProcurementOrderCapabilityHttpTests
{
    [Theory]
    [InlineData("disabled", false)]
    [InlineData("missing-connection", false)]
    [InlineData("absent-policy", false)]
    [InlineData("duplicate-policy", false)]
    [InlineData("future-policy", false)]
    [InlineData("expired-policy", false)]
    [InlineData("other-company", false)]
    [InlineData("unapproved-policy", false)]
    [InlineData("disabled-policy", false)]
    [InlineData("schema-not-ready", false)]
    [InlineData("finance-checker-schema-not-ready", false)]
    [InlineData("pending-procurement-schema", false)]
    [InlineData("absent-delivery-policy", false)]
    [InlineData("absent-delivery-channels", false)]
    [InlineData("invalid-freshness", false)]
    [InlineData("configured", true)]
    [InlineData("pending-model", false)]
    [InlineData("unknown-model", false)]
    public async Task Mapped_metadata_exposes_only_configured_current_available_orders(string scenario, bool expected)
    {
        var company = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var policy = new ProcurementManualFinancePolicy
        {
            Enabled = true, LegalEntityId = company, ApprovedPolicyReference = "TEST-INDEPENDENTLY-APPROVED",
            EffectiveFrom = now.AddDays(-1), EffectiveUntil = now.AddDays(1),
        };
        var options = new ProcurementOrderExecutionOptions
        {
            Enabled = scenario != "disabled", ExecutionDatabaseConfigured = scenario != "missing-connection",
            ManualCommitmentPolicies = [policy],
            ApprovedManualDispatchPolicyReference = "TEST-APPROVED-DISPATCH", AllowedDeliveryChannels = ["SecureEmail"],
        };
        switch (scenario)
        {
            case "absent-policy": options.ManualCommitmentPolicies = []; break;
            case "duplicate-policy": options.ManualCommitmentPolicies = [policy, policy]; break;
            case "future-policy": policy.EffectiveFrom = now.AddDays(1); break;
            case "expired-policy": policy.EffectiveUntil = now.AddDays(-1); break;
            case "other-company": policy.LegalEntityId = Guid.NewGuid(); break;
            case "unapproved-policy": policy.ApprovedPolicyReference = " "; break;
            case "disabled-policy": policy.Enabled = false; break;
            case "absent-delivery-policy": options.ApprovedManualDispatchPolicyReference = ""; break;
            case "absent-delivery-channels": options.AllowedDeliveryChannels = []; break;
            case "invalid-freshness": policy.EvidenceMaximumAgeMinutes = 0; break;
        }
        var readiness = new Readiness(scenario != "schema-not-ready");
        var scope = new ProcurementRequestScope();
        scope.Set("capability-test", company, Guid.NewGuid().ToString("D"), null, null, false, "test");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(scope);
        builder.Services.AddSingleton<IProcurementScope>(scope);
        builder.Services.AddDbContext<ProcurementDbContext>(o => { o.UseSqlite(connection); SyntheticProcurementMetadataModel.Configure(o, scenario); });
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(new ProcurementApprovalExecutionOptions());
        builder.Services.AddSingleton<IProcurementOrderSchemaReadiness>(readiness);
        builder.Services.AddSingleton(new ProcurementReceivingExecutionOptions());
        builder.Services.AddSingleton(new ProcurementSourcingDraftExecutionOptions());
        builder.Services.AddSingleton<IProcurementFinanceCheckerSchemaReadiness>(new FinanceCheckerTestSchemaReadiness { Ready = scenario != "finance-checker-schema-not-ready" });
        builder.Services.AddSingleton(new ProcurementInvoiceMatchingExecutionOptions());
        builder.Services.AddSingleton(new ProcurementManualPaymentOptions()); // Explicitly off in this metadata-only fixture.
        builder.Services.AddSingleton(new CoreDocumentFeature(false));
        builder.Services.AddAuthentication("capability-test")
            .AddScheme<AuthenticationSchemeOptions, Authentication>("capability-test", _ => { });
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        ProcurementHostIntegration.MapMetadataRoute(app);
        using (var services = app.Services.CreateScope())
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            // Only migration history is needed by metadata. This is an availability unit fixture,
            // deliberately not a claim that PostgreSQL business tables/migrations were tested.
            var history = db.GetService<IHistoryRepository>();
            await SyntheticProcurementMetadataModel.CreateSqliteHistoryAsync(db);
            var migrations = db.Database.GetMigrations().ToArray();
            Assert.NotEmpty(migrations);
            foreach (var migration in scenario == "pending-procurement-schema" ? migrations.Skip(1) : migrations)
                await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(migration, "10.0.11")));
        }
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/procurement/v1/meta");
        if (scenario == "unknown-model")
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(0, readiness.Calls);
            return;
        }
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expected, body.GetProperty("capabilities").GetProperty("orders").GetBoolean());
        Assert.False(body.GetProperty("capabilities").GetProperty("manualPaymentWorkflow").GetBoolean());
        Assert.False(body.GetProperty("capabilities").GetProperty("principalInvoiceComparisonRead").GetBoolean());
        Assert.False(body.GetProperty("capabilities").GetProperty("principalInvoiceComparison").GetBoolean());
        Assert.Equal(scenario is not ("pending-procurement-schema" or "pending-model"), body.GetProperty("schemaReady").GetBoolean());
        Assert.Equal(expected || scenario == "schema-not-ready" ? 1 : 0, readiness.Calls);
    }

    [Theory]
    [InlineData("ready", true, 1)]
    [InlineData("draft-disabled", false, 0)]
    [InlineData("draft-unconfigured", false, 0)]
    [InlineData("approval-disabled", false, 0)]
    [InlineData("approval-principal-disabled", false, 0)]
    [InlineData("approval-unconfigured", false, 0)]
    [InlineData("readiness-false", false, 1)]
    [InlineData("department-scope", false, 0)]
    [InlineData("pending-schema", false, 0)]
    public async Task Principal_direct_drafts_metadata_matches_route_admission(
        string scenario, bool expected, int readinessCalls)
    {
        var scope = new ProcurementRequestScope();
        scope.Set("capability-test", Guid.NewGuid(), Guid.NewGuid().ToString("D"), null,
            scenario == "department-scope" ? Guid.NewGuid() : null, false, "test");
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(scope);
        builder.Services.AddSingleton<IProcurementScope>(scope);
        builder.Services.AddDbContext<ProcurementDbContext>(o =>
        {
            o.UseSqlite(connection);
            SyntheticProcurementMetadataModel.Configure(o, "principal-direct-capability");
        });
        builder.Services.AddSingleton(new ProcurementPrincipalOrderDraftOptions
        {
            Enabled = scenario != "draft-disabled",
            ExecutionDatabaseConfigured = scenario != "draft-unconfigured",
        });
        builder.Services.AddSingleton(new ProcurementApprovalExecutionOptions
        {
            Enabled = scenario != "approval-disabled",
            PrincipalExecutionEnabled = scenario != "approval-principal-disabled",
            ExecutionDatabaseConfigured = scenario != "approval-unconfigured",
        });
        var readiness = new PrincipalDraftReadiness(scenario != "readiness-false");
        builder.Services.AddSingleton<IProcurementPrincipalOrderDraftReadiness>(readiness);
        builder.Services.AddSingleton(new ProcurementOrderExecutionOptions());
        builder.Services.AddSingleton(new ProcurementReceivingExecutionOptions());
        builder.Services.AddSingleton(new ProcurementSourcingDraftExecutionOptions());
        builder.Services.AddSingleton<IProcurementFinanceCheckerSchemaReadiness>(new FinanceCheckerTestSchemaReadiness());
        builder.Services.AddSingleton(new ProcurementInvoiceMatchingExecutionOptions());
        builder.Services.AddSingleton(new ProcurementManualPaymentOptions());
        builder.Services.AddSingleton(new CoreDocumentFeature(false));
        builder.Services.AddAuthentication("capability-test")
            .AddScheme<AuthenticationSchemeOptions, Authentication>("capability-test", _ => { });
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        ProcurementHostIntegration.MapMetadataRoute(app);
        using (var services = app.Services.CreateScope())
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var history = db.GetService<IHistoryRepository>();
            await SyntheticProcurementMetadataModel.CreateSqliteHistoryAsync(db);
            var migrations = db.Database.GetMigrations().ToArray();
            Assert.NotEmpty(migrations);
            foreach (var migration in scenario == "pending-schema" ? migrations.Skip(1) : migrations)
                await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(migration, "10.0.11")));
        }
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/procurement/v1/meta");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expected, body.GetProperty("capabilities").GetProperty("principalDirectOrderDrafts").GetBoolean());
        Assert.Equal(readinessCalls, readiness.Calls);
    }

    private sealed class PrincipalDraftReadiness(bool ready) : IProcurementPrincipalOrderDraftReadiness
    {
        public int Calls { get; private set; }
        public Task<bool> IsReadyAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(ready);
        }
        public Task<bool> IsReadyAsync(ProcurementDbContext owner, bool write, CancellationToken ct) =>
            Task.FromResult(ready);
    }

    private sealed class Readiness(bool ready) : IProcurementOrderSchemaReadiness
    {
        public int Calls { get; private set; }
        public Task<bool> IsReadyAsync(CancellationToken ct) { Calls++; return Task.FromResult(ready); }
    }

    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim("sub", "fixture"), new Claim("tenant", "capability-test"), new Claim(ClaimTypes.Role, "erp_admin"),
            }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
