using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class CommercialDecimalJsonTests
{
    [Theory]
    [InlineData("12.3456")]
    [InlineData("\"12.34560000\"")]
    [InlineData("\"1.23456e1\"")]
    public void Stored_line_value_is_exact_and_legacy_output_stays_numeric(string raw)
    {
        var value = JsonSerializer.Deserialize<Line>("{\"Amount\":" + raw + "}")!;
        Assert.Equal(12.3456m, value.Amount);
        using var output = JsonDocument.Parse(JsonSerializer.Serialize(value));
        Assert.Equal(JsonValueKind.Number, output.RootElement.GetProperty("Amount").ValueKind);
        Assert.Equal(12.3456m, output.RootElement.GetProperty("Amount").GetDecimal());
    }

    [Theory]
    [InlineData("12.34567")]
    [InlineData("\"12.34567\"")]
    [InlineData("1.00000000000000000000000000001")]
    [InlineData("0.0000000000000000000000000000000001")]
    [InlineData("1e-1000")]
    [InlineData("1e1001")]
    [InlineData("1e-1001")]
    [InlineData("100000000000000")]
    [InlineData("\"NaN\"")]
    public void Line_lexeme_cannot_round_into_a_valid_stored_value(string raw)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Line>("{\"Amount\":" + raw + "}"));

    [Theory]
    [InlineData("\"1E-08\"")]
    [InlineData("1e-8")]
    [InlineData("0.00000001")]
    public void Gross_transport_preserves_eight_places_and_canonical_exponent_strings(string raw)
    {
        var value = JsonSerializer.Deserialize<Gross>("{\"Amount\":" + raw + "}")!;
        Assert.Equal(.00000001m, value.Amount);
        Assert.Equal(value.Amount, JsonSerializer.Deserialize<Gross>(JsonSerializer.Serialize(value))!.Amount);
    }

    [Theory]
    [InlineData("0.000000001")]
    [InlineData("\"1e-29\"")]
    [InlineData("1.00000000000000000000000000001")]
    [InlineData("79228162514264337593543950335.1")]
    [InlineData("79228162514264337593543950336")]
    [InlineData("1e100000")]
    [InlineData("\"-Infinity\"")]
    public void Gross_transport_rejects_lossy_clr_representation_or_unsupported_scale(string raw)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Gross>("{\"Amount\":" + raw + "}"));

    [Fact]
    public void Clr_maximum_and_scaled_trailing_zero_representation_are_exact()
    {
        Assert.Equal(decimal.MaxValue, ExactDecimalJsonCodec.Parse("79228162514264337593543950335", 38, 8));
        Assert.Equal(decimal.MaxValue, ExactDecimalJsonCodec.Parse("792281625142643375935439503350e-1", 38, 8));
        Assert.Equal(decimal.MinValue, ExactDecimalJsonCodec.Parse("-79228162514264337593543950335", 38, 8));
        Assert.False(ExactDecimalJsonCodec.IsRepresentable(.00001m, 18, 4));
        Assert.True(ExactDecimalJsonCodec.IsRepresentable(.00000001m, 38, 8));
    }

    [Fact]
    public void Token_and_exponent_work_are_bounded_before_big_integer_expansion()
    {
        Assert.Throws<JsonException>(() => ExactDecimalJsonCodec.Parse(new string('0', 129), 38, 8));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Gross>("{\"Amount\":\"" + new string('0', 129) + "\"}"));
        Assert.Throws<JsonException>(() => ExactDecimalJsonCodec.Parse("0e1001", 38, 8));
        Assert.Throws<JsonException>(() => ExactDecimalJsonCodec.Parse("1e-2147483648", 38, 8));
    }

    private sealed record Line([property: JsonConverter(typeof(CommercialLineDecimalJsonConverter))] decimal Amount);
    private sealed record Gross([property: JsonConverter(typeof(CommercialGrossDecimalJsonConverter))] decimal Amount);
}
