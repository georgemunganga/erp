using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class ApprovalRoutePreviewTests
{
    [Fact]
    public void Distinguishes_one_route_from_line_specific_routes_without_granting_authority()
    {
        var requestId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var lineOne = Guid.NewGuid();
        var lineTwo = Guid.NewGuid();
        var evaluation = new PolicyEvaluation
        {
            PurchaseRequestId = requestId, RequestVersion = 3, PolicyVersionId = policyId,
            Outcome = "Matched", DecisionJson = Decision(requestId, policyId, lineOne, lineTwo, "LineManager"),
        };
        var oneRoute = ApprovalRoutePreview.FromEvaluation(evaluation, [lineOne, lineTwo]);
        Assert.True(oneRoute.HasSingleRoute);
        Assert.Empty(oneRoute.Blockers);
        Assert.Equal(2, oneRoute.Lines.Count);
        Assert.Equal("LineManager", oneRoute.Lines[0].Stages[0].Assignee);

        Assert.Contains("PinnedDecisionLinesChanged",
            ApprovalRoutePreview.FromEvaluation(evaluation, [lineOne]).Blockers);
        Assert.Contains("PinnedDecisionLinesChanged",
            ApprovalRoutePreview.FromEvaluation(evaluation, [lineOne, Guid.NewGuid()]).Blockers);
        evaluation.DecisionJson = Decision(requestId, policyId, lineOne, lineOne, "LineManager");
        Assert.Contains("PinnedDecisionLinesInvalid",
            ApprovalRoutePreview.FromEvaluation(evaluation, [lineOne]).Blockers);

        evaluation.DecisionJson = Decision(requestId, policyId, lineOne, lineTwo, "BudgetOwner");
        var mixed = ApprovalRoutePreview.FromEvaluation(evaluation);
        Assert.False(mixed.HasSingleRoute);
        Assert.Contains("MixedLineApprovalRoutes", mixed.Blockers);
        evaluation.DecisionJson = "{}";
        Assert.Contains("PinnedDecisionInvalid", ApprovalRoutePreview.FromEvaluation(evaluation).Blockers);
    }

    private static string Decision(Guid requestId, Guid policyId, Guid lineOne, Guid lineTwo,
        string secondAssignee) => JsonSerializer.Serialize(new
    {
        requestId, requestVersion = 3, policyId,
        lines = new[]
        {
            new { lineId = lineOne, decision = new { status = "Match", approvalStages =
                new[] { new { code = "NEED", assignee = "LineManager", mode = "One" } } } },
            new { lineId = lineTwo, decision = new { status = "Match", approvalStages =
                new[] { new { code = "NEED", assignee = secondAssignee, mode = "One" } } } },
        },
    });
}
