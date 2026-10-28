using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalInvoiceMatchingSelectorShapeTests
{
    [Fact]
    public void Issued_line_without_receiving_state_remains_a_two_way_source_candidate()
    {
        var lineId=Guid.NewGuid();
        var line=new InvoiceIssuedOrderLine(lineId,1,"GOODS","EACH",2.5m,10.25m,1.125m);
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(
            ProcurementInvoiceMatchingRoutes.PrincipalSourceItem(Guid.NewGuid(),"PO-1",
                Guid.NewGuid(),lineId,"Line",line,null,null),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var row=json.RootElement;
        Assert.Equal(3,row.GetProperty("sourceSchemaVersion").GetInt32());
        Assert.Equal(lineId,row.GetProperty("purchaseOrderLineId").GetGuid());
        Assert.Equal("2.5",row.GetProperty("quantity").GetString());
        Assert.Equal("10.25",row.GetProperty("unitPrice").GetString());
        Assert.Equal(JsonValueKind.Null,row.GetProperty("principalSourceStateId").ValueKind);
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
        Assert.Equal(sourceId,row.GetProperty("principalSourceStateId").GetGuid());
        Assert.Equal(factId,row.GetProperty("principalReceivingFactId").GetGuid());
        Assert.Equal(JsonValueKind.Null,row.GetProperty("goodsReceiptLineId").ValueKind);
        Assert.Equal(JsonValueKind.Null,row.GetProperty("serviceAcceptanceId").ValueKind);
        Assert.Equal("2",row.GetProperty("acceptedQuantity").GetString());
        Assert.Equal("20",row.GetProperty("acceptedNetAmount").GetString());
        Assert.True(row.GetProperty("returnProposalHasNoCapacityEffect").GetBoolean());
        Assert.False(row.GetProperty("canAllocate").GetBoolean());
        Assert.False(row.GetProperty("canPostToFinance").GetBoolean());
    }
}
