using System.Text.Json;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Application.Payroll;

namespace Mightyfin.Erp.Hrm.Application.Time;

/// <summary>Leave requests, balances and attendance corrections. Balance is a
/// ledger sum (explainable), reservations happen on submit and are released on
/// approval/rejection/cancellation. Payroll cutoff warnings come from the
/// owning pay period.</summary>
public interface ITimeService
{
    Task<Paged<LeaveRequestDto>> ListLeaveAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<LeaveRequestDto> CreateLeaveAsync(LeaveRequestCreate request, CancellationToken ct);
    Task<List<LeaveBalanceDto>> GetBalancesAsync(Guid workerId, CancellationToken ct);
    // M16: self-service leave for the signed-in worker. Keyed on the Keycloak
    // subject id (not a caller-supplied worker id) so an employee can only see
    // their own requests and balances.
    Task<MyLeaveDto> MyLeaveAsync(string subjectId, CancellationToken ct);

    // M35: single self-service dashboard payload — today's attendance punch,
    // leave balances, and the worker's identity. One round-trip for the main
    // self-service page instead of 3 separate calls.
    Task<SelfDashboardDto> MyDashboardAsync(string subjectId, CancellationToken ct);
    Task<LeaveRequestDto> CancelLeaveAsync(Guid id, string subjectId, CancellationToken ct);
    Task<Paged<AttendanceCorrectionDto>> ListCorrectionsAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<AttendanceCorrectionDto> CreateCorrectionAsync(AttendanceCorrectionCreate request, CancellationToken ct);

    // M3: attendance, roster and decisions
    Task<PunchResultDto> ClockInAsync(Guid workerId, CancellationToken ct);
    Task<PunchResultDto> ClockOutAsync(Guid workerId, CancellationToken ct);
    Task<PunchResultDto> GetTodayAsync(Guid workerId, CancellationToken ct);
    Task<List<AttendanceRecordDto>> ListAttendanceAsync(Guid workerId, string? from, string? to, CancellationToken ct);
    Task<List<AttendanceRecordDto>> ListAttendanceForScopeAsync(string? from, string? to, CancellationToken ct);
    Task<List<AttendanceRecordDto>> ListOvertimeAsync(Guid? workerId, string? from, string? to, string? status, CancellationToken ct);
    Task<AttendanceRecordDto> DecideOvertimeAsync(Guid id, OvertimeDecisionRequest request, string actorSubjectId, CancellationToken ct);
    Task<List<RosterDayDto>> GetRosterAsync(Guid workerId, string? from, string? to, CancellationToken ct);
    Task<AttendanceCorrectionDto> DecideCorrectionAsync(Guid id, TimeDecisionRequest request, CancellationToken ct);
    Task<LeaveRequestDto> DecideLeaveAsync(Guid id, TimeDecisionRequest request, CancellationToken ct);
    // M28 operational administration
    Task<List<ShiftDto>> ListShiftsAsync(CancellationToken ct);
    Task<ShiftDto> CreateShiftAsync(ShiftCreateRequest request, CancellationToken ct);
    Task<ShiftDto> UpdateShiftAsync(Guid id, ShiftUpdateRequest request, CancellationToken ct);
    Task<ShiftDto> CloseShiftAsync(Guid id, CancellationToken ct);
    Task<ShiftAssignmentDto> AssignShiftAsync(Guid workerId, ShiftAssignmentRequest request, CancellationToken ct);
    Task<List<AttendanceRecordDto>> CreateManualAttendanceAsync(ManualAttendanceRequest request, string actorSubjectId, CancellationToken ct);
    Task<AttendanceImportResultDto> ImportAttendanceAsync(AttendanceImportRequest request, string actorSubjectId, CancellationToken ct);
    Task<AttendanceImportResultDto> ImportOvertimeAsync(OvertimeImportRequest request, string actorSubjectId, CancellationToken ct);
    Task<LeaveAccrualRunDto> RunLeaveAccrualAsync(LeaveAccrualRunRequest request, string actorSubjectId, CancellationToken ct);
    Task<LeaveAccrualRunDto> RunScheduledLeaveAccrualAsync(LeaveAccrualRunRequest request, CancellationToken ct);
    Task<LeaveBalanceAdjustmentDto> AdjustLeaveBalanceAsync(LeaveBalanceAdjustmentRequest request, string actorSubjectId, CancellationToken ct);
    Task<EscalationRunDto> EscalateOverdueAsync(CancellationToken ct);
    Task<TimeOperationsHistoryDto> GetOperationsHistoryAsync(CancellationToken ct);
    // M41 Gap 6a: leave encashment — HR converts unused leave balance into a cash
    // payout at the worker's daily rate (basic monthly / 26 working days).
    Task<List<LeaveEncashmentRequestDto>> ListEncashmentsAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<LeaveEncashmentRequestDto> CreateEncashmentAsync(LeaveEncashmentCreateRequest request, string actorSubjectId, CancellationToken ct);
    Task<LeaveEncashmentRequestDto> DecideEncashmentAsync(Guid id, LeaveEncashmentDecideRequest request, string actorSubjectId, CancellationToken ct);
    Task<LeaveEncashmentRateQuote> GetEncashmentRateAsync(Guid workerId, string leaveTypeCode, decimal days, CancellationToken ct);
}

public sealed record LeaveRequestDto(Guid Id, Guid WorkerId, string WorkerName, string LeaveTypeCode,
    string StartDate, string EndDate, decimal RequestedDays, string Status, bool BalanceReserved,
    bool CrossesCutoff, DateTimeOffset CreatedAt, Guid? LocationId = null);

/// <summary>M16: the signed-in worker's own leave request row, including the
/// rejection/return reason so the UI can explain why a request was sent back.</summary>
public sealed record SelfLeaveRequestDto(Guid Id, string LeaveTypeCode, string StartDate, string EndDate,
    decimal RequestedDays, string Status, string? RejectionReason, bool CrossesCutoff,
    DateTimeOffset CreatedAt);

/// <summary>M16: single self-service envelope — own identity, balances across
/// every configured leave type, and own leave requests.</summary>
public sealed record MyLeaveDto(Guid WorkerId, string WorkerName, string? EmployeeNo, bool Linked,
    List<LeaveBalanceDto> Balances, List<SelfLeaveRequestDto> Requests);

/// <summary>M35: self-service dashboard payload — today's attendance punch,
/// leave balances, and worker identity. One round-trip for the dashboard.</summary>
public sealed record SelfDashboardDto(Guid WorkerId, string WorkerName, string? EmployeeNo, bool Linked,
    PunchResultDto? TodayPunch, List<LeaveBalanceDto> Balances);
public sealed record AttendanceCorrectionDto(Guid Id, Guid WorkerId, string WorkerName, string WorkDate,
    string IssueType, string? ProposedClockIn, string? ProposedClockOut, string? ProposedStatus,
    string Reason, string Status, DateTimeOffset CreatedAt, Guid? LocationId = null);

