using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Team=Mightyfin.Erp.Hrm.Tests.CoreCompanyTeamCommandsTests;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class NativeEmployeeAdmissionTests
{
    private const string Client="391bdeaa-87b3-44b3-8e64-2110dcc1a564", Enrollment="01arz3ndektsv4rrffq69g5fav";
    private static OperationalSubjectObservation Person(Team.Fixture f,Team.Person p)=>new(p.Subject,Client,f.Clock.GetUtcNow().AddSeconds(30),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromSeconds(30));
    private static CommercialLicenseLease License(Team.Fixture f)=>new(Enrollment,["hrm"],2,f.Clock.GetUtcNow().AddSeconds(60),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromSeconds(60));

    [Fact]
    public async Task GovernanceAndEmailCannotOpenAttendance_ExplicitGrantAndEmployeeLinkAreBothRequired()
    {
        await using var f=await Team.Fixture.Start();var member=await f.Add("43",CoreCompanyRoles.Member);
        await using var db=f.Context();
        var company=await db.LegalEntities.SingleAsync(x=>x.Id==f.Company);company.TimeZoneId="Africa/Lusaka";
        var location=await db.WorkLocations.SingleAsync();
        var worker=new Worker{EmployeeNo="MOBILE-01",FirstName="Synthetic",LastName="Employee",Status="active",LocationId=location.Id,
            SubjectId=member.User.ToString("D"),StartDate=new(2020,1,1)};
        db.Workers.Add(worker);await db.SaveChangesAsync();
        var admission=new NativeEmployeeAdmission(t=>f.Context(t),(_,_)=>Task.FromResult(new CommercialLicenseResult(CommercialLicenseStatus.Authorized,License(f))),f.Clock);
        Assert.Equal(403,(await admission.ReadAsync(Person(f,member),f.Company,member.Link,default)).Status);
        var command=new ModuleAccessCommand(f.Company,member.Member,1,"hrm",0,Guid.NewGuid(),false,["employee"],["hrm-access"],"locations",[location.Id],false,true);
        Assert.Equal(ModuleAccessStatus.Changed,(await new CoreModuleAccessCommands(t=>f.Context(t),f.Clock).ExecuteAsync(Person(f,f.Owner),f.Owner.Proof,command,License(f))).Status);
        var allowed=await admission.ReadAsync(Person(f,member),f.Company,member.Link,default);
        Assert.Equal(200,allowed.Status);Assert.Equal(worker.Id,allowed.Grant!.Worker.Id);Assert.False(allowed.Grant.Scope.AllCompanyLocations);
        Assert.Equal([location.Id],allowed.Grant.Scope.LocationIds);
        Assert.Equal(403,(await admission.ReadAsync(Person(f,member),Guid.NewGuid(),member.Link,default)).Status);
        Assert.Equal(403,(await admission.ReadAsync(Person(f,f.Owner),f.Company,member.Link,default)).Status);
        worker.SubjectId="another-person";worker.Email="matching-but-not-authority@example.test";await db.SaveChangesAsync();
        Assert.Equal(403,(await admission.ReadAsync(Person(f,member),f.Company,member.Link,default)).Status);
        Assert.Empty(await db.LocalSessions.ToArrayAsync());Assert.Empty(await db.TenantRoleAssignments.ToArrayAsync());
    }

    [Fact]
    public async Task MissingEmployeeInactiveEmploymentRevokedGrantAndExpiredLicenseFailClosed()
    {
        await using var f=await Team.Fixture.Start();
        await using var db=f.Context();var location=await db.WorkLocations.SingleAsync();
        var command=new ModuleAccessCommand(f.Company,f.Owner.Member,1,"hrm",0,Guid.NewGuid(),false,["employee"],["hrm-access"],"locations",[location.Id],false,true);
        var commands=new CoreModuleAccessCommands(t=>f.Context(t),f.Clock);
        await commands.ExecuteAsync(Person(f,f.Owner),f.Owner.Proof,command,License(f));
        var deniedLicense=false;
        var admission=new NativeEmployeeAdmission(t=>f.Context(t),(_,_)=>Task.FromResult(deniedLicense?new CommercialLicenseResult(CommercialLicenseStatus.Denied):new CommercialLicenseResult(CommercialLicenseStatus.Authorized,License(f))),f.Clock);
        Assert.Equal(403,(await admission.ReadAsync(Person(f,f.Owner),f.Company,f.Owner.Link,default)).Status);
        var worker=new Worker{EmployeeNo="MOBILE-02",FirstName="Synthetic",LastName="Owner employee",Status="terminated",LocationId=location.Id,SubjectId=f.Owner.User.ToString("D")};
        db.Workers.Add(worker);await db.SaveChangesAsync();
        Assert.Equal(403,(await admission.ReadAsync(Person(f,f.Owner),f.Company,f.Owner.Link,default)).Status);
        worker.Status="active";await db.SaveChangesAsync();
        Assert.Equal(200,(await admission.ReadAsync(Person(f,f.Owner),f.Company,f.Owner.Link,default)).Status);
        deniedLicense=true;Assert.Equal(403,(await admission.ReadAsync(Person(f,f.Owner),f.Company,f.Owner.Link,default)).Status);deniedLicense=false;
        var expired=Person(f,f.Owner);f.Clock.Advance();
        Assert.Equal(403,(await admission.ReadAsync(expired,f.Company,f.Owner.Link,default)).Status);
        await commands.ExecuteAsync(Person(f,f.Owner),f.Owner.Proof,command with{ExpectedRevision=1,CommandKey=Guid.NewGuid(),Revoke=true,RoleKeys=[],Actions=[],ScopeKind="none",LocationIds=[],Confirm=true,AllCompanyLocationsConfirmed=false},License(f));
        Assert.Equal(403,(await admission.ReadAsync(Person(f,f.Owner),f.Company,f.Owner.Link,default)).Status);
    }

    [Fact]
    public void NativeProfileIsOffByDefaultAndCannotSilentlyReplaceStandaloneAuthentication()
    {
        var local=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ERP:AuthMode","local"}}).Build();
        var services=new ServiceCollection();NativeAttendanceApi.Register(services,local,"not-used");Assert.Empty(services);
        local["ERP:MobileAttendance:Enabled"]="true";
        Assert.Throws<InvalidOperationException>(()=>NativeAttendanceApi.Register(services,local,"not-used"));
    }
}
