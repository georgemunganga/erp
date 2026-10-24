using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

internal static partial class PrincipalApprovalHttpVerification
{
    // Exclusive fresh private database. The accepted P30 proof finishes before
    // this starts, so its three-order owner assertion and original result stay
    // byte-for-byte the same when this profile is absent.
    private static async Task VerifyPrincipalP33OrderEditAsync(PrincipalApprovalFixture fixture,string owner,
        PrincipalApprovalRuntimePrivileges.Connections roles,Dictionary<string,object?> result,
        Action<string> stage,PrincipalSend send,Action countRequest,PrincipalIssuedFixture issued)
    {
        Need(fixture.PrincipalPoProfile && Environment.GetEnvironmentVariable("P33_HTTP_VERIFY_PRINCIPAL_PO_EDIT")=="1",
            "P33 requires the separate genuine principal PO edit profile.");
        stage("P33ActualFiniteRevisionReadiness");
        await using var host=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,
            actualSchema:true,privateManualEvidence:true);
        host.PrepareEntryPointEnvironment();
        using var client=host.CreateClient(new(){AllowAutoRedirect=false});
        var meta=await send(client,HttpMethod.Get,"/api/procurement/v1/meta",fixture.Buyer);
        Need(meta.GetProperty("capabilities").GetProperty("principalOrderDraftRevision").GetBoolean(),
            "P33 actual captured schema and finite execution rights must admit the distinct edit capability.");
        var sourcePage=await send(client,HttpMethod.Get,PoRoot+"/approved-principal-direct-sources?page=1&pageSize=25",fixture.Buyer);
        var source=sourcePage.GetProperty("items").EnumerateArray().Single(x=>
            x.GetProperty("purchaseRequestLineId").GetGuid()==issued.RequestLineId);
        Need(source.GetProperty("availableQuantity").GetString()=="1" && source.GetProperty("canSelect").GetBoolean(),
            "P30 issued quantity must leave exactly one real approved source unit for the P33 edit draft.");
        var createBody=new{draft=new{supplierId=issued.SupplierId,currencyCode="ZMW",shipToRef="Private branch",
            billToRef="Private company",paymentTermRef="Net 30",
            expectedDeliveryOn=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14),
            lines=new[]{new{id=(Guid?)null,description="Exact Core principal source",unitCode="each",
                quantity=1.0000m,unitPrice=5.1234m,taxAmount=0m,costCenterRef=(string?)null,
                projectRef=(string?)null,expectedDeliveryOn=(DateOnly?)null}}},
            sources=new[]{new{draftLineIndex=0,purchaseRequestId=issued.RequestId,
                purchaseRequestLineId=issued.RequestLineId,
                expectedRequestVersion=source.GetProperty("expectedRequestVersion").GetInt64()}}};
        stage("P33RealApprovedSourceCreate");
        var createKey=Guid.NewGuid();
        var created=await send(client,HttpMethod.Post,PoRoot+"/from-approved-principal-direct-sources",
            fixture.Buyer,createBody,HttpStatusCode.Created,createKey);
        var orderId=created.GetProperty("orderId").GetGuid();
        var orderPath=$"{PoRoot}/{orderId}";
        var detail=await send(client,HttpMethod.Get,orderPath+"/principal-direct-draft",fixture.Buyer);
        var originalSha=detail.GetProperty("originalDirectSourceSha256").GetString();
        var originalVersion=detail.GetProperty("version").GetInt64();
        var lineId=detail.GetProperty("lines").EnumerateArray().Single().GetProperty("id").GetGuid();
        Need(detail.GetProperty("status").GetString()=="Draft" && detail.GetProperty("canEditDraft").GetBoolean()
            && detail.GetProperty("lines").EnumerateArray().Single().GetProperty("quantity").GetString()=="1"
            && originalSha is {Length:64},"New P33 draft did not retain editable original P30 source pins.");

        object EditBody(long version,decimal quantity,decimal unitPrice)=>new{
            expectedOrderVersion=version,originalDirectSourceSha256=originalSha,
            reason="Private downward commercial correction",draft=new{supplierId=issued.SupplierId,
                currencyCode="ZMW",shipToRef="Private branch",billToRef="Private company",paymentTermRef="Net 30",
                expectedDeliveryOn=DateOnly.Parse(detail.GetProperty("expectedDeliveryOn").GetString()!,
                    System.Globalization.CultureInfo.InvariantCulture),
                lines=new[]{new{id=(Guid?)lineId,description="Exact Core principal source",unitCode="each",
                    quantity,unitPrice,taxAmount=0m,costCenterRef=(string?)null,projectRef=(string?)null,
                    expectedDeliveryOn=(DateOnly?)null}}}};
        var editBody=EditBody(originalVersion,0.5000m,5.1234m);
        stage("P33CurrentActorAndUpwardAmountDenials");
        await send(client,HttpMethod.Post,orderPath+"/principal-direct-edit",fixture.Ungranted,
            editBody,HttpStatusCode.Forbidden,Guid.NewGuid());
        await using(var core=fixture.Core())
        {
            var buyerRole=await core.TenantRoleAssignments.SingleAsync(x=>x.RoleKey=="p30_po_buyer");
            buyerRole.Active=false;
            await core.SaveChangesAsync();
        }
        try
        {
            await send(client,HttpMethod.Post,orderPath+"/principal-direct-edit",fixture.Buyer,
                editBody,HttpStatusCode.Forbidden,Guid.NewGuid());
        }
        finally
        {
            await using var core=fixture.Core();
            var buyerRole=await core.TenantRoleAssignments.SingleAsync(x=>x.RoleKey=="p30_po_buyer");
            buyerRole.Active=true;
            await core.SaveChangesAsync();
        }
        await using(var core=fixture.Core())
        {
            var foreign=await core.WorkLocations.AsNoTracking().SingleAsync(x=>x.Code=="P30_FOREIGN");
            using var request=new HttpRequestMessage(HttpMethod.Post,orderPath+"/principal-direct-edit");
            request.Headers.Add("X-Private-P25-Subject",fixture.Buyer.ToString("D"));
            request.Headers.Add("X-Shell-Entity",fixture.Company.ToString("D"));
            request.Headers.Add("X-Shell-Location",foreign.Id.ToString("D"));
            request.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString("D"));
            request.Content=JsonContent.Create(editBody);
            countRequest();
            using var denied=await client.SendAsync(request);
            Need(denied.StatusCode==HttpStatusCode.Forbidden,
                "P33 edit must deny a selected Core branch outside the actor's current assignment.");
        }
        await send(client,HttpMethod.Post,orderPath+"/principal-direct-edit",fixture.Buyer,
            EditBody(originalVersion,1m,5.1235m),HttpStatusCode.Conflict,Guid.NewGuid());
        var unchanged=await send(client,HttpMethod.Get,orderPath+"/principal-direct-draft",fixture.Buyer);
        Need(unchanged.GetProperty("version").GetInt64()==originalVersion
            && unchanged.GetProperty("lines").EnumerateArray().Single().GetProperty("quantity").GetString()=="1"
            && unchanged.GetProperty("draftEditHistory").GetProperty("revisions").GetArrayLength()==0,
            "Denied P33 edit changed the original commercial draft or history.");

        stage("P33EditReplayAndOriginalKeyGetRecovery");
        var editKey=Guid.NewGuid();
        var edited=await send(client,HttpMethod.Post,orderPath+"/principal-direct-edit",fixture.Buyer,
            editBody,HttpStatusCode.OK,editKey);
        var replay=await send(client,HttpMethod.Post,orderPath+"/principal-direct-edit",fixture.Buyer,
            editBody,HttpStatusCode.OK,editKey);
        Need(replay.GetRawText()==edited.GetRawText() && edited.GetProperty("version").GetInt64()==originalVersion+1,
            "P33 Edit retry must return the original bytes and advance the parent version once.");
        var recovery=await send(client,HttpMethod.Get,PoRoot+"/principal-direct-command-receipts/Edit/"+editKey,fixture.Buyer);
        Need(recovery.GetProperty("responseJson").GetString()==edited.GetRawText()
            && recovery.GetProperty("originalStatusCode").GetInt32()==200,
            "P33 actor-owned Edit GET did not recover the original recorded result.");
        await send(client,HttpMethod.Get,PoRoot+"/principal-direct-command-receipts/Edit/"+editKey,
            fixture.Ungranted,null,HttpStatusCode.Forbidden);
        await send(client,HttpMethod.Post,orderPath+"/principal-direct-edit",fixture.Buyer,
            EditBody(originalVersion,0.25m,5.1234m),HttpStatusCode.Conflict,editKey);
        await send(client,HttpMethod.Post,orderPath+"/principal-direct-edit",fixture.Buyer,
            EditBody(originalVersion,0.25m,5.1234m),HttpStatusCode.Conflict,Guid.NewGuid());
        var issuedDetail=await send(client,HttpMethod.Get,$"{PoRoot}/{issued.OrderId}/principal-direct-draft",fixture.Buyer);
        await send(client,HttpMethod.Post,$"{PoRoot}/{issued.OrderId}/principal-direct-edit",fixture.Buyer,
            new{expectedOrderVersion=issuedDetail.GetProperty("version").GetInt64(),
                originalDirectSourceSha256=issuedDetail.GetProperty("originalDirectSourceSha256").GetString(),
                reason="Issued orders cannot be revised as drafts",draft=new{supplierId=issued.SupplierId,
                    currencyCode="ZMW",shipToRef="Private branch",billToRef="Private company",paymentTermRef="Net 30",
                    expectedDeliveryOn=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14),
                    lines=new[]{new{id=(Guid?)issued.OrderLineId,description="Exact Core principal source",unitCode="each",
                        quantity=1m,unitPrice=5.1234m,taxAmount=0m,costCenterRef=(string?)null,projectRef=(string?)null,
                        expectedDeliveryOn=(DateOnly?)null}}}},HttpStatusCode.Conflict,Guid.NewGuid());
        var after=await send(client,HttpMethod.Get,orderPath+"/principal-direct-draft",fixture.Buyer);
        Need(after.GetProperty("version").GetInt64()==originalVersion+1
            && after.GetProperty("originalDirectSourceSha256").GetString()==originalSha
            && after.GetProperty("lines").EnumerateArray().Single().GetProperty("quantity").GetString()=="0.5"
            && after.GetProperty("draftEditHistory").GetProperty("revisions").GetArrayLength()==1,
            "P33 revised detail omitted exact effective source and immutable revision history.");

        stage("P33IndependentOwnerReloadAndSourceConservation");
        await using(var db=fixture.Procurement())
        {
            var order=await db.PurchaseOrders.AsNoTracking().SingleAsync(x=>x.Id==orderId);
            var line=await db.PurchaseOrderLines.AsNoTracking().SingleAsync(x=>x.Id==lineId);
            var binding=await db.PrincipalOrderSourceBindings.AsNoTracking().SingleAsync(x=>x.PurchaseOrderId==orderId);
            var allocation=await db.PurchaseRequestAllocations.AsNoTracking().SingleAsync(x=>x.Id==binding.AllocationId);
            var sourceLine=await db.PurchaseRequestLines.AsNoTracking().SingleAsync(x=>x.Id==issued.RequestLineId);
            var origin=await db.PrincipalOrderCommercialOrigins.AsNoTracking().SingleAsync(x=>x.PurchaseOrderId==orderId);
            var revision=await db.PrincipalOrderDraftCommercialRevisions.AsNoTracking().SingleAsync(x=>x.PurchaseOrderId==orderId);
            var slice=await db.PrincipalOrderEffectiveSourceSlices.AsNoTracking().SingleAsync(x=>x.PurchaseOrderId==orderId);
            var receipt=await db.PrincipalOrderDraftReceipts.AsNoTracking().SingleAsync(x=>x.PurchaseOrderId==orderId&&x.Operation=="Edit");
            Need(order.Status=="Draft" && order.Version==originalVersion+1
                && order.OriginalDirectSourceSha256==originalSha && line.Quantity==0.5m
                && allocation.Quantity==0.5m && allocation.Status=="Reserved"
                && sourceLine.ApprovedQuantity==2m && sourceLine.OrderedQuantity==1m
                && sourceLine.AllocatedQuantity==0.5m && sourceLine.CancelledQuantity==0m
                && sourceLine.ApprovedQuantity-sourceLine.OrderedQuantity-sourceLine.AllocatedQuantity-sourceLine.CancelledQuantity==0.5m
                && origin.OriginalDirectSourceSha256==originalSha && revision.RevisionNumber==1
                && revision.CommandKey==editKey && slice.RevisionId==revision.Id
                && slice.SourceBindingId==binding.Id && slice.Quantity==0.5m
                && receipt.CommandKey==editKey && receipt.ResponseJson==edited.GetRawText()
                && await db.AuditEvents.CountAsync(x=>x.RecordId==orderId&&x.Action=="PrincipalOrderEdit")==1
                && await db.OutboxEvents.CountAsync(x=>x.AggregateId==orderId
                    &&x.EventType=="purchase_order.internal_unfunded_draft_revised.v2")==1
                && !await db.FinanceInvoiceHandoffs.AnyAsync() && !await db.FinancePaymentStatusRefs.AnyAsync(),
                "P33 owner graph, source conservation, immutable receipt or no-Finance boundary changed.");
        }
        stage("P33RevisedDraftCancelReleasesEffectiveQuantity");
        var cancel=await send(client,HttpMethod.Post,orderPath+"/principal-direct-cancel",fixture.Buyer,
            new{expectedOrderVersion=originalVersion+1,originalDirectSourceSha256=originalSha,
                reason="Private revised draft release"},HttpStatusCode.OK,Guid.NewGuid());
        Need(cancel.GetProperty("status").GetString()=="Cancelled",
            "P33 revised draft could not release only its effective reservation.");
        await using(var db=fixture.Procurement())
        {
            var allocation=await db.PurchaseRequestAllocations.AsNoTracking()
                .SingleAsync(x=>x.TargetType=="PurchaseOrder"&&x.TargetId==orderId);
            var sourceLine=await db.PurchaseRequestLines.AsNoTracking().SingleAsync(x=>x.Id==issued.RequestLineId);
            Need(allocation.Status=="Released"&&allocation.Quantity==0.5m
                &&sourceLine.ApprovedQuantity==2m&&sourceLine.OrderedQuantity==1m
                &&sourceLine.AllocatedQuantity==0m&&sourceLine.CancelledQuantity==0m
                &&await db.PrincipalOrderDraftCommercialRevisions.AsNoTracking()
                    .CountAsync(x=>x.PurchaseOrderId==orderId)==1,
                "P33 revised cancellation did not preserve history and restore exact source capacity.");
        }
        result["principalOrderRevision"]=new{status="PASS_PRIVATE_P33_PRINCIPAL_UNFUNDED_DRAFT_EDIT_ONLY",
            orderId,requestId=issued.RequestId,requestLineId=issued.RequestLineId,
            editKey,originalOrderVersion=originalVersion,revisedOrderVersion=originalVersion+1,
            retainedOriginalSource=true,actorOwnedEditRecovery=true,sourceConservation=true,
            releasedEffectiveQuantity=true,externalFinancePosting=false,supplierSendPerformed=false};
        result["status"]="PASS_PRIVATE_P33_PRINCIPAL_UNFUNDED_DRAFT_EDIT_ONLY";
    }
}
