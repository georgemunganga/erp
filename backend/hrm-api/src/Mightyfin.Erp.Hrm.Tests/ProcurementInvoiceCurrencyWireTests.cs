using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementInvoiceCurrencyWireTests
{
    [Fact]
    public void Saved_calculation_uses_original_line_numbers_even_when_canonical_ids_sort_differently()
    {
        var first = new SupplierInvoiceLine { Id = Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"), LineNumber = 1, Quantity = 2m, UnitPrice = 3.1234m };
        var second = new SupplierInvoiceLine { Id = Guid.Parse("11111111-1111-4111-8111-111111111111"), LineNumber = 2, Quantity = 1m, UnitPrice = 5.1234m };
        var precision = new InvoiceCurrencyPrecisionSnapshot("ZMW", Guid.NewGuid(), 1, 2, InvoiceMoneyRounding.ToEven);
        // The retained codec canonically sorts by ID; original line order is independent.
        var result = InvoiceCurrencyRoundedCalculation.Calculate(precision,
            [new(second.Id, second.Quantity, second.UnitPrice, 0m), new(first.Id, first.Quantity, first.UnitPrice, 0m)]);
        var json = JsonSerializer.SerializeToElement(ProcurementInvoiceIntakeRoutes.CalculationView(result, [first, second]));
        var lines = json.GetProperty("lines").EnumerateArray().ToArray();
        Assert.Equal(1, lines[0].GetProperty("lineNumber").GetInt32());
        Assert.Equal("2", lines[0].GetProperty("quantity").GetString());
        Assert.Equal("6.25", lines[0].GetProperty("net").GetString());
        Assert.Equal(2, lines[1].GetProperty("lineNumber").GetInt32());
        Assert.Equal("5.12", lines[1].GetProperty("net").GetString());
    }
}
