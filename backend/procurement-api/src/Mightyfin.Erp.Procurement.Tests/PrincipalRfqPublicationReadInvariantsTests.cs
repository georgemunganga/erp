using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqPublicationReadInvariantsTests
{
    [Theory]
    [InlineData("PublicationReview")]
    [InlineData("Published")]
    public void Review_and_published_demand_keep_the_exact_active_source_hold(string status)
    {
        var eventId = Guid.NewGuid(); var requestId = Guid.NewGuid();
        var requestLineId = Guid.NewGuid(); var sourcingLineId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var ev = new SourcingEvent { Id = eventId, ExecutionVersion = 2,
            Status = status, CurrencyCode = "ZMW" };
        var binding = new SourcingDemandBinding { SourcingEventId = eventId,
            SourcingLineId = sourcingLineId, PurchaseRequestId = requestId,
            PurchaseRequestLineId = requestLineId, AllocationId = allocationId,
            Quantity = 1.0001m, EstimatedValue = 1.00020001m, UnitCode = "EA",
            CategoryCode = "IT" };
        var line = new SourcingLine { Id = sourcingLineId, SourcingEventId = eventId,
            PurchaseRequestLineId = requestLineId, Quantity = 1.0001m,
            UnitCode = "EA", CategoryCode = "IT", Status = "Active" };
        var allocation = new PurchaseRequestAllocation { Id = allocationId,
            PurchaseRequestId = requestId, PurchaseRequestLineId = requestLineId,
            Status = "Reserved", TargetType = "SourcingEvent", TargetId = eventId,
            Quantity = 1.0001m, EstimatedValue = 1.00020001m, CurrencyCode = "ZMW" };
        PrincipalRfqPublicationReadInvariants.VerifyActiveHold(ev, binding, line, allocation);

        allocation.Status = "Released";
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqPublicationReadInvariants.VerifyActiveHold(ev, binding, line, allocation));
        allocation.Status = "Reserved";
        allocation.EstimatedValue = 1.0002m;
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqPublicationReadInvariants.VerifyActiveHold(ev, binding, line, allocation));
        allocation.EstimatedValue = 1.00020001m;
        line.Status = "Cancelled";
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqPublicationReadInvariants.VerifyActiveHold(ev, binding, line, allocation));
    }
}
