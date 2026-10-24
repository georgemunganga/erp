using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalPublicationSupplierSelectionTests
{
    [Fact]
    public void Eligibility_is_applied_before_total_and_page()
    {
        var now = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        var approved = new Supplier { Number = "S-1", DisplayName = "A Approved",
            Status = "Active", IsOrderable = true };
        var suspended = new Supplier { Number = "S-2", DisplayName = "B Suspended",
            Status = "Suspended", IsOrderable = false };
        var expired = new Supplier { Number = "S-3", DisplayName = "C Expired",
            Status = "Active", IsOrderable = true,
            Qualifications = [new SupplierQualification { QualificationType = "LICENCE",
                IsActive = true, ReviewStatus = "Verified", ExpiresOn = new DateOnly(2026, 10, 22) }] };
        var second = new Supplier { Number = "S-4", DisplayName = "D Approved",
            Status = "Active", IsOrderable = true };
        var page = PrincipalPublicationSupplierSelection.Page([approved, suspended, expired, second],
            now, 2, pageSize: 1);
        Assert.Equal(2, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(second.Id, page.Items[0].Id);
    }

    [Fact]
    public void Too_many_candidates_requires_a_narrower_search()
    {
        var candidates = Enumerable.Range(0, 501).Select(_ => new Supplier()).ToArray();
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalPublicationSupplierSelection.Page(candidates, DateTimeOffset.UtcNow, 1));
    }
}
