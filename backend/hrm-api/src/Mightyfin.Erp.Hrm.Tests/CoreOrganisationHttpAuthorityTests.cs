using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Branding;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using HrmRoutes = Mightyfin.Erp.Hrm.Api.Routing.Routes;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Real mapped routes, services, role/scope middleware and relational query filters.
/// Authentication reuses the existing synthetic TestServer handler; this is not a JWT/cookie or PostgreSQL gate.</summary>
public sealed class CoreOrganisationHttpAuthorityTests
{
    [Fact]
    public async Task ErpOnlyAdministratorWithoutWorker_UsesRealCoreRoutes_WithoutHrmAuthority()
    {
        await using var fixture = await Fixture.Start();
        var list = await fixture.Send(HttpMethod.Get, "/api/hrm/admin/legal-entities");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Single((await Json(list)).GetProperty("items").EnumerateArray());
        var created = await fixture.Send(HttpMethod.Post, "/api/hrm/admin/legal-entities",
            new { code = "SECOND", registeredName = "Second company", currency = " usd ", countryCode = " zm " }, selected: fixture.Company);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var second = (await Json(created)).GetProperty("id").GetGuid();
        // Core organization administration is tenant-wide. Selecting A does not revoke legitimate authority over B.
        var updated = await fixture.Send(HttpMethod.Patch, $"/api/hrm/admin/legal-entities/{second}",
            new { registeredName = "Saved profile", currency = " eur " }, selected: fixture.Company);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("EUR", (await Json(updated)).GetProperty("currency").GetString());
        var department = await fixture.Send(HttpMethod.Post, "/api/hrm/admin/org-units",
            new { code = "OPS", name = "Operations", legalEntityId = second, costCentreRef = "CC-1" });
        Assert.Equal(HttpStatusCode.Created, department.StatusCode);
        var unit = (await Json(department)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await fixture.Send(HttpMethod.Patch, $"/api/hrm/admin/org-units/{unit}", new { name = "Shared Operations" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Send(HttpMethod.Get, "/api/hrm/admin/org-units/tree")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Send(HttpMethod.Get, "/api/hrm/admin/org-units/entity-tree")).StatusCode);
        var before = await fixture.Footprint();
        foreach (var path in new[] { "/api/hrm/admin/calendars", "/api/hrm/workers", "/api/hrm/payroll/components" })
            await Error(await fixture.Send(HttpMethod.Get, path), HttpStatusCode.Forbidden, "forbidden");
        await Error(await fixture.Send(HttpMethod.Post, "/api/hrm/admin/calendars",
            new { name = "Denied calendar", legalEntityId = fixture.Company }), HttpStatusCode.Forbidden, "forbidden");
        await Error(await fixture.Send(HttpMethod.Post, "/api/hrm/workers",
            new { employeeNo = "DENIED", firstName = "Denied", lastName = "Worker", workerType = "employee" }), HttpStatusCode.Forbidden, "forbidden");
        await Error(await fixture.Send(HttpMethod.Post, "/api/hrm/payroll/components",
            new { code = "denied", name = "Denied component", componentType = "earning", calculationBasis = "fixed" }), HttpStatusCode.Forbidden, "forbidden");
        Assert.Equal(before, await fixture.Footprint());
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
        Assert.Empty(await db.LocalUsers.ToListAsync());
        Assert.Empty(await db.Workers.ToListAsync()); // No user-to-worker link or workforce role is fabricated for Core access.
        Assert.Contains(await db.AuditEntries.ToListAsync(), x => x.ActorSubjectId == fixture.Actor.ToString() && x.EntityType == nameof(LegalEntity));
    }

    [Fact]
    public async Task EmployeeAndAnonymousCannotMutateCoreMasters_NoEffects()
    {
        await using var fixture = await Fixture.Start();
        var before = await fixture.Footprint();
        await Error(await fixture.Send(HttpMethod.Post, "/api/hrm/admin/legal-entities",
            new { code = "DENIED", registeredName = "Denied company" }, role: "employee"), HttpStatusCode.Forbidden, "forbidden");
        await Error(await fixture.Send(HttpMethod.Patch, $"/api/hrm/admin/legal-entities/{fixture.Company}",
            new { currency = "USD" }, role: "employee"), HttpStatusCode.Forbidden, "forbidden");
        await Error(await fixture.Send(HttpMethod.Post, "/api/hrm/admin/org-units",
            new { code = "DENIED", name = "Denied department", legalEntityId = fixture.Company }, role: "employee"), HttpStatusCode.Forbidden, "forbidden");
        using var anonymous = new HttpRequestMessage(HttpMethod.Get, "/api/hrm/admin/legal-entities");
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.SendAsync(anonymous)).StatusCode);
        Assert.Equal(before, await fixture.Footprint());
    }

    [Fact]
    public async Task ForeignTenantAndForgedCompanyContext_NeverExposeOrMutateForeignMasters()
    {
        await using var fixture = await Fixture.Start();
        var before = await fixture.Footprint();
        // Existing ShellContextMiddleware ignores an unknown entity header for an unconfined administrator;
        // the tenant-filtered service still denies the requested foreign record. This is not a promised header-only 403.
        await Error(await fixture.Send(HttpMethod.Get, $"/api/hrm/admin/legal-entities/{fixture.ForeignCompany}", selected: fixture.ForeignCompany),
            HttpStatusCode.UnprocessableEntity, "legal-entity-not-found");
        await Error(await fixture.Send(HttpMethod.Patch, $"/api/hrm/admin/legal-entities/{fixture.ForeignCompany}",
            new { registeredName = "Must not change", currency = "USD" }, selected: fixture.ForeignCompany), HttpStatusCode.UnprocessableEntity, "legal-entity-not-found");
        await Error(await fixture.Send(HttpMethod.Post, "/api/hrm/admin/org-units",
            new { code = "FOREIGN", name = "Denied", legalEntityId = fixture.ForeignCompany }, selected: fixture.ForeignCompany), HttpStatusCode.UnprocessableEntity, "legal-entity-not-found");
        var list = await fixture.Send(HttpMethod.Get, "/api/hrm/admin/legal-entities", selected: fixture.ForeignCompany);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var only = Assert.Single((await Json(list)).GetProperty("items").EnumerateArray());
        Assert.Equal(fixture.Company, only.GetProperty("id").GetGuid());
        Assert.Equal(before, await fixture.Footprint());
    }

    [Fact]
    public async Task ManagerAssignmentAndMalformedCurrency_DenyBeforeTrackedMutation()
    {
        await using var fixture = await Fixture.Start();
        var created = await fixture.Send(HttpMethod.Post, "/api/hrm/admin/org-units",
            new { code = "OPS", name = "Original department", legalEntityId = fixture.Company });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var unit = (await Json(created)).GetProperty("id").GetGuid();
        var before = await fixture.Footprint();
        await Error(await fixture.Send(HttpMethod.Patch, $"/api/hrm/admin/org-units/{unit}",
            new { name = "Must not change", managerId = Guid.NewGuid() }), HttpStatusCode.Forbidden, "forbidden");
        await Error(await fixture.Send(HttpMethod.Post, "/api/hrm/admin/org-units",
            new { code = "DENIED", name = "Denied", legalEntityId = fixture.Company, managerId = Guid.NewGuid() }), HttpStatusCode.Forbidden, "forbidden");
        await Error(await fixture.Send(HttpMethod.Patch, $"/api/hrm/admin/legal-entities/{fixture.Company}",
            new { registeredName = "Must not change", currency = "12A" }), HttpStatusCode.UnprocessableEntity, "currency-invalid");
        await Error(await fixture.Send(HttpMethod.Post, "/api/hrm/admin/legal-entities",
            new { code = "BAD", registeredName = "Denied", currency = "ZMWX" }), HttpStatusCode.UnprocessableEntity, "currency-invalid");
        Assert.Equal(before, await fixture.Footprint());
    }

    [Fact]
    public async Task ErpAdminWithTimesheetRole_IsNotTimesheetOnly_ButStillCannotReadPayroll()
    {
        await using var fixture = await Fixture.Start();
        // Existing handler carries one role claim; WorkerPrincipal's existing JSON-array role parsing is unchanged.
        const string roles = "[\"erp_admin\",\"timesheet_operator\"]";
        Assert.Equal(HttpStatusCode.OK, (await fixture.Send(HttpMethod.Get, "/api/hrm/admin/legal-entities", role: roles)).StatusCode);
        await Error(await fixture.Send(HttpMethod.Get, "/api/hrm/payroll/components", role: roles), HttpStatusCode.Forbidden, "forbidden");
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await Json(response)).GetProperty("code").GetString());
    }

    private sealed class Fixture(WebApplication app, HrmDbContext anchor, Guid company, Guid foreignCompany) : IAsyncDisposable
    {
        public WebApplication App => app;
        public HttpClient Client { get; } = app.GetTestClient();
        public Guid Actor { get; } = Guid.NewGuid();
        public Guid Company => company;
        public Guid ForeignCompany => foreignCompany;
        public async Task<HttpResponseMessage> Send(HttpMethod method, string path, object? body = null, string role = "erp_admin", Guid? selected = null)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Add("X-Test-User", Actor.ToString());
            request.Headers.Add("X-Test-Role", role);
            request.Headers.Add("X-Shell-Entity", (selected ?? company).ToString());
            if (body is not null) request.Content = JsonContent.Create(body);
            return await Client.SendAsync(request);
        }
        public async Task<string> Footprint()
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
            return JsonSerializer.Serialize(new {
                companies = await db.LegalEntities.IgnoreQueryFilters().OrderBy(x => x.Id).Select(x => new { x.Id, x.TenantId, x.RegisteredName, x.Currency }).ToListAsync(),
                units = await db.OrgUnits.IgnoreQueryFilters().OrderBy(x => x.Id).Select(x => new { x.Id, x.TenantId, x.Name, x.LegalEntityId, x.ManagerId, x.Status }).ToListAsync(),
                locations = await db.WorkLocations.IgnoreQueryFilters().CountAsync(),
                workers = await db.Workers.IgnoreQueryFilters().CountAsync(),
                calendars = await db.WorkCalendars.IgnoreQueryFilters().CountAsync(),
                payroll = await db.SalaryComponents.IgnoreQueryFilters().CountAsync(),
                audit = await db.AuditEntries.IgnoreQueryFilters().CountAsync(),
            });
        }
        public static async Task<Fixture> Start()
        {
            var anchor = TestDbContextFactory.Create("tenant-a");
            var company = new LegalEntity { Code = "OWN", RegisteredName = "Owned company", Currency = "ZMW", CountryCode = "ZM", IsDefault = true };
            anchor.LegalEntities.Add(company);
            await anchor.SaveChangesAsync();
            var options = new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(anchor.Database.GetDbConnection()).Options;
            await using var foreign = new HrmDbContext(options, new FixedTenantAccessor("tenant-b"));
            var other = new LegalEntity { Code = "FOREIGN", RegisteredName = "Foreign company", Currency = "USD", CountryCode = "ZM" };
            foreign.LegalEntities.Add(other);
            await foreign.SaveChangesAsync();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseTestServer();
            builder.Configuration["HRM:DefaultTenantId"] = "tenant-a";
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ITenantAccessor, PrincipalTenantAccessor>();
            builder.Services.AddScoped<IAuthzService, AuthzServiceImpl>();
            builder.Services.AddScoped<AuditInterceptor>();
            builder.Services.AddScoped<ShellContext>();
            builder.Services.AddDbContext<HrmDbContext>((services, db) => db.UseSqlite(anchor.Database.GetDbConnection()).AddInterceptors(services.GetRequiredService<AuditInterceptor>()));
            builder.Services.AddScoped<IConfigRepository, ConfigRepository>();
            builder.Services.AddScoped<IConfigAdminService, ConfigAdminServiceImpl>();
            builder.Services.AddScoped<IConfigService, ConfigServiceImpl>();
            builder.Services.AddScoped<IJobsAdminService, JobsAdminServiceImpl>();
            builder.Services.AddScoped<ICompanyBrandingService, CompanyBrandingService>();
            builder.Services.AddScoped<IWorkerRepository, WorkerRepository>();
            builder.Services.AddScoped<IIdProvider, IdProvider>();
            builder.Services.AddScoped<IWorkerService, WorkerServiceImpl>();
            builder.Services.AddScoped<IWorkerImportService, WorkerImportService>();
            builder.Services.AddScoped<IWorkerLifecycleService, WorkerLifecycleServiceImpl>();
            builder.Services.AddScoped<IPayrollRepository, PayrollRepository>();
            builder.Services.AddScoped<IPayslipDocumentService, PayslipDocumentServiceImpl>();
            builder.Services.AddScoped<IPayrollService, PayrollServiceImpl>();
            builder.Services.AddScoped<IPeriodOvertimeService, PeriodOvertimeService>();
            builder.Services.AddScoped<Mightyfin.Erp.Hrm.Application.Benefits.IPeriodBenefitService, PeriodBenefitService>();
            builder.Services.AddScoped<IPayrollReportService, PayrollReportServiceImpl>();
            builder.Services.AddScoped<IPayrollReportPdfRenderer, PayrollReportPdfRendererImpl>();
            builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, ProcurementRequestHttpTests.TestAuthenticationHandler>("test", _ => { });
            builder.Services.AddAuthorization(authorization => {
                authorization.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
                    .RequireAssertion(context => HrmStaffAccess.IsStaff(context.User.Claims)).Build();
            });
            var app = builder.Build();
            // Program.cs' domain error boundary is inline, not an injectable middleware. Mirror only its existing
            // relevant statuses here; this fixture does not claim coverage of the full production hosting pipeline.
            app.Use(async (http, next) => {
                try { await next(http); }
                catch (DomainException failure) {
                    http.Response.StatusCode = failure.Code switch { "forbidden" => 403, "unauthorized" => 401, "worker-not-found" => 404, _ => 422 };
                    await http.Response.WriteAsJsonAsync(new ApiError(failure.Code, failure.Message, []));
                }
            });
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<TimesheetAccessMiddleware>();
            app.UseMiddleware<ShellContextMiddleware>();
            HrmRoutes.RegisterConfig(app);
            HrmRoutes.RegisterWorkers(app);
            HrmRoutes.RegisterPayroll(app);
            await app.StartAsync();
            return new Fixture(app, anchor, company.Id, other.Id);
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await app.DisposeAsync(); await anchor.DisposeAsync(); }
    }
}
