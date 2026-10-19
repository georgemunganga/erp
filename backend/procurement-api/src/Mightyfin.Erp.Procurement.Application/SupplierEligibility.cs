using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Current buying eligibility; a stored approval alone does not override expired evidence.</summary>
public static class SupplierEligibility
{
    public static bool CanReceiveNewOrders(Supplier supplier, DateTimeOffset at)
    {
        if (supplier.Status != "Active" || !supplier.IsOrderable
            || supplier.EligibleFrom > at || supplier.EligibleUntil < at
            || supplier.PaymentVerificationToken is not null && supplier.PaymentVerificationStatus != "Verified")
            return false;
        var today = DateOnly.FromDateTime(at.UtcDateTime);
        return supplier.Qualifications.Where(x => x.IsActive)
            .GroupBy(x => x.QualificationType, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).First())
            .All(x => x.ReviewStatus == "Verified" && (x.ExpiresOn is null || x.ExpiresOn >= today));
    }
}
