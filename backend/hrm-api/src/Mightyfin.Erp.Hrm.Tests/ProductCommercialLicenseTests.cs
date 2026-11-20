using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductCommercialLicenseTests
{
    private const string Enrollment = "01arz3ndektsv4rrffq69g5fav";
    private const string OtherEnrollment = "01arz3ndektsv4rrffq69g5faw";
    private const string ClientId = "391bdeaa-87b3-44b3-8e64-2110dcc1a564";
    private const string Credential = "synthetic-machine-secret";

    [Fact]
    public async Task Account_hub_licenses_require_complete_valid_per_module_user_limits()
    {
        using var f=new Fixture(requireCaps:true);
        Assert.Equal(CommercialLicenseStatus.Unavailable,(await f.Client.ReadAsync(Enrollment)).Status);
        f.Transport.Fault="caps";var lease=(await f.Client.ReadAsync(Enrollment)).Lease!;
        Assert.Equal(5,lease.UserLimit("hrm"));Assert.Null(lease.UserLimit("payroll"));
        foreach(var mode in new[]{"caps-missing-module","caps-zero","caps-extra","caps-duplicate"}) {
            f.Transport.Fault=mode;Assert.Equal(CommercialLicenseStatus.Unavailable,(await f.Client.ReadAsync(Enrollment)).Status);
        }
    }

    [Fact]
    public async Task Machine_channel_uses_exact_scope_endpoint_and_binding_without_any_human_credentials()
    {
        using var fixture = new Fixture();
        var result = await fixture.Client.ReadAsync(Enrollment);
        Assert.Equal(CommercialLicenseStatus.Authorized, result.Status);
        Assert.NotNull(result.Lease);
        Assert.Equal(Enrollment, result.Lease.EnrollmentId);
        Assert.True(result.Lease.AllowsWorkspaceModule(1, "hrm"));
        Assert.False(result.Lease.AllowsWorkspaceModule(1, "accounting"));
        Assert.False(result.Lease.AllowsWorkspaceModule(1, "HRM"));
        Assert.Equal(1, fixture.Transport.TokenCalls);
        Assert.Equal(1, fixture.Transport.ResourceCalls);
    }

    [Fact]
    public async Task Parallel_companies_share_only_machine_token_and_each_fetch_its_own_current_lease()
    {
        using var fixture = new Fixture();
        fixture.Transport.DelayToken = true;
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => fixture.Client.ReadAsync(i % 2 == 0 ? Enrollment : OtherEnrollment)));
        Assert.All(results, x => Assert.Equal(CommercialLicenseStatus.Authorized, x.Status));
        Assert.Equal(1, fixture.Transport.TokenCalls);
        Assert.Equal(12, fixture.Transport.ResourceCalls);
        Assert.Equal(6, results.Count(x => x.Lease!.EnrollmentId == Enrollment));
        Assert.Equal(6, results.Count(x => x.Lease!.EnrollmentId == OtherEnrollment));
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("product")]
    [InlineData("enrollment")]
    [InlineData("operational")]
    [InlineData("schema")]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("too-long")]
    [InlineData("past-subscription")]
    [InlineData("expired")]
    [InlineData("missing")]
    [InlineData("wrong-type")]
    [InlineData("invalid-module")]
    [InlineData("duplicate-module")]
    [InlineData("duplicate-property")]
    [InlineData("json")]
    [InlineData("media-type")]
    [InlineData("oversized")]
    [InlineData("redirect")]
    [InlineData("unavailable")]
    [InlineData("network")]
    [InlineData("timeout")]
    [InlineData("date-without-zone")]
    [InlineData("capacity-missing")][InlineData("capacity-null")][InlineData("capacity-string")]
    [InlineData("capacity-negative")][InlineData("capacity-over")][InlineData("capacity-fraction")]
    [InlineData("capacity-boolean")][InlineData("capacity-overflow")]
    public async Task Invalid_or_unavailable_provider_does_not_return_a_commercial_lease(string fault)
    {
        using var fixture = new Fixture();
        fixture.Transport.Fault = fault;
        var result = await fixture.Client.ReadAsync(Enrollment);
        Assert.Equal(CommercialLicenseStatus.Unavailable, result.Status);
        Assert.Null(result.Lease);
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(10000)]
    public async Task Purchased_capacity_is_required_for_each_allocated_slot_and_module(int capacity)
    {
        using var fixture = new Fixture();
        fixture.Transport.Capacity = capacity;
        var lease = (await fixture.Client.ReadAsync(Enrollment)).Lease!;
        Assert.Equal(capacity, lease.WorkspaceLimit);
        foreach (var slot in new[] { -1, 0, 1, capacity, capacity+1 })
        {
            var allowed = slot >= 1 && slot <= capacity;
            Assert.Equal(allowed, lease.AllowsWorkspace(slot));
            Assert.Equal(allowed, lease.AllowsWorkspaceModule(slot, "hrm"));
            Assert.False(lease.AllowsWorkspaceModule(slot, "unlicensed"));
        }
        fixture.Clock.Advance(TimeSpan.FromSeconds(60));
        Assert.False(lease.AllowsWorkspace(1));
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("lifetime")]
    [InlineData("expired")]
    [InlineData("type")]
    [InlineData("refresh")]
    [InlineData("duplicate")]
    [InlineData("empty")]
    [InlineData("malformed")]
    [InlineData("redirect")]
    public async Task Invalid_machine_token_never_reaches_the_licensing_resource(string fault)
    {
        using var fixture = new Fixture();
        fixture.Transport.TokenFault = fault;
        var result = await fixture.Client.ReadAsync(Enrollment);
        Assert.Equal(CommercialLicenseStatus.Unavailable, result.Status);
        Assert.Null(result.Lease);
        Assert.Equal(0, fixture.Transport.ResourceCalls);
    }

    [Theory]
    [InlineData("denied")]
    [InlineData("unknown")]
    public async Task Known_denial_or_unknown_binding_never_grants_modules(string fault)
    {
        using var fixture = new Fixture();
        fixture.Transport.Fault = fault;
        var result = await fixture.Client.ReadAsync(Enrollment);
        Assert.Equal(CommercialLicenseStatus.Denied, result.Status);
        Assert.Null(result.Lease);
    }

    [Fact]
    public async Task Outage_or_revocation_cannot_reuse_a_previous_commercial_lease()
    {
        using var fixture = new Fixture();
        Assert.Equal(CommercialLicenseStatus.Authorized, (await fixture.Client.ReadAsync(Enrollment)).Status);
        fixture.Transport.Fault = "unavailable";
        Assert.Equal(CommercialLicenseStatus.Unavailable, (await fixture.Client.ReadAsync(Enrollment)).Status);
        fixture.Transport.Fault = "forbidden";
        Assert.Equal(CommercialLicenseStatus.Unavailable, (await fixture.Client.ReadAsync(Enrollment)).Status);
        fixture.Transport.Fault = "";
        Assert.Equal(CommercialLicenseStatus.Authorized, (await fixture.Client.ReadAsync(Enrollment)).Status);
        Assert.Equal(2, fixture.Transport.TokenCalls);
        Assert.Equal(4, fixture.Transport.ResourceCalls);
    }

    [Fact]
    public async Task Lease_expires_at_subscription_deadline_and_monotonic_time_survives_clock_rollback()
    {
        using var fixture = new Fixture();
        fixture.Transport.ShortLease = true;
        var lease = (await fixture.Client.ReadAsync(Enrollment)).Lease!;
        fixture.Clock.Advance(TimeSpan.FromSeconds(9));
        Assert.True(lease.AllowsWorkspaceModule(1, "hrm"));
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(lease.IsCurrent);
        fixture.Clock.Now -= TimeSpan.FromHours(1);
        Assert.False(lease.AllowsWorkspaceModule(1, "hrm"));
    }

    [Fact]
    public async Task Token_is_refreshed_before_expiry_even_after_wall_clock_rollback()
    {
        using var fixture = new Fixture();
        await fixture.Client.ReadAsync(Enrollment);
        fixture.Clock.Advance(TimeSpan.FromSeconds(115));
        fixture.Clock.Now -= TimeSpan.FromMinutes(1);
        await fixture.Client.ReadAsync(Enrollment);
        Assert.Equal(2, fixture.Transport.TokenCalls);
    }

    [Fact]
    public async Task Resource_roundtrip_is_part_of_lease_budget()
    {
        using var fixture = new Fixture();
        fixture.Transport.AdvanceResource = TimeSpan.FromSeconds(8);
        fixture.Transport.ShortLease = true;
        var lease = (await fixture.Client.ReadAsync(Enrollment)).Lease!;
        Assert.True(lease.IsCurrent);
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.False(lease.IsCurrent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../../token")]
    [InlineData("01ARZ3NDEKTSV4RRFFQ69G5FAV")]
    [InlineData("01arz3ndektsv4rrffq69g5fav\n")]
    public async Task Noncanonical_enrollment_never_generates_network_traffic(string reference)
    {
        using var fixture = new Fixture();
        Assert.Equal(CommercialLicenseStatus.Unavailable, (await fixture.Client.ReadAsync(reference)).Status);
        Assert.Equal(0, fixture.Transport.TokenCalls);
        Assert.Equal(0, fixture.Transport.ResourceCalls);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_and_releases_single_flight_lock()
    {
        using var fixture = new Fixture();
        fixture.Transport.DelayUntilCanceled = true;
        using var cancellation = new CancellationTokenSource();
        var pending = fixture.Client.ReadAsync(Enrollment, cancellation.Token);
        await fixture.Transport.Started.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        fixture.Transport.DelayUntilCanceled = false;
        Assert.Equal(CommercialLicenseStatus.Authorized, (await fixture.Client.ReadAsync(Enrollment)).Status);
    }

    [Fact]
    public void Registration_is_default_off_and_requires_its_own_confidential_machine_client()
    {
        foreach (var enabled in new[] { new Dictionary<string, string?>(), new() { ["ERP:ProductLicensing:Enabled"] = "true" } })
        {
            var services = new ServiceCollection();
            services.AddProductCommercialLicense(new ConfigurationBuilder().AddInMemoryCollection(enabled).Build());
            Assert.DoesNotContain(services, x => x.ServiceType == typeof(ProductCommercialLicense));
        }
        var values = new Dictionary<string, string?> { ["ERP:ProductIdentity:Enabled"] = "true",
            ["ERP:ProductLicensing:Enabled"] = "true", ["ERP:ProductLicensing:ClientId"] = ClientId,
            ["ERP:ProductIdentity:ClientId"] = ClientId, ["ERP:ProductLicensing:ClientSecret"] = Credential };
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddProductCommercialLicense(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
        values["ERP:ProductIdentity:ClientId"] = ClientId.ToUpperInvariant();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddProductCommercialLicense(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
        values["ERP:ProductIdentity:ClientId"] = Guid.NewGuid().ToString();
        var configured = new ServiceCollection();
        configured.AddProductCommercialLicense(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        using var provider = configured.BuildServiceProvider();
        Assert.Same(provider.GetRequiredService<ProductCommercialLicense>(), provider.GetRequiredService<ProductCommercialLicense>());
    }

    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        private long timestamp;
        public override DateTimeOffset GetUtcNow() => Now;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        internal void Advance(TimeSpan duration) { Now += duration; timestamp += duration.Ticks; }
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly Clock Clock = new();
        internal readonly Transport Transport;
        internal readonly ProductCommercialLicense Client;
        private readonly HttpClient http;
        internal Fixture(bool requireCaps=false)
        {
            Transport = new Transport(Clock);
            http = new HttpClient(Transport);
            Client = new ProductCommercialLicense(http, Clock, ClientId, Credential, requireCaps);
        }
        public void Dispose() => http.Dispose();
    }
    private sealed class Transport(Clock clock) : HttpMessageHandler
    {
        internal int TokenCalls, ResourceCalls;
        internal int Capacity = 2;
        internal string Fault = "", TokenFault = "";
        internal bool DelayToken, DelayUntilCanceled, ShortLease;
        internal TimeSpan AdvanceResource;
        internal TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Assert.Equal("commercial-api.greenwebb.tech", request.RequestUri!.Host);
            Assert.Equal("https", request.RequestUri.Scheme);
            Assert.Empty(request.RequestUri.Query);
            Assert.False(request.Headers.Contains("Cookie"));
            Assert.Contains(request.Headers.Accept, value => value.MediaType == "application/json");
            if (request.Method == HttpMethod.Post)
            {
                Interlocked.Increment(ref TokenCalls);
                var body = await request.Content!.ReadAsStringAsync(cancellation);
                Assert.Equal("grant_type=client_credentials&client_id="+ClientId+"&client_secret="+Credential+"&scope=product%3Alicense", body);
                Assert.Null(request.Headers.Authorization);
                Assert.Equal("/api/v1/product-identity/token", request.RequestUri.AbsolutePath);
                Started.TrySetResult();
                if (DelayUntilCanceled) await Task.Delay(Timeout.Infinite, cancellation);
                if (DelayToken) await Task.Delay(50, cancellation);
                if (TokenFault == "redirect") return new(HttpStatusCode.Redirect);
                var fields = new Dictionary<string, object?> { ["token_type"] = TokenFault == "type" ? "Basic" : "Bearer",
                    ["expires_in"] = TokenFault == "lifetime" ? 121 : TokenFault == "expired" ? 0 : 120,
                    ["access_token"] = TokenFault == "empty" ? "" : TokenFault == "malformed" ? "invalid\r\ntoken" : "synthetic.machine.token" };
                if (TokenFault == "refresh") fields["refresh_token"] = "not-allowed";
                // Optional scope must not expand the machine channel.
                if (TokenFault == "scope") fields["scope"] = "product:identity product:license";
                var text = JsonSerializer.Serialize(fields);
                if (TokenFault == "duplicate") text = text[..^1]+",\"expires_in\":120}";
                return Json(text);
            }
            Interlocked.Increment(ref ResourceCalls);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("synthetic.machine.token", request.Headers.Authorization.Parameter);
            var enrollment = request.RequestUri.AbsolutePath.Split('/').Last();
            Assert.Equal("/api/v1/product-identity/licensing/enrollments/"+enrollment, request.RequestUri.AbsolutePath);
            if (Fault == "network") throw new HttpRequestException("Synthetic network failure");
            if (Fault == "timeout") throw new TaskCanceledException("Synthetic timeout");
            if (Fault == "unknown") return new(HttpStatusCode.NotFound);
            if (Fault == "forbidden") return new(HttpStatusCode.Forbidden);
            if (Fault == "redirect") return new(HttpStatusCode.Redirect);
            if (Fault == "unavailable") return new(HttpStatusCode.ServiceUnavailable);
            var observed = clock.Now;
            var fields2 = new Dictionary<string, object?> { ["schema_version"] = 1, ["issuer"] = ProductCommercialLicense.Issuer,
                ["product_code"] = "kuubu", ["product_enrollment_id"] = enrollment,
                ["workspace_limit"] = Capacity, ["commercial_access_authorized"] = true, ["operational_access_granted"] = false,
                ["module_codes"] = new[] { "hrm", "payroll" }, ["observed_at"] = observed,
                ["lease_until"] = observed.AddSeconds(ShortLease ? 10 : 60),
                ["access_until"] = observed.AddSeconds(ShortLease ? 10 : 600) };
            if(Fault.StartsWith("caps",StringComparison.Ordinal))fields2["module_user_limits"]=new Dictionary<string,int?>{{"hrm",5},{"payroll",null}};
            switch (Fault)
            {
                case "caps-missing-module": fields2["module_user_limits"]=new Dictionary<string,int?>{{"hrm",5}};break;
                case "caps-zero": fields2["module_user_limits"]=new Dictionary<string,int?>{{"hrm",0},{"payroll",null}};break;
                case "caps-extra": fields2["module_user_limits"]=new Dictionary<string,int?>{{"hrm",5},{"payroll",null},{"unbought",null}};break;
                case "capacity-missing": fields2.Remove("workspace_limit"); break;
                case "capacity-null": fields2["workspace_limit"] = null; break;
                case "capacity-string": fields2["workspace_limit"] = "2"; break;
                case "capacity-negative": fields2["workspace_limit"] = -1; break;
                case "capacity-over": fields2["workspace_limit"] = 10001; break;
                case "capacity-fraction": fields2["workspace_limit"] = 1.5; break;
                case "capacity-boolean": fields2["workspace_limit"] = true; break;
                case "capacity-overflow": fields2["workspace_limit"] = 10000000000L; break;
                case "issuer": fields2["issuer"] = "https://foreign.invalid"; break;
                case "product": fields2["product_code"] = "hosting"; break;
                case "enrollment": fields2["product_enrollment_id"] = "another-payer"; break;
                case "operational": fields2["operational_access_granted"] = true; break;
                case "schema": fields2["schema_version"] = 2; break;
                case "stale": fields2["observed_at"] = observed.AddSeconds(-6); break;
                case "future": fields2["observed_at"] = observed.AddSeconds(6); break;
                case "too-long": fields2["lease_until"] = observed.AddSeconds(61); break;
                case "past-subscription": fields2["access_until"] = observed.AddSeconds(59); break;
                case "expired": fields2["lease_until"] = observed; break;
                case "missing": fields2.Remove("issuer"); break;
                case "wrong-type": fields2["commercial_access_authorized"] = "true"; break;
                case "invalid-module": fields2["module_codes"] = new[] { "hrm\n" }; break;
                case "duplicate-module": fields2["module_codes"] = new[] { "hrm", "hrm" }; break;
                case "date-without-zone": fields2["observed_at"] = "2026-11-07T12:00:00"; break;
                case "denied": fields2["workspace_limit"] = 0; fields2["commercial_access_authorized"] = false; fields2["access_until"] = null;
                    fields2["module_codes"] = Array.Empty<string>(); fields2["lease_until"] = observed; break;
            }
            var payload = JsonSerializer.Serialize(new { data = fields2 });
            if(Fault=="caps-duplicate")payload=payload[..^2]+",\"module_user_limits\":{\"hrm\":5,\"payroll\":null}}}";
            if (Fault == "json") payload = "broken";
            if (Fault == "oversized") payload = "{\"padding\":\""+new string('a', 65536)+"\"}";
            if (Fault == "duplicate-property") payload = payload[..^2]+",\"product_code\":\"kuubu\"}}";
            clock.Advance(AdvanceResource);
            var response = Json(payload);
            if (Fault == "media-type") response.Content.Headers.ContentType = new("text/html");
            return response;
        }
        private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
        { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    }
}
