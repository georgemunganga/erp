using System.Net;
using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductInvitationRecipientTests
{
    private const string Client = "391bdeaa-87b3-44b3-8e64-2110dcc1a564";

    [Fact]
    public async Task Exact_recipient_is_short_lived_and_never_a_membership_or_operational_grant()
    {
        using var f = new Fixture();
        var result = await f.Read();
        Assert.Equal(InvitationRecipientStatus.Verified, result.Status);
        var proof = Assert.IsType<InvitationRecipientObservation>(result.Observation);
        Assert.Equal("42", proof.Subject); Assert.Equal(Client, proof.ClientId);
        Assert.Equal("invited+person@example.test", proof.VerifiedEmail);
        Assert.True(proof.IsCurrent);
        Assert.DoesNotContain(proof.VerifiedEmail, proof.ToString());
        f.Clock.Advance(TimeSpan.FromSeconds(30)); Assert.False(proof.IsCurrent);
        f.Clock.Now -= TimeSpan.FromHours(1); Assert.False(proof.IsCurrent);
    }

    [Theory]
    [InlineData("issuer")][InlineData("subject")][InlineData("client_id")][InlineData("product_code")]
    [InlineData("purpose")][InlineData("schema_version")][InlineData("email_verified")]
    [InlineData("operational_access_granted")][InlineData("missing")][InlineData("wrong-type")]
    [InlineData("email-uppercase")][InlineData("email-display")][InlineData("email-newline")]
    [InlineData("email-empty")][InlineData("email-null")][InlineData("email-too-long")][InlineData("email-unicode")]
    [InlineData("email-invalid")][InlineData("stale")][InlineData("future")][InlineData("expired")]
    [InlineData("too-long")][InlineData("no-zone")][InlineData("duplicate")][InlineData("duplicate-root")]
    [InlineData("oversized")][InlineData("html")][InlineData("json")][InlineData("redirect")]
    [InlineData("network")][InlineData("timeout")]
    public async Task Foreign_ambiguous_or_unavailable_proof_never_exposes_a_recipient(string fault)
    {
        using var f = new Fixture { Fault = fault };
        var result = await f.Read();
        Assert.Equal(InvitationRecipientStatus.Unavailable, result.Status);
        Assert.Null(result.Observation);
    }

    [Theory]
    [InlineData(401)][InlineData(403)][InlineData(404)]
    public async Task Current_provider_denial_cannot_reuse_a_previous_observation(int status)
    {
        using var f = new Fixture();
        Assert.Equal(InvitationRecipientStatus.Verified, (await f.Read()).Status);
        f.Status = status;
        var result = await f.Read();
        Assert.Equal(InvitationRecipientStatus.Denied, result.Status); Assert.Null(result.Observation);
        Assert.Equal(2, f.Calls);
    }

    [Theory]
    [InlineData("042", "person.token")][InlineData("42\n", "person.token")]
    [InlineData("42", "")][InlineData("42", "token\r\nInjected: value")]
    public async Task Invalid_subject_or_token_fails_before_network(string subject, string token)
    {
        using var f = new Fixture();
        Assert.Equal(InvitationRecipientStatus.Unavailable, (await f.Client.ReadAsync(subject, token)).Status);
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
        Assert.Equal(InvitationRecipientStatus.Verified, (await f.Read()).Status);
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
        internal readonly ProductInvitationRecipient Client;
        private readonly HttpClient http;
        internal string? Fault;
        internal int Status = 200, Calls, Lifetime = 30;
        internal TimeSpan Transit;
        internal bool Wait;
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Fixture() { http = new HttpClient(this, disposeHandler: false); Client = new(http, Clock, ProductInvitationRecipientTests.Client); }
        internal Task<InvitationRecipientResult> Read(CancellationToken ct = default) => Client.ReadAsync("42", "person.token", ct);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Calls++;
            Assert.Equal(ProductInvitationRecipient.Endpoint, request.RequestUri!.AbsoluteUri);
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
                ["client_id"] = ProductInvitationRecipientTests.Client, ["product_code"] = "kuubu",
                ["purpose"] = "company-invitation-v1", ["verified_email"] = "invited+person@example.test",
                ["email_verified"] = true, ["operational_access_granted"] = false,
                ["observed_at"] = Clock.Now.ToString("O"), ["proof_until"] = Clock.Now.AddSeconds(Lifetime).ToString("O") };
            switch (Fault)
            {
                case "issuer": case "subject": case "client_id": case "product_code": case "purpose": data[Fault] = "foreign"; break;
                case "schema_version": data[Fault] = 2; break;
                case "email_verified": data[Fault] = false; break;
                case "operational_access_granted": data[Fault] = true; break;
                case "missing": data.Remove("purpose"); break;
                case "wrong-type": data["email_verified"] = "true"; break;
                case "email-uppercase": data["verified_email"] = "Person@example.test"; break;
                case "email-display": data["verified_email"] = "Person <person@example.test>"; break;
                case "email-newline": data["verified_email"] = "person@example.test\n"; break;
                case "email-empty": data["verified_email"] = ""; break;
                case "email-null": data["verified_email"] = null; break;
                case "email-too-long": data["verified_email"] = new string('a', 255)+"@example.test"; break;
                case "email-unicode": data["verified_email"] = "pérson@example.test"; break;
                case "email-invalid": data["verified_email"] = "not-an-address"; break;
                case "stale": data["observed_at"] = Clock.Now.AddSeconds(-6).ToString("O"); break;
                case "future": data["observed_at"] = Clock.Now.AddSeconds(6).ToString("O"); break;
                case "expired": data["proof_until"] = Clock.Now.AddSeconds(-1).ToString("O"); break;
                case "too-long": data["proof_until"] = Clock.Now.AddSeconds(31).ToString("O"); break;
                case "no-zone": data["observed_at"] = "2026-11-08T12:00:00"; break;
                case "oversized": data["padding"] = new string('x', 9000); break;
            }
            var text = JsonSerializer.Serialize(new { data });
            if (Fault == "duplicate") text = text.Replace("\"email_verified\":true", "\"email_verified\":false,\"email_verified\":true");
            if (Fault == "duplicate-root") text = "{\"data\":{},"+text[1..];
            if (Fault == "json") text = "{";
            Clock.Advance(Transit);
            return new HttpResponseMessage((HttpStatusCode)(Fault == "redirect" ? 302 : Status)) {
                Content = new StringContent(text, Encoding.UTF8, Fault == "html" ? "text/html" : "application/json") };
        }
        protected override void Dispose(bool disposing) { if (disposing) http.Dispose(); base.Dispose(disposing); }
    }
}
