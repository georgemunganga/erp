using System.Net;
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
using Mightyfin.Erp.Core.Workflows.Application;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreWorkflowConfigurationHttpTests
{
    [Theory]
    [InlineData(null, false, HttpStatusCode.Unauthorized)]
    [InlineData("employee", true, HttpStatusCode.Forbidden)]
    [InlineData("procurement_officer", true, HttpStatusCode.Forbidden)]
    [InlineData("erp_admin", false, HttpStatusCode.Forbidden)]
    [InlineData("erp_admin", true, HttpStatusCode.OK)]
    [InlineData("hr_admin", true, HttpStatusCode.OK)]
    public async Task Shared_admin_and_tenant_guards_apply_without_employee_records(string? role, bool tenant, HttpStatusCode expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("workflow-test").AddScheme<AuthenticationSchemeOptions, TestAuth>("workflow-test", _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("hrm-admin", p => p.RequireAuthenticatedUser().RequireAssertion(c =>
                WorkerPrincipal.FromClaims(c.User.Claims).IsRole("hr_admin", "erp_admin")));
            CoreWorkspaceContextRoutes.AddPolicy(options);
        });
        builder.Services.AddSingleton<IWorkflowConfigurationProvider, ProcurementWorkflowConfigurationProvider>();
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); CoreWorkflowConfigurationRoutes.MapRoutes(app);
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/core/v1/workflow-configuration");
        if (role is not null) request.Headers.Add("X-Test-Role", role);
        if (tenant) request.Headers.Add("X-Test-Tenant", "workflow-fixture");
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(5, body.RootElement.GetProperty("items").GetArrayLength());
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            Assert.DoesNotContain("principalId", body.RootElement.ToString());
        }
    }

    private sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "11111111-1111-4111-8111-111111111111"), new(ClaimTypes.Role, role) };
            if (Request.Headers["X-Test-Tenant"].ToString() is { Length: > 0 } tenant) claims.Add(new("tenant", tenant));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, "workflow-test")), "workflow-test")));
        }
    }
}
