using System.Diagnostics;
using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class InvitationOperationsExecutableFactAttribute:FactAttribute
{
    public InvitationOperationsExecutableFactAttribute()
    {
        if(!OperatingSystem.IsLinux() || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUUBU_OPERATIONS_EXECUTABLE_FIXTURE")))
            Skip="Requires an explicitly prepared isolated Laravel/PostgreSQL/TLS/operator-executable fixture.";
    }
}

/// <summary>Runs the unmodified API executable as a child, without replacing its HTTP or SMTP clients.</summary>
[SupportedOSPlatform("linux")]
public sealed class InvitationOperationsExecutableTests
{
    private sealed class Tenant(string value):ITenantAccessor {public string GetTenantId()=>value;}
    [InvitationOperationsExecutableFact]
    public async Task Actual_operator_executable_reads_Laravel_authority_decrypts_original_mail_and_uses_STARTTLS()
    {
        var fixture=Environment.GetEnvironmentVariable("KUUBU_OPERATIONS_EXECUTABLE_FIXTURE")!;
        Assert.Equal("/evidence/provider/core-interop.json",fixture);
        using var metadata=JsonDocument.Parse(await File.ReadAllTextAsync(fixture));
        string Field(string name)=>metadata.RootElement.GetProperty(name).GetString()!;
        Assert.Equal("greenwebb-loopback-interop-v1",Field("fixture_scope"));
        var clock=TimeProvider.System;
        using var tls=X509CertificateLoader.LoadPkcs12FromFile("/evidence/smtp-fixture/commercial.pfx","",X509KeyStorageFlags.EphemeralKeySet);
        var builder=WebApplication.CreateBuilder();builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server=>server.Listen(IPAddress.Loopback,443,listen=>listen.UseHttps(tls)));
        await using var gateway=builder.Build();
        using var upstream=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromSeconds(15)};
        var tokenRequests=0;var licenseRequests=0;
        gateway.Run(async http=>{
            var path=http.Request.Path.Value!;
            if(path!="/api/v1/product-identity/token" && !path.StartsWith("/api/v1/product-identity/licensing/enrollments/",StringComparison.Ordinal)){
                http.Response.StatusCode=404;return;
            }
            if(path.EndsWith("/token",StringComparison.Ordinal))Interlocked.Increment(ref tokenRequests);else Interlocked.Increment(ref licenseRequests);
            using var request=new HttpRequestMessage(new HttpMethod(http.Request.Method),"http://kuubu-ops-provider01:8101"+path);
            if(http.Request.ContentLength is >0)request.Content=new StreamContent(http.Request.Body);
            foreach(var name in new[]{"Authorization","Accept"})if(http.Request.Headers.TryGetValue(name,out var value))request.Headers.TryAddWithoutValidation(name,value.ToArray());
            if(request.Content is not null)request.Content.Headers.ContentType=System.Net.Http.Headers.MediaTypeHeaderValue.Parse(http.Request.ContentType!);
            using var response=await upstream.SendAsync(request,http.RequestAborted);
            http.Response.StatusCode=(int)response.StatusCode;http.Response.ContentType=response.Content.Headers.ContentType?.ToString();
            await response.Content.CopyToAsync(http.Response.Body,http.RequestAborted);
        });
        await gateway.StartAsync();
        try {
            var connection=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("KUUBU_SESSION_TEST_POSTGRES"));
            Assert.Equal("kuubu_session_test_control",connection.Database);
            await using(var control=new NpgsqlConnection(connection.ConnectionString)){
                await control.OpenAsync();connection.Database="company_team_"+Guid.NewGuid().ToString("N");
                await using var create=new NpgsqlCommand("CREATE DATABASE \""+connection.Database+"\"",control);await create.ExecuteNonQueryAsync();
            }
            var options=new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection.ConnectionString,p=>p.MigrationsHistoryTable("__hrm_migrations","hrm")).Options;
            HrmDbContext Context(string tenant)=>new(options,new Tenant(tenant));
            await using(var schema=Context("schema"))await CoreSchemaStartup.EnsureAsync(schema,"Isolated operator executable",mayApply:true);
            using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false});
            var licensing=new ProductCommercialLicense(http,clock,Field("licensing_client_id"),Field("licensing_secret"));
            var lease=await licensing.ReadAsync(Field("enrollment_id"));Assert.Equal(CommercialLicenseStatus.Authorized,lease.Status);
            // Explicit synthetic payer-owner setup authority; the actual setup/browser contract is qualified separately.
            var authority=new WorkspaceSetupObservation(Field("owner_subject"),Field("client_id"),Field("customer_id"),Field("enrollment_id"),2,
                clock.GetUtcNow().AddSeconds(60),clock,clock.GetTimestamp(),TimeSpan.FromSeconds(60));
            var company=await new CoreWorkspaceProvisioner(Context,clock).CreateAsync(authority,lease.Lease!,"operator_executable_setup_request_00000001","Operator qualification company");
            Assert.Equal(WorkspaceCreateStatus.Created,company.Status);
            string tenantId;CompanyTeamActor actor;
            await using(var read=Context("inspect")){
                var binding=await read.WorkspaceBindings.IgnoreQueryFilters().SingleAsync();tenantId=binding.TenantId;
                actor=new(ProductCommercialLicense.Issuer,Field("owner_subject"),(await read.ExternalIdentityLinks.IgnoreQueryFilters().SingleAsync()).Id);
            }
            var keys="/evidence/operator-keys";Directory.CreateDirectory(keys);File.SetUnixFileMode(keys,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
            var keyServices=new ServiceCollection();keyServices.AddLogging();keyServices.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keys)).SetApplicationName("Kuubu.ProductIdentity.v1");
            using var keyProvider=keyServices.BuildServiceProvider();var protection=keyProvider.GetRequiredService<IDataProtectionProvider>();
            var protector=protection.CreateProtector(CoreCompanyInvitationsApi.DeliveryPurpose);
            var team=new CoreCompanyTeamApi(Context,licensing.ReadAsync,clock);
            async Task<(string Event,int Attempts)> IssueFailed(string email)
            {
                var current=await licensing.ReadAsync(Field("enrollment_id"));Assert.Equal(CommercialLicenseStatus.Authorized,current.Status);
                var issued=await new CoreCompanyInvitations(Context,clock,protector).IssueAsync(actor,new(company.CompanyId!.Value,Guid.NewGuid(),email,"member",true),current.Lease!);
                Assert.Equal(CompanyInvitationStatus.Issued,issued.Status);
                await using var sink=new SmtpCompanyInvitationWireTests.Sink("server",SmtpCompanyInvitationWireTests.Mode.RejectRecipient);
                var mail=new CoreCompanyInvitationMail(Context,team,protector,SmtpCompanyInvitationWireTests.Sender(sink),clock,Field("client_id"));
                var bound=new OutboxPublisherTenantAccessor();await using var store=new HrmDbContext(options,bound);
                Assert.True(await new CompanyInvitationDeliveryStore(store,bound,clock).ProcessNextAsync(mail.DeliverAsync,default));
                await sink.Complete();var session=Assert.Single(sink.Sessions);Assert.True(session.Tls && session.Authenticated);Assert.False(session.DataAccepted);
                await using var read=Context(tenantId);var row=await read.OutboxMessages.SingleAsync(x=>x.SubjectId==issued.InvitationId!.Value.ToString("D") && x.EventType==CompanyInvitationDeliveryStore.EventType);
                Assert.Equal("failed",row.Status);Assert.Equal(1,row.PublishAttempts);
                var wait=row.AvailableAt-clock.GetUtcNow();if(wait>TimeSpan.Zero)await Task.Delay(wait+TimeSpan.FromMilliseconds(100));
                return(row.PublicId,row.PublishAttempts);
            }
            var settings=new Dictionary<string,string>{["ConnectionStrings__InvitationOperations"]=connection.ConnectionString,
                ["ERP__CompanyInvitationOperations__Enabled"]="true",["ERP__CompanyInvitationOperations__RetryEnabled"]="true",["ERP__CompanyInvitationOperations__Operator"]="ops_executable_01",
                ["ERP__ProductIdentity__Enabled"]="true",["ERP__ProductLicensing__Enabled"]="true",["ERP__CompanyTeam__Enabled"]="true",["ERP__CompanyInvitations__Enabled"]="true",["ERP__CompanyInvitationAcceptance__Enabled"]="true",["ERP__CompanyInvitationDelivery__Enabled"]="true",
                ["ERP__ProductIdentity__ClientId"]=Field("client_id"),["ERP__ProductIdentity__ClientSecret"]=Field("client_secret"),["ERP__ProductIdentity__DataProtectionPath"]=keys,
                ["ERP__ProductLicensing__ClientId"]=Field("licensing_client_id"),["ERP__ProductLicensing__ClientSecret"]=Field("licensing_secret"),
                ["ERP__CompanyInvitationAcceptance__IntentPath"]="/evidence/operator-intents",
                ["ERP__CompanyInvitationDelivery__Smtp__Host"]="smtp.fixture.test",["ERP__CompanyInvitationDelivery__Smtp__From"]="hello@kuubuerp.com",
                ["ERP__CompanyInvitationDelivery__Smtp__Username"]="synthetic-smtp-user",["ERP__CompanyInvitationDelivery__Smtp__Password"]="SyntheticOnlySmtpPassword123!"};
            Directory.CreateDirectory(settings["ERP__CompanyInvitationAcceptance__IntentPath"]);File.SetUnixFileMode(settings["ERP__CompanyInvitationAcceptance__IntentPath"],UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
            async Task<JsonDocument> Run(string eventId,Guid key,int port,int expectedExit,string expectedStatus,string reason="relay-recovered")
            {
                var start=new ProcessStartInfo("dotnet"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,WorkingDirectory="/tmp"};
                foreach(var argument in new[]{"/api/Mightyfin.Erp.Hrm.Api.dll",CoreCompanyInvitationOperations.Mode,"--operation=retry","--tenant="+tenantId,"--event="+eventId,"--command-key="+key.ToString("D"),"--expected-attempts=1","--reason="+reason})start.ArgumentList.Add(argument);
                foreach(var setting in settings)start.Environment[setting.Key]=setting.Value;
                start.Environment["ERP__CompanyInvitationDelivery__Smtp__Port"]=port.ToString();
                using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
                try {await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(55));}finally{if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync();}}
                Assert.Equal(expectedExit,process.ExitCode);Assert.True(string.IsNullOrWhiteSpace(await error));
                var result=JsonDocument.Parse(await output);Assert.Equal(expectedStatus,result.RootElement.GetProperty("status").GetString());return result;
            }
            var first=await IssueFailed("invited@example.test");
            var originalKeys=Directory.GetFiles(keys).ToDictionary(x=>x,x=>File.ReadAllBytes(x));Assert.NotEmpty(originalKeys);
            await using(var sink=new SmtpCompanyInvitationWireTests.Sink("server",SmtpCompanyInvitationWireTests.Mode.Accept)){
                var key=Guid.NewGuid();using var sent=await Run(first.Event,key,sink.Port,0,"Attempted");
                Assert.Equal("delivery-accepted",sent.RootElement.GetProperty("outcome_state").GetString());await sink.Complete();
                using var replay=await Run(first.Event,key,sink.Port,0,"Existing");Assert.True(replay.RootElement.GetProperty("historical").GetBoolean());
                using var conflict=await Run(first.Event,key,sink.Port,3,"Conflict","operator-verified");
                var session=Assert.Single(sink.Sessions);Assert.True(session.Tls && session.Authenticated && session.DataAccepted);Assert.Equal(0,session.CleartextSensitive);
                var message=SmtpCompanyInvitationWireTests.Decode(session.Message!);
                Assert.Contains("https://app.kuubuerp.com/accept-invitation#token=",message.Body);
            }
            var second=await IssueFailed("second@example.test");
            using(var request=new HttpRequestMessage(HttpMethod.Post,"http://kuubu-ops-provider01:8101/__fixture/suspend")){
                request.Headers.Add("X-Kuubu-Test-Fixture",Field("fixture_marker"));using var response=await upstream.SendAsync(request);Assert.Equal(HttpStatusCode.NoContent,response.StatusCode);
            }
            using(var denied=await Run(second.Event,Guid.NewGuid(),1,3,"Attempted"))Assert.Equal("delivery-suppressed",denied.RootElement.GetProperty("outcome_state").GetString());
            Assert.Equal(originalKeys.Keys.Order(),Directory.GetFiles(keys).Order());foreach(var pair in originalKeys)Assert.Equal(pair.Value,File.ReadAllBytes(pair.Key));
            await using var check=Context(tenantId);
            Assert.Equal(2,await check.AuditEntries.CountAsync(x=>x.Action==CompanyInvitationDeliveryOperations.AuditAction));
            Assert.Equal(4,await check.AuditEntries.CountAsync(x=>x.Action=="core.company.invitation.delivery.attempted"));
            Assert.Single(await check.CompanyMemberships.ToArrayAsync());Assert.Empty(await check.LocalSessions.ToArrayAsync());
            Assert.Empty(await check.LocalCredentialLinks.ToArrayAsync());Assert.Empty(await check.TenantRoleAssignments.ToArrayAsync());
            Assert.True(tokenRequests>=3 && licenseRequests>=4);
            await File.WriteAllTextAsync("/evidence/operator-executable.json",JsonSerializer.Serialize(new{passed=true,database=connection.Database,
                token_requests=tokenRequests,license_requests=licenseRequests,operator_receipts=2,attempts=4,smtp_accepted=1,key_ring_unchanged=true,production_changes=false}));
        }finally{await gateway.StopAsync();}
    }
}
