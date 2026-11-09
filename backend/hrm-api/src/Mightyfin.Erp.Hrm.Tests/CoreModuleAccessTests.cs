using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Npgsql;
using Team= Mightyfin.Erp.Hrm.Tests.CoreCompanyTeamCommandsTests;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreModuleAccessTests
{
    private const string Client="391bdeaa-87b3-44b3-8e64-2110dcc1a564",Enrollment="01arz3ndektsv4rrffq69g5fav";
    private static OperationalSubjectObservation Person(Team.Fixture f,Team.Person? actor=null)=>new((actor??f.Owner).Subject,Client,f.Clock.GetUtcNow().AddSeconds(30),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromSeconds(30));
    private static CommercialLicenseLease License(Team.Fixture f,string module="hrm")=>new(Enrollment,[module],2,f.Clock.GetUtcNow().AddSeconds(60),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromSeconds(60));
    private static ModuleAccessCommand Grant(Team.Fixture f,Team.Person target)=>new(f.Company,target.Member,1,"hrm",0,Guid.NewGuid(),false,["hr_ops"],["hrm-access"],"company",[],true,true);
    private static Task<ModuleAccessResult> Run(Team.Fixture f,ModuleAccessCommand command,Team.Person? actor=null,CancellationToken ct=default)
        =>new CoreModuleAccessCommands(tenant=>f.Context(tenant),f.Clock).ExecuteAsync(Person(f,actor),(actor??f.Owner).Proof,command,License(f,command.ModuleCode),ct);
    private static async Task<ModuleAccessReadResult> Read(Team.Fixture f,Team.Person? actor=null,string module="hrm")
    {
        await using var db=f.Context("untrusted-module-read");var person=actor??f.Owner;
        return await new CoreModuleAccessReader(db,f.Clock).ReadAsync(Person(f,person),person.Link,f.Company,module,License(f,module));
    }
    [Fact]
    public async Task Governance_owner_has_no_module_access_until_an_explicit_confirmed_snapshot_and_never_gets_an_ERP_session()
    {
        await using var f=await Team.Fixture.Start();Assert.Equal(ModuleAccessStatus.Denied,(await Read(f)).Status);
        var result=await Run(f,Grant(f,f.Owner));Assert.Equal(ModuleAccessStatus.Changed,result.Status);Assert.Equal(1,result.Revision);
        var read=await Read(f);Assert.Equal(ModuleAccessStatus.Authorized,read.Status);var decision=Assert.IsType<CompanyModuleAccessDecision>(read.Decision);
        Assert.True(decision.AllCompanyLocations);Assert.Empty(decision.LocationIds);Assert.True(decision.AllowsAction("hrm-access"));Assert.False(decision.AllowsAction("hrm-admin"));
        Assert.Equal(f.Company,decision.CompanyId);Assert.Equal(f.Tenant,decision.TenantId);Assert.Equal(f.Owner.User,decision.PrincipalId);
        await using var db=f.Context();Assert.Empty(await db.LocalSessions.ToArrayAsync());Assert.Empty(await db.LocalCredentialLinks.ToArrayAsync());Assert.Empty(await db.TenantRoleAssignments.ToArrayAsync());
        Assert.All(await db.LocalUsers.ToArrayAsync(),u=>{Assert.Null(u.PasswordHash);Assert.Equal("",u.RolesCsv);});
        Assert.Single(await db.ModuleAccessVersions.ToArrayAsync());Assert.Single(await db.ModuleAccessReceipts.ToArrayAsync());
        Assert.Single(await db.AuditEntries.Where(x=>x.EntityType=="CoreModuleAccessVersion").ToArrayAsync());
        var message=Assert.Single(await db.OutboxMessages.Where(x=>x.EventType=="core.company.module.access.changed.v1").ToArrayAsync());
        Assert.DoesNotContain("hr_ops",message.PayloadJson);Assert.DoesNotContain("hrm-access",message.PayloadJson);
        f.Clock.Advance();Assert.False(decision.IsCurrent);Assert.False(decision.AllowsAction("hrm-access"));
    }
    [Theory]
    [InlineData("confirmation")][InlineData("company-confirmation")][InlineData("empty-locations")][InlineData("duplicate-location")]
    [InlineData("unknown-module")][InlineData("erp-admin")][InlineData("unknown-action")][InlineData("duplicate-action")][InlineData("hrm-admin-without-role")][InlineData("entry-missing")]
    public async Task Invalid_or_implicit_permissions_create_no_ledger_or_receipt(string fault)
    {
        await using var f=await Team.Fixture.Start();var command=Grant(f,f.Owner);var location=Guid.NewGuid();
        command=fault switch {
            "confirmation"=>command with{Confirm=false},"company-confirmation"=>command with{AllCompanyLocationsConfirmed=false},
            "empty-locations"=>command with{ScopeKind="locations",AllCompanyLocationsConfirmed=false},
            "duplicate-location"=>command with{ScopeKind="locations",AllCompanyLocationsConfirmed=false,LocationIds=[location,location]},
            "unknown-module"=>command with{ModuleCode="unknown"},"erp-admin"=>command with{RoleKeys=["erp_admin"]},
            "unknown-action"=>command with{Actions=["hrm-access","anything"]},"duplicate-action"=>command with{Actions=["hrm-access","hrm-access"]},
            "hrm-admin-without-role"=>command with{Actions=["hrm-access","hrm-admin"]},_=>command with{Actions=["hrm-admin"],RoleKeys=["hr_admin"]}
        };
        Assert.Equal(ModuleAccessStatus.Invalid,(await Run(f,command)).Status);
        await using var db=f.Context();Assert.Empty(await db.ModuleAccessVersions.ToArrayAsync());Assert.Empty(await db.ModuleAccessReceipts.ToArrayAsync());
    }
    [Theory]
    [InlineData(CoreCompanyRoles.Member)][InlineData(CoreCompanyRoles.Administrator)]
    public async Task Non_owner_cannot_grant_even_with_a_current_person_and_commercial_license(string role)
    {
        await using var f=await Team.Fixture.Start();var member=await f.Add("43",role);
        Assert.Equal(ModuleAccessStatus.Denied,(await Run(f,Grant(f,member),member)).Status);
    }
    [Fact]
    public async Task Exact_replay_has_one_outcome_changed_payload_conflicts_and_unchanged_new_key_has_no_duplicate_event()
    {
        await using var f=await Team.Fixture.Start();var command=Grant(f,f.Owner);
        var first=await Run(f,command);Assert.Equal(ModuleAccessStatus.Changed,first.Status);
        var replay=await Run(f,command);Assert.Equal(ModuleAccessStatus.Existing,replay.Status);Assert.Equal(first.VersionId,replay.VersionId);
        Assert.Equal(ModuleAccessStatus.Conflict,(await Run(f,command with{RoleKeys=["manager"]})).Status);
        Assert.Equal(ModuleAccessStatus.Conflict,(await Run(f,command with{CommandKey=Guid.NewGuid()})).Status);
        Assert.Equal(ModuleAccessStatus.Unchanged,(await Run(f,command with{ExpectedRevision=1,CommandKey=Guid.NewGuid()})).Status);
        await using var db=f.Context();Assert.Single(await db.ModuleAccessVersions.ToArrayAsync());Assert.Equal(2,await db.ModuleAccessReceipts.CountAsync());
        Assert.Single(await db.AuditEntries.Where(x=>x.EntityType=="CoreModuleAccessVersion").ToArrayAsync());
    }
    [Fact]
    public async Task Latest_revocation_and_membership_revision_never_fall_back_to_an_old_grant()
    {
        await using var f=await Team.Fixture.Start();var command=Grant(f,f.Owner);await Run(f,command);
        var revoke=command with{Revoke=true,ExpectedRevision=1,CommandKey=Guid.NewGuid(),RoleKeys=[],Actions=[],ScopeKind="none",AllCompanyLocationsConfirmed=false};
        Assert.Equal(ModuleAccessStatus.Changed,(await Run(f,revoke)).Status);Assert.Equal(ModuleAccessStatus.Denied,(await Read(f)).Status);
        Assert.Equal(ModuleAccessStatus.Existing,(await Run(f,command)).Status);Assert.Equal(ModuleAccessStatus.Denied,(await Read(f)).Status);
        Assert.Equal(ModuleAccessStatus.Changed,(await Run(f,command with{ExpectedRevision=2,CommandKey=Guid.NewGuid()})).Status);
        await using(var db=f.Context()){var member=await db.CompanyMemberships.SingleAsync(x=>x.Id==f.Owner.Member);member.Revision++;await db.SaveChangesAsync();}
        Assert.Equal(ModuleAccessStatus.Denied,(await Read(f)).Status);
    }
    [Fact]
    public async Task Branch_scope_requires_owned_locations_and_archival_invalidates_the_old_snapshot_without_widening()
    {
        await using var f=await Team.Fixture.Start();Guid branch,foreignBranch;
        await using(var db=f.Context("other-branch-tenant")) {
            var other=new LegalEntity{Code="OTHER",RegisteredName="Other entity"};db.LegalEntities.Add(other);
            var foreign=new WorkLocation{Code="OTHER",Name="Other company branch",LegalEntityId=other.Id};db.WorkLocations.Add(foreign);foreignBranch=foreign.Id;await db.SaveChangesAsync();
        }
        await using(var db=f.Context()) {
            var owned=new WorkLocation{Code="OWN",Name="Owned branch",LegalEntityId=f.Company};db.WorkLocations.Add(owned);branch=owned.Id;await db.SaveChangesAsync();
        }
        var command=Grant(f,f.Owner) with{ScopeKind="locations",LocationIds=[foreignBranch],AllCompanyLocationsConfirmed=false};
        Assert.Equal(ModuleAccessStatus.Denied,(await Run(f,command)).Status);
        command=command with{LocationIds=[branch],CommandKey=Guid.NewGuid()};Assert.Equal(ModuleAccessStatus.Changed,(await Run(f,command)).Status);
        var read=await Read(f);Assert.Equal(ModuleAccessStatus.Authorized,read.Status);Assert.False(read.Decision!.AllCompanyLocations);Assert.Equal(branch,Assert.Single(read.Decision.LocationIds));
        await using(var db=f.Context()){(await db.WorkLocations.SingleAsync(x=>x.Id==branch)).IsArchived=true;await db.SaveChangesAsync();}
        Assert.Equal(ModuleAccessStatus.Denied,(await Read(f)).Status);
    }
    [Fact]
    public async Task Legacy_branch_company_moves_remain_supported_by_the_new_relational_keys()
    {
        await using var f=await Team.Fixture.Start();Guid branch,target;
        await using(var db=f.Context("legacy-branch-tenant")) {
            var first=new LegalEntity{Code="FIRST",RegisteredName="First legacy entity"};
            var second=new LegalEntity{Code="SECOND",RegisteredName="Second legacy entity"};db.LegalEntities.AddRange(first,second);target=second.Id;
            var location=new WorkLocation{Code="MOVE",Name="Existing branch",LegalEntityId=first.Id};db.WorkLocations.Add(location);branch=location.Id;await db.SaveChangesAsync();
        }
        await using(var db=f.Context("legacy-branch-tenant")){(await db.WorkLocations.SingleAsync(x=>x.Id==branch)).LegalEntityId=target;await db.SaveChangesAsync();}
        await using(var db=f.Context("legacy-branch-tenant"))Assert.Equal(target,(await db.WorkLocations.SingleAsync(x=>x.Id==branch)).LegalEntityId);
    }
    [Fact]
    public async Task Procurement_uses_explicit_registered_actions_without_bootstrap_or_legacy_global_admin()
    {
        await using var f=await Team.Fixture.Start();var command=Grant(f,f.Owner) with{ModuleCode="procurement",RoleKeys=["procurement_manager"],Actions=["procurement-access","procurement-vendor-read"]};
        Assert.Equal(ModuleAccessStatus.Changed,(await Run(f,command)).Status);var read=await Read(f,module:"procurement");
        Assert.Equal(ModuleAccessStatus.Authorized,read.Status);Assert.True(read.Decision!.AllowsAction("procurement-vendor-read"));
        Assert.False(read.Decision.AllowsAction("procurement-vendor-approve"));Assert.False(read.Decision.AllowsAction("procurement-payment-record"));
        Assert.Equal(ModuleAccessStatus.Denied,(await Read(f,module:"hrm")).Status);
    }
    [Theory]
    [InlineData("fail")][InlineData("expire")][InlineData("cancel")]
    public async Task Save_failure_expired_authority_or_cancellation_roll_back_version_receipt_audit_and_event(string mode)
    {
        await using var f=await Team.Fixture.Start();using var stop=new CancellationTokenSource();f.Probe.Mode=mode;f.Probe.Stop=stop;
        if(mode=="cancel")await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>Run(f,Grant(f,f.Owner),ct:stop.Token));
        else Assert.Equal(mode=="expire"?ModuleAccessStatus.Denied:ModuleAccessStatus.Unavailable,(await Run(f,Grant(f,f.Owner))).Status);
        f.Probe.Mode="";await using var db=f.Context();Assert.Empty(await db.ModuleAccessVersions.ToArrayAsync());Assert.Empty(await db.ModuleAccessReceipts.ToArrayAsync());
        Assert.Empty(await db.AuditEntries.Where(x=>x.EntityType=="CoreModuleAccessVersion").ToArrayAsync());Assert.Empty(await db.OutboxMessages.Where(x=>x.EventType=="core.company.module.access.changed.v1").ToArrayAsync());
    }
    [Fact]
    public async Task Ledger_locations_and_receipts_are_append_only_through_the_context()
    {
        await using var f=await Team.Fixture.Start();await Run(f,Grant(f,f.Owner));
        foreach(var mode in new[]{"version-edit","version-delete","receipt-delete"}) {
            await using var db=f.Context();
            if(mode=="version-edit")(await db.ModuleAccessVersions.SingleAsync()).RoleKeysCsv="hr_admin";
            else if(mode=="version-delete")db.ModuleAccessVersions.Remove(await db.ModuleAccessVersions.SingleAsync());
            else db.ModuleAccessReceipts.Remove(await db.ModuleAccessReceipts.SingleAsync());
            await Assert.ThrowsAsync<DomainException>(()=>db.SaveChangesAsync());
        }
    }
    [Team.NativeTeamFact]
    public async Task Native_concurrent_same_key_has_one_version_receipt_audit_and_event()
    {
        await using var f=await Team.Fixture.Start();var command=Grant(f,f.Owner);
        var outcomes=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>Run(f,command)));
        Assert.Single(outcomes,x=>x.Status==ModuleAccessStatus.Changed);Assert.Equal(3,outcomes.Count(x=>x.Status==ModuleAccessStatus.Existing));
        await using var db=f.Context();Assert.Single(await db.ModuleAccessVersions.ToArrayAsync());Assert.Single(await db.ModuleAccessReceipts.ToArrayAsync());
        Assert.Single(await db.AuditEntries.Where(x=>x.EntityType=="CoreModuleAccessVersion").ToArrayAsync());Assert.Single(await db.OutboxMessages.Where(x=>x.EventType=="core.company.module.access.changed.v1").ToArrayAsync());
    }
    [Fact]
    public async Task Relational_keys_reject_foreign_tenant_locations_and_retargeted_predecessor_or_receipt()
    {
        await using var f=await Team.Fixture.Start();var member=await f.Add("43",CoreCompanyRoles.Member);var issued=await Run(f,Grant(f,f.Owner));Guid foreignLocation;
        await using(var foreign=f.Context("foreign-module-tenant")) {
            var company=new LegalEntity{Code="FOREIGN",RegisteredName="Foreign tenant"};foreign.LegalEntities.Add(company);
            var branch=new WorkLocation{Code="FOREIGN",Name="Foreign location",LegalEntityId=company.Id};foreign.WorkLocations.Add(branch);foreignLocation=branch.Id;await foreign.SaveChangesAsync();
        }
        await using(var db=f.Context()) {
            db.ModuleAccessLocations.Add(new(){LegalEntityId=f.Company,GrantVersionId=issued.VersionId!.Value,LocationId=foreignLocation});
            await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
        }
        await using(var db=f.Context()) {
            db.ModuleAccessVersions.Add(new(){LegalEntityId=f.Company,MembershipId=member.Member,MembershipRevision=1,ModuleCode="hrm",Revision=2,
                PreviousVersionId=issued.VersionId,PreviousRevision=1,ActingMembershipId=f.Owner.Member,ActingMembershipRevision=1,State="granted",ScopeKind="company",RoleKeysCsv="hr_ops",ActionCodesCsv="hrm-access"});
            await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
        }
        await using(var db=f.Context()) {
            db.ModuleAccessReceipts.Add(new(){LegalEntityId=f.Company,MembershipId=member.Member,ModuleCode="hrm",ActingMembershipId=f.Owner.Member,ActorIssuer=ProductCommercialLicense.Issuer,
                ActorSubject=f.Owner.Subject,CommandKey=Guid.NewGuid(),RequestHash=new string('a',64),ResultVersionId=issued.VersionId!.Value,ResultRevision=1});
            await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
        }
    }
    [Team.NativeTeamFact]
    public async Task Native_populated_rollback_refuses_to_destroy_grants_and_empty_schema_can_rollback_and_reapply()
    {
        await using(var f=await Team.Fixture.Start()) {
            await using var db=f.Context();var migrations=(await db.Database.GetAppliedMigrationsAsync()).ToArray();var current=Assert.Single(migrations,x=>x.EndsWith("_CoreModuleAccessGrants",StringComparison.Ordinal));
            var previous=migrations[Array.IndexOf(migrations,current)-1];await db.GetService<IMigrator>().MigrateAsync(previous);await db.Database.MigrateAsync();Assert.False(db.Database.HasPendingModelChanges());
        }
        await using(var f=await Team.Fixture.Start()) {
            await Run(f,Grant(f,f.Owner));await using var db=f.Context();var migrations=(await db.Database.GetAppliedMigrationsAsync()).ToArray();var current=Assert.Single(migrations,x=>x.EndsWith("_CoreModuleAccessGrants",StringComparison.Ordinal));
            var error=await Assert.ThrowsAnyAsync<Exception>(()=>db.GetService<IMigrator>().MigrateAsync(migrations[Array.IndexOf(migrations,current)-1]));
            Assert.IsType<PostgresException>(error.GetBaseException());Assert.Single(await db.ModuleAccessVersions.ToArrayAsync());Assert.Single(await db.ModuleAccessReceipts.ToArrayAsync());
        }
    }
}
