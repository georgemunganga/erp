using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceCurrencyRoundedCalculationTests
{
    private static readonly Guid ReferenceId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid FirstId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid SecondId = Guid.Parse("33333333-3333-4333-8333-333333333333");

    private static InvoiceCurrencyPrecisionSnapshot Precision(int minorUnits = 2,
        InvoiceMoneyRounding rounding = InvoiceMoneyRounding.ToEven)
        => new("ZMW", ReferenceId, 7, minorUnits, rounding);

    private static InvoiceCurrencyCalculationLineInput Line(decimal quantity, decimal rate,
        decimal tax = 0m, Guid? id = null) => new(id ?? FirstId, quantity, rate, tax);

    [Fact]
    public void Rounds_Line_Net_But_Preserves_Exact_Rate_And_Raw_Adjustment()
    {
        var precision = Precision();
        var result = InvoiceCurrencyRoundedCalculation.Calculate(precision, [Line(1m, 5.1234m)]);

        Assert.Same(precision, result.Precision);
        Assert.Equal(FirstId, result.Lines[0].OriginalLineId);
        Assert.Equal(5.1234m, result.Lines[0].UnitRate);
        Assert.Equal(5.1234m, result.Lines[0].RawNet);
        Assert.Equal(5.12m, result.Lines[0].RoundedNet);
        Assert.Equal(-0.0034m, result.Lines[0].RoundingAdjustment);
        Assert.Equal(5.12m, result.Gross);
        Assert.Equal(-0.0034m, result.RoundingAdjustment);
    }

    [Fact]
    public void Rounds_Each_Line_Before_Header_Sum_And_Uses_Explicit_Tie_Mode()
    {
        InvoiceCurrencyCalculationLineInput[] lines = [Line(1m, 0.005m), Line(1m, 0.005m, id: SecondId)];
        var even = InvoiceCurrencyRoundedCalculation.Calculate(Precision(), lines);
        var away = InvoiceCurrencyRoundedCalculation.Calculate(Precision(rounding: InvoiceMoneyRounding.AwayFromZero), lines);

        Assert.Equal(0.01m, even.RawNet);
        Assert.All(even.Lines, line => Assert.Equal(0m, line.RoundedNet));
        Assert.Equal(0m, even.RoundedNet); // Header rounding would incorrectly produce 0.01.
        Assert.All(away.Lines, line => Assert.Equal(0.01m, line.RoundedNet));
        Assert.Equal(0.02m, away.RoundedNet);
        Assert.Equal(0.01m, away.RoundingAdjustment);
        Assert.Equal(0.02m, InvoiceCurrencyRoundedCalculation.Calculate(Precision(),
            [Line(1m, 0.015m)]).RoundedNet); // ToEven rounds a half above an odd cent upward.
    }

    [Theory]
    [InlineData(0, "4.5", "4")]
    [InlineData(3, "5.1234", "5.123")]
    [InlineData(4, "5.1234", "5.1234")]
    public void Uses_Consumed_Currency_Precision(int minorUnits, string rateText, string expectedText)
    {
        var rate = decimal.Parse(rateText, System.Globalization.CultureInfo.InvariantCulture);
        var expected = decimal.Parse(expectedText, System.Globalization.CultureInfo.InvariantCulture);
        var result = InvoiceCurrencyRoundedCalculation.Calculate(Precision(minorUnits), [Line(1m, rate)]);
        Assert.Equal(expected, result.RoundedNet);
    }

    [Fact]
    public void Retains_Eight_Decimal_Raw_Product_Without_Rounding_Input()
    {
        var result = InvoiceCurrencyRoundedCalculation.Calculate(Precision(), [Line(0.0001m, 0.0001m)]);
        Assert.Equal(0.00000001m, result.RawNet);
        Assert.Equal(0m, result.RoundedNet);
        Assert.Equal(-0.00000001m, result.RoundingAdjustment);
        Assert.Equal(0.0001m, result.Lines[0].Quantity);
        Assert.Equal(0.0001m, result.Lines[0].UnitRate);
    }

    [Fact]
    public void Sums_Exact_Rounded_Net_And_Already_Quantized_Tax()
    {
        var result = InvoiceCurrencyRoundedCalculation.Calculate(Precision(),
            [Line(2m, 1.2345m, 0.03m), Line(1m, 0.005m, id: SecondId)]);
        Assert.Equal(2.474m, result.RawNet);
        Assert.Equal(2.47m, result.RoundedNet);
        Assert.Equal(0.03m, result.Tax);
        Assert.Equal(2.50m, result.Gross);
        Assert.Equal(-0.004m, result.RoundingAdjustment);
        Assert.Equal(2.50m, result.Lines[0].Gross);
    }

    [Fact]
    public void Rejects_Unowned_Or_Unsupported_Currency_Reference()
    {
        Assert.Equal("InvoiceCurrencyPrecisionUnavailable",
            Assert.Throws<InvalidOperationException>(() =>
                InvoiceCurrencyRoundedCalculation.Calculate(null!, [Line(1m, 1m)])).Message);
        foreach (var bad in new[]
        {
            Precision() with { CurrencyCode = null! },
            Precision() with { CurrencyCode = "zmw" },
            Precision() with { CurrencyCode = "US" },
            Precision() with { ReferenceId = Guid.Empty },
            Precision() with { Revision = 0 },
            Precision(-1), Precision(5),
            Precision(rounding: (InvoiceMoneyRounding)77)
        })
            Assert.Equal("InvoiceCurrencyPrecisionUnavailable",
                Assert.Throws<InvalidOperationException>(() =>
                    InvoiceCurrencyRoundedCalculation.Calculate(bad, [Line(1m, 1m)])).Message);
    }

    [Fact]
    public void Rejects_Duplicate_Unstable_Or_Invalid_Raw_Lines()
    {
        Assert.Equal("InvoiceCalculationInputInvalid",
            Assert.Throws<InvalidOperationException>(() =>
                InvoiceCurrencyRoundedCalculation.Calculate(Precision(), null!)).Message);
        InvoiceCurrencyCalculationLineInput[][] invalid =
        [
            [],
            [null!],
            [Line(1m, 1m), Line(1m, 1m)],
            [Line(1m, 1m, id: Guid.Empty)],
            [Line(0m, 1m)], [Line(-1m, 1m)],
            [Line(1.00001m, 1m)], [Line(1m, -1m)],
            [Line(1m, 1.00001m)], [Line(1m, 1m, -0.01m)]
        ];
        foreach (var lines in invalid)
            Assert.Equal("InvoiceCalculationInputInvalid",
                Assert.Throws<InvalidOperationException>(() =>
                    InvoiceCurrencyRoundedCalculation.Calculate(Precision(), lines)).Message);
    }

    [Fact]
    public void Rejects_Tax_With_More_Precision_Than_Currency()
    {
        Assert.Equal("InvoiceTaxPrecisionExceeded",
            Assert.Throws<InvalidOperationException>(() =>
                InvoiceCurrencyRoundedCalculation.Calculate(Precision(), [Line(1m, 1m, 0.001m)])).Message);
        Assert.Equal(1.001m,
            InvoiceCurrencyRoundedCalculation.Calculate(Precision(3), [Line(1m, 1m, 0.001m)]).Gross);
    }

    [Fact]
    public void Rejects_Amount_Overflow_Without_Silent_Decimal_Rounding()
    {
        Assert.Equal("InvoiceCalculationAmountOutOfRange",
            Assert.Throws<InvalidOperationException>(() =>
                InvoiceCurrencyRoundedCalculation.Calculate(Precision(4),
                    [Line(1m, 99_999_999_999_999.9999m, 0.0001m)])).Message);
        Assert.Throws<InvalidOperationException>(() =>
            InvoiceCurrencyRoundedCalculation.Calculate(Precision(),
                [Line(99_999_999_999_999.9999m, 99_999_999_999_999.9999m)]));
        Assert.Equal("InvoiceCalculationAmountOutOfRange",
            Assert.Throws<InvalidOperationException>(() =>
                InvoiceCurrencyRoundedCalculation.Calculate(Precision(4),
                    [Line(1m, 99_999_999_999_999.9999m), Line(1m, 0.0001m, id: SecondId)])).Message);
    }
}
