using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>A scoped retained commercial proof for successor operations, such
/// as receiving. The caller must first resolve the CURRENT Core actor/action
/// and pass its server-derived authorized branches. This does not authorize a
/// new purchase, supplier dispatch, receipt, Finance balance or payment.</summary>
internal static class ProcurementPrincipalOrderRetainedIssued
{
    internal sealed record Source(PurchaseOrderLine OrderLine,PrincipalOrderSourceBinding Binding,
        ApprovedPurchaseSource Original,PurchaseRequestAllocation Allocation,
        string? OriginalCategoryCode,string? OriginalPurchaseType);
    internal sealed record Result(PurchaseOrder Order,IReadOnlyList<PurchaseOrderLine> Lines,
        IReadOnlyList<Source> Sources,Source? SelectedLine,
        PrincipalOrderIssuedVersion Issued,PrincipalOrderSubmission Submission,
        PrincipalOrderFundingEvidence Funding,PrincipalOrderAcknowledgment Acknowledgment,
        ApprovalCase CoreCase,IReadOnlyList<PrincipalOrderAssignmentEvidence> Assignments,
        IReadOnlyList<ApprovalDecision> CoreDecisions);

    private sealed record BoundBranch(ProcurementRequestScope Parent,Guid Branch):IProcurementScope
    {
        public string TenantId=>Parent.TenantId;
        public Guid LegalEntityId=>Parent.LegalEntityId;
        public Guid? WorkLocationId=>Branch;
        public Guid? OrgUnitId=>Parent.OrgUnitId;
        public bool IsConfined=>true;
        public string SubjectId=>Parent.SubjectId;
        public Guid? WorkerId=>Parent.WorkerId;
        public Guid? CoreLinkedWorkerId=>Parent.CoreLinkedWorkerId;
        public string? CorrelationId=>Parent.CorrelationId;
    }
    private static InvalidOperationException Invalid()=>new("PrincipalOrderRetainedIssueUnavailable");

