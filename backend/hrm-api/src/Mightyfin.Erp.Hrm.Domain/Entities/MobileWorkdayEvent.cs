namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>Append-only employee declarations. Does not approve time or change payroll.</summary>
public sealed class MobileWorkdayEvent : Entity
{
    public Guid LegalEntityId { get; set; }
    public Guid WorkerId { get; set; }
    public Guid AttendanceRecordId { get; set; }
    public Guid LocationId { get; set; }
    public Guid RequestId { get; set; }
    public string RequestHash { get; set; } = null!;
    public int Revision { get; set; }
    public string Kind { get; set; } = null!;
    public string StateAfter { get; set; } = null!;
    public DateTimeOffset RecordedAt { get; set; }
    public string Reason { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Blockers { get; set; } = "";
    public string ActorSubjectId { get; set; } = null!;
}
