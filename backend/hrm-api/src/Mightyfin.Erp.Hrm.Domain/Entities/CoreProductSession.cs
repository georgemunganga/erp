namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>Immutable, explicitly selected ERP session; no provider credential is stored here.</summary>
public sealed class CoreProductSession : Entity
{
    public int PolicyVersion { get; set; } = 1;
    public string Issuer { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string ClientId { get; set; } = null!;
    public Guid PrincipalId { get; set; }
    public Guid LinkId { get; set; }
    public Guid LegalEntityId { get; set; }
    public Guid MembershipId { get; set; }
    public string ModuleCode { get; set; } = null!;
    public Guid GrantVersionId { get; set; }
    public int GrantRevision { get; set; }
    public string ScopeKind { get; set; } = null!;
    public string ScopeHash { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public string ProtectedToken { get; set; } = null!;
    public string CredentialHash { get; set; } = null!;
    public string BrowserHash { get; set; } = null!;
    public Guid CommandKey { get; set; }
    public string RequestHash { get; set; } = null!;
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
public sealed class CoreProductSessionLocation : Entity
{
    public Guid LegalEntityId { get; set; }
    public Guid SessionId { get; set; }
    public Guid LocationId { get; set; }
}
/// <summary>Append-only terminal revocation; never update/delete issuance evidence.</summary>
public sealed class CoreProductSessionRevocation : Entity
{
    public Guid LegalEntityId { get; set; }
    public Guid SessionId { get; set; }
    public DateTimeOffset RevokedAt { get; set; }
    public string Reason { get; set; } = null!; // logout | replaced
}
