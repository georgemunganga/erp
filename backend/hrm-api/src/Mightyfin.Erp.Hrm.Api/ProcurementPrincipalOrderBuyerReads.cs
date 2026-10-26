using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalOrderDraftRoutes
{
    internal static bool GovernedBuyerState(string? state) => state is null or "Draft" or "PendingApproval"
        or "Approved" or "Returned" or "Rejected" or "IssuePending" or "Issued" or "Disputed"
        or "ReleasePending" or "Cancelled";

    private static Task<IResult> GovernedBuyerList(int? page,int? pageSize,string? q,string? state,
        HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>RunGoverned(http,scope,async(db,cases,core,auth,token)=>
    {
        if(!GovernedBuyerState(state))throw new Problem("OrderStateInvalid",422);
        var paging=ProcurementSourcingDiscoveryQueries.Paging(page,pageSize,q);
        var actor=await Actor(core,scope,http,auth,"procurement-po-read",null,false,token);
        var branches=await ProcurementExecutionRecordScope.Branches(core,scope,null,false,token);
        var query=db.PurchaseOrders.AsNoTracking().Where(x=>x.PrincipalDraftSchemaVersion==2
            &&x.BuyerSubjectId==actor.PrincipalId);
        if(scope.WorkLocationId is { } selected)query=query.Where(x=>x.BranchRef==selected);
        if(!actor.OrganisationWide)query=query.Where(x=>x.BranchRef!=null&&branches.Contains(x.BranchRef.Value));
        if(state is not null)query=query.Where(x=>x.Status==state);
        if(paging.Search.Length>0)query=query.Where(x=>EF.Functions.ILike(x.Number,paging.Pattern,"\\"));
        var total=await query.CountAsync(token);
        var items=await query.OrderByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id)
            .Skip(paging.Offset).Take(paging.Size).Select(x=>new{x.Id,x.Number,x.SupplierId,
                x.BranchRef,x.CurrencyCode,x.Status,x.Version,x.CurrentRevision,x.CreatedAt,
                x.OriginalDirectSourceSha256}).ToArrayAsync(token);
        return new Reply(JsonSerializer.Serialize(new{items,total,page=paging.Number,pageSize=paging.Size,
            schemaVersion=2,readOnlyPreview=true},Json));
    },ct);

    private static Task<IResult> GovernedBuyerDetail(Guid id,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)=>RunGoverned(http,scope,async(db,cases,core,auth,token)=>
    {
        // Retain the original buyer fence. Finance and assigned reviewers use
        // their own purpose-specific views and do not gain general PO access.
        _=await Owned(db,core,scope,http,auth,"procurement-po-read",id,token);
        var load=await LoadGoverned(db,cases,core,scope,http,auth,"procurement-po-read",id,token,lockSources:false);
        var order=load.Order;
        if(!GovernedBuyerState(order.Status))throw new Problem("OrderStateNeedsReview",409);
        var expectedAllocation=order.Status switch
        {
            "Cancelled"=>"Released", "IssuePending" or "Issued" or "Disputed"=>"Ordered", _=>"Reserved"
        };
        if(load.Sources.Any(x=>x.Allocation.Status!=expectedAllocation))
            throw new Problem("OrderOriginalSourceUnavailable",409);
        var funding=await db.PrincipalOrderFundingEvidence.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.PurchaseOrderId==id&&x.CommercialRevision==1,token);
        var submission=await db.PrincipalOrderSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.PurchaseOrderId==id&&x.CommercialRevision==1,token);
        var workflow=submission is null?null:await cases.Cases.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.Id==submission.CoreApprovalCaseId&&x.SourceId==id
                &&x.SourceModule=="procurement"&&x.SourceType=="purchase_order"&&x.SourceVersion==1,token);
        var issued=await db.PrincipalOrderIssuedVersions.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.PurchaseOrderId==id,token);
        var acknowledgment=await db.PrincipalOrderAcknowledgments.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.PurchaseOrderId==id,token);
        var release=await db.PrincipalOrderReleaseRequests.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.PurchaseOrderId==id,token);
        var releaseConfirmation=await db.PrincipalOrderReleaseConfirmations.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.PurchaseOrderId==id,token);
        if(submission is not null && (workflow is null || funding is null
                || submission.FundingEvidenceId!=funding.Id
                || submission.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256)
            || issued is not null && (submission is null || issued.SubmissionId!=submission.Id
                || issued.FundingEvidenceId!=funding?.Id)
            || acknowledgment is not null && acknowledgment.IssuedVersionId!=issued?.Id
            || release is not null && release.FundingEvidenceId!=funding?.Id
            || releaseConfirmation is not null && releaseConfirmation.ReleaseRequestId!=release?.Id)
            throw new Problem("OrderGovernanceEvidenceChanged",409);
        var now=DateTimeOffset.UtcNow;
        var options=http.RequestServices.GetRequiredService<ProcurementOrderExecutionOptions>();
        async Task<bool> Can(string action)
        {
            if(!options.PrincipalExecutionEnabled)return false;
            try{_=await Actor(core,scope,http,auth,action,order.BranchRef,true,token);return true;}
            catch(Problem){return false;}
        }
        async Task<bool> Eligible(Func<Task> check)
        {
            try{await check();return true;}
            catch(Exception error) when(error is Problem or InvalidOperationException
                or ApprovedDemandLoadException or JsonException or ArgumentException or OverflowException)
            {return false;}
        }
        var canSubmit=order.Status=="Draft"&&order.CurrentRevision==0&&funding is not null
            &&submission is null&&release is null&&await Can("procurement-po-submit")
            &&await Eligible(async()=>{_=await PrepareSubmission(db,core,scope,http,auth,load,now,token);});
        var canIssue=order.Status=="Approved"&&order.CurrentRevision==1&&issued is null&&release is null
            &&await Can("procurement-po-issue")
            &&await Eligible(async()=>{_=await PrepareIssue(db,cases,core,scope,http,load,now,false,token);});
        var canRequestRelease=funding is not null&&release is null&&issued is null
            &&(order.Status is "Draft" or "PendingApproval" or "Approved" or "Returned" or "Rejected")
            &&await Can("procurement-po-edit");
        var canCancelDraft=order.Status=="Draft"&&order.CurrentRevision==0&&funding is null
            &&submission is null&&release is null&&await Can("procurement-po-edit");
        var canRecordAcknowledgment=order.Status=="IssuePending"&&issued is not null
            &&acknowledgment is null&&options.HasApprovedDeliveryPolicy
            &&await Can("procurement-po-dispatch-confirm");
        var labels=await Labels(db,core,order,scope,token);
        var totals=PurchaseOrderCommercialWorkflow.Totals(load.Lines);
        return new Reply(JsonSerializer.Serialize(new{schemaVersion=2,order.Id,order.Number,order.Status,
            order.Version,order.CurrentRevision,order.OriginalDirectSourceSha256,order.SupplierId,
            labels.SupplierDisplayName,order.BranchRef,labels.BranchDisplayName,order.CurrencyCode,
            order.ShipToRef,order.BillToRef,order.PaymentTermRef,order.ExpectedDeliveryOn,
            grossAmountExact=ProcurementEvidenceEncoding.Decimal(totals.Gross),
            lines=load.Lines.Select(line=>new{line.Id,line.LineNumber,line.Description,line.UnitCode,
                quantity=ProcurementEvidenceEncoding.Decimal(line.Quantity),
                unitPrice=ProcurementEvidenceEncoding.Decimal(line.UnitPrice),
                taxAmount=ProcurementEvidenceEncoding.Decimal(line.TaxAmount),
                line.CostCenterRef,line.ProjectRef,line.ExpectedDeliveryOn,line.PurchaseRequestLineId,
                purchaseRequestId=load.Bindings.Single(x=>x.PurchaseOrderLineId==line.Id).PurchaseRequestId,
                allocationStatus=load.Sources.Single(x=>x.OrderLine.Id==line.Id).Allocation.Status}).ToArray(),
            funding=funding is null?null:new{funding.Id,funding.ManualCommitmentReference,
                funding.ExternalEvidenceReference,funding.ApprovedManualPolicyReference,funding.ObservedAt},
            submission=submission is null?null:new{submission.Id,submission.CoreApprovalCaseId,
                submission.SubmittedAt,caseState=workflow!.State,caseVersion=workflow.Version},
            issuedVersion=issued is null?null:new{issued.Id,issued.AuthorizedAt},
            acknowledgment=acknowledgment is null?null:new{acknowledgment.Id,acknowledgment.Outcome,
                acknowledgment.Channel,acknowledgment.SupplierReference,acknowledgment.ObservedAt},
            release=release is null?null:new{release.Id,release.Reason,release.RequestedAt,
                confirmed=releaseConfirmation is not null},
            canSubmit,canIssue,canCancelDraft,canRequestRelease,canRecordAcknowledgment,
            allowedDeliveryChannels=options.HasApprovedDeliveryPolicy?options.AllowedDeliveryChannels:[],
            actionEligibilityIsCurrentPreview=true,actionsRevalidatedOnSave=true,
            manualExternalEvidenceOnly=true,financeLedgerChanged=false,supplierSendPerformed=false},Json));
    },ct);
}
