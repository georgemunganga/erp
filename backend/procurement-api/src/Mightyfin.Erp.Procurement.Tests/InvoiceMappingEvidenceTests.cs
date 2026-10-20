using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceMappingEvidenceTests
{
    [Fact]
    public void Operational_version_does_not_change_original_digest_but_original_amount_does()
    {
        var (invoice, line) = Records(); var original = InvoiceMappingEvidence.OriginalDigest(invoice, [line]);
        invoice.Version++; invoice.MappingRevision++; invoice.MatchingState = "ComparisonBlocked";
        Assert.Equal(original, InvoiceMappingEvidence.OriginalDigest(invoice, [line]));
        line.UnitPrice += .0001m;
        Assert.NotEqual(original, InvoiceMappingEvidence.OriginalDigest(invoice, [line]));
    }

    [Fact]
    public void Mapping_proposal_never_infers_verified_invoice_unit_from_declared_or_PO_unit()
    {
        var (invoice, line) = Records(); var mapping = Mapping(invoice, line);
        var projected = InvoiceMappingEvidence.Project(mapping, invoice, [line], InvoiceMappingReview.Unavailable("NoReview"));
        Assert.Empty(Assert.Single(projected.Lines).UnitCode);
        Assert.Null(line.PurchaseOrderLineId); Assert.Null(invoice.PurchaseOrderId);
        Assert.Equal(mapping.PurchaseOrderId, projected.PurchaseOrderId);
    }

    [Fact]
    public void Mapping_snapshot_requires_complete_unique_original_line_identity_and_scope()
    {
        var (invoice, line) = Records(); var link = Link(line);
        Assert.Throws<InvalidOperationException>(() => InvoiceMappingEvidence.Create(invoice, [line], Guid.NewGuid(), Guid.NewGuid(),
            [link, link], "mapper", Guid.NewGuid(), DateTimeOffset.UtcNow));
        line.LegalEntityId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => Mapping(invoice, line));
    }

    [Fact]
    public void Material_intake_change_invalidates_retained_mapping_without_rewriting_original()
    {
        var (invoice, line) = Records(); var mapping = Mapping(invoice, line);
        Assert.Equal(1, InvoiceMappingEvidence.Verify(mapping, invoice, [line]).Revision);
        invoice.InvoiceNumber = "CORRECTED";
        Assert.Throws<InvalidOperationException>(() => InvoiceMappingEvidence.Verify(mapping, invoice, [line]));
    }

    [Fact]
    public async Task Missing_owner_implementations_are_explicitly_unavailable_not_complete_zero()
    {
        var context = new InvoiceExposureReadContext("t", Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ZMW", new string('a',64), DateTimeOffset.UtcNow);
        var owner = await new UnavailableInvoiceHistoricalExposureOwner().ReadAsync(context, CancellationToken.None);
        Assert.False(owner.Complete); Assert.Empty(owner.Allocations); Assert.Null(owner.Watermark);
        Assert.False(InvoiceMatchExecutionEvidence.OwnerCovers(owner, context));
        var (invoice, line) = Records(); var mapping = Mapping(invoice, line);
        var review = await new UnavailableInvoiceMappingEvidenceReviewer().ReviewAsync(mapping,
            InvoiceMatchDocument.From(invoice, [line]), new(invoice.TenantId, invoice.LegalEntityId, invoice.BranchRef, "mapper", Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), DateTimeOffset.UtcNow), CancellationToken.None);
        Assert.False(review.Verified); Assert.Empty(review.VerifiedUnits);
    }

    private static InvoiceMatchMappingVersion Mapping(SupplierInvoice invoice, SupplierInvoiceLine line)
        => InvoiceMappingEvidence.Create(invoice, [line], Guid.NewGuid(), Guid.NewGuid(), [Link(line)], "mapper", Guid.NewGuid(), DateTimeOffset.UtcNow);
    private static InvoiceMappingLine Link(SupplierInvoiceLine line)
        => new(line.Id, Guid.NewGuid(), null, null, "EACH", Guid.Empty, "");
    private static (SupplierInvoice, SupplierInvoiceLine) Records()
    {
        var invoice = new SupplierInvoice { TenantId = "mapping", LegalEntityId = Guid.NewGuid(), SupplierId = Guid.NewGuid(),
            InvoiceNumber = "INV-1", NormalizedInvoiceNumber = "INV-1", CurrencyCode = "ZMW", IntakeChannel = "Manual",
            InvoiceDate = new(2026,10,20), NetAmount = 1, GrossAmount = 1 };
        var line = new SupplierInvoiceLine { TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, Description = "Original invoice", Quantity = 1, UnitPrice = 1, LineNumber = 1 };
        return (invoice, line);
    }
}
