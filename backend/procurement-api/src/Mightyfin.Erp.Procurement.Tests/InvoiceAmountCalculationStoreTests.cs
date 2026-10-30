using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Runs after Root wires model/save hooks; InMemory proves guards, not PG constraints.</summary>
public sealed class InvoiceAmountCalculationStoreTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    {
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "amount-store-tests";
    }

    private static (ProcurementDbContext Db, InvoiceAmountCalculation Row) Stage(bool audit = true, bool outbox = true)
    {
        var (invoice, lines, row) = InvoiceAmountCalculationEvidenceTests.Fixture();
        var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new Scope(invoice.TenantId, invoice.LegalEntityId, InvoiceAmountCalculationEvidenceTests.Actor));
        db.AddScoped(invoice); foreach (var line in lines) db.AddScoped(line); db.AddScoped(row);
        db.AddScoped(InvoiceIntakeCommandReceiptTests.ValidReceipt(invoice, lines, row));
        var marker = InvoiceAmountCalculationEvidence.MarkerJson(row);
        if (audit) db.AddScoped(new AuditEvent { RecordType = nameof(InvoiceAmountCalculation), RecordId = row.Id,
            Action = InvoiceAmountCalculationEvidence.AuditAction, ActorSubjectId = row.CreatedBy,
            CreatedBy = row.CreatedBy, ChangedFieldsJson = marker });
        if (outbox) db.AddScoped(new OutboxEvent { AggregateType = nameof(SupplierInvoice), AggregateId = invoice.Id,
            EventType = InvoiceAmountCalculationEvidence.EventType, EventVersion = 2, CreatedBy = row.CreatedBy,
            PayloadJson = marker, IdempotencyKey = "invoice-amount-calculation:" + row.Id.ToString("D") });
        return (db,row);
    }

    [Fact]
    public async Task Atomic_intake_persists_parent_child_and_explicit_markers_then_rejects_child_edit()
    {
        var (db,row) = Stage(); await using var owned = db;
        await db.SaveChangesAsync();
        Assert.Equal(1, await db.InvoiceAmountCalculations.CountAsync());
        var invoice = await db.SupplierInvoices.SingleAsync();
        var lines = await db.SupplierInvoiceLines.ToArrayAsync();
        _ = InvoiceAmountCalculationEvidence.Verify(row, invoice, lines);
        row.CoreCurrencyReferenceRevision++;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(false,true)][InlineData(true,false)][InlineData(false,false)]
    public async Task Generic_create_audits_do_not_replace_explicit_capture_markers(bool audit, bool outbox)
    {
        var (db,_) = Stage(audit,outbox); await using var owned = db;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal(0, await db.SupplierInvoices.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Marker_cannot_be_repointed_but_publisher_delivery_state_can_advance()
    {
        var (db,_) = Stage(); await using var owned = db; await db.SaveChangesAsync();
        var marker = await db.OutboxEvents.SingleAsync(x => x.EventType == InvoiceAmountCalculationEvidence.EventType);
        marker.State = "Published"; marker.PublishedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        marker.PayloadJson = "{}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Child_delete_is_denied()
    {
        var (db,_) = Stage(); await using var owned = db; await db.SaveChangesAsync();
        // Reload only the child so EF's tracked required receipt relationship
        // does not reject Remove before the store's immutability guard runs.
        db.ChangeTracker.Clear();
        var row = await db.InvoiceAmountCalculations.SingleAsync();
        db.InvoiceAmountCalculations.Remove(row);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void Model_has_scoped_parent_restrict_fk_and_unique_child_without_legacy_invoice_property()
    {
        var (db,_) = Stage(); using var owned = db;
        var entity = db.Model.FindEntityType(typeof(InvoiceAmountCalculation))!;
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { "TenantId", "LegalEntityId", "SupplierInvoiceId" }));
        Assert.Contains(entity.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(SupplierInvoice)
            && x.DeleteBehavior == DeleteBehavior.Restrict && x.Properties.Count == 3);
        Assert.DoesNotContain(db.Model.FindEntityType(typeof(SupplierInvoice))!.GetProperties(),
            x => x.Name == "CalculationSchemaVersion");
    }
    [Theory]
    [InlineData("header")][InlineData("line")][InlineData("added-line")][InlineData("moved-line")]
    public async Task Existing_v2_original_cannot_be_changed_through_old_intake_store_path(string change)
    {
        var (db,_) = Stage(); await using var owned = db; await db.SaveChangesAsync();
        // Force the save guard to resolve evidence from the store, rather than
        // accidentally trusting an already-tracked child supplied by a caller.
        db.ChangeTracker.Clear();
        var invoice = await db.SupplierInvoices.SingleAsync();
        var line = await db.SupplierInvoiceLines.SingleAsync();
        // Existing manual-line maintenance requires an owning parent CAS write.
        db.Entry(invoice).Property(x => x.UpdatedAt).IsModified = true;
        switch (change)
        {
            case "header": invoice.NetAmount = 5.13m; invoice.GrossAmount = 5.23m; break;
            case "line": line.UnitPrice = 5.1235m; break;
            case "added-line": db.AddScoped(new SupplierInvoiceLine { SupplierInvoiceId = invoice.Id,
                Description = "Hidden zero line", LineNumber = 2, Quantity = 1, UnitPrice = 0 }); break;
            case "moved-line": line.SupplierInvoiceId = Guid.NewGuid(); break;
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Existing_v2_matching_state_can_advance_without_changing_original()
    {
        var (db,_) = Stage(); await using var owned = db; await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var invoice = await db.SupplierInvoices.SingleAsync();
        invoice.MatchingState = "ComparisonBlocked";
        await db.SaveChangesAsync();
        var retained = await db.InvoiceAmountCalculations.AsNoTracking().SingleAsync();
        _ = InvoiceAmountCalculationEvidence.Verify(retained, invoice,
            await db.SupplierInvoiceLines.AsNoTracking().ToArrayAsync());
        Assert.Equal(2, invoice.Version);
    }

    [Fact]
    public async Task Existing_v1_without_calculation_child_keeps_original_editing_behavior()
    {
        var (db,row) = Stage(); await using var owned = db;
        db.Entry(row).State = EntityState.Detached;
        foreach (var entry in db.ChangeTracker.Entries<InvoiceIntakeCommandReceipt>().ToArray()) entry.State = EntityState.Detached;
        foreach (var entry in db.ChangeTracker.Entries<AuditEvent>().ToArray()) entry.State = EntityState.Detached;
        foreach (var entry in db.ChangeTracker.Entries<OutboxEvent>().ToArray()) entry.State = EntityState.Detached;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var invoice = await db.SupplierInvoices.SingleAsync();
        var line = await db.SupplierInvoiceLines.SingleAsync();
        line.UnitPrice = 6; invoice.NetAmount = 6; invoice.GrossAmount = 6.10m;
        await db.SaveChangesAsync();
        Assert.Equal(6m, (await db.SupplierInvoices.AsNoTracking().SingleAsync()).NetAmount);
        Assert.Empty(await db.InvoiceAmountCalculations.AsNoTracking().ToArrayAsync());
    }

}