public sealed class TimeServiceImpl(
    ITimeRepository repo,
    IAuthzService authz,
    IWorkflowService workflow,
    IWorkerRepository workers,
    Application.ShellContext? scope = null,
    IPayrollRepository? payroll = null,
    IOutboxWriter? outbox = null,
    IUnitOfWork? unitOfWork = null) : ITimeService
{
    private bool IsEmployeeOnly =>
        authz.IsRole("employee") && !authz.IsRole("hr_ops", "hr_admin", "manager", "payroll");

    private async Task RequireWorkerScopeAsync(Guid workerId, CancellationToken ct)
    {
        if (!IsEmployeeOnly) return;
        var worker = await workers.GetByIdAsync(workerId, ct)
            ?? throw new DomainException("worker-not-found", $"Worker {workerId} does not exist.");
        if (string.IsNullOrWhiteSpace(authz.CurrentSubjectId) ||
            !string.Equals(worker.SubjectId, authz.CurrentSubjectId, StringComparison.Ordinal))
            throw new DomainException("worker-access-denied", "Employees can access only their own time and leave records.");
    }

    public async Task<Paged<LeaveRequestDto>> ListLeaveAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee");
        if (IsEmployeeOnly && workerId is null)
            throw new DomainException("worker-access-denied", "Employees must use their own leave inbox.");
        if (workerId.HasValue) await RequireWorkerScopeAsync(workerId.Value, ct);
        var (items, total) = await repo.ListLeaveRequestsAsync(workerId, status, ct);
        // M44 branch scoping: an operator scoped to a branch sees only that
        // branch's requests (plus branch-less/legacy rows); entity-level
        // operators see everything.
        if (!IsEmployeeOnly && (scope?.IsScopedToBranch ?? false))
        {
            items = items.Where(lr => lr.LocationId == scope?.LocationId || lr.LocationId == null).ToList();
            total = items.Count;
        }
        return new Paged<LeaveRequestDto>(items.Select(Map).ToList(), total, 1, 50);
    }
    public async Task<LeaveRequestDto> CreateLeaveAsync(LeaveRequestCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        await RequireWorkerScopeAsync(request.WorkerId, ct);
        var start = DateOnly.Parse(request.StartDate);
        var end = DateOnly.Parse(request.EndDate);
        if (end < start) throw new DomainException("leave-invalid-dates", "End date is before start date.");

        var ledger = await repo.GetBalancesAsync(request.WorkerId, request.LeaveTypeCode, ct);
        var available = ledger.Where(l => l.Reason != "request").Sum(l => l.Days)
                       - ledger.Where(l => l.Reason == "request" && l.ReferenceId is null).Sum(l => Math.Abs(l.Days));

        var requestedDays = (decimal)(end.DayNumber - start.DayNumber + 1);
        var balances = await repo.GetLeaveTypeAsync(request.LeaveTypeCode, ct);
        if (balances is null) throw new DomainException("leave-type-not-found", $"Leave type {request.LeaveTypeCode} is not configured.");
        if (!balances.AllowNegative && requestedDays > available + 0.0001m)
            throw new DomainException("leave-insufficient-balance",
                $"Available balance ({available:F1} days) is less than requested ({requestedDays:F1} days).");

        // payroll cutoff warning: does the absence cross the current period cutoff?
        var cutoff = await repo.GetCurrentCutoffAsync(ct);
        var crosses = cutoff.HasValue && start <= cutoff.Value && end >= cutoff.Value;

        var lr = new LeaveRequest
        {
            WorkerId = request.WorkerId,
            LeaveTypeCode = request.LeaveTypeCode,
            StartDate = start, EndDate = end,
            IsPartialDay = request.IsPartialDay,
            StartTime = request.StartTime, EndTime = request.EndTime,
            RequestedDays = requestedDays,
            EvidenceAttached = request.EvidenceAttached,
            CreatedForPeriod = cutoff ?? start,
            CrossesCutoff = crosses,
            // M44 branch scoping: requests inherit the operator's work scope!.
            LocationId = (scope?.IsScopedToBranch ?? false) ? scope?.LocationId : null,
            Status = "submitted",
        };
        var worker = await workers.GetByIdAsync(request.WorkerId, ct)
            ?? throw new DomainException("worker-not-found", $"Worker {request.WorkerId} does not exist.");
        LeaveRequest created = lr;
        async Task CreateAndEnqueue(CancellationToken transactionCt)
        {
            created = await repo.CreateLeaveRequestAsync(lr, transactionCt);
            // reserve balance and open approval workflow
            await repo.ReserveBalanceAsync(request.WorkerId, request.LeaveTypeCode, -requestedDays, created.Id, transactionCt);
            await workflow.OpenAsync("leave", created.Id, created.WorkerId,
                JsonSerializer.Serialize(new { created.LeaveTypeCode, created.StartDate, created.EndDate, created.RequestedDays }), transactionCt);
            if (outbox is null) return;
            await EnqueueLeaveEventAsync(HrmEventTypes.LeaveRequested, created, worker, transactionCt);
        }
        if (unitOfWork is null)
            await CreateAndEnqueue(ct);
        else
            await unitOfWork.ExecuteAsync(CreateAndEnqueue, ct);
        return Map(created);
    }

    public async Task<List<LeaveBalanceDto>> GetBalancesAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin", "manager", "payroll");
        await RequireWorkerScopeAsync(workerId, ct);
        var ledger = await repo.GetLedgerAsync(workerId, ct);
        var types = await repo.GetLeaveTypesAsync(ct);
        return types.Select(t =>
        {
            var rows = ledger.Where(l => l.LeaveTypeCode == t.Code).ToList();
            // releases (reason == "request-release") just undo open reservations and
            // must not be counted as new accruals
            var accrued = rows.Where(r => r.Days > 0 && r.Reason != "request" && r.Reason != "request-release").Sum(r => r.Days);
            // taken is permanent deductions only: open reservations (reason == "request")
            // and forfeitures are reported separately so available arithmetic stays clean
            var taken = -rows.Where(r => r.Days < 0 && r.Reason != "request" && r.Reason != "forfeiture").Sum(r => r.Days);
            var reserved = -rows.Where(r => r.Days < 0 && r.Reason == "request").Sum(r => r.Days);
            var expired = -rows.Where(r => r.Days < 0 && r.Reason == "forfeiture").Sum(r => r.Days);
            return new LeaveBalanceDto(t.Code, t.Name, accrued, taken, reserved, expired, accrued - taken - reserved - expired);
        }).ToList();
    }

    public async Task<Paged<AttendanceCorrectionDto>> ListCorrectionsAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee");
        if (IsEmployeeOnly && workerId is null)
            throw new DomainException("worker-access-denied", "Employees must use their own attendance inbox.");
        if (workerId.HasValue) await RequireWorkerScopeAsync(workerId.Value, ct);
        var (items, total) = await repo.ListCorrectionsAsync(workerId, status, ct);
        // M44 branch scoping: scoped operators see only their branch's corrections.
        if (!IsEmployeeOnly && (scope?.IsScopedToBranch ?? false))
        {
            items = items.Where(c => c.LocationId == scope?.LocationId || c.LocationId == null).ToList();
            total = items.Count;
        }
        return new Paged<AttendanceCorrectionDto>(items.Select(c => new AttendanceCorrectionDto(
            c.Id, c.WorkerId, c.Worker?.FullName ?? "", c.WorkDate.ToString(), c.IssueType,
            c.ProposedClockIn?.ToString(), c.ProposedClockOut?.ToString(), c.ProposedStatus,
            c.Reason, c.Status, c.CreatedAt, c.LocationId)).ToList(), total, 1, 50);
    }

    public async Task<AttendanceCorrectionDto> CreateCorrectionAsync(AttendanceCorrectionCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        await RequireWorkerScopeAsync(request.WorkerId, ct);
        var c = new AttendanceCorrection
        {
            WorkerId = request.WorkerId,
            WorkDate = DateOnly.Parse(request.WorkDate),
            IssueType = request.IssueType,
            ProposedClockIn = request.ProposedClockIn is null ? null : TimeOnly.Parse(request.ProposedClockIn),
            ProposedClockOut = request.ProposedClockOut is null ? null : TimeOnly.Parse(request.ProposedClockOut),
            ProposedStatus = request.ProposedStatus,
            Reason = request.Reason,
            // M44 branch scoping: corrections inherit the operator's work scope!.
            LocationId = (scope?.IsScopedToBranch ?? false) ? scope?.LocationId : null,
            Status = "submitted",
        };
        var created = await repo.CreateCorrectionAsync(c, ct);
        await workflow.OpenAsync("attendance-correction", created.Id, created.WorkerId,
            JsonSerializer.Serialize(new { created.WorkDate, created.IssueType, created.ProposedClockIn, created.ProposedClockOut, created.ProposedStatus }), ct);
        return new AttendanceCorrectionDto(created.Id, created.WorkerId, created.Worker?.FullName ?? "",
            created.WorkDate.ToString(), created.IssueType, created.ProposedClockIn?.ToString(),
            created.ProposedClockOut?.ToString(), created.ProposedStatus, created.Reason, created.Status, created.CreatedAt, created.LocationId);
    }

    // ===================== M3: attendance, roster, decisions =====================

    public async Task<PunchResultDto> ClockInAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        await RequireWorkerScopeAsync(workerId, ct);
        return await GetOrCreatePunchAsync(workerId, DateOnly.FromDateTime(DateTime.UtcNow), clockIn: true, ct);
    }

    public async Task<PunchResultDto> ClockOutAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        await RequireWorkerScopeAsync(workerId, ct);
        return await GetOrCreatePunchAsync(workerId, DateOnly.FromDateTime(DateTime.UtcNow), clockIn: false, ct);
    }

    public async Task<PunchResultDto> GetTodayAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin", "manager", "payroll");
        await RequireWorkerScopeAsync(workerId, ct);
        return await GetOrCreatePunchAsync(workerId, DateOnly.FromDateTime(DateTime.UtcNow), clockIn: null, ct);
    }

    public async Task<List<AttendanceRecordDto>> ListAttendanceAsync(Guid workerId, string? from, string? to, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee", "payroll");
        await RequireWorkerScopeAsync(workerId, ct);
        var f = from is null ? (DateOnly?)null : DateOnly.Parse(from);
        var t = to is null ? (DateOnly?)null : DateOnly.Parse(to);
        var items = await repo.ListAttendanceAsync(workerId, f, t, ct);
        return items.Select(MapAttendance).ToList();
    }

    public async Task<List<AttendanceRecordDto>> ListAttendanceForScopeAsync(string? from, string? to, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "payroll", "timesheet_operator");
        DateOnly? f = from is null ? null : DateOnly.Parse(from);
        DateOnly? t = to is null ? null : DateOnly.Parse(to);
        var items = await repo.ListAttendanceForScopeAsync(f, t, scope?.LocationId, scope?.OrgUnitId, ct);
        if (scope is not null && scope.IsConfined)
            items = items.Where(a => (a.LocationId.HasValue && scope.AllowedLocationIds.Contains(a.LocationId.Value))
                || (!a.LocationId.HasValue && a.Worker?.LocationId.HasValue == true && scope.AllowedLocationIds.Contains(a.Worker.LocationId.Value))).ToList();
        return items.Select(MapAttendance).ToList();
    }

    public async Task<List<AttendanceRecordDto>> ListOvertimeAsync(Guid? workerId, string? from, string? to, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "payroll", "employee");
        if (IsEmployeeOnly && workerId is null)
        {
            var linked = string.IsNullOrWhiteSpace(authz.CurrentSubjectId)
                ? null
                : await workers.FindBySubjectIdAsync(authz.CurrentSubjectId, ct);
            workerId = linked?.Id ?? throw new DomainException("worker-not-linked", "No worker record is linked to your account.");
        }
        if (workerId.HasValue) await RequireWorkerScopeAsync(workerId.Value, ct);
        DateOnly? f = from is null ? null : DateOnly.Parse(from);
        DateOnly? t = to is null ? null : DateOnly.Parse(to);
        var items = await repo.ListOvertimeAsync(workerId, f, t, status, ct);
        if (!IsEmployeeOnly && (scope?.IsScopedToBranch ?? false))
            items = items.Where(a => a.LocationId == scope?.LocationId || a.LocationId == null).ToList();
        return items.Select(MapAttendance).ToList();
    }

    public async Task<AttendanceRecordDto> DecideOvertimeAsync(Guid id, OvertimeDecisionRequest request, string actorSubjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "payroll");
        var record = (await repo.ListOvertimeAsync(null, null, null, null, ct)).FirstOrDefault(a => a.Id == id)
            ?? throw new DomainException("overtime-not-found", $"Overtime record {id} does not exist.");
        if (!IsEmployeeOnly && (scope?.IsScopedToBranch ?? false)
            && record.LocationId.HasValue && record.LocationId != scope?.LocationId)
            throw new DomainException("overtime-scope-denied", "This overtime record is outside your assigned branch scope.");
        if (record.OvertimeHours <= 0)
            throw new DomainException("overtime-not-present", "This attendance record has no derived overtime hours.");
        if (record.OvertimePayrollRunId.HasValue)
            throw new DomainException("overtime-already-paid", "This overtime record is already linked to a payroll run and cannot be changed.");
        var beforeJson = AttendanceSnapshot(record);
        var action = (request.Action ?? "").Trim().ToLowerInvariant();
        if (action is not ("approve" or "reject"))
            throw new DomainException("overtime-invalid-decision", "Decision action must be approve or reject.");
        if (action == "reject" && string.IsNullOrWhiteSpace(request.Reason))
            throw new DomainException("overtime-rejection-reason-required", "A reason is required when rejecting overtime.");
        record.OvertimeStatus = action == "approve" ? "approved" : "rejected";
        record.OvertimeDecisionReason = request.Reason?.Trim();
        record.OvertimeDecidedBySubjectId = actorSubjectId;
        record.OvertimeDecidedAt = DateTimeOffset.UtcNow;
        async Task Decide(CancellationToken transactionCt)
        {
            record = await repo.UpdateAttendanceAsync(record, transactionCt);
            await WriteTimeAuditAsync("time.overtime", record.Id.ToString("D"), record.WorkerId,
                "decision", actorSubjectId, beforeJson, AttendanceSnapshot(record), transactionCt);
            if (outbox is not null)
            {
                await outbox.EnqueueAsync(HrmEventTypes.OvertimeDecided,
                    record.Worker?.SubjectId ?? record.WorkerId.ToString("D"),
                    new
                    {
                        overtime_id = record.Id,
                        worker_id = record.WorkerId,
                        work_date = record.WorkDate.ToString("yyyy-MM-dd"),
                        overtime_hours = record.OvertimeHours,
                        overtime_multiplier = record.OvertimeMultiplier,
                        status = record.OvertimeStatus,
                        reason = record.OvertimeDecisionReason,
                        decided_by = actorSubjectId
                    }, transactionCt);
            }
        }
        if (unitOfWork is null)
            await Decide(ct);
        else
            await unitOfWork.ExecuteAsync(Decide, ct);
        return MapAttendance(record);
    }

    public async Task<List<RosterDayDto>> GetRosterAsync(Guid workerId, string? from, string? to, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin", "manager", "payroll");
        await RequireWorkerScopeAsync(workerId, ct);
        var start = from is null ? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)) : DateOnly.Parse(from);
        var end = to is null ? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)) : DateOnly.Parse(to);
        if (end < start) throw new DomainException("roster-invalid-dates", "End date is before start date.");

        var calendars = await repo.ListCalendarsAsync(ct);
        var calendar = calendars.FirstOrDefault(c => c.IsDefault) ?? calendars.FirstOrDefault();

        var attendance = (await repo.ListAttendanceAsync(workerId, start, end, ct)).ToDictionary(a => a.WorkDate, a => a);
        var corrections = (await repo.ListCorrectionsAsync(workerId, null, ct)).Items
            .Where(c => c.WorkDate >= start && c.WorkDate <= end)
            .GroupBy(c => c.WorkDate).ToDictionary(g => g.Key, g => g.First());

        var cutoff = await repo.GetCurrentCutoffAsync(ct);

        var days = new List<RosterDayDto>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            var assignment = await repo.GetShiftAssignmentAsync(workerId, d, ct);
            var dayCalendar = assignment?.Calendar ?? calendar;
            var shift = assignment?.Shift;
            var isWeekend = dayCalendar is not null && dayCalendar.WeekendDays
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(w => string.Equals(w, d.DayOfWeek.ToString().ToLowerInvariant().Substring(0, 3), StringComparison.OrdinalIgnoreCase));
            var hol = dayCalendar?.Holidays.FirstOrDefault(h => HolidayDate(h) == d);
            var isHoliday = hol is not null;
            var att = attendance.GetValueOrDefault(d);
            var cor = corrections.GetValueOrDefault(d);

            string? status = null;
            if (att is not null) status = att.DerivedStatus;
            else if (d <= DateOnly.FromDateTime(DateTime.UtcNow) && !isWeekend && !isHoliday) status = "missing-punch";

            days.Add(new RosterDayDto(d.ToString(), d.DayOfWeek.ToString("d"), !isWeekend && !isHoliday,
                att?.ClockIn?.ToString(), att?.ClockOut?.ToString(), status,
                shift?.Name, shift?.StartTime.ToString(), shift?.EndTime.ToString(), dayCalendar?.Name, isHoliday, hol?.Name,
                cutoff?.ToString(), cor?.Status));
        }
        return days;
    }

    // ===================== M28: operational time administration =====================

    public async Task<List<ShiftDto>> ListShiftsAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "payroll");
        return (await repo.ListShiftsAsync(ct)).Select(MapShift).ToList();
    }

    public async Task<ShiftDto> CreateShiftAsync(ShiftCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("shift-required-fields", "Shift code and name are required.");
        if (!TimeOnly.TryParse(request.StartTime, out var start) || !TimeOnly.TryParse(request.EndTime, out var end))
            throw new DomainException("shift-invalid-time", "Shift start and end must be valid times.");
        if (request.DailyOvertimeThresholdHours <= 0 || request.StandardHours <= 0)
            throw new DomainException("shift-invalid-hours", "Standard and overtime-threshold hours must be positive.");
        var shift = await repo.CreateShiftAsync(new ShiftDefinition
        {
            Code = request.Code.Trim().ToUpperInvariant(), Name = request.Name.Trim(), StartTime = start, EndTime = end,
            UnpaidBreakMinutes = request.UnpaidBreakMinutes, StandardHours = request.StandardHours,
            DailyOvertimeThresholdHours = request.DailyOvertimeThresholdHours,
            WeekdayOvertimeMultiplier = request.WeekdayOvertimeMultiplier,
            RestDayOvertimeMultiplier = request.RestDayOvertimeMultiplier,
            HolidayOvertimeMultiplier = request.HolidayOvertimeMultiplier,
        }, ct);
        return MapShift(shift);
    }

    public async Task<ShiftDto> UpdateShiftAsync(Guid id, ShiftUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var shift = await repo.GetShiftAsync(id, ct)
            ?? throw new DomainException("shift-not-found", $"Shift {id} does not exist.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("shift-required-fields", "Shift name is required.");
        if (!TimeOnly.TryParse(request.StartTime, out var start) || !TimeOnly.TryParse(request.EndTime, out var end))
            throw new DomainException("shift-invalid-time", "Shift start and end must be valid times.");
        if (request.DailyOvertimeThresholdHours <= 0 || request.StandardHours <= 0)
            throw new DomainException("shift-invalid-hours", "Standard and overtime-threshold hours must be positive.");
        shift.Name = request.Name.Trim();
        shift.StartTime = start;
        shift.EndTime = end;
        shift.UnpaidBreakMinutes = request.UnpaidBreakMinutes;
        shift.StandardHours = request.StandardHours;
        shift.DailyOvertimeThresholdHours = request.DailyOvertimeThresholdHours;
        shift.WeekdayOvertimeMultiplier = request.WeekdayOvertimeMultiplier;
        shift.RestDayOvertimeMultiplier = request.RestDayOvertimeMultiplier;
        shift.HolidayOvertimeMultiplier = request.HolidayOvertimeMultiplier;
        shift.UpdatedAt = DateTimeOffset.UtcNow;
        shift.UpdatedBy = authz.CurrentSubjectId;
        await repo.UpdateShiftAsync(shift, ct);
        return MapShift(shift);
    }

    public async Task<ShiftDto> CloseShiftAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var shift = await repo.GetShiftAsync(id, ct)
            ?? throw new DomainException("shift-not-found", $"Shift {id} does not exist.");
        shift.IsActive = false;
        shift.UpdatedAt = DateTimeOffset.UtcNow;
        shift.UpdatedBy = authz.CurrentSubjectId;
        await repo.UpdateShiftAsync(shift, ct);
        return MapShift(shift);
    }

    public async Task<ShiftAssignmentDto> AssignShiftAsync(Guid workerId, ShiftAssignmentRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        _ = await workers.GetByIdAsync(workerId, ct) ?? throw new DomainException("worker-not-found", $"Worker {workerId} does not exist.");
        var shift = (await repo.ListShiftsAsync(ct)).FirstOrDefault(s => s.Id == request.ShiftId)
            ?? throw new DomainException("shift-not-found", $"Shift {request.ShiftId} does not exist.");
        if (!DateOnly.TryParse(request.EffectiveFrom, out var from) ||
            (request.EffectiveTo is not null && !DateOnly.TryParse(request.EffectiveTo, out _)))
            throw new DomainException("shift-assignment-invalid-date", "Effective dates must use yyyy-MM-dd.");
        DateOnly? to = request.EffectiveTo is null ? null : DateOnly.Parse(request.EffectiveTo);
        if (to.HasValue && to.Value < from) throw new DomainException("shift-assignment-invalid-range", "EffectiveTo cannot be before EffectiveFrom.");
        await repo.CloseOpenShiftAssignmentsAsync(workerId, from.AddDays(-1), ct);
        var created = await repo.CreateShiftAssignmentAsync(new WorkerShiftAssignment
        {
            WorkerId = workerId, ShiftId = shift.Id, CalendarId = request.CalendarId,
            EffectiveFrom = from, EffectiveTo = to,
        }, ct);
        return new ShiftAssignmentDto(created.Id, workerId, shift.Id, shift.Name, created.CalendarId,
            created.Calendar?.Name, from.ToString(), to?.ToString());
    }

    public async Task<List<AttendanceRecordDto>> CreateManualAttendanceAsync(ManualAttendanceRequest request,
        string actorSubjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "timesheet_operator");
        if (!DateOnly.TryParseExact(request.WorkDate, "yyyy-MM-dd", out var date) || date > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new DomainException("attendance-invalid-date", "Choose a valid work date today or earlier.");
        if (request.Rows == null || request.Rows.Count is < 1 or > 500)
            throw new DomainException("attendance-invalid-rows", "Enter between 1 and 500 attendance rows.");
        if (request.Rows.Select(r => r.WorkerId).Distinct().Count() != request.Rows.Count)
            throw new DomainException("attendance-duplicate-worker", "Each employee can appear only once for the work date.");
        var saved = new List<AttendanceRecordDto>();
        async Task Save(CancellationToken token)
        {
            var prepared = new List<AttendanceRecord>();
            foreach (var row in request.Rows)
            {
                var worker = await workers.GetByIdAsync(row.WorkerId, token)
                    ?? throw new DomainException("worker-not-found", "Employee not found.");
                if (worker.IsArchived || worker.Status is not ("active" or "on-leave" or "notice")
                    || (worker.StartDate.HasValue && worker.StartDate > date) || (worker.EndDate.HasValue && worker.EndDate < date))
                    throw new DomainException("attendance-worker-inactive", $"{worker.EmployeeNo}: employee is not active for this date.");
                if (scope != null && ((scope.LocationId.HasValue && scope.LocationId != worker.LocationId)
                    || (scope.OrgUnitId.HasValue && scope.OrgUnitId != worker.OrgUnitId)
                    || (scope.IsConfined && (!worker.LocationId.HasValue || !scope.AllowedLocationIds.Contains(worker.LocationId.Value)))))
                    throw new DomainException("forbidden", "Employee is outside your branch access.");
                if (!TimeOnly.TryParseExact(row.ClockIn, "HH:mm", out var clockIn)
                    || !TimeOnly.TryParseExact(row.ClockOut, "HH:mm", out var clockOut) || clockIn == clockOut)
                    throw new DomainException("attendance-invalid-time", $"{worker.EmployeeNo}: enter different clock-in and clock-out times in HH:mm format.");
                if (await repo.GetAttendanceAsync(worker.Id, date, token) != null)
                    throw new DomainException("attendance-already-recorded", $"{worker.EmployeeNo}: attendance already exists for {date:yyyy-MM-dd}. Refresh the table or use attendance corrections.");
                if (payroll != null && await payroll.IsAttendanceDateLockedAsync(worker.Id, date, token))
                    throw new DomainException("attendance-payroll-locked", $"{worker.EmployeeNo}: payroll inputs for this date are locked. Attendance cannot be added.");
                // A weekly recalculation can affect adjacent dates. Do not alter locked payroll through a different day.
                var weekStart = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
                var week = await repo.ListAttendanceAsync(worker.Id, weekStart, weekStart.AddDays(6), token);
                if (payroll != null)
                    foreach (var existing in week.Where(a => !a.OvertimePayrollRunId.HasValue))
                        if (await payroll.IsAttendanceDateLockedAsync(worker.Id, existing.WorkDate, token))
                            throw new DomainException("attendance-payroll-locked", $"{worker.EmployeeNo}: this week contains locked payroll attendance. Contact payroll before changing the week.");
                prepared.Add(new AttendanceRecord { WorkerId = worker.Id, Worker = worker, LocationId = worker.LocationId,
                    WorkDate = date, ClockIn = clockIn, ClockOut = clockOut, Source = "manual-entry" });
            }
            // Validate the complete batch before writing any rows.
            foreach (var record in prepared)
            {
                await ApplyHoursAsync(record, token);
                await repo.CreateAttendanceAsync(record, token);
                await RecalculateWeeklyOvertimeAsync(record.WorkerId, date, token);
                await WriteTimeAuditAsync("time.attendance", record.Id.ToString("D"), record.WorkerId,
                    "manual-create", actorSubjectId, null, AttendanceSnapshot(record), token);
                saved.Add(MapAttendance(record));
            }
        }
        if (unitOfWork == null) await Save(ct); else await unitOfWork.ExecuteAsync(Save, ct);
        return saved;
    }

    public async Task<AttendanceImportResultDto> ImportAttendanceAsync(AttendanceImportRequest request,
        string actorSubjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "timesheet_operator");
        if (string.IsNullOrWhiteSpace(request.FileName) || request.Rows.Count == 0)
            throw new DomainException("attendance-import-empty", "A file name and at least one attendance row are required.");
        if (request.Rows.Count > 10_000)
            throw new DomainException("attendance-import-too-large", "An attendance batch cannot exceed 10,000 rows.");
        var batch = await repo.CreateImportBatchAsync(new AttendanceImportBatch
        {
            FileName = request.FileName.Trim(), Status = "processing", RowCount = request.Rows.Count,
            ImportedBySubjectId = actorSubjectId,
        }, ct);
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var affectedWeeks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in request.Rows)
        {
            var key = $"{row.EmployeeNo}:{row.WorkDate}";
            if (!seen.Add(key)) { errors.Add($"{key}: duplicate row in batch"); continue; }
            var worker = await repo.FindWorkerByEmployeeNoAsync(row.EmployeeNo.Trim(), ct);
            if (worker is null) { errors.Add($"{key}: employee not found"); continue; }
            if (!AttendanceScope.Allows(worker, scope))
            { errors.Add($"{key}: employee is outside your attendance scope"); continue; }
            if (!DateOnly.TryParse(row.WorkDate, out var date) ||
                (row.ClockIn is not null && !TimeOnly.TryParse(row.ClockIn, out _)) ||
                (row.ClockOut is not null && !TimeOnly.TryParse(row.ClockOut, out _)))
            { errors.Add($"{key}: invalid date or time"); continue; }
            var existing = await repo.GetAttendanceAsync(worker.Id, date, ct);
            if (existing?.OvertimePayrollRunId.HasValue == true || existing?.OvertimeStatus == "paid")
            { errors.Add($"{key}: attendance is locked by payroll"); continue; }
            var beforeJson = existing is null ? null : AttendanceSnapshot(existing);
            var record = existing ?? new AttendanceRecord { WorkerId = worker.Id, WorkDate = date };
            record.ClockIn = row.ClockIn is null ? null : TimeOnly.Parse(row.ClockIn);
            record.ClockOut = row.ClockOut is null ? null : TimeOnly.Parse(row.ClockOut);
            record.Source = "device-import";
            record.ImportBatchId = batch.Id;
            await ApplyHoursAsync(record, ct);
            if (existing is null) { await repo.CreateAttendanceAsync(record, ct); batch.ImportedCount++; }
            else { await repo.UpdateAttendanceAsync(record, ct); batch.UpdatedCount++; }
            await WriteTimeAuditAsync("time.attendance", record.Id.ToString("D"), record.WorkerId,
                existing is null ? "import-create" : "import-update", actorSubjectId, beforeJson,
                AttendanceSnapshot(record), ct);
            affectedWeeks.Add(WeekKey(worker.Id, date));
        }
        foreach (var affected in affectedWeeks)
        {
            var parts = affected.Split('|');
            await RecalculateWeeklyOvertimeAsync(Guid.Parse(parts[0]), DateOnly.Parse(parts[1]), ct);
        }
        batch.RejectedCount = errors.Count;
        batch.ErrorsJson = errors.Count == 0 ? null : JsonSerializer.Serialize(errors);
        batch.Status = errors.Count == 0 ? "completed" : "completed-with-errors";
        await repo.UpdateImportBatchAsync(batch, ct);
        return new AttendanceImportResultDto(batch.Id, batch.FileName, batch.Status, batch.RowCount,
            batch.ImportedCount, batch.UpdatedCount, batch.RejectedCount, errors);
    }

    public async Task<AttendanceImportResultDto> ImportOvertimeAsync(OvertimeImportRequest request,
        string actorSubjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        if (string.IsNullOrWhiteSpace(request.FileName) || request.Rows.Count == 0)
            throw new DomainException("overtime-import-empty", "A file name and at least one overtime row are required.");
        if (request.Rows.Count > 10_000)
            throw new DomainException("overtime-import-too-large", "An overtime batch cannot exceed 10,000 rows.");

        var batch = await repo.CreateImportBatchAsync(new AttendanceImportBatch
        {
            FileName = $"overtime:{request.FileName.Trim()}",
            Status = "processing",
            RowCount = request.Rows.Count,
            ImportedBySubjectId = actorSubjectId,
        }, ct);

        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in request.Rows)
        {
            var key = $"{row.EmployeeNo}:{row.WorkDate}";
            if (!seen.Add(key)) { errors.Add($"{key}: duplicate row in batch"); continue; }
            if (row.OvertimeHours <= 0) { errors.Add($"{key}: overtime hours must be greater than zero"); continue; }
            var worker = await repo.FindWorkerByEmployeeNoAsync(row.EmployeeNo.Trim(), ct);
            if (worker is null) { errors.Add($"{key}: employee not found"); continue; }
            if (!DateOnly.TryParse(row.WorkDate, out var date)) { errors.Add($"{key}: invalid work date"); continue; }

            var existing = await repo.GetAttendanceAsync(worker.Id, date, ct);
            if (existing?.OvertimePayrollRunId.HasValue == true)
            {
                errors.Add($"{key}: overtime is already linked to payroll and cannot be changed");
                continue;
            }

            var beforeJson = existing is null ? null : AttendanceSnapshot(existing);
            var record = existing ?? new AttendanceRecord
            {
                WorkerId = worker.Id,
                WorkDate = date,
                DerivedStatus = "present",
            };
            var profile = payroll is null ? null : await payroll.FindOpenProfileAsync(worker.Id, ct);
            var category = profile?.OvertimeCategory ?? "ordinary";
            var divisor = profile?.MonthlyOvertimeDivisor > 0
                ? profile.MonthlyOvertimeDivisor
                : (category == "watchperson-guard" ? 240m : 208m);
            var assignment = await repo.GetShiftAssignmentAsync(worker.Id, date, ct);
            var shiftMultiplier = assignment?.Shift?.WeekdayOvertimeMultiplier is > 0
                ? assignment.Shift.WeekdayOvertimeMultiplier
                : 1.5m;
            record.Source = "overtime-import";
            record.ImportBatchId = batch.Id;
            record.ShiftId = assignment?.ShiftId;
            record.LocationId ??= (scope?.IsScopedToBranch ?? false) ? scope?.LocationId : worker.LocationId;
            record.OvertimeHours = row.OvertimeHours;
            record.OvertimeMultiplier = row.OvertimeMultiplier is > 0 ? row.OvertimeMultiplier.Value : shiftMultiplier;
            record.OvertimeHourlyDivisor = divisor;
            record.OvertimeRuleCode = category;
            record.OvertimeStatus = NormalizeImportedOvertimeStatus(row.Status, request.MarkApproved);
            record.OvertimeDecisionReason = string.IsNullOrWhiteSpace(row.Reason)
                ? "Imported overtime hours"
                : row.Reason.Trim();
            if (record.OvertimeStatus == "approved")
            {
                record.OvertimeDecidedBySubjectId = actorSubjectId;
                record.OvertimeDecidedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                record.OvertimeDecidedBySubjectId = null;
                record.OvertimeDecidedAt = null;
            }

            if (existing is null) { await repo.CreateAttendanceAsync(record, ct); batch.ImportedCount++; }
            else { await repo.UpdateAttendanceAsync(record, ct); batch.UpdatedCount++; }
            await WriteTimeAuditAsync("time.overtime", record.Id.ToString("D"), record.WorkerId,
                existing is null ? "overtime-import-create" : "overtime-import-update", actorSubjectId,
                beforeJson, AttendanceSnapshot(record), ct);
        }

        batch.RejectedCount = errors.Count;
        batch.ErrorsJson = errors.Count == 0 ? null : JsonSerializer.Serialize(errors);
        batch.Status = errors.Count == 0 ? "completed" : "completed-with-errors";
        await repo.UpdateImportBatchAsync(batch, ct);
        return new AttendanceImportResultDto(batch.Id, batch.FileName, batch.Status, batch.RowCount,
            batch.ImportedCount, batch.UpdatedCount, batch.RejectedCount, errors);
    }

    public async Task<LeaveAccrualRunDto> RunLeaveAccrualAsync(LeaveAccrualRunRequest request,
        string actorSubjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        return await RunLeaveAccrualCoreAsync(request, actorSubjectId, ct);
    }

    public Task<LeaveAccrualRunDto> RunScheduledLeaveAccrualAsync(LeaveAccrualRunRequest request, CancellationToken ct)
        => RunLeaveAccrualCoreAsync(request, "system:monthly-leave-accrual", ct);

    private async Task<LeaveAccrualRunDto> RunLeaveAccrualCoreAsync(LeaveAccrualRunRequest request,
        string actorSubjectId, CancellationToken ct)
    {
        if (!DateOnly.TryParse($"{request.Period}-01", out var periodDate) || request.Period.Length != 7)
            throw new DomainException("accrual-period-invalid", "Accrual period must use yyyy-MM.");
        if (periodDate > new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1))
            throw new DomainException("accrual-period-future", "Future leave periods cannot be accrued.");
        if (await repo.GetAccrualRunAsync(request.Period, ct) is not null)
            throw new DomainException("accrual-period-exists", $"Leave accrual has already run for {request.Period}.");
        var leaveTypes = (await repo.GetLeaveTypesAsync(ct)).Where(t => t.AutoAccrueMonthly
            && t.AccrualStartDate is not null && t.AccrualStartDate.Value <= periodDate && t.EffectiveFrom <= periodDate
            && (t.EffectiveTo is null || t.EffectiveTo >= periodDate)
            && t.DefaultDaysPerYear > 0).ToList();
        if (leaveTypes.Count == 0)
            throw new DomainException("accrual-no-types", "No monthly-accrual leave types are enabled for this period.");
        var workersToAccrue = await repo.ListAccrualWorkersAsync(periodDate, ct);
        if (workersToAccrue.Count == 0)
            throw new DomainException("accrual-no-workers", "No eligible employees exist for this month yet. Add employees before running accrual.");
        var run = new LeaveAccrualRun
        {
            Period = request.Period, Status = "processing", WorkerCount = workersToAccrue.Count,
            RunBySubjectId = actorSubjectId,
        };
        async Task Accrue(CancellationToken transactionCt)
        {
            run = await repo.CreateAccrualRunAsync(run, transactionCt);
            foreach (var worker in workersToAccrue)
            foreach (var type in leaveTypes)
            {
                var days = Math.Round(type.DefaultDaysPerYear / 12m, 4);
                if (days == 0) continue;
                await repo.AddLedgerEntryAsync(new LeaveBalanceLedger
                {
                    WorkerId = worker.Id, LeaveTypeCode = type.Code, Days = days, Reason = "monthly-accrual",
                    ReferenceId = run.Id, ReferenceType = "accrual-run", ForDate = periodDate,
                }, transactionCt);
                run.LedgerEntryCount++;
                run.TotalDaysAccrued += days;
            }
            run.Status = "completed";
            await repo.UpdateAccrualRunAsync(run, transactionCt);
        }
        if (unitOfWork is null) await Accrue(ct);
        else await unitOfWork.ExecuteAsync(Accrue, ct);
        return MapAccrual(run);
    }

    public async Task<LeaveBalanceAdjustmentDto> AdjustLeaveBalanceAsync(LeaveBalanceAdjustmentRequest request,
        string actorSubjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        if (request.Days == 0 || string.IsNullOrWhiteSpace(request.Reason))
            throw new DomainException("leave-adjustment-invalid", "A non-zero day adjustment and reason are required.");
        var forDate = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.ForDate is not null && (!DateOnly.TryParse(request.ForDate, out forDate)
            || forDate > DateOnly.FromDateTime(DateTime.UtcNow)))
            throw new DomainException("leave-adjustment-date-invalid", "Adjustment date must be a valid date no later than today.");
        var worker = await workers.GetByIdAsync(request.WorkerId, ct)
            ?? throw new DomainException("worker-not-found", $"Worker {request.WorkerId} does not exist.");
        _ = await repo.GetLeaveTypeAsync(request.LeaveTypeCode, ct)
            ?? throw new DomainException("leave-type-not-found", $"Leave type {request.LeaveTypeCode} does not exist.");
        var adjustment = new LeaveBalanceAdjustment
        {
            WorkerId = worker.Id, LeaveTypeCode = request.LeaveTypeCode, Days = request.Days,
            Reason = request.Reason.Trim(), AdjustedBySubjectId = actorSubjectId,
        };
        async Task PostAdjustment(CancellationToken transactionCt)
        {
            var ledger = await repo.AddLedgerEntryAsync(new LeaveBalanceLedger
            {
                WorkerId = worker.Id, LeaveTypeCode = request.LeaveTypeCode, Days = request.Days,
                Reason = "manual-adjustment", ReferenceId = adjustment.Id, ReferenceType = "adjustment",
                ForDate = forDate,
            }, transactionCt);
            adjustment.LedgerEntryId = ledger.Id;
            adjustment = await repo.CreateAdjustmentAsync(adjustment, transactionCt);
        }
        if (unitOfWork is null) await PostAdjustment(ct);
        else await unitOfWork.ExecuteAsync(PostAdjustment, ct);
        return new LeaveBalanceAdjustmentDto(adjustment.Id, worker.Id, worker.FullName,
            adjustment.LeaveTypeCode, adjustment.Days, adjustment.Reason, adjustment.AdjustedBySubjectId, adjustment.CreatedAt);
    }

    public async Task<EscalationRunDto> EscalateOverdueAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        return await workflow.EscalateOverdueAsync(ct);
    }

    public async Task<TimeOperationsHistoryDto> GetOperationsHistoryAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var imports = (await repo.ListImportBatchesAsync(ct)).Select(batch => new AttendanceImportHistoryDto(
            batch.Id, batch.FileName, batch.Status, batch.RowCount, batch.ImportedCount,
            batch.UpdatedCount, batch.RejectedCount, batch.ImportedBySubjectId, batch.CreatedAt)).ToList();
        var accruals = (await repo.ListAccrualRunsAsync(ct)).Select(MapAccrual).ToList();
        var adjustments = (await repo.ListAdjustmentsAsync(ct)).Select(adjustment => new LeaveBalanceAdjustmentDto(
            adjustment.Id, adjustment.WorkerId, adjustment.Worker?.FullName ?? "",
            adjustment.LeaveTypeCode, adjustment.Days, adjustment.Reason,
            adjustment.AdjustedBySubjectId, adjustment.CreatedAt)).ToList();
        var encashments = (await repo.ListEncashmentsAsync(null, null, ct)).Items
            .Select(e => new LeaveEncashmentHistoryDto(
                e.Id, e.WorkerId, e.Worker?.FullName ?? "", e.LeaveTypeCode, e.Days,
                e.GrossAmount, e.Status, e.CreatedBySubjectId, e.CreatedAt)).ToList();
        var timeAudits = (await repo.ListTimeAuditEntriesAsync(ct)).Select(a => new TimeAuditEntryDto(
            a.Id, a.EntityType, a.EntityId, a.Action, a.ActorSubjectId,
            a.BeforeJson, a.AfterJson, a.CreatedAt)).ToList();
        return new TimeOperationsHistoryDto(imports, accruals, adjustments, encashments, timeAudits);
    }

    // ===================== M41 Gap 6a: leave encashment =====================
    /// <summary>Standard workdays divisor used to derive a daily rate from the
    /// basic monthly salary. 26 days matches the common Zambian payroll
    /// convention (monthly / 26).</summary>
    internal const decimal DailyRateDivisor = 26m;

    public async Task<List<LeaveEncashmentRequestDto>> ListEncashmentsAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var (items, _) = await repo.ListEncashmentsAsync(workerId, status, ct);
        // M44 branch scoping: scoped operators see only their branch's encashments.
        if ((scope?.IsScopedToBranch ?? false))
            items = items.Where(e => e.LocationId == scope?.LocationId || e.LocationId == null).ToList();
        return items.Select(MapEncashment).ToList();
    }
    public async Task<LeaveEncashmentRateQuote> GetEncashmentRateAsync(Guid workerId, string leaveTypeCode, decimal days, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin", "manager", "payroll");
        await RequireWorkerScopeAsync(workerId, ct);
        var quote = await ComputeEncashmentQuoteAsync(workerId, days, ct);
        return quote;
    }

    public async Task<LeaveEncashmentRequestDto> CreateEncashmentAsync(LeaveEncashmentCreateRequest request, string actorSubjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin", "manager");
        await RequireWorkerScopeAsync(request.WorkerId, ct);
        if (request.Days <= 0)
            throw new DomainException("encashment-invalid-days", "Encashment days must be greater than zero.");
        var worker = await workers.GetByIdAsync(request.WorkerId, ct)
            ?? throw new DomainException("worker-not-found", $"Worker {request.WorkerId} does not exist.");
        var leaveType = await repo.GetLeaveTypeAsync(request.LeaveTypeCode, ct)
            ?? throw new DomainException("leave-type-not-found", $"Leave type {request.LeaveTypeCode} is not configured.");
        var quote = await ComputeEncashmentQuoteAsync(request.WorkerId, request.Days, ct);
        var balances = await GetBalancesAsync(request.WorkerId, ct);
        var available = balances.FirstOrDefault(b => string.Equals(b.LeaveTypeCode, request.LeaveTypeCode, StringComparison.OrdinalIgnoreCase))?.Available ?? 0;
        if (available < request.Days - 0.0001m)
            throw new DomainException("encashment-insufficient-balance",
                $"Available {leaveType.Name} balance ({available:F1} days) is less than requested for encashment ({request.Days:F1} days).");
        var enc = new LeaveEncashmentRequest
        {
            WorkerId = request.WorkerId,
            LeaveTypeCode = request.LeaveTypeCode,
            Days = request.Days,
            MonthlyRate = quote.MonthlyBasic,
            GrossAmount = quote.EstimatedGross,
            Note = request.Note ?? "",
            // M44 branch scoping: encashments inherit the operator's work scope!.
            LocationId = (scope?.IsScopedToBranch ?? false) ? scope?.LocationId : null,
            Status = "submitted",
            CreatedBySubjectId = actorSubjectId,
        };
        var created = await repo.CreateEncashmentAsync(enc, ct);
        await workflow.OpenAsync("leave-encashment", created.Id, created.WorkerId,
            JsonSerializer.Serialize(new { created.LeaveTypeCode, created.Days, created.MonthlyRate, created.GrossAmount }), ct);
        return MapEncashment(created);
    }

    public async Task<LeaveEncashmentRequestDto> DecideEncashmentAsync(Guid id, LeaveEncashmentDecideRequest request, string actorSubjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var enc = await repo.GetEncashmentAsync(id, ct)
            ?? throw new DomainException("encashment-not-found", $"Encashment request {id} does not exist.");
        if (enc.Status != "submitted")
            throw new DomainException("encashment-not-reviewable", $"Encashment request is {enc.Status} and cannot be decided.");
        // decisions always flow through the workflow engine so approvals,
        // returns and rejections get the same audit trail as leave requests;
        // the effect applier posts the ledger deduction on approval.
        var actorWorker = await workers.FindBySubjectIdAsync(actorSubjectId, ct);
        var actorId = actorWorker?.Id ?? Guid.Empty;
        if (actorId == Guid.Empty)
            throw new DomainException("encashment-decider-not-linked", "The decider's account is not linked to an employee record.");
        var workflowRequest = await workflow.GetOpenBySubjectAsync("leave-encashment", enc.WorkerId, ct);
        if (workflowRequest is null)
            throw new DomainException("encashment-no-workflow", "No open approval workflow exists for this encashment request.");
        await workflow.DecideAsync(workflowRequest.Id, actorId, new WorkflowDecisionRequest(request.Action, request.Reason), ct);
        var decided = await repo.GetEncashmentAsync(id, ct)
            ?? throw new DomainException("encashment-not-found", $"Encashment request {id} does not exist.");
        return MapEncashment(decided);
    }

    /// <summary>Worker's basic monthly amount and derived daily rate, or a
    /// zero-rate quote when no basic component exists on the open profile.</summary>
    private async Task<LeaveEncashmentRateQuote> ComputeEncashmentQuoteAsync(Guid workerId, decimal days, CancellationToken ct)
    {
        var profile = payroll is null ? null : await payroll.FindOpenProfileAsync(workerId, ct);
        var currency = profile?.PayGroup?.Currency ?? "ZMW";
        var basic = profile?.ComponentValues
            .FirstOrDefault(v => string.Equals(v.Component?.Code, "basic", StringComparison.OrdinalIgnoreCase))?.Amount ?? 0;
        var daily = basic > 0 ? Math.Round(basic / DailyRateDivisor, 2) : 0;
        var gross = Math.Round(days / DailyRateDivisor * basic, 2);
        return new LeaveEncashmentRateQuote(basic, daily, gross, currency);
    }

    private static LeaveEncashmentRequestDto MapEncashment(LeaveEncashmentRequest e) =>
        new(e.Id, e.WorkerId, e.Worker?.FullName ?? "", e.Worker?.EmployeeNo,
            e.LeaveTypeCode, e.Days, e.MonthlyRate, e.GrossAmount, e.Note, e.Status,
            e.CreatedBySubjectId, e.DecisionReason, e.CreatedAt, e.LocationId);

    public async Task<AttendanceCorrectionDto> DecideCorrectionAsync(Guid id, TimeDecisionRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager");
        var c = await repo.GetCorrectionAsync(id, ct) ?? throw new DomainException("correction-not-found", $"Attendance correction {id} does not exist.");
        if (c.Status != "submitted" && c.Status != "in-review")
            throw new DomainException("correction-not-reviewable", $"Correction is {c.Status} and cannot be decided.");

        switch (request.Action.ToLowerInvariant())
        {
            case "approve":
                c.Status = "approved";
                // apply the proposed values to the underlying attendance record (or create one)
                var existing = await repo.GetAttendanceAsync(c.WorkerId, c.WorkDate, ct);
                var beforeJson = existing is null ? null : AttendanceSnapshot(existing);
                if (existing is null)
                {
                    var corrected = new AttendanceRecord
                    {
                        WorkerId = c.WorkerId, WorkDate = c.WorkDate,
                        ClockIn = c.ProposedClockIn, ClockOut = c.ProposedClockOut,
                        Source = "corrected", DerivedStatus = c.ProposedStatus ?? "unknown",
                        // M44: inherit the branch of the correction that produced it.
                        LocationId = c.LocationId,
                    };
                    await ApplyHoursAsync(corrected, ct);
                    await repo.CreateAttendanceAsync(corrected, ct);
                    await WriteTimeAuditAsync("time.attendance", corrected.Id.ToString("D"), corrected.WorkerId,
                        "correction-create", authz.CurrentSubjectId ?? "system", beforeJson,
                        AttendanceSnapshot(corrected), ct);
                    await RecalculateWeeklyOvertimeAsync(c.WorkerId, c.WorkDate, ct);
                }
                else
                {
                    if (c.ProposedClockIn.HasValue) existing.ClockIn = c.ProposedClockIn;
                    if (c.ProposedClockOut.HasValue) existing.ClockOut = c.ProposedClockOut;
                    if (c.ProposedStatus is not null) existing.DerivedStatus = c.ProposedStatus;
                    existing.Source = "corrected";
                    await ApplyHoursAsync(existing, ct);
                    await repo.UpdateAttendanceAsync(existing, ct);
                    await WriteTimeAuditAsync("time.attendance", existing.Id.ToString("D"), existing.WorkerId,
                        "correction-update", authz.CurrentSubjectId ?? "system", beforeJson,
                        AttendanceSnapshot(existing), ct);
                    await RecalculateWeeklyOvertimeAsync(c.WorkerId, c.WorkDate, ct);
                }
                break;
            case "return":
                c.Status = "returned";
                c.RejectionReason = request.Reason;
                break;
            case "reject":
                c.Status = "rejected";
                c.RejectionReason = request.Reason;
                break;
            default:
                throw new DomainException("correction-invalid-decision",
                    "Decision action must be approve, return or reject.");
        }
        var decided = await repo.UpdateCorrectionAsync(c, ct);
        return new AttendanceCorrectionDto(decided.Id, decided.WorkerId, decided.Worker?.FullName ?? "",
            decided.WorkDate.ToString(), decided.IssueType, decided.ProposedClockIn?.ToString(),
            decided.ProposedClockOut?.ToString(), decided.ProposedStatus, decided.Reason,
            decided.Status, decided.CreatedAt, decided.LocationId);
    }

    public async Task<LeaveRequestDto> DecideLeaveAsync(Guid id, TimeDecisionRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager");
        var lr = await repo.GetLeaveRequestAsync(id, ct) ?? throw new DomainException("leave-not-found", $"Leave request {id} does not exist.");
        if (lr.Status != "submitted" && lr.Status != "in-review")
            throw new DomainException("leave-not-reviewable", $"Leave request is {lr.Status} and cannot be decided.");

        LeaveRequest decided = lr;
        async Task DecideAndEnqueue(CancellationToken transactionCt)
        {
            switch (request.Action.ToLowerInvariant())
            {
                case "approve":
                    lr.Status = "approved";
                    lr.BalanceReserved = true;
                    // convert the open reservation into a permanent (taken) ledger deduction
                    await repo.ConvertReservationAsync(lr.Id, transactionCt);
                    break;
                case "return":
                    lr.Status = "returned";
                    lr.RejectionReason = request.Reason;
                    await repo.ReleaseReservationAsync(lr.Id, transactionCt);
                    break;
                case "reject":
                    lr.Status = "rejected";
                    lr.RejectionReason = request.Reason;
                    await repo.ReleaseReservationAsync(lr.Id, transactionCt);
                    break;
                default:
                    throw new DomainException("leave-invalid-decision",
                        "Decision action must be approve, return or reject.");
            }
            decided = await repo.UpdateLeaveRequestAsync(lr, transactionCt);
            if (outbox is null || lr.Worker is null) return;
            await EnqueueLeaveEventAsync(HrmEventTypes.LeaveDecided, lr, lr.Worker, transactionCt);
        }
        if (unitOfWork is null)
            await DecideAndEnqueue(ct);
        else
            await unitOfWork.ExecuteAsync(DecideAndEnqueue, ct);
        return Map(decided);
    }

    private async Task<PunchResultDto> GetOrCreatePunchAsync(Guid workerId, DateOnly date, bool? clockIn, CancellationToken ct)
    {
        var rec = await repo.GetAttendanceAsync(workerId, date, ct);
        var beforeJson = rec is null ? null : AttendanceSnapshot(rec);
        if (rec is null)
        {
            rec = new AttendanceRecord { WorkerId = workerId, WorkDate = date, Source = "self-service",
                // M44: self-service punches inherit the operator's work scope (employees punch at their branch).
                LocationId = (scope?.IsScopedToBranch ?? false) ? scope?.LocationId : null };
            rec = await repo.CreateAttendanceAsync(rec, ct);
        }

        var now = TimeOnly.FromDateTime(DateTime.UtcNow);
        if (clockIn == true && rec.ClockIn is null)
            rec.ClockIn = now;
        if (clockIn == false && rec.ClockOut is null)
            rec.ClockOut = now;

        await ApplyHoursAsync(rec, ct);

        rec = await repo.UpdateAttendanceAsync(rec, ct);
        await WriteTimeAuditAsync("time.attendance", rec.Id.ToString("D"), rec.WorkerId,
            clockIn == true ? "clock-in" : clockIn == false ? "clock-out" : "view-today",
            authz.CurrentSubjectId ?? "system", beforeJson, AttendanceSnapshot(rec), ct);
        await RecalculateWeeklyOvertimeAsync(rec.WorkerId, rec.WorkDate, ct);
        rec = await repo.GetAttendanceAsync(workerId, date, ct) ?? rec;
        var state = rec.ClockIn is null ? "out" : rec.ClockOut is null ? "in" : "done";
        return new PunchResultDto(rec.Id, rec.WorkerId, rec.WorkDate.ToString(),
            rec.ClockIn?.ToString() ?? "", rec.ClockOut?.ToString() ?? "",
            rec.Source, rec.DerivedStatus, rec.TotalHours, state);
    }

    private static string DeriveStatus(TimeOnly? clockIn, TimeOnly? clockOut, string? current)
    {
        if (clockIn.HasValue && clockOut.HasValue) return "present";
        if (clockIn is null && clockOut is null) return "unknown";
        return current is "late" or "early-departure" or "half-day" or "absent" ? current : "present";
    }

    private async Task ApplyHoursAsync(AttendanceRecord record, CancellationToken ct)
    {
        var assignment = await repo.GetShiftAssignmentAsync(record.WorkerId, record.WorkDate, ct);
        var shift = assignment?.Shift;
        record.ShiftId = shift?.Id;
        record.ScheduledHours = shift?.StandardHours ?? 0;
        record.TotalHours = 0;
        record.RegularHours = 0;
        record.OvertimeHours = 0;
        record.OvertimeMultiplier = 0;
        record.OvertimeHourlyDivisor = 208;
        record.OvertimeRuleCode = "ordinary";
        if (record.ClockIn.HasValue && record.ClockOut.HasValue)
        {
            var elapsed = record.ClockOut.Value - record.ClockIn.Value;
            if (elapsed < TimeSpan.Zero) elapsed += TimeSpan.FromDays(1);
            record.TotalHours = Math.Max(0, Math.Round((decimal)elapsed.TotalHours - (shift?.UnpaidBreakMinutes ?? 0) / 60m, 4));

            var calendar = assignment?.Calendar ?? (await repo.ListCalendarsAsync(ct)).FirstOrDefault(c => c.IsDefault);
            var weekend = calendar is not null && IsWeekend(calendar, record.WorkDate);
            var holiday = calendar?.Holidays.Any(h => HolidayDate(h) == record.WorkDate) == true;
            if (holiday)
            {
                record.OvertimeHours = record.TotalHours;
                record.OvertimeMultiplier = shift?.HolidayOvertimeMultiplier ?? 2m;
            }
            else if (weekend)
            {
                record.OvertimeHours = record.TotalHours;
                record.OvertimeMultiplier = shift?.RestDayOvertimeMultiplier ?? 2m;
            }
            else
            {
                var threshold = shift?.DailyOvertimeThresholdHours ?? record.TotalHours;
                record.RegularHours = Math.Min(record.TotalHours, threshold);
                record.OvertimeHours = Math.Max(0, record.TotalHours - threshold);
                record.OvertimeMultiplier = record.OvertimeHours > 0 ? shift?.WeekdayOvertimeMultiplier ?? 1.5m : 0;
            }
        }
        record.DerivedStatus = DeriveStatus(record.ClockIn, record.ClockOut, record.DerivedStatus);
        if (record.OvertimeHours > 0 && !record.OvertimePayrollRunId.HasValue)
        {
            // Any newly derived or changed overtime must be reviewed again.
            record.OvertimeStatus = "pending";
            record.OvertimeDecisionReason = null;
            record.OvertimeDecidedBySubjectId = null;
            record.OvertimeDecidedAt = null;
        }
        else if (record.OvertimeHours <= 0 && !record.OvertimePayrollRunId.HasValue)
        {
            record.OvertimeStatus = "none";
            record.OvertimeDecisionReason = null;
            record.OvertimeDecidedBySubjectId = null;
            record.OvertimeDecidedAt = null;
        }
        if (shift is not null && record.ClockIn.HasValue && record.DerivedStatus == "present" && record.ClockIn > shift.StartTime)
            record.DerivedStatus = "late";
        if (shift is not null && record.ClockOut.HasValue && record.DerivedStatus == "present" && record.ClockOut < shift.EndTime)
            record.DerivedStatus = "early-departure";
    }

    private async Task RecalculateWeeklyOvertimeAsync(Guid workerId, DateOnly affectedDate, CancellationToken ct)
    {
        var weekStart = StartOfWeek(affectedDate);
        var weekEnd = weekStart.AddDays(6);
        var records = await repo.ListAttendanceForWorkerRangeAsync(workerId, weekStart, weekEnd, ct);
        if (records.Count == 0) return;

        var profile = payroll is null ? null : await payroll.FindOpenProfileAsync(workerId, ct);
        var category = profile?.OvertimeCategory ?? "ordinary";
        var weeklyThreshold = profile?.WeeklyOvertimeThresholdHours > 0
            ? profile.WeeklyOvertimeThresholdHours
            : (category == "watchperson-guard" ? 60m : 48m);
        var hourlyDivisor = profile?.MonthlyOvertimeDivisor > 0
            ? profile.MonthlyOvertimeDivisor
            : (category == "watchperson-guard" ? 240m : 208m);

        decimal regularWeekHours = 0;
        var defaultCalendar = (await repo.ListCalendarsAsync(ct)).FirstOrDefault(c => c.IsDefault);
        foreach (var record in records.OrderBy(r => r.WorkDate).ThenBy(r => r.ClockIn ?? TimeOnly.MinValue))
        {
            if (record.OvertimePayrollRunId.HasValue) continue;
            var before = (record.RegularHours, record.OvertimeHours, record.OvertimeMultiplier, record.OvertimeHourlyDivisor, record.OvertimeRuleCode);

            record.RegularHours = 0;
            record.OvertimeHours = 0;
            record.OvertimeMultiplier = 0;
            record.OvertimeHourlyDivisor = hourlyDivisor;
            record.OvertimeRuleCode = category;

            if (record.ClockIn.HasValue && record.ClockOut.HasValue && record.TotalHours > 0)
            {
                var assignment = await repo.GetShiftAssignmentAsync(record.WorkerId, record.WorkDate, ct);
                var shift = assignment?.Shift;
                var calendar = assignment?.Calendar ?? defaultCalendar;
                var weekend = calendar is not null && IsWeekend(calendar, record.WorkDate);
                var holiday = calendar?.Holidays.Any(h => HolidayDate(h) == record.WorkDate) == true;

                if (holiday)
                {
                    record.OvertimeHours = record.TotalHours;
                    record.OvertimeMultiplier = shift?.HolidayOvertimeMultiplier ?? 2m;
                }
                else if (weekend)
                {
                    record.OvertimeHours = record.TotalHours;
                    record.OvertimeMultiplier = shift?.RestDayOvertimeMultiplier ?? 2m;
                }
                else
                {
                    var dailyThreshold = shift?.DailyOvertimeThresholdHours > 0 ? shift.DailyOvertimeThresholdHours : 8m;
                    var remainingWeeklyRegular = Math.Max(0, weeklyThreshold - regularWeekHours);
                    var regularCap = Math.Min(dailyThreshold, remainingWeeklyRegular);
                    record.RegularHours = Math.Min(record.TotalHours, regularCap);
                    record.OvertimeHours = Math.Max(0, record.TotalHours - record.RegularHours);
                    if (record.OvertimeHours > 0)
                        record.OvertimeMultiplier = shift?.WeekdayOvertimeMultiplier ?? 1.5m;
                    regularWeekHours += record.RegularHours;
                }
            }

            var changed = before.RegularHours != record.RegularHours
                || before.OvertimeHours != record.OvertimeHours
                || before.OvertimeMultiplier != record.OvertimeMultiplier
                || before.OvertimeHourlyDivisor != record.OvertimeHourlyDivisor
                || before.OvertimeRuleCode != record.OvertimeRuleCode;
            if (record.OvertimeHours > 0 && changed)
            {
                record.OvertimeStatus = "pending";
                record.OvertimeDecisionReason = null;
                record.OvertimeDecidedBySubjectId = null;
                record.OvertimeDecidedAt = null;
            }
            else if (record.OvertimeHours <= 0)
            {
                record.OvertimeStatus = "none";
                record.OvertimeDecisionReason = null;
                record.OvertimeDecidedBySubjectId = null;
                record.OvertimeDecidedAt = null;
            }
            await repo.UpdateAttendanceAsync(record, ct);
            if (changed)
            {
                await WriteTimeAuditAsync("time.overtime", record.Id.ToString("D"), record.WorkerId,
                    "weekly-recalculation", "system", null,
                    AttendanceSnapshot(record), ct);
            }
        }
    }

    private static string WeekKey(Guid workerId, DateOnly date) => $"{workerId:D}|{StartOfWeek(date):yyyy-MM-dd}";

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private static bool IsWeekend(WorkCalendar calendar, DateOnly date)
    {
        var day = date.DayOfWeek.ToString().ToLowerInvariant()[..3];
        return calendar.WeekendDays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(w => string.Equals(w, day, StringComparison.OrdinalIgnoreCase));
    }

    private static DateOnly HolidayDate(PublicHoliday holiday)
        => holiday.ObservedOn is not null && DateOnly.TryParse(holiday.ObservedOn, out var observed)
            ? observed : holiday.HolidayDate;

    private static string NormalizeImportedOvertimeStatus(string? status, bool markApproved)
    {
        if (string.IsNullOrWhiteSpace(status)) return markApproved ? "approved" : "pending";
        var normalized = status.Trim().ToLowerInvariant();
        return normalized is "approved" or "pending" or "rejected" ? normalized
            : throw new DomainException("overtime-import-status-invalid", "Overtime status must be pending, approved, or rejected.");
    }

    private async Task WriteTimeAuditAsync(string entityType, string entityId, Guid? workerId,
        string action, string actorSubjectId, string? beforeJson, string? afterJson, CancellationToken ct)
    {
        await repo.AddTimeAuditEntryAsync(new AuditEntry
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            ActorSubjectId = string.IsNullOrWhiteSpace(actorSubjectId) ? "system" : actorSubjectId,
            CorrelationId = workerId?.ToString("D"),
        }, ct);
    }

    private static string AttendanceSnapshot(AttendanceRecord record) => JsonSerializer.Serialize(new
    {
        record.Id,
        record.WorkerId,
        record.WorkDate,
        record.ClockIn,
        record.ClockOut,
        record.Source,
        record.DerivedStatus,
        record.TotalHours,
        record.RegularHours,
        record.OvertimeHours,
        record.OvertimeMultiplier,
        record.OvertimeHourlyDivisor,
        record.OvertimeRuleCode,
        record.OvertimeStatus,
        record.OvertimeDecisionReason,
        record.OvertimeDecidedBySubjectId,
        record.OvertimeDecidedAt,
        record.OvertimePayrollRunId,
        record.ImportBatchId,
        record.LocationId,
    });

    // ===================== M16: self-service leave =====================

    public async Task<MyLeaveDto> MyLeaveAsync(string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin", "manager", "payroll");
        if (string.IsNullOrEmpty(subjectId))
            throw new DomainException("no-subject-claim", "The request carries no identity claim.");
        var worker = await workers.FindBySubjectIdAsync(subjectId, ct);
        if (worker is null)
            return new MyLeaveDto(Guid.Empty, "", null, false, [], []);
        var balances = await GetBalancesAsync(worker.Id, ct);
        var (items, _) = await repo.ListLeaveRequestsAsync(worker.Id, null, ct);
        var requests = items.Select(r => new SelfLeaveRequestDto(r.Id, r.LeaveTypeCode, r.StartDate.ToString(),
            r.EndDate.ToString(), r.RequestedDays, r.Status, r.RejectionReason, r.CrossesCutoff, r.CreatedAt)).ToList();
        return new MyLeaveDto(worker.Id, worker.FullName, worker.EmployeeNo, true, balances, requests);
    }

    // M35: self-service dashboard — today's punch + leave balances in one call.
    public async Task<SelfDashboardDto> MyDashboardAsync(string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin", "manager", "payroll");
        if (string.IsNullOrEmpty(subjectId))
            throw new DomainException("no-subject-claim", "The request carries no identity claim.");
        var worker = await workers.FindBySubjectIdAsync(subjectId, ct);
        if (worker is null)
            return new SelfDashboardDto(Guid.Empty, "", null, false, null, []);
        PunchResultDto? today = null;
        try { today = await GetTodayAsync(worker.Id, ct); }
        catch { /* not clocked in yet — leave as null */ }
        var balances = await GetBalancesAsync(worker.Id, ct);
        return new SelfDashboardDto(worker.Id, worker.FullName, worker.EmployeeNo, true, today, balances);
    }

    public async Task<LeaveRequestDto> CancelLeaveAsync(Guid id, string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin");
        if (string.IsNullOrEmpty(subjectId))
            throw new DomainException("no-subject-claim", "The request carries no identity claim.");
        var worker = await workers.FindBySubjectIdAsync(subjectId, ct)
            ?? throw new DomainException("no-worker-linked", "No worker record is linked to your account.");
        var lr = await repo.GetLeaveRequestAsync(id, ct)
            ?? throw new DomainException("leave-not-found", $"Leave request {id} does not exist.");
        if (lr.WorkerId != worker.Id)
            throw new DomainException("leave-not-owned", "You can only cancel your own leave requests.");
        if (lr.Status is not ("submitted" or "in-review" or "returned"))
            throw new DomainException("leave-not-cancellable",
                $"Leave request is {lr.Status} and cannot be cancelled. Only open requests can be cancelled.");

        async Task CancelAndEnqueue(CancellationToken transactionCt)
        {
            lr.Status = "cancelled";
            await repo.UpdateLeaveRequestAsync(lr, transactionCt);
            await repo.ReleaseReservationAsync(lr.Id, transactionCt);
            // close the approval workflow so nothing is left sitting in an approver's queue.
            // the workflow request is keyed on the worker, not the leave id
            var wfReq = await workflow.GetOpenBySubjectAsync("leave", worker.Id, transactionCt);
            if (wfReq is not null)
                await workflow.CancelAsync(wfReq.Id, transactionCt);
            if (outbox is null) return;
            await EnqueueLeaveEventAsync(HrmEventTypes.LeaveCancelled, lr, worker, transactionCt);
        }
        if (unitOfWork is null)
            await CancelAndEnqueue(ct);
        else
            await unitOfWork.ExecuteAsync(CancelAndEnqueue, ct);
        return Map(lr);
    }

    private async Task EnqueueLeaveEventAsync(string eventType, LeaveRequest leave, Worker worker, CancellationToken ct)
    {
        if (outbox is null) return;
        await outbox.EnqueueAsync(
            eventType,
            worker.SubjectId ?? worker.Id.ToString("D"),
            new
            {
                leave_id = leave.Id.ToString("D"),
                worker_id = worker.Id.ToString("D"),
                leave_type_code = leave.LeaveTypeCode,
                start_date = leave.StartDate.ToString("yyyy-MM-dd"),
                end_date = leave.EndDate.ToString("yyyy-MM-dd"),
                status = leave.Status,
                email = worker.Email ?? "",
                first_name = worker.FirstName,
                last_name = worker.LastName,
            },
            ct);
    }

    private static LeaveRequestDto Map(LeaveRequest r) => new(
        r.Id, r.WorkerId, r.Worker?.FullName ?? "", r.LeaveTypeCode, r.StartDate.ToString(),
        r.EndDate.ToString(), r.RequestedDays, r.Status, r.BalanceReserved, r.CrossesCutoff, r.CreatedAt, r.LocationId);

    private static AttendanceRecordDto MapAttendance(AttendanceRecord a) => new(
        a.Id, a.WorkerId, a.Worker?.FullName ?? "", a.WorkDate.ToString(),
        a.ClockIn?.ToString(), a.ClockOut?.ToString(), a.Source, a.DerivedStatus, a.TotalHours,
        a.ScheduledHours, a.RegularHours, a.OvertimeHours, a.OvertimeMultiplier, a.ShiftId, a.ImportBatchId,
        a.OvertimeStatus, a.OvertimeDecisionReason, a.OvertimeDecidedBySubjectId,
        a.OvertimeDecidedAt, a.OvertimePayrollRunId, a.OvertimePayrollLineId, a.Worker?.EmployeeNo ?? "");

    private static ShiftDto MapShift(ShiftDefinition shift) => new(
        shift.Id, shift.Code, shift.Name, shift.StartTime.ToString(), shift.EndTime.ToString(),
        shift.UnpaidBreakMinutes, shift.StandardHours, shift.DailyOvertimeThresholdHours,
        shift.WeekdayOvertimeMultiplier, shift.RestDayOvertimeMultiplier,
        shift.HolidayOvertimeMultiplier, shift.IsActive);

    private static LeaveAccrualRunDto MapAccrual(LeaveAccrualRun run) => new(
        run.Id, run.Period, run.Status, run.WorkerCount, run.LedgerEntryCount,
        run.TotalDaysAccrued, run.RunBySubjectId, run.CreatedAt);
}
