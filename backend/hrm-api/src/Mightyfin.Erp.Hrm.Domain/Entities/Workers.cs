namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>HRM-015: The person record. A worker may be an internal employee
/// (subject_id links to the Keycloak workforce realm) or an external/contingent
/// worker without an identity account — this is the party unification point.</summary>
public class Worker : Entity
{
    public string EmployeeNo { get; set; } = null!;            // tenant-scoped numbering sequence
    public string FirstName { get; set; } = null!;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = null!;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string? PreferredName { get; set; }
    public string? Email { get; set; }
    public string? PersonalEmail { get; set; }
    public string? Phone { get; set; }
    public string? PhotoUrl { get; set; }

    // Statutory / Zambian identity pack
    public string? Nrc { get; set; }           // national registration card
    public string? PassportNo { get; set; }
    public string? Tpin { get; set; }
    public string? NapsaNumber { get; set; }
    public string? NhimaNumber { get; set; }
    public string? Nationality { get; set; } = "Zambian";
    public string? DateOfBirth { get; set; }   // ISO date string; kept string to allow unknown/partial

    // Identity correlation (optional — contingent workers have none)
    public string? SubjectId { get; set; }
    public string WorkerType { get; set; } = "employee"; // employee | contingent | intern | volunteer
    public string Status { get; set; } = "pre-hire";     // pre-hire | active | on-leave | notice | terminated

    // Current assignment (denormalized read view; source of truth is Employment/Assignment)
    public Guid? OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }
    public Guid? LocationId { get; set; }
    public WorkLocation? Location { get; set; }
    public Guid? ManagerId { get; set; }
    public Worker? Manager { get; set; }
    public string? Grade { get; set; }
    public string? JobTitle { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    // M35: self-service notification preferences — JSON blob with channel and
    // topic toggles. Admins write through the admin profile page; employees
    // write through /me/preferences. Null = use organisation defaults.
    public string? NotificationPreferences { get; set; }

    public ICollection<EmergencyContact> EmergencyContacts { get; set; } = new List<EmergencyContact>();
    public ICollection<WorkerBankDetail> BankDetails { get; set; } = new List<WorkerBankDetail>();
    public ICollection<WorkerEducation> Education { get; set; } = new List<WorkerEducation>();
    public ICollection<ExternalWorkHistory> ExternalWorkHistory { get; set; } = new List<ExternalWorkHistory>();
    public ICollection<InternalWorkHistory> InternalWorkHistory { get; set; } = new List<InternalWorkHistory>();
}

/// <summary>M33: Education record for a worker.</summary>
public class WorkerEducation : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public string Institution { get; set; } = null!;     // school / university name
    public string Qualification { get; set; } = null!;   // e.g. Bachelor of Commerce
    public string? FieldOfStudy { get; set; }
    public string? Grade { get; set; }                   // result / class / GPA
    public int? StartYear { get; set; }
    public int? EndYear { get; set; }
}

/// <summary>M33: External (previous employer) work history for a worker.</summary>
public class ExternalWorkHistory : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public string Company { get; set; } = null!;
    public string? Role { get; set; }
    public string? StartDate { get; set; }               // ISO date, may be year-only
    public string? EndDate { get; set; }
    public string? Responsibilities { get; set; }
}

/// <summary>M33: Internal work history — moves within this organisation.</summary>
public class InternalWorkHistory : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public string OrgUnitName { get; set; } = null!;
    public string? Role { get; set; }
    public string? Grade { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? Reason { get; set; }                  // transfer | promotion | secondment | ...
}

/// <summary>HRM-016: An employment/assignment record. A worker can hold multiple
/// concurrent assignments (e.g. acting role, secondment), each effective-dated.</summary>
public class Assignment : Entity, IEffectiveDated
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public Guid LegalEntityId { get; set; }
    public LegalEntity? LegalEntity { get; set; }
    public Guid OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }
    public Guid LocationId { get; set; }
    public WorkLocation? Location { get; set; }
    public Guid? ManagerId { get; set; }
    public string? JobTitle { get; set; }
    public string? Grade { get; set; }
    public string? PositionNo { get; set; }
    public string ContractType { get; set; } = "permanent"; // permanent | fixed-term | part-time | casual | internship | apprenticeship
    public string WorkPattern { get; set; } = "full-time";
    public int ProbationMonths { get; set; } = 3;
    public int NoticeDays { get; set; } = 30;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }      // fixed-term or notice
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string Status { get; set; } = "current"; // proposed | current | future | ended
}

/// <summary>Tenant-owned contract policy used by employment assignments.
/// Archiving preserves historical assignments while preventing new use.</summary>
public class ContractType : Entity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int ProbationDays { get; set; }
    public int NoticeDays { get; set; } = 30;
    public bool IsActive { get; set; } = true;
}

/// <summary>HRM-017: Effective-dated movements (transfer, promotion, demotion,
/// secondment, acting). Submitted movements stay Pending until approved and
/// their effective date arrives; they never silently overwrite history.</summary>
public class Movement : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public string MovementType { get; set; } = null!; // transfer | promotion | demotion | secondment | acting | manager-change | grade-change
    public string Status { get; set; } = "draft";     // draft | pending | approved | returned | rejected | cancelled | executed
    public DateOnly EffectiveDate { get; set; }
    public string Reason { get; set; } = null!;

    // From (snapshot at submission)
    public Guid? FromOrgUnitId { get; set; }
    public string? FromJobTitle { get; set; }
    public string? FromGrade { get; set; }

    // To (requested)
    public Guid? ToOrgUnitId { get; set; }
    public string? ToJobTitle { get; set; }
    public string? ToGrade { get; set; }
    public Guid? ToLocationId { get; set; }
    public Guid? ToManagerId { get; set; }
    public decimal? SalaryChange { get; set; }       // new basic salary, nullable = unchanged
}

public class EmergencyContact : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public string Relationship { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>Zambia pack: bank and statutory payout details.</summary>
public class WorkerBankDetail : Entity
{
    public Guid WorkerId { get; set; }
    public Worker? Worker { get; set; }
    public string BankName { get; set; } = null!;
    public string BranchCode { get; set; } = null!;
    public string AccountNumber { get; set; } = null!;
    public string AccountName { get; set; } = null!;
    public string PaymentMethod { get; set; } = "bank"; // bank | mobile-money | cash
    public string? MobileMoneyNumber { get; set; }
    public bool IsPrimary { get; set; }
}
