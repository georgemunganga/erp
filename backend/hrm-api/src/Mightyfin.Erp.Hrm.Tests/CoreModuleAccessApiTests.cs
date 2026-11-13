using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Team=Mightyfin.Erp.Hrm.Tests.CoreCompanyTeamCommandsTests;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreModuleAccessApiTests
{
    internal const string Enrollment="01arz3ndektsv4rrffq69g5fav";
    internal static OperationalSubjectObservation Person(Team.Fixture f,string subject="42")=>new(subject,"391bdeaa-87b3-44b3-8e64-2110dcc1a564",f.Clock.GetUtcNow().AddSeconds(30),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromSeconds(30));
    internal static CommercialLicenseLease License(Team.Fixture f)=>new(Enrollment,["hrm","procurement"],2,f.Clock.GetUtcNow().AddSeconds(60),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromSeconds(60));
    internal static CoreModuleAccessApi Api(Team.Fixture f)=>new(tenant=>f.Context(tenant),(enrollment,_)=>{
        Assert.Equal(Enrollment,enrollment);return Task.FromResult(new CommercialLicenseResult(CommercialLicenseStatus.Authorized,License(f)));},f.Clock);
    [Fact]
    public async Task Owner_review_and_bounded_directories_show_explicit_members_and_locations_without_permissions_or_sessions()
    {
        await using var f=await Team.Fixture.Start();var api=Api(f);var target=await f.Add("43",CoreCompanyRoles.Member);
        Guid[] expectedLocations;
        await using(var db=f.Context()) {
            for(var i=0;i<23;i++)db.WorkLocations.Add(new(){LegalEntityId=f.Company,Code="BR"+i,Name="Branch "+i});
            await db.SaveChangesAsync();
            expectedLocations=await db.WorkLocations.Where(x=>x.LegalEntityId==f.Company && !x.IsArchived).Select(x=>x.Id).ToArrayAsync();
        }
        var members=await api.MembersAsync(Person(f),f.Owner.Proof,f.Company,null);Assert.Equal(ModuleAccessStatus.Authorized,members.Status);Assert.Equal(2,members.Items!.Length);
        var first=await api.LocationsAsync(Person(f),f.Owner.Proof,f.Company,null);Assert.Equal(ModuleAccessStatus.Authorized,first.Status);Assert.Equal(20,first.Items!.Length);Assert.NotNull(first.Next);
        var next=await api.LocationsAsync(Person(f),f.Owner.Proof,f.Company,Guid.Parse(first.Next));Assert.Equal(expectedLocations.Length-20,next.Items!.Length);Assert.Null(next.Next);Assert.Empty(first.Items.Select(x=>x.Id).Intersect(next.Items.Select(x=>x.Id)));
        Assert.Equal(expectedLocations.Order(),first.Items.Concat(next.Items).Select(x=>x.Id).Order());
        var review=await api.ReviewAsync(Person(f),f.Owner.Proof,f.Company,target.Member,"hrm");Assert.Equal(ModuleAccessStatus.Authorized,review.Status);Assert.Null(review.Grant);
        Assert.Equal(target.Member,review.Member!.MembershipId);Assert.DoesNotContain("erp_admin",review.Roles!);Assert.Contains("hrm-access",review.Actions!);
        await using var check=f.Context();Assert.Empty(await check.ModuleAccessVersions.ToArrayAsync());Assert.Empty(await check.LocalSessions.ToArrayAsync());
    }
    [Theory]
    [InlineData(CoreCompanyRoles.Member)][InlineData(CoreCompanyRoles.Administrator)]
    public async Task Governance_nonowners_cannot_review_other_people_or_enumerate_branches(string role)
    {
        await using var f=await Team.Fixture.Start();var actor=await f.Add("43",role);var reads=0;
        var api=new CoreModuleAccessApi(tenant=>f.Context(tenant),(_,_)=>{reads++;return Task.FromResult(new CommercialLicenseResult(CommercialLicenseStatus.Authorized,License(f)));},f.Clock);
        Assert.Equal(ModuleAccessStatus.Denied,(await api.MembersAsync(Person(f,actor.Subject),actor.Proof,f.Company,null)).Status);
        Assert.Equal(ModuleAccessStatus.Denied,(await api.LocationsAsync(Person(f,actor.Subject),actor.Proof,f.Company,null)).Status);
        Assert.Equal(ModuleAccessStatus.Denied,(await api.ReviewAsync(Person(f,actor.Subject),actor.Proof,f.Company,f.Owner.Member,"hrm")).Status);Assert.Equal(0,reads);
    }
    [Theory]
    [InlineData("denied")][InlineData("outage")][InlineData("wrong-enrollment")][InlineData("no-module")][InlineData("expired-person")][InlineData("revoked-owner")]
    public async Task Current_license_and_identity_authority_are_rechecked_after_the_commercial_call(string mode)
    {
        await using var f=await Team.Fixture.Start();
        var api=new CoreModuleAccessApi(tenant=>f.Context(tenant),async(_,ct)=>{
            if(mode=="expired-person")f.Clock.Advance();
            if(mode=="revoked-owner"){await using var db=f.Context();(await db.CompanyMemberships.SingleAsync(x=>x.Id==f.Owner.Member,ct)).IsActive=false;await db.SaveChangesAsync(ct);}
            if(mode is "denied" or "outage")return new CommercialLicenseResult(mode=="denied"?CommercialLicenseStatus.Denied:CommercialLicenseStatus.Unavailable);
            return new CommercialLicenseResult(CommercialLicenseStatus.Authorized,mode=="wrong-enrollment"?new("01arz3ndektsv4rrffq69g5faw",["hrm"],2,f.Clock.GetUtcNow().AddSeconds(60),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromSeconds(60)):
                mode=="no-module"?new(Enrollment,[],2,f.Clock.GetUtcNow().AddSeconds(60),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromSeconds(60)):License(f));
        },f.Clock);
        Assert.Equal(mode=="outage"?ModuleAccessStatus.Unavailable:ModuleAccessStatus.Denied,(await api.ReviewAsync(Person(f),f.Owner.Proof,f.Company,f.Owner.Member,"hrm")).Status);
    }
    [Fact]
    public async Task Review_preserves_the_latest_revocation_and_marks_changed_membership_or_archived_branch_for_confirmation()
    {
        await using var f=await Team.Fixture.Start();var api=Api(f);var target=await f.Add("43",CoreCompanyRoles.Member);Guid branch;
        await using(var db=f.Context()){var location=new WorkLocation{LegalEntityId=f.Company,Code="BR",Name="Review branch"};db.WorkLocations.Add(location);branch=location.Id;await db.SaveChangesAsync();}
        var command=new ModuleAccessCommand(f.Company,target.Member,1,"hrm",0,Guid.NewGuid(),false,["hr_ops"],["hrm-access"],"locations",[branch],false,true);
        Assert.Equal(ModuleAccessStatus.Changed,(await api.ExecuteAsync(Person(f),f.Owner.Proof,command)).Status);
        var review=await api.ReviewAsync(Person(f),f.Owner.Proof,f.Company,target.Member,"hrm");Assert.False(review.Grant!.RequiresReconfirmation);Assert.Equal("Review branch",Assert.Single(review.Grant.Locations).Name);
        await using(var db=f.Context()){(await db.WorkLocations.SingleAsync(x=>x.Id==branch)).IsArchived=true;(await db.CompanyMemberships.SingleAsync(x=>x.Id==target.Member)).Revision++;await db.SaveChangesAsync();}
        review=await api.ReviewAsync(Person(f),f.Owner.Proof,f.Company,target.Member,"hrm");Assert.True(review.Grant!.RequiresReconfirmation);Assert.Equal(2,review.Member!.Revision);Assert.Null(Assert.Single(review.Grant.Locations).Name);
        var revoke=command with{MembershipRevision=2,ExpectedRevision=1,CommandKey=Guid.NewGuid(),Revoke=true,RoleKeys=[],Actions=[],ScopeKind="none",LocationIds=[]};
        Assert.Equal(ModuleAccessStatus.Changed,(await api.ExecuteAsync(Person(f),f.Owner.Proof,revoke)).Status);
        review=await api.ReviewAsync(Person(f),f.Owner.Proof,f.Company,target.Member,"hrm");Assert.Equal("revoked",review.Grant!.State);Assert.Equal(2,review.Grant.Revision);
    }
}
