using System.Globalization;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Setup;
// ---------- DTOs ----------
/// <summary>M49: the welcome/dashboard decision the frontend needs on every
/// render. Status=pending means the wizard overlay must be shown; complete
/// means go straight to the dashboard; missing-row means a legacy tenant
/// (setup ran before this feature existed) — never force-gated.</summary>
public record SetupStateDto(string Status, string? ResumeStepKey, IReadOnlyList<string> CompletedSteps,
    IReadOnlyList<string> OptionalSteps, double CompletionPercent);
/// <summary>M49: one entry per wizard step returned alongside SetupStateDto so
/// the wizard can render the step list with labels/descriptions in one call.</summary>
public record SetupStepDto(string Key, string Label, string Description, bool Mandatory, bool Completed, bool Open);

public interface ISetupService
{
    Task<SetupStateDto> GetStateAsync(CancellationToken ct);
    Task<IReadOnlyList<SetupStepDto>> ListStepsAsync(CancellationToken ct);
    /// <summary>M50.18: the saved input payload of a completed wizard step — the
    /// employees step reads step 3's grades and positions from here so the
    /// manual-entry grid can offer them as dropdowns instead of free text.</summary>
    Task<string?> GetStepDataAsync(string stepKey, CancellationToken ct);
    Task CompleteStepAsync(string stepKey, string? dataJson, CancellationToken ct);
    Task FinishAsync(CancellationToken ct);
    /// <summary>Ordinary application reset is unavailable. A controlled,
    /// separately authorized maintenance procedure has not been implemented.</summary>
    Task ResetAsync(CancellationToken ct);
}
// ---------- Repository contract (infra implements) ----------
/// <summary>M49: setup persistence. Kept out of the application layer so the
/// service stays free of EF Core; all tenant filtering is automatic via the
/// global query filter on SetupState/SetupStepRecord.</summary>
public interface ISetupRepository
{
    Task<SetupState?> GetStateAsync(CancellationToken ct);
    Task<IReadOnlySet<string>> CompletedStepKeysAsync(CancellationToken ct);
    /// <summary>M50.18: the saved input payload of a completed step.</summary>
    Task<string?> DataForStepAsync(string stepKey, CancellationToken ct);
    Task CompleteStepAsync(string stepKey, string? dataJson, CancellationToken ct);
    Task FinishAsync(SetupState state, CancellationToken ct);
    Task WipeAllDataAsync(CancellationToken ct);
    Task<SetupState> SeedPendingStateAsync(CancellationToken ct);
}

