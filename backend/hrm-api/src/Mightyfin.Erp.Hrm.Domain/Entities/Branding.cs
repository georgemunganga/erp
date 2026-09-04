namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>Tenant-owned presentation settings. Assets are intentionally stored
/// with the tenant record so an uploaded logo cannot leak through a shared URL.
/// This is presentation only: it never changes statutory documents already issued.</summary>
public sealed class CompanyBranding : Entity
{
    public string DisplayName { get; set; } = "Mightyfin HRMS";
    public string PrimaryColor { get; set; } = "#5D2B85";
    public string SecondaryColor { get; set; } = "#17212B";
    public string AccentColor { get; set; } = "#FEC00F";
    public string RailColor { get; set; } = "#410064";
    public string? LogoLightDataUri { get; set; }
    public string? LogoDarkDataUri { get; set; }
    public string? FaviconDataUri { get; set; }
}
