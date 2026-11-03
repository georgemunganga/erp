using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Typed commercial columns and the exact owned V3 revision projection.
/// Document selections are proposals; only actual retained Core use-pin evidence
/// may appear as pins. No current employee link is needed to restore history.</summary>
public static class PrincipalQuotationStorage
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    public sealed record LineInput(Guid SourcingLineId, decimal Quantity, decimal UnitPrice,
        QuotationDiscount Discount, decimal DiscountValue, QuotationQuotedTax TaxTreatment,
        decimal QuotedTaxAmount, decimal DeliveryAmount, string? SupplierOfferedTaxCode,
        string? LeadTimeText, string? Notes);
    public sealed record StoredLine(Guid Id, Guid SourcingLineId, string Quantity, string UnitPrice,
        string DiscountKind, string DiscountValue, string TaxTreatment, string QuotedTaxAmount,
        string DeliveryAmount, string? SupplierOfferedTaxCode, string? LeadTimeText, string? Notes);
    public sealed record Pin(Guid Id, string TenantId, Guid LegalEntityId, DocumentUseReference Source,
        string OwnerModule, string OwnerType, Guid OwnerId, Guid DocumentId, Guid VersionId,
        long DocumentRevision, long VersionRevision, string Sha256);
    public sealed record Header(Guid Id, string TenantId, Guid LegalEntityId, Guid SourcingEventId,
        Guid SupplierId, Guid InvitationId, string Status, string CurrencyCode, string CapturedBy,
        long BidVersion, DateTimeOffset? ReceivedAt, string? CorrectionReason, QuotationCommercialTerms Offer);
    public sealed record Snapshot(int SchemaVersion, Header Header, IReadOnlyList<StoredLine> Lines,
        IReadOnlyList<Pin> DocumentPins, QuotationCommercialTotals Totals);
    public sealed record Restored(SupplierBid Bid, IReadOnlyList<SupplierBidLine> Lines,
        QuotationCommercialTerms Offer, IReadOnlyList<Pin> Pins);

    public static void Apply(SupplierBid bid, IReadOnlyList<SupplierBidLine> lines,
        QuotationCommercialTerms offer, IReadOnlyList<LineInput> input)
    {
        PrincipalQuotationCommercialTerms.Validate(offer);
        if (input is null || input.Count is < 1 or > 200 || input.Any(x => x is null)
            || lines is null || lines.Count != input.Count || lines.Any(x => x is null)
            || lines.Select(x => x.SourcingLineId).Distinct().Count() != lines.Count
            || input.Select(x => x.SourcingLineId).Distinct().Count() != input.Count)
            throw new InvalidOperationException("QuotationOfferLinesInvalid");
        Text(offer.SupplierQuoteReference, 200); Text(offer.PaymentTerms?.Code, 80);
        Text(offer.PaymentTerms?.Text, 2000); Text(offer.WarrantyText, 4000); Text(offer.DeliveryTermsText, 2000);
        if (offer.Documents!.Count > 100) throw new InvalidOperationException("QuotationDocumentsLimit");
        _ = PrincipalQuotationCommercialTerms.Calculate(input.Select(Commercial).ToArray());
        foreach (var value in input)
        {
            var line = lines.SingleOrDefault(x => x.SourcingLineId == value.SourcingLineId)
                ?? throw new InvalidOperationException("QuotationOfferLineChanged");
            if (line.SupplierBidId != bid.Id || line.TenantId != bid.TenantId || line.LegalEntityId != bid.LegalEntityId
                || line.Quantity != value.Quantity || line.UnitPrice != value.UnitPrice
                || line.TaxAmount != value.QuotedTaxAmount || line.DeliveryAmount != value.DeliveryAmount)
                throw new InvalidOperationException("QuotationOfferLineChanged");
            foreach (var amount in new[] { value.Quantity, value.UnitPrice, value.DiscountValue,
                value.QuotedTaxAmount, value.DeliveryAmount })
                if (!ExactDecimalJsonCodec.IsRepresentable(amount, 18, 4))
                    throw new InvalidOperationException("QuotationAmountPrecisionInvalid");
            Text(value.SupplierOfferedTaxCode, 80); Text(value.LeadTimeText, 200); Text(value.Notes, 4000);
        }
        foreach (var value in input)
        {
            var line = lines.Single(x => x.SourcingLineId == value.SourcingLineId);
            line.PrincipalCaptureVersion = 2; line.IsActive = true;
            line.DiscountKind = value.Discount.ToString(); line.DiscountValue = value.DiscountValue;
            line.TaxTreatment = value.TaxTreatment.ToString(); line.SupplierOfferedTaxCode = value.SupplierOfferedTaxCode;
            line.LeadTimeText = value.LeadTimeText; line.Notes = value.Notes;
        }
        bid.PrincipalCaptureVersion = 2; bid.SupplierQuoteReference = offer.SupplierQuoteReference;
        bid.QuoteDate = offer.QuoteDate; bid.OfferValidityKind = offer.Validity.ToString(); bid.ValidUntil = offer.ValidUntil;
        bid.SupplierOfferedPaymentCode = offer.PaymentTerms?.Code; bid.SupplierOfferedPaymentText = offer.PaymentTerms?.Text;
        bid.WarrantyText = offer.WarrantyText; bid.DeliveryTermsText = offer.DeliveryTermsText; bid.DeliveryDate = offer.DeliveryDate;
    }

    public static string Build(SupplierBid bid, IReadOnlyList<SupplierBidLine> lines,
        long resultVersion, IReadOnlyList<DocumentUseSelection> selections, IReadOnlyList<Pin> pins)
    {
        if (bid is null || bid.PrincipalCaptureVersion != 2 || resultVersion < 1 || bid.InvitationId is null
            || bid.EvidenceDocumentRefId is not null || lines is null || lines.Count is < 1 or > 200
            || lines.Any(x => x is null || x.PrincipalCaptureVersion != 2 || !x.IsActive)
            || pins is null || selections is null)
            throw new InvalidOperationException("QuotationV3Required");
        // Reuse exact scoped line/header/decimal/protocol shape from the accepted
        // V2 primitive, while explicitly adding every offered term for V3.
        _ = PrincipalManualQuotationSnapshot.Build(bid, lines);
        var offer = Offer(bid, selections);
        PrincipalQuotationCommercialTerms.Validate(offer);
        ValidatePins(bid, resultVersion, selections, pins);
        var commercial = lines.Select(Commercial).ToArray();
        var totals = PrincipalQuotationCommercialTerms.Calculate(commercial);
        var payload = new Snapshot(3, new(bid.Id, bid.TenantId, bid.LegalEntityId, bid.SourcingEventId,
            bid.SupplierId, bid.InvitationId.Value, bid.Status, bid.CurrencyCode, bid.SurrogateEnteredBy!,
            resultVersion, ProcurementEvidenceEncoding.DatabaseTimestamp(bid.ReceivedAt), bid.CorrectionReason,
            offer with { Documents = selections.OrderBy(x => x.DocumentId).ThenBy(x => x.VersionId).ToArray() }),
            lines.OrderBy(x => x.SourcingLineId).Select(x => new StoredLine(x.Id, x.SourcingLineId,
                Exact(x.Quantity), Exact(x.UnitPrice), x.DiscountKind!, Exact(x.DiscountValue), x.TaxTreatment!,
                Exact(x.TaxAmount), Exact(x.DeliveryAmount), x.SupplierOfferedTaxCode, x.LeadTimeText, x.Notes)).ToArray(),
            pins.OrderBy(x => x.DocumentId).ThenBy(x => x.VersionId).ToArray(), totals);
        var json = JsonSerializer.Serialize(payload, Json);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 8388608)
            throw new InvalidOperationException("QuotationSnapshotTooLarge");
        return json;
    }

    public static Restored Restore(string json)
    {
        var snapshot = JsonSerializer.Deserialize<Snapshot>(json, Json)
            ?? throw new InvalidOperationException("QuotationSnapshotUnavailable");
        if (snapshot.SchemaVersion != 3 || snapshot.Header is null || snapshot.Lines is null
            || snapshot.DocumentPins is null || snapshot.Header.Offer is null
            || snapshot.Lines.Any(x => x is null) || snapshot.DocumentPins.Any(x => x is null))
            throw new InvalidOperationException("QuotationSnapshotUnavailable");
        var header = snapshot.Header;
        var bid = new SupplierBid
        {
            Id = header.Id, TenantId = header.TenantId, LegalEntityId = header.LegalEntityId,
            SourcingEventId = header.SourcingEventId, SupplierId = header.SupplierId, InvitationId = header.InvitationId,
            Status = header.Status, CurrencyCode = header.CurrencyCode, SurrogateEnteredBy = header.CapturedBy,
            SourceChannel = "Manual", SubmissionVersion = 1, Version = header.BidVersion,
            ReceivedAt = header.ReceivedAt, CorrectionReason = header.CorrectionReason,
        };
        var lines = snapshot.Lines.Select(x => new SupplierBidLine
        {
            Id = x.Id, TenantId = bid.TenantId, LegalEntityId = bid.LegalEntityId, SupplierBidId = bid.Id,
            SourcingLineId = x.SourcingLineId, Quantity = Amount(x.Quantity), UnitPrice = Amount(x.UnitPrice),
            TaxAmount = Amount(x.QuotedTaxAmount), DeliveryAmount = Amount(x.DeliveryAmount),
        }).ToArray();
        var input = snapshot.Lines.Select(x => new LineInput(x.SourcingLineId, Amount(x.Quantity), Amount(x.UnitPrice),
            Code<QuotationDiscount>(x.DiscountKind), Amount(x.DiscountValue), Code<QuotationQuotedTax>(x.TaxTreatment),
            Amount(x.QuotedTaxAmount), Amount(x.DeliveryAmount), x.SupplierOfferedTaxCode, x.LeadTimeText, x.Notes)).ToArray();
        Apply(bid, lines, header.Offer, input);
        var expected = Build(bid, lines, header.BidVersion, header.Offer.Documents!, snapshot.DocumentPins);
        // TEXT evidence is the exact producer format: this also rejects duplicate,
        // missing, extra, reordered or inconsistently derived fields/totals.
        if (expected != json) throw new InvalidOperationException("QuotationSnapshotChanged");
        return new(bid, lines, header.Offer, snapshot.DocumentPins);
    }

    public static QuotationCommercialTerms Offer(SupplierBid bid, IReadOnlyList<DocumentUseSelection> documents) => new(
        bid.SupplierQuoteReference, bid.QuoteDate, Code<QuotationValidity>(bid.OfferValidityKind), bid.ValidUntil,
        bid.SupplierOfferedPaymentCode is null && bid.SupplierOfferedPaymentText is null ? null
            : new(bid.SupplierOfferedPaymentCode, bid.SupplierOfferedPaymentText),
        bid.WarrantyText, bid.DeliveryTermsText, bid.DeliveryDate, documents);
    public static QuotationCommercialLine Commercial(SupplierBidLine line) => new(line.SourcingLineId, line.Quantity,
        line.UnitPrice, Code<QuotationDiscount>(line.DiscountKind), line.DiscountValue,
        Code<QuotationQuotedTax>(line.TaxTreatment), line.TaxAmount, line.DeliveryAmount, line.SupplierOfferedTaxCode);
    public static QuotationCommercialLine Commercial(LineInput line) => new(line.SourcingLineId, line.Quantity,
        line.UnitPrice, line.Discount, line.DiscountValue, line.TaxTreatment, line.QuotedTaxAmount,
        line.DeliveryAmount, line.SupplierOfferedTaxCode);
    public static string PinDigest(IReadOnlyList<Pin> pins)
    {
        if (pins is null || pins.Any(x => x is null)) throw new InvalidOperationException("QuotationDocumentUseUnavailable");
        return ProcurementEvidenceEncoding.Digest(JsonSerializer.Serialize(pins.OrderBy(x => x.DocumentId).ThenBy(x => x.VersionId), Json));
    }
    public static Pin FromCore(DocumentUsePin pin) => new(pin.Id, pin.TenantId, pin.LegalEntityId, pin.Source,
        pin.OwnerModule, pin.OwnerType, pin.OwnerId, pin.DocumentId, pin.VersionId, pin.DocumentRevision,
        pin.VersionRevision, pin.Sha256);
    private static void ValidatePins(SupplierBid bid, long resultVersion, IReadOnlyList<DocumentUseSelection> selections, IReadOnlyList<Pin> pins)
    {
        if (pins.Count != selections.Count || pins.Count > 100 || pins.Any(x => x is null)
            || pins.Select(x => x.VersionId).Distinct().Count() != pins.Count)
            throw new InvalidOperationException("QuotationDocumentUseUnavailable");
        foreach (var pin in pins)
        {
            var selected = selections.SingleOrDefault(x => x.VersionId == pin.VersionId);
            if (selected is null || pin.Id == Guid.Empty || pin.TenantId != bid.TenantId
                || pin.LegalEntityId != bid.LegalEntityId || pin.Source is null || pin.Source.Module != "procurement"
                || pin.Source.RecordType != "supplier_quotation" || pin.Source.RecordId != bid.Id
                || pin.Source.Key == Guid.Empty || pin.Source.Revision < 1 || pin.Source.Revision > resultVersion || pin.Source.Kind != DocumentUseKind.Review
                || pin.OwnerModule != "procurement" || pin.OwnerType != "supplier_bid" || pin.OwnerId != bid.Id
                || selected.OwnerModule != pin.OwnerModule || selected.OwnerType != pin.OwnerType
                || selected.OwnerId != pin.OwnerId || selected.DocumentId != pin.DocumentId
                || selected.DocumentRevision != pin.DocumentRevision || selected.VersionRevision != pin.VersionRevision
                || selected.Sha256 != pin.Sha256)
                throw new InvalidOperationException("QuotationDocumentUseUnavailable");
        }
        if (pins.Select(x => x.Source).Distinct().Count() > 1)
            throw new InvalidOperationException("QuotationDocumentUseSetChanged");
    }
    private static T Code<T>(string? value) where T : struct, Enum =>
        Enum.TryParse<T>(value, false, out var parsed) && Enum.IsDefined(parsed) && parsed.ToString() == value
            ? parsed : throw new InvalidOperationException("QuotationOfferCodeInvalid");
    private static decimal Amount(string value)
    {
        var parsed = ExactDecimalJsonCodec.Parse(value, 18, 4, "QuotationAmount");
        return Exact(parsed) == value ? parsed : throw new InvalidOperationException("QuotationAmountNotCanonical");
    }
    private static string Exact(decimal value) => ProcurementEvidenceEncoding.Decimal(value);
    private static void Text(string? value, int max)
    {
        if (value is not null && (string.IsNullOrWhiteSpace(value) || value.Length > max))
            throw new InvalidOperationException("QuotationOfferTextInvalid");
    }
}
