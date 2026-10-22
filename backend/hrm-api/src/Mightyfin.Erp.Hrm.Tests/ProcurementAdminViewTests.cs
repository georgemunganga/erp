using Microsoft.AspNetCore.Authentication;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

// Replaces the obsolete employee-less-admin read-only contract. System accounts
// are authoritative; this mapped InMemory proof is not a PostgreSQL/SSO test.
public sealed class ProcurementAdminViewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Active_system_account_context_and_real_vendor_CRUD_do_not_require_an_employee(bool admin)
    {
        await using var f = await Fixture.Start(admin);
        var context = await f.Client.GetFromJsonAsync<JsonElement>("/api/procurement/v1/context");
        Assert.Equal(f.Account.Id, context.GetProperty("principalId").GetGuid());
        Assert.Equal(JsonValueKind.Null, context.GetProperty("workerId").ValueKind);
        Assert.False(context.GetProperty("adminViewOnly").GetBoolean());
        Assert.Contains(context.GetProperty("permissions").EnumerateArray(), x => x.GetString() == "procurement-vendor-create");
        var create = await f.Client.PostAsJsonAsync("/api/procurement/v1/vendors", Draft("Original"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(); var id = created.GetProperty("id").GetGuid();
        var detail = await f.Client.GetFromJsonAsync<JsonElement>("/api/procurement/v1/vendors/" + id);
        var update = await f.Client.PutAsJsonAsync("/api/procurement/v1/vendors/" + id,
            new { version = detail.GetProperty("version").GetInt64(), draft = Draft("Corrected") });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        detail = await f.Client.GetFromJsonAsync<JsonElement>("/api/procurement/v1/vendors/" + id);
        Assert.Equal("Corrected", detail.GetProperty("displayName").GetString());
        var cancel = await f.Client.PostAsJsonAsync("/api/procurement/v1/vendors/" + id + "/withdraw",
            new { version = detail.GetProperty("version").GetInt64(), reason = "Retain documentary draft" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        detail = await f.Client.GetFromJsonAsync<JsonElement>("/api/procurement/v1/vendors/" + id);
        Assert.Equal("Withdrawn", detail.GetProperty("status").GetString());
        Assert.Empty(f.Core.Workers); Assert.Null(f.Account.WorkerId);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("archived")]
    [InlineData("locked")]
    [InlineData("account-role-revoked")]
    [InlineData("role-inactive")]
    [InlineData("role-archived")]
    [InlineData("all-grants-revoked")]
    [InlineData("hr-only")]
    [InlineData("unknown-actions")]
    public async Task Current_Core_revocation_denies_stale_admin_claims_before_real_vendor_effects(string failure)
    {
        await using var f = await Fixture.Start();
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync("/api/procurement/v1/context")).StatusCode);
        if (failure == "inactive") f.Account.IsActive = false;
        if (failure == "archived") f.Account.IsArchived = true;
        if (failure == "locked") f.Account.LockedUntil = DateTimeOffset.UtcNow.AddDays(1);
        if (failure == "account-role-revoked") f.Account.RolesCsv = "";
        if (failure == "role-inactive") f.Role.Active = false;
        if (failure == "role-archived") f.Role.IsArchived = true;
        if (failure == "all-grants-revoked") f.Role.PermissionsCsv = "";
        if (failure == "hr-only") f.Role.PermissionsCsv = "hr_admin";
        if (failure == "unknown-actions") f.Role.PermissionsCsv = "procurement-made-up";
        await f.Core.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.GetAsync("/api/procurement/v1/context")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PostAsJsonAsync("/api/procurement/v1/vendors", Draft("Denied"))).StatusCode);
        Assert.Equal(0, await f.SupplierCount()); Assert.Empty(f.Core.Workers);
    }

    [Fact]
    public async Task Retaining_access_but_revoking_only_create_denies_command_and_updates_context()
    {
        await using var f = await Fixture.Start();
        f.Role.PermissionsCsv = "procurement-access,procurement-vendor-read"; await f.Core.SaveChangesAsync();
        var context = await f.Client.GetFromJsonAsync<JsonElement>("/api/procurement/v1/context");
        Assert.DoesNotContain(context.GetProperty("permissions").EnumerateArray(), x => x.GetString() == "procurement-vendor-create");
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PostAsJsonAsync("/api/procurement/v1/vendors", Draft("Denied"))).StatusCode);
        Assert.Equal(0, await f.SupplierCount());
    }

    [Theory]
    [InlineData("foreign-tenant")]
    [InlineData("foreign-company")]
    [InlineData("unassigned-branch")]
    [InlineData("missing-core-user")]
    [InlineData("external-subject-without-core-user")]
    public async Task System_account_scope_and_identity_denials_preserve_store(string failure)
    {
        await using var f = await Fixture.Start();
        if (failure == "foreign-tenant") f.Client.DefaultRequestHeaders.Add("X-Test-Tenant", "other-tenant");
        if (failure == "foreign-company") f.Client.DefaultRequestHeaders.Add("X-Shell-Entity", Guid.NewGuid().ToString());
        if (failure == "unassigned-branch")
        {
            var other = new WorkLocation { Code = "OTHER", Name = "Other", LegalEntityId = f.Company.Id };
            f.Core.WorkLocations.Add(other);
            f.Core.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = f.Account.Id, LocationId = f.Branch.Id });
            await f.Core.SaveChangesAsync();
            f.Client.DefaultRequestHeaders.Add("X-Shell-Location", other.Id.ToString());
        }
        if (failure == "missing-core-user") f.Client.DefaultRequestHeaders.Add("X-Test-Subject", Guid.NewGuid().ToString());
        if (failure == "external-subject-without-core-user") f.Client.DefaultRequestHeaders.Add("X-Test-Subject", "external-oidc-subject");
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PostAsJsonAsync("/api/procurement/v1/vendors", Draft("Denied"))).StatusCode);
        Assert.Equal(0, await f.SupplierCount());
    }

    [Fact]
    public async Task Clearing_legacy_admin_role_permissions_revokes_admin_module_access_too()
    {
        await using var f = await Fixture.Start(admin: true);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync("/api/procurement/v1/context")).StatusCode);
        f.Role.PermissionsCsv = ""; await f.Core.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.GetAsync("/api/procurement/v1/context")).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hr_admin")]
    [InlineData("procurement-unknown")]
    public async Task Revoking_a_known_legacy_role_never_restores_bootstrap_permissions(string replacement)
    {
        await using var f = await Fixture.Start();
        f.Account.RolesCsv = "procurement_manager"; f.Role.RoleKey = "procurement_manager";
        f.Role.PermissionsCsv = "procurement_manager"; await f.Core.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync("/api/procurement/v1/context")).StatusCode);
        f.Role.PermissionsCsv = replacement; await f.Core.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.GetAsync("/api/procurement/v1/context")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PostAsJsonAsync("/api/procurement/v1/vendors", Draft("Denied"))).StatusCode);
        Assert.Equal(0, await f.SupplierCount());
    }

    [Fact]
    public async Task Newly_added_Core_permission_is_effective_despite_stale_empty_module_claims()
    {
        await using var f = await Fixture.Start();
        f.Client.DefaultRequestHeaders.Add("X-Test-Role", "obsolete-external-role");
        f.Client.DefaultRequestHeaders.Add("X-Test-Configured", "true");
        f.Role.PermissionsCsv = "procurement-access,procurement-vendor-read"; await f.Core.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PostAsJsonAsync("/api/procurement/v1/vendors", Draft("Denied"))).StatusCode);
        f.Role.PermissionsCsv += ",procurement-vendor-create"; await f.Core.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Created, (await f.Client.PostAsJsonAsync("/api/procurement/v1/vendors", Draft("Granted by current Core"))).StatusCode);
        Assert.Equal(1, await f.SupplierCount());
    }

    [Fact]
    public async Task Inactive_missing_and_archived_role_definitions_never_survive_in_current_claims()
    {
        await using var f = await Fixture.Start();
        f.Account.RolesCsv += ",erp_admin,missing_role,retired_role";
        f.Core.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = "erp_admin", RoleName = "Inactive", Active = false, PermissionsCsv = "erp_admin" });
        f.Core.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = "retired_role", RoleName = "Retired", Active = true, IsArchived = true, PermissionsCsv = "procurement-access" });
        await f.Core.SaveChangesAsync();
        var roles = await f.Client.GetFromJsonAsync<string[]>("/api/procurement/v1/system-role-projection");
        Assert.Equal(new[] { "system_clerk" }, roles);
    }

    private static object Draft(string name) => new { legalName = name, displayName = name, countryCode = "ZM" };

    private sealed class Fixture(WebApplication app, HrmDbContext core, LocalUser account,
        TenantRoleAssignment role, LegalEntity company, WorkLocation branch) : IAsyncDisposable
    {
        public HrmDbContext Core => core; public LocalUser Account => account; public TenantRoleAssignment Role => role;
        public LegalEntity Company => company; public WorkLocation Branch => branch;
        public HttpClient Client { get; } = app.GetTestClient();
        public static async Task<Fixture> Start(bool admin = false)
        {
            var core = TestDbContextFactory.Create("system-tenant");
            var company = new LegalEntity { Code = "SYS", RegisteredName = "Synthetic", Currency = "ZMW", IsDefault = true };
            var branch = new WorkLocation { Code = "SYS", Name = "Synthetic branch", LegalEntityId = company.Id };
            var role = new TenantRoleAssignment { RoleKey = admin ? "erp_admin" : "system_clerk", RoleName = "Synthetic existing Core role", Active = true,
                PermissionsCsv = admin ? "erp_admin" : "procurement-access,procurement-vendor-read,procurement-vendor-create,procurement-vendor-edit" };
            var account = new LocalUser { Email = "system@example.test", NormalizedEmail = "SYSTEM@EXAMPLE.TEST", DisplayName = "System user",
                PasswordHash = "synthetic-unused", RolesCsv = role.RoleKey, IsActive = true };
            core.LegalEntities.Add(company); core.WorkLocations.Add(branch); core.LocalUsers.Add(account); core.TenantRoleAssignments.Add(role);
            await core.SaveChangesAsync();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
            builder.WebHost.UseTestServer(); builder.Services.AddSingleton(core); builder.Services.AddScoped<ProcurementRequestScope>();
            builder.Services.AddScoped<IProcurementScope>(sp => sp.GetRequiredService<ProcurementRequestScope>());
            builder.Services.AddDbContext<ProcurementDbContext>(options => options.UseInMemoryDatabase("system-principal-" + account.Id));
            // The fixture supplies its principal explicitly below. Register only
            // the normal test scheme plumbing required for real authorization
            // middleware to return Forbid instead of throwing on denied actions.
            builder.Services.AddAuthentication("test")
                .AddScheme<AuthenticationSchemeOptions, ProcurementRequestHttpTests.TestAuthenticationHandler>("test", _ => { });
            builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
            var app = builder.Build(); app.UseRouting();
            app.Use(async (http, next) =>
            {
                // Deliberately stale/admin-looking presentation: actual stored Core
                // account/grants below must decide, not this test authentication claim.
                http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tenant", http.Request.Headers["X-Test-Tenant"].FirstOrDefault() ?? "system-tenant"),
                    new Claim("sub", http.Request.Headers["X-Test-Subject"].FirstOrDefault() ?? account.Id.ToString("D")), new Claim(ClaimTypes.Role, http.Request.Headers["X-Test-Role"].FirstOrDefault() ?? "erp_admin") }
                    .Concat(http.Request.Headers["X-Test-Configured"] == "true" ? new[] { new Claim(ProcurementPermissions.ConfiguredClaim, "true") } : Array.Empty<Claim>()), "synthetic-test"));
                await next(http);
            });
            app.UseMiddleware<ProcurementCorePrincipalRefreshMiddleware>();
            app.UseAuthorization(); app.UseMiddleware<ProcurementScopeMiddleware>();
            ProcurementContextRoutes.MapRoutes(app); ProcurementVendorRoutes.MapRoutes(app);
            app.MapGet("/api/procurement/v1/system-role-projection", (Microsoft.AspNetCore.Http.HttpContext http) =>
                http.User.Claims.Where(x => x.Type == ClaimTypes.Role).Select(x => x.Value).ToArray())
                .RequireAuthorization("procurement-access");
            await app.StartAsync(); return new(app, core, account, role, company, branch);
        }
        public async Task<int> SupplierCount()
        {
            using var services = app.Services.CreateScope(); var scope = services.ServiceProvider.GetRequiredService<ProcurementRequestScope>();
            scope.Set("system-tenant", company.Id, account.Id.ToString("D"), null, null, false, "read-assertion"); scope.AssignPrincipal(account.Id);
            return await services.ServiceProvider.GetRequiredService<ProcurementDbContext>().Suppliers.CountAsync();
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await app.DisposeAsync(); }
    }
}