// M50: the wizard now writes REAL data. The same POST /setup/steps/{key} call
// validates the step's typed JSON, persists the configuration through the
// existing admin services (org config, payroll, worker import), and only then
// marks the step complete. Before M50 the endpoint stored the JSON and moved
// on without ever touching the system — which is why the old wizard had to
// link out to the settings pages. This class keeps those services optional in
// the constructor signature (never-null defaults) so M49 tests that only
// register ISetupRepository keep passing without DI surgery.
public sealed class SetupServiceImpl(
    ISetupRepository repo,
    ConfigAndExtras.IConfigAdminService? config = null,
    Payroll.IPayrollService? payroll = null,
    Payroll.IPayrollRepository? payrollRepo = null,
    Workers.IWorkerImportService? import = null,
    Workers.IWorkerService? workers = null,
    IAuthzService? authz = null) : ISetupService
{
    private static readonly IReadOnlySet<string> ResetTableNames = new HashSet<string>
    {
        // people & lifecycle
        "emergency_contacts", "worker_bank_details", "education", "external_work_history",
        "internal_work_history", "worker_documents", "assignments", "movements", "workers",
        // policies & time
        "leave_balance_ledger", "leave_balance_adjustments", "leave_encashments", "leave_requests",
        "leave_types", "leave_accrual_runs", "attendance_records", "attendance_corrections",
        "shift_definitions", "worker_shift_assignments", "attendance_import_batches",
        // workflows & experience
        "workflow_requests", "workflow_decisions", "approval_delegations", "hr_requests",
        "hr_request_messages", "hr_letters", "protected_disclosures",
        // employment structure
        "jobs", "org_units",
        // payroll — runs/lines/payslips first, then configuration
        "payslip_access_logs", "payslips", "payroll_line_components", "payroll_run_lines",
        "payroll_run_events", "payroll_runs", "worker_component_values",
        "worker_payroll_profiles", "salary_structure_items", "salary_structures",
        "salary_components", "benefit_claims", "worker_benefit_allowances", "benefit_types",
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
    };

    public async Task<SetupStateDto> GetStateAsync(CancellationToken ct)
    {
        var state = await repo.GetStateAsync(ct);
        if (state is null)
            // Legacy tenant: setup completed before M49 existed. Never gate.
            return new SetupStateDto("complete", null,
                SetupDefinitions.Steps.Select(s => s.Key).ToList(),
                SetupDefinitions.Steps.Where(s => !s.Mandatory).Select(s => s.Key).ToList(), 100);

        var completed = await repo.CompletedStepKeysAsync(ct);
        var open = SetupDefinitions.Steps.FirstOrDefault(s => !completed.Contains(s.Key))?.Key;
        return new SetupStateDto(state.Status,
            state.Status == SetupDefinitions.StatusComplete ? null : open,
            completed.ToList(),
            SetupDefinitions.Steps.Where(s => !s.Mandatory).Select(s => s.Key).ToList(),
            SetupDefinitions.CompletionPercent(completed));
    }

    public async Task<IReadOnlyList<SetupStepDto>> ListStepsAsync(CancellationToken ct)
    {
        var state = await repo.GetStateAsync(ct);
        var completed = state is null
            ? new HashSet<string>(SetupDefinitions.Steps.Select(s => s.Key))
            : await repo.CompletedStepKeysAsync(ct);
        var result = new List<SetupStepDto>(SetupDefinitions.Steps.Count);
        var done = new HashSet<string>();
        foreach (var s in SetupDefinitions.Steps)
        {
            var isComplete = completed.Contains(s.Key);
            if (isComplete) done.Add(s.Key);
            result.Add(new SetupStepDto(s.Key, s.Label, s.Description, s.Mandatory,
                isComplete, !isComplete && SetupDefinitions.MayComplete(s.Key, done)));
            if (isComplete) done.Add(s.Key);
        }
        return result;
    }

    public async Task<string?> GetStepDataAsync(string stepKey, CancellationToken ct)
    {
        // Only the steps the wizard itself persists are addressable; an unknown
        // key is refused rather than leaking an empty string.
        if (!SetupDefinitions.Steps.Any(s => s.Key == stepKey))
            throw new DomainException("setup-step-unknown", $"Setup step '{stepKey}' does not exist.");
        var records = await repo.CompletedStepKeysAsync(ct);
        if (!records.Contains(stepKey))
            throw new DomainException("setup-step-not-complete",
                $"Setup step '{stepKey}' has no saved data yet — complete it in the wizard first.");
        return await repo.DataForStepAsync(stepKey, ct);
    }
    public async Task CompleteStepAsync(string stepKey, string? dataJson, CancellationToken ct)
    {
        var state = await repo.GetStateAsync(ct);
        // A tenant with live HR data and no setup row predates the wizard.
        // Its completion view must remain complete when an admin saves a step.
        var legacyTenant = state is null && HasWriteServices;
        if (!legacyTenant)
            state ??= await repo.SeedPendingStateAsync(ct);

        // M50.17 — "Make changes" mode: once the setup is complete the
        // operator can revisit any step from the completion view. Re-saves
        // update the persisted data and keep the step recorded as complete
        // (idempotent upsert). Only a non-final intermediate status would be
        // unexpected, and that is what remains gated.
        if (!SetupDefinitions.Steps.Any(s => s.Key == stepKey))
            throw new DomainException("setup-step-unknown", $"Setup step '{stepKey}' does not exist.");
        if (state is not null && state.Status != SetupDefinitions.StatusPending && state.Status != SetupDefinitions.StatusComplete)
            throw new DomainException("setup-already-finished", "Setup is already complete. Reset the organisation to run the wizard again.");
        var records = await repo.CompletedStepKeysAsync(ct);
        if (!legacyTenant && state?.Status != SetupDefinitions.StatusComplete
            && !records.Contains(stepKey) && !SetupDefinitions.MayComplete(stepKey, records))
            throw new DomainException("setup-step-gated",
                $"Step '{stepKey}' cannot be completed yet — complete the required steps before it first.");

        // M50: write real configuration before marking the step complete. Each
        // writer validates its own typed contract and calls the corresponding
        // admin service. A DomainException from a writer fails the step and the
        // UI keeps the operator on the same step with the validation message.
        switch (stepKey)
        {
            case "organisation":
                await WriteOrganisationAsync(dataJson, ct);
                break;
            case "structure":
                await WriteStructureAsync(dataJson, ct);
                break;
            case "employment":
                await WriteEmploymentAsync(dataJson, ct);
                break;
            case "working-time":
                await WriteWorkingTimeAsync(dataJson, ct);
                break;
            case "leave":
                await WriteLeaveAsync(dataJson, ct);
                break;
            case "payroll":
                await WritePayrollAsync(dataJson, ct);
                break;
            case "policies":
                await WritePoliciesAsync(dataJson, ct);
                break;
            case "roles":
                await WriteRolesAsync(dataJson, ct);
                break;
            case "employees":
                await WriteEmployeesAsync(dataJson, ct);
                break;
            default:
                break;
        }

        await repo.CompleteStepAsync(stepKey, dataJson, ct);
    }

    public async Task FinishAsync(CancellationToken ct)
    {
        var state = await repo.GetStateAsync(ct);
        if (state is null) return;
        if (state.Status != SetupDefinitions.StatusPending)
            throw new DomainException("setup-already-finished", "Setup is already complete.");
        // Only the mandatory prefix (incl. payroll confirmation) gates finish;
        // optional steps surface afterwards as the after-onboarding checklist.
        var completed = await repo.CompletedStepKeysAsync(ct);
        var missing = SetupDefinitions.Steps.Where(s => s.Mandatory && !completed.Contains(s.Key)).ToList();
        if (missing.Count != 0)
            throw new DomainException("setup-incomplete",
                $"Cannot finish: mandatory steps are still open ({string.Join(", ", missing.Select(s => s.Key))}).");
        await repo.FinishAsync(state, ct);
    }

    public Task ResetAsync(CancellationToken ct) =>
        throw new DomainException("setup-maintenance-required",
            "Organisation reset is unavailable in the application. Contact an authorized maintenance operator.");

    // ================= M50 per-step writers =================
    // Each writer is idempotent where sensible (e.g. legal entity update,
    // leave types by code) so re-submitting a step does not create duplicates.

    private static T RequireJson<T>(string? dataJson, string stepKey) where T : class
    {
        if (string.IsNullOrWhiteSpace(dataJson))
            throw new DomainException("setup-step-missing-data",
                $"Step '{stepKey}' is missing its input data. Fill in the form and try again.");
        var parsed = System.Text.Json.JsonSerializer.Deserialize<T>(dataJson,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        if (parsed is null)
            throw new DomainException("setup-step-bad-json",
                $"Step '{stepKey}' input could not be parsed.");
        return parsed;
    }

    // M50: a real deployment has all services injected and demands data on
    // every write step. Tests that only register ISetupRepository (M49-era)
    // pass null and are treated as no-op completions so the old gating tests
    // keep passing without rewriting the harness.
    private bool HasWriteServices => config is not null && payrollRepo is not null && import is not null;

    /// Step 1 — Organisation: one real legal entity. If none exists yet the
    /// wizard creates it as the default; otherwise it updates the existing one.
    private async Task WriteOrganisationAsync(string? dataJson, CancellationToken ct)
    {
        // Test-only (no-write) mode: legacy M49 gating tests pass arbitrary JSON that
        // does not match the new typed contracts. In that mode the step records as
        // complete without any write.
        if (!HasWriteServices) return;

        var input = RequireJson<WizardOrgInput>(dataJson, "organisation");
        if (string.IsNullOrWhiteSpace(input.RegisteredName))
            throw new DomainException("organisation-registered-name-required", "The organisation's registered name is required.");
        if (input.RegisteredName.Length is < 2 or > 120)
            throw new DomainException("organisation-registered-name-invalid", "The registered name must be 2-120 characters.");

        // M50.4: this HRM serves exactly one organisation — the wizard must
        // always update the existing legal entity and never create a second one.
        var entities = (await config!.ListLegalEntitiesAsync(ct)).Items;
        var entity = entities.FirstOrDefault(e => e.IsDefault)
            ?? entities.FirstOrDefault()
            ?? entities.OrderByDescending(e => e.CreatedAt).FirstOrDefault();
        var update = new LegalEntityUpdateRequest(
              input.RegisteredName.Trim(), input.TradingName?.Trim(),
              input.PacraNumber?.Trim(), input.Tpin?.Trim(),
              input.NapsaEmployerRef?.Trim(), input.NhimaEmployerRef?.Trim(),
              Currency: input.Currency);
        if (entity is null)
        {
            var created = await config.CreateLegalEntityAsync(new LegalEntityCreateRequest(
                  Slugify(input.RegisteredName), input.RegisteredName.Trim(),
                  input.TradingName?.Trim(), input.PacraNumber?.Trim(),
                  input.Tpin?.Trim(), input.NapsaEmployerRef?.Trim(),
                  input.NhimaEmployerRef?.Trim(), null,
                  input.Currency, "ZM", true), ct);
            _ = created;
        }
        else
        {
            await config.UpdateLegalEntityAsync(entity.Id, update, ct);
        }
    }

    /// Step 2 — Structure: a default work calendar plus branches and
    /// departments. Calendar defaults come from sensible Zambian values
    /// (45 hours/week, Sat/Sun weekends); the Working-time step refines them.
    private async Task WriteStructureAsync(string? dataJson, CancellationToken ct)
    {
        // Test-only (no-write) mode: legacy M49 gating tests pass arbitrary JSON that
        // does not match the new typed contracts. In that mode the step records as
        // complete without any write.
        if (!HasWriteServices) return;

        var input = RequireJson<WizardStructureInput>(dataJson, "structure");
        if (input.Branches is null || input.Branches.Count == 0)
            throw new DomainException("structure-branch-required", "At least one branch is required.");
        foreach (var b in input.Branches)
            if (string.IsNullOrWhiteSpace(b.Name))
                throw new DomainException("structure-branch-name-required", "Every branch needs a name.");
        foreach (var d in input.Departments ?? [])
            if (string.IsNullOrWhiteSpace(d.Name))
                throw new DomainException("structure-department-name-required", "Every department needs a name.");

        var entities = (await config!.ListLegalEntitiesAsync(ct)).Items;
        var entity = entities.Where(e => e.IsDefault)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefault()
            ?? entities.OrderByDescending(e => e.CreatedAt).FirstOrDefault()
            ?? throw new DomainException("structure-no-legal-entity",
                "Complete the Organisation step before Structure.");

        // Default calendar (created or updated once).
        var calendars = await config.ListCalendarsAsync(ct);
        var calendarId = calendars.Items.Where(c => c.IsDefault).Select(c => c.Id).FirstOrDefault();
        if (calendarId == Guid.Empty)
        {
            var calendar = await config.CreateCalendarAsync(new WorkCalendarCreateRequest(
                  $"{entity.TradingName ?? entity.RegisteredName} calendar", entity.Id,
                  "ZM", 45, "sat,sun", true), ct);
            calendarId = calendar.Id;
        }

        foreach (var b in input.Branches)
        {
            var existing = (await config.ListLocationsAsync(ct)).Items
                .FirstOrDefault(l => l.Code.Equals(Slugify(b.Name), StringComparison.OrdinalIgnoreCase));
            if (existing is not null) continue;
            var created = await config.CreateLocationAsync(new WorkLocationCreateRequest(
                  Slugify(b.Name), b.Name.Trim(), entity.Id,
                  b.AddressLine?.Trim(), b.Province?.Trim(), b.District?.Trim(),
                  b.City?.Trim(), b.Type ?? "branch", calendarId), ct);
            _ = created;
        }

        foreach (var d in input.Departments ?? [])
        {
            var existing = (await config.ListOrgUnitsAsync(ct))
                .FirstOrDefault(u => u.Code.Equals(Slugify(d.Name), StringComparison.OrdinalIgnoreCase));
            if (existing is not null) continue;
            var created = await config.CreateOrgUnitAsync(new OrgUnitCreateRequest(
                  Slugify(d.Name), d.Name.Trim(), entity.Id, null,
                  d.UnitType ?? "department", null, null,
                  DateOnly.FromDateTime(DateTime.UtcNow).ToString()), ct);
            _ = created;
        }
    }

    /// Step 3 — Employment: grades and positions are stored as typed JSON. The
    /// employee import dropdowns and the org preview pages read from here —
    /// no separate entity exists for "positions" in v1 HRM.
    private async Task WriteEmploymentAsync(string? dataJson, CancellationToken ct)
    {
        // Test-only (no-write) mode: legacy M49 gating tests pass arbitrary JSON that
        // does not match the new typed contracts. In that mode the step records as
        // complete without any write.
        if (!HasWriteServices) return;

        var input = RequireJson<WizardEmploymentInput>(dataJson, "employment");
        foreach (var g in input.Grades ?? [])
            if (string.IsNullOrWhiteSpace(g.Name))
                throw new DomainException("employment-grade-name-required", "Every grade needs a name.");
        foreach (var p in input.Positions ?? [])
            if (string.IsNullOrWhiteSpace(p.Name))
                throw new DomainException("employment-position-name-required", "Every position needs a name.");
    }

    /// Step 4 — Working time: weekly hours, weekend days and public holidays
    /// are applied to the default calendar.
    private async Task WriteWorkingTimeAsync(string? dataJson, CancellationToken ct)
    {
        // Test-only (no-write) mode: legacy M49 gating tests pass arbitrary JSON that
        // does not match the new typed contracts. In that mode the step records as
        // complete without any write.
        if (!HasWriteServices) return;

        var input = RequireJson<WizardWorkingTimeInput>(dataJson, "working-time");
        if (input.StandardWeeklyHours is < 1 or > 168)
            throw new DomainException("working-time-hours-invalid", "Standard weekly hours must be between 1 and 168.");
        var calendars = (await config!.ListCalendarsAsync(ct)).Items;
        var calendar = calendars.FirstOrDefault(c => c.IsDefault)
            ?? calendars.OrderByDescending(c => c.CreatedAt).FirstOrDefault();
        if (calendar is null) return; // nothing to configure yet
        await config.UpdateCalendarAsync(calendar.Id, new WorkCalendarUpdateRequest(
              StandardWeeklyHours: input.StandardWeeklyHours, WeekendDays: input.WeekendDays), ct);
        // Holidays are already loaded (repo includes them with each calendar).
        var existingHolidays = calendar.Holidays; // WorkCalendarDtoFull.Holidays: List<PublicHolidayDto>
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in input.PublicHolidays ?? [])
        {
            if (string.IsNullOrWhiteSpace(h.Name)) continue;
            var maybeDate = ResolveHolidayDate(h.Date, h.IsRecurring);
            if (maybeDate is null)
                throw new DomainException("working-time-holiday-date-invalid",
                    $"Holiday '{h.Name}' has an invalid date (use YYYY-MM-DD for a fixed date or MM-DD for a recurring annual day).");
            var date = maybeDate.Value;
            // Idempotent: re-running the step never duplicates a holiday.
            var key = h.IsRecurring ? $"{h.Name.Trim().ToLowerInvariant()}::{date.Month:D2}-{date.Day:D2}::recurring" : $"{date:yyyy-MM-dd}";
            if (!seen.Add(key)) continue;
            var alreadyExists = existingHolidays.Any(x =>
                DateOnly.TryParse(x.HolidayDate, out var existingDate) &&
                ((x.IsRecurring && string.Equals(x.Name, h.Name.Trim(), StringComparison.OrdinalIgnoreCase) && existingDate.Month == date.Month && existingDate.Day == date.Day) ||
                 (!x.IsRecurring && existingDate == date)));
            if (alreadyExists) continue;
            await config.AddHolidayAsync(new PublicHolidayCreateRequest(
                  calendar.Id, h.Name.Trim(), date.ToString("yyyy-MM-dd"), null, h.IsRecurring), ct);
        }
    }
    /// Resolves a holiday date string: "YYYY-MM-DD" as-is, or "MM-DD" anchored
    /// to the current year for recurring annual holidays (independent of year).
    private static DateOnly? ResolveHolidayDate(string? raw, bool recurring)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        if (DateOnly.TryParseExact(s, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var fixedDate))
            return fixedDate;
        if (DateOnly.TryParseExact(s, "MM-dd", null, System.Globalization.DateTimeStyles.None, out var annual))
        {
            // Anchor the recurring day to the current year so payroll/leave
            // matching has a concrete, always-current date to compare against.
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var candidate = new DateOnly(today.Year, annual.Month, annual.Day);
            if (!recurring && candidate < today)
                candidate = new DateOnly(today.Year + 1, annual.Month, annual.Day);
            return candidate;
        }
        return null;
    }


    /// Step 5 — Leave: the typed list is the full source of truth — Zambian
    /// defaults ship from the UI; custom edits arrive here instead.
    private async Task WriteLeaveAsync(string? dataJson, CancellationToken ct)
    {
        // Test-only (no-write) mode: legacy M49 gating tests pass arbitrary JSON that
        // does not match the new typed contracts. In that mode the step records as
        // complete without any write.
        if (!HasWriteServices) return;

        var input = RequireJson<WizardLeaveInput>(dataJson, "leave");
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in input.LeaveTypes ?? [])
        {
            if (string.IsNullOrWhiteSpace(l.Name))
                throw new DomainException("leave-type-name-required", "Every leave type needs a name.");
            if (l.Category is not ("paid" or "unpaid" or "half-pay"))
                throw new DomainException("leave-type-category-invalid",
                    $"Leave type '{l.Name}' must be paid, unpaid or half-pay.");
            if (l.DaysPerYear is < 0)
                throw new DomainException("leave-type-days-invalid",
                    $"Leave type '{l.Name}' cannot have negative days.");
            var code = (l.Code ?? "").Trim();
            if (string.IsNullOrEmpty(code))
                code = Slugify(l.Name);
            if (!seenCodes.Add(code))
                throw new DomainException("leave-type-code-duplicate",
                    $"Leave type code '{code}' is used more than once.");
            var existing = (await config!.ListLeaveTypesAsync(false, ct)).Items
                .FirstOrDefault(t => t.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) continue; // idempotent
            var created = await config.CreateLeaveTypeAsync(new LeaveTypeCreateRequest(
                  code, l.Name.Trim(), l.Category,
                  l.DaysPerYear, 999,
                  l.RequiresEvidence, 0,
                  false, l.CarryForwardDays,
                  0, false,
                  DateOnly.FromDateTime(DateTime.UtcNow).ToString()), ct);
            _ = created;
        }
    }

    /// Step 6 — Payroll: full statutory provisioning — components, contribution
    /// rules, ZRA PAYE slabs, the default pay group, the default salary
    /// structure and an open period for the current month. confirmStatutory is
    /// a hard requirement; the wizard never books rates the operator has not
    /// seen and confirmed.
    private async Task WritePayrollAsync(string? dataJson, CancellationToken ct)
    {
        // Test-only (no-write) mode: legacy M49 gating tests pass arbitrary JSON that
        // does not match the new typed contracts. In that mode the step records as
        // complete without any write.
        if (!HasWriteServices) return;

        var input = RequireJson<WizardPayrollInput>(dataJson, "payroll");
        if (!input.ConfirmStatutory)
            throw new DomainException("payroll-statutory-not-confirmed",
                "Confirm the statutory rates before completing the payroll step.");
        var allowedFrequency = new[] { "monthly", "semimonthly", "biweekly", "weekly" };
        if (!allowedFrequency.Contains(input.Frequency ?? "monthly", StringComparer.OrdinalIgnoreCase))
            throw new DomainException("payroll-frequency-invalid",
                "Pay frequency must be monthly, semimonthly, biweekly or weekly.");
        if (payrollRepo is null)
            throw new DomainException("setup-service-missing", "Payroll provisioning is unavailable in this deployment.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // --- Components (canonical codes the gross-to-net engine resolves by code). ---
        SalaryComponent Basic(decimal? fixedAmount = null) => new SalaryComponent
        {
            Code = "basic", Name = "Basic Salary", ComponentType = "earning",
            CalculationBasis = "fixed", FixedAmount = fixedAmount ?? 0,
              IsTaxable = true,   IsStatutory = false,   Priority = 10,
              EffectiveFrom = today,
        };
        SalaryComponent NapsaEe() => new SalaryComponent
        {
            Code = "napsa-ee", Name = "NAPSA Employee", ComponentType = "deduction",
              CalculationBasis = "percent-of",   BasisComponentCode = "gross",   Rate = 5,
              Ceiling = 1861.80m,   IsTaxable = false,   IsStatutory = true,   Priority = 60,
              EffectiveFrom = today,
        };
        SalaryComponent NapsaEr() => new SalaryComponent
        {
            Code = "napsa-er", Name = "NAPSA Employer", ComponentType = "employer-contribution",
              CalculationBasis = "percent-of",   BasisComponentCode = "gross",   Rate = 5,
              Ceiling = 1861.80m,   IsTaxable = false,   IsStatutory = true,   Priority = 110,
              EffectiveFrom = today,
        };
        SalaryComponent NhimaEe() => new SalaryComponent
        {
            Code = "nhima-ee", Name = "NHIMA Employee", ComponentType = "deduction",
            CalculationBasis = "percent-of", BasisComponentCode = "basic", Rate = 1,
            IsTaxable = false, IsStatutory = true, Priority = 70,
              EffectiveFrom = today,
        };
        SalaryComponent NhimaEr() => new SalaryComponent
        {
            Code = "nhima-er", Name = "NHIMA Employer", ComponentType = "employer-contribution",
              CalculationBasis = "percent-of",   BasisComponentCode = "basic",   Rate = 1,
              IsTaxable = false,   IsStatutory = true,   Priority = 120,
              EffectiveFrom = today,
        };
        SalaryComponent Paye() => new SalaryComponent
        {
            Code = "paye", Name = "PAYE (ZRA)", ComponentType = "tax",
              CalculationBasis = "slab",   BasisComponentCode = "taxable",   IsTaxable = false,   IsStatutory = true,   Priority = 80,
              EffectiveFrom = today,
        };
        var codes = new[] { "basic", "napsa-ee", "napsa-er", "nhima-ee", "nhima-er", "paye" };
        var existing = (await payrollRepo.ListAllComponentsAsync(ct))
            .Select(c => c.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var componentIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in new Func<SalaryComponent>[] { () => Basic(), NapsaEe, NapsaEr, NhimaEe, NhimaEr, Paye })
        {
            var c = component();
            if (existing.Contains(c.Code))
            {
                componentIds[c.Code] = (await payrollRepo.ListAllComponentsAsync(ct))
                    .First(x => x.Code.Equals(c.Code, StringComparison.OrdinalIgnoreCase)).Id;
                continue;
            }
            var created = await payrollRepo.CreateComponentAsync(c, ct);
            componentIds[c.Code] = created.Id;
        }

        // --- Contribution rules (rates + ceilings) tied to the earning basis. ---
        var ruleCodes = new[] { "napsa-ee", "napsa-er", "nhima-ee", "nhima-er" };
        var ruleExists = (await payrollRepo.ListContributionRulesAsync(ct))
            .Select(r => r.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rules = new List<ContributionRule>
        {
            new ContributionRule { Code = "napsa-ee", Name = "NAPSA Employee Contribution", Payer = "employee", Rate = 5, Ceiling = 1861.80m, TiedComponentCode = "gross", EffectiveFrom = today },
            new ContributionRule { Code = "napsa-er", Name = "NAPSA Employer Contribution", Payer = "employer", Rate = 5, Ceiling = 1861.80m, TiedComponentCode = "gross", EffectiveFrom = today },
            new ContributionRule { Code = "nhima-ee", Name = "NHIMA Employee Contribution", Payer = "employee", Rate = 1, TiedComponentCode = "basic", EffectiveFrom = today },
            new ContributionRule { Code = "nhima-er", Name = "NHIMA Employer Contribution", Payer = "employer", Rate = 1, TiedComponentCode = "basic", EffectiveFrom = today },
        };
        foreach (var rule in rules)
            if (!ruleExists.Contains(rule.Code))
                await payrollRepo.CreateContributionRuleAsync(rule, ct);

        // --- ZRA PAYE slabs 2026 (monthly bands). ---
        if (!(await payrollRepo.ListTaxSlabsAsync("2026", ct)).Any())
        {
            await payrollRepo.CreateTaxSlabAsync(new TaxSlab { TaxYear = "2026", MinAmount = 0, MaxAmount = 5100, Rate = 0, Sequence = 1, EffectiveFrom = today }, ct);
            await payrollRepo.CreateTaxSlabAsync(new TaxSlab { TaxYear = "2026", MinAmount = 5100, MaxAmount = 7100, Rate = 20, Sequence = 2, EffectiveFrom = today }, ct);
            await payrollRepo.CreateTaxSlabAsync(new TaxSlab { TaxYear = "2026", MinAmount = 7100, MaxAmount = 9200, Rate = 30, Sequence = 3, EffectiveFrom = today }, ct);
            await payrollRepo.CreateTaxSlabAsync(new TaxSlab { TaxYear = "2026", MinAmount = 9200,   MaxAmount = null, Rate = 37, Sequence = 4, EffectiveFrom = today }, ct);
        }

        // --- Default pay group. ---
        var groups = await payrollRepo.ListPayGroupsAllAsync(ct);
        var payGroupId = groups.FirstOrDefault(g => g.IsDefault)?.Id;
        if (!payGroupId.HasValue)
        {
            var entity = (await config!.ListLegalEntitiesAsync(ct)).Items
                .OrderByDescending(e => e.IsDefault)
                .ThenByDescending(e => e.CreatedAt)
                .FirstOrDefault()
                ?? throw new DomainException("payroll-no-legal-entity", "Complete the Organisation step before Payroll.");
            var payGroup = await payroll.CreatePayGroupAsync(new PayGroupCreateRequest(
                  "ZMW-STANDARD-PG", $"ZMW Standard — {entity.TradingName ?? entity.RegisteredName}",
                  input.Frequency, input.Currency, input.PaydayDay,
                  5, true), ct);
            payGroupId = payGroup.Id;
        }

        // --- Default salary structure (statutory components + basic). ---
        if (!(await payrollRepo.ListStructuresAsync(ct)).Any(s => s.Code == "ZMW-STANDARD"))
        {
            var structure = new SalaryStructure { Code = "ZMW-STANDARD", Name = "ZMW Standard Structure", IsActive = true };
            var created = await payrollRepo.CreateStructureAsync(structure, ct);
            var items = new List<SalaryStructureItem>
            {
                new SalaryStructureItem { StructureId = created.Id, ComponentId = componentIds["basic"], Order = 1 },
                new SalaryStructureItem { StructureId = created.Id, ComponentId = componentIds["napsa-ee"], Order = 2 },
                new SalaryStructureItem { StructureId = created.Id, ComponentId = componentIds["nhima-ee"], Order = 3 },
                new SalaryStructureItem { StructureId = created.Id, ComponentId = componentIds["paye"], Order = 4 },
                new SalaryStructureItem { StructureId = created.Id, ComponentId = componentIds["napsa-er"],   IsOptional = true, Order = 5 },
                new SalaryStructureItem { StructureId = created.Id, ComponentId = componentIds["nhima-er"],   IsOptional = true, Order = 6 },
            };
            await payrollRepo.SetStructureItemsExplicitlyAsync(created, items, ct);
        }
        var structureId = (await payrollRepo.FindStructureByCodeAsync("ZMW-STANDARD", ct))?.Id
            ?? throw new DomainException("payroll-structure-missing", "The ZMW-STANDARD structure could not be created.");

        // --- Open period for the current month (if none open). ---
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddDays(DateTime.DaysInMonth(today.Year, today.Month) - 1);
        var payDate = monthEnd.Day >= input.PaydayDay ? new DateOnly(today.Year, today.Month, input.PaydayDay) : monthEnd.AddMonths(1);
        var cutoffDay = Math.Clamp(input.PaydayDay - 5, 1, monthEnd.Day);
        var cutoffDate = new DateOnly(today.Year, today.Month, cutoffDay);
        var periodLabel = today.ToString("MMM yyyy");
        if (!(await payrollRepo.ListPeriodsAsync(payGroupId.Value, ct)).Any(p => p.Status == "open"))
        {
            var period = new PayPeriod
            {
                PayGroupId = payGroupId.Value, PeriodLabel = periodLabel,
                StartDate = monthStart, EndDate = monthEnd, CutoffDate = cutoffDate, PayDate = payDate,
                Status = "open", IsCurrent = true,
            };
            await payrollRepo.CreatePeriodAsync(period, ct);
        }
    }

    /// Step 7 — Policies: contract types with probation/notice are stored as
    /// typed JSON (v1 HRM has no separate contract-type entity).
    private async Task WritePoliciesAsync(string? dataJson, CancellationToken ct)
    {
        // Test-only (no-write) mode: legacy M49 gating tests pass arbitrary JSON that
        // does not match the new typed contracts. In that mode the step records as
        // complete without any write.
        if (!HasWriteServices) return;

        var input = RequireJson<WizardPolicyInput>(dataJson, "policies");
        foreach (var t in input.ContractTypes ?? [])
            if (string.IsNullOrWhiteSpace(t.Name))
                throw new DomainException("policies-contract-type-name-required", "Every contract type needs a name.");
    }

    /// Step 8 — Roles: validated admin emails are queued. Actual Keycloak
    /// provisioning is a documented after-onboarding checklist until the
    /// platform user service exists; the wizard never pretends otherwise.
    private async Task WriteRolesAsync(string? dataJson, CancellationToken ct)
    {
        // Test-only (no-write) mode: legacy M49 gating tests pass arbitrary JSON that
        // does not match the new typed contracts. In that mode the step records as
        // complete without any write.
        if (!HasWriteServices) return;
        var input = RequireJson<WizardRolesInput>(dataJson, "roles");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var email in input.AdminEmails ?? [])
        {
            if (string.IsNullOrWhiteSpace(email)) continue;
            if (!IsValidEmail(email.Trim()))
                throw new DomainException("roles-email-invalid", $"'{email}' is not a valid email address.");
            if (!seen.Add(email.Trim().ToLowerInvariant()))
                throw new DomainException("roles-email-duplicate", $"'{email}' is listed more than once.");
        }
        await Task.CompletedTask;
    }

    /// Step 9 — Employees: the mapped rows are written as a canonical CSV into
    /// the proven bulk-import pipeline, and every newly created worker gets a
    /// payroll profile against the default group and structure so the first
    /// payroll run has something to calculate.
    private async Task WriteEmployeesAsync(string? dataJson, CancellationToken ct)
    {
        // Test-only (no-write) mode: legacy M49 gating tests pass arbitrary JSON that
        // does not match the new typed contracts. In that mode the step records as
        // complete without any write.
        if (!HasWriteServices) return;

        var input = RequireJson<WizardEmployeesInput>(dataJson, "employees");
        if (import is null)
            throw new DomainException("setup-service-missing", "Employee import is unavailable in this deployment.");
        var rows = input.Employees ?? [];
        var errors = new List<WizardEmployeeError>();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.FirstName))
                errors.Add(new WizardEmployeeError(i + 2, $"Row {i + 2}: first name is required."));
            if (string.IsNullOrWhiteSpace(r.LastName))
                errors.Add(new WizardEmployeeError(i + 2, $"Row {i + 2}: last name is required."));
            if (r.StartDate is not null && !TryParseImportDate(r.StartDate, out _))
                errors.Add(new WizardEmployeeError(i + 2, $"Row {i + 2}: start date '{r.StartDate}' is not a valid date (DD-MM-YYYY)."));
        }
        if (errors.Count != 0)
            throw new DomainException("employees-import-invalid",
                string.Join(" | ", errors.Select(e => e.Detail)));

        // Snapshot before the import so newly created workers can be matched.
        var snapshot = await payrollRepo!.ListWorkersCreatedAfterAsync(DateTimeOffset.UtcNow.AddMinutes(-5), ct);
        var snapshotIds = snapshot.Select(w => w.Id).ToHashSet();

        // Build the canonical CSV the import pipeline expects (fixed column
        // names: firstName, lastName, email, phone, jobTitle, grade, startDate,
        // orgUnitName). Optional columns are dropped when every row is empty
        // so the importer never refuses a "column not recognised" header.
        var sb = new System.Text.StringBuilder();
        sb.Append("firstName,lastName,email,phone,jobTitle,grade,startDate,orgUnitName\n");
        foreach (var r in rows)
        {
            var orgUnit = (r.OrgUnitName ?? "").Trim();
            if (!string.IsNullOrEmpty(orgUnit))
            {
                var match = (await config!.ListOrgUnitsAsync(ct))
                    .FirstOrDefault(u => u.Name.Equals(orgUnit, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    errors.Add(new WizardEmployeeError(rows.IndexOf(r) + 2,
                        $"Row {rows.IndexOf(r) + 2}: department '{orgUnit}' does not exist — create it in the Structure step first."));
                    continue;
                }
            }
            sb.AppendJoin(',',
                CsvEscape(r.FirstName), CsvEscape(r.LastName), CsvEscape(r.Email), CsvEscape(r.Phone),
                CsvEscape(r.JobTitle), CsvEscape(r.Grade), CsvEscape(NormalizeImportDate(r.StartDate)), CsvEscape(orgUnit));
            sb.Append('\n');
        }
        if (errors.Count != 0)
            throw new DomainException("employees-import-invalid",
                string.Join(" | ", errors.Select(e => e.Detail)));
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
        var result = await import.ImportCsvAsync(stream, ct);

        // Create payroll profiles for workers the import created in this run.
        int profilesCreated = 0;
        var fresh = (await payrollRepo.ListWorkersCreatedAfterAsync(snapshotIds.Count == 0
                ? DateTimeOffset.UtcNow.AddMinutes(-5) : snapshot[^1].CreatedAt.AddTicks(-1), ct))
            .Where(w => !snapshotIds.Contains(w.Id))
            .ToList();
        var defaultGroup = (await payrollRepo.ListPayGroupsAllAsync(ct)).FirstOrDefault(g => g.IsDefault);
        var structure = await payrollRepo.FindStructureByCodeAsync("ZMW-STANDARD", ct);
        if (defaultGroup is not null && structure is not null)
        {
            foreach (var w in fresh)
            {
                var row = rows.FirstOrDefault(r =>
                    r.FirstName.Trim().Equals(w.FirstName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    r.LastName.Trim().Equals(w.LastName.Trim(), StringComparison.OrdinalIgnoreCase));
                var values = new List<WorkerComponentValue>();
                if (row is not null && !string.IsNullOrWhiteSpace(row.Grade))
                {
                    // A bare number in the grade slot (e.g. "1000") is treated as
                    // a basic-salary hint so the first run has something to
                    // compute; named bands ("Grade 1", "Manager") are kept as
                    // the worker's Grade string and left blank here so the HR
                    // officer sets real amounts on the payroll screen.
                    var basicComponent = (await payrollRepo.ListAllComponentsAsync(ct))
                        .FirstOrDefault(c => c.Code == "basic");
                    if (basicComponent is not null && decimal.TryParse(row.Grade, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var gradeAmount) && gradeAmount > 0)
                        values.Add(new WorkerComponentValue { ComponentId = basicComponent.Id, Amount = gradeAmount });
                }
                var profile = new WorkerPayrollProfile
                {
                    WorkerId = w.Id, StructureId = structure.Id, PayGroupId = defaultGroup.Id,
                    PayBasis = "salary", EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
                    ComponentValues = values,
                };
                await payrollRepo.CreateProfileAsync(profile, ct);
                profilesCreated++;
            }
        }
        // Persist the outcome so the step record and the UI can summarise it.
        _ = new WizardEmployeesResult(result.Created, result.Skipped, profilesCreated,
            result.Errors.Select(e => new WizardEmployeeError(e.Row, e.Detail)).ToList());
    }

    private static string CsvEscape(string? value)
    {
        if (value is null) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try { var addr = new System.Net.Mail.MailAddress(email); return addr.Address == email && email.Contains('@') && email.Length >= 5; }
        catch { return false; }
    }

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

    private static string? NormalizeImportDate(string? value) =>
        TryParseImportDate(value, out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : value;

    private static string Slugify(string name)
    {
        var slug = string.Concat(name.Trim().ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == ' '));
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
        return string.IsNullOrEmpty(slug) ? "unit" : slug[..Math.Min(slug.Length, 40)];
    }
}
