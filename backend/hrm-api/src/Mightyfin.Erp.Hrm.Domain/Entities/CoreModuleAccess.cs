namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>Immutable decision version. Resolve latest revision before any operational admission.</summary>
public sealed class CoreModuleAccessVersion : Entity
{
    public Guid LegalEntityId { get; set; }
    public Guid MembershipId { get; set; }
    public int MembershipRevision { get; set; }
    public string ModuleCode { get; set; } = null!;
    public int Revision { get; set; }
    public Guid? PreviousVersionId { get; set; }
    public int PreviousRevision { get; set; }
    public int PolicyVersion { get; set; } = 1;
    public string State { get; set; } = null!; // granted | revoked
    public string RoleKeysCsv { get; set; } = "";
    public string ActionCodesCsv { get; set; } = "";
    public string ScopeKind { get; set; } = null!; // company | locations | none (revoked)
    public Guid ActingMembershipId { get; set; }
    public int ActingMembershipRevision { get; set; }
}

/// <summary>Immutable selected work location for one exact grant version/company.</summary>
public sealed class CoreModuleAccessLocation : Entity
{
    public Guid LegalEntityId { get; set; }
    public Guid GrantVersionId { get; set; }
    public Guid LocationId { get; set; }
}

/// <summary>Immutable actor-scoped command outcome; never a current authorization proof.</summary>
public sealed class CoreModuleAccessReceipt : Entity
{
    public Guid LegalEntityId { get; set; }
    public Guid MembershipId { get; set; }
    public string ModuleCode { get; set; } = null!;
    public Guid ActingMembershipId { get; set; }
    public string ActorIssuer { get; set; } = null!;
    public string ActorSubject { get; set; } = null!;
    public Guid CommandKey { get; set; }
    public string RequestHash { get; set; } = null!;
    public Guid ResultVersionId { get; set; }
    public int ResultRevision { get; set; }
}
