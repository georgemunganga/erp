namespace Mightyfin.Erp.Hrm.Application;

/// <summary>Small shared validation boundary for Core's versioned currency master.</summary>
public static class CoreCurrencyPrecisionRules
{
    public static bool CanonicalCode(string? code) => code is { Length: 3 } &&
        code.All(c => c is >= 'A' and <= 'Z');

    public static bool ValidPublish(string? code, int expectedRevision, int minorUnits,
        string? rounding, string? sourceReference, string? reason)
        => CanonicalCode(code) && expectedRevision is >= 0 and < int.MaxValue &&
           minorUnits is >= 0 and <= 4 && (rounding == "ToEven" || rounding == "AwayFromZero") &&
           NonBlank(sourceReference, 200) && NonBlank(reason, 500);

    public static bool CanPublish(int? latestRevision, int expectedRevision)
        => expectedRevision is >= 0 and < int.MaxValue &&
           (latestRevision ?? 0) == expectedRevision;

    public static bool CanRetire(int latestRevision, int targetRevision,
        int expectedRevision, DateTimeOffset? retiredAtUtc)
        => targetRevision > 0 && expectedRevision == targetRevision &&
           latestRevision == targetRevision && retiredAtUtc is null;

    public static bool NonBlank(string? value, int max)
        => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max;
}
