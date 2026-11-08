using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using CoreFixture = Mightyfin.Erp.Hrm.Tests.CoreCompanyTeamCommandsTests.Fixture;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductCompanyTeamHttpTests
{
    [Fact]
    public async Task Directory_members_confirmed_changes_retries_and_revoke_use_the_real_Core_transaction_without_ERP_admission()
    {
        await using var f = await Fixture.Start();
        var target = await f.Core.Add("43", CoreCompanyRoles.Member);
        await f.SignIn();
        var directory = await f.Get("/companies");
        Assert.Equal(HttpStatusCode.OK, directory.StatusCode);
        var companies = (await Body(directory)).GetProperty("companies");
        Assert.Equal(f.Core.Company, Assert.Single(companies.EnumerateArray()).GetProperty("company_id").GetGuid());
        Assert.Equal(0, f.State.Reads);
        var members = await f.Get(f.MemberPath);
        Assert.Equal(HttpStatusCode.OK, members.StatusCode);
        var memberBody = await Body(members);
        Assert.Equal("owner", memberBody.GetProperty("actor_role").GetString());
        Assert.Equal(2, memberBody.GetProperty("members").GetArrayLength());
        Assert.DoesNotContain("tenant", memberBody.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", memberBody.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var command = f.Command(target.Member, "role-change", "administrator");
        var changed = await f.Post(command);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal("Changed", (await Body(changed)).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await f.Post(command)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Post(f.Command(target.Member, "revoke"))).StatusCode);
        var revoked = await f.Post(f.Command(target.Member, "revoke", revision: 2));
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        var result = await Body(revoked);
        Assert.False(result.GetProperty("membership_active").GetBoolean());
        Assert.Equal(3, result.GetProperty("revision").GetInt32());
        Assert.False(result.GetProperty("operational_access_granted").GetBoolean());
        var historical = await Body(await f.Post(command));
        Assert.Equal("Existing", historical.GetProperty("status").GetString());
        Assert.Equal(2, historical.GetProperty("revision").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Post(f.Command(target.Member, "role-change", "member", 3))).StatusCode);
        Assert.Equal("01arz3ndektsv4rrffq69g5fav", f.State.LastEnrollment);
        Assert.Equal(7, f.State.Reads); // member read and every command, including replay/denials
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.GetErp()).StatusCode);
        await f.Core.AssertEffects(2, 2);
    }

    [Fact]
    public async Task Last_owner_foreign_company_member_link_and_other_identity_are_denied_without_scope_inference()
    {
        await using var f = await Fixture.Start();
        var second = await f.Core.Create("other_http_company_request_0000000001");
        await using var inspect = f.Core.Context("inspect");
        var foreign = await inspect.CompanyMemberships.IgnoreQueryFilters().SingleAsync(x => x.LegalEntityId == second.CompanyId);
        var foreignLink = await inspect.ExternalIdentityLinks.IgnoreQueryFilters().SingleAsync(x => x.TenantId == foreign.TenantId);
        await f.SignIn();
        Assert.Equal(HttpStatusCode.Conflict, (await f.Post(f.Command(f.Core.Owner.Member, "revoke"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Post(f.Command(foreign.Id, "revoke"))).StatusCode);
        var command = f.Command(f.Core.Owner.Member, "revoke");
        command["link_id"] = foreignLink.Id.ToString("D");
        var reads = f.State.Reads;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Post(command)).StatusCode);
        Assert.Equal(reads, f.State.Reads);
        await f.SignIn("99");
        Assert.Empty((await Body(await f.Get("/companies"))).GetProperty("companies").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Get(f.MemberPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Post(f.Command(f.Core.Owner.Member, "revoke"))).StatusCode);
        Assert.Equal(reads, f.State.Reads);
        await f.Core.AssertEffects(0, 0);
    }

    [Theory]
    [InlineData("missing-cookie", 401)]
    [InlineData("ordinary-cookie", 401)]
    [InlineData("missing-token", 403)]
    [InlineData("forged-token", 403)]
    [InlineData("foreign-origin", 403)]
    [InlineData("non-json", 403)]
    public async Task Browser_authentication_and_CSRF_failures_cannot_reach_licensing_or_commands(string mode, int status)
    {
        await using var f = await Fixture.Start();
        await f.SignIn(ordinary: mode == "ordinary-cookie");
        var response = await f.Post(f.Command(f.Core.Owner.Member, "revoke"), mode);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(0, f.State.Reads);
        await f.Core.AssertEffects(0, 0);
    }

    [Theory]
    [InlineData("unconfirmed")]
    [InlineData("missing-confirmation")]
    [InlineData("string-confirmation")]
    [InlineData("tenant")]
    [InlineData("payer")]
    [InlineData("actor")]
    [InlineData("duplicate")]
    [InlineData("uppercase-guid")]
    [InlineData("empty-guid")]
    [InlineData("compact-guid")]
    [InlineData("zero-revision")]
    [InlineData("string-revision")]
    [InlineData("fraction-revision")]
    [InlineData("operation")]
    [InlineData("revoke-role")]
    [InlineData("invalid-role")]
    [InlineData("array")]
    [InlineData("invalid-json")]
    [InlineData("oversized")]
    public async Task Strict_confirmed_contract_rejects_ambiguous_or_browser_authority_fields_before_licensing(string mode)
    {
        await using var f = await Fixture.Start();
        await f.SignIn();
        var command = f.Command(f.Core.Owner.Member, "revoke");
        switch (mode)
        {
            case "unconfirmed": command["confirmed"] = false; break;
            case "missing-confirmation": command.Remove("confirmed"); break;
            case "string-confirmation": command["confirmed"] = "true"; break;
            case "tenant": command["tenant_id"] = f.Core.Tenant; break;
            case "payer": command["customer_id"] = "01arz3ndektsv4rrffq69g5faw"; break;
            case "actor": command["actor_subject"] = "42"; break;
            case "uppercase-guid": command["company_id"] = "ABCDEFAB-1234-4321-ABCD-123456789ABC"; break;
            case "empty-guid": command["link_id"] = Guid.Empty.ToString("D"); break;
            case "compact-guid": command["link_id"] = f.Core.Owner.Link.ToString("N"); break;
            case "zero-revision": command["expected_revision"] = 0; break;
            case "string-revision": command["expected_revision"] = "1"; break;
            case "fraction-revision": command["expected_revision"] = 1.5; break;
            case "operation": command["operation"] = "reactivate"; break;
            case "revoke-role": command["role"] = "owner"; break;
            case "invalid-role": command["operation"] = "role-change"; command["role"] = "OWNER"; break;
        }
        var json = JsonSerializer.Serialize(command);
        if (mode == "duplicate") json = json[..^1]+",\"confirmed\":true}";
        if (mode == "array") json = "[]";
        if (mode == "invalid-json") json = "{";
        if (mode == "oversized") json = new string(' ', 16385);
        var response = await f.PostRaw(json);
        Assert.Equal(mode == "oversized" ? 413 : 400, (int)response.StatusCode);
        Assert.Equal(0, f.State.Reads);
        await f.Core.AssertEffects(0, 0);
    }

    [Theory]
    [InlineData("denied", 403)]
    [InlineData("unavailable", 503)]
    [InlineData("expired", 403)]
    [InlineData("wrong-enrollment", 403)]
    public async Task Fresh_license_is_required_for_members_commands_and_successful_command_replays(string mode, int status)
    {
        await using var f = await Fixture.Start();
        var target = await f.Core.Add("43", CoreCompanyRoles.Member);
        await f.SignIn();
        var command = f.Command(target.Member, "role-change", "administrator");
        Assert.Equal(HttpStatusCode.OK, (await f.Post(command)).StatusCode);
        if (mode == "denied") f.State.Status = CommercialLicenseStatus.Denied;
        if (mode == "unavailable") f.State.Status = CommercialLicenseStatus.Unavailable;
        if (mode == "expired") { f.State.Lease = f.Core.License(); f.Core.Clock.Advance(); }
        if (mode == "wrong-enrollment") f.State.Lease = f.Core.License("01arz3ndektsv4rrffq69g5fax");
        Assert.Equal(status, (int)(await f.Get(f.MemberPath)).StatusCode);
        Assert.Equal(status, (int)(await f.Post(command)).StatusCode);
        await f.Core.AssertEffects(1, 1);
    }

    [Fact]
    public async Task Administrator_can_revoke_ordinary_members_but_cannot_assign_roles_or_revoke_privileged_members()
    {
        await using var f = await Fixture.Start();
        var admin = await f.Core.Add("43", CoreCompanyRoles.Administrator);
        var target = await f.Core.Add("44", CoreCompanyRoles.Member);
        var peer = await f.Core.Add("45", CoreCompanyRoles.Administrator);
        await f.SignIn(admin.Subject);
        f.ActorLink = admin.Link;
        Assert.Equal(HttpStatusCode.OK, (await f.Get(f.MemberPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Post(f.Command(target.Member, "role-change", "administrator"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Post(f.Command(peer.Member, "revoke"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Post(f.Command(f.Core.Owner.Member, "revoke"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Post(f.Command(target.Member, "revoke"))).StatusCode);
        await f.Core.AssertEffects(1, 1);
    }

    [Theory]
    [InlineData("role")]
    [InlineData("membership")]
    [InlineData("link")]
    [InlineData("account")]
    [InlineData("locked")]
    [InlineData("company")]
    public async Task Current_Core_access_is_required_even_when_replaying_a_successful_command(string mode)
    {
        await using var f = await Fixture.Start();
        var target = await f.Core.Add("43", CoreCompanyRoles.Member);
        await f.SignIn();
        var command = f.Command(target.Member, "role-change", "administrator");
        Assert.Equal(HttpStatusCode.OK, (await f.Post(command)).StatusCode);
        await using (var db = f.Core.Context())
        {
            if (mode is "role" or "membership")
            {
                var row = await db.CompanyMemberships.SingleAsync(x => x.Id == f.Core.Owner.Member);
                if (mode == "role") row.Role = CoreCompanyRoles.Member;
                else row.IsActive = false;
                row.Revision++;
            }
            if (mode == "link") (await db.ExternalIdentityLinks.SingleAsync(x => x.Id == f.Core.Owner.Link)).IsActive = false;
            if (mode is "account" or "locked")
            {
                var user = await db.LocalUsers.SingleAsync(x => x.Id == f.Core.Owner.User);
                if (mode == "account") user.IsActive = false;
                else user.LockedUntil = f.Core.Clock.GetUtcNow().AddMinutes(5);
            }
            if (mode == "company") (await db.LegalEntities.SingleAsync(x => x.Id == f.Core.Company)).IsArchived = true;
            await db.SaveChangesAsync();
        }
        var reads = f.State.Reads;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Post(command)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Get(f.MemberPath)).StatusCode);
        Assert.Empty((await Body(await f.Get("/companies"))).GetProperty("companies").EnumerateArray());
        Assert.Equal(reads, f.State.Reads);
        await f.Core.AssertEffects(1, 1);
    }

    [Fact]
    public async Task Company_member_and_query_pagination_are_bounded_and_do_not_use_cursors_as_authority()
    {
        await using var f = await Fixture.Start();
        for (var i = 0; i < 23; i++) await f.Core.Add((100+i).ToString(), CoreCompanyRoles.Member);
        await f.SignIn();
        var first = await Body(await f.Get(f.MemberPath));
        Assert.Equal(20, first.GetProperty("members").GetArrayLength());
        var cursor = first.GetProperty("next_cursor").GetString();
        var second = await Body(await f.Get(f.MemberPath+"&after="+cursor));
        Assert.Equal(4, second.GetProperty("members").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("next_cursor").ValueKind);
        var ids = first.GetProperty("members").EnumerateArray().Concat(second.GetProperty("members").EnumerateArray())
            .Select(x => x.GetProperty("membership_id").GetGuid()).ToArray();
        Assert.Equal(24, ids.Distinct().Count());
        foreach (var path in new[] { "/companies?after=bad", "/companies?tenant_id=foreign", "/companies?after="+Guid.Empty,
            f.MemberPath+"&link_id="+f.ActorLink, f.MemberPath+"&after=", f.MemberPath+"&payer=foreign" })
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Get(path)).StatusCode);
        await f.Core.AssertEffects(0, 0);
    }

    [Fact]
    public async Task No_op_and_changed_request_replays_preserve_one_receipt_and_do_not_emit_false_events()
    {
        await using var f = await Fixture.Start();
        await f.SignIn();
        var command = f.Command(f.Core.Owner.Member, "role-change", "owner");
        Assert.Equal("Unchanged", (await Body(await f.Post(command))).GetProperty("status").GetString());
        Assert.Equal("Existing", (await Body(await f.Post(command))).GetProperty("status").GetString());
        command["role"] = "member";
        Assert.Equal(HttpStatusCode.Conflict, (await f.Post(command)).StatusCode);
        await f.Core.AssertEffects(0, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actor_revocation_while_fetching_license_is_rechecked_before_member_reads_and_commands(bool write)
    {
        await using var f = await Fixture.Start();
        await f.SignIn();
        f.State.BeforeReturn = async () =>
        {
            await using var db = f.Core.Context();
            var member = await db.CompanyMemberships.SingleAsync(x => x.Id == f.Core.Owner.Member);
            member.Role = CoreCompanyRoles.Member;
            member.Revision++;
            await db.SaveChangesAsync();
        };
        var response = write ? await f.Post(f.Command(f.Core.Owner.Member, "role-change", "owner")) : await f.Get(f.MemberPath);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, f.State.Reads);
        await f.Core.AssertEffects(0, 0);
    }

    private static async Task<JsonElement> Body(HttpResponseMessage response)
    { using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return json.RootElement.Clone(); }

    [Fact]
    public async Task Company_directory_paginates_all_explicit_managed_companies_without_granting_license_or_member_access()
    {
        await using var f = await Fixture.Start();
        await f.Core.Add("43", CoreCompanyRoles.Member);
        var clock = f.Core.Clock;
        const string enrollment = "01arz3ndektsv4rrffq69g5fav";
        for (var i = 0; i < 21; i++)
        {
            var license = new CommercialLicenseLease(enrollment, [], 100, clock.GetUtcNow().AddMinutes(1),
                clock, clock.GetTimestamp(), TimeSpan.FromMinutes(1));
            var created = await new CoreWorkspaceProvisioner(tenant => f.Core.Context(tenant), clock).CreateAsync(
                new("42", "391bdeaa-87b3-44b3-8e64-2110dcc1a564", "01arz3ndektsv4rrffq69g5faw", enrollment,
                    100, clock.GetUtcNow().AddMinutes(1), clock, clock.GetTimestamp(), TimeSpan.FromMinutes(1)),
                license, "team_http_directory_company_request_"+i.ToString("D8"), "Managed company "+i);
            Assert.Equal(WorkspaceCreateStatus.Created, created.Status);
        }
        await f.SignIn();
        var first = await Body(await f.Get("/companies"));
        Assert.Equal(20, first.GetProperty("companies").GetArrayLength());
        Assert.True(first.GetProperty("license_check_required").GetBoolean());
        Assert.False(first.GetProperty("operational_access_granted").GetBoolean());
        var second = await Body(await f.Get("/companies?after="+first.GetProperty("next_cursor").GetString()));
        Assert.Equal(2, second.GetProperty("companies").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("next_cursor").ValueKind);
        var ids = first.GetProperty("companies").EnumerateArray().Concat(second.GetProperty("companies").EnumerateArray())
            .Select(x => x.GetProperty("company_id").GetGuid()).ToArray();
        Assert.Equal(22, ids.Distinct().Count());
        Assert.DoesNotContain("enrollment", first.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tenant", first.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, f.State.Reads);
        await f.SignIn("43");
        Assert.Empty((await Body(await f.Get("/companies"))).GetProperty("companies").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Get(f.MemberPath)).StatusCode);
        Assert.Equal(0, f.State.Reads);
    }

    internal sealed class LicenseState(CoreFixture core)
    {
        internal int Reads;
        internal string? LastEnrollment;
        internal CommercialLicenseStatus Status = CommercialLicenseStatus.Authorized;
        internal CommercialLicenseLease? Lease;
        internal Func<Task>? BeforeReturn;
        internal async Task<CommercialLicenseResult> Read(string enrollment, CancellationToken cancellation)
        {
            Reads++;
            LastEnrollment = enrollment;
            if (BeforeReturn is not null) await BeforeReturn();
            return new CommercialLicenseResult(Status, Status == CommercialLicenseStatus.Authorized ? Lease ?? core.License() : null);
        }
    }

    internal sealed class Fixture(WebApplication app, HttpClient client, CoreFixture core, LicenseState state) : IAsyncDisposable
    {
        internal CoreFixture Core => core;
        internal LicenseState State => state;
        internal Guid ActorLink = core.Owner.Link;
        private string cookie = "";
        private string? token;
        internal string MemberPath => "/members?company_id="+core.Company.ToString("D")+"&link_id="+ActorLink.ToString("D");

        internal static async Task<Fixture> Start(bool invitations = false)
        {
            var core = await CoreFixture.Start();
            var state = new LicenseState(core);
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            var keys = Path.Combine(Path.GetTempPath(), "kuubu-team-http-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(keys);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(keys, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ERP:ProductIdentity:Enabled"] = "true", ["ERP:ProductLicensing:Enabled"] = "true", ["ERP:CompanyTeam:Enabled"] = "true",
                ["ERP:CompanyInvitations:Enabled"] = invitations.ToString(),
                ["ERP:ProductIdentity:ClientId"] = "4798a03c-5ed7-4438-9ec9-d46f63a2f4f1",
                ["ERP:ProductIdentity:ClientSecret"] = "synthetic-team-test-secret", ["ERP:ProductIdentity:DataProtectionPath"] = keys });
            builder.Services.AddScoped<HrmDbContext>(_ => core.Context("untrusted-default"));
            builder.Services.AddAuthentication(LocalAuthenticationHandler.Scheme)
                .AddScheme<LocalAuthOptions, LocalAuthenticationHandler>(LocalAuthenticationHandler.Scheme, _ => { });
            builder.Services.AddProductIdentity(builder.Configuration);
            builder.Services.AddSingleton(new CoreCompanyTeamApi(tenant => core.Context(tenant), state.Read, core.Clock));
            if (invitations) builder.Services.AddSingleton(provider => new CoreCompanyInvitationsApi(tenant => core.Context(tenant),
                provider.GetRequiredService<CoreCompanyTeamApi>(),core.Clock,
                provider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>().CreateProtector(CoreCompanyInvitationsApi.DeliveryPurpose)));
            builder.Services.AddAuthorization();
            var app = builder.Build();
            app.UseProductIdentityOriginGuard();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapProductCompanyTeam();
            app.MapGet("/erp-protected", () => "ERP records").RequireAuthorization();
            await app.StartAsync();
            var client = app.GetTestClient();
            client.BaseAddress = new Uri("https://app.kuubuerp.com");
            return new(app, client, core, state);
        }

        internal async Task SignIn(string subject = "42", bool ordinary = false)
        {
            var properties = new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) };
            if (!ordinary) properties.Items[ProductCompanyTeamFlow.Purpose] = ProductCompanyTeamFlow.PurposeValue;
            var identity = new ClaimsIdentity([new Claim(ProductIdentity.SubjectClaim, subject),
                new Claim(ProductIdentity.IssuerClaim, ProductCommercialLicense.Issuer),
                new Claim(ProductCompanyTeamFlow.NonceClaim, Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)))], ProductIdentity.PendingScheme);
            var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(ProductIdentity.PendingScheme);
            cookie = ProductIdentity.PendingCookie+"="+options.TicketDataFormat.Protect(new AuthenticationTicket(new ClaimsPrincipal(identity), properties, ProductIdentity.PendingScheme));
            if (ordinary) return;
            var session = await Get("/session");
            Assert.Equal(HttpStatusCode.OK, session.StatusCode);
            token = (await Body(session)).GetProperty("csrf_token").GetString();
            cookie += "; "+session.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith(ProductCompanyTeamFlow.CsrfCookie+"=")).Split(';')[0];
        }

        internal Dictionary<string, object?> Command(Guid member, string operation, string? role = null, int revision = 1)
        {
            var body = new Dictionary<string, object?> { ["company_id"] = core.Company.ToString("D"),
                ["link_id"] = ActorLink.ToString("D"), ["membership_id"] = member.ToString("D"),
                ["command_key"] = Guid.NewGuid().ToString("D"), ["expected_revision"] = revision,
                ["operation"] = operation, ["confirmed"] = true };
            if (role is not null) body["role"] = role;
            return body;
        }
        internal Task<HttpResponseMessage> Get(string path) => SendGet(ProductCompanyTeamFlow.Prefix+path);
        internal Task<HttpResponseMessage> GetErp() => SendGet("/erp-protected");
        private Task<HttpResponseMessage> SendGet(string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Cookie", cookie);
            return client.SendAsync(request);
        }
        internal Task<HttpResponseMessage> Post(Dictionary<string, object?> body, string mode = "", string path = "/commands") => PostRaw(JsonSerializer.Serialize(body), mode, path);
        internal Task<HttpResponseMessage> PostRaw(string body, string mode = "", string path = "/commands")
        {
            var request = new HttpRequestMessage(HttpMethod.Post, ProductCompanyTeamFlow.Prefix+path)
            { Content = new StringContent(body, Encoding.UTF8, mode == "non-json" ? "text/plain" : "application/json") };
            if (mode != "missing-cookie") request.Headers.Add("Cookie", cookie);
            request.Headers.Add("Origin", mode == "foreign-origin" ? "https://foreign.invalid" : "https://app.kuubuerp.com");
            if (mode != "missing-token" && token is not null) request.Headers.Add(ProductCompanyTeamFlow.CsrfHeader, mode == "forged-token" ? "forged" : token);
            return client.SendAsync(request);
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.DisposeAsync(); await core.DisposeAsync(); }
    }
}
