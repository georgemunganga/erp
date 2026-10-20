using Microsoft.EntityFrameworkCore;
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
        Assert.Contains(allocation.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(ReceivingSourceState)
            && fk.Properties.Select(x => x.Name).SequenceEqual(new[] { "TenantId","LegalEntityId","ReceivingSourceStateId","PurchaseOrderId","IssuedVersionId","PurchaseOrderLineId" }));
        foreach (var target in new[] { typeof(GoodsReceiptLine),typeof(ServiceAcceptance) })
            Assert.Contains(allocation.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == target
                && fk.Properties.Any(x => x.Name == "ReceivingSourceStateId") && fk.Properties.Any(x => x.Name == "PurchaseOrderLineId"));
        Assert.Equal(38,allocation.FindProperty(nameof(InvoiceMatchAllocation.NetAmount))!.GetPrecision());
        Assert.Equal(8,allocation.FindProperty(nameof(InvoiceMatchAllocation.NetAmount))!.GetScale());
        Assert.Contains(allocation.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(
            new[] { "TenantId","LegalEntityId","ExposureStateId","ExposureSequence","SupplierInvoiceLineId" }));
        var exception = db.Model.FindEntityType(typeof(InvoiceException))!;
        Assert.Contains(exception.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(InvoiceMatchResult)
            && fk.Properties.Any(x => x.Name == "SupplierInvoiceId") && fk.Properties.Any(x => x.Name == "MatchResultId"));
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
