using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Real routing/auth/scope tests. PostgreSQL writes are verified by the separate probe.</summary>
public sealed class ProcurementSpendingAuthorityHttpTests
{
    private const string Path = "/api/procurement/v1/configuration/spending-authorities";

    [Fact]
    public async Task Hidden_global_predecessor_does_not_advertise_activation_or_leak_its_identity()
    {
        await using var fixture = await Fixture.Start();
        var (pending, predecessor) = await fixture.SeedReviewFamily(globalActive: true, newerDraft: false);
        var response = await fixture.Send(HttpMethod.Get, Path + "/" + pending, "erp_admin", branch: fixture.Branch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(text);
        Assert.Equal(new[] { "Return" }, json.RootElement.GetProperty("allowedActions").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("previousGrantId").ValueKind);
        Assert.DoesNotContain(predecessor.ToString("D"), text);
    }

    [Fact]
    public async Task Newer_family_draft_suppresses_activation_of_older_pending_version()
    {
        await using var fixture = await Fixture.Start();
        var (pending, _) = await fixture.SeedReviewFamily(globalActive: false, newerDraft: true);
        var response = await fixture.Send(HttpMethod.Get, Path + "/" + pending, "erp_admin", branch: fixture.Branch);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "Return" }, json.GetProperty("allowedActions").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Contains(json.GetProperty("blockingReasons").EnumerateArray(), x => x.GetProperty("code").GetString() == "AuthorityLatestVersionRequired");
    }

    [Fact]
    public async Task Read_projection_has_exact_amount_name_and_current_server_actions()
    {
        await using var fixture = await Fixture.Start();
        var response = await fixture.Send(HttpMethod.Get, Path + "/" + fixture.Grant, "erp_admin", branch: fixture.OtherBranch);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("0", detail.GetProperty("maximumAmount").GetString());
        Assert.Equal("Authority owner", detail.GetProperty("principalDisplayName").GetString());
        Assert.Equal(new[] { "Edit" }, detail.GetProperty("allowedActions").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.NotEmpty(detail.GetProperty("blockingReasons").EnumerateArray());
        await fixture.SetReadOnly();
        response = await fixture.Send(HttpMethod.Get, Path + "/" + fixture.Grant, "erp_admin", branch: fixture.OtherBranch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(detail.GetProperty("allowedActions").EnumerateArray());
    }

    [Fact]
    public async Task People_lookup_is_scoped_bounded_and_omits_contact_credentials_and_ambiguous_logins()
    {
        await using var fixture = await Fixture.Start();
        var response = await fixture.Send(HttpMethod.Get, Path + "/lookups/people?limit=1", "erp_admin", branch: fixture.Branch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var person = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(fixture.User.ToString("D"), person.GetProperty("principalId").GetString());
        Assert.Equal(new[] { "displayName", "principalId", "workerId" }, person.EnumerateObject().Select(x => x.Name).Order().ToArray());
        response = await fixture.Send(HttpMethod.Get, Path + "/lookups/people?limit=500", "erp_admin", branch: fixture.OtherBranch);
        body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(body.GetProperty("items").EnumerateArray());
        Assert.Equal(50, body.GetProperty("limit").GetInt32());
        await fixture.AddAlias();
        response = await fixture.Send(HttpMethod.Get, Path + "/lookups/people", "erp_admin", branch: fixture.Branch);
        body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(body.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Send(HttpMethod.Get, Path + "/lookups/people", "employee")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_and_unconfigured_roles_cannot_read_or_mutate_authority_configuration()
    {
        await using var fixture = await Fixture.Start();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Send(HttpMethod.Get, Path, "employee")).StatusCode);
        foreach (var suffix in new[] { "", "/submit", "/return", "/activate", "/revoke", "/supersede" })
        {
            var url = suffix.Length == 0 ? Path : Path + "/" + fixture.Grant + suffix;
            Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Send(HttpMethod.Post, url, "procurement_manager",
                new { version = 1, reason = "Review" })).StatusCode);
        }
    }

    [Fact]
    public async Task Canonical_account_read_is_scoped_by_selected_branch_and_foreign_records_are_hidden()
    {
        await using var fixture = await Fixture.Start();
        var list = await fixture.Send(HttpMethod.Get, Path, "erp_admin", branch: fixture.Branch);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(body.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Send(HttpMethod.Get,
            Path + "/" + fixture.Grant, "erp_admin", branch: fixture.Branch)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Send(HttpMethod.Get,
            Path + "/" + fixture.Grant, "erp_admin", branch: fixture.OtherBranch)).StatusCode);
    }

