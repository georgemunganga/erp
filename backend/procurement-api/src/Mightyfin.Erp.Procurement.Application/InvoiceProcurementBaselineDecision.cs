using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>One Core decision over an already captured owner baseline.
/// Approval of this case is still only a prerequisite for the historical
/// owner producer; this transition issues no billing capacity.</summary>
public static class InvoiceProcurementBaselineDecision
{
    public sealed record Command(Guid Key,long ExpectedCaseVersion,
        long ExpectedAssignmentVersion,string Action,string? Reason,DateTimeOffset At);

    public static ApprovalActionResult Apply(InvoiceProcurementOwnerBaseline baseline,
        ApprovalCase workflow,ApprovalAssignment assignment,
        IReadOnlyDictionary<string,Guid?> reviewerWorkerAliases,
        InvoiceProcurementBaselineReviewPolicyRules.Person reviewer,
        PolicyVersion? currentPolicy,Command command,ICollection<ApprovalDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(reviewer);
        ArgumentNullException.ThrowIfNull(decisions);
        InvoiceProcurementBaselineEvidence.VerifyPendingCore(baseline,workflow,reviewerWorkerAliases);
        if(command.Key==Guid.Empty || command.At==default || command.At.Offset!=TimeSpan.Zero
            || command.At<baseline.CapturedAtUtc || command.ExpectedCaseVersion!=workflow.Version
            || command.ExpectedAssignmentVersion!=assignment.Version
            || command.Action is not ("Approve" or "Return" or "Reject")
            || command.Action!="Approve" && string.IsNullOrWhiteSpace(command.Reason)
            || command.Reason is {Length:>1000}
            || assignment.CaseId!=workflow.Id || assignment.AssigneeSubjectId!=reviewer.PrincipalId
            || assignment.PermissionKey!=InvoiceProcurementBaselineEvidence.ReviewPermission
            || reviewer.TenantId!=baseline.TenantId || reviewer.LegalEntityId!=baseline.LegalEntityId
            || !reviewer.Active || !reviewer.OrganisationWide
            || reviewer.Permissions is null
            || !reviewer.Permissions.Contains("procurement-access")
            || !reviewer.Permissions.Contains(InvoiceProcurementBaselineEvidence.ReviewPermission)
            || !ProcurementPrincipalEvidence.Independent(baseline.MakerPrincipalId,
                baseline.MakerWorkerAliasId,reviewer.PrincipalId,reviewer.WorkerAliasId))
            throw new InvalidOperationException("InvoiceOwnerCoreDecisionInvalid");
        if(command.Action=="Approve")
        {
            if(currentPolicy is null || currentPolicy.Id!=workflow.PolicyVersionId
                || currentPolicy.PolicyVersionNumber!=workflow.PolicyVersionNumber)
                throw new InvalidOperationException("InvoiceOwnerCurrentReviewPolicyRequired");
            _=InvoiceProcurementBaselineReviewPolicyRules.Resolve(currentPolicy,
                baseline.TenantId,baseline.LegalEntityId,baseline.MakerPrincipalId,
                baseline.MakerWorkerAliasId,reviewer,command.At);
        }
        var at=ProcurementEvidenceEncoding.DatabaseTimestamp(command.At)
            ??throw new InvalidOperationException("InvoiceOwnerCoreDecisionInvalid");
        return ApprovalEngine.Decide(workflow,assignment,
            new(reviewer.PrincipalId,reviewer.TenantId,reviewer.LegalEntityId,reviewer.Permissions),
            command.Action,command.Key.ToString("D"),command.Reason?.Trim(),decisions,at);
    }
}
