using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;
public sealed class ReceivingAcceptanceHistoryTests
{
    [Fact]
    public void Inactive_historical_PO_acceptance_is_not_discarded_when_current_line_has_zero()
    {
        var active = new PurchaseOrderLine { IsActive = true }; var inactive = new PurchaseOrderLine { IsActive = false, AcceptedQuantity = 0.25m };
        Assert.False(ReceivingAcceptanceHistory.IsVerified([active, inactive], [], [], new Dictionary<Guid, decimal>(), new HashSet<Guid>(), 0));
        // Verified historical facts may account for an inactive record; its quantity still participates in the PR total.
        Assert.True(ReceivingAcceptanceHistory.IsVerified([active, inactive], [], [],
            new Dictionary<Guid, decimal> { [inactive.Id] = 0.25m }, new HashSet<Guid>(), 0.25m));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Malformed_version_one_certificate_does_not_turn_missing_state_into_zero(bool missingBinding)
    {
        var line = new PurchaseOrderLine(); var state = Guid.NewGuid();
        var row = new ServiceAcceptance { PurchaseOrderLineId = line.Id, ExecutionVersion = 1, Status = "Accepted", CertifiedAt = DateTimeOffset.UtcNow,
            ReceivingSourceStateId = missingBinding ? state : null, SourceStateBindingId = Guid.Empty };
        Assert.False(ReceivingAcceptanceHistory.IsVerified([line], [], [row], new Dictionary<Guid, decimal>(), new HashSet<Guid> { state }, 0));
    }
    [Fact]
    public void A_version_one_service_draft_intended_quantity_does_not_claim_certification()
    {
        var line = new PurchaseOrderLine(); var row = new ServiceAcceptance { PurchaseOrderLineId = line.Id, ExecutionVersion = 1,
            Status = "PendingCertification", AcceptedQuantity = 1, AcceptedAmount = 100 };
        Assert.True(ReceivingAcceptanceHistory.IsVerified([line], [], [row], new Dictionary<Guid, decimal>(), new HashSet<Guid>(), 0));
        row.CertifiedAt = DateTimeOffset.UtcNow;
        Assert.False(ReceivingAcceptanceHistory.IsVerified([line], [], [row], new Dictionary<Guid, decimal>(), new HashSet<Guid>(), 0));
    }
    [Fact]
    public void Missing_or_unloaded_state_binds_neither_goods_nor_services()
    {
        var line = new PurchaseOrderLine(); var state = Guid.NewGuid();
        var goods = new GoodsReceiptLine { PurchaseOrderLineId = line.Id, ExecutionVersion = 1, PhysicalObservationId = Guid.NewGuid(),
            ReceivingSourceStateId = state, SourceStateBindingId = state };
        var service = new ServiceAcceptance { PurchaseOrderLineId = line.Id, ExecutionVersion = 1, CertifiedAt = DateTimeOffset.UtcNow,
            ReceivingSourceStateId = state, SourceStateBindingId = state };
        Assert.False(ReceivingAcceptanceHistory.IsVerified([line], [goods], [], new Dictionary<Guid, decimal>(), new HashSet<Guid>(), 0));
        Assert.False(ReceivingAcceptanceHistory.IsVerified([line], [], [service], new Dictionary<Guid, decimal>(), new HashSet<Guid>(), 0));
    }
}
