using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record ApprovalRouteLine(Guid LineId, IReadOnlyList<PolicyPreviewStage> Stages);
public sealed record ApprovalRoutePreviewResult(bool HasSingleRoute,
    IReadOnlyList<ApprovalRouteLine> Lines, IReadOnlyList<string> Blockers);

/// <summary>Shows the pinned rule route. This preview cannot assign or approve a case.</summary>
public static class ApprovalRoutePreview
{
    public static ApprovalRoutePreviewResult FromEvaluation(PolicyEvaluation? evaluation,
        IReadOnlyCollection<Guid>? activeLineIds = null)
    {
        if (evaluation is null || evaluation.Outcome != "Matched")
            return new(false, [], ["BuyingRulesNotMatched"]);
        try
        {
            using var json = JsonDocument.Parse(evaluation.DecisionJson);
            var root = json.RootElement;
            if (root.GetProperty("requestId").GetGuid() != evaluation.PurchaseRequestId
                || root.GetProperty("requestVersion").GetInt64() != evaluation.RequestVersion
                || root.GetProperty("policyId").GetGuid() != evaluation.PolicyVersionId)
                return new(false, [], ["PinnedDecisionMismatch"]);
            var lines = new List<ApprovalRouteLine>();
            var seen = new HashSet<Guid>();
            foreach (var line in root.GetProperty("lines").EnumerateArray())
            {
                var lineId = line.GetProperty("lineId").GetGuid();
                if (lineId == Guid.Empty || !seen.Add(lineId))
                    return new(false, [], ["PinnedDecisionLinesInvalid"]);
                var decision = line.GetProperty("decision");
                if (decision.GetProperty("status").GetString() != "Match")
                    return new(false, [], ["BuyingRulesNotMatched"]);
                var stages = decision.GetProperty("approvalStages").EnumerateArray()
                    .Select(stage => new PolicyPreviewStage(
                        stage.GetProperty("code").GetString() ?? "",
                        stage.GetProperty("assignee").GetString() ?? "",
                        stage.GetProperty("mode").GetString() ?? ""))
                    .ToArray();
                if (stages.Length == 0 || stages.Any(stage =>
                    string.IsNullOrWhiteSpace(stage.Code) || string.IsNullOrWhiteSpace(stage.Assignee)
                    || string.IsNullOrWhiteSpace(stage.Mode)))
                    return new(false, [], ["ApprovalStageMissing"]);
                lines.Add(new(lineId, stages));
            }
            if (lines.Count == 0) return new(false, [], ["NoActiveLines"]);
            if (activeLineIds is not null && (activeLineIds.Count != seen.Count
                || activeLineIds.Any(lineId => !seen.Contains(lineId))))
                return new(false, [], ["PinnedDecisionLinesChanged"]);
            var first = JsonSerializer.Serialize(lines[0].Stages);
            var mixed = lines.Skip(1).Any(line => JsonSerializer.Serialize(line.Stages) != first);
            return new(!mixed, lines, mixed ? ["MixedLineApprovalRoutes"] : []);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException
            or FormatException)
        { return new(false, [], ["PinnedDecisionInvalid"]); }
    }
}
