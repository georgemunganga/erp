using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceAmountCalculationEvidenceTests
{
    internal const string Actor = "12345678-1234-1234-1234-123456789abc";
    internal static readonly DateTimeOffset At = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);
    internal static (SupplierInvoice Invoice, SupplierInvoiceLine[] Lines, InvoiceAmountCalculation Row) Fixture()
    {
        var invoice = new SupplierInvoice { TenantId = "amount-tests", LegalEntityId = Guid.NewGuid(),
            CreatedBy = Actor, CurrencyCode = "ZMW", IntakeChannel = "Manual", SupplierId = Guid.NewGuid(), InvoiceNumber = "V2-1",
            NormalizedInvoiceNumber = "V2-1", InvoiceDate = new(2026, 10, 30),
            NetAmount = 5.12m, TaxAmount = .10m, GrossAmount = 5.22m };
        var line = new SupplierInvoiceLine { TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, Description = "Precision", LineNumber = 1,
            Quantity = 1m, UnitPrice = 5.1234m, TaxAmount = .10m };
        var lines = new[] { line };
        var row = InvoiceAmountCalculationEvidence.Create(invoice, lines,
            new("ZMW", Guid.NewGuid(), 1, 2, InvoiceMoneyRounding.ToEven), new string('a',64), At.AddDays(-1), At, Actor);
        return (invoice, lines, row);
    }

    [Fact]
    public void Retains_raw_eight_decimal_product_and_signed_adjustment_without_changing_invoice()
    {
        var (invoice, lines, row) = Fixture();
        var result = InvoiceAmountCalculationEvidence.Verify(row, invoice, lines);
        Assert.Equal(5.1234m, result.RawNet); Assert.Equal(-.0034m, result.RoundingAdjustment);
        Assert.Contains("\"rawNet\":\"5.12340000\"", row.CalculationJson);
        Assert.Contains("\"roundingAdjustment\":\"-0.00340000\"", row.CalculationJson);
        Assert.Equal(5.1234m, lines[0].UnitPrice); Assert.Equal(5.12m, invoice.NetAmount);
        Assert.Equal(64, row.CalculationPolicySha256.Length);
        Assert.Contains(row.CalculationSha256, InvoiceAmountCalculationEvidence.MarkerJson(row));
    }

    [Theory]
    [InlineData("duplicate")][InlineData("unknown")][InlineData("numeric")][InlineData("whitespace")]
    public void Canonical_payload_rejects_duplicate_unknown_wrong_type_or_reformatted_keys(string mutation)
    {
        var (invoice, lines, row) = Fixture();
        row.CalculationJson = mutation switch
        {
            "duplicate" => row.CalculationJson.Insert(1, "\"schemaVersion\":2,"),
            "unknown" => row.CalculationJson.Insert(1, "\"extra\":true,"),
            "numeric" => row.CalculationJson.Replace("\"5.12340000\"", "5.12340000"),
            _ => " " + row.CalculationJson
        };
        // Even a caller recomputing both digest fields cannot admit noncanonical bytes.
        row.CalculationPolicySha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(row.CalculationJson);
        row.CalculationSha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(row.CalculationJson)));
        Assert.Throws<InvalidOperationException>(() => InvoiceAmountCalculationEvidence.Verify(row, invoice, lines));
    }

    [Theory]
    [InlineData("header")][InlineData("scope")][InlineData("line")][InlineData("reference")]
    [InlineData("future")][InlineData("actor")][InlineData("updated")][InlineData("version")]
    public void Rejects_changed_original_reference_actor_or_mutable_child(string mutation)
    {
        var (invoice, lines, row) = Fixture();
        switch(mutation)
        {
            case "header": invoice.GrossAmount += .01m;
                row.InvoiceOriginalSha256 = InvoiceMappingEvidence.OriginalDigest(invoice, lines); break;
            case "scope": lines[0].LegalEntityId = Guid.NewGuid(); break;
            case "line": lines[0].UnitPrice += .0001m; break;
            case "reference": row.CoreCurrencyReferenceRevision++; break;
            case "future": row.CorePublishedAtUtc = At.AddSeconds(1); break;
            case "actor": row.CreatedBy = Guid.NewGuid().ToString(); break;
            case "updated": row.UpdatedAt = At; break;
            case "version": row.Version = 2; break;
        }
        Assert.Throws<InvalidOperationException>(() => InvoiceAmountCalculationEvidence.Verify(row, invoice, lines));
    }

    [Fact]
    public void Missing_inactive_duplicate_and_foreign_original_lines_are_denied()
    {
        var (invoice, lines, row) = Fixture();
        Assert.Throws<InvalidOperationException>(() => InvoiceAmountCalculationEvidence.Verify(row, invoice, []));
        Assert.Throws<InvalidOperationException>(() => InvoiceAmountCalculationEvidence.Verify(row, invoice, [lines[0],lines[0]]));
        lines[0].IsActive = false;
        Assert.Throws<InvalidOperationException>(() => InvoiceAmountCalculationEvidence.Verify(row, invoice, lines));
    }

    [Fact]
    public void Database_save_delay_is_valid_but_before_observation_and_nonUTC_are_denied()
    {
        var (invoice, lines, row) = Fixture();
        var normalized = InvoiceAmountCalculationEvidence.Create(invoice, lines,
            new("ZMW", row.CoreCurrencyReferenceId, 1, 2, InvoiceMoneyRounding.ToEven),
            new string('a',64), At.AddDays(-1).AddTicks(7), At.AddTicks(7), Actor);
        Assert.Equal(At, normalized.ObservedAtUtc);
        Assert.Equal(At.AddDays(-1), normalized.CorePublishedAtUtc);
        row.CreatedAt = At.AddSeconds(1);
        _ = InvoiceAmountCalculationEvidence.Verify(row, invoice, lines);
        row.CreatedAt = At.AddTicks(-1);
        Assert.Throws<InvalidOperationException>(() => InvoiceAmountCalculationEvidence.Verify(row, invoice, lines));
        row.CreatedAt = At; row.ObservedAtUtc = At.ToOffset(TimeSpan.FromHours(2));
        Assert.Throws<InvalidOperationException>(() => InvoiceAmountCalculationEvidence.Verify(row, invoice, lines));
    }
}
