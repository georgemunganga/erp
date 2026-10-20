using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    private Guid? browserOrderSourceId;
    private Guid? browserOrderLineId;

    private async Task<object> PrepareOrderBrowserFixture()
    {
        Require(OrderDistinctRuntime, "Browser order proof requires separate bootstrap/runtime identities.");
        await Authority(OrderManager2, "BROWSER-ORDER-COMMERCIAL-M2", "PurchaseOrder.Approve");
        Guid supplierId;
        // Only supporting master/config fixtures use the owner; source approval does not.
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var supplier = new Supplier { Number = "BROWSER-PO", LegalName = "Browser synthetic order supplier",
                DisplayName = "Browser synthetic order supplier", CountryCode = "ZM", Status = "Active", IsOrderable = true };
            db.AddScoped(supplier); await db.SaveChangesAsync(); supplierId = supplier.Id;
        }
        var source = await NewApprovedOrderSource("Browser approved source for independent order lifecycle");
        var requestId = source.GetProperty("id").GetGuid();
        var lineId = source.GetProperty("lines")[0].GetProperty("id").GetGuid();
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var line = await db.PurchaseRequestLines.SingleAsync(x => x.Id == lineId);
            Require(line.ApprovedQuantity == 1 && line.AllocatedQuantity == 0 && line.OrderedQuantity == 0
                && !await db.PurchaseRequestAllocations.AnyAsync(x => x.PurchaseRequestLineId == lineId),
                "Browser source must be genuinely approved and remain unconsumed for conversion.");
        }
        browserOrderSourceId = requestId; browserOrderLineId = lineId;
        return new
        {
            source, sourceRequestId = requestId, sourceLineId = lineId, supplierId,
            supplierDisplayName = "Browser synthetic order supplier", sourceReservationReference = await SourceReservation(requestId),
            quantity = "1", unitPrice = "100", confirmedGrossAmount = "100", currencyCode = "ZMW",
            buyerPrincipalId = OrderBuyer, financePrincipalId = OrderFinance, approverPrincipalId = OrderManager2,
            dispatcherPrincipalId = OrderManager1, approvalMode = "One", approvalStage = "PO-COMMERCIAL",
            approvedManualFinancePolicyReference = "SYNTHETIC-APPROVED-PO-FINANCE",
            approvedManualDispatchPolicyReference = "SYNTHETIC-APPROVED-DELIVERY", deliveryChannel = "SecureEmail",
        };
    }

    public async Task VerifyOrderBrowserCompletion()
    {
        Require(browserOrderSourceId.HasValue && browserOrderLineId.HasValue, "No synthetic browser source was prepared.");
        using var services = Scope(OrderBuyer);
        var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var allocation = await db.PurchaseRequestAllocations.SingleAsync(x => x.PurchaseRequestLineId == browserOrderLineId!.Value);
        var order = await db.PurchaseOrders.SingleAsync(x => x.Id == allocation.TargetId);
        var line = await db.PurchaseRequestLines.SingleAsync(x => x.Id == browserOrderLineId!.Value);
        var orderLine = await db.PurchaseOrderLines.SingleAsync(x => x.PurchaseOrderId == order.Id && x.IsActive);
        var submission = await db.OrderSubmissions.SingleAsync(x => x.PurchaseOrderId == order.Id);
        var finance = await db.OrderFinanceEvidence.SingleAsync(x => x.PurchaseOrderId == order.Id);
        var issued = await db.OrderIssuedVersions.SingleAsync(x => x.PurchaseOrderId == order.Id);
        var delivered = await db.OrderDeliveryEvidence.SingleAsync(x => x.IssuedVersionId == issued.Id);
        var core = services.ServiceProvider.GetRequiredService<Mightyfin.Erp.Core.Approvals.Infrastructure.ApprovalDbContext>();
        var workflow = await core.Cases.SingleAsync(x => x.Id == submission.CoreApprovalCaseId);
        var decision = await core.Decisions.SingleAsync(x => x.CaseId == workflow.Id);
        Require(order.Status == "Issued" && order.CurrencyCode == "ZMW" && orderLine.Quantity == 1m && orderLine.UnitPrice == 100m && orderLine.TaxAmount == 0m
            && line.ApprovedQuantity == 1m && line.OrderedQuantity == 1m && line.AllocatedQuantity == 0m
            && allocation.Quantity == 1m && workflow.State == "Approved" && decision.Action == "Approve"
            && decision.ActorSubjectId == OrderManager2.ToString("D")
            && finance.ConfirmedGrossAmount == 100m && finance.VerifiedByPrincipalId == OrderFinance.ToString("D")
            && issued.SubmissionId == submission.Id && delivered.Outcome == "Delivered" && delivered.Channel == "SecureEmail"
            && delivered.VerifiedByPrincipalId == OrderManager1.ToString("D"),
            "Browser PO completion failed persisted exact amounts, independent actors, Core decision, source conservation or actual sending evidence.");
        Console.WriteLine("PASS browser PO persisted DB: one allocation/Finance/submission/Core vote/issued version/delivery, exact 100 ZMW and conserved source quantities");
    }
}
