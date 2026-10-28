using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalReceivingBusinessDateTests
{
    private sealed class Clock(DateTimeOffset at) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => at; }

    [Theory]
    [InlineData("Africa/Lusaka", "2026-10-28T23:30:00.1234567Z", 2026, 10, 29, 120)]
    [InlineData("America/New_York", "2026-10-28T01:30:00.1234567Z", 2026, 10, 27, -240)]
    [InlineData("Asia/Kathmandu", "2026-10-28T23:30:00Z", 2026, 10, 29, 345)]
    public async Task Reads_the_selected_Core_company_and_retains_one_canonical_observation(
        string zone, string instant, int year, int month, int day, int minutes)
    {
        await using var db=TestDbContextFactory.Create("receiving-core-date");
        var company=new LegalEntity { Code="DATE", RegisteredName="Date company", TimeZoneId=zone };
        db.LegalEntities.Add(company); await db.SaveChangesAsync();
        var at=DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture);
        var witness=await ProcurementPrincipalReceivingBusinessDate.ObserveAsync(db,
            "receiving-core-date",company.Id,new Clock(at),default);
        Assert.Equal(new DateOnly(year,month,day),witness.CompanyBusinessDate);
        Assert.Equal(zone,witness.CompanyTimeZoneId); Assert.Equal(minutes,witness.UtcOffsetMinutes);
        Assert.Equal(company.Id,witness.LegalEntityId); Assert.Equal("receiving-core-date",witness.TenantId);
        Assert.Equal(at.Ticks-at.Ticks%10,witness.ObservedAtUtc.Ticks);
        Assert.Equal(TimeSpan.Zero,witness.ObservedAtUtc.Offset);
        company.TimeZoneId="UTC"; await db.SaveChangesAsync();
        Assert.Equal(zone,witness.CompanyTimeZoneId); Assert.Equal(minutes,witness.UtcOffsetMinutes);
        var changed=await ProcurementPrincipalReceivingBusinessDate.ObserveAsync(db,
            "receiving-core-date",company.Id,new Clock(at),default);
        Assert.Equal("UTC",changed.CompanyTimeZoneId); Assert.Equal(0,changed.UtcOffsetMinutes);
    }

    [Fact]
    public async Task Foreign_company_does_not_fall_back_to_another_calendar()
    {
        await using var db=TestDbContextFactory.Create("receiving-core-date");
        var company=new LegalEntity { Code="DATE", RegisteredName="Date company" };
        db.LegalEntities.Add(company); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>ProcurementPrincipalReceivingBusinessDate
            .ObserveAsync(db,"another-tenant",company.Id,new Clock(DateTimeOffset.UtcNow),default));
    }

    [Fact]
    public async Task Invalid_persisted_calendar_fails_without_using_UTC_as_a_fallback()
    {
        await using var db=TestDbContextFactory.Create("receiving-core-date");
        var company=new LegalEntity { Code="DATE", RegisteredName="Date company", TimeZoneId="not-a-zone" };
        db.LegalEntities.Add(company); await db.SaveChangesAsync();
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>ProcurementPrincipalReceivingBusinessDate
            .ObserveAsync(db,"receiving-core-date",company.Id,new Clock(DateTimeOffset.UtcNow),default));
        Assert.Equal("PrincipalReceivingCompanyBusinessDateUnavailable",error.Message);
    }

    [Fact]
    public void Inconsistent_Core_observation_is_rejected()
    {
        var at=new DateTimeOffset(2026,10,28,23,30,0,TimeSpan.Zero);
        Assert.Throws<InvalidOperationException>(()=>ProcurementPrincipalReceivingBusinessDate.FromCore(
            "tenant",Guid.NewGuid(),new(new DateOnly(2026,10,28),"Africa/Lusaka",at)));
        Assert.Throws<InvalidOperationException>(()=>ProcurementPrincipalReceivingBusinessDate.FromCore(
            "tenant",Guid.NewGuid(),new(new DateOnly(2026,10,29),"Africa/Lusaka",at.ToOffset(TimeSpan.FromHours(2)))));
    }
}
