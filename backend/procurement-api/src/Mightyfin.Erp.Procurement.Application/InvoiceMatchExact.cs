using System.Globalization;
using System.Numerics;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Integer arithmetic avoids decimal multiplication/division silently rounding a financial intermediate.</summary>
internal static class InvoiceMatchExact
{
    // Quantities/rates remain four-decimal inputs; exact acceptance ceilings need
    // eight decimals. Conversion never multiplies a decimal, which could overflow
    // or silently round before becoming a BigInteger.
    private const int Scale = 8;
    private static readonly BigInteger UnitScale = 100_000_000;
    internal static bool Input(decimal value, bool positive = false) => value >= 0
        && (!positive || value > 0) && value <= 99_999_999_999_999.9999m && decimal.Round(value, 4) == value;
    internal static bool AcceptedNetInput(decimal value) => value >= 0 && decimal.Round(value, Scale) == value;
    internal static BigInteger Units(decimal value)
    {
        var bits = decimal.GetBits(value);
        var coefficient = (BigInteger)(uint)bits[0] | ((BigInteger)(uint)bits[1] << 32)
            | ((BigInteger)(uint)bits[2] << 64);
        var decimalScale = (bits[3] >> 16) & 0xff;
        if (decimalScale <= Scale) coefficient *= BigInteger.Pow(10, Scale - decimalScale);
        else
        {
            coefficient = BigInteger.DivRem(coefficient, BigInteger.Pow(10, decimalScale - Scale), out var remainder);
            if (remainder != 0) throw new InvalidOperationException("InvoiceExactScaleUnsupported");
        }
        return bits[3] < 0 ? -coefficient : coefficient;
    }
    internal static BigInteger Round(BigInteger numerator, BigInteger denominator, InvoiceMoneyRounding rounding)
    {
        if (denominator <= 0 || numerator < 0) throw new ArgumentException("InvoiceExactAmountInvalid");
        var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
        var twice = remainder * 2;
        if (twice > denominator || twice == denominator && (rounding == InvoiceMoneyRounding.AwayFromZero || !quotient.IsEven)) quotient++;
        return quotient;
    }
    internal static BigInteger ProductMoney(BigInteger quantity, BigInteger price, InvoiceMatchingPolicy policy)
        => Round(quantity * price, BigInteger.Pow(10, Scale * 2 - policy.MoneyScale), policy.Rounding) * BigInteger.Pow(10, Scale - policy.MoneyScale);
    internal static BigInteger RatioMoney(BigInteger amount, BigInteger numerator, BigInteger denominator, InvoiceMatchingPolicy policy)
        => Round(amount * numerator, denominator * BigInteger.Pow(10, Scale - policy.MoneyScale), policy.Rounding)
            * BigInteger.Pow(10, Scale - policy.MoneyScale);
    internal static bool MoneyInput(decimal value, InvoiceMatchingPolicy policy)
        => policy.MoneyScale is >= 0 and <= 4
            && Input(value) && Units(value) % BigInteger.Pow(10, Scale - policy.MoneyScale) == 0;
    internal static bool Within(BigInteger variance, BigInteger baseline, InvoiceFieldTolerance tolerance)
    {
        variance = BigInteger.Abs(variance); baseline = BigInteger.Abs(baseline);
        var absolute = variance <= Units(tolerance.Absolute);
        var percent = variance * (100 * UnitScale) <= baseline * Units(tolerance.Percent);
        return tolerance.Combination == InvoiceToleranceCombination.AllLimits ? absolute && percent : absolute || percent;
    }
    internal static string Text(BigInteger value)
    {
        var negative = value.Sign < 0; var raw = BigInteger.Abs(value).ToString(CultureInfo.InvariantCulture).PadLeft(Scale + 1, '0');
        var result = (raw[..^Scale] + "." + raw[^Scale..]).TrimEnd('0').TrimEnd('.');
        return negative ? "-" + result : result;
    }
}
