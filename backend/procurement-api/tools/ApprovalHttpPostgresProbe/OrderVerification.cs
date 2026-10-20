using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    public async Task VerifyInitialOrders()
    {
        Require(OrderDistinctRuntime, "Order proof must use a restricted runtime principal.");
        await VerifyBranchFenceCleanup();
        await VerifyCoreBranchEndpointCoupling();
        await Authority(OrderManager1, "ORDER-SOURCE-M1"); await Authority(OrderManager2, "ORDER-SOURCE-M2");
        await Authority(OrderManager2, "ORDER-COMMERCIAL-M2", "PurchaseOrder.Approve");
        await VerifyTypedAuthoritySettings();
        Guid supplier;
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var row = new Supplier { Number = "SYNTHETIC-PO", LegalName = "Synthetic order supplier", DisplayName = "Synthetic order supplier",
                CountryCode = "ZM", Status = "Active", IsOrderable = true };
            db.AddScoped(row); await db.SaveChangesAsync(); supplier = row.Id;
        }
        var source = await NewApprovedOrderSource("Actual controller scope stamping and allocation proof");
        var sourceId = source.GetProperty("id").GetGuid(); var lineId = source.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var conversion = OrderConversion(source, supplier); var convertKey = Guid.NewGuid();
        var converted = await Send(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer, conversion, convertKey);
        await Check(converted, HttpStatusCode.Created); var convertedText = await converted.Content.ReadAsStringAsync();
        var order = JsonDocument.Parse(convertedText).RootElement.Clone(); var orderId = order.GetProperty("id").GetGuid();
        var retry = await Send(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer, conversion, convertKey);
        await Check(retry, HttpStatusCode.Created); Require(await retry.Content.ReadAsStringAsync() == convertedText, "Conversion retry lost exact receipt.");
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer, conversion, Guid.NewGuid());
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, Root + "/orders/from-approved-sources", OrderRequester, conversion, Guid.NewGuid());
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            Require(await db.PurchaseOrders.CountAsync() == 1, "Failed/replayed conversions left an extra PO.");
            var allocations = await db.PurchaseRequestAllocations.Where(x => x.PurchaseRequestLineId == lineId).ToArrayAsync();
            var line = await db.PurchaseRequestLines.SingleAsync(x => x.Id == lineId);
            Require(allocations.Length == 1 && allocations[0].TargetId == orderId && allocations[0].Quantity == 1
                && allocations[0].TenantId == "probe" && allocations[0].LegalEntityId == OrderCompany && line.AllocatedQuantity == 1,
                "Actual conversion failed scoped allocation/counter invariants.");
        }
        Console.WriteLine("PASS order: mapped positive conversion + exact replay + exhausted/unauthorized denial + rollback counters");
        await VerifyConversionReplayTargetScope(orderId, conversion, convertKey, convertedText);
        await VerifyOrderDraftReadContract(orderId, convertKey);
        var commitment = "SYNTHETIC-SINGLE-PO-COMMITMENT";
        var reservation = await SourceReservation(sourceId);
        var funded = await Send(HttpMethod.Post, $"{Root}/orders/{orderId}/finance-confirmations", OrderFinance,
            OrderFunding(order, commitment, reservation, "PO-EVIDENCE-1"), Guid.NewGuid());
        await Check(funded, HttpStatusCode.OK); order = await funded.Content.ReadFromJsonAsync<JsonElement>();
        var submitted = await Send(HttpMethod.Post, $"{Root}/orders/{orderId}/submit", OrderBuyer,
            new { expectedOrderVersion = order.GetProperty("version").GetInt64() }, Guid.NewGuid());
        await Check(submitted, HttpStatusCode.Created);
        var history = await Get($"{Root}/orders/{orderId}/commercial-history", OrderManager2);
        var caseView = history.GetProperty("cases")[0];
        var task = caseView.GetProperty("stages")[0].GetProperty("assignments")[0]; var assignmentId = task.GetProperty("id").GetGuid();
        var decisionBody = new { action = "Approve", expectedOrderVersion = history.GetProperty("order").GetProperty("version").GetInt64(),
            expectedCaseVersion = caseView.GetProperty("version").GetInt64(), expectedAssignmentVersion = task.GetProperty("version").GetInt64() };
        var decisionKey = Guid.NewGuid(); var decisionPath = $"{Root}/orders/{orderId}/assignments/{assignmentId}/decisions";
        await Expect(HttpStatusCode.NotFound, HttpMethod.Post, decisionPath, OrderManager1, decisionBody, Guid.NewGuid());
        var approved = await Send(HttpMethod.Post, decisionPath, OrderManager2, decisionBody, decisionKey);
        await Check(approved, HttpStatusCode.OK); var approvedText = await approved.Content.ReadAsStringAsync();
        order = JsonDocument.Parse(approvedText).RootElement.Clone();
        var decisionReplay = await Send(HttpMethod.Post, decisionPath, OrderManager2, decisionBody, decisionKey);
        await Check(decisionReplay, HttpStatusCode.OK); Require(await decisionReplay.Content.ReadAsStringAsync() == approvedText, "PO decision replay changed reply.");
        await SetSupplierState(supplier, "Suspended");
        var issueKey = Guid.NewGuid(); var issueBody = new { expectedOrderVersion = order.GetProperty("version").GetInt64() };
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, $"{Root}/orders/{orderId}/issue", OrderBuyer, issueBody, issueKey);
        await SetSupplierState(supplier, "Active");
        var issued = await Send(HttpMethod.Post, $"{Root}/orders/{orderId}/issue", OrderBuyer, issueBody, issueKey);
        await Check(issued, HttpStatusCode.OK); var issueText = await issued.Content.ReadAsStringAsync();
        order = JsonDocument.Parse(issueText).RootElement.Clone();
        Require(order.GetProperty("status").GetString() == "IssuePending", "Issue falsely claimed supplier delivery.");
        var issueReplay = await Send(HttpMethod.Post, $"{Root}/orders/{orderId}/issue", OrderBuyer, issueBody, issueKey);
        await Check(issueReplay, HttpStatusCode.OK); Require(await issueReplay.Content.ReadAsStringAsync() == issueText, "Issue duplicated or lost receipt.");
        history = await Get($"{Root}/orders/{orderId}/commercial-history", OrderBuyer);
        var issuedVersionId = history.GetProperty("issued")[0].GetProperty("id").GetGuid();
        await VerifyOrderDispatchReadContract(orderId, issuedVersionId, issueKey);
        var delivered = await Send(HttpMethod.Post, $"{Root}/orders/{orderId}/delivery-confirmations", OrderManager1,
            new { expectedOrderVersion = order.GetProperty("version").GetInt64(), issuedVersionId, deliveryReference = "DELIVERY-1",
                outcome = "Delivered", channel = "SecureEmail", evidenceReference = "SYNTHETIC-DELIVERY", observedAt = DateTimeOffset.UtcNow }, Guid.NewGuid());
        await Check(delivered, HttpStatusCode.OK);
        Require((await delivered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString() == "Issued", "Delivery did not advance issued state.");
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>(); var line = await db.PurchaseRequestLines.SingleAsync(x => x.Id == lineId);
            Require(line.OrderedQuantity == 1 && line.AllocatedQuantity == 0
                && await db.OrderIssuedVersions.CountAsync(x => x.PurchaseOrderId == orderId) == 1,
                "Issue replay duplicated commercial version/quantity.");
        }
        Console.WriteLine("PASS order: independent funding + Core assignment/decision + supplier denial + seal/replay + actual delivery");
        await VerifyCommitmentOwnershipAndDraftCancel(supplier, commitment);
        await VerifyFundedUnsubmittedCancellation(supplier);
        await VerifyOrderAmountNotRepresentable(supplier);
        await VerifyDecimalStringOrderTransport(supplier);
        await VerifyOrderExactInput(supplier);
        await VerifyOrderRevokedGrantReplay(orderId, decisionPath, decisionBody, decisionKey, issueBody, issueKey);
    }

    private async Task VerifyCommitmentOwnershipAndDraftCancel(Guid supplier, string existingCommitment)
    {
        var source = await NewApprovedOrderSource("Ownership/cancellation denial"); var sourceId = source.GetProperty("id").GetGuid();
        var response = await Send(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer, OrderConversion(source, supplier), Guid.NewGuid());
        await Check(response, HttpStatusCode.Created); var order = await response.Content.ReadFromJsonAsync<JsonElement>(); var id = order.GetProperty("id").GetGuid();
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, $"{Root}/orders/{id}/finance-confirmations", OrderFinance,
            OrderFunding(order, existingCommitment, await SourceReservation(sourceId), "DIFFERENT-EVIDENCE-SAME-COMMITMENT"), Guid.NewGuid());
        await VerifyQueuedBranchRevocation(id, sourceId, order.GetProperty("version").GetInt64());
        order = await Get($"{Root}/orders/{id}", OrderBuyer);
        var key = Guid.NewGuid(); var body = new { expectedOrderVersion = order.GetProperty("version").GetInt64(), reason = "Cancel unfunded draft" };
        var cancelled = await Send(HttpMethod.Post, $"{Root}/orders/{id}/source-cancel", OrderBuyer, body, key);
        await Check(cancelled, HttpStatusCode.OK); var first = await cancelled.Content.ReadAsStringAsync();
        var replay = await Send(HttpMethod.Post, $"{Root}/orders/{id}/source-cancel", OrderBuyer, body, key);
        await Check(replay, HttpStatusCode.OK); Require(first == await replay.Content.ReadAsStringAsync(), "Unfunded cancel retry changed reply.");
        using var services = Scope(OrderBuyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var line = await db.PurchaseRequestLines.SingleAsync(x => x.PurchaseRequestId == sourceId);
        Require(line.AllocatedQuantity == 0 && line.OrderedQuantity == 0 && await db.OrderCommitmentOwnership.CountAsync() == 1
            && !await db.OrderFinanceEvidence.AnyAsync(x => x.PurchaseOrderId == id), "Failed commitment ownership/cancel corrupted funding or quantities.");
        Console.WriteLine("PASS order: cross-PO commitment reference denial + unfunded cancel exact replay + source release");
    }

    private async Task VerifyFundedUnsubmittedCancellation(Guid supplier)
    {
        var source = await NewApprovedOrderSource("Funded unsubmitted cancellation");
        var sourceId = source.GetProperty("id").GetGuid(); var reservation = await SourceReservation(sourceId);
        var converted = await Send(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer, OrderConversion(source, supplier), Guid.NewGuid());
        await Check(converted, HttpStatusCode.Created); var order = await converted.Content.ReadFromJsonAsync<JsonElement>();
        var id = order.GetProperty("id").GetGuid();
        var funded = await Send(HttpMethod.Post, $"{Root}/orders/{id}/finance-confirmations", OrderFinance,
            OrderFunding(order, "SYNTHETIC-UNSUBMITTED-HELD", reservation, "UNSUBMITTED-FINANCE-EVIDENCE"), Guid.NewGuid());
        await Check(funded, HttpStatusCode.OK); order = await funded.Content.ReadFromJsonAsync<JsonElement>();
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, $"{Root}/orders/{id}/source-cancel", OrderBuyer,
            new { expectedOrderVersion = order.GetProperty("version").GetInt64(), reason = "Cannot drop a held commitment" }, Guid.NewGuid());
        var key = Guid.NewGuid(); var body = new { expectedOrderVersion = order.GetProperty("version").GetInt64(),
            evidenceReference = "UNSUBMITTED-RELEASE", releasedGrossAmount = 100, currencyCode = "ZMW",
            restoredReservationReferences = new[] { reservation }, reason = "External Finance release and PR restoration verified",
            confirmedAt = DateTimeOffset.UtcNow };
        var released = await Send(HttpMethod.Post, $"{Root}/orders/{id}/draft-finance-release-confirmations", OrderFinance, body, key);
        await Check(released, HttpStatusCode.OK); var first = await released.Content.ReadAsStringAsync();
        var replay = await Send(HttpMethod.Post, $"{Root}/orders/{id}/draft-finance-release-confirmations", OrderFinance, body, key);
        await Check(replay, HttpStatusCode.OK); Require(first == await replay.Content.ReadAsStringAsync(), "Unsubmitted Finance release retry changed response.");
        using var services = Scope(OrderBuyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var resolution = await db.OrderFinanceResolutions.SingleAsync(x => x.PurchaseOrderId == id);
        var line = await db.PurchaseRequestLines.SingleAsync(x => x.PurchaseRequestId == sourceId);
        Require(resolution.SubmissionId is null && resolution.Outcome == "CancelledUnsubmittedOrder"
            && !await db.OrderSubmissions.AnyAsync(x => x.PurchaseOrderId == id) && line.AllocatedQuantity == 0
            && (await db.PurchaseOrders.SingleAsync(x => x.Id == id)).FinanceCommitmentRef is null,
            "Unsubmitted cancellation invented an approval case or stranded source/funding state.");
        Console.WriteLine("PASS order: funded Draft cannot cancel without independent release; no-Core release exact replay frees quantity");
    }

    private async Task VerifyOrderRevokedGrantReplay(Guid orderId, string decisionPath, object decisionBody,
        Guid decisionKey, object issueBody, Guid issueKey)
    {
        await Revoke(OrderManager2);
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, decisionPath, OrderManager2, decisionBody, decisionKey);
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, $"{Root}/orders/{orderId}/issue", OrderBuyer, issueBody, issueKey);
        Console.WriteLine("PASS order: revoked finite authority denies exact decision and issue replay");
    }

    private async Task<JsonElement> NewApprovedOrderSource(string purpose, decimal quantity = 1m,
        decimal unitPrice = 100m, decimal checkedAmount = 100m, string? purchaseType = null)
    {
        var source = await NewFundedRequest(purpose, quantity, unitPrice, checkedAmount, purchaseType); var opened = await Open(source); var caseId = opened.GetProperty("caseId").GetGuid();
        for (var votes = 0; votes < 3; votes++)
        {
            var detail = await Get($"{Root}/approvals/{caseId}", OrderManager1);
            var assignment = detail.GetProperty("stages").EnumerateArray().Where(x => x.GetProperty("state").GetString() == "Active")
                .SelectMany(x => x.GetProperty("assignments").EnumerateArray()).First(x => x.GetProperty("state").GetString() == "Pending");
            var actor = Guid.Parse(assignment.GetProperty("principalId").GetString()!);
            await Expect(HttpStatusCode.OK, HttpMethod.Post, DecisionPath(caseId, assignment), actor, Decision(detail, assignment, "Approve"), Guid.NewGuid());
        }
        return await Get($"{Root}/requests/{source.GetProperty("id").GetGuid()}", OrderRequester);
    }
    private object OrderConversion(JsonElement source, Guid supplier) => new { draft = new { supplierId = supplier, currencyCode = "ZMW",
        lines = new[] { new { description = "Synthetic item", unitCode = "each", quantity = 1, unitPrice = 100, taxAmount = 0 } } },
        sources = new[] { new { draftLineIndex = 0, purchaseRequestId = source.GetProperty("id").GetGuid(),
            purchaseRequestLineId = source.GetProperty("lines")[0].GetProperty("id").GetGuid(), expectedRequestVersion = source.GetProperty("version").GetInt64() } } };
    private static object OrderFunding(JsonElement order, string commitment, string reservation, string evidence) => new {
        expectedOrderVersion = order.GetProperty("version").GetInt64(), commercialRevision = 1,
        commitmentReference = commitment, evidenceReference = evidence, currencyCode = "ZMW", confirmedGrossAmount = 100,
        sourceReservationReferences = new[] { reservation }, confirmedAt = DateTimeOffset.UtcNow };
    private async Task<string> SourceReservation(Guid request)
    {
        using var services = Scope(OrderBuyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        return await db.BudgetCheckSnapshots.Where(x => x.PurchaseRequestId == request).Select(x => x.ReservationRef).SingleAsync()
            ?? throw new InvalidOperationException("Synthetic source reservation missing.");
    }
    private async Task SetSupplierState(Guid id, string status)
    {
        using var services = Scope(OrderBuyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var supplier = await db.Suppliers.SingleAsync(x => x.Id == id); supplier.Status = status; await db.SaveChangesAsync();
    }
    private static string InitialOrderRules() => JsonSerializer.Serialize(new {
        schemaVersion = 1, transactionType = "PurchaseOrder", currencyCode = "ZMW", amountBasis = "ApprovedLineBase",
        rules = new[] { new { code = "COMMERCIAL", priority = 0,
            when = new { categoryCodes = Array.Empty<string>(), purchaseTypes = Array.Empty<string>(), branchIds = Array.Empty<Guid>() },
            result = new { budgetReservation = "Required", sourcingMethodCode = (string?)null, minimumQuoteCount = (int?)null,
                approvalStages = new[] { new { code = "PO_COMMERCIAL", assignee = "ProcurementManager", mode = "One" } } } } } });
}
