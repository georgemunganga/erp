using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Exact numeric(18,4) transport. Never parses through binary floating point.</summary>
public sealed class AuthorityAmountJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        string? raw;
        if (reader.TokenType == JsonTokenType.String) raw = reader.GetString();
        else if (reader.TokenType == JsonTokenType.Number)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            raw = document.RootElement.GetRawText();
        }
        else throw new JsonException("AuthorityAmountInvalid");
        if (raw is null || raw.Length > 128) throw new JsonException("AuthorityAmountInvalid");
        var match = Regex.Match(raw.Trim(), @"^(-?)([0-9]+)(?:\.([0-9]+))?(?:[eE]([+-]?[0-9]+))?$", RegexOptions.CultureInvariant);
        if (!match.Success) throw new JsonException("AuthorityAmountInvalid");
        var exponent = 0;
        if (match.Groups[4].Success && (!int.TryParse(match.Groups[4].Value, NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out exponent) || Math.Abs((long)exponent) > 1000))
            throw new JsonException("AuthorityAmountInvalid");
        var digits = (match.Groups[2].Value + match.Groups[3].Value).TrimStart('0');
        if (digits.Length == 0) return 0m;
        var scale = match.Groups[3].Length - exponent;
        while (scale > 0 && digits.EndsWith('0')) { digits = digits[..^1]; scale--; }
        if (scale > 4 || digits.Length - scale > 14) throw new JsonException("AuthorityAmountPrecisionOrRangeInvalid");
        if (scale < 0) { digits += new string('0', -scale); scale = 0; }
        if (scale > 0) { digits = digits.PadLeft(scale + 1, '0'); digits = digits.Insert(digits.Length - scale, "."); }
        return decimal.Parse(match.Groups[1].Value + digits, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteStringValue(Format(value));

    public static string Format(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
