using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    private async Task VerifySelectorRetainedScope(Guid principal,Guid invoiceId,Guid orderId,Guid lineId)
    {
        Guid otherBranch; string orderNumber;
        using(var services=Scope(OrderBuyer))
        {
            var core=services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var branch=new WorkLocation { LegalEntityId=OrderCompany,Code="SELECTOR-B-"+Guid.NewGuid().ToString("N"),Name="Synthetic selector B" };
            core.WorkLocations.Add(branch);core.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId=principal,LocationId=branch.Id });
            await core.SaveChangesAsync();otherBranch=branch.Id;
            var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            orderNumber=await db.PurchaseOrders.Where(x=>x.Id==orderId).Select(x=>x.Number).SingleAsync();
            // Owner-only corruption fixture, never runtime authority: sealed issue remains A.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE procurement.purchase_orders SET branch_ref={otherBranch} WHERE tenant_id={"probe"} AND legal_entity_id={OrderCompany} AND id={orderId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE procurement.supplier_invoices SET branch_ref={otherBranch} WHERE tenant_id={"probe"} AND legal_entity_id={OrderCompany} AND id={invoiceId}");
        }
        try
        {
            var prefix=$"{Root}/invoices/{invoiceId}/matching/sources";
            using var response=await Send(HttpMethod.Get,prefix+"?page=1&pageSize=1",principal,selectedLocation:otherBranch);
            await Check(response,HttpStatusCode.OK);var body=await response.Content.ReadFromJsonAsync<JsonElement>();
            Require(body.GetProperty("total").GetInt32()==0 && body.GetProperty("items").GetArrayLength()==0
                && !body.GetRawText().Contains(orderNumber,StringComparison.Ordinal),"Mutable B header leaked original A issued metadata/count before paging.");
            using var detail=await Send(HttpMethod.Get,prefix+$"/{orderId}/lines/{lineId}/acceptances",principal,selectedLocation:otherBranch);
            await Check(detail,HttpStatusCode.NotFound);
        }
        finally
        {
            using var services=Scope(OrderBuyer);var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE procurement.purchase_orders SET branch_ref={OrderBranch} WHERE tenant_id={"probe"} AND legal_entity_id={OrderCompany} AND id={orderId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE procurement.supplier_invoices SET branch_ref={OrderBranch} WHERE tenant_id={"probe"} AND legal_entity_id={OrderCompany} AND id={invoiceId}");
        }
    }
    private async Task VerifySelectorMissingStateHistory(Guid principal,Guid existingOrder)
    {
        Guid supplier;
        using(var services=Scope(OrderBuyer)) supplier=await services.ServiceProvider.GetRequiredService<ProcurementDbContext>().PurchaseOrders.Where(x=>x.Id==existingOrder).Select(x=>x.SupplierId).SingleAsync();
        var source=await DeliveredReceivingOrder(supplier,"Goods");
        using var capture=await Send(HttpMethod.Post,Root+"/invoices",OrderBuyer,new { supplierId=supplier,purchaseOrderId=source.OrderId,
            invoiceNumber="SELECTOR-EMPTY-"+Guid.NewGuid().ToString("N"),invoiceDate=DateOnly.FromDateTime(DateTime.UtcNow),currencyCode="ZMW",
            lines=new[] { new { description="Selector missing-state historical boundary",quantity=1m,unitPrice=100m,taxAmount=0m } } });
        await Check(capture,HttpStatusCode.Created);var invoice=await capture.Content.ReadFromJsonAsync<JsonElement>();var invoiceId=invoice.GetProperty("id").GetGuid();
        var path=$"{Root}/invoices/{invoiceId}/matching/sources/{source.OrderId}/lines/{source.LineId}/acceptances";
        var before=await InvoiceSelectorFootprint(invoiceId);var empty=await Get(path,principal);
        Require(empty.GetProperty("state").GetString()=="NoRecordedAcceptance" && empty.GetProperty("total").GetInt32()==0
            && !empty.GetProperty("canAllocate").GetBoolean(),"Genuinely clean missing-state source is not distinguished from unknown history.");
        Require(before==await InvoiceSelectorFootprint(invoiceId),"Clean empty source read created a zero state or other evidence.");
        using(var services=Scope(OrderBuyer))
        {
            var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var receipt=new GoodsReceipt { PurchaseOrderId=source.OrderId,Number="SELECTOR-LEGACY-"+Guid.NewGuid().ToString("N"),
                ReceiverSubjectId=OrderRequester.ToString("D"),ReceivedAt=DateTimeOffset.UtcNow,Status="Accepted" };
            db.AddScoped(receipt);db.AddScoped(new GoodsReceiptLine { GoodsReceiptId=receipt.Id,PurchaseOrderLineId=source.LineId,
                ReceivedQuantity=.5m,AcceptedQuantity=.5m,Status="Accepted" });await db.SaveChangesAsync();
        }
        before=await InvoiceSelectorFootprint(invoiceId);var unknown=await Get(path,principal);
        Require(unknown.GetProperty("state").GetString()=="Unavailable" && unknown.GetProperty("total").ValueKind==JsonValueKind.Null
            && unknown.GetProperty("items").GetArrayLength()==0 && unknown.GetProperty("blocker").GetString()=="AcceptanceHistoryUnavailable",
            "Legacy accepted rows without a receiving state were misrepresented as proven empty history.");
        Require(before==await InvoiceSelectorFootprint(invoiceId),"Unknown legacy history GET repaired/created state or changed evidence.");
        using(var services=Scope(OrderBuyer))
        {
            var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            Require(!await db.ReceivingSourceStates.AnyAsync(x=>x.PurchaseOrderLineId==source.LineId),"History selector manufactured a zero state.");
        }
        // Independently test a nonzero inactive PO counter with no receipt or state.
        var counter=await DeliveredReceivingOrder(supplier,"Goods");
        using var capture2=await Send(HttpMethod.Post,Root+"/invoices",OrderBuyer,new { supplierId=supplier,purchaseOrderId=counter.OrderId,
            invoiceNumber="SELECTOR-COUNTER-"+Guid.NewGuid().ToString("N"),invoiceDate=DateOnly.FromDateTime(DateTime.UtcNow),currencyCode="ZMW",
            lines=new[] { new { description="Selector inactive historical counter",quantity=1m,unitPrice=100m,taxAmount=0m } } });
        await Check(capture2,HttpStatusCode.Created);var invoice2=await capture2.Content.ReadFromJsonAsync<JsonElement>();var counterInvoice=invoice2.GetProperty("id").GetGuid();
        using(var services=Scope(OrderBuyer))
        {
            var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var line=await db.PurchaseOrderLines.AsNoTracking().SingleAsync(x=>x.Id==counter.LineId);
            db.AddScoped(new PurchaseOrderLine { PurchaseOrderId=counter.OrderId,PurchaseRequestLineId=line.PurchaseRequestLineId,IsActive=false,
                LineNumber=2,Description="Retained inactive exposure",UnitCode="each",Quantity=.25m,UnitPrice=100,AcceptedQuantity=.25m });
            await db.SaveChangesAsync();
        }
        before=await InvoiceSelectorFootprint(counterInvoice);
        var counterResult=await Get($"{Root}/invoices/{counterInvoice}/matching/sources/{counter.OrderId}/lines/{counter.LineId}/acceptances",principal);
        Require(counterResult.GetProperty("state").GetString()=="Unavailable" && counterResult.GetProperty("total").ValueKind==JsonValueKind.Null,
            "Inactive historical accepted counter without state was silently treated as zero exposure.");
        Require(before==await InvoiceSelectorFootprint(counterInvoice),"Inactive history selector changed business evidence.");
        Console.WriteLine("PASS invoice selector source boundaries: retained-A/mutable-B count privacy, genuinely clean empty vs legacy accepted and inactive counter unknown; no GET writes or manufactured state.");
    }
}
