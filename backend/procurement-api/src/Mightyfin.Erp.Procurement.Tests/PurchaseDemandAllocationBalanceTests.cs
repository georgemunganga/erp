using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PurchaseDemandAllocationBalanceTests
{
    [Fact]
    public void PO_and_RFQ_holds_share_one_remaining_quantity_and_retained_inactive_owner_is_not_free_demand()
    {
        var f = new PurchaseOrderSourceAllocationTests.Fixture(); var ordered = f.Reserve(3);
        f.PoLine.IsActive = false; // retained owner line still owns its hold; reducer never filters inactive history.
        var rfq = AddRfq(f, 4);
        Assert.Equal(new PurchaseDemandBalance(3, 7, 0), Balance(f));
        Assert.Equal(3, f.Available()); // Existing direct adapter observes BOTH target types.
        var before = (f.Line.AllocatedQuantity, f.Line.OrderedQuantity, f.Request.UpdatedAt);
        var other = f.NewOrder(); var line = f.NewPoLine(other, 4);
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderSourceAllocation.Reserve(f.Source, other, line,
            4, Guid.NewGuid(), "buyer", f.Now.AddMinutes(2)));
        Assert.Equal(before, (f.Line.AllocatedQuantity, f.Line.OrderedQuantity, f.Request.UpdatedAt));
        f.PoLine.IsActive = true; f.Order.Status = "Approved";
        PurchaseOrderSourceAllocation.ConfirmOrdered(f.Source, f.Order, f.PoLine, ordered, f.Now.AddMinutes(2));
        Assert.Equal(new PurchaseDemandBalance(3, 4, 3), Balance(f));
        // Arrange a retained released RFQ fact (no RFQ lifecycle/mutation API is supplied by this increment).
        rfq.Status = "Released"; f.Line.AllocatedQuantity = 0;
        Assert.Equal(new PurchaseDemandBalance(7, 0, 3), Balance(f));
        Assert.Equal(2, f.Ledger.Count); // historical row was retained, not filtered/deleted.
    }

    [Theory]
    [InlineData("unknown-target")][InlineData("unknown-status")][InlineData("rfq-ordered")]
    [InlineData("missing-owner")][InlineData("foreign-owner")][InlineData("wrong-source-owner")]
    [InlineData("empty-target")][InlineData("foreign-row")][InlineData("cache-drift")]
    [InlineData("duplicate-owner")][InlineData("duplicate-row")][InlineData("unknown-released-owner")]
    public void Unknown_incomplete_or_nonowned_allocations_cannot_inflate_available_demand(string corruption)
    {
        var f = new PurchaseOrderSourceAllocationTests.Fixture(); var row = AddRfq(f, 4);
        switch (corruption)
        {
            case "unknown-target": row.TargetType = "Quotation"; break;
            case "unknown-status": row.Status = "Pending"; break;
            case "rfq-ordered": row.Status = "Ordered"; break;
            case "missing-owner": f.Owners.RemoveAll(x => x.TargetType == "SourcingEvent"); break;
            case "foreign-owner":
                f.Owners.RemoveAll(x => x.TargetType == "SourcingEvent");
                var foreignCompany = Guid.NewGuid();
                f.Owners.Add(PurchaseDemandAllocationOwner.FromSourcing(new SourcingEvent { Id = row.TargetId!.Value,
                    TenantId = f.Request.TenantId, LegalEntityId = foreignCompany }, new SourcingLine {
                    SourcingEventId = row.TargetId.Value, TenantId = f.Request.TenantId, LegalEntityId = foreignCompany,
                    PurchaseRequestLineId = f.Line.Id })); break;
            case "wrong-source-owner":
                f.Owners.RemoveAll(x => x.TargetType == "SourcingEvent");
                f.Owners.Add(PurchaseDemandAllocationOwner.FromSourcing(new SourcingEvent { Id = row.TargetId!.Value,
                    TenantId = f.Request.TenantId, LegalEntityId = f.Company }, new SourcingLine {
                    SourcingEventId = row.TargetId.Value, TenantId = f.Request.TenantId, LegalEntityId = f.Company,
                    PurchaseRequestLineId = Guid.NewGuid() })); break;
            case "empty-target": row.TargetId = Guid.Empty; break;
            case "foreign-row": row.TenantId = "other"; break;
            case "cache-drift": f.Line.AllocatedQuantity = 0; break;
            case "duplicate-owner": f.Owners.Add(f.Owners.Last()); break;
            case "duplicate-row": f.Ledger.Add(row); break;
            default: row.Status = "Released"; f.Line.AllocatedQuantity = 0; f.Owners.RemoveAll(x => x.TargetType == "SourcingEvent"); break;
        }
        var before = (f.Line.AllocatedQuantity, f.Line.OrderedQuantity, f.Request.UpdatedAt);
        Assert.Equal("DemandAllocationUnreconciled", Assert.Throws<InvalidOperationException>(() => Balance(f)).Message);
        Assert.Equal(before, (f.Line.AllocatedQuantity, f.Line.OrderedQuantity, f.Request.UpdatedAt));
    }

    [Fact]
    public void Approved_demand_verification_does_not_turn_competitive_route_into_direct_order_permission()
    {
        var f = new PurchaseOrderSourceAllocationTests.Fixture();
        f.Line.Route = "PendingSourcing"; f.Method.ExecutionRoute = "CompetitiveSourcing";
        ApprovedPurchaseDemand.Verify(f.Source, f.Request.TenantId, f.Company, f.Now.AddMinutes(2));
        Assert.Throws<InvalidOperationException>(() => f.Available());
        Assert.Throws<InvalidOperationException>(() => f.Reserve(1));
        Assert.Empty(f.Ledger); Assert.Equal(0, f.Line.AllocatedQuantity);
        f.Line.Route = "Unassigned";
        Assert.Equal("OrderSourceMethodNotDirect", Assert.Throws<InvalidOperationException>(() => f.Available()).Message);
    }

    [Theory]
    [InlineData("scope")][InlineData("core-source")][InlineData("decision")][InlineData("finance")][InlineData("worker-pin")]
    public void Extracted_approved_demand_keeps_genuine_scope_Core_Finance_and_retained_worker_guards(string corruption)
    {
        var f = new PurchaseOrderSourceAllocationTests.Fixture();
        ApprovedPurchaseDemand.Verify(f.Source, f.Request.TenantId, f.Company, f.Now.AddMinutes(2));
        switch (corruption)
        {
            case "scope": f.Line.LegalEntityId = Guid.NewGuid(); break;
            case "core-source": f.Case.SourceId = Guid.NewGuid(); break;
            case "decision": f.Source.FinalDecision.Action = "Reject"; break;
            case "finance": f.Source.Finance.ReservationRef = "CHANGED"; break;
            default:
                typeof(PurchaseRequestApprovalSubmission).GetProperty(nameof(PurchaseRequestApprovalSubmission.WorkerIdentitySha256))!
                    .SetValue(f.Source.Submission, new string('0',64)); break;
        }
        Assert.Throws<InvalidOperationException>(() => ApprovedPurchaseDemand.Verify(f.Source,
            f.Request.TenantId, f.Company, f.Now.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => f.Available());
    }

    [Fact]
    public void Complete_cross_target_reducer_rejects_overallocation_without_mutating_and_preserves_exact_PO_product_guard()
    {
        var f = new PurchaseOrderSourceAllocationTests.Fixture(); _ = f.Reserve(7); _ = AddRfq(f, 4);
        Assert.Equal("DemandOverallocated", Assert.Throws<InvalidOperationException>(() => Balance(f)).Message);
        const decimal maximum = 99999999999999.9999m;
        var high = new PurchaseOrderSourceAllocationTests.Fixture(maximum, .0001m);
        _ = AddRfq(high, .0001m); high.PoLine.UnitPrice = maximum; high.PoLine.Quantity = maximum - .0001m;
        var before = (high.Line.AllocatedQuantity, high.Line.OrderedQuantity, high.Line.Route);
        Assert.Equal("ExactDecimalNotRepresentable", Assert.Throws<InvalidOperationException>(() =>
            PurchaseOrderSourceAllocation.Reserve(high.Source, high.Order, high.PoLine, maximum - .0001m,
                Guid.NewGuid(), "buyer", high.Now.AddMinutes(2))).Message);
        Assert.Equal(before, (high.Line.AllocatedQuantity, high.Line.OrderedQuantity, high.Line.Route));
        Assert.Single(high.Ledger);
    }

    private static PurchaseDemandBalance Balance(PurchaseOrderSourceAllocationTests.Fixture f) =>
        PurchaseDemandAllocationBalance.Evaluate(f.Request, f.Line, f.Ledger, f.Owners, f.Request.TenantId, f.Company);
    private static PurchaseRequestAllocation AddRfq(PurchaseOrderSourceAllocationTests.Fixture f, decimal quantity)
    {
        var rfq = new SourcingEvent { TenantId = f.Request.TenantId, LegalEntityId = f.Company };
        var line = new SourcingLine { TenantId = f.Request.TenantId, LegalEntityId = f.Company,
            SourcingEventId = rfq.Id, PurchaseRequestLineId = f.Line.Id, Quantity = quantity, Status = "Cancelled" };
        f.Owners.Add(PurchaseDemandAllocationOwner.FromSourcing(rfq, line));
        var row = new PurchaseRequestAllocation { TenantId = f.Request.TenantId, LegalEntityId = f.Company,
            PurchaseRequestId = f.Request.Id, PurchaseRequestLineId = f.Line.Id, TargetType = "SourcingEvent", TargetId = rfq.Id,
            Quantity = quantity, EstimatedValue = ExactDecimalArithmetic.Multiply(quantity, f.Line.EstimatedUnitPrice),
            CurrencyCode = f.Request.CurrencyCode, Status = "Reserved", Method = "SYNTHETIC-COMPETITION",
            IdempotencyKey = Guid.NewGuid().ToString("D") };
        f.Ledger.Add(row); f.Line.AllocatedQuantity = ExactDecimalArithmetic.Add(f.Line.AllocatedQuantity, quantity);
        return row;
    }
}
