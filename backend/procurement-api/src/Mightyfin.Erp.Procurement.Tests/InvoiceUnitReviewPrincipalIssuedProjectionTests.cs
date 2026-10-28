using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceUnitReviewPrincipalIssuedProjectionTests
{
    [Fact]
    public void Principal_projection_uses_real_principal_issued_id_and_authorization_time_without_legacy_or_receiving_rows()
    {
        var f = Fixture();
        InvoiceUnitReviewPrincipalIssuedProjection.Verify(f.Projected, f.Order, f.Issued, f.Lines, f.Categories);
        Assert.Equal(f.Issued.Id, f.Projected.RevisionId);
        Assert.Equal(f.Issued.AuthorizedAt, f.Projected.IssuedAt);
        Assert.Equal(2, f.Projected.Lines.Count);
    }

    [Theory]
    [InlineData("issued-parent")][InlineData("supplier")][InlineData("branch")][InlineData("tenant")]
    [InlineData("currency")][InlineData("authorization-time")][InlineData("snapshot")][InlineData("row-version")]
    [InlineData("missing-line")][InlineData("duplicate-line")][InlineData("unit")][InlineData("category")]
    [InlineData("quantity")][InlineData("price")][InlineData("tax")]
    public void Changed_principal_or_commercial_projection_cannot_be_saved(string fault)
    {
        var f = Fixture(); var p = f.Projected;
        p = fault switch
        {
            "issued-parent" => p with { RevisionId = Guid.NewGuid() },
            "supplier" => p with { SupplierId = Guid.NewGuid() },
            "branch" => p with { BranchRef = Guid.NewGuid() },
            "tenant" => p with { TenantId = "other" },
            "currency" => p with { CurrencyCode = "USD" },
            "authorization-time" => p with { IssuedAt = p.IssuedAt.AddHours(1) },
            "snapshot" => p with { IssuedReference = new string('b', 64) },
            "row-version" => p with { Version = p.Version + 1 },
            "missing-line" => p with { Lines = [p.Lines[0]] },
            "duplicate-line" => p with { Lines = [p.Lines[0], p.Lines[0]] },
            "unit" => p with { Lines = [p.Lines[0] with { UnitCode = "BOX" }, p.Lines[1]] },
            "category" => p with { Lines = [p.Lines[0] with { CategoryCode = "other" }, p.Lines[1]] },
            "quantity" => p with { Lines = [p.Lines[0] with { Quantity = 100 }, p.Lines[1]] },
            "price" => p with { Lines = [p.Lines[0] with { UnitPrice = 0 }, p.Lines[1]] },
            "tax" => p with { Lines = [p.Lines[0] with { TaxAmount = 100 }, p.Lines[1]] },
            _ => throw new InvalidOperationException()
        };
        Assert.Throws<InvalidOperationException>(() => InvoiceUnitReviewPrincipalIssuedProjection.Verify(
            p, f.Order, f.Issued, f.Lines, f.Categories));
    }

    [Fact]
    public void Retained_snapshot_category_survives_a_later_live_PR_category_edit()
    {
        var f = DemandFixture();
        var live = new PurchaseRequestLine { Id = f.Binding.PurchaseRequestLineId, CategoryCode = "LATER_EDIT" };
        var category = InvoiceUnitReviewPrincipalIssuedProjection.RetainedCategory(
            f.Order, f.Line, f.Binding, [f.Allocation], f.Snapshot);
        Assert.Equal("ORIGINAL_APPROVED", category);
        Assert.NotEqual(live.CategoryCode, category);
        foreach (var malformed in new[] { "{", "{}" })
            Assert.Throws<InvalidOperationException>(() => InvoiceUnitReviewPrincipalIssuedProjection.RetainedCategory(
                f.Order, f.Line, f.Binding, [f.Allocation], malformed));
    }

    [Theory]
    [InlineData("foreign-target")][InlineData("not-ordered")][InlineData("wrong-demand")]
    [InlineData("wrong-line")][InlineData("wrong-scope")][InlineData("quantity")]
    [InlineData("currency")][InlineData("missing-snapshot-line")]
    public void Another_allocation_or_source_snapshot_cannot_supply_the_retained_category(string fault)
    {
        var f = DemandFixture();
        switch (fault)
        {
            case "foreign-target": f.Allocation.TargetId = Guid.NewGuid(); break;
            case "not-ordered": f.Allocation.Status = "Reserved"; break;
            case "wrong-demand": f.Allocation.PurchaseRequestId = Guid.NewGuid(); break;
            case "wrong-line": f.Allocation.PurchaseRequestLineId = Guid.NewGuid(); break;
            case "wrong-scope": f.Allocation.LegalEntityId = Guid.NewGuid(); break;
            case "quantity": f.Allocation.Quantity++; break;
            case "currency": f.Allocation.CurrencyCode = "USD"; break;
            case "missing-snapshot-line": f = f with { Snapshot = "{\"legacyBusiness\":{\"lines\":[]}}" }; break;
        }
        Assert.Throws<InvalidOperationException>(() => InvoiceUnitReviewPrincipalIssuedProjection.RetainedCategory(
            f.Order, f.Line, f.Binding, [f.Allocation], f.Snapshot));
    }

    private sealed record Demand(PurchaseOrder Order, PurchaseOrderLine Line, PrincipalOrderSourceBinding Binding,
        PurchaseRequestAllocation Allocation, string Snapshot);
    private static Demand DemandFixture()
    {
        var f = Fixture(); var requestId = Guid.NewGuid(); var requestLineId = Guid.NewGuid();
        f.Lines[0].PurchaseRequestLineId = requestLineId;
        var allocation = new PurchaseRequestAllocation { TenantId = f.Order.TenantId,
            LegalEntityId = f.Order.LegalEntityId, PurchaseRequestId = requestId, PurchaseRequestLineId = requestLineId,
            Status = "Ordered", TargetType = "PurchaseOrder", TargetId = f.Order.Id,
            Quantity = 1, EstimatedValue = 5.1234m, CurrencyCode = "ZMW" };
        var binding = new PrincipalOrderSourceBinding { TenantId = f.Order.TenantId, LegalEntityId = f.Order.LegalEntityId,
            PurchaseOrderId = f.Order.Id, PurchaseOrderLineId = f.Lines[0].Id, PurchaseRequestId = requestId,
            PurchaseRequestLineId = requestLineId, AllocationId = allocation.Id, BranchId = f.Order.BranchRef!.Value,
            Quantity = allocation.Quantity, EstimatedValue = allocation.EstimatedValue, CurrencyCode = "ZMW" };
        var snapshot = System.Text.Json.JsonSerializer.Serialize(new { legacyBusiness = new { lines = new[] {
            new { Id = requestLineId, CategoryCode = "ORIGINAL_APPROVED" } } } });
        return new(f.Order, f.Lines[0], binding, allocation, snapshot);
    }

    private static (PurchaseOrder Order, PrincipalOrderIssuedVersion Issued, PurchaseOrderLine[] Lines,
        Dictionary<Guid, string> Categories, InvoiceIssuedOrder Projected) Fixture()
    {
        var order = new PurchaseOrder { TenantId = "principal-unit-review", LegalEntityId = Guid.NewGuid(),
            BranchRef = Guid.NewGuid(), SupplierId = Guid.NewGuid(), CurrencyCode = "ZMW",
            PrincipalDraftSchemaVersion = 2, Status = "Issued", Version = 4 };
        var at = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        var issued = new PrincipalOrderIssuedVersion { TenantId = order.TenantId, LegalEntityId = order.LegalEntityId,
            PurchaseOrderId = order.Id, CommercialRevision = 1, AuthorizedAt = at, CommercialSnapshotSha256 = new string('a', 64) };
        var lines = Enumerable.Range(1, 2).Select(n => new PurchaseOrderLine { TenantId = order.TenantId,
            LegalEntityId = order.LegalEntityId, PurchaseOrderId = order.Id, LineNumber = n,
            UnitCode = "EACH", Quantity = n, UnitPrice = 5.1234m, TaxAmount = 0.5m, Version = n }).ToArray();
        var categories = lines.ToDictionary(x => x.Id, x => "ORIGINAL_" + x.LineNumber);
        var projected = new InvoiceIssuedOrder(order.Id, issued.Id, 1, order.Version, order.TenantId,
            order.LegalEntityId, order.BranchRef, order.SupplierId, order.CurrencyCode, "Issued",
            issued.CommercialSnapshotSha256, at, lines.Select(x => new InvoiceIssuedOrderLine(
                x.Id, x.Version, categories[x.Id], x.UnitCode, x.Quantity, x.UnitPrice, x.TaxAmount)).ToArray());
        return (order, issued, lines, categories, projected);
    }
}
