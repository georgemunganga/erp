using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Team = Mightyfin.Erp.Hrm.Tests.CoreCompanyTeamCommandsTests;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreErpSessionScopeTests
{
    private const string Client = "391bdeaa-87b3-44b3-8e64-2110dcc1a564";
    private static OperationalSubjectObservation Person(Team.Fixture f, string subject = "42", string client = Client) =>
        new(subject, client, f.Clock.GetUtcNow().AddSeconds(30), f.Clock, f.Clock.GetTimestamp(), TimeSpan.FromSeconds(30));
    private static CommercialLicenseLease License(Team.Fixture f, string[]? modules = null) =>
        new("01arz3ndektsv4rrffq69g5fav", modules ?? ["hrm"], 2, f.Clock.GetUtcNow().AddMinutes(1), f.Clock, f.Clock.GetTimestamp(), TimeSpan.FromMinutes(1));
    private sealed record Ready(Guid A, Guid B, Guid Foreign, Guid OtherCompany, ModuleAccessCommand Command, ModuleAccessResult Grant);
    private static async Task<Ready> Prepare(Team.Fixture f, bool companyGrant = true)
    {
        // A licensed tenant cannot create another legal entity directly. Use
        // the actual purchased-slot provisioner for the separate company.
        var other = await f.Create("second_session_scope_company_request_000000001");
        Assert.Equal(WorkspaceCreateStatus.Created, other.Status);
        await using var db = f.Context();
        var otherBinding = await db.WorkspaceBindings.IgnoreQueryFilters().SingleAsync(x => x.LegalEntityId == other.CompanyId);
        var a = new WorkLocation { Code = "A", Name = "A", LegalEntityId = f.Company };
        var b = new WorkLocation { Code = "B", Name = "B", LegalEntityId = f.Company };
        var foreign = new WorkLocation { Code = "FOREIGN", Name = "Other company branch", LegalEntityId = other.CompanyId!.Value };
        db.WorkLocations.AddRange(a, b); await db.SaveChangesAsync();
        await using (var foreignDb = f.Context(otherBinding.TenantId)) { foreignDb.WorkLocations.Add(foreign); await foreignDb.SaveChangesAsync(); }
        var command = new ModuleAccessCommand(f.Company, f.Owner.Member, 1, "hrm", 0, Guid.NewGuid(), false,
            ["hr_ops"], ["hrm-access"], companyGrant ? "company" : "locations", companyGrant ? [] : [a.Id, b.Id], companyGrant, true);
        var grant = await new CoreModuleAccessCommands(t => f.Context(t), f.Clock).ExecuteAsync(Person(f), f.Owner.Proof, command, License(f));
        Assert.Equal(ModuleAccessStatus.Changed, grant.Status);
        return new(a.Id, b.Id, foreign.Id, other.CompanyId!.Value, command, grant);
    }
    private static CoreErpSessionSelection Selection(Team.Fixture f, Ready r) =>
        new(f.Company, f.Owner.Link, "hrm", r.Grant.VersionId!.Value, r.Grant.Revision, false, [r.A], false);
    private static CoreErpScopeRequest Request(Team.Fixture f, Ready r) => new(f.Company, "hrm", "hrm-access", false, [r.A]);
    private static async Task<CoreErpSessionSelectionResult> Select(Team.Fixture f, CoreErpSessionSelection s)
    {
        await using var db = f.Context("untrusted-session-default");
        return await new CoreErpSessionScopeAuthority(db, f.Clock).SelectAsync(Person(f), License(f), s);
    }
    private static async Task<CoreErpScopeResult> Authorize(Team.Fixture f, CoreErpSessionBinding b, CoreErpScopeRequest r,
        OperationalSubjectObservation? person = null, CommercialLicenseLease? license = null, CancellationToken ct = default)
    {
        await using var db = f.Context("untrusted-session-default");
        return await new CoreErpSessionScopeAuthority(db, f.Clock).AuthorizeAsync(b, person ?? Person(f), license ?? License(f), r, ct);
    }
    [Fact]
    public async Task Company_grant_can_be_narrowed_but_a_request_cannot_widen_the_selected_branch()
    {
        await using var f = await Team.Fixture.Start(); var r = await Prepare(f); var selected = Selection(f, r);
        var result = await Select(f, selected); var binding = Assert.IsType<CoreErpSessionBinding>(result.Binding);
        selected.LocationIds[0] = r.B; Assert.Equal(r.A, Assert.Single(binding.LocationIds));
        var allowed = await Authorize(f, binding, Request(f, r)); Assert.Equal(ModuleAccessStatus.Authorized, allowed.Status);
        var scope = Assert.IsType<CoreErpRequestScope>(allowed.Scope);
        Assert.Equal(f.Tenant, scope.TenantId); Assert.Equal(f.Company, scope.CompanyId); Assert.Equal(f.Owner.User, scope.PrincipalId);
        Assert.Equal(r.A, Assert.Single(scope.LocationIds)); Assert.False(scope.AllCompanyLocations);
        Assert.True(scope.AllowsAction("hrm-access")); Assert.False(scope.AllowsAction("hrm-admin")); Assert.DoesNotContain("erp_admin", scope.Roles);
        Assert.Equal(ModuleAccessStatus.Denied, (await Authorize(f, binding, Request(f, r) with { LocationIds = [r.B] })).Status);
        Assert.Equal(ModuleAccessStatus.Denied, (await Authorize(f, binding, Request(f, r) with { AllCompanyLocations = true, LocationIds = [] })).Status);
        Assert.Equal(ModuleAccessStatus.Denied, (await Authorize(f, binding, Request(f, r) with { LocationIds = [] })).Status);
        f.Clock.Advance(); Assert.False(scope.IsCurrent); Assert.False(scope.AllowsAction("hrm-access"));
        await using var db = f.Context(); Assert.Empty(await db.LocalSessions.ToArrayAsync()); Assert.Empty(await db.TenantRoleAssignments.ToArrayAsync());
        Assert.Single(await db.ModuleAccessVersions.ToArrayAsync()); Assert.Single(await db.ModuleAccessReceipts.ToArrayAsync());
    }
    [Fact]
    public async Task Company_wide_selection_needs_confirmation_and_an_actual_company_wide_grant()
    {
        await using var f = await Team.Fixture.Start(); var r = await Prepare(f, false);
        var s = Selection(f, r) with { AllCompanyLocations = true, LocationIds = [] };
        Assert.Equal(ModuleAccessStatus.Invalid, (await Select(f, s)).Status);
        Assert.Equal(ModuleAccessStatus.Denied, (await Select(f, s with { ConfirmCompanyWide = true })).Status);
    }
    [Fact]
    public async Task Company_wide_binding_still_requires_exact_company_module_action_and_owned_branch()
    {
        await using var f = await Team.Fixture.Start(); var r = await Prepare(f);
        var s = Selection(f, r) with { AllCompanyLocations = true, LocationIds = [], ConfirmCompanyWide = true };
        var b = Assert.IsType<CoreErpSessionBinding>((await Select(f, s)).Binding);
        Assert.Equal(ModuleAccessStatus.Authorized, (await Authorize(f, b, Request(f, r))).Status);
        var wide = await Authorize(f, b, Request(f, r) with { AllCompanyLocations = true, LocationIds = [] });
        Assert.True(Assert.IsType<CoreErpRequestScope>(wide.Scope).AllCompanyLocations);
        foreach (var denied in new[] { Request(f, r) with { LocationIds = [r.Foreign] }, Request(f, r) with { CompanyId = r.OtherCompany },
            Request(f, r) with { Module = "procurement" }, Request(f, r) with { Action = "hrm-admin" }, Request(f, r) with { Action = "unknown" } })
            Assert.Equal(ModuleAccessStatus.Denied, (await Authorize(f, b, denied)).Status);
    }
    [Theory]
    [InlineData("empty")][InlineData("duplicate")][InlineData("zero")][InlineData("too-many")][InlineData("module")]
    [InlineData("confirmation")][InlineData("missing-version")][InlineData("stale-version")][InlineData("stale-revision")][InlineData("foreign-branch")]
    public async Task Selection_rejects_malformed_or_stale_scope_without_inferred_access(string fault)
    {
        await using var f = await Team.Fixture.Start(); var r = await Prepare(f); var s = Selection(f, r);
        s = fault switch {
            "empty" => s with { LocationIds = [] }, "duplicate" => s with { LocationIds = [r.A, r.A] },
            "zero" => s with { LocationIds = [Guid.Empty] }, "too-many" => s with { LocationIds = Enumerable.Range(0,129).Select(_ => Guid.NewGuid()).ToArray() },
            "module" => s with { Module = "payroll" }, "confirmation" => s with { ConfirmCompanyWide = true },
            "missing-version" => s with { ExpectedGrantVersionId = Guid.Empty }, "stale-version" => s with { ExpectedGrantVersionId = Guid.NewGuid() },
            "stale-revision" => s with { ExpectedGrantRevision = 2 }, _ => s with { LocationIds = [r.Foreign] }
        };
        var expected = fault.StartsWith("stale-", StringComparison.Ordinal) ? ModuleAccessStatus.Conflict : fault == "foreign-branch" ? ModuleAccessStatus.Denied : ModuleAccessStatus.Invalid;
        var result = await Select(f, s); Assert.Equal(expected, result.Status); Assert.Null(result.Binding);
    }
    [Theory]
    [InlineData("person")][InlineData("client")][InlineData("tenant")][InlineData("principal")][InlineData("link")][InlineData("company")]
    [InlineData("module")][InlineData("version")][InlineData("revision")][InlineData("policy")][InlineData("foreign-branch")][InlineData("empty-branch")]
    public async Task Corrupt_or_retargeted_binding_cannot_authorize_a_request(string fault)
    {
        await using var f = await Team.Fixture.Start(); var r = await Prepare(f);
        var b = Assert.IsType<CoreErpSessionBinding>((await Select(f, Selection(f, r))).Binding);
        b = fault switch {
            "person" => b with { Subject = "43" }, "client" => b with { ClientId = Guid.NewGuid().ToString("D") },
            "tenant" => b with { TenantId = "foreign-tenant" }, "principal" => b with { PrincipalId = Guid.NewGuid() },
            "link" => b with { LinkId = Guid.NewGuid() }, "company" => b with { CompanyId = r.OtherCompany },
            "module" => b with { Module = "procurement" }, "version" => b with { GrantVersionId = Guid.NewGuid() },
            "revision" => b with { GrantRevision = 2 }, "policy" => b with { PolicyVersion = 2 },
            "foreign-branch" => b with { LocationIds = Array.AsReadOnly(new[] { r.Foreign }) }, _ => b with { LocationIds = Array.AsReadOnly(Array.Empty<Guid>()) }
        };
        var result = await Authorize(f, b, Request(f, r)); Assert.Equal(ModuleAccessStatus.Denied, result.Status); Assert.Null(result.Scope);
    }
    [Theory]
    [InlineData("revoke")][InlineData("widen")][InlineData("membership")][InlineData("archive")][InlineData("move")][InlineData("license")][InlineData("expired-proof")]
    public async Task Current_change_invalidates_existing_binding_without_falling_back_or_expanding(string change)
    {
        await using var f = await Team.Fixture.Start(); var r = await Prepare(f);
        var b = Assert.IsType<CoreErpSessionBinding>((await Select(f, Selection(f, r))).Binding);
        var person = Person(f); var license = License(f);
        if (change is "revoke" or "widen") {
            var command = r.Command with { ExpectedRevision = 1, CommandKey = Guid.NewGuid() };
            if (change == "revoke") command = command with { Revoke = true, RoleKeys = [], Actions = [], ScopeKind = "none", AllCompanyLocationsConfirmed = false };
            else command = command with { RoleKeys = ["hr_admin"], Actions = ["hrm-access", "hrm-admin"] };
            Assert.Equal(ModuleAccessStatus.Changed, (await new CoreModuleAccessCommands(t => f.Context(t), f.Clock).ExecuteAsync(Person(f), f.Owner.Proof, command, License(f))).Status);
        }
        else if (change is "membership" or "archive" or "move") {
            await using var db = f.Context();
            if (change == "membership") (await db.CompanyMemberships.SingleAsync(x => x.Id == f.Owner.Member)).Revision++;
            else { var branch = await db.WorkLocations.SingleAsync(x => x.Id == r.A); if (change == "archive") branch.IsArchived = true; else branch.LegalEntityId = r.OtherCompany; }
            await db.SaveChangesAsync();
        }
        else if (change == "license") license = License(f, []);
        else f.Clock.Advance();
        Assert.Equal(ModuleAccessStatus.Denied, (await Authorize(f, b, Request(f, r), person, license)).Status);
    }
    private sealed class MutateDuringRead(Action mutate, string? table = null, int trigger = 1) : DbCommandInterceptor
    {
        private int reads;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if ((table is null || command.CommandText.Contains(table, StringComparison.Ordinal)) && ++reads == trigger) mutate();
            return base.ReaderExecutingAsync(command, data, result, ct);
        }
    }
    private static HrmDbContext Intercept(Team.Fixture f, Action mutate, string? table = null, int trigger = 1)
    {
        using var original = f.Context();
        var options = Assert.IsType<DbContextOptions<HrmDbContext>>(original.GetService<IDbContextOptions>());
        return new(new DbContextOptionsBuilder<HrmDbContext>(options).AddInterceptors(new MutateDuringRead(mutate, table, trigger)).Options,
            new FixedTenantAccessor("untrusted-intercepted-default"));
    }
    [Fact]
    public async Task Selection_copies_branch_input_before_the_first_database_await()
    {
        await using var f = await Team.Fixture.Start(); var r = await Prepare(f); var selection = Selection(f, r);
        await using var db = Intercept(f, () => selection.LocationIds[0] = r.B);
        var selected = await new CoreErpSessionScopeAuthority(db, f.Clock).SelectAsync(Person(f), License(f), selection);
        Assert.Equal(r.B, selection.LocationIds[0]); Assert.Equal(r.A, Assert.Single(Assert.IsType<CoreErpSessionBinding>(selected.Binding).LocationIds));
    }
    [Fact]
    public async Task Request_copies_branch_input_before_the_first_database_await()
    {
        await using var f = await Team.Fixture.Start(); var r = await Prepare(f);
        var binding = Assert.IsType<CoreErpSessionBinding>((await Select(f, Selection(f, r) with { LocationIds = [r.A, r.B] })).Binding);
        var request = Request(f, r); await using var db = Intercept(f, () => request.LocationIds[0] = r.B);
        var result = await new CoreErpSessionScopeAuthority(db, f.Clock).AuthorizeAsync(binding, Person(f), License(f), request);
        Assert.Equal(r.B, request.LocationIds[0]); Assert.Equal(r.A, Assert.Single(Assert.IsType<CoreErpRequestScope>(result.Scope).LocationIds));
    }
    [Fact]
    public async Task Selected_branch_move_during_final_parent_grant_read_cannot_hide_under_a_company_wide_grant()
    {
        await using var f = await Team.Fixture.Start(); var r = await Prepare(f);
        var binding = Assert.IsType<CoreErpSessionBinding>((await Select(f, Selection(f, r))).Binding);
        var moved = false;
        await using var db = Intercept(f, () => {
            using var change = f.Context(); var branch = change.WorkLocations.Single(x => x.Id == r.A);
            branch.LegalEntityId = r.OtherCompany; change.SaveChanges(); moved = true;
        }, "core_module_access_versions", 3);
        var result = await new CoreErpSessionScopeAuthority(db, f.Clock).AuthorizeAsync(binding, Person(f), License(f), Request(f, r));
        Assert.True(moved); Assert.Equal(ModuleAccessStatus.Denied, result.Status); Assert.Null(result.Scope);
    }
    [Fact]
    public async Task Database_unavailability_never_uses_a_previously_authorized_scope()
    {
        await using var f = await Team.Fixture.Start(); var r = await Prepare(f);
        var b = Assert.IsType<CoreErpSessionBinding>((await Select(f, Selection(f, r))).Binding);
        Assert.Equal(ModuleAccessStatus.Authorized, (await Authorize(f, b, Request(f, r))).Status);
        await using (var db = f.Context()) {
            var table = db.Database.IsNpgsql() ? "hrm.core_module_access_versions" : "core_module_access_versions";
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE " + table + " RENAME TO fixture_unavailable_module_versions");
        }
        var denied = await Authorize(f, b, Request(f, r)); Assert.Equal(ModuleAccessStatus.Unavailable, denied.Status); Assert.Null(denied.Scope);
    }
    [Fact]
    public async Task Cancellation_propagates_and_never_yields_a_scope()
    {
        await using var f = await Team.Fixture.Start(); var r = await Prepare(f);
        var b = Assert.IsType<CoreErpSessionBinding>((await Select(f, Selection(f, r))).Binding);
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Authorize(f, b, Request(f, r), ct: stop.Token));
    }
}
