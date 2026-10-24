using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    private static async Task ReceivingOtherBranchAsync(HttpClient client,PrincipalApprovalFixture fixture,
        string receiptPath,Dictionary<string,object?> result,Action countRequest)
    {
        async Task<JsonElement?> Read(Guid actor,string path,HttpStatusCode expected)
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,path);
            request.Headers.Add("X-Private-P25-Subject",actor.ToString("D"));
            request.Headers.Add("X-Shell-Entity",fixture.Company.ToString("D"));
            request.Headers.Add("X-Shell-Location",fixture.OtherReceivingBranch.ToString("D"));
            countRequest();using var response=await client.SendAsync(request);
            Need(response.StatusCode==expected,"Receiving selected-branch admission/record confinement failed.");
            if(expected!=HttpStatusCode.OK)return null;
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());return json.RootElement.Clone();
        }
        await Read(fixture.Receiver,receiptPath+"/principal-draft",HttpStatusCode.Forbidden);
        await Read(fixture.ReceivingOutsider,receiptPath+"/principal-draft",HttpStatusCode.NotFound);
        var page=(await Read(fixture.ReceivingOutsider,ReceivingRoot+"/receiving/principal-worklist?offset=0&limit=10",HttpStatusCode.OK))!.Value;
        Need(page.GetProperty("total").GetInt32()==0&&page.GetProperty("records").GetArrayLength()==0,
            "Other-branch receiving count/page disclosed the original receipt.");
        result["principalReceivingBranchConfinement"]=true;
    }

    private static async Task VerifyReceivingTamperDenialsAsync(NpgsqlConnection owner,PrincipalApprovalFixture fixture,
        HttpClient client,PrincipalSend send,string receiptPath,Guid logicalLine,PrincipalIssuedFixture goods,
        Guid acceptanceKey)
    {
        // Negative-only owner corruption. Every change is restored from the actual
        // original row values in finally, then a full footprint equality is required.
        // Nothing here is used to construct a successful business response.
        var baseline=await PrincipalFootprintAsync(owner);
        decimal poQuantity,prQuantity;
        await using(var db=fixture.Procurement())
        {
            poQuantity=(await db.PurchaseOrderLines.SingleAsync(x=>x.Id==goods.OrderLineId)).AcceptedQuantity;
            prQuantity=(await db.PurchaseRequestLines.SingleAsync(x=>x.Id==goods.RequestLineId)).AcceptedQuantity;
        }
        var taskPage=await send(client,HttpMethod.Get,receiptPath+"/principal-acceptance-task?offset=0&limit=25",fixture.Acceptor);
        var task=taskPage.GetProperty("records").EnumerateArray().Single();
        async Task Counters(decimal po,decimal pr)
        {
            await using var transaction=await owner.BeginTransactionAsync();
            foreach(var tuple in new[]{(Table:"purchase_order_lines",Id:goods.OrderLineId,Quantity:po),(Table:"purchase_request_lines",Id:goods.RequestLineId,Quantity:pr)})
            {
                // Table names are a fixed allowlist above; all data is parameterized.
                await using var command=new NpgsqlCommand($"UPDATE procurement.{tuple.Table} SET accepted_quantity=@value WHERE tenant_id=@tenant AND legal_entity_id=@company AND id=@id",owner,transaction);
                command.Parameters.AddWithValue("value",tuple.Quantity);command.Parameters.AddWithValue("tenant",PrincipalApprovalFixture.Tenant);
                command.Parameters.AddWithValue("company",fixture.Company);command.Parameters.AddWithValue("id",tuple.Id);
                Need(await command.ExecuteNonQueryAsync()==1,"Private receiving counter negative did not target exactly one original row.");
            }
            await transaction.CommitAsync();
        }
        try
        {
            await Counters(poQuantity+0.25m,prQuantity+0.25m);
            var corrupted=await PrincipalFootprintAsync(owner);
            await send(client,HttpMethod.Get,receiptPath+"/principal-draft",fixture.Receiver,null,HttpStatusCode.Conflict);
            await send(client,HttpMethod.Post,$"{receiptPath}/lines/{logicalLine}/principal-accept",fixture.Acceptor,
                new{version=task.GetProperty("version").GetInt64(),sourceVersion=task.GetProperty("sourceVersion").GetInt64(),quantity="0.0001",evidenceReference="PRIVATE-REC:counter-divergence-denied"},HttpStatusCode.Conflict,Guid.NewGuid());
            Need(await PrincipalFootprintAsync(owner)==corrupted,"Diverged counter rejection left a partial effect.");
        }
        finally{await Counters(poQuantity,prQuantity);}
        Need(await PrincipalFootprintAsync(owner)==baseline,"Counter negative did not restore original complete source footprint.");
        string originalSha;
        await using(var db=fixture.Procurement())originalSha=(await db.PrincipalReceivingCommandReceipts.SingleAsync(x=>x.CommandKey==acceptanceKey)).ResponseSha256;
        async Task ReceiptSha(string sha)
        {
            await using var command=new NpgsqlCommand("UPDATE procurement.principal_receiving_receipts SET response_sha256=@sha WHERE tenant_id=@tenant AND legal_entity_id=@company AND command_key=@key",owner);
            command.Parameters.AddWithValue("sha",sha);command.Parameters.AddWithValue("tenant",PrincipalApprovalFixture.Tenant);
            command.Parameters.AddWithValue("company",fixture.Company);command.Parameters.AddWithValue("key",acceptanceKey);
            Need(await command.ExecuteNonQueryAsync()==1,"Private receipt negative did not target one retained command.");
        }
        try
        {
            await ReceiptSha(originalSha==new string('b',64)?new string('c',64):new string('b',64));
            var corrupted=await PrincipalFootprintAsync(owner);
            await send(client,HttpMethod.Get,receiptPath+"/principal-draft",fixture.Receiver,null,HttpStatusCode.Conflict);
            Need(await PrincipalFootprintAsync(owner)==corrupted,"Broken original receipt rejection changed retained evidence.");
        }
        finally{await ReceiptSha(originalSha);}
        Need(await PrincipalFootprintAsync(owner)==baseline,"Receipt negative did not restore original complete source footprint.");
        // Same original rows, exact ownership FK (not a fabricated positive fact).
        await using(var transaction=await owner.BeginTransactionAsync())
        {
            try
            {
                await using var command=new NpgsqlCommand("UPDATE procurement.principal_receiving_receipts SET actor_principal_id=@actor WHERE tenant_id=@tenant AND legal_entity_id=@company AND command_key=@key; SET CONSTRAINTS ALL IMMEDIATE",owner,transaction);
                command.Parameters.AddWithValue("actor",Guid.NewGuid().ToString("D"));command.Parameters.AddWithValue("tenant",PrincipalApprovalFixture.Tenant);
                command.Parameters.AddWithValue("company",fixture.Company);command.Parameters.AddWithValue("key",acceptanceKey);
                await command.ExecuteNonQueryAsync();Need(false,"Wrong-actor original receipt escaped its exact fact binding.");
            }
            catch(PostgresException error) when(error.SqlState=="23503"&&error.ConstraintName=="fk_prcv_exact_fact_receipt"){}
            finally{await transaction.RollbackAsync();}
        }
        Need(await PrincipalFootprintAsync(owner)==baseline,"Exact receipt ownership FK negative changed retained rows.");
    }
}
