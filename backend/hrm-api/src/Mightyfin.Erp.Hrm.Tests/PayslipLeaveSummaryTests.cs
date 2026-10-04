using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;

namespace Mightyfin.Erp.Hrm.Tests;

public class PayslipLeaveSummaryTests
{
    [Fact]
    public async Task SeptemberPayslipBalanceExcludesOctoberAccrualAndMatchesLedgerCategories()
    {
        await using var db = TestDbContextFactory.Create();
        var worker = new Worker { EmployeeNo = "TEST-03", FirstName = "Test", LastName = "Worker" };
        db.Workers.Add(worker);
        db.LeaveTypes.AddRange(
            new LeaveType { Code = "uat-annual", Name = "Annual Leave", DefaultDaysPerYear = 24 },
            new LeaveType { Code = "study", Name = "Study Leave", DefaultDaysPerYear = 14 },
            new LeaveType { Code = "sick", Name = "Sick Leave" });
        db.LeaveBalanceLedgers.AddRange(
            new LeaveBalanceLedger { WorkerId = worker.Id, LeaveTypeCode = "uat-annual", Days = 14, Reason = "manual-adjustment", ForDate = new(2026, 8, 31) },
            new LeaveBalanceLedger { WorkerId = worker.Id, LeaveTypeCode = "uat-annual", Days = 2, Reason = "monthly-accrual", ForDate = new(2026, 9, 1) },
            new LeaveBalanceLedger { WorkerId = worker.Id, LeaveTypeCode = "uat-annual", Days = -7, Reason = "taken", ForDate = new(2026, 9, 22) },
            new LeaveBalanceLedger { WorkerId = worker.Id, LeaveTypeCode = "uat-annual", Days = 2, Reason = "monthly-accrual", ForDate = new(2026, 10, 1) },
            new LeaveBalanceLedger { WorkerId = worker.Id, LeaveTypeCode = "study", Days = 7, Reason = "manual-adjustment", ForDate = new(2026, 9, 18) },
            new LeaveBalanceLedger { WorkerId = worker.Id, LeaveTypeCode = "study", Days = -7, Reason = "taken", ForDate = new(2026, 9, 18) });
        await db.SaveChangesAsync();

        var service = new PayslipLeaveSummaryService(db);
        var september = await service.GetBalancesAsync(worker.Id, new(2026, 9, 30), default);
        var annual = Assert.Single(september, row => row.LeaveTypeCode == "uat-annual");
        Assert.Equal(24, annual.YearlyEntitlement);
        Assert.Equal(16, annual.Credited);
        Assert.Equal(7, annual.Taken);
        Assert.Equal(9, annual.Available);
        Assert.Equal("2026-09-30", annual.AsOfDate);
        Assert.Equal(0, Assert.Single(september, row => row.LeaveTypeCode == "study").Available);
        Assert.DoesNotContain(september, row => row.LeaveTypeCode == "sick");
        var october = await service.GetBalancesAsync(worker.Id, new(2026, 10, 31), default);
        Assert.Equal(11, Assert.Single(october, row => row.LeaveTypeCode == "uat-annual").Available);
    }

    [Fact]
    public async Task ApprovedLeaveAppearsInItsPayPeriodAndCancelledLeaveDoesNot()
    {
        await using var db = TestDbContextFactory.Create();
        var worker = new Worker { EmployeeNo = "TEST-01", FirstName = "Test", LastName = "Worker" };
        var entity = new LegalEntity { Code = "test", RegisteredName = "Test" };
        db.Workers.Add(worker);
        db.LegalEntities.Add(entity);
        db.WorkCalendars.Add(new WorkCalendar { Name = "Sunday rest", LegalEntityId = entity.Id,
            IsDefault = true, WeekendDays = "sun" });
        db.LeaveTypes.AddRange(
            new LeaveType { Code = "annual", Name = "Annual Leave" },
            new LeaveType { Code = "study", Name = "Study Leave" });
        db.LeaveRequests.AddRange(
            new LeaveRequest { WorkerId = worker.Id, LeaveTypeCode = "annual",
                StartDate = new(2026, 9, 22), EndDate = new(2026, 9, 29), RequestedDays = 7, Status = "approved" },
            new LeaveRequest { WorkerId = worker.Id, LeaveTypeCode = "study",
                StartDate = new(2026, 9, 18), EndDate = new(2026, 9, 25), RequestedDays = 7, Status = "approved" },
            new LeaveRequest { WorkerId = worker.Id, LeaveTypeCode = "annual",
                StartDate = new(2026, 9, 1), EndDate = new(2026, 9, 2), RequestedDays = 2, Status = "cancelled" });
        await db.SaveChangesAsync();

        var rows = await new PayslipLeaveSummaryService(db).GetAsync(worker.Id,
            new(2026, 9, 1), new(2026, 9, 30), default);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.LeaveTypeName == "Annual Leave" && row.Days == 7);
        Assert.Contains(rows, row => row.LeaveTypeName == "Study Leave" && row.Days == 7);
        Assert.Equal(14, rows.Sum(row => row.Days));
    }

    [Fact]
    public async Task CrossMonthLeaveUsesOnlyEligibleDaysWithinEachPeriod()
    {
        await using var db = TestDbContextFactory.Create();
        var worker = new Worker { EmployeeNo = "TEST-02", FirstName = "Test", LastName = "Worker" };
        var entity = new LegalEntity { Code = "test2", RegisteredName = "Test" };
        db.Workers.Add(worker);
        db.LegalEntities.Add(entity);
        db.WorkCalendars.Add(new WorkCalendar { Name = "Sunday rest", LegalEntityId = entity.Id,
            IsDefault = true, WeekendDays = "sun" });
        db.LeaveTypes.Add(new LeaveType { Code = "annual", Name = "Annual Leave" });
        db.LeaveRequests.Add(new LeaveRequest { WorkerId = worker.Id, LeaveTypeCode = "annual",
            StartDate = new(2026, 9, 29), EndDate = new(2026, 10, 3), RequestedDays = 5, Status = "approved" });
        await db.SaveChangesAsync();

        var service = new PayslipLeaveSummaryService(db);
        var september = await service.GetAsync(worker.Id, new(2026, 9, 1), new(2026, 9, 30), default);
        var october = await service.GetAsync(worker.Id, new(2026, 10, 1), new(2026, 10, 31), default);

        Assert.Equal(2, Assert.Single(september).Days);
        Assert.Equal("2026-09-29", september[0].StartDate);
        Assert.Equal("2026-09-30", september[0].EndDate);
        Assert.Equal(3, Assert.Single(october).Days);
        Assert.Equal("2026-10-01", october[0].StartDate);
        Assert.Equal("2026-10-03", october[0].EndDate);
    }
}
