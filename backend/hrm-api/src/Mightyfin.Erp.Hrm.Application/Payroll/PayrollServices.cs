using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Domain.Entities;

[assembly: InternalsVisibleTo("Mightyfin.Erp.Hrm.Tests")]

namespace Mightyfin.Erp.Hrm.Application.Payroll;

/// <summary>J-groups 01-23: Payroll setup reads and the run lifecycle.
/// Calculation is intentionally synchronous per-run-line for v1 (the calculation
/// job table preserves the boundary for a future worker/queue extraction).</summary>
public interface IPayrollService
{
    // Setup reads
    Task<List<SalaryComponentDto>> ListComponentsAsync(string? type, CancellationToken ct);
    Task<List<PayGroupDto>> ListPayGroupsAsync(CancellationToken ct);
    Task<List<PayPeriodDto>> ListPeriodsAsync(Guid payGroupId, CancellationToken ct);
    Task<PayPeriodDto> CreateHistoricalPeriodAsync(HistoricalPayPeriodCreateRequest request, CancellationToken ct);
    Task<List<TaxSlabDto>> ListTaxSlabsAsync(string taxYear, CancellationToken ct);
    Task<List<ContributionRuleDto>> ListContributionRulesAsync(CancellationToken ct);

    // M20: payroll setup admin (write surface)
    Task<List<PayGroupFullDto>> ListPayGroupsFullAsync(CancellationToken ct);
    // M50: wizard provisions the first pay group (no create endpoint existed before).
    Task<PayGroupFullDto> CreatePayGroupAsync(PayGroupCreateRequest request, CancellationToken ct);
    Task<PayGroupFullDto> UpdatePayGroupAsync(Guid id, PayGroupUpdateRequest request, CancellationToken ct);
    Task<TaxSlabDto> UpdateTaxSlabAsync(Guid id, TaxSlabUpdateRequest request, CancellationToken ct);
    Task<ContributionRuleDto> UpdateContributionRuleAsync(Guid id, ContributionRuleUpdateRequest request, CancellationToken ct);
    Task<SalaryComponentDto> CreateSalaryComponentAsync(SalaryComponentCreateRequest request, CancellationToken ct);
    Task<SalaryComponentDto> UpdateSalaryComponentAsync(Guid id, SalaryComponentUpdateRequest request, CancellationToken ct);
    // M21: salary structures admin (which components + default amounts ship with a structure)
    Task<List<SalaryStructureDto>> ListStructuresAsync(CancellationToken ct);
    Task<SalaryStructureDto> GetStructureAsync(Guid id, CancellationToken ct);
    Task<SalaryStructureDto> CreateStructureAsync(SalaryStructureCreateRequest request, CancellationToken ct);
    Task<SalaryStructureDto> UpdateStructureAsync(Guid id, SalaryStructureUpdateRequest request, CancellationToken ct);
    // M5 setup: worker payroll profiles (basic salary + allowances per worker)
    Task<List<WorkerPayrollProfileDto>> ListProfilesAsync(Guid? workerId, CancellationToken ct);
    Task<WorkerPayrollProfileDto> UpsertProfileAsync(Guid workerId, WorkerPayrollProfileCreate request, CancellationToken ct);
    // M41 Gap 3: pay-basis control (salary | timesheet) per worker profile
    Task<WorkerPayrollProfileDto> SetPayBasisAsync(Guid workerId, PayBasisUpdateRequest request, CancellationToken ct);
    Task<WorkerPayrollProfileDto> SetOvertimePolicyAsync(Guid workerId, OvertimePolicyUpdateRequest request, CancellationToken ct);

    // Salary advances: issued by HR/payroll and optionally recovered through payslip deductions.
    Task<List<SalaryAdvanceDto>> ListSalaryAdvancesAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<SalaryAdvanceDto> CreateSalaryAdvanceAsync(SalaryAdvanceCreateRequest request, CancellationToken ct, string actorSubjectId = "system");
    Task<SalaryAdvanceDto> UpdateSalaryAdvanceAsync(Guid id, SalaryAdvanceUpdateRequest request, CancellationToken ct);
    Task<SalaryAdvanceDto> CancelSalaryAdvanceAsync(Guid id, SalaryAdvanceCancelRequest request, CancellationToken ct, string actorSubjectId = "system");

    // Run lifecycle
    Task<Paged<PayrollRunDto>> ListRunsAsync(CancellationToken ct);
    // M48: the top-HR approval queue — in-review branch runs with their
    // control totals, branch name, and the moment each run was submitted.
    Task<List<PayrollQueueItemDto>> ListPayrollQueueAsync(CancellationToken ct);
    Task<PayrollRunPreflightDto> GetRunPreflightAsync(PayrollRunCreate request, CancellationToken ct);
    Task<PayrollRunDto> CreateRunAsync(PayrollRunCreate request, CancellationToken ct, string actorSubjectId = "system");
    Task<PayrollRunDto> UpdateRunAsync(Guid id, PayrollRunUpdate request, CancellationToken ct, string actorSubjectId = "system");
    Task<PayrollRunDto> GetRunAsync(Guid id, CancellationToken ct);
    Task<PayrollCalculationReadinessDto> GetCalculationReadinessAsync(Guid id, CancellationToken ct);
    Task<PayrollRunDto> LockRunAsync(Guid id, CancellationToken ct, string actorSubjectId = "system");
    Task<PayrollRunDto> CalculateRunAsync(Guid id, CancellationToken ct, string actorSubjectId = "system");
    Task<WorkerPayslipPreviewDto> PreviewWorkerPayslipAsync(Guid workerId, CancellationToken ct);
    Task<Paged<PayrollRunLineDto>> GetRunLinesAsync(Guid id, CancellationToken ct);
    Task<PayrollRunDto> DecideExceptionAsync(Guid id, Guid lineId, PayrollExceptionDecisionRequest request, CancellationToken ct, string actorSubjectId = "system");
    Task<PayrollRunDto> ApplyCorrectionAsync(Guid id, Guid lineId, PayrollCorrectionRequest request, CancellationToken ct, string actorSubjectId = "system");
    Task<PayrollRunDto> ApplyReleasedCorrectionAsync(Guid id, Guid lineId, PayrollCorrectionRequest request, CancellationToken ct, string actorSubjectId = "system");
    Task<PayrollRunDto> ApproveRunAsync(Guid id, string? note, CancellationToken ct, string actorSubjectId = "system");
    // M46: branch payroll draft (in-review) workflow — the preparer sends the
    // calculated branch run up for top-HR approval; it then appears on the
    // approver's queue. draft | calculated -> in-review, branch run only.
    Task<PayrollRunDto> SubmitRunAsync(Guid id, CancellationToken ct, string actorSubjectId = "system");
    Task<PayrollRunDto> ReleaseRunAsync(Guid id, CancellationToken ct, string actorSubjectId = "system");

    // Top-admin mistake control before release: void the run without deleting its audit/data.
    Task<PayrollRunDto> CancelRunAsync(Guid id, PayrollRunReverseCreate request, CancellationToken ct, string actorSubjectId = "system");

    // M6: reversal of a released/closed run (audit-preserving — never deletes history)
    Task<PayrollRunDto> ReverseRunAsync(Guid id, PayrollRunReverseCreate request, CancellationToken ct, string actorSubjectId = "system");

    // M27: bank-file workflow, reconciliation, and run-scoped audit history.
    Task<PayrollRunDto> GeneratePaymentFileAsync(Guid id, CancellationToken ct, string actorSubjectId = "system");
    Task<PayrollPaymentReadinessDto> GetPaymentReadinessAsync(Guid id, CancellationToken ct);
    Task<string> DownloadPaymentFileAsync(Guid id, CancellationToken ct);
    Task<PayrollRunDto> ApprovePaymentFileAsync(Guid id, PayrollPaymentApprovalRequest request, CancellationToken ct, string actorSubjectId = "system");
    Task<PayrollRunDto> ReleasePaymentFileAsync(Guid id, CancellationToken ct, string actorSubjectId = "system");
    Task<PayrollRunDto> ReconcileRunAsync(Guid id, PayrollReconciliationRequest request, CancellationToken ct, string actorSubjectId = "system");
    Task<List<PayrollRunEventDto>> GetRunAuditAsync(Guid id, CancellationToken ct);
    Task<string> ExportRunAuditAsync(Guid id, CancellationToken ct);

    // M6: YTD-aware payslip document generation
    Task<PayslipDto> GeneratePayslipDocumentAsync(Guid payslipId, CancellationToken ct);

    // M6: statutory employer liability report for a period (ZRA PAYE, NAPSA, NHIMA)
    Task<EmployerLiabilityReportDto> EmployerLiabilityReportAsync(Guid payPeriodId, CancellationToken ct);

    // callerSubject: the token subject from the route layer — when supplied,
    // an employee-only caller is restricted to their own payslips (M25).
    Task<Paged<PayslipDto>> GetPayslipsAsync(Guid workerId, string? callerSubject = null, CancellationToken ct = default);
    Task<PayslipDto?> GetPayslipByIdAsync(Guid id, string? callerSubject = null, CancellationToken ct = default);

    // M25 self-service: the signed-in worker's own payslips, keyed on the
    // token subject — an employee can never reach another worker's slips.
    Task<Paged<PayslipDto>> GetMyPayslipsAsync(string subjectId, CancellationToken ct);
    Task<PayslipDto?> GetMyPayslipByIdAsync(Guid id, string subjectId, CancellationToken ct);
    Task<string> GetMyPayslipDownloadUrlAsync(Guid id, string subjectId, CancellationToken ct);
    Task<byte[]> GetMyPayslipPreviewAsync(Guid id, string subjectId, CancellationToken ct);

    // M24: statutory identity readiness per run — hard gate on release.
    Task<StatutoryReadinessDto> GetRunStatutoryReadinessAsync(Guid id, CancellationToken ct);

    // M34: admin payslip list per run (real IDs for navigation), bulk PDF
    // generation, and raw PDF preview bytes.
    Task<List<PayslipDto>> ListRunPayslipsAsync(Guid runId, CancellationToken ct);
    Task<List<PayslipDto>> GenerateAllPayslipDocumentsAsync(Guid runId, CancellationToken ct);
    Task<byte[]> GetPayslipPreviewAsync(Guid payslipId, CancellationToken ct);
}

public sealed record SalaryComponentDto(Guid Id, string Code, string Name, string ComponentType,
    string CalculationBasis, string? BasisComponentCode, decimal? Rate, decimal? FixedAmount,
    decimal? Ceiling, bool IsTaxable, bool IsStatutory, int Priority, int Version, bool IsActive);
public sealed record PayPeriodDto(Guid Id, string PeriodLabel, string StartDate, string EndDate, string CutoffDate, string PayDate, string Status, bool IsHistorical = false);
public sealed record HistoricalPayPeriodCreateRequest(Guid PayGroupId, string PeriodLabel, string StartDate,
    string EndDate, string CutoffDate, string PayDate, string Reason);
public sealed record TaxSlabDto(Guid Id, string TaxYear, decimal MinAmount, decimal? MaxAmount, decimal Rate, int Sequence);
public sealed record ContributionRuleDto(Guid Id, string Code, string Name, string Payer, decimal Rate, decimal? Ceiling, decimal? Floor);
public sealed record WorkerPayslipPreviewDto(string Status, string PeriodLabel, string Currency,
    List<string> Guardrails, PayrollRunLineDto? Line);
public sealed record SalaryAdvanceDto(Guid Id, Guid WorkerId, string WorkerName, string? EmployeeNo,
    decimal Amount, decimal InstallmentAmount, decimal RecoveredAmount, decimal RemainingAmount,
    string Currency, string IssueDate, string DeductionStartDate, bool DeductFromPayslip,
    string Status, string? Reason, string? Reference, DateTimeOffset CreatedAt);
public sealed record SalaryAdvanceCreateRequest(Guid WorkerId, decimal Amount, decimal InstallmentAmount,
    string? Currency, string IssueDate, string DeductionStartDate, bool DeductFromPayslip,
    string? Reason, string? Reference);
public sealed record SalaryAdvanceUpdateRequest(decimal? InstallmentAmount = null, bool? DeductFromPayslip = null,
    string? DeductionStartDate = null, string? Reason = null, string? Reference = null);
public sealed record SalaryAdvanceCancelRequest(string Reason);

// ---------- M20: payroll setup admin (write surface) ----------
public sealed record PayGroupUpdateRequest(string? Code = null, string? Name = null,
    string? Frequency = null, string? Currency = null, int? CalendarDayOfMonth = null,
    int? InputCutoffDaysBeforePayday = null, bool? IsDefault = null);
public sealed record TaxSlabUpdateRequest(decimal? Rate = null, decimal? MaxAmount = null);
public sealed record ContributionRuleUpdateRequest(
    decimal? Rate = null,
    decimal? Ceiling = null,
    decimal? Floor = null,
    bool CeilingSpecified = false,
    bool FloorSpecified = false);
public sealed record SalaryComponentCreateRequest(
    string Code,
    string Name,
    string ComponentType,
    string CalculationBasis,
    string? BasisComponentCode = null,
    decimal? Rate = null,
    decimal? FixedAmount = null,
    decimal? Ceiling = null,
    bool IsTaxable = true,
    int Priority = 100);
public sealed record SalaryComponentUpdateRequest(
    string? Name = null,
    string? CalculationBasis = null,
    string? BasisComponentCode = null,
    decimal? Rate = null,
    decimal? FixedAmount = null,
    decimal? Ceiling = null,
    bool? IsTaxable = null,
    bool? IsArchived = null,
    bool RateSpecified = false,
    bool FixedAmountSpecified = false,
    bool CeilingSpecified = false);
// ---------- M20 DTO extras ----------
public sealed record PayGroupFullDto(Guid Id, string Code, string Name, string Frequency, string Currency,
    int CalendarDayOfMonth, int InputCutoffDaysBeforePayday, bool IsDefault, string Status);

