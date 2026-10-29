using System.Globalization;
using System.Numerics;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>
/// A consumed projection of a future Core-owned currency reference. This pure
/// calculation does not establish that the reference is current or authorized.
/// </summary>
public sealed record InvoiceCurrencyPrecisionSnapshot(
    string CurrencyCode, Guid ReferenceId, int Revision, int MinorUnits, InvoiceMoneyRounding Rounding);

public sealed record InvoiceCurrencyCalculationLineInput(
    Guid OriginalLineId, decimal Quantity, decimal UnitRate, decimal EnteredTax);

public sealed record InvoiceCurrencyCalculationLine(
    Guid OriginalLineId, decimal Quantity, decimal UnitRate, decimal RawNet,
    decimal RoundedNet, decimal EnteredTax, decimal Gross, decimal RoundingAdjustment);

public sealed record InvoiceCurrencyCalculationResult(
    InvoiceCurrencyPrecisionSnapshot Precision,
    IReadOnlyList<InvoiceCurrencyCalculationLine> Lines,
    decimal RawNet, decimal RoundedNet, decimal Tax, decimal Gross, decimal RoundingAdjustment);

/// <summary>
/// New-invoice v2 arithmetic only. No route or store calls this until Core's
/// versioned reference and retained calculation evidence are integrated.
/// </summary>
public static class InvoiceCurrencyRoundedCalculation
{
    public static InvoiceCurrencyCalculationResult Calculate(
        InvoiceCurrencyPrecisionSnapshot precision,
        IReadOnlyList<InvoiceCurrencyCalculationLineInput> lines)
    {
        if (precision is null || precision.CurrencyCode is not { Length: 3 } code
            || code.Any(c => c is < 'A' or > 'Z')
            || precision.ReferenceId == Guid.Empty || precision.Revision < 1
            || precision.MinorUnits is < 0 or > 4 || !Enum.IsDefined(precision.Rounding))
            throw new InvalidOperationException("InvoiceCurrencyPrecisionUnavailable");
        if (lines is null || lines.Count is < 1 or > 200
            || lines.Any(x => x is null || x.OriginalLineId == Guid.Empty
                || !InvoiceMatchExact.Input(x.Quantity, positive: true)
                || !InvoiceMatchExact.Input(x.UnitRate)
                || !InvoiceMatchExact.Input(x.EnteredTax))
            || lines.Select(x => x.OriginalLineId).Distinct().Count() != lines.Count)
            throw new InvalidOperationException("InvoiceCalculationInputInvalid");

        var currencyQuantum = BigInteger.Pow(10, 8 - precision.MinorUnits);
        var productDenominator = BigInteger.Pow(10, 16 - precision.MinorUnits);
        var output = new List<InvoiceCurrencyCalculationLine>(lines.Count);
        foreach (var line in lines)
        {
            if (InvoiceMatchExact.Units(line.EnteredTax) % currencyQuantum != 0)
                throw new InvalidOperationException("InvoiceTaxPrecisionExceeded");
            var raw = ExactDecimalArithmetic.Multiply(line.Quantity, line.UnitRate);
            // InvoiceMatchExact.Round is the existing integer tie rule used by
            // matching. Quantity/rate each have eight-digit scaled integers.
            var product = InvoiceMatchExact.Units(line.Quantity) * InvoiceMatchExact.Units(line.UnitRate);
            var roundedMinor = InvoiceMatchExact.Round(product, productDenominator, precision.Rounding);
            var rounded = Amount(roundedMinor * currencyQuantum);
            var gross = ExactDecimalArithmetic.Add(rounded, line.EnteredTax);
            RequireAmount(gross);
            output.Add(new(line.OriginalLineId, line.Quantity, line.UnitRate, raw, rounded,
                line.EnteredTax, gross, ExactDecimalArithmetic.Subtract(rounded, raw)));
        }

        var rawNet = ExactDecimalArithmetic.Sum(output.Select(x => x.RawNet));
        var net = ExactDecimalArithmetic.Sum(output.Select(x => x.RoundedNet));
        var tax = ExactDecimalArithmetic.Sum(output.Select(x => x.EnteredTax));
        var total = ExactDecimalArithmetic.Add(net, tax);
        RequireAmount(net); RequireAmount(tax); RequireAmount(total);
        return new(precision, output.AsReadOnly(), rawNet, net, tax, total,
            ExactDecimalArithmetic.Subtract(net, rawNet));
    }

    private static decimal Amount(BigInteger units)
    {
        if (units < 0)
            throw new InvalidOperationException("InvoiceCalculationAmountOutOfRange");
        if (!decimal.TryParse(InvoiceMatchExact.Text(units), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var result))
            throw new InvalidOperationException("InvoiceCalculationAmountOutOfRange");
        RequireAmount(result);
        return result;
    }

    private static void RequireAmount(decimal value)
    {
        if (!InvoiceMatchExact.Input(value))
            throw new InvalidOperationException("InvoiceCalculationAmountOutOfRange");
    }
}
