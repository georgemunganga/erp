using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Uses the actual mapped route input records. PostgreSQL probe separately
/// exercises raw HTTP rejection/no persistence and valid Finance exponent roundtrip.</summary>
public sealed class ProcurementOrderExactInputJsonTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("quantity")]
    [InlineData("unitPrice")]
    [InlineData("taxAmount")]
    public void Every_PO_route_line_property_rejects_lexeme_precision_before_decimal_rounding(string field)
    {
        var body = "{\"supplierId\":\"" + Guid.NewGuid() + "\",\"currencyCode\":\"ZMW\",\"lines\":[{\"description\":\"Exact\",\"unitCode\":\"each\",\""
            + field + "\":1.00000000000000000000000000001}]}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OrderDraftInput>(body, Json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ConvertApprovedOrderInput>("{\"draft\":" + body + ",\"sources\":[]}", Json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OrderSourceDraftInput>("{\"expectedOrderVersion\":1,\"draft\":" + body + "}", Json));
    }

    [Fact]
    public void Actual_Finance_confirmation_and_both_release_inputs_preserve_exponent_eight_places()
    {
        var confirmation = JsonSerializer.Deserialize<OrderFinanceInput>("{\"confirmedGrossAmount\":\"1E-08\"}", Json)!;
        var release = JsonSerializer.Deserialize<OrderFinanceResolutionInput>("{\"releasedGrossAmount\":\"1E-08\"}", Json)!;
        Assert.Equal(.00000001m, confirmation.ConfirmedGrossAmount);
        Assert.Equal(.00000001m, release.ReleasedGrossAmount);
        using var output = JsonDocument.Parse(JsonSerializer.Serialize(confirmation, Json));
        Assert.Equal(JsonValueKind.Number, output.RootElement.GetProperty("confirmedGrossAmount").ValueKind);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OrderFinanceInput>("{\"confirmedGrossAmount\":0.000000001}", Json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OrderFinanceResolutionInput>("{\"releasedGrossAmount\":1e-1000}", Json));
    }
}
