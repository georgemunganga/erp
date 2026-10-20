using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

sealed partial class Probe
{
    private async Task VerifyInvoiceSourceSelectors(Guid invoiceId,Guid orderId,Guid lineId,Guid issuedId,Guid receiptLineId)
    {
        Guid principal;
        using(var services=Scope(OrderBuyer))
        {
            var core=services.ServiceProvider.GetRequiredService<HrmDbContext>();
            core.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey="matching_selector",RoleName="Synthetic invoice-purpose matcher",Active=true,
                PermissionsCsv="procurement-access,procurement-invoice-read,procurement-invoice-match" });
            var worker=new Worker { EmployeeNo="HTTP-invoice-selector",FirstName="Invoice",LastName="Selector fixture",Status="active",LocationId=OrderBranch };
            var user=new LocalUser { Email="invoice-selector@example.test",NormalizedEmail="INVOICE-SELECTOR@EXAMPLE.TEST",DisplayName="Invoice selector",
                PasswordHash="synthetic-no-login-secret",WorkerId=worker.Id,RolesCsv="matching_selector" };
            core.Workers.Add(worker);core.LocalUsers.Add(user);core.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId=user.Id,LocationId=OrderBranch });
            await core.SaveChangesAsync();principal=user.Id;
        }
        var context=await Get(Root+"/context",principal);
        var permissions=context.GetProperty("permissions").EnumerateArray().Select(x=>x.GetString()).ToHashSet();
        Require(!permissions.Contains("procurement-po-read") && !permissions.Contains("procurement-receipt-view") && !permissions.Contains("procurement-invoice-capture"),
            "Invoice selector fixture gained broad read or mapping write authority.");
        var before=await InvoiceSelectorFootprint(invoiceId);
        var prefix=$"{Root}/invoices/{invoiceId}/matching/sources";
        var sources=await Get(prefix+"?page=1&pageSize=25",principal);
        Require(sources.GetProperty("items").EnumerateArray().All(x=>x.GetProperty("orderId").GetGuid()==orderId),
            "Invoice selector escaped original intake PO binding.");
        var selected=sources.GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("purchaseOrderLineId").GetGuid()==lineId);
        Require(selected.GetProperty("orderId").GetGuid()==orderId && selected.GetProperty("issuedVersionId").GetGuid()==issuedId
            && selected.GetProperty("sourceState").GetString()=="VerifiedIssued" && selected.GetProperty("method").GetString()=="ThreeWay"
            && !selected.GetProperty("canAllocate").GetBoolean(),"Invoice source selector lost scoped sealed source or fabricated authority.");
        var literal=await Get(prefix+"?q=%25_%5C&pageSize=25",principal);
        Require(literal.GetProperty("total").GetInt32()==0,"Search metacharacters were treated as unescaped wildcards.");
        await Expect(HttpStatusCode.UnprocessableEntity,HttpMethod.Get,prefix+"?pageSize=26",principal,null);
        await Expect(HttpStatusCode.UnprocessableEntity,HttpMethod.Get,prefix+"?q="+new string('x',121),principal,null);
        await Expect(HttpStatusCode.NotFound,HttpMethod.Get,$"{Root}/invoices/{Guid.NewGuid()}/matching/sources",principal,null);
        var acceptancePath=prefix+$"/{orderId}/lines/{lineId}/acceptances";
        var accepted=await Get(acceptancePath,principal);
        Require(accepted.GetProperty("state").GetString()=="VerifiedHistorical"
            && accepted.GetProperty("availabilityMeaning").GetString()=="HistoricalPhysicalFactsOnly"
            && !accepted.GetProperty("canAllocate").GetBoolean() && !accepted.GetProperty("canPostToFinance").GetBoolean(),
            "Invoice acceptance selector claimed allocation/Finance availability.");
        string expectedQuantity,expectedNet;
        using(var services=Scope(OrderBuyer))
        {
            var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var effects=await db.ReceivingAcceptanceEffects.AsNoTracking().Where(x=>x.GoodsReceiptLineId==receiptLineId).ToArrayAsync();
            expectedQuantity=ProcurementEvidenceEncoding.Decimal(ExactDecimalArithmetic.Sum(effects.Select(x=>x.QuantityDelta)));
            expectedNet=ProcurementEvidenceEncoding.Decimal(ExactDecimalArithmetic.Sum(effects.Select(x=>x.NetAmountDelta)));
        }
        var fact=accepted.GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("id").GetGuid()==receiptLineId);
        Require(fact.GetProperty("kind").GetString()=="Goods" && fact.GetProperty("historicalNetQuantity").GetString()==expectedQuantity
            && fact.GetProperty("historicalNetAmount").GetString()==expectedNet,"Invoice acceptance selector changed exact recorded physical facts.");
        foreach(var payload in new[] { sources,accepted })
        {
            var json=payload.GetRawText();
            Require(!json.Contains("allocations",StringComparison.OrdinalIgnoreCase) && !json.Contains("bank",StringComparison.OrdinalIgnoreCase)
                && !json.Contains("policyRules",StringComparison.OrdinalIgnoreCase) && !json.Contains("OwnerWatermark",StringComparison.OrdinalIgnoreCase),
                "Invoice-purpose selectors leaked unrelated commercial/owner facts.");
        }
        await Expect(HttpStatusCode.NotFound,HttpMethod.Get,prefix+$"/{Guid.NewGuid()}/lines/{lineId}/acceptances",principal,null);
        await Expect(HttpStatusCode.NotFound,HttpMethod.Get,prefix+$"/{orderId}/lines/{Guid.NewGuid()}/acceptances",principal,null);
        Require(before==await InvoiceSelectorFootprint(invoiceId),"Selector GET created receiving/mapping/claim/audit/outbox evidence.");
        Console.WriteLine("PASS invoice-purpose selectors: finite explicit matching/read without PO/receipt/capture grants, exact scoped issued/physical facts, escaped search, page bounds, unknown source denials and zero GET writes.");
    }
    private async Task<string> InvoiceSelectorFootprint(Guid id)
    {
        var invoice=await InvoiceApReadFootprint(id);
        using var services=Scope(OrderBuyer);var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        return JsonSerializer.Serialize(new { invoice,states=await db.ReceivingSourceStates.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            effects=await db.ReceivingAcceptanceEffects.CountAsync(),receipts=await db.GoodsReceipts.CountAsync(),services=await db.ServiceAcceptances.CountAsync() },Json);
    }
}
