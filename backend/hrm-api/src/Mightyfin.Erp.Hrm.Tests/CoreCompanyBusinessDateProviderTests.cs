using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreCompanyBusinessDateProviderTests
{
    private sealed class FixedClock(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }

    [Fact]
    public async Task ComputesCompanyDateFromPersistedZoneAndTrustedClock()
    {
        await using var db = TestDbContextFactory.Create("date-tenant");
        var company = new LegalEntity { Code = "DATE", RegisteredName = "Date Company", TimeZoneId = "Pacific/Auckland" };
        db.LegalEntities.Add(company);
        await db.SaveChangesAsync();
        var instant = new DateTimeOffset(2026, 10, 23, 23, 30, 0, TimeSpan.Zero);
        var provider = new CoreCompanyBusinessDateProvider(db, new FixedClock(instant));
        var first = await provider.GetAsync("date-tenant", company.Id, default);
        Assert.Equal(new DateOnly(2026, 10, 24), first.Date);
        Assert.Equal("Pacific/Auckland", first.TimeZoneId);
        Assert.Equal(instant, first.ObservedAtUtc);

        company.TimeZoneId = "UTC";
        await db.SaveChangesAsync();
        var changed = await provider.GetAsync("date-tenant", company.Id, default);
        Assert.Equal(new DateOnly(2026, 10, 23), changed.Date);
        Assert.Equal("UTC", changed.TimeZoneId);
    }

    [Fact]
    public async Task RejectsUnavailableOrForeignCompanyWithoutFallback()
    {
        await using var db = TestDbContextFactory.Create("date-tenant");
        var company = new LegalEntity { Code = "DATE", RegisteredName = "Date Company" };
        db.LegalEntities.Add(company);
        await db.SaveChangesAsync();
        var provider = new CoreCompanyBusinessDateProvider(db, new FixedClock(DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync("foreign-tenant", company.Id, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync("date-tenant", Guid.NewGuid(), default));
        company.IsArchived = true;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync("date-tenant", company.Id, default));
    }
}
