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
    private static Task<IResult> Sources(int? page,int? pageSize,string? q,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)=>Run(http,scope,async(db,cases,core,auth,token)=>
    {
        var paging=ProcurementSourcingDiscoveryQueries.Paging(page,pageSize,q);
        var actor=await Actor(core,scope,http,auth,"procurement-po-convert",null,false,token);
        var branches=await ProcurementExecutionRecordScope.Branches(core,scope,null,false,token);
        var requests=db.PurchaseRequests.AsNoTracking().Where(x=>x.Status=="Approved"||x.Status=="PartiallyOrdered"||x.Status=="Ordered");
        if(scope.WorkLocationId is { } selected)requests=requests.Where(x=>x.BranchRef==selected);
        if(!actor.OrganisationWide)
            requests=requests.Where(x=>x.BranchRef!=null&&branches.Contains(x.BranchRef.Value));
        var query=from line in db.PurchaseRequestLines.AsNoTracking()
            join request in requests on line.PurchaseRequestId equals request.Id
            where request.ApprovalState=="Approved" && line.ApprovalStatus=="Approved"
                && (line.Route=="Unassigned" || line.Route=="DirectPurchase")
            select new{request.Id,request.Number,request.BranchRef,request.Version,
                lineId=line.Id,line.LineNumber,line.Description};
        if(paging.Search.Length>0)query=query.Where(x=>EF.Functions.ILike(x.Number,paging.Pattern,"\\")
            || EF.Functions.ILike(x.Description,paging.Pattern,"\\"));
        var candidateTotal=await query.CountAsync(token);
        var rows=await query.OrderBy(x=>x.Number).ThenBy(x=>x.Id).ThenBy(x=>x.LineNumber)
            .ThenBy(x=>x.lineId).Skip(paging.Offset).Take(paging.Size).ToArrayAsync(token);
        var items=new List<object>();var now=DateTimeOffset.UtcNow;
        foreach(var row in rows)
        {
            try
            {
                var source=await Source(db,cases,core,scope,http,auth,"procurement-po-convert",row.Id,row.lineId,token);
                if(source.Submission.SnapshotSchemaVersion!=3 || source.Submission.FinanceIdentitySchemaVersion!=2)
                    throw new InvalidOperationException("OrderPrincipalSourceRequired");
                var finance=await ProcurementApprovalFinanceEligibility.CheckPrincipalAsync(source.Request,source.Finance,
                    http.RequestServices.GetRequiredService<ProcurementApprovalExecutionOptions>(),core,db,scope,now,token);
                if(finance.Mode!=source.Submission.FinanceEvidenceMode
                    || finance.Reference!=source.Submission.ManualEvidenceAuthorityRef
                    || await ProcurementCatalogClassificationBinding.StoredIssue(db,[source.Line],token) is not null)
                    throw new InvalidOperationException("OrderPrincipalSourceUnavailable");
                var available=PurchaseOrderSourceAllocation.Check(source,scope.TenantId,scope.LegalEntityId,now);
                items.Add(new{purchaseRequestId=row.Id,purchaseRequestLineId=row.lineId,requestNumber=row.Number,
                    row.BranchRef,expectedRequestVersion=source.Request.Version,source.Line.Description,
                    source.Line.UnitCode,source.Line.CategoryCode,
                    availableQuantity=ProcurementEvidenceEncoding.Decimal(available.AvailableQuantity),
                    approvedUnitEstimate=ProcurementEvidenceEncoding.Decimal(available.ApprovedUnitPrice),
                    available.CurrencyCode,sourceSubmissionId=source.Submission.Id,sourceCaseId=source.CoreCase.Id,
                    methodId=source.Method.Id,methodRevision=source.Method.Version,
                    canSelect=available.AvailableQuantity>0,canSubmit=false});
            }
            catch(Exception error) when(error is ApprovedDemandLoadException or InvalidOperationException
                or JsonException or OverflowException or ArgumentException)
            {
                items.Add(new{purchaseRequestId=row.Id,purchaseRequestLineId=row.lineId,requestNumber=row.Number,
                    state="Unavailable",canSelect=false,canSubmit=false});
            }
        }
        return new Reply(JsonSerializer.Serialize(new{items,candidateTotal,page=paging.Number,pageSize=paging.Size,
            eligibilityMeaning="CurrentPreviewOnly",canSubmit=false},Json));
    },ct);

    private static Task<IResult> List(int? page,int? pageSize,string? q,string? state,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)=>Run(http,scope,async(db,cases,core,auth,token)=>
    {
        var paging=ProcurementSourcingDiscoveryQueries.Paging(page,pageSize,q);
        if(state is not (null or "Draft" or "Cancelled" or "PendingApproval" or "Approved"
            or "Returned" or "Rejected" or "IssuePending" or "Issued" or "Disputed"))
            throw new Problem("OrderStateInvalid",422);
        var actor=await Actor(core,scope,http,auth,"procurement-po-read",null,false,token);
        var branches=await ProcurementExecutionRecordScope.Branches(core,scope,null,false,token);
        var query=db.PurchaseOrders.AsNoTracking().Where(x=>x.PrincipalDraftSchemaVersion==2
            &&x.BuyerSubjectId==actor.PrincipalId);
        if(scope.WorkLocationId is { } selected)query=query.Where(x=>x.BranchRef==selected);
        if(!actor.OrganisationWide)query=query.Where(x=>x.BranchRef!=null&&branches.Contains(x.BranchRef.Value));
        if(state is not null)query=query.Where(x=>x.Status==state);
        if(paging.Search.Length>0)query=query.Where(x=>EF.Functions.ILike(x.Number,paging.Pattern,"\\"));
        var total=await query.CountAsync(token);
        var rows=await query.OrderByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id)
            .Skip(paging.Offset).Take(paging.Size).Select(x=>new{x.Id,x.Number,x.SupplierId,x.BranchRef,x.CurrencyCode,
                x.Status,x.Version,x.CreatedAt,x.OriginalDirectSourceSha256}).ToArrayAsync(token);
        return new Reply(JsonSerializer.Serialize(new{items=rows,total,page=paging.Number,pageSize=paging.Size,
            executionMode="PrincipalOrderV2",manualFundingOnly=true,supplierSendPerformed=false},Json));
    },ct);

    private static Task<IResult> Detail(Guid id,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var order=await Owned(db,core,scope,http,auth,"procurement-po-read",id,token);
            var lines=await db.PurchaseOrderLines.AsNoTracking().Where(x=>x.PurchaseOrderId==id&&x.IsActive)
                .OrderBy(x=>x.LineNumber).ToArrayAsync(token);
            var bindings=await db.PrincipalOrderSourceBindings.AsNoTracking().Where(x=>x.PurchaseOrderId==id).ToArrayAsync(token);
            PrincipalOrderDraftPins.Verify(order,lines,bindings);
            var ids=bindings.Select(x=>x.AllocationId).ToArray();
            var allocations=await db.PurchaseRequestAllocations.AsNoTracking().Where(x=>ids.Contains(x.Id)).ToArrayAsync(token);
            if(allocations.Length!=bindings.Length || bindings.Any(x=>
                allocations.Count(a=>a.Id==x.AllocationId&&a.TargetType=="PurchaseOrder"&&a.TargetId==id
                    &&a.Quantity==x.Quantity&&a.EstimatedValue==x.EstimatedValue
                    &&a.Status==(order.Status=="Cancelled"?"Released"
                        :order.Status is "IssuePending" or "Issued" or "Disputed"?"Ordered":"Reserved"))!=1))
                throw new Problem("OrderOriginalSourceUnavailable",409);
            return new Reply(JsonSerializer.Serialize(new{order.Id,order.Number,order.SupplierId,
                order.CurrencyCode,order.BranchRef,order.Status,order.Version,order.CreatedAt,
                order.ShipToRef,order.BillToRef,order.PaymentTermRef,order.ExpectedDeliveryOn,
                order.ClosureReason,order.OriginalDirectSourceSha256,
                lines=lines.Select(line=>new{line.Id,line.LineNumber,line.Description,line.UnitCode,
                    quantity=ProcurementEvidenceEncoding.Decimal(line.Quantity),
                    unitPrice=ProcurementEvidenceEncoding.Decimal(line.UnitPrice),
                    taxAmount=ProcurementEvidenceEncoding.Decimal(line.TaxAmount),
                    line.CostCenterRef,line.ProjectRef,line.ExpectedDeliveryOn,
                    line.PurchaseRequestLineId,
                    purchaseRequestId=bindings.Single(x=>x.PurchaseOrderLineId==line.Id).PurchaseRequestId,
                    allocationStatus=allocations.Single(x=>x.Id==bindings.Single(b=>b.PurchaseOrderLineId==line.Id).AllocationId).Status}).ToArray(),
                executionMode="PrincipalOrderV2",manualFundingOnly=true,
                financeReservationChanged=false,supplierSendPerformed=false},Json));
        },ct);

    private static Task<IResult> Review(Guid id,Guid assignmentId,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)=>Run(http,scope,async(db,cases,core,auth,token)=>
    {
        var pin=await db.PrincipalOrderAssignmentEvidence.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.CoreAssignmentId==assignmentId&&x.PrincipalId==scope.SubjectId,token)
            ??throw new Problem("OrderAssignmentNotFound",404);
        var submission=await db.PrincipalOrderSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.Id==pin.SubmissionId&&x.PurchaseOrderId==id,token)
            ??throw new Problem("OrderAssignmentNotFound",404);
        var order=await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id
            &&x.PrincipalDraftSchemaVersion==2,token)??throw new Problem("OrderNotFound",404);
        _=await Actor(core,scope,http,auth,"procurement-po-approve",order.BranchRef,true,token);
        var task=await cases.Assignments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==assignmentId
            &&x.CaseId==submission.CoreApprovalCaseId&&x.StageId==pin.CoreStageId
            &&x.AssigneeSubjectId==scope.SubjectId&&x.PermissionKey=="procurement-po-approve"
            &&x.DelegatedFromSubjectId==null,token)??throw new Problem("OrderAssignmentNotFound",404);
        var workflow=await cases.Cases.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==submission.CoreApprovalCaseId
            &&x.SourceModule=="procurement"&&x.SourceType=="purchase_order"
            &&x.SourceId==id&&x.SourceVersion==1,token)??throw new Problem("OrderApprovalCaseNotFound",409);
        var stage=await cases.Stages.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==task.StageId
            &&x.CaseId==workflow.Id,token)??throw new Problem("OrderAssignmentNotFound",404);
        var funding=await db.PrincipalOrderFundingEvidence.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.Id==submission.FundingEvidenceId&&x.PurchaseOrderId==id,token)
            ??throw new Problem("OrderFundingEvidenceMissing",409);
        var lines=await db.PurchaseOrderLines.AsNoTracking().Where(x=>x.PurchaseOrderId==id&&x.IsActive)
            .OrderBy(x=>x.LineNumber).ToArrayAsync(token);
        var bindings=await db.PrincipalOrderSourceBindings.AsNoTracking().Where(x=>x.PurchaseOrderId==id)
            .ToArrayAsync(token);
        PrincipalOrderDraftPins.Verify(order,lines,bindings);
        if(submission.CommercialSnapshotSha256!=ProcurementEvidenceEncoding.Digest(submission.CommercialSnapshotJson)
            || submission.CommercialSnapshotJson!=funding.CommercialSnapshotJson
            || submission.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
            || pin.CoreApprovalCaseId!=workflow.Id || pin.PrincipalId!=task.AssigneeSubjectId)
            throw new Problem("OrderApprovalEvidenceChanged",409);
        return new Reply(JsonSerializer.Serialize(new{order.Id,order.Number,order.Status,order.Version,
            order.CurrencyCode,order.SupplierId,order.BranchRef,submissionId=submission.Id,
            submission.CommercialRevision,submission.CommercialSnapshotJson,
            lineBaseAmountExact=ProcurementEvidenceEncoding.Decimal(submission.LineBaseAmount),
            grossAmountExact=ProcurementEvidenceEncoding.Decimal(submission.GrossAmount),
            funding.ManualCommitmentReference,funding.ExternalEvidenceReference,
            funding.ApprovedManualPolicyReference,
            coreCaseId=workflow.Id,caseState=workflow.State,caseVersion=workflow.Version,
            assignmentId=task.Id,assignmentState=task.State,assignmentVersion=task.Version,
            canDecide=order.Status=="PendingApproval"&&workflow.State=="Pending"
                &&stage.State=="Active"&&task.State=="Pending",
            financePostingCreated=false,supplierSendPerformed=false},Json));
    },ct);

    private static Task<IResult> Recover(string operation,Guid key,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)=>Run(http,scope,async(db,cases,core,auth,token)=>
    {
        if(key==Guid.Empty)throw new Problem("OrderReceiptNotFound",404);
        var action=operation switch
        {
            "Create"=>"procurement-po-convert",
            "Cancel"=>"procurement-po-edit",
            "Funding"=>"procurement-po-finance-confirm",
            "Submit"=>"procurement-po-submit",
            "Decide"=>"procurement-po-approve",
            "Issue"=>"procurement-po-issue",
            "Ack"=>"procurement-po-dispatch-confirm",
            _=>throw new Problem("OrderReceiptNotFound",404)
        };
        _=await Actor(core,scope,http,auth,action,null,false,token);
        var receipt=await Replay(db,core,scope,http,auth,action,operation,key,null,null,token)
            ??throw new Problem("OrderReceiptNotFound",404);
        return new Reply(JsonSerializer.Serialize(new{operation,commandKey=key,
            originalStatusCode=receipt.Status,responseJson=receipt.Body,recordedHistoricalResult=true,
            historicalOnly=true,financeReservationChanged=false,supplierSendPerformed=false},Json));
    },ct);
}
