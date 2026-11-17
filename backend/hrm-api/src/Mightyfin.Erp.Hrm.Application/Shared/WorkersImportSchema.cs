// M31 — workers (employees) schema for the shared import/export engine.
// Reuses the existing worker creation service so imports go through the same
// lifecycle as the UI form (naming rules, entity/unit resolution, validations),
// and adds Update mode matched on employee number with NRC/NAPSA fallback.
using System.Globalization;
using Mightyfin.Erp.Hrm.Application.Benefits;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Shared;

public sealed class WorkersImportSchema : IImportSchemaWithExport
{
    private readonly IWorkerRepository repo;
    private readonly IWorkerService workers;
    private readonly IPayrollRepository? payrollRepo;
    private readonly IPayrollService? payroll;
    private readonly ITimeService? time;
    private readonly IBenefitRepository? benefits;
    private readonly IAuthzService authz;
    private readonly ShellContext scope;

    public WorkersImportSchema(IWorkerRepository repo, IWorkerService workers, IAuthzService authz, ShellContext scope,
        IPayrollRepository? payrollRepo = null, IPayrollService? payroll = null, ITimeService? time = null,
        IBenefitRepository? benefits = null)
    {
        this.repo = repo;
        this.workers = workers;
        this.payrollRepo = payrollRepo;
        this.payroll = payroll;
        this.time = time;
        this.benefits = benefits;
        this.authz = authz;
        this.scope = scope;
    }

    public string TypeKey => "workers";
    public string DisplayName => "Employees";

    public List<ImportFieldDef> Fields =>
    [
        new("employeeNo", "Employee number", false, NaturalKey: true, Example: "EMP-0008"),
        new("firstName", "First name", true),
        new("lastName", "Last name", true),
        new("middleName", "Middle name", false, Example: "Chileshe"),
        new("email", "Work email", true, FormatNote: "e.g. employee@example.com"),
        new("phone", "Phone", true, FormatNote: "e.g. 0971234567"),
        new("nrc", "NRC", false, FormatNote: "e.g. 123456/78/1"),
        new("tpin", "TPIN", false, FormatNote: "10 digits"),
        new("napsaNumber", "NAPSA number", false, FormatNote: "e.g. NAPSA-001"),
        new("nhimaNumber", "NHIMA number", false, FormatNote: "e.g. NHIMA-001"),
        new("grade", "Grade", false, Example: "G5"),
        new("jobTitle", "Job title", false, Example: "Accounts Officer"),
        new("startDate", "Start date", false, FormatNote: "DD-MM-YYYY"),
        new("locationId", "Branch", false, FormatNote: "existing branch name or GUID, case-insensitive; current work scope is used when empty"),
        new("workerType", "Employment type", true, Example: "Permanent / Employee | Contractor / Contingent | Intern | Volunteer", FormatNote: "case-insensitive; contract terms are configured separately"),
        new("orgUnitName", "Department", false, FormatNote: "exact department name, e.g. Finance"),
        new("payGroup", "Pay group", false, FormatNote: "required when Basic salary is supplied, e.g. Monthly ZMW"),
        new("basicSalary", "Basic salary", false, FormatNote: "plain ZMW amount; creates or updates the basic pay assignment"),
        new("costOfLivingAllowance", "Cost of living allowance", false, FormatNote: "monthly ZMW amount; uses an active Cost of Living Allowance salary component or payroll benefit"),
        new("salaryEffectiveFrom", "Salary effective from", false, FormatNote: "DD-MM-YYYY; defaults to start date or today"),
        new("overtime.workDate", "Overtime: Work date", false, FormatNote: "DD-MM-YYYY; required with overtime hours"),
        new("overtime.hours", "Overtime: Hours", false, FormatNote: "positive number; required with overtime work date"),
        new("overtime.multiplier", "Overtime: Multiplier", false, FormatNote: "optional; uses the employee shift rule when empty"),
        new("overtime.reason", "Overtime: Reason", false),
        new("overtime.status", "Overtime: Status", false, FormatNote: "pending or approved; defaults to pending"),

        // M31b flattening: history child fields (export-only in v1; import
        // matches parent and ignores these or uses them for bulk child init)
        new("edu.institution", "Education: Institution", false),
        new("edu.qualification", "Education: Qualification", false),
        new("edu.startYear", "Education: Start Year", false),
        new("edu.endYear", "Education: End Year", false),
        new("ext.company", "Previous Employer: Company", false),
        new("ext.role", "Previous Employer: Role", false),
        new("ext.startDate", "Previous Employer: Start Date", false),
        new("ext.endDate", "Previous Employer: End Date", false),
        new("int.orgUnitName", "Internal Move: Dept", false),
        new("int.role", "Internal Move: Role", false),
        new("int.startDate", "Internal Move: Start Date", false),
    ];

