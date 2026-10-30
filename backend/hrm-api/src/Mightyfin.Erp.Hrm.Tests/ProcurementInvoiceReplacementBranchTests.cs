using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed partial class ProcurementRequestHttpTests
{
    [Fact]
    public async Task Replacement_orgwide_workerless_maker_expands_only_current_active_company_branches()
    {
        await using var fixture = await Fixture.Start();
        var branches = await fixture.SeedAllRequestBranches();
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access,procurement-invoice-read,procurement-invoice-capture");
        await fixture.ArchiveAllRequestBranchAssignments();
        var before = await fixture.OriginalValueFootprint();
        var proof = await fixture.ReplacementBranchProjection(branches.First);
        Assert.True(proof.Person.OrganisationWide);Assert.Null(proof.Person.WorkerId);
        Assert.Empty(proof.Declared);
        Assert.Equal(proof.Active.Order(), proof.Allowed.Order());
        Assert.Contains(branches.First, proof.Allowed);Assert.Contains(branches.Second, proof.Allowed);
        Assert.Equal(before, await fixture.OriginalValueFootprint());
    }

    [Fact]
    public async Task Replacement_confined_workerless_maker_keeps_its_assigned_current_branch_set()
    {
        await using var fixture = await Fixture.Start();
        var branches = await fixture.SeedAllRequestBranches();
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access,procurement-invoice-read,procurement-invoice-capture");
        var proof = await fixture.ReplacementBranchProjection(branches.Second);
        Assert.False(proof.Person.OrganisationWide);Assert.Null(proof.Person.WorkerId);
        Assert.Contains(branches.First, proof.Allowed);Assert.Contains(branches.Second, proof.Allowed);
        Assert.True(proof.Allowed.Length < proof.Active.Length);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.ReplacementBranchProjection(null));
    }

    private sealed partial class Fixture
    {
        public async Task<(ProcurementPrincipalPeople.Person Person, Guid[] Declared, Guid[] Allowed, Guid[] Active)>
            ReplacementBranchProjection(Guid? selectedBranch)
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var selected = selectedBranch ?? await core.WorkLocations.Where(x => x.Code == "ALL-C").Select(x => x.Id).SingleAsync();
            var actor = await ProcurementPrincipalPeople.Resolve(core, "tenant-a", company, Owner.ToString("D"), default)
                ?? throw new InvalidOperationException("Test current Core actor unavailable");
            var scope = new ProcurementRequestScope();
            scope.Set("tenant-a", company, Owner.ToString("D"), selected, null, !actor.OrganisationWide, "replacement-branch-test");
            scope.AssignPrincipal(Owner);
            var declared = await ProcurementExecutionRecordScope.Branches(core, scope, selected, true, default);
            var allowed = await ProcurementInvoiceMatchingRoutes.PrincipalMatchingBranches(core, scope, default);
            var active = await core.WorkLocations.Where(x => x.TenantId == "tenant-a" && x.LegalEntityId == company
                && !x.IsArchived).Select(x => x.Id).ToArrayAsync();
            return (actor, declared, allowed.ToArray(), active);
        }
    }
}
