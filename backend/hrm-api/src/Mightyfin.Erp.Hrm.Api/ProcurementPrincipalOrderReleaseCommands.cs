using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
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
        return RunGoverned(http,scope,async(db,cases,core,auth,token)=>
        {
            var key=Key(http);var reason=Clean(input?.Reason);
            if(input is null || input.ExpectedOrderVersion<1 || input.OriginalDirectSourceSha256 is not {Length:64}
                || reason is null || reason.Length>2000)throw new Problem("OrderReleaseRequestInvalid",422);
            var fingerprint=Fingerprint(new{schemaVersion=2,operation="ReleaseRequest",id,actor=scope.SubjectId,
                input.ExpectedOrderVersion,input.OriginalDirectSourceSha256,reason});
            if(await ReplayGoverned(db,cases,core,scope,http,auth,"procurement-po-edit","ReleaseRequest",key,fingerprint,id,token) is { } prior)
                return prior;
            var load=await LoadGoverned(db,cases,core,scope,http,auth,"procurement-po-edit",id,token);
            var order=load.Order;var now=DateTimeOffset.UtcNow;
            var buyer=await Actor(core,scope,http,auth,"procurement-po-edit",order.BranchRef,true,token);
            if(order.BuyerSubjectId!=buyer.PrincipalId || order.Version!=input.ExpectedOrderVersion
                || order.OriginalDirectSourceSha256!=input.OriginalDirectSourceSha256
                || order.Status is not ("Draft" or "PendingApproval" or "Returned" or "Rejected" or "Approved")
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
            Guid? cancelledCaseId=null;
            Guid? cancellationDecisionId=null;
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
                await cases.Database.ExecuteSqlInterpolatedAsync($"""SELECT "Id" FROM core_approval.cases WHERE "TenantId"={scope.TenantId} AND "LegalEntityId"={scope.LegalEntityId} AND "Id"={submission.CoreApprovalCaseId} FOR UPDATE""",token);
                var workflow=await cases.Cases.Include(x=>x.Stages).ThenInclude(x=>x.Assignments)
                    .SingleOrDefaultAsync(x=>x.Id==submission.CoreApprovalCaseId
                    &&x.SourceId==id&&x.SourceType=="purchase_order"&&x.SourceModule=="procurement"
                    &&x.SourceVersion==1,token)??throw new Problem("OrderCoreCaseMissing",409);
                if(order.CurrentRevision!=1 || order.ApprovalInstanceRef!=workflow.Id.ToString("D")
                    || workflow.PolicyVersionId!=submission.PolicyVersionId
                    || workflow.PolicyVersionNumber!=submission.PolicyVersionNumber
                    || workflow.MakerSubjectId!=buyer.PrincipalId)
                    throw new Problem("OrderCoreOutcomeChanged",409);
                if(order.Status=="PendingApproval")
                {
                    // Withdraw the actual Core process in this same transaction.
                    // Approval authority and a fresh funding attestation are not
                    // prerequisites for stopping an unissued commitment.
                    var active=workflow.Stages.Where(x=>x.State=="Active").OrderBy(x=>x.Sequence)
                        .SelectMany(x=>x.Assignments.Where(a=>a.State=="Pending").OrderBy(a=>a.Id))
                        .FirstOrDefault()??throw new Problem("OrderCoreOutcomeChanged",409);
                    var decisions=await cases.Decisions.Where(x=>x.CaseId==workflow.Id).ToListAsync(token);
                    ApprovalEngine.Cancel(workflow,active,new ApprovalActor(buyer.PrincipalId,scope.TenantId,
                        scope.LegalEntityId,new HashSet<string>{"procurement-po-edit"}),"procurement-po-edit",
                        key.ToString("D"),reason,decisions,now);
                    var cancellation=decisions.Single(x=>x.IdempotencyKey==key.ToString("D")&&x.Action=="Cancel");
                    cases.Decisions.Add(cancellation);
                    cancelledCaseId=workflow.Id;cancellationDecisionId=cancellation.Id;
                }
                else
                {
                    if(workflow.State!=order.Status || workflow.CompletedAt is null)
                        throw new Problem("OrderCoreOutcomeChanged",409);
                    var action=order.Status switch {"Returned"=>"Return","Rejected"=>"Reject","Approved"=>"Approve",_=>throw new Problem("OrderCoreOutcomeChanged",409)};
                    if(!await cases.Decisions.AsNoTracking().AnyAsync(x=>x.CaseId==workflow.Id&&x.Action==action,token)
                        || !await db.PrincipalOrderDecisions.AsNoTracking().AnyAsync(x=>x.SubmissionId==submission.Id&&x.Action==action,token))
                        throw new Problem("OrderCoreOutcomeChanged",409);
                }
            }
            var request=new PrincipalOrderReleaseRequest{CommandKey=key,PurchaseOrderId=id,FundingEvidenceId=funding.Id,
                CancelledCoreCaseId=cancelledCaseId,CoreCancellationDecisionId=cancellationDecisionId,
                OriginalStatus=order.Status,CommercialRevision=order.CurrentRevision,
                RequestedOrderVersion=order.Version,
                OriginalDirectSourceSha256=order.OriginalDirectSourceSha256!,Reason=reason,
                RequestedByPrincipalId=buyer.PrincipalId,RequestedByWorkerAliasId=buyer.CoreLinkedWorkerId,
                RequestedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(now)!.Value};
            db.AddScoped(request);
            order.Status="ReleasePending";
            RecordGoverned(db,scope,order,"ReleaseRequest",key,new{releaseRequestId=request.Id,
                originalStatus=request.OriginalStatus,sourceQuantityReleased=false,financePostingChanged=false});
            await cases.SaveChangesAsync(token);
            return await SaveGoverned(db,scope,order,"ReleaseRequest",key,fingerprint,token);
        },ct);
    }

    private static Task<IResult> ConfirmRelease(Guid id,ReleaseConfirmationInput input,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)
    {
        try{GovernedEnabled(http);}catch(Problem p){return Task.FromResult(Error(p.Message,p.Status));}
        return RunGoverned(http,scope,async(db,cases,core,auth,token)=>
        {
            var key=Key(http);
            if(input is null || input.ExpectedOrderVersion<1 || input.ReleaseRequestId==Guid.Empty
                || input.CurrencyCode is null || input.ExternalReleaseEvidenceReference is null
                || input.RestoredSourceReservationReferences is null || input.ConfirmedAt==default)
                throw new Problem("OrderReleaseConfirmationInvalid",422);
            var fingerprint=Fingerprint(new{schemaVersion=2,operation="ReleaseConfirm",id,actor=scope.SubjectId,input});
            if(await ReplayGoverned(db,cases,core,scope,http,auth,"procurement-po-finance-confirm","ReleaseConfirm",key,fingerprint,id,token) is { } prior)
                return prior;
            var load=await LoadGoverned(db,cases,core,scope,http,auth,"procurement-po-finance-confirm",id,token);
            var order=load.Order;var now=DateTimeOffset.UtcNow;
            if(order.Status!="ReleasePending" || order.Version!=input.ExpectedOrderVersion
                || await db.PrincipalOrderReleaseConfirmations.AnyAsync(x=>x.PurchaseOrderId==id,token))
                throw new Problem("OrderReleaseConfirmationSourceChanged",409);
            var request=await db.PrincipalOrderReleaseRequests.SingleOrDefaultAsync(x=>x.Id==input.ReleaseRequestId
                &&x.PurchaseOrderId==id,token)??throw new Problem("OrderReleaseRequestMissing",409);
            if(!await CoreWithdrawalMatches(cases,order,request,token))
                throw new Problem("OrderCoreWithdrawalChanged",409);
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
                (order.BuyerSubjectId,funding.BuyerWorkerAliasId)})
                if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,original.Item1,original.Item2,
                    confirmer.PrincipalId,confirmer.CoreLinkedWorkerId,token))
                    throw new Problem("OrderReleaseSegregationRequired",403);
            foreach(var pin in load.Bindings)
                if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,
                    pin.RequesterPrincipalId.ToString("D"),pin.RequesterWorkerAliasId,
                    confirmer.PrincipalId,confirmer.CoreLinkedWorkerId,token))
                    throw new Problem("OrderReleaseSegregationRequired",403);
            foreach(var source in load.Sources)
                if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,
                    source.Source.Submission.MakerSubjectId,source.Source.Submission.MakerWorkerId,
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
            db.AddScoped(new PrincipalOrderReleaseConfirmation{CommandKey=key,ResultingOrderVersion=checked(order.Version+1),PurchaseOrderId=id,ReleaseRequestId=request.Id,
                FundingEvidenceId=funding.Id,ManualCommitmentReference=funding.ManualCommitmentReference,
                ExternalReleaseEvidenceReference=external,ApprovedManualPolicyReference=policy.ApprovedPolicyReference,
                RestoredSourceReservationsJson=JsonSerializer.Serialize(expected.Order(StringComparer.Ordinal).ToArray()),
                ReleasedGrossAmount=funding.AttestedGrossAmount,CurrencyCode=funding.CurrencyCode,
                ConfirmedByPrincipalId=confirmer.PrincipalId,ConfirmedByWorkerAliasId=confirmer.CoreLinkedWorkerId,
                ConfirmedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(input.ConfirmedAt)!.Value});
            order.Status="Cancelled";order.ClosureReason=request.Reason;
            order.ClosedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(now);
            RecordGoverned(db,scope,order,"ReleaseConfirm",key,new{releaseRequestId=request.Id,
                originalStatus=request.OriginalStatus,externalReleaseAttested=true,
                procurementAllocationReleased=true,financePostingChanged=false});
            return await SaveGoverned(db,scope,order,"ReleaseConfirm",key,fingerprint,token);
        },ct);
    }
}