    [Fact]
    public async Task Current_Core_grants_override_stale_claims_and_worker_subject_is_not_a_canonical_account()
    {
        await using var fixture = await Fixture.Start();
        await fixture.SetRoles("employee");
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Send(HttpMethod.Get, Path, "erp_admin")).StatusCode);
        await fixture.SetRoles("erp_admin");
        var invalidSubject = await fixture.Send(HttpMethod.Post, Path, "erp_admin", new
        {
            authorityReference = "AUTH-OTHER", principalId = Guid.NewGuid().ToString("D"), draft = new { },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidSubject.StatusCode);
        Assert.Equal("AuthoritySubjectUnavailable", (await invalidSubject.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString());
    }

    [Fact]
    public async Task Write_refuses_nontransactional_provider_and_cannot_report_fake_activation()
    {
        await using var fixture = await Fixture.Start();
        var result = await fixture.Send(HttpMethod.Post, Path, "erp_admin", new
        {
            authorityReference = "AUTH-NEW", principalId = fixture.User.ToString("D"), draft = new { },
        });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        Assert.Equal("AuthorityPostgresTransactionRequired", (await result.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString());
    }

    [Fact]
    public async Task Default_PR_settings_and_explicit_PO_settings_do_not_mix_typed_authority_families()
    {
        await using var fixture = await Fixture.Start();
        var orderGrant = await fixture.SeedOrderGrant();
        var requestList = await fixture.Send(HttpMethod.Get, Path, "erp_admin", branch: fixture.OtherBranch);
        Assert.Equal(HttpStatusCode.OK, requestList.StatusCode);
        var requests = await requestList.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, requests.GetProperty("total").GetInt32());
        Assert.Equal(fixture.Grant, Assert.Single(requests.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal("PurchaseRequest.Approve", requests.GetProperty("createAction").GetString());
        Assert.Equal("EstimatedLineBase", requests.GetProperty("createAmountBasis").GetString());
        var orderList = await fixture.Send(HttpMethod.Get, Path + "?action=PurchaseOrder.Approve", "erp_admin", branch: fixture.OtherBranch);
        Assert.Equal(HttpStatusCode.OK, orderList.StatusCode);
        var orders = await orderList.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, orders.GetProperty("total").GetInt32());
        var grant = Assert.Single(orders.GetProperty("items").EnumerateArray());
        Assert.Equal(orderGrant, grant.GetProperty("id").GetGuid());
        Assert.Equal("PurchaseOrder.Approve", grant.GetProperty("action").GetString());
        Assert.Equal("procurement-po-approve", grant.GetProperty("permission").GetString());
        Assert.Equal("ApprovedLineBase", orders.GetProperty("createAmountBasis").GetString());
        Assert.Equal("0", grant.GetProperty("maximumAmount").GetString());
        Assert.Equal(new[] { "Edit" }, grant.GetProperty("allowedActions").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await fixture.Send(HttpMethod.Get,
            Path + "?action=PurchaseOrder.Pay", "erp_admin")).StatusCode);
    }

    [Fact]
    public async Task Named_people_lookup_uses_the_requested_PR_or_PO_permission_without_cross_granting()
    {
        await using var fixture = await Fixture.Start();
        var (requestPerson, orderPerson) = await fixture.SeedActionPeople();
        var requestLookup = await fixture.Send(HttpMethod.Get, Path + "/lookups/people?search=only", "erp_admin", branch: fixture.Branch);
        Assert.Equal(HttpStatusCode.OK, requestLookup.StatusCode);
        var requests = await requestLookup.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(requestPerson.ToString("D"), Assert.Single(requests.GetProperty("items").EnumerateArray()).GetProperty("principalId").GetString());
        var orderLookup = await fixture.Send(HttpMethod.Get,
            Path + "/lookups/people?search=only&action=PurchaseOrder.Approve", "erp_admin", branch: fixture.Branch);
        Assert.Equal(HttpStatusCode.OK, orderLookup.StatusCode);
        var orders = await orderLookup.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(orderPerson.ToString("D"), Assert.Single(orders.GetProperty("items").EnumerateArray()).GetProperty("principalId").GetString());
    }

    private sealed class Fixture(WebApplication app, Guid company, Guid user, Guid branch, Guid otherBranch, Guid grant) : IAsyncDisposable
    {
        public Guid User => user;
        public Guid Branch => branch;
        public Guid OtherBranch => otherBranch;
        public Guid Grant => grant;
        public HttpClient Client { get; } = app.GetTestClient();
        public async Task<HttpResponseMessage> Send(HttpMethod method, string path, string role,
            object? body = null, Guid? branch = null)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Add("X-Test-User", user.ToString());
            request.Headers.Add("X-Test-Role", role);
            request.Headers.Add("X-Shell-Entity", company.ToString());
            if (branch.HasValue) request.Headers.Add("X-Shell-Location", branch.ToString());
            if (body is not null) request.Content = JsonContent.Create(body);
            return await Client.SendAsync(request);
        }
        public async Task SetRoles(string roles)
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.LocalUsers.SingleAsync(x => x.Id == user)).RolesCsv = roles;
            await core.SaveChangesAsync();
        }
        public async Task SetReadOnly()
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.LocalUsers.SingleAsync(x => x.Id == user)).RolesCsv = "employee";
            core.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = "employee", RoleName = "Reader", Active = true,
                PermissionsCsv = "procurement-access,procurement-authority-read" });
            await core.SaveChangesAsync();
        }
        public async Task AddAlias()
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var original = await core.LocalUsers.SingleAsync(x => x.Id == user);
            core.LocalUsers.Add(new LocalUser { Email = "alias@example.test", NormalizedEmail = "ALIAS@EXAMPLE.TEST",
                DisplayName = "Alias", PasswordHash = "test-placeholder-only", RolesCsv = "erp_admin", WorkerId = original.WorkerId });
            await core.SaveChangesAsync();
        }
        public async Task<(Guid Pending, Guid Predecessor)> SeedReviewFamily(bool globalActive, bool newerDraft)
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            LocalUser Person(string name)
            {
                var worker = new Worker { EmployeeNo = name, FirstName = name, LastName = "Fixture", Status = "active", LocationId = otherBranch };
                var account = new LocalUser { Email = name + "@example.test", NormalizedEmail = name.ToUpperInvariant() + "@EXAMPLE.TEST",
                    DisplayName = name, PasswordHash = "fixture-only", RolesCsv = "erp_admin", WorkerId = worker.Id };
                core.Workers.Add(worker); core.LocalUsers.Add(account); return account;
            }
            var owner = Person("FamilyOwner"); var subject = Person("FamilySubject"); await core.SaveChangesAsync();
            async Task<T> AsActor<T>(Guid principal, Func<ProcurementDbContext, Task<T>> work)
            {
                using var actingServices = app.Services.CreateScope();
                actingServices.ServiceProvider.GetRequiredService<ProcurementRequestScope>().Set(
                    "tenant-a", company, principal.ToString("D"), null, null, false, "read-family-fixture");
                return await work(actingServices.ServiceProvider.GetRequiredService<ProcurementDbContext>());
            }
            var maker = new SpendingAuthorityConfigurationActor("tenant-a", company, owner.Id.ToString("D"), true,
                new HashSet<string>([SpendingAuthorityConfiguration.ConfigurePermission]));
            var reviewer = new SpendingAuthorityConfigurationActor("tenant-a", company, user.ToString("D"), true,
                new HashSet<string>([SpendingAuthorityConfiguration.ReviewPermission]));
            var target = new SpendingAuthoritySubject("tenant-a", company, subject.Id.ToString("D"), true, true);
            var now = DateTimeOffset.UtcNow.AddMinutes(-1);
            SpendingAuthorityDraft Draft(bool all) => new("ZMW", "EstimatedLineBase", 100, now.AddDays(-1),
                AllBranches: all, BranchRef: all ? null : branch, AllDepartments: true, AllCategories: true);
            var firstId = await AsActor(owner.Id, async db =>
            {
                var first = SpendingAuthorityConfiguration.CreateDraft("FAMILY", target.PrincipalId, Draft(globalActive), maker, now, []);
                db.AddScoped(first); await db.SaveChangesAsync();
                if (globalActive)
                {
                    SpendingAuthorityConfiguration.Submit(first, first.Version, maker, target, now);
                    await db.SaveChangesAsync();
                }
                return first.Id;
            });
            if (globalActive)
                await AsActor(user, async db =>
                {
                    var first = await db.SpendingAuthorityGrants.SingleAsync(x => x.Id == firstId);
                    SpendingAuthorityConfiguration.Activate(first, first.Version, reviewer, target, "Reviewed", now, [first]);
                    await db.SaveChangesAsync(); return true;
                });
            var pendingId = await AsActor(owner.Id, async db =>
            {
                var first = await db.SpendingAuthorityGrants.SingleAsync(x => x.Id == firstId);
                var pending = SpendingAuthorityConfiguration.CreateDraft("FAMILY", target.PrincipalId, Draft(false), maker, now, [first]);
                db.AddScoped(pending); await db.SaveChangesAsync();
                SpendingAuthorityConfiguration.Submit(pending, pending.Version, maker, target, now); await db.SaveChangesAsync();
                if (newerDraft)
                {
                    var next = SpendingAuthorityConfiguration.CreateDraft("FAMILY", target.PrincipalId, Draft(false), maker, now, [first, pending]);
                    db.AddScoped(next); await db.SaveChangesAsync();
                }
                return pending.Id;
            });
            return (pendingId, firstId);
        }
        public async Task<Guid> SeedOrderGrant()
        {
            using var services = app.Services.CreateScope();
            services.ServiceProvider.GetRequiredService<ProcurementRequestScope>().Set(
                "tenant-a", company, user.ToString("D"), null, null, false, "typed-authority-family-fixture");
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var existing = await db.SpendingAuthorityGrants.SingleAsync(x => x.Id == grant);
            var actor = new SpendingAuthorityConfigurationActor("tenant-a", company, user.ToString("D"), true,
                new HashSet<string>([SpendingAuthorityConfiguration.ConfigurePermission]));
            var order = SpendingAuthorityConfiguration.CreateDraft(existing.AuthorityReference, user.ToString("D"),
                new("ZMW", "ApprovedLineBase", BranchRef: otherBranch), actor, DateTimeOffset.UtcNow, [existing], "PurchaseOrder.Approve");
            db.AddScoped(order); await db.SaveChangesAsync(); return order.Id;
        }
        public async Task<(Guid Request, Guid Order)> SeedActionPeople()
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            LocalUser Person(string name, string role)
            {
                var worker = new Worker { EmployeeNo = name, FirstName = name, LastName = "Fixture", Status = "active", LocationId = branch };
                var account = new LocalUser { Email = name + "@example.test", NormalizedEmail = name.ToUpperInvariant() + "@EXAMPLE.TEST",
                    DisplayName = name + " only", PasswordHash = "fixture-only", RolesCsv = role, WorkerId = worker.Id };
                core.Workers.Add(worker); core.LocalUsers.Add(account); return account;
            }
            core.TenantRoleAssignments.AddRange(
                new TenantRoleAssignment { RoleKey = "pr_lookup", RoleName = "PR only fixture", Active = true,
                    PermissionsCsv = "procurement-access,procurement-approval-decide" },
                new TenantRoleAssignment { RoleKey = "po_lookup", RoleName = "PO only fixture", Active = true,
                    PermissionsCsv = "procurement-access,procurement-po-approve" });
            var requestPerson = Person("PR", "pr_lookup"); var orderPerson = Person("PO", "po_lookup");
            await core.SaveChangesAsync(); return (requestPerson.Id, orderPerson.Id);
        }
        public static async Task<Fixture> Start()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseTestServer();
            var database = Guid.NewGuid().ToString();
            builder.Services.AddScoped<ITenantAccessor>(_ => new FixedTenantAccessor("tenant-a"));
            builder.Services.AddDbContext<HrmDbContext>(o => o.UseInMemoryDatabase("authority-core-" + database));
            builder.Services.AddScoped<ProcurementRequestScope>();
            builder.Services.AddScoped<IProcurementScope>(s => s.GetRequiredService<ProcurementRequestScope>());
            builder.Services.AddDbContext<ProcurementDbContext>(o => o.UseInMemoryDatabase("authority-proc-" + database));
            builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions,
                ProcurementRequestHttpTests.TestAuthenticationHandler>("test", _ => { });
            builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
            var app = builder.Build();
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            app.UseMiddleware<ProcurementScopeMiddleware>();
            ProcurementSpendingAuthorityRoutes.MapRoutes(app);
            var company = new LegalEntity { Code = "AUTH", RegisteredName = "Authority test company", Currency = "ZMW" };
            var branch = new WorkLocation { Code = "A", Name = "A", LegalEntityId = company.Id };
            var other = new WorkLocation { Code = "B", Name = "B", LegalEntityId = company.Id };
            var worker = new Worker { EmployeeNo = "AUTH", FirstName = "Authority", LastName = "Owner",
                Status = "active", LocationId = branch.Id };
            var user = new LocalUser { Email = "authority@example.test", NormalizedEmail = "AUTHORITY@EXAMPLE.TEST",
                DisplayName = "Authority owner", PasswordHash = "test-placeholder-only", RolesCsv = "erp_admin", WorkerId = worker.Id };
            Guid grantId;
            using (var services = app.Services.CreateScope())
            {
                var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
                core.LegalEntities.Add(company); core.WorkLocations.AddRange(branch, other);
                core.Workers.Add(worker); core.LocalUsers.Add(user); await core.SaveChangesAsync();
                var scope = services.ServiceProvider.GetRequiredService<ProcurementRequestScope>();
                scope.Set("tenant-a", company.Id, user.Id.ToString("D"), null, null, false, "authority-seed");
                var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
                var actor = new SpendingAuthorityConfigurationActor("tenant-a", company.Id, user.Id.ToString("D"), true,
                    new HashSet<string>([SpendingAuthorityConfiguration.ConfigurePermission]));
                var grant = SpendingAuthorityConfiguration.CreateDraft("AUTH-EXISTING", user.Id.ToString("D"),
                    new("ZMW", "EstimatedLineBase", BranchRef: other.Id), actor, DateTimeOffset.UtcNow, []);
                db.AddScoped(grant); await db.SaveChangesAsync(); grantId = grant.Id;
            }
            await app.StartAsync();
            return new(app, company.Id, user.Id, branch.Id, other.Id, grantId);
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await app.DisposeAsync(); }
    }
}
