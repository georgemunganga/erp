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

/// <summary>Optional existing18,4 commercial fields; null remains null, output remains numeric.</summary>
public sealed class NullableCommercialLineDecimalJsonConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
        => reader.TokenType==JsonTokenType.Null?null:ExactDecimalJsonCodec.Read(ref reader,18,4,"CommercialLineAmount");
    public override void Write(Utf8JsonWriter writer,decimal? value,JsonSerializerOptions options)
    { if(value.HasValue) writer.WriteNumberValue(value.Value);else writer.WriteNullValue(); }
}
/// <summary>Existing item unit conversion is18,6, independently of price precision.</summary>
public sealed class PurchaseUnitConversionJsonConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
        => reader.TokenType==JsonTokenType.Null?null:ExactDecimalJsonCodec.Read(ref reader,18,6,"PurchaseUnitConversion");
    public override void Write(Utf8JsonWriter writer,decimal? value,JsonSerializerOptions options)
    { if(value.HasValue) writer.WriteNumberValue(value.Value);else writer.WriteNullValue(); }
}
/// <summary>Existing request discount is7,4; business validation separately restricts0–100.</summary>
public sealed class RequestDiscountJsonConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
        => reader.TokenType==JsonTokenType.Null?null:ExactDecimalJsonCodec.Read(ref reader,7,4,"RequestDiscount");
    public override void Write(Utf8JsonWriter writer,decimal? value,JsonSerializerOptions options)
    { if(value.HasValue) writer.WriteNumberValue(value.Value);else writer.WriteNullValue(); }
}
