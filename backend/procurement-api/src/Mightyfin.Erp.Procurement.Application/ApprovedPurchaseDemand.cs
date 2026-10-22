using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Approved PR/Finance business evidence only. It grants no route or action authority.
/// Caller still resolves current canonical actors/permissions, current Finance and locks sources.</summary>
public static class ApprovedPurchaseDemand
{
    public static void Verify(ApprovedPurchaseSource source, string tenant, Guid company, DateTimeOffset at)
    {
        var request = source.Request; var line = source.Line;
        if (request.TenantId != tenant || request.LegalEntityId != company || line.TenantId != tenant
            || line.LegalEntityId != company || line.PurchaseRequestId != request.Id
            || request.Lines.Count(x => x.Id == line.Id) != 1
            || !ReferenceEquals(request.Lines.Single(x => x.Id == line.Id), line)) Fail("ApprovedDemandScopeMismatch");
        if (request.Status is not ("Approved" or "PartiallyOrdered" or "Ordered")
            || request.ApprovalState != "Approved" || line.ApprovalStatus != "Approved"
            || line.ApprovedQuantity <= 0 || line.ApprovedQuantity > line.RequestedQuantity)
            Fail("ApprovedDemandNotApproved");
        var pin = source.Submission; var outcome = source.Outcome; var core = source.CoreCase;
        var decision = source.FinalDecision;
        if (!PurchaseRequestApprovalPinning.VerifyBusinessEvidence(pin, request, source.Evaluation,
                source.Policy, source.Finance, tenant, company).IsValid
            || outcome.TenantId != tenant || outcome.LegalEntityId != company
            || outcome.PurchaseRequestId != request.Id || outcome.SubmissionId != pin.Id
            || outcome.SubmittedBusinessVersion != pin.SubmittedBusinessVersion
            || outcome.CoreApprovalCaseId != pin.CoreApprovalCaseId || outcome.Outcome != "Approved"
            || core.Id != pin.CoreApprovalCaseId || core.TenantId != tenant || core.LegalEntityId != company
            || core.SourceModule != "procurement" || core.SourceType != "purchase_request"
            || core.SourceId != request.Id || core.SourceVersion != pin.SubmittedBusinessVersion || core.State != "Approved"
            || core.PolicyVersionId != pin.PolicyVersionId || core.PolicyVersionNumber != pin.PolicyVersionNumber
            || core.MakerSubjectId != request.CreatedBy || core.CompletedAt is null || at < core.CompletedAt
            || decision.Id != outcome.CoreDecisionId || decision.CaseId != core.Id || decision.Action != "Approve"
            || decision.TenantId != tenant || decision.LegalEntityId != company
            || decision.ActorSubjectId == core.MakerSubjectId || outcome.DecisionCommandKey != decision.IdempotencyKey
            || core.CompletionKey != decision.IdempotencyKey)
            Fail("ApprovedDemandAuthorityMismatch");
    }
    private static void Fail(string code) => throw new InvalidOperationException(code);
}
