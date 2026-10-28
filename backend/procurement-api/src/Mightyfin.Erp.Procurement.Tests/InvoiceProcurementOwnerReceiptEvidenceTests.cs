using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceProcurementOwnerReceiptEvidenceTests
{
    [Theory]
    [InlineData("Open","Pending",201,false)]
    [InlineData("Decide","Approved",200,true)]
    [InlineData("Decide","Returned",200,true)]
    public void Typed_response_binds_original_key_case_scope_and_digest(
        string operation,string state,int status,bool decided)
    {
        var baseline=new InvoiceProcurementOwnerBaseline
        {
            TenantId="t",LegalEntityId=Guid.NewGuid(),CoreCaseId=Guid.NewGuid(),
            MakerPrincipalId=Guid.NewGuid().ToString("D"),InventorySetSha256=new string('a',64),
            CapturedAtUtc=new DateTimeOffset(2026,10,28,16,0,0,TimeSpan.Zero)
        };
        var response=new InvoiceProcurementOwnerReceiptEvidence.Response(1,baseline.Id,baseline.CoreCaseId,
            decided?Guid.NewGuid():null,operation,Guid.NewGuid(),decided?2:1,state,baseline.InventorySetSha256);
        var json=InvoiceProcurementOwnerReceiptEvidence.Encode(response);
        var receipt=Row(baseline,response,json,status);
        Assert.Equal(response,InvoiceProcurementOwnerReceiptEvidence.Verify(receipt,baseline));

        var wrong=new InvoiceProcurementOwnerReceiptEvidence.Response(1,baseline.Id,baseline.CoreCaseId,
            response.CoreDecisionId,operation,Guid.NewGuid(),response.CoreCaseVersion,state,baseline.InventorySetSha256);
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementOwnerReceiptEvidence.Verify(
            Row(baseline,response,InvoiceProcurementOwnerReceiptEvidence.Encode(wrong),status),baseline));
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementOwnerReceiptEvidence.Verify(
            Row(baseline,response,"{}",status),baseline));
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementOwnerReceiptEvidence.Verify(
            Row(baseline,response,json[..^1]+",\"extra\":1}",status),baseline));
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementOwnerReceiptEvidence.Verify(
            Row(baseline,response,json,status==201?200:201),baseline));
    }

    private static InvoiceProcurementOwnerCommandReceipt Row(InvoiceProcurementOwnerBaseline baseline,
        InvoiceProcurementOwnerReceiptEvidence.Response response,string json,int status)
    {
        var actor=response.Operation=="Open"?baseline.MakerPrincipalId:Guid.NewGuid().ToString("D");
        return new()
        {
            TenantId=baseline.TenantId,LegalEntityId=baseline.LegalEntityId,
            BaselineId=baseline.Id,CoreCaseId=baseline.CoreCaseId,CoreDecisionId=response.CoreDecisionId,
            Operation=response.Operation,CommandKey=response.CommandKey,
            ActorPrincipalId=actor,CreatedBy=actor,CreatedAt=baseline.CapturedAtUtc.AddMinutes(1),
            RequestSha256=new string('b',64),
            ResponseJson=json,ResponseSha256=ProcurementEvidenceEncoding.Digest(json),
            ResponseStatus=status,CoreCaseVersion=response.CoreCaseVersion
        };
    }
}
