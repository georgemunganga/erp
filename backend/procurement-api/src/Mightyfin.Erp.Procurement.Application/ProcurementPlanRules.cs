namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Draft-only planning inputs. Approval and Finance budget decisions are separate contracts.</summary>
public sealed record ProcurementPlanLineInput(Guid? Id, Guid? CatalogItemId, string? Description,
    string? CategoryCode, string? UnitCode, decimal EstimatedQuantity, decimal EstimatedAmount,
    DateOnly? ExpectedOn, string? FundingSourceRef);

public sealed record ProcurementPlanInput(string? FinancialPeriodRef, string? CurrencyCode,
    string? FundingSourceRef, IReadOnlyList<ProcurementPlanLineInput>? Lines);

public static class ProcurementPlanRules
{
    public static string? Validate(ProcurementPlanInput input)
    {
        if (string.IsNullOrWhiteSpace(input.FinancialPeriodRef) || input.FinancialPeriodRef.Trim().Length > 100)
            return "Choose a financial period of up to 100 characters.";
        if (string.IsNullOrWhiteSpace(input.CurrencyCode) || input.CurrencyCode.Trim().Length != 3 ||
            !input.CurrencyCode.Trim().All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
            return "Choose a three-letter currency code.";
        if (input.FundingSourceRef?.Length > 100)
            return "Funding source reference is too long.";
        if (input.Lines is null || input.Lines.Count is < 1 or > 100)
            return "Add between 1 and 100 planned needs.";
        if (input.Lines.Where(line => line.Id.HasValue).Select(line => line.Id).Distinct().Count()
            != input.Lines.Count(line => line.Id.HasValue))
            return "A planned need was repeated.";
        decimal total = 0;
        foreach (var line in input.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Description) || line.Description.Trim().Length > 1000)
                return "Every planned need requires a description of up to 1,000 characters.";
            if (string.IsNullOrWhiteSpace(line.UnitCode) || line.UnitCode.Trim().Length > 30)
                return "Every planned need requires a unit.";
            if (line.CategoryCode?.Length > 100 || line.FundingSourceRef?.Length > 100)
                return "A category or funding reference is too long.";
            if (!ValidDecimal(line.EstimatedQuantity, positive: true) || !ValidDecimal(line.EstimatedAmount))
                return "Quantity must be positive and amount cannot be negative; use at most four decimal places.";
            if (line.ExpectedOn is DateOnly date && date.Year < 1900)
                return "Choose a valid expected date.";
            try { total += line.EstimatedAmount; }
            catch (OverflowException) { return "The estimated total is too large."; }
            if (total > 99_999_999_999_999.9999m)
                return "The estimated total is too large.";
        }
        return null;
    }

    private static bool ValidDecimal(decimal value, bool positive = false) =>
        (positive ? value > 0 : value >= 0) && value <= 99_999_999_999_999.9999m
        && decimal.Round(value, 4) == value;
}
