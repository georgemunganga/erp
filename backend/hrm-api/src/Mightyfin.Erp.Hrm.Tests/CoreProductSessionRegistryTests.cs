using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Team = Mightyfin.Erp.Hrm.Tests.CoreCompanyTeamCommandsTests;

namespace Mightyfin.Erp.Hrm.Tests;
public sealed class CoreProductSessionRegistryTests
{
    private const string Client="391bdeaa-87b3-44b3-8e64-2110dcc1a564";
    private static OperationalSubjectObservation Person(Team.Fixture f,string subject="42",string client=Client)=>new(subject,client,f.Clock.GetUtcNow().AddSeconds(30),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromSeconds(30));
    private static CommercialLicenseLease License(Team.Fixture f,string[]? modules=null)=>new("01arz3ndektsv4rrffq69g5fav",modules??["hrm"],2,f.Clock.GetUtcNow().AddMinutes(1),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromMinutes(1));
    internal sealed record Target(string Tenant,Guid Company,Guid Link,Guid User,Guid Member,Guid Location,Guid Version);
    internal sealed class Fixture(Team.Fixture team,string keys):IAsyncDisposable
    {
        internal Team.Fixture Core=>team;
        internal Target Target=null!;
        internal string Credential=ProductInvitationIntentStore.Random(),Browser=ProductInvitationIntentStore.Random();
        internal IDataProtectionProvider Protection=>DataProtectionProvider.Create(new DirectoryInfo(keys),o=>o.SetApplicationName("Kuubu.SessionRegistry.Fixture.v1"));
        internal CoreProductSessionRegistry Registry(int limit=30)=>new(t=>team.Context(t),DataProtectionProvider.Create(new DirectoryInfo(keys),o=>o.SetApplicationName("Kuubu.SessionRegistry.Fixture.v1")),team.Clock,limit);
        internal CoreProductSessionIssue Command(Target? target=null)=>new(Selection(target??Target),Guid.NewGuid(),Credential,Browser,team.Clock.GetUtcNow().AddHours(4));
        internal CoreErpSessionSelection Selection(Target target)=>new(target.Company,target.Link,"hrm",target.Version,1,false,[target.Location],false);
        internal CoreErpScopeRequest Request(Target? target=null)=>new((target??Target).Company,"hrm","hrm-access",false,[(target??Target).Location]);
        internal async Task<Target> Grant(Guid company)
        {
            await using var lookup=team.Context("untrusted-fixture");var bound=await lookup.WorkspaceBindings.IgnoreQueryFilters().SingleAsync(x=>x.LegalEntityId==company);
            await using var db=team.Context(bound.TenantId);
            var link=await db.ExternalIdentityLinks.SingleAsync();var member=await db.CompanyMemberships.SingleAsync();var location=await db.WorkLocations.FirstAsync(x=>x.LegalEntityId==company);
            var result=await new CoreModuleAccessCommands(t=>team.Context(t),team.Clock).ExecuteAsync(Person(team),new(ProductCommercialLicense.Issuer,"42",link.Id),
                new(company,member.Id,1,"hrm",0,Guid.NewGuid(),false,["hr_ops"],["hrm-access"],"company",[],true,true),License(team));
            Assert.Equal(ModuleAccessStatus.Changed,result.Status);return new(bound.TenantId,company,link.Id,member.LocalUserId,member.Id,location.Id,result.VersionId!.Value);
        }
        internal static async Task<Fixture> Start()
        {
            var team=await Team.Fixture.Start();var keys=Path.Combine(Path.GetTempPath(),"kuubu-session-registry-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(keys);
            if(!OperatingSystem.IsWindows())File.SetUnixFileMode(keys,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
            var f=new Fixture(team,keys);f.Target=await f.Grant(team.Company);return f;
        }
        public ValueTask DisposeAsync()=>team.DisposeAsync();
    }
    private static async Task<CoreErpScopeResult> Resolve(Fixture f,CoreIssuedProductSession session,CoreErpScopeRequest? request=null,
        string? credential=null,string? browser=null,OperationalSubjectObservation? person=null,CommercialLicenseLease? license=null)
        =>await f.Registry().ResolveAsync(session.Token,credential??f.Credential,browser??f.Browser,person??Person(f.Core),license??License(f.Core),request??f.Request());
    private static async Task<CoreIssuedProductSession> Issue(Fixture f,CoreProductSessionIssue? command=null)
    {
        var result=await f.Registry().IssueAsync(Person(f.Core),License(f.Core),command??f.Command());Assert.Equal(ProductSessionStatus.Issued,result.Status);return Assert.IsType<CoreIssuedProductSession>(result.Session);
    }
    [Fact]
    public async Task Explicit_issue_persists_bound_scope_without_plaintext_secrets_or_legacy_roles()
    {
        await using var f=await Fixture.Start();var session=await Issue(f);var result=await Resolve(f,session);
        var scope=Assert.IsType<CoreErpRequestScope>(result.Scope);Assert.Equal(f.Target.Company,scope.CompanyId);Assert.Equal(f.Target.Tenant,scope.TenantId);Assert.Equal(f.Target.Location,Assert.Single(scope.LocationIds));
        Assert.Equal(ModuleAccessStatus.Denied,(await Resolve(f,session,f.Request() with{AllCompanyLocations=true,LocationIds=[]})).Status);
        await using var db=f.Core.Context();var row=Assert.Single(await db.ProductSessions.ToArrayAsync());Assert.NotEqual(session.Token,row.ProtectedToken);
        Assert.Equal(64,row.TokenHash.Length);Assert.DoesNotContain(session.Token,row.ProtectedToken);Assert.DoesNotContain(f.Credential,row.ProtectedToken);Assert.DoesNotContain(f.Browser,row.ProtectedToken);
        Assert.Single(await db.ProductSessionLocations.ToArrayAsync());Assert.Empty(await db.ProductSessionRevocations.ToArrayAsync());Assert.Empty(await db.LocalSessions.ToArrayAsync());Assert.Empty(await db.TenantRoleAssignments.ToArrayAsync());
        var audit=Assert.Single(await db.AuditEntries.Where(x=>x.EntityType=="CoreProductSession").ToArrayAsync());Assert.DoesNotContain(session.Token,audit.AfterJson!);
        Assert.Single(await db.OutboxMessages.Where(x=>x.EventType=="core.product.session.issued.v1").ToArrayAsync());
    }
    [Fact]
    public async Task Retry_across_independent_key_ring_readers_returns_exact_token_without_another_issue()
    {
        await using var f=await Fixture.Start();var command=f.Command();var first=await Issue(f,command);
        var retry=await f.Registry().IssueAsync(Person(f.Core),License(f.Core),command);Assert.Equal(ProductSessionStatus.Existing,retry.Status);Assert.Equal(first,retry.Session);
        Assert.DoesNotContain(first.Token,retry.ToString());await using var db=f.Core.Context();Assert.Single(await db.ProductSessions.ToArrayAsync());Assert.Single(await db.ProductSessionLocations.ToArrayAsync());
        Assert.Single(await db.AuditEntries.Where(x=>x.EntityType=="CoreProductSession").ToArrayAsync());
    }
    [Theory]
    [InlineData("scope")][InlineData("credential")][InlineData("browser")]
    public async Task Reusing_issue_key_for_different_intent_is_a_conflict(string change)
    {
        await using var f=await Fixture.Start();var command=f.Command();await Issue(f,command);
        command=change switch{"scope"=>command with{Selection=command.Selection with{AllCompanyLocations=true,LocationIds=[],ConfirmCompanyWide=true}},"credential"=>command with{CredentialHandle=ProductInvitationIntentStore.Random()},_=>command with{BrowserBinding=ProductInvitationIntentStore.Random()}};
        var result=await f.Registry().IssueAsync(Person(f.Core),License(f.Core),command);Assert.Equal(ProductSessionStatus.Conflict,result.Status);Assert.Null(result.Session);
    }
    [Fact]
    public async Task Logout_is_durable_idempotent_and_cannot_be_replayed_into_active_access()
    {
        await using var f=await Fixture.Start();var command=f.Command();var session=await Issue(f,command);
        Assert.Equal(ProductSessionStatus.Revoked,await f.Registry().RevokeAsync(session.Token,f.Credential,f.Browser,"42",Client));
        Assert.Equal(ProductSessionStatus.Existing,await f.Registry().RevokeAsync(session.Token,f.Credential,f.Browser,"42",Client));
        Assert.Equal(ModuleAccessStatus.Denied,(await Resolve(f,session)).Status);
        Assert.Equal(ProductSessionStatus.Denied,(await f.Registry().IssueAsync(Person(f.Core),License(f.Core),command)).Status);
        await using var db=f.Core.Context();Assert.Single(await db.ProductSessionRevocations.ToArrayAsync());Assert.Equal(2,await db.AuditEntries.CountAsync(x=>x.EntityType=="CoreProductSession"));
    }
    [Theory]
    [InlineData("credential")][InlineData("browser")][InlineData("person")][InlineData("client")][InlineData("token")]
    public async Task Cookie_material_does_not_work_with_another_browser_credential_or_identity(string wrong)
    {
        await using var f=await Fixture.Start();var session=await Issue(f);
        var result=await Resolve(f,wrong=="token"?session with{Token=ProductInvitationIntentStore.Random()}:session,
            credential:wrong=="credential"?ProductInvitationIntentStore.Random():null,browser:wrong=="browser"?ProductInvitationIntentStore.Random():null,
            person:wrong=="person"?Person(f.Core,"43"):wrong=="client"?Person(f.Core,client:Guid.NewGuid().ToString("D")):null);
        Assert.Equal(ModuleAccessStatus.Denied,result.Status);Assert.Null(result.Scope);
    }
    [Fact]
    public async Task Absolute_expiry_is_not_extended_by_fresh_person_or_license_observations()
    {
        await using var f=await Fixture.Start();var command=f.Command() with{CredentialExpiresAt=f.Core.Clock.GetUtcNow().AddSeconds(20)};var session=await Issue(f,command);
        Assert.Equal(command.CredentialExpiresAt,session.ExpiresAt);f.Core.Clock.Advance();Assert.Equal(ModuleAccessStatus.Denied,(await Resolve(f,session)).Status);
        Assert.Equal(ProductSessionStatus.Denied,(await f.Registry().IssueAsync(Person(f.Core),License(f.Core),command)).Status);
    }
    [Fact]
    public async Task Regrant_never_rebases_the_stored_binding_to_a_newer_grant()
    {
        await using var f=await Fixture.Start();var session=await Issue(f);var t=f.Target;
        var revoke=new ModuleAccessCommand(t.Company,t.Member,1,"hrm",1,Guid.NewGuid(),true,[],[],"none",[],false,true);
        var commands=new CoreModuleAccessCommands(x=>f.Core.Context(x),f.Core.Clock);var actor=new CompanyTeamActor(ProductCommercialLicense.Issuer,"42",t.Link);
        Assert.Equal(ModuleAccessStatus.Changed,(await commands.ExecuteAsync(Person(f.Core),actor,revoke,License(f.Core))).Status);
        Assert.Equal(ModuleAccessStatus.Denied,(await Resolve(f,session)).Status);
        Assert.Equal(ModuleAccessStatus.Changed,(await commands.ExecuteAsync(Person(f.Core),actor,revoke with{ExpectedRevision=2,CommandKey=Guid.NewGuid(),Revoke=false,RoleKeys=["hr_ops"],Actions=["hrm-access"],ScopeKind="company",AllCompanyLocationsConfirmed=true},License(f.Core))).Status);
        Assert.Equal(ModuleAccessStatus.Denied,(await Resolve(f,session)).Status);await using var db=f.Core.Context();Assert.Equal(t.Version,(await db.ProductSessions.SingleAsync()).GrantVersionId);
    }
    [Fact]
    public async Task Explicit_company_switch_revokes_previous_cookie_in_the_same_transaction_with_correct_tenant_evidence()
    {
        await using var f=await Fixture.Start();var first=await Issue(f);var company=await f.Core.Create("session_registry_second_company_request_00000001");Assert.Equal(WorkspaceCreateStatus.Created,company.Status);
        var other=await f.Grant(company.CompanyId!.Value);var second=await Issue(f,f.Command(other));
        Assert.Equal(ModuleAccessStatus.Denied,(await Resolve(f,first)).Status);Assert.Equal(ModuleAccessStatus.Authorized,(await Resolve(f,second,f.Request(other))).Status);
        await using var db=f.Core.Context();var revoked=Assert.Single(await db.ProductSessionRevocations.IgnoreQueryFilters().ToArrayAsync());Assert.Equal(f.Target.Tenant,revoked.TenantId);
        var evidence=await db.AuditEntries.IgnoreQueryFilters().Where(x=>x.EntityType=="CoreProductSession").ToArrayAsync();Assert.Equal(3,evidence.Length);
        Assert.Equal(f.Target.Tenant,Assert.Single(evidence,x=>x.Action=="core.product.session.replaced").TenantId);
        Assert.Equal(3,await db.OutboxMessages.IgnoreQueryFilters().CountAsync(x=>x.EventType.StartsWith("core.product.session.")));
    }
    [Fact]
    public async Task Recent_issue_budget_survives_revocation_and_retries_do_not_consume_another_slot()
    {
        await using var f=await Fixture.Start();var command=f.Command();var registry=f.Registry(2);
        Assert.Equal(ProductSessionStatus.Issued,(await registry.IssueAsync(Person(f.Core),License(f.Core),command)).Status);
        Assert.Equal(ProductSessionStatus.Existing,(await registry.IssueAsync(Person(f.Core),License(f.Core),command)).Status);
        var second=await registry.IssueAsync(Person(f.Core),License(f.Core),f.Command());Assert.Equal(ProductSessionStatus.Issued,second.Status);
        await registry.RevokeAsync(second.Session!.Token,f.Credential,f.Browser,"42",Client);
        Assert.Equal(ProductSessionStatus.Limited,(await f.Registry(2).IssueAsync(Person(f.Core),License(f.Core),f.Command())).Status);
    }
    [Theory]
    [InlineData("fail")][InlineData("expire")][InlineData("cancel")]
    public async Task Failure_or_lost_authority_rolls_back_session_locations_revocations_audit_and_outbox(string mode)
    {
        await using var f=await Fixture.Start();using var stop=new CancellationTokenSource();f.Core.Probe.Mode=mode;f.Core.Probe.Stop=stop;
        if(mode=="cancel")await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>f.Registry().IssueAsync(Person(f.Core),License(f.Core),f.Command(),stop.Token));
        else Assert.Equal(mode=="expire"?ProductSessionStatus.Denied:ProductSessionStatus.Unavailable,(await f.Registry().IssueAsync(Person(f.Core),License(f.Core),f.Command())).Status);
        await using var db=f.Core.Context();Assert.Empty(await db.ProductSessions.ToArrayAsync());Assert.Empty(await db.ProductSessionLocations.ToArrayAsync());Assert.Empty(await db.ProductSessionRevocations.ToArrayAsync());
        Assert.Empty(await db.AuditEntries.Where(x=>x.EntityType=="CoreProductSession").ToArrayAsync());Assert.Empty(await db.OutboxMessages.Where(x=>x.EventType.StartsWith("core.product.session.")).ToArrayAsync());
    }
    [Fact]
    public async Task Session_evidence_cannot_be_edited_or_deleted_through_the_context()
    {
        await using var f=await Fixture.Start();await Issue(f);
        await using var db=f.Core.Context();(await db.ProductSessions.SingleAsync()).ExpiresAt=f.Core.Clock.GetUtcNow().AddDays(2);
        await Assert.ThrowsAsync<Mightyfin.Erp.Hrm.Application.DomainException>(()=>db.SaveChangesAsync());
    }
    [Fact]
    public async Task Sub_microsecond_deadline_and_protected_metadata_round_trip_exactly()
    {
        await using var f=await Fixture.Start();var command=f.Command() with{CredentialExpiresAt=f.Core.Clock.GetUtcNow().AddHours(4).AddTicks(7)};
        var first=await Issue(f,command);var retry=await f.Registry().IssueAsync(Person(f.Core),License(f.Core),command);
        Assert.Equal(ProductSessionStatus.Existing,retry.Status);Assert.Equal(command.CredentialExpiresAt,retry.Session!.ExpiresAt);
        Assert.Equal(ModuleAccessStatus.Authorized,(await Resolve(f,first)).Status);
    }
    [Fact]
    public async Task Tokens_and_server_browser_credential_handles_are_excluded_from_default_JSON()
    {
        await using var f=await Fixture.Start();var command=f.Command();var session=await Issue(f,command);
        var json=System.Text.Json.JsonSerializer.Serialize(new{command,session});
        Assert.DoesNotContain(session.Token,json);Assert.DoesNotContain(f.Credential,json);Assert.DoesNotContain(f.Browser,json);
    }
    [Fact]
    public async Task Branch_rows_cannot_be_appended_to_a_previously_issued_scope_through_EF()
    {
        await using var f=await Fixture.Start();var session=await Issue(f);
        await using var db=f.Core.Context();db.ProductSessionLocations.Add(new(){LegalEntityId=f.Target.Company,SessionId=session.Id,LocationId=f.Target.Location});
        var error=await Assert.ThrowsAsync<Mightyfin.Erp.Hrm.Application.DomainException>(()=>db.SaveChangesAsync());
        Assert.Contains("original issue transaction",error.Message);
    }
    [Fact]
    public async Task Direct_SQL_branch_append_is_blocked_or_rejected_by_the_authenticated_scope_digest()
    {
        await using var f=await Fixture.Start();Guid extra;
        await using(var db=f.Core.Context()){var branch=new WorkLocation{Code="EXTRA",Name="Extra branch",LegalEntityId=f.Target.Company};extra=branch.Id;db.WorkLocations.Add(branch);await db.SaveChangesAsync();}
        var session=await Issue(f);await using var attack=f.Core.Context();
        var sql=attack.Database.IsNpgsql()
            ? "INSERT INTO hrm.core_product_session_locations (id,tenant_id,legal_entity_id,session_id,location_id,created_at,created_by,is_archived) VALUES ({0},{1},{2},{3},{4},{5},'fixture',FALSE)"
            : "INSERT INTO core_product_session_locations (id,tenant_id,legal_entity_id,session_id,location_id,created_at,created_by,is_archived) VALUES ({0},{1},{2},{3},{4},{5},'fixture',FALSE)";
        var args=new object[]{Guid.CreateVersion7(),f.Target.Tenant,f.Target.Company,session.Id,extra,f.Core.Clock.GetUtcNow()};
        if(attack.Database.IsNpgsql())await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(()=>attack.Database.ExecuteSqlRawAsync(sql,args));
        else {await attack.Database.ExecuteSqlRawAsync(sql,args);Assert.Equal(ModuleAccessStatus.Denied,(await Resolve(f,session)).Status);}
    }
    [Fact]
    public async Task Missing_key_ring_is_unavailable_without_a_success_fallback_or_permanent_revocation()
    {
        await using var f=await Fixture.Start();var session=await Issue(f);var wrong=new CoreProductSessionRegistry(t=>f.Core.Context(t),new EphemeralDataProtectionProvider(),f.Core.Clock);
        var result=await wrong.ResolveAsync(session.Token,f.Credential,f.Browser,Person(f.Core),License(f.Core),f.Request());Assert.Equal(ModuleAccessStatus.Unavailable,result.Status);Assert.Null(result.Scope);
        Assert.Equal(ModuleAccessStatus.Authorized,(await Resolve(f,session)).Status);
    }
    [Fact]
    public async Task Session_deadline_is_capped_at_eight_hours_even_with_a_longer_parent_deadline()
    {
        await using var f=await Fixture.Start();var session=await Issue(f,f.Command() with{CredentialExpiresAt=f.Core.Clock.GetUtcNow().AddDays(2)});
        Assert.Equal(f.Core.Clock.GetUtcNow().AddHours(8),session.ExpiresAt);
    }
    [Theory]
    [InlineData("credential")][InlineData("browser")][InlineData("key")][InlineData("branches")][InlineData("client")][InlineData("expired")]
    public async Task Invalid_or_expired_issue_material_allocates_no_session(string invalid)
    {
        await using var f=await Fixture.Start();var command=f.Command();
        command=invalid switch{"credential"=>command with{CredentialHandle="bad"},"browser"=>command with{BrowserBinding="bad"},"key"=>command with{CommandKey=Guid.Empty},
            "branches"=>command with{Selection=command.Selection with{LocationIds=[]}},"expired"=>command with{CredentialExpiresAt=f.Core.Clock.GetUtcNow()},_=>command};
        var result=await f.Registry().IssueAsync(invalid=="client"?Person(f.Core,client:"bad"):Person(f.Core),License(f.Core),command);
        Assert.Equal(invalid=="expired"?ProductSessionStatus.Denied:ProductSessionStatus.Invalid,result.Status);Assert.Null(result.Session);
        await using var db=f.Core.Context();Assert.Empty(await db.ProductSessions.ToArrayAsync());
    }
    [Fact]
    public async Task Failed_cross_company_switch_rolls_back_old_tenant_revocation_and_preserves_old_access()
    {
        await using var f=await Fixture.Start();var first=await Issue(f);var other=await f.Core.Create("failed_session_switch_company_request_000000001");var target=await f.Grant(other.CompanyId!.Value);
        f.Core.Probe.Mode="fail";Assert.Equal(ProductSessionStatus.Unavailable,(await f.Registry().IssueAsync(Person(f.Core),License(f.Core),f.Command(target))).Status);f.Core.Probe.Mode="";
        Assert.Equal(ModuleAccessStatus.Authorized,(await Resolve(f,first)).Status);await using var db=f.Core.Context();
        Assert.Single(await db.ProductSessions.IgnoreQueryFilters().ToArrayAsync());Assert.Empty(await db.ProductSessionRevocations.IgnoreQueryFilters().ToArrayAsync());
        Assert.Single(await db.AuditEntries.IgnoreQueryFilters().Where(x=>x.EntityType=="CoreProductSession").ToArrayAsync());
    }
    [Team.NativeTeamFact]
    public async Task Native_session_and_location_evidence_rejects_direct_update_and_delete()
    {
        await using var f=await Fixture.Start();await Issue(f);await using var db=f.Core.Context();
        foreach(var sql in new[]{"UPDATE hrm.core_product_sessions SET expires_at = expires_at+1","DELETE FROM hrm.core_product_session_locations"}) {
            var error=await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlRawAsync(sql));Assert.Equal("23514",error.SqlState);
        }
    }
    [Team.NativeTeamFact]
    public async Task Native_composite_keys_reject_a_foreign_company_grant_on_a_new_session()
    {
        await using var f=await Fixture.Start();await Issue(f);var company=await f.Core.Create("foreign_session_grant_company_request_0000000001");var other=await f.Grant(company.CompanyId!.Value);
        await using var db=f.Core.Context();var original=await db.ProductSessions.AsNoTracking().SingleAsync();
        db.ProductSessions.Add(new(){PolicyVersion=1,Issuer=original.Issuer,Subject=original.Subject,ClientId=original.ClientId,PrincipalId=original.PrincipalId,LinkId=original.LinkId,
            LegalEntityId=original.LegalEntityId,MembershipId=original.MembershipId,ModuleCode=original.ModuleCode,GrantVersionId=other.Version,GrantRevision=1,ScopeKind="company",
            TokenHash=ProductInvitationIntentStore.Hash(ProductInvitationIntentStore.Random()),ProtectedToken=original.ProtectedToken,ScopeHash=original.ScopeHash,
            CredentialHash=original.CredentialHash,BrowserHash=original.BrowserHash,CommandKey=Guid.NewGuid(),RequestHash=original.RequestHash,IssuedAt=original.IssuedAt,ExpiresAt=original.ExpiresAt});
        var error=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());Assert.Equal("23503",Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);
    }
    [Team.NativeTeamFact]
    public async Task Native_empty_migration_rollback_reapplies_but_populated_rollback_preserves_session_evidence()
    {
        await using var f=await Fixture.Start();await using(var db=f.Core.Context()) {
            var applied=(await db.Database.GetAppliedMigrationsAsync()).ToArray();var current=Assert.Single(applied,x=>x.EndsWith("_CoreProductSessions",StringComparison.Ordinal));
            var previous=applied[Array.IndexOf(applied,current)-1];await db.GetService<IMigrator>().MigrateAsync(previous);
            await db.Database.MigrateAsync();Assert.False(db.Database.HasPendingModelChanges());
        }
        await Issue(f);await using(var db=f.Core.Context()) {
            var applied=(await db.Database.GetAppliedMigrationsAsync()).ToArray();var current=Assert.Single(applied,x=>x.EndsWith("_CoreProductSessions",StringComparison.Ordinal));
            var error=await Assert.ThrowsAnyAsync<Exception>(()=>db.GetService<IMigrator>().MigrateAsync(applied[Array.IndexOf(applied,current)-1]));
            Assert.Equal("55000",Assert.IsType<Npgsql.PostgresException>(error.GetBaseException()).SqlState);Assert.Single(await db.ProductSessions.ToArrayAsync());Assert.Single(await db.ProductSessionLocations.ToArrayAsync());
        }
    }
    [Team.NativeTeamFact]
    public async Task Native_concurrent_same_command_issues_one_session_and_recovers_one_token()
    {
        await using var f=await Fixture.Start();var command=f.Command();var outcomes=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>f.Registry().IssueAsync(Person(f.Core),License(f.Core),command)));
        Assert.Single(outcomes,x=>x.Status==ProductSessionStatus.Issued);Assert.Equal(3,outcomes.Count(x=>x.Status==ProductSessionStatus.Existing));Assert.Single(outcomes.Select(x=>x.Session!.Token).Distinct());
        await using var db=f.Core.Context();Assert.Single(await db.ProductSessions.ToArrayAsync());
    }
}
