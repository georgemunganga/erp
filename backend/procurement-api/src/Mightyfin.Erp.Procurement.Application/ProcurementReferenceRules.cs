namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Validation for draft procurement reference data. Publishing uses a separate policy decision.</summary>
public static class ProcurementReferenceRules
{
    public static string? ValidateCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 50)
            return "Enter a code of up to 50 characters.";
        var normalized = code.Trim().ToUpperInvariant();
        if (!(normalized[0] is >= 'A' and <= 'Z' or >= '0' and <= '9') ||
            normalized.Any(c => !(c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-')))
            return "Use letters, numbers, underscore or hyphen in the code.";
        return null;
    }

    public static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            return "Enter a name of up to 200 characters.";
        return null;
    }

    public static string? ValidateOptional(string? value, string label, int maxLength) =>
        value?.Trim().Length > maxLength ? $"{label} is too long." : null;
}
