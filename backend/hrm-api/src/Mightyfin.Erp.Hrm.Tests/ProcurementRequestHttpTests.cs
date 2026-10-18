using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
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
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Exercises the mapped HTTP routes with the real Core scope middleware and Procurement query filters.</summary>
public sealed class ProcurementRequestHttpTests
{
    [Fact]
    public async Task Draft_request_persists_and_is_hidden_from_another_employee()
    {
        await using var fixture = await Fixture.Start();
        var client = fixture.Client;
        var create = new HttpRequestMessage(HttpMethod.Post, "/api/procurement/v1/requests")
        {
            Content = JsonContent.Create(Draft("Office laptops")),
        };
        create.Headers.Add("X-Test-User", fixture.Owner.ToString());
        var created = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var record = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = record.GetProperty("id").GetGuid();
        Assert.Equal("ZMW", record.GetProperty("currencyCode").GetString());
        Assert.Equal("Draft", record.GetProperty("status").GetString());
        Assert.Single(record.GetProperty("lines").EnumerateArray());

        var own = await Send(client, HttpMethod.Get, $"/api/procurement/v1/requests/{id}", fixture.Owner);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var other = await Send(client, HttpMethod.Get, $"/api/procurement/v1/requests/{id}", fixture.Other);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        var otherList = await Send(client, HttpMethod.Get, "/api/procurement/v1/requests", fixture.Other);
        Assert.Equal(0, (await otherList.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("count").GetInt32());

        var wrongCompany = await Send(client, HttpMethod.Get, $"/api/procurement/v1/requests/{id}",
            fixture.Owner, fixture.OtherCompany);
        Assert.Equal(HttpStatusCode.Forbidden, wrongCompany.StatusCode);
    }

    [Fact]
    public async Task Draft_write_requires_an_active_employee_and_action_permission()
    {
        await using var fixture = await Fixture.Start();
        var noSession = await fixture.Client.PostAsJsonAsync("/api/procurement/v1/requests", Draft("No session"));
        Assert.Equal(HttpStatusCode.Unauthorized, noSession.StatusCode);

        var noPermission = new HttpRequestMessage(HttpMethod.Post, "/api/procurement/v1/requests")
        {
            Content = JsonContent.Create(Draft("No permission")),
        };
        noPermission.Headers.Add("X-Test-User", fixture.Owner.ToString());
        noPermission.Headers.Add("X-Test-Role", "ap_processor");
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.SendAsync(noPermission)).StatusCode);