    public async Task<ImportRowOutcome> PreviewRowAsync(IDictionary<string, string> row, string mode, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var isUpdate = mode.Equals("update", StringComparison.OrdinalIgnoreCase) || mode.Equals("fill-missing", StringComparison.OrdinalIgnoreCase);

        // ---- Non-negotiable required fields. Nothing processes without them —
        // except Update mode on an existing record: it already carries its
        // identity on file, so the import patches only the supplied fields. ----
        var employeeNo = row.Get("employeeNo").Trim();
        var nrc = row.Get("nrc").Trim();
        var napsa = row.Get("napsaNumber").Trim();
        var target = await ResolveTargetAsync(employeeNo, nrc, napsa, row.Get("email"), row.Get("phone"), row.Get("tpin"), ct);
        if (!isUpdate && target is not null)
            return new ImportRowOutcome("error",
                !string.IsNullOrWhiteSpace(employeeNo) ? $"Employee number '{employeeNo}' is already assigned to an existing employee ({target.FullName}). Insert mode never overwrites — review the spreadsheet or switch to Update mode."
                : !string.IsNullOrWhiteSpace(nrc) ? $"NRC '{nrc}' is already registered to an existing employee ({target.FullName}). Insert mode never overwrites — review the spreadsheet or switch to Update mode."
                : $"NAPSA number '{napsa}' is already registered to an existing employee ({target.FullName}). Insert mode never overwrites — review the spreadsheet or switch to Update mode.");
        // ---- Child rows (education / previous employment / internal moves) —
        // optional, but when supplied they must be self-consistent (e.g. an
        // education end year may not precede its start year). ----
        if (IsNonBlank(row.Get("edu.institution")) || IsNonBlank(row.Get("edu.qualification")) ||
            IsNonBlank(row.Get("edu.startYear")) || IsNonBlank(row.Get("edu.endYear")))
        {
            var inst = row.Get("edu.institution").Trim();
            var qual = row.Get("edu.qualification").Trim();
            if (IsNonBlank(inst) || IsNonBlank(qual))
            {
                if (string.IsNullOrWhiteSpace(inst) || string.IsNullOrWhiteSpace(qual))
                    return new ImportRowOutcome("error",
                        string.IsNullOrWhiteSpace(inst) ? "Education row: Institution is empty — both Institution and Qualification must be filled for an education record."
                        : "Education row: Qualification is empty — both Institution and Qualification must be filled for an education record.");
                if (int.TryParse(row.Get("edu.startYear").Trim(), out var sy) &&
                    int.TryParse(row.Get("edu.endYear").Trim(), out var ey) && ey < sy)
                    return new ImportRowOutcome("error",
                        $"Education row: end year '{ey}' is before start year '{sy}' — an education record cannot end before it begins.");
            }
        }

        if (isUpdate && target is null)
        {
            var key = !string.IsNullOrWhiteSpace(employeeNo) ? $"employee number '{employeeNo}'"
                : !string.IsNullOrWhiteSpace(nrc) ? $"NRC '{nrc}'"
                : !string.IsNullOrWhiteSpace(napsa) ? $"NAPSA number '{napsa}'"
                : "employee number, NRC or NAPSA number";
            return new ImportRowOutcome("error", $"No existing employee matches {key}. Check the identifier or switch to Insert mode.");
        }
        var formatErrors = new List<string>();
        if (target is null)
        {
            var missing = new List<string>();
            foreach (var field in Fields)
                if (field.Required && string.IsNullOrWhiteSpace(row.Get(field.Key)))
                    missing.Add(field.Label);
            if (missing.Count > 0)
                formatErrors.Add($"Missing required { (missing.Count == 1 ? "field" : "fields") }: {string.Join(", ", missing)}. The row cannot be imported until they are filled in.");
        }

        var firstName = row.Get("firstName");
        var lastName = row.Get("lastName");
        var email = row.Get("email").Trim();
        var phone = row.Get("phone").Trim();
        var workerType = NormalizeWorkerType(row.Get("workerType"));

        // ---- Format checks with explicit what-is-wrong wording.
        // In Update mode a blank value means "leave the record's existing
        // value untouched", so only non-blank values are checked. ----
        if (EmailInvalid(email))
            formatErrors.Add($"Work email '{email}' is not a valid email address.");
        if (isUpdate && string.IsNullOrWhiteSpace(phone))
            phone = null; // blank in Update mode means "keep the existing phone"
        if (!string.IsNullOrWhiteSpace(phone) && !PhoneValid(phone))
            formatErrors.Add($"Phone '{phone}' is not valid — use 9–15 digits, e.g. 0971234567.");
        if (!string.IsNullOrWhiteSpace(workerType) &&
            workerType is not ("employee" or "contingent" or "intern" or "volunteer"))
            formatErrors.Add($"Employment type '{row.Get("workerType")}' is not valid — use Permanent / Employee, Contractor / Contingent, Intern or Volunteer (any letter case).");
        var tpin = row.Get("tpin").Trim();
        if (!string.IsNullOrWhiteSpace(tpin) && !TpinValid(tpin))
            formatErrors.Add($"TPIN '{tpin}' is not valid — use exactly 10 digits.");
        var startDate = row.Get("startDate").Trim();
        if (!string.IsNullOrWhiteSpace(startDate) && !TryParseImportDate(startDate, out _))
            formatErrors.Add($"Start date '{startDate}' is not valid — use DD-MM-YYYY, e.g. 02-01-2026.");

        foreach (var (key, label) in new[] { ("firstName", "First name"), ("middleName", "Middle name"), ("lastName", "Last name") })
            if (!string.IsNullOrWhiteSpace(row.Get(key)) && !System.Text.RegularExpressions.Regex.IsMatch(row.Get(key), @"\p{L}"))
                formatErrors.Add($"{label} must contain letters.");
        if (!string.IsNullOrWhiteSpace(nrc) && !System.Text.RegularExpressions.Regex.IsMatch(nrc, @"^\d{6}/\d{2}/\d$"))
            formatErrors.Add("NRC must use the format 123456/78/1.");
        Guid? resolvedLocation = null;
        var branch = row.Get("locationId").Trim();
        if (!string.IsNullOrWhiteSpace(branch))
        {
            var locations = await repo.ListAllLocationsAsync(ct);
            var matches = locations.Where(location => !location.IsArchived).Where(location => Guid.TryParse(branch, out var id)
                ? location.Id == id : string.Equals(location.Name, branch, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1)
                formatErrors.Add(matches.Length == 0 ? $"Branch '{branch}' does not exist in your company access. Choose an existing branch." : $"Branch '{branch}' is ambiguous. Use its branch ID.");
            else if ((scope.EntityId.HasValue && matches[0].LegalEntityId != scope.EntityId.Value)
                || (scope.IsConfined && !scope.AllowedLocationIds.Contains(matches[0].Id)))
                formatErrors.Add("The selected branch is outside your company or assigned branch access.");
            else resolvedLocation = matches[0].Id;
        }
        if (formatErrors.Count > 0) return new ImportRowOutcome("error", string.Join(" ", formatErrors));

        // ---- Cross-field uniqueness: never import two rows that collide with each
        // other, so a bad spreadsheet cannot mass-duplicate identities in one batch.
        // Existing-identity sets are loaded once per preview pass (small scans),
        // then each row checks against them plus the in-file Seen sets. ----
        if (!IdentitiesLoaded)
        {
            var all = await repo.ListAllWorkersAsync(null, ct);
            ExistingWorkers = all;
            IdentitiesLoaded = true;
            foreach (var w in all)
            {
                if (!string.IsNullOrWhiteSpace(w.Email)) ExistingEmails.Add(w.Email.ToLowerInvariant());
                if (!string.IsNullOrWhiteSpace(w.Phone)) ExistingPhones.Add(w.Phone);
                if (!string.IsNullOrWhiteSpace(w.Nrc)) ExistingNrcs.Add(w.Nrc.ToLowerInvariant());
                if (!string.IsNullOrWhiteSpace(w.Tpin)) ExistingTpins.Add(w.Tpin);
                if (!string.IsNullOrWhiteSpace(w.EmployeeNo)) ExistingEmployeeNos.Add(w.EmployeeNo.ToLowerInvariant());
            }
        }
        if (!string.IsNullOrWhiteSpace(email))
        {
            var targetOwnsThis = target is not null &&
                !string.IsNullOrWhiteSpace(target.Email) &&
                target.Email.Equals(email, StringComparison.OrdinalIgnoreCase);
            if (!targetOwnsThis && ExistingEmails.Contains(email.ToLowerInvariant()))
                return new ImportRowOutcome("error", $"Work email '{email}' is already in use by an existing employee.");
            if (!targetOwnsThis && !SeenEmails.Add(email.ToLowerInvariant()))
                return new ImportRowOutcome("error", $"Work email '{email}' appears twice in this file. Each employee must have a unique email.");
        }
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var targetOwnsThis = target is not null && string.Equals(target.Phone, phone, StringComparison.OrdinalIgnoreCase);
            if (!targetOwnsThis && ExistingPhones.Contains(phone)) return new ImportRowOutcome("error", $"Phone '{phone}' is already in use by an existing employee.");
            if (!targetOwnsThis && !SeenPhones.Add(phone)) return new ImportRowOutcome("error", $"Phone '{phone}' appears twice in this file. Each employee must have a unique phone number.");
        }
        if (!string.IsNullOrWhiteSpace(tpin))
        {
            var targetOwnsThis = target is not null && string.Equals(target.Tpin, tpin, StringComparison.OrdinalIgnoreCase);
            if (!targetOwnsThis && ExistingTpins.Contains(tpin)) return new ImportRowOutcome("error", $"TPIN '{tpin}' is already in use by an existing employee.");
            if (!targetOwnsThis && !SeenTpins.Add(tpin)) return new ImportRowOutcome("error", $"TPIN '{tpin}' appears twice in this file. Each employee must have a unique TPIN.");
        }
        // Update mode matched THIS record — its own identifiers are exempt
        // from the within-file uniqueness check (updating yourself is allowed).
        if (target is not null)
        {
            if (!string.IsNullOrWhiteSpace(target.Email)) _ = SeenEmails.Add(target.Email.ToLowerInvariant());
            if (!string.IsNullOrWhiteSpace(target.EmployeeNo)) _ = SeenEmployeeNos.Add(target.EmployeeNo.ToLowerInvariant());
            if (!string.IsNullOrWhiteSpace(target.Nrc)) _ = SeenNrcs.Add(target.Nrc.ToLowerInvariant());
        }
        if (!string.IsNullOrWhiteSpace(employeeNo))
        {
            var targetOwnsThis = target is not null &&
                !string.IsNullOrWhiteSpace(target.EmployeeNo) &&
                target.EmployeeNo.Equals(employeeNo, StringComparison.OrdinalIgnoreCase);
            if (!targetOwnsThis && ExistingEmployeeNos.Contains(employeeNo.ToLowerInvariant()))
                return new ImportRowOutcome("error", $"Employee number '{employeeNo}' is already assigned to an existing employee.");
            // The matched record's own key was already exempted below;
            // only flag genuine within-file duplicates.
            if (!targetOwnsThis && !SeenEmployeeNos.Add(employeeNo.ToLowerInvariant()))
                return new ImportRowOutcome("error", $"Employee number '{employeeNo}' appears twice in this file. Each employee must have a unique number.");
        }
        if (!string.IsNullOrWhiteSpace(nrc))
        {
            var targetOwnsThis = target is not null &&
                !string.IsNullOrWhiteSpace(target.Nrc) &&
                target.Nrc.Equals(nrc, StringComparison.OrdinalIgnoreCase);
            if (!targetOwnsThis && ExistingNrcs.Contains(nrc.ToLowerInvariant()))
                return new ImportRowOutcome("error", $"NRC '{nrc}' is already in use by an existing employee.");
            if (!targetOwnsThis && !SeenNrcs.Add(nrc.ToLowerInvariant()))
                return new ImportRowOutcome("error", $"NRC '{nrc}' appears twice in this file. Each employee must have a unique NRC.");
        }

        if (!string.IsNullOrWhiteSpace(napsa))
        {
            var targetOwnsThis = target is not null && string.Equals(target.NapsaNumber, napsa, StringComparison.OrdinalIgnoreCase);
            if (!targetOwnsThis && !SeenNapsas.Add(napsa))
                return new ImportRowOutcome("error", $"NAPSA number '{napsa}' appears twice in this file. Correct the duplicate before importing.");
        }

        // ---- Reference data that must exist before payroll-relevant rows land. ----
        var orgUnits = await repo.ListAllOrgUnitsAsync(ct);
        var orgUnitName = row.Get("orgUnitName").Trim();
        if (!string.IsNullOrWhiteSpace(orgUnitName) &&
            !orgUnits.Any(u => u.Name.Equals(orgUnitName, StringComparison.OrdinalIgnoreCase)))
            return new ImportRowOutcome("error", $"No department named '{orgUnitName}' exists. Create it under Organisation structure first.");

        var resolved = new Dictionary<string, string>(row, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(workerType)) resolved["workerType"] = workerType;
        if (resolvedLocation.HasValue) resolved["locationId"] = resolvedLocation.Value.ToString();
        if (!string.IsNullOrWhiteSpace(orgUnitName)) resolved["orgUnitName"] = orgUnitName;
        var enrichmentError = await ValidateEnrichmentAsync(resolved, target, ct);
        if (enrichmentError is not null) return new ImportRowOutcome("error", enrichmentError);
        if (target is not null) resolved["__workerId"] = target.Id.ToString();

        // ---- Natural-key match drives the Insert-vs-Update status in the preview. ----
        if (target is not null)
            return new ImportRowOutcome("update", mode.Equals("fill-missing", StringComparison.OrdinalIgnoreCase) ? $"Existing record: {target.FullName}; only blank fields will be filled." : $"Existing record: {target.FullName}", resolved);
        return new ImportRowOutcome("create", null, resolved);
    }

