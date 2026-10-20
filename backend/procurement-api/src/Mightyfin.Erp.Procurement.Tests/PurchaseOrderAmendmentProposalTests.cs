using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PurchaseOrderAmendmentProposalTests
{
    [Fact]
    public void Proposal_retains_current_issued_terms_and_only_describes_positive_quantity_holds()
    {
        var f = new Fixture(); f.ProposedLine.Quantity = 2.3456m;
        var originalJson = f.Baseline.CommercialSnapshotJson;
        var plan = f.Propose(); var delta = Assert.Single(plan.SourceDeltas);
        Assert.Equal(1.1111m, delta.QuantityHoldRequired); Assert.Equal(0, delta.QuantityReleaseOnApply);
        Assert.Equal(1.37165295m, delta.LineBaseDelta);
        Assert.Equal(1.2345m, f.CurrentLine.Quantity); Assert.Equal("Issued", f.Current.Status);
        Assert.Equal(1, f.Current.CurrentRevision); Assert.Equal(originalJson, f.Baseline.CommercialSnapshotJson);
        Assert.Equal("Draft", plan.Revision.Status); Assert.Null(plan.Revision.ApprovalInstanceRef);
        Assert.Equal(2, plan.Revision.RevisionNumber); Assert.Equal(f.Baseline.Id, plan.Revision.BaseIssuedVersionId);
        Assert.Equal(originalJson, plan.Revision.BeforeSnapshotJson);
    }

    [Fact]
    public void Decrease_is_a_future_apply_delta_and_does_not_reduce_existing_ordered_terms()
    {
        var f = new Fixture(); f.CurrentLine.AcceptedQuantity = 0.5m; f.ProposedLine.Quantity = 0.6m;
        var delta = Assert.Single(f.Propose().SourceDeltas);
        Assert.Equal(0, delta.QuantityHoldRequired); Assert.Equal(0.6345m, delta.QuantityReleaseOnApply);
        Assert.Equal(1.2345m, f.CurrentLine.Quantity); Assert.Equal(0.5m, f.CurrentLine.AcceptedQuantity);
    }

    [Theory]
    [InlineData("supplier")]
    [InlineData("currency")]
    [InlineData("unit")]
    [InlineData("line-id")]
    [InlineData("source-line")]
    [InlineData("tenant")]
    [InlineData("below-accepted")]
    [InlineData("invoiced-repricing")]
    [InlineData("stale-version")]
    [InlineData("changed-baseline-hash")]
    public void Guard_rejects_changes_that_need_another_controlled_business_route(string mutation)
    {
        var f = new Fixture(); f.ProposedLine.Description = "Authorized proposed wording";
        switch (mutation)
        {
            case "supplier": f.Proposed.SupplierId = Guid.NewGuid(); break;
            case "currency": f.Proposed.CurrencyCode = "USD"; break;
            case "unit": f.ProposedLine.UnitCode = "BOX"; break;
            case "line-id": f.ProposedLine.Id = Guid.NewGuid(); break;
            case "source-line": f.ProposedLine.PurchaseRequestLineId = Guid.NewGuid(); break;
            case "tenant": f.Proposed.TenantId = "other-tenant"; break;
            case "below-accepted": f.CurrentLine.AcceptedQuantity = 1; f.ProposedLine.Quantity = 0.5m; break;
            case "invoiced-repricing": f.CurrentLine.InvoicedQuantity = 0.5m; f.ProposedLine.UnitPrice++; break;
            case "stale-version": f.Current.CurrentRevision++; break;
            case "changed-baseline-hash": f.Baseline = f.BaselineCopy(new string('a', 64)); break;
        }
        Assert.Throws<InvalidOperationException>(() => f.Propose());
        Assert.Equal("Issued", f.Current.Status); Assert.Equal(1.2345m, f.CurrentLine.Quantity);
    }

    [Fact]
    public void Operational_row_revision_change_is_not_a_new_commercial_baseline()
    {
        var f = new Fixture(); f.Current.Version++; f.CurrentLine.AcceptedQuantity = 0.5m;
        f.Proposed.ExpectedDeliveryOn = new DateOnly(2026, 11, 20);
        Assert.Equal(2, f.Propose().Revision.RevisionNumber);
    }

    [Fact]
    public void Noop_and_duplicate_or_missing_lines_do_not_create_revision_numbers()
    {
        var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() => f.Propose());
        f.ProposedLine.Description = "Changed description";
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderAmendmentProposal.Create(f.Current,
            [f.CurrentLine], f.Supplier, f.Baseline, f.Proposed, [], "Reason"));
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderAmendmentProposal.Create(f.Current,
            [f.CurrentLine], f.Supplier, f.Baseline, f.Proposed, [f.ProposedLine, f.ProposedLine], "Reason"));
        Assert.Equal(1, f.Current.CurrentRevision);
    }

    private sealed class Fixture
    {
        public PurchaseOrder Current { get; }
        public PurchaseOrder Proposed { get; }
        public PurchaseOrderLine CurrentLine { get; }
        public PurchaseOrderLine ProposedLine { get; }
        public Supplier Supplier { get; }
        public PurchaseOrderIssuedVersion Baseline { get; set; }
        public Fixture()
        {
            var company = Guid.NewGuid(); var supplier = Guid.NewGuid();
            Current = new() { TenantId = "tenant", LegalEntityId = company, SupplierId = supplier,
                Number = "PO-TEST", BuyerSubjectId = "buyer", CurrencyCode = "ZMW", CurrentRevision = 1,
                Status = "Issued", IssuedAt = DateTimeOffset.UtcNow, Version = 17 };
            Proposed = new() { Id = Current.Id, TenantId = "tenant", LegalEntityId = company, SupplierId = supplier,
                Number = Current.Number, BuyerSubjectId = "buyer", CurrencyCode = "ZMW", CurrentRevision = 1 };
            CurrentLine = new() { TenantId = "tenant", LegalEntityId = company, PurchaseOrderId = Current.Id,
                PurchaseRequestLineId = Guid.NewGuid(), LineNumber = 1, Description = "Synthetic item", UnitCode = "EA",
                Quantity = 1.2345m, UnitPrice = 1.2345m, TaxAmount = 0 };
            ProposedLine = new() { Id = CurrentLine.Id, TenantId = "tenant", LegalEntityId = company,
                PurchaseOrderId = Current.Id, PurchaseRequestLineId = CurrentLine.PurchaseRequestLineId, LineNumber = 1,
                Description = CurrentLine.Description, UnitCode = "EA", Quantity = 1.2345m, UnitPrice = 1.2345m };
            Supplier = new() { Id = supplier, TenantId = "tenant", LegalEntityId = company,
                LegalName = "Synthetic Supplier", DisplayName = "Supplier", CountryCode = "ZM" };
            var json = PurchaseOrderCommercialWorkflow.Snapshot(Current, [CurrentLine], Supplier, 1);
            Baseline = new() { TenantId = "tenant", LegalEntityId = company, PurchaseOrderId = Current.Id,
                RevisionNumber = 1, CommercialSnapshotJson = json, CommercialSnapshotSha256 = ProcurementEvidenceEncoding.Digest(json) };
        }
        public PurchaseOrderIssuedVersion BaselineCopy(string hash) => new() { Id = Baseline.Id,
            TenantId = Baseline.TenantId, LegalEntityId = Baseline.LegalEntityId, PurchaseOrderId = Baseline.PurchaseOrderId,
            RevisionNumber = Baseline.RevisionNumber, CommercialSnapshotJson = Baseline.CommercialSnapshotJson,
            CommercialSnapshotSha256 = hash };
        public PurchaseOrderAmendmentDraft Propose() => PurchaseOrderAmendmentProposal.Create(Current,
            [CurrentLine], Supplier, Baseline, Proposed, [ProposedLine], "Delivery/quantity change requested");
    }
}
