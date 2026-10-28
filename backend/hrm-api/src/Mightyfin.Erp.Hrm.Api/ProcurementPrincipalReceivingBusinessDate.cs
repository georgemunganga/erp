using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Adapts the shared Core company calendar to retained Procurement evidence.
/// The caller supplies its already-enlisted Core context; no HTTP date or timezone is trusted.</summary>
internal static class ProcurementPrincipalReceivingBusinessDate
{
    internal static async Task<PrincipalReceivingBusinessDate> ObserveAsync(HrmDbContext core,
        string tenantId, Guid companyId, TimeProvider clock, CancellationToken ct)
    {
        try
        {
            var observation = await new CoreCompanyBusinessDateProvider(core, clock)
                .GetAsync(tenantId, companyId, ct);
            return FromCore(tenantId, companyId, observation);
        }
        catch (DomainException error)
        {
            throw new InvalidOperationException("PrincipalReceivingCompanyBusinessDateUnavailable", error);
        }
    }

    internal static PrincipalReceivingBusinessDate FromCore(string tenantId, Guid companyId,
        CoreCompanyBusinessDate observation)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || companyId == Guid.Empty || observation is null
            || observation.Date == default || observation.ObservedAtUtc == default
            || observation.ObservedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("PrincipalReceivingCompanyBusinessDateUnavailable");
        var zoneId = CompanyTimeZone.Validate(observation.TimeZoneId);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        // PostgreSQL timestamps retain microseconds. Keep one observation and
        // truncate only its sub-microsecond ticks, never read a second clock.
        var at = new DateTimeOffset(observation.ObservedAtUtc.Ticks
            - observation.ObservedAtUtc.Ticks % 10, TimeSpan.Zero);
        var offset = zone.GetUtcOffset(at);
        if (offset.TotalMinutes != Math.Truncate(offset.TotalMinutes)
            || offset.TotalMinutes is < -840 or > 840
            || DateOnly.FromDateTime(at.ToOffset(offset).DateTime) != observation.Date)
            throw new InvalidOperationException("PrincipalReceivingCompanyBusinessDateUnavailable");
        return new(1, tenantId, companyId, observation.Date, zoneId,
            checked((int)offset.TotalMinutes), at);
    }
}
