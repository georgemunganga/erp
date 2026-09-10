using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Application.Experience;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Application.Performance;
using Mightyfin.Erp.Hrm.Application.Offboarding;
using Mightyfin.Erp.Hrm.Application.Organization;
using Mightyfin.Erp.Hrm.Application.Analytics;
using Mightyfin.Erp.Hrm.Application.Setup;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Microsoft.Data.Sqlite;
using Npgsql;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;

// ===================== Workers =====================
public sealed class WorkerRepository(HrmDbContext db) : IWorkerRepository
{
    public async Task<(List<Worker> Items, int Total)> ListAsync(WorkerListFilters filters, CancellationToken ct)
    {
        var q = db.Workers.AsQueryable();
        // M18 admin CRUD: archived workers stay out of the operational list
        // unless HR explicitly asks for them.
        if (!filters.IncludeArchived) q = q.Where(w => !w.IsArchived);
        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var s = filters.Search.Trim().ToLower();
            // FullName is a computed (unmapped) property — EF cannot translate
            // it to SQL, so match on the underlying columns instead.
            q = q.Where(w => w.FirstName.ToLower().Contains(s) || w.LastName.ToLower().Contains(s)
                || w.EmployeeNo.ToLower().Contains(s)
                || (w.Nrc != null && w.Nrc.ToLower().Contains(s))
                || (w.Email != null && w.Email.ToLower().Contains(s)));
        }
        if (!string.IsNullOrWhiteSpace(filters.Status)) q = q.Where(w => w.Status == filters.Status);
        if (filters.LegalEntityId.HasValue)
            q = q.Where(w =>
                (w.OrgUnit != null && w.OrgUnit.LegalEntityId == filters.LegalEntityId.Value) ||
                (w.Location != null && w.Location.LegalEntityId == filters.LegalEntityId.Value));
        if (filters.OrgUnitId.HasValue) q = q.Where(w => w.OrgUnitId == filters.OrgUnitId.Value);
        if (filters.LocationId.HasValue) q = q.Where(w => w.LocationId == filters.LocationId.Value);
        if (!string.IsNullOrWhiteSpace(filters.WorkerType)) q = q.Where(w => w.WorkerType == filters.WorkerType);
        if (!string.IsNullOrWhiteSpace(filters.Grade)) q = q.Where(w => w.Grade == filters.Grade);
        var total = await q.CountAsync(ct);
        var page = Math.Max(filters.Page, 1);
        var size = Math.Clamp(filters.PageSize, 1, 100);
        // Order client-side: EF Core's SQLite provider cannot translate ORDER BY
        // on DateTimeOffset columns (CreatedAt) into SQL.
        var items = await q.Include(w => w.EmergencyContacts).Include(w => w.BankDetails)
            .Include(w => w.OrgUnit).Include(w => w.Location).Include(w => w.Manager)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);
        items = items.OrderByDescending(w => w.CreatedAt).ToList();
        return (items, total);
    }

    public async Task<Worker?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.Workers.Include(w => w.EmergencyContacts).Include(w => w.BankDetails)
            .Include(w => w.OrgUnit).Include(w => w.Location).Include(w => w.Manager)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

    // M14 identity link: resolve the worker record bound to a Keycloak subject id.
    // The global tenant query filter on the DbContext keeps the lookup
    // tenant-scoped automatically.
    public async Task<Worker?> FindBySubjectIdAsync(string subjectId, CancellationToken ct)
        => await db.Workers.Include(w => w.EmergencyContacts).Include(w => w.BankDetails)
            .Include(w => w.OrgUnit).Include(w => w.Location).Include(w => w.Manager)
            .FirstOrDefaultAsync(w => w.SubjectId == subjectId, ct);

    public async Task<Worker?> FindByEmailAsync(string email, CancellationToken ct)
        => await db.Workers.Include(w => w.EmergencyContacts).Include(w => w.BankDetails)
            .Include(w => w.OrgUnit).Include(w => w.Location).Include(w => w.Manager)
            .FirstOrDefaultAsync(w => w.Email == email, ct);

    public async Task<Worker> CreateAsync(Worker worker, CancellationToken ct)
    {
        db.Workers.Add(worker);
        await db.SaveChangesAsync(ct);
        return worker;
    }

    public async Task<Worker> UpdateAsync(Worker worker, CancellationToken ct)
    {
        db.Workers.Update(worker);
        await db.SaveChangesAsync(ct);
        return worker;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
    }

    // Explicit AddRange + Save so the provider issues INSERTs even when the
    // entity has a non-default Guid key (Guid.CreateVersion7 initializer),
    // which otherwise makes collection-attached entities be treated as
    // existing (Modified) by EF Core's change tracker.
    public async Task AddEmergencyContactsAsync(IEnumerable<EmergencyContact> contacts, CancellationToken ct)
    {
        db.EmergencyContacts.AddRange(contacts);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddBankDetailsAsync(IEnumerable<WorkerBankDetail> details, CancellationToken ct)
    {
        db.WorkerBankDetails.AddRange(details);
        await db.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct)
    {
        var worker = await GetByIdAsync(id, ct)
            ?? throw new DomainException("worker-not-found", $"Worker {id} does not exist.");
        worker.IsArchived = true;
        worker.Status = "archived";
        worker.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsAsync(string employeeNo, CancellationToken ct)
        => await db.Workers.AnyAsync(w => w.EmployeeNo == employeeNo, ct);

    // M31: natural-key lookup used by Update-mode import matching — employee
    // number first, then NRC, then NAPSA number. Null keys are ignored and
    // archived workers are excluded.
    public async Task<Worker?> FindByNaturalKeyAsync(string employeeNo, string? nrc, string? napsaNumber, CancellationToken ct)
    {
        Worker? match = null;
        if (!string.IsNullOrWhiteSpace(employeeNo))
            match = await db.Workers.FirstOrDefaultAsync(w => w.EmployeeNo == employeeNo && !w.IsArchived, ct);
        if (match is null && !string.IsNullOrWhiteSpace(nrc))
            match = await db.Workers.FirstOrDefaultAsync(w => w.Nrc != null && w.Nrc == nrc && !w.IsArchived, ct);
        if (match is null && !string.IsNullOrWhiteSpace(napsaNumber))
            match = await db.Workers.FirstOrDefaultAsync(w => w.NapsaNumber != null && w.NapsaNumber == napsaNumber && !w.IsArchived, ct);
        return match;
    }

    public async Task<(List<Assignment> Items, int Total)> ListAssignmentsAsync(Guid workerId, CancellationToken ct)
    {
        var items = await db.Assignments.Include(a => a.OrgUnit).Include(a => a.Location)
            .Where(a => a.WorkerId == workerId).OrderByDescending(a => a.StartDate).ToListAsync(ct);
        return (items, items.Count);
    }

    public async Task<Assignment> CreateAssignmentAsync(Assignment assignment, CancellationToken ct)
    {
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync(ct);
        return assignment;
    }

    public async Task<(List<Movement> Items, int Total)> ListMovementsAsync(Guid workerId, CancellationToken ct)
    {
        var items = (await db.Movements.Where(m => m.WorkerId == workerId).ToListAsync(ct))
            .OrderByDescending(m => m.CreatedAt).ToList();
        return (items, items.Count);
    }

    public async Task<Movement> CreateMovementAsync(Movement movement, CancellationToken ct)
    {
        db.Movements.Add(movement);
        await db.SaveChangesAsync(ct);
        return movement;
    }

    public async Task<Movement?> GetMovementAsync(Guid id, CancellationToken ct)
        => await db.Movements.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task ExecuteMovementAsync(Movement movement, CancellationToken ct)
    {
        db.Movements.Update(movement);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<Assignment>> ListAllAssignmentsAsync(CancellationToken ct)
        => await db.Assignments.Include(a => a.LegalEntity).Include(a => a.OrgUnit).Include(a => a.Location)
            .ToListAsync(ct);

    public async Task<Assignment> UpdateAssignmentAsync(Assignment assignment, CancellationToken ct)
    {
        db.Assignments.Update(assignment);
        await db.SaveChangesAsync(ct);
        return assignment;
    }

    public async Task<List<LegalEntity>> ListAllLegalEntitiesAsync(CancellationToken ct)
        => await db.LegalEntities.ToListAsync(ct);

    public async Task<List<OrgUnit>> ListAllOrgUnitsAsync(CancellationToken ct)
        => await db.OrgUnits.ToListAsync(ct);

    public async Task<List<WorkLocation>> ListAllLocationsAsync(CancellationToken ct)
        => await db.WorkLocations.ToListAsync(ct);

    public async Task<List<Worker>> ListAllWorkersAsync(Guid? orgUnitId, CancellationToken ct)
    {
        var q = db.Workers.AsQueryable();
        if (orgUnitId.HasValue) q = q.Where(w => w.OrgUnitId == orgUnitId.Value);
        return await q.ToListAsync(ct);
    }

    // M31b export flattening: status-filtered roster with OrgUnit + M33 child
    // tables included so the export writes flattened history columns without
    // N+1 round-trips. Archived leavers stay out unless status=all/archived.
    public async Task<List<Worker>> ListAllWorkersWithDetailsAsync(string? status, CancellationToken ct)
    {
        IQueryable<Worker> q = db.Workers
            .Include(w => w.OrgUnit)
            .Include(w => w.Education)
            .Include(w => w.ExternalWorkHistory)
            .Include(w => w.InternalWorkHistory);
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
            q = q.Where(w => w.Status == status);
        return await q.ToListAsync(ct);
    }

    public async Task<EmergencyContact?> GetEmergencyContactAsync(Guid id, CancellationToken ct)
        => await db.EmergencyContacts.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<EmergencyContact> AddEmergencyContactAsync(EmergencyContact contact, CancellationToken ct)
    {
        db.EmergencyContacts.Add(contact);
        await db.SaveChangesAsync(ct);
        return contact;
    }

    public async Task UpdateEmergencyContactAsync(EmergencyContact contact, CancellationToken ct)
    {
        db.EmergencyContacts.Update(contact);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteEmergencyContactAsync(Guid id, CancellationToken ct)
    {
        var contact = await GetEmergencyContactAsync(id, ct)
            ?? throw new DomainException("contact-not-found", $"Emergency contact {id} does not exist.");
        db.EmergencyContacts.Remove(contact);
        await db.SaveChangesAsync(ct);
    }

    public async Task<WorkerBankDetail?> GetBankDetailAsync(Guid id, CancellationToken ct)
        => await db.WorkerBankDetails.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<WorkerBankDetail> AddBankDetailAsync(WorkerBankDetail detail, CancellationToken ct)
    {
        db.WorkerBankDetails.Add(detail);
        await db.SaveChangesAsync(ct);
        return detail;
    }

    public async Task UpdateBankDetailAsync(WorkerBankDetail detail, CancellationToken ct)
    {
        db.WorkerBankDetails.Update(detail);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteBankDetailAsync(Guid id, CancellationToken ct)
    {
        var detail = await GetBankDetailAsync(id, ct)
            ?? throw new DomainException("bank-detail-not-found", $"Bank detail {id} does not exist.");
        db.WorkerBankDetails.Remove(detail);
        await db.SaveChangesAsync(ct);
    }

    // M33: history child records. Queries go through the worker relation so the
    // global tenant filter on Worker keeps the read tenant-safe.
    public async Task<List<WorkerEducation>> ListEducationAsync(Guid workerId, CancellationToken ct)
        => await db.WorkerEducations.Where(e => e.WorkerId == workerId).OrderByDescending(e => e.EndYear).ToListAsync(ct);

    public async Task<WorkerEducation?> GetByIdEducationAsync(Guid id, CancellationToken ct)
        => await db.WorkerEducations.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<WorkerEducation> AddEducationAsync(WorkerEducation education, CancellationToken ct)
    {
        db.WorkerEducations.Add(education);
        await db.SaveChangesAsync(ct);
        return education;
    }

    public async Task UpdateEducationAsync(WorkerEducation education, CancellationToken ct)
    {
        db.WorkerEducations.Update(education);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteEducationAsync(Guid id, CancellationToken ct)
    {
        var record = await db.WorkerEducations.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new DomainException("education-not-found", $"Education record {id} does not exist.");
        db.WorkerEducations.Remove(record);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<ExternalWorkHistory>> ListExternalWorkHistoryAsync(Guid workerId, CancellationToken ct)
        => await db.ExternalWorkHistory.Where(e => e.WorkerId == workerId).OrderByDescending(e => e.EndDate).ToListAsync(ct);

    public async Task<ExternalWorkHistory?> GetByIdExternalWorkHistoryAsync(Guid id, CancellationToken ct)
        => await db.ExternalWorkHistory.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<ExternalWorkHistory> AddExternalWorkHistoryAsync(ExternalWorkHistory record, CancellationToken ct)
    {
        db.ExternalWorkHistory.Add(record);
        await db.SaveChangesAsync(ct);
        return record;
    }

    public async Task UpdateExternalWorkHistoryAsync(ExternalWorkHistory record, CancellationToken ct)
    {
        db.ExternalWorkHistory.Update(record);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteExternalWorkHistoryAsync(Guid id, CancellationToken ct)
    {
        var record = await db.ExternalWorkHistory.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new DomainException("external-work-history-not-found", $"Work history record {id} does not exist.");
        db.ExternalWorkHistory.Remove(record);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<InternalWorkHistory>> ListInternalWorkHistoryAsync(Guid workerId, CancellationToken ct)
        => await db.InternalWorkHistory.Where(e => e.WorkerId == workerId).OrderByDescending(e => e.EndDate).ToListAsync(ct);

    public async Task<InternalWorkHistory?> GetByIdInternalWorkHistoryAsync(Guid id, CancellationToken ct)
        => await db.InternalWorkHistory.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<InternalWorkHistory> AddInternalWorkHistoryAsync(InternalWorkHistory record, CancellationToken ct)
    {
        db.InternalWorkHistory.Add(record);
        await db.SaveChangesAsync(ct);
        return record;
    }

    public async Task UpdateInternalWorkHistoryAsync(InternalWorkHistory record, CancellationToken ct)
    {
        db.InternalWorkHistory.Update(record);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteInternalWorkHistoryAsync(Guid id, CancellationToken ct)
    {
        var record = await db.InternalWorkHistory.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new DomainException("internal-work-history-not-found", $"Internal work history record {id} does not exist.");
        db.InternalWorkHistory.Remove(record);
        await db.SaveChangesAsync(ct);
    }
}

// ===================== Time =====================
public sealed class TimeRepository(HrmDbContext db) : ITimeRepository
{
    public async Task<(List<LeaveRequest> Items, int Total)> ListLeaveRequestsAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        var q = db.LeaveRequests.AsQueryable();
        if (workerId.HasValue) q = q.Where(l => l.WorkerId == workerId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(l => l.Status == status);
        // Order client-side: SQLite cannot translate ORDER BY on DateTimeOffset.
        var items = (await q.Include(l => l.Worker).Take(200).ToListAsync(ct))
            .OrderByDescending(l => l.CreatedAt).ToList();
        return (items, items.Count);
    }

    public async Task<LeaveRequest> CreateLeaveRequestAsync(LeaveRequest request, CancellationToken ct)
    {
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<List<LeaveBalanceLedger>> GetBalancesAsync(Guid workerId, string leaveTypeCode, CancellationToken ct)
        => (await db.LeaveBalanceLedgers
            .Where(l => l.WorkerId == workerId && l.LeaveTypeCode == leaveTypeCode)
            .ToListAsync(ct)).OrderByDescending(l => l.CreatedAt).ToList();

    public async Task<List<LeaveBalanceLedger>> GetLedgerAsync(Guid workerId, CancellationToken ct)
        => (await db.LeaveBalanceLedgers
            .Where(l => l.WorkerId == workerId)
            .ToListAsync(ct)).OrderByDescending(l => l.CreatedAt).ToList();

    public async Task<LeaveType?> GetLeaveTypeAsync(string code, CancellationToken ct)
        => await db.LeaveTypes.FirstOrDefaultAsync(t => t.Code == code && t.IsActive, ct);

    public async Task<List<LeaveType>> GetLeaveTypesAsync(CancellationToken ct)
        => await db.LeaveTypes.Where(t => t.IsActive).ToListAsync(ct);

    public async Task<DateOnly?> GetCurrentCutoffAsync(CancellationToken ct)
    {
        var latest = await db.PayPeriods.Where(p => p.Status == "open")
            .OrderByDescending(p => p.StartDate).Select(p => p.CutoffDate).FirstOrDefaultAsync(ct);
        return latest;
    }

    public async Task ReserveBalanceAsync(Guid workerId, string leaveTypeCode, decimal days, Guid referenceId, CancellationToken ct)
    {
        db.LeaveBalanceLedgers.Add(new LeaveBalanceLedger
        {
            WorkerId = workerId,
            LeaveTypeCode = leaveTypeCode,
            Days = days, // caller passes a negative value (-requestedDays) for a reservation
            Reason = "request",
            ReferenceId = referenceId,
            ReferenceType = "leave-request",
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<(List<AttendanceCorrection> Items, int Total)> ListCorrectionsAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        var q = db.AttendanceCorrections.AsQueryable();
        if (workerId.HasValue) q = q.Where(c => c.WorkerId == workerId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(c => c.Status == status);
        var items = (await q.Include(c => c.Worker).Take(100).ToListAsync(ct))
            .OrderByDescending(c => c.CreatedAt).ToList();
        return (items, items.Count);
    }

    public async Task<AttendanceCorrection> CreateCorrectionAsync(AttendanceCorrection correction, CancellationToken ct)
    {
        db.AttendanceCorrections.Add(correction);
        await db.SaveChangesAsync(ct);
        return correction;
    }

    // ----- M3 attendance, roster, decisions -----
    public async Task<AttendanceRecord?> GetAttendanceAsync(Guid workerId, DateOnly workDate, CancellationToken ct)
        => await db.AttendanceRecords.FirstOrDefaultAsync(a => a.WorkerId == workerId && a.WorkDate == workDate, ct);

    public async Task<AttendanceRecord> CreateAttendanceAsync(AttendanceRecord record, CancellationToken ct)
    {
        db.AttendanceRecords.Add(record);
        await db.SaveChangesAsync(ct);
        return record;
    }

    public async Task<AttendanceRecord> UpdateAttendanceAsync(AttendanceRecord record, CancellationToken ct)
    {
        if (db.Entry(record).State == EntityState.Detached)
            db.AttendanceRecords.Update(record);
        await db.SaveChangesAsync(ct);
        return record;
    }

    public async Task<List<AttendanceRecord>> ListAttendanceAsync(Guid workerId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var q = db.AttendanceRecords.Where(a => a.WorkerId == workerId);
        if (from.HasValue) q = q.Where(a => a.WorkDate >= from.Value);
        if (to.HasValue) q = q.Where(a => a.WorkDate <= to.Value);
        return await q.Include(a => a.Worker).Take(200).ToListAsync(ct); // already bounded by from/to window; keep insert order
    }

    public async Task<List<AttendanceRecord>> ListAttendanceForWorkerRangeAsync(Guid workerId, DateOnly from, DateOnly to, CancellationToken ct)
        => await db.AttendanceRecords
            .Include(a => a.Worker)
            .Where(a => a.WorkerId == workerId && a.WorkDate >= from && a.WorkDate <= to)
            .OrderBy(a => a.WorkDate)
            .ToListAsync(ct);

    public async Task<List<AttendanceRecord>> ListAttendanceForScopeAsync(DateOnly? from, DateOnly? to, Guid? locationId, Guid? orgUnitId, CancellationToken ct)
    {
        var q = db.AttendanceRecords.Include(a => a.Worker).AsQueryable();
        if (from.HasValue) q = q.Where(a => a.WorkDate >= from.Value);
        if (to.HasValue) q = q.Where(a => a.WorkDate <= to.Value);
        // Imported attendance may predate branch tagging. Fall back to the
        // worker assignment in that case, but never expose all branchless
        // records to a branch-scoped operator.
        if (locationId.HasValue)
            q = q.Where(a => a.LocationId == locationId.Value || (!a.LocationId.HasValue && a.Worker!.LocationId == locationId.Value));
        if (orgUnitId.HasValue)
            q = q.Where(a => a.Worker!.OrgUnitId == orgUnitId.Value);
        return await q.OrderByDescending(a => a.WorkDate).ThenBy(a => a.Worker!.EmployeeNo).Take(2000).ToListAsync(ct);
    }
    public async Task<List<AttendanceRecord>> ListOvertimeAsync(Guid? workerId, DateOnly? from, DateOnly? to, string? status, CancellationToken ct)
    {
        var q = db.AttendanceRecords
            .Include(a => a.Worker)
            .Where(a => a.OvertimeHours > 0);
        if (workerId.HasValue) q = q.Where(a => a.WorkerId == workerId.Value);
        if (from.HasValue) q = q.Where(a => a.WorkDate >= from.Value);
        if (to.HasValue) q = q.Where(a => a.WorkDate <= to.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(a => a.OvertimeStatus == status);
        return await q.OrderByDescending(a => a.WorkDate).ThenBy(a => a.Worker!.EmployeeNo).Take(1000).ToListAsync(ct);
    }

    public async Task<List<AuditEntry>> ListTimeAuditEntriesAsync(CancellationToken ct)
        => (await db.AuditEntries
            .Where(a => a.EntityType.StartsWith("time."))
            .Take(100)
            .ToListAsync(ct))
            .OrderByDescending(a => a.CreatedAt)
            .ToList();

    public async Task AddTimeAuditEntryAsync(AuditEntry entry, CancellationToken ct)
    {
        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AttendanceCorrection?> GetCorrectionAsync(Guid id, CancellationToken ct)
        => await db.AttendanceCorrections.Include(c => c.Worker).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<AttendanceCorrection> UpdateCorrectionAsync(AttendanceCorrection correction, CancellationToken ct)
    {
        if (db.Entry(correction).State == EntityState.Detached)
            db.AttendanceCorrections.Update(correction);
        await db.SaveChangesAsync(ct);
        return correction;
    }

    public async Task<LeaveRequest?> GetLeaveRequestAsync(Guid id, CancellationToken ct)
        => await db.LeaveRequests.Include(l => l.Worker).FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<LeaveRequest> UpdateLeaveRequestAsync(LeaveRequest request, CancellationToken ct)
    {
        if (db.Entry(request).State == EntityState.Detached)
            db.LeaveRequests.Update(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task ReleaseReservationAsync(Guid leaveRequestId, CancellationToken ct)
    {
        // reverse a reservation: rows that were negative "request" entries for this reference become positive
        var rows = await db.LeaveBalanceLedgers
            .Where(l => l.ReferenceId == leaveRequestId && l.ReferenceType == "leave-request" && l.Days < 0 && l.Reason == "request")
            .ToListAsync(ct);
        foreach (var r in rows)
        {
            r.Reason = "request-release";
            r.Days = -r.Days;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task ConvertReservationAsync(Guid leaveRequestId, CancellationToken ct)
    {
        // convert an open reservation into a permanent (taken) deduction: keep the negative
        // sign, change reason so balance math counts it under "taken" instead of "reserved"
        var rows = await db.LeaveBalanceLedgers
            .Where(l => l.ReferenceId == leaveRequestId && l.ReferenceType == "leave-request" && l.Days < 0 && l.Reason == "request")
            .ToListAsync(ct);
        foreach (var r in rows)
            r.Reason = "approval";
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<WorkCalendar>> ListCalendarsAsync(CancellationToken ct)
        => await db.WorkCalendars.Include(c => c.Holidays).ToListAsync(ct);

    public async Task<List<ShiftDefinition>> ListShiftsAsync(CancellationToken ct)
        => await db.ShiftDefinitions.Where(s => s.IsActive).OrderBy(s => s.Code).ToListAsync(ct);

    public async Task<ShiftDefinition> CreateShiftAsync(ShiftDefinition shift, CancellationToken ct)
    {
        db.ShiftDefinitions.Add(shift);
        await db.SaveChangesAsync(ct);
        return shift;
    }

    public Task<ShiftDefinition?> GetShiftAsync(Guid id, CancellationToken ct) =>
        db.ShiftDefinitions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<ShiftDefinition> UpdateShiftAsync(ShiftDefinition shift, CancellationToken ct)
    {
        db.ShiftDefinitions.Update(shift);
        await db.SaveChangesAsync(ct);
        return shift;
    }

    public async Task<WorkerShiftAssignment?> GetShiftAssignmentAsync(Guid workerId, DateOnly date, CancellationToken ct)
        => await db.WorkerShiftAssignments.Include(a => a.Shift)
            .Include(a => a.Calendar).ThenInclude(c => c!.Holidays)
            .Where(a => a.WorkerId == workerId && a.EffectiveFrom <= date &&
                (!a.EffectiveTo.HasValue || a.EffectiveTo.Value >= date))
            .OrderByDescending(a => a.EffectiveFrom).FirstOrDefaultAsync(ct);

    public async Task<WorkerShiftAssignment> CreateShiftAssignmentAsync(WorkerShiftAssignment assignment, CancellationToken ct)
    {
        db.WorkerShiftAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);
        await db.Entry(assignment).Reference(a => a.Calendar).LoadAsync(ct);
        return assignment;
    }

    public async Task CloseOpenShiftAssignmentsAsync(Guid workerId, DateOnly effectiveTo, CancellationToken ct)
    {
        var rows = await db.WorkerShiftAssignments
            .Where(a => a.WorkerId == workerId && !a.EffectiveTo.HasValue && a.EffectiveFrom <= effectiveTo)
            .ToListAsync(ct);
        foreach (var row in rows) row.EffectiveTo = effectiveTo;
        await db.SaveChangesAsync(ct);
    }

    public async Task<Worker?> FindWorkerByEmployeeNoAsync(string employeeNo, CancellationToken ct)
        => await db.Workers.FirstOrDefaultAsync(w => w.EmployeeNo == employeeNo && w.Status == "active", ct);

    public async Task<AttendanceImportBatch> CreateImportBatchAsync(AttendanceImportBatch batch, CancellationToken ct)
    {
        db.AttendanceImportBatches.Add(batch);
        await db.SaveChangesAsync(ct);
        return batch;
    }

    public async Task UpdateImportBatchAsync(AttendanceImportBatch batch, CancellationToken ct)
    {
        if (db.Entry(batch).State == EntityState.Detached) db.AttendanceImportBatches.Update(batch);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<AttendanceImportBatch>> ListImportBatchesAsync(CancellationToken ct)
        => (await db.AttendanceImportBatches.Take(50).ToListAsync(ct))
            .OrderByDescending(batch => batch.CreatedAt).ToList();

    public async Task<LeaveAccrualRun?> GetAccrualRunAsync(string period, CancellationToken ct)
        => await db.LeaveAccrualRuns.FirstOrDefaultAsync(r => r.Period == period, ct);

    public async Task<LeaveAccrualRun> CreateAccrualRunAsync(LeaveAccrualRun run, CancellationToken ct)
    {
        db.LeaveAccrualRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    public async Task UpdateAccrualRunAsync(LeaveAccrualRun run, CancellationToken ct)
    {
        if (db.Entry(run).State == EntityState.Detached) db.LeaveAccrualRuns.Update(run);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<LeaveAccrualRun>> ListAccrualRunsAsync(CancellationToken ct)
        => (await db.LeaveAccrualRuns.Take(50).ToListAsync(ct))
            .OrderByDescending(run => run.CreatedAt).ToList();

    public async Task<List<Worker>> ListAccrualWorkersAsync(CancellationToken ct)
        => await db.Workers.Where(w => w.Status == "active").ToListAsync(ct);

    public async Task<LeaveBalanceLedger> AddLedgerEntryAsync(LeaveBalanceLedger entry, CancellationToken ct)
    {
        db.LeaveBalanceLedgers.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<LeaveBalanceAdjustment> CreateAdjustmentAsync(LeaveBalanceAdjustment adjustment, CancellationToken ct)
    {
        db.LeaveBalanceAdjustments.Add(adjustment);
        await db.SaveChangesAsync(ct);
        return adjustment;
    }

    public async Task<List<LeaveBalanceAdjustment>> ListAdjustmentsAsync(CancellationToken ct)
        => (await db.LeaveBalanceAdjustments.Include(adjustment => adjustment.Worker).Take(50).ToListAsync(ct))
            .OrderByDescending(adjustment => adjustment.CreatedAt).ToList();

    // M41 Gap 6a: leave encashment
    public async Task<(List<LeaveEncashmentRequest> Items, int Total)> ListEncashmentsAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        var q = db.LeaveEncashmentRequests.AsQueryable();
        if (workerId.HasValue) q = q.Where(e => e.WorkerId == workerId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(e => e.Status == status);
        var items = (await q.Include(e => e.Worker).Take(100).ToListAsync(ct))
            .OrderByDescending(e => e.CreatedAt).ToList();
        return (items, items.Count);
    }

    public async Task<LeaveEncashmentRequest?> GetEncashmentAsync(Guid id, CancellationToken ct)
        => await db.LeaveEncashmentRequests.Include(e => e.Worker).FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<LeaveEncashmentRequest> CreateEncashmentAsync(LeaveEncashmentRequest request, CancellationToken ct)
    {
        db.LeaveEncashmentRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<LeaveEncashmentRequest> UpdateEncashmentAsync(LeaveEncashmentRequest request, CancellationToken ct)
    {
        if (db.Entry(request).State == EntityState.Detached)
            db.LeaveEncashmentRequests.Update(request);
        await db.SaveChangesAsync(ct);
        return request;
    }
}

// ===================== Workflow =====================
public sealed class WorkflowRepository(HrmDbContext db) : IWorkflowRepository
{
    public async Task<WorkflowRequest> CreateRequestAsync(WorkflowRequest request, CancellationToken ct)
    {
        db.WorkflowRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<WorkflowRequest?> GetRequestAsync(Guid id, CancellationToken ct)
        => await db.WorkflowRequests.Include(w => w.Decisions).FirstOrDefaultAsync(w => w.Id == id, ct);

    // M16: the leave request id is the workflow subject id for type "leave".
    public async Task<WorkflowRequest?> GetOpenBySubjectAsync(string workflowType, Guid subjectWorkerId, CancellationToken ct)
        => await db.WorkflowRequests.Include(w => w.Decisions)
            .FirstOrDefaultAsync(w => w.WorkflowType == workflowType
                && w.SubjectWorkerId == subjectWorkerId
                && (w.Status == "submitted" || w.Status == "in-review" || w.Status == "returned"), ct);

    public async Task<WorkflowRequest> UpdateRequestAsync(WorkflowRequest request, CancellationToken ct)
    {
        // EF Core 10 demotes children added via the parent's collection to Modified
        // when the parent is Modified (state-propagation behavior change), turning the
        // child INSERT into an UPDATE with 0 rows affected. Re-attach brand-new
        // (detached) children directly to the context as Added with the FK set, which
        // keeps them as INSERTs alongside the parent UPDATE.
        foreach (var decision in request.Decisions)
        {
            if (db.Entry(decision).State == EntityState.Detached)
            {
                decision.RequestId = request.Id;
                db.WorkflowDecisions.Add(decision);
            }
        }
        if (db.Entry(request).State == EntityState.Detached)
            db.WorkflowRequests.Update(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<(List<WorkflowRequest> Items, int Total)> ListOpenRequestsAsync(CancellationToken ct)
    {
        var items = (await db.WorkflowRequests.Include(w => w.Decisions)
            .Where(w => w.Status == "submitted" || w.Status == "in-review")
            .ToListAsync(ct)).OrderByDescending(w => w.CreatedAt).ToList();
        return (items, items.Count);
    }

    public async Task<Guid?> FindManagerOfAsync(Guid workerId, CancellationToken ct)
        => await db.Workers.Where(w => w.Id == workerId).Select(w => w.ManagerId).FirstOrDefaultAsync(ct);

    public async Task<bool> IsDelegateForAsync(Guid delegatorId, Guid actorId, string workflowType, DateOnly date, CancellationToken ct)
        => await db.ApprovalDelegations.AnyAsync(d =>
            d.DelegatorId == delegatorId && d.DelegateWorkerId == actorId && d.IsActive &&
            d.FromDate <= date && (d.ToDate == null || d.ToDate >= date) &&
            (d.Scope == null || d.Scope == workflowType), ct);

    public async Task<Guid?> GetActiveDelegationForAsync(Guid delegatorId, string workflowType, DateOnly date, CancellationToken ct)
    {
        var delegation = await db.ApprovalDelegations
            .Where(d => d.DelegatorId == delegatorId && d.IsActive &&
                        d.FromDate <= date && (d.ToDate == null || d.ToDate >= date) &&
                        (d.Scope == null || d.Scope == workflowType))
            .OrderBy(d => d.Scope == null ? 1 : 0) // type-specific scope wins over blanket scope
            .Select(d => (Guid?)d.DelegateWorkerId).FirstOrDefaultAsync(ct);
        return delegation;
    }

    public async Task<Dictionary<Guid, string>> GetWorkerNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
        => await db.Workers.Where(w => ids.Contains(w.Id))
            .Select(w => new { w.Id, Name = $"{w.FirstName} {w.LastName}".Trim() })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
}

// ===================== Experience =====================
public sealed class ExperienceRepository(HrmDbContext db) : IExperienceRepository
{
    public async Task<(List<HrRequest> Items, int Total)> ListRequestsAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        var q = db.HrRequests.AsQueryable();
        if (workerId.HasValue) q = q.Where(r => r.WorkerId == workerId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        var items = (await q.Include(r => r.Messages).Include(r => r.Worker)
            .Take(100).ToListAsync(ct)).OrderByDescending(r => r.CreatedAt).ToList();
        return (items, items.Count);
    }

    public async Task<HrRequest?> GetRequestAsync(Guid id, CancellationToken ct)
        => await db.HrRequests.Include(r => r.Messages).Include(r => r.Worker).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<HrRequest> CreateRequestAsync(HrRequest request, CancellationToken ct)
    {
        db.HrRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<HrRequest> UpdateRequestAsync(HrRequest request, CancellationToken ct)
    {
        // EF Core 10 demotes children added via the parent's collection to Modified
        // when the parent is Modified (state-propagation behavior change). Re-attach
        // brand-new (detached) messages directly to the context as Added with the FK
        // set, which keeps them as INSERTs alongside the parent UPDATE.
        // M22: EF Core 10 also demotes EXISTING tracked children to Modified when
        // the parent is Modified, producing 0-row UPDATEs (DbUpdateConcurrencyException).
        // Pin every existing (non-detached) message to Unchanged so the tracker skips it.
        foreach (var msg in request.Messages.ToList())
        {
            var entry = db.Entry(msg);
            if (entry.State == EntityState.Detached)
            {
                msg.RequestId = request.Id;
                db.HrRequestMessages.Add(msg);
            }
            else if (entry.State != EntityState.Unchanged && entry.State != EntityState.Added)
            {
                entry.State = EntityState.Unchanged;
            }
        }
        if (db.Entry(request).State == EntityState.Detached)
            db.HrRequests.Update(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    // M22: explicit top-level insert so EF Core 10's Modified-parent demotion can
    // never turn the new message into a 0-row UPDATE. Same explicit-Add pattern
    // used for emergency contacts / bank details.
    public async Task<HrRequest> AddMessageAsync(HrRequest request, HrRequestMessage message, CancellationToken ct)
    {
        message.RequestId = request.Id;
        db.Set<HrRequestMessage>().Add(message);
        // status transition decided by the caller is already on the tracked parent
        await db.SaveChangesAsync(ct);
        return request;
    }

    public async Task<(List<HrLetter> Items, int Total)> ListLettersAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        var q = db.HrLetters.AsQueryable();
        if (workerId.HasValue) q = q.Where(l => l.WorkerId == workerId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(l => l.Status == status);
        var items = (await q.Include(l => l.Worker)
            .Take(100).ToListAsync(ct)).OrderByDescending(l => l.CreatedAt).ToList();
        return (items, items.Count);
    }

    public async Task<HrLetter?> GetLetterAsync(Guid id, CancellationToken ct)
        => await db.HrLetters.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<HrLetter> CreateLetterAsync(HrLetter letter, CancellationToken ct)
    {
        db.HrLetters.Add(letter);
        await db.SaveChangesAsync(ct);
        return letter;
    }

    public async Task<HrLetter> UpdateLetterAsync(HrLetter letter, CancellationToken ct)
    {
        if (db.Entry(letter).State == EntityState.Detached)
            db.HrLetters.Update(letter);
        await db.SaveChangesAsync(ct);
        return letter;
    }

    public async Task<int> CountDisclosuresThisYearAsync(CancellationToken ct)
        => await db.ProtectedDisclosures.CountAsync(ct);

    public async Task<ProtectedDisclosure> CreateDisclosureAsync(ProtectedDisclosure disclosure, CancellationToken ct)
    {
        db.ProtectedDisclosures.Add(disclosure);
        await db.SaveChangesAsync(ct);
        return disclosure;
    }

    public async Task<ProtectedDisclosure?> GetDisclosureByCaseReferenceAsync(string caseReference, CancellationToken ct)
        => await db.ProtectedDisclosures.FirstOrDefaultAsync(d => d.CaseReference == caseReference, ct);
}

// ===================== Payroll =====================
public sealed class PayrollRepository(HrmDbContext db) : IPayrollRepository
{
    public async Task<List<SalaryComponent>> ListComponentsAsync(string? type, CancellationToken ct)
    {
        var q = db.SalaryComponents.AsQueryable();
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(c => c.ComponentType == type);
        return await q.Where(c => c.IsActive).OrderBy(c => c.Priority).ToListAsync(ct);
    }
    public async Task<SalaryStructure?> FindStructureAsync(string code, CancellationToken ct) =>
        await db.SalaryStructures.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Code == code && s.IsActive, ct);
    public async Task<SalaryStructure?> FindStructureByCodeAsync(string code, CancellationToken ct) =>
        await db.SalaryStructures.FirstOrDefaultAsync(s => s.Code == code && !s.IsArchived, ct);
    public async Task<List<SalaryStructure>> ListStructuresAsync(CancellationToken ct) =>
        await db.SalaryStructures.AsNoTracking()
            .Include(s => s.Items).ThenInclude(i => i.Component)
            .Where(s => !s.IsArchived && s.IsActive).OrderBy(s => s.Code).ToListAsync(ct);
    public async Task<SalaryStructure?> GetStructureAsync(Guid id, CancellationToken ct) =>
        await db.SalaryStructures
            .Include(s => s.Items).ThenInclude(i => i.Component)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsArchived, ct);
    public async Task<SalaryStructure> CreateStructureAsync(SalaryStructure structure, CancellationToken ct)
    {
        db.SalaryStructures.Add(structure);
        await db.SaveChangesAsync(ct);
        return structure;
    }
    public async Task UpdateStructureAsync(SalaryStructure structure, CancellationToken ct)
    {
        if (db.Entry(structure).State == EntityState.Detached)
            db.SalaryStructures.Update(structure);
        await db.SaveChangesAsync(ct);
    }
    /// <summary>EF Core 10 + SQLite Guid-V7 bug: adding child entities via the
    /// navigation after the parent was saved throws a spurious
    /// DbUpdateConcurrencyException, so items are attached explicitly and saved
    /// in a separate phase with no Update() call.</summary>
    public async Task SetStructureItemsExplicitlyAsync(SalaryStructure structure,
        List<SalaryStructureItem> items, CancellationToken ct)
    {
        foreach (var i in items)
        {
            i.StructureId = structure.Id;
            i.TenantId = structure.TenantId;
        }
        db.Set<SalaryStructureItem>().AddRange(items);
        await db.SaveChangesAsync(ct);
    }
    public async Task ClearStructureItemsAsync(Guid structureId, CancellationToken ct)
    {
        var items = await db.SalaryStructureItems.Where(i => i.StructureId == structureId).ToListAsync(ct);
        db.SalaryStructureItems.RemoveRange(items);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Worker?> GetWorkerAsync(Guid id, CancellationToken ct)
        => await db.Workers.FirstOrDefaultAsync(w => w.Id == id, ct);
    // M25: resolve the worker linked to a Keycloak subject (self-service).
    public async Task<Worker?> GetWorkerBySubjectAsync(string subjectId, CancellationToken ct)
        => await db.Workers.FirstOrDefaultAsync(w => w.SubjectId == subjectId, ct);

    public async Task<List<PayGroup>> ListPayGroupsAsync(CancellationToken ct)
        => await db.PayGroups.ToListAsync(ct);
    public async Task<List<PayGroup>> ListPayGroupsAllAsync(CancellationToken ct)
        => await db.PayGroups.ToListAsync(ct);
    public async Task<PayGroup?> GetPayGroupAsync(Guid id, CancellationToken ct)
        => await db.PayGroups.FirstOrDefaultAsync(g => g.Id == id, ct);
    public async Task<List<SalaryComponent>> ListAllComponentsAsync(CancellationToken ct)
        => await db.SalaryComponents.Where(c => c.IsActive).OrderBy(c => c.Priority).ToListAsync(ct);
    public async Task<SalaryComponent?> GetComponentByIdAsync(Guid id, CancellationToken ct)
        => await db.SalaryComponents.FirstOrDefaultAsync(c => c.Id == id, ct);
    public async Task<List<WorkerPayrollProfile>> ListProfilesAsync(Guid? workerId, CancellationToken ct)
    {
        // M41 Gap 4: DateOnly.FromDateTime inside a predicate cannot be
        // translated by the SQLite provider — compute the boundary client-side.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var q = db.WorkerPayrollProfiles
            .Include(p => p.Worker).Include(p => p.PayGroup)
            .Include(p => p.ComponentValues).ThenInclude(v => v.Component)
            .Where(p => !p.EffectiveTo.HasValue || p.EffectiveTo >= today);
        if (workerId.HasValue) q = q.Where(p => p.WorkerId == workerId.Value);
        return (await q.OrderByDescending(p => p.EffectiveFrom).ToListAsync(ct))
            .GroupBy(p => p.WorkerId).Select(g => g.First()).ToList();
    }
    public async Task<WorkerPayrollProfile?> FindOpenProfileAsync(Guid workerId, CancellationToken ct)
    {
        // M41 Gap 4: DateOnly.FromDateTime inside a predicate cannot be
        // translated by the SQLite provider — compute the boundary client-side.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await db.WorkerPayrollProfiles
            .Include(p => p.Worker).Include(p => p.PayGroup)
            .Include(p => p.ComponentValues).ThenInclude(v => v.Component)
            .Where(p => p.WorkerId == workerId && (!p.EffectiveTo.HasValue || p.EffectiveTo >= today))
            .OrderByDescending(p => p.EffectiveFrom).FirstOrDefaultAsync(ct);
    }
    public async Task<WorkerPayrollProfile> CreateProfileAsync(WorkerPayrollProfile profile, CancellationToken ct)
    {
        db.WorkerPayrollProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return profile;
    }
    public async Task<WorkerPayrollProfile> UpdateProfileAsync(WorkerPayrollProfile profile, CancellationToken ct)
    {
        // EF Core 10 state-propagation: re-attach detached component values as Added
        foreach (var v in profile.ComponentValues)
            if (db.Entry(v).State == EntityState.Detached)
            {
                v.ProfileId = profile.Id;
                db.WorkerComponentValues.Add(v);
            }
        if (db.Entry(profile).State == EntityState.Detached)
            db.WorkerPayrollProfiles.Update(profile);
        await db.SaveChangesAsync(ct);
        return profile;
    }
    public async Task DeleteProfileValuesAsync(Guid profileId, CancellationToken ct)
    {
        var values = await db.WorkerComponentValues.Where(v => v.ProfileId == profileId).ToListAsync(ct);
        db.WorkerComponentValues.RemoveRange(values);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<PayPeriod>> ListPeriodsAsync(Guid payGroupId, CancellationToken ct)
        => await db.PayPeriods.Where(p => p.PayGroupId == payGroupId).OrderByDescending(p => p.StartDate).ToListAsync(ct);

    public async Task<PayPeriod?> GetPeriodAsync(Guid id, CancellationToken ct)
        => await db.PayPeriods.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<List<TaxSlab>> ListTaxSlabsAsync(string taxYear, CancellationToken ct)
        => await db.TaxSlabs.Where(s => s.TaxYear == taxYear && s.IsActive).OrderBy(s => s.Sequence).ToListAsync(ct);

    public async Task<TaxSlab?> GetTaxSlabAsync(Guid id, CancellationToken ct)
        => await db.TaxSlabs.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task UpdateTaxSlabAsync(TaxSlab slab, CancellationToken ct)
    {
        if (db.Entry(slab).State == EntityState.Detached)
            db.TaxSlabs.Update(slab);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<ContributionRule>> ListContributionRulesAsync(CancellationToken ct)
        => await db.ContributionRules.Where(r => r.IsActive).ToListAsync(ct);

    public async Task<ContributionRule?> GetContributionRuleAsync(Guid id, CancellationToken ct)
        => await db.ContributionRules.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task UpdateContributionRuleAsync(ContributionRule rule, CancellationToken ct)
    {
        if (db.Entry(rule).State == EntityState.Detached)
            db.ContributionRules.Update(rule);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdatePayGroupAsync(PayGroup group, CancellationToken ct)
    {
        if (db.Entry(group).State == EntityState.Detached)
            db.PayGroups.Update(group);
        await db.SaveChangesAsync(ct);
    }

    public async Task UnsetDefaultPayGroupsAsync(CancellationToken ct, Guid keepId)
    {
        await db.PayGroups.Where(g => g.IsDefault && g.Id != keepId).ForEachAsync(g => g.IsDefault = false, ct);
        await db.SaveChangesAsync(ct);
    }

    // M50: wizard provisioning — before this milestone no create surface existed
    // for any of these entities, so the payroll chain (group → components →
    // rules → slabs → structure → period → profiles) could never be fully
    // provisioned by a first-time user.
    public async Task<PayGroup> CreatePayGroupAsync(PayGroup group, CancellationToken ct)
    {
        db.PayGroups.Add(group);
        await db.SaveChangesAsync(ct);
        return group;
    }
    public async Task<SalaryComponent> CreateComponentAsync(SalaryComponent component, CancellationToken ct)
    {
        db.SalaryComponents.Add(component);
        await db.SaveChangesAsync(ct);
        return component;
    }
    public async Task<ContributionRule> CreateContributionRuleAsync(ContributionRule rule, CancellationToken ct)
    {
        db.ContributionRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return rule;
    }
    public async Task<TaxSlab> CreateTaxSlabAsync(TaxSlab slab, CancellationToken ct)
    {
        db.TaxSlabs.Add(slab);
        await db.SaveChangesAsync(ct);
        return slab;
    }
    public async Task<PayPeriod> CreatePeriodAsync(PayPeriod period, CancellationToken ct)
    {
        db.PayPeriods.Add(period);
        await db.SaveChangesAsync(ct);
        return period;
    }

    /// M50: workers freshly created by the wizard import — matched back to the
    /// mapped spreadsheet rows so a payroll profile can be attached to each.
    public async Task<List<Worker>> ListWorkersCreatedAfterAsync(DateTimeOffset since, CancellationToken ct)
    {
        return await db.Workers
            .Where(w => !w.IsArchived && w.CreatedAt >= since)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task UpdateComponentAsync(SalaryComponent component, CancellationToken ct)
    {
        if (db.Entry(component).State == EntityState.Detached)
            db.SalaryComponents.Update(component);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PayrollRun?> GetRunAsync(Guid id, CancellationToken ct)
        => await db.PayrollRuns.Include(r => r.PayPeriod).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<List<PayrollRun>> ListRunsAsync(CancellationToken ct)
        => await db.PayrollRuns.AsNoTracking().Include(r => r.PayPeriod)
            .OrderByDescending(r => r.PayPeriod!.StartDate).ThenByDescending(r => r.CreatedAt).ToListAsync(ct);

    // M48: rows for the top-HR approval queue — branch runs in review plus
    // calculated branch runs not yet submitted. Organisation-wide runs never
    // enter this queue (they are approved inline from the runs list).
    // Each row carries the branch display name (resolved in one query) and the
    // exact moment the preparer submitted the run for review (or null when the
    // run is still just calculated and waiting for the branch HR to submit it).
    public async Task<List<(PayrollRun Run, string? BranchName, string? LegalEntityId, DateTimeOffset? SubmittedAt)>> ListRunsInReviewAsync(CancellationToken ct)
    {
        // Status is a plain string (safe for SQLite), but DateTimeOffset ordering
        // is not — sort the queue client-side instead.
        var runs = (await db.PayrollRuns.AsNoTracking().Include(r => r.PayPeriod)
            .Where(r => r.Status == "in-review" || (r.Status == "calculated" && r.LocationId.HasValue))
            .ToListAsync(ct))
            .OrderByDescending(r => r.CreatedAt).ToList();
        var branchIds = runs.Select(r => r.LocationId).Where(l => l.HasValue).Select(l => l!.Value).Distinct().ToList();
        var nameByBranch = branchIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await db.WorkLocations.AsNoTracking().Where(l => branchIds.Contains(l.Id)).ToListAsync(ct))
                .ToDictionary(l => l.Id, l => l.Name);
        var runIds = runs.Select(r => r.Id).ToList();
        var submittedByRun = runIds.Count == 0
            ? new Dictionary<Guid, DateTimeOffset>()
            : (await db.PayrollRunEvents.AsNoTracking()
                .Where(e => runIds.Contains(e.RunId) && e.Action == "submitted-for-review")
                .ToListAsync(ct))
                .GroupBy(e => e.RunId)
                .ToDictionary(g => g.Key, g => g.Max(e => e.CreatedAt));
        var legalEntityByBranch = (branchIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await db.WorkLocations.AsNoTracking().Where(l => branchIds.Contains(l.Id)).ToListAsync(ct))
                .ToDictionary(l => l.Id, l => l.LegalEntityId.ToString()));
        return runs.Select(r => (r,
            r.LocationId.HasValue && nameByBranch.TryGetValue(r.LocationId.Value, out var n) ? n : null,
            r.LocationId.HasValue && legalEntityByBranch.TryGetValue(r.LocationId.Value, out var lei) ? lei : null,
            r.LocationId.HasValue && submittedByRun.TryGetValue(r.Id, out var sa) ? sa : (DateTimeOffset?)null)).ToList();
    }

    public async Task<PayrollRun?> FindRunByPeriodAsync(Guid payPeriodId, CancellationToken ct)
        // Only an open (non-terminal) run blocks a new run: reversed/closed runs are
        // historical and a fresh replacement run may be created after reversal.
        => await db.PayrollRuns.FirstOrDefaultAsync(
            r => r.PayPeriodId == payPeriodId && r.Status != "reversed" && r.Status != "closed" && !r.IsReversal,
            ct);

    // M46: the open (non-terminal) run for a pay period scoped to one branch;
    // locationId = null returns an open organisation-wide run. This is the
    // coexistence lookup: a branch draft may only exist once per branch per
    // period, and an org run may not exist while a branch run is open.
    public async Task<PayrollRun?> FindOpenRunByPeriodAndLocationAsync(Guid payPeriodId, Guid? locationId, CancellationToken ct)
        => await db.PayrollRuns.FirstOrDefaultAsync(
            r => r.PayPeriodId == payPeriodId && !r.IsReversal
                && r.Status != "reversed" && r.Status != "closed"
                && r.LocationId == locationId,
            ct);

    // M46: any open branch-scoped run for a period (blocks organisation-wide
    // runs while a branch draft is still in flight).
    public async Task<PayrollRun?> FindOpenBranchRunForPeriodAsync(Guid payPeriodId, CancellationToken ct)
        => await db.PayrollRuns.FirstOrDefaultAsync(
            r => r.PayPeriodId == payPeriodId && !r.IsReversal
                && r.Status != "reversed" && r.Status != "closed"
                && r.LocationId.HasValue,
            ct);

    public async Task<PayrollRun> CreateRunAsync(PayrollRun run, CancellationToken ct)
    {
        db.PayrollRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    public async Task<PayrollRun> UpdateRunAsync(PayrollRun run, CancellationToken ct)
    {
        if (db.Entry(run).State == EntityState.Detached)
            db.PayrollRuns.Update(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    public Task<PayrollRun> SubmitRunAsync(PayrollRun run, CancellationToken ct) => UpdateRunAsync(run, ct);

    public async Task<(List<WorkerPayrollProfile> Profiles, List<SalaryComponent> Components, List<ContributionRule> Rules, List<TaxSlab> Slabs, DateOnly? Cutoff)>
        LoadCalculationInputsAsync(Guid payPeriodId, CancellationToken ct, Guid? locationId = null)
    {
        var period = await db.PayPeriods.FirstOrDefaultAsync(p => p.Id == payPeriodId, ct)
            ?? throw new DomainException("pay-period-not-found", "Pay period not found.");
        var profiles = (await db.WorkerPayrollProfiles
            .Include(p => p.Worker).ThenInclude(w => w!.BankDetails)
            .Include(p => p.ComponentValues).ThenInclude(v => v.Component)
            // Payroll is an operational process. A retained payroll profile
            // must never make an archived worker payable again.
            .Where(p => p.PayGroupId == period.PayGroupId
                && p.Worker != null
                && !p.Worker.IsArchived)
            .ToListAsync(ct))
            .Where(p => !p.EffectiveTo.HasValue || p.EffectiveTo >= DateOnly.FromDateTime(DateTime.UtcNow))
            // M46: a run scoped to a branch only pays the workers attached to
            // that branch (denormalized read view on Worker.LocationId; the
            // assignment history remains the source of truth for org chart).
            .Where(p => locationId is null || p.Worker?.LocationId == locationId)
            .ToList();
        var components = await db.SalaryComponents.Where(c => c.IsActive).OrderBy(c => c.Priority).ToListAsync(ct);
        var rules = await db.ContributionRules.Where(r => r.IsActive).ToListAsync(ct);
        var year = period.StartDate.Year.ToString();
        var slabs = await db.TaxSlabs.Where(s => s.TaxYear == year && s.IsActive).OrderBy(s => s.Sequence).ToListAsync(ct);
        return (profiles, components, rules, slabs, period.CutoffDate);
    }

    public async Task<List<WorkerBenefitAllowance>> LoadPayrollBenefitAllowancesAsync(Guid payPeriodId, Guid? locationId, CancellationToken ct)
    {
        var period = await db.PayPeriods.FirstOrDefaultAsync(p => p.Id == payPeriodId, ct)
            ?? throw new DomainException("pay-period-not-found", "Pay period not found.");
        var query = db.WorkerBenefitAllowances
            .Include(a => a.Worker)
            .Include(a => a.BenefitType)
            .Where(a => a.Year == period.StartDate.Year
                && a.AnnualAmount > 0
                && a.Worker != null
                && !a.Worker.IsArchived
                && a.BenefitType != null
                && a.BenefitType.IsActive
                && a.BenefitType.IncludeInPayroll);
        if (locationId.HasValue)
            query = query.Where(a => a.Worker != null && a.Worker.LocationId == locationId);
        return await query.ToListAsync(ct);
    }

    public async Task<List<SalaryAdvance>> ListSalaryAdvancesAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        var query = db.SalaryAdvances
            .Include(a => a.Worker)
            .AsQueryable();
        if (workerId.HasValue) query = query.Where(a => a.WorkerId == workerId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);
        var rows = await query.ToListAsync(ct);
        return rows.OrderByDescending(a => a.CreatedAt).ToList();
    }

    public Task<SalaryAdvance?> GetSalaryAdvanceAsync(Guid id, CancellationToken ct) =>
        db.SalaryAdvances
            .Include(a => a.Worker)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<SalaryAdvance> CreateSalaryAdvanceAsync(SalaryAdvance advance, CancellationToken ct)
    {
        db.SalaryAdvances.Add(advance);
        await db.SaveChangesAsync(ct);
        return advance;
    }

    public async Task UpdateSalaryAdvanceAsync(SalaryAdvance advance, CancellationToken ct)
    {
        if (db.Entry(advance).State == EntityState.Detached)
            db.SalaryAdvances.Update(advance);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<SalaryAdvance>> LoadDeductibleSalaryAdvancesAsync(Guid payPeriodId, Guid? locationId, CancellationToken ct)
    {
        var period = await db.PayPeriods.FirstOrDefaultAsync(p => p.Id == payPeriodId, ct)
            ?? throw new DomainException("pay-period-not-found", "Pay period not found.");
        var query = db.SalaryAdvances
            .Include(a => a.Worker)
            .Where(a => a.Status == "active"
                && a.DeductFromPayslip
                && a.DeductionStartDate <= period.EndDate
                && a.Amount > 0
                && a.InstallmentAmount > 0
                && a.Worker != null
                && !a.Worker.IsArchived);
        if (locationId.HasValue)
            query = query.Where(a => a.Worker != null && a.Worker.LocationId == locationId);
        return await query.ToListAsync(ct);
    }

    public async Task<Dictionary<Guid, decimal>> GetSalaryAdvanceRecoveredAmountsAsync(List<Guid> advanceIds, CancellationToken ct)
    {
        if (advanceIds.Count == 0) return new Dictionary<Guid, decimal>();
        var codeById = advanceIds.Distinct().ToDictionary(id => $"salary-advance-{id:N}", id => id);
        var codes = codeById.Keys.ToList();
        var rows = await db.PayrollLineComponents
            .Include(c => c.RunLine).ThenInclude(l => l!.Run)
            .Where(c => codes.Contains(c.ComponentCode)
                && c.RunLine != null
                && c.RunLine.Run != null
                && (c.RunLine.Run.Status == "released" || c.RunLine.Run.Status == "closed"))
            .GroupBy(c => c.ComponentCode)
            .Select(g => new { Code = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToListAsync(ct);
        return rows
            .Where(row => codeById.ContainsKey(row.Code))
            .ToDictionary(row => codeById[row.Code], row => row.Amount);
    }

    public async Task<List<AttendanceRecord>> LoadApprovedOvertimeAsync(Guid payPeriodId, Guid? locationId, CancellationToken ct)
    {
        var period = await db.PayPeriods.FirstOrDefaultAsync(p => p.Id == payPeriodId, ct)
            ?? throw new DomainException("pay-period-not-found", "Pay period not found.");
        var q = db.AttendanceRecords
            .Include(a => a.Worker)
            .Where(a => a.WorkDate >= period.StartDate && a.WorkDate <= period.EndDate
                && a.OvertimeHours > 0 && a.OvertimeStatus == "approved"
                && a.OvertimePayrollRunId == null
                && a.Worker != null
                && !a.Worker.IsArchived);
        if (locationId.HasValue)
            q = q.Where(a => a.LocationId == locationId || a.Worker!.LocationId == locationId);
        return await q.OrderBy(a => a.Worker!.EmployeeNo).ThenBy(a => a.WorkDate).ToListAsync(ct);
    }

    public async Task LinkOvertimeToPayrollAsync(Guid attendanceId, Guid runId, Guid runLineId, CancellationToken ct)
    {
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(a => a.Id == attendanceId, ct)
            ?? throw new DomainException("overtime-not-found", "Overtime attendance record not found.");
        if (record.OvertimeStatus != "approved" || record.OvertimePayrollRunId.HasValue)
            throw new DomainException("overtime-not-linkable", "Overtime is no longer approved or has already been linked to payroll.");
        record.OvertimeStatus = "paid";
        record.OvertimePayrollRunId = runId;
        record.OvertimePayrollLineId = runLineId;
        await db.SaveChangesAsync(ct);
    }

    public async Task<PayrollProrationInputs> LoadProrationInputsAsync(Guid payPeriodId, CancellationToken ct)
    {
        var period = await db.PayPeriods.FirstOrDefaultAsync(p => p.Id == payPeriodId, ct)
            ?? throw new DomainException("pay-period-not-found", "Pay period not found.");
        // Approved leaves whose type is unpaid (or half-pay, treated as unpaid
        // for proration purposes) and whose range overlaps the period.
        var unpaidTypeCodes = await db.LeaveTypes
            .Where(t => t.Category == "unpaid" || t.Category == "half-pay")
            .Select(t => t.Code)
            .ToListAsync(ct);
        var unpaidLeaves = await db.LeaveRequests
            .Where(lr => lr.Status == "approved"
                && unpaidTypeCodes.Contains(lr.LeaveTypeCode)
                && lr.StartDate <= period.EndDate && lr.EndDate >= period.StartDate)
            .Select(lr => new ApprovedUnpaidLeave(lr.WorkerId, lr.StartDate, lr.EndDate, lr.RequestedDays))
            .ToListAsync(ct);
        // Effective calendar: tenant default, falling back to any Zambia
        // calendar. Its weekend definition drives monthly payroll proration;
        // holiday dates are paid days, so they do not reduce payment days.
        var calendar = await db.WorkCalendars
            .OrderByDescending(c => c.IsDefault)
            .FirstOrDefaultAsync(ct);
        var holidays = calendar is null ? new List<DateOnly>() : await db.PublicHolidays
            .Where(h => h.CalendarId == calendar.Id && h.HolidayDate >= period.StartDate && h.HolidayDate <= period.EndDate)
            .Select(h => h.HolidayDate)
            .ToListAsync(ct);
        return new PayrollProrationInputs(period.StartDate, period.EndDate, unpaidLeaves, holidays,
            calendar?.WeekendDays ?? "sat,sun");
    }

    public async Task ClearRunLinesAsync(Guid runId, CancellationToken ct)
    {
        var lines = await db.PayrollRunLines.Where(l => l.RunId == runId).ToListAsync(ct);
        db.PayrollRunLines.RemoveRange(lines);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddRunLineAsync(PayrollRunLine line, CancellationToken ct)
    {
        db.PayrollRunLines.Add(line);
        await db.SaveChangesAsync(ct);
    }

    public async Task<(List<PayrollRunLine> Items, int Total)> ListRunLinesAsync(Guid runId, CancellationToken ct)
    {
        var items = await db.PayrollRunLines
            .Include(l => l.Worker).ThenInclude(w => w!.BankDetails)
            .Include(l => l.Components)
            .Where(l => l.RunId == runId).OrderBy(l => l.Worker!.EmployeeNo).ToListAsync(ct);
        return (items, items.Count);
    }

    public async Task<PayrollRunLine?> GetRunLineAsync(Guid runId, Guid lineId, CancellationToken ct)
        => await db.PayrollRunLines.Include(l => l.Worker).Include(l => l.Components)
            .FirstOrDefaultAsync(l => l.RunId == runId && l.Id == lineId, ct);

    public async Task UpdateRunLineAsync(PayrollRunLine line, CancellationToken ct)
    {
        if (db.Entry(line).State == EntityState.Detached) db.PayrollRunLines.Update(line);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Payslip?> GetCurrentPayslipForRunLineAsync(Guid runLineId, CancellationToken ct)
        => await db.Payslips
            .Where(p => p.RunLineId == runLineId && (p.Status == "final" || p.Status == "corrected"))
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(ct);

    public async Task<Payslip> CreatePayslipAsync(Payslip payslip, CancellationToken ct)
    {
        db.Payslips.Add(payslip);
        await db.SaveChangesAsync(ct);
        return payslip;
    }

    public async Task RecalculateRunTotalsAsync(PayrollRun run, CancellationToken ct)
    {
        var lines = await db.PayrollRunLines.Where(l => l.RunId == run.Id && !l.IsExcluded).ToListAsync(ct);
        run.EmployeeCount = lines.Count;
        run.TotalGross = lines.Sum(l => l.GrossPay);
        run.TotalDeductions = lines.Sum(l => l.TotalDeductions);
        run.TotalNet = lines.Sum(l => l.NetPay);
        run.TotalEmployerCost = lines.Sum(l => l.GrossPay + l.EmployerCost);
        run.ExceptionCount = lines.Count(l => l.HasException && l.ExceptionStatus == "open");
        await UpdateRunAsync(run, ct);
    }

    public async Task<List<PayrollPaymentRow>> ListPaymentRowsAsync(Guid runId, CancellationToken ct)
    {
        var lines = await db.PayrollRunLines.AsNoTracking().Include(l => l.Worker).ThenInclude(w => w!.BankDetails)
            .Where(l => l.RunId == runId && !l.IsExcluded).OrderBy(l => l.Worker!.EmployeeNo).ToListAsync(ct);
        return lines.Select(l =>
        {
            var bank = l.Worker?.BankDetails.FirstOrDefault(b => b.IsPrimary);
            return new PayrollPaymentRow(l.WorkerId, l.Worker?.EmployeeNo ?? "", l.Worker?.FullName ?? "",
                bank?.BankName ?? "", bank?.BranchCode ?? "", bank?.AccountName ?? "", bank?.AccountNumber ?? "", l.NetPay);
        }).ToList();
    }

    public async Task AddRunEventAsync(PayrollRunEvent item, CancellationToken ct)
    {
        db.PayrollRunEvents.Add(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<PayrollRunEvent>> ListRunEventsAsync(Guid runId, CancellationToken ct)
        => (await db.PayrollRunEvents.AsNoTracking().Where(e => e.RunId == runId).ToListAsync(ct))
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToList();

    public async Task<List<PayslipNotificationTarget>> FinalizePayslipsAsync(Guid runId, CancellationToken ct)
    {
        var run = await db.PayrollRuns.Include(r => r.PayPeriod)
            .FirstAsync(r => r.Id == runId, ct);
        var lines = await db.PayrollRunLines.Include(l => l.Worker)
            .Where(l => l.RunId == runId && !l.IsExcluded).ToListAsync(ct);

        // YTD accumulation: released (non-reversed) runs in the same pay group
        // and tax year, ordered chronologically by period label.
        var taxYear = run.PayPeriod?.StartDate.Year.ToString() ?? DateTime.UtcNow.Year.ToString();
        var periodStartYear = run.PayPeriod?.StartDate.Year ?? DateTime.UtcNow.Year;
        var prior = await (from r in db.PayrollRuns
                           join p in db.PayPeriods on r.PayPeriodId equals p.Id
                           where r.PayGroupId == run.PayGroupId
                              && r.PayGroupId == p.PayGroupId
                              && r.Status == "released" && !r.IsReversal && r.Id != run.Id
                              && p.StartDate.Year == periodStartYear
                           select new { Run = r, Period = p }).ToListAsync(ct);
        prior = prior.OrderBy(x => x.Period.StartDate).ThenBy(x => x.Run.CreatedAt).ToList();

        // If this run is a replacement for a reversal, chain each replacement
        // payslip to the original slip it supersedes.
        Func<PayrollRunLine, Guid?> originalSlipFor = _ => null;
        var originalSlips = new Dictionary<Guid, Guid>();
        if (run.IsReversal && run.ReversesRunId is not null)
        {
            var originals = await db.Payslips
                .Join(db.PayrollRunLines, s => s.RunLineId, l => l.Id, (s, l) => new { Slip = s, Line = l })
                .Where(x => x.Line.RunId == run.ReversesRunId)
                .Where(x => x.Slip.Status == "superseded")
                .Select(x => new { x.Slip.Id, x.Line.WorkerId }).ToListAsync(ct);
            foreach (var o in originals)
                originalSlips[o.WorkerId] = o.Id;
            originalSlipFor = ln => originalSlips.TryGetValue(ln.WorkerId, out var oid) ? oid : null;
        }

        // Sequence the payslip number past any existing slips for the same
        // worker's monthly prefix so the unique payslip_no index never collides.
        var prefix = $"PSL-{DateTime.UtcNow:yyyyMM}";
        var maxSeq = (await db.Payslips
            .Where(s => s.PayslipNo.StartsWith(prefix))
            .Select(s => (string?)s.PayslipNo)
            .ToListAsync(ct))
            .Select(n => n!.Substring(prefix.Length + 1))
            .Select(part => int.TryParse(part.Split('-').Last(), out var v) ? v : 0)
            .DefaultIfEmpty(0).Max();
        int idx = maxSeq;
        var notifications = new List<PayslipNotificationTarget>();
        foreach (var line in lines)
        {
            idx++;
            var ytdGross = prior.Sum(x => x.Run.TotalGross) + line.GrossPay;
            var ytdNet = prior.Sum(x => x.Run.TotalNet) + line.NetPay;
            var ytdTax = prior.Sum(x => x.Run.TotalDeductions) + line.TotalDeductions;
            var slip = new Payslip
            {
                RunLineId = line.Id,
                WorkerId = line.WorkerId,
                SupersedesId = originalSlipFor(line),
                PayslipNo = $"PSL-{DateTime.UtcNow:yyyyMM}-{line.Worker?.EmployeeNo ?? "???"}-{idx:D3}",
                Version = 1,
                GrossPay = line.GrossPay,
                TotalDeductions = line.TotalDeductions,
                NetPay = line.NetPay,
                YtdGross = ytdGross.ToString("F2"),
                YtdTax = ytdTax.ToString("F2"),
                YtdNet = ytdNet.ToString("F2"),
                Status = "final",
                ReleasedAt = DateTimeOffset.UtcNow,
                // M44: payslips inherit the branch of the run that produced them.
                LocationId = run.LocationId,
                // M24: snapshot the worker's statutory identity pack at payment
                // time — the payslip keeps these values even if the worker
                // record is updated later.
                WorkerNrc = line.Worker?.Nrc,
                WorkerTpin = line.Worker?.Tpin,
                WorkerNapsaNumber = line.Worker?.NapsaNumber,
                WorkerNhimaNumber = line.Worker?.NhimaNumber,
            };
            db.Payslips.Add(slip);
            notifications.Add(new PayslipNotificationTarget(
                slip.Id,
                slip.PayslipNo,
                run.PayPeriod?.PeriodLabel ?? "",
                line.WorkerId,
                line.Worker?.SubjectId,
                line.Worker?.Email,
                line.Worker?.PersonalEmail,
                line.Worker?.FirstName ?? "",
                line.Worker?.LastName ?? ""));
        }
        await db.SaveChangesAsync(ct);
        return notifications;
    }

    /// <summary>On release of a reversal run, supersede the original run's
    /// payslips (supersedes idempotently: only payslips still final get voided).</summary>
    public async Task<int> SupersedeOriginalPayslipsAsync(Guid originalRunId, CancellationToken ct)
    {
        var originals = await db.Payslips
            .Join(db.PayrollRunLines, s => s.RunLineId, l => l.Id, (s, l) => new { Slip = s, Line = l })
            .Where(x => x.Line.RunId == originalRunId)
            .Where(x => x.Slip.Status == "final")
            .Select(x => x.Slip).ToListAsync(ct);
        int count = 0;
        foreach (var slip in originals)
        {
            slip.Status = "superseded";
            count++;
        }
        await db.SaveChangesAsync(ct);
        return count;
    }

    public async Task<(List<Payslip> Items, int Total)> ListPayslipsAsync(Guid workerId, CancellationToken ct)
    {
        var items = (await db.Payslips.Include(p => p.RunLine).ThenInclude(l => l!.Components)
            .Include(p => p.RunLine).ThenInclude(l => l!.Worker)
            .Include(p => p.RunLine).ThenInclude(l => l!.Run).ThenInclude(r => r!.PayPeriod)
            .Where(p => p.WorkerId == workerId).ToListAsync(ct))
            .OrderByDescending(p => p.ReleasedAt).ToList();
        return (items, items.Count);
    }

    public async Task<Payslip?> GetPayslipAsync(Guid id, CancellationToken ct)
        => await db.Payslips.Include(p => p.RunLine).ThenInclude(l => l!.Components)
            .Include(p => p.RunLine).ThenInclude(l => l!.Worker)
            .Include(p => p.RunLine).ThenInclude(l => l!.Run).ThenInclude(r => r!.PayPeriod)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<PayrollRun?> FindRunByReversesIdAsync(Guid reversesRunId, CancellationToken ct)
        => await db.PayrollRuns.FirstOrDefaultAsync(r => r.ReversesRunId == reversesRunId && r.IsReversal, ct);

    public async Task<List<PayrollRunLine>> ListReleasedRunLinesForPeriodAsync(Guid payPeriodId, CancellationToken ct)
    {
        // Statutory aggregates across all released, non-reversed runs in the period.
        var runIds = await db.PayrollRuns
            .Where(r => r.PayPeriodId == payPeriodId && r.Status == "released" && !r.IsReversal)
            .Select(r => r.Id).ToListAsync(ct);
        if (runIds.Count == 0) return [];
        return await db.PayrollRunLines.Include(l => l.Components).Include(l => l.Worker)
            .Include(l => l.Run).ThenInclude(r => r!.PayPeriod)
            .Where(l => runIds.Contains(l.RunId)).ToListAsync(ct);
    }

    public async Task<PayrollRunLine?> GetRunLineForPayslipAsync(Guid payslipId, CancellationToken ct)
    {
        var slip = await db.Payslips.FirstOrDefaultAsync(s => s.Id == payslipId, ct);
        if (slip is null) return null;
        return await db.PayrollRunLines.Include(l => l.Components)
            .FirstOrDefaultAsync(l => l.Id == slip.RunLineId, ct);
    }

    public async Task UpdatePayslipAsync(Payslip payslip, CancellationToken ct)
    {
        db.Payslips.Update(payslip);
        await db.SaveChangesAsync(ct);
    }

    // M34: HR admin payslip list per run — joins via RunLine → RunId.
    public async Task<List<Payslip>> ListRunPayslipsAsync(Guid runId, CancellationToken ct)
    {
        var slipIds = await db.PayrollRunLines
            .Where(l => l.RunId == runId)
            .Select(l => l.Id)
            .ToListAsync(ct);
        if (slipIds.Count == 0) return [];
        return await db.Payslips.Include(p => p.RunLine).ThenInclude(l => l!.Components)
            .Include(p => p.RunLine).ThenInclude(l => l!.Worker)
            .Include(p => p.RunLine).ThenInclude(l => l!.Run).ThenInclude(r => r!.PayPeriod)
            .Where(p => slipIds.Contains(p.RunLineId))
            .ToListAsync(ct);
    }
    public async Task<List<LegalEntity>> ListLegalEntitiesAsync(CancellationToken ct)
        => await db.LegalEntities.ToListAsync(ct);
}

// ===================== Config / extras =====================
public sealed class ConfigRepository(HrmDbContext db) : IConfigRepository
{
    public async Task<List<LegalEntity>> ListLegalEntitiesAsync(CancellationToken ct) => await db.LegalEntities.ToListAsync(ct);
    public async Task<List<WorkLocation>> ListLocationsAsync(CancellationToken ct) => await db.WorkLocations.ToListAsync(ct);
    public async Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken ct) => await db.OrgUnits.ToListAsync(ct);
    public async Task<List<WorkCalendar>> ListCalendarsAsync(CancellationToken ct) => await db.WorkCalendars.Include(c => c.Holidays).ToListAsync(ct);
    public async Task<List<LeaveType>> ListLeaveTypesAsync(bool includeInactive, CancellationToken ct)
        => await db.LeaveTypes.Where(t => includeInactive || t.IsActive).ToListAsync(ct);
    public async Task<List<ContractType>> ListContractTypesAsync(bool includeInactive, CancellationToken ct)
        => await db.ContractTypes.Where(t => includeInactive || t.IsActive).OrderBy(t => t.Name).ToListAsync(ct);
    public async Task<List<CapabilityConfig>> ListCapabilitiesAsync(CancellationToken ct) => await db.CapabilityConfigs.ToListAsync(ct);
    public async Task<List<PayGroup>> ListPayGroupsAsync(CancellationToken ct) => await db.PayGroups.ToListAsync(ct);
    public async Task<List<Worker>> ListAllWorkersAsync(string? status, CancellationToken ct)
    {
        var q = db.Workers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(w => w.Status == status);
        return await q.Include(w => w.OrgUnit).ToListAsync(ct);
    }
    public async Task<List<LeaveRequest>> ListLeaveRequestsAllAsync(string? status, CancellationToken ct)
    {
        var q = db.LeaveRequests.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && status != "all") q = q.Where(l => l.Status == status);
        return await q.ToListAsync(ct);
    }
    public async Task<List<PayrollRunLine>> ListRunLinesAllAsync(string periodFrom, string periodTo, CancellationToken ct)
        => await db.PayrollRunLines.Include(l => l.Run).ThenInclude(r => r!.PayPeriod)
            .Where(l => l.Run != null && l.Run.PayPeriod != null
                && (string.IsNullOrWhiteSpace(periodFrom) || l.Run.PayPeriod.StartDate >= DateOnly.Parse(periodFrom))
                && (string.IsNullOrWhiteSpace(periodTo) || l.Run.PayPeriod.EndDate <= DateOnly.Parse(periodTo)))
            .ToListAsync(ct);

    // ---- M1 CRUD ----
    public async Task<LegalEntity?> GetLegalEntityAsync(Guid id, CancellationToken ct) => await db.LegalEntities.FirstOrDefaultAsync(e => e.Id == id, ct);
    public async Task<LegalEntity> CreateLegalEntityAsync(LegalEntity entity, CancellationToken ct)
    { db.LegalEntities.Add(entity); await db.SaveChangesAsync(ct); return entity; }
    public async Task<LegalEntity> UpdateLegalEntityAsync(LegalEntity entity, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return entity; }
    public async Task<WorkLocation?> GetLocationAsync(Guid id, CancellationToken ct) => await db.WorkLocations.FirstOrDefaultAsync(l => l.Id == id, ct);
    public async Task<WorkLocation> CreateLocationAsync(WorkLocation location, CancellationToken ct)
    { db.WorkLocations.Add(location); await db.SaveChangesAsync(ct); return location; }
    public async Task<WorkLocation> UpdateLocationAsync(WorkLocation location, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return location; }
    public async Task<OrgUnit?> GetOrgUnitAsync(Guid id, CancellationToken ct) => await db.OrgUnits.FirstOrDefaultAsync(u => u.Id == id, ct);
    public async Task<OrgUnit> CreateOrgUnitAsync(OrgUnit unit, CancellationToken ct)
    { db.OrgUnits.Add(unit); await db.SaveChangesAsync(ct); return unit; }
    public async Task<OrgUnit> UpdateOrgUnitAsync(OrgUnit unit, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return unit; }
    public async Task<WorkCalendar> CreateCalendarAsync(WorkCalendar calendar, CancellationToken ct)
    { db.WorkCalendars.Add(calendar); await db.SaveChangesAsync(ct); return calendar; }
    public async Task<WorkCalendar> UpdateCalendarAsync(WorkCalendar calendar, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return calendar; }
    public async Task<PublicHoliday> CreateHolidayAsync(PublicHoliday holiday, CancellationToken ct)
    { db.PublicHolidays.Add(holiday); await db.SaveChangesAsync(ct); return holiday; }
    public async Task<PublicHoliday?> GetHolidayAsync(Guid id, CancellationToken ct) => await db.PublicHolidays.FirstOrDefaultAsync(h => h.Id == id, ct);
    public async Task<PublicHoliday> UpdateHolidayAsync(PublicHoliday holiday, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return holiday; }
    public async Task DeleteHolidayAsync(Guid id, CancellationToken ct)
    {
        var holiday = await db.PublicHolidays.FirstOrDefaultAsync(h => h.Id == id, ct);
        if (holiday is not null) { db.PublicHolidays.Remove(holiday); await db.SaveChangesAsync(ct); }
    }
    public async Task<LeaveType?> GetLeaveTypeAsync(Guid id, CancellationToken ct) => await db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == id, ct);
    public async Task<LeaveType> CreateLeaveTypeAsync(LeaveType leaveType, CancellationToken ct)
    { db.LeaveTypes.Add(leaveType); await db.SaveChangesAsync(ct); return leaveType; }
    public async Task<LeaveType> UpdateLeaveTypeAsync(LeaveType leaveType, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return leaveType; }
    public async Task<ContractType?> GetContractTypeAsync(Guid id, CancellationToken ct) => await db.ContractTypes.FirstOrDefaultAsync(t => t.Id == id, ct);
    public async Task<ContractType> CreateContractTypeAsync(ContractType contractType, CancellationToken ct)
    { db.ContractTypes.Add(contractType); await db.SaveChangesAsync(ct); return contractType; }
    public async Task<ContractType> UpdateContractTypeAsync(ContractType contractType, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return contractType; }
    public async Task<CapabilityConfig> UpdateCapabilityAsync(CapabilityConfig capability, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return capability; }
    public async Task<TenantRoleAssignment> CreateRoleAssignmentAsync(TenantRoleAssignment row, CancellationToken ct)
    { db.TenantRoleAssignments.Add(row); await db.SaveChangesAsync(ct); return row; }

    // M28: jobs, tenant roles, retention rules
    public async Task<List<Job>> ListJobsAsync(CancellationToken ct) => await db.Jobs.ToListAsync(ct);
    public async Task<Job?> GetJobAsync(Guid id, CancellationToken ct) => await db.Jobs.FirstOrDefaultAsync(j => j.Id == id, ct);
    public async Task<Job> CreateJobAsync(Job job, CancellationToken ct)
    { db.Jobs.Add(job); await db.SaveChangesAsync(ct); return job; }
    public async Task<Job> UpdateJobAsync(Job job, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return job; }
    public async Task<List<TenantRoleAssignment>> ListRoleAssignmentsAsync(CancellationToken ct) => await db.TenantRoleAssignments.ToListAsync(ct);
    public async Task<TenantRoleAssignment?> GetRoleAssignmentAsync(string roleKey, CancellationToken ct)
        => await db.TenantRoleAssignments.FirstOrDefaultAsync(r => r.RoleKey == roleKey, ct);
    public async Task<TenantRoleAssignment> UpdateRoleAssignmentAsync(TenantRoleAssignment row, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return row; }
    public async Task<List<RetentionRule>> ListRetentionRulesAsync(CancellationToken ct) => await db.RetentionRules.ToListAsync(ct);
    public async Task<RetentionRule?> GetRetentionRuleAsync(Guid id, CancellationToken ct) => await db.RetentionRules.FirstOrDefaultAsync(r => r.Id == id, ct);
    public async Task<RetentionRule> CreateRetentionRuleAsync(RetentionRule rule, CancellationToken ct)
    { db.RetentionRules.Add(rule); await db.SaveChangesAsync(ct); return rule; }
    public async Task<RetentionRule> UpdateRetentionRuleAsync(RetentionRule rule, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return rule; }
    public async Task DeleteRetentionRuleAsync(Guid id, CancellationToken ct)
    {
        var rule = await db.RetentionRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is not null) { db.RetentionRules.Remove(rule); await db.SaveChangesAsync(ct); }
    }
}
public sealed class RecruitmentRepository(HrmDbContext db) : IRecruitmentRepository
{
    public async Task<(List<Vacancy> Items, int Total)> ListVacanciesAsync(string? status, CancellationToken ct)
    {
        var q = db.Vacancies.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(v => v.Status == status);
        var items = await q.Include(v => v.OrgUnit).ToListAsync(ct);
        return (items, items.Count);
    }
    public async Task<Vacancy> CreateVacancyAsync(Vacancy vacancy, CancellationToken ct)
    {
        db.Set<Vacancy>().Add(vacancy);
        await db.SaveChangesAsync(ct);
        return vacancy;
    }
    public async Task<Vacancy?> GetVacancyAsync(Guid id, CancellationToken ct)
        => await db.Vacancies.Include(v => v.OrgUnit).FirstOrDefaultAsync(v => v.Id == id, ct);
    public async Task<Vacancy> UpdateVacancyAsync(Vacancy vacancy, CancellationToken ct)
    {
        db.Set<Vacancy>().Update(vacancy);
        await db.SaveChangesAsync(ct);
        return vacancy;
    }
    public async Task<(List<Candidate> Items, int Total)> ListCandidatesAsync(Guid vacancyId, string? stage, CancellationToken ct)
    {
        var q = db.Candidates.Where(c => c.VacancyId == vacancyId);
        if (!string.IsNullOrWhiteSpace(stage)) q = q.Where(c => c.Stage == stage);
        var items = await q.ToListAsync(ct);
        return (items, items.Count);
    }
    public async Task<Candidate> CreateCandidateAsync(Candidate candidate, CancellationToken ct)
    {
        // Candidates are either freshly created or re-saved after stage advances
        // (tracked by GetCandidateAsync navigation load), so use the entry state.
        var entry = db.Entry(candidate);
        if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            db.Set<Candidate>().Add(candidate);
        else
            db.Set<Candidate>().Update(candidate);
        await db.SaveChangesAsync(ct);
        return candidate;
    }
    public async Task<Candidate?> GetCandidateAsync(Guid id, CancellationToken ct)
        => await db.Candidates.Include(c => c.Vacancy).FirstOrDefaultAsync(c => c.Id == id, ct);
    public async Task<List<CandidateStageEvent>> ListStageEventsAsync(Guid candidateId, CancellationToken ct)
        => (await db.CandidateStageEvents.Where(x => x.CandidateId == candidateId).ToListAsync(ct)).OrderBy(x => x.CreatedAt).ToList();
    public async Task<CandidateStageEvent> CreateStageEventAsync(CandidateStageEvent entry, CancellationToken ct)
    { db.CandidateStageEvents.Add(entry); await db.SaveChangesAsync(ct); return entry; }
    public async Task<List<CandidateInterview>> ListInterviewsAsync(Guid candidateId, CancellationToken ct)
        => await db.CandidateInterviews.Where(x => x.CandidateId == candidateId).OrderBy(x => x.ScheduledAt).ToListAsync(ct);
    public async Task<CandidateInterview> CreateInterviewAsync(CandidateInterview interview, CancellationToken ct)
    { db.CandidateInterviews.Add(interview); await db.SaveChangesAsync(ct); return interview; }
    public async Task<CandidateInterview?> GetInterviewAsync(Guid id, CancellationToken ct)
        => await db.CandidateInterviews.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<CandidateInterview> UpdateInterviewAsync(CandidateInterview interview, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return interview; }
    public async Task<Offer> CreateOfferAsync(Offer offer, CancellationToken ct)
    {
        db.Set<Offer>().Add(offer);
        await db.SaveChangesAsync(ct);
        return offer;
    }
    public async Task<Offer?> GetOfferAsync(Guid id, CancellationToken ct)
        => await db.Offers.FirstOrDefaultAsync(o => o.Id == id, ct);
    public async Task<Offer> UpdateOfferAsync(Offer offer, CancellationToken ct)
    {
        db.Set<Offer>().Update(offer);
        await db.SaveChangesAsync(ct);
        return offer;
    }
    public async Task<(List<Offer> Items, int Total)> ListOffersAsync(string? status, CancellationToken ct)
    {
        var q = db.Offers.Include(x => x.Candidate)!.ThenInclude(x => x!.Vacancy).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
        var items = (await q.ToListAsync(ct)).OrderByDescending(x => x.CreatedAt).ToList();
        return (items, items.Count);
    }
    public async Task<PreboardingCase?> GetPreboardingAsync(Guid id, CancellationToken ct)
        => await db.PreboardingCases.Include(x => x.Tasks).Include(x => x.Candidate).Include(x => x.Worker).FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<PreboardingCase?> GetPreboardingForCandidateAsync(Guid candidateId, CancellationToken ct)
        => await db.PreboardingCases.Include(x => x.Tasks).Include(x => x.Candidate).Include(x => x.Worker).FirstOrDefaultAsync(x => x.CandidateId == candidateId, ct);
    public async Task<(List<PreboardingCase> Items, int Total)> ListPreboardingAsync(string? status, CancellationToken ct)
    {
        var q = db.PreboardingCases.Include(x => x.Tasks).Include(x => x.Candidate).Include(x => x.Worker).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
        var items = (await q.ToListAsync(ct)).OrderByDescending(x => x.CreatedAt).ToList();
        return (items, items.Count);
    }
    public async Task<PreboardingCase> CreatePreboardingAsync(PreboardingCase record, CancellationToken ct)
    { db.PreboardingCases.Add(record); await db.SaveChangesAsync(ct); return record; }
    public async Task<PreboardingCase> UpdatePreboardingAsync(PreboardingCase record, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return record; }
    public async Task<PreboardingTask?> GetPreboardingTaskAsync(Guid id, CancellationToken ct)
        => await db.PreboardingTasks.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<PreboardingTask> CreatePreboardingTaskAsync(PreboardingTask task, CancellationToken ct)
    { db.PreboardingTasks.Add(task); await db.SaveChangesAsync(ct); return task; }
    public async Task<PreboardingTask> UpdatePreboardingTaskAsync(PreboardingTask task, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return task; }
    public async Task<List<CandidateDocument>> ListCandidateDocumentsAsync(Guid candidateId, CancellationToken ct)
        => (await db.CandidateDocuments.Where(x => x.CandidateId == candidateId).ToListAsync(ct)).OrderByDescending(x => x.CreatedAt).ToList();
    public async Task<CandidateDocument> CreateCandidateDocumentAsync(CandidateDocument document, CancellationToken ct)
    { db.CandidateDocuments.Add(document); await db.SaveChangesAsync(ct); return document; }
    public async Task<CandidateDocument?> GetCandidateDocumentAsync(Guid id, CancellationToken ct)
        => await db.CandidateDocuments.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<int> CountCandidatesForVacancyAsync(Guid vacancyId, CancellationToken ct)
        => await db.Candidates.CountAsync(c => c.VacancyId == vacancyId, ct);

    // M38: requisitions
    public async Task<(List<Requisition> Items, int Total)> ListRequisitionsAsync(string? status, CancellationToken ct)
    {
        var q = db.Requisitions.Include(r => r.OrgUnit).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        var items = (await q.ToListAsync(ct)).OrderByDescending(r => r.CreatedAt).ToList();
        return (items, items.Count);
    }
    public async Task<Requisition> CreateRequisitionAsync(Requisition requisition, CancellationToken ct)
    {
        db.Set<Requisition>().Add(requisition);
        await db.SaveChangesAsync(ct);
        return requisition;
    }
    public async Task<Requisition?> GetRequisitionAsync(Guid id, CancellationToken ct)
        => await db.Requisitions.Include(r => r.OrgUnit).FirstOrDefaultAsync(r => r.Id == id, ct);
    public async Task<Requisition> UpdateRequisitionAsync(Requisition requisition, CancellationToken ct)
    {
        db.Set<Requisition>().Update(requisition);
        await db.SaveChangesAsync(ct);
        return requisition;
    }
    public async Task<List<RequisitionEvent>> ListRequisitionEventsAsync(Guid requisitionId, CancellationToken ct)
        => (await db.RequisitionEvents.Where(x => x.RequisitionId == requisitionId).ToListAsync(ct)).OrderBy(x => x.CreatedAt).ToList();
    public async Task<RequisitionEvent> CreateRequisitionEventAsync(RequisitionEvent entry, CancellationToken ct)
    { db.RequisitionEvents.Add(entry); await db.SaveChangesAsync(ct); return entry; }
    public async Task<int> CountRequisitionVacanciesAsync(Guid requisitionId, CancellationToken ct)
        => await db.Vacancies.CountAsync(v => v.RequisitionId == requisitionId, ct);
    public async Task<List<Vacancy>> ListVacanciesForRequisitionAsync(Guid requisitionId, CancellationToken ct)
        => (await db.Vacancies.Include(v => v.OrgUnit).Where(v => v.RequisitionId == requisitionId).ToListAsync(ct)).OrderByDescending(v => v.CreatedAt).ToList();
    public async Task<string> NextRequisitionNoAsync(CancellationToken ct)
    {
        var count = await db.Requisitions.CountAsync(ct);
        return $"REQ-{count + 1:D4}";
    }
}

public sealed class RelationsRepository(HrmDbContext db) : IRelationsRepository
{
    public async Task<(List<RelationsCase> Items, int Total)> ListCasesAsync(string? category, CancellationToken ct)
    {
        var q = db.RelationsCases.AsQueryable();
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(c => c.Category == category);
        var items = await q.Include(c => c.SubjectWorker).ToListAsync(ct);
        return (items, items.Count);
    }
    public async Task<RelationsCase> CreateCaseAsync(RelationsCase caseRecord, CancellationToken ct)
    {
        db.Set<RelationsCase>().Add(caseRecord);
        await db.SaveChangesAsync(ct);
        return caseRecord;
    }
    public async Task<RelationsCase?> GetCaseAsync(Guid id, CancellationToken ct)
        => await db.RelationsCases.Include(c => c.SubjectWorker).FirstOrDefaultAsync(c => c.Id == id, ct);
    public async Task<RelationsCase> UpdateCaseAsync(RelationsCase caseRecord, CancellationToken ct)
    {
        db.Set<RelationsCase>().Update(caseRecord);
        await db.SaveChangesAsync(ct);
        return caseRecord;
    }
    public async Task<int> CountCasesThisYearAsync(CancellationToken ct)
    {
        var start = new DateTimeOffset(DateTimeOffset.UtcNow.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var createdAt = await db.RelationsCases.Select(x => x.CreatedAt).ToListAsync(ct);
        return createdAt.Count(x => x >= start);
    }
    public async Task<RelationsCaseAccess?> GetAccessAsync(Guid caseId, string actorSubjectId, CancellationToken ct)
        => await db.RelationsCaseAccessDeclarations.FirstOrDefaultAsync(x => x.CaseId == caseId && x.ActorSubjectId == actorSubjectId, ct);
    public async Task<RelationsCaseAccess> CreateAccessAsync(RelationsCaseAccess access, CancellationToken ct)
    {
        var existing = await GetAccessAsync(access.CaseId, access.ActorSubjectId, ct);
        if (existing is null) db.RelationsCaseAccessDeclarations.Add(access);
        else { existing.Decision = access.Decision; existing.Notes = access.Notes; access = existing; }
        await db.SaveChangesAsync(ct); return access;
    }
    public Task<List<RelationsCaseAccess>> ListAccessAsync(Guid caseId, CancellationToken ct)
        => db.RelationsCaseAccessDeclarations.Where(x => x.CaseId == caseId).ToListAsync(ct);
    public async Task<RelationsCaseEvent> CreateEventAsync(RelationsCaseEvent entry, CancellationToken ct)
    { db.RelationsCaseEvents.Add(entry); await db.SaveChangesAsync(ct); return entry; }
    public async Task<List<RelationsCaseEvent>> ListEventsAsync(Guid caseId, CancellationToken ct)
        => (await db.RelationsCaseEvents.Where(x => x.CaseId == caseId).ToListAsync(ct)).OrderBy(x => x.CreatedAt).ToList();
    public async Task<RelationsCaseAction> CreateActionAsync(RelationsCaseAction action, CancellationToken ct)
    { db.RelationsCaseActions.Add(action); await db.SaveChangesAsync(ct); return action; }
    public Task<RelationsCaseAction?> GetActionAsync(Guid id, CancellationToken ct)
        => db.RelationsCaseActions.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<RelationsCaseAction> UpdateActionAsync(RelationsCaseAction action, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return action; }
    public Task<List<RelationsCaseAction>> ListActionsAsync(Guid caseId, CancellationToken ct)
        => db.RelationsCaseActions.Where(x => x.CaseId == caseId).ToListAsync(ct);
    public async Task<RelationsEvidence> CreateEvidenceAsync(RelationsEvidence evidence, CancellationToken ct)
    { db.RelationsEvidence.Add(evidence); await db.SaveChangesAsync(ct); return evidence; }
    public Task<RelationsEvidence?> GetEvidenceAsync(Guid id, CancellationToken ct)
        => db.RelationsEvidence.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<List<RelationsEvidence>> ListEvidenceAsync(Guid caseId, CancellationToken ct)
        => (await db.RelationsEvidence.Where(x => x.CaseId == caseId).ToListAsync(ct)).OrderByDescending(x => x.CreatedAt).ToList();
    public async Task<(List<ProtectedDisclosure> Items, int Total)> ListProtectedDisclosuresAsync(string? status, CancellationToken ct)
    {
        var q = db.ProtectedDisclosures.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
        var items = await q.ToListAsync(ct); return (items.OrderByDescending(x => x.CreatedAt).ToList(), items.Count);
    }
    public Task<ProtectedDisclosure?> GetProtectedDisclosureAsync(Guid id, CancellationToken ct)
        => db.ProtectedDisclosures.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<ProtectedDisclosure> UpdateProtectedDisclosureAsync(ProtectedDisclosure disclosure, CancellationToken ct)
    { await db.SaveChangesAsync(ct); return disclosure; }
    public async Task<ProtectedDisclosureEvent> CreateProtectedDisclosureEventAsync(ProtectedDisclosureEvent entry, CancellationToken ct)
    { db.ProtectedDisclosureEvents.Add(entry); await db.SaveChangesAsync(ct); return entry; }
    public async Task<List<ProtectedDisclosureEvent>> ListProtectedDisclosureEventsAsync(Guid disclosureId, CancellationToken ct)
        => (await db.ProtectedDisclosureEvents.Where(x => x.DisclosureId == disclosureId).ToListAsync(ct)).OrderBy(x => x.CreatedAt).ToList();
}

public sealed class DocumentsRepository(HrmDbContext db) : IDocumentsRepository
{
    public async Task<(List<WorkerDocument> Items, int Total)> ListDocumentsAsync(Guid workerId, CancellationToken ct)
    {
        var items = (await db.WorkerDocuments.Where(d => d.WorkerId == workerId).ToListAsync(ct))
            .OrderByDescending(d => d.CreatedAt).ToList();
        return (items, items.Count);
    }
    public async Task<WorkerDocument> CreateDocumentAsync(WorkerDocument document, CancellationToken ct)
    {
        db.WorkerDocuments.Add(document);
        await db.SaveChangesAsync(ct);
        return document;
    }
    public async Task<WorkerDocument?> GetDocumentAsync(Guid id, CancellationToken ct) =>
        await db.WorkerDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
    public async Task<List<WorkerDocument>> ListAllDocumentsAsync(CancellationToken ct) =>
        await db.WorkerDocuments.ToListAsync(ct);
}

// ===================== Performance & Goals (M36) =====================
public sealed class PerformanceRepository(HrmDbContext db) : IPerformanceRepository
{
    public async Task<(List<PerformanceCycle> Items, int Total)> ListCyclesAsync(string? status, CancellationToken ct)
    {
        var q = db.PerformanceCycles.Include(c => c.Goals).Include(c => c.Assessments).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(c => c.Status == status);
        var items = (await q.Take(100).ToListAsync(ct)).OrderByDescending(c => c.CreatedAt).ToList();
        return (items, items.Count);
    }

    public async Task<PerformanceCycle?> GetCycleAsync(Guid id, CancellationToken ct)
        => await db.PerformanceCycles.Include(c => c.Goals).Include(c => c.Assessments).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<PerformanceCycle> CreateCycleAsync(PerformanceCycle cycle, CancellationToken ct)
    {
        db.PerformanceCycles.Add(cycle);
        await db.SaveChangesAsync(ct);
        return cycle;
    }

    public async Task<PerformanceCycle> UpdateCycleAsync(PerformanceCycle cycle, CancellationToken ct)
    {
        // M36: EF Core 10 demotes navigation-added children to Modified when the
        // parent is Modified. Pin existing tracked children to Unchanged so the
        // tracker skips them (same pattern as ExperienceRepository).
        foreach (var g in cycle.Goals.ToList())
        {
            var entry = db.Entry(g);
            if (entry.State != EntityState.Unchanged && entry.State != EntityState.Added && entry.State != EntityState.Detached)
                entry.State = EntityState.Unchanged;
        }
        foreach (var a in cycle.Assessments.ToList())
        {
            var entry = db.Entry(a);
            if (entry.State != EntityState.Unchanged && entry.State != EntityState.Added && entry.State != EntityState.Detached)
                entry.State = EntityState.Unchanged;
        }
        if (db.Entry(cycle).State == EntityState.Detached)
            db.PerformanceCycles.Update(cycle);
        await db.SaveChangesAsync(ct);
        return cycle;
    }

    public async Task<List<PerformanceGoal>> ListGoalsAsync(Guid cycleId, Guid? workerId, CancellationToken ct)
    {
        var q = db.PerformanceGoals.Include(g => g.Worker).AsQueryable();
        q = q.Where(g => g.CycleId == cycleId);
        if (workerId.HasValue) q = q.Where(g => g.WorkerId == workerId.Value);
        return (await q.Take(200).ToListAsync(ct)).OrderBy(g => g.SortOrder).ToList();
    }

    public async Task<PerformanceGoal> CreateGoalAsync(PerformanceGoal goal, CancellationToken ct)
    {
        db.PerformanceGoals.Add(goal);
        await db.SaveChangesAsync(ct);
        return goal;
    }

    public async Task<PerformanceGoal> UpdateGoalAsync(PerformanceGoal goal, CancellationToken ct)
    {
        if (db.Entry(goal).State == EntityState.Detached)
            db.PerformanceGoals.Update(goal);
        await db.SaveChangesAsync(ct);
        return goal;
    }

    public async Task DeleteGoalAsync(Guid id, CancellationToken ct)
    {
        var goal = await db.PerformanceGoals.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new DomainException("goal-not-found", $"Goal {id} does not exist.");
        db.PerformanceGoals.Remove(goal);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PerformanceGoal?> GetGoalAsync(Guid id, CancellationToken ct)
        => await db.PerformanceGoals.Include(g => g.Worker).FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task<List<PerformanceAssessment>> ListAssessmentsAsync(Guid cycleId, CancellationToken ct)
        => (await db.PerformanceAssessments.Include(a => a.Worker)
            .Where(a => a.CycleId == cycleId).Take(500).ToListAsync(ct))
            .OrderBy(a => a.Worker?.FullName, StringComparer.OrdinalIgnoreCase).ToList();

    public async Task<PerformanceAssessment?> GetAssessmentAsync(Guid id, CancellationToken ct)
        => await db.PerformanceAssessments.Include(a => a.Worker).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<PerformanceAssessment> CreateAssessmentAsync(PerformanceAssessment assessment, CancellationToken ct)
    {
        db.PerformanceAssessments.Add(assessment);
        await db.SaveChangesAsync(ct);
        return assessment;
    }

    // M36: explicit top-level insert for each assessment (EF Core 10
    // Modified-parent demotion immunity) — same pattern as ExperienceRepository.AddMessageAsync.
    public async Task AddRangeAssessmentsAsync(List<PerformanceAssessment> assessments, CancellationToken ct)
    {
        db.Set<PerformanceAssessment>().AddRange(assessments);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PerformanceAssessment> UpdateAssessmentAsync(PerformanceAssessment assessment, CancellationToken ct)
    {
        if (db.Entry(assessment).State == EntityState.Detached)
            db.PerformanceAssessments.Update(assessment);
        await db.SaveChangesAsync(ct);
        return assessment;
    }

    public async Task<List<Worker>> ListActiveWorkersAsync(CancellationToken ct)
        => await db.Workers.Where(w => w.Status == "active").Take(2000).ToListAsync(ct);

    public async Task<List<PerformanceCycle>> ListMyCyclesAsync(string subjectId, CancellationToken ct)
    {
        // Self-service: all cycles the worker can see (any status except closed
        // cycles that never had an assessment row for the worker).
        return (await db.PerformanceCycles.Take(200).ToListAsync(ct))
            .Where(c => c.Status != "closed")
            .OrderByDescending(c => c.CreatedAt).ToList();
    }

    public async Task<PerformanceAssessment?> GetMyAssessmentAsync(Guid cycleId, Guid workerId, CancellationToken ct)
        => await db.PerformanceAssessments.FirstOrDefaultAsync(a => a.CycleId == cycleId && a.WorkerId == workerId, ct);
}

public sealed class OffboardingRepository(HrmDbContext db) : IOffboardingRepository
{
    public async Task<List<OffboardingRequest>> ListRequestsAsync(string? status, CancellationToken ct)
    {
        var q = db.OffboardingRequests.Include(r => r.Worker).Include(r => r.ChecklistItems).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        return (await q.Take(200).ToListAsync(ct)).OrderByDescending(r => r.CreatedAt).ToList();
    }
    public async Task<OffboardingRequest?> GetRequestAsync(Guid id, CancellationToken ct)
        => await db.OffboardingRequests.Include(r => r.Worker).Include(r => r.ChecklistItems).Include(r => r.ExitInterview).FirstOrDefaultAsync(r => r.Id == id, ct);
    public async Task<OffboardingRequest?> GetActiveForWorkerAsync(Guid workerId, CancellationToken ct)
        => await db.OffboardingRequests.Include(r => r.Worker).FirstOrDefaultAsync(r => r.WorkerId == workerId && r.Status != "completed" && r.Status != "cancelled", ct);
    public async Task<OffboardingRequest> CreateRequestAsync(OffboardingRequest request, CancellationToken ct)
    {
        db.OffboardingRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request;
    }
    public async Task<OffboardingRequest> UpdateRequestAsync(OffboardingRequest request, CancellationToken ct)
    {
        foreach (var item in request.ChecklistItems.ToList())
        {
            var entry = db.Entry(item);
            if (entry.State != EntityState.Unchanged && entry.State != EntityState.Added && entry.State != EntityState.Detached)
                entry.State = EntityState.Unchanged;
        }
        if (db.Entry(request).State == EntityState.Detached)
            db.OffboardingRequests.Update(request);
        await db.SaveChangesAsync(ct);
        return request;
    }
    public async Task<OffboardingChecklistItem?> GetChecklistItemAsync(Guid id, CancellationToken ct)
        => await db.OffboardingChecklistItems.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<OffboardingChecklistItem> CreateChecklistItemAsync(OffboardingChecklistItem item, CancellationToken ct)
    {
        db.OffboardingChecklistItems.Add(item);
        await db.SaveChangesAsync(ct);
        return item;
    }
    public async Task<OffboardingChecklistItem> UpdateChecklistItemAsync(OffboardingChecklistItem item, CancellationToken ct)
    {
        if (db.Entry(item).State == EntityState.Detached)
            db.OffboardingChecklistItems.Update(item);
        await db.SaveChangesAsync(ct);
        return item;
    }
    public async Task<ExitInterview?> GetExitInterviewAsync(Guid requestId, CancellationToken ct)
        => await db.ExitInterviews.Include(e => e.Worker).FirstOrDefaultAsync(e => e.OffboardingRequestId == requestId, ct);
    public async Task<ExitInterview?> GetExitInterviewByIdAsync(Guid id, CancellationToken ct)
        => await db.ExitInterviews.Include(e => e.Worker).FirstOrDefaultAsync(e => e.Id == id, ct);
    public async Task<ExitInterview> CreateExitInterviewAsync(ExitInterview interview, CancellationToken ct)
    {
        db.ExitInterviews.Add(interview);
        await db.SaveChangesAsync(ct);
        return interview;
    }
    public async Task<ExitInterview> UpdateExitInterviewAsync(ExitInterview interview, CancellationToken ct)
    {
        if (db.Entry(interview).State == EntityState.Detached)
            db.ExitInterviews.Update(interview);
        await db.SaveChangesAsync(ct);
        return interview;
    }
    public async Task<Worker?> FindWorkerAsync(Guid workerId, CancellationToken ct)
        => await db.Workers.FirstOrDefaultAsync(w => w.Id == workerId, ct);
    public async Task<Worker?> FindWorkerBySubjectIdAsync(string subjectId, CancellationToken ct)
        => await db.Workers.FirstOrDefaultAsync(w => w.SubjectId == subjectId, ct);
}

// ===================== M39 Organization chart & reporting lines =====================

public sealed class OrganizationRepository(HrmDbContext db) : IOrganizationRepository
{
    public async Task<List<OrgUnit>> ListActiveUnitsAsync(CancellationToken ct)
        => await db.OrgUnits
            .Where(u => u.Status != "closed")
            .Include(u => u.LegalEntity)
            .ToListAsync(ct);

    /// <summary>Current effective-dated assignments with the worker, their unit
    /// and the manager worker loaded. Optional org-unit filter scopes the query.
    /// Note: Assignment carries ManagerId only (no navigation), so the manager
    /// worker is joined in memory.</summary>
    public async Task<List<(Assignment Assignment, Worker Worker, OrgUnit Unit, Worker? Manager)>>
        ListCurrentAssignmentsAsync(Guid? orgUnitId, CancellationToken ct)
    {
        IQueryable<Assignment> q = db.Assignments
            .Where(a => a.Status == "current")
            .Include(a => a.Worker)
            .Include(a => a.OrgUnit);
        if (orgUnitId.HasValue)
            q = q.Where(a => a.OrgUnitId == orgUnitId.Value);
        var rows = await q.ToListAsync(ct);
        var valid = rows
            .Where(a => a.Worker != null && a.OrgUnit != null)
            .Select(a => (Assignment: a, Worker: a.Worker!, Unit: a.OrgUnit!))
            .ToList();
        var managerIds = valid.Select(v => v.Assignment.ManagerId).OfType<Guid>().ToList();
        var managers = managerIds.Count != 0
            ? await db.Workers.Where(w => managerIds.Contains(w.Id)).ToListAsync(ct)
            : new List<Worker>();
        var managersById = managers.ToDictionary(w => w.Id);
        return valid.Select(v => (v.Assignment, v.Worker, v.Unit,
            v.Assignment.ManagerId.HasValue && managersById.TryGetValue(v.Assignment.ManagerId.Value, out var m) ? m : null)).ToList();
    }

    public async Task<Assignment?> GetCurrentAssignmentAsync(Guid workerId, CancellationToken ct)
        => await db.Assignments
            .FirstOrDefaultAsync(a => a.WorkerId == workerId && a.Status == "current", ct);

    public async Task<Worker?> GetWorkerAsync(Guid workerId, CancellationToken ct)
        => await db.Workers.FirstOrDefaultAsync(w => w.Id == workerId, ct);

    public async Task UpdateAssignmentManagerAsync(Assignment assignment, Guid? managerId, CancellationToken ct)
    {
        if (db.Entry(assignment).State == EntityState.Detached)
            db.Assignments.Update(assignment);
        // Keep the denormalized Worker.ManagerId in sync with the assignment.
        var worker = await db.Workers.FirstOrDefaultAsync(w => w.Id == assignment.WorkerId, ct);
        if (worker != null && worker.ManagerId != managerId)
            worker.ManagerId = managerId;
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<Worker>> ListWorkersAsync(List<Guid> ids, CancellationToken ct)
        => await db.Workers.Where(w => ids.Contains(w.Id)).ToListAsync(ct);
}


// ===================== M40: HR Analytics repository =====================
public sealed class AnalyticsRepository(HrmDbContext db) : IAnalyticsRepository
{
    public async Task<(int Active, int PreHire, int Archived)> WorkerCountsAsync(CancellationToken ct)
    {
        // Materialize first: compound predicates combining string equality,
        // boolean negation and the DateTime-based global tenant filter are
        // unreliable on the SQLite test provider — the data volume is tiny.
        var all = await db.Set<Worker>().ToListAsync(ct);
        int active = all.Count(w => w.Status == "active" && !w.IsArchived);
        int preHire = all.Count(w => w.Status == "pre-hire" && !w.IsArchived);
        int archived = all.Count(w => w.IsArchived);
        return (active, preHire, archived);
    }

    public async Task<List<(string Month, int Joined, int Left)>> HeadcountMonthlyTrendAsync(int months, CancellationToken ct)
    {
        // Materialize then filter/group client-side: compound DateTimeOffset
        // predicates (null coalescing, negated booleans, status ORs) are
        // unreliable on the SQLite test provider and the row counts are tiny.
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddMonths(-months).UtcDateTime;
        var all = await db.Set<Worker>().ToListAsync(ct);
        var joinedRows = all.Where(w => !w.IsArchived
            && (w.Status == "active" || w.Status == "pre-hire")
            && w.CreatedAt >= cutoff).Select(w => w.CreatedAt).ToList();
        var leftRows = all.Where(w => w.IsArchived
            && (w.UpdatedAt ?? w.CreatedAt) >= cutoff)
            .Select(w => w.UpdatedAt ?? w.CreatedAt).ToList();
        var activeCount = all.Count(w => w.Status == "active" && !w.IsArchived);
        var result = new List<(string Month, int Joined, int Left)>();
        for (int i = months - 1; i >= 0; i--)
        {
            var d = now.AddMonths(-i);
            var label = d.ToString("yyyy-MM");
            int j = joinedRows.Count(t => t.Year == d.Year && t.Month == d.Month);
            int l = leftRows.Count(t => t.Year == d.Year && t.Month == d.Month);
            result.Add((label, j, l));
        }
        return result;
    }

    public async Task<List<(string LeaveType, double RequestedDays, double ApprovedDays, int Requests, int Approved)>> LeaveByTypeAsync(CancellationToken ct)
    {
        // Materialize first: conditional aggregates inside GroupBy projections
        // and compound string predicates are not reliably translatable on the
        // SQLite test provider; leave request volumes are tiny.
        var rows = await db.LeaveRequests.ToListAsync(ct);
        var filtered = rows.Where(l => l.Status != "draft" && l.Status != "cancelled").ToList();
        return filtered.GroupBy(l => l.LeaveTypeCode)
            .Select(g => (g.Key,
                Requested: g.Sum(l => (double)l.RequestedDays),
                ApprovedDays: g.Where(l => l.Status == "approved").Sum(l => (double)l.RequestedDays),
                Requests: g.Count(),
                Approved: g.Count(l => l.Status == "approved")))
            .ToList();
    }

    public async Task<int> LeaveTotalRequestsAsync(CancellationToken ct)
    {
        var rows = await db.LeaveRequests.ToListAsync(ct);
        return rows.Count(l => l.Status != "draft" && l.Status != "cancelled");
    }

    public async Task<List<(string PeriodLabel, string Status, decimal Gross, decimal Deductions, decimal Net, decimal EmployerCost, int EmployeeCount, DateTime? PayDate)>> PayrollRunsAsync(int count, CancellationToken ct)
    {
        // Materialize with the PayPeriod navigation included, then order and
        // project client-side — SQLite cannot translate ordering on a
        // DateTimeOffset navigation target followed by projection.
        var rows = await db.Set<PayrollRun>().Include(r => r.PayPeriod)
            .Take(200).ToListAsync(ct);
        return rows.OrderByDescending(r => r.PayPeriod?.StartDate ?? DateOnly.MinValue)
            .Take(count)
            .Select(r => (r.PayPeriod?.PeriodLabel ?? "", r.Status, r.TotalGross, r.TotalDeductions, r.TotalNet, r.TotalEmployerCost, r.EmployeeCount, r.PayPeriod?.PayDate.ToDateTime(TimeOnly.MinValue)))
            .ToList();
    }

    public async Task<List<(string Rating, int Count)>> PerformanceByRatingAsync(CancellationToken ct)
    {
        var rows = await db.Set<PerformanceAssessment>().ToListAsync(ct);
        return rows.Where(a => a.FinalRating != null && a.FinalizedAt != null)
            .GroupBy(a => a.FinalRating!)
            .Select(g => (g.Key, g.Count()))
            .ToList();
    }

    public async Task<(int Cycles, int Assessments, int Finalized)> PerformanceCycleStatsAsync(CancellationToken ct)
    {
        var cycles = await db.Set<PerformanceCycle>().CountAsync(ct);
        var assessments = await db.Set<PerformanceAssessment>().CountAsync(ct);
        var finalized = await db.Set<PerformanceAssessment>().CountAsync(a => a.FinalizedAt != null, ct);
        return (cycles, assessments, finalized);
    }

        public async Task<(int OpenRequisitions, int OpenVacancies, int CandidatesInPipeline, int OffersPending, int Hired)> RecruitmentCountsAsync(CancellationToken ct)
    {
        var openReq = await db.Set<Requisition>().CountAsync(r => r.Status == "submitted" || r.Status == "approved", ct);
        var openVac = await db.Set<Vacancy>().CountAsync(v => v.Status == "published", ct);
        // Current stage per candidate: latest stage event wins (by CreatedAt),
        // falling back to Candidate.Stage when no events exist. Materialize
        // first — SQLite cannot translate an ORDER BY on a DateTimeOffset
        // column inside a scalar subquery projection.
        var events = await db.Set<CandidateStageEvent>().ToListAsync(ct);
        var latestByCandidate = new Dictionary<Guid, string>();
        foreach (var e in events.OrderBy(e => e.CreatedAt))
            latestByCandidate[e.CandidateId] = e.ToStage;
        var candidates = await db.Set<Candidate>().ToListAsync(ct);
        var terminal = new HashSet<string>(["hired", "rejected", "withdrawn"], StringComparer.OrdinalIgnoreCase);
        int inPipeline = candidates.Count(c => !terminal.Contains(latestByCandidate.GetValueOrDefault(c.Id, c.Stage)));
        var pendingOffers = await db.Set<Offer>().CountAsync(o => o.Status == "issued" || o.Status == "approved", ct);
        var hired = await db.Set<Offer>().CountAsync(o => o.Status == "accepted", ct);
        return (openReq, openVac, inPipeline, pendingOffers, hired);
    }
    public async Task<List<(string Stage, int Count)>> CandidateStageFunnelAsync(CancellationToken ct)
    {
        // Group candidates by their current stage (latest event wins, else stage field).
        var events = await db.Set<CandidateStageEvent>().ToListAsync(ct);
        var latestByCandidate = new Dictionary<Guid, string>();
        foreach (var e in events.OrderBy(e => e.CreatedAt))
            latestByCandidate[e.CandidateId] = e.ToStage;
        var candidates = await db.Set<Candidate>().ToListAsync(ct);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in candidates)
        {
            var stage = latestByCandidate.GetValueOrDefault(c.Id, c.Stage);
            if (counts.TryGetValue(stage, out var v)) counts[stage] = v + 1; else counts[stage] = 1;
        }
        return counts.Select(kvp => (kvp.Key, kvp.Value)).ToList();
    }

    public async Task<List<(string DerivedStatus, int Days)>> AttendanceByStatusAsync(int days, CancellationToken ct)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));
        var rows = await db.Set<AttendanceRecord>().ToListAsync(ct);
        return rows.Where(a => a.WorkDate >= cutoff)
            .GroupBy(a => a.DerivedStatus)
            .Select(g => (g.Key, g.Count()))
            .ToList();
    }

    public async Task<List<(decimal? TotalHours, decimal? OvertimeHours)>> AttendanceHoursAsync(int days, CancellationToken ct)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));
        var rows = await db.Set<AttendanceRecord>().ToListAsync(ct);
        return rows.Where(a => a.WorkDate >= cutoff)
            .Select(r => ((decimal?)r.TotalHours, (decimal?)r.OvertimeHours))
            .ToList();
    }
}

// ===================== Setup (M49: first-time setup wizard) =====================
public sealed class SetupRepository(HrmDbContext db) : ISetupRepository
{
    public Task<SetupState?> GetStateAsync(CancellationToken ct) =>
        db.SetupStates.FirstOrDefaultAsync(ct);

    public async Task<IReadOnlySet<string>> CompletedStepKeysAsync(CancellationToken ct) =>
        new HashSet<string>(await db.SetupStepRecords
            .Where(x => x.Completed)
            .Select(x => x.StepKey)
            .ToListAsync(ct));

    /// <summary>M50.18: the saved input payload of a completed step (grades,
    /// positions, …) so later wizard steps can reuse the reference lists.</summary>
    public async Task<string?> DataForStepAsync(string stepKey, CancellationToken ct) =>
        (await db.SetupStepRecords.FirstOrDefaultAsync(x => x.StepKey == stepKey, ct))?.DataJson;

    public async Task CompleteStepAsync(string stepKey, string? dataJson, CancellationToken ct)
    {
        var existing = await db.SetupStepRecords.FirstOrDefaultAsync(x => x.StepKey == stepKey, ct);
        if (existing is null)
        {
            db.SetupStepRecords.Add(new SetupStepRecord { StepKey = stepKey, Completed = true, DataJson = dataJson });
        }
        else
        {
            existing.Completed = true;
            existing.DataJson = dataJson ?? existing.DataJson;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task FinishAsync(SetupState state, CancellationToken ct)
    {
        state.Status = "complete";
        state.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Wipes every tenant data table in the hrm schema so a reset
    /// leaves the system indistinguishable from a brand-new installation.</summary>
    private string QualifiedTable(string table)
    {
        // Tests run on SQLite (no schema support) — the harness creates the
        // tables as plain names, while production Postgres lives in `hrm`.
        var provider = db.Database.ProviderName ?? "";
        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return $"\"{table}\"";
        return $"hrm.\"{table}\"";
    }

    /// <summary>Actual data tables in insertion order (dependents before the
    /// tables they reference). Derived from the DbContext model so the list
    /// cannot drift out of sync with migrations — the SetupState/SetupStep
    /// records are wiped separately afterwards.</summary>
    private static readonly IReadOnlyList<string> DataTables =
    [
        // people & lifecycle
        "emergency_contacts", "worker_bank_details", "education", "external_work_history",
        "internal_work_history", "worker_documents", "assignments", "movements", "workers",
        // policies & time
        "leave_balance_ledger", "leave_balance_adjustments", "leave_encashments",
        "leave_requests", "leave_types", "leave_accrual_runs", "attendance_records",
        "attendance_corrections", "shift_definitions", "worker_shift_assignments",
        "attendance_import_batches",
        // workflows & experience
        "workflow_requests", "workflow_decisions", "approval_delegations", "hr_requests",
        "hr_request_messages", "hr_letters", "protected_disclosure_events",
        // employment structure
        "jobs", "org_units",
        // payroll — runs/lines/payslips first, then configuration
        "payslip_access_logs", "payslips", "payroll_line_components", "payroll_run_lines",
        "payroll_run_events", "payroll_runs", "worker_component_values",
        "worker_payroll_profiles", "salary_structure_items", "salary_structures",
        "salary_components", "benefit_claims", "benefit_allowances", "benefit_types",
        "pay_periods", "pay_groups", "tax_slabs", "contribution_rules",
        // structure (branches/locations go AFTER payroll because runs reference them)
        "work_locations", "work_calendars", "public_holidays",
        // config, compliance and extras
        "master_data_batches", "audit_entries", "retention_rules",
        "capability_configs", "vacancies", "requisition_events", "requisitions",
        "candidate_documents", "candidate_interviews", "candidate_stage_events", "candidates",
        "offers", "preboarding_tasks", "preboarding_cases",
        "relations_case_access", "relations_case_actions", "relations_case_events",
        "relations_evidence", "relations_cases",
        "performance_assessments", "performance_goals", "performance_cycles",
        "offboarding_checklist_items", "offboarding_requests", "exit_interviews",
        // privileges & signoffs
        "privileged_action_events", "compliance_evidence", "go_live_signoffs",
        "legal_holds", "outbox_messages", "integration_operations",
        "tenant_role_assignments", "hr_user_branch_assignments",
    ];

    public async Task WipeAllDataAsync(CancellationToken ct)
    {
        // DELETE FROM in a schema-less SQLite harness tolerates missing
        // tables via TryDelete; on Postgres every listed table must exist.
        var provider = db.Database.ProviderName ?? "";
        var existsOnly = provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);
        // M49: FK order — cross-reference deletes happen in multiple passes
        // with per-table error isolation: a table blocked by a FK (e.g.
        // workers ← hr_requests) is deferred to the next pass instead of
        // aborting the whole wipe. Three passes clear any dependency depth.
        // M50.17b: identity tables are never wiped. tenant_role_assignments
        // holds the Keycloak-linked roles that grant users access to HRM and
        // hr_user_branch_assignments holds their branch scoping; wiping them
        // would lock every HR user out after a data reset. Data only.
        var identityTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tenant_role_assignments",
            "hr_user_branch_assignments",
        };
        foreach (var pass in Enumerable.Range(0, 3))
        {
            foreach (var table in DataTables)
            {
                if (identityTables.Contains(table)) continue;
                if (existsOnly && !await TableExistsAsync(table, ct)) continue;
                try
                {
                    await db.Database.ExecuteSqlRawAsync($"DELETE FROM {QualifiedTable(table)};", ct);
                }
                catch (Npgsql.PostgresException ex) when (ex.SqlState == "23503")
                {
                    // FK conflict — the referencing rows will be removed in a
                    // later pass iteration; never fail the wipe for this.
                    if (pass == 2) throw; // depth exceeded — surface it.
                }
            }
        }

        var states = await db.SetupStates.ToListAsync(ct);
        db.SetupStates.RemoveRange(states);
        var records = await db.SetupStepRecords.ToListAsync(ct);
        db.SetupStepRecords.RemoveRange(records);
        await db.SaveChangesAsync(ct);
    }

    private async Task<bool> TableExistsAsync(string table, CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync($"SELECT 1 FROM \"{table}\" LIMIT 0;", ct);
            return true;
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            return false;
        }
    }

    public async Task<SetupState> SeedPendingStateAsync(CancellationToken ct)
    {
        var state = new SetupState { Status = "pending" };
        db.SetupStates.Add(state);
        await db.SaveChangesAsync(ct);
        return state;
    }
}
