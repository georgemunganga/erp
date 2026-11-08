using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreCompanyTeamCommandsTests
{
    [Fact]
    public async Task Confirmed_role_change_is_audited_once_and_replay_is_a_historical_receipt_not_current_access()
    {
        await using var f=await Fixture.Start(); var target=await f.Add("43",CoreCompanyRoles.Member);
        var command=f.Command(target,CompanyTeamOperation.ChangeRole,CoreCompanyRoles.Administrator);
        var result=await f.Run(f.Owner,command); Assert.Equal(CompanyTeamStatus.Changed,result.Status); Assert.Equal(2,result.Revision);
        Assert.Equal(CompanyTeamStatus.Existing,(await f.Run(f.Owner,command)).Status);
        Assert.Equal(CompanyTeamStatus.Conflict,(await f.Run(f.Owner,command with {Role=CoreCompanyRoles.Owner})).Status);
        Assert.Equal(CompanyTeamStatus.Conflict,(await f.Run(f.Owner,command with {CommandKey=Guid.NewGuid()})).Status);
        Assert.Equal(CompanyTeamStatus.Changed,(await f.Run(f.Owner,command with {CommandKey=Guid.NewGuid(),ExpectedRevision=2,Role=CoreCompanyRoles.Owner})).Status);
        var replay=await f.Run(f.Owner,command); Assert.Equal(CompanyTeamStatus.Existing,replay.Status);
        Assert.Equal(CoreCompanyRoles.Administrator,replay.Role); Assert.Equal(2,replay.Revision);
        await using var db=f.Context(); Assert.Equal(CoreCompanyRoles.Owner,(await db.CompanyMemberships.SingleAsync(x=>x.Id==target.Member)).Role);
        await f.AssertEffects(2,2);
    }

    [Theory]
    [InlineData(true,null)]
    [InlineData(false,CoreCompanyRoles.Member)]
    public async Task Last_owner_cannot_be_removed_or_demoted(bool revoke,string? role)
    {
        await using var f=await Fixture.Start();
        Assert.Equal(CompanyTeamStatus.LastOwner,(await f.Run(f.Owner,f.Command(f.Owner,revoke?CompanyTeamOperation.Revoke:CompanyTeamOperation.ChangeRole,role))).Status);
        await f.AssertEffects(0,0);
    }

    [Theory]
    [InlineData("inactive-member")][InlineData("inactive-user")][InlineData("revoked-link")][InlineData("locked-user")]
    public async Task An_unusable_other_owner_does_not_satisfy_owner_handover(string fault)
    {
        await using var f=await Fixture.Start(); var other=await f.Add("43",CoreCompanyRoles.Owner);
        await using(var db=f.Context()){
            if(fault=="inactive-member") (await db.CompanyMemberships.SingleAsync(x=>x.Id==other.Member)).IsActive=false;
            if(fault=="inactive-user") (await db.LocalUsers.SingleAsync(x=>x.Id==other.User)).IsActive=false;
            if(fault=="revoked-link") (await db.ExternalIdentityLinks.SingleAsync(x=>x.Id==other.Link)).IsActive=false;
            if(fault=="locked-user") (await db.LocalUsers.SingleAsync(x=>x.Id==other.User)).LockedUntil=f.Clock.GetUtcNow().AddHours(1);
            await db.SaveChangesAsync();
        }
        Assert.Equal(CompanyTeamStatus.LastOwner,(await f.Run(f.Owner,f.Command(f.Owner,CompanyTeamOperation.Revoke))).Status);
        await f.AssertEffects(0,0);
    }

    [Fact]
    public async Task Explicit_owner_handover_preserves_an_owner_and_removed_authority_cannot_replay()
    {
        await using var f=await Fixture.Start(); await f.Add("43",CoreCompanyRoles.Owner);
        var command=f.Command(f.Owner,CompanyTeamOperation.ChangeRole,CoreCompanyRoles.Member);
        Assert.Equal(CompanyTeamStatus.Changed,(await f.Run(f.Owner,command)).Status);
        Assert.Equal(CompanyTeamStatus.Denied,(await f.Run(f.Owner,command)).Status);
        await f.AssertEffects(1,1);
    }

    [Fact]
    public async Task Demotion_to_administrator_cannot_replay_an_owner_only_role_command()
    {
        await using var f=await Fixture.Start();await f.Add("43",CoreCompanyRoles.Owner);
        var command=f.Command(f.Owner,CompanyTeamOperation.ChangeRole,CoreCompanyRoles.Administrator);
        Assert.Equal(CompanyTeamStatus.Changed,(await f.Run(f.Owner,command)).Status);
        Assert.Equal(CompanyTeamStatus.Denied,(await f.Run(f.Owner,command)).Status);
        await f.AssertEffects(1,1);
    }

    [Fact]
    public async Task Administrator_can_revoke_members_but_cannot_change_roles_or_remove_privileged_members()
    {
        await using var f=await Fixture.Start(); var admin=await f.Add("43",CoreCompanyRoles.Administrator);
        var member=await f.Add("44",CoreCompanyRoles.Member); var peer=await f.Add("45",CoreCompanyRoles.Administrator);
        Assert.Equal(CompanyTeamStatus.Changed,(await f.Run(admin,f.Command(member,CompanyTeamOperation.Revoke))).Status);
        Assert.Equal(CompanyTeamStatus.Unchanged,(await f.Run(admin,f.Command(member,CompanyTeamOperation.Revoke) with {ExpectedRevision=2})).Status);
        Assert.Equal(CompanyTeamStatus.Denied,(await f.Run(admin,f.Command(peer,CompanyTeamOperation.Revoke))).Status);
        Assert.Equal(CompanyTeamStatus.Denied,(await f.Run(admin,f.Command(f.Owner,CompanyTeamOperation.Revoke))).Status);
        var another=await f.Add("46",CoreCompanyRoles.Member);
        Assert.Equal(CompanyTeamStatus.Denied,(await f.Run(admin,f.Command(another,CompanyTeamOperation.ChangeRole,CoreCompanyRoles.Owner))).Status);
        await f.AssertEffects(1,2);
    }

    [Fact]
    public async Task Ordinary_members_and_revoked_actor_links_cannot_mutate_or_replay()
    {
        await using var f=await Fixture.Start(); var member=await f.Add("43",CoreCompanyRoles.Member);
        Assert.Equal(CompanyTeamStatus.Denied,(await f.Run(member,f.Command(f.Owner,CompanyTeamOperation.Revoke))).Status);
        var command=f.Command(member,CompanyTeamOperation.ChangeRole,CoreCompanyRoles.Administrator);
        Assert.Equal(CompanyTeamStatus.Changed,(await f.Run(f.Owner,command)).Status);
        await using(var db=f.Context()){
            (await db.ExternalIdentityLinks.SingleAsync(x=>x.Id==f.Owner.Link)).IsActive=false;await db.SaveChangesAsync();
        }
        Assert.Equal(CompanyTeamStatus.Denied,(await f.Run(f.Owner,command)).Status);
        await f.AssertEffects(1,1);
    }

    [Fact]
    public async Task No_change_still_reserves_the_command_so_a_later_retry_cannot_undo_a_newer_change()
    {
        await using var f=await Fixture.Start(); var target=await f.Add("43",CoreCompanyRoles.Member);
        var noop=f.Command(target,CompanyTeamOperation.ChangeRole,CoreCompanyRoles.Member);
        Assert.Equal(CompanyTeamStatus.Unchanged,(await f.Run(f.Owner,noop)).Status);
        Assert.Equal(CompanyTeamStatus.Changed,(await f.Run(f.Owner,noop with {CommandKey=Guid.NewGuid(),Role=CoreCompanyRoles.Administrator})).Status);
        Assert.Equal(CompanyTeamStatus.Existing,(await f.Run(f.Owner,noop)).Status);
        await using var db=f.Context(); Assert.Equal(CoreCompanyRoles.Administrator,(await db.CompanyMemberships.SingleAsync(x=>x.Id==target.Member)).Role);
        await f.AssertEffects(1,2);
    }

    [Theory]
    [InlineData("expired")][InlineData("foreign-license")][InlineData("foreign-subject")]
    [InlineData("foreign-link")][InlineData("foreign-company")][InlineData("foreign-member")]
    public async Task Foreign_or_expired_proof_has_no_side_effect(string fault)
    {
        await using var f=await Fixture.Start(); var target=await f.Add("43",CoreCompanyRoles.Member);
        var actor=f.Owner;var command=f.Command(target,CompanyTeamOperation.Revoke);var license=f.License();
        if(fault=="expired") f.Clock.Advance();
        if(fault=="foreign-license") license=f.License("01arz3ndektsv4rrffq69g5faw");
        if(fault=="foreign-subject") actor=actor with {Subject="99"};
        if(fault=="foreign-link") actor=actor with {Link=Guid.NewGuid()};
        if(fault=="foreign-company") command=command with {CompanyId=Guid.NewGuid()};
        if(fault=="foreign-member") command=command with {MembershipId=Guid.NewGuid()};
        Assert.Equal(CompanyTeamStatus.Denied,(await f.Service.ExecuteAsync(actor.Proof,command,license)).Status);
        await f.AssertEffects(0,0);
    }

    [Theory]
    [InlineData("key")][InlineData("revision")][InlineData("role")][InlineData("operation")][InlineData("revoke-role")]
    public async Task Malformed_commands_are_rejected_before_writes(string fault)
    {
        await using var f=await Fixture.Start(); var target=await f.Add("43",CoreCompanyRoles.Member);
        var command=f.Command(target,CompanyTeamOperation.ChangeRole,CoreCompanyRoles.Administrator);
        command=fault switch {"key"=>command with {CommandKey=Guid.Empty},"revision"=>command with {ExpectedRevision=0},
            "role"=>command with {Role="erp_admin"},"operation"=>command with {Operation=(CompanyTeamOperation)99},
            _=>command with {Operation=CompanyTeamOperation.Revoke,Role=CoreCompanyRoles.Member}};
        Assert.Equal(CompanyTeamStatus.Invalid,(await f.Run(f.Owner,command)).Status);await f.AssertEffects(0,0);
    }

    [Theory]
    [InlineData("expire")][InlineData("fail")][InlineData("cancel")]
    public async Task Post_save_failure_expiry_and_cancellation_roll_back_membership_receipt_and_events(string mode)
    {
        await using var f=await Fixture.Start();var target=await f.Add("43",CoreCompanyRoles.Member);
        f.Probe.Mode=mode;using var stop=new CancellationTokenSource();f.Probe.Stop=stop;
        var action=()=>f.Service.ExecuteAsync(f.Owner.Proof,f.Command(target,CompanyTeamOperation.Revoke),f.License(),stop.Token);
        if(mode=="cancel") await Assert.ThrowsAnyAsync<OperationCanceledException>(action);
        else Assert.Equal(mode=="expire"?CompanyTeamStatus.Denied:CompanyTeamStatus.Unavailable,(await action()).Status);
        await f.AssertEffects(0,0);await using var db=f.Context();
        var member=await db.CompanyMemberships.SingleAsync(x=>x.Id==target.Member);Assert.True(member.IsActive);Assert.Equal(1,member.Revision);
    }

    [Fact]
    public async Task Receipts_cannot_be_modified_or_retargeted_to_another_company_using_the_same_actor_key()
    {
        await using var f=await Fixture.Start();var command=f.Command(f.Owner,CompanyTeamOperation.ChangeRole,CoreCompanyRoles.Owner);
        Assert.Equal(CompanyTeamStatus.Unchanged,(await f.Run(f.Owner,command)).Status);
        await using(var db=f.Context()){
            (await db.CompanyTeamCommandReceipts.SingleAsync()).RequestHash=new string('a',64);
            await Assert.ThrowsAsync<DomainException>(()=>db.SaveChangesAsync());
        }
        var second=await f.Create("second_team_company_request_00000000001");
        await using var inspect=f.Context("inspect");
        var binding=await inspect.WorkspaceBindings.IgnoreQueryFilters().SingleAsync(x=>x.LegalEntityId==second.CompanyId);
        var link=await inspect.ExternalIdentityLinks.IgnoreQueryFilters().SingleAsync(x=>x.TenantId==binding.TenantId);
        var membership=await inspect.CompanyMemberships.IgnoreQueryFilters().SingleAsync(x=>x.TenantId==binding.TenantId);
        var result=await f.Service.ExecuteAsync(new(ProductCommercialLicense.Issuer,"42",link.Id),
            command with {CompanyId=binding.LegalEntityId,MembershipId=membership.Id},f.License());
        Assert.Equal(CompanyTeamStatus.Conflict,result.Status);
    }

    [NativeTeamFact]
    public async Task Native_concurrent_self_demotions_leave_one_owner_and_execute_the_real_governance_query()
    {
        await using var f=await Fixture.Start();var other=await f.Add("43",CoreCompanyRoles.Owner);
        var outcomes=await Task.WhenAll(f.Run(f.Owner,f.Command(f.Owner,CompanyTeamOperation.ChangeRole,CoreCompanyRoles.Member)),
            f.Run(other,f.Command(other,CompanyTeamOperation.ChangeRole,CoreCompanyRoles.Member)));
        Assert.Single(outcomes,x=>x.Status==CompanyTeamStatus.Changed);Assert.Single(outcomes,x=>x.Status==CompanyTeamStatus.LastOwner);
        await using var db=f.Context();Assert.Equal(1,await db.CompanyMemberships.CountAsync(x=>x.Role==CoreCompanyRoles.Owner&&x.IsActive));
        await f.AssertEffects(1,1);
    }

    [NativeTeamFact]
    public async Task Native_concurrent_retries_commit_one_command_receipt_audit_and_event()
    {
        await using var f=await Fixture.Start();var target=await f.Add("43",CoreCompanyRoles.Member);
        var command=f.Command(target,CompanyTeamOperation.ChangeRole,CoreCompanyRoles.Administrator);
        var results=await Task.WhenAll(Enumerable.Range(0,10).Select(_=>f.Run(f.Owner,command)));
        Assert.Single(results,x=>x.Status==CompanyTeamStatus.Changed);Assert.Equal(9,results.Count(x=>x.Status==CompanyTeamStatus.Existing));
        await f.AssertEffects(1,1);
    }

    [NativeTeamFact]
    public async Task Native_receipt_scope_immutability_and_populated_rollback_are_preserved()
    {
        await using var f=await Fixture.Start();var target=await f.Add("43",CoreCompanyRoles.Member);
        Assert.Equal(CompanyTeamStatus.Changed,(await f.Run(f.Owner,f.Command(target,CompanyTeamOperation.ChangeRole,CoreCompanyRoles.Administrator))).Status);
        await using var db=f.Context();
        foreach(var sql in new[]{"UPDATE hrm.core_company_team_command_receipts SET result_role='owner'","DELETE FROM hrm.core_company_team_command_receipts"}){
            var denied=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlRawAsync(sql));
            Assert.Equal(PostgresErrorCodes.CheckViolation,denied.SqlState);
        }
        var migrations=(await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var own=Assert.Single(migrations,x=>x.EndsWith("_CoreCompanyTeamCommands",StringComparison.Ordinal));
        var failure=await Assert.ThrowsAsync<PostgresException>(()=>db.GetService<IMigrator>().MigrateAsync(migrations[Array.IndexOf(migrations,own)-1]));
        Assert.Equal(PostgresErrorCodes.CheckViolation,failure.SqlState);
        Assert.Contains(own,await db.Database.GetAppliedMigrationsAsync());
        var receipt=await db.CompanyTeamCommandReceipts.SingleAsync();
        await using(var bad=f.Context()){
            bad.CompanyTeamCommandReceipts.Add(new(){LegalEntityId=f.Company,ActorLocalUserId=f.Owner.User,
                ActorIssuer=receipt.ActorIssuer,ActorSubject=receipt.ActorSubject,CommandKey=Guid.NewGuid(),
                MembershipId=Guid.NewGuid(),RequestHash=receipt.RequestHash,Operation=receipt.Operation,
                ResultRole=receipt.ResultRole,ResultActive=receipt.ResultActive,ResultRevision=receipt.ResultRevision});
            var denied=await Assert.ThrowsAsync<DbUpdateException>(()=>bad.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation,Assert.IsType<PostgresException>(denied.InnerException).SqlState);
        }
        await f.AssertEffects(1,1);
    }

    [NativeTeamFact]
    public async Task Native_empty_command_history_can_roll_back_without_removing_owner_membership()
    {
        await using var f=await Fixture.Start();await using var db=f.Context();
        var migrations=(await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var own=Assert.Single(migrations,x=>x.EndsWith("_CoreCompanyTeamCommands",StringComparison.Ordinal));
        await db.GetService<IMigrator>().MigrateAsync(migrations[Array.IndexOf(migrations,own)-1]);
        Assert.Single(await db.WorkspaceBindings.ToArrayAsync());
        await db.Database.MigrateAsync();Assert.False(db.Database.HasPendingModelChanges());
        var owner=await db.CompanyMemberships.SingleAsync();Assert.Equal(1,owner.Revision);Assert.Equal(CoreCompanyRoles.Owner,owner.Role);
        Assert.Equal(CompanyTeamStatus.Unchanged,(await f.Run(f.Owner,f.Command(f.Owner,CompanyTeamOperation.ChangeRole,CoreCompanyRoles.Owner))).Status);
    }

    public sealed class NativeTeamFactAttribute:FactAttribute
    {public NativeTeamFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUUBU_SESSION_TEST_POSTGRES")))Skip="Requires isolated native team-command PostgreSQL fixture.";}}
    internal sealed record Person(string Subject,Guid Link,Guid User,Guid Member)
    {internal CompanyTeamActor Proof=>new(ProductCommercialLicense.Issuer,Subject,Link);}
    internal sealed class Probe(CoreWorkspaceProvisionerTests.Clock clock):SaveChangesInterceptor
    {
        internal string Mode="";internal CancellationTokenSource? Stop;internal string? Failure;
        public override Task SaveChangesFailedAsync(DbContextErrorEventData data,CancellationToken cancellation=default)
        {Failure=data.Exception.GetBaseException().Message;return Task.CompletedTask;}
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data,int result,CancellationToken cancellation=default)
        {if(Mode=="expire")clock.Advance();if(Mode=="fail")throw new DbUpdateException("Synthetic post-save failure");
            if(Mode=="cancel"){Stop!.Cancel();cancellation.ThrowIfCancellationRequested();}return ValueTask.FromResult(result);}
    }
    internal sealed class Fixture(DbContextOptions<HrmDbContext> options,SqliteConnection? sqlite,CoreWorkspaceProvisionerTests.Clock clock,Probe probe):IAsyncDisposable
    {
        private const string Enrollment="01arz3ndektsv4rrffq69g5fav";
        internal CoreWorkspaceProvisionerTests.Clock Clock=>clock;internal Probe Probe=>probe;
        internal Guid Company;internal string Tenant=null!;internal Person Owner=null!;
        internal HrmDbContext Context(string? tenant=null)=>new(options,new Scope(tenant??Tenant));
        internal CoreCompanyTeamCommands Service=>new(x=>Context(x),clock);
        internal CommercialLicenseLease License(string enrollment=Enrollment)=>new(enrollment,[],2,clock.GetUtcNow().AddMinutes(1),clock,clock.GetTimestamp(),TimeSpan.FromMinutes(1));
        internal Task<CompanyTeamResult> Run(Person actor,CompanyTeamCommand command)=>Service.ExecuteAsync(actor.Proof,command,License());
        internal CompanyTeamCommand Command(Person target,CompanyTeamOperation operation,string? role=null)=>new(Company,target.Member,Guid.NewGuid(),1,operation,role);
        internal Task<WorkspaceCreateResult> Create(string key)=>new CoreWorkspaceProvisioner(x=>Context(x),clock).CreateAsync(
            new("42","391bdeaa-87b3-44b3-8e64-2110dcc1a564","01arz3ndektsv4rrffq69g5faw",Enrollment,2,clock.GetUtcNow().AddMinutes(1),clock,clock.GetTimestamp(),TimeSpan.FromMinutes(1)),License(),key,"Company");
        internal async Task<Person> Add(string subject,string role)
        {
            await using var db=Context();var user=LocalUser.Federated("Explicitly accepted team fixture");user.TenantId=Tenant;
            var link=new ExternalIdentityLink{TenantId=Tenant,Issuer=ProductCommercialLicense.Issuer,Subject=subject,LocalUserId=user.Id};
            var member=new CoreCompanyMembership{TenantId=Tenant,LegalEntityId=Company,LocalUserId=user.Id,Role=role};
            db.LocalUsers.Add(user);db.ExternalIdentityLinks.Add(link);db.CompanyMemberships.Add(member);await db.SaveChangesAsync();
            return new(subject,link.Id,user.Id,member.Id);
        }
        internal async Task AssertEffects(int changes,int receipts)
        {
            await using var db=Context();Assert.Equal(changes,await db.AuditEntries.CountAsync(x=>x.EntityType=="CoreCompanyMembership"));
            Assert.Equal(changes,await db.OutboxMessages.CountAsync(x=>x.EventType=="core.company.member.access.changed.v1"));
            Assert.Equal(receipts,await db.CompanyTeamCommandReceipts.CountAsync());Assert.Empty(await db.LocalSessions.ToArrayAsync());
            Assert.Empty(await db.LocalCredentialLinks.ToArrayAsync());Assert.Empty(await db.TenantRoleAssignments.ToArrayAsync());
            Assert.All(await db.LocalUsers.ToArrayAsync(),u=>{Assert.Null(u.PasswordHash);Assert.Equal("",u.RolesCsv);});
        }
        internal static async Task<Fixture> Start()
        {
            var clock=new CoreWorkspaceProvisionerTests.Clock();var probe=new Probe(clock);
            var builder=new DbContextOptionsBuilder<HrmDbContext>().AddInterceptors(probe);SqliteConnection? sqlite=null;
            var native=Environment.GetEnvironmentVariable("KUUBU_SESSION_TEST_POSTGRES");
            if(string.IsNullOrWhiteSpace(native)){sqlite=new("Data Source=:memory:");await sqlite.OpenAsync();builder.UseSqlite(sqlite);}
            else{
                var connection=new NpgsqlConnectionStringBuilder(native);Assert.Equal("kuubu_session_test_control",connection.Database);
                await using var control=new NpgsqlConnection(connection.ConnectionString);await control.OpenAsync();
                connection.Database="company_team_"+Guid.NewGuid().ToString("N");
                await using var create=new NpgsqlCommand("CREATE DATABASE \""+connection.Database+"\"",control);await create.ExecuteNonQueryAsync();
                builder.UseNpgsql(connection.ConnectionString,pg=>pg.MigrationsHistoryTable("__hrm_migrations","hrm"));
            }
            var f=new Fixture(builder.Options,sqlite,clock,probe);await using var db=f.Context("schema");
            if(sqlite is null){await CoreSchemaStartup.EnsureAsync(db,"Isolated team-command fixture",mayApply:true);Assert.False(db.Database.HasPendingModelChanges());}
            else await db.Database.EnsureCreatedAsync();
            var created=await f.Create("first_team_company_request_000000000001");
            Assert.True(created.Status==WorkspaceCreateStatus.Created,"Synthetic fixture creation failed: "+probe.Failure);
            f.Company=created.CompanyId!.Value;var binding=await db.WorkspaceBindings.IgnoreQueryFilters().SingleAsync();f.Tenant=binding.TenantId;
            var link=await db.ExternalIdentityLinks.IgnoreQueryFilters().SingleAsync();var member=await db.CompanyMemberships.IgnoreQueryFilters().SingleAsync();
            f.Owner=new("42",link.Id,binding.OwnerLocalUserId,member.Id);return f;
        }
        public async ValueTask DisposeAsync()
        {
            if(sqlite is not null)await sqlite.DisposeAsync();
            else{
                // Each fixture owns a unique database/pool. Do not retain its
                // idle connectors while subsequent native cases create theirs.
                await using var db=Context("fixture-cleanup");
                using var pool=new NpgsqlConnection(db.Database.GetConnectionString());
                NpgsqlConnection.ClearPool(pool);
            }
        }
        private sealed class Scope(string tenant):ITenantAccessor{public string GetTenantId()=>tenant;}
    }
}
