namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>Core company governance, separate from billing membership and module roles.</summary>
public sealed class CoreCompanyMembership : Entity
{
    public Guid LegalEntityId { get; set; }
    public Guid LocalUserId { get; set; }
    public string Role { get; set; } = CoreCompanyRoles.Member;
    public bool IsActive { get; set; } = true;
}

public static class CoreCompanyRoles
{
    public const string Owner = "owner";
    public const string Administrator = "administrator";
    public const string Member = "member";
    public static bool IsValid(string role) => role is Owner or Administrator or Member;
}
