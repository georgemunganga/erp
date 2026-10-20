using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceMatchingPersistenceTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    { public Guid? WorkLocationId => null; public Guid? OrgUnitId => null; public bool IsConfined => false; public string? CorrelationId => null; }
    private static ProcurementDbContext Context() => new(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new Scope("invoice-test", Guid.NewGuid(), "matcher"));
    [Fact]
    public void Claims_bind_exact_invoice_line_result_exposure_issued_line_and_receiving_source_state()
    {
        using var db = Context(); var allocation = db.Model.FindEntityType(typeof(InvoiceMatchAllocation))!;
        Assert.Contains(allocation.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(SupplierInvoiceLine)
            && fk.Properties.Select(x => x.Name).SequenceEqual(new[] { "TenantId","LegalEntityId","SupplierInvoiceLineId","SupplierInvoiceId" }));
        var result=db.Model.FindEntityType(typeof(InvoiceMatchResult))!;
        Assert.Contains(result.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(InvoiceMatchMappingVersion)
            && fk.Properties.Select(x=>x.Name).SequenceEqual(new[] { "TenantId","LegalEntityId","MappingVersionId","SupplierInvoiceId","PurchaseOrderId","IssuedVersionId" }));
        Assert.Contains(allocation.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(ReceivingSourceState)
            && fk.Properties.Select(x => x.Name).SequenceEqual(new[] { "TenantId","LegalEntityId","ReceivingSourceStateId","PurchaseOrderId","IssuedVersionId","PurchaseOrderLineId" }));
        foreach (var target in new[] { typeof(GoodsReceiptLine),typeof(ServiceAcceptance) })
            Assert.Contains(allocation.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == target
                && fk.Properties.Select(x=>x.Name).SequenceEqual(new[] { "TenantId","LegalEntityId",target==typeof(GoodsReceiptLine)?"GoodsReceiptLineId":"ServiceAcceptanceId" }));
        Assert.Equal(38,allocation.FindProperty(nameof(InvoiceMatchAllocation.NetAmount))!.GetPrecision());
        Assert.Equal(8,allocation.FindProperty(nameof(InvoiceMatchAllocation.NetAmount))!.GetScale());
        Assert.Contains(allocation.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(
            new[] { "TenantId","LegalEntityId","ExposureStateId","ExposureSequence","SupplierInvoiceLineId" }));
        var exception = db.Model.FindEntityType(typeof(InvoiceException))!;
        Assert.Contains(exception.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(InvoiceMatchResult)
            && fk.Properties.Any(x => x.Name == "SupplierInvoiceId") && fk.Properties.Any(x => x.Name == "MatchResultId"));
    }
    [Fact]
    public void Matching_does_not_reintroduce_mutable_receiving_completion_pins_as_EF_keys()
    {
        using var db=Context();
        foreach(var (type,names) in new[] {
            (typeof(GoodsReceiptLine),new[] { "SourceStateBindingId","PhysicalBindingId","ReceivingSourceStateId","PhysicalObservationId" }),
            (typeof(ServiceAcceptance),new[] { "SourceStateBindingId","ReceivingSourceStateId","CertificationEffectId" }) })
        {
            var entity=db.Model.FindEntityType(type)!;
            foreach(var name in names)
            {
                var property=entity.FindProperty(name)!;
                Assert.DoesNotContain(entity.GetKeys(),key=>key.Properties.Contains(property));
                Assert.Equal(ValueGenerated.Never,property.ValueGenerated);
            }
            Assert.Contains(entity.GetIndexes(),index=>index.IsUnique && index.Properties.Select(x=>x.Name)
                .SequenceEqual(new[] { "TenantId","LegalEntityId","Id","PurchaseOrderLineId","SourceStateBindingId" }));
        }
    }
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task Retained_mapping_and_capacity_claim_cannot_be_overwritten(bool mapping)
    {
        await using var db = Context();
        if(mapping) db.Entry(new InvoiceMatchMappingVersion()).State = EntityState.Modified;
        else db.Entry(new InvoiceMatchAllocation()).State = EntityState.Modified;
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
        Assert.Equal(mapping ? "InvoiceMappingAppendOnly" : "InvoiceAllocationAppendOnly",error.Message);
    }
    [Fact]
    public async Task Original_intake_stays_usable_but_material_header_or_line_overwrite_is_denied()
    {
        await using var db = Context(); var invoice = new SupplierInvoice { SupplierId = Guid.NewGuid(), InvoiceNumber = "INV-1",
            NormalizedInvoiceNumber = "INV-1", CurrencyCode = "ZMW", IntakeChannel = "Manual", InvoiceDate = new(2026,10,20), NetAmount=1,GrossAmount=1 };
        db.AddScoped(invoice); var line = new SupplierInvoiceLine { SupplierInvoiceId = invoice.Id, LineNumber = 1, Description = "Original", Quantity = 1, UnitPrice = 1 };
        db.AddScoped(line); await db.SaveChangesAsync();
        Assert.Equal("NotCompared",invoice.MatchingState); Assert.Null(invoice.CurrentMappingVersionId);
        line.Quantity=2;
        Assert.Equal("OriginalInvoiceLineImmutable",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
        db.ChangeTracker.Clear(); invoice=await db.SupplierInvoices.SingleAsync(); invoice.NetAmount=2;
        Assert.Equal("OriginalInvoiceImmutable",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
    }
    [Fact]
    public async Task Persisted_intake_line_set_cannot_grow_after_its_original_document_was_captured()
    {
        await using var db=Context(); var invoice=new SupplierInvoice { SupplierId=Guid.NewGuid(),InvoiceNumber="INV-1",NormalizedInvoiceNumber="INV-1",CurrencyCode="ZMW",IntakeChannel="Manual",InvoiceDate=new(2026,10,20) };
        db.AddScoped(invoice); await db.SaveChangesAsync();
        db.AddScoped(new SupplierInvoiceLine { SupplierInvoiceId=invoice.Id,LineNumber=1,Description="Late inserted line",Quantity=1,UnitPrice=1 });
        Assert.Equal("OriginalInvoiceLineSetClosed",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
    }
    [Fact]
    public async Task Blocked_result_still_cannot_bind_a_valid_mapping_to_a_different_issued_source()
    {
        await using var db=Context(); var invoice=new SupplierInvoice { SupplierId=Guid.NewGuid(),InvoiceNumber="INV-1",NormalizedInvoiceNumber="INV-1",CurrencyCode="ZMW",IntakeChannel="Manual",InvoiceDate=new(2026,10,20),NetAmount=1,GrossAmount=1 };
        db.AddScoped(invoice); var line=new SupplierInvoiceLine { SupplierInvoiceId=invoice.Id,LineNumber=1,Description="Original",Quantity=1,UnitPrice=1 };
        db.AddScoped(line); await db.SaveChangesAsync();
        var mapping=InvoiceMappingEvidence.Create(invoice,[line],Guid.NewGuid(),Guid.NewGuid(),
            [new(line.Id,Guid.NewGuid(),null,null,"each",Guid.Empty,"")],"matcher",Guid.NewGuid(),DateTimeOffset.UtcNow);
        db.AddScoped(mapping); invoice.CurrentMappingVersionId=mapping.Id; invoice.MappingRevision=mapping.MappingRevision; await db.SaveChangesAsync();
        db.AddScoped(new InvoiceMatchResult { SupplierInvoiceId=invoice.Id,ExecutionVersion=1,MappingVersionId=mapping.Id,
            PurchaseOrderId=Guid.NewGuid(),IssuedVersionId=Guid.NewGuid(),InvoiceVersion=invoice.Version,
            InvoiceOriginalSha256=mapping.InvoiceOriginalSha256,Outcome="Blocked",ActorPrincipalId="matcher",ActorWorkerId=Guid.NewGuid(),
            CommandKey=Guid.NewGuid(),EvidenceJson="{}",EvidenceSha256=ProcurementEvidenceEncoding.Digest("{}") });
        Assert.Equal("InvoiceMatchCurrentSourceRequired",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
    }
    [Fact]
    public async Task A_new_claim_cannot_reuse_an_unchanged_previously_consumed_exposure_sequence()
    {
        var f=new InvoiceMatchExecutionEvidenceTests.Fixture();
        var claim=Assert.Single(f.Claims()); // Positive pure authority check first; no fabricated incomplete source rows.
        await using var db=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,f);
        f.Exposure.Sequence=claim.ExposureSequence; f.Exposure.CoverageState="Verified";
        f.Exposure.OwnerReference=f.Owner.OwnerReference; f.Exposure.OwnerWatermark=f.Owner.Watermark;
        f.Exposure.OwnerEvidenceSha256=f.Owner.EvidenceSha256; f.Exposure.OwnerValidUntil=f.Owner.ValidUntil;
        f.Invoice.ActiveMatchClaimResultId=f.Result.Id;
        db.Attach(f.Invoice); db.Attach(f.Line); db.Attach(f.Mapping); db.Attach(f.Result); db.Attach(f.Exposure);
        Assert.Equal(EntityState.Unchanged,db.Entry(f.Exposure).State);
        db.AddScoped(claim);
        // All current original/mapping/Compared result/lease/source/amount bindings are valid.
        // Only the absence of a new tracked exposure advancement must deny this new claim.
        Assert.Equal("InvoiceAllocationAuthorityRequired",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
    }
    [Fact]
    public async Task Unknown_checkpoint_cannot_invent_a_claim_sequence_without_immutable_claim_batch()
    {
        await using var db = Context(); var exposure = new InvoiceMatchExposureState { PurchaseOrderId=Guid.NewGuid(),IssuedVersionId=Guid.NewGuid(),SupplierId=Guid.NewGuid(),CurrencyCode="ZMW" };
        db.AddScoped(exposure); await db.SaveChangesAsync(); exposure.Sequence=1;
        Assert.Equal("InvoiceExposureClaimBatchRequired",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
    }
    [Fact]
    public async Task Current_mapping_pointer_cannot_be_rebound_without_new_complete_mapping_fact()
    {
        await using var db=Context(); var invoice=new SupplierInvoice { SupplierId=Guid.NewGuid(),InvoiceNumber="INV-1",NormalizedInvoiceNumber="INV-1",CurrencyCode="ZMW",IntakeChannel="Manual",InvoiceDate=new(2026,10,20) };
        db.AddScoped(invoice); await db.SaveChangesAsync(); invoice.CurrentMappingVersionId=Guid.NewGuid(); invoice.MappingRevision=1;
        Assert.Equal("InvoiceMappingPointerFactRequired",(await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync())).Message);
    }
}
