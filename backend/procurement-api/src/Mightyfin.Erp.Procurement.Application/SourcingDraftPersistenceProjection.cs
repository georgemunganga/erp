using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Typed storage projections only; these do not replace RestoreVerified or current access.</summary>
public static class SourcingDraftPersistenceProjection
{
    public static RfqOwnedDraftEvidence Evidence(SourcingDraftEvidence row) => new(row.SchemaVersion,
        row.SourcingEventId, row.TenantId, row.LegalEntityId, row.BranchRef, row.MakerSubjectId,
        row.MakerWorkerId ?? throw new InvalidOperationException("RfqWorkerEvidenceRequired"), row.CreateCommandKey, row.OriginalDraftFingerprint, row.PayloadJson, row.PayloadSha256);
    public static RfqDemandPin Pin(SourcingDemandBinding row) => new(row.PurchaseRequestId,
        row.PurchaseRequestLineId, row.RequestVersion, row.SubmissionId, row.SubmittedVersion,
        row.CoreApprovalCaseId, row.ApprovalOutcomeId, row.CoreDecisionId, row.RequestSha,
        row.PolicyDecisionSha, row.PolicyRulesSha, row.FinanceSnapshotId, row.FinanceSha,
        row.MethodId, row.MethodRevision, row.MethodCode, row.MinimumQuoteCount,
        row.MakerWorkerId ?? throw new InvalidOperationException("RfqWorkerEvidenceRequired"),
        row.RequesterWorkerId ?? throw new InvalidOperationException("RfqWorkerEvidenceRequired"),
        row.AllocationId, row.SourcingLineId, row.Quantity, row.UnitEstimate,
        row.EstimatedValue, row.UnitCode, row.CategoryCode, row.DepartmentRef);
}
