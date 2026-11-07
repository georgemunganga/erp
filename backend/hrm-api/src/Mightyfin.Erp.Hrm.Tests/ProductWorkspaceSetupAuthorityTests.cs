using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductWorkspaceSetupAuthorityTests
{
    private const string Customer = "01arz3ndektsv4rrffq69g5fav";
    private const string Enrollment = "01arz3ndektsv4rrffq69g5faw";
    private const string ClientId = "391bdeaa-87b3-44b3-8e64-2110dcc1a564";

    [Fact]
    public async Task Exact_person_and_payer_observation_is_bounded_and_contains_no_token_or_operational_grant()
    {
        using var f = new Fixture();
        var result = await f.Read();
        Assert.Equal(WorkspaceAuthorityStatus.Verified, result.Status);
        var observation = Assert.IsType<WorkspaceSetupObservation>(result.Observation);
        Assert.Equal("42", observation.Subject);
        Assert.Equal(ClientId, observation.ClientId);
        Assert.Equal(Customer, observation.CustomerId);
        Assert.Equal(Enrollment, observation.EnrollmentId);
        Assert.True(observation.IsCurrent);
        f.Clock.Advance(TimeSpan.FromSeconds(60));
        Assert.False(observation.IsCurrent);
        f.Clock.Now -= TimeSpan.FromHours(1);
        Assert.False(observation.IsCurrent);
        Assert.Equal(1, f.Transport.Calls);
    }

    [Theory]
    [InlineData("issuer")][InlineData("subject")][InlineData("client")][InlineData("payer")]
    [InlineData("product")][InlineData("action")][InlineData("control")][InlineData("operational")]
    [InlineData("schema")][InlineData("enrollment")][InlineData("uppercase-enrollment")]
    [InlineData("stale")][InlineData("future")][InlineData("too-long")][InlineData("expired")]
    [InlineData("missing")][InlineData("wrong-type")][InlineData("date-without-zone")]
    [InlineData("duplicate")][InlineData("duplicate-root")][InlineData("json")][InlineData("oversized")]
    [InlineData("media-type")][InlineData("redirect")][InlineData("unavailable")]
    [InlineData("network")][InlineData("timeout")]
    [InlineData("capacity-missing")][InlineData("capacity-null")][InlineData("capacity-string")]
    [InlineData("capacity-negative")][InlineData("capacity-over")][InlineData("capacity-fraction")]
    [InlineData("capacity-boolean")][InlineData("capacity-overflow")]
    public async Task Foreign_ambiguous_expired_or_unavailable_provider_never_returns_authority(string fault)
    {
        using var f = new Fixture();
        f.Transport.Fault = fault;
        var result = await f.Read();
        Assert.Equal(WorkspaceAuthorityStatus.Unavailable, result.Status);
        Assert.Null(result.Observation);
    }

    [Theory]
    [InlineData(401)][InlineData(403)][InlineData(404)]
    public async Task Provider_denial_never_reuses_previous_observation(int status)
    {
        using var f = new Fixture();
        Assert.Equal(WorkspaceAuthorityStatus.Verified, (await f.Read()).Status);
        f.Transport.Status = status;
        var denied = await f.Read();
        Assert.Equal(WorkspaceAuthorityStatus.Denied, denied.Status);
        Assert.Null(denied.Observation);
        f.Transport.Status = 200;
        f.Transport.Fault = "unavailable";
        Assert.Null((await f.Read()).Observation);
        Assert.Equal(3, f.Transport.Calls);
    }

    [Theory]
    [InlineData("042", Customer, "token")][InlineData("42\n", Customer, "token")]
    [InlineData("42", "01ARZ3NDEKTSV4RRFFQ69G5FAV", "token")]
    [InlineData("42", Customer+"\n", "token")][InlineData("42", "../foreign", "token")]
    [InlineData("42", Customer, "")][InlineData("42", Customer, "token\r\nInjected: value")]
    public async Task Noncanonical_input_is_denied_before_network(string subject, string customer, string token)
    {
        using var f = new Fixture();
        Assert.Equal(WorkspaceAuthorityStatus.Unavailable, (await f.Client.ReadAsync(subject, customer, token)).Status);
        Assert.Equal(0, f.Transport.Calls);
    }

    [Fact]
    public async Task Response_time_is_included_and_short_authority_cannot_be_prolonged()
    {
        using var f = new Fixture();
        f.Transport.Lifetime = 10;
        f.Transport.Elapsed = TimeSpan.FromSeconds(8);
        var observation = (await f.Read()).Observation!;
        Assert.True(observation.IsCurrent);
        f.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.False(observation.IsCurrent);
        f.Clock.Now -= TimeSpan.FromHours(1);
        Assert.False(observation.IsCurrent);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_and_next_read_is_independent()
    {
        using var f = new Fixture();
        f.Transport.WaitForCancellation = true;
        using var cancellation = new CancellationTokenSource();
        var pending = f.Read(cancellation.Token);
        await f.Transport.Started.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        f.Transport.WaitForCancellation = false;
        Assert.Equal(WorkspaceAuthorityStatus.Verified, (await f.Read()).Status);
    }

    [Fact]
    public void Registration_defaults_off_and_requires_identity_and_licensing()
    {
        var services = new ServiceCollection();
        services.AddProductWorkspaceSetup(new ConfigurationBuilder().Build());
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(ProductWorkspaceSetupAuthority));
        var values = new Dictionary<string, string?> { ["ERP:WorkspaceSetup:Enabled"] = "true" };
        Assert.Throws<InvalidOperationException>(() => services.AddProductWorkspaceSetup(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
        values["ERP:ProductIdentity:Enabled"] = "true";
        values["ERP:ProductIdentity:ClientId"] = ClientId;
        Assert.Throws<InvalidOperationException>(() => services.AddProductWorkspaceSetup(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
        values["ERP:ProductLicensing:Enabled"] = "true";
        services.AddProductWorkspaceSetup(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        Assert.Contains(services, x => x.ServiceType == typeof(ProductWorkspaceSetupAuthority));
    }

    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        private long ticks;
        public override DateTimeOffset GetUtcNow() => Now;
        public override long GetTimestamp() => ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        internal void Advance(TimeSpan value) { Now += value; ticks += value.Ticks; }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly Clock Clock = new();
        internal readonly Transport Transport;
        internal readonly ProductWorkspaceSetupAuthority Client;
        private readonly HttpClient http;
        internal Fixture() { Transport = new(Clock); http = new(Transport); Client = new(http, Clock, ClientId); }
        internal Task<WorkspaceAuthorityResult> Read(CancellationToken ct = default) => Client.ReadAsync("42", Customer, "synthetic.person.token", ct);
        public void Dispose() => http.Dispose();
    }

    private sealed class Transport(Clock clock) : HttpMessageHandler
    {
        internal int Calls, Status = 200, Lifetime = 60;
        internal string Fault = "";
        internal TimeSpan Elapsed;
        internal bool WaitForCancellation;
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(ProductCommercialLicense.Issuer+"/api/v1/product-identity/customers/"+Customer+"/workspace-setup-authority", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("synthetic.person.token", request.Headers.Authorization.Parameter);
            Assert.Equal("application/json", Assert.Single(request.Headers.Accept).MediaType);
            Assert.Null(request.Content);
            if (WaitForCancellation) { Started.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            if (Fault == "network") throw new HttpRequestException("Synthetic failure");
            if (Fault == "timeout") throw new TaskCanceledException("Synthetic timeout");
            if (Status != 200) return new((HttpStatusCode)Status);
            if (Fault == "redirect") return new(HttpStatusCode.Redirect);
            if (Fault == "unavailable") return new(HttpStatusCode.ServiceUnavailable);
            var now = clock.Now;
            var fields = new Dictionary<string, object?> { ["schema_version"] = 1, ["issuer"] = ProductCommercialLicense.Issuer,
                ["subject"] = "42", ["client_id"] = ClientId, ["customer_id"] = Customer,
                ["product_code"] = "kuubu", ["product_enrollment_id"] = Enrollment,
                ["workspace_limit"] = 2, ["action"] = "request_workspace_creation", ["payer_control_verified"] = true,
                ["operational_access_granted"] = false, ["observed_at"] = now, ["authority_until"] = now.AddSeconds(Lifetime) };
            switch (Fault)
            {
                case "capacity-missing": fields.Remove("workspace_limit"); break;
                case "capacity-null": fields["workspace_limit"] = null; break;
                case "capacity-string": fields["workspace_limit"] = "2"; break;
                case "capacity-negative": fields["workspace_limit"] = -1; break;
                case "capacity-over": fields["workspace_limit"] = 10001; break;
                case "capacity-fraction": fields["workspace_limit"] = 1.5; break;
                case "capacity-boolean": fields["workspace_limit"] = true; break;
                case "capacity-overflow": fields["workspace_limit"] = 10000000000L; break;
                case "issuer": fields["issuer"] = "https://foreign.invalid"; break;
                case "subject": fields["subject"] = "43"; break;
                case "client": fields["client_id"] = Guid.NewGuid().ToString(); break;
                case "payer": fields["customer_id"] = Enrollment; break;
                case "product": fields["product_code"] = "hosting"; break;
                case "action": fields["action"] = "rebind_company"; break;
                case "control": fields["payer_control_verified"] = false; break;
                case "operational": fields["operational_access_granted"] = true; break;
                case "schema": fields["schema_version"] = 2; break;
                case "enrollment": fields["product_enrollment_id"] = "foreign"; break;
                case "uppercase-enrollment": fields["product_enrollment_id"] = Enrollment.ToUpperInvariant(); break;
                case "stale": fields["observed_at"] = now.AddSeconds(-6); break;
                case "future": fields["observed_at"] = now.AddSeconds(6); break;
                case "too-long": fields["authority_until"] = now.AddSeconds(61); break;
                case "expired": fields["authority_until"] = now; break;
                case "missing": fields.Remove("issuer"); break;
                case "wrong-type": fields["payer_control_verified"] = "true"; break;
                case "date-without-zone": fields["observed_at"] = "2026-11-07T12:00:00"; break;
            }
            var json = JsonSerializer.Serialize(new { data = fields });
            if (Fault == "duplicate") json = json[..^2]+",\"subject\":\"42\"}}";
            if (Fault == "duplicate-root") json = json[..^1]+",\"data\":{}}";
            if (Fault == "json") json = "broken";
            if (Fault == "oversized") json = "{\"padding\":\""+new string('a', 65536)+"\"}";
            clock.Advance(Elapsed);
            return new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, Fault == "media-type" ? "text/html" : "application/json") };
        }
    }
}
