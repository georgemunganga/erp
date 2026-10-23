namespace Mightyfin.Erp.Hrm.Application.ConfigAndExtras;

/// <summary>Validation and date conversion for the persisted Core company time zone.</summary>
public static class CompanyTimeZone
{
    public const string DefaultId = "UTC";

    public static string Validate(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 100)
            throw new DomainException("invalid-time-zone", "A valid company time zone is required.");
        var normalized = id.Trim();
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(normalized);
            // Require IANA-style IDs (or UTC), not platform-specific Windows names.
            if (normalized != DefaultId && !normalized.Contains('/'))
                throw new DomainException("invalid-time-zone", "Use an IANA company time zone or UTC.");
            return normalized;
        }
        catch (TimeZoneNotFoundException)
        {
            throw new DomainException("invalid-time-zone", "The company time zone is not recognized.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new DomainException("invalid-time-zone", "The company time zone is invalid.");
        }
    }

    public static DateOnly BusinessDate(DateTimeOffset utcNow, string persistedId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Validate(persistedId));
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow.ToUniversalTime(), zone).DateTime);
    }
}
