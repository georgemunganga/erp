namespace Mightyfin.Erp.Hrm.Domain.Entities;

/// <summary>Tenant-owned presentation settings. Assets are intentionally stored
/// with the tenant record so an uploaded logo cannot leak through a shared URL.
/// This is presentation only: it never changes statutory documents already issued.</summary>
public sealed class CompanyBranding : Entity
{
    public string DisplayName { get; set; } = "HR workspace";
    public string CompanyName { get; set; } = "Company";
    public string CompanyDomain { get; set; } = "";
    public string LoginHeading { get; set; } = "";
    public string LoginDescription { get; set; } = "";
    public string EmailPlaceholder { get; set; } = "";
    public string PasswordPlaceholder { get; set; } = "Enter your password";
    public string SupportEmail { get; set; } = "";
    public string PrimaryColor { get; set; } = "#012642";
    public string PrimaryForegroundColor { get; set; } = "#FFFFFF";
    public string ButtonColor { get; set; } = "#012642";
    public string ButtonForegroundColor { get; set; } = "#FFFFFF";
    public string SecondaryColor { get; set; } = "#E8F0F5";
    public string SecondaryForegroundColor { get; set; } = "#012642";
    public string AccentColor { get; set; } = "#E8F0F5";
    public string AccentForegroundColor { get; set; } = "#012642";
    public string RailColor { get; set; } = "#012642";
    public string RailForegroundColor { get; set; } = "#FFFFFF";
    public string RailMutedColor { get; set; } = "#A7C7DA";
    public string RailActiveColor { get; set; } = "#0B3A5D";
    public string? LogoLightDataUri { get; set; }
    public string? LogoDarkDataUri { get; set; }
    public string? FaviconDataUri { get; set; }
}
