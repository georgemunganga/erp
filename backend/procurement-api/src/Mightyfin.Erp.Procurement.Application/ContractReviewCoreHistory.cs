using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Strict retained Core decision precondition for another contract vote.
/// A mutable Case.State or Stage.State alone is never evidence of prior quorum.</summary>
public static class ContractReviewCoreHistory
{
    public static void VerifyPending(ContractReviewSubmission source, ApprovalCase approval,
        IReadOnlyList<ContractReviewAssignmentPins.Pin> pins, IReadOnlyCollection<ApprovalDecision> decisions)
    {
        Need(approval.State == "Pending" && approval.CompletedAt is null && approval.CompletionKey is null
            && approval.OpenedAt == source.SubmittedAt, "ContractReviewCoreHistoryInvalid");
        var stages = approval.Stages.OrderBy(x => x.Sequence).ToArray();
        var assignments = stages.SelectMany(x => x.Assignments).ToArray();
        Need(stages.Length > 0 && stages.Select(x => x.Sequence).SequenceEqual(Enumerable.Range(1, stages.Length))
            && stages.Count(x => x.State == "Active") == 1
            && assignments.Length == pins.Count && decisions.Count <= assignments.Length,
            "ContractReviewCoreHistoryInvalid");
        var active = stages.Single(x => x.State == "Active");
        Need(stages.Where(x => x.Sequence < active.Sequence).All(x => x.State == "Approved")
            && stages.Where(x => x.Sequence > active.Sequence).All(x => x.State == "Waiting"),
            "ContractReviewCoreHistoryInvalid");
        var known = pins.ToDictionary(x => x.AssignmentId);
        var seen = new HashSet<Guid>(); var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var decision in decisions)
        {
            Need(decision.TenantId == source.TenantId && decision.LegalEntityId == source.LegalEntityId
                && decision.CaseId == approval.Id && decision.Action == "Approve"
                && decision.Id != Guid.Empty && decision.DecidedAt >= approval.OpenedAt
                && ProcurementEvidenceEncoding.DatabaseTimestamp(decision.DecidedAt) == decision.DecidedAt
                && known.TryGetValue(decision.AssignmentId, out var pin)
                && pin.PrincipalId == decision.ActorSubjectId
                && assignments.Any(x => x.Id == decision.AssignmentId && x.StageId == decision.StageId)
                && seen.Add(decision.AssignmentId) && keys.Add(decision.IdempotencyKey)
                && Guid.TryParseExact(decision.IdempotencyKey, "D", out var key) && key != Guid.Empty,
                "ContractReviewCoreHistoryInvalid");
        }
        foreach (var stage in stages)
        {
            var stageVotes = decisions.Count(x => x.StageId == stage.Id);
            var required = stage.Mode == "Any" ? 1 : stage.Assignments.Count;
            Need(stage.Mode is "Any" or "All" && stage.RequiredApprovals == required
                && stage.Assignments.All(x => x.State == (seen.Contains(x.Id) ? "Approved"
                    : stage.State == "Approved" ? "Superseded" : "Pending")),
                "ContractReviewCoreHistoryInvalid");
            if (stage.State == "Approved")
                Need(stageVotes == required, "ContractReviewCoreHistoryInvalid");
            else if (stage.State == "Active")
                Need(stageVotes < required, "ContractReviewCoreHistoryInvalid");
            else Need(stageVotes == 0, "ContractReviewCoreHistoryInvalid");
        }
        var last = approval.OpenedAt;
        foreach (var stage in stages)
            foreach (var decision in decisions.Where(x => x.StageId == stage.Id).OrderBy(x => x.DecidedAt))
            {
                Need(decision.DecidedAt >= last, "ContractReviewCoreHistoryInvalid");
                last = decision.DecidedAt;
            }
    }

    private static void Need(bool condition, string code)
    { if (!condition) throw new InvalidOperationException(code); }
}
