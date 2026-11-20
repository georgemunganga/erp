using System.Net.Mail;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using CoreFixture=Mightyfin.Erp.Hrm.Tests.CoreCompanyTeamCommandsTests.Fixture;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreCompanyInvitationMailTests
{
    [Fact]
    public void Delivery_requires_reviewed_dependencies_and_non_API_modes_do_not_register_SMTP()
    {
        var empty=new ConfigurationBuilder().Build();var services=new ServiceCollection();
        services.AddCoreCompanyTeamApi(empty);Assert.DoesNotContain(services,x=>x.ServiceType==typeof(CoreCompanyInvitationDeliveryWorker));
        var data=new Dictionary<string,string?>{["ERP:CompanyInvitationDelivery:Enabled"]="true"};
        Assert.Throws<InvalidOperationException>(()=>new ServiceCollection().AddCoreCompanyTeamApi(new ConfigurationBuilder().AddInMemoryCollection(data).Build()));
        foreach(var key in new[]{"ProductIdentity","ProductLicensing","CompanyTeam","CompanyInvitations"})data["ERP:"+key+":Enabled"]="true";
        Assert.Throws<InvalidOperationException>(()=>new ServiceCollection().AddCoreCompanyTeamApi(new ConfigurationBuilder().AddInMemoryCollection(data).Build()));
        data["ERP:CompanyInvitationAcceptance:Enabled"]="true";
        var config=new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        const string database="Host=127.0.0.1;Database=synthetic_not_connected;Username=synthetic";
        var owner=new ServiceCollection();owner.AddCoreCompanyTeamApi(config,database,runInvitationDelivery:false);
        Assert.DoesNotContain(owner,x=>x.ServiceType==typeof(ICompanyInvitationMail));
        Assert.DoesNotContain(owner,x=>x.ServiceType==typeof(CoreCompanyInvitationDeliveryWorker));
        Assert.Throws<InvalidOperationException>(()=>new ServiceCollection().AddCoreCompanyTeamApi(config,database,runInvitationDelivery:true));
    }
    private const string Client="391bdeaa-87b3-44b3-8e64-2110dcc1a564";
    private sealed class Capture:ICompanyInvitationMail
    {
        internal readonly List<InvitationMailEnvelope> Messages=[];
        internal bool Fail;
        internal Func<Task>? BeforeReturn;
        public async Task SendAsync(InvitationMailEnvelope envelope,CancellationToken cancellation) {
            cancellation.ThrowIfCancellationRequested();Messages.Add(envelope);
            if(BeforeReturn is not null)await BeforeReturn();
            if(Fail)throw new IOException("Synthetic private transport failure");
        }
    }
    private sealed class Fixture(CoreFixture core,IDataProtector protection):IAsyncDisposable
    {
        internal CoreFixture Core=>core;
        internal readonly Capture Capture=new();
        internal readonly ProductCompanyTeamHttpTests.LicenseState State=new(core);
        internal CoreCompanyInvitations Invitations=>new(x=>core.Context(x),core.Clock,protection);
        internal CoreCompanyInvitationMail Mail=>new(x=>core.Context(x),new(x=>core.Context(x),State.Read,core.Clock),
            protection,Capture,core.Clock,Client);
        internal async Task<OutboxMessage> Issue() {
            var result=await Invitations.IssueAsync(core.Owner.Proof,new(core.Company,Guid.NewGuid(),"invited@example.test","member",true),core.License());
            Assert.Equal(CompanyInvitationStatus.Issued,result.Status);
            await using var db=core.Context();return await db.OutboxMessages.AsNoTracking().SingleAsync(x=>x.EventType==CompanyInvitationDeliveryStore.EventType);
        }
        internal static async Task<Fixture> Start()=>new(await CoreFixture.Start(),new EphemeralDataProtectionProvider().CreateProtector(CoreCompanyInvitationsApi.DeliveryPurpose));
        public ValueTask DisposeAsync()=>core.DisposeAsync();
    }
    [Fact]
    public async Task Current_exact_intent_uses_fresh_license_and_protected_fixed_link_without_new_access()
    {
        await using var f=await Fixture.Start();var intent=await f.Issue();
        Assert.Equal(InvitationMailOutcome.Accepted,await f.Mail.DeliverAsync(intent,default));
        var mail=Assert.Single(f.Capture.Messages);Assert.True(mail.Recipient=="invited@example.test");
        var uri=new Uri(mail.ReviewUrl);Assert.Equal("https://app.kuubuerp.com",uri.GetLeftPart(UriPartial.Authority));
        Assert.Equal("/accept-invitation",uri.AbsolutePath);Assert.Empty(uri.Query);
        Assert.True(ProductInvitationIntentStore.Opaque(new Uri(mail.ReviewUrl).Fragment[7..]));
        Assert.Equal(1,f.State.Reads);Assert.Equal("01arz3ndektsv4rrffq69g5fav",f.State.LastEnrollment);
        Assert.Equal(nameof(InvitationMailEnvelope),mail.ToString());
        await using var db=f.Core.Context();Assert.Single(await db.CompanyMemberships.ToArrayAsync());
        Assert.Single(await db.CompanyInvitationReceipts.ToArrayAsync());
        Assert.Empty(await db.LocalSessions.ToArrayAsync());Assert.Empty(await db.LocalCredentialLinks.ToArrayAsync());
        Assert.Empty(await db.TenantRoleAssignments.ToArrayAsync());
    }
    [Theory]
    [InlineData("expired")][InlineData("revoked")][InlineData("license")][InlineData("inviter")][InlineData("late-revoke")]
    public async Task Stale_intents_or_current_authority_denial_never_send(string fault)
    {
        await using var f=await Fixture.Start();var intent=await f.Issue();
        async Task Revoke()=>Assert.Equal(CompanyInvitationStatus.Revoked,(await f.Invitations.RevokeAsync(f.Core.Owner.Proof,
            new(f.Core.Company,Guid.Parse(intent.SubjectId),Guid.NewGuid(),1,true),f.Core.License())).Status);
        if(fault=="revoked")await Revoke();
        if(fault=="expired")for(var i=0;i<1441;i++)f.Core.Clock.Advance();
        if(fault=="license")f.State.Status=CommercialLicenseStatus.Denied;
        if(fault=="inviter"){
            await using var db=f.Core.Context();(await db.LocalUsers.SingleAsync(x=>x.Id==f.Core.Owner.User)).IsActive=false;await db.SaveChangesAsync();
        }
        if(fault=="late-revoke")f.State.BeforeReturn=Revoke;
        Assert.Equal(InvitationMailOutcome.Suppressed,await f.Mail.DeliverAsync(intent,default));Assert.Empty(f.Capture.Messages);
    }
    [Theory]
    [InlineData("scope")][InlineData("event")][InlineData("version")][InlineData("subject")][InlineData("extra")][InlineData("duplicate")][InlineData("correlation")][InlineData("actor")]
    public async Task Queue_selectors_cannot_substitute_for_the_committed_intent(string fault)
    {
        await using var f=await Fixture.Start();var intent=await f.Issue();
        if(fault=="scope")intent.TenantId="foreign-tenant";
        if(fault=="event")intent.EventType="hrm.account.access_link";
        if(fault=="version")intent.EventVersion="2";
        if(fault=="subject")intent.SubjectId=Guid.NewGuid().ToString("D");
        if(fault=="extra")intent.PayloadJson=intent.PayloadJson[..^1]+",\"email\":\"other@example.test\"}";
        if(fault=="duplicate")intent.PayloadJson=intent.PayloadJson[..^1]+",\"company_id\":\""+f.Core.Company+"\"}";
        if(fault=="correlation")intent.CorrelationId=new string('0',64);
        if(fault=="actor")intent.CreatedBy="other";
        Assert.Equal(InvitationMailOutcome.Invalid,await f.Mail.DeliverAsync(intent,default));Assert.Empty(f.Capture.Messages);
    }
    [Fact]
    public async Task Unavailable_license_and_transport_errors_are_retryable_and_message_identity_is_stable()
    {
        await using var f=await Fixture.Start();var intent=await f.Issue();
        f.State.Status=CommercialLicenseStatus.Unavailable;
        Assert.Equal(InvitationMailOutcome.Retry,await f.Mail.DeliverAsync(intent,default));Assert.Empty(f.Capture.Messages);
        f.State.Status=CommercialLicenseStatus.Authorized;f.Capture.Fail=true;
        Assert.Equal(InvitationMailOutcome.Retry,await f.Mail.DeliverAsync(intent,default));
        f.Capture.Fail=false;Assert.Equal(InvitationMailOutcome.Accepted,await f.Mail.DeliverAsync(intent,default));
        Assert.Equal(2,f.Capture.Messages.Count);
        Assert.True(f.Capture.Messages[0].MessageId==f.Capture.Messages[1].MessageId);
        Assert.True(f.Capture.Messages[0].ReviewUrl==f.Capture.Messages[1].ReviewUrl);
    }
    [Fact]
    public async Task Generic_publisher_cannot_acknowledge_an_invitation_as_smtp_skipped()
    {
        await using var f=await Fixture.Start();var intent=await f.Issue();
        await using var db=f.Core.Context();
        var store=new EfOutboxPublisherStore(db,new OutboxPublisherTenantAccessor());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>store.CompleteAsync(intent.Id,true,"smtp-skipped",null,default));
        Assert.Equal("pending",(await db.OutboxMessages.AsNoTracking().SingleAsync(x=>x.Id==intent.Id)).Status);
    }
    [Fact]
    public async Task Smtp_adapter_uses_Kuubu_sender_plain_body_and_stable_id_with_no_automatic_access()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["ERP:CompanyInvitationDelivery:Smtp:Host"]="smtp.example.test",["ERP:CompanyInvitationDelivery:Smtp:From"]="hello@kuubuerp.com"}).Build();
        var sent=0;
        var smtp=new SmtpCompanyInvitationMail(config,TimeProvider.System,(message,cancellation)=>{
            sent++;Assert.Equal("Kuubu",message.From!.DisplayName);Assert.False(message.IsBodyHtml);
            Assert.Contains("Company acceptance and module permissions are separate.",message.Body);
            Assert.Equal("<synthetic@kuubuerp.com>",message.Headers["Message-ID"]);return Task.CompletedTask;
        });
        var envelope=new InvitationMailEnvelope("invited@example.test","Example Company","member","https://app.kuubuerp.com/accept-invitation#token="+ProductInvitationIntentStore.Random(),
            "<synthetic@kuubuerp.com>",DateTimeOffset.UtcNow.AddMinutes(1),()=>true);
        await smtp.SendAsync(envelope,default);Assert.Equal(1,sent);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>smtp.SendAsync(envelope with{IsCurrent=()=>false},default));Assert.Equal(1,sent);
    }
    [Theory]
    [InlineData("Host","")][InlineData("Host","https://smtp.example.test")]
    [InlineData("Host","127.0.0.1")][InlineData("Host","::1")][InlineData("Host","2130706433")]
    [InlineData("From","hello@kuubuerp.com\r\nBcc:other@example.test")]
    [InlineData("Port","0")][InlineData("Port","bad")][InlineData("Username","unpaired")]
    public void Invalid_SMTP_settings_fail_without_echoing_configuration(string name,string value)
    {
        var data=new Dictionary<string,string?>{
            ["ERP:CompanyInvitationDelivery:Smtp:Host"]="smtp.example.test",["ERP:CompanyInvitationDelivery:Smtp:From"]="hello@kuubuerp.com"};
        data["ERP:CompanyInvitationDelivery:Smtp:"+name]=value;
        var error=Assert.Throws<InvalidOperationException>(()=>new SmtpCompanyInvitationMail(new ConfigurationBuilder().AddInMemoryCollection(data).Build(),TimeProvider.System));
        Assert.Equal("Reviewed Kuubu SMTP settings are required.",error.Message);
    }
    private static HrmDbContext PublisherContext(CoreFixture core,OutboxPublisherTenantAccessor tenant)
    {
        using var db=core.Context();
        return new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(db.Database.GetConnectionString()).Options,tenant);
    }
    private sealed class Scope(string tenant):ITenantAccessor {public string GetTenantId()=>tenant;}
    [CoreCompanyTeamCommandsTests.NativeTeamFact]
    public async Task Native_invitation_revocation_waits_for_the_bounded_send_authority_lock()
    {
        await using var f=await Fixture.Start();var intent=await f.Issue();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Capture.BeforeReturn=async()=>{entered.TrySetResult();await release.Task.WaitAsync(TimeSpan.FromSeconds(15));};
        var tenant=new OutboxPublisherTenantAccessor();await using var publisher=PublisherContext(f.Core,tenant);
        var sending=new CompanyInvitationDeliveryStore(publisher,tenant,f.Core.Clock).ProcessNextAsync(f.Mail.DeliverAsync,default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        using var source=f.Core.Context();
        var appName="invitation-revoke-probe-"+Guid.NewGuid().ToString("N");
        var connection=new Npgsql.NpgsqlConnectionStringBuilder(source.Database.GetConnectionString()){ApplicationName=appName}.ConnectionString;
        var options=new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection).Options;
        var revoker=new CoreCompanyInvitations(t=>new HrmDbContext(options,new Scope(t)),f.Core.Clock,
            new EphemeralDataProtectionProvider().CreateProtector(CoreCompanyInvitationsApi.DeliveryPurpose));
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var revoking=revoker.RevokeAsync(f.Core.Owner.Proof,new(f.Core.Company,Guid.Parse(intent.SubjectId),Guid.NewGuid(),1,true),f.Core.License(),deadline.Token);
        try {
            await source.Database.OpenConnectionAsync();var observed=false;
            for(var i=0;i<100;i++){
                await using var query=new Npgsql.NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE application_name=@app AND wait_event_type='Lock'",(Npgsql.NpgsqlConnection)source.Database.GetDbConnection());
                query.Parameters.AddWithValue("app",appName);
                if(Convert.ToInt32(await query.ExecuteScalarAsync())>0){observed=true;break;}
                await Task.Delay(25);
            }
            Assert.True(observed,"The revocation must be observed waiting on PostgreSQL's authority lock.");
            Assert.False(revoking.IsCompleted);
        }finally{release.TrySetResult();}
        Assert.True(await sending);Assert.Equal(CompanyInvitationStatus.Revoked,(await revoking).Status);
        using var pool=new Npgsql.NpgsqlConnection(connection);Npgsql.NpgsqlConnection.ClearPool(pool);
        Assert.Single(f.Capture.Messages);
    }
    [CoreCompanyTeamCommandsTests.NativeTeamFact]
    public async Task Native_worker_acknowledges_once_and_generic_publisher_never_claims_the_delivery()
    {
        await using var f=await Fixture.Start();var intent=await f.Issue();var tenant=new OutboxPublisherTenantAccessor();
        await using(var db=PublisherContext(f.Core,tenant)){
            var generic=new EfOutboxPublisherStore(db,tenant);var claimed=await generic.ClaimAsync(50,default);
            Assert.DoesNotContain(claimed,x=>x.Id==intent.Id);
        }
        await using(var db=PublisherContext(f.Core,tenant)){
            var store=new CompanyInvitationDeliveryStore(db,tenant,f.Core.Clock);
            Assert.True(await store.ProcessNextAsync(f.Mail.DeliverAsync,default));
            Assert.False(await store.ProcessNextAsync(f.Mail.DeliverAsync,default));
        }
        Assert.Single(f.Capture.Messages);
        await using var check=f.Core.Context();var row=await check.OutboxMessages.SingleAsync(x=>x.Id==intent.Id);
        Assert.Equal("delivery-accepted",row.Status);Assert.Equal(1,row.PublishAttempts);Assert.Null(row.LastError);
        Assert.Single(await check.AuditEntries.Where(x=>x.Action=="core.company.invitation.delivery.attempted").ToArrayAsync());
        Assert.Single(await check.CompanyMemberships.ToArrayAsync());Assert.Single(await check.CompanyInvitationReceipts.ToArrayAsync());
    }
    [CoreCompanyTeamCommandsTests.NativeTeamFact]
    public async Task Native_competing_workers_skip_a_locked_delivery_without_duplicate_send()
    {
        await using var f=await Fixture.Start();await f.Issue();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var one=new OutboxPublisherTenantAccessor();var two=new OutboxPublisherTenantAccessor();
        await using var first=PublisherContext(f.Core,one);await using var second=PublisherContext(f.Core,two);
        var pending=new CompanyInvitationDeliveryStore(first,one,f.Core.Clock).ProcessNextAsync(async(row,ct)=>{
            entered.SetResult();await release.Task.WaitAsync(TimeSpan.FromSeconds(15),ct);return await f.Mail.DeliverAsync(row,ct);
        },default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try {Assert.False(await new CompanyInvitationDeliveryStore(second,two,f.Core.Clock).ProcessNextAsync(f.Mail.DeliverAsync,default));}
        finally {release.TrySetResult();}
        Assert.True(await pending);Assert.Single(f.Capture.Messages);
    }
    [CoreCompanyTeamCommandsTests.NativeTeamFact]
    public async Task Native_failed_attempt_retries_same_intent_then_stops_at_the_attempt_cap()
    {
        await using var f=await Fixture.Start();var intent=await f.Issue();
        using(var cancelled=new CancellationTokenSource()) {
            var scoped=new OutboxPublisherTenantAccessor();await using var db=PublisherContext(f.Core,scoped);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new CompanyInvitationDeliveryStore(db,scoped,f.Core.Clock)
                .ProcessNextAsync((_,ct)=>{cancelled.Cancel();return Task.FromCanceled<InvitationMailOutcome>(ct);},cancelled.Token));
        }
        await using(var check=f.Core.Context()){
            var unchanged=await check.OutboxMessages.SingleAsync(x=>x.Id==intent.Id);
            Assert.Equal("pending",unchanged.Status);Assert.Equal(0,unchanged.PublishAttempts);
            Assert.Empty(await check.AuditEntries.Where(x=>x.Action=="core.company.invitation.delivery.attempted").ToArrayAsync());
        }
        for(var attempt=1;attempt<=8;attempt++) {
            var tenant=new OutboxPublisherTenantAccessor();await using(var db=PublisherContext(f.Core,tenant))
                Assert.True(await new CompanyInvitationDeliveryStore(db,tenant,f.Core.Clock).ProcessNextAsync((_,_)=>Task.FromResult(InvitationMailOutcome.Retry),default));
            await using var check=f.Core.Context();var row=await check.OutboxMessages.SingleAsync(x=>x.Id==intent.Id);
            Assert.Equal(attempt,row.PublishAttempts);Assert.Equal(attempt==8?"delivery-exhausted":"failed",row.Status);
            Assert.Equal("invitation-delivery-unconfirmed",row.LastError);
            if(attempt<8) {row.AvailableAt=f.Core.Clock.GetUtcNow();await check.SaveChangesAsync();}
        }
        var last=new OutboxPublisherTenantAccessor();await using var final=PublisherContext(f.Core,last);
        Assert.False(await new CompanyInvitationDeliveryStore(final,last,f.Core.Clock).ProcessNextAsync(f.Mail.DeliverAsync,default));
        Assert.Empty(f.Capture.Messages);
    }
}
