using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalOrderCommercialSnapshotTests
{
    [Fact]
    public void Principal_snapshot_pins_exact_terms_and_never_uses_worker_identity()
    {
        var company=Guid.NewGuid();
        var order=new PurchaseOrder{TenantId="principal-order-test",LegalEntityId=company,
            PrincipalDraftSchemaVersion=2,OriginalDirectSourceSha256=new string('a',64),
            Number="PO-ONE",BuyerSubjectId=Guid.NewGuid().ToString("D"),SupplierId=Guid.NewGuid(),
            BranchRef=Guid.NewGuid(),CurrencyCode="ZMW",Status="Draft"};
        var supplier=new Supplier{TenantId=order.TenantId,LegalEntityId=company,Id=order.SupplierId,
            LegalName="Original name",DisplayName="Supplier",CountryCode="ZM"};
        var line=new PurchaseOrderLine{PurchaseOrderId=order.Id,LineNumber=1,
            Description="Laptop",UnitCode="EA",Quantity=2m,UnitPrice=5.1234m};
        var captured=PrincipalOrderCommercialSnapshot.Capture(order,[line],supplier,1);
        Assert.Contains("5.1234",captured);
        order.Status="Approved";
        Assert.Equal(captured,PrincipalOrderCommercialSnapshot.Capture(order,[line],supplier,1));
        Assert.True(PrincipalOrderCommercialSnapshot.RetainedMatches(order,[line],captured,1));
        line.UnitPrice=5.1235m;
        Assert.NotEqual(captured,PrincipalOrderCommercialSnapshot.Capture(order,[line],supplier,1));
        Assert.False(PrincipalOrderCommercialSnapshot.RetainedMatches(order,[line],captured,1));
        line.UnitPrice=5.1234m;
        supplier.LegalName="Renamed";
        Assert.NotEqual(captured,PrincipalOrderCommercialSnapshot.Capture(order,[line],supplier,1));
        Assert.True(PrincipalOrderCommercialSnapshot.RetainedMatches(order,[line],captured,1));
        Assert.Throws<InvalidOperationException>(()=>PrincipalOrderCommercialSnapshot.Capture(order,[line],supplier,2));
    }
}
