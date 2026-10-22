using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Exact numeric(18,4) transport. Never parses through binary floating point.</summary>
public sealed class AuthorityAmountJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => ExactDecimalJsonCodec.Read(ref reader, 18, 4, "AuthorityAmount");

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteStringValue(Format(value));

    public static string Format(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
