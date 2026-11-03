using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Actual owning command handlers and Core projections with real EF guards.
/// InMemory deliberately bypasses no schema-readiness flag: these are handler tests,
/// not mapped PostgreSQL transaction, finite-role or native workflow acceptance.</summary>
public sealed class ProcurementPlanPolicyLifecycleTests
{
    [Fact]
    public async Task Workerless_policy_author_may_be_named_reviewer_and_other_checker_publishes_real_lineage()
    {
        await using var f = await Fixture.Start();
        var created = await f.Create(f.Rules());
        Assert.Equal(201, Status(created));
        var id = Body(created).GetProperty("id").GetGuid();
        Assert.Equal("Draft", Body(created).GetProperty("state").GetString());
        var submitted = await f.Command("SubmitReviewPolicy", id, new ProcurementPolicyDraftRoutes.ReviewSubmission(1, "Review named people"));
        Assert.Equal(200, Status(submitted));
        Assert.Equal("PendingReview", Body(submitted).GetProperty("state").GetString());
        var self = await f.Command("PublishPolicy", id, new ProcurementPolicyDraftRoutes.PublicationInput(2, "Self publication"));
        Assert.Equal(403, Status(self));
        var published = await f.Command("PublishPolicy", id, new ProcurementPolicyDraftRoutes.PublicationInput(2, "Independent publication"), f.Publisher.Id);
        Assert.Equal(200, Status(published));
        Assert.Equal("Published", Body(published).GetProperty("state").GetString());
        using var db = f.Context(f.Author.Id);
        Assert.Equal(3, await db.OperationalPolicyIdentityRecords.CountAsync());
        Assert.Equal(new[] { "Created", "Published", "Submitted" }, await db.OperationalPolicyIdentityRecords
            .OrderBy(x => x.Stage).Select(x => x.Stage).ToArrayAsync());
        Assert.All(await db.OperationalPolicyIdentityRecords.ToArrayAsync(), x => { Assert.Equal(2, x.SchemaVersion); Assert.Null(x.ActorWorkerId); });
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("currency")]
    [InlineData("assignee")]
    public async Task Invalid_creation_never_persists_policy_or_identity_or_audit(string kind)
    {
        await using var f = await Fixture.Start();
        var rules = kind == "schema" ? "{\"schemaVersion\":1,\"stages\":[]}"
            : kind == "currency" ? f.Rules().Replace("\"schemaVersion\":2", "\"currencyCode\":\"USD\",\"schemaVersion\":2")
            : f.Rules(final: Guid.NewGuid());
        var result = await f.Create(rules);
        Assert.Equal(422, Status(result));
        using var db = f.Context(f.Author.Id);
        Assert.Empty(await db.PolicyVersions.ToArrayAsync());
        Assert.Empty(await db.OperationalPolicyIdentityRecords.ToArrayAsync());
        Assert.Empty(await db.AuditEvents.ToArrayAsync());
    }