public sealed class PayrollServiceImpl(IPayrollRepository repo, IAuthzService authz,
    IPayslipDocumentService payslipDocument,
    Application.ShellContext? scope = null,
    IOutboxWriter? outbox = null,
    IUnitOfWork? unitOfWork = null) : IPayrollService
{
    public async Task<List<SalaryComponentDto>> ListComponentsAsync(string? type, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var items = await repo.ListComponentsAsync(type, ct);
        return items.Select(MapComponent).ToList();
    }

    public async Task<List<PayGroupDto>> ListPayGroupsAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var items = await repo.ListPayGroupsAsync(ct);
        return items.Select(g => new PayGroupDto(g.Id, g.Code, g.Name, g.Frequency, g.Currency, g.CalendarDayOfMonth)).ToList();
    }

    public async Task<List<PayPeriodDto>> ListPeriodsAsync(Guid payGroupId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var items = await repo.ListPeriodsAsync(payGroupId, ct);
        return items.Select(p => new PayPeriodDto(p.Id, p.PeriodLabel, p.StartDate.ToString(), p.EndDate.ToString(), p.CutoffDate.ToString(), p.PayDate.ToString(), p.Status, p.IsHistorical)).ToList();
    }

    public async Task<PayPeriodDto> CreateHistoricalPeriodAsync(HistoricalPayPeriodCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin", "payroll");
        var group = await repo.GetPayGroupAsync(request.PayGroupId, ct)
            ?? throw new DomainException("pay-group-not-found", "Choose an existing pay group.");
        if (!DateOnly.TryParse(request.StartDate, out var start) || !DateOnly.TryParse(request.EndDate, out var end)
            || !DateOnly.TryParse(request.CutoffDate, out var cutoff) || !DateOnly.TryParse(request.PayDate, out var payDate))
            throw new DomainException("historical-period-date-invalid", "Enter valid historical period, cutoff and pay dates.");
        var reason = (request.Reason ?? "").Trim();
        if (reason.Length < 10) throw new DomainException("historical-period-reason-required", "Explain the historical entry in at least 10 characters.");
        if (start > end || cutoff > payDate || end >= DateOnly.FromDateTime(DateTime.UtcNow))
            throw new DomainException("historical-period-invalid", "A historical period must end before today and have a cutoff on or before its pay date.");
        if ((request.PeriodLabel ?? "").Trim().Length < 3)
            throw new DomainException("historical-period-label-required", "Provide a clear period label, for example June 2026.");
        var existing = await repo.ListPeriodsAsync(group.Id, ct);
        if (existing.Any(p => p.StartDate == start && p.EndDate == end))
            throw new DomainException("historical-period-duplicate", "A pay period already covers those dates for this pay group.");
        var created = await repo.CreatePeriodAsync(new PayPeriod { PayGroupId = group.Id, PeriodLabel = request.PeriodLabel.Trim(), StartDate = start, EndDate = end, CutoffDate = cutoff, PayDate = payDate, Status = "historical", IsHistorical = true, HistoricalReason = reason, IsCurrent = false }, ct);
        return new PayPeriodDto(created.Id, created.PeriodLabel, created.StartDate.ToString(), created.EndDate.ToString(), created.CutoffDate.ToString(), created.PayDate.ToString(), created.Status, true);
    }

    public async Task<List<TaxSlabDto>> ListTaxSlabsAsync(string taxYear, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var items = await repo.ListTaxSlabsAsync(taxYear, ct);
        return items.Select(s => new TaxSlabDto(s.Id, s.TaxYear, s.MinAmount, s.MaxAmount, s.Rate, s.Sequence)).ToList();
    }

    // ---------- M20: payroll setup admin ----------
    public async Task<List<PayGroupFullDto>> ListPayGroupsFullAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var items = await repo.ListPayGroupsAllAsync(ct);
        return items.Select(g => new PayGroupFullDto(g.Id, g.Code, g.Name, g.Frequency, g.Currency,
            g.CalendarDayOfMonth, g.InputCutoffDaysBeforePayday, g.IsDefault,
            g.IsArchived ? "archived" : "active")).ToList();
    }

    /// <summary>M50: the setup wizard provisions the organisation's first pay
    /// group. Before this milestone no create surface existed — groups could
    /// only be updated — so the wizard could never complete the payroll chain.
    /// Creating a group as the default silently demotes any existing default.</summary>
    public async Task<PayGroupFullDto> CreatePayGroupAsync(PayGroupCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var code = (request.Code ?? "").Trim();
        if (code.Length is < 2 or > 24)
            throw new DomainException("pay-group-code-invalid", "Pay group code must be 2-24 characters.");
        var groups = await repo.ListPayGroupsAllAsync(ct);
        if (groups.Any(g => g.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException("pay-group-code-duplicate", $"A pay group with code {code} already exists.");
        var name = (request.Name ?? "").Trim();
        if (name.Length is < 2 or > 60)
            throw new DomainException("pay-group-name-invalid", "Pay group name must be 2-60 characters.");
        if (request.CalendarDayOfMonth is < 1 or > 31)
            throw new DomainException("pay-group-payday-invalid", "Payday (day of month) must be between 1 and 31.");
        var group = new PayGroup
        {
            Code = code, Name = name, Frequency = request.Frequency, Currency = request.Currency,
            CalendarDayOfMonth = request.CalendarDayOfMonth,
            InputCutoffDaysBeforePayday = request.InputCutoffDaysBeforePayday,
            IsDefault = request.IsDefault,
        };
        if (group.IsDefault)
            await repo.UnsetDefaultPayGroupsAsync(ct, group.Id);
        await repo.CreatePayGroupAsync(group, ct);
        return new PayGroupFullDto(group.Id, group.Code, group.Name, group.Frequency, group.Currency,
            group.CalendarDayOfMonth, group.InputCutoffDaysBeforePayday, group.IsDefault, "active");
    }

    public async Task<PayGroupFullDto> UpdatePayGroupAsync(Guid id, PayGroupUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var group = await repo.GetPayGroupAsync(id, ct)
            ?? throw new DomainException("pay-group-not-found", $"Pay group {id} does not exist.");
        if (request.Code is not null) group.Code = request.Code.Trim();
        if (request.Name is not null) group.Name = request.Name.Trim();
        if (request.Frequency is not null) group.Frequency = request.Frequency;
        if (request.Currency is not null) group.Currency = request.Currency;
        if (request.CalendarDayOfMonth is not null) group.CalendarDayOfMonth = request.CalendarDayOfMonth.Value;
        if (request.InputCutoffDaysBeforePayday is not null) group.InputCutoffDaysBeforePayday = request.InputCutoffDaysBeforePayday.Value;
        if (request.IsDefault is not null && request.IsDefault == true) await repo.UnsetDefaultPayGroupsAsync(ct, id);
        if (request.IsDefault is not null) group.IsDefault = request.IsDefault.Value;
        if (group.IsArchived)
            throw new DomainException("pay-group-archived", "An archived pay group cannot be changed. Create a new group instead.");
        await repo.UpdatePayGroupAsync(group, ct);
        return new PayGroupFullDto(group.Id, group.Code, group.Name, group.Frequency, group.Currency,
            group.CalendarDayOfMonth, group.InputCutoffDaysBeforePayday, group.IsDefault,
            group.IsArchived ? "archived" : "active");
    }

    public async Task<TaxSlabDto> UpdateTaxSlabAsync(Guid id, TaxSlabUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var slab = await repo.GetTaxSlabAsync(id, ct)
            ?? throw new DomainException("tax-slab-not-found", $"Tax slab {id} does not exist.");
        if (slab.IsArchived)
            throw new DomainException("tax-slab-archived", "An archived slab cannot be changed. Create a new slab version instead.");
        if (request.Rate is not null)
        {
            if (request.Rate.Value < 0 || request.Rate.Value > 100)
                throw new DomainException("tax-slab-rate-out-of-range", "Slab rate must be between 0 and 100 percent.");
            slab.Rate = request.Rate.Value;
        }
        if (request.MaxAmount is not null) slab.MaxAmount = request.MaxAmount;
        await repo.UpdateTaxSlabAsync(slab, ct);
        return new TaxSlabDto(slab.Id, slab.TaxYear, slab.MinAmount, slab.MaxAmount, slab.Rate, slab.Sequence);
    }

    public async Task<ContributionRuleDto> UpdateContributionRuleAsync(Guid id, ContributionRuleUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var rule = await repo.GetContributionRuleAsync(id, ct)
            ?? throw new DomainException("contribution-rule-not-found", $"Contribution rule {id} does not exist.");
        if (rule.IsArchived)
            throw new DomainException("contribution-rule-archived", "An archived rule cannot be changed. Create a new rule version instead.");
        if (request.Rate is not null)
        {
            if (request.Rate.Value < 0 || request.Rate.Value > 100)
                throw new DomainException("contribution-rate-out-of-range", "Contribution rate must be between 0 and 100 percent.");
            rule.Rate = request.Rate.Value;
        }
        if (request.CeilingSpecified) rule.Ceiling = request.Ceiling;
        else if (request.Ceiling is not null) rule.Ceiling = request.Ceiling;
        if (request.FloorSpecified) rule.Floor = request.Floor;
        else if (request.Floor is not null) rule.Floor = request.Floor;
        await repo.UpdateContributionRuleAsync(rule, ct);
        return new ContributionRuleDto(rule.Id, rule.Code, rule.Name, rule.Payer, rule.Rate, rule.Ceiling, rule.Floor);
    }

    public async Task<SalaryComponentDto> UpdateSalaryComponentAsync(Guid id, SalaryComponentUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var comp = await repo.GetComponentByIdAsync(id, ct)
            ?? throw new DomainException("salary-component-not-found", $"Salary component {id} does not exist.");
        if (request.IsArchived == true)
        {
            comp.IsArchived = true;
            comp.IsActive = false;
            await repo.UpdateComponentAsync(comp, ct);
        }
        else
        {
            if (request.Name is not null) comp.Name = request.Name.Trim();
            if (request.CalculationBasis is not null) comp.CalculationBasis = request.CalculationBasis;
            if (request.BasisComponentCode is not null) comp.BasisComponentCode = request.BasisComponentCode;
            if (request.RateSpecified) comp.Rate = request.Rate;
            else if (request.Rate is not null) comp.Rate = request.Rate;
            if (request.FixedAmountSpecified) comp.FixedAmount = request.FixedAmount;
            else if (request.FixedAmount is not null) comp.FixedAmount = request.FixedAmount;
            if (request.CeilingSpecified) comp.Ceiling = request.Ceiling;
            else if (request.Ceiling is not null) comp.Ceiling = request.Ceiling;
            if (request.IsTaxable is not null) comp.IsTaxable = request.IsTaxable.Value;
            if (request.IsArchived == false)
            {
                comp.IsArchived = false;
                comp.IsActive = true;
            }
            await repo.UpdateComponentAsync(comp, ct);
        }
        return MapComponent(comp);
    }

    public async Task<SalaryComponentDto> CreateSalaryComponentAsync(SalaryComponentCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var code = (request.Code ?? "").Trim().ToLowerInvariant();
        if (code.Length is < 2 or > 40 || code.Any(ch => !(char.IsLower(ch) || char.IsDigit(ch) || ch == '-')))
            throw new DomainException("salary-component-code-invalid", "Component code must be 2-40 lowercase letters, numbers or hyphens.");
        var name = (request.Name ?? "").Trim();
        if (name.Length is < 2 or > 80)
            throw new DomainException("salary-component-name-invalid", "Component name must be 2-80 characters.");
        var allowedTypes = new[] { "earning", "deduction", "employer-contribution", "tax" };
        if (!allowedTypes.Contains(request.ComponentType, StringComparer.OrdinalIgnoreCase))
            throw new DomainException("salary-component-type-invalid", "Component type must be earning, deduction, employer-contribution or tax.");
        var allowedBases = new[] { "fixed", "percent-of", "slab" };
        if (!allowedBases.Contains(request.CalculationBasis, StringComparer.OrdinalIgnoreCase))
            throw new DomainException("salary-component-basis-invalid", "Calculation basis must be fixed, percent-of or slab.");
        if (request.Priority is < 1 or > 1000)
            throw new DomainException("salary-component-priority-invalid", "Evaluation priority must be between 1 and 1000.");
        if (request.FixedAmount is < 0 || request.Ceiling is < 0)
            throw new DomainException("salary-component-amount-invalid", "Fixed amount and ceiling cannot be negative.");

        var allComponents = await repo.ListAllComponentsAsync(ct);
        if (allComponents.Any(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException("salary-component-code-duplicate", $"A salary component with code {code} already exists.");

        var calculationBasis = request.CalculationBasis.ToLowerInvariant();
        var basisCode = string.IsNullOrWhiteSpace(request.BasisComponentCode)
            ? null
            : request.BasisComponentCode.Trim().ToLowerInvariant();
        if (calculationBasis == "percent-of")
        {
            if (basisCode is null)
                throw new DomainException("salary-component-basis-required", "Choose the component this percentage is calculated from.");
            if (basisCode == code)
                throw new DomainException("salary-component-basis-circular", "A component cannot be calculated from itself.");
            if (basisCode is not ("gross" or "taxable") && !allComponents.Any(c => c.Code.Equals(basisCode, StringComparison.OrdinalIgnoreCase)))
                throw new DomainException("salary-component-basis-not-found", $"Basis component {basisCode} does not exist.");
            if (request.Rate is null or <= 0 or > 100)
                throw new DomainException("salary-component-rate-invalid", "Percentage rate must be greater than 0 and no more than 100.");
        }

        var component = new SalaryComponent
        {
            Code = code,
            Name = name,
            ComponentType = request.ComponentType.ToLowerInvariant(),
            CalculationBasis = calculationBasis,
            BasisComponentCode = calculationBasis is "percent-of" or "slab" ? basisCode : null,
            Rate = calculationBasis == "percent-of" ? request.Rate : null,
            FixedAmount = calculationBasis == "fixed" ? request.FixedAmount : null,
            Ceiling = request.Ceiling,
            IsTaxable = request.IsTaxable,
            IsStatutory = false,
            Priority = request.Priority,
            Version = 1,
            IsActive = true,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        await repo.CreateComponentAsync(component, ct);
        return MapComponent(component);
    }

    private static SalaryComponentDto MapComponent(SalaryComponent comp) =>
        new(comp.Id, comp.Code, comp.Name, comp.ComponentType, comp.CalculationBasis,
            comp.BasisComponentCode, comp.Rate, comp.FixedAmount, comp.Ceiling,
            comp.IsTaxable, comp.IsStatutory, comp.Priority, comp.Version, comp.IsActive);

    // ---------- M21: salary structure administration ----------
    public async Task<List<SalaryStructureDto>> ListStructuresAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var items = await repo.ListStructuresAsync(ct);
        return items.Select(MapStructure).ToList();
    }

    public async Task<SalaryStructureDto> GetStructureAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var structure = await repo.GetStructureAsync(id, ct)
            ?? throw new DomainException("salary-structure-not-found", $"Salary structure {id} does not exist.");
        return MapStructure(structure);
    }

    public async Task<SalaryStructureDto> CreateStructureAsync(SalaryStructureCreateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var code = (request.Code ?? "").Trim();
        if (code.Length is < 2 or > 24)
            throw new DomainException("structure-code-invalid", "Structure code must be 2-24 characters.");
        if (!string.Equals(code, code.ToUpperInvariant(), StringComparison.Ordinal))
            throw new DomainException("structure-code-invalid", "Structure code must be uppercase.");
        var existing = await repo.FindStructureByCodeAsync(code, ct);
        if (existing is not null)
            throw new DomainException("structure-code-duplicate", $"A structure with code {code} already exists.");
        var structure = new SalaryStructure { Code = code, Name = (request.Name ?? "").Trim(), Version = 1, IsActive = true };
        await repo.CreateStructureAsync(structure, ct);
        await SetStructureItemsAsync(structure, request.Items, ct);
        return MapStructure(await repo.GetStructureAsync(structure.Id, ct) ?? structure);
    }

    public async Task<SalaryStructureDto> UpdateStructureAsync(Guid id, SalaryStructureUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        var structure = await repo.GetStructureAsync(id, ct)
            ?? throw new DomainException("salary-structure-not-found", $"Salary structure {id} does not exist.");
        if (structure.Code == "ZMW-STANDARD" && request.IsActive == false)
            throw new DomainException("default-structure-protected",
                "The ZMW-STANDARD structure is the payroll default and cannot be deactivated.");
        if (request.Name is not null) structure.Name = request.Name.Trim();
        if (request.IsActive is not null) structure.IsActive = request.IsActive.Value;
        await repo.UpdateStructureAsync(structure, ct);
        if (request.Items is not null) await SetStructureItemsAsync(structure, request.Items, ct);
        return MapStructure(await repo.GetStructureAsync(structure.Id, ct) ?? structure);
    }

    private async Task SetStructureItemsAsync(SalaryStructure structure,
        List<SalaryStructureItemUpsert> items, CancellationToken ct)
    {
        var seen = new HashSet<Guid>();
        foreach (var item in items)
        {
            if (!seen.Add(item.ComponentId))
                throw new DomainException("structure-item-duplicate",
                    $"Component {item.ComponentId} is listed twice in the structure.");
            var comp = await repo.GetComponentByIdAsync(item.ComponentId, ct)
                ?? throw new DomainException("component-not-found",
                    $"Component {item.ComponentId} does not exist and cannot be added to the structure.");
            if (comp.IsArchived)
                throw new DomainException("structure-item-archived",
                    $"Component {comp.Code} is archived and cannot be part of a structure.");
        }
        await repo.ClearStructureItemsAsync(structure.Id, ct);
        int order = 0;
        var newItems = new List<SalaryStructureItem>();
        foreach (var item in items)
        {
            var comp = await repo.GetComponentByIdAsync(item.ComponentId, ct);
            newItems.Add(new SalaryStructureItem
            {
                StructureId = structure.Id,
                ComponentId = item.ComponentId,
                DefaultAmount = item.DefaultAmount,
                IsOptional = item.IsOptional ?? comp!.ComponentType != "earning",
                Order = item.Order ?? order++,
            });
        }
        // EF Core 10 + SQLite Guid-V7: insert children via explicit AddRange in
        // a separate SaveChanges phase — navigation-based insert after the
        // parent was saved throws a spurious concurrency exception.
        await repo.SetStructureItemsExplicitlyAsync(structure, newItems, ct);
    }

    private static SalaryStructureDto MapStructure(SalaryStructure s) => new(
        s.Id, s.Code, s.Name, s.Version, s.IsActive,
        s.Items.Select(i => new SalaryStructureItemDto(i.Id, i.ComponentId,
            i.Component?.Code ?? "", i.Component?.Name ?? "",
            i.DefaultAmount, i.IsOptional, i.Order)).OrderBy(i => i.Order).ToList());

    public async Task<List<ContributionRuleDto>> ListContributionRulesAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var items = await repo.ListContributionRulesAsync(ct);
        return items.Select(r => new ContributionRuleDto(r.Id, r.Code, r.Name, r.Payer, r.Rate, r.Ceiling, r.Floor)).ToList();
    }

    public async Task<Paged<PayrollRunDto>> ListRunsAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var items = await repo.ListRunsAsync(ct);
        // M44 branch scoping: scoped operators see only their branch's runs.
        if ((scope?.IsScopedToBranch ?? false))
            items = items.Where(r => r.LocationId == scope?.LocationId || r.LocationId == null).ToList();
        return new Paged<PayrollRunDto>(items.Select(MapRun).ToList(), items.Count, 1, 100);
    }

    // M48: the top-HR payroll approval queue. Only org-wide (non-confined)
    // HR can review branch runs — confined branch HR get a plain 403 here.
    // Each row carries the branch name (resolved once, for the whole result)
    // and the exact moment the preparer submitted the run for review (the
    // "submitted-for-review" audit event), so the approver sees how long a
    // run has been waiting and can sanity-check the control totals at a glance.
    public async Task<List<PayrollQueueItemDto>> ListPayrollQueueAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        if (scope is not null && scope.IsConfined)
            throw new DomainException("payroll-queue-confined",
                "The payroll approval queue is for organisation-wide HR only. Your account is confined to a branch — branch payroll runs must be submitted up to top HR for approval.");
        // The repository resolves branch names, the parent legal entity, and the
        // submitted-at stamp in one pass — the service keeps the confinement guard.
        var rows = await repo.ListRunsInReviewAsync(ct);
        return rows.Select(row => new PayrollQueueItemDto(
            row.Run.Id, row.Run.Status, row.Run.PayPeriod?.PeriodLabel ?? "",
            row.Run.LocationId, row.BranchName,
            string.IsNullOrEmpty(row.LegalEntityId) ? Guid.Empty : Guid.Parse(row.LegalEntityId),
            row.Run.EmployeeCount, row.Run.TotalGross, row.Run.TotalNet, row.Run.TotalDeductions, row.Run.TotalEmployerCost,
            row.Run.ExceptionCount, row.Run.PreparedBySubjectId,
            row.SubmittedAt,
            row.Run.CreatedAt)).ToList();
    }

    public async Task<PayrollRunPreflightDto> GetRunPreflightAsync(PayrollRunCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        return await BuildRunPreflightAsync(request, ct);
    }

    public async Task<PayrollRunDto> CreateRunAsync(PayrollRunCreate request, CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var preflight = await BuildRunPreflightAsync(request, ct);
        var failures = preflight.Checks.Where(c => c.State == "fail").ToList();
        if (failures.Count > 0)
            throw new DomainException("payroll-run-preflight-failed",
                "Payroll run preflight failed: " + string.Join("; ", failures.Select(f => f.Label)));
        var period = await repo.GetPeriodAsync(request.PayPeriodId, ct)
            ?? throw new DomainException("pay-period-not-found", $"Pay period {request.PayPeriodId} does not exist.");
        if (request.IsHistorical != period.IsHistorical)
            throw new DomainException("payroll-history-mode-mismatch", request.IsHistorical
                ? "Choose a historical period when recording a backdated payroll run."
                : "This is a historical period. Start it using Backdated payroll mode so normal cutoffs stay unchanged.");
        if ((!request.IsHistorical && period.Status != "open") || (request.IsHistorical && period.Status != "historical"))
            throw new DomainException("pay-period-not-open", $"Pay period {period.PeriodLabel} is {period.Status} and cannot accept this type of run.");
        var historicalReason = (request.HistoricalReason ?? "").Trim();
        if (request.IsHistorical && historicalReason.Length < 10)
            throw new DomainException("historical-run-reason-required", "Explain the backdated payroll entry in at least 10 characters.");
        // M46 branch payroll drafts: a draft tagged with LocationId belongs to
        // one branch and pays only that branch's workers; multiple branch runs
        // may coexist for a period (one per branch), but an organisation-wide
        // run may not exist while any branch run for the period is open.
        var targetLocation = (scope?.IsScopedToBranch ?? false) ? scope?.LocationId : null;
        if (targetLocation.HasValue)
        {
            var sameBranch = await repo.FindOpenRunByPeriodAndLocationAsync(request.PayPeriodId, targetLocation, ct);
            if (sameBranch is not null)
                throw new DomainException("run-already-exists",
                    $"A payroll run already exists for this period at branch {targetLocation}.");
        }
        else
        {
            var orgOpen = await repo.FindRunByPeriodAsync(request.PayPeriodId, ct);
            if (orgOpen is not null)
                throw new DomainException("run-already-exists", "A payroll run already exists for this period.");
            var branchOpen = await repo.FindOpenBranchRunForPeriodAsync(request.PayPeriodId, ct);
            if (branchOpen is not null)
                throw new DomainException("branch-run-open",
                    "A branch payroll draft is open for this period. Resolve it first — an organisation-wide run cannot run alongside a branch draft, otherwise workers would be paid twice.");
        }
        // M44 branch scoping: a run created while scoped to a branch is that branch's run (draft flows up).
        var run = new PayrollRun { PayPeriodId = request.PayPeriodId, PayGroupId = request.PayGroupId,
            Status = "draft", CalcVersion = "engine-v1", PreparedBySubjectId = actorSubjectId,
            LocationId = targetLocation, IsHistorical = request.IsHistorical,
            HistoricalReason = request.IsHistorical ? historicalReason : null };
        var created = await repo.CreateRunAsync(run, ct);
        await RecordEventAsync(created, "created", actorSubjectId, null, "draft", null, null, ct);
        return MapRun(created);
    }

    public async Task<PayrollRunDto> UpdateRunAsync(Guid id, PayrollRunUpdate request, CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct)
            ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        if (run.Status != "draft")
            throw new DomainException("payroll-run-not-draft",
                $"Run is in status {run.Status}; only draft payroll runs can change pay group or period.");

        if ((scope?.IsScopedToBranch ?? false) && run.LocationId != scope?.LocationId)
            throw new DomainException("payroll-run-scope-denied",
                "This payroll run is outside your current branch scope.");

        var group = await repo.GetPayGroupAsync(request.PayGroupId, ct)
            ?? throw new DomainException("pay-group-not-found", $"Pay group {request.PayGroupId} does not exist.");
        var period = await repo.GetPeriodAsync(request.PayPeriodId, ct)
            ?? throw new DomainException("pay-period-not-found", $"Pay period {request.PayPeriodId} does not exist.");
        if (period.PayGroupId != group.Id)
            throw new DomainException("pay-period-group-mismatch",
                $"Pay period {period.PeriodLabel} does not belong to pay group {group.Name}.");
        if (period.Status != "open")
            throw new DomainException("pay-period-not-open",
                $"Pay period {period.PeriodLabel} is {period.Status} and cannot be used by a draft run.");

        var targetLocation = run.LocationId;
        if (targetLocation.HasValue)
        {
            var sameBranch = await repo.FindOpenRunByPeriodAndLocationAsync(request.PayPeriodId, targetLocation, ct);
            if (sameBranch is not null && sameBranch.Id != run.Id)
                throw new DomainException("run-already-exists",
                    $"Another payroll run already exists for this period at branch {targetLocation}.");
        }
        else
        {
            var orgOpen = await repo.FindRunByPeriodAsync(request.PayPeriodId, ct);
            if (orgOpen is not null && orgOpen.Id != run.Id)
                throw new DomainException("run-already-exists", "Another payroll run already exists for this period.");
            var branchOpen = await repo.FindOpenBranchRunForPeriodAsync(request.PayPeriodId, ct);
            if (branchOpen is not null && branchOpen.Id != run.Id)
                throw new DomainException("branch-run-open",
                    "A branch payroll draft is open for this period. Resolve it first before moving this organisation-wide run there.");
        }

        var oldPeriodId = run.PayPeriodId;
        var oldPayGroupId = run.PayGroupId;
        run.PayPeriodId = request.PayPeriodId;
        run.PayGroupId = request.PayGroupId;
        run.PayPeriod = period;
        run.ApprovalNote = string.IsNullOrWhiteSpace(request.ApprovalNote) ? null : request.ApprovalNote.Trim();
        await repo.UpdateRunAsync(run, ct);
        await RecordEventAsync(run, "draft-updated", actorSubjectId, "draft", "draft", run.ApprovalNote,
            new { oldPeriodId, oldPayGroupId, run.PayPeriodId, run.PayGroupId }, ct);
        return MapRun(run);
    }

    private async Task<PayrollRunPreflightDto> BuildRunPreflightAsync(PayrollRunCreate request, CancellationToken ct)
    {
        var checks = new List<PayrollRunPreflightCheckDto>();
        var period = await repo.GetPeriodAsync(request.PayPeriodId, ct);
        var group = await repo.GetPayGroupAsync(request.PayGroupId, ct);
        var targetLocation = (scope?.IsScopedToBranch ?? false) ? scope?.LocationId : null;

        if (period is null)
        {
            checks.Add(new("period-exists", "Selected pay period exists", "fail",
                $"Pay period {request.PayPeriodId} does not exist.", 0));
            return new PayrollRunPreflightDto(request.PayPeriodId, request.PayGroupId, targetLocation,
                false, 0, 0, checks);
        }
        if (group is null)
        {
            checks.Add(new("pay-group-exists", "Selected pay group exists", "fail",
                $"Pay group {request.PayGroupId} does not exist.", 0));
        }
        var periodAvailable = request.IsHistorical ? period.IsHistorical && period.Status == "historical" : !period.IsHistorical && period.Status == "open";
        checks.Add(new("period-open", request.IsHistorical ? "Selected historical period is available" : "Selected pay period is open",
            periodAvailable ? "pass" : "fail",
            request.IsHistorical ? $"{period.PeriodLabel} is a protected historical period." : $"{period.PeriodLabel} is {period.Status}.", 0));
        if (request.IsHistorical)
            checks.Add(new("history-reason", "Historical entry is explained",
                (request.HistoricalReason ?? "").Trim().Length >= 10 ? "pass" : "fail",
                "Historical runs preserve normal deadlines and never generate a bank payment file.", 0));
        checks.Add(new("period-pay-group", "Pay period belongs to selected pay group",
            period.PayGroupId == request.PayGroupId ? "pass" : "fail",
            period.PayGroupId == request.PayGroupId
                ? "The selected period and pay group match."
                : "Choose a period generated for this pay group.",
            0));

        var existingIssue = await FindRunBlockingIssueAsync(request.PayPeriodId, targetLocation, ct);
        checks.Add(new("no-open-run", "No conflicting open payroll run",
            existingIssue is null ? "pass" : "fail",
            existingIssue ?? "No open run conflicts with this scope and period.", 0));

        var inputs = await repo.LoadCalculationInputsAsync(request.PayPeriodId, ct, targetLocation);
        var profiles = inputs.Profiles.Where(p => p.PayGroupId == request.PayGroupId).ToList();
        var duplicateWorkers = profiles.GroupBy(p => p.WorkerId).Where(g => g.Count() > 1).ToList();
        var missingBank = profiles.Count(p =>
            p.Worker?.BankDetails is null ||
            !p.Worker.BankDetails.Any(b => b.IsPrimary && !string.IsNullOrWhiteSpace(b.AccountNumber)));
        var missingStatutory = profiles.Count(p =>
            p.Worker is null ||
            string.IsNullOrWhiteSpace(p.Worker.Nrc) ||
            string.IsNullOrWhiteSpace(p.Worker.Tpin) ||
            string.IsNullOrWhiteSpace(p.Worker.NapsaNumber) ||
            string.IsNullOrWhiteSpace(p.Worker.NhimaNumber));

        checks.Add(new("population", "Workers have payroll profiles",
            profiles.Count > 0 ? "pass" : targetLocation.HasValue ? "warn" : "fail",
            profiles.Count > 0
                ? $"{profiles.Count} worker{(profiles.Count == 1 ? "" : "s")} will be included."
                : targetLocation.HasValue
                    ? "No active payroll profiles were found for this branch scope yet."
                    : "No active payroll profiles were found for this pay group and organisation scope.",
            profiles.Count));
        checks.Add(new("duplicates", "No duplicate pay profiles in this group",
            duplicateWorkers.Count == 0 ? "pass" : "warn",
            duplicateWorkers.Count == 0
                ? "No worker appears more than once in this pay group."
                : $"{duplicateWorkers.Count} worker{(duplicateWorkers.Count == 1 ? "" : "s")} have more than one active profile.",
            duplicateWorkers.Count));
        checks.Add(new("bank", "Bank details present",
            missingBank == 0 ? "pass" : "warn",
            missingBank == 0
                ? "Every included worker has a primary bank account."
                : $"{missingBank} included worker{(missingBank == 1 ? "" : "s")} are missing primary bank details.",
            missingBank));
        checks.Add(new("statutory", "Statutory identity pack present",
            missingStatutory == 0 ? "pass" : "warn",
            missingStatutory == 0
                ? "Every included worker has NRC, TPIN, NAPSA and NHIMA values."
                : $"{missingStatutory} included worker{(missingStatutory == 1 ? "" : "s")} are missing NRC, TPIN, NAPSA or NHIMA values.",
            missingStatutory));
        checks.Add(new("dates", "Cutoff is before pay date",
            period.CutoffDate <= period.PayDate ? "pass" : "fail",
            period.CutoffDate <= period.PayDate
                ? $"Time cutoff {period.CutoffDate:yyyy-MM-dd} is before pay date {period.PayDate:yyyy-MM-dd}."
                : $"Time cutoff {period.CutoffDate:yyyy-MM-dd} is after pay date {period.PayDate:yyyy-MM-dd}.",
            0));

        return new PayrollRunPreflightDto(request.PayPeriodId, request.PayGroupId, targetLocation,
            checks.All(c => c.State != "fail"), profiles.Count, checks.Count(c => c.State == "warn"), checks);
    }

    private async Task<string?> FindRunBlockingIssueAsync(Guid payPeriodId, Guid? targetLocation, CancellationToken ct)
    {
        if (targetLocation.HasValue)
        {
            var sameBranch = await repo.FindOpenRunByPeriodAndLocationAsync(payPeriodId, targetLocation, ct);
            return sameBranch is null ? null : $"A payroll run already exists for this period at branch {targetLocation}.";
        }
        var orgOpen = await repo.FindRunByPeriodAsync(payPeriodId, ct);
        if (orgOpen is not null) return "A payroll run already exists for this period.";
        var branchOpen = await repo.FindOpenBranchRunForPeriodAsync(payPeriodId, ct);
        return branchOpen is null
            ? null
            : "A branch payroll draft is open for this period. Resolve it before opening an organisation-wide run.";
    }

    /// <summary>Gross-to-net engine: applies active components in priority order
    /// per enrolled worker profile, taxes via progressive slab lookup, caps
    /// statutory contributions at ceilings, and records explainable line
    /// components with a pinned rule-version snapshot.</summary>
    public async Task<List<WorkerPayrollProfileDto>> ListProfilesAsync(Guid? workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var profiles = await repo.ListProfilesAsync(workerId, ct);
        return profiles.Select(MapProfile).ToList();
    }

    public async Task<WorkerPayrollProfileDto> UpsertProfileAsync(Guid workerId, WorkerPayrollProfileCreate request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var worker = await repo.GetWorkerAsync(workerId, ct) ?? throw new DomainException("worker-not-found", $"Worker {workerId} does not exist.");
        var group = await repo.GetPayGroupAsync(request.PayGroupId, ct)
            ?? throw new DomainException("pay-group-not-found", "Pay group does not exist.");
        if (!DateOnly.TryParse(request.EffectiveFrom, out var effective))
            throw new DomainException("bad-date", "EffectiveFrom must be a valid date (yyyy-MM-dd).");
        var allComponents = await repo.ListAllComponentsAsync(ct);
        var normalizedValues = new List<WorkerComponentValueCreate>();
        foreach (var v in request.Values)
        {
            var comp = await repo.GetComponentByIdAsync(v.ComponentId, ct);
            if (comp is null && !string.IsNullOrWhiteSpace(v.ComponentCode))
                comp = allComponents.FirstOrDefault(c => c.Code.Equals(v.ComponentCode, StringComparison.OrdinalIgnoreCase));
            if (comp is null)
                throw new DomainException("component-not-found", $"Component {v.ComponentCode ?? v.ComponentId.ToString()} does not exist.");
            normalizedValues.Add(new WorkerComponentValueCreate(comp.Id, comp.Code, v.Amount));
        }
        request = request with { Values = normalizedValues };
        var hasOvertimePayload = request.OvertimeCategory is not null
            || request.WeeklyOvertimeThresholdHours.HasValue
            || request.MonthlyOvertimeDivisor.HasValue;
        var overtime = NormalizeOvertimePolicy(request.OvertimeCategory, request.WeeklyOvertimeThresholdHours, request.MonthlyOvertimeDivisor);

        var defaultStructure = await repo.FindStructureAsync("ZMW-STANDARD", ct);

        var existing = await repo.FindOpenProfileAsync(workerId, ct);
        WorkerPayrollProfile profile;
        if (existing is null)
        {
            profile = new WorkerPayrollProfile
            {
                WorkerId = workerId, PayGroupId = request.PayGroupId, EffectiveFrom = effective,
                StructureId = defaultStructure?.Id ?? Guid.Empty,
                PayBasis = request.PayBasis ?? "salary",
                OvertimeCategory = overtime.Category,
                WeeklyOvertimeThresholdHours = overtime.WeeklyThreshold,
                MonthlyOvertimeDivisor = overtime.MonthlyDivisor,
            };
            await repo.CreateProfileAsync(profile, ct);
        }
        else
        {
            existing.PayGroupId = request.PayGroupId;
            existing.PayBasis = request.PayBasis ?? existing.PayBasis;
            if (hasOvertimePayload)
            {
                existing.OvertimeCategory = overtime.Category;
                existing.WeeklyOvertimeThresholdHours = overtime.WeeklyThreshold;
                existing.MonthlyOvertimeDivisor = overtime.MonthlyDivisor;
            }
            profile = existing;
        }
        await repo.DeleteProfileValuesAsync(profile.Id, ct);
        foreach (var v in request.Values)
            profile.ComponentValues.Add(new WorkerComponentValue { ComponentId = v.ComponentId, Amount = v.Amount });
        await repo.UpdateProfileAsync(profile, ct);
        return MapProfile(await repo.FindOpenProfileAsync(workerId, ct) ?? profile);
    }

    /// <summary>M41 Gap 3: pay-basis control. HR marks whether a worker would be
    /// paid on the salary basis (default) or timesheet basis. Timesheet-driven
    /// pay is not implemented yet — runs always calculate salary-basis; the flag
    /// is a planning control and surfaces in the profile UI.</summary>
    public async Task<WorkerPayrollProfileDto> SetPayBasisAsync(Guid workerId, PayBasisUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var basis = (request.PayBasis ?? "").Trim().ToLowerInvariant();
        if (basis != "salary" && basis != "timesheet")
            throw new DomainException("bad-pay-basis", "PayBasis must be 'salary' or 'timesheet'.");
        var profile = await repo.FindOpenProfileAsync(workerId, ct);
        if (profile is null)
            throw new DomainException("payroll-profile-not-found", "This worker has no open payroll profile. Set up the profile first.");
        profile.PayBasis = basis;
        await repo.UpdateProfileAsync(profile, ct);
        return MapProfile(profile);
    }

    public async Task<WorkerPayrollProfileDto> SetOvertimePolicyAsync(Guid workerId, OvertimePolicyUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var profile = await repo.FindOpenProfileAsync(workerId, ct);
        if (profile is null)
            throw new DomainException("payroll-profile-not-found", "This worker has no open payroll profile. Set up the profile first.");
        var overtime = NormalizeOvertimePolicy(request.OvertimeCategory, request.WeeklyOvertimeThresholdHours, request.MonthlyOvertimeDivisor);
        profile.OvertimeCategory = overtime.Category;
        profile.WeeklyOvertimeThresholdHours = overtime.WeeklyThreshold;
        profile.MonthlyOvertimeDivisor = overtime.MonthlyDivisor;
        await repo.UpdateProfileAsync(profile, ct);
        return MapProfile(profile);
    }

    public async Task<List<SalaryAdvanceDto>> ListSalaryAdvancesAsync(Guid? workerId, string? status, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var requestedStatus = status?.Trim().ToLowerInvariant();
        var advances = await repo.ListSalaryAdvancesAsync(workerId, requestedStatus == "settled" ? null : requestedStatus, ct);
        var recovered = await repo.GetSalaryAdvanceRecoveredAmountsAsync(advances.Select(a => a.Id).ToList(), ct);
        var mapped = advances.Select(a => MapAdvance(a, recovered.TryGetValue(a.Id, out var paid) ? paid : 0m)).ToList();
        return string.IsNullOrWhiteSpace(requestedStatus) || requestedStatus == "all"
            ? mapped
            : mapped.Where(a => string.Equals(a.Status, requestedStatus, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<SalaryAdvanceDto> CreateSalaryAdvanceAsync(SalaryAdvanceCreateRequest request, CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var worker = await repo.GetWorkerAsync(request.WorkerId, ct)
            ?? throw new DomainException("worker-not-found", "Employee not found.");
        if (request.Amount <= 0)
            throw new DomainException("salary-advance-invalid", "Advance amount must be greater than zero.");
        if (request.InstallmentAmount <= 0)
            throw new DomainException("salary-advance-invalid", "Deduction amount must be greater than zero.");
        if (request.InstallmentAmount > request.Amount)
            throw new DomainException("salary-advance-invalid", "Deduction amount cannot be more than the advance amount.");
        if (!DateOnly.TryParse(request.IssueDate, out var issueDate))
            throw new DomainException("bad-date", "Issue date must be a valid date.");
        if (!DateOnly.TryParse(request.DeductionStartDate, out var startDate))
            throw new DomainException("bad-date", "Deduction start date must be a valid date.");
        var advance = new SalaryAdvance
        {
            WorkerId = worker.Id,
            Amount = Math.Round(request.Amount, 2),
            InstallmentAmount = Math.Round(request.InstallmentAmount, 2),
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "ZMW" : request.Currency.Trim().ToUpperInvariant(),
            IssueDate = issueDate,
            DeductionStartDate = startDate,
            DeductFromPayslip = request.DeductFromPayslip,
            Status = "active",
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
            CreatedBySubjectId = actorSubjectId,
        };
        await repo.CreateSalaryAdvanceAsync(advance, ct);
        advance.Worker = worker;
        return MapAdvance(advance, 0m);
    }

    public async Task<SalaryAdvanceDto> UpdateSalaryAdvanceAsync(Guid id, SalaryAdvanceUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin", "payroll");
        var advance = await repo.GetSalaryAdvanceAsync(id, ct)
            ?? throw new DomainException("salary-advance-not-found", "Salary advance not found.");
        if (advance.Status != "active")
            throw new DomainException("salary-advance-closed", "Only active salary advances can be changed.");
        var recovered = (await repo.GetSalaryAdvanceRecoveredAmountsAsync([advance.Id], ct))
            .GetValueOrDefault(advance.Id);
        if (request.InstallmentAmount.HasValue)
        {
            if (request.InstallmentAmount.Value <= 0)
                throw new DomainException("salary-advance-invalid", "Deduction amount must be greater than zero.");
            if (request.InstallmentAmount.Value > advance.Amount - recovered + 0.0001m)
                throw new DomainException("salary-advance-invalid", "Deduction amount cannot be more than the remaining advance balance.");
            advance.InstallmentAmount = Math.Round(request.InstallmentAmount.Value, 2);
        }
        if (request.DeductFromPayslip.HasValue) advance.DeductFromPayslip = request.DeductFromPayslip.Value;
        if (request.DeductionStartDate is not null)
        {
            if (!DateOnly.TryParse(request.DeductionStartDate, out var startDate))
                throw new DomainException("bad-date", "Deduction start date must be a valid date.");
            advance.DeductionStartDate = startDate;
        }
        if (request.Reason is not null) advance.Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (request.Reference is not null) advance.Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();
        await repo.UpdateSalaryAdvanceAsync(advance, ct);
        return MapAdvance(advance, recovered);
    }

    public async Task<SalaryAdvanceDto> CancelSalaryAdvanceAsync(Guid id, SalaryAdvanceCancelRequest request, CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("hr_admin", "payroll");
        var advance = await repo.GetSalaryAdvanceAsync(id, ct)
            ?? throw new DomainException("salary-advance-not-found", "Salary advance not found.");
        if (advance.Status != "active")
            throw new DomainException("salary-advance-closed", "Only active salary advances can be cancelled.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new DomainException("salary-advance-cancel-reason", "Cancellation reason is required.");
        var recovered = (await repo.GetSalaryAdvanceRecoveredAmountsAsync([advance.Id], ct))
            .GetValueOrDefault(advance.Id);
        advance.Status = "cancelled";
        advance.DeductFromPayslip = false;
        advance.CancelledBySubjectId = actorSubjectId;
        advance.CancelledAt = DateTimeOffset.UtcNow;
        advance.CancellationReason = request.Reason.Trim();
        await repo.UpdateSalaryAdvanceAsync(advance, ct);
        return MapAdvance(advance, recovered);
    }

    private static (string Category, decimal WeeklyThreshold, decimal MonthlyDivisor) NormalizeOvertimePolicy(
        string? category, decimal? weeklyThreshold, decimal? monthlyDivisor)
    {
        var normalized = (category ?? "ordinary").Trim().ToLowerInvariant();
        if (normalized is "watchperson" or "guard" or "watchperson_guard" or "watchperson-guard")
            normalized = "watchperson-guard";
        if (normalized != "ordinary" && normalized != "watchperson-guard")
            throw new DomainException("bad-overtime-category", "Overtime category must be ordinary or watchperson-guard.");
        var defaultWeekly = normalized == "watchperson-guard" ? 60m : 48m;
        var defaultDivisor = normalized == "watchperson-guard" ? 240m : 208m;
        var weekly = weeklyThreshold ?? defaultWeekly;
        var divisor = monthlyDivisor ?? defaultDivisor;
        if (weekly <= 0 || divisor <= 0)
            throw new DomainException("bad-overtime-policy", "Weekly overtime threshold and monthly divisor must be greater than zero.");
        return (normalized, weekly, divisor);
    }

    /// <summary>Locks the run for editing (freeze inputs before calculation).
    /// Segregation of duties: only draft runs can be locked; calculate then
    /// proceeds from locked.</summary>
    public async Task<PayrollRunDto> LockRunAsync(Guid id, CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        if (run.Status != "draft")
            throw new DomainException("run-not-lockable", $"Run is in status {run.Status}; only draft runs can be locked.");
        var readiness = await BuildCalculationReadinessAsync(run, ct);
        if (!readiness.Ready)
            throw new DomainException("payroll-calculation-readiness-failed",
                "Payroll calculation readiness failed: " + string.Join("; ", readiness.Checks.Where(c => c.State == "fail").Select(c => c.Label)));
        run.Status = "locked";
        run.LockedBySubjectId = actorSubjectId;
        await repo.UpdateRunAsync(run, ct);
        await RecordEventAsync(run, "inputs-locked", actorSubjectId, "draft", "locked", null, null, ct);
        return MapRun(run);
    }

    public async Task<PayrollCalculationReadinessDto> GetCalculationReadinessAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct)
            ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        return await BuildCalculationReadinessAsync(run, ct);
    }

    private async Task<PayrollCalculationReadinessDto> BuildCalculationReadinessAsync(PayrollRun run, CancellationToken ct)
    {
        var period = run.PayPeriod ?? await repo.GetPeriodAsync(run.PayPeriodId, ct)
            ?? throw new DomainException("pay-period-not-found", $"Pay period {run.PayPeriodId} does not exist.");
        var (profilesRaw, components, rules, slabs, _) = await repo.LoadCalculationInputsAsync(run.PayPeriodId, ct, run.LocationId);
        var profiles = profilesRaw.Where(p => p.PayGroupId == run.PayGroupId).ToList();
        var prorationInputs = await repo.LoadProrationInputsAsync(run.PayPeriodId, ct);
        var approvedOvertime = await repo.LoadApprovedOvertimeAsync(run.PayPeriodId, run.LocationId, ct);
        var issues = new List<PayrollCalculationReadinessIssueDto>();
        var checks = new List<PayrollCalculationReadinessCheckDto>();

        var activeComponents = components.Where(c => c.IsActive).ToList();
        var earningComponents = activeComponents.Where(c => c.ComponentType == "earning").ToList();
        var basicComponent = activeComponents.FirstOrDefault(c => c.Code.Equals("basic", StringComparison.OrdinalIgnoreCase));
        var missingBankProfiles = profiles.Where(p =>
            p.Worker?.BankDetails is null ||
            !p.Worker.BankDetails.Any(b => b.IsPrimary && !string.IsNullOrWhiteSpace(b.AccountNumber))).ToList();
        foreach (var profile in profiles)
        {
            var worker = profile.Worker;
            if (worker is null)
            {
                issues.Add(new(profile.WorkerId, "", "Unknown worker", "Payroll profile is not linked to a worker record.", "fail"));
                continue;
            }
            if (basicComponent is null || !profile.ComponentValues.Any(v => v.ComponentId == basicComponent.Id && v.Amount > 0))
                issues.Add(new(worker.Id, worker.EmployeeNo, worker.FullName, "Basic salary is missing or zero on the payroll profile.", "fail"));
            if (profile.PayBasis.Equals("timesheet", StringComparison.OrdinalIgnoreCase))
                issues.Add(new(worker.Id, worker.EmployeeNo, worker.FullName, "Timesheet pay basis is marked on this profile, but timesheet payroll is not implemented yet.", "warn"));
        }

        checks.Add(new("run-status", "Run can still be calculated",
            run.Status is "draft" or "locked" or "calculated" ? "pass" : "fail",
            run.Status is "draft" or "locked" or "calculated"
                ? $"Run status is {run.Status}."
                : $"Run status is {run.Status}; only draft, locked or calculated runs can pass this input check.",
            0));
        checks.Add(new("population", "Payroll profiles selected",
            profiles.Count > 0 ? "pass" : "fail",
            profiles.Count > 0
                ? $"{profiles.Count} worker{(profiles.Count == 1 ? "" : "s")} match this run's pay group and branch scope."
                : "No worker payroll profiles match this run's pay group and branch scope.",
            profiles.Count));
        checks.Add(new("earning-components", "Active earning components configured",
            earningComponents.Count > 0 ? "pass" : "fail",
            earningComponents.Count > 0
                ? $"{earningComponents.Count} active earning component{(earningComponents.Count == 1 ? "" : "s")} will be evaluated."
                : "No active earning components are configured.",
            earningComponents.Count));
        checks.Add(new("basic-salary", "Basic salary values present",
            issues.Any(i => i.Issue.Contains("Basic salary", StringComparison.OrdinalIgnoreCase) && i.Severity == "fail") ? "fail" : "pass",
            basicComponent is null
                ? "The configured component code 'basic' is missing."
                : "Every included worker has a positive basic salary value.",
            issues.Count(i => i.Issue.Contains("Basic salary", StringComparison.OrdinalIgnoreCase))));
        checks.Add(new("bank-details", "Payment details recorded",
            missingBankProfiles.Count == 0 ? "pass" : "warn",
            missingBankProfiles.Count == 0
                ? "Every included worker has primary payment details."
                : $"{missingBankProfiles.Count} included worker{(missingBankProfiles.Count == 1 ? "" : "s")} are missing primary payment details. Payroll can be reviewed and released, but a bank payment file cannot be generated for them until this is fixed.",
            missingBankProfiles.Count));
        checks.Add(new("tax-slabs", "Tax slabs configured for period year",
            slabs.Count > 0 ? "pass" : "fail",
            slabs.Count > 0
                ? $"{slabs.Count} tax slab{(slabs.Count == 1 ? "" : "s")} configured for {period.StartDate.Year}."
                : $"No active tax slabs are configured for {period.StartDate.Year}. Configure PAYE tax slabs before calculation.",
            slabs.Count));

        var statutoryComponents = activeComponents.Where(c => c.IsStatutory && c.CalculationBasis == "percent-of").ToList();
        var missingRules = statutoryComponents
            .Where(c => !rules.Any(r => r.Code.Equals(c.Code, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        checks.Add(new("contribution-rules", "Statutory contribution rules configured",
            missingRules.Count == 0 ? "pass" : "warn",
            missingRules.Count == 0
                ? $"{rules.Count} active contribution rule{(rules.Count == 1 ? "" : "s")} are available."
                : $"Missing active contribution rule configuration for: {string.Join(", ", missingRules.Select(r => r.Code))}.",
            missingRules.Count));
        checks.Add(new("overtime", "Approved overtime input loaded", "pass",
            approvedOvertime.Count > 0
                ? $"{approvedOvertime.Count} approved overtime record{(approvedOvertime.Count == 1 ? "" : "s")} will be included."
                : "No approved overtime records are waiting for this run.",
            approvedOvertime.Count));
        checks.Add(new("proration", "Leave proration input loaded", "pass",
            prorationInputs.UnpaidLeaves.Count > 0
                ? $"{prorationInputs.UnpaidLeaves.Count} approved unpaid leave record{(prorationInputs.UnpaidLeaves.Count == 1 ? "" : "s")} may prorate payment days."
                : "No approved unpaid leave records overlap this period.",
            prorationInputs.UnpaidLeaves.Count));
        checks.Add(new("period-dates", "Period dates are valid",
            period.StartDate <= period.EndDate && period.CutoffDate <= period.PayDate ? "pass" : "fail",
            period.StartDate <= period.EndDate && period.CutoffDate <= period.PayDate
                ? $"{period.PeriodLabel} runs from {period.StartDate:yyyy-MM-dd} to {period.EndDate:yyyy-MM-dd}; cutoff is {period.CutoffDate:yyyy-MM-dd}."
                : "The pay period date range or cutoff/pay-date order is invalid.",
            0));

        var failures = checks.Count(c => c.State == "fail");
        var warnings = checks.Count(c => c.State == "warn") + issues.Count(i => i.Severity == "warn");
        return new PayrollCalculationReadinessDto(run.Id, failures == 0, profiles.Count,
            failures, warnings, checks, issues.OrderBy(i => i.Severity).ThenBy(i => i.EmployeeNo).ToList());
    }

    public async Task<PayrollRunDto> CalculateRunAsync(Guid id, CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        if (run.Status is not "locked" and not "calculated")
            throw new DomainException("run-not-calculation-ready", $"Run is in status {run.Status} and cannot be calculated.");
        var readiness = await BuildCalculationReadinessAsync(run, ct);
        if (!readiness.Ready)
            throw new DomainException("payroll-calculation-readiness-failed",
                "Payroll calculation readiness failed: " + string.Join("; ", readiness.Checks.Where(c => c.State == "fail").Select(c => c.Label)));
        run.Status = "calculating";
        await repo.UpdateRunAsync(run, ct);

        // M46: a branch run pays only the workers attached to its branch;
        // an organisation-wide run pays everyone.
        var (profiles, components, rules, slabs, cutoff) = await repo.LoadCalculationInputsAsync(run.PayPeriodId, ct, run.LocationId);
        var payrollBenefits = (await repo.LoadPayrollBenefitAllowancesAsync(run.PayPeriodId, run.LocationId, ct))
            .GroupBy(x => x.WorkerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var salaryAdvances = (await repo.LoadDeductibleSalaryAdvancesAsync(run.PayPeriodId, run.LocationId, ct))
            .GroupBy(x => x.WorkerId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var advanceRecovered = await repo.GetSalaryAdvanceRecoveredAmountsAsync(
            salaryAdvances.Values.SelectMany(x => x).Select(x => x.Id).Distinct().ToList(), ct);
        var prorationInputs = await repo.LoadProrationInputsAsync(run.PayPeriodId, ct);
        var approvedOvertime = await repo.LoadApprovedOvertimeAsync(run.PayPeriodId, run.LocationId, ct);
        int exceptions = 0;
        run.TotalGross = run.TotalDeductions = run.TotalNet = run.TotalEmployerCost = 0;
        run.EmployeeCount = 0;
        run.ExceptionCount = 0;
        await repo.ClearRunLinesAsync(run.Id, ct);

        foreach (var profile in profiles)
        {
            var worker = profile.Worker;
            if (worker is null) { exceptions++; continue; }
            var ctx = new CalcContext(worker, profile, components, rules, slabs);

            // M41 Gap 2: how many days did this worker actually earn pay for?
            var (workingDays, paymentDays, note) = PaymentDaysCalculator.For(
                prorationInputs, worker, prorationInputs.UnpaidLeaves.Where(l => l.WorkerId == worker.Id).ToList());
            ctx.SetProration(workingDays, paymentDays, note);
            // Milestone 1: approved attendance overtime is a first-class earning.
            // It is intentionally added before statutory components so PAYE and
            // percentage-based deductions see the same explainable gross basis.
            ctx.AddOvertime(approvedOvertime.Where(a => a.WorkerId == worker.Id).ToList());
            ctx.AddPayrollBenefits(payrollBenefits.TryGetValue(worker.Id, out var benefits) ? benefits : []);
            foreach (var comp in components.Where(c => c.IsActive).OrderBy(c => c.Priority))
                ctx.Evaluate(comp);
            ctx.AddSalaryAdvances(
                salaryAdvances.TryGetValue(worker.Id, out var advances) ? advances : [],
                advanceRecovered);
            var net = ctx.Gross - ctx.Deductions;
            if (net < 0) { exceptions++; ctx.ExceptionReason = "negative-net"; }
            // Missing payment details do not affect gross-to-net calculation or
            // payslip release. They remain a payment-readiness warning and block
            // the bank-file step until HR records a primary payment method.
            run.EmployeeCount++;
            run.TotalGross += ctx.Gross;
            run.TotalDeductions += ctx.Deductions;
            run.TotalNet += net;
            run.TotalEmployerCost += ctx.EmployerCost + ctx.Gross;
            run.ExceptionCount = exceptions;

            var line = new PayrollRunLine
            {
                RunId = run.Id, WorkerId = worker.Id,
                GrossPay = Math.Round(ctx.Gross, 2), TotalDeductions = Math.Round(ctx.Deductions, 2),
                NetPay = Math.Round(net, 2), EmployerCost = Math.Round(ctx.EmployerCost, 2),
                HasException = ctx.ExceptionReason is not null, ExceptionReason = ctx.ExceptionReason,
                ComponentCount = ctx.Components.Count,
                RuleVersionSnapshot = JsonSerializer.Serialize(components.Select(c => new { c.Id, c.Version }).ToList()),
                WorkingDays = ctx.WorkingDays, PaymentDays = ctx.PaymentDays,
                ProrationNote = ctx.ProrationNote,
            };
            foreach (var lc in ctx.Components)
                line.Components.Add(new PayrollLineComponent { ComponentCode = lc.Code, ComponentName = lc.Name, ComponentType = lc.Type, Amount = lc.Amount, Explanation = lc.Explanation, IsStatutory = lc.IsStatutory });
            await repo.AddRunLineAsync(line, ct);
        }
        run.Status = "calculated";
        run.CalculatedBySubjectId = actorSubjectId;
        await repo.UpdateRunAsync(run, ct);
        await RecordEventAsync(run, "calculated", actorSubjectId, "calculating", "calculated", null,
            new { run.EmployeeCount, run.ExceptionCount, run.TotalGross, run.TotalNet }, ct);
        return MapRun(run);
    }

    public async Task<PayrollRunDto> GetRunAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        return MapRun(run);
    }

    public async Task<Paged<PayrollRunLineDto>> GetRunLinesAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var (items, total) = await repo.ListRunLinesAsync(id, ct);
        return new Paged<PayrollRunLineDto>(items.Select(l => new PayrollRunLineDto(
            l.Id, l.WorkerId, l.Worker?.FullName ?? "", l.Worker?.EmployeeNo ?? "",
            l.GrossPay, l.TotalDeductions, l.NetPay, l.EmployerCost, l.HasException, l.ExceptionReason,
            l.Components.Select(c => new PayrollLineComponentDto(c.ComponentCode, c.ComponentName, c.ComponentType, c.Amount, c.Explanation, c.IsStatutory)).ToList(),
            l.ExceptionStatus, l.ExceptionDecisionReason, l.ExceptionDecidedBySubjectId, l.ExceptionDecidedAt, l.IsExcluded,
            l.WorkingDays, l.PaymentDays, l.ProrationNote)).ToList(), total, 1, 100);
    }

    public async Task<WorkerPayslipPreviewDto> PreviewWorkerPayslipAsync(Guid workerId, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "payroll", "hr_admin");
        var guardrails = new List<string>();
        var openProfile = await repo.FindOpenProfileAsync(workerId, ct);
        if (openProfile is null)
            return new WorkerPayslipPreviewDto("blocked", "", "ZMW",
                ["No active payroll profile is linked to this employee. Add a payroll profile before previewing a payslip."], null);

        var payGroup = await repo.GetPayGroupAsync(openProfile.PayGroupId, ct);
        var periods = await repo.ListPeriodsAsync(openProfile.PayGroupId, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var period = periods.FirstOrDefault(p => p.IsCurrent)
            ?? periods.FirstOrDefault(p => p.StartDate <= today && p.EndDate >= today)
            ?? periods.FirstOrDefault();
        if (period is null)
            return new WorkerPayslipPreviewDto("blocked", "", payGroup?.Currency ?? "ZMW",
                ["No pay period exists for this employee's pay group. Create a pay period before previewing a payslip."], null);

        var locationId = openProfile.Worker?.LocationId;
        var (profiles, components, rules, slabs, _) = await repo.LoadCalculationInputsAsync(period.Id, ct, locationId);
        var profile = profiles.FirstOrDefault(p => p.WorkerId == workerId) ?? openProfile;
        var worker = profile.Worker ?? await repo.GetWorkerAsync(workerId, ct);
        if (worker is null)
            return new WorkerPayslipPreviewDto("blocked", period.PeriodLabel, payGroup?.Currency ?? "ZMW",
                ["The payroll profile is not linked to an employee record."], null);

        var activeComponents = components.Where(c => c.IsActive).OrderBy(c => c.Priority).ToList();
        var basicComponent = activeComponents.FirstOrDefault(c => c.Code.Equals("basic", StringComparison.OrdinalIgnoreCase));
        if (basicComponent is null)
            guardrails.Add("Basic salary component is not active in payroll configuration.");
        else if (!profile.ComponentValues.Any(v => v.ComponentId == basicComponent.Id && v.Amount > 0))
            guardrails.Add("Basic salary is missing or zero on the employee payroll profile.");
        if (!activeComponents.Any(c => c.ComponentType == "earning"))
            guardrails.Add("No active earning components are configured.");
        if (!worker.BankDetails.Any(b => b.IsPrimary))
            guardrails.Add("Primary bank or mobile money payment details are missing.");

        var ctx = new CalcContext(worker, profile, activeComponents, rules, slabs);
        var prorationInputs = await repo.LoadProrationInputsAsync(period.Id, ct);
        var (workingDays, paymentDays, note) = PaymentDaysCalculator.For(
            prorationInputs, worker, prorationInputs.UnpaidLeaves.Where(l => l.WorkerId == worker.Id).ToList());
        ctx.SetProration(workingDays, paymentDays, note);
        ctx.AddOvertime((await repo.LoadApprovedOvertimeAsync(period.Id, locationId, ct))
            .Where(a => a.WorkerId == worker.Id).ToList());
        ctx.AddPayrollBenefits((await repo.LoadPayrollBenefitAllowancesAsync(period.Id, locationId, ct))
            .Where(a => a.WorkerId == worker.Id).ToList());
        foreach (var comp in activeComponents)
            ctx.Evaluate(comp);
        var previewAdvances = (await repo.LoadDeductibleSalaryAdvancesAsync(period.Id, locationId, ct))
            .Where(a => a.WorkerId == worker.Id).ToList();
        ctx.AddSalaryAdvances(previewAdvances,
            await repo.GetSalaryAdvanceRecoveredAmountsAsync(previewAdvances.Select(a => a.Id).ToList(), ct));
        var net = ctx.Gross - ctx.Deductions;
        if (ctx.Gross <= 0)
            guardrails.Add("Gross pay is zero. Check basic salary, earning components and salary profile setup.");
        if (net <= 0)
            guardrails.Add("Net pay is zero or negative. Review deductions before releasing a payslip.");
        if (ctx.ExceptionReason is not null)
            guardrails.Add(ctx.ExceptionReason);

        var line = new PayrollRunLineDto(Guid.Empty, worker.Id, worker.FullName, worker.EmployeeNo,
            Math.Round(ctx.Gross, 2), Math.Round(ctx.Deductions, 2), Math.Round(net, 2),
            Math.Round(ctx.EmployerCost, 2), guardrails.Count > 0, guardrails.FirstOrDefault(),
            ctx.Components.Select(c => new PayrollLineComponentDto(c.Code, c.Name, c.Type, c.Amount, c.Explanation, c.IsStatutory)).ToList(),
            WorkingDays: ctx.WorkingDays, PaymentDays: ctx.PaymentDays, ProrationNote: ctx.ProrationNote);

        return new WorkerPayslipPreviewDto(guardrails.Count == 0 ? "ready" : "blocked",
            period.PeriodLabel, payGroup?.Currency ?? "ZMW", guardrails, line);
    }

    public async Task<PayrollRunDto> DecideExceptionAsync(Guid id, Guid lineId, PayrollExceptionDecisionRequest request,
        CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await RequireCalculatedRunAsync(id, ct);
        var line = await repo.GetRunLineAsync(id, lineId, ct)
            ?? throw new DomainException("payroll-line-not-found", $"Line {lineId} does not belong to run {id}.");
        if (!line.HasException)
            throw new DomainException("payroll-line-no-exception", "This line has no exception to decide.");
        var decision = request.Decision.Trim().ToLowerInvariant();
        if (decision is not ("resolved" or "waived" or "excluded"))
            throw new DomainException("bad-exception-decision", "Decision must be resolved, waived, or excluded.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new DomainException("exception-reason-required", "A reason is required for every exception decision.");
        if (decision == "waived" && string.Equals(run.PreparedBySubjectId, actorSubjectId, StringComparison.Ordinal))
            throw new DomainException("exception-self-waiver", "The run preparer cannot waive their own exception. A separate approver must record the waiver.");

        line.ExceptionStatus = decision;
        line.ExceptionDecisionReason = request.Reason.Trim();
        line.ExceptionDecidedBySubjectId = actorSubjectId;
        line.ExceptionDecidedAt = DateTimeOffset.UtcNow;
        line.IsExcluded = decision == "excluded";
        await repo.UpdateRunLineAsync(line, ct);
        await repo.RecalculateRunTotalsAsync(run, ct);
        await RecordEventAsync(run, $"exception-{decision}", actorSubjectId, run.Status, run.Status,
            request.Reason, new { lineId, line.WorkerId, line.ExceptionReason }, ct);
        return MapRun(run);
    }

    public async Task<PayrollRunDto> ApplyCorrectionAsync(Guid id, Guid lineId, PayrollCorrectionRequest request,
        CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await RequireCalculatedRunAsync(id, ct);
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new DomainException("correction-reason-required", "A reason is required for a payroll correction.");
        var line = await repo.GetRunLineAsync(id, lineId, ct)
            ?? throw new DomainException("payroll-line-not-found", $"Line {lineId} does not belong to run {id}.");
        var component = line.Components.FirstOrDefault(c => c.ComponentCode.Equals(request.ComponentCode, StringComparison.OrdinalIgnoreCase));
        if (component is null && request.ComponentCode.Equals("one-off-payment-top-up", StringComparison.OrdinalIgnoreCase))
        {
            component = new PayrollLineComponent
            {
                ComponentCode = "one-off-payment-top-up",
                ComponentName = "Approved payment top-up",
                ComponentType = "earning",
                IsStatutory = false,
                Explanation = "Approved taxable one-off payment top-up.",
            };
            line.Components.Add(component);
        }
        if (component is null)
            throw new DomainException("payroll-component-not-found", $"Component {request.ComponentCode} is not present on this line.");
        var before = component.Amount;
        component.Amount = Math.Round(request.Amount, 2);
        component.Explanation = $"HR-authorised August 2026 overtime correction: K{component.Amount:N2} monthly gross earning. This correction is not annualised and does not create another bank payment.";
        var delta = component.Amount - before;
        switch (component.ComponentType)
        {
            case "earning": line.GrossPay += delta; line.NetPay += delta; break;
            case "deduction":
            case "tax": line.TotalDeductions += delta; line.NetPay -= delta; break;
            case "employer-contribution": line.EmployerCost += delta; break;
        }
        line.ExceptionStatus = "resolved";
        line.ExceptionDecisionReason = request.Reason.Trim();
        line.ExceptionDecidedBySubjectId = actorSubjectId;
        line.ExceptionDecidedAt = DateTimeOffset.UtcNow;
        line.IsExcluded = false;
        await repo.UpdateRunLineAsync(line, ct);
        await repo.RecalculateRunTotalsAsync(run, ct);
        await RecordEventAsync(run, "correction-applied", actorSubjectId, run.Status, run.Status, request.Reason,
            new { lineId, line.WorkerId, component = component.ComponentCode, before, after = component.Amount }, ct);
        return MapRun(run);
    }

    /// <summary>
    /// Applies a narrowly scoped correction to a payroll that has already been paid. The bank
    /// payment remains untouched; a corrected payslip version becomes the current document.
    /// This is intentionally separate from draft/calculated corrections.
    /// </summary>
    public async Task<PayrollRunDto> ApplyReleasedCorrectionAsync(Guid id, Guid lineId, PayrollCorrectionRequest request,
        CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("hr_admin");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new DomainException("correction-reason-required", "A reason is required for a payroll correction.");

        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        if (run.Status != "released" || run.PaymentStatus != "released")
            throw new DomainException("released-correction-not-available", "This correction route is only available after payslips and the payment file have both been released.");

        var line = await repo.GetRunLineAsync(id, lineId, ct)
            ?? throw new DomainException("payroll-line-not-found", $"Line {lineId} does not belong to run {id}.");
        var component = line.Components.FirstOrDefault(c => c.ComponentCode.Equals(request.ComponentCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainException("payroll-component-not-found", $"Component {request.ComponentCode} is not present on this line.");
        if (component.ComponentType != "earning" || component.IsStatutory)
            throw new DomainException("released-correction-component-not-allowed", "Only a non-statutory earning can be corrected after payment.");

        var previousPayslip = await repo.GetCurrentPayslipForRunLineAsync(line.Id, ct)
            ?? throw new DomainException("payslip-not-found", "The paid payroll line does not have a current payslip to correct.");
        var before = component.Amount;
        component.Amount = Math.Round(request.Amount, 2);

        var (_, definitions, rules, slabs, _) = await repo.LoadCalculationInputsAsync(run.PayPeriodId, ct, run.LocationId);
        RecalculateReleasedLine(line, definitions, rules, slabs);
        await repo.UpdateRunLineAsync(line, ct);
        await repo.RecalculateRunTotalsAsync(run, ct);

        previousPayslip.Status = "superseded";
        await repo.UpdatePayslipAsync(previousPayslip, ct);
        var corrected = new Payslip
        {
            RunLineId = line.Id,
            WorkerId = line.WorkerId,
            PayslipNo = $"{previousPayslip.PayslipNo}-V{previousPayslip.Version + 1}",
            Version = previousPayslip.Version + 1,
            SupersedesId = previousPayslip.Id,
            GrossPay = line.GrossPay,
            TotalDeductions = line.TotalDeductions,
            NetPay = line.NetPay,
            YtdGross = (ParseMoney(previousPayslip.YtdGross) + (line.GrossPay - previousPayslip.GrossPay)).ToString("F2"),
            YtdTax = (ParseMoney(previousPayslip.YtdTax) + (line.TotalDeductions - previousPayslip.TotalDeductions)).ToString("F2"),
            YtdNet = (ParseMoney(previousPayslip.YtdNet) + (line.NetPay - previousPayslip.NetPay)).ToString("F2"),
            Status = "corrected",
            ReleasedAt = DateTimeOffset.UtcNow,
            LocationId = previousPayslip.LocationId,
            WorkerNrc = previousPayslip.WorkerNrc,
            WorkerTpin = previousPayslip.WorkerTpin,
            WorkerNapsaNumber = previousPayslip.WorkerNapsaNumber,
            WorkerNhimaNumber = previousPayslip.WorkerNhimaNumber,
        };
        await repo.CreatePayslipAsync(corrected, ct);
        await RecordEventAsync(run, "released-correction-applied", actorSubjectId, "released", "released", request.Reason,
            new { lineId, line.WorkerId, component = component.ComponentCode, before, after = component.Amount, correctedPayslipId = corrected.Id, paymentTreatment = "already-paid-no-bank-file-generated" }, ct);
        return MapRun(run);
    }

    private static decimal ParseMoney(string? value) => decimal.TryParse(value, out var amount) ? amount : 0m;

    private static void RecalculateReleasedLine(PayrollRunLine line, List<SalaryComponent> definitions,
        List<ContributionRule> rules, List<TaxSlab> slabs)
    {
        var configured = definitions.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var earnings = line.Components.Where(c => c.ComponentType == "earning").ToList();
        var gross = earnings.Sum(c => c.Amount);
        var taxable = earnings.Sum(c => !configured.TryGetValue(c.ComponentCode, out var definition) || definition.IsTaxable ? c.Amount : 0m);
        decimal Resolve(string code) => code.Equals("gross", StringComparison.OrdinalIgnoreCase) ? gross : code.Equals("taxable", StringComparison.OrdinalIgnoreCase) ? taxable : line.Components.FirstOrDefault(c => c.ComponentCode.Equals(code, StringComparison.OrdinalIgnoreCase))?.Amount ?? 0m;
        foreach (var statutory in line.Components.Where(c => c.IsStatutory && c.ComponentType != "earning"))
        {
            if (!configured.TryGetValue(statutory.ComponentCode, out var definition)) continue;
            decimal amount;
            var rule = rules.FirstOrDefault(r => r.IsActive && r.Code.Equals(statutory.ComponentCode, StringComparison.OrdinalIgnoreCase));
            if (rule is not null)
            {
                amount = Resolve(rule.TiedComponentCode ?? "") * rule.Rate / 100m;
                if (rule.Ceiling.HasValue) amount = Math.Min(amount, rule.Ceiling.Value);
                if (rule.Floor.HasValue) amount = Math.Max(amount, rule.Floor.Value);
            }
            else if (definition.CalculationBasis == "slab")
            {
                amount = slabs.Where(s => s.IsActive).OrderBy(s => s.Sequence).Sum(s =>
                {
                    if (taxable <= s.MinAmount) return 0m;
                    return Math.Max(0m, Math.Min(taxable, s.MaxAmount ?? taxable) - s.MinAmount) * s.Rate / 100m;
                });
            }
            else if (definition.CalculationBasis == "percent-of") amount = Resolve(definition.BasisComponentCode ?? "") * (definition.Rate ?? 0m) / 100m;
            else continue;
            statutory.Amount = Math.Round(amount, 2);
            statutory.Explanation = definition.CalculationBasis == "slab"
                ? $"Progressive PAYE slab calculation on taxable income K{taxable:N2} (ZRA bands)"
                : $"Recalculated from corrected payroll gross K{gross:N2}.";
        }
        line.GrossPay = Math.Round(gross, 2);
        line.TotalDeductions = Math.Round(line.Components.Where(c => c.ComponentType is "deduction" or "tax").Sum(c => c.Amount), 2);
        line.EmployerCost = Math.Round(line.Components.Where(c => c.ComponentType == "employer-contribution").Sum(c => c.Amount), 2);
        line.NetPay = Math.Round(line.GrossPay - line.TotalDeductions, 2);
    }

    private async Task<PayrollRun> RequireCalculatedRunAsync(Guid id, CancellationToken ct)
    {
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        if (run.Status != "calculated")
            throw new DomainException("run-not-correctable", $"Run is in status {run.Status}; exceptions and corrections can only change a calculated run before approval.");
        return run;
    }

    public async Task<PayrollRunDto> ApproveRunAsync(Guid id, string? note, CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        if (run.Status is not "calculated" and not "in-review")
            throw new DomainException("run-not-review-ready", $"Run is in status {run.Status}; it must be calculated (and submitted) before approval.");
        if (actorSubjectId != "system" && string.Equals(run.PreparedBySubjectId, actorSubjectId, StringComparison.Ordinal)
            && !authz.IsRole("hr_admin"))
            throw new DomainException("run-self-approval", "The person who prepared this run cannot approve it. Send it to a separate payroll or HR approver.");
        // M46: a branch payroll draft flows UP for approval — the approver of a
        // branch run must be org-wide HR (no branch assignments). Confined
        // branch HR cannot approve their own branch's run; peer approval at the
        // same branch stays possible only for org-wide runs.
        if (run.LocationId.HasValue && actorSubjectId != "system"
            && (scope?.IsConfined ?? false) && (scope?.AllowedLocationIds.Contains(run.LocationId.Value) ?? false))
            throw new DomainException("run-branch-approval-confined",
                "This run belongs to a branch and must be approved by organisation-wide HR. Your account is confined to a branch and cannot approve branch payroll runs — send it to top HR.");
        var (lines, _) = await repo.ListRunLinesAsync(id, ct);
        var outstanding = lines.Count(l => l.HasException && l.ExceptionStatus == "open" && !l.IsExcluded);
        if (actorSubjectId != "system" && outstanding > 0)
            throw new DomainException("run-exceptions-open", $"Run has {outstanding} outstanding exception(s). Resolve, waive, or exclude each exception before approval.");
        var fromStatus = run.Status;
        run.Status = "approved";
        run.ApprovalNote = note;
        run.ApprovedBySubjectId = actorSubjectId;
        await repo.UpdateRunAsync(run, ct);
        await RecordEventAsync(run, "approved", actorSubjectId, fromStatus, "approved", note, null, ct);
        return MapRun(run);
    }

    // M46: the branch preparer sends their calculated draft up for top-HR
    // approval (draft | calculated -> in-review, branch run only). This puts
    // the run on the organisation-wide approver's review queue and documents
    // the maker-checker hand-off on the audit trail.
    public async Task<PayrollRunDto> SubmitRunAsync(Guid id, CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        if (!run.LocationId.HasValue)
            throw new DomainException("run-not-branch", "Only a branch payroll run can be sent for review — organisation-wide runs go straight to approval.");
        if (run.Status is not "draft" and not "calculated")
            throw new DomainException("run-not-submittable", $"Run is in status {run.Status}; only a draft or calculated branch run can be sent for review.");
        if (actorSubjectId != "system" && string.Equals(run.PreparedBySubjectId, actorSubjectId, StringComparison.Ordinal) && run.Status == "draft")
            throw new DomainException("run-not-calculated-yet", "Calculate the branch run before sending it for review — the approver must be able to see the figures.");
        var (lines, _) = await repo.ListRunLinesAsync(id, ct);
        if (actorSubjectId != "system" && !lines.Any())
            throw new DomainException("run-no-lines", "The run has no pay lines. Calculate it before sending for review.");
        var fromStatus = run.Status;
        run.Status = "in-review";
        await repo.SubmitRunAsync(run, ct);
        await RecordEventAsync(run, "submitted-for-review", actorSubjectId, fromStatus, "in-review", null, null, ct);
        return MapRun(run);
    }

    /// <summary>Release finalizes payslips and generates the payment file
    /// payload. Segregation of duties applies to payroll officers; HR admin can
    /// override it when they intentionally carry final payroll authority.</summary>
    public async Task<PayrollRunDto> ReleaseRunAsync(Guid id, CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        var isHrAdmin = authz.IsRole("hr_admin");
        // M24: every worker in the run must carry the statutory identity pack
        // (NRC, TPIN, NAPSA number, NHIMA number) before payslips can be released.
        var readiness = await GetRunStatutoryReadinessAsync(id, ct);
        if (!readiness.IsReady)
        {
            var blockers = readiness.Workers.Where(w => !w.Ready).ToList();
            var detail = string.Join("; ", blockers.Select(b =>
                $"{b.EmployeeNo} {b.FullName}" +
                (b.HasNrc ? "" : " NRC") +
                (b.HasTpin ? "" : " TPIN") +
                (b.HasNapsaNumber ? "" : " NAPSA") +
                (b.HasNhimaNumber ? "" : " NHIMA")));
            throw new DomainException("run-statutory-readiness",
                $"Run cannot be released: {blockers.Count} worker(s) are missing statutory identity references ({detail}). Collect the references on each worker record, then release again.");
        }
        if (run.Status != "approved")
            throw new DomainException("run-not-releasable", $"Run is in status {run.Status}; it must be approved by a separate reviewer before release.");
        if (!isHrAdmin && actorSubjectId != "system" && string.Equals(run.ApprovedBySubjectId, actorSubjectId, StringComparison.Ordinal))
            throw new DomainException("run-self-release", "The approver cannot also release the run. A separate payroll officer must release it.");
        async Task ReleaseAndEnqueue(CancellationToken transactionCt)
        {
            run.Status = "released";
            run.ReleasedBySubjectId = actorSubjectId;
            await repo.UpdateRunAsync(run, transactionCt);
            // Only a released run consumes approved overtime. Calculation remains
            // idempotent because approved rows stay approved until this boundary.
            var overtimeToLink = await repo.LoadApprovedOvertimeAsync(run.PayPeriodId, run.LocationId, transactionCt);
            var (releasedLines, _) = await repo.ListRunLinesAsync(run.Id, transactionCt);
            foreach (var overtime in overtimeToLink)
            {
                var line = releasedLines.FirstOrDefault(l => l.WorkerId == overtime.WorkerId);
                if (line is not null)
                    await repo.LinkOvertimeToPayrollAsync(overtime.Id, run.Id, line.Id, transactionCt);
            }
            // Reversal runs negate the original: supersede its payslips FIRST so the
            // replacement payslips generated below can chain to them via SupersedesId.
            if (run.IsReversal && run.ReversesRunId.HasValue)
            {
                await repo.SupersedeOriginalPayslipsAsync(run.ReversesRunId.Value, transactionCt);
                var original = await repo.GetRunAsync(run.ReversesRunId.Value, transactionCt);
                if (original is not null && original.Status == "reversed")
                {
                    original.Status = "closed"; // release cycle complete
                    await repo.UpdateRunAsync(original, transactionCt);
                }
            }

            var targets = await repo.FinalizePayslipsAsync(run.Id, transactionCt);
            if (outbox is null) return; // legacy/unit-test construction only; production always registers the writer
            foreach (var target in targets)
            {
                await outbox.EnqueueAsync(
                    HrmEventTypes.PayslipReleased,
                    target.SubjectId ?? target.WorkerId.ToString("D"),
                    new
                    {
                        payslip_id = target.PayslipId.ToString("D"),
                        payslip_no = target.PayslipNo,
                        period_label = target.PeriodLabel,
                        worker_id = target.WorkerId.ToString("D"),
                        email = target.Email ?? "",
                        emails = new[] { target.Email, target.PersonalEmail }
                            .Where(email => !string.IsNullOrWhiteSpace(email))
                            .Select(email => email!.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray(),
                        first_name = target.FirstName,
                        last_name = target.LastName,
                    },
                    transactionCt);
            }
        }

        if (unitOfWork is null)
            await ReleaseAndEnqueue(ct);
        else
            await unitOfWork.ExecuteAsync(ReleaseAndEnqueue, ct);
        await RecordEventAsync(run, "payslips-released", actorSubjectId, "approved", "released", null,
            new { run.EmployeeCount, run.TotalNet }, ct);
        return MapRun(run);
    }

    /// <summary>Voids an unreleased payroll run audit-preservingly. This is the
    /// top-admin exit path for setup/calculation mistakes before payslips have
    /// been released. Lines remain inspectable; status becomes terminal so a
    /// clean replacement run may be created for the same period/scope.</summary>
    public async Task<PayrollRunDto> CancelRunAsync(Guid id, PayrollRunReverseCreate request, CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("hr_admin");
        var reason = (request.Reason ?? "").Trim();
        if (reason.Length < 5)
            throw new DomainException("run-cancel-reason-required", "A cancellation reason of at least 5 characters is required.");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        if (run.IsReversal)
            throw new DomainException("run-reversal-cancel-blocked", "A reversal run cannot be cancelled. Release it to complete the reversal or leave it as audit evidence.");
        if (run.Status is "released" or "closed")
            throw new DomainException("run-cancel-requires-reversal", $"Run is in status {run.Status}; released payroll must be reversed instead of cancelled.");
        if (run.Status == "reversed")
            throw new DomainException("run-already-terminal", "This run has already been reversed or voided.");
        var fromStatus = run.Status;
        run.Status = "reversed";
        await repo.UpdateRunAsync(run, ct);
        await RecordEventAsync(run, "cancelled", actorSubjectId, fromStatus, "reversed", reason,
            new { control = "top-admin-void", run.PaymentStatus, run.TotalNet }, ct);
        return MapRun(run);
    }

    /// <summary>Reverses a released or closed run audit-preservingly: creates a new draft
    /// reversal run in the same period whose release negates the original. The
    /// original run's payslips are superseded once the reversal is released; the
    /// original's status moves to reversed and it can never be re-released.</summary>
    public async Task<PayrollRunDto> ReverseRunAsync(Guid id, PayrollRunReverseCreate request, CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        if (run.IsReversal)
            throw new DomainException("run-already-reversal", "A reversal run cannot itself be reversed; create a new regular run instead.");

        // Idempotency: at most one reversal run per original run.
        var existingReversal = await repo.FindRunByReversesIdAsync(run.Id, ct);
        if (existingReversal is not null)
            throw new DomainException("run-reversal-exists", $"Run {id} already has a reversal run {existingReversal.Id} (status {existingReversal.Status}).");
        if (run.Status is not ("released" or "closed"))
            throw new DomainException("run-not-reversible", $"Run is in status {run.Status}; only released or closed runs can be reversed.");

        var fromStatus = run.Status;

        // Mark original as reversed so it is excluded from control totals.
        run.Status = "reversed";
        await repo.UpdateRunAsync(run, ct);

        var reversal = new PayrollRun
        {
            PayPeriodId = run.PayPeriodId,
            PayGroupId = run.PayGroupId,
            Status = "draft",
            IsReversal = true,
            ReversesRunId = run.Id,
            // M44: the reversal belongs to the same branch as the run it reverses.
            LocationId = run.LocationId,
        };
        await repo.CreateRunAsync(reversal, ct);
        reversal.PreparedBySubjectId = actorSubjectId;
        await repo.UpdateRunAsync(reversal, ct);
        await RecordEventAsync(run, "reversal-created", actorSubjectId, fromStatus, "reversed", request.Reason,
            new { reversalRunId = reversal.Id }, ct);
        await RecordEventAsync(reversal, "created-as-reversal", actorSubjectId, null, "draft", request.Reason,
            new { originalRunId = run.Id }, ct);
        return MapRun(reversal);
    }

    public async Task<PayrollRunDto> GeneratePaymentFileAsync(Guid id, CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        if (run.IsHistorical)
            throw new DomainException("historical-run-payment-blocked", "This is a historical payroll record. It cannot generate a bank payment file because payment was handled outside the HRM.");
        if (run.Status != "released")
            throw new DomainException("payment-run-not-released", "Payslips must be released before a payment file can be generated.");
        if (run.PaymentStatus != "not-created")
            throw new DomainException("payment-file-exists", $"The payment workflow is already in status {run.PaymentStatus}.");
        var rows = await repo.ListPaymentRowsAsync(id, ct);
        if (rows.Count == 0) throw new DomainException("payment-file-empty", "No payable workers remain in this run.");
        if (rows.Any(x => string.IsNullOrWhiteSpace(x.AccountNumber)))
            throw new DomainException("payment-bank-details-missing", "Every payable worker must have primary bank details before file generation.");
        run.PaymentStatus = "generated";
        run.PaymentFileReference = $"PAY-{DateTimeOffset.UtcNow:yyyyMMdd}-{run.Id.ToString("N")[..8].ToUpperInvariant()}";
        run.PaymentFileGeneratedAt = DateTimeOffset.UtcNow;
        run.PaymentFileGeneratedBySubjectId = actorSubjectId;
        await repo.UpdateRunAsync(run, ct);
        await RecordEventAsync(run, "payment-file-generated", actorSubjectId, "not-created", "generated", null,
            new { run.PaymentFileReference, rowCount = rows.Count, run.TotalNet }, ct);
        return MapRun(run);
    }

    public async Task<PayrollPaymentReadinessDto> GetPaymentReadinessAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        _ = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        var rows = await repo.ListPaymentRowsAsync(id, ct);
        var issues = rows
            .Where(x => string.IsNullOrWhiteSpace(x.AccountNumber))
            .Select(x => new PayrollPaymentReadinessIssueDto(
                x.WorkerId, x.EmployeeNo, x.WorkerName, x.Amount, "Primary bank details are missing."))
            .ToList();
        return new PayrollPaymentReadinessDto(
            id, rows.Count > 0 && issues.Count == 0, rows.Count, rows.Sum(x => x.Amount),
            issues.Count, issues);
    }

    public async Task<string> DownloadPaymentFileAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        if (run.PaymentStatus == "not-created") throw new DomainException("payment-file-not-generated", "Generate the payment file first.");
        var sb = new StringBuilder("payment_reference,employee_no,employee_name,bank,branch_code,account_name,account_number,amount,currency\n");
        foreach (var row in await repo.ListPaymentRowsAsync(id, ct))
            sb.AppendLine(string.Join(',', Csv(run.PaymentFileReference), Csv(row.EmployeeNo), Csv(row.WorkerName), Csv(row.BankName),
                Csv(row.BranchCode), Csv(row.AccountName), Csv(row.AccountNumber), row.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), "ZMW"));
        return sb.ToString();
    }

    public async Task<PayrollRunDto> ApprovePaymentFileAsync(Guid id, PayrollPaymentApprovalRequest request,
        CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        var isHrAdmin = authz.IsRole("hr_admin");
        if (run.PaymentStatus != "generated") throw new DomainException("payment-not-approvable", $"Payment is in status {run.PaymentStatus}.");
        if (!isHrAdmin && string.Equals(run.PaymentFileGeneratedBySubjectId, actorSubjectId, StringComparison.Ordinal))
            throw new DomainException("payment-self-approval", "The person who generated the payment file cannot approve it.");
        run.PaymentStatus = "approved";
        run.PaymentApprovedBySubjectId = actorSubjectId;
        await repo.UpdateRunAsync(run, ct);
        await RecordEventAsync(run, "payment-approved", actorSubjectId, "generated", "approved", request.Note, null, ct);
        return MapRun(run);
    }

    public async Task<PayrollRunDto> ReleasePaymentFileAsync(Guid id, CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        var isHrAdmin = authz.IsRole("hr_admin");
        if (run.PaymentStatus != "approved") throw new DomainException("payment-not-releasable", $"Payment is in status {run.PaymentStatus}.");
        if (!isHrAdmin && string.Equals(run.PaymentApprovedBySubjectId, actorSubjectId, StringComparison.Ordinal))
            throw new DomainException("payment-self-release", "The payment approver cannot also release the bank instruction.");
        if (!isHrAdmin && string.Equals(run.PaymentFileGeneratedBySubjectId, actorSubjectId, StringComparison.Ordinal))
            throw new DomainException("payment-generator-release", "The person who generated the payment file cannot release the bank instruction.");
        run.PaymentStatus = "released";
        run.PaymentReleasedBySubjectId = actorSubjectId;
        await repo.UpdateRunAsync(run, ct);
        await RecordEventAsync(run, "payment-released", actorSubjectId, "approved", "released", null,
            new { run.PaymentFileReference, run.TotalNet }, ct);
        return MapRun(run);
    }

    public async Task<PayrollRunDto> ReconcileRunAsync(Guid id, PayrollReconciliationRequest request,
        CancellationToken ct, string actorSubjectId = "system")
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        if (run.PaymentStatus != "released") throw new DomainException("payment-not-reconcilable", $"Payment is in status {run.PaymentStatus}.");
        if (string.IsNullOrWhiteSpace(request.Reference)) throw new DomainException("reconciliation-reference-required", "A bank reconciliation reference is required.");
        if (request.ActualAmount != run.TotalNet)
            throw new DomainException("reconciliation-amount-mismatch", $"Bank total {request.ActualAmount:F2} does not match payroll net {run.TotalNet:F2}.");
        run.PaymentStatus = "reconciled";
        run.Status = "closed";
        run.ReconciledBySubjectId = actorSubjectId;
        run.ReconciliationReference = request.Reference.Trim();
        run.ReconciledAmount = request.ActualAmount;
        run.ReconciledAt = DateTimeOffset.UtcNow;
        await repo.UpdateRunAsync(run, ct);
        await RecordEventAsync(run, "reconciled-and-closed", actorSubjectId, "released", "closed", request.Note,
            new { request.Reference, request.ActualAmount }, ct);
        return MapRun(run);
    }

    public async Task<List<PayrollRunEventDto>> GetRunAuditAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        _ = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        return (await repo.ListRunEventsAsync(id, ct)).Select(MapEvent).ToList();
    }

    public async Task<string> ExportRunAuditAsync(Guid id, CancellationToken ct)
    {
        var rows = await GetRunAuditAsync(id, ct);
        var sb = new StringBuilder("timestamp,action,actor,from_status,to_status,reason,details\n");
        foreach (var row in rows)
            sb.AppendLine(string.Join(',', Csv(row.CreatedAt.ToString("O")), Csv(row.Action), Csv(row.ActorSubjectId),
                Csv(row.FromStatus), Csv(row.ToStatus), Csv(row.Reason), Csv(row.DetailsJson)));
        return sb.ToString();
    }

    private async Task RecordEventAsync(PayrollRun run, string action, string actor, string? from, string? to,
        string? reason, object? details, CancellationToken ct) =>
        await repo.AddRunEventAsync(new PayrollRunEvent
        {
            RunId = run.Id, Action = action, ActorSubjectId = actor, FromStatus = from, ToStatus = to,
            Reason = reason, DetailsJson = details is null ? null : JsonSerializer.Serialize(details)
        }, ct);

    private static PayrollRunEventDto MapEvent(PayrollRunEvent e) =>
        new(e.Id, e.Action, e.ActorSubjectId, e.FromStatus, e.ToStatus, e.Reason, e.DetailsJson, e.CreatedAt);

    private static string Csv(object? value)
    {
        var text = value?.ToString() ?? "";
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    /// <summary>Employer statutory liability for a period, aggregated across all
    /// released (non-reversed) runs: ZRA PAYE, NAPSA EE+ER and NHIMA EE+ER.</summary>
    public async Task<EmployerLiabilityReportDto> EmployerLiabilityReportAsync(Guid payPeriodId, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var period = await repo.GetPeriodAsync(payPeriodId, ct)
            ?? throw new DomainException("pay-period-not-found", $"Pay period {payPeriodId} does not exist.");

        var lines = await repo.ListReleasedRunLinesForPeriodAsync(payPeriodId, ct);
        var components = (await repo.ListAllComponentsAsync(ct))
            .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // aggregate per statutory component code, split by payer
        var agg = new Dictionary<string, EmployerLiabilityRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            foreach (var lc in line.Components)
            {
                if (!lc.IsStatutory) continue;
                var payer = lc.ComponentType switch
                {
                    "employer-contribution" => "employer",
                    "deduction" => "employee",
                    "tax" => "employee",
                    _ => "other",
                };
                var key = $"{lc.ComponentCode}:{payer}";
                if (!agg.TryGetValue(key, out var row))
                {
                    components.TryGetValue(lc.ComponentCode, out var comp);
                    row = new EmployerLiabilityRow(lc.ComponentCode, lc.ComponentName, payer, 0, 0);
                    agg[key] = row;
                }
                agg[key] = row with { TotalAmount = row.TotalAmount + lc.Amount, WorkerCount = row.WorkerCount + 1 };
            }
        }
        var rows = agg.Values.OrderByDescending(r => r.TotalAmount).ToList();
        return new EmployerLiabilityReportDto(
            period.PeriodLabel,
            period.PeriodLabel.Contains("-") && period.PeriodLabel.Length >= 4 ? period.PeriodLabel[..4] : (period.PeriodLabel.Length >= 4 ? period.PeriodLabel[^4..] : ""),
            rows,
            rows.Where(r => r.Payer == "employer").Sum(r => r.TotalAmount),
            DateTimeOffset.UtcNow);
    }

    /// <summary>Generates a payslip PDF document and stores its URL on the
    /// payslip (idempotent — re-generating replaces the document).</summary>
    public async Task<PayslipDto> GeneratePayslipDocumentAsync(Guid payslipId, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var slip = await repo.GetPayslipAsync(payslipId, ct)
            ?? throw new DomainException("payslip-not-found", $"Payslip {payslipId} does not exist.");
        var line = await repo.GetRunLineForPayslipAsync(slip.Id, ct)
            ?? throw new DomainException("payslip-line-missing", $"Payslip {payslipId} has no run line.");

        slip.DocumentUrl = await payslipDocument.GenerateAsync(slip, line, ct);
        slip.Status = "final";
        await repo.UpdatePayslipAsync(slip, ct);
        return MapPayslip(slip);
    }

    // M34: admin payslip list for a specific run — returns real payslip IDs
    // so the UI can link from run detail to the actual payslip record.
    public async Task<List<PayslipDto>> ListRunPayslipsAsync(Guid runId, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var items = await repo.ListRunPayslipsAsync(runId, ct);
        return items.Select(MapPayslip).ToList();
    }

    // M34: bulk PDF generation for all payslips in a run. Idempotent — slips
    // that already have a DocumentUrl are returned unchanged; slips without
    // one are generated fresh.
    public async Task<List<PayslipDto>> GenerateAllPayslipDocumentsAsync(Guid runId, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var items = await repo.ListRunPayslipsAsync(runId, ct);
        if (items.Count == 0)
            throw new DomainException("run-has-no-payslips", $"Run {runId} has no payslip records. Payslips are created when the run is released.");
        var results = new List<PayslipDto>();
        foreach (var slip in items)
        {
            if (string.IsNullOrWhiteSpace(slip.DocumentUrl))
            {
                var line = await repo.GetRunLineForPayslipAsync(slip.Id, ct)
                    ?? throw new DomainException("payslip-line-missing", $"Payslip {slip.Id} has no run line.");
                slip.DocumentUrl = await payslipDocument.GenerateAsync(slip, line, ct);
                slip.Status = "final";
                await repo.UpdatePayslipAsync(slip, ct);
            }
            results.Add(MapPayslip(slip));
        }
        return results;
    }

    // M34: raw PDF bytes for inline preview. Generates the document on demand
    // if it does not exist yet.
    public async Task<byte[]> GetPayslipPreviewAsync(Guid payslipId, CancellationToken ct)
    {
        authz.RequireAnyRole("payroll", "hr_admin");
        var slip = await repo.GetPayslipAsync(payslipId, ct)
            ?? throw new DomainException("payslip-not-found", $"Payslip {payslipId} does not exist.");
        return await EnsurePayslipPdfBytesAsync(slip, payslipId, ct);
    }

    private async Task<byte[]> EnsurePayslipPdfBytesAsync(Payslip slip, Guid payslipId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slip.DocumentUrl))
        {
            var line = await repo.GetRunLineForPayslipAsync(slip.Id, ct)
                ?? throw new DomainException("payslip-line-missing", $"Payslip {payslipId} has no run line.");
            slip.DocumentUrl = await payslipDocument.GenerateAsync(slip, line, ct);
            slip.Status = "final";
            await repo.UpdatePayslipAsync(slip, ct);
        }
        // Download the PDF from durable storage and return raw bytes.
        if (slip.DocumentUrl!.StartsWith("file://"))
        {
            // Local file: read directly from filesystem.
            var localPath = slip.DocumentUrl["file://".Length..];
            if (!File.Exists(localPath))
                throw new DomainException("payslip-file-missing", $"Payslip PDF file not found at {localPath}");
            return await File.ReadAllBytesAsync(localPath, ct);
        }
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var bytes = await httpClient.GetByteArrayAsync(slip.DocumentUrl, ct);
        return bytes;
    }

    public async Task<Paged<PayslipDto>> GetPayslipsAsync(Guid workerId, string? callerSubject = null, CancellationToken ct = default)
    {
        authz.RequireAnyRole("employee", "payroll", "hr_admin");
        // M25 ownership guard: an employee can only ever read their own
        // payslips through the shared endpoint; HR roles keep broad access.
        if (authz.IsRole("employee") && !authz.IsRole("payroll", "hr_admin", "hr_ops"))
            await GuardOwnWorkerAsync(workerId, callerSubject, ct, "payslips");
        var (items, total) = await repo.ListPayslipsAsync(workerId, ct);
        // M44 branch scoping: scoped operators see only their branch's payslips.
        if ((scope?.IsScopedToBranch ?? false) && authz.IsRole("payroll", "hr_admin"))
        {
            items = items.Where(s => s.LocationId == scope?.LocationId || s.LocationId == null).ToList();
            total = items.Count;
        }
        return new Paged<PayslipDto>(items.Select(MapPayslip).ToList(), total, 1, 50);
    }

    public async Task<PayslipDto?> GetPayslipByIdAsync(Guid id, string? callerSubject = null, CancellationToken ct = default)
    {
        authz.RequireAnyRole("employee", "payroll", "hr_admin");
        var slip = await repo.GetPayslipAsync(id, ct);
        if (slip is null) return null;
        // M25 ownership guard: an employee can only ever read their own slip.
        if (authz.IsRole("employee") && !authz.IsRole("payroll", "hr_admin", "hr_ops"))
            await GuardOwnWorkerAsync(slip.WorkerId, callerSubject, ct, "payslip");
        return MapPayslip(slip);
    }

    public async Task<Paged<PayslipDto>> GetMyPayslipsAsync(string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "payroll", "hr_admin", "manager", "hr_ops");
        if (string.IsNullOrEmpty(subjectId))
            throw new DomainException("no-subject-claim", "The request carries no identity claim.");
        var worker = await repo.GetWorkerBySubjectAsync(subjectId, ct);
        if (worker is null)
            return new Paged<PayslipDto>([], 0, 1, 50);
        var (items, total) = await repo.ListPayslipsAsync(worker.Id, ct);
        return new Paged<PayslipDto>(items.Select(MapPayslip).ToList(), total, 1, 50);
    }

    public async Task<PayslipDto?> GetMyPayslipByIdAsync(Guid id, string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "payroll", "hr_admin", "manager", "hr_ops");
        if (string.IsNullOrEmpty(subjectId))
            throw new DomainException("no-subject-claim", "The request carries no identity claim.");
        var worker = await repo.GetWorkerBySubjectAsync(subjectId, ct);
        var slip = await repo.GetPayslipAsync(id, ct);
        if (slip is null) return null;
        // An impostor or unlinked caller can never see a slip that belongs to
        // someone else (and gets no hint about its existence).
        if (worker is null || slip.WorkerId != worker.Id)
            throw new DomainException("payslip-not-owned", "The payslip does not belong to the signed-in worker.");
        return MapPayslip(slip);
    }

    public async Task<string> GetMyPayslipDownloadUrlAsync(Guid id, string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "payroll", "hr_admin", "manager", "hr_ops");
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new DomainException("no-subject-claim", "The request carries no identity claim.");
        var worker = await repo.GetWorkerBySubjectAsync(subjectId, ct);
        var slip = await repo.GetPayslipAsync(id, ct)
            ?? throw new DomainException("payslip-not-found", $"Payslip {id} does not exist.");
        if (worker is null || slip.WorkerId != worker.Id)
            throw new DomainException("payslip-not-owned", "The payslip does not belong to the signed-in worker.");
        if (string.IsNullOrWhiteSpace(slip.DocumentUrl))
        {
            var line = await repo.GetRunLineForPayslipAsync(slip.Id, ct)
                ?? throw new DomainException("payslip-line-missing", $"Payslip {id} has no run line.");
            slip.DocumentUrl = await payslipDocument.GenerateAsync(slip, line, ct);
            slip.Status = "final";
            await repo.UpdatePayslipAsync(slip, ct);
        }
        return slip.DocumentUrl;
    }

    public async Task<byte[]> GetMyPayslipPreviewAsync(Guid id, string subjectId, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "payroll", "hr_admin", "manager", "hr_ops");
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new DomainException("no-subject-claim", "The request carries no identity claim.");
        var worker = await repo.GetWorkerBySubjectAsync(subjectId, ct);
        var slip = await repo.GetPayslipAsync(id, ct)
            ?? throw new DomainException("payslip-not-found", $"Payslip {id} does not exist.");
        if (worker is null || slip.WorkerId != worker.Id)
            throw new DomainException("payslip-not-owned", "The payslip does not belong to the signed-in worker.");
        return await EnsurePayslipPdfBytesAsync(slip, id, ct);
    }

    // M25: for an employee-only caller, resolve their own worker from the
    // token subject (supplied by the route layer) and confirm the worker id
    // matches their own record.
    private async Task GuardOwnWorkerAsync(Guid workerId, string? subjectId, CancellationToken ct, string resource)
    {
        // Only constrain a caller whose identity we know; an unknown subject
        // (test harness / unlinked caller) keeps the legacy broad-read path.
        if (string.IsNullOrEmpty(subjectId)) return;
        var own = await repo.GetWorkerBySubjectAsync(subjectId, ct);
        if (own is null || own.Id != workerId)
            throw new DomainException($"{resource}-not-owned", $"The {resource} does not belong to the signed-in worker.");
    }

    private static PayslipDto MapPayslip(Payslip p) => new(
        p.Id, p.PayslipNo, p.Version, p.GrossPay, p.TotalDeductions, p.NetPay,
        p.YtdGross, p.YtdTax, p.YtdNet, p.Status, p.DocumentUrl, p.ReleasedAt, p.SupersedesId,
        // M24: statutory identity pack snapshotted at payment time
        p.WorkerNrc, p.WorkerTpin, p.WorkerNapsaNumber, p.WorkerNhimaNumber,
        p.RunLine?.Worker?.FullName, p.RunLine?.Worker?.EmployeeNo,
        p.RunLine?.Run?.PayPeriod?.PeriodLabel, p.RunLine?.Run?.PayPeriod?.PayDate.ToString("yyyy-MM-dd"),
        p.RunLine?.RunId, "ZMW",
        p.RunLine?.Components.Select(c => new PayrollLineComponentDto(c.ComponentCode, c.ComponentName, c.ComponentType, c.Amount, c.Explanation, c.IsStatutory)).ToList(),
        p.LocationId);

    /// <summary>M24: per-worker statutory identity readiness for the run. The
    /// four Zambian identity references (NRC, TPIN, NAPSA, NHIMA) must all be
    /// present on every worker line before the run can be released.</summary>
    public async Task<StatutoryReadinessDto> GetRunStatutoryReadinessAsync(Guid id, CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "payroll", "hr_admin");
        var run = await repo.GetRunAsync(id, ct) ?? throw new DomainException("payroll-run-not-found", $"Run {id} does not exist.");
        var (items, _) = await repo.ListRunLinesAsync(id, ct);
        var workers = items.Select(l => l.Worker).Where(w => w is not null).Cast<Worker>().ToList();
        var item = workers.Select(w =>
        {
            var hasNrc = !string.IsNullOrWhiteSpace(w.Nrc);
            var hasTpin = !string.IsNullOrWhiteSpace(w.Tpin);
            var hasNapsa = !string.IsNullOrWhiteSpace(w.NapsaNumber);
            var hasNhima = !string.IsNullOrWhiteSpace(w.NhimaNumber);
            return new WorkerStatutoryItemDto(w!.Id, w.EmployeeNo, w.FullName, hasNrc, hasTpin, hasNapsa, hasNhima, hasNrc && hasTpin && hasNapsa && hasNhima);
        }).ToList();
        return new StatutoryReadinessDto(run.Id, run.PayPeriod?.PeriodLabel, item.All(x => x.Ready), item.Count, item);
    }

    private static PayrollRunDto MapRun(PayrollRun r) => new(
        r.Id, r.Status, r.PayPeriod?.PeriodLabel ?? "", r.EmployeeCount, r.TotalGross, r.TotalDeductions, r.TotalNet,
        r.TotalEmployerCost, r.ExceptionCount, r.CalcVersion, r.CreatedAt, r.IsReversal, r.ReversesRunId,
        r.PreparedBySubjectId, r.ApprovedBySubjectId, r.ReleasedBySubjectId, r.PaymentStatus,
        r.PaymentFileReference, r.PaymentFileGeneratedBySubjectId, r.PaymentApprovedBySubjectId, r.PaymentReleasedBySubjectId,
        r.ReconciliationReference, r.ReconciledAmount, r.ReconciledAt, r.LocationId,
        r.PayPeriodId, r.PayGroupId, r.ApprovalNote, r.IsHistorical, r.HistoricalReason);

    private static WorkerPayrollProfileDto MapProfile(WorkerPayrollProfile p) => new(
        p.Id, p.WorkerId, p.Worker?.FullName, p.PayGroupId, p.PayGroup?.Name, p.EffectiveFrom.ToString(),
        p.ComponentValues.Select(v => new WorkerComponentValueDto(v.ComponentId,
            v.Component?.Code ?? "", v.Component?.Name ?? "", v.Amount)).ToList(),
        p.PayBasis ?? "salary", p.OvertimeCategory ?? "ordinary",
        p.WeeklyOvertimeThresholdHours <= 0 ? 48 : p.WeeklyOvertimeThresholdHours,
        p.MonthlyOvertimeDivisor <= 0 ? 208 : p.MonthlyOvertimeDivisor);

    private static SalaryAdvanceDto MapAdvance(SalaryAdvance a, decimal recovered)
    {
        recovered = Math.Round(Math.Max(0, recovered), 2);
        var remaining = Math.Round(Math.Max(0, a.Amount - recovered), 2);
        return new SalaryAdvanceDto(a.Id, a.WorkerId, a.Worker?.FullName ?? $"Employee {a.WorkerId}",
            a.Worker?.EmployeeNo, a.Amount, a.InstallmentAmount, recovered, remaining,
            a.Currency, a.IssueDate.ToString("yyyy-MM-dd"), a.DeductionStartDate.ToString("yyyy-MM-dd"),
            a.DeductFromPayslip, remaining <= 0 && a.Status == "active" ? "settled" : a.Status,
            a.Reason, a.Reference, a.CreatedAt);
    }

    private async Task<string> GetComponentCode(Guid componentId, CancellationToken ct)
    {
        var c = await repo.GetComponentByIdAsync(componentId, ct);
        return c?.Code ?? "";
    }
}

