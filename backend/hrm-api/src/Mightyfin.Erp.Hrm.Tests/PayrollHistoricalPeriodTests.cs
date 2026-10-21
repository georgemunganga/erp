using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public class PayrollHistoricalPeriodTests
{
    [Fact]
    public async Task CalculationInputs_UseProfileEffectiveInSelectedPeriod()
    {
        using var db = TestDbContextFactory.Create("historical-profile-dates");
        var group = new PayGroup { Code = "MONTHLY", Name = "Monthly", Currency = "ZMW" };
        var september = new PayPeriod
        {
            PayGroup = group, PeriodLabel = "September 2026", StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 9, 30), CutoffDate = new DateOnly(2026, 9, 23),
            PayDate = new DateOnly(2026, 9, 28), Status = "historical", IsHistorical = true,
        };
        var worker = new Worker { EmployeeNo = "HIST-1", FirstName = "Historical", LastName = "Worker", Status = "active", WorkerType = "employee" };
        var structure = new SalaryStructure { Code = "HIST-STRUCT", Name = "Historical structure" };
        db.PayGroups.Add(group);
        db.PayPeriods.Add(september);
        db.Workers.Add(worker);
        db.SalaryStructures.Add(structure);
        db.WorkerPayrollProfiles.Add(new WorkerPayrollProfile
        {
            Worker = worker, PayGroup = group, Structure = structure, EffectiveFrom = new DateOnly(2026, 10, 1),
        });
        await db.SaveChangesAsync();

        var repo = new PayrollRepository(db);
        var before = await repo.LoadCalculationInputsAsync(september.Id, CancellationToken.None);
        Assert.Empty(before.Profiles);

        var profile = db.WorkerPayrollProfiles.Single();
        profile.EffectiveFrom = new DateOnly(2026, 9, 1);
        await db.SaveChangesAsync();
        var after = await repo.LoadCalculationInputsAsync(september.Id, CancellationToken.None);
        Assert.Single(after.Profiles);
    }
}
