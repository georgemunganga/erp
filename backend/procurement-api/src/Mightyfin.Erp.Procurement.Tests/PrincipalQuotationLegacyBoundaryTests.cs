using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class PrincipalManualQuotationEvidenceTests
{
    [Theory]
    [InlineData("header", "SupplierQuoteReference")]
    [InlineData("header", "QuoteDate")]
    [InlineData("header", "OfferValidityKind")]
    [InlineData("header", "ValidUntil")]
    [InlineData("header", "SupplierOfferedPaymentCode")]
    [InlineData("header", "SupplierOfferedPaymentText")]
    [InlineData("header", "WarrantyText")]
    [InlineData("header", "DeliveryTermsText")]
    [InlineData("header", "DeliveryDate")]
    [InlineData("line", "DiscountKind")]
    [InlineData("line", "DiscountValue")]
    [InlineData("line", "TaxTreatment")]
    [InlineData("line", "SupplierOfferedTaxCode")]
    [InlineData("line", "IsActive")]
    public void Legacy_added_or_modified_record_cannot_overpost_any_new_offer_field(string kind, string field)
    {
        foreach (var existing in new[] { false, true })
        {
            var f = new Fixture(); var draft = f.Draft(f.Contact()); using var db = f.Context();
            ProcurementRecord target = kind == "header" ? draft.Bid : draft.Lines[0];
            if (existing) db.Attach(target);
            else if (target is SupplierBid header) db.AddScoped(header);
            else db.AddScoped((SupplierBidLine)target);
            var property = target.GetType().GetProperty(field)!;
            object value = property.PropertyType == typeof(DateOnly?) ? new DateOnly(2026, 11, 1)
                : property.PropertyType == typeof(decimal) ? 1m
                : property.PropertyType == typeof(bool) ? false : "Offered outside owned workflow";
            property.SetValue(target, value);
            Assert.Equal("ManualQuotationOwnedCommandRequired", Assert.Throws<InvalidOperationException>(() => Prepare(db)).Message);
            Assert.Equal(0, kind == "header" ? draft.Bid.PrincipalCaptureVersion : draft.Lines[0].PrincipalCaptureVersion);
        }
    }

    [Theory]
    [InlineData("header")]
    [InlineData("line")]
    public void Unchanged_legacy_offer_values_do_not_block_an_unrelated_old_field_edit(string kind)
    {
        var f = new Fixture(); var draft = f.Draft(f.Contact()); using var db = f.Context();
        if (kind == "header")
        {
            draft.Bid.SupplierQuoteReference = "Retained nondefault legacy field";
            db.Attach(draft.Bid); draft.Bid.CorrectionReason = "Unrelated existing field";
        }
        else
        {
            draft.Lines[0].DiscountKind = "Historic supplied value";
            db.Attach(draft.Lines[0]); draft.Lines[0].Notes = "Unrelated existing field";
        }
        Prepare(db);
        Assert.NotEmpty(db.ChangeTracker.Entries<AuditEvent>());
    }
}
