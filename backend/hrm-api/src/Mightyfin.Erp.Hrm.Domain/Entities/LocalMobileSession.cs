namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>Mobile-only opaque credentials; never a browser LocalSession.</summary>
public sealed class LocalMobileSession : Entity
{
    public Guid LocalUserId { get; set; }
    public Guid WorkerId { get; set; }
    public Guid LegalEntityId { get; set; }
    public string Origin { get; set; } = null!;
    public string PasswordVersion { get; set; } = null!;
    public string AccessHash { get; set; } = null!;
    public string RefreshHash { get; set; } = null!;
    public DateTimeOffset AccessExpiresAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
