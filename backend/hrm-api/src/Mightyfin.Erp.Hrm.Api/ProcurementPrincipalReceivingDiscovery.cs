using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalReceivingRoutes
{
    /// <summary>A bounded commercial source selector, not a delivery or acceptance
    /// worklist. No source state is created and no absent state is interpreted as zero.</summary>
    private static Task<IResult> Sources(string? search,int? offset,int? limit,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,"procurement-receipt-create",async(db,cases,_,actor,token)=>
        {
            var text=search?.Trim()??"";if(text.Length>160)throw new Problem("ReceivingSearchTooLong",422);
            var start=Math.Clamp(offset??0,0,100_000);var size=Math.Clamp(limit??25,1,25);
            // Publication/readiness precede count. Current configuration grants no
            // receiving action; Run already resolved canonical account/action/sites.
            var policy=await ProcurementPrincipalReceivingPolicy.CurrentRecordAsync(db,scope,http.RequestServices,token);
            var document=PrincipalReceivingPolicyRules.Parse(policy.RulesJson);
            var orders=ProcurementPrincipalOrderRetainedIssued.QueryBoundIssued(db,scope,actor.AllowedBranches)
                .AsNoTracking().Where(x=>x.CurrencyCode==document.CurrencyCode);
            if(text.Length!=0)orders=orders.Where(x=>x.Number.ToLower().Contains(text.ToLower()));
            var candidates=from order in orders
                join issued in db.PrincipalOrderIssuedVersions.AsNoTracking() on order.Id equals issued.PurchaseOrderId
                join binding in db.PrincipalOrderSourceBindings.AsNoTracking() on order.Id equals binding.PurchaseOrderId
                join line in db.PurchaseOrderLines.AsNoTracking() on binding.PurchaseOrderLineId equals line.Id
                where issued.CommercialRevision==1&&line.IsActive&&binding.BranchId==order.BranchRef
                    &&line.PurchaseOrderId==order.Id&&line.PurchaseRequestLineId==binding.PurchaseRequestLineId
                    &&binding.OriginalDirectSourceSha256==order.OriginalDirectSourceSha256
                select new{OrderId=order.Id,OrderNumber=order.Number,IssuedId=issued.Id,LineId=line.Id,line.LineNumber};
            // This is explicitly candidate count, not promised operational capacity.
            // Full retained proof + configured classification is checked below and
            // again by every command. A bad candidate aborts; it is never serialized
            // as a misleading ready/zero-capacity item.
            var total=await candidates.CountAsync(token);
            var page=await candidates.OrderBy(x=>x.OrderNumber).ThenBy(x=>x.OrderId).ThenBy(x=>x.LineNumber)
                .ThenBy(x=>x.LineId).Skip(start).Take(size).ToArrayAsync(token);
            var records=new List<object>();
            foreach(var row in page)
            {
                var source=await NewSource(db,cases,scope,http,actor,row.OrderId,row.IssuedId,row.LineId,token,false);
                var pin=source.Commercial;
                var state=await db.PrincipalReceivingSourceStates.SingleOrDefaultAsync(x=>x.IssuedVersionId==pin.IssuedVersionId&&x.PurchaseOrderLineId==pin.PurchaseOrderLineId,token);
                string history;string? accepted=null,net=null,remaining=null;long? sequence=null;
                if(state is not null)
                {
                    var loaded=await StoredSource(db,cases,scope,actor,source,false,token);
                    var balance=PrincipalControlledReceiving.Balance(loaded.Ledger);history="Recorded";
                    accepted=Exact(balance.AcceptedQuantity);net=Exact(balance.AcceptedNetAmount);remaining=Exact(balance.RemainingQuantity);sequence=loaded.State.Sequence;
                }
                else
                {
                    // Existing legacy receiving or counters require owner reconciliation;
                    // they cannot disappear behind an empty new-ledger query.
                    var legacy=await new Mightyfin.Erp.Procurement.Infrastructure.Data.PrincipalReceivingStore(db).HasLegacyHistoryAsync(pin.PurchaseOrderLineId,token)
                        ||await db.PurchaseOrderLines.AnyAsync(x=>x.Id==pin.PurchaseOrderLineId&&x.AcceptedQuantity!=0,token);
                    history=legacy?"RequiresReconciliation":"NoSuccessorHistory";
                }
                records.Add(new{purchaseOrderId=pin.PurchaseOrderId,purchaseOrderNumber=row.OrderNumber,issuedVersionId=pin.IssuedVersionId,
                    purchaseOrderLineId=pin.PurchaseOrderLineId,lineNumber=row.LineNumber,kind=pin.Kind.ToString(),siteId=pin.BranchId,
                    pin.CategoryCode,pin.PurchaseType,pin.CurrencyCode,pin.UnitCode,authorizedQuantity=Exact(pin.AuthorizedQuantity),netUnitPrice=Exact(pin.NetUnitPrice),
                    historyState=history,sourceVersion=sequence,currentAcceptedQuantity=accepted,currentAcceptedNetAmount=net,remainingQuantity=remaining,
                    supplierAcknowledgmentIsDelivery=false,actionCandidatesAreAuthority=false});
            }
            return new Reply(200,System.Text.Json.JsonSerializer.Serialize(new{records,total,offset=start,limit=size,executionVersion=2,
                countMeaning="ScopedCommercialSourceCandidates",isAuthority=false},Json));
        },ct);
}
