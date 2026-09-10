using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Workers;

/// <summary>M2 worker lifecycle implementation. Movements follow the
/// draft -> pending -> approved -> executed flow: a movement only takes effect
/// once approved AND its effective date has arrived.</summary>
public sealed class WorkerLifecycleServiceImpl(
    IWorkerRepository repo, IAuthzService authz) : IWorkerLifecycleService
{
    // ================= Assignments =================

    public async Task<List<AssignmentDto>> ListAssignmentsAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "payroll", "employee");
        await RequireWorkerExistsAsync(workerId, ct);
        var (items, _) = await repo.ListAssignmentsAsync(workerId, ct);
        return items.Select(ToAssignmentDto).ToList();
    }

    public async Task<AssignmentDto> CreateAssignmentAsync(Guid workerId, AssignmentCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        if (request.WorkerId != default && request.WorkerId != workerId)
            throw new DomainException("validation-failed", "Worker id in the body does not match the route.");
        var start = DateOnly.Parse(request.StartDate);
        var entities = await repo.ListAllLegalEntitiesAsync(ct);
        if (entities.All(e => e.Id != request.LegalEntityId))
            throw new DomainException("legal-entity-not-found", $"Legal entity {request.LegalEntityId} does not exist.");
        var units = await repo.ListAllOrgUnitsAsync(ct);
        if (units.All(u => u.Id != request.OrgUnitId))
            throw new DomainException("org-unit-not-found", $"Org unit {request.OrgUnitId} does not exist.");
        var locations = await repo.ListAllLocationsAsync(ct);
        if (locations.All(l => l.Id != request.LocationId))
            throw new DomainException("location-not-found", $"Location {request.LocationId} does not exist.");

        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var status = start > now ? "proposed" : "current";

        var assignment = new Assignment
        {
            WorkerId = workerId, LegalEntityId = request.LegalEntityId, OrgUnitId = request.OrgUnitId,
            LocationId = request.LocationId, ManagerId = request.ManagerId, JobTitle = request.JobTitle?.Trim(),
            Grade = request.Grade?.Trim(), PositionNo = request.PositionNo?.Trim(),
            ContractType = request.ContractType, WorkPattern = request.WorkPattern,
            ProbationMonths = Math.Max(0, request.ProbationMonths), NoticeDays = Math.Max(0, request.NoticeDays),
            StartDate = start, EndDate = string.IsNullOrWhiteSpace(request.EndDate) ? null : DateOnly.Parse(request.EndDate),
            EffectiveFrom = start,
            Status = status,
        };
        var created = await repo.CreateAssignmentAsync(assignment, ct);

        if (status == "current") await ApplyAssignmentToWorkerAsync(workerId, created, ct);
        return ToAssignmentDto(created);
    }

    public async Task<AssignmentDto> UpdateAssignmentAsync(Guid workerId, Guid assignmentId, AssignmentUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var assignments = await repo.ListAllAssignmentsAsync(ct);
        var assignment = assignments.FirstOrDefault(a => a.Id == assignmentId && a.WorkerId == workerId)
            ?? throw new DomainException("assignment-not-found", $"Assignment {assignmentId} does not belong to worker {workerId}.");
        if (assignment.Status == "ended")
            throw new DomainException("assignment-ended", "Ended assignments cannot be modified.");

        var units = await repo.ListAllOrgUnitsAsync(ct);
        var locations = await repo.ListAllLocationsAsync(ct);
        if (request.OrgUnitId.HasValue && units.All(u => u.Id != request.OrgUnitId))
            throw new DomainException("org-unit-not-found", $"Org unit {request.OrgUnitId} does not exist.");
        if (request.LocationId.HasValue && locations.All(l => l.Id != request.LocationId))
            throw new DomainException("location-not-found", $"Location {request.LocationId} does not exist.");

        if (request.OrgUnitId.HasValue) assignment.OrgUnitId = request.OrgUnitId.Value;
        if (request.LocationId.HasValue) assignment.LocationId = request.LocationId.Value;
        if (request.ManagerId.HasValue) assignment.ManagerId = request.ManagerId;
        if (request.JobTitle is not null) assignment.JobTitle = request.JobTitle.Trim();
        if (request.Grade is not null) assignment.Grade = request.Grade.Trim();
        if (request.PositionNo is not null) assignment.PositionNo = request.PositionNo.Trim();
        if (request.ContractType is not null) assignment.ContractType = request.ContractType;
        if (request.WorkPattern is not null) assignment.WorkPattern = request.WorkPattern;
        if (request.ProbationMonths.HasValue) assignment.ProbationMonths = Math.Max(0, request.ProbationMonths.Value);
        if (request.NoticeDays.HasValue) assignment.NoticeDays = Math.Max(0, request.NoticeDays.Value);
        if (request.EndDate is not null) assignment.EndDate = string.IsNullOrWhiteSpace(request.EndDate) ? null : DateOnly.Parse(request.EndDate);
        if (request.EffectiveTo is not null) assignment.EffectiveTo = string.IsNullOrWhiteSpace(request.EffectiveTo) ? null : DateOnly.Parse(request.EffectiveTo);
        if (request.Status is not null) assignment.Status = request.Status;

        var updated = await repo.UpdateAssignmentAsync(assignment, ct);
        if (updated.Status == "current") await ApplyAssignmentToWorkerAsync(workerId, updated, ct);
        return ToAssignmentDto(updated);
    }

    public async Task EndAssignmentAsync(Guid workerId, Guid assignmentId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var assignments = await repo.ListAllAssignmentsAsync(ct);
        var assignment = assignments.FirstOrDefault(a => a.Id == assignmentId && a.WorkerId == workerId)
            ?? throw new DomainException("assignment-not-found", $"Assignment {assignmentId} does not belong to worker {workerId}.");
        assignment.Status = "ended";
        assignment.EffectiveTo = DateOnly.FromDateTime(DateTime.UtcNow);
        await repo.UpdateAssignmentAsync(assignment, ct);
    }

    // ================= Movements =================

    public async Task<List<MovementDetailDto>> ListMovementsAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "payroll", "employee");
        await RequireWorkerExistsAsync(workerId, ct);
        var (items, _) = await repo.ListMovementsAsync(workerId, ct);
        var units = await repo.ListAllOrgUnitsAsync(ct);
        return items.Select(m => ToMovementDetailDto(m, units)).ToList();
    }

    public async Task<MovementDetailDto> CreateMovementAsync(Guid workerId, MovementCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager");
        if (request.WorkerId != default && request.WorkerId != workerId)
            throw new DomainException("validation-failed", "Worker id in the body does not match the route.");
        await RequireWorkerExistsAsync(workerId, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveDate = DateOnly.Parse(request.EffectiveDate);
        if (effectiveDate < today)
            throw new DomainException("movement-backdated", "Movements cannot be backdated; choose today or a future date.");
        var worker = await repo.GetByIdAsync(workerId, ct) ?? throw new DomainException("worker-not-found", $"Worker {workerId} does not exist.");
        var units = await repo.ListAllOrgUnitsAsync(ct);
        if (request.ToOrgUnitId.HasValue && units.All(u => u.Id != request.ToOrgUnitId))
            throw new DomainException("org-unit-not-found", $"Target org unit {request.ToOrgUnitId} does not exist.");
        if (request.ToManagerId.HasValue && (await repo.ListAllWorkersAsync(null, ct)).All(w => w.Id != request.ToManagerId))
            throw new DomainException("manager-not-found", $"Target manager {request.ToManagerId} does not exist.");

        var movement = new Movement
        {
            WorkerId = workerId, MovementType = request.MovementType, Status = "draft",
            EffectiveDate = effectiveDate, Reason = request.Reason.Trim(),
            FromOrgUnitId = worker.OrgUnitId, FromJobTitle = worker.JobTitle, FromGrade = worker.Grade,
            ToOrgUnitId = request.ToOrgUnitId, ToJobTitle = request.ToJobTitle?.Trim(),
            ToGrade = request.ToGrade?.Trim(), ToLocationId = request.ToLocationId,
            ToManagerId = request.ToManagerId, SalaryChange = request.SalaryChange,
        };
        return ToMovementDetailDto(await repo.CreateMovementAsync(movement, ct), units);
    }

    public async Task<MovementDetailDto> GetMovementAsync(Guid workerId, Guid movementId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "payroll");
        var movement = await repo.GetMovementAsync(movementId, ct)
            ?? throw new DomainException("movement-not-found", $"Movement {movementId} does not exist.");
        if (movement.WorkerId != workerId)
            throw new DomainException("movement-not-found", $"Movement {movementId} does not belong to worker {workerId}.");
        return ToMovementDetailDto(movement, await repo.ListAllOrgUnitsAsync(ct));
    }

    public async Task<List<MovementImpactDto>> PreviewMovementAsync(Guid workerId, Guid movementId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager");
        var movement = await repo.GetMovementAsync(movementId, ct)
            ?? throw new DomainException("movement-not-found", $"Movement {movementId} does not exist.");
        if (movement.WorkerId != workerId)
            throw new DomainException("movement-not-found", $"Movement {movementId} does not belong to worker {workerId}.");
        var impacts = new List<MovementImpactDto>();
        var units = await repo.ListAllOrgUnitsAsync(ct);
        if (movement.FromOrgUnitId != movement.ToOrgUnitId && movement.ToOrgUnitId.HasValue)
            impacts.Add(new("org_unit", units.FirstOrDefault(u => u.Id == movement.FromOrgUnitId)?.Name ?? "?",
                units.FirstOrDefault(u => u.Id == movement.ToOrgUnitId)?.Name ?? "?"));
        if (movement.FromJobTitle != movement.ToJobTitle)
            impacts.Add(new("job_title", movement.FromJobTitle ?? "", movement.ToJobTitle ?? ""));
        if (movement.FromGrade != movement.ToGrade)
            impacts.Add(new("grade", movement.FromGrade ?? "", movement.ToGrade ?? ""));
        if (movement.SalaryChange.HasValue)
            impacts.Add(new("basic_salary", movement.SalaryChange.HasValue && movement.SalaryChange > 0 ? movement.SalaryChange.Value.ToString("0.##") : "unchanged", ""));
        return impacts;
    }

    public async Task SubmitMovementAsync(Guid workerId, Guid movementId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager");
        var movement = await RequireMovementOwnedAsync(workerId, movementId, ct);
        if (movement.Status != "draft")
            throw new DomainException("movement-not-allowed", "Only draft movements can be submitted.");
        movement.Status = "pending";
        await repo.ExecuteMovementAsync(movement, ct);
    }

    public async Task ApproveMovementAsync(Guid workerId, Guid movementId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var movement = await RequireMovementOwnedAsync(workerId, movementId, ct);
        if (movement.Status != "pending")
            throw new DomainException("movement-not-allowed", "Only pending movements can be approved.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (movement.EffectiveDate <= today)
        {
            // Effective date arrived: execute immediately — apply to worker + create future/current assignment
            await ExecuteMovementOnWorkerAsync(movement, ct);
            movement.Status = "executed";
        }
        else
        {
            movement.Status = "approved";
        }
        await repo.ExecuteMovementAsync(movement, ct);
    }

    public async Task RejectMovementAsync(Guid workerId, Guid movementId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var movement = await RequireMovementOwnedAsync(workerId, movementId, ct);
        if (movement.Status != "pending")
            throw new DomainException("movement-not-allowed", "Only pending movements can be rejected.");
        movement.Status = "rejected";
        await repo.ExecuteMovementAsync(movement, ct);
    }

    public async Task CancelMovementAsync(Guid workerId, Guid movementId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager");
        var movement = await RequireMovementOwnedAsync(workerId, movementId, ct);
        if (movement.Status is "approved" or "executed")
            throw new DomainException("movement-not-allowed", $"A {movement.Status} movement cannot be cancelled.");
        movement.Status = "cancelled";
        await repo.ExecuteMovementAsync(movement, ct);
    }

    // ================= Emergency contacts =================

    public async Task<List<EmergencyContactDto>> ListEmergencyContactsAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "payroll", "employee");
        await RequireWorkerExistsAsync(workerId, ct);
        var worker = await repo.GetByIdAsync(workerId, ct) ?? throw new DomainException("worker-not-found", $"Worker {workerId} does not exist.");
        return worker.EmergencyContacts.Select(c => new EmergencyContactDto(c.Id, c.Relationship, c.FullName, c.Phone, c.IsPrimary)).ToList();
    }

    public async Task<EmergencyContactDto> AddEmergencyContactAsync(Guid workerId, EmergencyContactRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee");
        await RequireWorkerExistsAsync(workerId, ct);
        if (string.IsNullOrWhiteSpace(request.Relationship) || string.IsNullOrWhiteSpace(request.FullName))
            throw new DomainException("validation-failed", "Relationship and full name are required.");
        var contact = new EmergencyContact
        {
            WorkerId = workerId, Relationship = request.Relationship.Trim(), FullName = request.FullName.Trim(),
            Phone = request.Phone?.Trim(), IsPrimary = request.IsPrimary,
        };
        var created = await repo.AddEmergencyContactAsync(contact, ct);
        return new EmergencyContactDto(created.Id, created.Relationship, created.FullName, created.Phone, created.IsPrimary);
    }

    public async Task<EmergencyContactDto> UpdateEmergencyContactAsync(Guid workerId, Guid contactId, EmergencyContactRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee");
        var contact = await repo.GetEmergencyContactAsync(contactId, ct)
            ?? throw new DomainException("contact-not-found", $"Emergency contact {contactId} does not exist.");
        if (contact.WorkerId != workerId)
            throw new DomainException("contact-not-found", $"Contact {contactId} does not belong to worker {workerId}.");
        if (request.Relationship is not null) contact.Relationship = request.Relationship.Trim();
        if (request.FullName is not null) contact.FullName = request.FullName.Trim();
        if (request.Phone is not null) contact.Phone = request.Phone.Trim();
        contact.IsPrimary = request.IsPrimary;
        await repo.UpdateEmergencyContactAsync(contact, ct);
        return new EmergencyContactDto(contact.Id, contact.Relationship, contact.FullName, contact.Phone, contact.IsPrimary);
    }

    public async Task DeleteEmergencyContactAsync(Guid workerId, Guid contactId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee");
        var contact = await repo.GetEmergencyContactAsync(contactId, ct)
            ?? throw new DomainException("contact-not-found", $"Emergency contact {contactId} does not exist.");
        if (contact.WorkerId != workerId)
            throw new DomainException("contact-not-found", $"Contact {contactId} does not belong to worker {workerId}.");
        await repo.DeleteEmergencyContactAsync(contactId, ct);
    }

    // ================= Bank details =================

    public async Task<List<WorkerBankDetailDto>> ListBankDetailsAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "payroll");
        await RequireWorkerExistsAsync(workerId, ct);
        var worker = await repo.GetByIdAsync(workerId, ct) ?? throw new DomainException("worker-not-found", $"Worker {workerId} does not exist.");
        return worker.BankDetails.Select(b => new WorkerBankDetailDto(b.Id, b.BankName, b.BranchCode,
            b.AccountNumber, b.AccountName, b.PaymentMethod, b.MobileMoneyNumber, b.IsPrimary)).ToList();
    }

    public async Task<WorkerBankDetailDto> AddBankDetailAsync(Guid workerId, BankDetailRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll", "employee");
        await RequireWorkerExistsAsync(workerId, ct);
        var method = NormalizePaymentMethod(request.PaymentMethod);
        ValidatePaymentDetail(method, request);
        var detail = new WorkerBankDetail
        {
            WorkerId = workerId,
            BankName = CleanPaymentValue(request.BankName, method == "bank" ? "Bank" : method == "mobile-money" ? "Mobile money" : method == "cash" ? "Cash" : "Accounts payable"),
            BranchCode = CleanPaymentValue(request.BranchCode, "N/A"),
            AccountNumber = CleanPaymentValue(request.AccountNumber, method == "mobile-money" ? request.MobileMoneyNumber ?? "N/A" : "N/A"),
            AccountName = CleanPaymentValue(request.AccountName, "N/A"),
            PaymentMethod = method, MobileMoneyNumber = request.MobileMoneyNumber?.Trim(),
            IsPrimary = request.IsPrimary,
        };
        var created = await repo.AddBankDetailAsync(detail, ct);
        return new WorkerBankDetailDto(created.Id, created.BankName, created.BranchCode,
            created.AccountNumber, created.AccountName, created.PaymentMethod, created.MobileMoneyNumber, created.IsPrimary);
    }

    public async Task<WorkerBankDetailDto> UpdateBankDetailAsync(Guid workerId, Guid bankId, BankDetailRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll", "employee");
        var detail = await repo.GetBankDetailAsync(bankId, ct)
            ?? throw new DomainException("bank-detail-not-found", $"Bank detail {bankId} does not exist.");
        if (detail.WorkerId != workerId)
            throw new DomainException("bank-detail-not-found", $"Bank detail {bankId} does not belong to worker {workerId}.");
        var method = NormalizePaymentMethod(request.PaymentMethod);
        ValidatePaymentDetail(method, request);
        detail.BankName = CleanPaymentValue(request.BankName, method == "bank" ? "Bank" : method == "mobile-money" ? "Mobile money" : method == "cash" ? "Cash" : "Accounts payable");
        detail.BranchCode = CleanPaymentValue(request.BranchCode, "N/A");
        detail.AccountNumber = CleanPaymentValue(request.AccountNumber, method == "mobile-money" ? request.MobileMoneyNumber ?? "N/A" : "N/A");
        detail.AccountName = CleanPaymentValue(request.AccountName, "N/A");
        detail.PaymentMethod = method;
        detail.MobileMoneyNumber = request.MobileMoneyNumber?.Trim();
        detail.IsPrimary = request.IsPrimary;
        await repo.UpdateBankDetailAsync(detail, ct);
        return new WorkerBankDetailDto(detail.Id, detail.BankName, detail.BranchCode,
            detail.AccountNumber, detail.AccountName, detail.PaymentMethod, detail.MobileMoneyNumber, detail.IsPrimary);
    }

    private static string NormalizePaymentMethod(string? value)
    {
        var method = (value ?? "bank").Trim().ToLowerInvariant().Replace("_", "-");
        return method switch
        {
            "bank transfer" => "bank",
            "mobile money" => "mobile-money",
            "paid through accounts payable, not payroll" => "accounts-payable",
            "" => "bank",
            _ => method,
        };
    }

    private static void ValidatePaymentDetail(string method, BankDetailRequest request)
    {
        switch (method)
        {
            case "bank":
                if (string.IsNullOrWhiteSpace(request.BankName) || string.IsNullOrWhiteSpace(request.BranchCode) || string.IsNullOrWhiteSpace(request.AccountNumber))
                    throw new DomainException("validation-failed", "Bank name, branch code and account number are required for bank transfer.");
                break;
            case "mobile-money":
                if (string.IsNullOrWhiteSpace(request.MobileMoneyNumber))
                    throw new DomainException("validation-failed", "Mobile money number is required for mobile-money payout.");
                break;
            case "cash":
            case "accounts-payable":
                break;
            default:
                throw new DomainException("validation-failed", "Payment method must be bank, mobile-money, cash or accounts-payable.");
        }
    }

    private static string CleanPaymentValue(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    public async Task DeleteBankDetailAsync(Guid workerId, Guid bankId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll", "employee");
        var detail = await repo.GetBankDetailAsync(bankId, ct)
            ?? throw new DomainException("bank-detail-not-found", $"Bank detail {bankId} does not exist.");
        if (detail.WorkerId != workerId)
            throw new DomainException("bank-detail-not-found", $"Bank detail {bankId} does not belong to worker {workerId}.");
        await repo.DeleteBankDetailAsync(bankId, ct);
    }

    // ================= M33: worker history (education / external / internal) =================

    public async Task<List<WorkerEducationDto>> ListEducationAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "payroll", "employee");
        await RequireWorkerExistsAsync(workerId, ct);
        return (await repo.ListEducationAsync(workerId, ct)).Select(e => new WorkerEducationDto(e.Id, e.Institution, e.Qualification, e.FieldOfStudy, e.Grade, e.StartYear, e.EndYear)).ToList();
    }

    public async Task<WorkerEducationDto> AddEducationAsync(Guid workerId, EducationRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee");
        await RequireWorkerExistsAsync(workerId, ct);
        if (string.IsNullOrWhiteSpace(request.Institution) || string.IsNullOrWhiteSpace(request.Qualification))
            throw new DomainException("validation-failed", "Institution and qualification are required.");
        if (request.StartYear.HasValue && request.EndYear.HasValue && request.EndYear.Value < request.StartYear.Value)
            throw new DomainException("validation-failed", "The end year must not be before the start year.");
        var record = new WorkerEducation
        {
            WorkerId = workerId, Institution = request.Institution.Trim(), Qualification = request.Qualification.Trim(),
            FieldOfStudy = request.FieldOfStudy?.Trim(), Grade = request.Grade?.Trim(),
            StartYear = request.StartYear, EndYear = request.EndYear,
        };
        var created = await repo.AddEducationAsync(record, ct);
        return new WorkerEducationDto(created.Id, created.Institution, created.Qualification, created.FieldOfStudy, created.Grade, created.StartYear, created.EndYear);
    }

    public async Task<WorkerEducationDto> UpdateEducationAsync(Guid workerId, Guid recordId, EducationRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee");
        var record = await repo.GetByIdEducationAsync(recordId, ct)
            ?? throw new DomainException("education-not-found", $"Education record {recordId} does not exist.");
        if (record.WorkerId != workerId)
            throw new DomainException("education-not-found", $"Education record {recordId} does not belong to worker {workerId}.");
        if (request.Institution is not null) record.Institution = request.Institution.Trim();
        if (request.Qualification is not null) record.Qualification = request.Qualification.Trim();
        if (request.FieldOfStudy is not null) record.FieldOfStudy = request.FieldOfStudy.Trim();
        if (request.Grade is not null) record.Grade = request.Grade.Trim();
        record.StartYear = request.StartYear ?? record.StartYear;
        record.EndYear = request.EndYear ?? record.EndYear;
        if (record.StartYear.HasValue && record.EndYear.HasValue && record.EndYear.Value < record.StartYear.Value)
            throw new DomainException("validation-failed", "The end year must not be before the start year.");
        await repo.UpdateEducationAsync(record, ct);
        return new WorkerEducationDto(record.Id, record.Institution, record.Qualification, record.FieldOfStudy, record.Grade, record.StartYear, record.EndYear);
    }

    public async Task DeleteEducationAsync(Guid workerId, Guid recordId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee");
        var record = await repo.GetByIdEducationAsync(recordId, ct)
            ?? throw new DomainException("education-not-found", $"Education record {recordId} does not exist.");
        if (record.WorkerId != workerId)
            throw new DomainException("education-not-found", $"Education record {recordId} does not belong to worker {workerId}.");
        await repo.DeleteEducationAsync(recordId, ct);
    }

    public async Task<List<ExternalWorkHistoryDto>> ListExternalWorkHistoryAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "payroll", "employee");
        await RequireWorkerExistsAsync(workerId, ct);
        return (await repo.ListExternalWorkHistoryAsync(workerId, ct)).Select(e => new ExternalWorkHistoryDto(e.Id, e.Company, e.Role, e.StartDate, e.EndDate, e.Responsibilities)).ToList();
    }

    public async Task<ExternalWorkHistoryDto> AddExternalWorkHistoryAsync(Guid workerId, ExternalWorkHistoryRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee");
        await RequireWorkerExistsAsync(workerId, ct);
        if (string.IsNullOrWhiteSpace(request.Company))
            throw new DomainException("validation-failed", "Company name is required.");
        if (IsInvalidDateRange(request.StartDate, request.EndDate))
            throw new DomainException("validation-failed", "The end date must not be before the start date.");
        var record = new ExternalWorkHistory
        {
            WorkerId = workerId, Company = request.Company.Trim(), Role = request.Role?.Trim(),
            StartDate = NormalizeDate(request.StartDate), EndDate = NormalizeDate(request.EndDate),
            Responsibilities = request.Responsibilities?.Trim(),
        };
        var created = await repo.AddExternalWorkHistoryAsync(record, ct);
        return new ExternalWorkHistoryDto(created.Id, created.Company, created.Role, created.StartDate, created.EndDate, created.Responsibilities);
    }

    public async Task<ExternalWorkHistoryDto> UpdateExternalWorkHistoryAsync(Guid workerId, Guid recordId, ExternalWorkHistoryRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee");
        var record = await repo.GetByIdExternalWorkHistoryAsync(recordId, ct)
            ?? throw new DomainException("external-work-history-not-found", $"Work history record {recordId} does not exist.");
        if (record.WorkerId != workerId)
            throw new DomainException("external-work-history-not-found", $"Work history record {recordId} does not belong to worker {workerId}.");
        if (request.Company is not null) record.Company = request.Company.Trim();
        if (request.Role is not null) record.Role = request.Role.Trim();
        if (request.StartDate is not null) record.StartDate = NormalizeDate(request.StartDate);
        if (request.EndDate is not null) record.EndDate = NormalizeDate(request.EndDate);
        if (request.Responsibilities is not null) record.Responsibilities = request.Responsibilities.Trim();
        if (IsInvalidDateRange(record.StartDate, record.EndDate))
            throw new DomainException("validation-failed", "The end date must not be before the start date.");
        await repo.UpdateExternalWorkHistoryAsync(record, ct);
        return new ExternalWorkHistoryDto(record.Id, record.Company, record.Role, record.StartDate, record.EndDate, record.Responsibilities);
    }

    public async Task DeleteExternalWorkHistoryAsync(Guid workerId, Guid recordId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee");
        var record = await repo.GetByIdExternalWorkHistoryAsync(recordId, ct)
            ?? throw new DomainException("external-work-history-not-found", $"Work history record {recordId} does not exist.");
        if (record.WorkerId != workerId)
            throw new DomainException("external-work-history-not-found", $"Work history record {recordId} does not belong to worker {workerId}.");
        await repo.DeleteExternalWorkHistoryAsync(recordId, ct);
    }

    public async Task<List<InternalWorkHistoryDto>> ListInternalWorkHistoryAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "payroll", "employee");
        await RequireWorkerExistsAsync(workerId, ct);
        return (await repo.ListInternalWorkHistoryAsync(workerId, ct)).Select(e => new InternalWorkHistoryDto(e.Id, e.OrgUnitName, e.Role, e.Grade, e.StartDate, e.EndDate, e.Reason)).ToList();
    }

    public async Task<InternalWorkHistoryDto> AddInternalWorkHistoryAsync(Guid workerId, InternalWorkHistoryRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee");
        await RequireWorkerExistsAsync(workerId, ct);
        if (string.IsNullOrWhiteSpace(request.OrgUnitName))
            throw new DomainException("validation-failed", "Department / org unit name is required.");
        if (IsInvalidDateRange(request.StartDate, request.EndDate))
            throw new DomainException("validation-failed", "The end date must not be before the start date.");
        var record = new InternalWorkHistory
        {
            WorkerId = workerId, OrgUnitName = request.OrgUnitName.Trim(), Role = request.Role?.Trim(),
            Grade = request.Grade?.Trim(), StartDate = NormalizeDate(request.StartDate),
            EndDate = NormalizeDate(request.EndDate), Reason = request.Reason?.Trim(),
        };
        var created = await repo.AddInternalWorkHistoryAsync(record, ct);
        return new InternalWorkHistoryDto(created.Id, created.OrgUnitName, created.Role, created.Grade, created.StartDate, created.EndDate, created.Reason);
    }

    public async Task<InternalWorkHistoryDto> UpdateInternalWorkHistoryAsync(Guid workerId, Guid recordId, InternalWorkHistoryRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee");
        var record = await repo.GetByIdInternalWorkHistoryAsync(recordId, ct)
            ?? throw new DomainException("internal-work-history-not-found", $"Internal work history record {recordId} does not exist.");
        if (record.WorkerId != workerId)
            throw new DomainException("internal-work-history-not-found", $"Internal work history record {recordId} does not belong to worker {workerId}.");
        if (request.OrgUnitName is not null) record.OrgUnitName = request.OrgUnitName.Trim();
        if (request.Role is not null) record.Role = request.Role.Trim();
        if (request.Grade is not null) record.Grade = request.Grade.Trim();
        if (request.StartDate is not null) record.StartDate = NormalizeDate(request.StartDate);
        if (request.EndDate is not null) record.EndDate = NormalizeDate(request.EndDate);
        if (request.Reason is not null) record.Reason = request.Reason.Trim();
        if (IsInvalidDateRange(record.StartDate, record.EndDate))
            throw new DomainException("validation-failed", "The end date must not be before the start date.");
        await repo.UpdateInternalWorkHistoryAsync(record, ct);
        return new InternalWorkHistoryDto(record.Id, record.OrgUnitName, record.Role, record.Grade, record.StartDate, record.EndDate, record.Reason);
    }

    public async Task DeleteInternalWorkHistoryAsync(Guid workerId, Guid recordId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "manager", "employee");
        var record = await repo.GetByIdInternalWorkHistoryAsync(recordId, ct)
            ?? throw new DomainException("internal-work-history-not-found", $"Internal work history record {recordId} does not exist.");
        if (record.WorkerId != workerId)
            throw new DomainException("internal-work-history-not-found", $"Internal work history record {recordId} does not belong to worker {workerId}.");
        await repo.DeleteInternalWorkHistoryAsync(recordId, ct);
    }

    /// <summary>Accepts "YYYY-MM-DD" ISO dates or year-only "YYYY" strings; returns
    /// null for blanks. Dates are stored normalised so range checks stay simple.</summary>
    private static string? NormalizeDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = raw.Trim();
        if (value.Length == 4 && int.TryParse(value, out var year))
            return $"{year:D4}";
        if (DateOnly.TryParse(value, out var date))
            return date.ToString("yyyy-MM-dd");
        // Pass through unparseable values; downstream checks may complain.
        return value;
    }

    private static bool IsInvalidDateRange(string? start, string? end)
    {
        if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end)) return false;
        return string.CompareOrdinal(end, start) < 0;
    }

    // ================= Onboarding / offboarding =================

    /// <summary>Onboarding readiness: an active assignment with a filled statutory
    /// pack (NRC, TPIN, NAPSA) and at least one bank account for payroll payout.</summary>
    public async Task<OnboardingPlanDto> GetOnboardingAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll", "employee", "manager");
        var worker = await repo.GetByIdAsync(workerId, ct) ?? throw new DomainException("worker-not-found", $"Worker {workerId} does not exist.");
        var (assignments, _) = await repo.ListAssignmentsAsync(workerId, ct);
        var hasCurrentAssignment = assignments.Any(a => a.Status == "current" || a.Status == "future");
        var tasks = new[]
        {
            hasCurrentAssignment,
            !string.IsNullOrWhiteSpace(worker.Nrc),
            !string.IsNullOrWhiteSpace(worker.Tpin),
            !string.IsNullOrWhiteSpace(worker.NapsaNumber),
            worker.BankDetails.Any(),
        };
        return new OnboardingPlanDto(workerId, tasks.All(t => t), tasks.Count(t => t), tasks.Length);
    }

    /// <summary>Offboarding: closes the current assignment, ends the worker record
    /// and returns the clearance checklist (open statutory/banking items).</summary>
    public async Task<OffboardingResultDto> OffboardAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var worker = await repo.GetByIdAsync(workerId, ct) ?? throw new DomainException("worker-not-found", $"Worker {workerId} does not exist.");
        if (worker.Status == "terminated")
            return new OffboardingResultDto(true, []);
        var (assignments, _) = await repo.ListAssignmentsAsync(workerId, ct);
        var current = assignments.FirstOrDefault(a => a.Status == "current");
        if (current is not null)
        {
            current.Status = "ended";
            current.EffectiveTo = DateOnly.FromDateTime(DateTime.UtcNow);
            await repo.UpdateAssignmentAsync(current, ct);
        }
        worker.Status = "terminated";
        worker.EndDate = DateOnly.FromDateTime(DateTime.UtcNow);
        await repo.UpdateAsync(worker, ct);

        var openItems = new List<string>();
        if (string.IsNullOrWhiteSpace(worker.NapsaNumber)) openItems.Add("nap_sa_number");
        if (!worker.BankDetails.Any()) openItems.Add("bank_detail");
        if (assignments.Any(a => a.Status == "future")) openItems.Add("future_assignment");
        return new OffboardingResultDto(openItems.Count == 0, openItems.ToArray());
    }

    // ================= Helpers =================

    private async Task<Worker> RequireWorkerExistsAsync(Guid workerId, CancellationToken ct)
        => await repo.GetByIdAsync(workerId, ct) ?? throw new DomainException("worker-not-found", $"Worker {workerId} does not exist.");

    private async Task<Movement> RequireMovementOwnedAsync(Guid workerId, Guid movementId, CancellationToken ct)
    {
        var movement = await repo.GetMovementAsync(movementId, ct)
            ?? throw new DomainException("movement-not-found", $"Movement {movementId} does not exist.");
        if (movement.WorkerId != workerId)
            throw new DomainException("movement-not-found", $"Movement {movementId} does not belong to worker {workerId}.");
        return movement;
    }

    /// <summary>Denormalize the current assignment onto the worker read view.</summary>
    private async Task ApplyAssignmentToWorkerAsync(Guid workerId, Assignment assignment, CancellationToken ct)
    {
        var worker = await repo.GetByIdAsync(workerId, ct);
        if (worker is null) return;
        worker.OrgUnitId = assignment.OrgUnitId;
        worker.LocationId = assignment.LocationId;
        worker.ManagerId = assignment.ManagerId;
        worker.JobTitle = assignment.JobTitle ?? worker.JobTitle;
        worker.Grade = assignment.Grade ?? worker.Grade;
        worker.ContractType = assignment.ContractType;
        worker.StartDate ??= assignment.StartDate;
        if (worker.Status == "pre-hire") worker.Status = "active";
        await repo.UpdateAsync(worker, ct);
    }

    private async Task ExecuteMovementOnWorkerAsync(Movement movement, CancellationToken ct)
    {
        var worker = await repo.GetByIdAsync(movement.WorkerId, ct);
        if (worker is null) return;
        if (movement.ToOrgUnitId.HasValue) worker.OrgUnitId = movement.ToOrgUnitId;
        if (movement.ToLocationId.HasValue) worker.LocationId = movement.ToLocationId;
        if (movement.ToManagerId.HasValue) worker.ManagerId = movement.ToManagerId;
        if (movement.ToJobTitle is not null) worker.JobTitle = movement.ToJobTitle;
        if (movement.ToGrade is not null) worker.Grade = movement.ToGrade;
        await repo.UpdateAsync(worker, ct);
    }

    private static AssignmentDto ToAssignmentDto(Assignment a) => new(
        a.Id, a.JobTitle, a.Grade, a.ContractType, a.Status, a.StartDate.ToString("yyyy-MM-dd"),
        a.EndDate?.ToString("yyyy-MM-dd"), a.OrgUnit?.Name ?? "", a.Location?.Name ?? "");

    private static MovementDetailDto ToMovementDetailDto(Movement m, List<OrgUnit> units) => new(
        m.Id, m.WorkerId, m.MovementType, m.Status, m.EffectiveDate.ToString("yyyy-MM-dd"), m.Reason,
        m.FromOrgUnitId, m.FromOrgUnitId.HasValue ? units.FirstOrDefault(u => u.Id == m.FromOrgUnitId)?.Name : null,
        m.FromJobTitle, m.FromGrade, m.ToOrgUnitId,
        m.ToOrgUnitId.HasValue ? units.FirstOrDefault(u => u.Id == m.ToOrgUnitId)?.Name : null,
        m.ToJobTitle, m.ToGrade, m.ToLocationId, m.ToManagerId, m.SalaryChange, m.CreatedAt);
}
