using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    // Called only inside the existing shared owning transaction. This does not
    // accept a caller's assertion that the principal issued version was approved.
    private void RequirePrincipalUnitReviewIssued(InvoiceUnitReviewSource source,
        InvoiceIssuedOrder projected, ApprovalDbContext core)
    {
        var order = PurchaseOrders.Single(x => x.Id == source.PurchaseOrderId);
        var lines = PurchaseOrderLines.Where(x => x.PurchaseOrderId == order.Id && x.IsActive).ToArray();
        var bindings = PrincipalOrderSourceBindings.Where(x => x.PurchaseOrderId == order.Id).ToArray();
        PrincipalOrderDraftPins.Verify(order, lines, bindings);
        var issued = PrincipalOrderIssuedVersions.Single(x => x.Id == source.IssuedVersionId);
        var submission = PrincipalOrderSubmissions.Single(x => x.Id == issued.SubmissionId);
        var funding = PrincipalOrderFundingEvidence.Single(x => x.Id == issued.FundingEvidenceId);
        var ack = PrincipalOrderAcknowledgments.Single(x => x.IssuedVersionId == issued.Id && x.Outcome == "Accepted");
        var policy = PolicyVersions.Single(x => x.Id == submission.PolicyVersionId);
        foreach (var row in new ProcurementRecord[] { order, issued, submission, funding, ack, policy })
            if (row.TenantId != source.TenantId || row.LegalEntityId != source.LegalEntityId)
                throw new InvalidOperationException("InvoiceUnitReviewPrincipalIssuedScopeRequired");
        if (order.BranchRef != source.BranchRef || order.Status != "Issued" || order.CurrentRevision != 1
            || order.SubmittedBusinessVersion != 1 || order.SubmittedAt is null || order.FinanceCommitmentRef is not null
            || issued.SchemaVersion != 2 || submission.SchemaVersion != 2 || funding.SchemaVersion != 2
            || issued.PurchaseOrderId != order.Id || submission.PurchaseOrderId != order.Id || funding.PurchaseOrderId != order.Id
            || issued.CommercialRevision != 1 || submission.CommercialRevision != 1 || funding.CommercialRevision != 1
            || submission.FundingEvidenceId != funding.Id || issued.CoreApprovalCaseId != submission.CoreApprovalCaseId
            || order.ApprovalInstanceRef != issued.CoreApprovalCaseId.ToString("D")
            || issued.OriginalDirectSourceSha256 != order.OriginalDirectSourceSha256
            || submission.OriginalDirectSourceSha256 != order.OriginalDirectSourceSha256
            || funding.OriginalDirectSourceSha256 != order.OriginalDirectSourceSha256
            || issued.CommercialSnapshotSha256 != ProcurementEvidenceEncoding.Digest(issued.CommercialSnapshotJson)
            || issued.CommercialSnapshotJson != submission.CommercialSnapshotJson
            || submission.CommercialSnapshotJson != funding.CommercialSnapshotJson
            || issued.CommercialSnapshotSha256 != submission.CommercialSnapshotSha256
            || submission.CommercialSnapshotSha256 != funding.CommercialSnapshotSha256
            || issued.ManualCommitmentReference != funding.ManualCommitmentReference
            || submission.BuyerPrincipalId != order.BuyerSubjectId || submission.BuyerWorkerAliasId != funding.BuyerWorkerAliasId
            || policy.PolicyKey != "PURCHASE_ORDER" || submission.PolicyVersionNumber != policy.PolicyVersionNumber
            || submission.PolicyRulesSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
            || funding.CurrencyCode != order.CurrencyCode || submission.CurrencyCode != order.CurrencyCode
            || ack.PurchaseOrderId != order.Id || ack.ObservedAt < issued.AuthorizedAt || order.IssuedAt != ack.ObservedAt
            || !PrincipalOrderCommercialSnapshot.RetainedMatches(order, lines, issued.CommercialSnapshotJson, 1)
            || (submission.LineBaseAmount, submission.GrossAmount) != PurchaseOrderCommercialWorkflow.Totals(lines)
            || funding.AttestedGrossAmount != submission.GrossAmount)
            throw new InvalidOperationException("InvoiceUnitReviewPrincipalIssuedParentsRequired");
        var workflow = core.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
            .Single(x => x.Id == issued.CoreApprovalCaseId);
        if (workflow.State != "Approved" || workflow.TenantId != source.TenantId || workflow.LegalEntityId != source.LegalEntityId
            || workflow.SourceModule != "procurement" || workflow.SourceType != "purchase_order" || workflow.SourceId != order.Id
            || workflow.SourceVersion != 1 || workflow.MakerSubjectId != order.BuyerSubjectId
            || workflow.PolicyVersionId != policy.Id || workflow.PolicyVersionNumber != submission.PolicyVersionNumber
            || workflow.CompletedAt is null || workflow.CompletionKey is null)
            throw new InvalidOperationException("InvoiceUnitReviewPrincipalIssuedCoreRequired");
        var tasks = workflow.Stages.SelectMany(x => x.Assignments).ToArray();
        var pins = PrincipalOrderAssignments.Where(x => x.SubmissionId == submission.Id).ToArray();
        if (tasks.Length == 0 || pins.Length != tasks.Length || tasks.Any(x => pins.Count(p =>
            p.CoreApprovalCaseId == workflow.Id && p.CoreStageId == x.StageId
            && p.CoreAssignmentId == x.Id && p.PrincipalId == x.AssigneeSubjectId) != 1))
            throw new InvalidOperationException("InvoiceUnitReviewPrincipalIssuedAssignmentsRequired");
        foreach (var pin in pins)
        {
            var literal = JsonSerializer.Serialize(new { pin.PrincipalId, CoreLinkedWorkerId = pin.WorkerAliasId,
                pin.GrantId, pin.GrantRevision, pin.AuthorityVersion }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (pin.TenantId != source.TenantId || pin.LegalEntityId != source.LegalEntityId
                || pin.AuthorityEvidenceSha256 != ProcurementEvidenceEncoding.Digest(literal))
                throw new InvalidOperationException("InvoiceUnitReviewPrincipalIssuedAssignmentEvidenceRequired");
        }
        var decisions = core.Decisions.Where(x => x.CaseId == workflow.Id).ToArray();
        var mirrors = PrincipalOrderDecisions.Where(x => x.SubmissionId == submission.Id).ToArray();
        if (decisions.Length == 0 || mirrors.Length != decisions.Length
            || !decisions.Any(x => x.Action == "Approve" && x.IdempotencyKey == workflow.CompletionKey)
            || decisions.Any(x => mirrors.Count(m => m.TenantId == source.TenantId && m.LegalEntityId == source.LegalEntityId
                && m.CoreDecisionId == x.Id && m.CoreApprovalCaseId == workflow.Id && m.CoreAssignmentId == x.AssignmentId
                && m.ActorPrincipalId == x.ActorSubjectId && m.Action == x.Action) != 1))
            throw new InvalidOperationException("InvoiceUnitReviewPrincipalIssuedDecisionsRequired");
        var categories = new Dictionary<Guid, string>();
        foreach (var binding in bindings)
        {
            var line = lines.Single(x => x.Id == binding.PurchaseOrderLineId);
            // Reuse the owning loader and full retained ledger in this SAME transaction.
            // The storage interceptor is synchronous; this EF loader does not create a
            // new connection, transaction, approval engine or detached authority witness.
            ApprovedPurchaseSource original;
            try
            {
                original = ApprovedDemandSourceLoader.LoadRetainedAsync(this, core,
                    new UnitReviewDemandBranch(scope, binding.BranchId), binding.PurchaseRequestId,
                    binding.PurchaseRequestLineId,
                    new RetainedDemandSourceIdentity(binding.SubmissionId, binding.ApprovalOutcomeId, binding.MethodId))
                    .GetAwaiter().GetResult();
            }
            catch (ApprovedDemandLoadException error)
            {
                throw new InvalidOperationException("InvoiceUnitReviewPrincipalDemandEvidenceRequired", error);
            }
            if (original.Request.BranchRef != order.BranchRef
                || original.Submission.SnapshotSchemaVersion != 3 || original.Submission.FinanceIdentitySchemaVersion != 2
                || original.Submission.RequestSnapshotSha256 != binding.RequestSnapshotSha256
                || original.Submission.PolicyDecisionSha256 != binding.PolicyDecisionSha256
                || original.Submission.PolicyRulesSha256 != binding.PolicyRulesSha256
                || original.Submission.FinanceSnapshotSha256 != binding.FinanceSnapshotSha256
                || original.Submission.PrincipalIdentitySha256 != binding.PrincipalIdentitySha256
                || original.Outcome.Id != binding.ApprovalOutcomeId || original.CoreCase.Id != binding.CoreApprovalCaseId
                || original.FinalDecision.Id != binding.CoreDecisionId || original.Finance.Id != binding.FinanceSnapshotId
                || original.Submission.RequestSnapshotSha256 != ProcurementEvidenceEncoding.Digest(original.Submission.RequestSnapshotJson)
                || original.Submission.PolicyDecisionSha256 != ProcurementEvidenceEncoding.Digest(original.Evaluation.DecisionJson)
                || original.Submission.PolicyRulesSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(original.Policy.RulesJson)
                || !PurchaseRequestApprovalPinning.HasRetainedPrincipalIdentity(original.Submission)
                || !FinanceSnapshotCodec.Matches(original.Finance, original.Submission.FinanceIdentitySchemaVersion,
                    original.Submission.FinanceSnapshotJson, original.Submission.FinanceSnapshotSha256)
                || original.CoreCase.State != "Approved" || original.Outcome.Outcome != "Approved"
                || original.FinalDecision.Action != "Approve" || original.FinalDecision.Id != original.Outcome.CoreDecisionId)
                throw new InvalidOperationException("InvoiceUnitReviewPrincipalDemandEvidenceRequired");
            _ = PurchaseDemandAllocationBalance.Evaluate(original.Request, original.Line,
                original.Allocations, original.AllocationOwners ?? [], scope.TenantId, scope.LegalEntityId);
            categories.Add(line.Id, InvoiceUnitReviewPrincipalIssuedProjection.RetainedCategory(
                order, line, binding, original.Allocations, original.Submission.RequestSnapshotJson));
        }
        InvoiceUnitReviewPrincipalIssuedProjection.Verify(projected, order, issued, lines, categories);
    }
    private sealed record UnitReviewDemandBranch(IProcurementScope Parent, Guid Branch) : IProcurementScope
    {
        public string TenantId => Parent.TenantId;
        public Guid LegalEntityId => Parent.LegalEntityId;
        public Guid? WorkLocationId => Branch;
        public Guid? OrgUnitId => Parent.OrgUnitId;
        public bool IsConfined => true;
        public string SubjectId => Parent.SubjectId;
        public Guid? WorkerId => Parent.WorkerId;
        public Guid? CoreLinkedWorkerId => Parent.CoreLinkedWorkerId;
        public string? CorrelationId => Parent.CorrelationId;
    }
}
