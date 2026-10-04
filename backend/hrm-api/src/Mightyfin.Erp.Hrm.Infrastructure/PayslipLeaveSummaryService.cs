using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;

public sealed class PayslipLeaveSummaryService(HrmDbContext db) : IPayslipLeaveSummaryService
{
    public async Task<List<PayslipLeaveTakenDto>> GetAsync(Guid workerId, DateOnly periodStart,
        DateOnly periodEnd, CancellationToken ct)
    {
        if (periodEnd < periodStart) return [];
        var requests = await db.LeaveRequests.AsNoTracking()
            .Where(r => !r.IsArchived && r.WorkerId == workerId && r.Status == "approved"
                && r.StartDate <= periodEnd && r.EndDate >= periodStart)
            .OrderBy(r => r.StartDate).ThenBy(r => r.EndDate).ToListAsync(ct);
        if (requests.Count == 0) return [];

        var worker = await db.Workers.AsNoTracking().FirstAsync(w => w.Id == workerId, ct);
        var codes = requests.Select(r => r.LeaveTypeCode).Distinct().ToList();
        var types = await db.LeaveTypes.AsNoTracking().Where(t => codes.Contains(t.Code)).ToListAsync(ct);
        var typeNames = types.GroupBy(t => t.Code)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.CreatedAt).First().Name);
        var calendars = await db.WorkCalendars.AsNoTracking().Include(c => c.Holidays)
            .Where(c => !c.IsArchived).ToListAsync(ct);
        var firstDate = requests.Min(r => r.StartDate);
        var lastDate = requests.Max(r => r.EndDate);
        var assignments = await db.WorkerShiftAssignments.AsNoTracking()
            .Where(a => !a.IsArchived && a.WorkerId == workerId && a.EffectiveFrom <= lastDate
                && (a.EffectiveTo == null || a.EffectiveTo >= firstDate)).ToListAsync(ct);
        var branchCalendarId = worker.LocationId.HasValue
            ? await db.WorkLocations.AsNoTracking().Where(l => l.Id == worker.LocationId.Value)
                .Select(l => l.DefaultCalendarId).FirstOrDefaultAsync(ct)
            : null;

        bool IsWorkingDay(DateOnly date)
        {
            var assignment = assignments.Where(a => a.EffectiveFrom <= date
                    && (a.EffectiveTo == null || a.EffectiveTo >= date))
                .OrderByDescending(a => a.EffectiveFrom).FirstOrDefault();
            var calendar = calendars.FirstOrDefault(c => c.Id == assignment?.CalendarId)
                ?? calendars.FirstOrDefault(c => c.Id == branchCalendarId)
                ?? calendars.FirstOrDefault(c => c.IsDefault);
            if (calendar is null) return true;
            var day = date.DayOfWeek.ToString()[..3];
            if (calendar.WeekendDays.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Contains(day, StringComparer.OrdinalIgnoreCase)) return false;
            return !calendar.Holidays.Where(h => !h.IsArchived).Any(h =>
            {
                var observed = DateOnly.TryParse(h.ObservedOn, out var value) ? value : h.HolidayDate;
                return observed == date || (h.IsRecurring && observed.Month == date.Month && observed.Day == date.Day);
            });
        }

        return requests.Select(request =>
        {
            var days = PayslipLeaveDays.WithinPeriod(request, periodStart, periodEnd, IsWorkingDay);
            var start = request.StartDate < periodStart ? periodStart : request.StartDate;
            var end = request.EndDate > periodEnd ? periodEnd : request.EndDate;
            return new PayslipLeaveTakenDto(request.LeaveTypeCode,
                typeNames.GetValueOrDefault(request.LeaveTypeCode) ?? request.LeaveTypeCode,
                start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"), days);
        }).Where(row => row.Days > 0).ToList();
    }
}
