using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

internal static partial class PrincipalApprovalHttpVerification
{
    // A second genuinely approved request and issued PO, never a raw positive
    // receiving/source seed or a rewrite of the already-issued goods snapshot.
    private static async Task<PrincipalIssuedFixture> CreateActualReceivingServiceSourceAsync(
        PrincipalApprovalFixture fixture,HttpClient client,PrincipalSend send,Guid supplierId)
    {
        const string requests="/api/procurement/v1/requests";
        var created=await send(client,HttpMethod.Post,requests,fixture.Maker,new{
            purpose="Private completed service receiving source",neededBy=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),currencyCode="ZMW",
            lines=new[]{new{description="Exact service half certificates",unitCode="each",requestedQuantity="1",estimatedUnitPrice="0.0001",categoryCode="PRIVATE_P25",purchaseType="SERVICE"}}},HttpStatusCode.Created);
        var requestId=created.GetProperty("id").GetGuid();var requestPath=requests+"/"+requestId;
        var submitted=await send(client,HttpMethod.Post,requestPath+"/submit",fixture.Maker,new{version=created.GetProperty("version").GetInt64()});
        await send(client,HttpMethod.Post,requestPath+"/budget-checks",fixture.Checker,new{
            requestVersion=submitted.GetProperty("version").GetInt64(),financeCheckRef="PRIVATE_SERVICE_MANUAL_CHECK",
            status="Available",checkedAmount="0.0001",currencyCode="ZMW",evidenceRef="private-fixture:service-manual-evidence",
            decisionReason="Private manual attestation only; no external reservation verified",
            reservationRef="PRIVATE_SERVICE_RECORDED_RESERVATION",reservationStatus="Confirmed"},HttpStatusCode.Created);
        var current=await send(client,HttpMethod.Get,requestPath,fixture.Maker);
        var opened=await send(client,HttpMethod.Post,requestPath+"/approval-cases",fixture.Maker,
            new{expectedRequestVersion=current.GetProperty("version").GetInt64()},HttpStatusCode.Created,Guid.NewGuid());
        var caseId=opened.GetProperty("caseId").GetGuid();
        var detail=await send(client,HttpMethod.Get,"/api/procurement/v1/approvals/"+caseId,fixture.Approver);
        var assignment=detail.GetProperty("stages").EnumerateArray().Single().GetProperty("assignments").EnumerateArray().Single();
        var decision=await send(client,HttpMethod.Post,$"/api/procurement/v1/approvals/{caseId}/assignments/{assignment.GetProperty("id").GetGuid()}/decisions",fixture.Approver,new{
            action="Approve",reason="Independent private service demand review",expectedCaseVersion=detail.GetProperty("caseVersion").GetInt64(),
            expectedAssignmentVersion=assignment.GetProperty("version").GetInt64(),expectedRequestVersion=detail.GetProperty("requestVersion").GetInt64()},HttpStatusCode.OK,Guid.NewGuid());
        Need(decision.GetProperty("requestState").GetString()=="Approved","Actual service demand was not independently approved.");
        var page=await send(client,HttpMethod.Get,PoRoot+"/approved-principal-direct-sources?page=1&pageSize=25",fixture.Buyer);
        var selected=page.GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("purchaseRequestId").GetGuid()==requestId);
        var requestLineId=selected.GetProperty("purchaseRequestLineId").GetGuid();
        var order=await send(client,HttpMethod.Post,PoRoot+"/from-approved-principal-direct-sources",fixture.Buyer,new{
            draft=new{supplierId,currencyCode="ZMW",shipToRef="Private service branch",billToRef="Private company",paymentTermRef="Net 30",
                expectedDeliveryOn=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14),lines=new[]{new{id=(Guid?)null,description="Exact completed service",
                    unitCode="each",quantity=1m,unitPrice=0.0001m,taxAmount=0m,costCenterRef=(string?)null,projectRef=(string?)null,expectedDeliveryOn=(DateOnly?)null}}},
            sources=new[]{new{draftLineIndex=0,purchaseRequestId=requestId,purchaseRequestLineId=requestLineId,
                expectedRequestVersion=selected.GetProperty("expectedRequestVersion").GetInt64()}}},HttpStatusCode.Created,Guid.NewGuid());
        var orderId=order.GetProperty("orderId").GetGuid();var orderPath=PoRoot+"/"+orderId;
        var orderDetail=await send(client,HttpMethod.Get,orderPath+"/principal-direct-draft",fixture.Buyer);
        var finance=await send(client,HttpMethod.Get,orderPath+"/principal-finance-detail",fixture.PoFinance);
        Need(finance.GetProperty("grossAmountExact").GetString()=="0.0001","Actual service commitment lost original precision.");
        var funded=await send(client,HttpMethod.Post,orderPath+"/principal-funding",fixture.PoFinance,new{
            expectedOrderVersion=orderDetail.GetProperty("version").GetInt64(),manualCommitmentReference="PRIVATE_SERVICE_COMMITMENT",
            externalEvidenceReference="PRIVATE_SERVICE_EXTERNAL_REFERENCE",currencyCode="ZMW",attestedGrossAmount=0.0001m,
            sourceReservationReferences=finance.GetProperty("sourceReservationReferences").EnumerateArray().Select(x=>x.GetString()!).ToArray(),observedAt=DateTimeOffset.UtcNow},HttpStatusCode.OK,Guid.NewGuid());
        await send(client,HttpMethod.Post,orderPath+"/principal-submit",fixture.Buyer,new{expectedOrderVersion=funded.GetProperty("version").GetInt64()},HttpStatusCode.Created,Guid.NewGuid());
        var assigned=await send(client,HttpMethod.Get,PoRoot+"/principal-assigned-reviews?page=1&pageSize=25",fixture.PoApprover);
        var task=assigned.GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("orderId").GetGuid()==orderId);
        var approved=await send(client,HttpMethod.Post,$"{orderPath}/principal-decisions/{task.GetProperty("assignmentId").GetGuid()}",fixture.PoApprover,new{
            action="Approve",reason=(string?)null,expectedOrderVersion=task.GetProperty("version").GetInt64(),expectedCaseVersion=task.GetProperty("caseVersion").GetInt64(),
            expectedAssignmentVersion=task.GetProperty("assignmentVersion").GetInt64()},HttpStatusCode.OK,Guid.NewGuid());
        var issued=await send(client,HttpMethod.Post,orderPath+"/principal-issue",fixture.Buyer,new{expectedOrderVersion=approved.GetProperty("version").GetInt64()},HttpStatusCode.Created,Guid.NewGuid());
        var ackView=await send(client,HttpMethod.Get,orderPath+"/principal-fulfilment-detail?view=ack",fixture.PoAck);
        var issuedId=ackView.GetProperty("issuedVersionId").GetGuid();
        var acknowledged=await send(client,HttpMethod.Post,orderPath+"/principal-acknowledgment",fixture.PoAck,new{
            expectedOrderVersion=issued.GetProperty("version").GetInt64(),issuedVersionId=issuedId,outcome="Accepted",channel="ManualRecord",
            supplierReference="PRIVATE_SERVICE_MANUAL_ACK",evidenceReference="PRIVATE_SERVICE_SUPPLIER_REFERENCE",observedAt=DateTimeOffset.UtcNow},HttpStatusCode.OK,Guid.NewGuid());
        Need(acknowledged.GetProperty("status").GetString()=="Issued"&&!acknowledged.GetProperty("supplierSendPerformed").GetBoolean(),
            "Manual service acknowledgment incorrectly claimed a supplier dispatch.");
        await using var db=fixture.Procurement();var line=await db.PurchaseOrderLines.SingleAsync(x=>x.PurchaseOrderId==orderId);
        return new(requestId,requestLineId,orderId,issuedId,line.Id,supplierId);
    }
}