    [Fact]
    public async Task Missing_Core_currency_denies_configuration_without_inventing_a_policy_currency()
    {
        await using var f = await Fixture.Start();
        f.Company.Currency = ""; await f.Core.SaveChangesAsync();
        Assert.Equal(409, Status(await f.Create(f.Rules())));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Assignee_permission_revocation_is_rechecked_before_submit_and_publish(bool publish)
    {
        await using var f = await Fixture.Start();
        var id = Body(await f.Create(f.Rules())).GetProperty("id").GetGuid();
        if (publish) Assert.Equal(200, Status(await f.Command("SubmitReviewPolicy", id,
            new ProcurementPolicyDraftRoutes.ReviewSubmission(1, "Submit"))));
        f.FinalRole.PermissionsCsv = "procurement-access"; await f.Core.SaveChangesAsync();
        var denied = publish
            ? await f.Command("PublishPolicy", id, new ProcurementPolicyDraftRoutes.PublicationInput(2, "Publish"), f.Publisher.Id)
            : await f.Command("SubmitReviewPolicy", id, new ProcurementPolicyDraftRoutes.ReviewSubmission(1, "Submit"));
        Assert.Equal(422, Status(denied));
        using var db = f.Context(f.Author.Id);
        var policy = await db.PolicyVersions.SingleAsync();
        Assert.Equal(publish ? "PendingReview" : "Draft", policy.State);
        Assert.Equal(publish ? 2 : 1, await db.OperationalPolicyIdentityRecords.CountAsync());
    }

    [Fact]
    public async Task Review_and_final_alias_collision_or_branch_confined_assignee_denies_global_creation()
    {
        await using var f = await Fixture.Start();
        var alias = Guid.NewGuid(); f.Author.WorkerId = alias; f.Final.WorkerId = alias;
        await f.Core.SaveChangesAsync();
        Assert.Equal(422, Status(await f.Create(f.Rules())));
        f.Author.WorkerId = null; f.Final.WorkerId = null;
        var second = new WorkLocation { TenantId = Fixture.Tenant, LegalEntityId = f.Company.Id, Code = "B", Name = "Other" };
        f.Core.WorkLocations.Add(second);
        f.Core.UserBranchAssignments.Add(
            new HrUserBranchAssignment { TenantId = Fixture.Tenant, UserId = f.Final.Id, LocationId = second.Id });
        await f.Core.SaveChangesAsync();
        Assert.Equal(422, Status(await f.Create(f.Rules())));
    }

    [Fact]
    public async Task Picker_counts_only_eligible_people_before_paging_and_hydrates_only_current_named_ids()
    {
        await using var f = await Fixture.Start();
        var review = Body(await f.Picker("Review"));
        Assert.Equal(1, review.GetProperty("candidateTotal").GetInt32());
        Assert.Equal(f.Author.Id, review.GetProperty("items")[0].GetProperty("principalId").GetGuid());
        var final = Body(await f.Picker("Final", f.Final.Id.ToString("D") + "," + f.Publisher.Id.ToString("D")));
        Assert.Equal(1, final.GetProperty("candidateTotal").GetInt32());
        Assert.Equal(f.Final.Id, final.GetProperty("items")[0].GetProperty("principalId").GetGuid());
        var after = Body(await f.Picker("Final", null, 1));
        Assert.Equal(1, after.GetProperty("candidateTotal").GetInt32());
        Assert.Empty(after.GetProperty("items").EnumerateArray());
        f.Final.IsActive = false; await f.Core.SaveChangesAsync();
        var unavailable = Body(await f.Picker("Final", f.Final.Id.ToString("D")));
        Assert.Equal(0, unavailable.GetProperty("candidateTotal").GetInt32());
        Assert.Empty(unavailable.GetProperty("items").EnumerateArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Picker_rejects_invalid_hydration_ids_without_returning_labels(string ids)
    {
        await using var f = await Fixture.Start();
        Assert.Equal(422, Status(await f.Picker("Review", ids)));
    }

    [Fact]
    public async Task Draft_update_rejects_malformed_rules_without_mutating_saved_lineage()
    {
        await using var f = await Fixture.Start();
        var id = Body(await f.Create(f.Rules())).GetProperty("id").GetGuid();
        var update = new ProcurementPolicyDraftRoutes.VersionedPolicyDraft(1,
            new ProcurementPolicyDraftRoutes.PolicyDraft(PlanReviewPolicyRules.PolicyKey,
                DateTimeOffset.UtcNow.AddDays(1), null, "{\"schemaVersion\":2,\"stages\":[]}"));
        Assert.Equal(422, Status(await f.Command("UpdatePolicy", id, update)));
        using var db = f.Context(f.Author.Id);
        Assert.Equal(f.Rules(), (await db.PolicyVersions.SingleAsync()).RulesJson);
        Assert.Single(await db.OperationalPolicyIdentityRecords.ToArrayAsync());
    }

    [Fact]
    public async Task Picker_rejects_duplicate_and_excessive_hydration_without_unbounded_resolution()
    {
        await using var f = await Fixture.Start();
        var duplicate = f.Author.Id.ToString("D") + "," + f.Author.Id.ToString("D");
        Assert.Equal(422, Status(await f.Picker("Review", duplicate)));
        Assert.Equal(422, Status(await f.Picker("Review", string.Join(',', Enumerable.Range(0, 51).Select(_ => Guid.NewGuid().ToString("D"))))));
    }

    [Theory]
    [InlineData("All")]
    [InlineData("Department")]
    public async Task Current_global_configuration_authority_survives_shell_All_or_department(string shell)
    {
        await using var f = await Fixture.Start(); f.ShellContext = shell;
        var picker = Body(await f.Picker("Review"));
        Assert.Equal(1, picker.GetProperty("candidateTotal").GetInt32());
        Assert.Equal(f.Author.Id, picker.GetProperty("items")[0].GetProperty("principalId").GetGuid());
        var created = await f.Create(f.Rules()); Assert.Equal(201, Status(created));
        var id = Body(created).GetProperty("id").GetGuid();
        Assert.Equal(200, Status(await f.Command("SubmitReviewPolicy", id,
            new ProcurementPolicyDraftRoutes.ReviewSubmission(1, "Review"))));
        Assert.Equal(200, Status(await f.Command("PublishPolicy", id,
            new ProcurementPolicyDraftRoutes.PublicationInput(2, "Publish"), f.Publisher.Id)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Current_branch_confined_actor_cannot_change_global_policy_even_with_configuration_permission(bool publish)
    {
        await using var f = await Fixture.Start();
        var id = Body(await f.Create(f.Rules())).GetProperty("id").GetGuid();
        if (publish) Assert.Equal(200, Status(await f.Command("SubmitReviewPolicy", id,
            new ProcurementPolicyDraftRoutes.ReviewSubmission(1, "Review"))));
        var actor = publish ? f.Publisher.Id : f.Author.Id;
        f.Core.UserBranchAssignments.Add(new HrUserBranchAssignment { TenantId = Fixture.Tenant,
            UserId = actor, LocationId = f.Branch.Id });
        await f.Core.SaveChangesAsync();
        var denied = publish
            ? await f.Command("PublishPolicy", id, new ProcurementPolicyDraftRoutes.PublicationInput(2, "Publish"), actor)
            : await f.Command("SubmitReviewPolicy", id, new ProcurementPolicyDraftRoutes.ReviewSubmission(1, "Review"), actor);
        Assert.Equal(403, Status(denied));
        using var db = f.Context(actor);
        Assert.Equal(publish ? "PendingReview" : "Draft", (await db.PolicyVersions.SingleAsync()).State);
        Assert.Equal(publish ? 2 : 1, await db.OperationalPolicyIdentityRecords.CountAsync());
    }

    [Fact]
    public async Task Branch_confined_creator_cannot_create_an_entity_global_policy()
    {
        await using var f = await Fixture.Start();
        f.Core.UserBranchAssignments.Add(new HrUserBranchAssignment { TenantId = Fixture.Tenant,
            UserId = f.Author.Id, LocationId = f.Branch.Id }); await f.Core.SaveChangesAsync();
        Assert.Equal(403, Status(await f.Create(f.Rules())));
        using var db = f.Context(f.Author.Id); Assert.Empty(await db.PolicyVersions.ToArrayAsync());
    }

    [Fact]
    public async Task Global_assignee_must_cover_company_even_if_confined_to_the_selected_branch()
    {
        await using var f = await Fixture.Start();
        f.Core.UserBranchAssignments.Add(new HrUserBranchAssignment { TenantId = Fixture.Tenant,
            UserId = f.Final.Id, LocationId = f.Branch.Id }); await f.Core.SaveChangesAsync();
        Assert.Equal(422, Status(await f.Create(f.Rules())));
        var picker = Body(await f.Picker("Final", f.Final.Id.ToString("D")));
        Assert.Equal(0, picker.GetProperty("candidateTotal").GetInt32());
        Assert.Empty(picker.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Confined_configuration_reader_does_not_gain_global_people_directory_access()
    {
        await using var f = await Fixture.Start();
        f.Core.UserBranchAssignments.Add(new HrUserBranchAssignment { TenantId = Fixture.Tenant,
            UserId = f.Author.Id, LocationId = f.Branch.Id }); await f.Core.SaveChangesAsync();
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>(await f.Picker("Review"));
    }

    private static int? Status(IResult result) => ((IStatusCodeHttpResult)result).StatusCode;
    private static JsonElement Body(IResult result) => JsonSerializer.SerializeToElement(((IValueHttpResult)result).Value,
        new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private static Task<IResult> Invoke(string method, params object?[] args)
        => (Task<IResult>)typeof(ProcurementPolicyDraftRoutes).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, args)!;

    private sealed class Fixture : IAsyncDisposable
    {
        internal const string Tenant = "plan-policy-tests";
        internal required HrmDbContext Core;
        internal required LegalEntity Company;
        internal required WorkLocation Branch;
        internal required LocalUser Author;
        internal required LocalUser Publisher;
        internal required LocalUser Final;
        internal required TenantRoleAssignment FinalRole;
        internal string ShellContext = "";
        private readonly DbContextOptions<ProcurementDbContext> procurement = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot()).Options;
        internal ProcurementRequestScope Scope(Guid actor)
        {
            var scope = new ProcurementRequestScope();
            scope.Set(Tenant, Company.Id, actor.ToString("D"), ShellContext == "" ? Branch.Id : null,
                ShellContext == "Department" ? Guid.NewGuid() : null, false, "handler-test",
                explicitAllBranches: ShellContext == "All", permittedWorkLocationIds: new[] { Branch.Id });
            scope.AssignPrincipal(actor); return scope;
        }
        internal ProcurementDbContext Context(Guid actor) => new(procurement, Scope(actor));
        internal string Rules(Guid? final = null) => JsonSerializer.Serialize(new { schemaVersion = 2, stages = new[] {
            new { purpose = "Review", mode = "Any", requiredApprovals = 1, assigneePrincipalIds = new[] { Author.Id.ToString("D") } },
            new { purpose = "Final", mode = "Any", requiredApprovals = 1, assigneePrincipalIds = new[] { (final ?? Final.Id).ToString("D") } } } });
        internal async Task<IResult> Create(string rules)
        {
            using var db = Context(Author.Id);
            return await Invoke("CreatePolicy", new ProcurementPolicyDraftRoutes.PolicyDraft(PlanReviewPolicyRules.PolicyKey,
                DateTimeOffset.UtcNow.AddDays(1), null, rules), db, Core, Scope(Author.Id), new DefaultHttpContext(), CancellationToken.None);
        }
        internal async Task<IResult> Command(string name, Guid id, object input, Guid? actor = null)
        {
            var who = actor ?? Author.Id; using var db = Context(who);
            return await Invoke(name, id, input, db, Core, Scope(who), new DefaultHttpContext(), CancellationToken.None);
        }
        internal Task<IResult> Picker(string purpose, string? ids = null, int offset = 0)
            => Invoke("PlanReviewers", purpose, null, ids, offset, 1, new DefaultHttpContext(), Core, Scope(Author.Id), CancellationToken.None);
        internal static async Task<Fixture> Start()
        {
            var core = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
                new FixedTenantAccessor(Tenant));
            var company = new LegalEntity { TenantId = Tenant, Code = "A", RegisteredName = "Policy test", Currency = "ZMW" };
            var branch = new WorkLocation { TenantId = Tenant, LegalEntityId = company.Id, Code = "A", Name = "Branch" };
            static LocalUser Person(string label) => new() { TenantId = Tenant, Email = label + "@example.test",
                NormalizedEmail = (label + "@example.test").ToUpperInvariant(), DisplayName = label,
                PasswordHash = "unused-test", RolesCsv = label, IsActive = true };
            var author = Person("author"); var publisher = Person("publisher"); var final = Person("final");
            var finalRole = new TenantRoleAssignment { TenantId = Tenant, RoleKey = "final", RoleName = "Final",
                PermissionsCsv = "procurement-access,procurement-plan-approve" };
            core.LegalEntities.Add(company); core.WorkLocations.Add(branch); core.LocalUsers.AddRange(author, publisher, final);
            core.TenantRoleAssignments.AddRange(finalRole,
                new TenantRoleAssignment { TenantId = Tenant, RoleKey = "author", RoleName = "Author",
                    PermissionsCsv = "procurement-access,procurement-configuration-read,procurement-configuration-edit,procurement-plan-review" },
                new TenantRoleAssignment { TenantId = Tenant, RoleKey = "publisher", RoleName = "Publisher",
                    PermissionsCsv = "procurement-access,procurement-configuration-read,procurement-configuration-approve" });
            await core.SaveChangesAsync();
            return new Fixture { Core = core, Company = company, Branch = branch, Author = author,
                Publisher = publisher, Final = final, FinalRole = finalRole };
        }
        public ValueTask DisposeAsync() => Core.DisposeAsync();
    }
}
