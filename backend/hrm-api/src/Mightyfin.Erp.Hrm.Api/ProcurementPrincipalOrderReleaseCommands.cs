using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalOrderDraftRoutes
{
    private static Task<IResult> RequestRelease(Guid id,ReleaseRequestInput input,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)
    {
        try{GovernedEnabled(http);}catch(Problem p){return Task.FromResult(Error(p.Message,p.Status));}
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var key=Key(http);var reason=Clean(input?.Reason);
            if(input is null || input.ExpectedOrderVersion<1 || input.OriginalDirectSourceSha256 is not {Length:64}
                || reason is null || reason.Length>2000)throw new Problem("OrderReleaseRequestInvalid",422);
            var fingerprint=Fingerprint(new{schemaVersion=2,operation="ReleaseRequest",id,actor=scope.SubjectId,
                input.ExpectedOrderVersion,input.OriginalDirectSourceSha256,reason});
            if(await Replay(db,core,scope,http,auth,"procurement-po-edit","ReleaseRequest",key,fingerprint,id,token) is { } prior)
                return prior;
            var load=await LoadGoverned(db,cases,core,scope,http,auth,"procurement-po-edit",id,token);
            var order=load.Order;var now=DateTimeOffset.UtcNow;
            var buyer=await Actor(core,scope,http,auth,"procurement-po-edit",order.BranchRef,true,token);
            if(order.BuyerSubjectId!=buyer.PrincipalId || order.Version!=input.ExpectedOrderVersion
                || order.OriginalDirectSourceSha256!=input.OriginalDirectSourceSha256
                || order.Status is not ("Draft" or "Returned" or "Rejected")
                || await db.PrincipalOrderReleaseRequests.AnyAsync(x=>x.PurchaseOrderId==id,token))
                throw new Problem("OrderReleaseRequestSourceChanged",409);
            var funding=await db.PrincipalOrderFundingEvidence.SingleOrDefaultAsync(x=>x.PurchaseOrderId==id
                &&x.CommercialRevision==1,token)??throw new Problem("OrderManualCommitmentMissing",409);
            if(funding.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                || funding.CommercialSnapshotSha256!=ProcurementEvidenceEncoding.Digest(funding.CommercialSnapshotJson)
                || !PrincipalOrderCommercialSnapshot.RetainedMatches(order,load.Lines,
                    funding.CommercialSnapshotJson,1)
                || funding.CurrencyCode!=order.CurrencyCode
                || funding.AttestedGrossAmount!=PurchaseOrderCommercialWorkflow.Totals(load.Lines).Gross
                || load.Sources.Any(x=>x.Allocation.Status!="Reserved"))
                throw new Problem("OrderOriginalCommitmentChanged",409);
            if(order.Status=="Draft")
            {
                if(order.CurrentRevision!=0 || order.ApprovalInstanceRef is not null
                    || await db.PrincipalOrderSubmissions.AnyAsync(x=>x.PurchaseOrderId==id,token))
                    throw new Problem("OrderReleaseRequestSourceChanged",409);
            }
            else
            {
                var submission=await db.PrincipalOrderSubmissions.SingleOrDefaultAsync(x=>x.PurchaseOrderId==id
                    &&x.CommercialRevision==1&&x.FundingEvidenceId==funding.Id,token)
                    ??throw new Problem("OrderSubmissionMissing",409);
                var workflow=await cases.Cases.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==submission.CoreApprovalCaseId
                    &&x.SourceId==id&&x.SourceType=="purchase_order"&&x.SourceModule=="procurement"
                    &&x.SourceVersion==1,token)??throw new Problem("OrderCoreCaseMissing",409);
                if(order.CurrentRevision!=1 || workflow.State!=order.Status || workflow.CompletedAt is null
                    || order.ApprovalInstanceRef!=workflow.Id.ToString("D"))
                    throw new Problem("OrderCoreOutcomeChanged",409);
                var decisionAction=order.Status=="Returned"?"Return":"Reject";
                var terminal=await cases.Decisions.AsNoTracking().AnyAsync(x=>x.CaseId==workflow.Id
                    &&x.Action==decisionAction,token);
                var mirror=await db.PrincipalOrderDecisions.AsNoTracking().AnyAsync(x=>x.SubmissionId==submission.Id
                    &&x.Action==decisionAction,token);
                if(!terminal || !mirror)throw new Problem("OrderCoreOutcomeChanged",409);
            }
            var request=new PrincipalOrderReleaseRequest{PurchaseOrderId=id,FundingEvidenceId=funding.Id,
                OriginalStatus=order.Status,CommercialRevision=order.CurrentRevision,
                RequestedOrderVersion=order.Version,
                OriginalDirectSourceSha256=order.OriginalDirectSourceSha256!,Reason=reason,
                RequestedByPrincipalId=buyer.PrincipalId,RequestedByWorkerAliasId=buyer.CoreLinkedWorkerId,
                RequestedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(now)!.Value};
            db.AddScoped(request);
            order.Status="ReleasePending";
            Record(db,scope,order,"ReleaseRequest",key,new{releaseRequestId=request.Id,
                originalStatus=request.OriginalStatus,sourceQuantityReleased=false,financePostingChanged=false});
            return await Save(db,scope,order,"ReleaseRequest",key,fingerprint,token);
        },ct);
    }

    private static Task<IResult> ConfirmRelease(Guid id,ReleaseConfirmationInput input,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)
    {
        try{GovernedEnabled(http);}catch(Problem p){return Task.FromResult(Error(p.Message,p.Status));}
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var key=Key(http);
            if(input is null || input.ExpectedOrderVersion<1 || input.ReleaseRequestId==Guid.Empty
                || input.CurrencyCode is null || input.ExternalReleaseEvidenceReference is null
                || input.RestoredSourceReservationReferences is null || input.ConfirmedAt==default)
                throw new Problem("OrderReleaseConfirmationInvalid",422);
            var fingerprint=Fingerprint(new{schemaVersion=2,operation="ReleaseConfirm",id,actor=scope.SubjectId,input});
            if(await Replay(db,core,scope,http,auth,"procurement-po-finance-confirm","ReleaseConfirm",key,fingerprint,id,token) is { } prior)
                return prior;
            var load=await LoadGoverned(db,cases,core,scope,http,auth,"procurement-po-finance-confirm",id,token);
            var order=load.Order;var now=DateTimeOffset.UtcNow;
            if(order.Status!="ReleasePending" || order.Version!=input.ExpectedOrderVersion
                || await db.PrincipalOrderReleaseConfirmations.AnyAsync(x=>x.PurchaseOrderId==id,token))
                throw new Problem("OrderReleaseConfirmationSourceChanged",409);
            var request=await db.PrincipalOrderReleaseRequests.SingleOrDefaultAsync(x=>x.Id==input.ReleaseRequestId
                &&x.PurchaseOrderId==id,token)??throw new Problem("OrderReleaseRequestMissing",409);
            var funding=await db.PrincipalOrderFundingEvidence.SingleOrDefaultAsync(x=>x.Id==request.FundingEvidenceId
                &&x.PurchaseOrderId==id,token)??throw new Problem("OrderManualCommitmentMissing",409);
            var policy=ManualPolicy(http,scope.LegalEntityId,now);
            var external=Clean(input.ExternalReleaseEvidenceReference);
            if(request.CommercialRevision!=order.CurrentRevision || request.RequestedOrderVersion>=order.Version
                || request.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                || request.RequestedByPrincipalId!=order.BuyerSubjectId
                || funding.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                || funding.CommercialSnapshotSha256!=ProcurementEvidenceEncoding.Digest(funding.CommercialSnapshotJson)
                || !PrincipalOrderCommercialSnapshot.RetainedMatches(order,load.Lines,
                    funding.CommercialSnapshotJson,1)
                || funding.AttestedGrossAmount!=PurchaseOrderCommercialWorkflow.Totals(load.Lines).Gross
                || input.CurrencyCode!=funding.CurrencyCode || input.CurrencyCode!=order.CurrencyCode
                || input.ReleasedGrossAmount!=funding.AttestedGrossAmount
                || external is null || external.Length>500
                || input.ConfirmedAt<request.RequestedAt || input.ConfirmedAt<funding.ObservedAt
                || input.ConfirmedAt>now || policy.EvidenceMaximumAgeMinutes is { } minutes
                    &&input.ConfirmedAt<now.AddMinutes(-minutes))
                throw new Problem("OrderExternalReleaseEvidenceInvalid",409);
            var expected=JsonSerializer.Deserialize<string[]>(funding.SourceReservationsJson)??[];
            if(expected.Length==0 || expected.Any(string.IsNullOrWhiteSpace)
                || expected.Distinct(StringComparer.Ordinal).Count()!=expected.Length
                || input.RestoredSourceReservationReferences.Count!=expected.Length
                || input.RestoredSourceReservationReferences.Any(string.IsNullOrWhiteSpace)
                || !expected.Order(StringComparer.Ordinal).SequenceEqual(
                    input.RestoredSourceReservationReferences.Order(StringComparer.Ordinal)))
                throw new Problem("OrderSourceReservationRestorationChanged",409);
            var confirmer=await Actor(core,scope,http,auth,"procurement-po-finance-confirm",order.BranchRef,true,token);
            foreach(var original in new[]{(request.RequestedByPrincipalId,request.RequestedByWorkerAliasId),
                (funding.RecordedByPrincipalId,funding.RecordedByWorkerAliasId)})
                if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,original.Item1,original.Item2,
                    confirmer.PrincipalId,confirmer.CoreLinkedWorkerId,token))
                    throw new Problem("OrderReleaseSegregationRequired",403);
            foreach(var pin in load.Bindings)
                if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,
                    pin.RequesterPrincipalId.ToString("D"),pin.RequesterWorkerAliasId,
                    confirmer.PrincipalId,confirmer.CoreLinkedWorkerId,token))
                    throw new Problem("OrderReleaseSegregationRequired",403);
            foreach(var source in load.Sources)
                foreach(var original in new[]{(source.Source.Submission.MakerSubjectId,
                        source.Source.Submission.MakerWorkerId),
                    (source.Source.Finance.CheckedBySubjectId,source.Source.Finance.CheckedByWorkerId)})
                    if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,
                        original.Item1,original.Item2,confirmer.PrincipalId,confirmer.CoreLinkedWorkerId,token))
                        throw new Problem("OrderReleaseSegregationRequired",403);
            var approvers=await db.PrincipalOrderDecisions.AsNoTracking().Where(x=>x.SubmissionId!=Guid.Empty
                &&db.PrincipalOrderSubmissions.Any(s=>s.Id==x.SubmissionId&&s.PurchaseOrderId==id)).ToArrayAsync(token);
            foreach(var approver in approvers)
                if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,
                    approver.ActorPrincipalId,approver.ActorWorkerAliasId,
                    confirmer.PrincipalId,confirmer.CoreLinkedWorkerId,token))
                    throw new Problem("OrderReleaseSegregationRequired",403);
            if(load.Sources.Length==0 || load.Sources.Any(x=>x.Allocation.Status!="Reserved"
                || x.Allocation.Quantity!=x.OrderLine.Quantity
                || x.Source.Line.AllocatedQuantity!=ExactDecimalArithmetic.Sum(x.Source.Allocations
                    .Where(a=>a.Status=="Reserved").Select(a=>a.Quantity))))
                throw new Problem("OrderAllocationChanged",409);
            var next=load.Sources.Select(x=>(Source:x,
                Quantity:ExactDecimalArithmetic.Subtract(x.Source.Line.AllocatedQuantity,x.Allocation.Quantity))).ToArray();
            foreach(var item in next)
            {
                item.Source.Source.Line.AllocatedQuantity=item.Quantity;
                item.Source.Source.Request.UpdatedAt=now;
                item.Source.Allocation.Status="Released";
                item.Source.Allocation.Reason=request.Reason;
            }
            db.AddScoped(new PrincipalOrderReleaseConfirmation{PurchaseOrderId=id,ReleaseRequestId=request.Id,
                FundingEvidenceId=funding.Id,ManualCommitmentReference=funding.ManualCommitmentReference,
                ExternalReleaseEvidenceReference=external,ApprovedManualPolicyReference=policy.ApprovedPolicyReference,
                RestoredSourceReservationsJson=JsonSerializer.Serialize(expected.Order(StringComparer.Ordinal).ToArray()),
                ReleasedGrossAmount=funding.AttestedGrossAmount,CurrencyCode=funding.CurrencyCode,
                ConfirmedByPrincipalId=confirmer.PrincipalId,ConfirmedByWorkerAliasId=confirmer.CoreLinkedWorkerId,
                ConfirmedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(input.ConfirmedAt)!.Value});
            order.Status="Cancelled";order.ClosureReason=request.Reason;
            order.ClosedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(now);
            Record(db,scope,order,"ReleaseConfirm",key,new{releaseRequestId=request.Id,
                originalStatus=request.OriginalStatus,externalReleaseAttested=true,
                procurementAllocationReleased=true,financePostingChanged=false});
            return await Save(db,scope,order,"ReleaseConfirm",key,fingerprint,token);
        },ct);
    }
}
