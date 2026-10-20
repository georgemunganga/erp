using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementInvoiceMatchingRoutes
{
    private static (int Page,int Size,string? Pattern) SelectorPage(int? page,int? pageSize,string? q)
    {
        if(page is <1 or >100000 || pageSize is <1 or >25 || q?.Length>120 || q?.Any(char.IsControl)==true)
            throw new MatchingProblem("InvoiceSourceQueryInvalid",422);
        var text=q?.Trim();
        return(page??1,pageSize??25,string.IsNullOrEmpty(text)?null:"%"+text.Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_")+"%");
    }
    private static async Task<InvoiceMatchingPolicy> SelectorPolicy(ProcurementDbContext db,SupplierInvoice invoice,CancellationToken ct)
    {
        var now=Now();
        var policies=await db.PolicyVersions.AsNoTracking().Where(x=>x.PolicyKey==InvoiceMatchingPolicyRules.PolicyKey && x.State=="Published"
            && x.EffectiveFrom<=now && (x.EffectiveTo==null || now<x.EffectiveTo)).Take(2).ToArrayAsync(ct);
        if(policies.Length!=1) throw new MatchingProblem("InvoicePublishedMatchingPolicyUnavailable",409);
        var policy=InvoiceMatchingPolicyRules.Project(policies[0]);
        if(!policy.Rules.Any(x=>x.CurrencyCode==invoice.CurrencyCode)) throw new MatchingProblem("InvoiceMatchingCurrencyUnsupported",409);
        return policy;
    }
    private static async Task<SupplierInvoice> SelectorInvoice(ProcurementDbContext db,HrmDbContext core,ProcurementRequestScope scope,
        HttpContext http,IAuthorizationService auth,Guid id,CancellationToken ct)
    {
        var invoice=await Invoice(db,scope,id,ct);
        _=await Actor(core,scope,http,auth,"procurement-invoice-read",invoice.BranchRef,ct);
        _=await Actor(core,scope,http,auth,"procurement-invoice-match",invoice.BranchRef,ct);
        return invoice;
    }
    private static IQueryable<PurchaseOrder> SelectorOrders(ProcurementDbContext db,SupplierInvoice invoice)
        =>db.PurchaseOrders.AsNoTracking().Where(x=>x.SupplierId==invoice.SupplierId && x.CurrencyCode==invoice.CurrencyCode
            && x.BranchRef==invoice.BranchRef && x.Status=="Issued" && (invoice.PurchaseOrderId==null || x.Id==invoice.PurchaseOrderId));

    private static Task<IResult> Sources(Guid id,int? page,int? pageSize,string? q,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var paging=SelectorPage(page,pageSize,q); var invoice=await SelectorInvoice(db,core,scope,http,auth,id,token);
            var policy=await SelectorPolicy(db,invoice,token);
            var query=from order in SelectorOrders(db,invoice)
                join issued in db.OrderIssuedVersions.AsNoTracking() on new { order.Id,Revision=order.CurrentRevision } equals new { Id=issued.PurchaseOrderId,Revision=issued.RevisionNumber }
                join line in db.PurchaseOrderLines.AsNoTracking() on order.Id equals line.PurchaseOrderId
                where line.IsActive && (paging.Pattern==null || EF.Functions.ILike(order.Number,paging.Pattern,"\\") || EF.Functions.ILike(line.Description,paging.Pattern,"\\"))
                orderby order.Number,order.Id,line.LineNumber,line.Id
                select new { orderId=order.Id,orderNumber=order.Number,issuedVersionId=issued.Id,lineId=line.Id,line.Description };
            var total=await query.CountAsync(token); var rows=await query.Skip((paging.Page-1)*paging.Size).Take(paging.Size).ToArrayAsync(token);
            var items=new List<object>();
            foreach(var row in rows)
            {
                try
                {
                    // Sealed source verification only, not a per-row ledger/exposure reconciliation.
                    var source=await ProcurementReceivingRoutes.InvoiceSelectorLine(db,cases,scope,row.orderId,row.issuedVersionId,row.lineId,token);
                    var rule=policy.Rules.SingleOrDefault(x=>x.CategoryCode==source.CategoryCode && x.CurrencyCode==invoice.CurrencyCode);
                    items.Add(new { row.orderId,row.orderNumber,row.issuedVersionId,purchaseOrderLineId=row.lineId,row.Description,
                        source.UnitCode,source.Quantity,source.UnitPrice,source.TaxAmount,source.CategoryCode,
                        method=rule?.Method.ToString(),acceptanceKind=rule?.AcceptanceKind.ToString(),sourceState="VerifiedIssued",
                        blocker=rule is null?"MatchingCategoryUnavailable":null,canAllocate=false,canPostToFinance=false });
                }
                catch(Exception error) when(error is InvalidOperationException or JsonException or KeyNotFoundException)
                {
                    items.Add(new { row.orderId,row.orderNumber,row.issuedVersionId,purchaseOrderLineId=row.lineId,row.Description,
                        sourceState="Unavailable",blocker="IssuedEvidenceUnavailable",canAllocate=false,canPostToFinance=false });
                }
            }
            return JsonSerializer.Serialize(new { invoiceId=id,invoiceVersion=invoice.Version,policyVersionId=policy.Id,items,total,
                page=paging.Page,pageSize=paging.Size,isAuthority=false,canPostToFinance=false },Json);
        },ct);

    private static Task<IResult> Acceptances(Guid id,Guid orderId,Guid lineId,int? page,int? pageSize,string? q,
        HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var paging=SelectorPage(page,pageSize,q); var invoice=await SelectorInvoice(db,core,scope,http,auth,id,token);
            var policy=await SelectorPolicy(db,invoice,token);
            var order=await SelectorOrders(db,invoice).SingleOrDefaultAsync(x=>x.Id==orderId,token)??throw new MatchingProblem("InvoiceMatchingOrderNotFound",404);
            var issued=await db.OrderIssuedVersions.AsNoTracking().SingleOrDefaultAsync(x=>x.PurchaseOrderId==orderId && x.RevisionNumber==order.CurrentRevision,token)
                ??throw new MatchingProblem("InvoiceIssuedSourceUnavailable",409);
            if(!await db.PurchaseOrderLines.AsNoTracking().AnyAsync(x=>x.Id==lineId && x.PurchaseOrderId==orderId && x.IsActive,token))
                throw new MatchingProblem("InvoiceMatchingLineNotFound",404);
            var source=await ProcurementReceivingRoutes.InvoiceSelectorLine(db,cases,scope,orderId,issued.Id,lineId,token);
            var rule=policy.Rules.SingleOrDefault(x=>x.CategoryCode==source.CategoryCode && x.CurrencyCode==invoice.CurrencyCode);
            if(rule is null) throw new MatchingProblem("InvoiceMatchingCategoryUnavailable",409);
            var goods=from line in db.GoodsReceiptLines.AsNoTracking()
                join header in db.GoodsReceipts.AsNoTracking() on line.GoodsReceiptId equals header.Id
                where line.PurchaseOrderLineId==lineId && line.ExecutionVersion==1 && header.PurchaseOrderId==orderId && header.IssuedBindingId==issued.Id
                    && header.BranchRef==invoice.BranchRef && line.AcceptedQuantity>0
                    && (paging.Pattern==null || EF.Functions.ILike(header.Number,paging.Pattern,"\\"))
                select new { id=line.Id,reference=header.Number,kind="Goods" };
            var services=db.ServiceAcceptances.AsNoTracking().Where(x=>x.PurchaseOrderLineId==lineId && x.ExecutionVersion==1
                && x.SourcePurchaseOrderId==orderId && x.IssuedBindingId==issued.Id && x.CertifiedAt!=null && x.AcceptedQuantity>0
                && (paging.Pattern==null || EF.Functions.ILike(x.DeliverableRef,paging.Pattern,"\\")))
                .Select(x=>new { id=x.Id,reference=x.DeliverableRef,kind="Service" });
            var acceptanceKind=rule.AcceptanceKind?.ToString();
            var query=goods.Concat(services).Where(x=>acceptanceKind==null || x.kind==acceptanceKind);
            var total=await query.CountAsync(token); var rows=await query.OrderBy(x=>x.reference).ThenBy(x=>x.id).Skip((paging.Page-1)*paging.Size).Take(paging.Size).ToArrayAsync(token);
            var state="VerifiedHistorical"; string? blocker=null; var items=new List<object>();
            try
            {
                // One bounded full-ledger bridge per selected source, never one per candidate row.
                await ProcurementReceivingRoutes.RequireInvoiceSelectorHistoryBound(db,orderId,token);
                var history=await ProcurementReceivingRoutes.InvoiceEvidence(db,cases,scope,orderId,issued.Id,[],token);
                if(history.Blockers.Count!=0) throw new InvalidOperationException("HistoricalEvidenceUnavailable");
                if(total==0) state="NoRecordedAcceptance";
                foreach(var row in rows)
                {
                    var fact=history.Acceptances.SingleOrDefault(x=>x.Id==row.id && x.PurchaseOrderLineId==lineId && x.Kind.ToString()==row.kind);
                    if(fact is null || fact.Reversed) throw new InvalidOperationException("HistoricalEvidenceUnavailable");
                    items.Add(new { row.id,row.reference,row.kind,sourceVersion=fact.Version,
                        goodsReceiptLineId=row.kind=="Goods"?(Guid?)row.id:null,serviceAcceptanceId=row.kind=="Service"?(Guid?)row.id:null,
                        acceptedQuantity=Exact(fact.AcceptedQuantity),returnedQuantity=Exact(fact.ReturnedQuantity),
                        historicalNetQuantity=Exact(ExactDecimalArithmetic.Subtract(fact.AcceptedQuantity,fact.ReturnedQuantity)),
                        acceptedNetAmount=Exact(fact.AcceptedNetAmount),returnedNetAmount=Exact(fact.ReturnedNetAmount),
                        historicalNetAmount=Exact(ExactDecimalArithmetic.Subtract(fact.AcceptedNetAmount,fact.ReturnedNetAmount)),
                        fact.AcceptedAt,canAllocate=false,canPostToFinance=false });
                }
            }
            catch(Exception error) when(error is InvalidOperationException or JsonException or KeyNotFoundException)
            { state="Unavailable";blocker="AcceptanceHistoryUnavailable";items.Clear(); }
            return JsonSerializer.Serialize(new { invoiceId=id,invoiceVersion=invoice.Version,orderId,issuedVersionId=issued.Id,purchaseOrderLineId=lineId,
                policyVersionId=policy.Id,method=rule.Method.ToString(),state,blocker,items,total=state=="Unavailable"?(int?)null:total,
                page=paging.Page,pageSize=paging.Size,availabilityMeaning="HistoricalPhysicalFactsOnly",isAuthority=false,canAllocate=false,canPostToFinance=false },Json);
        },ct);
}
