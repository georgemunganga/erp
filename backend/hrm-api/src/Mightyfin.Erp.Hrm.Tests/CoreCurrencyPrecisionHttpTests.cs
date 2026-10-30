using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreCurrencyPrecisionHttpTests
{
    [Fact]
    public async Task Scoped_workerless_read_and_original_actor_recovery_do_not_widen_company_access()
    {
        await using var f = await Fixture.Start();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Get(null, f.CompanyA)).StatusCode);
        using var own = await f.Get(f.Reader, f.CompanyA);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var list = await own.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("ZMW", list.GetProperty("companyCurrency").GetString());
        Assert.False(list.GetProperty("canConfigure").GetBoolean());
        Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Get(f.Reader, f.CompanyB)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.GetDetail(f.Reader, f.CompanyA, f.UsdVersion)).StatusCode);
        using var admin = await f.Get(f.Editor, f.CompanyA);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        var adminList = await admin.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(adminList.GetProperty("canConfigure").GetBoolean());
        Assert.Equal(2, adminList.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.Created, (await f.Recover(f.Editor, f.CompanyA, seeded: true)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Recover(f.OtherEditor, f.CompanyA, seeded: true)).StatusCode);
    }

    [Fact]
    public async Task Publish_requires_current_explicit_configure_grant_and_replays_original_response()
    {
        await using var f = await Fixture.Start();
        var request = new CoreCurrencyPrecisionRoutes.PublishInput(f.CompanyA, "ZMW", 1, 2,
            "ToEven", "reviewed-ZMW-precision", "New reviewed revision");
        using var denied = await f.Publish(f.Reader, request);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var first = await f.Publish(f.Editor, request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var original = await first.Content.ReadAsStringAsync();
        using var replay = await f.Publish(f.Editor, request);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(original, await replay.Content.ReadAsStringAsync());
        using var changed = await f.Publish(f.Editor, request with { MinorUnits = 3 });
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using var recovered = await f.Recover(f.Editor, f.CompanyA);
        Assert.Equal(original, await recovered.Content.ReadAsStringAsync());
        Assert.Equal(2, await f.CountVersions("ZMW"));
    }

    [Fact]
    public async Task Published_calculation_fields_and_command_receipts_are_immutable()
    {
        await using var f = await Fixture.Start();
        await using var db = f.Context();
        var version = await db.CurrencyPrecisionVersions.SingleAsync(x => x.CurrencyCode == "ZMW");
        version.MinorUnits = 4;
        Assert.Throws<DomainException>(() => db.SaveChanges());
        db.Entry(version).State = EntityState.Detached;
        var receipt = await db.CurrencyPrecisionCommandReceipts.SingleAsync();
        receipt.ResponseJson = "{}";
        Assert.Throws<DomainException>(() => db.SaveChanges());
    }

    [Fact]
    public async Task Retiring_latest_revision_does_not_reactivate_an_older_reference()
    {
        await using var f = await Fixture.Start();
        using var retired = await f.Retire(f.Editor, f.ZmwVersion, f.CompanyA, expectedRevision: 1);
        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        var body = await retired.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(body.GetProperty("current").GetBoolean());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, body.GetProperty("retiredAtUtc").ValueKind);
        await using var db = f.Context();
        Assert.NotNull((await db.CurrencyPrecisionVersions.SingleAsync(x => x.Id == f.ZmwVersion)).RetirementReason);
        using var listResponse = await f.Get(f.Editor, f.CompanyA);
        var list = await listResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(list.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == f.ZmwVersion)
            .GetProperty("current").GetBoolean());
        using var stale = await f.Retire(f.Editor, f.ZmwVersion, f.CompanyA,
            expectedRevision: 2, differentKey: true);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Retained_publish_response_remains_exact_after_retirement_but_rehashed_wrong_body_is_denied()
    {
        await using var f = await Fixture.Start();
        var request = new CoreCurrencyPrecisionRoutes.PublishInput(f.CompanyA, "ZMW", 1, 2,
            "ToEven", "reviewed-ZMW-precision", "New reviewed revision");
        using var first = await f.Publish(f.Editor, request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var original = await first.Content.ReadAsStringAsync();
        var id = (await first.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        using var retired = await f.Retire(f.Editor, id, f.CompanyA, expectedRevision: 2,
            differentKey: true);
        Assert.Equal(HttpStatusCode.OK, retired.StatusCode);
        var originalRetire = await retired.Content.ReadAsStringAsync();
        using var retiredReplay = await f.Retire(f.Editor, id, f.CompanyA, expectedRevision: 2,
            differentKey: true);
        Assert.Equal(originalRetire, await retiredReplay.Content.ReadAsStringAsync());
        using var retiredRecovery = await f.Recover(f.Editor, f.CompanyA,
            operation: "Retire", key: Guid.Parse("33333333-3333-4333-8333-333333333333"));
        Assert.Equal(originalRetire, await retiredRecovery.Content.ReadAsStringAsync());
        using var replay = await f.Publish(f.Editor, request);
        Assert.Equal(original, await replay.Content.ReadAsStringAsync());
        using var recovered = await f.Recover(f.Editor, f.CompanyA);
        Assert.Equal(original, await recovered.Content.ReadAsStringAsync());

        await using var db = f.Context();
        var receipt = await db.CurrencyPrecisionCommandReceipts.SingleAsync(x => x.VersionId == id
            && x.Operation == "Publish");
        var version = await db.CurrencyPrecisionVersions.SingleAsync(x => x.Id == id);
        var wrong = original.Replace("\"current\":true", "\"current\":false", StringComparison.Ordinal);
        Assert.NotEqual(original, wrong);
        receipt.ResponseJson = wrong;
        receipt.ResponseSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(wrong))).ToLowerInvariant();
        Assert.False(CoreCurrencyPrecisionRoutes.RetainedResponseMatches(receipt, version, f.CompanyA));
        receipt.ResponseJson = original;
        receipt.ResponseSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(original))).ToLowerInvariant();
        receipt.VersionId = f.UsdVersion;
        Assert.False(CoreCurrencyPrecisionRoutes.RetainedResponseMatches(receipt, version, f.CompanyA));
    }

    [Fact]
    public async Task Disabled_publication_preserves_history_and_original_key_reads()
    {
        await using var f = await Fixture.Start(publishEnabled: false);
        Assert.Equal(HttpStatusCode.OK, (await f.Get(f.Editor, f.CompanyA)).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await f.Recover(f.Editor, f.CompanyA, seeded: true)).StatusCode);
        using var blocked = await f.Publish(f.Editor, new(f.CompanyA, "ZMW", 1,
            2, "ToEven", "reviewed", "new revision"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, blocked.StatusCode);
        Assert.Equal(1, await f.CountVersions("ZMW"));
    }

    private sealed class Fixture(WebApplication app, HrmDbContext anchor, string store,
        InMemoryDatabaseRoot root, Guid reader, Guid editor, Guid otherEditor,
        Guid companyA, Guid companyB, Guid zmwVersion, Guid usdVersion) : IAsyncDisposable
    {
        private readonly HttpClient client = app.GetTestClient();
        private readonly string _store = store;
        private readonly InMemoryDatabaseRoot _root = root;
        internal Guid Reader => reader;
        internal Guid Editor => editor;
        internal Guid OtherEditor => otherEditor;
        internal Guid CompanyA => companyA;
        internal Guid CompanyB => companyB;
        internal Guid ZmwVersion => zmwVersion;
        internal Guid UsdVersion => usdVersion;
        private static readonly Guid Key = Guid.Parse("11111111-1111-4111-8111-111111111111");
        private static readonly Guid SeedKey = Guid.Parse("22222222-2222-4222-8222-222222222222");
        private static readonly Guid StaleKey = Guid.Parse("33333333-3333-4333-8333-333333333333");

        internal HrmDbContext Context() => new(new DbContextOptionsBuilder<HrmDbContext>()
            .UseInMemoryDatabase(_store, _root)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options,
            new FixedTenantAccessor("tenant-a"));
        internal async Task<int> CountVersions(string code)
        {
            await using var db = Context();
            return await db.CurrencyPrecisionVersions.CountAsync(x => x.CurrencyCode == code);
        }
        internal Task<HttpResponseMessage> Get(Guid? actor, Guid company)
            => Send(HttpMethod.Get, $"/api/core/v1/currency-precisions?companyId={company:D}", actor);
        internal Task<HttpResponseMessage> GetDetail(Guid actor, Guid company, Guid id)
            => Send(HttpMethod.Get, $"/api/core/v1/currency-precisions/{id:D}?companyId={company:D}", actor);
        internal Task<HttpResponseMessage> Recover(Guid actor, Guid company, bool seeded = false,
            string operation = "Publish", Guid? key = null)
            => Send(HttpMethod.Get,
                $"/api/core/v1/currency-precisions/commands/{operation}/{(key ?? (seeded ? SeedKey : Key)):D}?companyId={company:D}", actor);
        internal Task<HttpResponseMessage> Publish(Guid actor, CoreCurrencyPrecisionRoutes.PublishInput input)
            => Send(HttpMethod.Post, "/api/core/v1/currency-precisions", actor, input);
        internal Task<HttpResponseMessage> Retire(Guid actor, Guid id, Guid company,
            int expectedRevision, bool differentKey = false)
            => Send(HttpMethod.Post, $"/api/core/v1/currency-precisions/{id:D}/retire", actor,
                new CoreCurrencyPrecisionRoutes.RetireInput(company, expectedRevision, "Reviewed retirement"),
                differentKey ? StaleKey : Key);
        private async Task<HttpResponseMessage> Send(HttpMethod method, string url, Guid? actor,
            object? body = null, Guid? commandKey = null)
        {
            using var request = new HttpRequestMessage(method, url);
            if (actor is { } id) request.Headers.Add("X-Test-Actor", id.ToString("D"));
            request.Headers.Add("Idempotency-Key", (commandKey ?? Key).ToString("D"));
            if (body is not null) request.Content = JsonContent.Create(body);
            var response = await client.SendAsync(request);
            Assert.True(response.StatusCode != HttpStatusCode.InternalServerError,
                await response.Content.ReadAsStringAsync());
            return response;
        }
        internal static async Task<Fixture> Start(bool publishEnabled = true)
        {
            var store = "core-currency-" + Guid.NewGuid().ToString("N");
            var root = new InMemoryDatabaseRoot();
            var options = new DbContextOptionsBuilder<HrmDbContext>()
                .UseInMemoryDatabase(store, root)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
            var anchor = new HrmDbContext(options, new FixedTenantAccessor("tenant-a"));
            var a = new LegalEntity { TenantId = "tenant-a", Code = "A", RegisteredName = "Alpha", Currency = "ZMW" };
            var b = new LegalEntity { TenantId = "tenant-a", Code = "B", RegisteredName = "Bravo", Currency = "USD" };
            var branch = new WorkLocation { TenantId = "tenant-a", LegalEntityId = a.Id, Code = "A1", Name = "Alpha branch" };
            var reader = Account("reader", "currency_viewer");
            var editor = Account("editor", "currency_editor");
            var other = Account("other", "currency_editor");
            var zmw = Version("ZMW"); var usd = Version("USD");
            var response = System.Text.Json.JsonSerializer.Serialize(new CoreCurrencyPrecisionRoutes.VersionView(
                zmw.Id, zmw.CurrencyCode, zmw.Revision, zmw.MinorUnits, zmw.Rounding,
                zmw.SourceReference, zmw.PublishedAtUtc, null, zmw.PayloadSha256, true,
                null, SeedKey, "Publish"), new System.Text.Json.JsonSerializerOptions(
                System.Text.Json.JsonSerializerDefaults.Web));
            anchor.LegalEntities.AddRange(a, b); anchor.WorkLocations.Add(branch);
            anchor.LocalUsers.AddRange(reader, editor, other);
            anchor.UserBranchAssignments.Add(new HrUserBranchAssignment {
                TenantId = "tenant-a", UserId = reader.Id, UserEmail = reader.Email, LocationId = branch.Id });
            anchor.TenantRoleAssignments.AddRange(
                new TenantRoleAssignment { TenantId = "tenant-a", RoleKey = "currency_viewer",
                    RoleName = "Currency reader", Category = "hrm",
                    PermissionsCsv = CoreCurrencyPrecisionPermissions.Read },
                new TenantRoleAssignment { TenantId = "tenant-a", RoleKey = "currency_editor",
                    RoleName = "Currency editor", Category = "procurement",
                    PermissionsCsv = CoreCurrencyPrecisionPermissions.Configure });
            anchor.CurrencyPrecisionVersions.AddRange(zmw, usd);
            anchor.CurrencyPrecisionCommandReceipts.Add(new CoreCurrencyPrecisionCommandReceipt {
                TenantId = "tenant-a", ActorPrincipalId = editor.Id, CompanyId = a.Id,
                VersionId = zmw.Id, Operation = "Publish", CommandKey = SeedKey,
                RequestSha256 = new string('0', 64), ResponseJson = response,
                ResponseSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(response))).ToLowerInvariant(),
                ResponseStatus = 201, CompletedAtUtc = DateTimeOffset.UtcNow });
            await anchor.SaveChangesAsync();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
            builder.Configuration["HRM:DefaultTenantId"] = "tenant-a";
            builder.Configuration["Core:CurrencyPrecision:ReadEnabled"] = "true";
            builder.Configuration["Core:CurrencyPrecision:PublishEnabled"] = publishEnabled ? "true" : "false";
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ITenantAccessor, PrincipalTenantAccessor>();
            builder.Services.AddDbContext<HrmDbContext>((_, db) => db.UseInMemoryDatabase(store, root)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddScoped<ICoreCurrencyPrecisionSchemaReadiness, TestCurrencySchemaAdmission>();
            builder.Services.AddAuthentication("currency-test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("currency-test", _ => { });
            builder.Services.AddAuthorization(CoreWorkspaceContextRoutes.AddPolicy);
            var app = builder.Build(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            CoreCurrencyPrecisionRoutes.MapRoutes(app);
            await app.StartAsync();
            return new(app, anchor, store, root, reader.Id, editor.Id, other.Id,
                a.Id, b.Id, zmw.Id, usd.Id);
        }
        private static LocalUser Account(string name, string role) => new() {
            TenantId = "tenant-a", Email = name + "@example.invalid",
            NormalizedEmail = name.ToUpperInvariant() + "@EXAMPLE.INVALID",
            DisplayName = name, PasswordHash = "synthetic-not-a-password", RolesCsv = role,
            IsActive = true };
        private static CoreCurrencyPrecisionVersion Version(string code)
        {
            var row = new CoreCurrencyPrecisionVersion {
                TenantId = "tenant-a", CurrencyCode = code, Revision = 1, MinorUnits = 2,
                Rounding = "ToEven", SourceReference = "reviewed", PublicationReason = "initial",
                PublishedAtUtc = DateTimeOffset.UtcNow.AddDays(-1) };
            var payload = System.Text.Json.JsonSerializer.Serialize(new { row.CurrencyCode, row.Revision,
                row.MinorUnits, row.Rounding, row.SourceReference, row.PublicationReason,
                row.PublishedAtUtc }, new System.Text.Json.JsonSerializerOptions(
                System.Text.Json.JsonSerializerDefaults.Web));
            row.PayloadSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
            return row;
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.DisposeAsync(); await anchor.DisposeAsync(); }
    }

    // Only transport/domain behavior is under test here; native admission has
    // its own PostgreSQL rehearsal. Production registers the real inspector.
    private sealed class TestCurrencySchemaAdmission : ICoreCurrencyPrecisionSchemaReadiness
    {
        public Task<bool> IsReadyAsync(bool write, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-Actor"], out var actor))
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, actor.ToString("D")),
                new Claim("tenant", "tenant-a") };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, "currency-test")), "currency-test")));
        }
    }
}
