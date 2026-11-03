using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class PrincipalManualQuotationEvidenceTests
{
    [Fact]
    public void Actual_model_has_scoped_protocol_contact_and_same_bid_predecessor_foreign_keys()
    {
        var f = new Fixture(); using var db = f.Context();
        var version = db.Model.FindEntityType(typeof(SourcingManualQuoteVersion))!;
        var contact = db.Model.FindEntityType(typeof(SourcingManualContact))!;
        var line = db.Model.FindEntityType(typeof(SupplierBidLine))!;
        Assert.Equal("sourcing_manual_quote_versions", version.GetTableName());
        Assert.Equal("sourcing_manual_contacts", contact.GetTableName());
        var predecessor = Assert.Single(version.GetForeignKeys(), x =>
            x.PrincipalEntityType.ClrType == typeof(SourcingManualQuoteVersion));
        Assert.Equal(new[] { "TenantId", "LegalEntityId", "SupplierBidId", "PreviousVersionId" },
            predecessor.Properties.Select(x => x.Name));
        Assert.Equal(DeleteBehavior.Restrict, predecessor.DeleteBehavior);
        Assert.DoesNotContain(version.GetForeignKeys().SelectMany(x => x.Properties), x => x.Name == "BidVersion");
        var source = Assert.Single(version.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(SourcingManualContact));
        Assert.Equal(new[] { "TenantId", "LegalEntityId", "SourcingEventId", "InvitationId", "SupplierId", "BranchId", "ManualContactId" },
            source.Properties.Select(x => x.Name));
        Assert.Contains(line.GetForeignKeys(), x => x.Properties.Any(p => p.Name == "PrincipalCaptureVersion")
            && x.PrincipalEntityType.ClrType == typeof(SupplierBid) && x.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(contact.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(SourcingPublicationDecision)
            && x.Properties.Any(p => p.Name == "PublicationOpeningId"));
    }

    [Fact]
    public void Actual_model_preserves_legacy_null_offer_and_raw_precision_without_fake_derived_amount_columns()
    {
        var f = new Fixture(); using var db = f.Context();
        var header = db.Model.FindEntityType(typeof(SupplierBid))!;
        var line = db.Model.FindEntityType(typeof(SupplierBidLine))!;
        Assert.True(header.FindProperty(nameof(SupplierBid.SupplierQuoteReference))!.IsNullable);
        Assert.True(header.FindProperty(nameof(SupplierBid.OfferValidityKind))!.IsNullable);
        Assert.Equal(0, header.FindProperty(nameof(SupplierBid.PrincipalCaptureVersion))!.GetDefaultValue());
        Assert.Equal(18, line.FindProperty(nameof(SupplierBidLine.DiscountValue))!.GetPrecision());
        Assert.Equal(4, line.FindProperty(nameof(SupplierBidLine.DiscountValue))!.GetScale());
        Assert.Equal(true, line.FindProperty(nameof(SupplierBidLine.IsActive))!.GetDefaultValue());
        Assert.DoesNotContain(header.GetProperties(), x => x.Name is "DraftRevision" or "CommercialTotalsJson");
        Assert.DoesNotContain(line.GetProperties(), x => x.Name is "DiscountedTotal" or "PayableTotal");
    }
}
