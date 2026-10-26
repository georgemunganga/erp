using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Current Core authority and fail-closed HTTP admission; real PG migration,
/// finite roles and successful commit are proved by a separate private cohort.</summary>
public sealed class ProcurementMasterImportHttpTests
{
    [Fact]
    public async Task Workerless_explicit_importer_needs_native_create_and_physical_readiness()
    {
        await using var fixture = await Fixture.Start();
        foreach (var (user, expected) in new[] {
            (fixture.ImportOnly, HttpStatusCode.Forbidden),
            (fixture.NativeOnly, HttpStatusCode.Forbidden),
            (fixture.LegacyAdmin, HttpStatusCode.Forbidden),
            (fixture.Both, HttpStatusCode.ServiceUnavailable) })
        {
            using var response = await fixture.Send(user, HttpMethod.Get, "/api/procurement/v1/vendors/import/schema");
            Assert.Equal(expected, response.StatusCode);
        }
        using (var all = await fixture.Send(fixture.Both, HttpMethod.Get,
                   "/api/procurement/v1/vendors/import/schema", allBranches: true))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, all.StatusCode);
        Assert.Equal((0, 0, 0), await fixture.MasterFootprint());
    }

    [Fact]
    public async Task Revoked_current_import_permission_and_unassigned_company_cannot_recover_or_preview()
    {
        await using var fixture = await Fixture.Start();
        using (var response = await fixture.Send(fixture.Both, HttpMethod.Get,
                   "/api/procurement/v1/items/import/" + Guid.NewGuid()))
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using (var foreign = await fixture.Send(fixture.Both, HttpMethod.Get,
                   "/api/procurement/v1/vendors/import/schema", foreignCompany: true))
            Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        await fixture.RevokeImport();
        foreach (var path in new[] { "/api/procurement/v1/vendors/import/schema",
                     "/api/procurement/v1/vendors/import/preview",
                     "/api/procurement/v1/items/import/" + Guid.NewGuid() })
        {
            using var response = await fixture.Send(fixture.Both,
                path.EndsWith("/preview", StringComparison.Ordinal) ? HttpMethod.Post : HttpMethod.Get,
                path);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        Assert.Equal((0, 0, 0), await fixture.MasterFootprint());
    }

    [Fact]
    public async Task Current_company_and_selected_branch_are_rechecked_after_a_wait()
    {
        var options = new DbContextOptionsBuilder<HrmDbContext>()
            .UseInMemoryDatabase("import-current-scope-" + Guid.NewGuid()).Options;
        await using var core = new HrmDbContext(options, new FixedTenantAccessor("tenant-a"));
        var company = new LegalEntity { Code = "A", RegisteredName = "Current A", Currency = "ZMW" };
        var branch = new WorkLocation { Code = "A1", Name = "Selected", LegalEntityId = company.Id };
        var remaining = new WorkLocation { Code = "A2", Name = "Remaining", LegalEntityId = company.Id };
        var actor = Guid.NewGuid();
        var selected = new HrUserBranchAssignment { UserId = actor, LocationId = branch.Id };
        var other = new HrUserBranchAssignment { UserId = actor, LocationId = remaining.Id };
        core.LegalEntities.Add(company); core.WorkLocations.AddRange(branch, remaining);
        core.UserBranchAssignments.AddRange(selected, other);
        await core.SaveChangesAsync();
        var scope = new ProcurementRequestScope();
        scope.Set("tenant-a", company.Id, actor.ToString("D"), branch.Id, null, true, "test");
        scope.AssignPrincipal(actor);
        Assert.True(await ProcurementMasterImportRoutes.CurrentScopeAllowed(core, scope, CancellationToken.None));
        selected.IsArchived = true;
        await core.SaveChangesAsync();
        Assert.False(await ProcurementMasterImportRoutes.CurrentScopeAllowed(core, scope, CancellationToken.None));
        company.IsArchived = true;
        await core.SaveChangesAsync();
        Assert.False(await ProcurementMasterImportRoutes.CurrentScopeAllowed(core, scope, CancellationToken.None));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly WebApplication app;
        private HttpClient? client;
        private readonly Guid company;
        private readonly Guid otherCompany;
        private readonly Guid branch;
        public Guid Both { get; } = Guid.NewGuid();
        public Guid ImportOnly { get; } = Guid.NewGuid();
        public Guid NativeOnly { get; } = Guid.NewGuid();
        public Guid LegacyAdmin { get; } = Guid.NewGuid();

        private Fixture(WebApplication app, Guid company, Guid otherCompany, Guid branch)
        {
            this.app = app;
            this.company = company; this.otherCompany = otherCompany;
            this.branch = branch;
        }

        public static async Task<Fixture> Start()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
            builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Procurement:MasterImport:Enabled"] = "true" });
            var store = Guid.NewGuid().ToString("N");
            builder.Services.AddScoped<ITenantAccessor>(_ => new FixedTenantAccessor("tenant-a"));
            builder.Services.AddDbContext<HrmDbContext>(o => o.UseInMemoryDatabase("import-core-" + store));
            builder.Services.AddScoped<ProcurementRequestScope>();
            builder.Services.AddScoped<IProcurementScope>(sp => sp.GetRequiredService<ProcurementRequestScope>());
            builder.Services.AddDbContext<ProcurementDbContext>(o => o.UseInMemoryDatabase("import-proc-" + store));
            builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions,
                ProcurementRequestHttpTests.TestAuthenticationHandler>("test", _ => { });
            builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
            var app = builder.Build();
            app.UseRouting(); app.UseAuthentication();
            app.UseMiddleware<ProcurementCorePrincipalRefreshMiddleware>();
            app.UseAuthorization(); app.UseMiddleware<ProcurementScopeMiddleware>();
            ProcurementMasterImportRoutes.MapRoutes(app);
            var company = new LegalEntity { Code = "A", RegisteredName = "Company A", Currency = "ZMW", IsDefault = true };
            var other = new LegalEntity { Code = "B", RegisteredName = "Company B", Currency = "USD" };
            var branch = new WorkLocation { Code = "A1", Name = "Main", LegalEntityId = company.Id };
            var fixture = new Fixture(app, company.Id, other.Id, branch.Id);
            using (var seed = app.Services.CreateScope())
            {
                var core = seed.ServiceProvider.GetRequiredService<HrmDbContext>();
                core.LegalEntities.AddRange(company, other); core.WorkLocations.Add(branch);
                core.UserBranchAssignments.Add(new HrUserBranchAssignment
                    { UserId = fixture.Both, LocationId = branch.Id });
                foreach (var (id, role, actions) in new[] {
                    (fixture.Both, "csv_both", "procurement-access,procurement-vendor-import,procurement-vendor-create,procurement-item-import,procurement-item-manage"),
                    (fixture.ImportOnly, "csv_import_only", "procurement-access,procurement-vendor-import"),
                    (fixture.NativeOnly, "csv_native_only", "procurement-access,procurement-vendor-create"),
                    (fixture.LegacyAdmin, "erp_admin", "erp_admin") })
                {
                    core.LocalUsers.Add(new LocalUser { Id = id, Email = id + "@example.test",
                        NormalizedEmail = id + "@EXAMPLE.TEST", DisplayName = role,
                        PasswordHash = "synthetic", RolesCsv = role, IsActive = true });
                    core.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = role,
                        RoleName = role, Active = true, PermissionsCsv = actions });
                }
                await core.SaveChangesAsync();
            }
            await app.StartAsync();
            fixture.client = app.GetTestClient();
            return fixture;
        }

        public async Task<HttpResponseMessage> Send(Guid user, HttpMethod method, string path,
            bool foreignCompany = false, bool allBranches = false)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Add("X-Test-User", user.ToString());
            request.Headers.Add("X-Test-Role", "erp_admin"); // stored role remains authoritative
            request.Headers.Add("X-Shell-Entity", (foreignCompany ? otherCompany : company).ToString());
            if (allBranches) request.Headers.Add("X-Shell-Branch-Scope", "all");
            else if (!foreignCompany) request.Headers.Add("X-Shell-Location", branch.ToString());
            if (method == HttpMethod.Post) request.Content = JsonContent.Create(new { });
            return await client!.SendAsync(request);
        }

        public async Task RevokeImport()
        {
            using var seed = app.Services.CreateScope();
            var core = seed.ServiceProvider.GetRequiredService<HrmDbContext>();
            var role = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "csv_both");
            role.PermissionsCsv = "procurement-access,procurement-vendor-create,procurement-item-manage";
            await core.SaveChangesAsync();
        }

        public async Task<(int Batches, int Suppliers, int Items)> MasterFootprint()
        {
            using var seed = app.Services.CreateScope();
            seed.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
                .Set("tenant-a", company, Both.ToString(), branch, null, false, "verify");
            var db = seed.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            return (await db.ImportBatches.CountAsync(), await db.Suppliers.CountAsync(),
                await db.CatalogItems.CountAsync());
        }

        public async ValueTask DisposeAsync() { client?.Dispose(); await app.DisposeAsync(); }
    }
}