    // --- Batch-level identity uniqueness sets (cleared per schema instance).
    private readonly HashSet<string> SeenEmails = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> SeenEmployeeNos = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> SeenNrcs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> SeenNapsas = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> SeenPhones = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> SeenTpins = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> ExistingEmails = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> ExistingEmployeeNos = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> ExistingNrcs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> ExistingPhones = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> ExistingTpins = new(StringComparer.OrdinalIgnoreCase);
    private bool IdentitiesLoaded;
    private List<Worker> ExistingWorkers = [];

    private static string NormalizeWorkerType(string value) => value.Trim().ToLowerInvariant() switch
    {
        "permanent" or "employee" => "employee",
        "contractor" or "contingent" => "contingent",
        "intern" => "intern",
        "volunteer" => "volunteer",
        var other => other,
    };

    private static readonly System.Text.RegularExpressions.Regex EmailRx = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static bool EmailInvalid(string email) => !string.IsNullOrWhiteSpace(email) && !EmailRx.IsMatch(email);
    // Zambian-style mobile landline numbers: digits only, 9-15 long (0971234567 style).
    private static bool PhoneValid(string phone) => !string.IsNullOrWhiteSpace(phone) && System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\+?\d{9,15}$");
    // ZRA TPIN: exactly 10 digits.
    private static bool TpinValid(string tpin) => System.Text.RegularExpressions.Regex.IsMatch(tpin, @"^\d{10}$");

