using System.Text.Json;
using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public enum QuotationValidity { Unspecified, Fixed, OpenEnded }
public enum QuotationDiscount { None, Amount, Percent }
public enum QuotationQuotedTax { Exclusive, Inclusive }

/// <summary>Supplier documentary values, not verified Core catalog references.</summary>
public sealed record SupplierOfferedPaymentTerms(string? Code, string? Text);

/// <summary>
/// Pure proposed offer contract, not a persisted entity, command or document authority.
/// Documents must eventually be supplied by the real Core-resolved selection adapter.
/// Structural validation never certifies an upload, scope, availability or clean scan.
/// </summary>
public sealed record QuotationCommercialTerms(string? SupplierQuoteReference, DateOnly? QuoteDate,
    QuotationValidity Validity, DateOnly? ValidUntil, SupplierOfferedPaymentTerms? PaymentTerms,
    string? WarrantyText, string? DeliveryTermsText, DateOnly? DeliveryDate,
    IReadOnlyList<DocumentUseSelection>? Documents);

public sealed record QuotationCommercialLine(Guid SourcingLineId, decimal Quantity, decimal UnitPrice,
    QuotationDiscount Discount, decimal DiscountValue, QuotationQuotedTax TaxTreatment,
    decimal QuotedTaxAmount, decimal DeliveryAmount, string? SupplierOfferedTaxCode);

/// <summary>Canonical exact documentary totals. No database precision or tax-law claim.</summary>
public sealed record QuotationCommercialTotals(string GoodsBeforeDiscount, string DiscountAmount,
    string GoodsAfterDiscount, string NetGoods, string QuotedTax, string Delivery, string Payable);

public static class PrincipalQuotationCommercialTerms
{
    public static void Validate(QuotationCommercialTerms? terms)
    {
        if (terms is null || !Enum.IsDefined(terms.Validity)) Fail();
        var value = terms!;
        Text(value.SupplierQuoteReference); Text(value.WarrantyText); Text(value.DeliveryTermsText);
        if (value.PaymentTerms is not null)
        {
            Text(value.PaymentTerms.Code); Text(value.PaymentTerms.Text);
            if (value.PaymentTerms.Code is null && value.PaymentTerms.Text is null) Fail();
        }
        if (value.QuoteDate == DateOnly.MinValue || value.ValidUntil == DateOnly.MinValue
            || value.DeliveryDate == DateOnly.MinValue) Fail();
        if (value.Validity == QuotationValidity.Fixed)
        {
            if (value.ValidUntil is null || (value.QuoteDate is { } date && value.ValidUntil < date)) Fail();
        }
        else if (value.ValidUntil is not null) Fail();
        if (value.Documents is null) Fail();
        var versions = new HashSet<Guid>();
        foreach (var document in value.Documents!)
        {
            if (document is null || string.IsNullOrWhiteSpace(document.OwnerModule)
                || string.IsNullOrWhiteSpace(document.OwnerType) || document.OwnerId == Guid.Empty
                || document.DocumentId == Guid.Empty || document.VersionId == Guid.Empty
                || document.DocumentRevision < 1 || document.VersionRevision < 1
                || document.Sha256 is not { Length: 64 }
                || document.Sha256.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f'))
                || !versions.Add(document.VersionId)) Fail();
        }
    }

