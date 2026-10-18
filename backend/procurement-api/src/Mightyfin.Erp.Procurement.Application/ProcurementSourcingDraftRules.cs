namespace Mightyfin.Erp.Procurement.Application;

public sealed record SourcingLineInput(Guid? Id, Guid? PurchaseRequestLineId, string? Description,
    string? UnitCode, decimal Quantity, string? CategoryCode, DateOnly? NeededBy);

public sealed record SourcingDraftInput(string? Title, string? Method, string? CurrencyCode,
    DateTimeOffset? OpensAt, DateTimeOffset? ClosesAt, IReadOnlyList<SourcingLineInput>? Lines);

public static class ProcurementSourcingDraftRules
{
    public static string? Validate(SourcingDraftInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 300)
            return "Enter a sourcing title of up to 300 characters.";
        if (string.IsNullOrWhiteSpace(input.Method) || input.Method.Trim().Length > 40)
            return "Choose a sourcing method of up to 40 characters.";
        if (string.IsNullOrWhiteSpace(input.CurrencyCode) || input.CurrencyCode.Trim().Length != 3
            || !input.CurrencyCode.Trim().All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
            return "Choose a three-letter currency code.";
        if (input.OpensAt.HasValue && input.ClosesAt.HasValue && input.ClosesAt <= input.OpensAt)
            return "Closing time must follow opening time.";
        if (input.Lines is null || input.Lines.Count is < 1 or > 100)
            return "Add between 1 and 100 sourcing lines.";
        if (input.Lines.Where(x => x.Id.HasValue).Select(x => x.Id).Distinct().Count()
            != input.Lines.Count(x => x.Id.HasValue))
            return "A sourcing line was repeated.";
        foreach (var line in input.Lines)
        {
            if (line.PurchaseRequestLineId.HasValue)
                return "Linking approved request lines is not ready for this draft route.";
            if (string.IsNullOrWhiteSpace(line.Description) || line.Description.Trim().Length > 1000)
                return "Every sourcing line needs a description of up to 1,000 characters.";
            if (string.IsNullOrWhiteSpace(line.UnitCode) || line.UnitCode.Trim().Length > 30)
                return "Every sourcing line needs a unit.";
            if (line.Quantity <= 0 || line.Quantity > 99_999_999_999.9999m
                || decimal.Round(line.Quantity, 4) != line.Quantity)
                return "Every sourcing line needs a positive quantity with at most four decimal places.";
            if (line.CategoryCode?.Length > 100)
                return "A category code is too long.";
        }
        return null;
    }
}
