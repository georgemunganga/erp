using System.Numerics;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>
/// Exact arithmetic for existing decimal-valued domain contracts. Decimal multiplication
/// and addition can silently round even in checked blocks. Calculate with scaled integers,
/// then reject results which cannot be represented exactly by System.Decimal. This does
/// not widen persisted columns or claim all numeric(38,8) values fit the application type.
/// </summary>
public static class ExactDecimalArithmetic
{
    private static readonly BigInteger MaximumCoefficient = (BigInteger.One << 96) - 1;

    public static decimal Multiply(decimal left, decimal right)
    {
        var a = Parts(left); var b = Parts(right);
        return ToDecimal(a.Coefficient * b.Coefficient, a.Scale + b.Scale);
    }

    public static decimal Add(decimal left, decimal right) => Sum(new[] { left, right });
    public static decimal Subtract(decimal left, decimal right) => Sum(new[] { left, -right });

    public static decimal Sum(IEnumerable<decimal> values)
    {
        BigInteger coefficient = BigInteger.Zero;
        var scale = 0;
        foreach (var value in values)
        {
            var next = Parts(value);
            if (next.Scale > scale)
            { coefficient *= BigInteger.Pow(10, next.Scale - scale); scale = next.Scale; }
            coefficient += next.Coefficient * BigInteger.Pow(10, scale - next.Scale);
        }
        return ToDecimal(coefficient, scale);
    }

    private static (BigInteger Coefficient, int Scale) Parts(decimal value)
    {
        var bits = decimal.GetBits(value);
        var coefficient = (BigInteger)(uint)bits[0] | ((BigInteger)(uint)bits[1] << 32)
            | ((BigInteger)(uint)bits[2] << 64);
        return ((bits[3] & int.MinValue) != 0 ? -coefficient : coefficient, (bits[3] >> 16) & 0xff);
    }

    private static decimal ToDecimal(BigInteger signed, int scale)
    {
        var coefficient = BigInteger.Abs(signed);
        // Removing trailing zeros is lossless, including when an intermediate product's
        // scale exceeds 28. Never trim a nonzero digit to fit the decimal coefficient.
        while (scale > 0 && coefficient % 10 == 0) { coefficient /= 10; scale--; }
        if (scale > 28 || coefficient > MaximumCoefficient)
            throw new InvalidOperationException("ExactDecimalNotRepresentable");
        return new decimal(unchecked((int)(uint)(coefficient & uint.MaxValue)),
            unchecked((int)(uint)((coefficient >> 32) & uint.MaxValue)),
            unchecked((int)(uint)((coefficient >> 64) & uint.MaxValue)), signed.Sign < 0, (byte)scale);
    }
}
