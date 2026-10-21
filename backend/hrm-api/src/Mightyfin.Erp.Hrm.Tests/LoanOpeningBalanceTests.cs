using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public class LoanOpeningBalanceTests
{
    [Fact]
    public async Task ImportedLoan_KeepsOpeningRecoveryAndStartsInOctober()
    {
        using var db = TestDbContextFactory.Create("loan-opening-balance");
        var worker = new Worker { EmployeeNo = "LOAN-1", FirstName = "Test", LastName = "Worker", Status = "active", WorkerType = "employee" };
        var group = new PayGroup { Code = "MONTHLY", Name = "Monthly", Currency = "ZMW" };
        var september = new PayPeriod { PayGroup = group, PeriodLabel = "September 2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 9, 30), CutoffDate = new DateOnly(2026, 9, 23), PayDate = new DateOnly(2026, 9, 28) };
        var october = new PayPeriod { PayGroup = group, PeriodLabel = "October 2026", StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2026, 10, 31), CutoffDate = new DateOnly(2026, 10, 23), PayDate = new DateOnly(2026, 10, 28) };
        var loan = new SalaryAdvance { Worker = worker, Amount = 26741m, OpeningRecoveredAmount = 7500m, InstallmentAmount = 2500m, RecordType = "loan", IssueDate = new DateOnly(2026, 1, 7), DeductionStartDate = new DateOnly(2026, 10, 1), DeductFromPayslip = true };
        db.PayPeriods.AddRange(september, october);
        db.SalaryAdvances.Add(loan);
        await db.SaveChangesAsync();

        var repo = new PayrollRepository(db);
        var recovered = await repo.GetSalaryAdvanceRecoveredAmountsAsync([loan.Id], default);
        var septemberLoans = await repo.LoadDeductibleSalaryAdvancesAsync(september.Id, null, default);
        var octoberLoans = await repo.LoadDeductibleSalaryAdvancesAsync(october.Id, null, default);

        Assert.Equal(7500m, recovered[loan.Id]);
        Assert.Equal(19241m, loan.Amount - recovered[loan.Id]);
        Assert.Empty(septemberLoans);
        Assert.Single(octoberLoans);
    }

    [Fact]
    public async Task ReleasedPayroll_AddsToOlderRepayments_AndCapsFinalInstalment()
    {
        using var db = TestDbContextFactory.Create("loan-release-recovery");
        var worker = new Worker { EmployeeNo = "LOAN-2", FirstName = "Test", LastName = "Borrower", Status = "active", WorkerType = "employee" };
        var group = new PayGroup { Code = "MONTHLY", Name = "Monthly", Currency = "ZMW" };
        var period = new PayPeriod { PayGroup = group, PeriodLabel = "October 2026", StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2026, 10, 31), CutoffDate = new DateOnly(2026, 10, 23), PayDate = new DateOnly(2026, 10, 28) };
        var loan = new SalaryAdvance { Worker = worker, Amount = 1000m, OpeningRecoveredAmount = 850m, InstallmentAmount = 100m, RecordType = "loan", DeductionStartDate = new DateOnly(2026, 10, 1), DeductFromPayslip = true };
        var run = new PayrollRun { PayPeriod = period, PayGroupId = group.Id, Status = "draft" };
        var line = new PayrollRunLine { Run = run, Worker = worker, GrossPay = 500m, TotalDeductions = 100m, NetPay = 400m };
        line.Components.Add(new PayrollLineComponent { ComponentCode = $"salary-advance-{loan.Id:N}", ComponentName = "Loan recovery", ComponentType = "deduction", Amount = 100m });
        db.SalaryAdvances.Add(loan);
        db.PayrollRunLines.Add(line);
        await db.SaveChangesAsync();

        var repo = new PayrollRepository(db);
        var beforeRelease = await repo.GetSalaryAdvanceRecoveredAmountsAsync([loan.Id], default);
        Assert.Equal(850m, beforeRelease[loan.Id]);

        run.Status = "released";
        await db.SaveChangesAsync();
        var afterRelease = await repo.GetSalaryAdvanceRecoveredAmountsAsync([loan.Id], default);
        Assert.Equal(950m, afterRelease[loan.Id]);

        var context = new CalcContext(worker, new WorkerPayrollProfile { Worker = worker, PayGroup = group }, [], [], []);
        context.AddSalaryAdvances([loan], afterRelease);
        var finalDeduction = Assert.Single(context.Components);
        Assert.Equal(50m, finalDeduction.Amount);
        Assert.Equal(50m, context.Deductions);

        var finalRun = new PayrollRun { PayPeriod = period, PayGroupId = group.Id, Status = "released" };
        var finalLine = new PayrollRunLine { Run = finalRun, Worker = worker, GrossPay = 500m, TotalDeductions = 50m, NetPay = 450m };
        finalLine.Components.Add(new PayrollLineComponent { ComponentCode = $"salary-advance-{loan.Id:N}", ComponentName = "Loan recovery", ComponentType = "deduction", Amount = 50m });
        db.PayrollRunLines.Add(finalLine);
        await db.SaveChangesAsync();
        var settled = await repo.GetSalaryAdvanceRecoveredAmountsAsync([loan.Id], default);
        Assert.Equal(1000m, settled[loan.Id]);
        var nextContext = new CalcContext(worker, new WorkerPayrollProfile { Worker = worker, PayGroup = group }, [], [], []);
        nextContext.AddSalaryAdvances([loan], settled);
        Assert.Empty(nextContext.Components);
    }

    [Fact]
    public void FinalInstalment_AbsorbsTwoNgweeRoundingResidue()
    {
        var worker = new Worker { EmployeeNo = "LOAN-3", FirstName = "Test", LastName = "Borrower" };
        var loan = new SalaryAdvance
        {
            Worker = worker, Amount = 6700m, OpeningRecoveredAmount = 0m,
            InstallmentAmount = 957.14m, RecordType = "loan", DeductFromPayslip = true,
        };
        var context = new CalcContext(worker, new WorkerPayrollProfile { Worker = worker }, [], [], []);

        context.AddSalaryAdvances([loan], new Dictionary<Guid, decimal> { [loan.Id] = 5742.84m });

        Assert.Equal(957.16m, Assert.Single(context.Components).Amount);
        Assert.Equal(957.16m, context.Deductions);
    }
}
