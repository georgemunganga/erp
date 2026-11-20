namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>Explicit company invitation. The secret and delivery address are protected, never returned as membership.</summary>
public sealed class CoreCompanyInvitation : Entity
{
    public Guid LegalEntityId { get; set; }
    public string Issuer { get; set; } = null!;
    public string InviterSubject { get; set; } = null!;
    public Guid InviterLinkId { get; set; }
    public Guid InviterLocalUserId { get; set; }
    public string RecipientHash { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public string ProtectedDelivery { get; set; } = null!;
    public string Role { get; set; } = CoreCompanyRoles.Member;
    public DateTimeOffset ExpiresAt { get; set; }
    public string State { get; set; } = "pending";
    public int Revision { get; set; } = 1;
    public string? AcceptedSubject { get; set; }
    public Guid? AcceptedMembershipId { get; set; }
}

/// <summary>Append-only command outcome. Never stores an invitation secret or recipient address.</summary>
public sealed class CoreCompanyInvitationReceipt : Entity
{
    public Guid LegalEntityId { get; set; }
    public Guid InvitationId { get; set; }
    public string ActorIssuer { get; set; } = null!;
    public string ActorSubject { get; set; } = null!;
    public Guid CommandKey { get; set; }
    public string RequestHash { get; set; } = null!;
    public string Operation { get; set; } = null!;
    public string ResultState { get; set; } = null!;
    public int ResultRevision { get; set; }
    public Guid? ResultMembershipId { get; set; }
}
