using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Api;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Actual mapped gate only. Deliberately registers no database/coordinator/Core store,
/// and never supplies ready schema or fake successful business responses.</summary>
public sealed class ProcurementSourcingExecutionGateHttpTests
{
    [Fact]
    public async Task Principal_publication_paths_fail_closed_before_database_resolution()
    {
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
        var scope = new ProcurementRequestScope();
        scope.Set("rfq-publication-gate", Guid.NewGuid(), Guid.NewGuid().ToString("D"), null, null, false, "gate");
        builder.Services.AddSingleton(scope);
        builder.Services.AddSingleton(new ProcurementPrincipalSourcingPublicationOptions());
        builder.Services.AddSingleton(new ProcurementSourcingDraftExecutionOptions());
        builder.Services.AddSingleton<IProcurementSourcingDraftSchemaReadiness,
            UnavailableProcurementSourcingDraftSchemaReadiness>();
        builder.Services.AddAuthentication("rfq-gate").AddScheme<AuthenticationSchemeOptions, Authentication>("rfq-gate", _ => { });
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
        ProcurementSourcingExecutionRoutes.MapRoutes(app); await app.StartAsync();
        using var client = app.GetTestClient(); var id = Guid.NewGuid();
        foreach (var path in new[] { "principal-publications", $"{id}/principal-publication",
            $"principal-publication-commands/{Guid.NewGuid()}" })
        {
            using var response = await client.GetAsync("/api/procurement/v1/sourcing/" + path);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("SourcingDraftExecutionUnavailable",
                (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
        using var open = await client.PostAsJsonAsync($"/api/procurement/v1/sourcing/{id}/principal-publication-reviews",
            new { expectedEventVersion = 1, originalDraftFingerprint = new string('a', 64), supplierIds = new[] { Guid.NewGuid() } });
        using var assigned = await client.GetAsync("/api/procurement/v1/sourcing/principal-publication-reviews/assigned");
        Assert.Equal(HttpStatusCode.Forbidden, open.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, assigned.StatusCode);
        // No Core, Record, or Approval store is registered in this host.
    }

    [Theory]
    [InlineData(false, true)][InlineData(true, false)][InlineData(true, true)]
    public async Task Every_governed_path_remains_unavailable_without_real_schema_before_any_store_resolution(bool enabled, bool configured)
    {
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
        var scope = new ProcurementRequestScope();
        scope.Set("rfq-gate", Guid.NewGuid(), Guid.NewGuid().ToString("D"), null, null, false, "gate");
        builder.Services.AddSingleton(scope);
        builder.Services.AddSingleton(new ProcurementSourcingDraftExecutionOptions { Enabled = enabled, ExecutionDatabaseConfigured = configured });
        builder.Services.AddSingleton<IProcurementSourcingDraftSchemaReadiness, UnavailableProcurementSourcingDraftSchemaReadiness>();
        builder.Services.AddAuthentication("rfq-gate").AddScheme<AuthenticationSchemeOptions, Authentication>("rfq-gate", _ => { });
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
        ProcurementSourcingExecutionRoutes.MapRoutes(app); await app.StartAsync();
        using var client = app.GetTestClient(); var id = Guid.NewGuid();
        var responses = new[] {
            await client.GetAsync("/api/procurement/v1/sourcing/approved-sources"),
            await client.GetAsync("/api/procurement/v1/sourcing/source-drafts"),
            await client.PostAsJsonAsync("/api/procurement/v1/sourcing/from-approved-sources", new {
                title = "Draft", sources = new[] { new { purchaseRequestId = Guid.NewGuid(), purchaseRequestLineId = Guid.NewGuid(), expectedRequestVersion = 1, quantity = "1.0001" } } }),
            await client.PostAsJsonAsync($"/api/procurement/v1/sourcing/{id}/source-cancel", new { expectedEventVersion = 1, originalDraftFingerprint = new string('a',64), reason = "No longer needed" }),
            await client.GetAsync($"/api/procurement/v1/sourcing/{id}/source-draft"),
            await client.GetAsync($"/api/procurement/v1/sourcing/command-receipts/Create/{Guid.NewGuid()}"),
        };
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
                Assert.Equal("SourcingDraftExecutionUnavailable", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
                Assert.True(response.Headers.CacheControl?.NoStore);
            }
        }
    }
    [Theory]
    [InlineData("08006", HttpStatusCode.ServiceUnavailable)]
    [InlineData("40001", HttpStatusCode.Conflict)]
    [InlineData("40P01", HttpStatusCode.Conflict)]
    [InlineData("23505", HttpStatusCode.Conflict)]
    public async Task Wrapped_store_failures_escape_row_unavailable_and_fail_mapped_get_without_store_writes(
        string sqlState, HttpStatusCode expected)
    {
        // Shape emitted by execution strategies: outer InvalidOperationException is NOT
        // a domain eligibility exception merely because the wrapper has that type.
        var error = new InvalidOperationException("Strategy wrapper",
            new InvalidOperationException("Inner wrapper", new PostgresException("Synthetic store fault", "ERROR", "ERROR", sqlState)));
        Assert.False(ProcurementSourcingExecutionRoutes.IsDiscoveryEvidenceFailure(error));
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
        var scope = new ProcurementRequestScope();
        scope.Set("rfq-failure", Guid.NewGuid(), Guid.NewGuid().ToString("D"), null, null, false, "failure");
        builder.Services.AddSingleton(scope);
        builder.Services.AddSingleton(new ProcurementSourcingDraftExecutionOptions { Enabled = true, ExecutionDatabaseConfigured = true });
        var readiness = new FailingReadiness(error);
        builder.Services.AddSingleton<IProcurementSourcingDraftSchemaReadiness>(readiness);
        builder.Services.AddAuthentication("rfq-gate").AddScheme<AuthenticationSchemeOptions, Authentication>("rfq-gate", _ => { });
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
        ProcurementSourcingExecutionRoutes.MapRoutes(app); await app.StartAsync();
        using var client = app.GetTestClient();
        foreach (var path in new[] { "approved-sources", "source-drafts" })
        {
            using var response = await client.GetAsync("/api/procurement/v1/sourcing/" + path);
            Assert.Equal(expected, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(expected == HttpStatusCode.ServiceUnavailable ? "SourcingSaveUnconfirmed" : "SourcingEvidenceNeedsReview",
                body.GetProperty("code").GetString());
            Assert.False(body.TryGetProperty("items", out _));
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
        Assert.Equal(2, readiness.Attempts);
        // No DbContext/coordinator/store exists in this host: no writes can have occurred.
    }
    private sealed class FailingReadiness(Exception error) : IProcurementSourcingDraftSchemaReadiness
    {
        internal int Attempts { get; private set; }
        public Task<bool> IsReadyAsync(CancellationToken ct) { ++Attempts; return Task.FromException<bool>(error); }
    }
    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(new[] { new Claim("sub", "fixture"), new Claim("tenant", "rfq-gate"), new Claim(ClaimTypes.Role, "erp_admin") }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
