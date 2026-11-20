using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Team=Mightyfin.Erp.Hrm.Tests.CoreCompanyTeamCommandsTests;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreOwnerModuleAccessTests
{
    private const string Enrollment="01arz3ndektsv4rrffq69g5fav";
    private static OperationalSubjectObservation Person(Team.Fixture f,Team.Person? p=null)=>new((p??f.Owner).Subject,
        "391bdeaa-87b3-44b3-8e64-2110dcc1a564",f.Clock.GetUtcNow().AddSeconds(30),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromSeconds(30));
    private static CommercialLicenseLease License(Team.Fixture f,string[]? modules=null)=>new(Enrollment,modules??["hrm","procurement"],2,
        f.Clock.GetUtcNow().AddSeconds(60),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromSeconds(60));
    private static Task<ModuleAccessStatus> Ensure(Team.Fixture f,string module="hrm",Team.Person? p=null,CommercialLicenseLease? license=null)
        =>new CoreOwnerModuleAccess(t=>f.Context(t),f.Clock).EnsureAsync(Person(f,p),(p??f.Owner).Proof,f.Company,module,license??License(f));

    [Theory]
    [InlineData("hrm")][InlineData("procurement")]
    public async Task First_owner_open_persists_company_wide_enabled_module_admin_once_without_creating_employee_or_session(string module)
    {
        await using var f=await Team.Fixture.Start();Assert.Equal(ModuleAccessStatus.Changed,await Ensure(f,module));
        Assert.Equal(ModuleAccessStatus.Unchanged,await Ensure(f,module));
        await using var db=f.Context();var v=Assert.Single(await db.ModuleAccessVersions.ToArrayAsync());
        Assert.Equal("company",v.ScopeKind);Assert.DoesNotContain("employee",CoreModuleAccessCatalog.Parse(v.RoleKeysCsv));
        Assert.Equal(CoreModuleAccessCatalog.Actions(module).Order(),CoreModuleAccessCatalog.Parse(v.ActionCodesCsv));
        Assert.Empty(await db.ModuleAccessLocations.ToArrayAsync());Assert.Single(await db.ModuleAccessReceipts.ToArrayAsync());
        Assert.Single(await db.AuditEntries.Where(x=>x.EntityType=="CoreModuleAccessVersion").ToArrayAsync());
        Assert.Empty(await db.Workers.ToArrayAsync());Assert.Empty(await db.ProductSessions.ToArrayAsync());
        var read=await new CoreModuleAccessReader(db,f.Clock).ReadAsync(Person(f),f.Owner.Link,f.Company,module,License(f));
        Assert.Equal(ModuleAccessStatus.Authorized,read.Status);Assert.True(read.Decision!.AllCompanyLocations);
        if(module=="hrm")Assert.Contains("hr_admin",read.Decision.Roles);
    }
    [Fact]
    public async Task Owner_branch_restriction_is_superseded_in_history_and_explicit_employee_role_is_preserved()
    {
        await using var f=await Team.Fixture.Start();Guid branch;
        await using(var db=f.Context()){var l=new WorkLocation{Code="HQ",Name="Office",LegalEntityId=f.Company};db.WorkLocations.Add(l);await db.SaveChangesAsync();branch=l.Id;}
        var cmd=new ModuleAccessCommand(f.Company,f.Owner.Member,1,"hrm",0,Guid.NewGuid(),false,["employee","hr_admin"],["hrm-access","hrm-admin"],"locations",[branch],false,true);
        Assert.Equal(ModuleAccessStatus.Changed,(await new CoreModuleAccessCommands(t=>f.Context(t),f.Clock).ExecuteAsync(Person(f),f.Owner.Proof,cmd,License(f))).Status);
        Assert.Equal(ModuleAccessStatus.Changed,await Ensure(f));await using var verify=f.Context();
        var versions=await verify.ModuleAccessVersions.OrderBy(x=>x.Revision).ToArrayAsync();Assert.Equal(2,versions.Length);
        Assert.Equal("locations",versions[0].ScopeKind);Assert.Equal("company",versions[1].ScopeKind);
        Assert.Equal(versions[0].Id,versions[1].PreviousVersionId);Assert.Contains("employee",CoreModuleAccessCatalog.Parse(versions[1].RoleKeysCsv));
    }
    [Theory]
    [InlineData("administrator")][InlineData("member")]
    public async Task Delegated_members_are_not_promoted_or_given_module_access(string role)
    {
        await using var f=await Team.Fixture.Start();var p=await f.Add("43",role);
        Assert.Equal(ModuleAccessStatus.Unchanged,await Ensure(f,p:p));await using var db=f.Context();Assert.Empty(await db.ModuleAccessVersions.ToArrayAsync());
    }
    [Fact]
    public async Task Unlicensed_module_and_unrelated_verified_identity_cannot_create_owner_grants()
    {
        await using var f=await Team.Fixture.Start();Assert.Equal(ModuleAccessStatus.Denied,await Ensure(f,license:License(f,["procurement"])));
        var other=await f.Add("43",CoreCompanyRoles.Member);
        Assert.Equal(ModuleAccessStatus.Denied,await new CoreOwnerModuleAccess(t=>f.Context(t),f.Clock)
            .EnsureAsync(Person(f,other),f.Owner.Proof,f.Company,"hrm",License(f)));
        await using var db=f.Context();Assert.Empty(await db.ModuleAccessVersions.ToArrayAsync());
    }
    [Fact]
    public async Task Ownership_removal_invalidates_default_grant_and_never_recreates_it_as_a_member()
    {
        await using var f=await Team.Fixture.Start();await Ensure(f);
        await using(var db=f.Context()){var m=await db.CompanyMemberships.SingleAsync();m.Role=CoreCompanyRoles.Member;m.Revision++;await db.SaveChangesAsync();}
        Assert.Equal(ModuleAccessStatus.Unchanged,await Ensure(f));await using var verify=f.Context();Assert.Single(await verify.ModuleAccessVersions.ToArrayAsync());
        Assert.Equal(ModuleAccessStatus.Denied,(await new CoreModuleAccessReader(verify,f.Clock).ReadAsync(Person(f),f.Owner.Link,f.Company,"hrm",License(f))).Status);
    }
}
