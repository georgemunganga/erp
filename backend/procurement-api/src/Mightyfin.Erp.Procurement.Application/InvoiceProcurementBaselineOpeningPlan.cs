using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Builds the exact single-decision Core source before the shared
/// Procurement/Core transaction stages the immutable inventory capture.</summary>
public static class InvoiceProcurementBaselineOpeningPlan
{
    public static ApprovalCase Prepare(Guid baselineId, Guid caseId, PolicyVersion policy,
        string tenantId, Guid companyId, string makerPrincipalId, Guid? makerWorkerAliasId,
        InvoiceProcurementBaselineReviewPolicyRules.Person reviewer, DateTimeOffset at)
    {
        if(baselineId==Guid.Empty || caseId==Guid.Empty || baselineId==caseId
            || at==default || at.Offset!=TimeSpan.Zero)
            throw new InvalidOperationException("InvoiceOwnerCoreOpeningInvalid");
        reviewer=InvoiceProcurementBaselineReviewPolicyRules.Resolve(policy,tenantId,companyId,
            makerPrincipalId,makerWorkerAliasId,reviewer,at);
        var capturedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(at)
            ??throw new InvalidOperationException("InvoiceOwnerCoreOpeningInvalid");
        var workflow=new ApprovalCase
        {
            Id=caseId,TenantId=tenantId,LegalEntityId=companyId,
            SourceModule="procurement",SourceType=InvoiceProcurementBaselineEvidence.SourceType,
            SourceId=baselineId,SourceVersion=1,PolicyVersionId=policy.Id,
            PolicyVersionNumber=policy.PolicyVersionNumber,MakerSubjectId=makerPrincipalId,
            OpenedAt=capturedAt.Value
        };
        var stage=new ApprovalStage
        {
            TenantId=tenantId,LegalEntityId=companyId,CaseId=workflow.Id,
            Sequence=1,Code="InvoiceOwnerBaselineReview",Mode="Any",RequiredApprovals=1,State="Active"
        };
        stage.Assignments.Add(new ApprovalAssignment
        {
            TenantId=tenantId,LegalEntityId=companyId,CaseId=workflow.Id,
            StageId=stage.Id,AssigneeSubjectId=reviewer.PrincipalId,
            PermissionKey=InvoiceProcurementBaselineEvidence.ReviewPermission
        });
        workflow.Stages.Add(stage);
        ApprovalEngine.ValidateNewCase(workflow);
        InvoiceProcurementBaselineEvidence.VerifyPendingCore(
            new InvoiceProcurementOwnerBaseline
            {
                Id=baselineId,TenantId=tenantId,LegalEntityId=companyId,
                CoreCaseId=caseId,MakerPrincipalId=makerPrincipalId,
                MakerWorkerAliasId=makerWorkerAliasId,CapturedAtUtc=capturedAt.Value
            },workflow,new Dictionary<string,Guid?> { [reviewer.PrincipalId]=reviewer.WorkerAliasId });
        return workflow;
    }
}
