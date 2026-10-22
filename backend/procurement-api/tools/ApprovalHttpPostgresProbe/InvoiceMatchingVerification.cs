using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    /// <summary>Real routes and PostgreSQL fixture with intentionally unavailable real owners.
    /// This is proof of durable blocked comparison and recovery, never synthetic Finance completeness.</summary>
    public async Task VerifyUnavailableInvoiceMatching()
    {
        Guid orderId,lineId,issuedId,receiptLineId,supplierId;
        using(var services=Scope(OrderBuyer))
        {
            var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var effect=await db.ReceivingAcceptanceEffects.Where(x=>x.GoodsReceiptLineId!=null).OrderBy(x=>x.Id).FirstAsync();
            orderId=effect.Source.PurchaseOrderId; lineId=effect.Source.PurchaseOrderLineId; issuedId=effect.Source.IssuedVersionId;
            receiptLineId=effect.GoodsReceiptLineId!.Value; supplierId=await db.PurchaseOrders.Where(x=>x.Id==orderId).Select(x=>x.SupplierId).SingleAsync();
        }
        await VerifyExactInvoiceIntake(supplierId);
        var capture=await Send(HttpMethod.Post,Root+"/invoices",OrderBuyer,new { supplierId,purchaseOrderId=orderId,
            invoiceNumber="MATCHING-UNAVAILABLE-1",invoiceDate=new DateOnly(2026,10,20),currencyCode="ZMW",
            lines=new[] { new { description="Original invoice",quantity=1m,unitPrice=100m,taxAmount=0m } } });
        await Check(capture,HttpStatusCode.Created); var invoice=await capture.Content.ReadFromJsonAsync<JsonElement>(); var id=invoice.GetProperty("id").GetGuid();
        var detail=await Get($"{Root}/invoices/{id}",OrderBuyer); var invoiceLineId=detail.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var input=new { version=invoice.GetProperty("version").GetInt64(),purchaseOrderId=orderId,issuedVersionId=issuedId,
            lines=new[] { new { invoiceLineId,purchaseOrderLineId=lineId,goodsReceiptLineId=receiptLineId,serviceAcceptanceId=(Guid?)null,
                invoiceUnitCode="each",documentVersionId=Guid.Empty,documentLineReference="" } } };
        var mapKey=Guid.NewGuid(); var mapped=await Send(HttpMethod.Post,$"{Root}/invoices/{id}/matching/mappings",OrderBuyer,input,mapKey);
        await Check(mapped,HttpStatusCode.OK); var mapText=await mapped.Content.ReadAsStringAsync(); var mapping=JsonDocument.Parse(mapText).RootElement.GetProperty("result");
        var compareInput=new { version=mapping.GetProperty("version").GetInt64(),mappingVersionId=mapping.GetProperty("id").GetGuid(),reason="Demonstrate unknown owner history blocks comparison" };
        var compareKey=Guid.NewGuid();
        await Expect(HttpStatusCode.Conflict,HttpMethod.Post,$"{Root}/invoices/{id}/matching/compare",OrderBuyer,compareInput,compareKey);
        using(var services=Scope(OrderBuyer))
        {
            var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            Require(!await db.InvoiceMatchResults.AnyAsync(x=>x.SupplierInvoiceId==id),"Missing matching policy persisted a fabricated comparison.");
        }
        await PublishMatchingPolicy();
        await VerifyInvoiceSourceSelectors(id,orderId,lineId,issuedId,receiptLineId);
        var compared=await Send(HttpMethod.Post,$"{Root}/invoices/{id}/matching/compare",OrderBuyer,compareInput,compareKey);
        await Check(compared,HttpStatusCode.OK); var comparedText=await compared.Content.ReadAsStringAsync(); var result=JsonDocument.Parse(comparedText).RootElement.GetProperty("result");
        Require(result.GetProperty("outcome").GetString()=="Blocked" && !result.GetProperty("hasCapacityClaim").GetBoolean()
            && !result.GetProperty("canPostToFinance").GetBoolean(),"Unavailable owner/reviewer fabricated matching/AP authority.");
        await VerifyInvoiceAllocationOwnership(id,result.GetProperty("id").GetGuid());
        await VerifyInvoiceMatchingRequiredEvidence(id,result.GetProperty("id").GetGuid());
        var retry=await Send(HttpMethod.Post,$"{Root}/invoices/{id}/matching/compare",OrderBuyer,compareInput,compareKey);
        await Check(retry,HttpStatusCode.OK); Require(await retry.Content.ReadAsStringAsync()==comparedText,"Blocked comparison exact retry changed durable receipt.");
        await Expect(HttpStatusCode.Conflict,HttpMethod.Post,$"{Root}/invoices/{id}/matching/compare",OrderBuyer,
            new { compareInput.version,compareInput.mappingVersionId,reason="Different body" },compareKey);
        await Expect(HttpStatusCode.Forbidden,HttpMethod.Post,$"{Root}/invoices/{id}/matching/compare",OrderRequester,compareInput,compareKey);
        var recovery=await Get($"{Root}/invoices/{id}/matching/commands/{compareKey}?operation=InvoiceCompare",OrderBuyer);
        Require(recovery.GetProperty("recordedHistoricalResult").GetBoolean() && !recovery.GetProperty("isCurrentReplayAuthorization").GetBoolean(),"Recovery claimed current authority.");
        Guid otherBranch;
        using(var services=Scope(OrderBuyer))
        {
            var core=services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var other=new WorkLocation { LegalEntityId=OrderCompany,Code="MATCHING-OTHER",Name="Other matching context" };
            core.WorkLocations.Add(other); await core.SaveChangesAsync(); otherBranch=other.Id;
        }
        async Task<HttpResponseMessage> AtOtherBranch(HttpMethod method,string path,object? body=null,Guid? commandKey=null)
        {
            var request=new HttpRequestMessage(method,path); request.Headers.Add("X-Probe-User",OrderBuyer.ToString("D"));
            request.Headers.Add("X-Shell-Entity",OrderCompany.ToString("D")); request.Headers.Add("X-Shell-Location",otherBranch.ToString("D"));
            if(commandKey is { } k) request.Headers.Add("Idempotency-Key",k.ToString("D"));
            if(body is not null) request.Content=JsonContent.Create(body,options:Json);
            return await client.SendAsync(request);
        }
        // Gate 1: the selected branch is not assigned. Middleware must deny before any
        // invoice/replay lookup; a record-level 404 is not the expected contract here.
        Guid? buyerWorker; string buyerRoles;
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var identity = await core.LocalUsers.AsNoTracking().SingleAsync(x => x.Id == OrderBuyer);
            buyerWorker = identity.WorkerId; buyerRoles = identity.RolesCsv;
            Require(identity.IsActive && !identity.IsArchived && buyerWorker is not null,
                "Foreign-branch probe requires the existing active canonical buyer.");
            Require(await core.UserBranchAssignments.AnyAsync(x => x.UserId == OrderBuyer && x.LocationId == OrderBranch && !x.IsArchived)
                && !await core.UserBranchAssignments.AnyAsync(x => x.UserId == OrderBuyer && x.LocationId == otherBranch && !x.IsArchived),
                "Foreign-branch probe must begin confined to its actual home assignment, not empty=organisation-wide access.");
        }
        var beforeUnassigned = await InvoiceApReadFootprint(id);
        await Check(await AtOtherBranch(HttpMethod.Post,$"{Root}/invoices/{id}/matching/compare",compareInput,compareKey),HttpStatusCode.Forbidden);
        await Check(await AtOtherBranch(HttpMethod.Get,$"{Root}/invoices/{id}/matching/commands/{compareKey}?operation=InvoiceCompare"),HttpStatusCode.Forbidden);
        await Check(await AtOtherBranch(HttpMethod.Get,$"{Root}/invoices/{id}/matching"),HttpStatusCode.Forbidden);
        Require(beforeUnassigned == await InvoiceApReadFootprint(id), "Unassigned selected-branch denials changed invoice/history/claim/audit/outbox evidence.");

        // Gate 2: explicitly grant only the synthetic new branch through the existing
        // Core membership model. Current action grants/account/worker and home access stay intact.
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            core.UserBranchAssignments.Add(new Mightyfin.Erp.Hrm.Application.HrUserBranchAssignment
                { UserId = OrderBuyer, LocationId = otherBranch });
            await core.SaveChangesAsync();
            var identity = await core.LocalUsers.AsNoTracking().SingleAsync(x => x.Id == OrderBuyer);
            Require(identity.IsActive && !identity.IsArchived && identity.WorkerId == buyerWorker && identity.RolesCsv == buyerRoles
                && await core.UserBranchAssignments.AnyAsync(x => x.UserId == OrderBuyer && x.LocationId == OrderBranch && !x.IsArchived)
                && await core.UserBranchAssignments.AnyAsync(x => x.UserId == OrderBuyer && x.LocationId == otherBranch && !x.IsArchived),
                "Adding synthetic branch membership changed canonical identity/actions or removed the original home scope.");
        }
        var beforeForeignInvoice = await InvoiceApReadFootprint(id);
        await Check(await AtOtherBranch(HttpMethod.Post,$"{Root}/invoices/{id}/matching/compare",compareInput,compareKey),HttpStatusCode.NotFound);
        await Check(await AtOtherBranch(HttpMethod.Get,$"{Root}/invoices/{id}/matching/commands/{compareKey}?operation=InvoiceCompare"),HttpStatusCode.NotFound);
        await Check(await AtOtherBranch(HttpMethod.Get,$"{Root}/invoices/{id}/matching"),HttpStatusCode.NotFound);
        Require(beforeForeignInvoice == await InvoiceApReadFootprint(id), "Authorized foreign-branch invoice denials changed invoice/history/claim/audit/outbox evidence.");
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            // Unselected confined context picks the first assigned UUID, not necessarily home.
            // Retire only this temporary fixture membership before later original-branch scenarios.
            (await core.UserBranchAssignments.SingleAsync(x => x.UserId == OrderBuyer && x.LocationId == otherBranch && !x.IsArchived)).IsArchived = true;
            await core.SaveChangesAsync();
            Require(await core.UserBranchAssignments.AnyAsync(x => x.UserId == OrderBuyer && x.LocationId == OrderBranch && !x.IsArchived),
                "Foreign-branch cleanup removed original home access.");
        }
        var state=await Get($"{Root}/invoices/{id}/matching",OrderBuyer);
        Require(state.GetProperty("comparisons").GetArrayLength()==1 && !state.GetProperty("canPostToFinance").GetBoolean(),"Matching history duplicated a retry or claimed AP readiness.");
        var concurrentInput=new { version=result.GetProperty("version").GetInt64(),compareInput.mappingVersionId,reason="Two separate comparison attempts on one invoice version" };
        var concurrent=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Send(HttpMethod.Post,$"{Root}/invoices/{id}/matching/compare",OrderBuyer,concurrentInput,Guid.NewGuid())));
        Require(concurrent.Count(x=>x.StatusCode==HttpStatusCode.OK)==1 && concurrent.Count(x=>x.StatusCode==HttpStatusCode.Conflict)==1,
            "Concurrent comparison did not preserve one immutable result and one safe version conflict.");
        var beforeReadiness = await InvoiceApReadFootprint(id);
        for (var read = 0; read < 2; read++)
        {
            var ap = await Get($"{Root}/invoices/{id}/finance-readiness", OrderBuyer);
            Require(ap.GetProperty("state").GetString() == "Blocked" && !ap.GetProperty("canPrepareHandoff").GetBoolean(),
                "Recorded comparison became AP handoff authority.");
            Require(ap.GetProperty("evidence").GetProperty("matchingSchemaAvailable").GetBoolean()
                && ap.GetProperty("evidence").GetProperty("recordedComparison").GetProperty("outcome").GetString() == "Blocked",
                "Actual owned blocked comparison was not shown as recorded evidence.");
            var blockers = ap.GetProperty("blockers").EnumerateArray().Select(x => x.GetProperty("code").GetString()).ToArray();
            Require(blockers.Contains("InvoiceValidationAuthorityUnavailable") && blockers.Contains("FinanceReceiverUnavailable"),
                "Unavailable invoice approval or Finance owner was silently inferred.");
            Require(!ap.GetRawText().Contains("allocations", StringComparison.OrdinalIgnoreCase)
                && !ap.GetRawText().Contains("ownerReference", StringComparison.OrdinalIgnoreCase), "AP readiness exported private source/owner history.");
        }
        Require(beforeReadiness == await InvoiceApReadFootprint(id), "AP readiness GET created or changed invoice/claim/Finance evidence.");
        using(var services=Scope(OrderBuyer))
        {
            var db=services.ServiceProvider.GetRequiredService<ProcurementDbContext>(); var saved=await db.SupplierInvoices.SingleAsync(x=>x.Id==id);
            Require(saved.Status=="Received" && saved.FinancePostingRef==null && saved.ActiveMatchClaimResultId==null,"Comparison changed invoice approval/Finance ownership.");
            Require(!await db.InvoiceMatchAllocations.AnyAsync(x=>x.SupplierInvoiceId==id),"Unknown owner checkpoint was treated as complete zero history.");
            Require(await db.InvoiceExceptions.AnyAsync(x=>x.SupplierInvoiceId==id && x.OwnerWorkerId!=null),"Blocked invoice lacks a responsible exception owner.");
            var retainedComparison=await db.InvoiceMatchResults.SingleAsync(x=>x.Id==saved.LastMatchResultId);
            var expectedEvent=retainedComparison.ExecutionVersion==2 ? "supplier_invoice.compared.v2" : "supplier_invoice.compared.v1";
            Require(await db.OutboxEvents.AnyAsync(x=>x.AggregateId==id && x.EventType==expectedEvent),"Comparison missing versioned transactional outbox evidence.");
        }
    }
    private async Task<string> InvoiceApReadFootprint(Guid id)
    {
        using var services = Scope(OrderBuyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        return JsonSerializer.Serialize(new
        {
            invoice = await db.SupplierInvoices.AsNoTracking().SingleAsync(x => x.Id == id),
            lines = await db.SupplierInvoiceLines.AsNoTracking().Where(x => x.SupplierInvoiceId == id).OrderBy(x => x.Id).ToArrayAsync(),
            mappings = await db.InvoiceMatchMappingVersions.CountAsync(), results = await db.InvoiceMatchResults.CountAsync(),
            exceptions = await db.InvoiceExceptions.CountAsync(), claims = await db.InvoiceMatchAllocations.CountAsync(),
            exposure = await db.InvoiceMatchExposureStates.CountAsync(), receipts = await db.CommercialCommandReceipts.CountAsync(),
            handoffs = await db.FinanceInvoiceHandoffs.CountAsync(), payments = await db.FinancePaymentStatusRefs.CountAsync(),
            audit = await db.AuditEvents.CountAsync(), outbox = await db.OutboxEvents.CountAsync(),
        }, Json);
    }
    private async Task PublishMatchingPolicy()
    {
        var exact=new { absolute="0",percent="0",combination="AllLimits" };
        var starts=DateTimeOffset.UtcNow.AddSeconds(10);
        var created=await Send(HttpMethod.Post,Root+"/configuration/policies",OrderBuyer,new { policyKey="INVOICE_MATCHING",effectiveFrom=starts,
            rulesJson=JsonSerializer.Serialize(new { schemaVersion=1,transactionType="InvoiceMatching",currencyCode="ZMW",moneyScale=2,rounding="ToEven",
                rules=new[] { new { categoryCode="IT",method="ThreeWay",acceptanceKind="Goods",quantity=exact,unitPrice=exact,tax=exact,total=exact } } }) });
        await Check(created,HttpStatusCode.Created); var policy=await created.Content.ReadFromJsonAsync<JsonElement>(); var id=policy.GetProperty("id").GetGuid();
        var submitted=await Send(HttpMethod.Post,$"{Root}/configuration/policies/{id}/submit-review",OrderBuyer,
            new { version=policy.GetProperty("version").GetInt64(),reason="Explicit synthetic matching rules" });
        await Check(submitted,HttpStatusCode.OK); policy=await submitted.Content.ReadFromJsonAsync<JsonElement>();
        await Expect(HttpStatusCode.Forbidden,HttpMethod.Post,$"{Root}/configuration/policies/{id}/publish",OrderBuyer,
            new { version=policy.GetProperty("version").GetInt64(),reason="Maker cannot publish own policy" });
        await Expect(HttpStatusCode.OK,HttpMethod.Post,$"{Root}/configuration/policies/{id}/publish",OrderFinance,
            new { version=policy.GetProperty("version").GetInt64(),reason="Independent synthetic matching review" });
        var delay=starts-DateTimeOffset.UtcNow; if(delay>TimeSpan.Zero) await Task.Delay(delay+TimeSpan.FromMilliseconds(50));
    }
}
