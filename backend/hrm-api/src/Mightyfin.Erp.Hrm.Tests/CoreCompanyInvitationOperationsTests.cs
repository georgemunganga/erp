using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using CoreFixture=Mightyfin.Erp.Hrm.Tests.CoreCompanyTeamCommandsTests.Fixture;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreCompanyInvitationOperationsTests
{
    private static Dictionary<string,string?> Settings()=>new(){["ERP:CompanyInvitationOperations:Enabled"]="true",
        ["ERP:CompanyInvitationOperations:RetryEnabled"]="true",["ERP:CompanyInvitationOperations:Operator"]="ops_service_01",
        ["ERP:ProductIdentity:Enabled"]="true",["ERP:ProductLicensing:Enabled"]="true",["ERP:CompanyTeam:Enabled"]="true",
        ["ERP:CompanyInvitations:Enabled"]="true",["ERP:CompanyInvitationAcceptance:Enabled"]="true",["ERP:CompanyInvitationDelivery:Enabled"]="true"};
    private static string[] Arguments()=>[CoreCompanyInvitationOperations.Mode,"--operation=retry","--tenant="+Guid.NewGuid().ToString("D"),
        "--event="+Guid.NewGuid().ToString("D"),"--command-key="+Guid.NewGuid().ToString("D"),"--expected-attempts=1","--reason=relay-recovered"];
    private static IConfiguration Config(Dictionary<string,string?> values)=>new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    [Fact]
    public void Local_operator_mode_is_separate_from_API_migrations_and_identity_bootstrap()
    {
        var args=new[]{CoreCompanyInvitationOperations.Mode,"--operation=report"};var mode=CoreHostStartupPolicy.Parse(args);
        Assert.Equal(CoreHostMode.InvitationDeliveryOperations,mode);
        var settings=Settings();settings["ERP:Database:MigrateOnStartup"]="true";
        Assert.False(CoreHostStartupPolicy.MayApplyCoreMigrations(mode,Config(settings),true,args));
        Assert.False(CoreHostStartupPolicy.MayBootstrapLocalIdentity(mode));
        Assert.Throws<InvalidOperationException>(()=>CoreHostStartupPolicy.Parse([CoreCompanyInvitationOperations.Mode,"--run-outbox-publisher"]));
        Assert.Throws<InvalidOperationException>(()=>CoreHostStartupPolicy.Parse([CoreCompanyInvitationOperations.Mode,"--apply-migrations-only"]));
        Assert.Throws<InvalidOperationException>(()=>CoreHostStartupPolicy.Parse([CoreCompanyInvitationOperations.Mode+"=true"]));
    }
    [Fact]
    public void Reporting_needs_no_SMTP_or_product_identity_and_retry_is_separately_default_off()
    {
        var report=new[]{CoreCompanyInvitationOperations.Mode,"--operation=report"};
        Assert.Throws<InvalidOperationException>(()=>CoreCompanyInvitationOperations.Parse(report,new ConfigurationBuilder().Build()));
        var values=new Dictionary<string,string?>{["ERP:CompanyInvitationOperations:Enabled"]="true"};
        Assert.True(CoreCompanyInvitationOperations.Parse(report,Config(values)).Report);
        Assert.Throws<InvalidOperationException>(()=>CoreCompanyInvitationOperations.Parse(Arguments(),Config(values)));
        Assert.NotNull(CoreCompanyInvitationOperations.Parse(Arguments(),Config(Settings())).Retry);
    }
    [Theory]
    [InlineData("--urls=http://example.invalid")][InlineData("--operation=retry")][InlineData("--operator-id=another")]
    [InlineData("--expected-attempts=0")][InlineData("--expected-attempts=8")][InlineData("--tenant=foreign")]
    [InlineData("--reason=free-text")][InlineData("--command-key=00000000-0000-0000-0000-000000000000")]
    public void Unreviewed_duplicate_or_scope_changing_operator_arguments_fail_closed(string invalid)
    {
        var args=Arguments();var key=invalid.Split('=')[0];
        if(key=="--operation")args=[..args,invalid];
        else if(args.Any(x=>x.StartsWith(key+"=",StringComparison.Ordinal)))args=args.Select(x=>x.StartsWith(key+"=",StringComparison.Ordinal)?invalid:x).ToArray();
        else args=[..args,invalid];
        Assert.Throws<InvalidOperationException>(()=>CoreCompanyInvitationOperations.Parse(args,Config(Settings())));
    }
    [Theory]
    [InlineData("ops@example.test","relay-recovered",1)][InlineData("short","relay-recovered",1)]
    [InlineData("ops_service_01","unreviewed-reason",1)][InlineData("ops_service_01","relay-recovered",0)]
    [InlineData("ops_service_01","relay-recovered",8)]
    public void Operator_identity_reason_and_attempt_bounds_are_not_free_text(string actor,string reason,int attempt)
        =>Assert.False(CompanyInvitationDeliveryOperations.Valid(new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),attempt,actor,reason)));

    private sealed class Capture:ICompanyInvitationMail
    {
        internal int Count;
        public Task SendAsync(InvitationMailEnvelope envelope,CancellationToken ct){ct.ThrowIfCancellationRequested();Count++;return Task.CompletedTask;}
    }
    private sealed class Fixture(CoreFixture core):IAsyncDisposable
    {
        internal CoreFixture Core=>core;
        internal readonly IDataProtector Protection=new EphemeralDataProtectionProvider().CreateProtector(CoreCompanyInvitationsApi.DeliveryPurpose);
        internal readonly Capture Capture=new();
        internal bool Denied;
        internal CoreCompanyInvitationMail Mail=>new(t=>core.Context(t),new(t=>core.Context(t),(_,_)=>Task.FromResult(new CommercialLicenseResult(
            Denied?CommercialLicenseStatus.Denied:CommercialLicenseStatus.Authorized,Denied?null:core.License())),core.Clock),
            Protection,Capture,core.Clock,"391bdeaa-87b3-44b3-8e64-2110dcc1a564");
        internal HrmDbContext Publisher(OutboxPublisherTenantAccessor tenant)
        {using var source=core.Context();return new(new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(source.Database.GetConnectionString()).Options,tenant);}
        internal async Task<OutboxMessage> Failed()
        {
            var issued=await new CoreCompanyInvitations(t=>core.Context(t),core.Clock,Protection).IssueAsync(core.Owner.Proof,
                new(core.Company,Guid.NewGuid(),"invited@example.test","member",true),core.License());
            Assert.Equal(CompanyInvitationStatus.Issued,issued.Status);
            var tenant=new OutboxPublisherTenantAccessor();await using(var db=Publisher(tenant))
                Assert.True(await new CompanyInvitationDeliveryStore(db,tenant,core.Clock).ProcessNextAsync((_,_)=>Task.FromResult(InvitationMailOutcome.Retry),default));
            await using var check=core.Context();return await check.OutboxMessages.AsNoTracking().SingleAsync(x=>x.EventType==CompanyInvitationDeliveryStore.EventType);
        }
        internal InvitationDeliveryOperatorRequest Command(OutboxMessage row)=>new(Guid.Parse(core.Tenant),Guid.Parse(row.PublicId),Guid.NewGuid(),1,"ops_service_01","relay-recovered");
        internal async Task<InvitationDeliveryOperatorResult> Retry(InvitationDeliveryOperatorRequest command,Func<OutboxMessage,CancellationToken,Task<InvitationMailOutcome>>? mail=null,CancellationToken ct=default)
        {
            var tenant=new OutboxPublisherTenantAccessor();await using var db=Publisher(tenant);
            return await new CompanyInvitationDeliveryOperations(db,tenant,core.Clock).RetryAsync(command,mail??Mail.DeliverAsync,ct);
        }
        internal static async Task<Fixture> Start()=>new(await CoreFixture.Start());
        public ValueTask DisposeAsync()=>core.DisposeAsync();
    }
    [CoreCompanyTeamCommandsTests.NativeTeamFact]
    public async Task Native_report_is_read_only_and_one_due_operator_retry_has_a_durable_idempotent_receipt()
    {
        await using var f=await Fixture.Start();var row=await f.Failed();var command=f.Command(row);
        var tenant=new OutboxPublisherTenantAccessor();await using(var db=f.Publisher(tenant)){
            var before=await new CompanyInvitationDeliveryOperations(db,tenant,f.Core.Clock).ReportAsync(default);
            Assert.Equal(1,before.RetryWaiting);Assert.Equal(0,before.RetryDue);Assert.Equal(0,before.Accepted);
            Assert.DoesNotContain("@",JsonSerializer.Serialize(before));
        }
        Assert.Equal(InvitationDeliveryOperatorStatus.NotEligible,(await f.Retry(command)).Status);Assert.Equal(0,f.Capture.Count);
        f.Core.Clock.Advance();var first=await f.Retry(command);
        Assert.Equal(new(InvitationDeliveryOperatorStatus.Attempted,"delivery-accepted",2),first);
        Assert.Equal(new(InvitationDeliveryOperatorStatus.Existing,"delivery-accepted",2),await f.Retry(command));Assert.Equal(1,f.Capture.Count);
        Assert.Equal(InvitationDeliveryOperatorStatus.Conflict,(await f.Retry(command with{Reason="operator-verified"})).Status);
        Assert.Equal(InvitationDeliveryOperatorStatus.NotEligible,(await f.Retry(command with{Key=Guid.NewGuid()})).Status);
        await using var check=f.Core.Context();var saved=await check.OutboxMessages.SingleAsync(x=>x.Id==row.Id);
        Assert.Equal(2,saved.PublishAttempts);Assert.Equal(row.PayloadJson,saved.PayloadJson);Assert.Equal(row.PublicId,saved.PublicId);
        Assert.Equal(2,await check.AuditEntries.CountAsync(x=>x.Action=="core.company.invitation.delivery.attempted"));
        Assert.Single(await check.AuditEntries.Where(x=>x.Action==CompanyInvitationDeliveryOperations.AuditAction).ToArrayAsync());
        Assert.Single(await check.CompanyMemberships.ToArrayAsync());Assert.Empty(await check.LocalSessions.ToArrayAsync());
    }
    [CoreCompanyTeamCommandsTests.NativeTeamFact]
    public async Task Native_operator_cancellation_rolls_back_and_competing_workers_skip_the_same_row()
    {
        await using var f=await Fixture.Start();var row=await f.Failed();f.Core.Clock.Advance();var command=f.Command(row);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop=new CancellationTokenSource();
        var pending=f.Retry(command,async(_,ct)=>{entered.SetResult();await release.Task.WaitAsync(ct);return InvitationMailOutcome.Accepted;},stop.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try {
            Assert.Equal(InvitationDeliveryOperatorStatus.BusyOrMissing,(await f.Retry(command)).Status);
            var tenant=new OutboxPublisherTenantAccessor();await using var db=f.Publisher(tenant);
            Assert.False(await new CompanyInvitationDeliveryStore(db,tenant,f.Core.Clock).ProcessNextAsync(f.Mail.DeliverAsync,default));
        }finally {await stop.CancelAsync();release.TrySetResult();}
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending.WaitAsync(TimeSpan.FromSeconds(5)));
        await using var check=f.Core.Context();var saved=await check.OutboxMessages.SingleAsync(x=>x.Id==row.Id);
        Assert.Equal("failed",saved.Status);Assert.Equal(1,saved.PublishAttempts);
        Assert.Empty(await check.AuditEntries.Where(x=>x.Action==CompanyInvitationDeliveryOperations.AuditAction).ToArrayAsync());
    }
    [CoreCompanyTeamCommandsTests.NativeTeamFact]
    public async Task Native_operator_does_not_bypass_fresh_license_authority()
    {
        await using var f=await Fixture.Start();var row=await f.Failed();f.Core.Clock.Advance();f.Denied=true;
        var command=f.Command(row);
        Assert.Equal(new(InvitationDeliveryOperatorStatus.Attempted,"delivery-suppressed",2),await f.Retry(command));
        Assert.Equal(InvitationDeliveryOperatorStatus.NotEligible,(await f.Retry(command with{Key=Guid.NewGuid()})).Status);
        Assert.Equal(0,f.Capture.Count);await using var db=f.Core.Context();Assert.Single(await db.CompanyMemberships.ToArrayAsync());
        Assert.Empty(await db.LocalCredentialLinks.ToArrayAsync());Assert.Empty(await db.TenantRoleAssignments.ToArrayAsync());
    }
    [CoreCompanyTeamCommandsTests.NativeTeamFact]
    public async Task Native_operator_never_resets_exhausted_attempts()
    {
        await using var f=await Fixture.Start();var row=await f.Failed();var command=f.Command(row);
        for(var attempt=2;attempt<=8;attempt++) {
            await using(var check=f.Core.Context()){
                var due=await check.OutboxMessages.Where(x=>x.Id==row.Id).Select(x=>x.AvailableAt).SingleAsync();
                while(f.Core.Clock.GetUtcNow()<due)f.Core.Clock.Advance();
            }
            var tenant=new OutboxPublisherTenantAccessor();await using var db=f.Publisher(tenant);
            Assert.True(await new CompanyInvitationDeliveryStore(db,tenant,f.Core.Clock).ProcessNextAsync((_,_)=>Task.FromResult(InvitationMailOutcome.Retry),default));
        }
        Assert.Equal(InvitationDeliveryOperatorStatus.NotEligible,(await f.Retry(command with{ExpectedAttempts=7})).Status);
        await using var final=f.Core.Context();var exhausted=await final.OutboxMessages.SingleAsync(x=>x.Id==row.Id);
        Assert.Equal("delivery-exhausted",exhausted.Status);Assert.Equal(8,exhausted.PublishAttempts);
        Assert.Equal(8,await final.AuditEntries.CountAsync(x=>x.Action=="core.company.invitation.delivery.attempted"));
        Assert.Empty(await final.AuditEntries.Where(x=>x.Action==CompanyInvitationDeliveryOperations.AuditAction).ToArrayAsync());
        Assert.Equal(0,f.Capture.Count);
    }
    [CoreCompanyTeamCommandsTests.NativeTeamFact]
    public async Task Native_operator_rejects_revoked_and_foreign_targets_without_attempting_send()
    {
        await using var f=await Fixture.Start();var row=await f.Failed();var command=f.Command(row);
        Assert.Equal(InvitationDeliveryOperatorStatus.BusyOrMissing,(await f.Retry(command with{Tenant=Guid.NewGuid()})).Status);
        Assert.Equal(InvitationDeliveryOperatorStatus.BusyOrMissing,(await f.Retry(command with{Event=Guid.NewGuid()})).Status);
        f.Core.Clock.Advance();
        var revoked=await new CoreCompanyInvitations(t=>f.Core.Context(t),f.Core.Clock,f.Protection).RevokeAsync(f.Core.Owner.Proof,
            new(f.Core.Company,Guid.Parse(row.SubjectId),Guid.NewGuid(),1,true),f.Core.License());
        Assert.Equal(CompanyInvitationStatus.Revoked,revoked.Status);
        Assert.Equal(InvitationDeliveryOperatorStatus.NotEligible,(await f.Retry(command)).Status);
        Assert.Equal(0,f.Capture.Count);await using var check=f.Core.Context();
        Assert.Equal(1,(await check.OutboxMessages.SingleAsync(x=>x.Id==row.Id)).PublishAttempts);
        Assert.Empty(await check.AuditEntries.Where(x=>x.Action==CompanyInvitationDeliveryOperations.AuditAction).ToArrayAsync());
    }
    [CoreCompanyTeamCommandsTests.NativeTeamFact]
    public async Task Native_operator_cannot_revive_an_expired_invitation()
    {
        await using var f=await Fixture.Start();var row=await f.Failed();
        for(var i=0;i<1440;i++)f.Core.Clock.Advance();
        Assert.Equal(InvitationDeliveryOperatorStatus.NotEligible,(await f.Retry(f.Command(row))).Status);
        Assert.Equal(0,f.Capture.Count);await using var check=f.Core.Context();
        Assert.Equal(1,(await check.OutboxMessages.SingleAsync(x=>x.Id==row.Id)).PublishAttempts);
        Assert.Empty(await check.AuditEntries.Where(x=>x.Action==CompanyInvitationDeliveryOperations.AuditAction).ToArrayAsync());
    }
}
