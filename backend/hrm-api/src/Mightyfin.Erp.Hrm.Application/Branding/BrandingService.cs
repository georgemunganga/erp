namespace Mightyfin.Erp.Hrm.Application.Branding;

public sealed record CompanyBrandingDto(string DisplayName, string CompanyName, string CompanyDomain,
    string LoginHeading, string LoginDescription, string EmailPlaceholder, string PasswordPlaceholder,
    string SupportEmail, string PrimaryColor, string PrimaryForegroundColor,
    string ButtonColor, string ButtonForegroundColor,
    string SecondaryColor, string SecondaryForegroundColor, string AccentColor, string AccentForegroundColor,
    string RailColor, string RailForegroundColor, string RailMutedColor, string RailActiveColor,
    string? LogoLightDataUri, string? LogoDarkDataUri, string? FaviconDataUri, DateTimeOffset? UpdatedAt);

public sealed record CompanyBrandingUpdateRequest(string? DisplayName, string? CompanyName, string? CompanyDomain,
    string? LoginHeading, string? LoginDescription, string? EmailPlaceholder, string? PasswordPlaceholder,
    string? SupportEmail, string? PrimaryColor,
    string? PrimaryForegroundColor, string? ButtonColor, string? ButtonForegroundColor,
    string? SecondaryColor, string? SecondaryForegroundColor,
    string? AccentColor, string? AccentForegroundColor, string? RailColor, string? RailForegroundColor,
    string? RailMutedColor, string? RailActiveColor, string? LogoLightDataUri,
    string? LogoDarkDataUri, string? FaviconDataUri);

public interface ICompanyBrandingService
{
    Task<CompanyBrandingDto> GetAsync(CancellationToken ct);
    Task<CompanyBrandingDto> GetPublicAsync(CancellationToken ct);
    Task<CompanyBrandingDto> UpdateAsync(CompanyBrandingUpdateRequest request, CancellationToken ct);
    Task<CompanyBrandingDto> ResetAsync(CancellationToken ct);
}
