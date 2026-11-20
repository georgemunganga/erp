namespace Mightyfin.Erp.Hrm.Application.Payroll;

/// <summary>Calendar dates are concrete payroll inputs; no bank payment is scheduled here.</summary>
public static class PayrollPayday
{
    public static void Validate(string rule, string frequency)
    {
        if (rule is not ("calendar-day" or "last-working-day") || rule == "last-working-day" && frequency != "monthly")
            throw new DomainException("pay-group-payday-rule-invalid", "Last working day requires monthly payroll; otherwise choose a calendar day.");
    }

    public static DateOnly Resolve(int year, int month, int day, string rule,
        string weekendDays = "sat,sun", IEnumerable<DateOnly>? holidays = null)
    {
        Validate(rule, "monthly");
        if (day is < 1 or > 31) throw new DomainException("pay-group-payday-invalid", "Payday must be between 1 and 31.");
        var end = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        if (rule == "calendar-day") return new DateOnly(year, month, Math.Min(day, end.Day));
        var weekends = weekendDays.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.ToLowerInvariant()).ToHashSet();
        string[] names = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];
        if (weekends.Count >= 7 || weekends.Any(x => !names.Contains(x)))
            throw new DomainException("payday-calendar-invalid", "Choose a work calendar with at least one working day.");
        var excluded = holidays?.ToHashSet() ?? [];
        for (var date = end; date.Month == month; date = date.AddDays(-1))
            if (!weekends.Contains(names[(int)date.DayOfWeek]) && !excluded.Contains(date)) return date;
        throw new DomainException("payday-calendar-invalid", "No working day is available in this pay month.");
    }
}
