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
