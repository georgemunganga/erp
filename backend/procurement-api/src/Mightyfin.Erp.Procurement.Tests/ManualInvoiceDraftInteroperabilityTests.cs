using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Owning-store and codec regression only. InMemory does not prove issued-PO
/// authorization, PostgreSQL FKs, concurrency or Finance ownership.</summary>
public sealed class ManualInvoiceDraftInteroperabilityTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    { public Guid? WorkLocationId => null; public Guid? OrgUnitId => null; public bool IsConfined => false; public string? CorrelationId => null; }
    private static ProcurementDbContext Context() => new(new DbContextOptionsBuilder<ProcurementDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new Scope("intake-interop", Guid.NewGuid(), "maker"));
    private static async Task<(SupplierInvoice Header, SupplierInvoiceLine First, SupplierInvoiceLine Second)> Intake(ProcurementDbContext db)
    {
        var header = new SupplierInvoice { SupplierId = Guid.NewGuid(), InvoiceNumber = "MANUAL", NormalizedInvoiceNumber = "MANUAL",
            CurrencyCode = "ZMW", IntakeChannel = "Manual", InvoiceDate = new(2026,10,22), NetAmount = 3, GrossAmount = 3 };
        db.AddScoped(header);
        var one = new SupplierInvoiceLine { SupplierInvoiceId = header.Id, LineNumber = 1, Description = "First", Quantity = 1, UnitPrice = 1 };
        var two = new SupplierInvoiceLine { SupplierInvoiceId = header.Id, LineNumber = 2, Description = "Retained removed original", Quantity = 1, UnitPrice = 2 };
        db.AddScoped(one); db.AddScoped(two); await db.SaveChangesAsync(); return (header, one, two);
    }
    private static InvoiceMatchMappingVersion Mapping(SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> lines)
        => InvoiceMappingEvidence.Create(invoice, lines, Guid.NewGuid(), Guid.NewGuid(),
            lines.Where(line => line.IsActive).Select(line => new InvoiceMappingLine(line.Id, Guid.NewGuid(), null, null, "EACH", Guid.NewGuid(), "document-line")).ToArray(),
            "maker", Guid.NewGuid(), DateTimeOffset.UtcNow);
    [Fact]
    public async Task Manual_correction_retains_removed_history_then_first_mapping_freezes_exact_active_set()
    {
        await using var db = Context(); var (invoice, first, removed) = await Intake(db);
        first.Description = "Corrected first"; first.UnitPrice = 1.2345m; removed.IsActive = false;
        var added = new SupplierInvoiceLine { SupplierInvoiceId = invoice.Id, LineNumber = 3, Description = "Added correction", Quantity = 1, UnitPrice = 2 };
        db.AddScoped(added); invoice.NetAmount = invoice.GrossAmount = 3.2345m; await db.SaveChangesAsync();
        Assert.True(await db.ManualInvoiceDrafts().AnyAsync(x => x.Id == invoice.Id));
        Assert.Equal(3, await db.SupplierInvoiceLines.CountAsync(x => x.SupplierInvoiceId == invoice.Id));
        var all = await db.SupplierInvoiceLines.Where(x => x.SupplierInvoiceId == invoice.Id).ToArrayAsync();
        var active = all.Where(line => line.IsActive).ToArray();
        Assert.Equal(2, active.Length); Assert.Contains(all, line => line.Id == removed.Id && !line.IsActive && line.UnitPrice == 2);
        var mapping = Mapping(invoice, active); db.AddScoped(mapping);
        invoice.MappingRevision = mapping.MappingRevision; invoice.CurrentMappingVersionId = mapping.Id; invoice.MatchingState = "MappingUnverified";
        await db.SaveChangesAsync();
        Assert.False(await db.ManualInvoiceDrafts().AnyAsync(x => x.Id == invoice.Id));
        Assert.Equal("Unverified", mapping.VerificationState); // No original-unit/PO/Finance authority is invented by this test.
        Assert.Equal(mapping.InvoiceOriginalSha256, InvoiceMappingEvidence.OriginalDigest(invoice, all));
        Assert.Equal(2, InvoiceMappingEvidence.Verify(mapping, invoice, all).Lines.Count);
        Assert.Equal(2, InvoiceMatchDocument.From(invoice, all).Lines.Count);
        var before = await Snapshot(db, invoice.Id);
        first.Quantity = 2;
        Assert.Equal("OriginalInvoiceLineImmutable", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        db.ChangeTracker.Clear(); Assert.Equal(before, await Snapshot(db, invoice.Id));
        removed = await db.SupplierInvoiceLines.SingleAsync(x => x.Id == removed.Id); removed.IsActive = true;
        Assert.Equal("OriginalInvoiceLineImmutable", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        db.ChangeTracker.Clear(); Assert.Equal(before, await Snapshot(db, invoice.Id));
        invoice = await db.SupplierInvoices.SingleAsync(x => x.Id == invoice.Id); invoice.Status = "Cancelled";
        Assert.Equal("OriginalInvoiceImmutable", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        db.ChangeTracker.Clear(); Assert.Equal(before, await Snapshot(db, invoice.Id));
    }
    [Theory]
    [InlineData("document")]
    [InlineData("credit")]
    [InlineData("finance")]
    [InlineData("legacy-result")]
    public async Task Persisted_downstream_or_document_evidence_prevents_manual_correction(string kind)
    {
        await using var db = Context(); var (invoice, first, _) = await Intake(db);
        switch (kind)
        {
            case "document": db.AddScoped(new ProcurementAttachmentRef { OwnerType = nameof(SupplierInvoice), OwnerId = invoice.Id, DocumentServiceId = "opaque-owner-document", FileName = "invoice.pdf", ScanStatus = "Clean" }); break;
            case "credit": db.AddScoped(new SupplierCreditNoteRef { SupplierId = invoice.SupplierId, SupplierInvoiceId = invoice.Id, CreditNumber = "LEGACY", CurrencyCode = "ZMW", Amount = 1, Reason = "Retained source" }); break;
            case "finance": db.AddScoped(new FinanceInvoiceHandoff { SupplierInvoiceId = invoice.Id, IdempotencyKey = "retained-owner-ref" }); break;
            case "legacy-result": db.AddScoped(new InvoiceMatchResult { SupplierInvoiceId = invoice.Id, MatchType = "TwoWay", Outcome = "Unknown", MatchedAt = DateTimeOffset.UtcNow }); break;
        }
        await db.SaveChangesAsync(); var before = await Snapshot(db, invoice.Id); first.Description = "Must not overwrite";
        Assert.Equal("OriginalInvoiceLineImmutable", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        db.ChangeTracker.Clear(); Assert.Equal(before, await Snapshot(db, invoice.Id));
    }
    [Fact]
    public async Task First_mapping_and_material_correction_cannot_be_combined_to_adopt_a_new_original()
    {
        await using var db = Context(); var (invoice, first, second) = await Intake(db);
        var before = await Snapshot(db, invoice.Id); first.Description = "Unsaved replacement";
        var mapping = Mapping(invoice, [first, second]); db.AddScoped(mapping);
        invoice.MappingRevision = mapping.MappingRevision; invoice.CurrentMappingVersionId = mapping.Id; invoice.MatchingState = "MappingUnverified";
        Assert.Equal("OriginalInvoiceLineImmutable", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        db.ChangeTracker.Clear(); Assert.Equal(before, await Snapshot(db, invoice.Id)); Assert.Empty(await db.InvoiceMatchMappingVersions.ToArrayAsync());
    }
    [Fact]
    public async Task Attached_fake_unprocessed_parent_cannot_reopen_or_append_to_a_persisted_original()
    {
        await using var db = Context(); var (invoice, first, second) = await Intake(db);
        var mapping = Mapping(invoice, [first, second]); db.AddScoped(mapping);
        invoice.MappingRevision = mapping.MappingRevision; invoice.CurrentMappingVersionId = mapping.Id; invoice.MatchingState = "MappingUnverified";
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        db.Attach(new SupplierInvoice { Id = invoice.Id, TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierId = invoice.SupplierId, InvoiceNumber = invoice.InvoiceNumber, NormalizedInvoiceNumber = invoice.NormalizedInvoiceNumber,
            CurrencyCode = invoice.CurrencyCode, IntakeChannel = "Manual", InvoiceDate = invoice.InvoiceDate });
        db.AddScoped(new SupplierInvoiceLine { SupplierInvoiceId = invoice.Id, LineNumber = 3, Description = "Must not adopt", Quantity = 1, UnitPrice = 1 });
        Assert.Equal("OriginalInvoiceLineSetClosed", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
    }
    [Fact]
    public async Task Existing_line_only_save_cannot_bypass_the_owning_invoice_CAS()
    {
        await using var db = Context(); var (invoice, first, _) = await Intake(db);
        var before = await Snapshot(db, invoice.Id);
        first.Description = "Line-only change without parent fence";
        Assert.Equal(EntityState.Unchanged, db.Entry(invoice).State);
        Assert.Equal("OriginalInvoiceLineImmutable", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        db.ChangeTracker.Clear(); Assert.Equal(before, await Snapshot(db, invoice.Id));
    }
    [Fact]
    public async Task Manual_correction_requires_the_real_persisted_parent_version_not_a_changed_token()
    {
        await using var db = Context(); var (invoice, first, _) = await Intake(db);
        var before = await Snapshot(db, invoice.Id);
        first.Description = "Stale parent token"; invoice.NetAmount = invoice.GrossAmount = 4;
        db.Entry(invoice).Property(row => row.Version).OriginalValue = invoice.Version - 1;
        Assert.Equal("OriginalInvoiceLineImmutable", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        db.ChangeTracker.Clear(); Assert.Equal(before, await Snapshot(db, invoice.Id));
    }
    [Fact]
    public void All_active_legacy_original_digest_keeps_the_exact_version_one_wire_shape()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111"); var entity = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var supplier = Guid.Parse("33333333-3333-3333-3333-333333333333"); var lineId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var invoice = new SupplierInvoice { Id = id, TenantId = "legacy", LegalEntityId = entity, SupplierId = supplier,
            InvoiceNumber = "OLD", NormalizedInvoiceNumber = "OLD", InvoiceDate = new(2026,10,20), CurrencyCode = "ZMW", NetAmount = 1.2345m, GrossAmount = 1.2345m };
        var line = new SupplierInvoiceLine { Id = lineId, SupplierInvoiceId = id, TenantId = "legacy", LegalEntityId = entity,
            LineNumber = 1, Description = "Original", Quantity = 1, UnitPrice = 1.2345m };
        const string oldWire = """
            {"schemaVersion":1,"id":"11111111-1111-1111-1111-111111111111","tenantId":"legacy","legalEntityId":"22222222-2222-2222-2222-222222222222","branchRef":null,"supplierId":"33333333-3333-3333-3333-333333333333","purchaseOrderId":null,"invoiceNumber":"OLD","normalizedInvoiceNumber":"OLD","invoiceDate":"2026-10-20","dueDate":null,"currencyCode":"ZMW","net":"1.2345","tax":"0","gross":"1.2345","evidenceDocumentRefId":null,"creditForInvoiceRef":null,"lines":[{"id":"44444444-4444-4444-4444-444444444444","supplierInvoiceId":"11111111-1111-1111-1111-111111111111","tenantId":"legacy","legalEntityId":"22222222-2222-2222-2222-222222222222","lineNumber":1,"description":"Original","quantity":"1","unitPrice":"1.2345","tax":"0","taxCodeRef":null,"costCenterRef":null}]}
            """;
        Assert.Equal(ProcurementEvidenceEncoding.PolicyJsonDigest(oldWire), InvoiceMappingEvidence.OriginalDigest(invoice, [line]));
        var retired = new SupplierInvoiceLine { Id = Guid.NewGuid(), SupplierInvoiceId = id, TenantId = "legacy", LegalEntityId = entity,
            LineNumber = 2, Description = "Retained history excluded from commercial original", Quantity = 1, UnitPrice = 99, IsActive = false };
        Assert.Equal(InvoiceMappingEvidence.OriginalDigest(invoice, [line]), InvoiceMappingEvidence.OriginalDigest(invoice, [line, retired]));
    }
    private static async Task<string> Snapshot(ProcurementDbContext db, Guid id) => JsonSerializer.Serialize(new
    {
        invoice = await db.SupplierInvoices.AsNoTracking().SingleAsync(x => x.Id == id),
        lines = await db.SupplierInvoiceLines.AsNoTracking().Where(x => x.SupplierInvoiceId == id).OrderBy(x => x.LineNumber).ToArrayAsync(),
        mappings = await db.InvoiceMatchMappingVersions.AsNoTracking().Where(x => x.SupplierInvoiceId == id).OrderBy(x => x.Id).ToArrayAsync(),
        auditCount = await db.AuditEvents.CountAsync(),
    });
}
