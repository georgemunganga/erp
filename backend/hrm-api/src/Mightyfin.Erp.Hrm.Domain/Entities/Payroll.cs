namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>J-group 07: A salary component — the atomic unit of a payslip.
/// Components are reusable, versioned, and mapped to tax/accounting treatment.</summary>
public class SalaryComponent : Entity, IVersioned
{
    public string Code { get; set; } = null!;          // basic | housing-allowance | transport | napsa-ee | nhima-ee | paye | loan-recovery
    public string Name { get; set; } = null!;
    public string ComponentType { get; set; } = null!; // earning | deduction | employer-contribution | tax
    public string CalculationBasis { get; set; } = "fixed"; // fixed | percent-of | formula | slab
    public string? BasisComponentCode { get; set; }    // when percent-of / formula references another component
    public decimal? Rate { get; set; }                 // percentage rate (0..100) when basis is percent-of
    public decimal? FixedAmount { get; set; }
    public decimal? Ceiling { get; set; }              // statutory ceiling (NAPSA)
    public bool IsTaxable { get; set; }
    public bool IsStatutory { get; set; }
    public string? GlAccountRef { get; set; }          // accounting integration
    public int Priority { get; set; } = 100;           // evaluation order in the engine
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>J-group 06: A reusable package of component assignments (which
/// components apply to which grade/worker class), versioned.</summary>
public class SalaryStructure : Entity, IVersioned
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public ICollection<SalaryStructureItem> Items { get; set; } = new List<SalaryStructureItem>();
}

public class SalaryStructureItem : Entity
{
    public Guid StructureId { get; set; }
    public SalaryStructure? Structure { get; set; }
    public Guid ComponentId { get; set; }
    public SalaryComponent? Component { get; set; }
    public decimal? DefaultAmount { get; set; }      // default fixed amount for this component
    public bool IsOptional { get; set; }
    public int Order { get; set; }
}

/// <summary>Worker-to-structure assignment: per-worker overrides of component
/// amounts (basic salary etc.), effective-dated.</summary>
public class WorkerPayrollProfile : Entity, IEffectiveDated
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }

    /// <summary>M41 Gap 3 pay-basis control: "salary" (default) or "timesheet".
    /// Timesheet-based pay is not implemented yet — this is a planning control
    /// letting HR mark which workers would be timesheet-paid when that mode
    /// arrives. Until then, every worker is paid on the salary basis.</summary>
    public string PayBasis { get; set; } = "salary"; // salary | timesheet
    /// <summary>Payroll overtime category. ordinary uses the Employment Code
    /// 48-hour weekly threshold and 208 monthly divisor; watchperson-guard uses
    /// 60 hours and 240. Values stay on the profile so this remains payroll
    /// configuration, not a hardcoded employee master assumption.</summary>
    public string OvertimeCategory { get; set; } = "ordinary"; // ordinary | watchperson-guard
    public decimal WeeklyOvertimeThresholdHours { get; set; } = 48;
    public decimal MonthlyOvertimeDivisor { get; set; } = 208;
    public Guid StructureId { get; set; }
    public SalaryStructure? Structure { get; set; }
    public Guid PayGroupId { get; set; }
    public PayGroup? PayGroup { get; set; }
    public ICollection<WorkerComponentValue> ComponentValues { get; set; } = new List<WorkerComponentValue>();
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

public class WorkerComponentValue : Entity
{
    public Guid ProfileId { get; set; }
    public WorkerPayrollProfile? Profile { get; set; }
    public Guid ComponentId { get; set; }
    public SalaryComponent? Component { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>M41 Gap 6b: flexible benefit claims. A BenefitType is an org-wide
/// claim category (e.g. medical reimbursement, airtime, transport) with an
/// optional default annual cap. A WorkerBenefitAllowance sets the annual
/// amount an individual worker may claim for a type. A BenefitClaim is a
/// single reimbursement request against that allowance.</summary>
public class BenefitType : Entity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    /// <summary>Optional org-level ceiling per worker per year when no
    /// worker-specific allowance exists. 0 = no cap configured.</summary>
    public decimal AnnualCap { get; set; }
    public bool RequiresEvidence { get; set; }
    /// <summary>When enabled, the worker's annual allowance is paid through
    /// payroll as a monthly earning. When disabled, it remains claim-only.</summary>
    public bool IncludeInPayroll { get; set; }
    /// <summary>Payroll-paid cash or taxable benefits are included in PAYE
    /// taxable emoluments. Genuine non-cash exempt benefits remain false.</summary>
    public bool IsTaxable { get; set; }
    public bool IsActive { get; set; } = true;
}

public class WorkerBenefitAllowance : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public Guid BenefitTypeId { get; set; }
    public BenefitType? BenefitType { get; set; }
    /// <summary>Annual claimable amount for this worker and type. 0 = nothing claimable.</summary>
    public decimal AnnualAmount { get; set; }
    public int Year { get; set; }
}

