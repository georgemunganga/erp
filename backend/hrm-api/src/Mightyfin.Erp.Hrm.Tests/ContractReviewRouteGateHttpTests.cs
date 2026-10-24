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
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Tests;

// Real production composition seam and mapped handlers/authorization. Authentication
// is synthetic. All unavailable paths are negative admission/fault injection only;
// none establishes positive physical readiness, current Core authority or a workflow.
public sealed class ContractReviewRouteGateHttpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task Feature_off_or_missing_options_leaves_all_review_routes_unmapped(bool? enabled)
    {
        await using var fixture = await Fixture.Create(enabled, true, new() { Enabled = true, PrincipalExecutionEnabled = true, ExecutionDatabaseConfigured = true });
        foreach (var endpoint in Endpoints())
        {
            using var response = await fixture.Send(endpoint, true);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        Assert.Equal(0, fixture.TransactionAttempts);
        Assert.Equal(0, fixture.ReadinessCalls);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public async Task Authorized_enabled_routes_with_missing_execution_gate_return_503_without_store_use(
        bool configured, bool approvals, bool principal, bool approvalConnection)
    {
        await using var fixture = await Fixture.Create(true, configured,
            new() { Enabled = approvals, PrincipalExecutionEnabled = principal, ExecutionDatabaseConfigured = approvalConnection });
        foreach (var endpoint in Endpoints())
        {
            using var response = await fixture.Send(endpoint, true);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("ContractReviewUnavailable", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? "");
        }
        Assert.Equal(0, fixture.TransactionAttempts);
        Assert.Equal(0, fixture.ReadinessCalls);
    }

    [Fact]
    public async Task Every_mapped_route_requires_its_explicit_permission_before_execution_or_readiness()
    {
        await using var fixture = await Fixture.Create(true, true,
            new() { Enabled = true, PrincipalExecutionEnabled = true, ExecutionDatabaseConfigured = true });
        foreach (var endpoint in Endpoints())
        {
            using var response = await fixture.Send(endpoint, false);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        Assert.Equal(0, fixture.TransactionAttempts);
        Assert.Equal(0, fixture.ReadinessCalls);
    }

    [Fact]
    public async Task Authorized_routes_report_unconfirmed_503_when_the_execution_session_cannot_be_established()
    {
        await using var fixture = await Fixture.Create(true, true,
            new() { Enabled = true, PrincipalExecutionEnabled = true, ExecutionDatabaseConfigured = true });
        foreach (var endpoint in Endpoints())
        {
            using var response = await fixture.Send(endpoint, true);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("ContractReviewSaveUnconfirmed", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            Assert.DoesNotContain("negative transport sentinel", await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(6, fixture.TransactionAttempts);
        Assert.Equal(0, fixture.ReadinessCalls);
    }

    private sealed record Endpoint(HttpMethod Method, string Path, string Permission, object? Input = null);
    private static Endpoint[] Endpoints()
    {
        var id = Guid.NewGuid(); var assignment = Guid.NewGuid(); var branch = Guid.NewGuid();
        return [new(HttpMethod.Post, $"/api/procurement/v1/contracts/{id}/internal-review/submit", "procurement-contract-submit", new ProcurementContractReviewRoutes.SubmitInput(1, branch)),
            new(HttpMethod.Post, $"/api/procurement/v1/contracts/{id}/internal-review/assignments/{assignment}/decide", "procurement-contract-review", new ProcurementContractReviewRoutes.DecisionInput(1, 1, 1, "Approve", "test")),
            new(HttpMethod.Post, $"/api/procurement/v1/contracts/{id}/internal-review/correct", "procurement-contract-submit", new ProcurementContractReviewRoutes.CorrectionInput(1, "test")),
            new(HttpMethod.Get, "/api/procurement/v1/contracts/internal-review/assigned", "procurement-contract-review-read"),
            new(HttpMethod.Get, $"/api/procurement/v1/contracts/{id}/internal-review/assignments/{assignment}", "procurement-contract-review-read"),
            new(HttpMethod.Get, $"/api/procurement/v1/contracts/internal-review/commands/{Guid.NewGuid()}", "procurement-access")];
    }

    private sealed class Fixture(WebApplication app) : IAsyncDisposable
    {
        private HttpClient? client;
        internal int TransactionAttempts;
        internal int ReadinessCalls;
        internal async Task<HttpResponseMessage> Send(Endpoint endpoint, bool authorized)
        {
            using var request = new HttpRequestMessage(endpoint.Method, endpoint.Path);
            request.Headers.Add("X-Test-Action", authorized ? endpoint.Permission : "none");
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
            if (endpoint.Input is not null) request.Content = JsonContent.Create(endpoint.Input);
            return await (client ??= app.GetTestClient()).SendAsync(request);
        }
        internal static async Task<Fixture> Create(bool? enabled, bool configured, ProcurementApprovalExecutionOptions approvals)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
            if (enabled is { } flag) builder.Services.AddSingleton(new ProcurementContractReviewOptions { Enabled = flag, ExecutionDatabaseConfigured = configured });
            builder.Services.AddSingleton(approvals);
            var scope = new ProcurementRequestScope();
            scope.Set("contract-gate", Guid.NewGuid(), Guid.NewGuid().ToString("D"), null, null, false, "test");
            builder.Services.AddSingleton(scope);
            Fixture? fixture = null;
            builder.Services.AddSingleton<IProcurementContractReviewSchemaReadiness>(new Readiness(() => fixture!.ReadinessCalls++));
            builder.Services.AddSingleton<ProcurementApprovalTransaction>(_ =>
            {
                fixture!.TransactionAttempts++;
                // An unavailable physical execution boundary, not a fake database or
                // ready callback. Production handler classifies the real provider exception.
                throw new NpgsqlException("negative transport sentinel");
            });
            builder.Services.AddAuthentication("contract-gate").AddScheme<AuthenticationSchemeOptions, Authentication>("contract-gate", _ => { });
            builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
            var app = builder.Build();
            fixture = new(app);
            app.UseAuthentication(); app.UseAuthorization();
            ProcurementHostIntegration.MapContractReviewRoutes(app);
            await app.StartAsync();
            return fixture;
        }
        public async ValueTask DisposeAsync() { client?.Dispose(); await app.DisposeAsync(); }
    }
    private sealed class Readiness(Action called) : IProcurementContractReviewSchemaReadiness
    {
        public Task<bool> IsReadReadyAsync(ProcurementDbContext owner, CancellationToken ct) { called(); return Task.FromResult(false); }
        public Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct) { called(); return Task.FromResult(false); }
    }
    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var action = Request.Headers["X-Test-Action"].ToString();
            var claims = new List<Claim> { new("sub", "fixture"), new("tenant", "contract-gate"), new(ProcurementPermissions.ConfiguredClaim, "true") };
            // The recovery route's access policy is denied too when no action was supplied.
            if (action != "none") { claims.Add(new(ProcurementPermissions.ActionClaim, "procurement-access")); claims.Add(new(ProcurementPermissions.ActionClaim, action)); }
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
}
