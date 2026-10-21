namespace Mightyfin.Erp.Core.Approvals.Domain;

public sealed record ApprovalActor(string SubjectId, string TenantId, Guid LegalEntityId,
    IReadOnlySet<string> Permissions);

public sealed record ApprovalActionResult(bool Recorded, bool CaseCompleted, string State);

/// <summary>
/// Reusable task mechanics only. The source module must validate its current record,
/// policy, budget and authority limits in the same transaction before calling Decide.
/// </summary>
public static class ApprovalEngine
{
    public static void ValidateNewCase(ApprovalCase approvalCase)
    {
        if (string.IsNullOrWhiteSpace(approvalCase.TenantId) || approvalCase.LegalEntityId == Guid.Empty ||
            string.IsNullOrWhiteSpace(approvalCase.MakerSubjectId) || approvalCase.SourceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(approvalCase.SourceModule) || string.IsNullOrWhiteSpace(approvalCase.SourceType) ||
            approvalCase.SourceVersion < 1 || approvalCase.PolicyVersionId == Guid.Empty ||
            approvalCase.PolicyVersionNumber < 1 || approvalCase.Stages.Count == 0)
            throw new ArgumentException("Approval source, scope, policy and stages are required.");
        var ordered = approvalCase.Stages.OrderBy(x => x.Sequence).ToList();
        if (ordered.Select(x => x.Sequence).Distinct().Count() != ordered.Count ||
            ordered[0].State != "Active" || ordered.Skip(1).Any(x => x.State != "Waiting"))
            throw new ArgumentException("Stages must have a single active first stage and unique order.");
        foreach (var stage in ordered)
        {
            if (stage.CaseId != approvalCase.Id || stage.TenantId != approvalCase.TenantId ||
                stage.LegalEntityId != approvalCase.LegalEntityId || stage.Assignments.Count == 0 ||
                stage.RequiredApprovals < 1 || stage.RequiredApprovals > stage.Assignments.Count ||
                stage.Mode is not ("Any" or "All" or "Quorum") ||
                stage.Mode == "Any" && stage.RequiredApprovals != 1 ||
                stage.Mode == "All" && stage.RequiredApprovals != stage.Assignments.Count ||
                stage.Assignments.Select(x => x.AssigneeSubjectId).Distinct().Count() != stage.Assignments.Count ||
                stage.Assignments.Any(x => x.CaseId != approvalCase.Id || x.StageId != stage.Id ||
                    x.TenantId != approvalCase.TenantId || x.LegalEntityId != approvalCase.LegalEntityId ||
                    x.AssigneeSubjectId == approvalCase.MakerSubjectId || string.IsNullOrWhiteSpace(x.PermissionKey)))
                throw new ArgumentException("An approval stage has invalid scope, quorum or assignments.");
        }
    }

    // Empty reasons have one representation; human whitespace differences do not change a decision.
    private static string? NormalizeReason(string? value)
    {
        var normalized = string.Join(" ", (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length == 0 ? null : normalized;
    }

    public static ApprovalActionResult Decide(ApprovalCase approvalCase, ApprovalAssignment assignment,
        ApprovalActor actor, string action, string idempotencyKey, string? reason,
        ICollection<ApprovalDecision> decisions, DateTimeOffset now)
    {
        idempotencyKey = idempotencyKey?.Trim() ?? string.Empty;
        reason = NormalizeReason(reason);
        if (idempotencyKey.Length is < 1 or > 150)
            throw new ArgumentException("A decision idempotency key of at most 150 characters is required.", nameof(idempotencyKey));
        if (reason?.Length > 2000)
            throw new ArgumentException("A decision reason cannot exceed 2000 characters.", nameof(reason));
        if (approvalCase.TenantId != actor.TenantId || approvalCase.LegalEntityId != actor.LegalEntityId ||
            assignment.TenantId != actor.TenantId || assignment.LegalEntityId != actor.LegalEntityId ||
            assignment.CaseId != approvalCase.Id)
            throw new UnauthorizedAccessException("Approval scope does not match the actor.");
        if (approvalCase.MakerSubjectId == actor.SubjectId)
            throw new UnauthorizedAccessException("A maker cannot approve their own record.");
        if (assignment.AssigneeSubjectId != actor.SubjectId || !actor.Permissions.Contains(assignment.PermissionKey))
            throw new UnauthorizedAccessException("The actor is not assigned and authorized for this task.");
        var previous = decisions.SingleOrDefault(x => x.CaseId == approvalCase.Id && x.IdempotencyKey == idempotencyKey);
        if (previous is not null)
        {
            if (previous.ActorSubjectId != actor.SubjectId || previous.AssignmentId != assignment.Id || previous.Action != action
                || NormalizeReason(previous.Reason) != reason)
                throw new InvalidOperationException("The decision key was already used for another action.");
            return new ApprovalActionResult(false, approvalCase.State == "Approved", approvalCase.State);
        }
        if (approvalCase.State != "Pending" || assignment.State != "Pending")
            throw new InvalidOperationException("This task is no longer pending.");
        if (action is not ("Approve" or "Reject" or "Return"))
            throw new ArgumentException("Unsupported approval decision.", nameof(action));
        if (action != "Approve" && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A rejection or return requires a reason.", nameof(reason));

        var stage = approvalCase.Stages.Single(x => x.Id == assignment.StageId);
        if (stage.State != "Active" || stage.Assignments.All(x => x.Id != assignment.Id))
            throw new InvalidOperationException("The assignment is not in the active stage.");
        var activeSequence = approvalCase.Stages.Where(x => x.State == "Active").Min(x => x.Sequence);
        if (stage.Sequence != activeSequence)
            throw new InvalidOperationException("An earlier stage must finish first.");

        decisions.Add(new ApprovalDecision
        {
            TenantId = actor.TenantId, LegalEntityId = actor.LegalEntityId,
            CaseId = approvalCase.Id, StageId = stage.Id, AssignmentId = assignment.Id,
            ActorSubjectId = actor.SubjectId, Action = action, Reason = reason,
            IdempotencyKey = idempotencyKey, DecidedAt = now,
        });
        assignment.State = action switch { "Approve" => "Approved", "Reject" => "Rejected", _ => "Returned" };
        if (action != "Approve")
        {
            stage.State = assignment.State;
            approvalCase.State = assignment.State;
            approvalCase.CompletedAt = now;
            return new ApprovalActionResult(true, false, approvalCase.State);
        }

        var approvedCount = stage.Assignments.Count(x => x.State == "Approved");
        if (approvedCount < stage.RequiredApprovals)
            return new ApprovalActionResult(true, false, approvalCase.State);

        stage.State = "Approved";
        foreach (var outstanding in stage.Assignments.Where(x => x.State == "Pending"))
            outstanding.State = "Superseded";
        var next = approvalCase.Stages.Where(x => x.State == "Waiting").OrderBy(x => x.Sequence).FirstOrDefault();
        if (next is not null)
        {
            next.State = "Active";
            return new ApprovalActionResult(true, false, approvalCase.State);
        }
        approvalCase.State = "Approved";
        approvalCase.CompletedAt = now;
        approvalCase.CompletionKey = idempotencyKey;
        return new ApprovalActionResult(true, true, approvalCase.State);
    }
}
