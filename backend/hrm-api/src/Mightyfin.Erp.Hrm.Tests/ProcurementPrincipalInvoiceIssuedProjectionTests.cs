using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalInvoiceIssuedProjectionTests
{
    [Fact]
    public void Unit_review_projects_all_issued_lines_without_any_receiving_state()
    {
        var retained=Fixture();
        var projected=ProcurementPrincipalInvoiceIssuedWitness.ProjectIssuedOrder(retained);
        Assert.Equal(2,projected.Lines.Count);
        Assert.Equal(retained.Lines.Select(x=>x.Id),projected.Lines.Select(x=>x.Id));
        Assert.Equal(new[]{"ORIGINAL_A","ORIGINAL_B"},projected.Lines.Select(x=>x.CategoryCode));
        Assert.Equal(retained.Issued.AuthorizedAt,projected.IssuedAt);
        Assert.NotEqual(retained.Acknowledgment.ObservedAt,projected.IssuedAt);
        Assert.Equal(retained.Issued.CommercialSnapshotSha256,projected.IssuedReference);
        Assert.Equal(retained.Order.SupplierId,projected.SupplierId);
        Assert.Equal(retained.Order.CurrencyCode,projected.CurrencyCode);
        Assert.Null(retained.SelectedLine);
    }

    [Fact]
    public void Missing_or_duplicate_original_lines_cannot_shrink_the_reviewed_issued_set()
    {
        var retained=Fixture();
        Assert.Throws<InvalidOperationException>(()=>ProcurementPrincipalInvoiceIssuedWitness.ProjectIssuedOrder(
            retained with{Sources=retained.Sources.Take(1).ToArray()}));
        Assert.Throws<InvalidOperationException>(()=>ProcurementPrincipalInvoiceIssuedWitness.ProjectIssuedOrder(
            retained with{Sources=[retained.Sources[0],retained.Sources[0]]}));
        Assert.Throws<InvalidOperationException>(()=>ProcurementPrincipalInvoiceIssuedWitness.ProjectIssuedOrder(
            retained with{Sources=[retained.Sources[0] with{OriginalCategoryCode=null},retained.Sources[1]]}));
    }

    private static ProcurementPrincipalOrderRetainedIssued.Result Fixture()
    {
        var company=Guid.NewGuid();var order=new PurchaseOrder
        {
            TenantId="m6-unit-review",LegalEntityId=company,BranchRef=Guid.NewGuid(),
            SupplierId=Guid.NewGuid(),CurrencyCode="ZMW",PrincipalDraftSchemaVersion=2,Status="Issued"
        };
        var lines=new[]{Line(order,1),Line(order,2)};
        var sources=lines.Select((line,index)=>new ProcurementPrincipalOrderRetainedIssued.Source(
            line,new PrincipalOrderSourceBinding(),null!,new PurchaseRequestAllocation(),
            index==0?"ORIGINAL_A":"ORIGINAL_B","SERVICES")).ToArray();
        var at=new DateTimeOffset(2026,10,28,12,0,0,TimeSpan.Zero);
        var issued=new PrincipalOrderIssuedVersion{PurchaseOrderId=order.Id,CommercialRevision=1,
            AuthorizedAt=at,CommercialSnapshotSha256=new string('a',64)};
        return new(order,lines,sources,null,issued,new PrincipalOrderSubmission(),
            new PrincipalOrderFundingEvidence(),new PrincipalOrderAcknowledgment{ObservedAt=at.AddHours(1)},
            new ApprovalCase(),[],[]);
    }
    private static PurchaseOrderLine Line(PurchaseOrder order,int number)=>new()
    {
        TenantId=order.TenantId,LegalEntityId=order.LegalEntityId,PurchaseOrderId=order.Id,
        LineNumber=number,UnitCode="EACH",Quantity=number,UnitPrice=5.1234m
    };
}
