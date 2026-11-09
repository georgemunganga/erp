using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using CoreFixture=Mightyfin.Erp.Hrm.Tests.CoreCompanyTeamCommandsTests.Fixture;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class InvitationSmtpWireFactAttribute:FactAttribute
{
    public InvitationSmtpWireFactAttribute()
    {
        if(!OperatingSystem.IsLinux() || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUUBU_INVITATION_SMTP_WIRE_FIXTURE")))
            Skip="Requires a private Linux SMTP/TLS fixture with process-only CA trust.";
    }
}
public sealed class InvitationSmtpStoreWireFactAttribute:FactAttribute
{
    public InvitationSmtpStoreWireFactAttribute()
    {
        if(!OperatingSystem.IsLinux() || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUUBU_INVITATION_SMTP_WIRE_FIXTURE"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUUBU_SESSION_TEST_POSTGRES")))
            Skip="Requires private SMTP/TLS and PostgreSQL fixtures.";
    }
}

/// <summary>Actual SmtpClient wire tests. No send override or certificate-validation callback.</summary>
[SupportedOSPlatform("linux")]
public sealed class SmtpCompanyInvitationWireTests
{
    private const string User="synthetic-smtp-user";
    private const string Password="SyntheticOnlySmtpPassword123!";
    private const string Client="391bdeaa-87b3-44b3-8e64-2110dcc1a564";
    private enum Mode { Accept, NoStartTls, RejectRecipient, LoseAcknowledgement, HoldAcknowledgement }
    private static string Fixture()
    {
        var path=Environment.GetEnvironmentVariable("KUUBU_INVITATION_SMTP_WIRE_FIXTURE")!;
        Assert.True(Path.IsPathFullyQualified(path));Assert.Null(new DirectoryInfo(path).LinkTarget);
        Assert.Equal("kuubu-invitation-smtp-wire-v1",File.ReadAllText(Path.Combine(path,"scope.txt")));
        Assert.Equal(Path.Combine(path,"root.pem"),Environment.GetEnvironmentVariable("SSL_CERT_FILE"));
        Assert.Equal(Path.Combine(path,"trust"),Environment.GetEnvironmentVariable("SSL_CERT_DIR"));
        Assert.Equal(UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute,File.GetUnixFileMode(path));
        return path;
    }
    private sealed class Session
    {
        internal bool Tls,Authenticated,DataAccepted;
        internal SslProtocols Protocol;
        internal int CleartextSensitive;
        internal readonly List<string> Verbs=[];
        internal string? Message;
        public override string ToString()=>nameof(Session);
    }
    private sealed class Sink:IAsyncDisposable
    {
        private readonly TcpListener listener=new(IPAddress.Loopback,0);
        private readonly CancellationTokenSource stopping=new(TimeSpan.FromSeconds(30));
        private readonly X509Certificate2 certificate;
        private readonly Mode[] modes;
        private readonly Task task;
        internal readonly ConcurrentQueue<Session> Sessions=new();
        internal readonly TaskCompletionSource DataReceived=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Port=>((IPEndPoint)listener.LocalEndpoint).Port;
        internal Sink(string certificateName,params Mode[] modes)
        {
            var path=Path.Combine(Fixture(),certificateName+".pfx");
            Assert.Equal(UnixFileMode.UserRead|UnixFileMode.UserWrite,File.GetUnixFileMode(path));
            certificate=X509CertificateLoader.LoadPkcs12FromFile(path,"",X509KeyStorageFlags.EphemeralKeySet);
            this.modes=modes;listener.Start();task=Run();
        }
        private async Task Run()
        {
            foreach(var mode in modes) {
                try {
                    using var client=await listener.AcceptTcpClientAsync(stopping.Token);
                    var evidence=new Session();Sessions.Enqueue(evidence);
                    await Serve(client.GetStream(),mode,evidence,stopping.Token);
                }catch(Exception error)when(error is IOException or AuthenticationException or OperationCanceledException or SocketException){}
            }
        }
        private async Task Serve(Stream network,Mode mode,Session evidence,CancellationToken cancellation)
        {
            Stream stream=network;SslStream? tls=null;
            var reader=new StreamReader(stream,Encoding.ASCII,false,1024,leaveOpen:true);
            var writer=new StreamWriter(stream,Encoding.ASCII,1024,leaveOpen:true){NewLine="\r\n",AutoFlush=true};
            async Task Reply(string value)=>await writer.WriteLineAsync(value.AsMemory(),cancellation);
            async Task<string?> Line() {
                var value=await reader.ReadLineAsync(cancellation);
                if(value?.Length>8192)throw new IOException("Fixture line bound exceeded.");
                return value;
            }
            try {
                await Reply("220 private-smtp-fixture ESMTP");
                while(await Line() is { } line) {
                    var parts=line.Split(' ',StringSplitOptions.RemoveEmptyEntries);
                    var verb=parts.FirstOrDefault()?.ToUpperInvariant()??"";evidence.Verbs.Add(verb);
                    if(!evidence.Tls && verb is "AUTH" or "MAIL" or "RCPT" or "DATA"){
                        evidence.CleartextSensitive++;await Reply("530 STARTTLS required");continue;
                    }
                    if(verb is "EHLO" or "HELO") {
                        await Reply("250-private-smtp-fixture");
                        if(evidence.Tls)await Reply("250-AUTH LOGIN");
                        else if(mode!=Mode.NoStartTls)await Reply("250-STARTTLS");
                        await Reply("250 SIZE 65536");
                    } else if(verb=="STARTTLS") {
                        await Reply("220 Begin TLS");reader.Dispose();writer.Dispose();
                        tls=new SslStream(network,leaveInnerStreamOpen:true);
                        await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{
                            ServerCertificate=certificate,ClientCertificateRequired=false,
                            EnabledSslProtocols=SslProtocols.Tls12|SslProtocols.Tls13
                        },cancellation);
                        evidence.Tls=true;evidence.Protocol=tls.SslProtocol;stream=tls;
                        reader=new StreamReader(stream,Encoding.ASCII,false,1024,leaveOpen:true);
                        writer=new StreamWriter(stream,Encoding.ASCII,1024,leaveOpen:true){NewLine="\r\n",AutoFlush=true};
                    } else if(verb=="AUTH") {
                        string? suppliedUser;
                        if(parts.Length>=3)suppliedUser=parts[2];
                        else {await Reply("334 VXNlcm5hbWU6");suppliedUser=await Line();}
                        await Reply("334 UGFzc3dvcmQ6");var suppliedPassword=await Line();
                        evidence.Authenticated=suppliedUser==Convert.ToBase64String(Encoding.ASCII.GetBytes(User))
                            && suppliedPassword==Convert.ToBase64String(Encoding.ASCII.GetBytes(Password));
                        await Reply(evidence.Authenticated?"235 Authenticated":"535 Authentication failed");
                    } else if(verb=="MAIL")await Reply(evidence.Authenticated?"250 Sender accepted":"530 Authentication required");
                    else if(verb=="RCPT")await Reply(mode==Mode.RejectRecipient?"550 Synthetic recipient rejected":"250 Recipient accepted");
                    else if(verb=="DATA") {
                        await Reply("354 End with dot");var message=new StringBuilder();
                        while(await Line() is { } content && content!="."){
                            if(message.Length+content.Length>65536)throw new IOException("Fixture message bound exceeded.");
                            message.Append(content.StartsWith("..",StringComparison.Ordinal)?content[1..]:content).Append("\r\n");
                        }
                        evidence.Message=message.ToString();DataReceived.TrySetResult();
                        if(mode==Mode.LoseAcknowledgement)return;
                        if(mode==Mode.HoldAcknowledgement){await Task.Delay(Timeout.InfiniteTimeSpan,cancellation);return;}
                        await Reply("250 Queued by private fixture");evidence.DataAccepted=true;
                    } else if(verb=="QUIT"){await Reply("221 Closing");return;}
                    else if(verb is "RSET" or "NOOP")await Reply("250 OK");
                    else await Reply("500 Unsupported fixture command");
                }
            }finally{reader.Dispose();writer.Dispose();if(tls is not null)await tls.DisposeAsync();}
        }
        internal Task Complete()=>task.WaitAsync(TimeSpan.FromSeconds(15));
        public async ValueTask DisposeAsync()
        {
            await stopping.CancelAsync();listener.Stop();
            try{await task.WaitAsync(TimeSpan.FromSeconds(5));}catch(OperationCanceledException){}
            certificate.Dispose();stopping.Dispose();
        }
    }
    private static SmtpCompanyInvitationMail Sender(Sink sink,TimeProvider? clock=null)=>new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string,string?>{
            ["ERP:CompanyInvitationDelivery:Smtp:Host"]="smtp.fixture.test",
            ["ERP:CompanyInvitationDelivery:Smtp:Port"]=sink.Port.ToString(),
            ["ERP:CompanyInvitationDelivery:Smtp:From"]="hello@kuubuerp.com",
            ["ERP:CompanyInvitationDelivery:Smtp:Username"]=User,
            ["ERP:CompanyInvitationDelivery:Smtp:Password"]=Password
        }).Build(),clock??TimeProvider.System);
    private static InvitationMailEnvelope Envelope()=>new("invited@example.test","Synthetic company","member",
        "https://app.kuubuerp.com/accept-invitation#token="+ProductInvitationIntentStore.Random(),
        "<kuubu-invitation-"+Guid.NewGuid().ToString("D")+"@kuubuerp.com>",DateTimeOffset.UtcNow.AddMinutes(1),()=>true);
    private static (string Headers,string Body) Decode(string message)
    {
        var parts=message.Split("\r\n\r\n",2);Assert.Equal(2,parts.Length);
        Assert.True(parts[0].Contains("Content-Transfer-Encoding: base64",StringComparison.OrdinalIgnoreCase));
        return(parts[0],Encoding.UTF8.GetString(Convert.FromBase64String(parts[1])));
    }
    [InvitationSmtpWireFact]
    public async Task Trusted_STARTTLS_authenticates_and_transfers_the_actual_Kuubu_message()
    {
        await using var sink=new Sink("server",Mode.Accept);var envelope=Envelope();
        await Sender(sink).SendAsync(envelope,default);await sink.Complete();
        var session=Assert.Single(sink.Sessions);Assert.True(session.Tls && session.Authenticated && session.DataAccepted);
        Assert.True(session.Protocol is SslProtocols.Tls12 or SslProtocols.Tls13);Assert.Equal(0,session.CleartextSensitive);
        var decoded=Decode(session.Message!);
        Assert.True(decoded.Headers.Contains("hello@kuubuerp.com",StringComparison.Ordinal));
        Assert.True(decoded.Headers.Contains(envelope.MessageId,StringComparison.Ordinal));
        Assert.True(decoded.Body.Contains(envelope.ReviewUrl,StringComparison.Ordinal));
    }
    [InvitationSmtpWireFact]
    public async Task Missing_STARTTLS_never_sends_credentials_recipient_or_body()
    {
        await using var sink=new Sink("server",Mode.NoStartTls);
        await Assert.ThrowsAnyAsync<Exception>(()=>Sender(sink).SendAsync(Envelope(),default));await sink.Complete();
        var session=Assert.Single(sink.Sessions);Assert.Equal(0,session.CleartextSensitive);Assert.False(session.Authenticated);
        Assert.Contains("EHLO",session.Verbs);
        Assert.Null(session.Message);Assert.DoesNotContain("MAIL",session.Verbs);Assert.DoesNotContain("AUTH",session.Verbs);
    }
    private static async Task RejectCertificate(string name)
    {
        await using var sink=new Sink(name,Mode.Accept);
        await Assert.ThrowsAnyAsync<Exception>(()=>Sender(sink).SendAsync(Envelope(),default));await sink.Complete();
        var session=Assert.Single(sink.Sessions);Assert.Contains("STARTTLS",session.Verbs);
        Assert.False(session.Authenticated);Assert.Equal(0,session.CleartextSensitive);Assert.Null(session.Message);
        Assert.DoesNotContain("AUTH",session.Verbs);Assert.DoesNotContain("MAIL",session.Verbs);
    }
    [InvitationSmtpWireFact] public Task Untrusted_certificate_is_rejected_without_an_application_validation_override()=>RejectCertificate("untrusted");
    [InvitationSmtpWireFact] public Task Wrong_host_certificate_is_rejected_even_when_its_CA_is_trusted()=>RejectCertificate("wrong-host");
    [InvitationSmtpWireFact]
    public async Task Rejected_recipient_never_transfers_DATA_or_claims_acceptance()
    {
        await using var sink=new Sink("server",Mode.RejectRecipient);
        await Assert.ThrowsAnyAsync<Exception>(()=>Sender(sink).SendAsync(Envelope(),default));await sink.Complete();
        var session=Assert.Single(sink.Sessions);Assert.True(session.Tls && session.Authenticated);
        Assert.Contains("RCPT",session.Verbs);
        Assert.Null(session.Message);Assert.False(session.DataAccepted);Assert.DoesNotContain("DATA",session.Verbs);
    }
    [InvitationSmtpWireFact]
    public async Task Cancellation_while_acknowledgement_is_pending_is_not_reported_as_success()
    {
        await using var sink=new Sink("server",Mode.HoldAcknowledgement);using var stopping=new CancellationTokenSource();
        var sending=Sender(sink).SendAsync(Envelope(),stopping.Token);
        await sink.DataReceived.Task.WaitAsync(TimeSpan.FromSeconds(15));await stopping.CancelAsync();
        var error=await Record.ExceptionAsync(()=>sending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.NotNull(error);Assert.IsNotType<TimeoutException>(error);
        Assert.True(sending.IsCompleted && !sending.IsCompletedSuccessfully);
        var session=Assert.Single(sink.Sessions);Assert.True(session.Tls && session.Authenticated);Assert.False(session.DataAccepted);
    }
    private static HrmDbContext Publisher(CoreFixture core,OutboxPublisherTenantAccessor tenant)
    {
        using var original=core.Context();
        return new(new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(original.Database.GetConnectionString()).Options,tenant);
    }
    [InvitationSmtpStoreWireFact]
    public async Task PostgreSQL_worker_records_actual_TLS_acceptance_without_provisioning_access()
    {
        await using var core=await CoreFixture.Start();await using var sink=new Sink("server",Mode.Accept);
        var protection=new EphemeralDataProtectionProvider().CreateProtector(CoreCompanyInvitationsApi.DeliveryPurpose);
        Assert.Equal(CompanyInvitationStatus.Issued,(await new CoreCompanyInvitations(t=>core.Context(t),core.Clock,protection)
            .IssueAsync(core.Owner.Proof,new(core.Company,Guid.NewGuid(),"invited@example.test","member",true),core.License())).Status);
        var mail=new CoreCompanyInvitationMail(t=>core.Context(t),new(t=>core.Context(t),(_,_)=>Task.FromResult(new CommercialLicenseResult(CommercialLicenseStatus.Authorized,core.License())),core.Clock),
            protection,Sender(sink,core.Clock),core.Clock,Client);
        var tenant=new OutboxPublisherTenantAccessor();await using(var db=Publisher(core,tenant))
            Assert.True(await new CompanyInvitationDeliveryStore(db,tenant,core.Clock).ProcessNextAsync(mail.DeliverAsync,default));
        await sink.Complete();Assert.True(Assert.Single(sink.Sessions).DataAccepted);
        await using var check=core.Context();var row=await check.OutboxMessages.SingleAsync(x=>x.EventType==CompanyInvitationDeliveryStore.EventType);
        Assert.Equal("delivery-accepted",row.Status);Assert.Equal(1,row.PublishAttempts);Assert.Null(row.LastError);
        Assert.Single(await check.CompanyMemberships.ToArrayAsync());Assert.Single(await check.CompanyInvitationReceipts.ToArrayAsync());
        Assert.Empty(await check.LocalSessions.ToArrayAsync());Assert.Empty(await check.LocalCredentialLinks.ToArrayAsync());Assert.Empty(await check.TenantRoleAssignments.ToArrayAsync());
    }
    [InvitationSmtpStoreWireFact]
    public async Task Lost_SMTP_acknowledgement_retries_the_same_message_and_invitation_with_durable_outcomes()
    {
        await using var core=await CoreFixture.Start();await using var sink=new Sink("server",Mode.LoseAcknowledgement,Mode.Accept);
        var protection=new EphemeralDataProtectionProvider().CreateProtector(CoreCompanyInvitationsApi.DeliveryPurpose);
        Assert.Equal(CompanyInvitationStatus.Issued,(await new CoreCompanyInvitations(t=>core.Context(t),core.Clock,protection)
            .IssueAsync(core.Owner.Proof,new(core.Company,Guid.NewGuid(),"invited@example.test","member",true),core.License())).Status);
        var mail=new CoreCompanyInvitationMail(t=>core.Context(t),new(t=>core.Context(t),(_,_)=>Task.FromResult(new CommercialLicenseResult(CommercialLicenseStatus.Authorized,core.License())),core.Clock),
            protection,Sender(sink,core.Clock),core.Clock,Client);
        for(var attempt=1;attempt<=2;attempt++){
            var tenant=new OutboxPublisherTenantAccessor();await using(var db=Publisher(core,tenant))
                Assert.True(await new CompanyInvitationDeliveryStore(db,tenant,core.Clock).ProcessNextAsync(mail.DeliverAsync,default));
            await using var check=core.Context();var row=await check.OutboxMessages.SingleAsync(x=>x.EventType==CompanyInvitationDeliveryStore.EventType);
            Assert.Equal(attempt,row.PublishAttempts);Assert.Equal(attempt==1?"failed":"delivery-accepted",row.Status);
            if(attempt==1){Assert.Equal("invitation-delivery-unconfirmed",row.LastError);row.AvailableAt=core.Clock.GetUtcNow();await check.SaveChangesAsync();}
        }
        await sink.Complete();var sessions=sink.Sessions.ToArray();Assert.Equal(2,sessions.Length);
        Assert.False(sessions[0].DataAccepted);Assert.True(sessions[1].DataAccepted);
        var first=Decode(sessions[0].Message!);var second=Decode(sessions[1].Message!);
        Assert.True(first.Body==second.Body);
        string MessageId(string headers)=>Regex.Replace(headers,"\r\n[ \t]+"," ").Split("\r\n")
            .Single(x=>x.StartsWith("Message-ID:",StringComparison.OrdinalIgnoreCase)).Split(':',2)[1].Trim();
        var id=MessageId(first.Headers);Assert.StartsWith("<kuubu-invitation-",id);Assert.EndsWith("@kuubuerp.com>",id);
        Assert.True(id==MessageId(second.Headers));
        await using var final=core.Context();Assert.Single(await final.CompanyInvitations.ToArrayAsync());
        Assert.Single(await final.CompanyInvitationReceipts.ToArrayAsync());Assert.Single(await final.CompanyMemberships.ToArrayAsync());
        Assert.Equal(2,await final.AuditEntries.CountAsync(x=>x.Action=="core.company.invitation.delivery.attempted"));
    }
}
