using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Actual mapped HTTP unavailability gates on an unsupported owner, not fake PostgreSQL/schema success.</summary>
public sealed class ProcurementOperationalPolicyLifecycleGateHttpTests
{
    private const string Path = "/api/procurement/v1/configuration/policies";
    [Theory] [InlineData("RECEIVING")] [InlineData("INVOICE_MATCHING")]
    public async Task Unavailable_owning_schema_blocks_every_operational_mutation_but_keeps_historical_reads(string key)
    {
        await using var f = await Fixture.Start(key); var before = await f.Footprint();
        var draft = new { policyKey = key, effectiveFrom = DateTimeOffset.UtcNow.AddDays(3), effectiveTo = (DateTimeOffset?)null, rulesJson = "{\"draft\":true}" };
        var attempts = new[]
        {
            (HttpMethod.Post, Path, (object)draft),
            (HttpMethod.Put, Path + "/" + f.Policy, new { version = 1, draft }),
            (HttpMethod.Post, Path + "/" + f.Policy + "/submit-review", new { version = 1, reason = "Review" }),
            (HttpMethod.Post, Path + "/" + f.Policy + "/publish", new { version = 1, reason = "Independent review" }),
            (HttpMethod.Post, Path + "/" + f.Policy + "/return-for-changes", new { version = 1, reason = "Revise" }),
        };
        foreach (var (method, url, body) in attempts)
        {
            var response = await f.Send(method, url, body);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("operational-policy-identity-unavailable", error.GetProperty("code").GetString());
            Assert.Equal(before, await f.Footprint());
        }
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await f.Send(HttpMethod.Get, Path + "/effective?key=" + key)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Send(HttpMethod.Get, Path + "?key=" + key)).StatusCode);
        var detail = await f.Send(HttpMethod.Get, Path + "/" + f.Policy);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal("Published", (await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("state").GetString());
        Assert.Equal(before, await f.Footprint());
    }

    private sealed class Fixture(WebApplication app, Guid company, Guid user, Guid policy) : IAsyncDisposable
    {
        internal Guid Policy => policy;
        private HttpClient Client { get; } = app.GetTestClient();
        internal Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body = null)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Add("X-Test-User", user.ToString("D")); request.Headers.Add("X-Test-Role", "erp_admin");
            request.Headers.Add("X-Shell-Entity", company.ToString("D"));
            if (body is not null) request.Content = JsonContent.Create(body);
            return Client.SendAsync(request);
        }
        internal async Task<string> Footprint()
        {
            using var services = app.Services.CreateScope(); var scope = services.ServiceProvider.GetRequiredService<ProcurementRequestScope>();
            scope.Set("tenant-a", company, user.ToString("D"), null, null, false, "gate-footprint");
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return JsonSerializer.Serialize(new
            {
                policies = await db.PolicyVersions.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
                identities = await db.OperationalPolicyIdentityRecords.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
                audits = await db.AuditEvents.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
                outbox = await db.OutboxEvents.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            });
        }
        internal static async Task<Fixture> Start(string key)
        {
            var database = Guid.NewGuid().ToString(); var root = new InMemoryDatabaseRoot();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseTestServer(); builder.Services.AddScoped<ITenantAccessor>(_ => new FixedTenantAccessor("tenant-a"));
            builder.Services.AddDbContext<HrmDbContext>(x => x.UseInMemoryDatabase("op-policy-gate-core-" + database));
            builder.Services.AddScoped<ProcurementRequestScope>();
            builder.Services.AddScoped<IProcurementScope>(s => s.GetRequiredService<ProcurementRequestScope>());
            builder.Services.AddDbContext<ProcurementDbContext>(x => x.UseInMemoryDatabase(database, root));
            builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions,
                ProcurementRequestHttpTests.TestAuthenticationHandler>("test", _ => { });
            builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
            // Deliberately no schema-readiness registration/provider override.
            var app = builder.Build(); app.UseRouting(); app.UseAuthentication(); app.UseMiddleware<ProcurementCorePrincipalRefreshMiddleware>(); app.UseAuthorization();
            app.UseMiddleware<ProcurementScopeMiddleware>(); ProcurementPolicyDraftRoutes.MapRoutes(app);
            var company = new LegalEntity { Code = "OP", RegisteredName = "Operational policy gate", Currency = "ZMW" };
            var branch = new WorkLocation { Code = "HOME", Name = "Home", LegalEntityId = company.Id };
            var worker = new Worker { EmployeeNo = "OP", FirstName = "Policy", LastName = "Admin", LocationId = branch.Id, Status = "active" };
            var user = new LocalUser { WorkerId = worker.Id, RolesCsv = "erp_admin", IsActive = true,
                Email = "policy@example.test", NormalizedEmail = "POLICY@EXAMPLE.TEST", DisplayName = "Policy admin", PasswordHash = "synthetic-not-a-credential" };
            var policy = new PolicyVersion { TenantId = "tenant-a", LegalEntityId = company.Id, PolicyKey = key, PolicyVersionNumber = 1,
                State = "Published", RulesJson = "{\"legacyUnknown\":true}", Version = 1, CreatedBy = user.Id.ToString("D"),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1), EffectiveFrom = DateTimeOffset.UtcNow.AddHours(-1), PublishedBy = Guid.NewGuid().ToString("D"), PublishedAt = DateTimeOffset.UtcNow.AddHours(-2) };
            using (var services = app.Services.CreateScope())
            {
                var core = services.ServiceProvider.GetRequiredService<HrmDbContext>(); core.LegalEntities.Add(company);
                core.WorkLocations.Add(branch); core.Workers.Add(worker); core.LocalUsers.Add(user);
                core.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = "erp_admin",
                    RoleName = "Synthetic operational policy administrator", Active = true, PermissionsCsv = "erp_admin" });
                await core.SaveChangesAsync();
                var scope = services.ServiceProvider.GetRequiredService<ProcurementRequestScope>();
                scope.Set("tenant-a", company.Id, user.Id.ToString("D"), null, null, false, "legacy-gate-seed");
                var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
                // Owner-only pre-migration legacy fixture, never a successful reviewed policy/proof.
                await using var raw = new DbContext(new DbContextOptionsBuilder().UseInMemoryDatabase(database, root).UseModel(db.Model).Options);
                raw.Add(policy); await raw.SaveChangesAsync();
            }
            await app.StartAsync(); return new(app, company.Id, user.Id, policy.Id);
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await app.DisposeAsync(); }
    }
}
