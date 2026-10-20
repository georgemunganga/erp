using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class ApprovalAssigneePreviewTests
{
    [Fact]
    public void Resolves_unique_manager_but_never_assigns_the_maker()
    {
        var route = new ApprovalRoutePreviewResult(true,
            [new(Guid.NewGuid(), [new("NEED", "LineManager", "One")])], []);
        var eligible = new Dictionary<string, IReadOnlyList<ApprovalAssignee>>
        {
            ["LineManager"] = [new("maker", "Requester"), new("manager", "Manager")],
        };
        var result = ApprovalAssigneePreview.Resolve(route, "maker", eligible);
        Assert.True(result.Resolved);
        Assert.Equal("manager", Assert.Single(result.Stages[0].Candidates).SubjectId);
        Assert.Empty(result.Blockers);
    }

    [Fact]
    public void Reports_missing_and_ambiguous_people_without_opening_a_case()
    {
        var route = new ApprovalRoutePreviewResult(true,
            [new(Guid.NewGuid(), [new("BUDGET", "BudgetOwner", "One"),
                new("PROC", "ProcurementManager", "One")])], []);
        var eligible = new Dictionary<string, IReadOnlyList<ApprovalAssignee>>
        {
            ["BudgetOwner"] = [new("maker", "Requester")],
            ["ProcurementManager"] = [new("a", "A"), new("b", "B")],
        };
        var result = ApprovalAssigneePreview.Resolve(route, "maker", eligible);
        Assert.False(result.Resolved);
        Assert.Contains("BUDGET:BudgetOwnerMappingRequired", result.Blockers);
        Assert.Contains("PROC:ApproverChoiceRequired", result.Blockers);
    }

    [Fact]
    public void Does_not_resolve_mixed_line_routes()
    {
        var route = new ApprovalRoutePreviewResult(false, [], ["MixedLineApprovalRoutes"]);
        var result = ApprovalAssigneePreview.Resolve(route, "maker",
            new Dictionary<string, IReadOnlyList<ApprovalAssignee>>());
        Assert.False(result.Resolved);
        Assert.Contains("MixedLineApprovalRoutes", result.Blockers);
    }

    [Fact]
    public void All_mode_retains_each_distinct_eligible_reviewer()
    {
        var route = new ApprovalRoutePreviewResult(true,
            [new(Guid.NewGuid(), [new("PROC", "ProcurementManager", "All")])], []);
        var eligible = new Dictionary<string, IReadOnlyList<ApprovalAssignee>>
        {
            ["ProcurementManager"] = [new("a", "A"), new("b", "B"), new("a", "A duplicate")],
        };
        var result = ApprovalAssigneePreview.Resolve(route, "maker", eligible);
        Assert.True(result.Resolved);
        Assert.Equal(2, result.Stages[0].Candidates.Count);
    }
}
