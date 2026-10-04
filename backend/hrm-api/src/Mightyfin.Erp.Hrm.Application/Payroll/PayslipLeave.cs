using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Payroll;

public sealed record PayslipLeaveTakenDto(string LeaveTypeCode, string LeaveTypeName,
    string StartDate, string EndDate, decimal Days);

public sealed record PayslipLeaveBalanceDto(string LeaveTypeCode, string LeaveTypeName,
    string AsOfDate, int YearlyEntitlement, decimal Credited, decimal Taken,
    decimal Reserved, decimal Expired, decimal Available);

public interface IPayslipLeaveSummaryService
{
    Task<List<PayslipLeaveTakenDto>> GetAsync(Guid workerId, DateOnly periodStart,
        DateOnly periodEnd, CancellationToken ct);
    Task<List<PayslipLeaveBalanceDto>> GetBalancesAsync(Guid workerId, DateOnly periodEnd,
        CancellationToken ct);
}

public static class PayslipLeaveDays
{
    public static decimal WithinPeriod(LeaveRequest request, DateOnly periodStart,
        DateOnly periodEnd, Func<DateOnly, bool> isWorkingDay)
    {
        var start = request.StartDate > periodStart ? request.StartDate : periodStart;
        var end = request.EndDate < periodEnd ? request.EndDate : periodEnd;
        if (end < start || request.RequestedDays <= 0) return 0;
        if (start == request.StartDate && end == request.EndDate) return request.RequestedDays;

        var fullDays = 0;
        var overlapDays = 0;
        for (var date = request.StartDate; date <= request.EndDate; date = date.AddDays(1))
        {
            if (!isWorkingDay(date)) continue;
            fullDays++;
            if (date >= start && date <= end) overlapDays++;
        }
        if (fullDays == 0 || overlapDays == 0) return 0;
        // Preserve the approved request's exact day total, including partial days.
        // Split a cross-period request by its eligible workdays only.
        return Math.Round(request.RequestedDays * overlapDays / fullDays, 2);
    }
}
