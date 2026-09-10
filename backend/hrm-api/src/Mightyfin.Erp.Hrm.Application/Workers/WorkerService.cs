using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Workers;

/// <summary>Worker record CRUD and search. All queries are tenant-scoped and
/// honour status filters; search indexes employee number, name and NRC.</summary>
public interface IWorkerService
{
    Task<Paged<WorkerDto>> ListAsync(WorkerListFilters filters, CancellationToken ct);
    Task<WorkerDto?> GetByIdAsync(Guid id, CancellationToken ct);
    // M14 identity link: worker record bound to the caller's Keycloak subject.
    Task<WorkerDto?> GetBySubjectAsync(string subjectId, CancellationToken ct);
    Task<WorkerDto> CreateAsync(WorkerCreateRequest request, CancellationToken ct);
    Task<WorkerDto> UpdateAsync(Guid id, WorkerUpdateRequest request, CancellationToken ct);
    Task ArchiveAsync(Guid id, CancellationToken ct);
    // M15 self-service: workers update their own profile through GET /hrm/me +
    // PUT /hrm/me/profile. Only employee-owned fields are accepted; admin-only
    // fields (status, grade, title, org, manager, dates) are never mutable here.
    Task<WorkerDto> UpdateOwnProfileAsync(WorkerSubjectUpdateRequest request, CancellationToken ct);
    // M27 P0 UX audit: admin identity-linking (PUT /workers/{id}/account-link)
    // — without this endpoint My HR / My documents / self-leave were a
    // circular dead end for every unlinked worker.
    Task<WorkerDto> LinkAccountAsync(Guid workerId, WorkerAccountLinkRequest request, CancellationToken ct);
    Task<Paged<AssignmentDto>> ListAssignmentsAsync(Guid workerId, CancellationToken ct);
    Task<AssignmentDto> CreateAssignmentAsync(AssignmentCreateRequest request, CancellationToken ct);
    Task<Paged<MovementDto>> ListMovementsAsync(Guid workerId, CancellationToken ct);
    Task<MovementDto> CreateMovementAsync(MovementCreateRequest request, CancellationToken ct);
    Task ExecuteMovementAsync(Guid movementId, CancellationToken ct);

    // M35: self-service notification preferences — GET /me/preferences and
    // PUT /me/preferences. Null = organisation defaults.
    Task<string?> GetMyPreferencesAsync(string subjectId, CancellationToken ct);
    Task<string?> UpdateMyPreferencesAsync(string subjectId, string preferencesJson, CancellationToken ct);
}

public sealed record AssignmentDto(Guid Id, string JobTitle, string? Grade, string ContractType,
    string Status, string StartDate, string? EndDate, string OrgUnitName, string LocationName);

public sealed record MovementDto(Guid Id, string MovementType, string Status, string Reason,
    string EffectiveDate, string? ToOrgUnitName, string? ToJobTitle, string? ToGrade,
    DateTimeOffset CreatedAt);

public sealed class WorkerServiceImpl(IWorkerRepository repo, IAuthzService authz, IIdProvider idGen) : IWorkerService
{
    public async Task<Paged<WorkerDto>> ListAsync(WorkerListFilters filters, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll", "manager");
        var (items, total) = await repo.ListAsync(filters, ct);
        var includeSensitive = authz.CanAccessSensitive("worker-identifiers");
        return new Paged<WorkerDto>(items.Select(x => Map(x, includeSensitive)).ToList(), total, filters.Page, filters.PageSize);
    }

