using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M3: time module tests (attendance punches, roster, leave balances,
/// correction and leave decisions) over EF InMemory with a fixed tenant.</summary>
public class TimeServiceTests
{
    [Fact]
    public async Task Empty_accrual_does_not_consume_the_month_before_the_first_employee_is_added()
    {
        var (service, ctx, worker, _, _) = Build();
        worker.Status = "pre-hire"; await ctx.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            service.RunScheduledLeaveAccrualAsync(new LeaveAccrualRunRequest("2026-08"), default));
        Assert.Equal("accrual-no-workers", error.Code);
        Assert.Empty(await ctx.LeaveAccrualRuns.ToListAsync());
        worker.Status = "active"; await ctx.SaveChangesAsync();
        var run = await service.RunScheduledLeaveAccrualAsync(new LeaveAccrualRunRequest("2026-08"), default);
        Assert.Equal(1, run.WorkerCount);
        Assert.Equal("system:monthly-leave-accrual", run.RunBySubjectId);
    }

    private sealed class TimesheetAuthz : IAuthzService
    {
        public string CurrentSubjectId => "timesheet-clerk";
        public void RequireAnyRole(params string[] roles)
        {
            if (!IsRole(roles)) throw new DomainException("forbidden", "Access denied.");
        }
        public bool IsRole(params string[] roles) => roles.Contains("timesheet_operator");
        public bool CanAccessSensitive(string category) => false;
    }

    [Fact]
    public async Task TimesheetOperatorCanImportAndReadOtherEmployeeAttendance()
    {
        var (service, ctx, worker, _, _) = Build(authz: new TimesheetAuthz());
        using (ctx)
        {
            var result = await service.ImportAttendanceAsync(new AttendanceImportRequest("hours.csv",
                [new(worker.EmployeeNo, "2026-09-21", "08:00", "17:00")]), "timesheet-clerk", default);
            Assert.Equal(1, result.ImportedCount);
            Assert.Equal(0, result.RejectedCount);
            var rows = await service.ListAttendanceForScopeAsync("2026-09-21", "2026-09-21", default);
            Assert.Equal(worker.Id, Assert.Single(rows).WorkerId);
            var error = await Assert.ThrowsAsync<DomainException>(() => service.DecideOvertimeAsync(
                rows[0].Id, new OvertimeDecisionRequest("approve"), "timesheet-clerk", default));
            Assert.Equal("forbidden", error.Code);
            await Assert.ThrowsAsync<DomainException>(() => service.ListLeaveAsync(null, null, default));
            await Assert.ThrowsAsync<DomainException>(() => service.GetOperationsHistoryAsync(default));
            await Assert.ThrowsAsync<DomainException>(() => service.ImportOvertimeAsync(
                new OvertimeImportRequest("ot.csv", [], true), "timesheet-clerk", default));
        }
    }

    [Fact]
    public async Task AttendanceImportCannotOverwritePayrollLinkedRecord()
    {
        var (service, ctx, worker, _, _) = Build(authz: new TimesheetAuthz());
        using (ctx)
        {
            var record = new AttendanceRecord { WorkerId = worker.Id, WorkDate = new DateOnly(2026, 9, 21),
                ClockIn = new TimeOnly(8, 0), ClockOut = new TimeOnly(17, 0), OvertimePayrollRunId = Guid.NewGuid() };
            ctx.AttendanceRecords.Add(record);
            await ctx.SaveChangesAsync();
            var result = await service.ImportAttendanceAsync(new AttendanceImportRequest("hours.csv",
                [new(worker.EmployeeNo, "2026-09-21", "06:00", "22:00")]), "timesheet-clerk", default);
            Assert.Equal(1, result.RejectedCount);
            Assert.Equal(new TimeOnly(8, 0), record.ClockIn);
        }
    }

    [Fact]
    public async Task AttendanceImportHonoursAssignedBranch()
    {
        var authz = new TimesheetAuthz();
        var (_, ctx, worker, wf, _) = Build(authz: authz);
        using (ctx)
        {
            var scope = new ShellContext { LocationId = Guid.NewGuid() };
            var service = new TimeServiceImpl(new TimeRepository(ctx), authz, wf, new WorkerRepository(ctx), scope);
            var result = await service.ImportAttendanceAsync(new AttendanceImportRequest("hours.csv",
                [new(worker.EmployeeNo, "2026-09-21", "08:00", "17:00")]), "timesheet-clerk", default);
            Assert.Equal(1, result.RejectedCount);
            Assert.Empty(ctx.AttendanceRecords);
        }
    }

    private sealed class EmployeeAuthz(string subject) : IAuthzService
    {
        public string CurrentSubjectId => subject;
        public void RequireAnyRole(params string[] roles)
        {
            if (!roles.Contains("employee")) throw new DomainException("forbidden", "Employee access denied.");
        }
        public bool IsRole(params string[] roles) => roles.Contains("employee");
        public bool CanAccessSensitive(string category) => false;
    }

    /// <summary>No-op double for the workflow effect applier (leave decisions are
    /// applied directly by the time service; the workflow engine applies its own
    /// effects through this interface but tests run the direct service path).</summary>
    private sealed class NoOpLeaveEffectApplier : ILeaveEffectApplier
    {
        public Task ApplyAsync(WorkflowRequest request, string decisionAction, CancellationToken ct) => Task.CompletedTask;
    }

    private static (TimeServiceImpl service, HrmDbContext ctx, Worker worker, WorkflowServiceImpl wf, WorkflowRepository wfRepo) Build(
        string tenant = "test-tenant", IAuthzService? authz = null)
    {
        authz ??= new PermissiveAuthz();
        var ctx = TestDbContextFactory.Create(tenant);
        var wfRepo = new WorkflowRepository(ctx);
        var wf = new WorkflowServiceImpl(wfRepo, authz, new NoOpLeaveEffectApplier());
        var repo = new TimeRepository(ctx);
        var workerRepo = new WorkerRepository(ctx);
        var service = new TimeServiceImpl(repo, authz, wf, workerRepo);
        var worker = new Worker
        {
            EmployeeNo = "EMP-TM-001", FirstName = "Time", LastName = "Worker",
            WorkerType = "employee", Status = "active", Nationality = "ZM",
            TenantId = "test-tenant", SubjectId = "subject-001",
        };
        ctx.Workers.Add(worker);
        // seed calendar: weekend days sat/sun, default
        var calendar = new WorkCalendar
        {
            Name = "Test Calendar", CountryCode = "ZM", StandardWeeklyHours = 45,
            WeekendDays = "sat,sun", IsDefault = true,
            TenantId = "test-tenant",
        };
        ctx.WorkCalendars.Add(calendar);
        ctx.LeaveTypes.Add(new LeaveType
        {
            Code = "annual", Name = "Annual Leave", Category = "annual",
            DefaultDaysPerYear = 12, IsActive = true, AllowNegative = false,
            AutoAccrueMonthly = true, AccrualStartDate = new DateOnly(2026, 1, 1),
            MaxConsecutiveDays = 999, RequiresEvidence = false, MinNoticeDays = 0,
            AllowsPartialDays = false, CarryForwardDays = 0, CarryForwardExpiryMonths = 0,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            TenantId = "test-tenant",
        });
        ctx.SaveChanges();
        return (service, ctx, worker, wf, wfRepo);
    }

    private static (TimeServiceImpl Service, HrmDbContext Db, Worker Worker) ManualBuild(ShellContext? scope = null, IAuthzService? authz = null)
    {
        var (_, db, worker, wf, _) = Build();
        return (new TimeServiceImpl(new TimeRepository(db), authz ?? new PermissiveAuthz(), wf,
            new WorkerRepository(db), scope, new PayrollRepository(db), unitOfWork: new EfUnitOfWork(db)), db, worker);
    }

    [Fact]
    public async Task TimesheetOperatorCanRecordOtherEmployeesButCannotManageShifts()
    {
        var (service, db, worker) = ManualBuild(authz: new TimesheetAuthz());
        using (db)
        {
            var other = new Worker { EmployeeNo = "FRONT-DESK-OTHER", FirstName = "Other", LastName = "Employee", Status = "active" };
            db.Workers.Add(other);
            await db.SaveChangesAsync();
            var rows = await service.CreateManualAttendanceAsync(new("2026-08-21",
                [new(worker.Id, "08:00", "17:00"), new(other.Id, "08:00", "18:00")]), "timesheet-clerk", default);
            Assert.Equal(2, rows.Count);
            Assert.All(await db.AuditEntries.Where(a => a.Action == "manual-create").ToListAsync(), a => Assert.Equal("timesheet-clerk", a.ActorSubjectId));
            await Assert.ThrowsAsync<DomainException>(() => service.CloseShiftAsync(Guid.NewGuid(), default));
        }
    }

    [Fact]
    public async Task ManualAttendance_UsesShiftRulesAndAuditAndRejectsDuplicate()
    {
        var (service, db, worker) = ManualBuild();
        var shift = new ShiftDefinition { Code = "DAY", Name = "Day", StartTime = new(8,0), EndTime = new(17,0),
            UnpaidBreakMinutes = 60, StandardHours = 8, DailyOvertimeThresholdHours = 8, WeekdayOvertimeMultiplier = 1.5m };
        db.ShiftDefinitions.Add(shift);
        db.WorkerShiftAssignments.Add(new WorkerShiftAssignment { WorkerId = worker.Id, ShiftId = shift.Id, EffectiveFrom = new(2026,8,1) });
        await db.SaveChangesAsync();
        var request = new ManualAttendanceRequest("2026-08-20", [new(worker.Id, "08:00", "19:00")]);
        var result = await service.CreateManualAttendanceAsync(request, "hr-operator", default);
        Assert.Equal(10m, result.Single().TotalHours);
        Assert.Equal(2m, result.Single().OvertimeHours);
        Assert.Equal("pending", result.Single().OvertimeStatus);
        Assert.Equal("manual-entry", result.Single().Source);
        Assert.Contains(await db.AuditEntries.ToListAsync(), a => a.Action == "manual-create" && a.ActorSubjectId == "hr-operator");
        await Assert.ThrowsAsync<DomainException>(() => service.CreateManualAttendanceAsync(request, "hr-operator", default));
        Assert.Single(await db.AttendanceRecords.ToListAsync());
    }

    [Fact]
    public async Task ManualAttendance_InvalidBulkRowSavesNothingAndOvernightWorks()
    {
        var (service, db, worker) = ManualBuild();
        var other = new Worker { EmployeeNo = "OTHER", FirstName = "Other", LastName = "Employee", Status = "active" };
        db.Workers.Add(other); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => service.CreateManualAttendanceAsync(new("2026-08-21",
            [new(worker.Id, "08:00", "17:00"), new(other.Id, "08:00", "08:00")]), "hr", default));
        Assert.Empty(await db.AttendanceRecords.ToListAsync());
        var rows = await service.CreateManualAttendanceAsync(new("2026-08-21",
            [new(worker.Id, "22:00", "06:00"), new(other.Id, "08:00", "17:00")]), "hr", default);
        Assert.Equal(2, rows.Count);
        Assert.Equal(8m, rows.Single(r => r.WorkerId == worker.Id).TotalHours);
        Assert.Equal(2, await db.AttendanceRecords.CountAsync());
    }

    [Fact]
    public async Task ManualAttendance_RejectsOtherBranchAndEmployeeRole()
    {
        var (service, _, worker) = ManualBuild(new ShellContext { LocationId = Guid.NewGuid() });
        await Assert.ThrowsAsync<DomainException>(() => service.CreateManualAttendanceAsync(new("2026-08-21", [new(worker.Id, "08:00", "17:00")]), "hr", default));
        var (employeeService, _, ownWorker) = ManualBuild(authz: new EmployeeAuthz("subject-001"));
        await Assert.ThrowsAsync<DomainException>(() => employeeService.CreateManualAttendanceAsync(new("2026-08-21", [new(ownWorker.Id, "08:00", "17:00")]), "employee", default));
    }

    [Fact]
    public async Task ManualAttendance_RejectsLockedPayrollAndFutureDates()
    {
        var (service, db, worker) = ManualBuild();
        var stack = await PayrollEngineTests.SeedStackAsync(db);
        db.PayrollRuns.Add(new PayrollRun { PayPeriodId = stack.P1.Id, PayGroupId = stack.Group.Id, Status = "released" });
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<DomainException>(() => service.CreateManualAttendanceAsync(new("2026-06-15", [new(stack.Profile.WorkerId, "08:00", "17:00")]), "hr", default));
        Assert.Contains("locked", error.Message);
        await Assert.ThrowsAsync<DomainException>(() => service.CreateManualAttendanceAsync(new(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1).ToString("yyyy-MM-dd"), [new(worker.Id, "08:00", "17:00")]), "hr", default));
        Assert.Empty(await db.AttendanceRecords.ToListAsync());
    }

    [Fact]
    public async Task EmployeeCannotClockOrCorrectAnotherWorker()
    {
        var (service, ctx, worker, _, _) = Build(authz: new EmployeeAuthz("subject-001"));
        var other = new Worker
        {
            EmployeeNo = "EMP-TM-OTHER", FirstName = "Other", LastName = "Worker",
            WorkerType = "employee", Status = "active", Nationality = "ZM",
            TenantId = "test-tenant", SubjectId = "subject-002",
        };
        ctx.Workers.Add(other);
        ctx.SaveChanges();

        var clock = await Assert.ThrowsAsync<DomainException>(() =>
            service.ClockInAsync(other.Id, CancellationToken.None));
        Assert.Equal("worker-access-denied", clock.Code);

        var correction = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateCorrectionAsync(new AttendanceCorrectionCreate(
                other.Id, DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
                "missed-punch", "08:00", "17:00", null, "Reader failed"), CancellationToken.None));
        Assert.Equal("worker-access-denied", correction.Code);

        var own = await service.ClockInAsync(worker.Id, CancellationToken.None);
        Assert.Equal("in", own.State);
    }

    [Fact]
    public async Task ClockIn_CreatesRecordWithStateIn()
    {
        var (service, ctx, worker, _, _) = Build();
        var result = await service.ClockInAsync(worker.Id, CancellationToken.None);
        var record = await ctx.AttendanceRecords.FirstAsync(a => a.WorkerId == worker.Id);
        Assert.Equal("in", result.State);
        Assert.NotNull(record.ClockIn);
        Assert.Null(record.ClockOut);
        Assert.Equal("self-service", record.Source);
    }

    [Fact]
    public async Task ClockOut_MarksRecordDoneWithPositiveHours()
    {
        var (service, ctx, worker, _, _) = Build();
        await service.ClockInAsync(worker.Id, CancellationToken.None);
        // simulate a gap by faking the clock-in time back 2 hours
        var rec = await ctx.AttendanceRecords.FirstAsync(a => a.WorkerId == worker.Id);
        rec.ClockIn = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(-2));
        ctx.SaveChanges();
        var result = await service.ClockOutAsync(worker.Id, CancellationToken.None);
        Assert.Equal("done", result.State);
        Assert.True(result.TotalHours > 1.9m && result.TotalHours < 2.1m, $"expected ~2h, got {result.TotalHours}");
        var record = await ctx.AttendanceRecords.FirstAsync(a => a.WorkerId == worker.Id);
        Assert.NotNull(record.ClockOut);
        Assert.Equal("present", record.DerivedStatus);
    }

    [Fact]
    public async Task GetRoster_ClassifiesWorkingDays_Weekends_AndMissingPunch()
    {
        var (service, ctx, worker, _, _) = Build();
        // punch only on Monday (today may be any day — punch today explicitly)
        await service.ClockInAsync(worker.Id, CancellationToken.None);
        await service.ClockOutAsync(worker.Id, CancellationToken.None);

        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var to = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var roster = await service.GetRosterAsync(worker.Id,
            from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"), CancellationToken.None);
        Assert.Equal(11, roster.Count);

        var punched = roster.Single(r => r.ClockIn is not null);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.Parse(punched.Date));
        Assert.Equal("present", punched.Status);

        var weekends = roster.Where(r => !r.IsWorkingDay).ToList();
        Assert.True(weekends.Count >= 2, $"expected at least 2 weekend days, got {weekends.Count}");

        // past working days (excluding today, which was punched) must be flagged missing-punch
        var missing = roster.Where(r => r.Status == "missing-punch").ToList();
        Assert.True(missing.Count > 0, "working days without punches should be flagged missing-punch");
    }

    [Fact]
    public async Task DecideCorrection_Approve_AppliesProposedValues()
    {
        var (service, ctx, worker, _, _) = Build();
        var repo = new TimeRepository(ctx);
        var correction = new AttendanceCorrection
        {
            WorkerId = worker.Id, WorkDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IssueType = "missed-punch", Reason = "forgot badge", Status = "submitted",
            ProposedClockIn = new TimeOnly(8, 0), ProposedClockOut = new TimeOnly(16, 0),
        };
        await repo.CreateCorrectionAsync(correction, CancellationToken.None);

        var decided = await service.DecideCorrectionAsync(correction.Id,
            new TimeDecisionRequest("approve", null), CancellationToken.None);
        Assert.Equal("approved", decided.Status);

        var record = await repo.GetAttendanceAsync(worker.Id, correction.WorkDate, CancellationToken.None);
        Assert.NotNull(record);
        Assert.Equal("corrected", record!.Source);
        Assert.Equal(new TimeOnly(8, 0), record.ClockIn);
        Assert.Equal(8m, record.TotalHours);
    }

    [Fact]
    public async Task CreateLeave_InsufficientBalance_Throws()
    {
        var (service, ctx, worker, _, _) = Build();
        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateLeaveAsync(
            new LeaveRequestCreate(worker.Id, "annual",
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd"),
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)).ToString("yyyy-MM-dd")),
            CancellationToken.None));
        Assert.Equal("leave-insufficient-balance", ex.Code);
    }

    [Fact]
    public async Task CreateLeave_ReservesBalance()
    {
        var (service, ctx, worker, _, _) = Build();
        var repo = new TimeRepository(ctx);
        // accrue 12 days
        ctx.LeaveBalanceLedgers.Add(new LeaveBalanceLedger
        {
            WorkerId = worker.Id, LeaveTypeCode = "annual", Days = 12m,
            Reason = "accrual", ReferenceType = "", ForDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TenantId = "test-tenant",
        });
        ctx.SaveChanges();

        var created = await service.CreateLeaveAsync(new LeaveRequestCreate(
            worker.Id, "annual",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd"),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd")),
            CancellationToken.None);
        Assert.Equal("submitted", created.Status);
        Assert.False(created.BalanceReserved);

        var ledger = await repo.GetLedgerAsync(worker.Id, CancellationToken.None);
        var reservation = ledger.Single(l => l.Reason == "request");
        Assert.Equal(-1m, reservation.Days);
        Assert.NotNull(reservation.ReferenceId);

        var balances = await service.GetBalancesAsync(worker.Id, CancellationToken.None);
        var annual = balances.Single(b => b.LeaveTypeCode == "annual");
        Assert.Equal(12m, annual.Accrued);
        Assert.Equal(1m, annual.Reserved);
        Assert.Equal(11m, annual.Available);
    }

    [Fact]
    public async Task DecideLeave_Approve_ConvertsReservationToTaken()
    {
        var (service, ctx, worker, _, _) = Build();
        var repo = new TimeRepository(ctx);
        ctx.LeaveBalanceLedgers.Add(new LeaveBalanceLedger
        {
            WorkerId = worker.Id, LeaveTypeCode = "annual", Days = 12m,
            Reason = "accrual", ReferenceType = "", ForDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TenantId = "test-tenant",
        });
        ctx.SaveChanges();

        var created = await service.CreateLeaveAsync(new LeaveRequestCreate(
            worker.Id, "annual",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd"),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd")),
            CancellationToken.None);

        var decided = await service.DecideLeaveAsync(created.Id,
            new TimeDecisionRequest("approve", null), CancellationToken.None);
        Assert.Equal("approved", decided.Status);
        Assert.True(decided.BalanceReserved);

        var balances = await service.GetBalancesAsync(worker.Id, CancellationToken.None);
        var annual = balances.Single(b => b.LeaveTypeCode == "annual");
        Assert.Equal(1m, annual.Taken);
        Assert.Equal(0m, annual.Reserved);
        Assert.Equal(11m, annual.Available);
    }

    [Fact]
    public async Task DecideLeave_Reject_ReleasesReservation()
    {
        var (service, ctx, worker, _, _) = Build();
        var repo = new TimeRepository(ctx);
        ctx.LeaveBalanceLedgers.Add(new LeaveBalanceLedger
        {
            WorkerId = worker.Id, LeaveTypeCode = "annual", Days = 12m,
            Reason = "accrual", ReferenceType = "", ForDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TenantId = "test-tenant",
        });
        ctx.SaveChanges();

        var created = await service.CreateLeaveAsync(new LeaveRequestCreate(
            worker.Id, "annual",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd"),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd")),
            CancellationToken.None);

        var decided = await service.DecideLeaveAsync(created.Id,
            new TimeDecisionRequest("reject", "not this time"), CancellationToken.None);
        Assert.Equal("rejected", decided.Status);

        var ledger = await repo.GetLedgerAsync(worker.Id, CancellationToken.None);
        Assert.DoesNotContain(ledger, l => l.Days < 0);
        var balances = await service.GetBalancesAsync(worker.Id, CancellationToken.None);
        var annual = balances.Single(b => b.LeaveTypeCode == "annual");
        Assert.Equal(0m, annual.Reserved);
        Assert.Equal(12m, annual.Available);
    }

    // ===================== M17: admin leave approvals inbox =====================

    [Fact]
    public async Task ListLeave_CompanyWide_WhenWorkerIdIsNull()
    {
        var (service, ctx, worker, _, _) = Build();
        await SubmitLeaveAsync(ctx, service, worker);

        // a second, unlinked worker also submits — the admin list must see BOTH
        var other = new Worker
        {
            EmployeeNo = "EMP-TM-002", FirstName = "Second", LastName = "Worker",
            WorkerType = "employee", Status = "active", Nationality = "ZM",
            TenantId = "test-tenant", SubjectId = "subject-002",
        };
        ctx.Workers.Add(other);
        ctx.LeaveBalanceLedgers.Add(new LeaveBalanceLedger
        {
            WorkerId = other.Id, LeaveTypeCode = "annual", Days = 20m,
            Reason = "accrual", ReferenceType = "", ForDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TenantId = "test-tenant",
        });
        ctx.SaveChanges();
        var created2 = await service.CreateLeaveAsync(new LeaveRequestCreate(
            other.Id, "annual",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)).ToString("yyyy-MM-dd"),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)).ToString("yyyy-MM-dd")),
            CancellationToken.None);

        // company-wide list: no workerId filter, no status filter
        var all = await service.ListLeaveAsync(null, null, CancellationToken.None);
        Assert.Equal(2, all.Items.Count);
        Assert.Equal(2, all.TotalCount);
        Assert.Contains(all.Items, r => r.Id == created2.Id);
        Assert.Contains(all.Items, r => r.Id == created2.Id && r.WorkerId == other.Id);

        // employee-owned filter still works on top
        var own = await service.ListLeaveAsync(worker.Id, null, CancellationToken.None);
        Assert.Single(own.Items);

        var submitted = await service.ListLeaveAsync(null, "submitted", CancellationToken.None);
        Assert.Equal(2, submitted.Items.Count);
    }

    // ===================== M16: self-service leave =====================

    private static async Task<LeaveRequestDto> SubmitLeaveAsync(HrmDbContext ctx, TimeServiceImpl service, Worker worker,
        int daysFromNow = 1, int duration = 1)
    {
        // accrue enough balance so the request goes through
        ctx.LeaveBalanceLedgers.Add(new LeaveBalanceLedger
        {
            WorkerId = worker.Id, LeaveTypeCode = "annual", Days = 20m,
            Reason = "accrual", ReferenceType = "", ForDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TenantId = "test-tenant",
        });
        ctx.SaveChanges();
        return await service.CreateLeaveAsync(new LeaveRequestCreate(
            worker.Id, "annual",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysFromNow)).ToString("yyyy-MM-dd"),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysFromNow + duration - 1)).ToString("yyyy-MM-dd")),
            CancellationToken.None);
    }

    [Fact]
    public async Task MyLeave_ScopesToLinkedWorker_WithBalancesAndOwnRequests()
    {
        var (service, ctx, worker, _, _) = Build();
        var created = await SubmitLeaveAsync(ctx, service, worker);

        // the subject bound to this worker
        var inbox = await service.MyLeaveAsync("subject-001", CancellationToken.None);
        Assert.True(inbox.Linked);
        Assert.Equal("EMP-TM-001", inbox.EmployeeNo);
        Assert.Single(inbox.Requests);
        Assert.Equal(created.Id, inbox.Requests[0].Id);
        Assert.Equal("submitted", inbox.Requests[0].Status);
        Assert.Contains(inbox.Balances, b => b.LeaveTypeCode == "annual" && b.Reserved == 1m);

        // an unlinked (or other) subject sees an empty inbox, never anyone else's rows
        var empty = await service.MyLeaveAsync("subject-unknown", CancellationToken.None);
        Assert.False(empty.Linked);
        Assert.Empty(empty.Requests);
    }

    [Fact]
    public async Task CancelLeave_SetsCancelledAndReleasesReservation()
    {
        var (service, ctx, worker, _, _) = Build();
        var repo = new TimeRepository(ctx);
        var created = await SubmitLeaveAsync(ctx, service, worker);

        var cancelled = await service.CancelLeaveAsync(created.Id, "subject-001", CancellationToken.None);
        Assert.Equal("cancelled", cancelled.Status);

        var ledger = await repo.GetLedgerAsync(worker.Id, CancellationToken.None);
        Assert.DoesNotContain(ledger, l => l.Days < 0);
        var balances = await service.GetBalancesAsync(worker.Id, CancellationToken.None);
        var annual = balances.Single(b => b.LeaveTypeCode == "annual");
        Assert.Equal(0m, annual.Reserved);
        Assert.Equal(20m, annual.Available);

        // workflow request was closed via the cancel transition
        var wfReq = await ctx.WorkflowRequests.FirstAsync(w => w.SubjectWorkerId == worker.Id);
        Assert.Equal("cancelled", wfReq.Status);
    }

    [Fact]
    public async Task CancelLeave_FinalStatus_Throws()
    {
        var (service, ctx, worker, _, _) = Build();
        var created = await SubmitLeaveAsync(ctx, service, worker);
        await service.DecideLeaveAsync(created.Id, new TimeDecisionRequest("approve", null), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CancelLeaveAsync(created.Id, "subject-001", CancellationToken.None));
        Assert.Equal("leave-not-cancellable", ex.Code);
    }

    [Fact]
    public async Task CancelLeave_NotOwned_Throws()
    {
        var (service, ctx, worker, _, _) = Build();
        var created = await SubmitLeaveAsync(ctx, service, worker);

        // an unlinked subject cannot touch the request at all
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CancelLeaveAsync(created.Id, "subject-other", CancellationToken.None));
        Assert.Equal("no-worker-linked", ex.Code);

        // a DIFFERENT linked worker also cannot cancel it
        var other = new Worker
        {
            EmployeeNo = "EMP-TM-002", FirstName = "Other", LastName = "Colleague",
            WorkerType = "employee", Status = "active", Nationality = "ZM",
            TenantId = "test-tenant", SubjectId = "subject-other",
        };
        ctx.Workers.Add(other);
        ctx.SaveChanges();

        var ex2 = await Assert.ThrowsAsync<DomainException>(() =>
            service.CancelLeaveAsync(created.Id, "subject-other", CancellationToken.None));
        Assert.Equal("leave-not-owned", ex2.Code);
    }

    // ===================== M28: operational time and leave =====================

    [Fact]
    public async Task AttendanceImport_UsesAssignedShift_AndCalculatesOvertime()
    {
        var (service, ctx, worker, _, _) = Build();
        var shift = await service.CreateShiftAsync(new ShiftCreateRequest(
            "DAY", "Day shift", "08:00", "17:00", 30, 8, 8, 1.5m, 2, 2), CancellationToken.None);
        var calendarId = await ctx.WorkCalendars.Select(c => c.Id).SingleAsync();
        await service.AssignShiftAsync(worker.Id,
            new ShiftAssignmentRequest(shift.Id, calendarId, "2026-08-01"), CancellationToken.None);

        var result = await service.ImportAttendanceAsync(new AttendanceImportRequest("clock.csv",
            [new AttendanceImportRow(worker.EmployeeNo, "2026-08-17", "08:00", "18:00")]),
            "hr-admin", CancellationToken.None);

        Assert.Equal("completed", result.Status);
        Assert.Equal(1, result.ImportedCount);
        var record = await ctx.AttendanceRecords.SingleAsync();
        Assert.Equal(9.5m, record.TotalHours);
        Assert.Equal(8m, record.RegularHours);
        Assert.Equal(1.5m, record.OvertimeHours);
        Assert.Equal(1.5m, record.OvertimeMultiplier);
        Assert.Equal(shift.Id, record.ShiftId);
        Assert.Equal(result.BatchId, record.ImportBatchId);
        var history = await service.GetOperationsHistoryAsync(CancellationToken.None);
        Assert.Contains(history.Imports, batch => batch.BatchId == result.BatchId && batch.ImportedCount == 1);
    }

    [Fact]
    public async Task AttendanceImport_RecalculatesWeeklyOvertimeAfterFortyEightHours()
    {
        var (service, ctx, worker, _, _) = Build();
        var calendar = await ctx.WorkCalendars.SingleAsync();
        calendar.WeekendDays = "";
        await ctx.SaveChangesAsync();
        var shift = await service.CreateShiftAsync(new ShiftCreateRequest(
            "WK", "Weekly test shift", "08:00", "16:00", 0, 8, 8, 1.5m, 2, 2), CancellationToken.None);
        await service.AssignShiftAsync(worker.Id,
            new ShiftAssignmentRequest(shift.Id, calendar.Id, "2026-08-01"), CancellationToken.None);

        await service.ImportAttendanceAsync(new AttendanceImportRequest("week.csv",
            Enumerable.Range(17, 7)
                .Select(day => new AttendanceImportRow(worker.EmployeeNo, $"2026-08-{day}", "08:00", "16:00"))
                .ToList()),
            "hr-admin", CancellationToken.None);

        var records = await ctx.AttendanceRecords.OrderBy(r => r.WorkDate).ToListAsync();
        Assert.Equal(7, records.Count);
        Assert.Equal(48m, records.Take(6).Sum(r => r.RegularHours));
        Assert.All(records.Take(6), r => Assert.Equal(0m, r.OvertimeHours));
        Assert.Equal(0m, records[6].RegularHours);
        Assert.Equal(8m, records[6].OvertimeHours);
        Assert.Equal(1.5m, records[6].OvertimeMultiplier);
        Assert.Equal(208m, records[6].OvertimeHourlyDivisor);
        Assert.Equal("ordinary", records[6].OvertimeRuleCode);
    }

    [Fact]
    public async Task AttendanceImport_ReconcilesDuplicateAndUnknownEmployees()
    {
        var (service, ctx, worker, _, _) = Build();
        var result = await service.ImportAttendanceAsync(new AttendanceImportRequest("errors.csv",
            [
                new AttendanceImportRow(worker.EmployeeNo, "2026-08-17", "08:00", "17:00"),
                new AttendanceImportRow(worker.EmployeeNo, "2026-08-17", "08:05", "17:05"),
                new AttendanceImportRow("MISSING", "2026-08-17", "08:00", "17:00"),
            ]), "hr-admin", CancellationToken.None);

        Assert.Equal("completed-with-errors", result.Status);
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(2, result.RejectedCount);
        Assert.Contains(result.Errors, error => error.Contains("duplicate row"));
        Assert.Contains(result.Errors, error => error.Contains("employee not found"));
        Assert.NotNull((await ctx.AttendanceImportBatches.SingleAsync()).ErrorsJson);
    }

    [Fact]
    public async Task OvertimeImport_CreatesPendingAuditedOvertimeRecord()
    {
        var (service, ctx, worker, _, _) = Build();

        var result = await service.ImportOvertimeAsync(new OvertimeImportRequest("overtime.csv",
            [new OvertimeImportRow(worker.EmployeeNo, "2026-08-20", 2.5m, null, "Supervisor sheet")]),
            "hr-admin", CancellationToken.None);

        Assert.Equal("completed", result.Status);
        Assert.Equal(1, result.ImportedCount);
        var record = await ctx.AttendanceRecords.SingleAsync();
        Assert.Equal("overtime-import", record.Source);
        Assert.Equal(2.5m, record.OvertimeHours);
        Assert.Equal(1.5m, record.OvertimeMultiplier);
        Assert.Equal(208m, record.OvertimeHourlyDivisor);
        Assert.Equal("ordinary", record.OvertimeRuleCode);
        Assert.Equal("pending", record.OvertimeStatus);

        var audit = await ctx.AuditEntries.SingleAsync(a => a.EntityType == "time.overtime");
        Assert.Equal("overtime-import-create", audit.Action);
        Assert.Equal("hr-admin", audit.ActorSubjectId);
        Assert.Contains("Supervisor sheet", audit.AfterJson);
    }

    [Fact]
    public async Task OvertimeImport_CannotChangePayrollLinkedOvertime()
    {
        var (service, ctx, worker, _, _) = Build();
        ctx.AttendanceRecords.Add(new AttendanceRecord
        {
            WorkerId = worker.Id,
            WorkDate = new DateOnly(2026, 8, 20),
            Source = "overtime-import",
            DerivedStatus = "present",
            OvertimeHours = 2m,
            OvertimeMultiplier = 1.5m,
            OvertimeStatus = "paid",
            OvertimePayrollRunId = Guid.CreateVersion7(),
            TenantId = "test-tenant",
        });
        await ctx.SaveChangesAsync();

        var result = await service.ImportOvertimeAsync(new OvertimeImportRequest("overtime.csv",
            [new OvertimeImportRow(worker.EmployeeNo, "2026-08-20", 3m)]),
            "hr-admin", CancellationToken.None);

        Assert.Equal("completed-with-errors", result.Status);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Contains(result.Errors, error => error.Contains("already linked to payroll"));
        var record = await ctx.AttendanceRecords.SingleAsync();
        Assert.Equal(2m, record.OvertimeHours);
    }

    [Fact]
    public async Task AccrualRun_IsIdempotent_AndAdjustmentChangesAvailableBalance()
    {
        var (service, ctx, worker, _, _) = Build();
        var run = await service.RunLeaveAccrualAsync(new LeaveAccrualRunRequest("2026-08"),
            "hr-admin", CancellationToken.None);
        Assert.Equal(1, run.WorkerCount);
        Assert.Equal(1, run.LedgerEntryCount);
        Assert.Equal(1m, run.TotalDaysAccrued);

        var duplicate = await Assert.ThrowsAsync<DomainException>(() =>
            service.RunLeaveAccrualAsync(new LeaveAccrualRunRequest("2026-08"), "hr-admin", CancellationToken.None));
        Assert.Equal("accrual-period-exists", duplicate.Code);

        await service.AdjustLeaveBalanceAsync(new LeaveBalanceAdjustmentRequest(
            worker.Id, "annual", 2.5m, "Opening balance correction", "2026-08-31"), "hr-admin", CancellationToken.None);
        var annual = (await service.GetBalancesAsync(worker.Id, CancellationToken.None)).Single();
        Assert.Equal(3.5m, annual.Accrued);
        Assert.Equal(3.5m, annual.Available);
        Assert.Contains(await ctx.LeaveBalanceLedgers.ToListAsync(), entry =>
            entry.Reason == "manual-adjustment" && entry.ForDate == new DateOnly(2026, 8, 31));
    }

    [Fact]
    public async Task AccrualRun_OnlyCreditsEnabledTypesFromTheirStartMonth()
    {
        var (service, ctx, worker, _, _) = Build();
        ctx.LeaveTypes.Add(new LeaveType
        {
            Code = "study", Name = "Study Leave", Category = "paid", DefaultDaysPerYear = 14,
            EffectiveFrom = new DateOnly(2026, 1, 1), IsActive = true, TenantId = "test-tenant",
        });
        ctx.SaveChanges();

        var run = await service.RunLeaveAccrualAsync(new LeaveAccrualRunRequest("2026-09"),
            "hr-admin", CancellationToken.None);

        Assert.Equal(1, run.LedgerEntryCount);
        Assert.Equal(1m, run.TotalDaysAccrued);
        var entry = Assert.Single(ctx.LeaveBalanceLedgers.Where(e => e.WorkerId == worker.Id));
        Assert.Equal("annual", entry.LeaveTypeCode);
    }

    [Fact]
    public async Task EscalationRun_OnlyMovesOverdueTimeRequests()
    {
        var (service, ctx, worker, _, _) = Build();
        ctx.WorkflowRequests.AddRange(
            new WorkflowRequest
            {
                WorkflowType = "leave", SubjectWorkerId = worker.Id,
                Status = "submitted", DueAt = DateTimeOffset.UtcNow.AddDays(-1), TenantId = "test-tenant",
            },
            new WorkflowRequest
            {
                WorkflowType = "payroll", Status = "submitted",
                DueAt = DateTimeOffset.UtcNow.AddDays(-1), TenantId = "test-tenant",
            });
        await ctx.SaveChangesAsync();

        var result = await service.EscalateOverdueAsync(CancellationToken.None);
        Assert.Equal(1, result.Reviewed);
        Assert.Equal(1, result.Escalated);
        var leave = await ctx.WorkflowRequests.SingleAsync(r => r.WorkflowType == "leave");
        Assert.NotNull(leave.EscalatedAt);
        Assert.True(leave.DueAt > DateTimeOffset.UtcNow.AddDays(2));
        Assert.Null((await ctx.WorkflowRequests.SingleAsync(r => r.WorkflowType == "payroll")).EscalatedAt);
    }

    // ===================== M35: self-service dashboard =====================

    [Fact]
    public async Task MyDashboard_UnlinkedSubject_ReturnsNotLinked()
    {
        var (service, _, _, _, _) = Build();
        var dash = await service.MyDashboardAsync("nonexistent-subject", CancellationToken.None);
        Assert.False(dash.Linked);
        Assert.Null(dash.TodayPunch);
        Assert.Empty(dash.Balances);
    }

    [Fact]
    public async Task MyDashboard_LinkedSubject_ReturnsPunchAndBalances()
    {
        var (service, ctx, worker, _, _) = Build();
        // accrue 5 days annual
        ctx.LeaveBalanceLedgers.Add(new LeaveBalanceLedger
        {
            WorkerId = worker.Id, LeaveTypeCode = "annual", Days = 5m,
            Reason = "accrual", ReferenceType = "", ForDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TenantId = "test-tenant",
        });
        await ctx.SaveChangesAsync();

        // clock in
        await service.ClockInAsync(worker.Id, CancellationToken.None);

        var dash = await service.MyDashboardAsync("subject-001", CancellationToken.None);
        Assert.True(dash.Linked);
        Assert.Equal(worker.Id, dash.WorkerId);
        Assert.Equal("EMP-TM-001", dash.EmployeeNo);
        Assert.NotNull(dash.TodayPunch);
        Assert.Equal("in", dash.TodayPunch!.State);
        Assert.Single(dash.Balances);
        Assert.Equal(5m, dash.Balances[0].Available);
    }
}
