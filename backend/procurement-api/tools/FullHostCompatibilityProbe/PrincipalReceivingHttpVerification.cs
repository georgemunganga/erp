using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    private const string ReceivingRoot="/api/procurement/v1";
    private static async Task VerifyPrincipalReceivingAsync(PrincipalApprovalFixture fixture,string owner,
        PrincipalApprovalRuntimePrivileges.Connections roles,Dictionary<string,object?> result,Action<string> stage,
        PrincipalSend send,Action countRequest,PrincipalIssuedFixture goods)
    {
        Need(fixture.PrincipalReceivingProfile&&fixture.PrincipalPoProfile,"Receiving requires its explicit genuine PO cohort.");
        stage("P30ReceivingActualFiniteProvider");
        await using var host=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,true,true);
        host.PrepareEntryPointEnvironment();using var client=host.CreateClient(new(){AllowAutoRedirect=false});
        using(var services=host.Services.CreateScope())
        {
            var ready=services.ServiceProvider.GetRequiredService<IProcurementPrincipalReceivingReadiness>();
            Need(ready is ProcurementPrincipalReceivingReadiness&&await ready.IsReadyAsync(default),
                "Actual receiving migration/finite-role readiness must pass without a positive stub.");
        }
        var metadata=await send(client,HttpMethod.Get,ReceivingRoot+"/meta",fixture.Receiver);
        Need(metadata.GetProperty("capabilities").GetProperty("principalReceiving").GetBoolean(),"Normal host did not advertise proven receiving availability.");
        var context=await send(client,HttpMethod.Get,ReceivingRoot+"/context",fixture.Receiver);
        Need(context.GetProperty("workerId").ValueKind==JsonValueKind.Null,"Receiving fixture must remain a genuine Core-only actor.");
        stage("P30ReceivingIndependentRealPolicy");
        var policyStart=DateTimeOffset.UtcNow.AddSeconds(30);
        var rules=JsonSerializer.Serialize(new{schemaVersion=2,transactionType="Receiving",currencyCode="ZMW",rules=new object[]{
            new{code="PRIVATE_GOODS",categoryCode="PRIVATE_P25",purchaseType="GOODS",kind="Goods",siteIds=new[]{fixture.Branch},inspectionRequired=true,
                checklistReference="PRIVATE_CHECKLIST",inspectorPrincipalIds=new[]{fixture.Receiver.ToString("D"),fixture.Inspector.ToString("D")},
                acceptorPrincipalIds=new[]{fixture.Receiver.ToString("D"),fixture.Acceptor.ToString("D")},serviceOwnerPrincipalIds=Array.Empty<string>(),
                allowRequesterCertification=false,allowBuyerCertification=false,allowReceiverCertification=false,requireIndependentInspector=true,
                evidenceMode="ControlledManualReference",evidenceReferencePrefix="PRIVATE-REC:"},
            new{code="PRIVATE_SERVICE",categoryCode="PRIVATE_P25",purchaseType="SERVICE",kind="Service",siteIds=new[]{fixture.Branch},inspectionRequired=false,
                checklistReference=(string?)null,inspectorPrincipalIds=Array.Empty<string>(),acceptorPrincipalIds=Array.Empty<string>(),
                serviceOwnerPrincipalIds=new[]{fixture.ServiceOwner.ToString("D")},allowRequesterCertification=false,allowBuyerCertification=false,
                allowReceiverCertification=false,requireIndependentInspector=false,evidenceMode="ControlledManualReference",evidenceReferencePrefix="PRIVATE-REC:"}}});
        const string policyRoot="/api/procurement/v1/configuration/policies";
        var policy=await send(client,HttpMethod.Post,policyRoot,fixture.ConfigMaker,new{policyKey="RECEIVING",effectiveFrom=policyStart,effectiveTo=(DateTimeOffset?)null,rulesJson=rules},HttpStatusCode.Created);
        var policyPath=policyRoot+"/"+policy.GetProperty("id").GetGuid();
        var submitted=await send(client,HttpMethod.Post,policyPath+"/submit-review",fixture.ConfigMaker,new{version=policy.GetProperty("version").GetInt64(),reason="Private explicit receiving assignment review"});
        var published=await send(client,HttpMethod.Post,policyPath+"/publish",fixture.ConfigReviewer,new{version=submitted.GetProperty("version").GetInt64(),reason="Independent private Core receiving review"});
        Need(published.GetProperty("state").GetString()=="Published","Receiving policy was not independently published through actual routes.");
        var remaining=policyStart-DateTimeOffset.UtcNow;Need(remaining<=TimeSpan.FromSeconds(30),"Unexpected receiving policy wait.");
        if(remaining>TimeSpan.Zero)await Task.Delay(remaining+TimeSpan.FromMilliseconds(50));
        await using var ownerConnection=new NpgsqlConnection(owner);await ownerConnection.OpenAsync();
        var noEffects=await PrincipalFootprintAsync(ownerConnection);
        var sources=await send(client,HttpMethod.Get,ReceivingRoot+"/receiving/principal-sources?offset=0&limit=25",fixture.Receiver);
        Need(sources.GetProperty("records").EnumerateArray().Any(x=>x.GetProperty("purchaseOrderId").GetGuid()==goods.OrderId
            &&x.GetProperty("purchaseOrderLineId").GetGuid()==goods.OrderLineId&&!x.GetProperty("supplierAcknowledgmentIsDelivery").GetBoolean()),
            "Actual issued/manual-ack source was absent or falsely labelled delivery.");
        Need(await PrincipalFootprintAsync(ownerConnection)==noEffects,"Source discovery must not create a zero source ledger or other effects.");

        stage("P30ReceivingGoodsDraftRevisionReplayAndScope");
        var createKey=Guid.NewGuid();var observed=DateTimeOffset.UtcNow;
        var draft=new{purchaseOrderId=goods.OrderId,issuedVersionId=goods.IssuedId,siteId=fixture.Branch,receivedAt=observed,
            deliveryNoteRef="PRIVATE_PHYSICAL_NOTE",notes="Original physical note retained",lines=new[]{new{id=(Guid?)null,purchaseOrderLineId=goods.OrderLineId,receivedQuantity="2",rejectedQuantity="0"}}};
        var created=await send(client,HttpMethod.Post,ReceivingRoot+"/receipts/principal-drafts",fixture.Receiver,draft,HttpStatusCode.Created,createKey);
        var receiptId=created.GetProperty("id").GetGuid();var path=ReceivingRoot+"/receipts/"+receiptId;
        var createReplay=await send(client,HttpMethod.Post,ReceivingRoot+"/receipts/principal-drafts",fixture.Receiver,draft,HttpStatusCode.Created,createKey);
        Need(createReplay.GetRawText()==created.GetRawText(),"Receiving Create exact replay changed original bytes.");
        await send(client,HttpMethod.Post,ReceivingRoot+"/receipts/principal-drafts",fixture.ReceiverPeer,draft,HttpStatusCode.Conflict,createKey);
        var detail=await send(client,HttpMethod.Get,path+"/principal-draft",fixture.Receiver);
        var logicalLine=detail.GetProperty("receipt").GetProperty("lines").EnumerateArray().Single().GetProperty("id").GetGuid();
        Need(detail.GetProperty("receipt").GetProperty("physicalEvidenceRecorded").GetBoolean()==false,
            "Draft quantity must not be treated as recorded physical delivery.");
        var revise=new{version=detail.GetProperty("version").GetInt64(),draft=new{draft.purchaseOrderId,draft.issuedVersionId,draft.siteId,draft.receivedAt,
            draft.deliveryNoteRef,notes="Revised physical note retained",lines=new[]{new{id=logicalLine,purchaseOrderLineId=goods.OrderLineId,receivedQuantity="2",rejectedQuantity="0"}}}};
        var revised=await send(client,HttpMethod.Put,path+"/principal-draft",fixture.Receiver,revise,HttpStatusCode.OK,Guid.NewGuid());
        var baseline=await PrincipalFootprintAsync(ownerConnection);
        await send(client,HttpMethod.Put,path+"/principal-draft",fixture.Receiver,revise,HttpStatusCode.Conflict,Guid.NewGuid());
        await send(client,HttpMethod.Get,path+"/principal-draft",fixture.Inspector,null,HttpStatusCode.Forbidden);
        await ReceivingOtherBranchAsync(client,fixture,path,result,countRequest);
        Need(await PrincipalFootprintAsync(ownerConnection)==baseline,"Stale/foreign/purpose-only read denial changed receiving state.");

        stage("P30ActualPhysicalRecordAndIndependentInspection");
        var recordKey=Guid.NewGuid();var recordBody=new{version=revised.GetProperty("version").GetInt64(),evidenceReference="PRIVATE-REC:physical-observation"};
        var recorded=await send(client,HttpMethod.Post,path+"/principal-record",fixture.Receiver,recordBody,HttpStatusCode.OK,recordKey);
        var recordReplay=await send(client,HttpMethod.Post,path+"/principal-record",fixture.Receiver,recordBody,HttpStatusCode.OK,recordKey);
        Need(recordReplay.GetRawText()==recorded.GetRawText(),"Physical recording replay duplicated or changed its original result.");
        var queue=await send(client,HttpMethod.Get,ReceivingRoot+"/receiving/principal-inspection-tasks?offset=0&limit=25",fixture.Inspector);
        var task=queue.GetProperty("records").EnumerateArray().Single(x=>x.GetProperty("id").GetGuid()==receiptId);
        Need(task.GetProperty("purchaseOrderLineId").GetGuid()==goods.OrderLineId&&task.GetProperty("projection").GetProperty("physicalQuantity").GetString()=="2",
            "Purpose-only inspection lost actual source/physical overdelivery facts.");
        Need(!task.TryGetProperty("notes",out _)&&!task.TryGetProperty("creatorPrincipalId",out _)&&!task.GetProperty("projection").TryGetProperty("netAmount",out _),
            "Inspection-purpose response exposed owner/private monetary fields.");
        var inspectBody=new{version=task.GetProperty("version").GetInt64(),sourceVersion=task.GetProperty("sourceVersion").GetInt64(),quantity="1.5",evidenceReference="PRIVATE-REC:inspection"};
        baseline=await PrincipalFootprintAsync(ownerConnection);
        await send(client,HttpMethod.Post,$"{path}/lines/{logicalLine}/principal-inspect",fixture.Receiver,inspectBody,HttpStatusCode.Forbidden,Guid.NewGuid());
        Need(await PrincipalFootprintAsync(ownerConnection)==baseline,"Self inspection denial changed physical evidence.");
        await send(client,HttpMethod.Post,$"{path}/lines/{logicalLine}/principal-inspect",fixture.Inspector,inspectBody,HttpStatusCode.OK,Guid.NewGuid());

        stage("P30ExactPositiveAcceptanceReceiptAndCounterGuards");
        var acceptedTask=await send(client,HttpMethod.Get,path+"/principal-acceptance-task?offset=0&limit=25",fixture.Acceptor);
        var acceptRow=acceptedTask.GetProperty("records").EnumerateArray().Single();
        var acceptKey=Guid.NewGuid();var acceptBody=new{version=acceptRow.GetProperty("version").GetInt64(),sourceVersion=acceptRow.GetProperty("sourceVersion").GetInt64(),quantity="0.0001",evidenceReference="PRIVATE-REC:small-exact-acceptance"};
        var accepted=await send(client,HttpMethod.Post,$"{path}/lines/{logicalLine}/principal-accept",fixture.Acceptor,acceptBody,HttpStatusCode.OK,acceptKey);
        var exactEffect=accepted.GetProperty("receipt").GetProperty("lineReadiness").EnumerateArray().Single().GetProperty("effects").EnumerateArray().Single(x=>x.GetProperty("kind").GetString()=="Acceptance");
        Need(exactEffect.GetProperty("netAmount").GetString()=="0.00051234","Goods acceptance lost its exact eight-place product.");
        var replay=await send(client,HttpMethod.Post,$"{path}/lines/{logicalLine}/principal-accept",fixture.Acceptor,acceptBody,HttpStatusCode.OK,acceptKey);
        Need(replay.GetRawText()==accepted.GetRawText(),"Acceptance replay changed stored history.");
        var recovered=await send(client,HttpMethod.Get,ReceivingRoot+"/receiving/principal-command-receipts/Accept/"+acceptKey,fixture.Acceptor);
        Need(recovered.GetRawText()==accepted.GetRawText(),"GET receiving recovery did not return exact original bytes.");
        await VerifyReceivingTamperDenialsAsync(ownerConnection,fixture,client,send,path,logicalLine,goods,acceptKey);
        var refreshed=await send(client,HttpMethod.Get,path+"/principal-acceptance-task?offset=0&limit=25",fixture.Acceptor);
        var next=refreshed.GetProperty("records").EnumerateArray().Single();
        baseline=await PrincipalFootprintAsync(ownerConnection);
        await send(client,HttpMethod.Post,$"{path}/lines/{logicalLine}/principal-accept",fixture.Acceptor,new{version=next.GetProperty("version").GetInt64(),sourceVersion=next.GetProperty("sourceVersion").GetInt64(),quantity="1",evidenceReference="PRIVATE-REC:excess"},HttpStatusCode.Conflict,Guid.NewGuid());
        Need(await PrincipalFootprintAsync(ownerConnection)==baseline,"Over-capacity acceptance wrote a partial effect.");
        await send(client,HttpMethod.Post,$"{path}/lines/{logicalLine}/principal-accept",fixture.Acceptor,new{version=next.GetProperty("version").GetInt64(),sourceVersion=next.GetProperty("sourceVersion").GetInt64(),quantity="0.9999",evidenceReference="PRIVATE-REC:remaining-accepted"},HttpStatusCode.OK,Guid.NewGuid());
        var historicalCreate=await send(client,HttpMethod.Get,ReceivingRoot+"/receiving/principal-command-receipts/Create/"+createKey,fixture.Receiver);
        Need(historicalCreate.GetRawText()==created.GetRawText(),"Historical create recovery was rewritten to today's recorded state.");

        stage("P30UnusedReceivingDraftCancelNoConsumption");
        var auxiliary=await send(client,HttpMethod.Post,ReceivingRoot+"/receipts/principal-drafts",fixture.Receiver,draft,HttpStatusCode.Created,Guid.NewGuid());
        var auxiliaryId=auxiliary.GetProperty("id").GetGuid();var cancelBody=new{version=auxiliary.GetProperty("version").GetInt64(),reason="Unused extra physical draft"};
        await send(client,HttpMethod.Post,$"{ReceivingRoot}/receipts/{auxiliaryId}/principal-cancel",fixture.ReceiverPeer,cancelBody,HttpStatusCode.NotFound,Guid.NewGuid());
        await send(client,HttpMethod.Post,$"{ReceivingRoot}/receipts/{auxiliaryId}/principal-cancel",fixture.Receiver,cancelBody,HttpStatusCode.OK,Guid.NewGuid());
        stage("P30GenuineServicePRAndIssuedSource");
        var service=await CreateActualReceivingServiceSourceAsync(fixture,client,send,goods.SupplierId);
        var serviceCertificates=new List<Guid>();
        for(var number=0;number<2;number++)
        {
            var serviceDraft=await send(client,HttpMethod.Post,ReceivingRoot+"/service-acceptances/principal-drafts",fixture.Receiver,
                new{purchaseOrderId=service.OrderId,issuedVersionId=service.IssuedId,purchaseOrderLineId=service.OrderLineId,deliverableReference="Private completed half "+number,
                    periodFrom=DateOnly.FromDateTime(DateTime.UtcNow),periodTo=DateOnly.FromDateTime(DateTime.UtcNow),quantity="0.5",notes="Private retained service note"},HttpStatusCode.Created,Guid.NewGuid());
            var certificateId=serviceDraft.GetProperty("id").GetGuid();serviceCertificates.Add(certificateId);
            var certification=await send(client,HttpMethod.Get,$"{ReceivingRoot}/service-acceptances/{certificateId}/principal-certification-task?offset=0&limit=25",fixture.ServiceOwner);
            var serviceTask=certification.GetProperty("records").EnumerateArray().Single();
            Need(serviceTask.GetProperty("sourceVersion").GetInt64()==number&&!serviceTask.GetProperty("projection").TryGetProperty("notes",out _),
                "Second partial service did not use current shared source cursor or leaked notes.");
            var certificate=await send(client,HttpMethod.Post,$"{ReceivingRoot}/service-acceptances/{certificateId}/principal-certify",fixture.ServiceOwner,
                new{version=serviceTask.GetProperty("version").GetInt64(),sourceVersion=serviceTask.GetProperty("sourceVersion").GetInt64(),quantity="0.5",evidenceReference="PRIVATE-REC:service-half-"+number},HttpStatusCode.OK,Guid.NewGuid());
            Need(certificate.GetProperty("service").GetProperty("acceptedNetAmount").GetString()=="0.00005","Half-service acceptance lost exact net value.");
        }
        await VerifyReceivingFreshOwnerAsync(fixture,goods,service,receiptId,auxiliaryId,serviceCertificates,result);
    }
}
