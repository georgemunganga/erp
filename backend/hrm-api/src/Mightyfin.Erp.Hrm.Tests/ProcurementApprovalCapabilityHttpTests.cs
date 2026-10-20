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
public sealed class ProcurementApprovalCapabilityHttpTests
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
    [InlineData("pending-procurement-schema", false)]
    [InlineData("configured", true)]
    public async Task Mapped_metadata_exposes_only_configured_current_available_approvals(string scenario, bool expected)
    {
        var company = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var policy = new ProcurementManualFinancePolicy
        {
            Enabled = true, LegalEntityId = company, ApprovedPolicyReference = "TEST-INDEPENDENTLY-APPROVED",
            EffectiveFrom = now.AddDays(-1), EffectiveUntil = now.AddDays(1),
        };
        var options = new ProcurementApprovalExecutionOptions
        {
            Enabled = scenario != "disabled", ExecutionDatabaseConfigured = scenario != "missing-connection",
            ManualFinancePolicies = [policy],
        };
        switch (scenario)
        {
            case "absent-policy": options.ManualFinancePolicies = []; break;
            case "duplicate-policy": options.ManualFinancePolicies = [policy, policy]; break;
            case "future-policy": policy.EffectiveFrom = now.AddDays(1); break;
            case "expired-policy": policy.EffectiveUntil = now.AddDays(-1); break;
            case "other-company": policy.LegalEntityId = Guid.NewGuid(); break;
            case "unapproved-policy": policy.ApprovedPolicyReference = " "; break;
            case "disabled-policy": policy.Enabled = false; break;
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
        builder.Services.AddDbContext<ProcurementDbContext>(o => o.UseSqlite(connection));
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(new ProcurementOrderExecutionOptions());
        builder.Services.AddSingleton<IProcurementApprovalSchemaReadiness>(readiness);
        builder.Services.AddSingleton(new ProcurementReceivingExecutionOptions());
        builder.Services.AddSingleton(new ProcurementInvoiceMatchingExecutionOptions());
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
            await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());
            var migrations = db.Database.GetMigrations().ToArray();
            Assert.NotEmpty(migrations);
            foreach (var migration in scenario == "pending-procurement-schema" ? migrations.Skip(1) : migrations)
                await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(migration, "10.0.11")));
        }
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/procurement/v1/meta");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expected, body.GetProperty("capabilities").GetProperty("approvals").GetBoolean());
        Assert.Equal(scenario != "pending-procurement-schema", body.GetProperty("schemaReady").GetBoolean());
        Assert.Equal(expected || scenario == "schema-not-ready" ? 1 : 0, readiness.Calls);
    }

    private sealed class Readiness(bool ready) : IProcurementApprovalSchemaReadiness
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
