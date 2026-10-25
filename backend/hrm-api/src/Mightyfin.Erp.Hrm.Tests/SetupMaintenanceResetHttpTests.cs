using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Setup;
using Mightyfin.Erp.Hrm.Domain.Entities;
using HrmRoutes = Mightyfin.Erp.Hrm.Api.Routing.Routes;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class SetupMaintenanceResetHttpTests
{
    [Fact]
    public async Task ConfirmedUnconfinedAdministratorCannotResetOrReachRepository()
    {
        await using var fixture = await Fixture.Start();
        using var response = await fixture.Post("hr_admin", "RESET");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("setup-maintenance-required", body.GetProperty("code").GetString());
        Assert.Equal(0, fixture.Repository.Calls);
    }

    [Fact]
    public async Task MissingConfirmationAndNonAdministratorStillDenyBeforeService()
    {
        await using var fixture = await Fixture.Start();
        using var missing = await fixture.Post("hr_admin", "OTHER");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        using var nonAdmin = await fixture.Post("employee", "RESET");
        Assert.Equal(HttpStatusCode.Forbidden, nonAdmin.StatusCode);
        Assert.Equal(0, fixture.Repository.Calls);
    }

    private sealed class Fixture(WebApplication app, RepositoryTrap repository) : IAsyncDisposable
    {
        private readonly HttpClient client = app.GetTestClient();
        public RepositoryTrap Repository => repository;

        public async Task<HttpResponseMessage> Post(string role, string confirmation)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/hrm/setup/reset")
            { Content = JsonContent.Create(new { confirm = confirmation }) };
            request.Headers.Add("X-Test-Role", role);
            return await client.SendAsync(request);
        }

        public static async Task<Fixture> Start()
        {
            var repository = new RepositoryTrap();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
            builder.Services.AddAuthentication("setup-reset-test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("setup-reset-test", _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddScoped(_ => new ShellContext());
            builder.Services.AddSingleton<ISetupRepository>(repository);
            builder.Services.AddScoped<ISetupService, SetupServiceImpl>();
            var app = builder.Build();
            app.Use(async (context, next) =>
            {
                try { await next(context); }
                catch (DomainException error)
                {
                    context.Response.StatusCode = error.Code switch
                    {
                        "setup-maintenance-required" => 503,
                        "forbidden" => 403,
                        _ => 422
                    };
                    await context.Response.WriteAsJsonAsync(new { code = error.Code });
                }
            });
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            HrmRoutes.RegisterSetup(app);
            await app.StartAsync();
            return new(app, repository);
        }

        public async ValueTask DisposeAsync() { client.Dispose(); await app.DisposeAsync(); }
    }

    public sealed class RepositoryTrap : ISetupRepository
    {
        public int Calls { get; private set; }
        private T Unexpected<T>() { Calls++; throw new InvalidOperationException("Reset reached setup persistence."); }
        public Task<SetupState?> GetStateAsync(CancellationToken ct) => Unexpected<Task<SetupState?>>();
        public Task<IReadOnlySet<string>> CompletedStepKeysAsync(CancellationToken ct) => Unexpected<Task<IReadOnlySet<string>>>();
        public Task<string?> DataForStepAsync(string stepKey, CancellationToken ct) => Unexpected<Task<string?>>();
        public Task CompleteStepAsync(string stepKey, string? dataJson, CancellationToken ct) => Unexpected<Task>();
        public Task FinishAsync(SetupState state, CancellationToken ct) => Unexpected<Task>();
        public Task WipeAllDataAsync(CancellationToken ct) => Unexpected<Task>();
        public Task<SetupState> SeedPendingStateAsync(CancellationToken ct) => Unexpected<Task<SetupState>>();
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            if (string.IsNullOrWhiteSpace(role)) return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "setup-reset-test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "setup-reset-test")));
        }
    }
}
