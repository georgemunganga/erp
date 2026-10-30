using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed partial class ProcurementRequestHttpTests
{
    [Fact]
    public async Task Currency_preview_and_v2_capture_fail_closed_without_changing_invoice_history_when_disabled()
    {
        await using var fixture = await Fixture.Start();
        var vendor = await fixture.AddInvoiceSupplier();
        await fixture.ConfigureCoreRole(fixture.Owner, "ap_processor");
        var branch = await fixture.IntakeSelectedBranch();
        var before = await fixture.InvoiceReadinessFootprint();
        var input = new InvoiceIntakeInput(vendor, null, "V2-NOT-ENABLED", new(2026, 10, 30), null, "ZMW",
            [new("Goods", 1m, 5.1234m, 0m, null, null)], IntakeSchemaVersion: 2);
        foreach (var path in new[] { "/api/procurement/v1/invoices/calculation-preview", "/api/procurement/v1/invoices" })
        {
            using var response = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner,
                input, "ap_processor", selectedLocation: branch);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        Assert.Equal(before, await fixture.InvoiceReadinessFootprint());
    }

    [Fact]
    public async Task Unknown_invoice_calculation_version_cannot_fall_back_to_legacy_capture()
    {
        await using var fixture = await Fixture.Start();
        var vendor = await fixture.AddInvoiceSupplier();
        await fixture.ConfigureCoreRole(fixture.Owner, "ap_processor");
        var before = await fixture.InvoiceReadinessFootprint();
        using var response = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/invoices", fixture.Owner,
            new InvoiceIntakeInput(vendor, null, "UNKNOWN-CALC", new(2026, 10, 30), null, "ZMW",
                [new("Goods", 1m, 5m, 0m, null, null)], IntakeSchemaVersion: 3),
            "ap_processor", selectedLocation: await fixture.IntakeSelectedBranch());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(before, await fixture.InvoiceReadinessFootprint());
    }

    [Fact]
    public async Task Missing_calculation_version_keeps_legacy_four_decimal_amounts_exact()
    {
        await using var fixture = await Fixture.Start();
        var vendor = await fixture.AddInvoiceSupplier();
        await fixture.ConfigureCoreRole(fixture.Owner, "ap_processor");
        using var response = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/invoices", fixture.Owner,
            new InvoiceIntakeInput(vendor, null, "LEGACY-FOUR-DECIMALS", new(2026, 10, 30), null, "ZMW",
                [new("Goods", 1m, 5.1234m, 0m, null, null)]),
            "ap_processor", selectedLocation: await fixture.IntakeSelectedBranch());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invoice = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5.1234m, invoice.GetProperty("grossAmount").GetDecimal());
    }
}
