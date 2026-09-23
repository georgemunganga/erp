using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Branding;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;

public sealed class CompanyBrandingService(HrmDbContext db, IAuthzService authz) : ICompanyBrandingService
{
    private static readonly Regex HexColour = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);
    private const int MaxAssetBytes = 512 * 1024;

    public async Task<CompanyBrandingDto> GetAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("employee", "manager", "hr_ops", "hr_admin", "payroll", "finance");
        return ToDto(await GetOrCreateAsync(ct));
    }

    public async Task<CompanyBrandingDto> UpdateAsync(CompanyBrandingUpdateRequest request, CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var item = await GetOrCreateAsync(ct);
        if (request.DisplayName is not null)
        {
            var name = request.DisplayName.Trim();
            if (name.Length is < 2 or > 80) throw new DomainException("branding-name-invalid", "Company display name must be between 2 and 80 characters.");
            item.DisplayName = name;
        }
        item.PrimaryColor = Colour(request.PrimaryColor, item.PrimaryColor, "primaryColor");
        item.SecondaryColor = Colour(request.SecondaryColor, item.SecondaryColor, "secondaryColor");
        item.AccentColor = Colour(request.AccentColor, item.AccentColor, "accentColor");
        item.RailColor = Colour(request.RailColor, item.RailColor, "railColor");
        item.LogoLightDataUri = Asset(request.LogoLightDataUri, item.LogoLightDataUri, "light logo");
        item.LogoDarkDataUri = Asset(request.LogoDarkDataUri, item.LogoDarkDataUri, "dark logo");
        item.FaviconDataUri = Asset(request.FaviconDataUri, item.FaviconDataUri, "favicon");
        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<CompanyBrandingDto> ResetAsync(CancellationToken ct)
    {
        authz.RequireAnyRole("hr_admin");
        var item = await GetOrCreateAsync(ct);
        item.DisplayName = "Mightyfin HRMS";
        item.PrimaryColor = "#5D2B85"; item.SecondaryColor = "#17212B";
        item.AccentColor = "#FEC00F"; item.RailColor = "#410064";
        item.LogoLightDataUri = null; item.LogoDarkDataUri = null; item.FaviconDataUri = null;
        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    private async Task<CompanyBranding> GetOrCreateAsync(CancellationToken ct)
    {
        var item = await db.CompanyBrandings.SingleOrDefaultAsync(ct);
        if (item is not null) return item;
        item = new CompanyBranding();
        db.CompanyBrandings.Add(item);
        await db.SaveChangesAsync(ct);
        return item;
    }
    private static string Colour(string? value, string fallback, string field)
    {
        if (value is null) return fallback;
        var normal = value.Trim().ToUpperInvariant();
        if (!HexColour.IsMatch(normal)) throw new DomainException("branding-colour-invalid", $"{field} must be a six-digit hex colour, for example #5D2B85.");
        return normal;
    }
    private static string? Asset(string? value, string? fallback, string field)
    {
        if (value is null) return fallback;
        if (value == "") return null;
        var comma = value.IndexOf(',');
        if (comma < 0 || !value[..comma].StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) || !value[..comma].Contains(";base64", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("branding-asset-invalid", $"The {field} must be a PNG, JPEG, WebP, SVG, or ICO image.");
        try
        {
            if (Convert.FromBase64String(value[(comma + 1)..]).Length > MaxAssetBytes)
                throw new DomainException("branding-asset-too-large", $"The {field} must be 512 KB or smaller.");
        }
        catch (FormatException) { throw new DomainException("branding-asset-invalid", $"The {field} image data is invalid."); }
        return value;
    }
    private static CompanyBrandingDto ToDto(CompanyBranding x) => new(x.DisplayName, x.PrimaryColor, x.SecondaryColor,
        x.AccentColor, x.RailColor, x.LogoLightDataUri, x.LogoDarkDataUri, x.FaviconDataUri, x.UpdatedAt);
}
