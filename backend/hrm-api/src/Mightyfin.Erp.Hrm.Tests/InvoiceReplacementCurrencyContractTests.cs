using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class InvoiceReplacementCurrencyContractTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Replacement_requires_explicit_original_currency_before_storage(string? currency)
    {
        var http = new DefaultHttpContext();
        var scope = new ProcurementRequestScope();
        scope.Set("replacement-currency-test", Guid.NewGuid(), Guid.NewGuid().ToString("D"),
            Guid.NewGuid(), null, false, "test");
        var corrected = new InvoiceIntakeInput(Guid.NewGuid(), null, "SUPPLIER-CORRECTED-2",
            new DateOnly(2026, 10, 30), null, currency,
            [new InvoiceIntakeLineInput("Corrected supplier line", 1m, 5.1234m, 0m, null, null)],
            IntakeSchemaVersion: 2);
        // Null DbContext proves a missing currency is rejected before touching
        // a replacement receipt, Core default, policy, or any business row.
        var response = await ProcurementInvoiceIntakeRoutes.ReplaceCurrencyInvoice(Guid.NewGuid(),
            new InvoiceReplacementInput(4, "Supplier corrected the issued invoice", corrected),
            http.Request, null!, scope, default);
        Assert.Equal(422, Assert.IsAssignableFrom<IStatusCodeHttpResult>(response).StatusCode);
        var body = JsonSerializer.SerializeToElement(Assert.IsAssignableFrom<IValueHttpResult>(response).Value);
        Assert.Equal("validation-failed", body.GetProperty("code").GetString());
        Assert.Contains("currency explicitly", body.GetProperty("message").GetString());
    }

    [Fact]
    public void Replacement_retains_source_currency_even_if_company_default_changes()
    {
        Assert.True(ProcurementInvoiceIntakeRoutes.SameReplacementCurrency("ZMW", " zmw "));
        Assert.False(ProcurementInvoiceIntakeRoutes.SameReplacementCurrency("ZMW", "USD"));
        Assert.False(ProcurementInvoiceIntakeRoutes.SameReplacementCurrency("ZMW", "EUR"));
    }
}
