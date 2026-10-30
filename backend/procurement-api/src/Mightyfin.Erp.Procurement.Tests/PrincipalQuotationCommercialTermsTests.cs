using System.Globalization;
using System.Text.Json;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalQuotationCommercialTermsTests
{
    private static readonly Guid LineId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static QuotationCommercialTerms Terms() => new("SUP-0042", new(2026, 10, 30),
        QuotationValidity.Unspecified, null, new("NET30 offered", "30 days after acceptance"),
        "12 months offered", "Deliver to Lusaka", new(2026, 11, 15), Array.Empty<DocumentUseSelection>());
    private static QuotationCommercialLine Line() => new(LineId, 2m, 50m,
        QuotationDiscount.None, 0m, QuotationQuotedTax.Exclusive, 16m, 5m, "Supplier VAT code");
    private static DocumentUseSelection Document(Guid? version = null) => new("procurement", "supplier-bid", LineId,
        Guid.Parse("22222222-2222-4222-8222-222222222222"), version ?? Guid.Parse("33333333-3333-4333-8333-333333333333"), 1, 1, new string('a', 64));
    private static decimal D(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(QuotationDiscount.None, "0", QuotationQuotedTax.Exclusive, "16", "100", "121")]
    [InlineData(QuotationDiscount.Amount, "10", QuotationQuotedTax.Exclusive, "16", "90", "111")]
    [InlineData(QuotationDiscount.Percent, "10", QuotationQuotedTax.Exclusive, "16", "90", "111")]
    [InlineData(QuotationDiscount.Percent, "100", QuotationQuotedTax.Exclusive, "0", "0", "5")]
    [InlineData(QuotationDiscount.None, "0", QuotationQuotedTax.Inclusive, "16", "84", "105")]
    [InlineData(QuotationDiscount.Amount, "10", QuotationQuotedTax.Inclusive, "16", "74", "95")]
    [InlineData(QuotationDiscount.Percent, "100", QuotationQuotedTax.Inclusive, "0", "0", "5")]
    public void Exact_goods_discount_precedes_declared_tax_and_excludes_delivery(QuotationDiscount kind,
        string value, QuotationQuotedTax tax, string amount, string net, string payable)
    {
        var result = PrincipalQuotationCommercialTerms.Calculate(new[] { Line() with
            { Discount = kind, DiscountValue = D(value), TaxTreatment = tax, QuotedTaxAmount = D(amount) } });
        Assert.Equal(net, result.NetGoods); Assert.Equal(payable, result.Payable);
        Assert.Equal("5", result.Delivery);
    }

    [Theory]
    [InlineData(QuotationDiscount.None, "1", QuotationQuotedTax.Exclusive, "0")]
    [InlineData(QuotationDiscount.Amount, "101", QuotationQuotedTax.Exclusive, "0")]
    [InlineData(QuotationDiscount.Percent, "100.0001", QuotationQuotedTax.Exclusive, "0")]
    [InlineData(QuotationDiscount.Amount, "-1", QuotationQuotedTax.Exclusive, "0")]
    [InlineData(QuotationDiscount.Amount, "99", QuotationQuotedTax.Inclusive, "2")]
    [InlineData(QuotationDiscount.Percent, "100", QuotationQuotedTax.Inclusive, "0.0001")]
    [InlineData((QuotationDiscount)99, "0", QuotationQuotedTax.Exclusive, "0")]
    [InlineData(QuotationDiscount.None, "0", (QuotationQuotedTax)99, "0")]
    public void Invalid_or_over_discount_and_inclusive_tax_are_denied(QuotationDiscount kind,
        string value, QuotationQuotedTax tax, string amount) => Assert.Throws<InvalidOperationException>(() =>
        PrincipalQuotationCommercialTerms.Calculate(new[] { Line() with
            { Discount = kind, DiscountValue = D(value), TaxTreatment = tax, QuotedTaxAmount = D(amount) } }));

    [Fact]
    public void Zero_price_and_zero_discount_remain_valid_without_negative_price_workaround()
    {
        var result = PrincipalQuotationCommercialTerms.Calculate(new[] { Line() with
            { UnitPrice = 0m, QuotedTaxAmount = 0m, DeliveryAmount = 0m } });
        Assert.Equal("0", result.Payable);
    }

    [Fact]
    public void Percent_math_rejects_nonrepresentable_fraction_instead_of_rounding()
    {
        var smallest = D("0.0000000000000000000000000001");
        Assert.Throws<InvalidOperationException>(() => PrincipalQuotationCommercialTerms.Calculate(new[] {
            Line() with { Quantity = 1, UnitPrice = smallest, Discount = QuotationDiscount.Percent,
                DiscountValue = 1, QuotedTaxAmount = 0, DeliveryAmount = 0 } }));
    }

    [Fact]
    public void Percent_accepts_exact_final_result_without_requiring_representable_intermediate()
    {
        var smallest = D("0.0000000000000000000000000001");
        Assert.Equal(smallest, ExactDecimalArithmetic.PercentOf(100m, smallest));
        Assert.Equal(decimal.MaxValue, ExactDecimalArithmetic.PercentOf(decimal.MaxValue, 100m));
        var result = PrincipalQuotationCommercialTerms.Calculate(new[] { Line() with {
            Quantity = 1, UnitPrice = decimal.MaxValue, Discount = QuotationDiscount.Percent,
            DiscountValue = 100, QuotedTaxAmount = 0, DeliveryAmount = 0 } });
        Assert.Equal("0", result.Payable);
    }

    [Theory]
    [InlineData("0", "100", "0")]
    [InlineData("100", "0", "0")]
    [InlineData("100", "12.5", "12.5")]
    [InlineData("-100", "12.5", "-12.5")]
    [InlineData("100", "-12.5", "-12.5")]
    [InlineData("-100", "-12.5", "12.5")]
    public void Generic_percent_utility_is_exact_and_does_not_invent_discount_policy(string basis, string percent, string expected)
        => Assert.Equal(D(expected), ExactDecimalArithmetic.PercentOf(D(basis), D(percent)));

    [Fact]
    public void Equivalent_decimal_scales_produce_identical_snapshot()
    {
        var line = Line() with { Quantity = 2.0000m, UnitPrice = 50.00m, DiscountValue = 0.00m,
            QuotedTaxAmount = 16.000m, DeliveryAmount = 5.0000m };
        Assert.Equal(PrincipalQuotationCommercialTerms.Snapshot(Terms(), new[] { Line() }),
            PrincipalQuotationCommercialTerms.Snapshot(Terms(), new[] { line }));
    }

    [Fact]
    public void Quantity_times_price_and_aggregate_overflow_are_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => PrincipalQuotationCommercialTerms.Calculate(new[] {
            Line() with { Quantity = 2, UnitPrice = decimal.MaxValue } }));
        var large = Line() with { Quantity = 1, UnitPrice = decimal.MaxValue, QuotedTaxAmount = 0, DeliveryAmount = 0 };
        Assert.Throws<InvalidOperationException>(() => PrincipalQuotationCommercialTerms.Calculate(new[] {
            large, large with { SourcingLineId = Guid.NewGuid() } }));
    }

    [Theory]
    [InlineData(QuotationValidity.Unspecified, false)]
    [InlineData(QuotationValidity.OpenEnded, false)]
    [InlineData(QuotationValidity.Fixed, true)]
    public void Explicit_validity_modes_are_retained(QuotationValidity mode, bool fixedDate)
    {
        var terms = Terms() with { Validity = mode, ValidUntil = fixedDate ? new DateOnly(2026, 11, 30) : null };
        using var snapshot = JsonDocument.Parse(PrincipalQuotationCommercialTerms.Snapshot(terms, new[] { Line() }));
        Assert.Equal(mode.ToString(), snapshot.RootElement.GetProperty("validity").GetString());
        Assert.Equal("2026-10-30", snapshot.RootElement.GetProperty("quoteDate").GetString());
        Assert.Equal("2026-11-15", snapshot.RootElement.GetProperty("deliveryDate").GetString());
        if (fixedDate) Assert.Equal("2026-11-30", snapshot.RootElement.GetProperty("validUntil").GetString());
        else Assert.Equal(JsonValueKind.Null, snapshot.RootElement.GetProperty("validUntil").ValueKind);
    }

    [Fact]
    public void Draft_can_leave_reference_date_and_terms_unspecified()
    {
        PrincipalQuotationCommercialTerms.Validate(new(null, null, QuotationValidity.Unspecified, null,
            null, null, null, null, Array.Empty<DocumentUseSelection>()));
    }

    [Fact]
    public void Invalid_validity_and_ambiguous_empty_payment_terms_fail()
    {
        foreach (var terms in new[] {
            Terms() with { Validity = QuotationValidity.Fixed },
            Terms() with { Validity = QuotationValidity.Fixed, ValidUntil = new(2026, 10, 29) },
            Terms() with { Validity = QuotationValidity.OpenEnded, ValidUntil = new(2026, 11, 30) },
            Terms() with { ValidUntil = new(2026, 11, 30) },
            Terms() with { Validity = (QuotationValidity)99 },
            Terms() with { QuoteDate = DateOnly.MinValue },
            Terms() with { PaymentTerms = new(null, null) },
            Terms() with { SupplierQuoteReference = " " } })
            Assert.Throws<InvalidOperationException>(() => PrincipalQuotationCommercialTerms.Validate(terms));
    }

    [Fact]
    public void Multiple_actual_selection_shapes_sort_canonically_without_certifying_documents()
    {
        var doc1 = Document(); var doc2 = Document(Guid.Parse("44444444-4444-4444-8444-444444444444"));
        var first = Terms() with { Documents = new[] { doc2, doc1 } };
        var second = first with { Documents = new[] { doc1, doc2 } };
        var line2 = Line() with { SourcingLineId = Guid.Parse("55555555-5555-4555-8555-555555555555") };
        var originalDocuments = first.Documents!.ToArray();
        var originalLines = new[] { line2, Line() }; var savedLines = originalLines.ToArray();
        var snapshot = PrincipalQuotationCommercialTerms.Snapshot(first, originalLines);
        Assert.Equal(snapshot, PrincipalQuotationCommercialTerms.Snapshot(second, new[] { Line(), line2 }));
        Assert.Equal<DocumentUseSelection>(originalDocuments, first.Documents!); Assert.Equal(savedLines, originalLines);
        Assert.DoesNotContain("scanStatus", snapshot); Assert.DoesNotContain("CorePaymentTermRef", snapshot);
    }

    [Fact]
    public void Null_duplicate_or_tampered_selection_shape_is_rejected()
    {
        foreach (var docs in new IReadOnlyList<DocumentUseSelection>?[] { null, new[] { (DocumentUseSelection)null! },
            new[] { Document(), Document() }, new[] { Document() with { Sha256 = "fake" } },
            new[] { Document() with { VersionId = Guid.Empty } }, new[] { Document() with { DocumentRevision = 0 } } })
            Assert.Throws<InvalidOperationException>(() => PrincipalQuotationCommercialTerms.Snapshot(Terms() with { Documents = docs }, new[] { Line() }));
        Assert.Throws<InvalidOperationException>(() => PrincipalQuotationCommercialTerms.Snapshot(null, new[] { Line() }));
    }

    [Fact]
    public void Null_duplicate_and_invalid_price_lines_fail_before_snapshot()
    {
        foreach (var lines in new IReadOnlyList<QuotationCommercialLine>?[] { null, Array.Empty<QuotationCommercialLine>(),
            new[] { (QuotationCommercialLine)null! }, new[] { Line(), Line() },
            new[] { Line() with { Quantity = 0 } }, new[] { Line() with { UnitPrice = -1 } },
            new[] { Line() with { QuotedTaxAmount = -1 } }, new[] { Line() with { DeliveryAmount = -1 } } })
            Assert.Throws<InvalidOperationException>(() => PrincipalQuotationCommercialTerms.Snapshot(Terms(), lines));
    }

    [Fact]
    public void Every_offered_term_and_price_change_changes_snapshot()
    {
        var terms = Terms(); var lines = new[] { Line() }; var original = PrincipalQuotationCommercialTerms.Snapshot(terms, lines);
        foreach (var changed in new[] { terms with { SupplierQuoteReference = "SUP-0043" }, terms with { QuoteDate = new(2026, 10, 31) },
            terms with { WarrantyText = "24 months" }, terms with { DeliveryTermsText = "Collect" },
            terms with { DeliveryDate = new(2026, 11, 16) }, terms with { PaymentTerms = new("Other offered code", "Other offered text") },
            terms with { Documents = new[] { Document() } } })
            Assert.NotEqual(original, PrincipalQuotationCommercialTerms.Snapshot(changed, lines));
        Assert.NotEqual(original, PrincipalQuotationCommercialTerms.Snapshot(terms, new[] { Line() with { SupplierOfferedTaxCode = "Other offered code" } }));
        Assert.NotEqual(original, PrincipalQuotationCommercialTerms.Snapshot(terms, new[] { Line() with { Discount = QuotationDiscount.Amount, DiscountValue = 1 } }));
    }
}
