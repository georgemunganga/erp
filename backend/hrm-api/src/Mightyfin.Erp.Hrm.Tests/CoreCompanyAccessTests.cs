using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreCompanyAccessTests
{
    private const string Enrollment = "01arz3ndektsv4rrffq69g5fav";
    private const string Client = "391bdeaa-87b3-44b3-8e64-2110dcc1a564";

    [Theory]
    [InlineData("member")][InlineData("owner")][InlineData("administrator")]
    public async Task Operational_company_review_reads_only_explicit_membership_and_the_bound_purchase(string role)
    {
        await using var f=await Fixture.Start();await using var db=f.Context("untrusted-company-review");
        await using(var change=f.Context(f.Binding.TenantId)){(await change.CompanyMemberships.SingleAsync()).Role=role;await change.SaveChangesAsync();}
        var person=new OperationalSubjectObservation("42",Client,f.Clock.GetUtcNow().AddSeconds(30),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromSeconds(30));
        var calls=0;
        var api=new CoreOperationalCompanies(db,(enrollment,_)=>{calls++;Assert.Equal(Enrollment,enrollment);
            return Task.FromResult(new CommercialLicenseResult(CommercialLicenseStatus.Authorized,new(Enrollment,["hrm","procurement"],2,
                f.Clock.GetUtcNow().AddSeconds(60),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromSeconds(60))));},f.Clock);
        var page=await api.DirectoryAsync(person,null);Assert.Equal(CompanyTeamReadStatus.Ok,page.Status);Assert.Equal(role,Assert.Single(page.Items!).Role);Assert.Equal(0,calls);
        Assert.Equal(CompanyTeamReadStatus.Denied,(await api.ReviewAsync(person,Guid.NewGuid(),f.Binding.LegalEntityId)).Status);Assert.Equal(0,calls);
        var review=await api.ReviewAsync(person,f.Link.Id,f.Binding.LegalEntityId);Assert.Equal(CompanyTeamReadStatus.Ok,review.Status);
        Assert.Equal(["hrm","procurement"],Assert.IsType<string[]>(review.LicensedModules));Assert.Equal(role,review.Company!.Role);Assert.Equal(1,calls);
        Assert.Empty(await db.LocalSessions.IgnoreQueryFilters().ToArrayAsync());Assert.Empty(await db.TenantRoleAssignments.IgnoreQueryFilters().ToArrayAsync());
    }
    [Theory]
    [InlineData("revoked-member")][InlineData("person-expired")][InlineData("license-denied")][InlineData("license-unavailable")][InlineData("foreign-license")]
    public async Task Operational_company_review_rechecks_person_membership_and_license_after_the_provider_call(string fault)
    {
        await using var f=await Fixture.Start();await using var db=f.Context("untrusted-company-review");
        var person=new OperationalSubjectObservation("42",Client,f.Clock.GetUtcNow().AddSeconds(30),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromSeconds(30));
        var api=new CoreOperationalCompanies(db,async (_,_)=> {
            if(fault=="revoked-member") {await using var change=f.Context(f.Binding.TenantId);(await change.CompanyMemberships.SingleAsync()).IsActive=false;await change.SaveChangesAsync();}
            if(fault=="person-expired")f.Clock.Advance();
            return fault=="license-denied"?new(CommercialLicenseStatus.Denied):fault=="license-unavailable"?new(CommercialLicenseStatus.Unavailable)
                :new CommercialLicenseResult(CommercialLicenseStatus.Authorized,f.License(enrollment:fault=="foreign-license"?"foreign":Enrollment));
        },f.Clock);
        var review=await api.ReviewAsync(person,f.Link.Id,f.Binding.LegalEntityId);
        Assert.Equal(fault=="license-unavailable"?CompanyTeamReadStatus.Unavailable:CompanyTeamReadStatus.Denied,review.Status);
        Assert.Null(review.Company);Assert.Null(review.LicensedModules);
    }

    [Theory]
    [InlineData(CoreCompanyRoles.Owner, true, true)]
    [InlineData(CoreCompanyRoles.Administrator, true, false)]
    [InlineData(CoreCompanyRoles.Member, false, false)]
    public async Task Explicit_company_governance_is_separate_from_operational_and_billing_roles(string role, bool team, bool owners)
    {
        await using var f = await Fixture.Start();
        await using var db = f.Context(f.Binding.TenantId);
        var membership = await db.CompanyMemberships.SingleAsync();
        membership.Role = role; await db.SaveChangesAsync();
        var decision = await new CoreCompanyAccess(db, f.Clock).ReadAsync(ProductCommercialLicense.Issuer,
            "42", f.Link.Id, f.Binding.LegalEntityId, f.License());
        Assert.NotNull(decision); Assert.Equal(role, decision.Role);
        Assert.Equal(team, decision.MayManageTeam); Assert.Equal(owners, decision.MayManageOwners);
        Assert.Equal(f.Binding.TenantId, decision.TenantId);
        var directory=await new CoreCompanyAccess(db,f.Clock).MemberDirectoryAsync(ProductCommercialLicense.Issuer,"42",null);
        Assert.Equal(f.Binding.LegalEntityId,Assert.Single(directory.Rows).CompanyId);
        var governance=await new CoreCompanyAccess(db,f.Clock).DirectoryAsync(ProductCommercialLicense.Issuer,"42",null);
        Assert.Equal(team?1:0,governance.Rows.Length);
        Assert.Equal("", (await db.LocalUsers.SingleAsync()).RolesCsv);
        Assert.Empty(await db.LocalSessions.ToArrayAsync()); Assert.Empty(await db.TenantRoleAssignments.ToArrayAsync());
    }

    [Theory]
    [InlineData("foreign-subject")][InlineData("foreign-issuer")][InlineData("foreign-link")]
    [InlineData("foreign-company")][InlineData("foreign-license")][InlineData("expired-license")]
    [InlineData("zero-capacity")][InlineData("inactive-user")][InlineData("archived-user")]
    [InlineData("locked-user")][InlineData("revoked-link")][InlineData("archived-link")]
    [InlineData("inactive-membership")][InlineData("archived-membership")][InlineData("archived-company")]
    [InlineData("missing-membership")]
    [InlineData("foreign-binding-issuer")]
    public async Task Company_access_denies_without_every_current_independent_proof(string fault)
    {
        await using var f = await Fixture.Start();
        await using var db = f.Context(f.Binding.TenantId);
        var user = await db.LocalUsers.SingleAsync();
        var link = await db.ExternalIdentityLinks.SingleAsync();
        var member = await db.CompanyMemberships.SingleAsync();
        if (fault == "inactive-user") user.IsActive = false;
        if (fault == "archived-user") user.IsArchived = true;
        if (fault == "locked-user") user.LockedUntil = f.Clock.GetUtcNow().AddMinutes(1);
        if (fault == "revoked-link") link.IsActive = false;
        if (fault == "archived-link") link.IsArchived = true;
        if (fault == "inactive-membership") member.IsActive = false;
        if (fault == "archived-membership") member.IsArchived = true;
        if (fault == "archived-company") (await db.LegalEntities.SingleAsync()).IsArchived = true;
        await db.SaveChangesAsync();
        if (fault == "missing-membership")
            await db.Database.ExecuteSqlRawAsync("DELETE FROM core_company_memberships");
        if (fault == "foreign-binding-issuer")
            await db.Database.ExecuteSqlRawAsync("UPDATE core_workspace_bindings SET issuer='https://other.example'");
        var license = f.License(fault == "zero-capacity" ? 0 : 2, fault == "foreign-license" ? "other" : Enrollment);
        if (fault == "expired-license") f.Clock.Advance();
        Assert.Null(await new CoreCompanyAccess(db, f.Clock).ReadAsync(
            fault == "foreign-issuer" ? "https://other.example" : ProductCommercialLicense.Issuer,
            fault == "foreign-subject" ? "43" : "42", fault == "foreign-link" ? Guid.NewGuid() : f.Link.Id,
            fault == "foreign-company" ? Guid.NewGuid() : f.Binding.LegalEntityId, license));
    }

    [Fact]
    public async Task Historical_allocation_owner_does_not_override_revoked_company_membership()
    {
        await using var f = await Fixture.Start();
        await using var db = f.Context(f.Binding.TenantId);
        (await db.CompanyMemberships.SingleAsync()).IsActive = false;
        await db.SaveChangesAsync();
        Assert.NotNull(await db.WorkspaceBindings.SingleAsync());
        Assert.Null(await new CoreCompanyAccess(db, f.Clock).ReadAsync(ProductCommercialLicense.Issuer, "42", f.Link.Id,
            f.Binding.LegalEntityId, f.License()));
    }

    [Fact]
    public async Task Same_identity_link_cannot_select_a_different_company_even_with_the_same_purchase()
    {
        await using var f = await Fixture.Start();
        var second = await f.Create("second_company_request_00000000000000001");
        Assert.Equal(WorkspaceCreateStatus.Created, second.Status);
        await using var db = f.Context("untrusted-default");
        Assert.Null(await new CoreCompanyAccess(db, f.Clock).ReadAsync(ProductCommercialLicense.Issuer, "42", f.Link.Id,
            second.CompanyId!.Value, f.License()));
    }

    [Fact]
    public async Task Company_membership_binding_cannot_be_retargeted_or_deleted_and_roles_are_bounded()
    {
        foreach (var change in new[] { "company", "user", "delete", "role" })
        {
            await using var f = await Fixture.Start();
            await using var db = f.Context(f.Binding.TenantId);
            var member = await db.CompanyMemberships.SingleAsync();
            if (change == "company") member.LegalEntityId = Guid.NewGuid();
            if (change == "user") member.LocalUserId = Guid.NewGuid();
            if (change == "delete") db.CompanyMemberships.Remove(member);
            if (change == "role") member.Role = "erp_admin";
            // Company now participates in the receipt's composite principal key;
            // EF rejects retargeting before the ordinary domain write guard.
            if (change == "company") await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            else await Assert.ThrowsAsync<DomainException>(() => db.SaveChangesAsync());
            await using var unchanged = f.Context(f.Binding.TenantId);
            Assert.Equal(f.Binding.LegalEntityId, (await unchanged.CompanyMemberships.SingleAsync()).LegalEntityId);
        }
    }

    [Fact]
    public async Task Relational_constraints_reject_unknown_roles_duplicate_and_cross_tenant_memberships()
    {
        await using var f = await Fixture.Start();
        await using (var db = f.Context(f.Binding.TenantId))
        {
            await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(
                "UPDATE core_company_memberships SET role='erp_admin'"));
            db.CompanyMemberships.Add(new() { TenantId = f.Binding.TenantId, LegalEntityId = f.Binding.LegalEntityId,
                LocalUserId = f.Binding.OwnerLocalUserId, Role = CoreCompanyRoles.Member });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        Guid foreignUser;
        await using (var other = f.Context("other-customer"))
        {
            var user = LocalUser.Federated("Other customer member"); foreignUser = user.Id;
            other.LocalUsers.Add(user); await other.SaveChangesAsync();
        }
        await using (var db = f.Context(f.Binding.TenantId))
        {
            db.CompanyMemberships.Add(new() { TenantId = f.Binding.TenantId, LegalEntityId = f.Binding.LegalEntityId,
                LocalUserId = foreignUser, Role = CoreCompanyRoles.Member });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using var inspect = f.Context(f.Binding.TenantId);
        Assert.Equal(CoreCompanyRoles.Owner, (await inspect.CompanyMemberships.SingleAsync()).Role);
    }

    private sealed class Fixture(SqliteConnection sqlite, DbContextOptions<HrmDbContext> options) : IAsyncDisposable
    {
        internal CoreWorkspaceProvisionerTests.Clock Clock { get; } = new();
        internal CoreWorkspaceBinding Binding { get; private set; } = null!;
        internal ExternalIdentityLink Link { get; private set; } = null!;
        internal HrmDbContext Context(string tenant) => new(options, new Tenant(tenant));
        internal CommercialLicenseLease License(int limit = 2, string enrollment = Enrollment) => new(enrollment, [], limit,
            Clock.GetUtcNow().AddSeconds(60), Clock, Clock.GetTimestamp(), TimeSpan.FromSeconds(60));
        internal Task<WorkspaceCreateResult> Create(string key) => new CoreWorkspaceProvisioner(Context, Clock).CreateAsync(
            new("42", Client, "01arz3ndektsv4rrffq69g5faw", Enrollment, 2, Clock.GetUtcNow().AddSeconds(60),
                Clock, Clock.GetTimestamp(), TimeSpan.FromSeconds(60)), License(), key, "Company");
        internal static async Task<Fixture> Start()
        {
            var sqlite = new SqliteConnection("Data Source=:memory:"); await sqlite.OpenAsync();
            var f = new Fixture(sqlite, new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(sqlite).Options);
            await using var db = f.Context("schema"); await db.Database.EnsureCreatedAsync();
            Assert.Equal(WorkspaceCreateStatus.Created, (await f.Create("first_company_request_000000000000000001")).Status);
            f.Binding = await db.WorkspaceBindings.IgnoreQueryFilters().SingleAsync();
            f.Link = await db.ExternalIdentityLinks.IgnoreQueryFilters().SingleAsync();
            return f;
        }
        public async ValueTask DisposeAsync() => await sqlite.DisposeAsync();
        private sealed class Tenant(string value) : ITenantAccessor { public string GetTenantId() => value; }
    }
}
