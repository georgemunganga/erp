using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceProcurementHistoricalClaimsTests
{
    [Fact]
    public void Active_old_claim_requires_exact_pointer_graph_and_contiguous_exposure_sequence()
    {
        const string tenant="tenant-a";
        var company=Guid.NewGuid();var supplier=Guid.NewGuid();var po=Guid.NewGuid();
        var issued=Guid.NewGuid();var invoiceId=Guid.NewGuid();var lineId=Guid.NewGuid();
        var originalSha=new string('a',64);
        var mapping=new InvoiceMatchMappingVersion{TenantId=tenant,LegalEntityId=company,
            SupplierInvoiceId=invoiceId,PurchaseOrderId=po,IssuedVersionId=issued,MappingRevision=1,
            InvoiceOriginalSha256=originalSha};
        var result=new InvoiceMatchResult{TenantId=tenant,LegalEntityId=company,
            SupplierInvoiceId=invoiceId,MappingVersionId=mapping.Id,PurchaseOrderId=po,
            IssuedVersionId=issued,ExecutionVersion=1,InvoiceVersion=1,
            InvoiceOriginalSha256=originalSha,Outcome="Compared"};
        var invoice=new SupplierInvoice{Id=invoiceId,TenantId=tenant,LegalEntityId=company,
            SupplierId=supplier,CurrencyCode="ZMW",CurrentMappingVersionId=mapping.Id,
            ActiveMatchClaimResultId=result.Id};
        var orderLineId=Guid.NewGuid();
        var line=new SupplierInvoiceLine{Id=lineId,TenantId=tenant,LegalEntityId=company,
            SupplierInvoiceId=invoiceId,PurchaseOrderLineId=orderLineId};
        var exposure=new InvoiceMatchExposureState{TenantId=tenant,LegalEntityId=company,
            PurchaseOrderId=po,IssuedVersionId=issued,SupplierId=supplier,CurrencyCode="ZMW",Sequence=1};
        var allocation=new InvoiceMatchAllocation{TenantId=tenant,LegalEntityId=company,
            SupplierInvoiceId=invoiceId,SupplierInvoiceLineId=lineId,MatchResultId=result.Id,
            ExposureStateId=exposure.Id,PurchaseOrderId=po,IssuedVersionId=issued,
            PurchaseOrderLineId=orderLineId,SupplierId=supplier,CurrencyCode="ZMW",UnitCode="EA",
            Kind="Debit",Quantity=1,NetAmount=10,TaxAmount=0,ExposureSequence=1,
            ValidationReference="match",OwnerCoverageReference="owner"};
        var ids=new Dictionary<string,Guid>{{"supplier_invoices",invoiceId},{"supplier_invoice_lines",lineId},
            {"invoice_match_mapping_versions",mapping.Id},{"invoice_match_results",result.Id},
            {"invoice_match_allocations",allocation.Id},{"invoice_match_exposure_states",exposure.Id}};
        var tables=InvoiceProcurementOwnerTables.Names.Select(name=>
        {
            if(!ids.TryGetValue(name,out var id))
                return new InvoiceProcurementCapturedTable(name,[]);
            var json=JsonSerializer.Serialize(new{id,version=1,tenant_id=tenant,legal_entity_id=company});
            return new InvoiceProcurementCapturedTable(name,
                [new(id,1,ProcurementEvidenceEncoding.PolicyJsonDigest(json),json)]);
        }).ToArray();
        var capture=new InvoiceProcurementInventoryCapture(tenant,company,tables);
        var active=InvoiceProcurementHistoricalClaims.Verify(capture,[invoice],[line],[mapping],[result],[allocation],[exposure]);
        Assert.Equal(allocation.Id,Assert.Single(active).Id);
        line.IsActive=false;
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementHistoricalClaims.Verify(
            capture,[invoice],[line],[mapping],[result],[allocation],[exposure]));
        line.IsActive=true;
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementHistoricalClaims.Verify(
            capture,[new SupplierInvoice{Id=invoiceId,TenantId=tenant,LegalEntityId=company,
                SupplierId=supplier,CurrencyCode="ZMW",CurrentMappingVersionId=mapping.Id}],
            [line],[mapping],[result],[allocation],[exposure]));
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementHistoricalClaims.Verify(
            capture,[new SupplierInvoice{Id=invoiceId,TenantId=tenant,LegalEntityId=Guid.NewGuid(),
                SupplierId=supplier,CurrencyCode="ZMW",CurrentMappingVersionId=mapping.Id,
                ActiveMatchClaimResultId=result.Id}],
            [line],[mapping],[result],[allocation],[exposure]));
        exposure.Sequence=2;
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementHistoricalClaims.Verify(
            capture,[invoice],[line],[mapping],[result],[allocation],[exposure]));
    }
}
