using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class SpendingAuthorityPersistenceTests
{
    private sealed class Scope(Guid company, string actor) : IProcurementScope
    {
        public string TenantId => "tenant";
        public Guid LegalEntityId => company;
        public string SubjectId => actor;
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "authority-test";
    }
    private static ProcurementDbContext Context(string database, Guid company, string actor)
        => new(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(database).Options, new Scope(company, actor));

    [Fact]
    public async Task Lifecycle_versions_advance_once_and_changes_are_scoped_and_audited()
    {
        var name = Guid.NewGuid().ToString(); var company = Guid.NewGuid();
        var id = await Create(name, company);
        await using (var db = Context(name, company, "owner"))
        {
            var grant = await db.SpendingAuthorityGrants.Include(x => x.Categories).SingleAsync();
            Assert.Equal(1, grant.Version);
            SpendingAuthorityConfiguration.Submit(grant, 1, Actor(company, "owner",
                SpendingAuthorityConfiguration.ConfigurePermission), Subject(company), DateTimeOffset.UtcNow);
            Assert.Equal(2, grant.Version);
            await db.SaveChangesAsync();
            Assert.Equal(2, grant.Version);
            Assert.Equal(2, await db.AuditEvents.CountAsync(x => x.RecordId == id));
        }
        await using (var db = Context(name, company, "reviewer"))
        {
            var grant = await db.SpendingAuthorityGrants.Include(x => x.Categories).SingleAsync();
            SpendingAuthorityConfiguration.Activate(grant, 2, Actor(company, "reviewer",
                SpendingAuthorityConfiguration.ReviewPermission), Subject(company), "Approved finite limit", DateTimeOffset.UtcNow, [grant]);
            await db.SaveChangesAsync();
            Assert.Equal(3, grant.Version);
        }
        await using var foreign = Context(name, Guid.NewGuid(), "reviewer");
        Assert.Empty(await foreign.SpendingAuthorityGrants.ToListAsync());
        Assert.Empty(await foreign.SpendingAuthorityCategories.ToListAsync());
    }

    [Theory]
    [InlineData("ChangeAmount")]
    [InlineData("ChangeCategory")]
    [InlineData("AddCategory")]
    [InlineData("DeleteCategory")]
    public async Task Published_business_and_all_child_mutation_paths_are_denied(string mutation)
    {
        var name = Guid.NewGuid().ToString(); var company = Guid.NewGuid();
        await Publish(name, company);
        await using var db = Context(name, company, "reviewer");
        var grant = await db.SpendingAuthorityGrants.Include(x => x.Categories).SingleAsync();
        switch (mutation)
        {
            case "ChangeAmount": grant.MaximumAmount = 1000; break;
            case "ChangeCategory": grant.Categories.Single().CategoryCode = "OTHER"; break;
            case "AddCategory": db.AddScoped(new ProcurementSpendingAuthorityCategory
                { GrantId = grant.Id, CategoryCode = "OTHER" }); break;
            case "DeleteCategory": db.SpendingAuthorityCategories.Remove(grant.Categories.Single()); break;
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_boolean_overloads_also_enforce_publication_and_terminal_guards()
    {
        var name = Guid.NewGuid().ToString(); var company = Guid.NewGuid();
        await Publish(name, company);
        await using var db = Context(name, company, "reviewer");
        var grant = await db.SpendingAuthorityGrants.Include(x => x.Categories).SingleAsync();
        grant.MaximumAmount = 1000;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(true));
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges(true));
    }

    [Fact]
    public async Task Draft_category_replacement_is_allowed_but_logical_key_changes_are_denied()
    {
        var name = Guid.NewGuid().ToString(); var company = Guid.NewGuid();
        await Create(name, company);
        await using var db = Context(name, company, "owner");
        var grant = await db.SpendingAuthorityGrants.Include(x => x.Categories).SingleAsync();
        db.SpendingAuthorityCategories.RemoveRange(grant.Categories);
        SpendingAuthorityConfiguration.EditDraft(grant, grant.Version, Draft() with { CategoryCodes = ["OFFICE"] },
            Actor(company, "owner", SpendingAuthorityConfiguration.ConfigurePermission), DateTimeOffset.UtcNow);
        db.SpendingAuthorityCategories.AddRange(grant.Categories);
        await db.SaveChangesAsync();
        Assert.Equal("OFFICE", (await db.SpendingAuthorityCategories.SingleAsync()).CategoryCode);
        grant.AuthorityReference = "RENAMED";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task New_rows_cannot_skip_draft_review_and_nonpostgres_commands_fail_closed()
    {
        var company = Guid.NewGuid();
        await using var db = Context(Guid.NewGuid().ToString(), company, "owner");
        var grant = SpendingAuthorityConfiguration.CreateDraft("AUTH", "subject", Draft(),
            Actor(company, "owner", SpendingAuthorityConfiguration.ConfigurePermission), DateTimeOffset.UtcNow, []);
        grant.State = "Active";
        db.AddScoped(grant);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => SpendingAuthorityFamilyTransaction.BeginAsync(db,
            "tenant", company, "subject", SpendingAuthorityConfiguration.ApprovalPermission,
            SpendingAuthorityConfiguration.ApprovalAction, "AUTH", CancellationToken.None));
    }

    private static SpendingAuthorityDraft Draft() => new("ZMW", "EstimatedLineBase", 100,
        DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), true, null, true, null, false, ["IT"]);
    private static SpendingAuthorityConfigurationActor Actor(Guid company, string actor, string permission)
        => new("tenant", company, actor, true, new HashSet<string>([permission]));
    private static SpendingAuthoritySubject Subject(Guid company) => new("tenant", company, "subject", true, true);
    private static async Task<Guid> Create(string name, Guid company)
    {
        await using var db = Context(name, company, "owner");
        var grant = SpendingAuthorityConfiguration.CreateDraft("AUTH", "subject", Draft(),
            Actor(company, "owner", SpendingAuthorityConfiguration.ConfigurePermission), DateTimeOffset.UtcNow, []);
        db.AddScoped(grant); await db.SaveChangesAsync(); return grant.Id;
    }
    private static async Task Publish(string name, Guid company)
    {
        await Create(name, company);
        await using (var db = Context(name, company, "owner"))
        {
            var grant = await db.SpendingAuthorityGrants.Include(x => x.Categories).SingleAsync();
            SpendingAuthorityConfiguration.Submit(grant, grant.Version, Actor(company, "owner",
                SpendingAuthorityConfiguration.ConfigurePermission), Subject(company), DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        await using (var db = Context(name, company, "reviewer"))
        {
            var grant = await db.SpendingAuthorityGrants.Include(x => x.Categories).SingleAsync();
            SpendingAuthorityConfiguration.Activate(grant, grant.Version, Actor(company, "reviewer",
                SpendingAuthorityConfiguration.ReviewPermission), Subject(company), "Independent review", DateTimeOffset.UtcNow, [grant]);
            await db.SaveChangesAsync();
        }
    }
}
