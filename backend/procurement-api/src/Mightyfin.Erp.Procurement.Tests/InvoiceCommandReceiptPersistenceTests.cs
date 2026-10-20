using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Full context-save regression, not a replacement for the actual PostgreSQL HTTP gate.
/// Blocked result below is synthetic guard evidence; it establishes no qualified claim or AP authority.</summary>
public sealed class InvoiceCommandReceiptPersistenceTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    { public Guid? WorkLocationId => null; public Guid? OrgUnitId => null; public bool IsConfined => false; public string? CorrelationId => "invoice-receipt"; }

    [Fact]
    public async Task Original_mapping_pointer_and_blocked_comparison_each_save_their_real_order_owned_receipt()
    {
        var scope = new Scope("invoice-receipts", Guid.NewGuid(), "invoice-processor");
        var options = new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ProcurementDbContext(options, scope);
        var order = new PurchaseOrder { Number = "PO-RECEIPT", SupplierId = Guid.NewGuid(), CurrencyCode = "ZMW", BuyerSubjectId = scope.SubjectId };
        var invoice = new SupplierInvoice { SupplierId = order.SupplierId, PurchaseOrderId = order.Id, CurrencyCode = "ZMW",
            InvoiceNumber = "RECEIPT-1", NormalizedInvoiceNumber = "RECEIPT-1", InvoiceDate = new(2026,10,20),
            Status = "Received", NetAmount = 10, GrossAmount = 10, IntakeChannel = "Manual" };
        var line = new SupplierInvoiceLine { SupplierInvoiceId = invoice.Id, LineNumber = 1, Description = "Original",
            Quantity = 1, UnitPrice = 10 };
        db.AddScoped(order); db.AddScoped(invoice); db.AddScoped(line); await db.SaveChangesAsync();
        var original = InvoiceMappingEvidence.OriginalDigest(invoice, new[] { line });
        var mapping = InvoiceMappingEvidence.Create(invoice, new[] { line }, order.Id, Guid.NewGuid(),
            new[] { new InvoiceMappingLine(line.Id, Guid.NewGuid(), null, null, "each", Guid.Empty, "") },
            scope.SubjectId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.AddScoped(mapping); invoice.CurrentMappingVersionId = mapping.Id; invoice.MappingRevision = mapping.MappingRevision;
        invoice.MatchingState = "MappingUnverified";
        await db.SaveChangesAsync();
        var mappingVersion = invoice.Version;
        db.AddScoped(Receipt(order.Id, scope.SubjectId, "InvoiceMapping"));
        await db.SaveChangesAsync(); // The former operation allowlist rejected this actual final save.
        Assert.Equal(mappingVersion, invoice.Version);

        var evidence = "{\"fixture\":\"blocked-only\"}";
        var result = new InvoiceMatchResult { SupplierInvoiceId = invoice.Id, ExecutionVersion = 1,
            MatchType = "Unresolved", Outcome = "Blocked", MappingVersionId = mapping.Id, PurchaseOrderId = order.Id,
            IssuedVersionId = mapping.IssuedVersionId, InvoiceVersion = invoice.Version, InvoiceOriginalSha256 = mapping.InvoiceOriginalSha256,
            ActorPrincipalId = scope.SubjectId, ActorWorkerId = Guid.NewGuid(), CommandKey = Guid.NewGuid(),
            EvidenceJson = evidence, EvidenceSha256 = ProcurementEvidenceEncoding.Digest(evidence), MatchedAt = DateTimeOffset.UtcNow };
        db.AddScoped(result); invoice.LastMatchResultId = result.Id; invoice.MatchRevision++;
        invoice.MatchingState = "ComparisonBlocked"; await db.SaveChangesAsync();
        var comparedVersion = invoice.Version;
        db.AddScoped(Receipt(order.Id, scope.SubjectId, "InvoiceCompare")); await db.SaveChangesAsync();
        Assert.Equal(comparedVersion, invoice.Version);

        await using var read = new ProcurementDbContext(options, scope);
        var stored = await read.SupplierInvoices.SingleAsync(x => x.Id == invoice.Id);
        Assert.Equal(original, InvoiceMappingEvidence.OriginalDigest(stored, await read.SupplierInvoiceLines.ToArrayAsync()));
        Assert.Equal(mapping.Id, stored.CurrentMappingVersionId); Assert.Equal(result.Id, stored.LastMatchResultId);
        Assert.Null(stored.ActiveMatchClaimResultId); Assert.Null(stored.FinancePostingRef);
        Assert.Empty(await read.InvoiceMatchAllocations.ToArrayAsync());
        var receipts = await read.CommercialCommandReceipts.OrderBy(x => x.Operation).ToArrayAsync();
        Assert.Equal(new[] { "InvoiceCompare", "InvoiceMapping" }, receipts.Select(x => x.Operation));
        Assert.All(receipts, value => Assert.Equal(order.Id, value.PurchaseOrderId));
        Assert.Single(await read.PurchaseOrders.Where(x => x.Id == order.Id).ToArrayAsync());
    }

    [Theory]
    [InlineData("InvoiceMapping", "other-actor")]
    [InlineData("InvoiceCompare", "other-actor")]
    [InlineData("InvoiceWhatever", "invoice-processor")]
    [InlineData("Invoice", "invoice-processor")]
    public async Task Wrong_actor_and_unregistered_invoice_operation_do_not_persist_any_receipt_or_audit(string operation, string actor)
    {
        var scope = new Scope("invoice-receipt-denial", Guid.NewGuid(), "invoice-processor");
        var options = new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ProcurementDbContext(options, scope);
        db.AddScoped(Receipt(Guid.NewGuid(), actor, operation));
        Assert.Equal("OrderCommandReceiptActorInvalid", (await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync())).Message);
        await using var read = new ProcurementDbContext(options, scope);
        Assert.Empty(await read.CommercialCommandReceipts.ToArrayAsync());
        Assert.Empty(await read.AuditEvents.ToArrayAsync()); Assert.Empty(await read.OutboxEvents.ToArrayAsync());
    }

    private static ProcurementCommercialCommandReceipt Receipt(Guid order, string actor, string operation) => new()
    { PurchaseOrderId = order, ActorPrincipalId = actor, Operation = operation, CommandKey = Guid.NewGuid(),
        Fingerprint = new string('a',64), ResponseJson = "{\"recorded\":true}", CompletedAt = DateTimeOffset.UtcNow };
}
