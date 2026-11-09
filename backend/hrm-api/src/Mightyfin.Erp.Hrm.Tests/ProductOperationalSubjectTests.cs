using System.Net;
using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductOperationalSubjectTests
{
    private const string Client = "391bdeaa-87b3-44b3-8e64-2110dcc1a564";

    [Fact]
    public async Task Exact_operational_person_is_short_lived_and_never_a_membership_or_session_grant()
    {
        using var f = new Fixture();
        var result = await f.Read();
        Assert.Equal(OperationalSubjectStatus.Verified, result.Status);
        var proof = Assert.IsType<OperationalSubjectObservation>(result.Observation);
        Assert.Equal("42", proof.Subject); Assert.Equal(Client, proof.ClientId);
        Assert.True(proof.IsCurrent);
        Assert.DoesNotContain("person.token", proof.ToString());
        f.Clock.Advance(TimeSpan.FromSeconds(30)); Assert.False(proof.IsCurrent);
        f.Clock.Now -= TimeSpan.FromHours(1); Assert.False(proof.IsCurrent);
    }

    [Theory]
    [InlineData("issuer")][InlineData("subject")][InlineData("client_id")][InlineData("product_code")]
    [InlineData("purpose")][InlineData("schema_version")][InlineData("verified")]
    [InlineData("operational_access_granted")][InlineData("extra-field")][InlineData("extra-root")][InlineData("missing")][InlineData("wrong-type")]

    [InlineData("stale")][InlineData("future")][InlineData("expired")]
    [InlineData("too-long")][InlineData("no-zone")][InlineData("duplicate")][InlineData("duplicate-root")]
    [InlineData("oversized")][InlineData("html")][InlineData("json")][InlineData("redirect")]
    [InlineData("network")][InlineData("timeout")]
    public async Task Foreign_ambiguous_or_unavailable_proof_never_returns_an_operational_observation(string fault)
    {
        using var f = new Fixture { Fault = fault };
        var result = await f.Read();
        Assert.Equal(OperationalSubjectStatus.Unavailable, result.Status);
        Assert.Null(result.Observation);
    }

    [Theory]
    [InlineData(401)][InlineData(403)][InlineData(404)]
    public async Task Current_provider_denial_cannot_reuse_a_previous_observation(int status)
    {
        using var f = new Fixture();
        Assert.Equal(OperationalSubjectStatus.Verified, (await f.Read()).Status);
        f.Status = status;
        var result = await f.Read();
        Assert.Equal(OperationalSubjectStatus.Denied, result.Status); Assert.Null(result.Observation);
        Assert.Equal(2, f.Calls);
    }

    [Theory]
    [InlineData("042", "person.token")][InlineData("42\n", "person.token")]
    [InlineData("42", "")][InlineData("42", "token\r\nInjected: value")]
    public async Task Invalid_subject_or_token_fails_before_network(string subject, string token)
    {
        using var f = new Fixture();
        Assert.Equal(OperationalSubjectStatus.Unavailable, (await f.Client.ReadAsync(subject, token)).Status);
        Assert.Equal(0, f.Calls);
    }

    [Fact]
    public async Task Transit_and_monotonic_time_prevent_proof_extension_or_wall_clock_revival()
    {
        using var f = new Fixture { Lifetime = 10, Transit = TimeSpan.FromSeconds(8) };
        var proof = (await f.Read()).Observation!;
        Assert.True(proof.IsCurrent);
        f.Clock.Advance(TimeSpan.FromSeconds(2)); Assert.False(proof.IsCurrent);
        f.Clock.Now -= TimeSpan.FromDays(1); Assert.False(proof.IsCurrent);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_and_no_observation_is_cached()
    {
        using var f = new Fixture { Wait = true };
        using var cancellation = new CancellationTokenSource();
        var pending = f.Read(cancellation.Token);
        await f.Started.Task; cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        f.Wait = false;
        Assert.Equal(OperationalSubjectStatus.Verified, (await f.Read()).Status);
    }

    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
        private long ticks;
        public override DateTimeOffset GetUtcNow() => Now;
        public override long GetTimestamp() => ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        internal void Advance(TimeSpan value) { Now += value; ticks += value.Ticks; }
    }

    private sealed class Fixture : HttpMessageHandler
    {
        internal readonly Clock Clock = new();
        internal readonly ProductOperationalSubject Client;
        private readonly HttpClient http;
        internal string? Fault;
        internal int Status = 200, Calls, Lifetime = 30;
        internal TimeSpan Transit;
        internal bool Wait;
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Fixture() { http = new HttpClient(this, disposeHandler: false); Client = new(http, Clock, ProductOperationalSubjectTests.Client); }
        internal Task<OperationalSubjectResult> Read(CancellationToken ct = default) => Client.ReadAsync("42", "person.token", ct);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Calls++;
            Assert.Equal(ProductOperationalSubject.Endpoint, request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Empty(request.RequestUri.Query);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("person.token", request.Headers.Authorization.Parameter);
            Assert.Null(request.Content);
            Started.TrySetResult();
            if (Wait) await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
            if (Fault == "network") throw new HttpRequestException();
            if (Fault == "timeout") throw new OperationCanceledException();
            var data = new Dictionary<string, object?> {
                ["schema_version"] = 1, ["issuer"] = ProductCommercialLicense.Issuer, ["subject"] = "42",
                ["client_id"] = ProductOperationalSubjectTests.Client, ["product_code"] = "kuubu",
                ["purpose"] = "erp-session-v1",
                ["verified"] = true, ["operational_access_granted"] = false,
                ["observed_at"] = Clock.Now.ToString("O"), ["proof_until"] = Clock.Now.AddSeconds(Lifetime).ToString("O") };
            switch (Fault)
            {
                case "issuer": case "subject": case "client_id": case "product_code": case "purpose": data[Fault] = "foreign"; break;
                case "schema_version": data[Fault] = 2; break;
                case "verified": data[Fault] = false; break;
                case "operational_access_granted": data[Fault] = true; break;
                case "missing": data.Remove("purpose"); break;
                case "extra-field": data["email"]="unrequested@example.test";break;
                case "wrong-type": data["verified"] = "true"; break;

                case "stale": data["observed_at"] = Clock.Now.AddSeconds(-6).ToString("O"); break;
                case "future": data["observed_at"] = Clock.Now.AddSeconds(6).ToString("O"); break;
                case "expired": data["proof_until"] = Clock.Now.AddSeconds(-1).ToString("O"); break;
                case "too-long": data["proof_until"] = Clock.Now.AddSeconds(31).ToString("O"); break;
                case "no-zone": data["observed_at"] = "2026-11-08T12:00:00"; break;
                case "oversized": data["padding"] = new string('x', 9000); break;
            }
            var text = JsonSerializer.Serialize(new { data });
            if (Fault == "duplicate") text = text.Replace("\"verified\":true", "\"verified\":false,\"verified\":true");
            if (Fault == "extra-root") text="{\"extra\":true,"+text[1..];
            if (Fault == "duplicate-root") text = "{\"data\":{},"+text[1..];
            if (Fault == "json") text = "{";
            Clock.Advance(Transit);
            return new HttpResponseMessage((HttpStatusCode)(Fault == "redirect" ? 302 : Status)) {
                Content = new StringContent(text, Encoding.UTF8, Fault == "html" ? "text/html" : "application/json") };
        }
        protected override void Dispose(bool disposing) { if (disposing) http.Dispose(); base.Dispose(disposing); }
    }
}
