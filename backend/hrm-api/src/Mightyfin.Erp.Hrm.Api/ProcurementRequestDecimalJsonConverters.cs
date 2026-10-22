using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Procurement.Application;
namespace Mightyfin.Erp.Hrm.Api;

// Thin transport adapters: share the existing bounded raw numeric/string lexeme codec.
public sealed class RequestLineDecimalJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => ExactDecimalJsonCodec.Read(ref reader, 18, 4, "RequestAmount");
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}
public sealed class RequestLineNullableDecimalJsonConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : ExactDecimalJsonCodec.Read(ref reader, 18, 4, "RequestAmount");
    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    { if (value.HasValue) writer.WriteNumberValue(value.Value); else writer.WriteNullValue(); }
}
