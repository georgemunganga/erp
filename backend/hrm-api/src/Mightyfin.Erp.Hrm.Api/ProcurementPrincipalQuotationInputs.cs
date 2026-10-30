using System.Text.Json.Serialization;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record QuotationLineInput(Guid SourcingLineId,
        [property: JsonConverter(typeof(CommercialLineDecimalJsonConverter))] decimal Quantity,
        [property: JsonConverter(typeof(CommercialLineDecimalJsonConverter))] decimal UnitPrice,
        [property: JsonConverter(typeof(JsonStringEnumConverter<QuotationDiscount>))] QuotationDiscount Discount,
        [property: JsonConverter(typeof(CommercialLineDecimalJsonConverter))] decimal DiscountValue,
        [property: JsonConverter(typeof(JsonStringEnumConverter<QuotationQuotedTax>))] QuotationQuotedTax TaxTreatment,
        [property: JsonConverter(typeof(CommercialLineDecimalJsonConverter))] decimal QuotedTaxAmount,
        [property: JsonConverter(typeof(CommercialLineDecimalJsonConverter))] decimal DeliveryAmount,
        string? SupplierOfferedTaxCode, string? LeadTimeText, string? Notes);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record QuotationOfferInput(string? SupplierQuoteReference, DateOnly? QuoteDate,
        [property: JsonConverter(typeof(JsonStringEnumConverter<QuotationValidity>))] QuotationValidity Validity,
        DateOnly? ValidUntil, SupplierOfferedPaymentTerms? PaymentTerms, string? WarrantyText,
        string? DeliveryTermsText, DateOnly? DeliveryDate, DocumentUseSelection[]? Documents);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record QuotationDraftInput(long ExpectedEventVersion, long ExpectedInvitationVersion,
        long? ExpectedBidVersion, string? Reason, DateTimeOffset? ReceivedAt,
        QuotationOfferInput? Offer, QuotationLineInput[]? Lines);

    // The endpoint owns the operation; clients cannot claim a different action,
    // principal, employee alias, quotation ID, scope or verified document pin.
    internal static PrincipalQuotationCommand QuotationInput(string operation, QuotationDraftInput? input)
    {
        if (input is null || input.Lines?.Any(x => x is null) == true)
            throw new Problem("QuotationInputInvalid", 422);
        var offer = input.Offer is null ? null : new QuotationCommercialTerms(input.Offer.SupplierQuoteReference,
            input.Offer.QuoteDate, input.Offer.Validity, input.Offer.ValidUntil, input.Offer.PaymentTerms,
            input.Offer.WarrantyText, input.Offer.DeliveryTermsText, input.Offer.DeliveryDate, input.Offer.Documents);
        return PrincipalQuotationCommandEvidence.Normalize(new(operation, input.ExpectedEventVersion,
            input.ExpectedInvitationVersion, input.ExpectedBidVersion, input.Reason, input.ReceivedAt, offer,
            input.Lines?.Select(x => new PrincipalQuotationStorage.LineInput(x.SourcingLineId, x.Quantity,
                x.UnitPrice, x.Discount, x.DiscountValue, x.TaxTreatment, x.QuotedTaxAmount, x.DeliveryAmount,
                x.SupplierOfferedTaxCode, x.LeadTimeText, x.Notes)).ToArray()));
    }
}