/// <summary>In-memory component evaluator for one worker in one run (unit-testable).</summary>
internal sealed class CalcContext
{
    public decimal Gross;
    public decimal Deductions;
    public decimal EmployerCost;
    public int WorkingDays;
    public int PaymentDays;
    public string? ProrationNote;
    public string? ExceptionReason;
    public readonly List<(string Code, string Name, string Type, decimal Amount, string Explanation, bool IsStatutory)> Components = [];
    private readonly Dictionary<string, decimal> _values = new();
    private readonly Worker _worker;
    private readonly WorkerPayrollProfile _profile;
    private readonly List<SalaryComponent> _components;
    private readonly List<ContributionRule> _rules;
    private readonly List<TaxSlab> _slabs;

    public CalcContext(Worker worker, WorkerPayrollProfile profile, List<SalaryComponent> components, List<ContributionRule> rules, List<TaxSlab> slabs)
    {
        _worker = worker; _profile = profile; _components = components; _rules = rules; _slabs = slabs;
    }

    public void Evaluate(SalaryComponent comp)
    {
        decimal amount = comp.CalculationBasis switch
        {
            // fixed basis: structure default first, then worker profile override
            "fixed" => ProfileAmountOrDefault(comp),
            "percent-of" => Resolve(comp.BasisComponentCode ?? "") * (comp.Rate ?? 0) / 100m,
            "slab" => ApplySlabs(Resolve(comp.BasisComponentCode ?? "basic")),
            _ => 0,
        };

        // M41 Gap 2: prorate salary-earned amounts when the worker did not earn
        // pay for the full period (mid-month starter/leaver or unpaid leave).
        // Only FIXED-basis components are scaled here — percent-of and slab
        // components read their bases through Resolve, which already returns
        // the prorated earnings, so scaling them again would double-prorate.
        // PAYE slabs follow the prorated gross automatically and statutory
        // rules re-apply floors/ceilings afterwards (e.g. NHIMA monthly
        // minimum).
        if (comp.CalculationBasis == "fixed") amount = ApplyProration(comp, amount);

        // statutory rules override (NAPSA ceiling example: min(pay*rate, ceiling)).
        // A rule is tied to an EARNING component (TiedComponentCode) and applies to
        // the deduction/tax/contribution component whose BasisComponentCode matches it.
        // Rule.Code must match the statutory component's code; TiedComponentCode
        // identifies the earning basis the percentage is taken from.
        var rule = comp.ComponentType != "earning"
            ? _rules.FirstOrDefault(r => r.Code == comp.Code && r.IsActive)
            : null;
        if (rule is not null)
        {
            var basis = Resolve(rule.TiedComponentCode ?? "");
            amount = basis * rule.Rate / 100m;
            if (rule.Ceiling.HasValue) amount = Math.Min(amount, rule.Ceiling.Value);
            if (rule.Floor.HasValue) amount = Math.Max(amount, rule.Floor.Value);
        }

        _values[comp.Code] = amount;
        Components.Add((comp.Code, comp.Name, comp.ComponentType, Math.Round(amount, 2),
            BuildExplanation(comp, amount), comp.IsStatutory));

        // Earnings must resolve before dependent components read them, so the
        // proration factor is baked into the resolved value immediately.
        if (comp.ComponentType == "earning")
        {
            _values[comp.Code] = amount;
            if (comp.IsTaxable) _taxableEarnings += amount;
            Gross += amount;
        }
        else if (comp.ComponentType is "deduction" or "tax") Deductions += amount;
        else if (comp.ComponentType == "employer-contribution") EmployerCost += amount;
    }

