namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>Core company governance, separate from billing membership and module roles.</summary>
public sealed class CoreCompanyMembership : Entity
{
    public Guid LegalEntityId { get; set; }
    public Guid LocalUserId { get; set; }
    public string Role { get; set; } = CoreCompanyRoles.Member;
    public bool IsActive { get; set; } = true;
    public int Revision { get; set; } = 1;
}

/// <summary>Immutable outcome of a confirmed company-governance command.</summary>
public sealed class CoreCompanyTeamCommandReceipt : Entity
{
    public Guid LegalEntityId { get; set; }
    public Guid ActorLocalUserId { get; set; }
    public string ActorIssuer { get; set; } = null!;
    public string ActorSubject { get; set; } = null!;
    public Guid CommandKey { get; set; }
    public Guid MembershipId { get; set; }
    public string RequestHash { get; set; } = null!;
    public string Operation { get; set; } = null!;
    public string ResultRole { get; set; } = null!;
    public bool ResultActive { get; set; }
    public int ResultRevision { get; set; }
}

public static class CoreCompanyRoles
{
    public const string Owner = "owner";
    public const string Administrator = "administrator";
    public const string Member = "member";
    public static bool IsValid(string role) => role is Owner or Administrator or Member;
}
