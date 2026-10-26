using System.Text.Json.Nodes;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalOrderGovernanceReceiptEncodingTests
{
    private static PurchaseOrder Order()=>new()
    {
        Id=Guid.NewGuid(),TenantId="receipt-company",LegalEntityId=Guid.NewGuid(),Number="PO-TEST-1",
        PrincipalDraftSchemaVersion=2,OriginalDirectSourceSha256=new string('a',64),Status="Draft"
    };
    private static PrincipalOrderDraftReceipt Receipt(PurchaseOrder order,Guid key,string body)=>new()
    {
        TenantId=order.TenantId,LegalEntityId=order.LegalEntityId,PurchaseOrderId=order.Id,
        SchemaVersion=2,Operation="Funding",CommandKey=key,ResultVersion=1,ResponseStatus=200,
        ActorPrincipalId=Guid.NewGuid().ToString("D"),CommandSha256=new string('b',64),
        ResponseJson=body,ResponseSha256=ProcurementEvidenceEncoding.Digest(body)
    };

    [Fact]
    public void Historical_result_stays_bound_after_order_progresses()
    {
        var order=Order();var key=Guid.NewGuid();var artifact=Guid.NewGuid();
        var body=PrincipalOrderGovernanceReceiptEncoding.Encode(order,"Funding",key,1,artifact);
        var receipt=Receipt(order,key,body);
        order.Status="Issued";order.Version=8;
        var result=PrincipalOrderGovernanceReceiptEncoding.Verify(receipt,order,"Funding",key);
        Assert.Equal("Draft",result.Status);
        Assert.Equal(1,result.Version);
        Assert.Equal(artifact,result.EvidenceId);
        Assert.False(result.SupplierSendPerformed);
        Assert.False(result.FinanceReservationChanged);
    }

    [Theory]
    [InlineData("orderId")]
    [InlineData("commandKey")]
    [InlineData("operation")]
    [InlineData("version")]
    [InlineData("supplierSendPerformed")]
    [InlineData("financeReservationChanged")]
    public void A_rehashed_body_cannot_change_its_target_operation_or_effects(string field)
    {
        var order=Order();var key=Guid.NewGuid();
        var body=PrincipalOrderGovernanceReceiptEncoding.Encode(order,"Funding",key,1,Guid.NewGuid());
        var json=JsonNode.Parse(body)!.AsObject();
        json[field]=field switch
        {
            "orderId" or "commandKey"=>JsonValue.Create(Guid.NewGuid().ToString("D")),
            "operation"=>JsonValue.Create("Issue"), "version"=>JsonValue.Create(100),
            _=>JsonValue.Create(true)
        };
        var receipt=Receipt(order,key,json.ToJsonString());
        Assert.Throws<InvalidOperationException>(()=>PrincipalOrderGovernanceReceiptEncoding.Verify(receipt,order,"Funding",key));
    }

    [Fact]
    public void Duplicate_keys_and_missing_fields_are_not_recovered_as_confirmed_results()
    {
        var order=Order();var key=Guid.NewGuid();
        var body=PrincipalOrderGovernanceReceiptEncoding.Encode(order,"Funding",key,1,Guid.NewGuid());
        var duplicate=body.Replace("\"schemaVersion\":2","\"schemaVersion\":2,\"schemaVersion\":2",StringComparison.Ordinal);
        Assert.NotEqual(body,duplicate);
        Assert.Throws<InvalidOperationException>(()=>PrincipalOrderGovernanceReceiptEncoding.Verify(
            Receipt(order,key,duplicate),order,"Funding",key));
        var missing=JsonNode.Parse(body)!.AsObject();missing.Remove("manualFundingOnly");
        Assert.Throws<InvalidOperationException>(()=>PrincipalOrderGovernanceReceiptEncoding.Verify(
            Receipt(order,key,missing.ToJsonString()),order,"Funding",key));
    }

    [Fact]
    public void M3b_draft_receipts_keep_their_original_separate_contract()
    {
        Assert.Throws<InvalidOperationException>(()=>PrincipalOrderGovernanceReceiptEncoding.StatusCode("Create"));
        Assert.Throws<InvalidOperationException>(()=>PrincipalOrderGovernanceReceiptEncoding.StatusCode("Cancel"));
    }
}
