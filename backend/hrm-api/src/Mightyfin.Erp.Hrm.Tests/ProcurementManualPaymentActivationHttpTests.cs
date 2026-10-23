using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Negative transport fault injection only. This does not certify normal login,
/// positive schema readiness or an actual PostgreSQL payment workflow.</summary>
public sealed class ProcurementManualPaymentActivationHttpTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Unactivated_routes_cannot_reach_readiness_or_store_even_with_a_ready_stub(bool enabled, bool configured)
    {
        var readiness = new ReadyCounter();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization(options =>
        {
            foreach (var action in new[] { "read", "create", "edit", "submit", "approve", "record", "confirm", "cancel" })
                options.AddPolicy("procurement-payment-" + action, policy => policy.RequireAuthenticatedUser());
        });
        builder.Services.AddSingleton(new ProcurementManualPaymentOptions { Enabled = enabled, ExecutionDatabaseConfigured = configured });
        builder.Services.AddSingleton<IProcurementManualPaymentGovernanceReadiness>(readiness);
        builder.Services.AddScoped(_ =>
        {
            var scope = new ProcurementRequestScope();
            var principal = Guid.NewGuid();
            scope.Set("negative-payment-activation", Guid.NewGuid(), principal.ToString("D"), null, null, false, "negative-route-test");
            scope.AssignPrincipal(principal);
            return scope;
        });
        builder.Services.AddScoped(services =>
        {
            var scope = services.GetRequiredService<ProcurementRequestScope>();
            // A deliberately unreachable test address. The activation guard must stop
            // before opening it, regardless of this readiness stub's answer.
            return new ProcurementApprovalTransaction("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1", scope, scope);
        });
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "negative-test") }, "negative-test"));
            await next(context);
        });
        app.UseAuthentication(); app.UseAuthorization();
        ProcurementManualPaymentRoutes.MapReadRoutes(app);
        ProcurementManualPaymentCommandRoutes.MapRoutes(app);
        await app.StartAsync();
        using var client = app.GetTestClient();
        var id = Guid.NewGuid(); var key = Guid.NewGuid();
        foreach (var (method, path) in new[] {
            (HttpMethod.Get, "/api/procurement/v1/manual-payments"),
            (HttpMethod.Get, $"/api/procurement/v1/manual-payments/{id}"),
            (HttpMethod.Get, $"/api/procurement/v1/manual-payments/commands/{key}"),
            (HttpMethod.Get, $"/api/procurement/v1/manual-payments/lookups/assignees?role=approve&branchId={id}"),
            (HttpMethod.Get, $"/api/procurement/v1/manual-payments/lookups/invoices?branchId={id}"),
            (HttpMethod.Get, "/api/procurement/v1/manual-payments/lookups/branches"),
            (HttpMethod.Post, "/api/procurement/v1/manual-payments"),
            (HttpMethod.Put, $"/api/procurement/v1/manual-payments/{id}"),
            (HttpMethod.Post, $"/api/procurement/v1/manual-payments/{id}/submit") })
        {
            using var request = new HttpRequestMessage(method, path);
            if (method != HttpMethod.Get) request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? "");
        }
        Assert.Equal(0, readiness.Calls);
    }

    private sealed class ReadyCounter : IProcurementManualPaymentGovernanceReadiness
    {
        internal int Calls { get; private set; }
        public Task<bool> IsReadyAsync(CancellationToken ct) { Calls++; return Task.FromResult(true); }
    }
}
