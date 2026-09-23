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

    public async Task<CompanyBrandingDto> GetPublicAsync(CancellationToken ct)
        => ToDto(await GetOrCreateAsync(ct));

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
        item.PrimaryForegroundColor = Colour(request.PrimaryForegroundColor, item.PrimaryForegroundColor, "primaryForegroundColor");
        item.ButtonColor = Colour(request.ButtonColor, item.ButtonColor, "buttonColor");
        item.ButtonForegroundColor = Colour(request.ButtonForegroundColor, item.ButtonForegroundColor, "buttonForegroundColor");
        item.SecondaryColor = Colour(request.SecondaryColor, item.SecondaryColor, "secondaryColor");
        item.SecondaryForegroundColor = Colour(request.SecondaryForegroundColor, item.SecondaryForegroundColor, "secondaryForegroundColor");
        item.AccentColor = Colour(request.AccentColor, item.AccentColor, "accentColor");
        item.AccentForegroundColor = Colour(request.AccentForegroundColor, item.AccentForegroundColor, "accentForegroundColor");
        item.RailColor = Colour(request.RailColor, item.RailColor, "railColor");
        item.RailForegroundColor = Colour(request.RailForegroundColor, item.RailForegroundColor, "railForegroundColor");
        item.RailMutedColor = Colour(request.RailMutedColor, item.RailMutedColor, "railMutedColor");
        item.RailActiveColor = Colour(request.RailActiveColor, item.RailActiveColor, "railActiveColor");
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
        item.DisplayName = "HR workspace";
        item.PrimaryColor = "#012642"; item.PrimaryForegroundColor = "#FFFFFF";
        item.ButtonColor = "#012642"; item.ButtonForegroundColor = "#FFFFFF";
        item.SecondaryColor = "#E8F0F5"; item.SecondaryForegroundColor = "#012642";
        item.AccentColor = "#E8F0F5"; item.AccentForegroundColor = "#012642";
        item.RailColor = "#012642"; item.RailForegroundColor = "#FFFFFF";
        item.RailMutedColor = "#A7C7DA"; item.RailActiveColor = "#0B3A5D";
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
    private static CompanyBrandingDto ToDto(CompanyBranding x) => new(x.DisplayName, x.PrimaryColor, x.PrimaryForegroundColor,
        x.ButtonColor, x.ButtonForegroundColor,
        x.SecondaryColor, x.SecondaryForegroundColor, x.AccentColor, x.AccentForegroundColor,
        x.RailColor, x.RailForegroundColor, x.RailMutedColor, x.RailActiveColor,
        x.LogoLightDataUri, x.LogoDarkDataUri, x.FaviconDataUri, x.UpdatedAt);
}
