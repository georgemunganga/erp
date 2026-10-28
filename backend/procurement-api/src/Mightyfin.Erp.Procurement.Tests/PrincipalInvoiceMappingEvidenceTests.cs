using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalInvoiceMappingEvidenceTests
{
    private static (SupplierInvoice Invoice,SupplierInvoiceLine Line) Original()
    {
        var invoice=new SupplierInvoice
        {
            TenantId="m6-test",LegalEntityId=Guid.NewGuid(),BranchRef=Guid.NewGuid(),
            SupplierId=Guid.NewGuid(),InvoiceNumber="M6-1",NormalizedInvoiceNumber="M6-1",
            CurrencyCode="ZMW",IntakeChannel="Manual",InvoiceDate=new DateOnly(2026,10,28),
            NetAmount=1m,GrossAmount=1m
        };
        var line=new SupplierInvoiceLine
        {
            TenantId=invoice.TenantId,LegalEntityId=invoice.LegalEntityId,
            SupplierInvoiceId=invoice.Id,LineNumber=1,Description="Original service",
            Quantity=1m,UnitPrice=1m
        };
        return(invoice,line);
    }

    [Fact]
    public void Separate_principal_payload_keeps_workerless_identity_and_exact_source_pair()
    {
        var (invoice,line)=Original();var order=Guid.NewGuid();var issued=Guid.NewGuid();
        var source=Guid.NewGuid();var fact=Guid.NewGuid();
        var link=new PrincipalInvoiceMappingLine(line.Id,Guid.NewGuid(),source,fact,"EACH",Guid.NewGuid(),"line 1");
        var payload=PrincipalInvoiceMappingEvidence.Create(invoice,[line],order,issued,[link],Guid.NewGuid().ToString("D"),null);
        Assert.Equal(3,payload.Snapshot.SchemaVersion);
        Assert.Null(payload.Snapshot.ActorWorkerId);
        Assert.Equal(fact,Assert.Single(payload.Snapshot.Lines).PrincipalReceivingFactId);
        var recovered=PrincipalInvoiceMappingEvidence.Verify(payload.Json,payload.Sha256,invoice,[line],order,issued,1);
        Assert.Equal(payload.Snapshot.ActorPrincipalId,recovered.ActorPrincipalId);
        Assert.Equal(payload.Snapshot.InvoiceOriginalSha256,recovered.InvoiceOriginalSha256);
        Assert.Equal(link,Assert.Single(recovered.Lines));
        Assert.DoesNotContain("goodsReceiptLineId",payload.Json,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("serviceAcceptanceId",payload.Json,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Two_way_mapping_has_no_physical_fact_and_mixed_or_stale_links_fail()
    {
        var (invoice,line)=Original();var order=Guid.NewGuid();var issued=Guid.NewGuid();var actor=Guid.NewGuid().ToString("D");
        var twoWay=new PrincipalInvoiceMappingLine(line.Id,Guid.NewGuid(),null,null,"EACH",Guid.Empty,"");
        var payload=PrincipalInvoiceMappingEvidence.Create(invoice,[line],order,issued,[twoWay],actor,null);
        Assert.Null(Assert.Single(PrincipalInvoiceMappingEvidence.Verify(payload.Json,payload.Sha256,invoice,[line],order,issued,1).Lines).PrincipalReceivingFactId);
        var documentedTwoWay=twoWay with{DocumentVersionId=Guid.NewGuid(),DocumentLineReference="invoice line 1"};
        Assert.Equal(documentedTwoWay,Assert.Single(PrincipalInvoiceMappingEvidence.Create(invoice,[line],order,issued,[documentedTwoWay],actor,null).Snapshot.Lines));
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceMappingEvidence.Create(invoice,[line],order,issued,
            [twoWay with{PrincipalReceivingFactId=Guid.NewGuid()}],actor,null));
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceMappingEvidence.Create(invoice,[line],order,issued,
            [twoWay with{InvoiceLineId=Guid.NewGuid()}],actor,null));
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceMappingEvidence.Verify(payload.Json,payload.Sha256,invoice,[line],order,Guid.NewGuid(),1));
        line.UnitPrice=2m;
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceMappingEvidence.Verify(payload.Json,payload.Sha256,invoice,[line],order,issued,1));
    }

    [Fact]
    public void Self_hashed_unknown_or_duplicate_payload_never_becomes_a_mapping()
    {
        var (invoice,line)=Original();var order=Guid.NewGuid();var issued=Guid.NewGuid();
        var payload=PrincipalInvoiceMappingEvidence.Create(invoice,[line],order,issued,
            [new(line.Id,Guid.NewGuid(),null,null,"EACH",Guid.Empty,"")],Guid.NewGuid().ToString("D"),null);
        var duplicate=payload.Json.Replace("\"schemaVersion\":3","\"schemaVersion\":3,\"schemaVersion\":3",StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceMappingEvidence.Verify(duplicate,
            ProcurementEvidenceEncoding.PolicyJsonDigest(duplicate),invoice,[line],order,issued,1));
        var unknown=payload.Json.Replace("\"schemaVersion\":3","\"schemaVersion\":3,\"unsupported\":true",StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceMappingEvidence.Verify(unknown,
            ProcurementEvidenceEncoding.PolicyJsonDigest(unknown),invoice,[line],order,issued,1));
    }

    [Fact]
    public void Three_way_selection_requires_exact_verified_issued_source_fact_and_line()
    {
        var (invoice,line)=Original();var order=Guid.NewGuid();var issued=Guid.NewGuid();
        var source=Guid.NewGuid();var factId=Guid.NewGuid();var orderLine=Guid.NewGuid();
        var link=new PrincipalInvoiceMappingLine(line.Id,orderLine,source,factId,"EACH",Guid.NewGuid(),"line 1");
        var payload=PrincipalInvoiceMappingEvidence.Create(invoice,[line],order,issued,[link],Guid.NewGuid().ToString("D"),null);
        var fact=new PrincipalInvoiceAcceptedFact(factId,source,1,order,issued,orderLine,
            ReceivingKind.Goods,1m,1m,new string('a',64),DateTimeOffset.UtcNow);
        var history=new PrincipalInvoiceAcceptedHistory(source,new string('b',64),1,[fact]);
        PrincipalInvoiceMappingEvidence.VerifyAcceptedSources(payload.Snapshot,[history]);
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceMappingEvidence.VerifyAcceptedSources(payload.Snapshot,[]));
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceMappingEvidence.VerifyAcceptedSources(payload.Snapshot,[history,history]));
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceMappingEvidence.VerifyAcceptedSources(payload.Snapshot,[history with{Facts=[fact with{PurchaseOrderLineId=Guid.NewGuid()}]}]));
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceMappingEvidence.VerifyAcceptedSources(payload.Snapshot,[history with{Facts=[fact with{IssuedVersionId=Guid.NewGuid()}]}]));
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceMappingEvidence.VerifyAcceptedSources(payload.Snapshot,[history with{Facts=[fact with{AcceptedQuantity=0}]}]));
    }

    [Fact]
    public void Retained_schema_three_mapping_row_binds_actor_scope_and_payload_without_legacy_rewrite()
    {
        var (invoice,line)=Original();var order=Guid.NewGuid();var issued=Guid.NewGuid();
        var actor=Guid.NewGuid().ToString("D");
        var payload=PrincipalInvoiceMappingEvidence.Create(invoice,[line],order,issued,
            [new(line.Id,Guid.NewGuid(),null,null,"EACH",Guid.Empty,"")],actor,null);
        var row=new InvoiceMatchMappingVersion{TenantId=invoice.TenantId,LegalEntityId=invoice.LegalEntityId,
            SupplierInvoiceId=invoice.Id,PurchaseOrderId=order,IssuedVersionId=issued,MappingRevision=1,
            InvoiceOriginalSha256=payload.Snapshot.InvoiceOriginalSha256,MappingJson=payload.Json,MappingSha256=payload.Sha256,
            ActorPrincipalId=actor,ActorWorkerId=null,IdentitySchemaVersion=2};
        Assert.Equal(3,PrincipalInvoiceMappingEvidence.VerifyStored(row,invoice,[line]).SchemaVersion);
        Assert.Throws<InvalidOperationException>(()=>InvoiceMappingEvidence.Verify(row,invoice,[line]));
        row=new InvoiceMatchMappingVersion{TenantId=invoice.TenantId,LegalEntityId=invoice.LegalEntityId,
            SupplierInvoiceId=invoice.Id,PurchaseOrderId=order,IssuedVersionId=issued,MappingRevision=1,
            InvoiceOriginalSha256=payload.Snapshot.InvoiceOriginalSha256,MappingJson=payload.Json,MappingSha256=payload.Sha256,
            ActorPrincipalId=Guid.NewGuid().ToString("D"),ActorWorkerId=null,IdentitySchemaVersion=2};
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceMappingEvidence.VerifyStored(row,invoice,[line]));
    }
}