    public static QuotationCommercialTotals Calculate(IReadOnlyList<QuotationCommercialLine>? lines)
    {
        if (lines is null || lines.Count == 0) Fail();
        var ids = new HashSet<Guid>();
        decimal gross = 0, discount = 0, goods = 0, net = 0, tax = 0, delivery = 0, payable = 0;
        // Stable source ordering also keeps exact intermediate arithmetic deterministic.
        if (lines!.Any(line => line is null)) Fail();
        foreach (var line in lines!.OrderBy(line => line.SourcingLineId))
        {
            if (line.SourcingLineId == Guid.Empty || !ids.Add(line.SourcingLineId)
                || line.Quantity <= 0 || line.UnitPrice < 0 || line.DiscountValue < 0
                || line.QuotedTaxAmount < 0 || line.DeliveryAmount < 0
                || !Enum.IsDefined(line.Discount) || !Enum.IsDefined(line.TaxTreatment)) Fail();
            Text(line.SupplierOfferedTaxCode);
            var basis = ExactDecimalArithmetic.Multiply(line.Quantity, line.UnitPrice);
            var reduction = line.Discount switch
            {
                QuotationDiscount.None when line.DiscountValue == 0 => 0,
                QuotationDiscount.Amount => line.DiscountValue,
                QuotationDiscount.Percent when line.DiscountValue <= 100 =>
                    ExactDecimalArithmetic.PercentOf(basis, line.DiscountValue),
                _ => throw new InvalidOperationException("QuotationCommercialTermsInvalid"),
            };
            if (reduction > basis) Fail();
            var after = ExactDecimalArithmetic.Subtract(basis, reduction);
            if (line.TaxTreatment == QuotationQuotedTax.Inclusive && line.QuotedTaxAmount > after) Fail();
            var lineNet = line.TaxTreatment == QuotationQuotedTax.Inclusive
                ? ExactDecimalArithmetic.Subtract(after, line.QuotedTaxAmount) : after;
            var linePayable = ExactDecimalArithmetic.Add(ExactDecimalArithmetic.Add(lineNet, line.QuotedTaxAmount), line.DeliveryAmount);
            gross = ExactDecimalArithmetic.Add(gross, basis);
            discount = ExactDecimalArithmetic.Add(discount, reduction);
            goods = ExactDecimalArithmetic.Add(goods, after);
            net = ExactDecimalArithmetic.Add(net, lineNet);
            tax = ExactDecimalArithmetic.Add(tax, line.QuotedTaxAmount);
            delivery = ExactDecimalArithmetic.Add(delivery, line.DeliveryAmount);
            payable = ExactDecimalArithmetic.Add(payable, linePayable);
        }
        return new(Exact(gross), Exact(discount), Exact(goods), Exact(net), Exact(tax), Exact(delivery), Exact(payable));
    }

    public static string Snapshot(QuotationCommercialTerms? terms, IReadOnlyList<QuotationCommercialLine>? lines)
    {
        Validate(terms);
        var totals = Calculate(lines);
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            terms!.SupplierQuoteReference,
            quoteDate = Date(terms.QuoteDate), validity = terms.Validity.ToString(), validUntil = Date(terms.ValidUntil),
            supplierOfferedPaymentTerms = terms.PaymentTerms,
            terms.WarrantyText, terms.DeliveryTermsText, deliveryDate = Date(terms.DeliveryDate),
            // These are supplied selections, not acquired Core pins or certified documents.
            proposedDocumentSelections = terms.Documents!.OrderBy(d => d.DocumentId).ThenBy(d => d.VersionId),
            lines = lines!.OrderBy(l => l.SourcingLineId).Select(l => new
            {
                l.SourcingLineId, quantity = Exact(l.Quantity), unitPrice = Exact(l.UnitPrice),
                discountKind = l.Discount.ToString(), discountValue = Exact(l.DiscountValue),
                discountBasis = "GoodsBeforeQuotedTaxExcludingDelivery",
                taxTreatment = l.TaxTreatment.ToString(), quotedTax = Exact(l.QuotedTaxAmount),
                delivery = Exact(l.DeliveryAmount), l.SupplierOfferedTaxCode,
            }), totals,
        });
    }

    private static string Exact(decimal value) => ProcurementEvidenceEncoding.Decimal(value);
    private static string? Date(DateOnly? value) => value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    private static void Text(string? value) { if (value is not null && string.IsNullOrWhiteSpace(value)) Fail(); }
    private static void Fail() => throw new InvalidOperationException("QuotationCommercialTermsInvalid");
}
