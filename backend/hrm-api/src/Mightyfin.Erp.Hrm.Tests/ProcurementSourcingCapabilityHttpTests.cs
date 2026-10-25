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
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Mapped availability contract only; fake ready schema is test-only, not a runtime provider.</summary>
public sealed class ProcurementSourcingCapabilityHttpTests
{
    [Fact]
    public async Task Production_readiness_and_options_cannot_advertise_an_absent_RFQ_adapter()
    {
        Assert.False(new ProcurementSourcingDraftExecutionOptions().Enabled);
        Assert.False(new ProcurementSourcingDraftExecutionOptions().PrincipalSourceEnabled);
        Assert.False(new ProcurementSourcingDraftExecutionOptions().ExecutionDatabaseConfigured);
        Assert.False(await new UnavailableProcurementSourcingDraftSchemaReadiness().IsReadyAsync(default));
    }

    [Theory]
    [InlineData(false, true, true, false, false, 0)]
    [InlineData(true, false, true, false, false, 0)]
    [InlineData(true, true, false, false, false, 1)]
    [InlineData(true, true, true, true, false, 0)]
    [InlineData(true, true, true, false, true, 1)]
    public async Task Governed_capability_requires_all_technical_gates_without_removing_legacy_drafts(
        bool enabled, bool configured, bool ready, bool pending, bool expected, int calls)
    {
        var scope = new ProcurementRequestScope();
        var principal = Guid.NewGuid();
        scope.Set("capability-test", Guid.NewGuid(), principal.ToString("D"), null, null, false, "test");
        scope.AssignPrincipal(principal);
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(scope); builder.Services.AddSingleton<IProcurementScope>(scope);
        builder.Services.AddDbContext<ProcurementDbContext>(o => { o.UseSqlite(connection); SyntheticProcurementMetadataModel.Configure(o, "synthetic-history-only"); });
        builder.Services.AddSingleton(new ProcurementSourcingDraftExecutionOptions { Enabled = enabled,
            PrincipalSourceEnabled = true, ExecutionDatabaseConfigured = configured });
        var readiness = new Readiness(ready);
        builder.Services.AddSingleton<IProcurementSourcingDraftSchemaReadiness>(readiness);
        builder.Services.AddSingleton(new ProcurementApprovalExecutionOptions { Enabled = true,
            PrincipalExecutionEnabled = true, ExecutionDatabaseConfigured = true });
        builder.Services.AddScoped(_ => new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>()
            .UseSqlite(connection).Options, new TestTenant()));
        builder.Services.AddSingleton(new ProcurementOrderExecutionOptions());
        builder.Services.AddSingleton(new ProcurementReceivingExecutionOptions());
        builder.Services.AddSingleton(new ProcurementInvoiceMatchingExecutionOptions());
        builder.Services.AddSingleton(new ProcurementManualPaymentOptions()); // Explicitly off in this metadata-only fixture.
        builder.Services.AddSingleton(new CoreDocumentFeature(false));
        builder.Services.AddAuthentication("capability-test").AddScheme<AuthenticationSchemeOptions, Authentication>("capability-test", _ => { });
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
        ProcurementHostIntegration.MapMetadataRoute(app);
        using (var services = app.Services.CreateScope())
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            // Metadata-only fixture: no claim of PostgreSQL schema or real workflow execution.
            var history = db.GetService<IHistoryRepository>();
            await SyntheticProcurementMetadataModel.CreateSqliteHistoryAsync(db);
            var migrations = db.Database.GetMigrations().ToArray(); Assert.NotEmpty(migrations);
            foreach (var migration in pending ? migrations.Skip(1) : migrations)
                await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(migration, "10.0.11")));
        }
        await app.StartAsync(); using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/procurement/v1/meta"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(); var capabilities = body.GetProperty("capabilities");
        Assert.Equal(expected, capabilities.GetProperty("sourcingSourceDrafts").GetBoolean());
        // An explicit principal flag and an available RFQ fake cannot replace the
        // absent actual P25 owning-store inspector. No Core account is inferred.
        Assert.False(capabilities.GetProperty("principalSourcingSourceDrafts").GetBoolean());
        // P36 cannot inherit P30 publication or source-draft readiness.
        Assert.False(capabilities.GetProperty("principalRfqRecommendationDrafts").GetBoolean());
        Assert.Equal(!pending, capabilities.GetProperty("sourcingDraft").GetBoolean());
        // Rates use the already-owned ContractRate table. They follow the same
        // pending-migration fence as the existing contract draft API.
        Assert.Equal(!pending, capabilities.GetProperty("contractDraft").GetBoolean());
        Assert.Equal(!pending, capabilities.GetProperty("contractDraftRates").GetBoolean());
        Assert.False(capabilities.GetProperty("sourcing").GetBoolean());
        Assert.False(capabilities.GetProperty("orders").GetBoolean());
        Assert.False(capabilities.GetProperty("manualPaymentWorkflow").GetBoolean());
        Assert.Equal(calls, readiness.Calls);
    }

    [Fact]
    public void Expired_or_missing_manual_finance_policy_does_not_hide_retained_principal_RFQ_drafts()
    {
        var sourcing = new ProcurementSourcingDraftExecutionOptions { Enabled = true,
            PrincipalSourceEnabled = true, ExecutionDatabaseConfigured = true };
        var approvals = new ProcurementApprovalExecutionOptions { Enabled = true,
            PrincipalExecutionEnabled = true, ExecutionDatabaseConfigured = true };
        Assert.False(approvals.HasCurrentManualEvidencePolicy(Guid.NewGuid(), DateTimeOffset.UtcNow));
        // The true argument represents separately established actual P25 physical
        // readiness. New Create still rechecks its original/current Finance proof.
        Assert.True(ProcurementPrincipalSourcingCapability.Prerequisites(false,sourcing,approvals,true));
        Assert.False(ProcurementPrincipalSourcingCapability.Prerequisites(false,sourcing,approvals,false));
    }

    [Theory]
    [InlineData(false, true, true, true, true, true, true, false)]
    [InlineData(true, false, true, true, true, true, true, false)]
    [InlineData(true, true, false, true, true, true, true, false)]
    [InlineData(true, true, true, false, true, true, true, false)]
    [InlineData(true, true, true, true, false, true, true, false)]
    [InlineData(true, true, true, true, true, false, true, false)]
    [InlineData(true, true, true, true, true, true, false, false)]
    [InlineData(true, true, true, true, true, true, true, true)]
    public void Principal_source_capability_requires_all_explicit_execution_dependencies(
        bool sourceEnabled, bool principalEnabled, bool sourceConfigured, bool approvalEnabled,
        bool approvalPrincipalEnabled, bool approvalConfigured, bool p25Available, bool expected)
    {
        var sourcing = new ProcurementSourcingDraftExecutionOptions { Enabled = sourceEnabled,
            PrincipalSourceEnabled = principalEnabled, ExecutionDatabaseConfigured = sourceConfigured };
        var approvals = new ProcurementApprovalExecutionOptions { Enabled = approvalEnabled,
            PrincipalExecutionEnabled = approvalPrincipalEnabled, ExecutionDatabaseConfigured = approvalConfigured };
        Assert.Equal(expected, ProcurementPrincipalSourcingCapability.Prerequisites(false,sourcing,approvals,p25Available));
        Assert.False(ProcurementPrincipalSourcingCapability.Prerequisites(true,sourcing,approvals,p25Available));
    }

    private sealed class Readiness(bool ready) : IProcurementSourcingDraftSchemaReadiness
    {
        public int Calls { get; private set; }
        public Task<bool> IsReadyAsync(CancellationToken ct) { Calls++; return Task.FromResult(ready); }
    }

    private sealed class TestTenant : ITenantAccessor
    {
        public string GetTenantId() => "capability-test";
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