public class BenefitClaim : Entity
{
    public Guid? PayPeriodId { get; set; }
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    // M44 branch scoping: branch the claim belongs to; null = global.
    public Guid? LocationId { get; set; }
    public Guid BenefitTypeId { get; set; }
    public BenefitType? BenefitType { get; set; }
    public decimal AmountClaimed { get; set; }
    public string Currency { get; set; } = "ZMW";
    public string? Note { get; set; }
    public bool EvidenceAttached { get; set; }
    public string Status { get; set; } = "submitted"; // submitted | approved | returned | rejected | paid
    public string? DecisionReason { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string? CreatedBySubjectId { get; set; }
    public string? DecidedBySubjectId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? PaidBySubjectId { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
}

/// <summary>Salary advance issued to an employee. When DeductFromPayslip is
/// enabled, payroll recovers the configured instalment from future payslips
/// until the advance amount has been fully recovered.</summary>
public class SalaryAdvance : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public decimal Amount { get; set; }
    public decimal InstallmentAmount { get; set; }
    public string Currency { get; set; } = "ZMW";
    public DateOnly? IssueDate { get; set; }
    public string RecordType { get; set; } = "salary-advance"; // salary-advance | loan
    public decimal OpeningRecoveredAmount { get; set; }
    public DateOnly DeductionStartDate { get; set; }
    public bool DeductFromPayslip { get; set; }
    public string Status { get; set; } = "active"; // active | settled | cancelled
    public string? Reason { get; set; }
    public string? Reference { get; set; }
    public string? CreatedBySubjectId { get; set; }
    public string? CancelledBySubjectId { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}

/// <summary>J-groups 03-04: Pay group defining frequency, calendar and currency.</summary>
public class PayGroup : Entity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Frequency { get; set; } = "monthly"; // monthly | semi-monthly | biweekly | weekly
    public string Currency { get; set; } = "ZMW";
    public int CalendarDayOfMonth { get; set; } = 25;  // standard payday
    public int InputCutoffDaysBeforePayday { get; set; } = 5;
    public bool IsDefault { get; set; }
}

/// <summary>J-group 03: Payroll periods created from the group's calendar.</summary>
public class PayPeriod : Entity
{
    public Guid PayGroupId { get; set; }
    public PayGroup? PayGroup { get; set; }
    public string PeriodLabel { get; set; } = null!;  // e.g. "Aug 2026"
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly CutoffDate { get; set; }
    public DateOnly PayDate { get; set; }
    public string Status { get; set; } = "open";      // open | locked | closed
    public bool IsCurrent { get; set; }
    // Historical periods are deliberately separate from the operational calendar:
    // creating one never reopens a closed period or changes its cutoff.
    public bool IsHistorical { get; set; }
    public string? HistoricalReason { get; set; }
}

/// <summary>J-group 11: Versioned tax slabs (ZRA PAYE) — configuration, not code
/// constants. Slabs are progressive: each bracket applies its rate to the band.</summary>
public class TaxSlab : Entity, IVersioned
{
    public string TaxYear { get; set; } = null!;      // e.g. "2026"
    public decimal MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }           // null = top band
    public decimal Rate { get; set; }                 // percent
    public int Sequence { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

/// <summary>J-group 10: Employee/employer statutory contribution rules (NAPSA,
/// NHIMA, WCFCB) with ceilings, floors and exemptions.</summary>
public class ContributionRule : Entity, IVersioned
{
    public string Code { get; set; } = null!;         // napsa-ee | napsa-er | nhima | wcfcb
    public string Name { get; set; } = null!;
    public string Payer { get; set; } = null!;        // employee | employer
    public decimal Rate { get; set; }
    public decimal? Ceiling { get; set; }
    public decimal? Floor { get; set; }
    public string? TiedComponentCode { get; set; }    // earning basis, e.g. "basic"
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}
