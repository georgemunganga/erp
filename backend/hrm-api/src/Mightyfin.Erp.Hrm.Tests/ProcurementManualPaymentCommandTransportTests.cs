using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementManualPaymentCommandTransportTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Manual_payment_command_activation_defaults_off()
    {
        var options = new ProcurementManualPaymentOptions();
        Assert.False(options.Enabled);
        Assert.False(options.ExecutionDatabaseConfigured);
    }


    [Theory]
    [InlineData("0.0001", "0.0001")]
    [InlineData("\"99999999999999.9999\"", "99999999999999.9999")]
    public void Create_preserves_exact_numeric_18_4_lexeme(string amount, string expected)
    {
        var json = $$"""
            {"branchId":"11111111-1111-4111-8111-111111111111","invoiceId":"22222222-2222-4222-8222-222222222222",
             "amount":{{amount}},"approverPrincipalId":"33333333-3333-4333-8333-333333333333",
             "recorderPrincipalId":"44444444-4444-4444-8444-444444444444",
             "confirmerPrincipalId":"55555555-5555-4555-8555-555555555555","purpose":"External transfer","title":"Example"}
            """;
        var result = JsonSerializer.Deserialize<ProcurementManualPaymentCommandRoutes.CreateInput>(json, Web);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Amount.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("0.00001")]
    [InlineData("100000000000000")]
    [InlineData("1e1000")]
    public void Create_rejects_precision_or_range_that_would_round(string amount)
    {
        var json = $$"""
            {"branchId":"11111111-1111-4111-8111-111111111111","invoiceId":"22222222-2222-4222-8222-222222222222",
             "amount":{{amount}},"approverPrincipalId":"33333333-3333-4333-8333-333333333333",
             "recorderPrincipalId":"44444444-4444-4444-8444-444444444444",
             "confirmerPrincipalId":"55555555-5555-4555-8555-555555555555","purpose":null,"title":null}
            """;
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProcurementManualPaymentCommandRoutes.CreateInput>(json, Web));
    }

    [Fact]
    public void Create_rejects_unmapped_bank_or_execution_fields()
    {
        const string json = """
            {"branchId":"11111111-1111-4111-8111-111111111111","invoiceId":"22222222-2222-4222-8222-222222222222",
             "amount":"1.0000","approverPrincipalId":"33333333-3333-4333-8333-333333333333",
             "recorderPrincipalId":"44444444-4444-4444-8444-444444444444",
             "confirmerPrincipalId":"55555555-5555-4555-8555-555555555555","bankAccount":"12345"}
            """;
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProcurementManualPaymentCommandRoutes.CreateInput>(json, Web));
    }
}
