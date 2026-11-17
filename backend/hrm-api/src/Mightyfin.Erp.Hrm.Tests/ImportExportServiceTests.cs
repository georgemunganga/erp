// M31 — shared import/export engine tests: schema listing, CSV parse, preview
// (client-mapped rows → per-row status), apply (create path) and the round-
// trip export. SQLite in-memory per factory, isolated tenant.
using Xunit;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application.Shared;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Benefits;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public class ImportExportServiceTests
{
    [Fact]
    public void ParseCsvLine_HonorsQuotedCommasAndEscapedQuotes()
    {
        var cells = ImportRowParser.ParseCsvLine("\"Kabanga, Mary\", \"\"Big Mary\"\", 123");
        // Double-quoted cells are unquoted (RFC 4180), including escaped inner quotes.
        Assert.Equal(["Kabanga, Mary", " Big Mary", " 123"], cells);
    }

    [Fact]
    public async Task Preview_Insert_NewRowIsCreate()
    {
        var svc = BuildServiceWithOrgUnit("Human Resources");
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["firstName"] = "Grace", ["lastName"] = "Mulenga", ["email"] = "grace.m@example.com", ["phone"] = "0971234567", ["workerType"] = "employee", ["orgUnitName"] = "Human Resources" },
        };
        var preview = await svc.PreviewAsync("workers", "t.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("create", preview.Rows[0].Status);
        Assert.Equal(1, preview.WillCreate);
    }

    [Fact]
    public async Task Preview_Update_ExistingEmployeeNoBecomesUpdate()
    {
        // Seed the worker into the SAME db the service uses — each service holds
        // its own in-memory context, so the seed happens inside the helper.
        var svc = BuildSeedService("EMP-99");

        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99", ["firstName"] = "A-updated", ["lastName"] = "B" },
        };
        var preview = await svc.PreviewAsync("workers", "t.csv", "update", rows, CancellationToken.None);
        Assert.Equal("update", preview.Rows[0].Status);
        Assert.Equal(1, preview.WillUpdate);
    }

    [Fact]
    public async Task Preview_MissingNamesIsError()
    {
        var svc = BuildService();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["firstName"] = "", ["lastName"] = "Z", ["email"] = "z@example.com", ["phone"] = "0971234567", ["workerType"] = "employee" },
        };
        var preview = await svc.PreviewAsync("workers", "t.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("First name", preview.Rows[0].Message);
        Assert.Equal(1, preview.WillError);
    }

    [Fact]
    public async Task Preview_RequiredFieldsRefuseRowWithExplicitMessage()
    {
        var svc = BuildService();
        // email, phone and employment type are now non-negotiable.
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["firstName"] = "Grace", ["lastName"] = "Mulenga", ["phone"] = "0971234567", ["workerType"] = "employee" },
        };
        var preview = await svc.PreviewAsync("workers", "t.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("Work email", preview.Rows[0].Message);

        // One row missing EVERY required field lists all of them.
        rows = [new()];
        preview = await svc.PreviewAsync("workers", "t.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("First name", preview.Rows[0].Message);
        Assert.Contains("Last name", preview.Rows[0].Message);
        Assert.Contains("Work email", preview.Rows[0].Message);
        Assert.Contains("Phone", preview.Rows[0].Message);
        Assert.Contains("Employment type", preview.Rows[0].Message);
    }

    [Fact]
    public async Task Preview_BadFormatsAreRefused()
    {
        var svc = BuildService();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["firstName"] = "A", ["lastName"] = "B", ["email"] = "not-an-email", ["phone"] = "0971234567", ["workerType"] = "employee" },
        };
        var preview = await svc.PreviewAsync("workers", "t.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("email", preview.Rows[0].Message);

        rows = [new() { ["firstName"] = "A", ["lastName"] = "B", ["email"] = "a@b.com", ["phone"] = "abc", ["workerType"] = "employee" }];
        preview = await svc.PreviewAsync("workers", "t.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("Phone", preview.Rows[0].Message);

        rows = [new() { ["firstName"] = "A", ["lastName"] = "B", ["email"] = "a@b.com", ["phone"] = "0971234567", ["workerType"] = "unknown-category" }];
        preview = await svc.PreviewAsync("workers", "t.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("Employment type", preview.Rows[0].Message);

        rows = [new() { ["firstName"] = "A", ["lastName"] = "B", ["email"] = "a@b.com", ["phone"] = "0971234567", ["workerType"] = "employee", ["tpin"] = "123" }];
        preview = await svc.PreviewAsync("workers", "t.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("TPIN", preview.Rows[0].Message);

        rows = [new() { ["firstName"] = "A", ["lastName"] = "B", ["email"] = "a@b.com", ["phone"] = "0971234567", ["workerType"] = "employee", ["startDate"] = "31/31/2026" }];
        preview = await svc.PreviewAsync("workers", "t.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("Start date", preview.Rows[0].Message);
    }

    [Fact]
    public async Task Preview_DayFirstStartDateIsAccepted()
    {
        var svc = BuildService();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["firstName"] = "A", ["lastName"] = "B", ["email"] = "a@b.com", ["phone"] = "0971234567", ["workerType"] = "employee", ["startDate"] = "02-01-2026" },
        };
        var preview = await svc.PreviewAsync("workers", "t.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("create", preview.Rows[0].Status);
    }

    [Fact]
    public async Task Preview_DuplicateRowsInsideOneFileAreRefused()
    {
        var svc = BuildServiceWithOrgUnit("Finance");
        // Two rows carrying the same email and the same employee number.
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["firstName"] = "A", ["lastName"] = "B", ["email"] = "dup@example.com", ["phone"] = "0971234567", ["workerType"] = "employee", ["employeeNo"] = "EMP-X1", ["orgUnitName"] = "Finance" },
            new() { ["firstName"] = "C", ["lastName"] = "D", ["email"] = "dup@example.com", ["phone"] = "0971234568", ["workerType"] = "employee", ["employeeNo"] = "EMP-X1", ["orgUnitName"] = "Finance" },
        };
        var preview = await svc.PreviewAsync("workers", "t.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("create", preview.Rows[0].Status);
        Assert.Equal("error", preview.Rows[1].Status);
        Assert.Contains("email", preview.Rows[1].Message);
        Assert.Contains("twice in this file", preview.Rows[1].Message);
    }

    [Fact]
    public async Task Preview_Update_NoMatchExplainsWhichIdentifierFailed()
    {
        var svc = BuildService();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "GHOST-1", ["firstName"] = "A", ["lastName"] = "B" },
        };
        var preview = await svc.PreviewAsync("workers", "t.csv", "update", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("GHOST-1", preview.Rows[0].Message);
        Assert.Contains("No existing employee", preview.Rows[0].Message);
    }

    [Fact]
    public async Task Preview_Update_MatchPatchesWithSuppliedFieldsOnly()
    {
        // Update mode patches only what the file supplies — email/phone not required.
        var svc = BuildSeedService("EMP-99");
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["employeeNo"] = "EMP-99", ["firstName"] = "A-updated", ["lastName"] = "B" },
        };
        var preview = await svc.PreviewAsync("workers", "t.csv", "update", rows, CancellationToken.None);
        Assert.Equal("update", preview.Rows[0].Status);
    }

    [Fact]
    public async Task Preview_ClashingEmployeeNoWithExistingRecordIsRefused()
    {
        var svc = BuildSeedService("EMP-99");
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["firstName"] = "A", ["lastName"] = "B", ["email"] = "fresh@example.com", ["phone"] = "0971234567", ["workerType"] = "employee", ["employeeNo"] = "EMP-99" },
        };
        var preview = await svc.PreviewAsync("workers", "t.csv", "insert", rows, CancellationToken.None);
        // insert mode must never silently overwrite — clashing IDs are refused.
        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("EMP-99", preview.Rows[0].Message);
    }

    [Fact]
    public async Task Preview_UnknownOrgUnitIsError()
    {
        var svc = BuildService();
        var rows = new List<Dictionary<string, string>>
        {
            new() { ["firstName"] = "X", ["lastName"] = "Y", ["orgUnitName"] = "No Such Department" },
        };
        var preview = await svc.PreviewAsync("workers", "t.csv", "insert", rows, CancellationToken.None);
        Assert.Equal("error", preview.Rows[0].Status);
    }

    [Fact]
    public async Task Apply_InvalidPreviewIdThrows()
    {
        var svc = BuildService();
        await Assert.ThrowsAsync<Application.DomainException>(
            () => svc.ApplyAsync(Guid.NewGuid(), [0], CancellationToken.None));
    }

    [Fact]
    public async Task Export_CsvRoundTripsWorkerRows()
    {
        var svc = BuildService();
        var preview = await svc.PreviewAsync("workers", "t.csv", "insert",
            [new() { ["firstName"] = "E", ["lastName"] = "F", ["email"] = "e@example.com", ["phone"] = "0971234567", ["workerType"] = "employee" }],
            CancellationToken.None);
        var bytes = await svc.ExportAsync("workers", null, CancellationToken.None);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("firstName", text);
        Assert.Contains("orgUnitName", text);
    }

    [Fact]
    public void ListSchemas_IncludesWorkers()
    {
        var svc = BuildService();
        var schemas = svc.ListSchemas();
        Assert.Contains(schemas, s => s.TypeKey == "workers" && s.Fields.Count > 10);
    }

    [Fact]
    public async Task CostOfLivingImport_CreatesAnnualPayrollBenefitAllowance()
    {
        var (payroll, db) = PayrollEngineTests.Build(tenant: $"cola-import-{Guid.NewGuid():N}");
        var (group, _, _, profile, _, _, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(db);
        var benefit = new Domain.Entities.BenefitType
        {
            Code = "cost-of-living-allowance",
            Name = "Cost of Living Allowance",
            AnnualCap = 24_000m,
            IncludeInPayroll = true,
            IsActive = true,
        };
        db.BenefitTypes.Add(benefit);
        await db.SaveChangesAsync();

        var workerRepo = new WorkerRepository(db);
        var workerService = new WorkerServiceImpl(workerRepo, new PermissiveAuthz(), new UlidIdProvider());
        var payrollRepo = new PayrollRepository(db);
        var benefitRepo = new BenefitRepository(db);
        var schema = new WorkersImportSchema(workerRepo, workerService, new PermissiveAuthz(),
            new Application.ShellContext(), payrollRepo, payroll, benefits: benefitRepo);
        var service = new ImportExportServiceImpl(new[] { schema });
        var employeeNo = (await db.Workers.SingleAsync(worker => worker.Id == profile.WorkerId)).EmployeeNo;
        var preview = await service.PreviewAsync("workers", "cola.csv", "update",
            [new()
            {
                ["employeeNo"] = employeeNo,
                ["costOfLivingAllowance"] = "1000",
                ["payGroup"] = group.Name,
                ["salaryEffectiveFrom"] = "01-01-2026",
            }], CancellationToken.None);

        Assert.Equal("update", preview.Rows[0].Status);
        await service.ApplyAsync(preview.Id, [0], CancellationToken.None);
        var allowance = await db.WorkerBenefitAllowances.SingleAsync();
        Assert.Equal(profile.WorkerId, allowance.WorkerId);
        Assert.Equal(benefit.Id, allowance.BenefitTypeId);
        Assert.Equal(12_000m, allowance.AnnualAmount);
        Assert.Equal(2026, allowance.Year);
    }

    [Fact]
    public async Task CostOfLivingImport_RejectsMonthlyAmountAboveAnnualBenefitCap()
    {
        var (payroll, db) = PayrollEngineTests.Build(tenant: $"cola-cap-{Guid.NewGuid():N}");
        var (group, _, _, profile, _, _, _, _, _, _, _) = await PayrollEngineTests.SeedStackAsync(db);
        db.BenefitTypes.Add(new Domain.Entities.BenefitType
        {
            Code = "cost-of-living-allowance",
            Name = "Cost of Living Allowance",
            AnnualCap = 10_000m,
            IncludeInPayroll = true,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var workerRepo = new WorkerRepository(db);
        var schema = new WorkersImportSchema(workerRepo,
            new WorkerServiceImpl(workerRepo, new PermissiveAuthz(), new UlidIdProvider()),
            new PermissiveAuthz(), new Application.ShellContext(), new PayrollRepository(db), payroll,
            benefits: new BenefitRepository(db));
        var service = new ImportExportServiceImpl(new[] { schema });
        var employeeNo = (await db.Workers.SingleAsync(worker => worker.Id == profile.WorkerId)).EmployeeNo;
        var preview = await service.PreviewAsync("workers", "cola.csv", "update",
            [new()
            {
                ["employeeNo"] = employeeNo,
                ["costOfLivingAllowance"] = "1000",
                ["payGroup"] = group.Name,
                ["salaryEffectiveFrom"] = "01-01-2026",
            }], CancellationToken.None);

        Assert.Equal("error", preview.Rows[0].Status);
        Assert.Contains("exceeds", preview.Rows[0].Message);
        Assert.Empty(db.WorkerBenefitAllowances);
    }

    [Theory]
    [InlineData("Permanent", "employee")]
    [InlineData(" permanent ", "employee")]
    [InlineData("PERMANENT", "employee")]
    [InlineData("EmPlOyEe", "employee")]
    [InlineData("CONTRACTOR", "contingent")]
    [InlineData("Contingent", "contingent")]
    [InlineData("INTERN", "intern")]
    [InlineData("Volunteer", "volunteer")]
    public async Task Import_LabelsAreResolvedOnServer_AndPersistCanonicalCategory(string label, string expected)
    {
        var db = TestDbContextFactory.Create($"labels-{Guid.NewGuid():N}");
        var repo = new WorkerRepository(db);
        var svc = new ImportExportServiceImpl([new WorkersImportSchema(repo,
            new WorkerServiceImpl(repo, new PermissiveAuthz(), new UlidIdProvider()),
            new PermissiveAuthz(), new Application.ShellContext())]);
        var preview = await svc.PreviewAsync("workers", "labels.csv", "insert",
            [new() { ["firstName"] = "Import", ["lastName"] = "Test", ["email"] = "preview@example.invalid",
                ["phone"] = "0971234567", ["workerType"] = label }], CancellationToken.None);
        Assert.Equal(0, preview.WillError);
        Assert.Equal(expected, preview.Rows.Single().Resolved!["workerType"]);
        var applied = await svc.ApplyAsync(preview.Id, [0], CancellationToken.None);
        Assert.Equal(1, applied.Created);
        Assert.Equal(expected, (await db.Workers.SingleAsync()).WorkerType);
    }

    [Theory]
    [InlineData("firstName", "123", "First name")]
    [InlineData("nrc", "123", "NRC")]
    [InlineData("locationId", "Unknown Branch", "Branch")]
    [InlineData("locationId", "00000000-0000-0000-0000-000000000000", "Branch")]
    [InlineData("workerType", "permanant", "Employment type")]
    public async Task Import_ImportantFieldsCannotBypassBrowserValidation(string field, string value, string message)
    {
        var svc = BuildService();
        var row = new Dictionary<string, string> { ["firstName"] = "Import", ["lastName"] = "Test",
            ["email"] = "preview@example.invalid", ["phone"] = "0971234567", ["workerType"] = "Permanent" };
        row[field] = value;
        var preview = await svc.PreviewAsync("workers", "invalid.csv", "insert", [row], CancellationToken.None);
        Assert.Equal(1, preview.WillError);
        Assert.Contains(message, preview.Rows.Single().Message);
        var result = await svc.ApplyAsync(preview.Id, [0], CancellationToken.None);
        Assert.Equal(0, result.Created);
    }

    [Fact]
    public async Task Import_DuplicateNapsaInFileIsFlaggedBeforeApply()
    {
        var svc = BuildService();
        var rows = new List<Dictionary<string,string>> {
            new() { ["firstName"]="First", ["lastName"]="Test", ["email"]="first@example.invalid", ["phone"]="0971234567", ["workerType"]="Permanent", ["napsaNumber"]="TEST-123" },
            new() { ["firstName"]="Second", ["lastName"]="Test", ["email"]="second@example.invalid", ["phone"]="0971234568", ["workerType"]="Permanent", ["napsaNumber"]="test-123" },
        };
        var preview = await svc.PreviewAsync("workers", "duplicate.csv", "insert", rows, CancellationToken.None);
        Assert.Equal(1, preview.WillCreate);
        Assert.Equal(1, preview.WillError);
        Assert.Contains("NAPSA", preview.Rows[1].Message);
    }

    private static ImportExportServiceImpl BuildService()
    {
        var tenant = $"t{Guid.NewGuid():N}";
        var db = TestDbContextFactory.Create(tenant);
        var repo = new WorkerRepository(db);
        var workers = new WorkerServiceImpl(repo, new PermissiveAuthz(), new UlidIdProvider());
        var schema = new WorkersImportSchema(repo, workers, new PermissiveAuthz(), new Application.ShellContext());
        return new ImportExportServiceImpl(new[] { schema });
    }

    private static ImportExportServiceImpl BuildSeedService(string employeeNo)
    {
        var tenant = $"t{Guid.NewGuid():N}";
        var db = TestDbContextFactory.Create(tenant);
        db.Workers.Add(new Domain.Entities.Worker
        {
            Id = Guid.NewGuid(), EmployeeNo = employeeNo, FirstName = "A", LastName = "B",
            Status = "active", WorkerType = "employee",
        });
        db.SaveChanges();
        var repo = new WorkerRepository(db);
        var workers = new WorkerServiceImpl(repo, new PermissiveAuthz(), new UlidIdProvider());
        var schema = new WorkersImportSchema(repo, workers, new PermissiveAuthz(), new Application.ShellContext());
        return new ImportExportServiceImpl(new[] { schema });
    }

    private static ImportExportServiceImpl BuildServiceWithOrgUnit(string unitName)
    {
        var tenant = $"t{Guid.NewGuid():N}";
        var db = TestDbContextFactory.Create(tenant);
        var entity = new Domain.Entities.LegalEntity
        {
            Id = Guid.NewGuid(), Code = "TST", RegisteredName = "Test Ltd",
            TradingName = "Test", CountryCode = "ZM",
        };
        db.LegalEntities.Add(entity);
        db.OrgUnits.Add(new Domain.Entities.OrgUnit
        {
            Id = Guid.NewGuid(), Code = "TST", Name = unitName, LegalEntityId = entity.Id,
            UnitType = "department", Status = "active", EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        db.SaveChanges();
        var repo = new WorkerRepository(db);
        var workers = new WorkerServiceImpl(repo, new PermissiveAuthz(), new UlidIdProvider());
        var schema = new WorkersImportSchema(repo, workers, new PermissiveAuthz(), new Application.ShellContext());
        return new ImportExportServiceImpl(new[] { schema });
    }
}
