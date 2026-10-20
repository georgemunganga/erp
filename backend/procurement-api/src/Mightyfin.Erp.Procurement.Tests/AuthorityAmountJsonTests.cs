using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class AuthorityAmountJsonTests
{
    [Theory]
    [InlineData("\"99999999999999.9999\"")]
    [InlineData("99999999999999.9999")]
    [InlineData("9.99999999999999999e13")]
    public void Maximum_round_trips_as_exact_string_without_double_rounding(string value)
    {
        var draft = JsonSerializer.Deserialize<SpendingAuthorityDraft>("{\"MaximumAmount\":" + value + "}")!;
        Assert.Equal(99_999_999_999_999.9999m, draft.MaximumAmount);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(draft));
        Assert.Equal("99999999999999.9999", json.RootElement.GetProperty("MaximumAmount").GetString());
    }

    [Theory]
    [InlineData("\"1.00001\"")]
    [InlineData("1.00001")]
    [InlineData("99999999999999.99999")]
    [InlineData("100000000000000")]
    [InlineData("\"100000000000000\"")]
    [InlineData("1e100000")]
    [InlineData("\"NaN\"")]
    public void Excess_precision_and_overflow_cannot_be_silently_rounded(string value)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SpendingAuthorityDraft>("{\"MaximumAmount\":" + value + "}"));

    [Fact]
    public void Legacy_numeric_and_string_represent_the_same_finite_decimal()
    {
        var numeric = JsonSerializer.Deserialize<SpendingAuthorityDraft>("{\"MaximumAmount\":123.4500}")!;
        var text = JsonSerializer.Deserialize<SpendingAuthorityDraft>("{\"MaximumAmount\":\"123.4500\"}")!;
        Assert.Equal(numeric.MaximumAmount, text.MaximumAmount);
        Assert.Equal("123.45", AuthorityAmountJsonConverter.Format(text.MaximumAmount));
    }
}
