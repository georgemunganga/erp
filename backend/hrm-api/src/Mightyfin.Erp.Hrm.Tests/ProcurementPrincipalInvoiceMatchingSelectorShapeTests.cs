using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalInvoiceMatchingSelectorShapeTests
{
    private static InvoiceCategoryMatchRule GoodsRule()
    {
        var exact=new InvoiceFieldTolerance(0,0,InvoiceToleranceCombination.AllLimits);
        return new InvoiceCategoryMatchRule("GOODS","ZMW",InvoiceMatchingMethod.ThreeWay,
            InvoiceAcceptanceKind.Goods,exact,exact,exact,exact);
    }

    private static PrincipalReceivingSource Source(ReceivingKind kind)
    {
        var at=new DateTimeOffset(2026,10,30,1,0,0,TimeSpan.Zero);
        var pin=new ReceivingSourcePin("tenant",Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),
            Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,"issued","source","category",
            "classification",kind,"GOODS","Goods","ZMW","EACH",1,5.1234m,"Unit");
        var actor=new PrincipalReceivingPerson(Guid.NewGuid().ToString("D"),null);
        return new PrincipalReceivingSource(2,pin,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),
            Guid.NewGuid(),"original",actor,actor,at,at,Guid.NewGuid());
    }

    [Fact]
    public void Mixed_verified_goods_and_service_sources_keep_page_and_block_only_incompatible_row()
    {
        var goodsState=new PrincipalReceivingSourceState { Id=Guid.NewGuid() };
        var serviceState=new PrincipalReceivingSourceState { Id=Guid.NewGuid() };
        var sources=new Dictionary<Guid,PrincipalReceivingSource> {
            [goodsState.Id]=Source(ReceivingKind.Goods),
            [serviceState.Id]=Source(ReceivingKind.Service)
        };
        var rule=GoodsRule();
        var goodsBlocker=ProcurementInvoiceMatchingRoutes.PrincipalSourceBlocker(rule,goodsState,sources);
        var serviceBlocker=ProcurementInvoiceMatchingRoutes.PrincipalSourceBlocker(rule,serviceState,sources);
        Assert.Null(goodsBlocker);
        Assert.Equal("InvoiceMatchingAcceptanceKindChanged",serviceBlocker);

        var line=new InvoiceIssuedOrderLine(Guid.NewGuid(),1,"GOODS","EACH",1,5.1234m,0);
        var items=new[] {
            ProcurementInvoiceMatchingRoutes.PrincipalSourceItem(Guid.NewGuid(),"PO-G",Guid.NewGuid(),
                line.Id,"Goods",line,rule,"tenant",Guid.NewGuid(),Guid.NewGuid(),goodsState,goodsBlocker),
            ProcurementInvoiceMatchingRoutes.PrincipalSourceItem(Guid.NewGuid(),"PO-S",Guid.NewGuid(),
                line.Id,"Service",line,rule,"tenant",Guid.NewGuid(),Guid.NewGuid(),serviceState,serviceBlocker)
        };
        using var json=JsonDocument.Parse(ProcurementInvoiceMatchingRoutes.PrincipalSourcePage(
            Guid.NewGuid(),3,Guid.NewGuid(),items,2,1,25));
        var page=json.RootElement;
        Assert.Equal(2,page.GetProperty("total").GetInt32());
        Assert.Equal(1,page.GetProperty("page").GetInt32());
        Assert.Equal(25,page.GetProperty("pageSize").GetInt32());
        var rows=page.GetProperty("items");
        Assert.Equal(2,rows.GetArrayLength());
        Assert.Equal(JsonValueKind.Null,rows[0].GetProperty("blocker").ValueKind);
        Assert.Equal("InvoiceMatchingAcceptanceKindChanged",rows[1].GetProperty("blocker").GetString());
        Assert.Equal("VerifiedIssued",rows[1].GetProperty("sourceState").GetString());
        Assert.False(rows[0].GetProperty("canAllocate").GetBoolean());
        Assert.False(rows[1].GetProperty("canAllocate").GetBoolean());
    }

    [Fact]
    public void Chosen_wrong_kind_still_fails_and_missing_or_malformed_witness_never_becomes_blocked_row()
    {
        var state=new PrincipalReceivingSourceState { Id=Guid.NewGuid() };
        var rule=GoodsRule();
        var service=new Dictionary<Guid,PrincipalReceivingSource> { [state.Id]=Source(ReceivingKind.Service) };
        var wrongKind=Assert.ThrowsAny<Exception>(()=>
            ProcurementInvoiceMatchingRoutes.RequirePrincipalAcceptanceKind(rule,state,service));
        Assert.Equal("InvoiceMatchingAcceptanceKindChanged",wrongKind.Message);
        var missing=Assert.ThrowsAny<Exception>(()=>
            ProcurementInvoiceMatchingRoutes.PrincipalSourceBlocker(rule,state,
                new Dictionary<Guid,PrincipalReceivingSource>()));
        Assert.Equal("InvoiceIssuedSourceUnavailable",missing.Message);
        var malformed=new Dictionary<Guid,PrincipalReceivingSource> { [state.Id]=Source((ReceivingKind)999) };
        var invalid=Assert.ThrowsAny<Exception>(()=>
            ProcurementInvoiceMatchingRoutes.PrincipalSourceBlocker(rule,state,malformed));
        Assert.Equal("InvoiceIssuedSourceUnavailable",invalid.Message);
    }

    [Fact]
    public void Issued_line_without_receiving_state_remains_a_two_way_source_candidate()
    {
        var lineId=Guid.NewGuid();
        var line=new InvoiceIssuedOrderLine(lineId,1,"GOODS","EACH",2.5m,10.25m,1.125m);
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(
            ProcurementInvoiceMatchingRoutes.PrincipalSourceItem(Guid.NewGuid(),"PO-1",
                Guid.NewGuid(),lineId,"Line",line,null,"tenant",Guid.NewGuid(),Guid.NewGuid(),null),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var row=json.RootElement;
        Assert.Equal(3,row.GetProperty("sourceSchemaVersion").GetInt32());
        Assert.Equal(lineId,row.GetProperty("purchaseOrderLineId").GetGuid());
        Assert.Equal("2.5",row.GetProperty("quantity").GetString());
        Assert.Equal("10.25",row.GetProperty("unitPrice").GetString());
        Assert.Equal(JsonValueKind.Null,row.GetProperty("principalReceivingSourceStateId").ValueKind);
        Assert.Equal(1,row.GetProperty("issuedLineVersion").GetInt64());
        Assert.Equal("VerifiedIssued",row.GetProperty("sourceState").GetString());
        Assert.False(row.GetProperty("canAllocate").GetBoolean());
        Assert.False(row.GetProperty("canPostToFinance").GetBoolean());
        Assert.False(row.TryGetProperty("goodsReceiptLineId",out _));
        Assert.False(row.TryGetProperty("serviceAcceptanceId",out _));
    }

    [Fact]
    public void Accepted_fact_selector_exposes_only_real_principal_identifiers()
    {
        var factId=Guid.NewGuid();var sourceId=Guid.NewGuid();
        var at=new DateTimeOffset(2026,10,28,10,0,0,TimeSpan.Zero);
        var fact=new PrincipalInvoiceAcceptedFact(factId,sourceId,3,Guid.NewGuid(),
            Guid.NewGuid(),Guid.NewGuid(),ReceivingKind.Goods,2m,20m,new string('a',64),at);
        var accepted=new PrincipalReceivingAcceptance(factId,Guid.NewGuid(),null!,
            null,null,2m,20m,new PrincipalReceivingPerson(Guid.NewGuid().ToString("D"),null),
            Guid.NewGuid(),new string('b',64),"MANUAL-REF",null,null,null,
            Guid.NewGuid(),3,at);
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(
            ProcurementInvoiceMatchingRoutes.PrincipalAcceptanceItem(fact,accepted),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var row=json.RootElement;
        Assert.Equal(3,row.GetProperty("sourceSchemaVersion").GetInt32());
        Assert.Equal(sourceId,row.GetProperty("principalReceivingSourceStateId").GetGuid());
        Assert.Equal(factId,row.GetProperty("principalReceivingFactId").GetGuid());
        Assert.Equal(new string('a',64),row.GetProperty("factPayloadSha256").GetString());
        Assert.Equal(JsonValueKind.Null,row.GetProperty("goodsReceiptLineId").ValueKind);
        Assert.Equal(JsonValueKind.Null,row.GetProperty("serviceAcceptanceId").ValueKind);
        Assert.Equal("2",row.GetProperty("acceptedQuantity").GetString());
        Assert.Equal("20",row.GetProperty("acceptedNetAmount").GetString());
        Assert.True(row.GetProperty("returnProposalHasNoCapacityEffect").GetBoolean());
        Assert.False(row.GetProperty("canAllocate").GetBoolean());
        Assert.False(row.GetProperty("canPostToFinance").GetBoolean());
    }
}
