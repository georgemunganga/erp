using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqPublicationPreparationTests
{
    private static (RfqPrincipalDraft Draft, Supplier Supplier, DateTimeOffset Now) Fixture()
    {
        var now = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        var company = Guid.NewGuid();
        var principal = Guid.NewGuid().ToString("D");
        var pin = new RfqPrincipalDemandPin(Guid.NewGuid(), Guid.NewGuid(), 2, Guid.NewGuid(), 2,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), new string('b', 64),
            new string('c', 64), Guid.NewGuid(), new string('d', 64), Guid.NewGuid(), 1, "RFQ", 1,
            principal, principal, null, null, new string('e', 64), Guid.NewGuid(), Guid.NewGuid(),
            1.0001m, 1.0001m, 1.00020001m, "EA", "OFFICE", null);
        var draft = GovernedPrincipalRfqDraft.NewDraft(Guid.NewGuid(), "tenant", company, null, "ZMW",
            "Office equipment", principal, null, "Draft", [pin]);
        var supplier = new Supplier { TenantId = "tenant", LegalEntityId = company, Status = "Active",
            IsOrderable = true };
        return (draft, supplier, now);
    }

    [Fact]
    public void Shortlist_preserves_exact_eight_digit_source_and_never_authorizes_publication()
    {
        var (draft, supplier, now) = Fixture();
        var proposal = PrincipalRfqPublicationPreparation.Prepare(draft,
            new SourcingPublicationControls(1, 1, false, false, false), [supplier], now);
        Assert.Equal(1.00020001m, proposal.SelectedOriginalAmount);
        Assert.Equal(draft.OriginalFingerprint, proposal.DraftFingerprint);
        Assert.Equal(64, proposal.SnapshotSha256.Length);
        Assert.False(proposal.CanPublish);
        Assert.Contains("IndependentCoreReviewRequired", proposal.OutstandingControls);
    }

    [Fact]
    public void Supplier_status_scope_expiry_and_configured_counts_fail_closed()
    {
        var (draft, supplier, now) = Fixture();
        var controls = new SourcingPublicationControls(2, 2, false, false, false);
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqPublicationPreparation.Prepare(draft, controls, [supplier], now));
        controls = controls with { MinimumQuoteCount = 1, MinimumInvitedSuppliers = 1 };
        supplier.Status = "Suspended";
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqPublicationPreparation.Prepare(draft, controls, [supplier], now));
        supplier.Status = "Active";
        supplier.EligibleUntil = now.AddSeconds(-1);
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqPublicationPreparation.Prepare(draft, controls, [supplier], now));
        supplier.EligibleUntil = null;
        supplier.LegalEntityId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqPublicationPreparation.Prepare(draft, controls, [supplier], now));
    }

    [Fact]
    public void Manual_quote_remains_draft_and_rejects_foreign_line_or_excess_quantity()
    {
        var (draft, supplier, _) = Fixture();
        var actor = Guid.NewGuid().ToString("D");
        var sourcing = new SourcingEvent { Id = draft.Id, TenantId = draft.TenantId,
            LegalEntityId = draft.CompanyId, ExecutionVersion = 2, Status = "Open", CurrencyCode = "ZMW" };
        var invitation = new SourcingInvitation { TenantId = draft.TenantId, LegalEntityId = draft.CompanyId,
            SourcingEventId = draft.Id, SupplierId = supplier.Id, Status = "ManualContactRecorded" };
        var bid = new SupplierBid { TenantId = draft.TenantId, LegalEntityId = draft.CompanyId,
            SourcingEventId = draft.Id, SupplierId = supplier.Id, InvitationId = invitation.Id,
            Status = "Draft", SourceChannel = "Manual", CurrencyCode = "ZMW", SurrogateEnteredBy = actor };
        var source = new SourcingLine { Id = draft.Lines[0].EventLineId, TenantId = draft.TenantId,
            LegalEntityId = draft.CompanyId, SourcingEventId = draft.Id, Quantity = 1.0001m, Status = "Active" };
        var quote = new SupplierBidLine { TenantId = draft.TenantId, LegalEntityId = draft.CompanyId,
            SupplierBidId = bid.Id, SourcingLineId = source.Id, Quantity = 1.0001m,
            UnitPrice = 1.0001m, TaxAmount = 0m, DeliveryAmount = 0m };
        PrincipalManualQuotationDraft.Validate(sourcing, invitation, bid, [source], [quote], actor);
        quote.Quantity = 1.0002m;
        Assert.Throws<InvalidOperationException>(() => PrincipalManualQuotationDraft.Validate(sourcing, invitation, bid, [source], [quote], actor));
        quote.Quantity = 1.0001m;
        quote.SourcingLineId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => PrincipalManualQuotationDraft.Validate(sourcing, invitation, bid, [source], [quote], actor));
        quote.SourcingLineId = source.Id;
        bid.SubmittedAt = DateTimeOffset.UtcNow;
        Assert.Throws<InvalidOperationException>(() => PrincipalManualQuotationDraft.Validate(sourcing, invitation, bid, [source], [quote], actor));
    }
}