    internal static async Task<Result> LoadAsync(ProcurementDbContext db,ApprovalDbContext cases,
        ProcurementRequestScope scope,IReadOnlySet<Guid> currentCoreAllowedBranches,
        Guid orderId,Guid issuedVersionId,Guid? selectedLineId,bool lockSources,CancellationToken ct)
    {
        if(orderId==Guid.Empty || issuedVersionId==Guid.Empty || selectedLineId==Guid.Empty
            || scope.OrgUnitId is not null || scope.PrincipalId?.ToString("D")!=scope.SubjectId
            || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches
            || currentCoreAllowedBranches.Count==0)
            throw Invalid();
        // Read the immutable branch before acquiring locks. The full order is
        // reloaded after locks; requests always precede the order lock.
        var pre=await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==orderId
            &&x.PrincipalDraftSchemaVersion==2,ct)??throw Invalid();
        Fence(pre,scope,currentCoreAllowedBranches);
        var requestIds=await db.PrincipalOrderSourceBindings.AsNoTracking()
            .Where(x=>x.PurchaseOrderId==orderId).Select(x=>x.PurchaseRequestId)
            .Distinct().OrderBy(x=>x).ToArrayAsync(ct);
        if(requestIds.Length is <1 or >100)throw Invalid();
        if(lockSources)
        {
            if(db.Database.CurrentTransaction is null)throw Invalid();
            foreach(var requestId in requestIds)
                await ProcurementApprovalExecutionLocks.LockRequestAsync(db,scope,requestId,ct);
            if(db.Database.IsNpgsql())
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.purchase_orders WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={orderId} FOR UPDATE",ct);
        }
        var order=await db.PurchaseOrders.SingleOrDefaultAsync(x=>x.Id==orderId
            &&x.PrincipalDraftSchemaVersion==2,ct)??throw Invalid();
        Fence(order,scope,currentCoreAllowedBranches);
        if(order.Status!="Issued" || order.CurrentRevision!=1 || order.SubmittedBusinessVersion!=1
            || order.IssuedAt is null || order.ApprovalInstanceRef is null || order.SubmittedAt is null
            || order.FinanceCommitmentRef is not null)throw Invalid();
        var lines=await db.PurchaseOrderLines.Where(x=>x.PurchaseOrderId==orderId&&x.IsActive)
            .OrderBy(x=>x.LineNumber).ToArrayAsync(ct);
        var bindings=await db.PrincipalOrderSourceBindings.Where(x=>x.PurchaseOrderId==orderId)
            .ToArrayAsync(ct);
        PrincipalOrderDraftPins.Verify(order,lines,bindings);
        if(lines.Length!=bindings.Length || lines.Length is <1 or >100
            || !requestIds.ToHashSet().SetEquals(bindings.Select(x=>x.PurchaseRequestId)))throw Invalid();
        var issued=await db.PrincipalOrderIssuedVersions.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.Id==issuedVersionId&&x.PurchaseOrderId==orderId,ct)??throw Invalid();
        var submission=await db.PrincipalOrderSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.Id==issued.SubmissionId&&x.PurchaseOrderId==orderId,ct)??throw Invalid();
        var policy=await db.PolicyVersions.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.Id==submission.PolicyVersionId&&x.PolicyKey=="PURCHASE_ORDER",ct)
            ??throw Invalid();
        var funding=await db.PrincipalOrderFundingEvidence.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.Id==issued.FundingEvidenceId&&x.PurchaseOrderId==orderId,ct)??throw Invalid();
        var acknowledgment=await db.PrincipalOrderAcknowledgments.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.IssuedVersionId==issued.Id&&x.PurchaseOrderId==orderId
                &&x.Outcome=="Accepted",ct)??throw Invalid();
        if(issued.CommercialRevision!=1 || submission.CommercialRevision!=1
            || funding.CommercialRevision!=1 || submission.FundingEvidenceId!=funding.Id
            || issued.CoreApprovalCaseId!=submission.CoreApprovalCaseId
            || order.ApprovalInstanceRef!=issued.CoreApprovalCaseId.ToString("D")
            || issued.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
            || submission.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
            || funding.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
            || issued.CommercialSnapshotSha256!=ProcurementEvidenceEncoding.Digest(issued.CommercialSnapshotJson)
            || issued.CommercialSnapshotJson!=submission.CommercialSnapshotJson
            || submission.CommercialSnapshotJson!=funding.CommercialSnapshotJson
            || submission.CommercialSnapshotSha256!=funding.CommercialSnapshotSha256
            || issued.CommercialSnapshotSha256!=submission.CommercialSnapshotSha256
            || issued.ManualCommitmentReference!=funding.ManualCommitmentReference
            || submission.BuyerPrincipalId!=order.BuyerSubjectId
            || submission.BuyerWorkerAliasId!=funding.BuyerWorkerAliasId
            || submission.PolicyVersionNumber!=policy.PolicyVersionNumber
            || submission.PolicyRulesSha256!=ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
            || funding.CurrencyCode!=order.CurrencyCode || submission.CurrencyCode!=order.CurrencyCode
            || acknowledgment.ObservedAt<issued.AuthorizedAt
            || order.IssuedAt!=acknowledgment.ObservedAt
            || !PrincipalOrderCommercialSnapshot.RetainedMatches(order,lines,
                issued.CommercialSnapshotJson,1)
            || (submission.LineBaseAmount,submission.GrossAmount)!=PurchaseOrderCommercialWorkflow.Totals(lines)
            || funding.AttestedGrossAmount!=submission.GrossAmount)throw Invalid();
        var workflow=await cases.Cases.AsNoTracking().Include(x=>x.Stages)
            .ThenInclude(x=>x.Assignments).SingleOrDefaultAsync(x=>x.Id==issued.CoreApprovalCaseId,ct)
            ??throw Invalid();
        if(workflow.State!="Approved" || workflow.SourceModule!="procurement"
            || workflow.SourceType!="purchase_order" || workflow.SourceId!=orderId
            || workflow.SourceVersion!=1 || workflow.TenantId!=scope.TenantId
            || workflow.LegalEntityId!=scope.LegalEntityId
            || workflow.MakerSubjectId!=order.BuyerSubjectId
            || workflow.PolicyVersionId!=submission.PolicyVersionId
            || workflow.PolicyVersionNumber!=submission.PolicyVersionNumber
            || workflow.CompletedAt is null || workflow.CompletionKey is null)throw Invalid();
        var tasks=workflow.Stages.SelectMany(x=>x.Assignments).ToArray();
        var pins=await db.PrincipalOrderAssignments.AsNoTracking().Where(x=>x.SubmissionId==submission.Id)
            .ToArrayAsync(ct);
        if(tasks.Length==0 || pins.Length!=tasks.Length
            || tasks.Any(x=>pins.Count(p=>p.CoreAssignmentId==x.Id&&p.CoreStageId==x.StageId
                &&p.CoreApprovalCaseId==workflow.Id&&p.PrincipalId==x.AssigneeSubjectId)==0))throw Invalid();
        foreach(var pin in pins)
        {
            var value=JsonSerializer.Serialize(new{pin.PrincipalId,
                CoreLinkedWorkerId=pin.WorkerAliasId,pin.GrantId,pin.GrantRevision,pin.AuthorityVersion},
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if(pin.AuthorityEvidenceSha256!=ProcurementEvidenceEncoding.Digest(value))throw Invalid();
        }
        var decisions=await cases.Decisions.AsNoTracking().Where(x=>x.CaseId==workflow.Id).ToArrayAsync(ct);
        var mirrors=await db.PrincipalOrderDecisions.AsNoTracking().Where(x=>x.SubmissionId==submission.Id)
            .ToArrayAsync(ct);
        if(decisions.Length==0 || mirrors.Length!=decisions.Length
            || !decisions.Any(x=>x.Action=="Approve"&&x.IdempotencyKey==workflow.CompletionKey)
            || decisions.Any(x=>mirrors.Count(m=>m.CoreDecisionId==x.Id
                &&m.CoreApprovalCaseId==workflow.Id&&m.CoreAssignmentId==x.AssignmentId
                &&m.ActorPrincipalId==x.ActorSubjectId&&m.Action==x.Action)!=1))throw Invalid();
        var sources=new List<Source>(bindings.Length);
        foreach(var binding in bindings)
        {
            var line=lines.SingleOrDefault(x=>x.Id==binding.PurchaseOrderLineId);
            if(line is null || binding.BranchId!=order.BranchRef)throw Invalid();
            var original=await ApprovedDemandSourceLoader.LoadRetainedAsync(db,cases,
                new BoundBranch(scope,binding.BranchId),binding.PurchaseRequestId,binding.PurchaseRequestLineId,
                new RetainedDemandSourceIdentity(binding.SubmissionId,binding.ApprovalOutcomeId,binding.MethodId),ct);
            var allocation=original.Allocations.SingleOrDefault(x=>x.Id==binding.AllocationId);
            if(allocation is null || allocation.Status!="Ordered"
                || allocation.TargetType!="PurchaseOrder" || allocation.TargetId!=order.Id
                || allocation.Quantity!=binding.Quantity || allocation.EstimatedValue!=binding.EstimatedValue
                || allocation.CurrencyCode!=binding.CurrencyCode
                || original.Request.BranchRef!=order.BranchRef
                || original.Submission.SnapshotSchemaVersion!=3
                || original.Submission.FinanceIdentitySchemaVersion!=2
                || original.Submission.RequestSnapshotSha256!=binding.RequestSnapshotSha256
                || original.Submission.PolicyDecisionSha256!=binding.PolicyDecisionSha256
                || original.Submission.PolicyRulesSha256!=binding.PolicyRulesSha256
                || original.Submission.FinanceSnapshotSha256!=binding.FinanceSnapshotSha256
                || original.Submission.PrincipalIdentitySha256!=binding.PrincipalIdentitySha256
                || original.Outcome.Id!=binding.ApprovalOutcomeId
                || original.CoreCase.Id!=binding.CoreApprovalCaseId
                || original.FinalDecision.Id!=binding.CoreDecisionId
                || original.Finance.Id!=binding.FinanceSnapshotId
                || original.Submission.RequestSnapshotSha256!=ProcurementEvidenceEncoding.Digest(
                    original.Submission.RequestSnapshotJson)
                || original.Submission.PolicyDecisionSha256!=ProcurementEvidenceEncoding.Digest(
                    original.Evaluation.DecisionJson)
                || original.Submission.PolicyRulesSha256!=ProcurementEvidenceEncoding.PolicyJsonDigest(
                    original.Policy.RulesJson)
                || !PurchaseRequestApprovalPinning.HasRetainedPrincipalIdentity(original.Submission)
                || !FinanceSnapshotCodec.Matches(original.Finance,
                    original.Submission.FinanceIdentitySchemaVersion,
                    original.Submission.FinanceSnapshotJson,original.Submission.FinanceSnapshotSha256)
                || original.CoreCase.State!="Approved" || original.Outcome.Outcome!="Approved"
                || original.FinalDecision.Action!="Approve"
                || original.FinalDecision.Id!=original.Outcome.CoreDecisionId)
                throw Invalid();
            _=PurchaseDemandAllocationBalance.Evaluate(original.Request,original.Line,
                original.Allocations,original.AllocationOwners??[],scope.TenantId,scope.LegalEntityId);
            using var snapshot=JsonDocument.Parse(original.Submission.RequestSnapshotJson);
            var pinned=snapshot.RootElement.GetProperty("legacyBusiness").GetProperty("lines")
                .EnumerateArray().Where(x=>x.GetProperty("Id").GetGuid()==binding.PurchaseRequestLineId)
                .ToArray();
            if(pinned.Length!=1)throw Invalid();
            sources.Add(new(line,binding,original,allocation,
                pinned[0].GetProperty("CategoryCode").GetString(),
                pinned[0].GetProperty("PurchaseType").GetString()));
        }
        var selected=selectedLineId is { } lineId?sources.SingleOrDefault(x=>x.OrderLine.Id==lineId):null;
        if(selectedLineId is not null && selected is null)throw Invalid();
        return new(order,lines,sources,selected,issued,submission,funding,acknowledgment,
            workflow,pins,decisions);
    }

    private static void Fence(PurchaseOrder order,ProcurementRequestScope scope,
        IReadOnlySet<Guid> currentCoreAllowedBranches)
    {
        if(order.TenantId!=scope.TenantId || order.LegalEntityId!=scope.LegalEntityId
            || order.BranchRef is not { } branch || !currentCoreAllowedBranches.Contains(branch)
            || scope.WorkLocationId is { } selected && selected!=branch)throw Invalid();
    }
}
