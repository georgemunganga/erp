namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>Core-owned immutable allocation and command receipt, never a billing membership.</summary>
public sealed class CoreWorkspaceBinding : Entity
{
    public string Issuer { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string ClientId { get; set; } = null!;
    public string CustomerId { get; set; } = null!;
    public string EnrollmentId { get; set; } = null!;
    public int Slot { get; set; }
    public int PurchasedLimit { get; set; }
    public Guid LegalEntityId { get; set; }
    public Guid OwnerLocalUserId { get; set; }
    public string IdempotencyHash { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public DateTimeOffset AuthorityUntil { get; set; }
    public DateTimeOffset LicenseUntil { get; set; }
}
