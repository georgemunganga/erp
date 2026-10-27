using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalOrderDraftRoutes
{
    private static Guid AddedGovernanceEvidenceId(ProcurementDbContext db,Guid orderId,string operation,Guid key)
    {
        IEnumerable<ProcurementRecord> rows=operation switch
        {
            "Funding"=>db.PrincipalOrderFundingEvidence.Local.Where(x=>x.PurchaseOrderId==orderId&&x.CommandKey==key),
            "Submit"=>db.PrincipalOrderSubmissions.Local.Where(x=>x.PurchaseOrderId==orderId&&x.CommandKey==key),
            "Decide"=>db.PrincipalOrderDecisions.Local.Where(x=>x.CommandKey==key
                &&db.PrincipalOrderSubmissions.Local.Any(s=>s.Id==x.SubmissionId&&s.PurchaseOrderId==orderId)),
            "Issue"=>db.PrincipalOrderIssuedVersions.Local.Where(x=>x.PurchaseOrderId==orderId&&x.CommandKey==key),
            "Ack"=>db.PrincipalOrderAcknowledgments.Local.Where(x=>x.PurchaseOrderId==orderId&&x.CommandKey==key),
            "ReleaseRequest"=>db.PrincipalOrderReleaseRequests.Local.Where(x=>x.PurchaseOrderId==orderId&&x.CommandKey==key),
            "ReleaseConfirm"=>db.PrincipalOrderReleaseConfirmations.Local.Where(x=>x.PurchaseOrderId==orderId&&x.CommandKey==key),
            _=>throw new Problem("OrderReceiptNeedsReview",409)
        };
        var added=rows.Where(x=>db.Entry(x).State==EntityState.Added).ToArray();
        if(added.Length!=1||added[0].Id==Guid.Empty)throw new Problem("OrderReceiptNeedsReview",409);
        return added[0].Id;
    }

    private static async Task<bool> GovernanceEvidenceMatches(ProcurementDbContext db,ApprovalDbContext cases,
        PurchaseOrder order,PrincipalOrderDraftReceipt receipt,PrincipalOrderGovernanceReceiptEncoding.Result result,
        CancellationToken ct)
    {
        var id=result.EvidenceId;var key=receipt.CommandKey;var actor=receipt.ActorPrincipalId;
        switch(receipt.Operation)
        {
            case "Funding":
                return await db.PrincipalOrderFundingEvidence.AsNoTracking().AnyAsync(x=>x.Id==id
                    &&x.CommandKey==key&&x.PurchaseOrderId==order.Id&&x.RecordedByPrincipalId==actor
                    &&x.OriginalDirectSourceSha256==order.OriginalDirectSourceSha256
                    &&x.ResultingOrderVersion==result.Version,ct);
            case "Submit":
                return await db.PrincipalOrderSubmissions.AsNoTracking().AnyAsync(x=>x.Id==id
                    &&x.CommandKey==key&&x.PurchaseOrderId==order.Id&&x.BuyerPrincipalId==actor
                    &&x.OriginalDirectSourceSha256==order.OriginalDirectSourceSha256
                    &&x.SubmittedRowVersion==result.Version,ct);
            case "Issue":
                return await db.PrincipalOrderIssuedVersions.AsNoTracking().AnyAsync(x=>x.Id==id
                    &&x.CommandKey==key&&x.PurchaseOrderId==order.Id&&x.AuthorizedByPrincipalId==actor
                    &&x.OriginalDirectSourceSha256==order.OriginalDirectSourceSha256
                    &&x.ResultingOrderVersion==result.Version,ct);
            case "Ack":
                return await db.PrincipalOrderAcknowledgments.AsNoTracking().AnyAsync(x=>x.Id==id
                    &&x.CommandKey==key&&x.PurchaseOrderId==order.Id&&x.RecordedByPrincipalId==actor
                    &&x.ResultingOrderVersion==result.Version
                    &&(x.Outcome=="Accepted"&&result.Status=="Issued"
                        ||x.Outcome=="Rejected"&&result.Status=="Disputed"),ct);
            case "ReleaseRequest":
                var request=await db.PrincipalOrderReleaseRequests.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id
                    &&x.CommandKey==key&&x.PurchaseOrderId==order.Id&&x.RequestedByPrincipalId==actor
                    &&x.OriginalDirectSourceSha256==order.OriginalDirectSourceSha256
                    &&x.RequestedOrderVersion+1==result.Version,ct);
                return request is not null&&await CoreWithdrawalMatches(cases,order,request,ct);
            case "ReleaseConfirm":
                return await db.PrincipalOrderReleaseConfirmations.AsNoTracking().AnyAsync(x=>x.Id==id
                    &&x.CommandKey==key&&x.PurchaseOrderId==order.Id&&x.ConfirmedByPrincipalId==actor
                    &&x.ResultingOrderVersion==result.Version
                    &&db.PrincipalOrderReleaseRequests.Any(r=>r.Id==x.ReleaseRequestId&&r.PurchaseOrderId==order.Id
                        &&r.FundingEvidenceId==x.FundingEvidenceId
                        &&r.OriginalDirectSourceSha256==order.OriginalDirectSourceSha256),ct);
            case "Decide":
                var decision=await db.PrincipalOrderDecisions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id
                    &&x.CommandKey==key&&x.ActorPrincipalId==actor,ct);
                if(decision is null)return false;
                var submission=await db.PrincipalOrderSubmissions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==decision.SubmissionId
                    &&x.PurchaseOrderId==order.Id&&x.CoreApprovalCaseId==decision.CoreApprovalCaseId
                    &&x.OriginalDirectSourceSha256==order.OriginalDirectSourceSha256,ct);
                if(submission is null)return false;
                var original=await cases.Decisions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==decision.CoreDecisionId
                    &&x.CaseId==decision.CoreApprovalCaseId&&x.AssignmentId==decision.CoreAssignmentId
                    &&x.ActorSubjectId==actor&&x.Action==decision.Action&&x.IdempotencyKey==key.ToString("D"),ct);
                var workflow=await cases.Cases.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==decision.CoreApprovalCaseId
                    &&x.SourceModule=="procurement"&&x.SourceType=="purchase_order"&&x.SourceId==order.Id
                    &&x.SourceVersion==submission.CommercialRevision&&x.PolicyVersionId==submission.PolicyVersionId,ct);
                if(original is null||workflow is null)return false;
                var terminal=decision.Action is "Return" or "Reject"
                    || workflow.CompletionKey==key.ToString("D");
                var expectedStatus=decision.Action switch
                {
                    "Approve"=>terminal?"Approved":"PendingApproval",
                    "Return" when terminal=>"Returned", "Reject" when terminal=>"Rejected", _=>""
                };
                return result.Status==expectedStatus&&(!terminal||workflow.State==expectedStatus)
                    &&result.Version==submission.SubmittedRowVersion+(terminal?1:0);
            default:return false;
        }
    }
    private static async Task<bool> CoreWithdrawalMatches(ApprovalDbContext cases,PurchaseOrder order,
        PrincipalOrderReleaseRequest request,CancellationToken ct)
    {
        if(request.OriginalStatus!="PendingApproval")
            return request.CancelledCoreCaseId is null&&request.CoreCancellationDecisionId is null;
        if(request.CancelledCoreCaseId is null||request.CoreCancellationDecisionId is null)return false;
        var workflow=await cases.Cases.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==request.CancelledCoreCaseId
            &&x.SourceModule=="procurement"&&x.SourceType=="purchase_order"&&x.SourceId==order.Id
            &&x.SourceVersion==request.CommercialRevision&&x.MakerSubjectId==request.RequestedByPrincipalId
            &&x.State=="Cancelled"&&x.CompletedAt!=null&&x.CompletionKey==request.CommandKey.ToString("D"),ct);
        if(workflow is null||order.ApprovalInstanceRef!=workflow.Id.ToString("D"))return false;
        return await cases.Decisions.AsNoTracking().AnyAsync(x=>x.Id==request.CoreCancellationDecisionId
            &&x.CaseId==workflow.Id&&x.ActorSubjectId==request.RequestedByPrincipalId&&x.Action=="Cancel"
            &&x.IdempotencyKey==request.CommandKey.ToString("D"),ct);
    }

}
