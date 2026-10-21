// M41 Gap 4 — bulk assignment of payroll profiles (pay structures) via the
// shared import/export tool. One spreadsheet row per worker: identity columns
// locate the worker (non-negotiable — the engine refuses rows without a
// resolvable employee number / NRC / NAPSA number), and each active salary
// component becomes its own mappable column so HR can assign or overwrite
// basic salary and allowances for many workers in one batch. Insert mode
// creates new profiles; Update mode patches only the components the file
// supplies, so a partially filled export-style file is round-trip safe.
using System.Globalization;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Application.Shared;

public sealed class PayrollProfilesImportSchema : IImportSchema
{
    // NOTE: profile persistence goes through IPayrollService.UpsertProfileAsync
    // (the same service the UI uses), not the raw repository — its normalisation
    // keeps component ids codes-consistent with the rest of the engine.
    private readonly IPayrollRepository repo;
    private readonly IPayrollService payroll;
    private readonly IWorkerRepository workers;
    private readonly IAuthzService authz;

    public PayrollProfilesImportSchema(IPayrollRepository repo, IPayrollService payroll,
        IWorkerRepository workers, IAuthzService authz)
    {
        this.repo = repo;
        this.payroll = payroll;
        this.workers = workers;
        this.authz = authz;
    }

    public string TypeKey => "payroll-profiles";
    public string DisplayName => "Payroll profiles";

    // Components are loaded lazily once per schema instance (same lazy-scan
    // pattern as the workers identity sets), then each active component becomes
    // a mappable import column keyed by its canonical code.
    private List<SalaryComponent> Components = [];
    private List<ImportFieldDef> ComponentFields = [];
    private bool ComponentsLoaded;

    private void EnsureComponents()
    {
        if (ComponentsLoaded) return;
        ComponentsLoaded = true;
        Components = repo.ListAllComponentsAsync(CancellationToken.None).GetAwaiter().GetResult();
        ComponentFields =
        [
            new ImportFieldDef("employeeNo", "Employee number", true, NaturalKey: true, Example: "EMP-0008"),
            new ImportFieldDef("nrc", "NRC", false, NaturalKey: true, FormatNote: "e.g. 123456/78/1"),
            new ImportFieldDef("napsaNumber", "NAPSA number", false, NaturalKey: true, FormatNote: "e.g. NAPSA-001"),
            new ImportFieldDef("payGroup", "Pay group", false, Example: "Monthly ZMW"),
            new ImportFieldDef("effectiveFrom", "Effective from", false, FormatNote: "DD-MM-YYYY; defaults to today"),
            .. Components.Select(c => new ImportFieldDef(c.Code, c.Name, false, Example: c.FixedAmount?.ToString("F2") ?? "0")),
        ];
    }

    public List<ImportFieldDef> Fields
    {
        get { EnsureComponents(); return ComponentFields; }
    }

