using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class SupplierEligibilityTests
{
    [Fact]
    public void Latest_verified_qualification_controls_orderability()
    {
        var at = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        var supplier = new Supplier { Status = "Active", IsOrderable = true };
        Assert.True(SupplierEligibility.CanReceiveNewOrders(supplier, at));
        supplier.Qualifications.Add(new SupplierQualification
        {
            QualificationType = "Insurance", ReviewStatus = "Verified",
            ExpiresOn = new DateOnly(2026, 10, 18), CreatedAt = at.AddDays(-2),
        });
        Assert.False(SupplierEligibility.CanReceiveNewOrders(supplier, at));
        supplier.Qualifications.Add(new SupplierQualification
        {
            QualificationType = "insurance", ReviewStatus = "Pending",
            ExpiresOn = new DateOnly(2027, 10, 19), CreatedAt = at.AddDays(-1),
        });
        Assert.False(SupplierEligibility.CanReceiveNewOrders(supplier, at));
        supplier.Qualifications.Last().ReviewStatus = "Verified";
        Assert.True(SupplierEligibility.CanReceiveNewOrders(supplier, at));
        supplier.Qualifications.Add(new SupplierQualification
        {
            QualificationType = "Insurance", ReviewStatus = "Pending", IsActive = false,
            ExpiresOn = new DateOnly(2027, 10, 19), CreatedAt = at,
        });
        Assert.True(SupplierEligibility.CanReceiveNewOrders(supplier, at));
        supplier.Qualifications.First(x => x.ReviewStatus == "Verified").IsActive = false;
        Assert.True(SupplierEligibility.CanReceiveNewOrders(supplier, at));
        supplier.EligibleUntil = at.AddMinutes(-1);
        Assert.False(SupplierEligibility.CanReceiveNewOrders(supplier, at));
    }
}
