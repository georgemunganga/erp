namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>An explicit Core-owned person/principal link; never an email join or commercial grant.</summary>
public sealed class ExternalIdentityLink : Entity
{
    public string Issuer { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public Guid LocalUserId { get; set; }
    public bool IsActive { get; set; } = true;
}
