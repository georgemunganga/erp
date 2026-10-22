using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Shared deterministic evidence encoding, including PostgreSQL jsonb and microsecond semantics.</summary>
public static class ProcurementEvidenceEncoding
{
    public static string Decimal(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
    public static string Digest(string? value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)));
    public static DateTimeOffset? DatabaseTimestamp(DateTimeOffset? value) => value is { } time
        ? new DateTimeOffset(time.UtcTicks - time.UtcTicks % 10, TimeSpan.Zero) : null;
    public static string PolicyJsonDigest(string? value)
    {
        try
        {
            using var json = JsonDocument.Parse(value ?? "");
            if (json.RootElement.ValueKind != JsonValueKind.Object) return "";
            using var bytes = new MemoryStream();
            using (var writer = new Utf8JsonWriter(bytes)) WriteCanonicalJson(writer, json.RootElement);
            return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
        }
        catch (Exception error) when (error is JsonException or FormatException or OverflowException) { return ""; }
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
                if (properties.GroupBy(x => x.Name, StringComparer.Ordinal).Any(x => x.Count() > 1))
                    throw new FormatException("Ambiguous policy object.");
                writer.WriteStartObject();
                foreach (var property in properties)
                { writer.WritePropertyName(property.Name); WriteCanonicalJson(writer, property.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) WriteCanonicalJson(writer, item);
                writer.WriteEndArray(); break;
            case JsonValueKind.Number:
                if (!value.TryGetDecimal(out var number)) throw new FormatException("Unsupported policy number.");
                var exact = NormalizeNumber(value.GetRawText());
                if (exact != NormalizeNumber(Decimal(number)))
                    throw new FormatException("Policy number loses decimal precision.");
                writer.WriteRawValue(exact); break;
            case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new FormatException("Unsupported policy value.");
        }
    }

    // Normalize exact base-10 coefficient/exponent strings, never binary floating point.
    // Comparing the raw and decimal forms detects TryGetDecimal's possible rounding.
    private static string NormalizeNumber(string literal)
    {
        var exponentAt = literal.IndexOf('e');
        if (exponentAt < 0) exponentAt = literal.IndexOf('E');
        var exponent = 0;
        if (exponentAt >= 0 && !int.TryParse(literal[(exponentAt + 1)..],
            NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
            throw new FormatException("Unsupported policy exponent.");
        var mantissa = exponentAt < 0 ? literal : literal[..exponentAt];
        var negative = mantissa[0] == '-';
        if (negative) mantissa = mantissa[1..];
        var point = mantissa.IndexOf('.');
        var fractionDigits = point < 0 ? 0 : mantissa.Length - point - 1;
        var digits = mantissa.Replace(".", "", StringComparison.Ordinal).TrimStart('0');
        if (digits.Length == 0) return "0";
        var trailing = 0;
        while (digits[digits.Length - trailing - 1] == '0') trailing++;
        if (trailing != 0) digits = digits[..^trailing];
        exponent = checked(exponent - fractionDigits + trailing);
        return (negative ? "-" : "") + digits + "e" + exponent.ToString(CultureInfo.InvariantCulture);
    }

}
