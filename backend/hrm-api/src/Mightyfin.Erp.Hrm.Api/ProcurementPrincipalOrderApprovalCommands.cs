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
    private static Task<IResult> Submit(Guid id,VersionInput input,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)
    {
        try{GovernedEnabled(http);}catch(Problem p){return Task.FromResult(Error(p.Message,p.Status));}
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var key=Key(http);
            if(input is null || input.ExpectedOrderVersion<1)throw new Problem("OrderSubmitInputInvalid",422);
            var fingerprint=Fingerprint(new{schemaVersion=2,operation="Submit",id,actor=scope.SubjectId,input});
            if(await Replay(db,core,scope,http,auth,"procurement-po-submit","Submit",key,fingerprint,id,token) is { } prior)return prior;
            var load=await LoadGoverned(db,cases,core,scope,http,auth,"procurement-po-submit",id,token);
            var order=load.Order;var now=DateTimeOffset.UtcNow;
            if(order.BuyerSubjectId!=scope.SubjectId || order.Status!="Draft" || order.CurrentRevision!=0
                || order.Version!=input.ExpectedOrderVersion || order.ApprovalInstanceRef is not null)
                throw new Problem("OrderSubmitSourceChanged",409);
            await CurrentPositiveSources(db,core,scope,http,load,now,token);
            var funding=await CurrentFunding(db,core,scope,http,load,1,now,token);
            var policies=await db.PolicyVersions.Where(x=>x.PolicyKey=="PURCHASE_ORDER"&&x.State=="Published"
                &&x.EffectiveFrom<=now&&(x.EffectiveTo==null||now<x.EffectiveTo)).ToArrayAsync(token);
            if(policies.Length!=1)throw new Problem("OrderPublishedPolicyRequired",409);
            var policy=policies[0];
            var route=PurchaseOrderCommercialWorkflow.PolicyRoute(order,load.Sources,policy,now);
            var plan=await ProcurementPrincipalOrderAuthorityResolver.ResolveAsync(order,load.Sources,route.Stages,
                funding,core,db,scope,now,token);
            var buyer=await Actor(core,scope,http,auth,"procurement-po-submit",order.BranchRef,true,token);
            if(buyer.PrincipalId!=order.BuyerSubjectId || buyer.CoreLinkedWorkerId!=load.Bindings[0].BuyerWorkerAliasId)
                throw new Problem("OrderBuyerIdentityChanged",409);
            var totals=PurchaseOrderCommercialWorkflow.Totals(load.Lines);
            var snapshot=PrincipalOrderCommercialSnapshot.Capture(order,load.Lines,load.Supplier,1);
            if(snapshot!=funding.CommercialSnapshotJson || ProcurementEvidenceEncoding.Digest(snapshot)!=funding.CommercialSnapshotSha256)
                throw new Problem("OrderFundingCommercialChanged",409);
            var workflow=new ApprovalCase{TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
                SourceModule="procurement",SourceType="purchase_order",SourceId=id,SourceVersion=1,
                PolicyVersionId=policy.Id,PolicyVersionNumber=policy.PolicyVersionNumber,
                MakerSubjectId=buyer.PrincipalId,OpenedAt=now};
            var submission=new PrincipalOrderSubmission{PurchaseOrderId=id,CommercialRevision=1,
                SubmittedRowVersion=checked(order.Version+1),CoreApprovalCaseId=workflow.Id,
                PolicyVersionId=policy.Id,PolicyVersionNumber=policy.PolicyVersionNumber,
                PolicyRulesSha256=ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
                PolicyDecisionJson=route.DecisionJson,FundingEvidenceId=funding.Id,
                BuyerPrincipalId=buyer.PrincipalId,BuyerWorkerAliasId=buyer.CoreLinkedWorkerId,
                OriginalDirectSourceSha256=order.OriginalDirectSourceSha256!,
                CommercialSnapshotSha256=funding.CommercialSnapshotSha256,CommercialSnapshotJson=snapshot,
                LineBaseAmount=totals.LineBase,GrossAmount=totals.Gross,CurrencyCode=order.CurrencyCode,
                SubmittedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(now)!.Value};
            var sequence=0;
            foreach(var resolved in plan)
            {
                var stage=new ApprovalStage{TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
                    CaseId=workflow.Id,Sequence=++sequence,Code=resolved.Route.Code,
                    Mode=resolved.Route.Mode=="One"?"Any":"All",
                    RequiredApprovals=resolved.Route.Mode=="One"?1:resolved.Assignees.Count,
                    State=sequence==1?"Active":"Waiting"};
                foreach(var candidate in resolved.Assignees)
                {
                    var assignment=new ApprovalAssignment{TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
                        CaseId=workflow.Id,StageId=stage.Id,AssigneeSubjectId=candidate.Person.PrincipalId,
                        PermissionKey="procurement-po-approve"};
                    stage.Assignments.Add(assignment);
                    var evidence=JsonSerializer.Serialize(new{candidate.Person.PrincipalId,
                        candidate.Person.CoreLinkedWorkerId,candidate.Authority.GrantId,
                        candidate.Authority.GrantRevision,candidate.Authority.AuthorityVersion},Json);
                    db.AddScoped(new PrincipalOrderAssignmentEvidence{SubmissionId=submission.Id,
                        CoreApprovalCaseId=workflow.Id,CoreStageId=stage.Id,CoreAssignmentId=assignment.Id,
                        GrantId=candidate.Authority.GrantId,GrantRevision=candidate.Authority.GrantRevision,
                        AuthorityVersion=candidate.Authority.AuthorityVersion,
                        PrincipalId=candidate.Person.PrincipalId,WorkerAliasId=candidate.Person.CoreLinkedWorkerId,
                        AuthorityEvidenceSha256=ProcurementEvidenceEncoding.Digest(evidence)});
                }
                workflow.Stages.Add(stage);
            }
            ApprovalEngine.ValidateNewCase(workflow);
            cases.AddCase(workflow);
            db.AddScoped(submission);
            order.CurrentRevision=1;order.SubmittedBusinessVersion=1;order.SubmittedAt=submission.SubmittedAt;
            order.ApprovalInstanceRef=workflow.Id.ToString("D");order.Status="PendingApproval";
            Record(db,scope,order,"Submit",key,new{submissionId=submission.Id,caseId=workflow.Id});
            await cases.SaveChangesAsync(token);
            return await Save(db,scope,order,"Submit",key,fingerprint,token);
        },ct);
    }

    private static Task<IResult> Decide(Guid id,Guid assignmentId,DecisionInput input,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)
    {
        try{GovernedEnabled(http);}catch(Problem p){return Task.FromResult(Error(p.Message,p.Status));}
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var key=Key(http);var reason=Clean(input?.Reason);
            if(input is null || input.Action is not ("Approve" or "Return" or "Reject")
                || input.ExpectedOrderVersion<1 || input.ExpectedCaseVersion<1 || input.ExpectedAssignmentVersion<1
                || reason?.Length>2000 || input.Action!="Approve" && reason is null)
                throw new Problem("OrderDecisionInputInvalid",422);
            var fingerprint=Fingerprint(new{schemaVersion=2,operation="Decide",id,assignmentId,actor=scope.SubjectId,
                input.Action,reason,input.ExpectedOrderVersion,input.ExpectedCaseVersion,input.ExpectedAssignmentVersion});
            if(await Replay(db,core,scope,http,auth,"procurement-po-approve","Decide",key,fingerprint,id,token) is { } prior)return prior;
            var load=await LoadGoverned(db,cases,core,scope,http,auth,"procurement-po-approve",id,token);
            var order=load.Order;var now=DateTimeOffset.UtcNow;
            var pin=await db.PrincipalOrderAssignments.SingleOrDefaultAsync(x=>x.CoreAssignmentId==assignmentId
                &&x.PrincipalId==scope.SubjectId,token)??throw new Problem("OrderAssignmentNotFound",404);
            var submission=await db.PrincipalOrderSubmissions.SingleOrDefaultAsync(x=>x.Id==pin.SubmissionId
                &&x.PurchaseOrderId==id,token)??throw new Problem("OrderSubmissionNotFound",409);
            var funding=await db.PrincipalOrderFundingEvidence.SingleOrDefaultAsync(x=>x.Id==submission.FundingEvidenceId
                &&x.PurchaseOrderId==id,token)??throw new Problem("OrderFundingEvidenceMissing",409);
            if(order.Status!="PendingApproval" || order.CurrentRevision!=1 || order.Version!=input.ExpectedOrderVersion)
                throw new Problem("OrderDecisionVersionChanged",409);
            var actor=await ProcurementPrincipalOrderAuthorityResolver.ValidateAsync(order,load.Sources,funding,
                submission,pin,core,db,scope,now,token);
            await cases.Database.ExecuteSqlInterpolatedAsync($"""SELECT "Id" FROM core_approval.cases WHERE "TenantId"={scope.TenantId} AND "LegalEntityId"={scope.LegalEntityId} AND "Id"={submission.CoreApprovalCaseId} FOR UPDATE""",token);
            var workflow=await cases.Cases.Include(x=>x.Stages).ThenInclude(x=>x.Assignments)
                .SingleOrDefaultAsync(x=>x.Id==submission.CoreApprovalCaseId,token)
                ??throw new Problem("OrderApprovalCaseNotFound",409);
            var assignment=workflow.Stages.SelectMany(x=>x.Assignments).SingleOrDefault(x=>x.Id==assignmentId
                &&x.AssigneeSubjectId==scope.SubjectId &&x.DelegatedFromSubjectId==null
                &&x.PermissionKey=="procurement-po-approve")??throw new Problem("OrderAssignmentNotFound",404);
            if(workflow.Version!=input.ExpectedCaseVersion || assignment.Version!=input.ExpectedAssignmentVersion
                || assignment.StageId!=pin.CoreStageId || workflow.SourceVersion!=1 || workflow.SourceId!=id
                || workflow.PolicyVersionId!=submission.PolicyVersionId)throw new Problem("OrderDecisionVersionChanged",409);
            var policy=await db.PolicyVersions.SingleOrDefaultAsync(x=>x.Id==submission.PolicyVersionId,token)
                ??throw new Problem("OrderPolicyMissing",409);
            await VerifySubmission(db,core,scope,http,load,submission,funding,policy,input.Action=="Approve",now,token);
            if(input.Action=="Approve")
            {
                var pins=await db.PrincipalOrderAssignments.Where(x=>x.SubmissionId==submission.Id).ToArrayAsync(token);
                var tasks=workflow.Stages.SelectMany(x=>x.Assignments).ToArray();
                if(pins.Length!=tasks.Length || tasks.Any(x=>pins.Count(p=>p.CoreAssignmentId==x.Id
                    &&p.PrincipalId==x.AssigneeSubjectId&&p.CoreStageId==x.StageId)!=1))
                    throw new Problem("OrderAssignmentSetChanged",409);
                foreach(var other in pins)
                    _=await ProcurementPrincipalOrderAuthorityResolver.ValidateAsync(order,load.Sources,funding,
                        submission,other,core,db,scope,now,token);
            }
            var decisions=await cases.Decisions.Where(x=>x.CaseId==workflow.Id).ToListAsync(token);
            var count=decisions.Count;
            ApprovalEngine.Decide(workflow,assignment,new ApprovalActor(scope.SubjectId,scope.TenantId,
                scope.LegalEntityId,new HashSet<string>{"procurement-po-approve"}),input.Action,key.ToString("D"),
                reason,decisions,now);
            var decision=decisions.Single(x=>x.IdempotencyKey==key.ToString("D"));
            if(decisions.Count>count)cases.Decisions.Add(decision);
            db.AddScoped(new PrincipalOrderDecisionEvidence{SubmissionId=submission.Id,
                CoreApprovalCaseId=workflow.Id,CoreAssignmentId=assignmentId,CoreDecisionId=decision.Id,
                ActorPrincipalId=actor.Person.PrincipalId,ActorWorkerAliasId=actor.Person.CoreLinkedWorkerId,
                Action=input.Action,Reason=reason,DecidedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(now)!.Value});
            if(workflow.State is "Approved" or "Returned" or "Rejected")
            {order.Status=workflow.State;order.ReturnReason=reason;}
            Record(db,scope,order,"Decide",key,new{decisionId=decision.Id,workflow.State});
            await cases.SaveChangesAsync(token);
            return await Save(db,scope,order,"Decide",key,fingerprint,token);
        },ct);
    }

    private static async Task VerifySubmission(ProcurementDbContext db,HrmDbContext core,
        ProcurementRequestScope scope,HttpContext http,GovernedOrder load,PrincipalOrderSubmission submission,
        PrincipalOrderFundingEvidence funding,PolicyVersion policy,bool current,DateTimeOffset now,CancellationToken ct)
    {
        var order=load.Order;
        if(submission.PurchaseOrderId!=order.Id || submission.CommercialRevision!=1
            || submission.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
            || submission.BuyerPrincipalId!=order.BuyerSubjectId || submission.BuyerWorkerAliasId!=funding.BuyerWorkerAliasId
            || submission.FundingEvidenceId!=funding.Id || submission.SubmittedRowVersion>order.Version
            || order.ApprovalInstanceRef!=submission.CoreApprovalCaseId.ToString("D")
            || submission.PolicyVersionId!=policy.Id || submission.PolicyVersionNumber!=policy.PolicyVersionNumber
            || policy.PolicyKey!="PURCHASE_ORDER" || policy.TenantId!=scope.TenantId
            || policy.LegalEntityId!=scope.LegalEntityId
            || submission.PolicyRulesSha256!=ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
            || submission.PolicyDecisionJson!=PurchaseOrderCommercialWorkflow.PolicyRoute(order,load.Sources,
                policy,submission.SubmittedAt).DecisionJson
            || submission.CommercialSnapshotJson!=funding.CommercialSnapshotJson
            || submission.CommercialSnapshotSha256!=funding.CommercialSnapshotSha256
            || submission.CommercialSnapshotSha256!=ProcurementEvidenceEncoding.Digest(submission.CommercialSnapshotJson)
            || submission.CurrencyCode!=order.CurrencyCode
            || (submission.LineBaseAmount,submission.GrossAmount)!=PurchaseOrderCommercialWorkflow.Totals(load.Lines))
            throw new Problem("OrderApprovalEvidenceChanged",409);
        if(current)
        {
            await CurrentPositiveSources(db,core,scope,http,load,now,ct);
            var actual=await CurrentFunding(db,core,scope,http,load,1,now,ct);
            if(actual.Id!=funding.Id)throw new Problem("OrderFundingEvidenceChanged",409);
        }
    }
}
