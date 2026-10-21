using System.Globalization;
using System.Numerics;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Integer arithmetic avoids decimal multiplication/division silently rounding a financial intermediate.</summary>
internal static class InvoiceMatchExact
{
    internal static readonly BigInteger TenThousand = 10_000;
    internal static bool Input(decimal value, bool positive = false) => value >= 0
        && (!positive || value > 0) && value <= 99_999_999_999_999.9999m && decimal.Round(value, 4) == value;
    internal static BigInteger Units(decimal value) => new(value * 10_000m);
    internal static BigInteger Round(BigInteger numerator, BigInteger denominator, InvoiceMoneyRounding rounding)
    {
        if (denominator <= 0 || numerator < 0) throw new ArgumentException("InvoiceExactAmountInvalid");
        var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
        var twice = remainder * 2;
        if (twice > denominator || twice == denominator && (rounding == InvoiceMoneyRounding.AwayFromZero || !quotient.IsEven)) quotient++;
        return quotient;
    }
    internal static BigInteger ProductMoney(BigInteger quantity, BigInteger price, InvoiceMatchingPolicy policy)
        => Round(quantity * price, BigInteger.Pow(10, 8 - policy.MoneyScale), policy.Rounding) * BigInteger.Pow(10, 4 - policy.MoneyScale);
    internal static BigInteger RatioMoney(BigInteger amount, BigInteger numerator, BigInteger denominator, InvoiceMatchingPolicy policy)
        => Round(amount * numerator, denominator * BigInteger.Pow(10, 4 - policy.MoneyScale), policy.Rounding)
            * BigInteger.Pow(10, 4 - policy.MoneyScale);
    internal static bool MoneyInput(decimal value, InvoiceMatchingPolicy policy)
        => Input(value) && Units(value) % BigInteger.Pow(10, 4 - policy.MoneyScale) == 0;
    internal static bool Within(BigInteger variance, BigInteger baseline, InvoiceFieldTolerance tolerance)
    {
        variance = BigInteger.Abs(variance); baseline = BigInteger.Abs(baseline);
        var absolute = variance <= Units(tolerance.Absolute);
        var percent = variance * 1_000_000 <= baseline * Units(tolerance.Percent);
        return tolerance.Combination == InvoiceToleranceCombination.AllLimits ? absolute && percent : absolute || percent;
    }
    internal static string Text(BigInteger value)
    {
        var negative = value.Sign < 0; var raw = BigInteger.Abs(value).ToString(CultureInfo.InvariantCulture).PadLeft(5, '0');
        var result = (raw[..^4] + "." + raw[^4..]).TrimEnd('0').TrimEnd('.');
        return negative ? "-" + result : result;
    }
}
