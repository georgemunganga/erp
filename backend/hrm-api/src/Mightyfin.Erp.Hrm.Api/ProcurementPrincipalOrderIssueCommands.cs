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
    private static Task<IResult> Issue(Guid id,VersionInput input,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)
    {
        try{GovernedEnabled(http);}catch(Problem p){return Task.FromResult(Error(p.Message,p.Status));}
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var key=Key(http);
            if(input is null || input.ExpectedOrderVersion<1)throw new Problem("OrderIssueInputInvalid",422);
            var fingerprint=Fingerprint(new{schemaVersion=2,operation="Issue",id,actor=scope.SubjectId,input});
            if(await Replay(db,core,scope,http,auth,"procurement-po-issue","Issue",key,fingerprint,id,token) is { } prior)return prior;
            var load=await LoadGoverned(db,cases,core,scope,http,auth,"procurement-po-issue",id,token);
            var order=load.Order;var now=DateTimeOffset.UtcNow;
            if(order.Status!="Approved" || order.Version!=input.ExpectedOrderVersion || order.CurrentRevision!=1
                || await db.PrincipalOrderIssuedVersions.AnyAsync(x=>x.PurchaseOrderId==id,token))
                throw new Problem("OrderIssueSourceChanged",409);
            var submission=await db.PrincipalOrderSubmissions.SingleOrDefaultAsync(x=>x.PurchaseOrderId==id
                &&x.CommercialRevision==1,token)??throw new Problem("OrderSubmissionMissing",409);
            var funding=await db.PrincipalOrderFundingEvidence.SingleOrDefaultAsync(x=>x.Id==submission.FundingEvidenceId
                &&x.PurchaseOrderId==id,token)??throw new Problem("OrderFundingMissing",409);
            var policy=await db.PolicyVersions.SingleOrDefaultAsync(x=>x.Id==submission.PolicyVersionId,token)
                ??throw new Problem("OrderPolicyMissing",409);
            await cases.Database.ExecuteSqlInterpolatedAsync($"""SELECT "Id" FROM core_approval.cases WHERE "TenantId"={scope.TenantId} AND "LegalEntityId"={scope.LegalEntityId} AND "Id"={submission.CoreApprovalCaseId} FOR UPDATE""",token);
            var workflow=await cases.Cases.Include(x=>x.Stages).ThenInclude(x=>x.Assignments)
                .SingleOrDefaultAsync(x=>x.Id==submission.CoreApprovalCaseId,token)
                ??throw new Problem("OrderCaseMissing",409);
            if(workflow.State!="Approved" || workflow.SourceType!="purchase_order"
                || workflow.SourceModule!="procurement" || workflow.SourceId!=id || workflow.SourceVersion!=1
                || workflow.PolicyVersionId!=submission.PolicyVersionId
                || workflow.PolicyVersionNumber!=submission.PolicyVersionNumber)
                throw new Problem("OrderCoreApprovalMissing",409);
            await VerifySubmission(db,core,scope,http,load,submission,funding,policy,true,now,token);
            var pins=await db.PrincipalOrderAssignmentEvidence.Where(x=>x.SubmissionId==submission.Id).ToArrayAsync(token);
            var tasks=workflow.Stages.SelectMany(x=>x.Assignments).ToArray();
            if(pins.Length!=tasks.Length || tasks.Any(x=>pins.Count(p=>p.CoreAssignmentId==x.Id
                &&p.PrincipalId==x.AssigneeSubjectId&&p.CoreStageId==x.StageId)!=1))
                throw new Problem("OrderAssignmentSetChanged",409);
            foreach(var pin in pins)
                _=await ProcurementPrincipalOrderAuthorityResolver.ValidateAsync(order,load.Sources,funding,
                    submission,pin,core,db,scope,now,token);
            var approvals=await cases.Decisions.Where(x=>x.CaseId==workflow.Id&&x.Action=="Approve").ToArrayAsync(token);
            if(approvals.Length==0 || !approvals.Any(x=>x.IdempotencyKey==workflow.CompletionKey))
                throw new Problem("OrderCoreDecisionMissing",409);
            var mirrored=await db.PrincipalOrderDecisions.Where(x=>x.SubmissionId==submission.Id).ToArrayAsync(token);
            if(approvals.Any(x=>mirrored.Count(y=>y.CoreDecisionId==x.Id&&y.CoreAssignmentId==x.AssignmentId
                &&y.ActorPrincipalId==x.ActorSubjectId&&y.Action=="Approve")!=1))
                throw new Problem("OrderDecisionEvidenceChanged",409);
            var issuer=await Actor(core,scope,http,auth,"procurement-po-issue",order.BranchRef,true,token);
            foreach(var source in load.Sources)
                PurchaseOrderSourceAllocation.ConfirmOrdered(source.Source,order,source.OrderLine,source.Allocation,now);
            var issued=new PrincipalOrderIssuedVersion{PurchaseOrderId=id,SubmissionId=submission.Id,
                FundingEvidenceId=funding.Id,CoreApprovalCaseId=workflow.Id,CommercialRevision=1,
                CommercialSnapshotJson=submission.CommercialSnapshotJson,
                CommercialSnapshotSha256=submission.CommercialSnapshotSha256,
                OriginalDirectSourceSha256=order.OriginalDirectSourceSha256!,
                ManualCommitmentReference=funding.ManualCommitmentReference,
                AuthorizedByPrincipalId=issuer.PrincipalId,AuthorizedByWorkerAliasId=issuer.CoreLinkedWorkerId,
                AuthorizedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(now)!.Value};
            db.AddScoped(issued);
            order.Status="IssuePending";
            Record(db,scope,order,"Issue",key,new{issuedVersionId=issued.Id,internalOnly=true,
                supplierSent=false,financePostingCreated=false});
            return await Save(db,scope,order,"Issue",key,fingerprint,token);
        },ct);
    }

    private static Task<IResult> Acknowledgment(Guid id,AcknowledgmentInput input,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)
    {
        try{GovernedEnabled(http);}catch(Problem p){return Task.FromResult(Error(p.Message,p.Status));}
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var key=Key(http);
            if(input is null || input.ExpectedOrderVersion<1 || input.IssuedVersionId==Guid.Empty
                || input.Outcome is not ("Accepted" or "Rejected")
                || input.Channel is null || input.SupplierReference is null || input.EvidenceReference is null)
                throw new Problem("OrderSupplierResponseInvalid",422);
            var fingerprint=Fingerprint(new{schemaVersion=2,operation="Ack",id,actor=scope.SubjectId,input});
            if(await Replay(db,core,scope,http,auth,"procurement-po-dispatch-confirm","Ack",key,fingerprint,id,token) is { } prior)return prior;
            var load=await LoadGoverned(db,cases,core,scope,http,auth,"procurement-po-dispatch-confirm",id,token);
            var order=load.Order;var now=DateTimeOffset.UtcNow;
            var issued=await db.PrincipalOrderIssuedVersions.SingleOrDefaultAsync(x=>x.Id==input.IssuedVersionId
                &&x.PurchaseOrderId==id,token)??throw new Problem("OrderIssuedVersionMissing",409);
            var policy=http.RequestServices.GetRequiredService<ProcurementOrderExecutionOptions>();
            var channel=Clean(input.Channel);var reference=Clean(input.SupplierReference);
            var evidence=Clean(input.EvidenceReference);
            if(order.Status!="IssuePending" || order.Version!=input.ExpectedOrderVersion
                || issued.CommercialRevision!=1 || issued.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                || issued.CommercialSnapshotSha256!=ProcurementEvidenceEncoding.Digest(issued.CommercialSnapshotJson)
                || string.IsNullOrWhiteSpace(policy.ApprovedManualDispatchPolicyReference)
                || !policy.AllowedDeliveryChannels.Contains(channel,StringComparer.Ordinal)
                || reference is null || reference.Length>200 || evidence is null || evidence.Length>500
                || input.ObservedAt<issued.AuthorizedAt || input.ObservedAt>now
                || await db.PrincipalOrderAcknowledgments.AnyAsync(x=>x.IssuedVersionId==issued.Id,token))
                throw new Problem("OrderSupplierResponseChanged",409);
            var recorder=await Actor(core,scope,http,auth,"procurement-po-dispatch-confirm",order.BranchRef,true,token);
            var submission=await db.PrincipalOrderSubmissions.SingleOrDefaultAsync(x=>x.Id==issued.SubmissionId,token)
                ??throw new Problem("OrderSubmissionMissing",409);
            var funding=await db.PrincipalOrderFundingEvidence.SingleOrDefaultAsync(x=>x.Id==issued.FundingEvidenceId,token)
                ??throw new Problem("OrderFundingMissing",409);
            foreach(var original in new[]{(issued.AuthorizedByPrincipalId,issued.AuthorizedByWorkerAliasId),
                (submission.BuyerPrincipalId,submission.BuyerWorkerAliasId),
                (funding.RecordedByPrincipalId,funding.RecordedByWorkerAliasId)})
                if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,original.Item1,original.Item2,
                    recorder.PrincipalId,recorder.CoreLinkedWorkerId,token))
                    throw new Problem("OrderSupplierResponseSegregationRequired",403);
            db.AddScoped(new PrincipalOrderAcknowledgment{IssuedVersionId=issued.Id,PurchaseOrderId=id,
                Outcome=input.Outcome,Channel=channel!,SupplierReference=reference,EvidenceReference=evidence,
                RecordedByPrincipalId=recorder.PrincipalId,RecordedByWorkerAliasId=recorder.CoreLinkedWorkerId,
                ObservedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(input.ObservedAt)!.Value});
            order.Status=input.Outcome=="Accepted"?"Issued":"Disputed";
            if(input.Outcome=="Accepted")order.IssuedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(input.ObservedAt);
            Record(db,scope,order,"Ack",key,new{issuedVersionId=issued.Id,outcome=input.Outcome,
                observedManualSupplierResponse=true,supplierSendNotPerformed=true});
            return await Save(db,scope,order,"Ack",key,fingerprint,token);
        },ct);
    }
}
