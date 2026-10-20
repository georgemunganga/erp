namespace Mightyfin.Erp.Procurement.Application;

public sealed record ApprovalAssignee(string SubjectId, string DisplayName);
public sealed record ApprovalStageAssignees(string Code, string AssigneeKind, string Mode,
    IReadOnlyList<ApprovalAssignee> Candidates, string? Blocker);
public sealed record ApprovalAssigneePreviewResult(bool Resolved,
    IReadOnlyList<ApprovalStageAssignees> Stages, IReadOnlyList<string> Blockers);

/// <summary>
/// A non-authoritative assignment preview. Core must revalidate identity, permission,
/// delegation and source version when an approval case is actually opened.
/// </summary>
public static class ApprovalAssigneePreview
{
    public static ApprovalAssigneePreviewResult Resolve(ApprovalRoutePreviewResult route,
        string makerSubjectId, IReadOnlyDictionary<string, IReadOnlyList<ApprovalAssignee>> candidates)
    {
        if (!route.HasSingleRoute || route.Lines.Count == 0)
            return new(false, [], route.Blockers.Count == 0 ? ["ApprovalRouteUnavailable"] : route.Blockers);

        var stages = new List<ApprovalStageAssignees>();
        var blockers = new List<string>();
        foreach (var stage in route.Lines[0].Stages)
        {
            var eligible = candidates.GetValueOrDefault(stage.Assignee) ?? [];
            eligible = eligible.Where(x => !string.IsNullOrWhiteSpace(x.SubjectId)
                    && x.SubjectId != makerSubjectId)
                .GroupBy(x => x.SubjectId, StringComparer.Ordinal)
                .Select(x => x.First()).OrderBy(x => x.DisplayName, StringComparer.Ordinal).ToArray();
            var blocker = eligible.Count == 0 && stage.Assignee == "BudgetOwner"
                ? "BudgetOwnerMappingRequired"
                : eligible.Count == 0 ? "ApproverUnavailable"
                : stage.Mode == "One" && eligible.Count != 1 ? "ApproverChoiceRequired"
                : stage.Mode is not ("One" or "All") ? "ApprovalModeUnsupported" : null;
            if (blocker is not null) blockers.Add(stage.Code + ":" + blocker);
            stages.Add(new(stage.Code, stage.Assignee, stage.Mode, eligible, blocker));
        }
        return new(blockers.Count == 0, stages, blockers);
    }
}