    private decimal Resolve(string code) =>
        code switch
        {
            "gross" => Gross,
            "taxable" => _taxableEarnings,
            // An earning already evaluated in this run carries its prorated
            // value in _values — that always wins over the raw profile
            // override, so dependent components read prorated earnings.
            _ when _values.ContainsKey(code) => _values[code],
            _ => _profile.ComponentValues.FirstOrDefault(v => v.Component?.Code == code)?.Amount ?? 0,
        };

    private decimal ProfileAmount(Guid componentId) =>
        _profile.ComponentValues.FirstOrDefault(v => v.ComponentId == componentId)?.Amount ?? 0;

    // A worker profile is an explicit override. Use the component default only
    // when this profile has no value for the component; this also preserves an
    // intentional zero override.
    private decimal ProfileAmountOrDefault(SalaryComponent comp) =>
        _profile.ComponentValues.Any(v => v.ComponentId == comp.Id)
            ? ProfileAmount(comp.Id)
            : comp.FixedAmount ?? 0;

    private decimal _lastTaxable;
    // PAYE follows chargeable/taxable emoluments, which can differ from gross
    // statutory earnings used by NAPSA.
    private decimal _taxableEarnings;

    private decimal ApplySlabs(decimal taxable)
    {
        _lastTaxable = taxable;
        decimal tax = 0;
        foreach (var slab in _slabs.Where(s => s.IsActive).OrderBy(s => s.Sequence))
        {
            if (taxable <= slab.MinAmount) break;
            var upper = slab.MaxAmount ?? taxable;
            var band = Math.Min(taxable, upper) - slab.MinAmount;
            if (band > 0) tax += band * slab.Rate / 100m;
        }
        return tax;
    }

