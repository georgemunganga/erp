namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Policy identifiers are codes, not display labels or trusted item classifications.</summary>
public static class ProcurementPolicyCode
{
    public const int MaximumLength = 100;

    // Strict policy/snapshot validation never normalizes existing evidence.
    public static bool IsValid(string? value) => value is { Length: > 0 and <= MaximumLength }
        && value.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');

    /// <summary>Only for newly supplied editable request inputs; never rewrite historical evidence.</summary>
    public static bool TryNormalizeOptional(string? input, out string? code)
    {
        code = null;
        if (string.IsNullOrWhiteSpace(input)) return true;
        var trimmed = input.Trim();
        // Validate ASCII before casing; Unicode lookalikes must not become policy authority.
        if (trimmed.Length > MaximumLength || !trimmed.All(c =>
                c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))
            return false;
        code = trimmed.ToUpperInvariant();
        return true;
    }

    public static string? NormalizeOptional(string? input) => TryNormalizeOptional(input, out var code)
        ? code : throw new ArgumentException("Use a policy code of up to 100 ASCII letters, digits or underscores.", nameof(input));
}
