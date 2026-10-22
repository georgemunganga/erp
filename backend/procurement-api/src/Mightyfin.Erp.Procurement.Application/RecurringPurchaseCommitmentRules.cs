using System.Text.Json.Serialization;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record RecurringPurchaseCommitmentInput(Guid SupplierId, Guid? SupplierContractId,
    string? Title, string? Frequency, DateOnly FirstCycleOn, DateOnly? LastCycleOn,
    DateOnly NextCycleOn, string? CurrencyCode,
    [property: JsonConverter(typeof(CommercialLineDecimalJsonConverter))] decimal ExpectedAmount);

public static class RecurringPurchaseCommitmentRules
{
    public static string? Validate(RecurringPurchaseCommitmentInput input)
    {
        if (input.SupplierId == Guid.Empty) return "Choose a supplier.";
        if (input.SupplierContractId == Guid.Empty) return "Choose a valid contract or leave it blank.";
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 300)
            return "Enter a title of up to 300 characters.";
        if (input.Frequency is not ("Weekly" or "Monthly" or "Quarterly" or "Yearly"))
            return "Choose weekly, monthly, quarterly, or yearly.";
        if (input.FirstCycleOn == default) return "Choose the first expected purchase date.";
        if (input.LastCycleOn.HasValue && input.LastCycleOn < input.FirstCycleOn)
            return "The last purchase date must follow the first.";
        if (input.NextCycleOn < input.FirstCycleOn ||
            (input.LastCycleOn.HasValue && input.NextCycleOn > input.LastCycleOn))
            return "The next purchase date must be within the schedule.";
        if (string.IsNullOrWhiteSpace(input.CurrencyCode) || input.CurrencyCode.Trim().Length != 3
            || !input.CurrencyCode.Trim().All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
            return "Choose a three-letter currency code.";
        if (input.ExpectedAmount <= 0 || input.ExpectedAmount > 99_999_999_999_999.9999m
            || decimal.Round(input.ExpectedAmount, 4) != input.ExpectedAmount)
            return "Expected amount must be positive and have at most four decimal places.";
        return null;
    }
}
