using System.Net;
using System.Net.Http.Json;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class InvitationSmtpBridgeFactAttribute:FactAttribute
{
    public InvitationSmtpBridgeFactAttribute()
    {
        if(!OperatingSystem.IsLinux() || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUUBU_INVITATION_SMTP_BRIDGE_MARKER"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUUBU_INVITATION_SMTP_WIRE_FIXTURE")))
            Skip="Requires a private Linux SMTP bridge with process-only CA trust.";
    }
}

/// <summary>Test-only transport composition. Never registered by the application.</summary>
internal sealed class InvitationSmtpBridgeClient(HttpClient channel,string marker):ICompanyInvitationMail
{
    internal string? ReceivedReviewUrl {get;private set;}
    public async Task SendAsync(InvitationMailEnvelope envelope,CancellationToken cancellation)
    {
        if(!envelope.IsCurrent())throw new InvalidOperationException("Invitation authority expired.");
        using var request=new HttpRequestMessage(HttpMethod.Post,"send");
        request.Headers.Add("X-Kuubu-Test-Fixture",marker);
        request.Content=JsonContent.Create(new InvitationSmtpBridgePacket(envelope.Recipient,envelope.Company,envelope.Role,
            envelope.ReviewUrl,envelope.MessageId,envelope.ExpiresAt));
        using var response=await channel.SendAsync(request,cancellation);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Private SMTP bridge failed.");
        var result=await response.Content.ReadFromJsonAsync<InvitationSmtpBridgeReceipt>(cancellation);
        if(result is null || !result.Accepted || result.ReviewUrl!=envelope.ReviewUrl)
            throw new InvalidOperationException("Private SMTP receipt mismatch.");
        ReceivedReviewUrl=result.ReviewUrl;
    }
}
internal sealed record InvitationSmtpBridgePacket(string Recipient,string Company,string Role,string ReviewUrl,string MessageId,DateTimeOffset ExpiresAt)
{public override string ToString()=>nameof(InvitationSmtpBridgePacket);}
internal sealed record InvitationSmtpBridgeReceipt(bool Accepted,string ReviewUrl)
{public override string ToString()=>nameof(InvitationSmtpBridgeReceipt);}

[SupportedOSPlatform("linux")]
public sealed class ProductInvitationSmtpBridgeTests
{
    [InvitationSmtpBridgeFact]
    public async Task Private_TLS_adapter_receives_browser_invitation_until_explicit_stop()
    {
        var marker=Environment.GetEnvironmentVariable("KUUBU_INVITATION_SMTP_BRIDGE_MARKER")!;
        Assert.InRange(marker.Length,43,128);
        var ready=Environment.GetEnvironmentVariable("KUUBU_INVITATION_SMTP_BRIDGE_READY")!;
        Assert.True(Path.IsPathFullyQualified(ready));Assert.False(File.Exists(ready));
        using var lifetime=new CancellationTokenSource(TimeSpan.FromMinutes(20));
        var stopped=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://0.0.0.0:8133").ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=16384);
        await using var app=builder.Build();var sends=0;var inFlight=0;
        bool Authorized(HttpContext http)=>CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(http.Request.Headers["X-Kuubu-Test-Fixture"].ToString())),
            SHA256.HashData(Encoding.UTF8.GetBytes(marker)));
        app.MapGet("/ready",(HttpContext http)=>Authorized(http)?Results.Ok():Results.StatusCode(403));
        app.MapPost("/stop",(HttpContext http)=>{
            if(!Authorized(http))return Results.StatusCode(403);
            stopped.TrySetResult();return Results.Ok();
        });
        app.MapPost("/send",async (HttpContext http)=>{
            if(!Authorized(http))return Results.StatusCode(403);
            if(Interlocked.CompareExchange(ref inFlight,1,0)!=0)return Results.StatusCode(409);
            try {
                var packet=await http.Request.ReadFromJsonAsync<InvitationSmtpBridgePacket>(http.RequestAborted);
                if(packet is null || packet.Recipient!="staff@example.test" || packet.Role!="member"
                    || packet.Company.Length>256 || packet.ExpiresAt<=DateTimeOffset.UtcNow || packet.ExpiresAt>DateTimeOffset.UtcNow.AddMinutes(2)
                    || !Regex.IsMatch(packet.ReviewUrl,@"\Ahttps://app\.kuubuerp\.com/accept-invitation#token=[A-Za-z0-9_-]{43}\z")
                    || !Regex.IsMatch(packet.MessageId,@"\A<kuubu-invitation-[0-9a-f-]{36}@kuubuerp\.com>\z"))return Results.BadRequest();
                using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted,lifetime.Token);
                cancellation.CancelAfter(TimeSpan.FromSeconds(20));
                await using var sink=new SmtpCompanyInvitationWireTests.Sink("server",SmtpCompanyInvitationWireTests.Mode.Accept);
                await SmtpCompanyInvitationWireTests.Sender(sink).SendAsync(new(packet.Recipient,packet.Company,packet.Role,
                    packet.ReviewUrl,packet.MessageId,packet.ExpiresAt,()=>packet.ExpiresAt>DateTimeOffset.UtcNow),cancellation.Token);
                await sink.Complete();var session=Assert.Single(sink.Sessions);
                Assert.True(session.Tls && session.Authenticated && session.DataAccepted);Assert.Equal(0,session.CleartextSensitive);
                var message=SmtpCompanyInvitationWireTests.Decode(session.Message!);
                var received=Regex.Matches(message.Body,@"https://app\.kuubuerp\.com/accept-invitation#token=[A-Za-z0-9_-]{43}");
                Assert.Single(received);Assert.True(received[0].Value==packet.ReviewUrl);
                Assert.True(message.Headers.Contains(packet.MessageId,StringComparison.Ordinal));
                Interlocked.Increment(ref sends);
                return Results.Json(new InvitationSmtpBridgeReceipt(true,received[0].Value));
            }catch(Exception){return Results.StatusCode(503);} // Never expose message/auth/link diagnostics.
            finally {Volatile.Write(ref inFlight,0);}
        });
        await app.StartAsync(lifetime.Token);
        await File.WriteAllTextAsync(ready,"{\"ready\":true,\"production_changes\":false}",lifetime.Token);
        try {await stopped.Task.WaitAsync(lifetime.Token);}
        finally {await app.StopAsync();}
        Assert.Equal(1,sends);Assert.Equal(0,Volatile.Read(ref inFlight));
    }
}
