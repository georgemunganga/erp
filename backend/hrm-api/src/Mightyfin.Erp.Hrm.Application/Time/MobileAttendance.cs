using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Time;

/// <summary>Invoked with trusted server time after employee/company admission, never device time.</summary>
public interface IMobileAttendanceService
{
    Task<PunchResultDto> PunchMobileAsync(Guid workerId, bool clockIn, DateTimeOffset serverTime,
        string timeZone, string? expectedDate, Guid? expectedRecord, CancellationToken ct);
}

public interface IMobileAttendanceRepository
{
    Task<List<AttendanceRecord>> OpenMobilePunchesAsync(Guid workerId, CancellationToken ct);
}

public static class MobileAttendanceTime
{
    public static (DateOnly Date, TimeOnly Time) Local(DateTimeOffset instant, string timeZone)
    {
        var local = TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(timeZone));
        return (DateOnly.FromDateTime(local.DateTime), TimeOnly.FromDateTime(local.DateTime));
    }

    public static DateTimeOffset? Instant(AttendanceRecord record, bool end, string timeZone)
    {
        var value = end ? record.ClockOut : record.ClockIn;
        if (value is null) return null;
        var date = record.WorkDate;
        if (end && record.ClockIn.HasValue && value.Value < record.ClockIn.Value) date = date.AddDays(1);
        // Existing legacy self-service stores UTC clock values. Native/manual/import
        // attendance is in company civil time; never silently reinterpret old rows.
        var stamp = date.ToDateTime(value.Value);
        return record.Source == "self-service"
            ? new DateTimeOffset(DateTime.SpecifyKind(stamp, DateTimeKind.Utc))
            : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(stamp, TimeZoneInfo.FindSystemTimeZoneById(timeZone)));
    }
}
