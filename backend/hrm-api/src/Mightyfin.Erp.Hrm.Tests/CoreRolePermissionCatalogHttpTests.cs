using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreRolePermissionCatalogHttpTests
{
    [Fact]
    public async Task Admin_read_catalog_is_exactly_current_policy_keys_and_does_not_disclose_role_assignments()
    {
        await using var fixture = await Fixture.Start();
        using var response = await fixture.Get("hr_admin");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? "");
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, body.GetProperty("schemaVersion").GetInt32());
        var actions = body.GetProperty("procurement").EnumerateArray().ToArray();
        var names = actions.Select(x => x.GetProperty("key").GetString()!).ToArray();
        Assert.Equal(93, names.Length);
        Assert.Equal(ProcurementActionCatalog.BootstrapGrants.Keys.Order(StringComparer.Ordinal), names);
        Assert.All(actions, x =>
        {
            Assert.False(string.IsNullOrWhiteSpace(x.GetProperty("group").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(x.GetProperty("label").GetString()));
        });
        Assert.Equal("Export vendors", actions.Single(x => x.GetProperty("key").GetString() == "procurement-vendor-export")
            .GetProperty("label").GetString());
        Assert.Equal("Export items", actions.Single(x => x.GetProperty("key").GetString() == "procurement-item-export")
            .GetProperty("label").GetString());
        Assert.DoesNotContain("permissionsCsv", body.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("roleKey", body.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Get("erp_admin")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_and_non_admin_cannot_read_role_catalog()
    {
        await using var fixture = await Fixture.Start();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Get(null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Get("procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Get("hr_ops")).StatusCode);
    }

    private sealed class Fixture(WebApplication app) : IAsyncDisposable
    {
        private readonly HttpClient client = app.GetTestClient();

        internal async Task<HttpResponseMessage> Get(string? role)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/core/v1/role-permissions");
            if (role is not null) request.Headers.Add("X-Test-Role", role);
            return await client.SendAsync(request);
        }

        internal static async Task<Fixture> Start()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
            builder.Services.AddAuthentication("role-catalog-test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("role-catalog-test", _ => { });
            builder.Services.AddAuthorization(options => options.AddPolicy("hrm-admin", policy => policy
                .RequireAuthenticatedUser().RequireAssertion(context =>
                    WorkerPrincipal.FromClaims(context.User.Claims).IsRole("hr_admin", "erp_admin"))));
            var app = builder.Build();
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            CoreRolePermissionCatalogRoutes.MapRoutes(app);
            await app.StartAsync();
            return new(app);
        }

        public async ValueTask DisposeAsync() { client.Dispose(); await app.DisposeAsync(); }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            if (string.IsNullOrWhiteSpace(role)) return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "role-catalog-test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "role-catalog-test")));
        }
    }
}
