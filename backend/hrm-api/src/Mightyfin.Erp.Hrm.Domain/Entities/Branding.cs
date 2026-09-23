namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>Tenant-owned presentation settings. Assets are intentionally stored
/// with the tenant record so an uploaded logo cannot leak through a shared URL.
/// This is presentation only: it never changes statutory documents already issued.</summary>
public sealed class CompanyBranding : Entity
{
    public string DisplayName { get; set; } = "Newworldcargo HRM";
    public string PrimaryColor { get; set; } = "#012642";
    public string SecondaryColor { get; set; } = "#E8F0F5";
    public string AccentColor { get; set; } = "#E8F0F5";
    public string RailColor { get; set; } = "#012642";
    public string? LogoLightDataUri { get; set; }
    public string? LogoDarkDataUri { get; set; }
    public string? FaviconDataUri { get; set; }
}
