using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class PrincipalControlledReceivingTests
{
    [Fact]
    public void Only_positive_service_fact_projects_with_its_exact_eight_place_amount()
    {
        var source = Source(ReceivingKind.Service, 0.0001m, 0.0001m);
        var policy = Policy(source);
        var empty = Empty(source, policy);
        var fact = PrincipalControlledReceiving.CertifyService(empty, policy, Actor(Inspector),
            Guid.NewGuid(), Guid.NewGuid(), 0, 0.0001m, "Original service unit",
            new DateOnly(2026, 10, 22), new DateOnly(2026, 10, 22), "REC:unit", At);
        var ledger = Add(empty, fact);
        var state = State(source, 1, 0.0001m, 0.00000001m);
        var line = new PurchaseOrderLine { Id = source.Commercial.PurchaseOrderLineId,
            TenantId = source.Commercial.TenantId, LegalEntityId = source.Commercial.LegalEntityId,
            PurchaseOrderId = source.Commercial.PurchaseOrderId, UnitCode = source.Commercial.UnitCode,
            AcceptedQuantity = 0.0001m };

        var accepted = Assert.Single(PrincipalInvoiceSourceProjection.Accepted(state, ledger, line));
        Assert.Equal(fact.Id, accepted.Id);
        Assert.Equal(fact.Sequence, accepted.Version);
        Assert.Equal(InvoiceAcceptanceKind.Service, accepted.Kind);
        Assert.Equal(0.0001m, accepted.AcceptedQuantity);
        Assert.Equal(0.00000001m, accepted.AcceptedNetAmount);
        Assert.False(accepted.Reversed);
        Assert.Equal(0m, accepted.ReturnedQuantity);
        Assert.NotEqual(source.SupplierAcknowledgmentId, accepted.Id);
    }

    [Fact]
    public void Physical_observation_without_acceptance_has_no_invoice_eligible_fact()
    {
        var source = Source(); var policy = Policy(source);
        var ledger = Recorded(source, policy);
        var state = State(source, ledger.Sequence);
        var line = new PurchaseOrderLine { Id = source.Commercial.PurchaseOrderLineId,
            TenantId = source.Commercial.TenantId, LegalEntityId = source.Commercial.LegalEntityId,
            PurchaseOrderId = source.Commercial.PurchaseOrderId, UnitCode = source.Commercial.UnitCode };
        Assert.Empty(PrincipalInvoiceSourceProjection.Accepted(state, ledger, line));
    }

    [Fact]
    public void Diverged_order_counter_or_mixed_line_identity_denies_projection()
    {
        var source = Source(ReceivingKind.Service, 1, 100); var policy = Policy(source);
        var fact = PrincipalControlledReceiving.CertifyService(Empty(source, policy), policy,
            Actor(Inspector), Guid.NewGuid(), Guid.NewGuid(), 0, 0.5m, "Half milestone",
            new DateOnly(2026, 10, 22), new DateOnly(2026, 10, 22), "REC:half", At);
        var ledger = Add(Empty(source, policy), fact);
        var state = State(source, 1, 0.5m, 50m);
        var line = new PurchaseOrderLine { Id = source.Commercial.PurchaseOrderLineId,
            TenantId = source.Commercial.TenantId, LegalEntityId = source.Commercial.LegalEntityId,
            PurchaseOrderId = source.Commercial.PurchaseOrderId, UnitCode = source.Commercial.UnitCode,
            AcceptedQuantity = 0.25m };
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceSourceProjection.Accepted(state, ledger, line));
        line.AcceptedQuantity = 0.5m;
        line.PurchaseOrderId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceSourceProjection.Accepted(state, ledger, line));
    }
}
