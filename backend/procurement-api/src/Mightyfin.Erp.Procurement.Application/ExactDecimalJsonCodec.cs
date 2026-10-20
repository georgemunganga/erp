using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Bounded decimal lexeme parser. Neither decimal.Parse nor floating point may
/// round an HTTP value before its database precision and CLR representation are checked.</summary>
public static class ExactDecimalJsonCodec
{
    private const int MaxTokenBytes = 128;
    private const int MaxExponent = 1000;
    private static readonly BigInteger MaxCoefficient = (BigInteger.One << 96) - 1;
    private static readonly Regex Syntax = new(@"^(-?)([0-9]+)(?:\.([0-9]+))?(?:[eE]([+-]?[0-9]+))?$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static decimal Read(ref Utf8JsonReader reader, int precision, int scale, string errorPrefix)
    {
        if (reader.TokenType is not (JsonTokenType.String or JsonTokenType.Number)
            || (reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length) > MaxTokenBytes)
            throw new JsonException(errorPrefix + "Invalid");
        var raw = reader.TokenType == JsonTokenType.String ? reader.GetString()
            : reader.HasValueSequence ? Encoding.UTF8.GetString(reader.ValueSequence.ToArray())
                : Encoding.UTF8.GetString(reader.ValueSpan);
        return Parse(raw, precision, scale, errorPrefix);
    }

    public static decimal Parse(string? raw, int precision, int scale, string errorPrefix = "Decimal")
    {
        if (precision is < 1 or > 38 || scale < 0 || scale > 28 || scale > precision)
            throw new ArgumentOutOfRangeException(nameof(precision));
        if (raw is null || raw.Length > MaxTokenBytes) throw new JsonException(errorPrefix + "Invalid");
        var match = Syntax.Match(raw.Trim());
        if (!match.Success) throw new JsonException(errorPrefix + "Invalid");
        var exponent = 0;
        if (match.Groups[4].Success && (!int.TryParse(match.Groups[4].Value, NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out exponent) || Math.Abs((long)exponent) > MaxExponent))
            throw new JsonException(errorPrefix + "Invalid");
        var digits = (match.Groups[2].Value + match.Groups[3].Value).TrimStart('0');
        if (digits.Length == 0) return 0m;
        var actualScale = match.Groups[3].Length - exponent;
        while (actualScale > 0 && digits.EndsWith('0')) { digits = digits[..^1]; actualScale--; }
        if (actualScale > scale || digits.Length - actualScale > precision - scale)
            throw new JsonException(errorPrefix + "PrecisionOrRangeInvalid");
        // Range check precedes expansion, so even a bounded huge exponent cannot allocate a huge coefficient.
        if (actualScale < 0) { digits += new string('0', -actualScale); actualScale = 0; }
        var coefficient = BigInteger.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
        if (coefficient > MaxCoefficient) throw new JsonException(errorPrefix + "NotRepresentable");
        var lo = unchecked((int)(uint)(coefficient & uint.MaxValue));
        var mid = unchecked((int)(uint)((coefficient >> 32) & uint.MaxValue));
        var hi = unchecked((int)(uint)((coefficient >> 64) & uint.MaxValue));
        return new decimal(lo, mid, hi, match.Groups[1].Length != 0, checked((byte)actualScale));
    }

    public static bool IsRepresentable(decimal value, int precision, int scale)
    {
        try { Parse(value.ToString(CultureInfo.InvariantCulture), precision, scale); return true; }
        catch (JsonException) { return false; }
    }
}
