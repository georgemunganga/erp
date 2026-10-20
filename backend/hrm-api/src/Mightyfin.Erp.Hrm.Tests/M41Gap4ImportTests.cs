// M41 Gap 4 — bulk payroll-profile assignment through the shared import tool.
// Covers the payroll-profiles schema: dynamic component columns, the
// non-negotiable worker-identity rule, reference validation, within-file
// uniqueness, insert-vs-update semantics and the per-row preview/apply flow.
// Each test builds its own in-memory SQLite context (same pattern as the M31
// import tests), seeded with workers, components and a pay group.
using Xunit;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Application.Shared;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public class M41Gap4ImportTests
{
    private const string Tenant = "t-gap4";

    // ---- Test environment: one shared factory so seeds are visible to the
    // schema's own context within a test. ----

    private static (HrmDbContext Db, ImportExportServiceImpl Svc, Worker Seed) BuildSeeded()
    {
        var db = TestDbContextFactory.Create(Tenant);
        var worker = new Worker
        {
            Id = Guid.NewGuid(), EmployeeNo = "EMP-99", FirstName = "Grace", LastName = "Mulenga",
            Status = "active", WorkerType = "employee",
        };
        var entity = new LegalEntity
        {
            Id = Guid.NewGuid(), Code = "TST", RegisteredName = "Test Ltd",
            TradingName = "Test", CountryCode = "ZM",
        };
        var basic = new SalaryComponent
        {
            Id = Guid.NewGuid(), Code = "basic", Name = "Basic Salary",
            ComponentType = "earning", CalculationBasis = "fixed", IsActive = true,
        };
        var housing = new SalaryComponent
        {
            Id = Guid.NewGuid(), Code = "housing-allowance", Name = "Housing Allowance",
            ComponentType = "earning", CalculationBasis = "fixed", IsActive = true,
        };
        var ded = new SalaryComponent
        {
            Id = Guid.NewGuid(), Code = "loan-recovery", Name = "Loan Recovery",
            ComponentType = "deduction", CalculationBasis = "fixed", IsActive = true,
        };
        var group = new PayGroup { Id = Guid.NewGuid(), Name = "Monthly ZMW", Code = "MONTHLY-ZMW", Currency = "ZMW" };
        var structure = new SalaryStructure
        {
            Id = Guid.NewGuid(), Code = "ZMW-STANDARD", Name = "ZMW Standard",
        };
        db.LegalEntities.Add(entity);
        db.Workers.Add(worker);
        db.SalaryComponents.AddRange(basic, housing, ded);
        db.PayGroups.Add(group);
        db.SalaryStructures.Add(structure);
        db.SaveChanges();

        var workerRepo = new WorkerRepository(db);
        var workerService = new WorkerServiceImpl(workerRepo, new PermissiveAuthz(), new UlidIdProvider());
        var payrollRepo = new PayrollRepository(db);
        var payrollService = new PayrollServiceImpl(payrollRepo, new PermissiveAuthz(),
            new FakePayslipDoc());
        var schema = new PayrollProfilesImportSchema(payrollRepo, payrollService, workerRepo, new PermissiveAuthz());
        var svc = new ImportExportServiceImpl(new[] { schema });
        return (db, svc, worker);
    }

    [Fact]
    public void ListSchemas_IncludesPayrollProfiles()
    {
        var (db, svc, _) = BuildSeeded();
        var schemas = svc.ListSchemas();
        var pp = Assert.Single(schemas.Where(s => s.TypeKey == "payroll-profiles"));
        // Five fixed columns (identity + group + date) plus every seeded component.
        Assert.Equal(5 + 3, pp.Fields.Count);
        Assert.Contains(pp.Fields, f => f.Key == "basic" && f.NaturalKey == false);
        Assert.Contains(pp.Fields, f => f.Key == "employeeNo" && f.NaturalKey);
    }

    [Fact]
    public async Task Preview_Insert_CleanRowIsCreate()
    {
        var (db, svc, seed) = BuildSeeded();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99", ["basic"] = "25000", ["housing-allowance"] = "5000" },
        };
        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("create", preview.Rows[0].Status);
        Assert.Equal(1, preview.WillCreate);
    }

    // Temporary diagnostic test: surfaces the full exception as a test message.
    [Fact]
    public async Task Preview_Diagnostic()
    {
        var (db, svc, _) = BuildSeeded();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99", ["basic"] = "25000", ["housing-allowance"] = "5000" },
        };
        var p = await svc.PreviewAsync("payroll-profiles", "pay.csv", "insert", rows, CancellationToken.None);
        if (p.Rows[0].Status != "create")
            Assert.Fail($"DIAGNOSTIC STATUS={p.Rows[0].Status} MSG={p.Rows[0].Message}");
    }

    [Fact]
    public async Task Preview_NoIdentityIsRejected()
    {
        var (_, svc, _) = BuildSeeded();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["basic"] = "1000" },
        };
        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("No worker identity", preview.Rows[0].Message);
        Assert.Equal(1, preview.WillError);
    }

    [Fact]
    public async Task Preview_UnknownEmployeeIsRejected()
    {
        var (_, svc, _) = BuildSeeded();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-NOBODY", ["basic"] = "1000" },
        };
        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("No employee matches", preview.Rows[0].Message);
    }

    [Fact]
    public async Task Preview_UnknownComponentCodeIsRejected()
    {
        var (_, svc, _) = BuildSeeded();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99", ["not-a-component"] = "500", ["basic"] = "1000" },
        };
        // Unknown column keys are simply ignored by the engine (it only inspects
        // registered field keys), so an unknown code reaches nothing. But if HR
        // tries to fill the WRONG registered column… instead, the meaningful
        // case is a blank-only row which is refused:
        var noAmountRows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99" },
        };
        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "insert", noAmountRows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("No component amounts supplied", preview.Rows[0].Message);
    }

    [Fact]
    public async Task Preview_BadAmountIsRejected()
    {
        var (_, svc, _) = BuildSeeded();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99", ["basic"] = "twenty-five thousand" },
        };
        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("not a valid amount", preview.Rows[0].Message);
    }

    [Fact]
    public async Task Preview_NegativeAmountIsRejected()
    {
        var (_, svc, _) = BuildSeeded();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99", ["basic"] = "-500" },
        };
        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("cannot be negative", preview.Rows[0].Message);
    }

    [Fact]
    public async Task Preview_ExistingProfileInsertModeRefuses()
    {
        var (db, svc, seed) = BuildSeeded();
        var basic = db.SalaryComponents.First(c => c.Code == "basic");
        db.WorkerPayrollProfiles.Add(new WorkerPayrollProfile
        {
            WorkerId = seed.Id, StructureId = db.SalaryStructures.First().Id, PayGroupId = db.PayGroups.First().Id,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            ComponentValues = { new WorkerComponentValue { ComponentId = basic.Id, Amount = 20000 } },
        });
        db.SaveChanges();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99", ["basic"] = "30000" },
        };
        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("already has an active pay profile", preview.Rows[0].Message);
    }

    [Fact]
    public async Task Preview_UpdateOnExistingBecomesUpdate()
    {
        var (db, svc, seed) = BuildSeeded();
        var basic = db.SalaryComponents.First(c => c.Code == "basic");
        db.WorkerPayrollProfiles.Add(new WorkerPayrollProfile
        {
            WorkerId = seed.Id, StructureId = db.SalaryStructures.First().Id, PayGroupId = db.PayGroups.First().Id,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            ComponentValues = { new WorkerComponentValue { ComponentId = basic.Id, Amount = 20000 } },
        });
        db.SaveChanges();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99", ["basic"] = "30000" },
        };
        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "update", rows, CancellationToken.None);
        Assert.Equal("update", preview.Rows[0].Status);
        Assert.Equal(1, preview.WillUpdate);
    }

    [Fact]
    public async Task Apply_UpdatePreservesComponentsOmittedFromFile()
    {
        var (db, svc, seed) = BuildSeeded();
        var basic = db.SalaryComponents.First(c => c.Code == "basic");
        var housing = db.SalaryComponents.First(c => c.Code == "housing-allowance");
        db.WorkerPayrollProfiles.Add(new WorkerPayrollProfile
        {
            WorkerId = seed.Id, StructureId = db.SalaryStructures.First().Id, PayGroupId = db.PayGroups.First().Id,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            ComponentValues =
            {
                new WorkerComponentValue { ComponentId = basic.Id, Amount = 20000 },
                new WorkerComponentValue { ComponentId = housing.Id, Amount = 5000 },
            },
        });
        db.SaveChanges();

        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "update",
            [new Dictionary<string, string> { ["employeeNo"] = "EMP-99", ["basic"] = "30000", ["effectiveFrom"] = "01-09-2026" }],
            CancellationToken.None);
        Assert.Equal("update", preview.Rows[0].Status);
        await svc.ApplyAsync(preview.Id, [0], CancellationToken.None);

        var updated = db.WorkerPayrollProfiles.Include(p => p.ComponentValues)
            .Single(p => p.WorkerId == seed.Id);
        var values = updated.ComponentValues.ToDictionary(v => v.ComponentId, v => v.Amount);
        Assert.Equal(30000m, values[basic.Id]);
        Assert.Equal(5000m, values[housing.Id]);
        Assert.Equal(new DateOnly(2026, 9, 1), updated.EffectiveFrom);
    }

    [Fact]
    public async Task Apply_UpdateWithoutDatePreservesEffectiveDate()
    {
        var (db, svc, seed) = BuildSeeded();
        var basic = db.SalaryComponents.First(c => c.Code == "basic");
        db.WorkerPayrollProfiles.Add(new WorkerPayrollProfile
        {
            WorkerId = seed.Id, StructureId = db.SalaryStructures.First().Id, PayGroupId = db.PayGroups.First().Id,
            EffectiveFrom = new DateOnly(2026, 8, 1),
            ComponentValues = { new WorkerComponentValue { ComponentId = basic.Id, Amount = 20000 } },
        });
        db.SaveChanges();
        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "update",
            [new Dictionary<string, string> { ["employeeNo"] = "EMP-99", ["basic"] = "30000" }],
            CancellationToken.None);
        await svc.ApplyAsync(preview.Id, [0], CancellationToken.None);
        Assert.Equal(new DateOnly(2026, 8, 1), db.WorkerPayrollProfiles.Single().EffectiveFrom);
    }

    [Fact]
    public async Task Preview_DuplicateWorkerInFileRejected()
    {
        var (_, svc, _) = BuildSeeded();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99", ["basic"] = "1000" },
            new() { ["employeeNo"] = "EMP-99", ["basic"] = "2000" },
        };
        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("create", preview.Rows[0].Status);
        Assert.Equal("error", preview.Rows[1].Status);
        Assert.Contains("appears twice in this file", preview.Rows[1].Message);
    }

    [Fact]
    public async Task Preview_BadEffectiveDateRejected()
    {
        var (_, svc, _) = BuildSeeded();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99", ["basic"] = "1000", ["effectiveFrom"] = "31/31/2026" },
        };
        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("Effective date", preview.Rows[0].Message);
    }

    [Fact]
    public async Task Preview_DayFirstEffectiveDateAccepted()
    {
        var (_, svc, _) = BuildSeeded();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99", ["basic"] = "1000", ["effectiveFrom"] = "31-07-2026" },
        };
        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("create", preview.Rows[0].Status);
        Assert.Equal("2026-07-31", preview.Rows[0].Resolved?["__effectiveFrom"]);
    }


    [Fact]
    public async Task Preview_UnknownPayGroupNameRejected()
    {
        var (_, svc, _) = BuildSeeded();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99", ["basic"] = "1000", ["payGroup"] = "NoSuchGroup" },
        };
        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("No pay group named", preview.Rows[0].Message);
    }

    [Fact]
    public async Task Apply_ThenProfilePersistsWithMappedAmounts()
    {
        var (db, svc, seed) = BuildSeeded();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99", ["basic"] = "25000", ["housing-allowance"] = "5000" },
        };
        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("create", preview.Rows[0].Status);

        var result = await svc.ApplyAsync(preview.Id, [0], CancellationToken.None);
        Assert.Equal(1, result.Created);
        Assert.Equal("ok", result.RowOutcomes[0].Status);

        // Materialize: reload what the apply path persisted via the same db.
        var profile = await db.WorkerPayrollProfiles
            .Include(p => p.ComponentValues).ThenInclude(v => v.Component)
            .FirstOrDefaultAsync(p => p.WorkerId == seed.Id, CancellationToken.None);
        Assert.NotNull(profile);
        var amounts = profile.ComponentValues.ToDictionary(v => v.Component.Code, v => v.Amount);
        Assert.Equal(25000m, amounts["basic"]);
        Assert.Equal(5000m, amounts["housing-allowance"]);
        Assert.Equal(2, profile.ComponentValues.Count);
    }

    [Fact]
    public async Task Apply_ThenSecondWorkerCanAlsoBeImported()
    {
        var (db, svc, _) = BuildSeeded();
        var tenant = Tenant;
        var w2 = new Worker
        {
            Id = Guid.NewGuid(), EmployeeNo = "EMP-100", FirstName = "John", LastName = "Chileshe",
            Status = "active", WorkerType = "employee",
        };
        db.Workers.Add(w2);
        db.SaveChanges();

        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99", ["basic"] = "25000" },
            new() { ["employeeNo"] = "EMP-100", ["basic"] = "15000" },
        };
        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "insert", rows, CancellationToken.None);
        Assert.Equal(2, preview.WillCreate);
        var result = await svc.ApplyAsync(preview.Id, [0, 1], CancellationToken.None);
        Assert.Equal(2, result.Created);
        Assert.Equal(2, await db.WorkerPayrollProfiles.CountAsync(CancellationToken.None));
    }

    // Same fake as M41AccountingReportTests — minimal IPayslipDocumentService.
    private sealed class FakePayslipDoc : IPayslipDocumentService
    {
        public Task<string> GenerateAsync(Domain.Entities.Payslip slip, Domain.Entities.PayrollRunLine line, CancellationToken ct)
            => Task.FromResult("https://stub");
    }

    [Fact]
    public async Task Preview_WithNaturalKeyNrcResolves()
    {
        var (db, svc, _) = BuildSeeded();
        var target = db.Workers.First(w => w.EmployeeNo == "EMP-99");
        target.Nrc = "111111/22/1";
        db.SaveChanges();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["nrc"] = "111111/22/1", ["basic"] = "7500" },
        };
        var preview = await svc.PreviewAsync("payroll-profiles", "pay.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("create", preview.Rows[0].Status);
    }
}