    /// Locates a worker by the row's natural keys, falling back to the work
    /// email when none of the keys were supplied. Preview already refused
    /// duplicate emails inside the file and against existing records, so the
    /// email uniquely identifies the just-created row.
    private async Task<Worker?> FindByNaturalOrEmailAsync(string? email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        return await repo.FindByEmailAsync(email, ct);
    }

    // Any supplied identity may locate an existing employee. If identities point
    // to different people, do not guess: the spreadsheet must be corrected.
    private async Task<Worker?> ResolveTargetAsync(string employeeNo, string nrc, string napsa,
        string email, string phone, string tpin, CancellationToken ct)
    {
        if (!IdentitiesLoaded)
        {
            ExistingWorkers = await repo.ListAllWorkersAsync(null, ct);
            foreach (var worker in ExistingWorkers)
            {
                if (!string.IsNullOrWhiteSpace(worker.Email)) ExistingEmails.Add(worker.Email.ToLowerInvariant());
                if (!string.IsNullOrWhiteSpace(worker.Phone)) ExistingPhones.Add(worker.Phone);
                if (!string.IsNullOrWhiteSpace(worker.Nrc)) ExistingNrcs.Add(worker.Nrc.ToLowerInvariant());
                if (!string.IsNullOrWhiteSpace(worker.Tpin)) ExistingTpins.Add(worker.Tpin);
                if (!string.IsNullOrWhiteSpace(worker.EmployeeNo)) ExistingEmployeeNos.Add(worker.EmployeeNo.ToLowerInvariant());
            }
            IdentitiesLoaded = true;
        }
        var matches = ExistingWorkers.Where(w =>
            (!string.IsNullOrWhiteSpace(employeeNo) && string.Equals(w.EmployeeNo, employeeNo, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(nrc) && string.Equals(w.Nrc, nrc, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(napsa) && string.Equals(w.NapsaNumber, napsa, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(email) && string.Equals(w.Email, email, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(phone) && string.Equals(w.Phone, phone, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(tpin) && string.Equals(w.Tpin, tpin, StringComparison.OrdinalIgnoreCase)))
            .DistinctBy(w => w.Id).ToList();
        if (matches.Count > 1)
            throw new DomainException("import-identity-conflict", "The identifiers on this row belong to different employees. Correct the employee number, email, phone, NRC, TPIN or NAPSA number before importing.");
        return matches.SingleOrDefault();
    }

    public async Task ApplyRowAsync(IDictionary<string, string> row, string mode, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        Worker? target = null;
        if (row.TryGetValue("__workerId", out var targetId) && Guid.TryParse(targetId, out var workerId))
            target = await repo.GetByIdAsync(workerId, ct);
        target ??= await ResolveTargetAsync(row.Get("employeeNo"), row.Get("nrc"), row.Get("napsaNumber"), row.Get("email"), row.Get("phone"), row.Get("tpin"), ct);
        var orgUnits = await repo.ListAllOrgUnitsAsync(ct);
        var orgUnitName = row.Get("orgUnitName").Trim();
        Guid? orgUnitId = null;
        if (!string.IsNullOrWhiteSpace(orgUnitName))
            orgUnitId = orgUnits.FirstOrDefault(u => u.Name.Equals(orgUnitName, StringComparison.OrdinalIgnoreCase))?.Id;
        if (!string.IsNullOrWhiteSpace(orgUnitName) && orgUnitId is null)
            throw new DomainException("org-unit-not-found", $"No department named '{orgUnitName}' exists.");
        var workerType = NormalizeWorkerType(row.Get("workerType"));
        if (workerType is not ("employee" or "contingent" or "intern" or "volunteer") && (target is null || !string.IsNullOrWhiteSpace(workerType)))
            throw new DomainException("import-worker-type", "Employment type must be recognized before importing. Preview the corrected row again.");

        if (target is not null)
        {
            var fillMissing = mode.Equals("fill-missing", StringComparison.OrdinalIgnoreCase);
            // Update mode — patch only the fields the row actually supplies;
            // blank values leave the record's existing values untouched.
            var patch = new WorkerUpdateRequest(
                FirstName: ValueToApply(target.FirstName, row.Get("firstName"), fillMissing),
                MiddleName: ValueToApply(target.MiddleName, row.Get("middleName"), fillMissing),
                LastName: ValueToApply(target.LastName, row.Get("lastName"), fillMissing),
                Email: ValueToApply(target.Email, row.Get("email"), fillMissing),
                Phone: ValueToApply(target.Phone, row.Get("phone"), fillMissing),
                Nrc: ValueToApply(target.Nrc, row.Get("nrc"), fillMissing),
                Tpin: ValueToApply(target.Tpin, row.Get("tpin"), fillMissing),
                NapsaNumber: ValueToApply(target.NapsaNumber, row.Get("napsaNumber"), fillMissing),
                NhimaNumber: ValueToApply(target.NhimaNumber, row.Get("nhimaNumber"), fillMissing),
                Grade: ValueToApply(target.Grade, row.Get("grade"), fillMissing),
                JobTitle: ValueToApply(target.JobTitle, row.Get("jobTitle"), fillMissing),
                OrgUnitId: orgUnitId,
                LocationId: Guid.TryParse(row.Get("locationId"), out var updateLocation)
                    && (!fillMissing || !target.LocationId.HasValue) ? updateLocation : null);
            await workers.UpdateAsync(target.Id, patch, ct);
            await ApplyChildRowsAsync(target.Id, row, orgUnits, ct);
        }

        // NOTE on blank-update semantics: ApplyRowAsync is only ever reached for
        // rows the preview approved. The preview already refused blank emails in
        // insert mode and treats blank optional values in update mode as
        // "unchanged", so no existing value is silently cleared here.
        else
        {
            // Insert mode — full lifecycle via the same service the UI uses.
            // M54 / M54.3: an explicit `locationId` column wins; otherwise the
            // row is hired into the operator's current work scope (branch
            // switcher / confinement — which is an ORG UNIT, not a work
            // location; the header may carry either).
            Guid? locationId = Guid.TryParse(OrNull(row.Get("locationId")), out var lid)
                ? lid
                : scope.LocationId;
            orgUnitId = orgUnitId ?? scope.OrgUnitId;
            var request = new WorkerCreateRequest(
                OrNull(row.Get("employeeNo")),
                OrNull(row.Get("firstName")), OrNull(row.Get("lastName")),
                MiddleName: OrNull(row.Get("middleName")),
                Email: OrNull(row.Get("email")),
                Phone: OrNull(row.Get("phone")),
                Nrc: OrNull(row.Get("nrc")),
                Tpin: OrNull(row.Get("tpin")),
                NapsaNumber: OrNull(row.Get("napsaNumber")),
                NhimaNumber: OrNull(row.Get("nhimaNumber")),
                Grade: OrNull(row.Get("grade")),
                JobTitle: OrNull(row.Get("jobTitle")),
                StartDate: NormalizedImportDate(row.Get("startDate")),
                WorkerType: workerType,
                OrgUnitId: orgUnitId,
                LocationId: locationId);
            await workers.CreateAsync(request, ct);
            // The freshly created worker is located again by the same natural
            // keys the import row was built with (CreateAsync returns a DTO,
            // not the id). Insert mode always carries a required email, so a
            // row without any natural keys falls back to locating by email.
            var created = await repo.FindByNaturalKeyAsync(
                OrNull(row.Get("employeeNo")), OrNull(row.Get("nrc")),
                OrNull(row.Get("napsaNumber")), ct)
                ?? await FindByNaturalOrEmailAsync(OrNull(row.Get("email")), ct)
                ?? throw new DomainException("import-create-lost",
                    "The worker row was applied but could not be re-located for history records — report this to support.");
            await ApplyChildRowsAsync(created.Id, row, orgUnits, ct);
            target = created;
        }
        await ApplyPayrollAndOvertimeAsync(target ?? throw new DomainException("import-worker-missing", "Imported employee could not be located."), row, mode, ct);
    }

    private async Task<string?> ValidateEnrichmentAsync(IDictionary<string, string> row, Worker? target, CancellationToken ct)
    {
        var basic = row.Get("basicSalary").Trim();
        var cola = row.Get("costOfLivingAllowance").Trim();
        if (!string.IsNullOrWhiteSpace(basic) || !string.IsNullOrWhiteSpace(cola))
        {
            if (payrollRepo is null) return "Payroll pay import is unavailable in this environment.";
            if (!string.IsNullOrWhiteSpace(basic) && (!decimal.TryParse(basic, NumberStyles.Number, CultureInfo.InvariantCulture, out _) || decimal.Parse(basic, CultureInfo.InvariantCulture) <= 0))
                return $"Basic salary '{basic}' is not valid — use a positive plain number, e.g. 12000.";
            if (!string.IsNullOrWhiteSpace(cola) && (!decimal.TryParse(cola, NumberStyles.Number, CultureInfo.InvariantCulture, out _) || decimal.Parse(cola, CultureInfo.InvariantCulture) < 0))
                return $"Cost of living allowance '{cola}' is not valid — use zero or a positive plain number.";
            var components = await payrollRepo.ListAllComponentsAsync(ct);
            var basicComponent = components.FirstOrDefault(c => c.Code.Equals("basic", StringComparison.OrdinalIgnoreCase) && c.IsActive);
            if (!string.IsNullOrWhiteSpace(basic) && basicComponent is null) return "No active Basic Salary component is configured. Configure it under Payroll configuration before importing salaries.";
            var colaComponent = components.FirstOrDefault(IsCostOfLiving);
            var colaBenefit = benefits is null ? null : (await benefits.ListBenefitTypesAsync(ct))
                .FirstOrDefault(type => type.IsActive && type.IncludeInPayroll && IsCostOfLiving(type));
            if (!string.IsNullOrWhiteSpace(cola) && colaComponent is null && colaBenefit is null)
                return "No active Cost of Living Allowance salary component or payroll benefit is configured. Add one under Payroll configuration or Benefits, mark the benefit as Added to payslip, then import again.";
            if (!string.IsNullOrWhiteSpace(cola) && colaComponent is null && colaBenefit is not null)
            {
                var annualAmount = decimal.Parse(cola, CultureInfo.InvariantCulture) * 12m;
                if (annualAmount > colaBenefit.AnnualCap)
                    return $"Cost of living allowance {cola} per month is {annualAmount:N2} per year, which exceeds the {colaBenefit.Name} annual cap of {colaBenefit.AnnualCap:N2}. Increase the configured cap or correct the import value.";
                row["__colaBenefitTypeId"] = colaBenefit.Id.ToString();
                row["__colaBenefitAnnualAmount"] = annualAmount.ToString(CultureInfo.InvariantCulture);
            }
            var groups = await payrollRepo.ListPayGroupsAsync(ct);
            var groupName = row.Get("payGroup").Trim();
            var group = !string.IsNullOrWhiteSpace(groupName) ? groups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase)) : groups.Count == 1 ? groups[0] : null;
            if (group is null) return string.IsNullOrWhiteSpace(groupName) ? "Payroll pay needs a Pay group because more than one pay group is configured." : $"No pay group named '{groupName}' exists.";
            if (!string.IsNullOrWhiteSpace(basic) && basicComponent is not null) row["__basicComponentId"] = basicComponent.Id.ToString();
            if (!string.IsNullOrWhiteSpace(cola) && colaComponent is not null) row["__colaComponentId"] = colaComponent.Id.ToString();
            row["__payGroupId"] = group.Id.ToString();
            var effectiveRaw = row.Get("salaryEffectiveFrom").Trim();
            if (!string.IsNullOrWhiteSpace(effectiveRaw) && !TryParseImportDate(effectiveRaw, out _)) return $"Salary effective date '{effectiveRaw}' is not valid — use DD-MM-YYYY.";
            DateOnly effective;
            if (!string.IsNullOrWhiteSpace(effectiveRaw))
                _ = TryParseImportDate(effectiveRaw, out effective);
            else if (!TryParseImportDate(row.Get("startDate"), out effective))
                effective = DateOnly.FromDateTime(DateTime.UtcNow);
            row["__salaryEffectiveFrom"] = effective.ToString("yyyy-MM-dd");
        }

        var overtimeDate = row.Get("overtime.workDate").Trim();
        var overtimeHours = row.Get("overtime.hours").Trim();
        if (string.IsNullOrWhiteSpace(overtimeDate) != string.IsNullOrWhiteSpace(overtimeHours)) return "Overtime requires both Overtime: Work date and Overtime: Hours.";
        if (!string.IsNullOrWhiteSpace(overtimeDate))
        {
            if (time is null) return "Overtime import is unavailable in this environment.";
            if (!TryParseImportDate(overtimeDate, out var date)) return $"Overtime work date '{overtimeDate}' is not valid — use DD-MM-YYYY.";
            if (!decimal.TryParse(overtimeHours, NumberStyles.Number, CultureInfo.InvariantCulture, out var hours) || hours <= 0) return $"Overtime hours '{overtimeHours}' must be a positive number.";
            var multiplier = row.Get("overtime.multiplier").Trim();
            if (!string.IsNullOrWhiteSpace(multiplier) && (!decimal.TryParse(multiplier, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value <= 0)) return $"Overtime multiplier '{multiplier}' must be a positive number.";
            var status = row.Get("overtime.status").Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(status) && status is not ("pending" or "approved")) return "Overtime status must be pending or approved.";
            if (target is null && string.IsNullOrWhiteSpace(row.Get("employeeNo"))) return "Overtime on a new employee requires an Employee number so the attendance record can be linked.";
            row["__overtimeDate"] = date.ToString("yyyy-MM-dd");
        }
        return null;
    }

    private async Task ApplyPayrollAndOvertimeAsync(Worker worker, IDictionary<string, string> row, string mode, CancellationToken ct)
    {
        if (row.ContainsKey("__basicComponentId") || row.ContainsKey("__colaComponentId") || row.ContainsKey("__colaBenefitTypeId"))
        {
            if (payrollRepo is null || payroll is null) throw new DomainException("import-payroll-unavailable", "Payroll pay import is unavailable in this environment.");
            var existing = await payrollRepo.FindOpenProfileAsync(worker.Id, ct);
            var fillMissing = mode.Equals("fill-missing", StringComparison.OrdinalIgnoreCase);
            var values = existing?.ComponentValues.Select(v => new WorkerComponentValueCreate(v.ComponentId, v.Component?.Code, v.Amount)).ToList() ?? [];
            var changed = false;
            changed |= ApplyImportedComponent(values, row, "__basicComponentId", "basicSalary", "basic", fillMissing);
            changed |= ApplyImportedComponent(values, row, "__colaComponentId", "costOfLivingAllowance", "cost-of-living-allowance", fillMissing);
            if (changed || existing is null)
                await payroll.UpsertProfileAsync(worker.Id, new WorkerPayrollProfileCreate(worker.Id, Guid.Parse(row["__payGroupId"]), row["__salaryEffectiveFrom"], values), ct);
        }
        if (row.TryGetValue("__colaBenefitTypeId", out var colaBenefitTypeId))
        {
            if (benefits is null) throw new DomainException("import-benefit-unavailable", "Payroll benefit import is unavailable in this environment.");
            var benefitTypeId = Guid.Parse(colaBenefitTypeId);
            var year = DateOnly.Parse(row["__salaryEffectiveFrom"], CultureInfo.InvariantCulture).Year;
            var existingAllowance = await benefits.GetAllowanceAsync(worker.Id, benefitTypeId, year, ct);
            if (!mode.Equals("fill-missing", StringComparison.OrdinalIgnoreCase) || existingAllowance is null)
            {
                await benefits.SetAllowanceAsync(new WorkerBenefitAllowance
                {
                    WorkerId = worker.Id,
                    BenefitTypeId = benefitTypeId,
                    AnnualAmount = decimal.Parse(row["__colaBenefitAnnualAmount"], CultureInfo.InvariantCulture),
                    Year = year,
                }, ct);
            }
        }
        if (row.TryGetValue("__overtimeDate", out var overtimeDate))
        {
            if (time is null) throw new DomainException("import-overtime-unavailable", "Overtime import is unavailable in this environment.");
            var multiplier = decimal.TryParse(row.Get("overtime.multiplier"), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : (decimal?)null;
            await time.ImportOvertimeAsync(new OvertimeImportRequest("employee-import", [new OvertimeImportRow(worker.EmployeeNo ?? throw new DomainException("import-overtime-employee-number", "Overtime needs an employee number."), overtimeDate, decimal.Parse(row.Get("overtime.hours"), CultureInfo.InvariantCulture), multiplier, OrNull(row.Get("overtime.reason")), OrNull(row.Get("overtime.status")))], row.Get("overtime.status").Equals("approved", StringComparison.OrdinalIgnoreCase)), authz.CurrentSubjectId ?? "system", ct);
        }
    }

    private static bool ApplyImportedComponent(List<WorkerComponentValueCreate> values, IDictionary<string, string> row,
        string idKey, string amountKey, string code, bool fillMissing)
    {
        if (!row.TryGetValue(idKey, out var rawId)) return false;
        var id = Guid.Parse(rawId);
        var index = values.FindIndex(v => v.ComponentId == id);
        if (index >= 0 && fillMissing) return false;
        var value = new WorkerComponentValueCreate(id, code, decimal.Parse(row.Get(amountKey), CultureInfo.InvariantCulture));
        if (index < 0) values.Add(value); else values[index] = value;
        return true;
    }

    private static bool IsCostOfLiving(SalaryComponent component) =>
        IsCostOfLiving(component.Code, component.Name);

    private static bool IsCostOfLiving(BenefitType type) =>
        IsCostOfLiving(type.Code, type.Name);

    private static bool IsCostOfLiving(string? code, string? name) =>
        string.Equals(code, "cost-of-living-allowance", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(code, "cost-of-living", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(code, "cola", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "Cost of Living Allowance", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "Cost of Living", StringComparison.OrdinalIgnoreCase);

    private static string? ValueToApply(string? existing, string imported, bool fillMissing)
    {
        var value = OrNull(imported);
        return value is null || (fillMissing && !string.IsNullOrWhiteSpace(existing)) ? null : value;
    }

    /// Applies the optional education / external / internal history child rows
    /// that the flattened import layout exposes (edu.*, ext.*, int.* keys).
    /// Only creates — never deletes — matching the blank-values-mean-unchanged
    /// contract documented above. A single child row per type is supported per
    /// import row, which matches the flattened export shape this schema reads.
    private async Task ApplyChildRowsAsync(Guid workerId, IDictionary<string, string> row,
        List<OrgUnit> orgUnits, CancellationToken ct)
    {
        // Education: both of the two visible columns must be present — a
        // half-filled education row silently imported would produce junk data.
        var eduInst = OrNull(row.Get("edu.institution"));
        var eduQual = OrNull(row.Get("edu.qualification"));
        if (eduInst is not null && eduQual is not null)
        {
            int.TryParse(OrNull(row.Get("edu.startYear")), out var sy);
            int.TryParse(OrNull(row.Get("edu.endYear")), out var ey);
            await repo.AddEducationAsync(new WorkerEducation
            {
                WorkerId = workerId,
                Institution = eduInst.Trim(),
                Qualification = eduQual.Trim(),
                StartYear = sy != 0 ? sy : null,
                EndYear = ey != 0 ? ey : null,
            }, ct);
        }

        // Previous employment (external work history) — company is the anchor.
        var extCompany = OrNull(row.Get("ext.company"));
        if (extCompany is not null)
        {
            await repo.AddExternalWorkHistoryAsync(new ExternalWorkHistory
            {
                WorkerId = workerId,
                Company = extCompany.Trim(),
                Role = OrNull(row.Get("ext.role"))?.Trim(),
                StartDate = NormalizedImportDate(row.Get("ext.startDate")),
                EndDate = NormalizedImportDate(row.Get("ext.endDate")),
            }, ct);
        }

        // Internal move — the resolved department name is what history stores.
        var intOrg = OrNull(row.Get("int.orgUnitName"));
        var intRole = OrNull(row.Get("int.role"));
        if (intOrg is not null || intRole is not null)
        {
            var resolved = orgUnits
                .FirstOrDefault(u => u.Name.Equals(intOrg ?? "", StringComparison.OrdinalIgnoreCase))?.Name ?? intOrg;
            if (string.IsNullOrWhiteSpace(resolved)) return; // nothing meaningful to record
            await repo.AddInternalWorkHistoryAsync(new InternalWorkHistory
            {
                WorkerId = workerId,
                OrgUnitName = resolved.Trim(),
                Role = intRole?.Trim(),
                StartDate = NormalizedImportDate(row.Get("int.startDate")),
            }, ct);
        }
    }

    private static bool IsNonBlank(string v) => !string.IsNullOrWhiteSpace(v);

    private static readonly string[] ImportDateFormats =
    [
        "dd-MM-yyyy",
        "dd/MM/yyyy",
        "dd.MM.yyyy",
        "yyyy-MM-dd",
    ];

    private static bool TryParseImportDate(string? value, out DateOnly date)
    {
        var t = value?.Trim();
        if (string.IsNullOrWhiteSpace(t))
        {
            date = default;
            return false;
        }

        return DateOnly.TryParseExact(t, ImportDateFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    }

    /// Accepts day-first import dates and returns the ISO value the domain services use.
    private static string? NormalizedImportDate(string? v)
    {
        return TryParseImportDate(v, out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }

    public async Task<List<Dictionary<string, string>>> ExportRowsAsync(string? filter, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        // M31b: filter support (status=Active) + child-table flattening.
        // If a worker has multiple history records, we repeat the parent row.
        var status = filter?.StartsWith("status=") == true ? filter[7..] : null;
        var items = await repo.ListAllWorkersWithDetailsAsync(status, ct);

        var rows = new List<Dictionary<string, string>>();
        foreach (var w in items)
        {
            var baseRow = new Dictionary<string, string>
            {
                ["employeeNo"] = w.EmployeeNo ?? "",
                ["firstName"] = w.FirstName,
                ["lastName"] = w.LastName,
                ["middleName"] = w.MiddleName ?? "",
                ["email"] = w.Email ?? "",
                ["phone"] = w.Phone ?? "",
                ["nrc"] = w.Nrc ?? "",
                ["tpin"] = w.Tpin ?? "",
                ["napsaNumber"] = w.NapsaNumber ?? "",
                ["nhimaNumber"] = w.NhimaNumber ?? "",
                ["grade"] = w.Grade ?? "",
                ["jobTitle"] = w.JobTitle ?? "",
                ["startDate"] = w.StartDate?.ToString("yyyy-MM-dd") ?? "",
                ["workerType"] = w.WorkerType ?? "employee",
                ["orgUnitName"] = w.OrgUnit?.Name ?? "",
            };

            // Flatten child records: repeat the parent row for each child record
            // across the three types. If no children, just the base row.
            var max = Math.Max(w.Education.Count, Math.Max(w.ExternalWorkHistory.Count, w.InternalWorkHistory.Count));
            if (max == 0)
            {
                rows.Add(baseRow);
                continue;
            }

            var edus = w.Education.ToList();
            var exts = w.ExternalWorkHistory.ToList();
            var ints = w.InternalWorkHistory.ToList();

            for (var i = 0; i < max; i++)
            {
                var row = new Dictionary<string, string>(baseRow);
                if (i < edus.Count)
                {
                    row["edu.institution"] = edus[i].Institution;
                    row["edu.qualification"] = edus[i].Qualification;
                    row["edu.startYear"] = edus[i].StartYear?.ToString() ?? "";
                    row["edu.endYear"] = edus[i].EndYear?.ToString() ?? "";
                }
                if (i < exts.Count)
                {
                    row["ext.company"] = exts[i].Company;
                    row["ext.role"] = exts[i].Role ?? "";
                    row["ext.startDate"] = exts[i].StartDate ?? "";
                    row["ext.endDate"] = exts[i].EndDate ?? "";
                }
                if (i < ints.Count)
                {
                    row["int.orgUnitName"] = ints[i].OrgUnitName;
                    row["int.role"] = ints[i].Role ?? "";
                    row["int.startDate"] = ints[i].StartDate ?? "";
                }
                rows.Add(row);
            }
        }
        return rows;
    }

    private static string? OrNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;
}

/// Convenience: dictionary lookup returning "" for missing keys.
internal static class ImportRowExtensions
{
    public static string Get(this IDictionary<string, string> row, string key)
        => row.TryGetValue(key, out var v) ? (v ?? "") : "";
}