        var inactive = new HttpRequestMessage(HttpMethod.Post, "/api/procurement/v1/requests")
        {
            Content = JsonContent.Create(Draft("Inactive employee")),
        };
        inactive.Headers.Add("X-Test-User", fixture.Inactive.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.SendAsync(inactive)).StatusCode);
    }

    [Fact]
    public async Task Vendor_and_item_drafts_respect_their_distinct_record_and_action_scopes()
    {
        await using var fixture = await Fixture.Start();
        var vendor = new HttpRequestMessage(HttpMethod.Post, "/api/procurement/v1/vendors")
        {
            Content = JsonContent.Create(new { legalName = "Acme Zambia Ltd", displayName = "Acme",
                countryCode = "ZM" }),
        };
        vendor.Headers.Add("X-Test-User", fixture.Owner.ToString());
        var savedVendor = await fixture.Client.SendAsync(vendor);
        Assert.Equal(HttpStatusCode.Created, savedVendor.StatusCode);
        var vendorId = (await savedVendor.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/vendors/{vendorId}", fixture.Other)).StatusCode);

        var deniedItem = new HttpRequestMessage(HttpMethod.Post, "/api/procurement/v1/items")
        {
            Content = JsonContent.Create(new { code = "LAP-1", name = "Laptop", kind = "Goods",
                categoryCode = "IT", unitCode = "each" }),
        };
        deniedItem.Headers.Add("X-Test-User", fixture.Owner.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.SendAsync(deniedItem)).StatusCode);

        var buyerItem = new HttpRequestMessage(HttpMethod.Post, "/api/procurement/v1/items")
        {
            Content = JsonContent.Create(new { code = "LAP-1", name = "Laptop", kind = "Goods",
                categoryCode = "IT", unitCode = "each" }),
        };
        buyerItem.Headers.Add("X-Test-User", fixture.Owner.ToString());
        buyerItem.Headers.Add("X-Test-Role", "procurement_buyer");
        var savedItem = await fixture.Client.SendAsync(buyerItem);
        Assert.Equal(HttpStatusCode.Created, savedItem.StatusCode);
        var result = await savedItem.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Inactive", result.GetProperty("status").GetString());
        Assert.False(result.GetProperty("isPurchasable").GetBoolean());
    }

    [Fact]
    public async Task Request_edit_rejects_stale_versions_and_withdrawal_locks_the_record()
    {
        await using var fixture = await Fixture.Start();
        var create = new HttpRequestMessage(HttpMethod.Post, "/api/procurement/v1/requests")
        {
            Content = JsonContent.Create(Draft("Two laptops")),
        };
        create.Headers.Add("X-Test-User", fixture.Owner.ToString());
        var created = await fixture.Client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var first = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = first.GetProperty("id").GetGuid();
        var lineId = first.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var version = first.GetProperty("version").GetInt64();
        var edit = new ProcurementRequestRoutes.UpdateRequest(version, "Three laptops", null, null,
            null, null, null, null, null, null,
            [new ProcurementRequestRoutes.RequestLineInput(lineId, "Laptop", "each", 3, 100,
                null, null, null, "Goods", null, null, null, null, null, null)]);

        var saved = await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/requests/{id}",
            fixture.Owner, edit);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var latest = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Three laptops", latest.GetProperty("purpose").GetString());
        Assert.Equal(3, latest.GetProperty("lines")[0].GetProperty("requestedQuantity").GetDecimal());
        Assert.True(latest.GetProperty("version").GetInt64() > version);

        var stale = await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/requests/{id}",
            fixture.Owner, edit);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var withdrawn = await SendJson(fixture.Client, HttpMethod.Post,
            $"/api/procurement/v1/requests/{id}/withdraw", fixture.Owner,
            new ProcurementRequestRoutes.VersionCommand(latest.GetProperty("version").GetInt64()));
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        Assert.Equal("Withdrawn", (await withdrawn.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/requests/{id}",
                fixture.Owner, edit)).StatusCode);
    }

    [Fact]
    public async Task Planning_and_sourcing_drafts_are_separate_from_buying_authority()
    {
        await using var fixture = await Fixture.Start();
        var planInput = new ProcurementPlanInput("FY2026", null, null,
            [new ProcurementPlanLineInput(null, null, "Office chairs", "OFFICE", "each", 10,
                5_000, null, null)]);
        var deniedPlan = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/plans",
            fixture.Owner, planInput);
        Assert.Equal(HttpStatusCode.Forbidden, deniedPlan.StatusCode);
        var savedPlan = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/plans",
            fixture.Owner, planInput, "manager");
        Assert.Equal(HttpStatusCode.Created, savedPlan.StatusCode);
        var plan = await savedPlan.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", plan.GetProperty("status").GetString());
        Assert.Equal("ZMW", plan.GetProperty("currencyCode").GetString());
        var planId = plan.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/plans/{planId}",
                fixture.Other, role: "manager")).StatusCode);

        var sourceInput = new SourcingDraftInput("Office chairs", "RFQ", null,
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(8),
            [new SourcingLineInput(null, null, "Office chairs", "each", 10, "OFFICE", null)]);
        var deniedSource = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/sourcing",
            fixture.Owner, sourceInput, "manager");
        Assert.Equal(HttpStatusCode.Forbidden, deniedSource.StatusCode);
        var savedSource = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/sourcing",
            fixture.Owner, sourceInput, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, savedSource.StatusCode);
        var source = await savedSource.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", source.GetProperty("status").GetString());
        Assert.Equal("ZMW", source.GetProperty("currencyCode").GetString());
        var sourceId = source.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/sourcing/{sourceId}",
                fixture.Other, role: "procurement_buyer")).StatusCode);
    }

    [Fact]
    public async Task Request_list_counts_only_current_lines_after_a_draft_revision()
    {
        await using var fixture = await Fixture.Start();
        var twoLines = Draft("Mixed equipment") with
        {
            Lines =
            [
                new ProcurementRequestRoutes.RequestLineInput(null, "Laptop", "each", 1, 100,
                    null, null, null, "Goods", null, null, null, null, null, null),
                new ProcurementRequestRoutes.RequestLineInput(null, "Printer", "each", 1, 80,
                    null, null, null, "Goods", null, null, null, null, null, null),
            ],
        };
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/requests",
            fixture.Owner, twoLines);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var record = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = record.GetProperty("id").GetGuid();
        var keep = record.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var revised = new ProcurementRequestRoutes.UpdateRequest(record.GetProperty("version").GetInt64(),
            "Mixed equipment", null, null, null, null, null, null, null, null,
            [twoLines.Lines![0] with { Id = keep }]);
        Assert.Equal(HttpStatusCode.OK,
            (await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/requests/{id}",
                fixture.Owner, revised)).StatusCode);
        var list = await Send(fixture.Client, HttpMethod.Get, "/api/procurement/v1/requests", fixture.Owner);
        var summary = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("records")[0];
        Assert.Equal(1, summary.GetProperty("lineCount").GetInt32());
    }

    private static ProcurementRequestRoutes.NewRequest Draft(string purpose) => new(
        purpose, null, null, null, null, null, null, null, null,
        [new ProcurementRequestRoutes.RequestLineInput(null, "Laptop", "each", 2, 100,
            null, null, null, "Goods", null, null, null, null, null, null)]);

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path,
        Guid user, Guid? company = null, string? role = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Test-User", user.ToString());
        if (company.HasValue) request.Headers.Add("X-Shell-Entity", company.Value.ToString());
        if (role is not null) request.Headers.Add("X-Test-Role", role);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendJson<T>(HttpClient client, HttpMethod method, string path,
        Guid user, T body, string? role = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Test-User", user.ToString());
        if (role is not null) request.Headers.Add("X-Test-Role", role);
        return client.SendAsync(request);
    }

    private sealed class Fixture(WebApplication app, Guid owner, Guid other, Guid inactive, Guid otherCompany)
        : IAsyncDisposable
    {
        public HttpClient Client { get; } = app.GetTestClient();
        public Guid Owner { get; } = owner;
        public Guid Other { get; } = other;
        public Guid Inactive { get; } = inactive;
        public Guid OtherCompany { get; } = otherCompany;

        public static async Task<Fixture> Start()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseTestServer();
            var databaseId = Guid.NewGuid().ToString("N");
            builder.Services.AddScoped<ITenantAccessor>(_ => new FixedTenantAccessor("tenant-a"));
            builder.Services.AddDbContext<HrmDbContext>(options =>
                options.UseInMemoryDatabase("proc-core-" + databaseId));
            builder.Services.AddScoped<ProcurementRequestScope>();
            builder.Services.AddScoped<IProcurementScope>(sp => sp.GetRequiredService<ProcurementRequestScope>());
            builder.Services.AddDbContext<ProcurementDbContext>(options =>
                options.UseInMemoryDatabase("proc-drafts-" + databaseId));
            builder.Services.AddAuthentication("test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("test", _ => { });
            builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
            var app = builder.Build();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<ProcurementScopeMiddleware>();
            ProcurementRequestRoutes.Map(app);
            ProcurementVendorRoutes.MapRoutes(app);
            ProcurementItemEndpoints.MapRoutes(app);
            ProcurementPlanRoutes.MapRoutes(app);
            ProcurementSourcingDraftRoutes.MapRoutes(app);

            var company = new LegalEntity { Code = "A", RegisteredName = "Amizo Power", Currency = "ZMW" };
            var otherCompany = new LegalEntity { Code = "B", RegisteredName = "Other company", Currency = "USD" };
            var branch = new WorkLocation { Code = "A1", Name = "Lusaka", LegalEntityId = company.Id };
            var owner = Guid.NewGuid();
            var other = Guid.NewGuid();
            var inactive = Guid.NewGuid();
            using (var seed = app.Services.CreateScope())
            {
                var core = seed.ServiceProvider.GetRequiredService<HrmDbContext>();
                core.LegalEntities.AddRange(company, otherCompany);
                core.WorkLocations.Add(branch);
                core.Workers.AddRange(
                    new Worker { EmployeeNo = "PROC-1", FirstName = "Owner", LastName = "Employee",
                        SubjectId = owner.ToString(), Status = "active", LocationId = branch.Id },
                    new Worker { EmployeeNo = "PROC-2", FirstName = "Other", LastName = "Employee",
                        SubjectId = other.ToString(), Status = "active", LocationId = branch.Id },
                    new Worker { EmployeeNo = "PROC-3", FirstName = "Inactive", LastName = "Employee",
                        SubjectId = inactive.ToString(), Status = "terminated", LocationId = branch.Id });
                await core.SaveChangesAsync();
            }
            await app.StartAsync();
            return new Fixture(app, owner, other, inactive, otherCompany.Id);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    public sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-User"], out var user))
                return Task.FromResult(AuthenticateResult.NoResult());
            var role = Request.Headers["X-Test-Role"].FirstOrDefault() ?? "employee";
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant", "tenant-a"),
                new Claim("sub", user.ToString()),
                new Claim(ClaimTypes.Role, role),
            ], "test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "test")));
        }
    }
}
