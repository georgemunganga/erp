using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Exact numeric(18,4) input for stored quantity, unit rate and tax. Keep legacy numeric output.</summary>
public sealed class CommercialLineDecimalJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => ExactDecimalJsonCodec.Read(ref reader, 18, 4, "CommercialLineAmount");
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}

/// <summary>Exact numeric(38,8) input, also requiring exact System.Decimal representation.
/// Finance gross is never forced through the four-decimal spending-limit converter.</summary>
public sealed class CommercialGrossDecimalJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => ExactDecimalJsonCodec.Read(ref reader, 38, 8, "CommercialGrossAmount");
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}