    public async Task<ImportRowOutcome> PreviewRowAsync(IDictionary<string, string> row, string mode, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        EnsureComponents();

        // ---- Identity: nothing processes without a resolvable worker. ----
        var employeeNo = row.Get("employeeNo").Trim();
        var nrc = row.Get("nrc").Trim();
        var napsa = row.Get("napsaNumber").Trim();
        if (string.IsNullOrWhiteSpace(employeeNo) && string.IsNullOrWhiteSpace(nrc) && string.IsNullOrWhiteSpace(napsa))
            return new ImportRowOutcome("error",
                "No worker identity on this row — provide an Employee number, NRC or NAPSA number before the row can be imported.");
        var worker = await workers.FindByNaturalKeyAsync(employeeNo, nrc, napsa, ct);
        if (worker is null)
        {
            var key = !string.IsNullOrWhiteSpace(employeeNo) ? $"employee number '{employeeNo}'"
                : !string.IsNullOrWhiteSpace(nrc) ? $"NRC '{nrc}'"
                : $"NAPSA number '{napsa}'";
            return new ImportRowOutcome("error", $"No employee matches {key}. Check the identifier.");
        }

        var existing = await repo.FindOpenProfileAsync(worker.Id, ct);
        var isUpdate = mode.Equals("update", StringComparison.OrdinalIgnoreCase);
        if (!isUpdate && existing is not null)
            return new ImportRowOutcome("error",
                $"Employee '{worker.FullName}' already has an active pay profile — switch the file to Update mode to change its values; Insert mode never overwrites.");

        // ---- Collect component amounts: blank means "no value in the row". ----
        var amounts = new List<(SalaryComponent Component, decimal Amount)>();
        foreach (var comp in Components)
        {
            var raw = row.Get(comp.Code);
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Any, null, out var amount))
                return new ImportRowOutcome("error", $"'{raw}' is not a valid amount for '{comp.Name}' — use a plain number, e.g. 25000.");
            if (amount < 0)
                return new ImportRowOutcome("error", $"'{raw}' is not a valid amount for '{comp.Name}' — amounts cannot be negative.");
            amounts.Add((comp, amount));
        }

        if (amounts.Count == 0)
            return new ImportRowOutcome("error",
                "No component amounts supplied — the row has nothing to assign. Add at least one component column such as basic or housing-allowance.");

        // ---- The profile must point at a real pay group. ----
        var groupName = row.Get("payGroup");
        PayGroup? group = null;
        if (!string.IsNullOrWhiteSpace(groupName))
        {
            var groups = await repo.ListPayGroupsAsync(ct);
            group = groups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
            if (group is null)
                return new ImportRowOutcome("error", $"No pay group named '{groupName}' exists — create it first or spell it exactly.");
        }
        else
        {
            // Resolve lazily from DB — the default group is "the only pay group".
            var groups = await repo.ListPayGroupsAsync(ct);
            group = groups.FirstOrDefault();
            if (group is null)
                return new ImportRowOutcome("error", "No pay group exists — create one before importing payroll profiles.");
        }

        // ---- Cross-row uniqueness: never let one file write two profiles for
        // the same worker. ----
        if (!SeenWorkers.Add(worker.Id.ToString()))
            return new ImportRowOutcome("error", $"'{worker.FullName}' appears twice in this file — one row per employee, with every component on that row.");

        // ---- Effective date validation. ----
        var effectiveFrom = row.Get("effectiveFrom").Trim();
        if (!string.IsNullOrWhiteSpace(effectiveFrom))
        {
            if (!TryParseImportDate(effectiveFrom, out _))
                return new ImportRowOutcome("error", $"Effective date '{effectiveFrom}' is not valid — use DD-MM-YYYY, e.g. 01-08-2026.");
        }

        // Update mode on an existing profile: the service preserves existing
        // component values when a component is not present in the row; insert
        // mode creates the full profile with exactly the row's amounts.
        var status = existing is not null ? "update" : "create";

        var values = amounts
            .Select(a => new WorkerComponentValueCreate(a.Component.Id, a.Component.Code, a.Amount))
            .ToList();
        var effective = string.IsNullOrWhiteSpace(effectiveFrom)
            ? existing?.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow)
            : ParseImportDate(effectiveFrom);

        var resolved = new Dictionary<string, string>(row, StringComparer.OrdinalIgnoreCase)
        {
            ["__workerId"] = worker.Id.ToString(),
            ["__payGroupId"] = group.Id.ToString(),
            ["__effectiveFrom"] = effective.ToString("yyyy-MM-dd"),
        };
        foreach (var (comp, amount) in amounts) resolved[comp.Code] = amount.ToString("F2");
        return new ImportRowOutcome(status,
            existing is not null ? $"Existing profile: {worker.FullName}" : $"New profile for {worker.FullName}", resolved);
    }

    public async Task ApplyRowAsync(IDictionary<string, string> row, string mode, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_ops", "hr_admin");
        // Apply runs on a fresh schema instance (per-request DI) — the component
        // list is only populated when a preview/field request touched it first,
        // so reload here instead of trusting another request's lazy load.
        EnsureComponents();
        var workerId = Guid.Parse(row["__workerId"]);
        var payGroupId = Guid.Parse(row["__payGroupId"]);
        var effectiveFrom = DateOnly.ParseExact(row["__effectiveFrom"], "yyyy-MM-dd");

        var values = new List<WorkerComponentValueCreate>();
        foreach (var comp in Components)
        {
            if (row.TryGetValue(comp.Code, out var raw) && decimal.TryParse(raw, System.Globalization.NumberStyles.Any, null, out var amount))
                values.Add(new WorkerComponentValueCreate(comp.Id, comp.Code, amount));
        }
        if (values.Count == 0)
            throw new DomainException("import-empty-profile", "The row has no component amounts to apply.");

        // UpsertProfileAsync replaces the profile's component collection. Carry
        // forward columns omitted by an update file so a partial import does
        // not silently remove existing salary and allowance assignments.
        if (mode.Equals("update", StringComparison.OrdinalIgnoreCase))
        {
            var existing = await repo.FindOpenProfileAsync(workerId, ct);
            if (existing is not null)
            {
                var supplied = values.Select(v => v.ComponentId).ToHashSet();
                foreach (var current in existing.ComponentValues.Where(v => !supplied.Contains(v.ComponentId)))
                {
                    var component = Components.FirstOrDefault(c => c.Id == current.ComponentId);
                    if (component is not null)
                        values.Add(new WorkerComponentValueCreate(component.Id, component.Code, current.Amount));
                }
            }
        }

        var request = new WorkerPayrollProfileCreate(workerId, payGroupId, effectiveFrom.ToString("yyyy-MM-dd"), values);
        await payroll.UpsertProfileAsync(workerId, request, ct);
    }

    // ---- Batch-level tracking (cleared per schema instance). ----
    private readonly HashSet<string> SeenWorkers = new(StringComparer.OrdinalIgnoreCase);

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

    private static DateOnly ParseImportDate(string value) =>
        TryParseImportDate(value, out var date)
            ? date
            : throw new DomainException("import-date-invalid", $"Date '{value}' is not valid.");
}