    private string BuildExplanation(SalaryComponent comp, decimal amount) =>
        comp.CalculationBasis switch
        {
            "percent-of" => $"{comp.Rate}% of {comp.BasisComponentCode ?? "basis"}",
            "slab" => $"Progressive PAYE slab calculation on taxable income K{_lastTaxable:N2} (ZRA bands)",
            _ => comp.Ceiling.HasValue ? $"Fixed/capped at ceiling {comp.Ceiling}" : "Fixed amount",
        };

    // M41 Gap 2: proration. Salary-earned components (earnings, deductions that
    // follow earnings, employer contributions) scale with the payment-days
    // factor; slab tax follows the prorated gross automatically and statutory
    // floors/ceilings re-apply afterwards.
    private decimal ApplyProration(SalaryComponent comp, decimal amount)
    {
        if (WorkingDays <= 0 || PaymentDays >= WorkingDays) return amount;
        var factor = (decimal)PaymentDays / WorkingDays;
        return Math.Round(amount * factor, 2);
    }

    public void AddOvertime(List<AttendanceRecord> records)
    {
        if (records.Count == 0) return;
        var hours = records.Sum(r => r.OvertimeHours);
        if (hours <= 0) return;
        var basic = Resolve("basic");
        var amount = records.Sum(r =>
        {
            var divisor = r.OvertimeHourlyDivisor > 0 ? r.OvertimeHourlyDivisor :
                (_profile.MonthlyOvertimeDivisor > 0 ? _profile.MonthlyOvertimeDivisor : 208m);
            return basic / divisor * r.OvertimeHours * r.OvertimeMultiplier;
        });
        amount = Math.Round(amount, 2);
        if (amount <= 0) return;
        _values["overtime"] = amount;
        var sourceIds = string.Join(", ", records.Select(r => r.Id.ToString("D")));
        Components.Add(("overtime", "Overtime", "earning", amount,
            $"{hours:N2} approved attendance overtime hour(s), weighted by recorded shift multiplier and configured hourly divisor; source attendance {sourceIds}", false));
        Gross += amount;
        _taxableEarnings += amount;
    }

