namespace Mightyfin.Erp.Hrm.Application.Branding;

public sealed record CompanyBrandingDto(string DisplayName, string PrimaryColor, string SecondaryColor,
    string AccentColor, string RailColor, string? LogoLightDataUri, string? LogoDarkDataUri,
    string? FaviconDataUri, DateTimeOffset? UpdatedAt);

public sealed record CompanyBrandingUpdateRequest(string? DisplayName, string? PrimaryColor,
    string? SecondaryColor, string? AccentColor, string? RailColor, string? LogoLightDataUri,
    string? LogoDarkDataUri, string? FaviconDataUri);

public interface ICompanyBrandingService
{
    Task<CompanyBrandingDto> GetAsync(CancellationToken ct);
    Task<CompanyBrandingDto> UpdateAsync(CompanyBrandingUpdateRequest request, CancellationToken ct);
    Task<CompanyBrandingDto> ResetAsync(CancellationToken ct);
}
