using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed partial class ProcurementRequestHttpTests
{
    // Explicit synthetic mapped-route readiness only. This is not a production schema/role proof.
    private sealed class PrincipalApprovalTestSchemaReadiness(bool ready):IProcurementPrincipalApprovalSchemaReadiness
    {
        public Task<bool> IsReadyAsync(ProcurementDbContext owner,CancellationToken ct)=>Task.FromResult(ready);
        public Task<bool> CanRecordAsync(ProcurementDbContext owner,CancellationToken ct)=>Task.FromResult(ready);
        public Task<bool> CanExecuteAsync(ProcurementDbContext owner,CancellationToken ct)=>Task.FromResult(ready);
    }

    [Theory]
    [InlineData(false,false,0)][InlineData(true,false,0)][InlineData(false,true,0)][InlineData(true,true,1)]
    public async Task New_request_original_provenance_requires_explicit_activation_and_owned_successor_readiness(bool enabled,bool ready,int expected)
    {
        await using var f=await Fixture.Start(principalApprovalEnabled:enabled,principalApprovalSchema:ready);
        await f.ConfigurePrincipalRequester(f.Owner,false);
        var response=await SendJson(f.Client,HttpMethod.Post,PrincipalRequests,f.Owner,new {purpose="Explicit principal funding source",
            lines=new[]{new {description="Supplies",unitCode="each",requestedQuantity="1",estimatedUnitPrice="1.0001"}}});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var body=await response.Content.ReadFromJsonAsync<JsonElement>();
        var stored=await f.PrincipalRequestStored(body.GetProperty("id").GetGuid());
        Assert.Equal(expected,stored.RequesterIdentitySchemaVersion);Assert.Null(stored.RequesterCoreWorkerId);
        if(expected==1)Assert.True(PurchaseRequestPrincipalIdentity.Valid(stored));else Assert.Null(stored.RequesterIdentitySha256);
    }

    [Theory]
    [InlineData("1", "1.0001", "1.0001", "1.0001")]
    [InlineData("1", "99999999999999.9999", "99999999999999.9999", "99999999999999.9999")]
    [InlineData("1.0001", "1.0001", "1.00020001", "1.0003")]
    [InlineData("0.0001", "0.0001", "0.00000001", "0.0001")]
    public async Task Real_mapped_workerless_funding_capture_and_exact_recorder_recovery_preserve_current_Core_grants(string quantity, string unitPrice, string estimate, string exactAmount)
    {
        await using var f=await Fixture.Start(principalApprovalEnabled:true,principalApprovalSchema:true);
        await f.ConfigurePrincipalRequester(f.Owner,false);await f.ConfigurePrincipalRequester(f.Other,false);
        await f.ConfigureCoreActions(f.Other,"procurement-access,procurement-budget-check");
        var created=await SendJson(f.Client,HttpMethod.Post,PrincipalRequests,f.Owner,new {purpose="Independent principal funding",neededBy=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),
            lines=new[]{new {description="Supplies",unitCode="each",requestedQuantity=quantity,estimatedUnitPrice=unitPrice}}});
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);var original=await created.Content.ReadFromJsonAsync<JsonElement>();
        var id=original.GetProperty("id").GetGuid();var path=$"{PrincipalRequests}/{id}";
        var submitted=await SendJson(f.Client,HttpMethod.Post,path+"/submit",f.Owner,new {version=original.GetProperty("version").GetInt64()});
        Assert.Equal(HttpStatusCode.OK,submitted.StatusCode);var source=await submitted.Content.ReadFromJsonAsync<JsonElement>();
        var queue=await Send(f.Client,HttpMethod.Get,PrincipalRequests+"/budget-queue",f.Other);
        Assert.Equal(HttpStatusCode.OK,queue.StatusCode);
        var queued=(await queue.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("records").EnumerateArray().Single(x=>x.GetProperty("id").GetGuid()==id);
        Assert.Equal(estimate,queued.GetProperty("estimatedAmountExact").GetString());
        Assert.Equal(exactAmount,queued.GetProperty("minimumCheckedAmountExact").GetString());
        var input=new {requestVersion=source.GetProperty("version").GetInt64(),financeCheckRef="PRINCIPAL-FIN-ONE",status="Available",
            checkedAmount=exactAmount,currencyCode="ZMW",evidenceRef="Synthetic manually reviewed Finance reference",
            reservationRef="Synthetic manual reservation reference",reservationStatus="Confirmed"};
        var captured=await SendJson(f.Client,HttpMethod.Post,path+"/budget-checks",f.Other,input);
        Assert.Equal(HttpStatusCode.Created,captured.StatusCode);var capture=await captured.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2,capture.GetProperty("checkerIdentitySchemaVersion").GetInt32());
        Assert.Equal(exactAmount,capture.GetProperty("checkedAmountExact").GetString());
        var history=await Send(f.Client,HttpMethod.Get,path+"/budget-checks",f.Other);
        Assert.Equal(HttpStatusCode.OK,history.StatusCode);
        var retained=(await history.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("records").EnumerateArray().Single();
        Assert.Equal(exactAmount,retained.GetProperty("checkedAmountExact").GetString());
        Assert.Equal(JsonValueKind.Null,capture.GetProperty("checkedByWorkerId").ValueKind);
        var before=await f.OriginalValueFootprint();
        Assert.Equal(HttpStatusCode.OK,(await SendJson(f.Client,HttpMethod.Post,path+"/budget-checks",f.Other,input)).StatusCode);
        Assert.Equal(before,await f.OriginalValueFootprint());
        await f.ConfigureCoreActions(f.Other,"procurement-access");
        Assert.Equal(HttpStatusCode.Forbidden,(await SendJson(f.Client,HttpMethod.Post,path+"/budget-checks",f.Other,input)).StatusCode);
        Assert.Equal(before,await f.OriginalValueFootprint());
    }
}
