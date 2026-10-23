using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Separate schema-2 PO commercial encoder. The retained worker1 encoder
/// remains untouched; a source PR approval never substitutes for this PO snapshot.</summary>
public static class PrincipalOrderCommercialSnapshot
{
    public static string Capture(PurchaseOrder order,IReadOnlyCollection<PurchaseOrderLine> lines,
        Supplier supplier,int revision)
    {
        if(order.PrincipalDraftSchemaVersion!=2 || revision!=1 || order.OriginalDirectSourceSha256 is not {Length:64}
            || supplier.Id!=order.SupplierId || lines.Count is <1 or >100
            || lines.Select(x=>x.Id).Distinct().Count()!=lines.Count)
            throw new InvalidOperationException("PrincipalOrderCommercialSourceInvalid");
        return JsonSerializer.Serialize(new{schemaVersion=2,order.TenantId,order.LegalEntityId,
            order.Id,order.Number,revision,order.OriginalDirectSourceSha256,
            order.BuyerSubjectId,order.BranchRef,order.CurrencyCode,order.SupplierId,
            supplier.LegalName,supplier.DisplayName,supplier.CountryCode,
            order.ShipToRef,order.BillToRef,order.PaymentTermRef,order.ExpectedDeliveryOn,
            lines=lines.OrderBy(x=>x.LineNumber).Select(x=>new{x.Id,x.LineNumber,
                x.PurchaseRequestLineId,x.CatalogItemId,x.Description,x.UnitCode,
                quantity=ProcurementEvidenceEncoding.Decimal(x.Quantity),
                unitPrice=ProcurementEvidenceEncoding.Decimal(x.UnitPrice),
                tax=ProcurementEvidenceEncoding.Decimal(x.TaxAmount),
                x.CostCenterRef,x.ProjectRef,x.ExpectedDeliveryOn}).ToArray()});
    }
    public static string Digest(PurchaseOrder order,IReadOnlyCollection<PurchaseOrderLine> lines,
        Supplier supplier,int revision)=>ProcurementEvidenceEncoding.Digest(Capture(order,lines,supplier,revision));

    /// <summary>Verify retained commercial facts after a supplier changes its
    /// display name. The old supplier names are evidence, not current eligibility.</summary>
    public static bool RetainedMatches(PurchaseOrder order,IReadOnlyCollection<PurchaseOrderLine> lines,
        string recorded,int revision)
    {
        using var document=JsonDocument.Parse(recorded);
        var original=document.RootElement;
        var supplier=new Supplier{Id=order.SupplierId,TenantId=order.TenantId,
            LegalEntityId=order.LegalEntityId,
            LegalName=original.GetProperty("LegalName").GetString()!,
            DisplayName=original.GetProperty("DisplayName").GetString()!,
            CountryCode=original.GetProperty("CountryCode").GetString()!};
        return recorded==Capture(order,lines,supplier,revision);
    }
}
