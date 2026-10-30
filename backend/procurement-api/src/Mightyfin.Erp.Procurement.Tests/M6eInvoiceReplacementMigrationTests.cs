using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Mightyfin.Erp.Procurement.Infrastructure.Migrations;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class M6eInvoiceReplacementMigrationTests
{
    [Fact]
    public void Up_adds_only_two_owned_evidence_tables_and_their_indexes()
    {
        var operations = Invoke("Up");
        var tables = operations.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(new[] { "invoice_replacement_links", "invoice_replacement_command_receipts" },
            tables.Select(x => x.Name));
        Assert.All(tables, x => Assert.Equal("procurement", x.Schema));
        Assert.All(operations, x => Assert.True(x is CreateTableOperation or CreateIndexOperation));
        Assert.All(operations.OfType<CreateIndexOperation>(), x =>
            Assert.Contains(x.Table, tables.Select(t => t.Name)));
        Assert.All(tables.SelectMany(x => x.ForeignKeys), x =>
            Assert.Equal(ReferentialAction.Restrict, x.OnDelete));
    }

    [Fact]
    public void Down_checks_all_owned_rows_and_markers_before_any_drop()
    {
        var operations = Invoke("Down");
        var guard = Assert.IsType<SqlOperation>(operations[0]);
        foreach (var table in new[] { "invoice_replacement_links", "invoice_replacement_command_receipts", "audit_events", "outbox_events" })
            Assert.Contains("procurement." + table, guard.Sql);
        Assert.Contains("IN ACCESS EXCLUSIVE MODE", guard.Sql);
        Assert.Contains("InvoiceReplacementLink", guard.Sql);
        Assert.Contains("InvoiceReplacementCommandReceipt", guard.Sql);
        Assert.Contains("InvoiceReplacementRecorded", guard.Sql);
        Assert.Contains("supplier_invoice.replaced.v1", guard.Sql);
        Assert.Contains("ERRCODE='55000'", guard.Sql);
        Assert.Equal(new[] { "invoice_replacement_command_receipts", "invoice_replacement_links" },
            operations.Skip(1).Select(x => Assert.IsType<DropTableOperation>(x).Name));
    }

    private static IReadOnlyList<MigrationOperation> Invoke(string method)
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(M6eControlledInvoiceReplacement).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(new M6eControlledInvoiceReplacement(), [builder]);
        return builder.Operations.ToArray();
    }
}
