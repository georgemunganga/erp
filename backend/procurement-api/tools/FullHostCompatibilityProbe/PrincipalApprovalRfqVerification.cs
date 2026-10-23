using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Procurement.Domain;

internal static partial class PrincipalApprovalHttpVerification
{
    private static async Task VerifyPrincipalRfqAsync(PrincipalApprovalFixture fixture,string owner,
        PrincipalApprovalRuntimePrivileges.Connections roles,Dictionary<string,object?> result,
        Action<string> stage,PrincipalSend send,Action countRequest)
    {
        Need(fixture.PrincipalRfqProfile && result.TryGetValue("positiveApprovalGraph",out var accepted)
            && accepted is true && result["positiveApprovalRequestId"] is Guid,
            "Principal RFQ requires its separately approved, genuinely competitive source.");
        var requestId=(Guid)result["positiveApprovalRequestId"]!;
        stage("P29ActualFinitePrincipalSourcingReadiness");
        await using var host=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,
            actualSchema:true,privateManualEvidence:true);
        host.PrepareEntryPointEnvironment();using var client=host.CreateClient(new(){AllowAutoRedirect=false});
        using(var registration=host.Services.CreateScope())
        {
            var actual=registration.ServiceProvider.GetRequiredService<IProcurementSourcingDraftSchemaReadiness>();
            Need(actual is ProcurementSourcingDraftSchemaReadiness && await actual.IsReadyAsync(default),
                "Actual P29 schema and bounded finite sourcing execution role are not ready.");
        }
        var metadata=await send(client,HttpMethod.Get,"/api/procurement/v1/meta",fixture.Buyer);
        Need(metadata.GetProperty("capabilities").GetProperty("principalSourcingSourceDrafts").GetBoolean(),
            "Normal host did not advertise the genuinely ready principal source draft.");
        const string root="/api/procurement/v1/sourcing";
        var picker=await send(client,HttpMethod.Get,root+"/approved-principal-sources?page=1&pageSize=25",fixture.Buyer);
        var source=picker.GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("purchaseRequestId").GetGuid()==requestId);
        Need(source.GetProperty("canSelect").GetBoolean()
            &&source.GetProperty("availableQuantity").GetString()=="2"
            &&source.GetProperty("approvedUnitEstimate").GetString()=="5.1234"
            &&source.GetProperty("minimumQuoteCount").GetInt32()==2,
            "Actual approved competitive source was not selectable with its retained quote rule.");
        var lineId=source.GetProperty("purchaseRequestLineId").GetGuid();
        await send(client,HttpMethod.Get,root+"/approved-principal-sources",fixture.Maker,null,HttpStatusCode.Forbidden);
        Guid foreignBranch=Guid.NewGuid();
        await using(var core=fixture.Core())
        {
            core.WorkLocations.Add(new WorkLocation{Id=foreignBranch,Code="P29_OTHER",Name="Private unassigned branch",
                LegalEntityId=fixture.Company});
            await core.SaveChangesAsync();
        }
        using(var wrongScope=new HttpRequestMessage(HttpMethod.Get,root+"/approved-principal-sources"))
        {
            wrongScope.Headers.Add("X-Private-P25-Subject",fixture.Buyer.ToString("D"));
            wrongScope.Headers.Add("X-Shell-Entity",fixture.Company.ToString("D"));
            wrongScope.Headers.Add("X-Shell-Location",foreignBranch.ToString("D"));
            countRequest();
            using var denied=await client.SendAsync(wrongScope);
            Need(denied.StatusCode==HttpStatusCode.Forbidden,
                "Unassigned current Core branch must not expose principal RFQ sources.");
        }

        stage("P29PrincipalDraftCreateReadAndReplay");
        var createKey=Guid.NewGuid();var body=new {title="Private principal RFQ draft",
            sources=new[]{new{purchaseRequestId=requestId,purchaseRequestLineId=lineId,
                expectedRequestVersion=source.GetProperty("expectedRequestVersion").GetInt64(),quantity="1.0000"}}};
        await send(client,HttpMethod.Post,root+"/from-approved-principal-sources",fixture.Ungranted,body,HttpStatusCode.Forbidden,Guid.NewGuid());
        var created=await send(client,HttpMethod.Post,root+"/from-approved-principal-sources",fixture.Buyer,body,HttpStatusCode.Created,createKey);
        var eventId=created.GetProperty("eventId").GetGuid();var eventPath=root+"/"+eventId;
        var read=await send(client,HttpMethod.Get,eventPath+"/principal-source-draft",fixture.Buyer);
        var selected=read.GetProperty("lines").EnumerateArray().Single();
        Need(read.GetProperty("status").GetString()=="Draft" && !read.GetProperty("canPublish").GetBoolean()
            &&read.GetProperty("supplierDeliveryState").GetString()=="NotStarted"
            &&!read.GetProperty("financeReservationChanged").GetBoolean()
            &&selected.GetProperty("purchaseRequestLineId").GetGuid()==lineId
            &&selected.GetProperty("quantity").GetString()=="1"
            &&selected.GetProperty("estimatedValue").GetString()=="5.1234"
            &&selected.GetProperty("allocationState").GetString()=="Reserved",
            "Principal RFQ draft read did not reflect the exact original held source.");
        await send(client,HttpMethod.Get,eventPath+"/principal-source-draft",fixture.Maker,null,HttpStatusCode.Forbidden);
        var replay=await send(client,HttpMethod.Post,root+"/from-approved-principal-sources",fixture.Buyer,body,HttpStatusCode.Created,createKey);
        Need(replay.GetRawText()==created.GetRawText(),"Same-key principal RFQ Create replay changed its recorded response.");
        await send(client,HttpMethod.Post,root+"/from-approved-principal-sources",fixture.Buyer,
            new {title="Changed command must not replay",body.sources},HttpStatusCode.Conflict,createKey);
        var recovery=await send(client,HttpMethod.Get,root+"/principal-command-receipts/Create/"+createKey,fixture.Buyer);
        Need(recovery.GetProperty("responseJson").GetString()==created.GetRawText(),
            "Actor-owned Create recovery did not return the original recorded bytes.");
        await send(client,HttpMethod.Get,root+"/principal-command-receipts/Create/"+createKey,fixture.Maker,null,HttpStatusCode.Forbidden);

        stage("P29OwnerOnlyConstraintNegatives");
        await using(var ownerConnection=new NpgsqlConnection(owner))
        {
            await ownerConnection.OpenAsync();
            result["principalRfqStorage"]=await VerifyPrincipalRfqRawStorageAsync(
                ownerConnection,fixture,eventId,createKey);
        }

        stage("P29PrincipalDraftCancelAndOwnerReload");
        var cancelKey=Guid.NewGuid();var cancelBody=new{expectedEventVersion=read.GetProperty("version").GetInt64(),
            originalDraftFingerprint=read.GetProperty("originalDraftFingerprint").GetString(),
            reason="Private exact approved demand no longer needed"};
        await send(client,HttpMethod.Post,eventPath+"/principal-source-cancel",fixture.Maker,cancelBody,HttpStatusCode.Forbidden,Guid.NewGuid());
        var cancelled=await send(client,HttpMethod.Post,eventPath+"/principal-source-cancel",fixture.Buyer,cancelBody,HttpStatusCode.OK,cancelKey);
        Need(cancelled.GetProperty("status").GetString()=="Cancelled","Principal RFQ cancellation did not persist.");
        var cancelReplay=await send(client,HttpMethod.Post,eventPath+"/principal-source-cancel",fixture.Buyer,cancelBody,HttpStatusCode.OK,cancelKey);
        Need(cancelReplay.GetRawText()==cancelled.GetRawText(),"Same-key principal RFQ Cancel replay changed its recorded response.");
        var cancelRecovery=await send(client,HttpMethod.Get,root+"/principal-command-receipts/Cancel/"+cancelKey,fixture.Buyer);
        Need(cancelRecovery.GetProperty("responseJson").GetString()==cancelled.GetRawText(),
            "Actor-owned Cancel recovery did not return the original recorded bytes.");
        var historicalCreate=await send(client,HttpMethod.Post,root+"/from-approved-principal-sources",fixture.Buyer,body,HttpStatusCode.Created,createKey);
        Need(historicalCreate.GetRawText()==created.GetRawText(),"Cancelled draft reinterpreted its original Create receipt.");
        var history=await send(client,HttpMethod.Get,eventPath+"/principal-source-draft",fixture.Buyer);
        Need(history.GetProperty("status").GetString()=="Cancelled"
            &&history.GetProperty("lines").EnumerateArray().Single().GetProperty("allocationState").GetString()=="Released",
            "Historical principal RFQ read did not preserve the cancelled allocation.");
        await using var db=fixture.Procurement();await using var core=fixture.Core();
        var request=await db.PurchaseRequests.Include(x=>x.Lines).SingleAsync(x=>x.Id==requestId);
        var ev=await db.SourcingEvents.SingleAsync(x=>x.Id==eventId);
        var evidence=await db.SourcingDraftEvidenceRecords.SingleAsync(x=>x.SourcingEventId==eventId);
        var binding=await db.SourcingDemandBindings.SingleAsync(x=>x.SourcingEventId==eventId);
        var allocation=await db.PurchaseRequestAllocations.SingleAsync(x=>x.Id==binding.AllocationId);
        var receipts=await db.SourcingCommandReceipts.Where(x=>x.SourcingEventId==eventId).ToArrayAsync();
        Need(!await core.Workers.AnyAsync() && await db.SourcingEvents.CountAsync()==1
            &&await db.SourcingLines.CountAsync()==1
            &&await db.SourcingDraftEvidenceRecords.CountAsync()==1
            &&await db.SourcingDemandBindings.CountAsync()==1
            &&await db.PurchaseRequestAllocations.CountAsync()==1
            &&request.RequesterIdentitySchemaVersion==1
            &&request.Lines.Single(x=>x.Id==lineId).AllocatedQuantity==0m
            &&ev.ExecutionVersion==2 &&ev.Status=="Cancelled" &&ev.OwnerSubjectId==fixture.Buyer.ToString("D")
            &&evidence.SchemaVersion==2 &&evidence.MakerPrincipalId==fixture.Buyer.ToString("D")
            &&binding.IdentitySchemaVersion==2 &&binding.PurchaseRequestLineId==lineId
            &&binding.Quantity==1m &&binding.EstimatedValue==5.1234m
            &&allocation.Status=="Released" &&allocation.Quantity==1m
            &&receipts.Length==2 &&receipts.Select(x=>x.Operation).ToHashSet().SetEquals(["Create","Cancel"])
            &&receipts.All(x=>x.ActorPrincipalId==fixture.Buyer.ToString("D") &&x.ResponseSchemaVersion==2),
            "Independent owner reload did not retain the exact principal RFQ hold/release and receipts.");
        Need(!await db.SourcingInvitations.AnyAsync()&&!await db.SupplierBids.AnyAsync()
            &&!await db.SourcingAwards.AnyAsync()&&!await db.PurchaseOrders.AnyAsync()
            &&!await db.FinanceInvoiceHandoffs.AnyAsync()&&!await db.FinancePaymentStatusRefs.AnyAsync(),
            "Internal principal RFQ draft invented publication, order, AP or payment facts.");
        result["principalRfq"] = new {status="PASS_PRIVATE_P29_INTERNAL_DRAFT_ONLY",requestId,lineId,eventId,
            createCommandKey=createKey,cancelCommandKey=cancelKey,workerless=true,
            originalPrincipalApproval=true,allocationRestored=true,actorOwnedRecovery=true,
            supplierPublished=false,externalFinanceAuthority=false};
        result["status"]="PASS_PRIVATE_P29_INTERNAL_DRAFT_ONLY";
    }
}
