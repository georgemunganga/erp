using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    private async Task VerifyOrderAmountNotRepresentable(Guid supplier)
    {
        // Full real PR approval path uses its existing finite 1000 grants. Source estimate
        // is 800.00000001; the separately confirmed four-decimal reservation fully covers it.
        // No fabricated approvals, widened authority or bootstrapped source facts are needed.
        const decimal quantity = 8000000.0001m;
        var source = await NewApprovedOrderSource("Exact PO error and rollback proof", quantity, 0.0001m, 800.0001m);
        var requestId = source.GetProperty("id").GetGuid();
        var lineId = source.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var converted = await Send(HttpMethod.Post, Root + "/orders/from-approved-sources", OrderBuyer,
            OrderConversion(source, supplier), Guid.NewGuid());
        await Check(converted, HttpStatusCode.Created);
        var order = await converted.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = order.GetProperty("id").GetGuid();
        // Current PO input limits prevent ordinary new drafts reaching this arithmetic
        // boundary. Rehearse a legacy/imported, previously rounded source-controlled draft
        // using only the explicitly separate bootstrap fixture context. The owning PR and
        // its approvals remain real and unchanged; no approved/issued PO is fabricated.
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var storedLine = await db.PurchaseOrderLines.SingleAsync(x => x.PurchaseOrderId == orderId);
            storedLine.Quantity = quantity; storedLine.UnitPrice = 99999999999999.9999m;
            var allocation = await db.PurchaseRequestAllocations.SingleAsync(x => x.TargetId == orderId);
            allocation.Quantity = quantity; allocation.EstimatedValue = checked(quantity * storedLine.UnitPrice);
            (await db.PurchaseRequestLines.SingleAsync(x => x.Id == lineId)).AllocatedQuantity = quantity;
            await db.SaveChangesAsync();
        }
        long requestVersion, orderVersion; DateTimeOffset? requestUpdated;
        int ordersBefore, allocationsBefore, receiptsBefore, financeBefore, ownershipBefore;
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var request = await db.PurchaseRequests.SingleAsync(x => x.Id == requestId);
            requestVersion = request.Version; requestUpdated = request.UpdatedAt;
            ordersBefore = await db.PurchaseOrders.CountAsync();
            allocationsBefore = await db.PurchaseRequestAllocations.CountAsync();
            receiptsBefore = await db.CommercialCommandReceipts.CountAsync();
            financeBefore = await db.OrderFinanceEvidence.CountAsync();
            ownershipBefore = await db.OrderCommitmentOwnership.CountAsync();
            orderVersion = (await db.PurchaseOrders.SingleAsync(x => x.Id == orderId)).Version;
        }
        var key = Guid.NewGuid();
        var reservation = await SourceReservation(requestId);
        var oldRoundedAmount = checked(quantity * 99999999999999.9999m);
        var input = new { expectedOrderVersion = orderVersion, commercialRevision = 1,
            commitmentReference = "SYNTHETIC-INEXACT-PO", evidenceReference = "SYNTHETIC-INEXACT-PO-FINANCE",
            currencyCode = "ZMW", confirmedGrossAmount = oldRoundedAmount,
            sourceReservationReferences = new[] { reservation }, confirmedAt = DateTimeOffset.UtcNow };
        var response = await Send(HttpMethod.Post, $"{Root}/orders/{orderId}/finance-confirmations", OrderFinance, input, key);
        await Check(response, HttpStatusCode.UnprocessableEntity);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Require(error.GetProperty("code").GetString() == "OrderAmountNotRepresentable"
            && error.GetProperty("detail").GetString()!.Contains("Check the quantities", StringComparison.Ordinal)
            && error.GetProperty("detail").GetString()!.Contains("No changes were saved", StringComparison.Ordinal),
            "Actual mapped PO amount failure lost its actionable 422 contract.");
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var request = await db.PurchaseRequests.SingleAsync(x => x.Id == requestId);
            var line = await db.PurchaseRequestLines.SingleAsync(x => x.Id == lineId);
            Require(request.Version == requestVersion && request.UpdatedAt == requestUpdated
                && line.AllocatedQuantity == quantity && line.OrderedQuantity == 0
                && (await db.PurchaseOrders.SingleAsync(x => x.Id == orderId)).Version == orderVersion
                && (await db.PurchaseOrders.SingleAsync(x => x.Id == orderId)).Status == "Draft"
                && await db.PurchaseOrders.CountAsync() == ordersBefore
                && await db.PurchaseRequestAllocations.CountAsync() == allocationsBefore
                && await db.CommercialCommandReceipts.CountAsync() == receiptsBefore
                && await db.OrderFinanceEvidence.CountAsync() == financeBefore
                && await db.OrderCommitmentOwnership.CountAsync() == ownershipBefore,
                "Rejected exact-math Finance command persisted a partial commitment, evidence, allocation, counter or receipt.");
        }
        Console.WriteLine("PASS mapped PO: unrepresentable decimal product returns actionable422 and no partial PostgreSQL Finance/business effects");
    }
}
