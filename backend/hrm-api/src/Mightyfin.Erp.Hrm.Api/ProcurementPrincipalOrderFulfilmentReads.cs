using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalOrderDraftRoutes
{
    private static string FulfilmentAction(string? view)=>view switch
    {
        "issue"=>"procurement-po-issue",
        "ack"=>"procurement-po-dispatch-confirm",
        _=>throw new Problem("OrderWorkViewInvalid",422)
    };

    // Separate purpose directory for independent issuer/supplier-response roles.
    // A buyer-only PO detail would require an unrelated permission and cannot
    // supply the exact retained issued version to an acknowledgment form.
    private static Task<IResult> FulfilmentWork(string? view,int? page,int? pageSize,string? q,
        HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
    {
        try{GovernedEnabled(http);}catch(Problem p){return Task.FromResult(Error(p.Message,p.Status));}
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var action=FulfilmentAction(view);
            var paging=ProcurementSourcingDiscoveryQueries.Paging(page,pageSize,q);
            _=await Actor(core,scope,http,auth,action,null,false,token);
            var branches=await CurrentTaskBranches(core,scope,token);
            var status=view=="issue"?"Approved":"IssuePending";
            var query=db.PurchaseOrders.AsNoTracking().Where(x=>x.PrincipalDraftSchemaVersion==2
                &&x.Status==status&&x.BranchRef!=null&&branches.Contains(x.BranchRef.Value)
                &&x.OriginalDirectSourceSha256!=null);
            if(paging.Search.Length>0)query=query.Where(x=>EF.Functions.ILike(x.Number,paging.Pattern,"\\"));
            var total=await query.CountAsync(token);
            var orders=await query.OrderBy(x=>x.CreatedAt).ThenBy(x=>x.Id)
                .Skip(paging.Offset).Take(paging.Size).ToArrayAsync(token);
            var items=new List<object>(orders.Length);
            foreach(var order in orders)
            {
                var lines=await db.PurchaseOrderLines.AsNoTracking().Where(x=>x.PurchaseOrderId==order.Id&&x.IsActive)
                    .ToArrayAsync(token);
                var bindings=await db.PrincipalOrderSourceBindings.AsNoTracking()
                    .Where(x=>x.PurchaseOrderId==order.Id).ToArrayAsync(token);
                PrincipalOrderDraftPins.Verify(order,lines,bindings);
                var issued=await db.PrincipalOrderIssuedVersions.AsNoTracking()
                    .SingleOrDefaultAsync(x=>x.PurchaseOrderId==order.Id,token);
                if(view=="issue"&&issued is not null || view=="ack"&&(issued is null
                    || issued.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256))
                    throw new Problem("OrderWorkEvidenceChanged",409);
                items.Add(new{order.Id,order.Number,order.Status,order.Version,order.SupplierId,
                    order.BranchRef,order.CurrencyCode,issuedVersionId=issued?.Id,
                    task=view=="issue"?"Authorize internal order version":"Record supplier response"});
            }
            return new Reply(JsonSerializer.Serialize(new{items,total,page=paging.Number,pageSize=paging.Size,
                view,externalSupplierSendPerformed=false,financePostingCreated=false},Json));
        },ct);
    }

    private static Task<IResult> FulfilmentDetail(Guid id,string? view,HttpContext http,
        ProcurementRequestScope scope,CancellationToken ct)
    {
        try{GovernedEnabled(http);}catch(Problem p){return Task.FromResult(Error(p.Message,p.Status));}
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var action=FulfilmentAction(view);
            var load=await LoadGoverned(db,cases,core,scope,http,auth,action,id,token,lockSources:false);
            var order=load.Order;
            if(order.Status!=(view=="issue"?"Approved":"IssuePending"))
                throw new Problem("OrderWorkNotFound",404);
            var submission=await db.PrincipalOrderSubmissions.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.PurchaseOrderId==id&&x.CommercialRevision==1,token)
                ??throw new Problem("OrderSubmissionMissing",409);
            var funding=await db.PrincipalOrderFundingEvidence.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.Id==submission.FundingEvidenceId&&x.PurchaseOrderId==id,token)
                ??throw new Problem("OrderFundingMissing",409);
            var issued=await db.PrincipalOrderIssuedVersions.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.PurchaseOrderId==id,token);
            if(submission.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                || funding.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                || funding.CommercialSnapshotJson!=submission.CommercialSnapshotJson
                || submission.CommercialSnapshotSha256!=ProcurementEvidenceEncoding.Digest(submission.CommercialSnapshotJson)
                || funding.CommercialSnapshotSha256!=submission.CommercialSnapshotSha256
                || funding.AttestedGrossAmount!=submission.GrossAmount
                || view=="issue"&&issued is not null
                || view=="ack"&&(issued is null || issued.CommercialRevision!=1
                    || issued.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                    || issued.CommercialSnapshotSha256!=submission.CommercialSnapshotSha256
                    || issued.SubmissionId!=submission.Id||issued.FundingEvidenceId!=funding.Id))
                throw new Problem("OrderWorkEvidenceChanged",409);
            var policy=http.RequestServices.GetRequiredService<ProcurementOrderExecutionOptions>();
            var labels=await Labels(db,core,order,scope,token);
            return new Reply(JsonSerializer.Serialize(new{order.Id,order.Number,order.Status,order.Version,
                order.SupplierId,order.BranchRef,labels.SupplierDisplayName,labels.BranchDisplayName,
                order.CurrencyCode,order.OriginalDirectSourceSha256,
                grossAmountExact=ProcurementEvidenceEncoding.Decimal(submission.GrossAmount),
                lines=load.Lines.OrderBy(x=>x.LineNumber).Select(line=>new{line.Id,line.LineNumber,
                    line.Description,line.UnitCode,quantity=ProcurementEvidenceEncoding.Decimal(line.Quantity),
                    unitPrice=ProcurementEvidenceEncoding.Decimal(line.UnitPrice),
                    taxAmount=ProcurementEvidenceEncoding.Decimal(line.TaxAmount)}).ToArray(),
                funding.ManualCommitmentReference,funding.ExternalEvidenceReference,
                funding.ApprovedManualPolicyReference,submissionId=submission.Id,
                issuedVersionId=issued?.Id,issuedAuthorizedAt=issued?.AuthorizedAt,
                allowedManualResponseChannels=view=="ack"&&policy.HasApprovedDeliveryPolicy
                    ?policy.AllowedDeliveryChannels:[],
                canIssue=view=="issue",canRecordSupplierResponse=view=="ack"&&policy.HasApprovedDeliveryPolicy,
                supplierSendPerformed=false,financePostingCreated=false},Json));
        },ct);
    }
}
