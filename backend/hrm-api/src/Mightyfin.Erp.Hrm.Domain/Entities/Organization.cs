namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>HRM-002: Legal entity of the organization (Zambia pack: registered
/// name, PACRA number, TPIN, NAPSA/NHIMA/WCFCB employer references).</summary>
public class LegalEntity : Entity
{
    public string Code { get; set; } = null!;          // unique per tenant
    public string RegisteredName { get; set; } = null!;
    public string? TradingName { get; set; }
    public string? PacraNumber { get; set; }
    public string? Tpin { get; set; }
    public string? NapsaEmployerRef { get; set; }
    public string? NhimaEmployerRef { get; set; }
    public string? WcfcbEmployerRef { get; set; }
    public string Currency { get; set; } = "ZMW";
    public string CountryCode { get; set; } = "ZM";
    /// <summary>IANA company time zone for business dates; UTC is the explicit platform default.</summary>
    public string TimeZoneId { get; set; } = "UTC";
    public bool IsDefault { get; set; }
}

/// <summary>Branch or work location under a legal entity.</summary>
public class WorkLocation : Entity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid LegalEntityId { get; set; }
    public LegalEntity? LegalEntity { get; set; }
    public string? AddressLine { get; set; }
    public string? Province { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
    public string? Type { get; set; } = "branch"; // branch | remote | site | headquarters
    public Guid? DefaultCalendarId { get; set; }
    public WorkCalendar? DefaultCalendar { get; set; }
}

/// <summary>Organization unit (department/section/team) in an effective-dated
/// hierarchy. ParentId enables tree structure; the hierarchy is resolved by
/// querying units effective at a date.</summary>
public class OrgUnit : Entity, IEffectiveDated
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid LegalEntityId { get; set; }
    public LegalEntity? LegalEntity { get; set; }
    public Guid? ParentId { get; set; }
    public OrgUnit? Parent { get; set; }
    public string? UnitType { get; set; } = "department"; // division | department | section | team
    public string? CostCentreRef { get; set; }
    public Guid? ManagerId { get; set; }          // current unit leader (worker id)
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string Status { get; set; } = "active"; // active | suspended | closed
}

/// <summary>Work calendar defining standard working days/hours plus public
/// holidays for a location or entity (Zambia calendar seeded).</summary>
public class WorkCalendar : Entity
{
    public string Name { get; set; } = null!;
    public Guid LegalEntityId { get; set; }
    public string CountryCode { get; set; } = "ZM";
    public int StandardWeeklyHours { get; set; } = 45;
    public string WeekendDays { get; set; } = "sat,sun";
    public bool IsDefault { get; set; }
    public ICollection<PublicHoliday> Holidays { get; set; } = new List<PublicHoliday>();
}

/// <summary>Public or gazetted holiday affecting leave entitlement and payroll.</summary>
public class PublicHoliday : Entity
{
    public string Name { get; set; } = null!;
    public Guid CalendarId { get; set; }
    public WorkCalendar? Calendar { get; set; }
    public DateOnly HolidayDate { get; set; }
    public string? ObservedOn { get; set; } // null => same day
    public bool IsRecurring { get; set; }    // e.g. 24 Oct every year
    public string? Description { get; set; }
}
