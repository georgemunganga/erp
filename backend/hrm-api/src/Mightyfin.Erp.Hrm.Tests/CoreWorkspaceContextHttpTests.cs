using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Mapped Core endpoint with synthetic authentication and real tenant-filtered
/// Core queries. Normal cookie/PostgreSQL acceptance belongs to the private HTTP probe.</summary>
public sealed class CoreWorkspaceContextHttpTests
{
    [Fact]
    public async Task Workerless_custom_account_sees_only_active_assigned_company_and_branch_without_Hrm_role()
    {
        await using var fixture = await Fixture.Start();
        using var response = await fixture.Get(fixture.Maker);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? "");
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(1, body.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("tenant-a", body.GetProperty("tenantId").GetString());
        Assert.Equal(fixture.Maker, body.GetProperty("principalId").GetGuid());
        Assert.False(body.GetProperty("organisationWide").GetBoolean());
        Assert.Equal(fixture.CompanyA, Assert.Single(body.GetProperty("companies").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(fixture.BranchA, Assert.Single(body.GetProperty("branches").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.DoesNotContain("worker", body.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Anonymous_missing_tenant_inactive_locked_and_other_tenant_accounts_are_denied()
    {
        await using var fixture = await Fixture.Start();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Get(null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Get(fixture.Maker, tenant: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Get(fixture.Inactive)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Get(fixture.Locked)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Get(fixture.ForeignUser)).StatusCode);
        using var foreign = await fixture.Get(fixture.ForeignUser, "tenant-b");
        Assert.Equal(HttpStatusCode.OK, foreign.StatusCode);
        var body = await foreign.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(fixture.ForeignCompany, Assert.Single(body.GetProperty("companies").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.DoesNotContain(fixture.CompanyA.ToString("D"), body.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Inactive_assignment_does_not_promote_a_confined_account_to_organisation_wide()
    {
        await using var fixture = await Fixture.Start();
        using var response = await fixture.Get(fixture.StaleAssignment);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(body.GetProperty("organisationWide").GetBoolean());
        Assert.Empty(body.GetProperty("companies").EnumerateArray());
        Assert.Empty(body.GetProperty("branches").EnumerateArray());
    }

    [Fact]
    public async Task No_assignments_keeps_existing_tenant_only_organisation_wide_choices()
    {
        await using var fixture = await Fixture.Start();
        using var response = await fixture.Get(fixture.OrganisationWide);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(body.GetProperty("organisationWide").GetBoolean());
        Assert.True(body.GetProperty("companies").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToHashSet().SetEquals(new[] { fixture.CompanyA, fixture.CompanyB }));
        Assert.True(body.GetProperty("branches").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToHashSet().SetEquals(new[] { fixture.BranchA, fixture.BranchB }));
        Assert.DoesNotContain(fixture.ForeignCompany.ToString("D"), body.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Fixture(WebApplication app, HrmDbContext anchor, Guid maker, Guid inactive,
        Guid locked, Guid stale, Guid wide, Guid foreignUser, Guid companyA, Guid companyB,
        Guid foreignCompany, Guid branchA, Guid branchB) : IAsyncDisposable
    {
        private const string Route = "/api/core/v1/workspace-context";
        private readonly HttpClient client = app.GetTestClient();
        internal Guid Maker => maker;
        internal Guid Inactive => inactive;
        internal Guid Locked => locked;
        internal Guid StaleAssignment => stale;
        internal Guid OrganisationWide => wide;
        internal Guid ForeignUser => foreignUser;
        internal Guid CompanyA => companyA;
        internal Guid CompanyB => companyB;
        internal Guid ForeignCompany => foreignCompany;
        internal Guid BranchA => branchA;
        internal Guid BranchB => branchB;

        internal async Task<HttpResponseMessage> Get(Guid? user, string? tenant = "tenant-a")
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Route);
            if (user is { } id) request.Headers.Add("X-Test-User", id.ToString("D"));
            if (tenant is not null) request.Headers.Add("X-Test-Tenant", tenant);
            return await client.SendAsync(request);
        }

        internal static async Task<Fixture> Start()
        {
            var anchor = TestDbContextFactory.Create("tenant-a");
            var companyA = new LegalEntity { TenantId = "tenant-a", Code = "A", RegisteredName = "Alpha", Currency = "ZMW", CountryCode = "ZM", IsDefault = true };
            var companyB = new LegalEntity { TenantId = "tenant-a", Code = "B", RegisteredName = "Bravo", Currency = "ZMW", CountryCode = "ZM" };
            var branchA = new WorkLocation { TenantId = "tenant-a", LegalEntityId = companyA.Id, Code = "A1", Name = "Alpha branch" };
            var branchB = new WorkLocation { TenantId = "tenant-a", LegalEntityId = companyB.Id, Code = "B1", Name = "Bravo branch" };
            var inactiveBranch = new WorkLocation { TenantId = "tenant-a", LegalEntityId = companyB.Id,
                Code = "B0", Name = "Inactive branch", IsArchived = true };
            var maker = Account("tenant-a", "Maker");
            var inactive = Account("tenant-a", "Inactive"); inactive.IsActive = false;
            var locked = Account("tenant-a", "Locked"); locked.LockedUntil = DateTimeOffset.UtcNow.AddHours(1);
            var stale = Account("tenant-a", "Stale");
            var wide = Account("tenant-a", "Wide");
            anchor.LegalEntities.AddRange(companyA, companyB);
            anchor.WorkLocations.AddRange(branchA, branchB, inactiveBranch);
            anchor.LocalUsers.AddRange(maker, inactive, locked, stale, wide);
            anchor.UserBranchAssignments.AddRange(Assignment("tenant-a", maker, branchA),
                Assignment("tenant-a", stale, inactiveBranch));
            await anchor.SaveChangesAsync();
            var options = new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(anchor.Database.GetDbConnection()).Options;
            Guid foreignId; Guid foreignUserId;
            await using (var foreign = new HrmDbContext(options, new FixedTenantAccessor("tenant-b")))
            {
                var company = new LegalEntity { TenantId = "tenant-b", Code = "FOREIGN", RegisteredName = "Foreign", Currency = "USD", CountryCode = "US" };
                var foreignBranch = new WorkLocation { TenantId = "tenant-b", LegalEntityId = company.Id, Code = "F1", Name = "Foreign branch" };
                var person = Account("tenant-b", "Foreign");
                foreign.LegalEntities.Add(company); foreign.WorkLocations.Add(foreignBranch); foreign.LocalUsers.Add(person);
                foreign.UserBranchAssignments.Add(Assignment("tenant-b", person, foreignBranch));
                await foreign.SaveChangesAsync(); foreignId = company.Id; foreignUserId = person.Id;
            }
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
            builder.Configuration["HRM:DefaultTenantId"] = "tenant-a";
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ITenantAccessor, PrincipalTenantAccessor>();
            builder.Services.AddDbContext<HrmDbContext>((_, db) => db.UseSqlite(anchor.Database.GetDbConnection()));
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddAuthentication("workspace-test")
                .AddScheme<AuthenticationSchemeOptions, WorkspaceAuthenticationHandler>("workspace-test", _ => { });
            builder.Services.AddAuthorization(CoreWorkspaceContextRoutes.AddPolicy);
            var app = builder.Build();
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            CoreWorkspaceContextRoutes.MapRoutes(app);
            await app.StartAsync();
            return new(app, anchor, maker.Id, inactive.Id, locked.Id, stale.Id, wide.Id,
                foreignUserId, companyA.Id, companyB.Id, foreignId, branchA.Id, branchB.Id);
        }
        private static LocalUser Account(string tenant, string name) => new()
        {
            TenantId = tenant, Email = name.ToLowerInvariant() + ".workspace@example.invalid",
            NormalizedEmail = name.ToUpperInvariant() + ".WORKSPACE@EXAMPLE.INVALID",
            DisplayName = "Synthetic " + name, PasswordHash = "not-used-by-mapped-auth",
            RolesCsv = "custom_procurement_reader", WorkerId = null, IsActive = true,
        };
        private static HrUserBranchAssignment Assignment(string tenant, LocalUser user, WorkLocation branch) => new()
        { TenantId = tenant, UserId = user.Id, UserEmail = user.Email, LocationId = branch.Id };
        public async ValueTask DisposeAsync() { client.Dispose(); await app.DisposeAsync(); await anchor.DisposeAsync(); }
    }

    private sealed class WorkspaceAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-User"], out var principal))
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, principal.ToString("D")) };
            var tenant = Request.Headers["X-Test-Tenant"].ToString();
            if (!string.IsNullOrEmpty(tenant)) claims.Add(new Claim("tenant", tenant));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, "workspace-test")), "workspace-test")));
        }
    }
}