    public async Task<WorkerDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll", "manager");
        var w = await repo.GetByIdAsync(id, ct);
        return w is null ? null : Map(w, authz.CanAccessSensitive("worker-identifiers"));
    }

    public async Task<WorkerDto?> GetBySubjectAsync(string subjectId, CancellationToken ct)
    {
        // M14 identity link: any authenticated role may resolve themselves.
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll", "manager", "employee", "investigator");
        var w = await repo.FindBySubjectIdAsync(subjectId, ct);
        return w is null ? null : Map(w, true);
    }

    public async Task<WorkerDto> CreateAsync(WorkerCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        // The UI never asks HR to type an employee number; issue one when empty.
        var employeeNo = string.IsNullOrWhiteSpace(request.EmployeeNo)
            ? await IssueEmployeeNoAsync(repo, ct)
            : request.EmployeeNo;
        var worker = new Worker
        {
            EmployeeNo = employeeNo,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName,
            PreferredName = request.PreferredName,
            Email = request.Email,
            PersonalEmail = request.PersonalEmail,
            Phone = request.Phone,
            Nrc = request.Nrc,
            PassportNo = request.PassportNo,
            Tpin = request.Tpin,
            NapsaNumber = request.NapsaNumber,
            NhimaNumber = request.NhimaNumber,
            Nationality = request.Nationality ?? "Zambian",
            DateOfBirth = request.DateOfBirth,
            WorkerType = request.WorkerType,
            Status = request.StartDate is not null && DateOnly.Parse(request.StartDate) <= DateOnly.FromDateTime(DateTime.UtcNow)
                ? "active"
                : "pre-hire",
            OrgUnitId = request.OrgUnitId,
            LocationId = request.LocationId,
            ManagerId = request.ManagerId,
            Grade = request.Grade,
            JobTitle = request.JobTitle,
            StartDate = request.StartDate is null ? null : DateOnly.Parse(request.StartDate),
        };
        foreach (var ec in request.EmergencyContacts ?? [])
            worker.EmergencyContacts.Add(new EmergencyContact { Relationship = ec.Relationship, FullName = ec.FullName, Phone = ec.Phone, IsPrimary = ec.IsPrimary });
        foreach (var bd in request.BankDetails ?? [])
            worker.BankDetails.Add(new WorkerBankDetail { BankName = bd.BankName, BranchCode = bd.BranchCode, AccountNumber = bd.AccountNumber, AccountName = bd.AccountName, PaymentMethod = bd.PaymentMethod, MobileMoneyNumber = bd.MobileMoneyNumber, IsPrimary = bd.IsPrimary });
        var created = await repo.CreateAsync(worker, ct);
        return Map(created, true);
    }

    public async Task<WorkerDto> UpdateAsync(Guid id, WorkerUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var worker = await repo.GetByIdAsync(id, ct)
            ?? throw new DomainException("worker-not-found", $"Worker {id} does not exist.");
        // M18 admin CRUD: an archived record is a historical one — edits on it
        // would silently resurrect a leaver into operational views.
        if (worker.IsArchived)
            throw new DomainException("worker-archived", "This worker record is archived. Reactivate it before editing, or create a new record instead.");
        if (request.FirstName is not null) worker.FirstName = request.FirstName;
        if (request.MiddleName is not null) worker.MiddleName = request.MiddleName;
        if (request.LastName is not null) worker.LastName = request.LastName;
        if (request.PreferredName is not null) worker.PreferredName = request.PreferredName;
        if (request.Email is not null) worker.Email = request.Email;
        if (request.PersonalEmail is not null) worker.PersonalEmail = request.PersonalEmail;
        if (request.Phone is not null) worker.Phone = request.Phone;
        if (request.Nrc is not null) worker.Nrc = request.Nrc;
        if (request.PassportNo is not null) worker.PassportNo = request.PassportNo;
        if (request.Tpin is not null) worker.Tpin = request.Tpin;
        if (request.NapsaNumber is not null) worker.NapsaNumber = request.NapsaNumber;
        if (request.NhimaNumber is not null) worker.NhimaNumber = request.NhimaNumber;
        if (request.Nationality is not null) worker.Nationality = request.Nationality;
        if (request.DateOfBirth is not null) worker.DateOfBirth = request.DateOfBirth;
        if (request.OrgUnitId.HasValue) worker.OrgUnitId = request.OrgUnitId.Value;
        if (request.LocationId.HasValue) worker.LocationId = request.LocationId.Value;
        if (request.ManagerId.HasValue) worker.ManagerId = request.ManagerId.Value;
        if (request.Grade is not null) worker.Grade = request.Grade;
        if (request.JobTitle is not null) worker.JobTitle = request.JobTitle;
        if (request.Status is not null) worker.Status = request.Status;
        if (request.StartDate is not null)
        {
            if (!DateOnly.TryParse(request.StartDate, out var startDate))
                throw new DomainException("invalid-start-date", "Employment start date must be a valid date.");
            if (worker.EndDate.HasValue && startDate > worker.EndDate.Value)
                throw new DomainException("invalid-employment-dates", "Employment start date cannot be after the employment end date.");
            worker.StartDate = startDate;
        }
        if (request.EndDate is not null) worker.EndDate = DateOnly.Parse(request.EndDate);
        // M27 P0 UX audit: the profile page's Link account action arrives here
        // (PUT /workers/{id}), so the admin update honours SubjectId with the
        // same single-link rule as LinkAccountAsync.
        if (request.SubjectId is not null)
        {
            var subject = request.SubjectId.Trim();
            if (subject.Length > 0)
            {
                var existing = await repo.FindBySubjectIdAsync(subject, ct);
                if (existing is not null && existing.Id != worker.Id)
                    throw new DomainException("subject-already-linked",
                        $"This identity is already linked to another worker record ({existing.EmployeeNo}).");
                worker.SubjectId = subject;
            }
            else
            {
                worker.SubjectId = null;
            }
        }
        if (request.EmergencyContacts is not null)
        {
            worker.EmergencyContacts.Clear();
            foreach (var ec in request.EmergencyContacts)
                worker.EmergencyContacts.Add(new EmergencyContact { Relationship = ec.Relationship, FullName = ec.FullName, Phone = ec.Phone, IsPrimary = ec.IsPrimary });
        }
        if (request.BankDetails is not null)
        {
            worker.BankDetails.Clear();
            foreach (var bd in request.BankDetails)
                worker.BankDetails.Add(new WorkerBankDetail { BankName = bd.BankName, BranchCode = bd.BranchCode, AccountNumber = bd.AccountNumber, AccountName = bd.AccountName, PaymentMethod = bd.PaymentMethod, MobileMoneyNumber = bd.MobileMoneyNumber, IsPrimary = bd.IsPrimary });
        }
        var updated = await repo.UpdateAsync(worker, ct);
        return Map(updated, true);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        await repo.ArchiveAsync(id, ct);
    }

    public async Task<WorkerDto> UpdateOwnProfileAsync(WorkerSubjectUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll", "manager", "employee", "investigator");
        // Self-service is keyed on the token subject, not a caller-supplied id,
        // so an employee can only ever reach their own record.
        var worker = await repo.FindBySubjectIdAsync(request.SubjectId, ct)
            ?? throw new DomainException("not-linked",
                "The signed-in identity is not linked to a worker record.");
        if (request.PreferredName is not null) worker.PreferredName = request.PreferredName;
        if (request.Email is not null) worker.Email = request.Email;
        if (request.Phone is not null) worker.Phone = request.Phone;
        if (request.Nrc is not null) worker.Nrc = request.Nrc;
        if (request.PassportNo is not null) worker.PassportNo = request.PassportNo;
        if (request.Tpin is not null) worker.Tpin = request.Tpin;
        if (request.NapsaNumber is not null) worker.NapsaNumber = request.NapsaNumber;
        if (request.NhimaNumber is not null) worker.NhimaNumber = request.NhimaNumber;
        if (request.Nationality is not null) worker.Nationality = request.Nationality;
        if (request.DateOfBirth is not null) worker.DateOfBirth = request.DateOfBirth;
        // EF Core 10 + SQLite treats entities with a non-default Guid key (Guid.CreateVersion7 initializer)
        // as "existing" (Modified) when attached to a tracked graph, so new children
        // must be inserted via explicit AddRange, after deletions are flushed first.
        var pendingEmergency = new List<EmergencyContact>();
        var pendingBank = new List<WorkerBankDetail>();
        if (request.EmergencyContacts is not null)
        {
            foreach (var existing in worker.EmergencyContacts.ToList())
                worker.EmergencyContacts.Remove(existing);
            foreach (var ec in request.EmergencyContacts)
                pendingEmergency.Add(new EmergencyContact { WorkerId = worker.Id, Relationship = ec.Relationship, FullName = ec.FullName, Phone = ec.Phone, IsPrimary = ec.IsPrimary });
        }
        if (request.BankDetails is not null)
        {
            foreach (var existing in worker.BankDetails.ToList())
                worker.BankDetails.Remove(existing);
            foreach (var bd in request.BankDetails)
                pendingBank.Add(new WorkerBankDetail { WorkerId = worker.Id, BankName = bd.BankName, BranchCode = bd.BranchCode, AccountNumber = bd.AccountNumber, AccountName = bd.AccountName, PaymentMethod = bd.PaymentMethod, MobileMoneyNumber = bd.MobileMoneyNumber, IsPrimary = bd.IsPrimary });
        }
        await repo.UpdateAsync(worker, ct); // scalars + child deletions flushed first
        // New children must be inserted via explicit Add (non-default Guid
        // keys otherwise make collection-attached entities "existing").
        if (pendingBank.Count > 0)
            await repo.AddBankDetailsAsync(pendingBank, ct);
        if (pendingEmergency.Count > 0)
            await repo.AddEmergencyContactsAsync(pendingEmergency, ct);
        return Map(worker, true);
    }

    // M27 P0 UX audit: admin identity-linking (PUT /workers/{id}/account-link).
    // hr_admin only: binds the worker to the identity's Keycloak subject so
    // My HR / My documents / self-leave resolve instead of 422-ing.
    public async Task<WorkerDto> LinkAccountAsync(Guid workerId, WorkerAccountLinkRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var worker = await repo.GetByIdAsync(workerId, ct)
            ?? throw new DomainException("worker-not-found", $"Worker {workerId} does not exist.");
        var subject = request.SubjectId?.Trim();
        if (string.IsNullOrWhiteSpace(subject))
            throw new DomainException("subject-required", "A subject id (from the identity provider) is required.");
        // Never allow two workers on one identity — that breaks self-service
        // isolation, so confirm the subject is free first.
        var existing = await repo.FindBySubjectIdAsync(subject, ct);
        if (existing is not null && existing.Id != worker.Id)
            throw new DomainException("subject-already-linked",
                $"That identity is already linked to {existing.FullName} ({existing.EmployeeNo}). Unlink it first.");
        worker.SubjectId = subject;
        var updated = await repo.UpdateAsync(worker, ct);
        return Map(updated, true);
    }

    public async Task<Paged<AssignmentDto>> ListAssignmentsAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll", "manager");
        var (items, total) = await repo.ListAssignmentsAsync(workerId, ct);
        return new Paged<AssignmentDto>(items.Select(a => new AssignmentDto(
            a.Id, a.JobTitle ?? "", a.Grade, a.ContractType, a.Status,
            a.StartDate.ToString(), a.EndDate?.ToString(),
            a.OrgUnit?.Name ?? "", a.Location?.Name ?? "")).ToList(), total, 1, 50);
    }

    public async Task<AssignmentDto> CreateAssignmentAsync(AssignmentCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var a = new Assignment
        {
            WorkerId = request.WorkerId,
            LegalEntityId = request.LegalEntityId,
            OrgUnitId = request.OrgUnitId,
            LocationId = request.LocationId,
            ManagerId = request.ManagerId,
            JobTitle = request.JobTitle,
            Grade = request.Grade,
            PositionNo = request.PositionNo,
            ContractType = request.ContractType,
            WorkPattern = request.WorkPattern,
            ProbationMonths = request.ProbationMonths,
            NoticeDays = request.NoticeDays,
            StartDate = DateOnly.Parse(request.StartDate),
            EndDate = request.EndDate is null ? null : DateOnly.Parse(request.EndDate),
            EffectiveFrom = DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime),
            Status = "current",
        };
        var created = await repo.CreateAssignmentAsync(a, ct);
        return new AssignmentDto(created.Id, created.JobTitle ?? "", created.Grade, created.ContractType,
            created.Status, created.StartDate.ToString(), created.EndDate?.ToString(),
            created.OrgUnit?.Name ?? "", created.Location?.Name ?? "");
    }

    public async Task<Paged<MovementDto>> ListMovementsAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager");
        var (items, total) = await repo.ListMovementsAsync(workerId, ct);
        return new Paged<MovementDto>(items.Select(m => new MovementDto(
            m.Id, m.MovementType, m.Status, m.Reason, m.EffectiveDate.ToString(),
            null, m.ToJobTitle, m.ToGrade, m.CreatedAt)).ToList(), total, 1, 50);
    }

    public async Task<MovementDto> CreateMovementAsync(MovementCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager");
        var worker = await repo.GetByIdAsync(request.WorkerId, ct)
            ?? throw new DomainException("worker-not-found", $"Worker {request.WorkerId} does not exist.");
        var m = new Movement
        {
            WorkerId = request.WorkerId,
            MovementType = request.MovementType,
            Reason = request.Reason,
            EffectiveDate = DateOnly.Parse(request.EffectiveDate),
            FromOrgUnitId = worker.OrgUnitId,
            FromJobTitle = worker.JobTitle,
            FromGrade = worker.Grade,
            ToOrgUnitId = request.ToOrgUnitId,
            ToJobTitle = request.ToJobTitle,
            ToGrade = request.ToGrade,
            ToLocationId = request.ToLocationId,
            ToManagerId = request.ToManagerId,
            SalaryChange = request.SalaryChange,
            Status = "pending",
        };
        var created = await repo.CreateMovementAsync(m, ct);
        return new MovementDto(created.Id, created.MovementType, created.Status, created.Reason,
            created.EffectiveDate.ToString(), null, created.ToJobTitle, created.ToGrade, created.CreatedAt);
    }

    public async Task ExecuteMovementAsync(Guid movementId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var m = await repo.GetMovementAsync(movementId, ct)
            ?? throw new DomainException("movement-not-found", $"Movement {movementId} does not exist.");
        if (m.Status != "approved")
            throw new DomainException("movement-not-approved", "Only approved movements can be executed.");
        if (m.EffectiveDate > DateOnly.FromDateTime(DateTimeOffset.UtcNow.DateTime))
            throw new DomainException("movement-not-due", "Movement effective date is in the future.");
        var worker = await repo.GetByIdAsync(m.WorkerId, ct)!;
        if (m.ToOrgUnitId.HasValue) worker!.OrgUnitId = m.ToOrgUnitId.Value;
        if (m.ToJobTitle is not null) worker!.JobTitle = m.ToJobTitle;
        if (m.ToGrade is not null) worker!.Grade = m.ToGrade;
        if (m.ToLocationId.HasValue) worker!.LocationId = m.ToLocationId.Value;
        if (m.ToManagerId.HasValue) worker!.ManagerId = m.ToManagerId.Value;
        await repo.ExecuteMovementAsync(m, ct);
    }

    /// <summary>Issues the next sequential employee number (EMP-0001, EMP-0002, ...) for the tenant, safe under concurrent create via the DB unique index fallback check.</summary>
    private static async Task<string> IssueEmployeeNoAsync(IWorkerRepository repo, CancellationToken ct)
    {
        for (var n = 1; n < 1000; n++)
        {
            var candidate = $"EMP-{n:D4}";
            if (!await repo.ExistsAsync(candidate, ct))
                return candidate;
        }
        return $"EMP-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }

    private static WorkerDto Map(Worker w, bool includeSensitive) => new(
        w.Id, w.EmployeeNo, w.FirstName, w.MiddleName, w.LastName, w.FullName, w.PreferredName,
        w.Email, includeSensitive ? w.PersonalEmail : null, w.Phone, w.PhotoUrl, Mask(w.Nrc, includeSensitive), Mask(w.PassportNo, includeSensitive),
        Mask(w.Tpin, includeSensitive), Mask(w.NapsaNumber, includeSensitive), Mask(w.NhimaNumber, includeSensitive),
        w.Nationality, includeSensitive ? w.DateOfBirth : null, includeSensitive ? w.SubjectId : null, w.WorkerType, w.ContractType, w.Status,
        w.OrgUnitId, w.OrgUnit?.Name, w.LocationId, w.Location?.Name, w.ManagerId,
        w.Manager?.FullName, w.Grade, w.JobTitle,
        w.StartDate?.ToString("yyyy-MM-dd"), w.EndDate?.ToString("yyyy-MM-dd"),
        includeSensitive && w.EmergencyContacts.Count > 0
            ? w.EmergencyContacts.Select(e => new EmergencyContactDto(e.Id, e.Relationship, e.FullName, e.Phone, e.IsPrimary)).ToList()
            : null,
        includeSensitive && w.BankDetails.Count > 0
            ? w.BankDetails.Select(b => new WorkerBankDetailDto(b.Id, b.BankName, b.BranchCode, b.AccountNumber, b.AccountName, b.PaymentMethod, b.MobileMoneyNumber, b.IsPrimary)).ToList()
            : null,
        w.Education.Select(e => new WorkerEducationDto(e.Id, e.Institution, e.Qualification, e.FieldOfStudy, e.Grade, e.StartYear, e.EndYear)).ToList(),
        w.ExternalWorkHistory.Select(e => new ExternalWorkHistoryDto(e.Id, e.Company, e.Role, e.StartDate, e.EndDate, e.Responsibilities)).ToList(),
        w.InternalWorkHistory.Select(e => new InternalWorkHistoryDto(e.Id, e.OrgUnitName, e.Role, e.Grade, e.StartDate, e.EndDate, e.Reason)).ToList(),
        w.CreatedAt, w.UpdatedAt);

    // M35: self-service notification preferences
    public async Task<string?> GetMyPreferencesAsync(string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin", "manager", "payroll");
        if (string.IsNullOrEmpty(subjectId))
            throw new DomainException("no-subject-claim", "The request carries no identity claim.");
        var worker = await repo.FindBySubjectIdAsync(subjectId, ct);
        return worker?.NotificationPreferences;
    }

    public async Task<string?> UpdateMyPreferencesAsync(string subjectId, string preferencesJson, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "hr_ops", "hr_admin", "manager", "payroll");
        if (string.IsNullOrEmpty(subjectId))
            throw new DomainException("no-subject-claim", "The request carries no identity claim.");
        // Validate JSON
        try { System.Text.Json.JsonDocument.Parse(preferencesJson); }
        catch (System.Text.Json.JsonException)
        {
            throw new DomainException("bad-preferences-json", "Notification preferences must be valid JSON.");
        }
        var worker = await repo.FindBySubjectIdAsync(subjectId, ct)
            ?? throw new DomainException("worker-not-linked", "Your organisation identity is not linked to an HRM worker record.");
        var entity = await repo.GetByIdAsync(worker.Id, ct)
            ?? throw new DomainException("worker-not-found", "Worker record not found.");
        entity.NotificationPreferences = preferencesJson;
        await repo.SaveChangesAsync(ct);
        return preferencesJson;
    }

    private static string? Mask(string? value, bool includeSensitive)
    {
        if (includeSensitive || string.IsNullOrWhiteSpace(value)) return value;
        var tail = value.Length <= 4 ? value : value[^4..];
        return $"••••{tail}";
    }
}
