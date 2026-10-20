using System.Text.Json.Serialization;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record ContractDraftInput(Guid SupplierId, string? Title, string? ContractType,
    string? CurrencyCode, [property: JsonConverter(typeof(CommercialLineDecimalJsonConverter))] decimal CeilingAmount, DateOnly? EffectiveFrom,
    DateOnly? ExpiresOn, DateOnly? RenewalNoticeOn);

public static class ProcurementContractDraftRules
{
    public static string? Validate(ContractDraftInput input)
    {
        if (input.SupplierId == Guid.Empty) return "Choose a supplier.";
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 300)
            return "Enter a contract title of up to 300 characters.";
        if (string.IsNullOrWhiteSpace(input.ContractType) || input.ContractType.Trim().Length > 80)
            return "Choose a contract type of up to 80 characters.";
        if (string.IsNullOrWhiteSpace(input.CurrencyCode) || input.CurrencyCode.Trim().Length != 3
            || !input.CurrencyCode.Trim().All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
            return "Choose a three-letter currency code.";
        if (input.CeilingAmount < 0 || input.CeilingAmount > 99_999_999_999_999.9999m
            || decimal.Round(input.CeilingAmount, 4) != input.CeilingAmount)
            return "The ceiling must be non-negative and have at most four decimal places.";
        if (input.EffectiveFrom.HasValue && input.ExpiresOn.HasValue && input.ExpiresOn < input.EffectiveFrom)
            return "Expiry must follow the start date.";
        if (input.RenewalNoticeOn.HasValue && input.ExpiresOn.HasValue && input.RenewalNoticeOn > input.ExpiresOn)
            return "Renewal notice cannot follow expiry.";
        return null;
    }
}
