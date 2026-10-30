using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceReplacementPersistenceTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    {
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "replacement-persistence";
    }
    private static readonly Scope Current = new("replacement-persistence", Guid.NewGuid(), Guid.NewGuid().ToString("D"));

    [Fact]
    public void PostgreSql_model_binds_replacement_endpoints_and_receipts_with_scoped_restrictive_keys()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=never_opened_replacement_model;Username=invalid").Options, Current);
        var model = db.GetService<IDesignTimeModel>().Model;
        var link = model.FindEntityType(typeof(InvoiceReplacementLink))!;
        var receipt = model.FindEntityType(typeof(InvoiceReplacementCommandReceipt))!;
        Assert.Equal("invoice_replacement_links", link.GetTableName());
        Assert.Equal("invoice_replacement_command_receipts", receipt.GetTableName());
        Assert.Equal(8, link.GetForeignKeys().Count());
        Assert.Equal(3, receipt.GetForeignKeys().Count());
        foreach (var fk in link.GetForeignKeys().Concat(receipt.GetForeignKeys()))
        {
            Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
            Assert.Equal(new[] { "TenantId", "LegalEntityId" }, fk.Properties.Take(2).Select(x => x.Name));
        }
        foreach (var name in new[] { "ux_invoice_replacement_source", "ux_invoice_replacement_successor" })
            Assert.True(link.GetIndexes().Single(x => x.GetDatabaseName() == name).IsUnique);
        foreach (var name in new[] { "ux_invoice_replacement_receipt_link", "ux_invoice_replacement_receipt_actor_key" })
            Assert.True(receipt.GetIndexes().Single(x => x.GetDatabaseName() == name).IsUnique);
        Assert.Contains("source_invoice_id<>successor_invoice_id", link.GetCheckConstraints().Single().Sql);
        Assert.Contains("octet_length(reason) BETWEEN 1 AND 1000", link.GetCheckConstraints().Single().Sql);
        Assert.Contains("response_status=201", receipt.GetCheckConstraints().Single().Sql);
    }

    [Theory]
    [InlineData(false, EntityState.Modified)]
    [InlineData(false, EntityState.Deleted)]
    [InlineData(true, EntityState.Modified)]
    [InlineData(true, EntityState.Deleted)]
    public async Task Existing_replacement_link_and_receipt_cannot_be_overwritten_or_deleted(bool receipt, EntityState state)
    {
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ProcurementDbContext(options, Current);
        ProcurementRecord row = receipt ? new InvoiceReplacementCommandReceipt() : new InvoiceReplacementLink();
        row.TenantId = Current.TenantId; row.LegalEntityId = Current.LegalEntityId;
        row.CreatedBy = Current.SubjectId; row.Version = 1;
        db.Attach(row); db.Entry(row).State = state;
        Assert.Equal(state == EntityState.Deleted
            ? "Procurement records must be closed, cancelled or reversed; physical deletion is not allowed."
            : "InvoiceReplacementAppendOnly", (await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.SaveChangesAsync())).Message);
        await using var read = new ProcurementDbContext(options, Current);
        Assert.Empty(await read.InvoiceReplacementLinks.ToArrayAsync());
        Assert.Empty(await read.InvoiceReplacementCommandReceipts.ToArrayAsync());
        Assert.Empty(await read.AuditEvents.ToArrayAsync());
    }
}