    public void AddPayrollBenefits(List<WorkerBenefitAllowance> allowances)
    {
        foreach (var allowance in allowances
            .Where(a => a.AnnualAmount > 0 && a.BenefitType?.IncludeInPayroll == true && a.BenefitType.IsActive)
            .OrderBy(a => a.BenefitType!.Name, StringComparer.OrdinalIgnoreCase))
        {
            var type = allowance.BenefitType!;
            var monthlyAmount = Math.Round(allowance.AnnualAmount / 12m, 2);
            if (WorkingDays > 0 && PaymentDays < WorkingDays)
                monthlyAmount = Math.Round(monthlyAmount * PaymentDays / WorkingDays, 2);
            if (monthlyAmount <= 0) continue;
            var code = $"benefit-{type.Code.Trim().ToLowerInvariant()}";
            _values[code] = monthlyAmount;
            Components.Add((code, type.Name, "earning", monthlyAmount,
                WorkingDays > 0 && PaymentDays < WorkingDays
                    ? $"Payroll benefit from {type.Name}: annual allowance K{allowance.AnnualAmount:N2} / 12 months, prorated for {PaymentDays:N0}/{WorkingDays:N0} paid days."
                    : $"Payroll benefit from {type.Name}: annual allowance K{allowance.AnnualAmount:N2} / 12 months.", false));
            Gross += monthlyAmount;
            if (type.IsTaxable) _taxableEarnings += monthlyAmount;
        }
    }

