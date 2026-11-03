using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using static Mightyfin.Erp.Hrm.Api.ProcurementSourcingExecutionRoutes;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalQuotationScopeTests
{
    [Theory]
    [InlineData("selected", true)]
    [InlineData("company", false)]
    [InlineData("all", false)]
    [InlineData("wrong-branch", false)]
    [InlineData("department", false)]
    public void New_quotation_write_context_requires_explicit_matching_branch_without_employee(string selection, bool expected)
    {
        var branch = Guid.NewGuid(); var scope = new ProcurementRequestScope(); var actor = Guid.NewGuid();
        scope.Set("tenant-a", Guid.NewGuid(), actor.ToString("D"),
            selection is "company" or "all" ? null : selection == "wrong-branch" ? Guid.NewGuid() : branch,
            selection == "department" ? Guid.NewGuid() : null, false, "quotation-scope", selection == "all");
        scope.AssignPrincipal(actor);
        Assert.Null(scope.WorkerId); Assert.Null(scope.CoreLinkedWorkerId);
        Assert.Equal(expected, QuotationWriteContextAllowed(scope, branch));
        // Context selection is not a permission or branch-assignment grant.
    }

    [Fact]
    public void Raw_All_cannot_become_a_default_branch_write()
    {
        var branch = Guid.NewGuid(); var scope = new ProcurementRequestScope();
        scope.Set("tenant-a", Guid.NewGuid(), Guid.NewGuid().ToString("D"), branch, null, true, "quotation-all-fallback");
        Assert.False(scope.IsExplicitAllBranches);
        Assert.True(QuotationWriteContextAllowed(scope, branch));
        Assert.False(QuotationWriteContextAllowed(scope, branch, rawAllHeader: true));
    }

    [Theory]
    [InlineData("quotation-sources", true)]
    [InlineData("quotation-commands/11111111-1111-1111-1111-111111111111", true)]
    [InlineData("11111111-1111-1111-1111-111111111111/quotation-drafts", true)]
    [InlineData("11111111-1111-1111-1111-111111111111/quotation-drafts/22222222-2222-2222-2222-222222222222", true)]
    [InlineData("11111111-1111-1111-1111-111111111111/quotation-drafts/22222222-2222-2222-2222-222222222222/versions/1", true)]
    [InlineData("11111111-1111-1111-1111-111111111111/quotation-drafts/22222222-2222-2222-2222-222222222222/versions/0", false)]
    [InlineData("quotation-commands/not-an-id", false)]
    [InlineData("quotation-sources/future", false)]
    [InlineData("11111111-1111-1111-1111-111111111111/quotation-drafts/22222222-2222-2222-2222-222222222222/revise", false)]
    public void Company_All_admits_only_named_retained_read_contracts(string suffix, bool expected)
    {
        var path = new Microsoft.AspNetCore.Http.PathString("/api/procurement/v1/sourcing/" + suffix);
        Assert.Equal(expected, QuotationAllReadPath(path, "GET"));
        Assert.False(QuotationAllReadPath(path, "POST"));
        Assert.False(QuotationAllReadPath(path, "DELETE"));
    }

    [Fact]
    public async Task Current_workerless_handler_uses_existing_Core_branch_grants_and_loses_access_after_reassignment()
    {
        await using var core = TestDbContextFactory.Create("tenant-a");
        var company = new LegalEntity { Code = "Q", RegisteredName = "Quote company", Currency = "ZMW" };
        var sourceBranch = new WorkLocation { Code = "A", Name = "Source branch", LegalEntityId = company.Id };
        var otherBranch = new WorkLocation { Code = "B", Name = "Other branch", LegalEntityId = company.Id };
        var handler = new LocalUser { Email = "quotation-handler@example.test", NormalizedEmail = "QUOTATION-HANDLER@EXAMPLE.TEST",
            DisplayName = "Current quotation handler", RolesCsv = "quotation_handler", PasswordHash = "unused-synthetic", IsActive = true };
        core.LegalEntities.Add(company); core.WorkLocations.AddRange(sourceBranch, otherBranch); core.LocalUsers.Add(handler);
        core.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = "quotation_handler", RoleName = "Quotation handler",
            Active = true, PermissionsCsv = "procurement-access,procurement-quotation-read,procurement-quotation-capture" });
        var assignment = new HrUserBranchAssignment { UserId = handler.Id, LocationId = sourceBranch.Id };
        core.UserBranchAssignments.Add(assignment); await core.SaveChangesAsync();
        var scope = new ProcurementRequestScope();
        scope.Set("tenant-a", company.Id, handler.Id.ToString("D"), sourceBranch.Id, null, true, "quotation-scope");
        scope.AssignPrincipal(handler.Id);
        var person = await ProcurementPrincipalPeople.Resolve(core, "tenant-a", company.Id, scope.SubjectId, default);
        Assert.NotNull(person); Assert.Null(person.WorkerId);
        Assert.Contains("procurement-quotation-capture", person.Permissions);
        Assert.Equal(new[] { sourceBranch.Id }, await ProcurementExecutionRecordScope.Branches(core, scope, sourceBranch.Id, true, default));
        assignment.LocationId = otherBranch.Id; await core.SaveChangesAsync();
        Assert.True(QuotationWriteContextAllowed(scope, sourceBranch.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ProcurementExecutionRecordScope.Branches(
            core, scope, sourceBranch.Id, true, default));
    }
}
