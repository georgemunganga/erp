using System.Text.Json.Serialization;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record ContractRateDraftInput(Guid? CatalogItemId, string? Description, string? UnitCode,
    [property: JsonConverter(typeof(CommercialLineDecimalJsonConverter))] decimal UnitPrice,
    [property: JsonConverter(typeof(NullableCommercialLineDecimalJsonConverter))] decimal? MaximumQuantity);

public static class ProcurementContractRateRules
{
    public static string? Validate(ContractRateDraftInput input)
    {
        if (input.CatalogItemId == Guid.Empty) return "Choose a valid item or leave the item empty.";
        if (string.IsNullOrWhiteSpace(input.Description) || input.Description.Trim().Length > 1000)
            return "Enter a rate description of up to 1,000 characters.";
        if (string.IsNullOrWhiteSpace(input.UnitCode) || input.UnitCode.Trim().Length > 40)
            return "Enter a unit of up to 40 characters.";
        if (!ExactDecimalJsonCodec.IsRepresentable(input.UnitPrice, 18, 4) || input.UnitPrice < 0)
            return "Enter a non-negative unit price with up to four decimal places.";
        if (input.MaximumQuantity is { } quantity
            && (!ExactDecimalJsonCodec.IsRepresentable(quantity, 18, 4) || quantity <= 0))
            return "Enter a positive maximum quantity with up to four decimal places.";
        return null;
    }
}
