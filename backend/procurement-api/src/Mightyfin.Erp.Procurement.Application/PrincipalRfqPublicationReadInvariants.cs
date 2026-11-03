using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Published/review RFQs retain live approved-demand holds; unlike a
/// cancelled draft they must not be projected as released demand.</summary>
public static class PrincipalRfqPublicationReadInvariants
{
    public static void VerifyActiveHold(SourcingEvent ev, SourcingDemandBinding binding,
        SourcingLine line, PurchaseRequestAllocation allocation)
    {
        if (ev.ExecutionVersion != 2 || ev.Status is not ("PublicationReview" or "Published")
            || binding.SourcingEventId != ev.Id || line.SourcingEventId != ev.Id
            || line.Id != binding.SourcingLineId || line.Status != "Active"
            || line.PurchaseRequestLineId != binding.PurchaseRequestLineId
            || line.Quantity != binding.Quantity || line.UnitCode != binding.UnitCode
            || line.CategoryCode != binding.CategoryCode
            || allocation.Id != binding.AllocationId
            || allocation.PurchaseRequestId != binding.PurchaseRequestId
            || allocation.PurchaseRequestLineId != binding.PurchaseRequestLineId
            || allocation.Status != "Reserved" || allocation.TargetType != "SourcingEvent"
            || allocation.TargetId != ev.Id || allocation.Quantity != binding.Quantity
            || allocation.EstimatedValue != binding.EstimatedValue
            || allocation.CurrencyCode != ev.CurrencyCode)
            throw new InvalidOperationException("SourcingPublishedDemandHoldChanged");
    }
    /// <summary>Historical source identity after release/cancellation, not a new
    /// reservation or evidence that demand is currently available to commit.</summary>
    public static void VerifyRetainedLine(SourcingEvent ev, SourcingDemandBinding binding,
        SourcingLine line, PurchaseRequestAllocation allocation)
    {
        if (ev.ExecutionVersion != 2 || ev.Status is not ("Published" or "Cancelled")
            || binding.TenantId != ev.TenantId || binding.LegalEntityId != ev.LegalEntityId
            || line.TenantId != ev.TenantId || line.LegalEntityId != ev.LegalEntityId
            || allocation.TenantId != ev.TenantId || allocation.LegalEntityId != ev.LegalEntityId
            || binding.SourcingEventId != ev.Id || line.SourcingEventId != ev.Id
            || line.Id != binding.SourcingLineId || line.Status is not ("Active" or "Cancelled")
            || line.PurchaseRequestLineId != binding.PurchaseRequestLineId
            || line.Quantity != binding.Quantity || line.UnitCode != binding.UnitCode
            || line.CategoryCode != binding.CategoryCode || allocation.Id != binding.AllocationId
            || allocation.PurchaseRequestId != binding.PurchaseRequestId
            || allocation.PurchaseRequestLineId != binding.PurchaseRequestLineId
            || allocation.TargetType != "SourcingEvent" || allocation.TargetId != ev.Id
            || allocation.Quantity != binding.Quantity || allocation.EstimatedValue != binding.EstimatedValue
            || allocation.CurrencyCode != ev.CurrencyCode)
            throw new InvalidOperationException("SourcingRetainedDemandIdentityChanged");
    }
}
