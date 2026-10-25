using System.Data;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    private const string GoodsReturnRoot="/api/procurement/v1/returns/principal-drafts";

    private static async Task VerifyPrincipalGoodsReturnAsync(PrincipalApprovalFixture fixture,string owner,
        PrincipalApprovalRuntimePrivileges.Connections roles,Dictionary<string,object?> result,
        Action<string> stage,PrincipalSend send,Action countRequest,PrincipalIssuedFixture goods)
    {
        Need(fixture.PrincipalGoodsReturnProfile&&fixture.PrincipalReceivingProfile&&fixture.PrincipalPoProfile,
            "P37 requires the real P30 issued Goods/acceptance cohort.");
        stage("P37ActualOrdinaryCompositionAndPhysicalReadiness");
        await using var host=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,true,true);
        host.PrepareEntryPointEnvironment();using var client=host.CreateClient(new(){AllowAutoRedirect=false});
        using(var scope=host.Services.CreateScope())
        {
            var actual=scope.ServiceProvider.GetService<IProcurementPrincipalGoodsReturnReadiness>();
            Need(actual is ProcurementPrincipalGoodsReturnStoreReadiness,
                "P37 normal Program must register its concrete provider; no substituted provider.");
        }
        var context=await send(client,HttpMethod.Get,"/api/procurement/v1/context",fixture.Receiver);
        Need(context.GetProperty("workerId").ValueKind==JsonValueKind.Null,
            "P37 actor acquired a fabricated employee identity.");
        var metadata=await send(client,HttpMethod.Get,"/api/procurement/v1/meta",fixture.Receiver);
        // The real execution transaction requires the request scope populated
        // by middleware. An unrelated root DI scope cannot certify readiness.
        Need(metadata.GetProperty("capabilities").GetProperty("principalGoodsReturnDrafts").GetBoolean(),
            "P37 normal metadata did not advertise the actual configured owner readiness.");
        result["p37ScopedMetadataReadinessVerified"]=true;
        Guid acceptedFact;JsonElement expectedLabels;
        await using(var db=fixture.Procurement())
        {
            var source=await db.PrincipalReceivingSourceStates.AsNoTracking()
                .SingleAsync(x=>x.PurchaseOrderLineId==goods.OrderLineId);
            var fact=await db.PrincipalReceivingFacts.AsNoTracking()
                .SingleAsync(x=>x.SourceStateId==source.Id&&x.Kind=="Acceptance"
                    &&x.Operation=="Accept"&&x.Quantity==0.9999m);
            acceptedFact=fact.Id;
            var acceptance=PrincipalReceivingStorageCodec.Decode<PrincipalReceivingAcceptance>(fact.PayloadJson,fact.PayloadSha256);
            var observedFact=await db.PrincipalReceivingFacts.AsNoTracking().SingleAsync(x=>x.Id==acceptance.ObservationId);
            var observed=PrincipalReceivingStorageCodec.Decode<PrincipalGoodsObservation>(observedFact.PayloadJson,observedFact.PayloadSha256);
            var order=await db.PurchaseOrders.AsNoTracking().SingleAsync(x=>x.Id==goods.OrderId);
            var line=await db.PurchaseOrderLines.AsNoTracking().SingleAsync(x=>x.Id==goods.OrderLineId);
            var issued=await db.PrincipalOrderIssuedVersions.AsNoTracking().SingleAsync(x=>x.Id==goods.IssuedId);
            using var snapshot=JsonDocument.Parse(issued.CommercialSnapshotJson);
            expectedLabels=JsonSerializer.SerializeToElement(new{purchaseOrderNumber=order.Number,
                supplierDisplayName=snapshot.RootElement.GetProperty("DisplayName").GetString(),
                lineNumber=line.LineNumber,description=line.Description,unitCode=observed.Source.Commercial.UnitCode,
                currencyCode=observed.Source.Commercial.CurrencyCode,deliveryNoteReference=observed.DeliveryNoteReference,
                receivedAt=observed.ObservedAt});
        }
        await using var ownerConnection=new NpgsqlConnection(owner);await ownerConnection.OpenAsync();
        var outsideBefore=await NonReturnBusinessRowsHashAsync(ownerConnection);
        var page=await send(client,HttpMethod.Get,GoodsReturnRoot+"/eligible-goods?offset=0",fixture.Receiver);
        Need(page.GetProperty("items").EnumerateArray().Any(x=>x.GetProperty("acceptanceFactId").GetGuid()==acceptedFact
            &&x.GetProperty("acceptedFactQuantityExact").GetString()=="0.9999"),
            "Eligible selector did not use the actual retained P30 Goods acceptance fact.");
        var eligible=page.GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("acceptanceFactId").GetGuid()==acceptedFact);
        CheckReturnLabels(eligible.GetProperty("labels"),expectedLabels);
        Need(await NonReturnBusinessRowsHashAsync(ownerConnection)==outsideBefore,
            "P37 eligible lookup changed P30/Inventory/Finance business rows.");

        stage("P37RealGoodsReturnCreateReplayRecover");
        var createKey=Guid.NewGuid();var create=new{acceptanceFactId=acceptedFact,quantity="0.5",
            reason="Private proposed goods return",evidenceReference="PRIVATE-RETURN:original"};
        var created=await send(client,HttpMethod.Post,GoodsReturnRoot,fixture.Receiver,create,HttpStatusCode.Created,createKey);
        CheckFrozenReturnAcknowledgement(created);
        var id=created.GetProperty("id").GetGuid();var path=GoodsReturnRoot+"/"+id;
        Need(created.GetProperty("status").GetString()=="Draft"&&created.GetProperty("version").GetInt64()==1
            &&created.GetProperty("quantityExact").GetString()=="0.5"
            &&created.GetProperty("draftOnly").GetBoolean()
            &&!created.GetProperty("stockChanged").GetBoolean()
            &&!created.GetProperty("financeChanged").GetBoolean()
            &&!created.GetProperty("supplierNotified").GetBoolean(),
            "P37 Create was not an internal no-effects Draft.");
        var replay=await send(client,HttpMethod.Post,GoodsReturnRoot,fixture.Receiver,create,HttpStatusCode.Created,createKey);
        Need(replay.GetRawText()==created.GetRawText(),"P37 Create replay did not preserve original 201 body.");
        await send(client,HttpMethod.Post,GoodsReturnRoot,fixture.Receiver,
            new{acceptanceFactId=acceptedFact,quantity="0.6",reason=create.reason,evidenceReference=create.evidenceReference},
            HttpStatusCode.Conflict,createKey);
        var recovery=await send(client,HttpMethod.Get,GoodsReturnRoot+"/commands/"+createKey,fixture.Receiver);
        Need(recovery.GetRawText()==created.GetRawText(),"P37 GET recovery changed original Create body.");
        var detail=await send(client,HttpMethod.Get,path,fixture.Receiver);
        Need(detail.GetProperty("id").GetGuid()==id&&detail.GetProperty("version").GetInt64()==1,
            "P37 detail failed retained Create readback.");
        CheckCurrentReturnView(detail,created,true,expectedLabels);
        var listed=await send(client,HttpMethod.Get,GoodsReturnRoot+"?offset=0",fixture.Receiver);
        Need(listed.GetProperty("items").EnumerateArray().Any(x=>x.GetProperty("id").GetGuid()==id),
            "P37 scoped list omitted original Draft.");
        CheckCurrentReturnView(listed.GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("id").GetGuid()==id),created,true,expectedLabels);
        Need(await NonReturnBusinessRowsHashAsync(ownerConnection)==outsideBefore,
            "P37 Create changed P30/Inventory/Finance business rows.");

        stage("P37CurrentCoreScopeAndStaleCasNegatives");
        var deniedBefore=await PrincipalFootprintAsync(ownerConnection);
        await send(client,HttpMethod.Post,GoodsReturnRoot,fixture.Ungranted,create,HttpStatusCode.Forbidden,Guid.NewGuid());
        var otherCreator=await send(client,HttpMethod.Get,path,fixture.ReceiverPeer);
        CheckCurrentReturnView(otherCreator,created,false,expectedLabels);
        await send(client,HttpMethod.Put,path,fixture.ReceiverPeer,
            new{expectedRevision=1L,quantity="0.5",reason="Not original creator",evidenceReference="PRIVATE-RETURN:denied"},
            HttpStatusCode.NotFound,Guid.NewGuid());
        await GoodsReturnOtherBranchAsync(client,fixture,path,countRequest);
        await send(client,HttpMethod.Post,GoodsReturnRoot,fixture.Receiver,
            new{acceptanceFactId=acceptedFact,quantity="1",reason=create.reason,evidenceReference=create.evidenceReference},
            HttpStatusCode.UnprocessableEntity,Guid.NewGuid());
        Need(await PrincipalFootprintAsync(ownerConnection)==deniedBefore,
            "P37 denied actor, branch or over-fact quantity changed retained evidence.");
        var reviseKey=Guid.NewGuid();var revise=new{expectedRevision=1L,quantity="0.75",
            reason="Revised private proposed return",evidenceReference="PRIVATE-RETURN:revised"};
        var revised=await send(client,HttpMethod.Put,path,fixture.Receiver,revise,HttpStatusCode.OK,reviseKey);
        Need(revised.GetProperty("version").GetInt64()==2&&revised.GetProperty("quantityExact").GetString()=="0.75",
            "P37 Revise did not persist exact quantity/version.");
        CheckFrozenReturnAcknowledgement(revised);
        CheckCurrentReturnView(await send(client,HttpMethod.Get,path,fixture.Receiver),revised,true,expectedLabels);
        var reviseReplay=await send(client,HttpMethod.Put,path,fixture.Receiver,revise,HttpStatusCode.OK,reviseKey);
        Need(reviseReplay.GetRawText()==revised.GetRawText(),"P37 Revise replay changed stored body.");
        var reviseRecovery=await send(client,HttpMethod.Get,GoodsReturnRoot+"/commands/"+reviseKey,fixture.Receiver);
        Need(reviseRecovery.GetRawText()==revised.GetRawText(),"P37 Revise GET recovery changed stored body.");
        deniedBefore=await PrincipalFootprintAsync(ownerConnection);
        await send(client,HttpMethod.Put,path,fixture.Receiver,revise,HttpStatusCode.Conflict,Guid.NewGuid());
        Need(await PrincipalFootprintAsync(ownerConnection)==deniedBefore,"P37 stale CAS wrote an extra revision.");

        stage("P37CurrentCoreGrantRevocation");
        await RevokeReturnActionAndCheckAsync(fixture,ownerConnection,client,send,path,revise,
            reviseKey,revised,expectedLabels);
        Need(await NonReturnBusinessRowsHashAsync(ownerConnection)==outsideBefore,
            "P37 Core revocation or restoration changed other business rows.");
        stage("P37CancelReplayAndOwnerReadback");
        var cancelKey=Guid.NewGuid();var cancel=new{expectedRevision=2L,reason="Private request abandoned"};
        var cancelled=await send(client,HttpMethod.Post,path+"/cancel",fixture.Receiver,cancel,HttpStatusCode.OK,cancelKey);
        Need(cancelled.GetProperty("status").GetString()=="Cancelled"
            &&cancelled.GetProperty("version").GetInt64()==3
            &&cancelled.GetProperty("quantityExact").GetString()=="0.75"
            &&cancelled.GetProperty("evidenceReference").GetString()==revise.evidenceReference,
            "P37 Cancel rewrote the proposed quantity/evidence.");
        CheckFrozenReturnAcknowledgement(cancelled);
        var cancelReplay=await send(client,HttpMethod.Post,path+"/cancel",fixture.Receiver,cancel,HttpStatusCode.OK,cancelKey);
        Need(cancelReplay.GetRawText()==cancelled.GetRawText(),"P37 Cancel replay changed original body.");
        var cancelRecovery=await send(client,HttpMethod.Get,GoodsReturnRoot+"/commands/"+cancelKey,fixture.Receiver);
        Need(cancelRecovery.GetRawText()==cancelled.GetRawText(),"P37 Cancel GET recovery changed stored body.");
        var final=await send(client,HttpMethod.Get,path,fixture.Receiver);
        Need(final.GetProperty("status").GetString()=="Cancelled"&&final.GetProperty("version").GetInt64()==3,
            "P37 Cancel was not durably visible in scoped detail.");
        CheckCurrentReturnView(final,cancelled,false,expectedLabels);
        Need(await NonReturnBusinessRowsHashAsync(ownerConnection)==outsideBefore,
            "P37 Draft lifecycle changed P30/Inventory/Finance or unrelated rows.");
        await using(var db=fixture.Procurement())
        {
            Need(await db.PrincipalGoodsReturnDrafts.CountAsync()==1
                &&await db.PrincipalGoodsReturnRevisions.CountAsync()==3
                &&await db.PrincipalGoodsReturnCommandReceipts.CountAsync()==3,
                "P37 owner reload found duplicated or missing original revisions/receipts.");
            var messages=await db.OutboxEvents.AsNoTracking()
                .Where(x=>x.AggregateType=="PrincipalGoodsReturnDraft").ToArrayAsync();
            Need(messages.Length==3&&messages.All(x=>
            {
                using var json=JsonDocument.Parse(x.PayloadJson);
                return !json.RootElement.GetProperty("approved").GetBoolean()
                    &&!json.RootElement.GetProperty("dispatched").GetBoolean()
                    &&!json.RootElement.GetProperty("stockChanged").GetBoolean()
                    &&!json.RootElement.GetProperty("financeChanged").GetBoolean();
            }),"P37 internal metadata events implied return dispatch, stock or Finance posting.");
        }
        result["principalGoodsReturn"]=new{status="PASS_PRIVATE_P37_GOODS_RETURN_DRAFT_ONLY",
            acceptedFactId=acceptedFact,draftId=id,createKey,reviseKey,cancelKey,
            revisions=3,originalReceiptCount=3,currentCoreGrantRevocationDenied=true,
            p30AndOtherBusinessRowsUnchanged=true,physicalReturnDispatched=false,
            stockPostingPerformed=false,financePostingPerformed=false,productionVerified=false};
        result["status"]="PASS_PRIVATE_P37_GOODS_RETURN_DRAFT_ONLY";
    }

    private static async Task RevokeReturnActionAndCheckAsync(PrincipalApprovalFixture fixture,
        NpgsqlConnection ownerConnection,HttpClient client,PrincipalSend send,string path,
        object staleBody,Guid reviseKey,JsonElement originalAck,JsonElement expectedLabels)
    {
        string original;
        await using(var core=fixture.Core())
        {
            var role=await core.TenantRoleAssignments.SingleAsync(x=>x.RoleKey=="p30_receiving_actor");
            original=role.PermissionsCsv;
            Need(original.Contains("procurement-return-edit",StringComparison.Ordinal),
                "P37 fixture did not persist the explicit current Core Edit grant.");
            role.PermissionsCsv=original.Replace(",procurement-return-edit",string.Empty,StringComparison.Ordinal);
            await core.SaveChangesAsync();
        }
        try
        {
            var deniedBefore=await PrincipalFootprintAsync(ownerConnection);
            var stillReadable=await send(client,HttpMethod.Get,path,fixture.Receiver);
            CheckCurrentReturnView(stillReadable,originalAck,false,expectedLabels);
            await send(client,HttpMethod.Put,path,fixture.Receiver,staleBody,HttpStatusCode.Forbidden,Guid.NewGuid());
            await send(client,HttpMethod.Get,GoodsReturnRoot+"/commands/"+reviseKey,
                fixture.Receiver,null,HttpStatusCode.Forbidden);
            Need(await PrincipalFootprintAsync(ownerConnection)==deniedBefore,
                "Revoked current Edit grant changed a retained Draft or receipt.");
        }
        finally
        {
            await using var core=fixture.Core();
            var role=await core.TenantRoleAssignments.SingleAsync(x=>x.RoleKey=="p30_receiving_actor");
            role.PermissionsCsv=original;await core.SaveChangesAsync();
        }
    }

    private static void CheckFrozenReturnAcknowledgement(JsonElement body)
        =>Need(!body.TryGetProperty("allowedActions",out _)&&!body.TryGetProperty("labels",out _),
            "Dynamic read hints entered the immutable original acknowledgement.");

    private static void CheckCurrentReturnView(JsonElement view,JsonElement originalAck,bool canEdit,JsonElement expectedLabels)
    {
        foreach(var key in new[]{"id","branchId","receiptId","acceptanceFactId","purchaseOrderId","purchaseOrderLineId"})
            Need(view.GetProperty(key).GetGuid()==originalAck.GetProperty(key).GetGuid(),"P37 current read source binding changed.");
        Need(view.GetProperty("version").GetInt64()==originalAck.GetProperty("version").GetInt64(),"P37 current read CAS changed.");
        foreach(var key in new[]{"status","quantityExact","acceptedFactQuantityExact","reason","evidenceReference"})
            Need(view.GetProperty(key).GetString()==originalAck.GetProperty(key).GetString(),"P37 current read retained fields changed.");
        var actions=view.GetProperty("allowedActions").EnumerateArray().Select(x=>x.GetString()).ToArray();
        Need(actions.SequenceEqual(canEdit?new[]{"Edit","Cancel"}:Array.Empty<string>()),"P37 current creator/state/action hints are wrong.");
        Need(view.GetProperty("draftOnly").GetBoolean()&&!view.GetProperty("stockChanged").GetBoolean()
            &&!view.GetProperty("financeChanged").GetBoolean()&&!view.GetProperty("supplierNotified").GetBoolean(),
            "P37 current read implied external effects.");
        CheckReturnLabels(view.GetProperty("labels"),expectedLabels);
    }

    private static void CheckReturnLabels(JsonElement labels,JsonElement expected)
    {
        foreach(var key in new[]{"purchaseOrderNumber","supplierDisplayName","description","unitCode","currencyCode","deliveryNoteReference"})
            Need(labels.GetProperty(key).GetString()==expected.GetProperty(key).GetString(),"P37 retained source label changed.");
        Need(labels.GetProperty("lineNumber").GetInt32()==expected.GetProperty("lineNumber").GetInt32()
            &&labels.GetProperty("receivedAt").GetDateTimeOffset()==expected.GetProperty("receivedAt").GetDateTimeOffset(),
            "P37 actual physical source line/time changed.");
    }

    private static async Task GoodsReturnOtherBranchAsync(HttpClient client,PrincipalApprovalFixture fixture,
        string detailPath,Action countRequest)
    {
        async Task<JsonElement?> Read(string path,HttpStatusCode expected)
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,path);
            request.Headers.Add("X-Private-P25-Subject",fixture.ReceivingOutsider.ToString("D"));
            request.Headers.Add("X-Shell-Entity",fixture.Company.ToString("D"));
            request.Headers.Add("X-Shell-Location",fixture.OtherReceivingBranch.ToString("D"));
            countRequest();using var response=await client.SendAsync(request);
            Need(response.StatusCode==expected,"P37 other-branch action exposed source or Draft.");
            if(expected!=HttpStatusCode.OK)return null;
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.Clone();
        }
        await Read(detailPath,HttpStatusCode.NotFound);
        var list=(await Read(GoodsReturnRoot+"?offset=0",HttpStatusCode.OK))!.Value;
        Need(list.GetProperty("items").GetArrayLength()==0,"P37 other-branch list leaked Draft.");
        var eligible=(await Read(GoodsReturnRoot+"/eligible-goods?offset=0",HttpStatusCode.Forbidden));
        _=eligible; // Outsider has read only, not the separate source-creation action.
    }

    /// <summary>All non-P37 business rows must remain byte-identical across a
    /// Draft lifecycle. Audit/outbox are explicitly allowed internal companions.</summary>
    private static async Task<string> NonReturnBusinessRowsHashAsync(NpgsqlConnection connection)
    {
        await using var tx=await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await using(var safe=new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL TIME ZONE 'UTC';",connection,tx))
            await safe.ExecuteNonQueryAsync();
        var rows=new List<(string Schema,string Table)>();
        await using(var command=new NpgsqlCommand("""
            SELECT n.nspname,c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE c.relkind IN ('r','p') AND n.nspname NOT LIKE 'pg_%'
              AND n.nspname<>'information_schema'
              AND NOT (n.nspname='procurement' AND c.relname IN
                ('principal_goods_return_drafts','principal_goods_return_revisions',
                 'principal_goods_return_receipts','audit_events','outbox_events'))
            ORDER BY 1,2
            """,connection,tx))
        await using(var reader=await command.ExecuteReaderAsync())
            while(await reader.ReadAsync())rows.Add((reader.GetString(0),reader.GetString(1)));
        var hash=new StringBuilder();
        foreach(var (schema,table) in rows)
        {
            var relation=PrincipalApprovalRuntimePrivileges.Q(schema)+"."+PrincipalApprovalRuntimePrivileges.Q(table);
            var fixtureRoleOnly=schema=="hrm"&&table=="tenant_role_assignments"
                ? " WHERE t.role_key <> 'p30_receiving_actor'" : string.Empty;
            await using var command=new NpgsqlCommand("""
                SELECT count(*)::text||':'||encode(sha256(convert_to(coalesce(string_agg(h,'' ORDER BY h),''),'UTF8')),'hex')
                FROM (SELECT encode(sha256(convert_to(to_jsonb(t)::text,'UTF8')),'hex') h FROM
                """+relation+" t"+fixtureRoleOnly+") q",connection,tx);
            hash.Append(schema).Append('.').Append(table).Append(':')
                .Append((string)(await command.ExecuteScalarAsync())!).Append('\n');
        }
        await tx.RollbackAsync();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hash.ToString())));
    }
}