    public void AddSalaryAdvances(List<SalaryAdvance> advances, Dictionary<Guid, decimal> recoveredByAdvance)
    {
        foreach (var advance in advances
            .Where(a => a.Status == "active" && a.DeductFromPayslip && a.Amount > 0 && a.InstallmentAmount > 0)
            .OrderBy(a => a.DeductionStartDate)
            .ThenBy(a => a.CreatedAt))
        {
            var recovered = recoveredByAdvance.TryGetValue(advance.Id, out var paid) ? paid : 0m;
            var remaining = Math.Round(Math.Max(0, advance.Amount - recovered), 2);
            if (remaining <= 0) continue;
            var deduction = Math.Round(Math.Min(advance.InstallmentAmount, remaining), 2);
            if (deduction <= 0) continue;
            var code = $"salary-advance-{advance.Id:N}";
            Components.Add((code, "Salary advance recovery", "deduction", deduction,
                $"Salary advance deduction: K{deduction:N2} of K{advance.Amount:N2}; K{Math.Max(0, remaining - deduction):N2} remains after this payroll. Reference {advance.Reference ?? advance.Id.ToString("D")}.", false));
            Deductions += deduction;
        }
    }

    public void SetProration(int workingDays, int paymentDays, string? note)
    {
        WorkingDays = workingDays;
        PaymentDays = paymentDays;
        ProrationNote = note;
    }
}

public interface IPayrollRepository
{
    Task<List<PayGroup>> ListPayGroupsAllAsync(CancellationToken ct);
    Task<PayGroup?> GetPayGroupAsync(Guid id, CancellationToken ct);
    Task<List<SalaryComponent>> ListAllComponentsAsync(CancellationToken ct);
    Task<SalaryComponent?> GetComponentByIdAsync(Guid id, CancellationToken ct);
    Task<List<WorkerPayrollProfile>> ListProfilesAsync(Guid? workerId, CancellationToken ct);
    Task<WorkerPayrollProfile?> FindOpenProfileAsync(Guid workerId, CancellationToken ct);
    Task<WorkerPayrollProfile> CreateProfileAsync(WorkerPayrollProfile profile, CancellationToken ct);
    Task<WorkerPayrollProfile> UpdateProfileAsync(WorkerPayrollProfile profile, CancellationToken ct);
    Task DeleteProfileValuesAsync(Guid profileId, CancellationToken ct);
    Task<SalaryStructure?> FindStructureAsync(string code, CancellationToken ct);
    Task<SalaryStructure?> FindStructureByCodeAsync(string code, CancellationToken ct);
    Task<List<SalaryStructure>> ListStructuresAsync(CancellationToken ct);
    Task<SalaryStructure?> GetStructureAsync(Guid id, CancellationToken ct);
    Task<SalaryStructure> CreateStructureAsync(SalaryStructure structure, CancellationToken ct);
    Task UpdateStructureAsync(SalaryStructure structure, CancellationToken ct);
    Task ClearStructureItemsAsync(Guid structureId, CancellationToken ct);
    Task SetStructureItemsExplicitlyAsync(SalaryStructure structure, List<SalaryStructureItem> items, CancellationToken ct);
    Task<Worker?> GetWorkerAsync(Guid id, CancellationToken ct);
    // M25: resolve the worker linked to a Keycloak subject (for self-service).
    Task<Worker?> GetWorkerBySubjectAsync(string subjectId, CancellationToken ct);
    Task<List<SalaryComponent>> ListComponentsAsync(string? type, CancellationToken ct);
    Task<List<PayGroup>> ListPayGroupsAsync(CancellationToken ct);
    Task<List<PayPeriod>> ListPeriodsAsync(Guid payGroupId, CancellationToken ct);
    Task<PayPeriod?> GetPeriodAsync(Guid id, CancellationToken ct);
    Task<List<WorkerBenefitAllowance>> LoadPayrollBenefitAllowancesAsync(Guid payPeriodId, Guid? locationId, CancellationToken ct);
    Task<List<SalaryAdvance>> ListSalaryAdvancesAsync(Guid? workerId, string? status, CancellationToken ct);
    Task<SalaryAdvance?> GetSalaryAdvanceAsync(Guid id, CancellationToken ct);
    Task<SalaryAdvance> CreateSalaryAdvanceAsync(SalaryAdvance advance, CancellationToken ct);
    Task UpdateSalaryAdvanceAsync(SalaryAdvance advance, CancellationToken ct);
    Task<List<SalaryAdvance>> LoadDeductibleSalaryAdvancesAsync(Guid payPeriodId, Guid? locationId, CancellationToken ct);
    Task<Dictionary<Guid, decimal>> GetSalaryAdvanceRecoveredAmountsAsync(List<Guid> advanceIds, CancellationToken ct);
    Task<List<TaxSlab>> ListTaxSlabsAsync(string taxYear, CancellationToken ct);
    Task<TaxSlab?> GetTaxSlabAsync(Guid id, CancellationToken ct);
    Task UpdateTaxSlabAsync(TaxSlab slab, CancellationToken ct);
    Task<List<ContributionRule>> ListContributionRulesAsync(CancellationToken ct);
    Task<ContributionRule?> GetContributionRuleAsync(Guid id, CancellationToken ct);
    Task UpdateContributionRuleAsync(ContributionRule rule, CancellationToken ct);
    Task UnsetDefaultPayGroupsAsync(CancellationToken ct, Guid keepId);
    // M50: wizard provisioning — the setup wizard creates the first pay group
    // and provisions statutory components, contribution rules, tax slabs and
    // the opening pay period so payroll can run end-to-end immediately.
    Task<PayGroup> CreatePayGroupAsync(PayGroup group, CancellationToken ct);
    Task<SalaryComponent> CreateComponentAsync(SalaryComponent component, CancellationToken ct);
    Task<ContributionRule> CreateContributionRuleAsync(ContributionRule rule, CancellationToken ct);
    Task<TaxSlab> CreateTaxSlabAsync(TaxSlab slab, CancellationToken ct);
    Task<PayPeriod> CreatePeriodAsync(PayPeriod period, CancellationToken ct);
    // M50: workers freshly created by the wizard import — matched back to the
    // mapped spreadsheet rows so a payroll profile can be attached to each.
    Task<List<Worker>> ListWorkersCreatedAfterAsync(DateTimeOffset since, CancellationToken ct);
    Task UpdatePayGroupAsync(PayGroup group, CancellationToken ct);
    Task UpdateComponentAsync(SalaryComponent component, CancellationToken ct);
    Task<PayrollRun?> GetRunAsync(Guid id, CancellationToken ct);
    Task<List<PayrollRun>> ListRunsAsync(CancellationToken ct);
    // M48: runs awaiting approval pipeline action (in-review, or a calculated branch run not yet submitted). Each row
    // carries the branch name and the moment the run was submitted, already resolved in one repository call.
    Task<List<(PayrollRun Run, string? BranchName, string? LegalEntityId, DateTimeOffset? SubmittedAt)>> ListRunsInReviewAsync(CancellationToken ct);
    Task<PayrollRun?> FindRunByPeriodAsync(Guid payPeriodId, CancellationToken ct);
    Task<PayrollRun?> FindOpenRunByPeriodAndLocationAsync(Guid payPeriodId, Guid? locationId, CancellationToken ct);
    Task<PayrollRun?> FindOpenBranchRunForPeriodAsync(Guid payPeriodId, CancellationToken ct);
    // M46: the open (non-terminal) run for a pay period scoped to one branch;
    // locationId = null returns an open organisation-wide run. Branch drafts
    // may coexist for the same period across different branches.
    Task<PayrollRun> CreateRunAsync(PayrollRun run, CancellationToken ct);
    Task<PayrollRun> UpdateRunAsync(PayrollRun run, CancellationToken ct);
    Task<PayrollRun> SubmitRunAsync(PayrollRun run, CancellationToken ct);
    Task<(List<WorkerPayrollProfile> Profiles, List<SalaryComponent> Components, List<ContributionRule> Rules, List<TaxSlab> Slabs, DateOnly? Cutoff)> LoadCalculationInputsAsync(Guid payPeriodId, CancellationToken ct, Guid? locationId = null);
    Task<List<AttendanceRecord>> LoadApprovedOvertimeAsync(Guid payPeriodId, Guid? locationId, CancellationToken ct);
    Task LinkOvertimeToPayrollAsync(Guid attendanceId, Guid runId, Guid runLineId, CancellationToken ct);
    Task ClearRunLinesAsync(Guid runId, CancellationToken ct);
    Task AddRunLineAsync(PayrollRunLine line, CancellationToken ct);
    Task<(List<PayrollRunLine> Items, int Total)> ListRunLinesAsync(Guid runId, CancellationToken ct);
    Task<PayrollRunLine?> GetRunLineAsync(Guid runId, Guid lineId, CancellationToken ct);
    Task UpdateRunLineAsync(PayrollRunLine line, CancellationToken ct);
    Task RecalculateRunTotalsAsync(PayrollRun run, CancellationToken ct);
    Task<List<PayrollPaymentRow>> ListPaymentRowsAsync(Guid runId, CancellationToken ct);
    Task AddRunEventAsync(PayrollRunEvent item, CancellationToken ct);
    Task<List<PayrollRunEvent>> ListRunEventsAsync(Guid runId, CancellationToken ct);
    Task<List<PayslipNotificationTarget>> FinalizePayslipsAsync(Guid runId, CancellationToken ct);
    Task<int> SupersedeOriginalPayslipsAsync(Guid originalRunId, CancellationToken ct);
    Task<PayrollRun?> FindRunByReversesIdAsync(Guid reversesRunId, CancellationToken ct);
    Task<List<PayrollRunLine>> ListReleasedRunLinesForPeriodAsync(Guid payPeriodId, CancellationToken ct);
    Task<PayrollRunLine?> GetRunLineForPayslipAsync(Guid payslipId, CancellationToken ct);
    Task UpdatePayslipAsync(Payslip payslip, CancellationToken ct);
    Task<(List<Payslip> Items, int Total)> ListPayslipsAsync(Guid workerId, CancellationToken ct);
    Task<Payslip?> GetPayslipAsync(Guid id, CancellationToken ct);
    Task<Payslip?> GetCurrentPayslipForRunLineAsync(Guid runLineId, CancellationToken ct);
    Task<Payslip> CreatePayslipAsync(Payslip payslip, CancellationToken ct);
    // M34: payslips for a specific run (HR admin surface).
    Task<List<Payslip>> ListRunPayslipsAsync(Guid runId, CancellationToken ct);
    // M41: legal entities for payroll report company headers.
    Task<List<LegalEntity>> ListLegalEntitiesAsync(CancellationToken ct);
    // M41 Gap 2: proration inputs — pay period dates, approved unpaid leave
    // requests overlapping the period (with leave-type category), and the
    // effective work calendar. Public holidays remain paid days.
    Task<PayrollProrationInputs> LoadProrationInputsAsync(Guid payPeriodId, CancellationToken ct);
}

/// <summary>M41 Gap 2: proration inputs snapshot for one pay period.</summary>
public sealed record PayrollProrationInputs(
    DateOnly PeriodStart, DateOnly PeriodEnd,
    List<ApprovedUnpaidLeave> UnpaidLeaves,
    List<DateOnly> HolidayDates,
    string WeekendDays = "sat,sun");

/// <summary>One approved leave of an unpaid leave type overlapping the period.</summary>
public sealed record ApprovedUnpaidLeave(Guid WorkerId, DateOnly StartDate, DateOnly EndDate, decimal RequestedDays);

public sealed record PayrollPaymentRow(Guid WorkerId, string EmployeeNo, string WorkerName,
    string BankName, string BranchCode, string AccountName, string AccountNumber, decimal Amount);
