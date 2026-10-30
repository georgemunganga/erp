using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Mightyfin.Erp.Procurement.Infrastructure.Migrations;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class M7aQuotationMigrationTests
{
    [Fact]
    public void Genuine_Up_preserves_old_foreign_keys_and_adds_two_scoped_sidecars_and_sixteen_columns()
    {
        var operations = Invoke("Up");
        var tables = operations.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(new[] { "sourcing_manual_contacts", "sourcing_manual_quote_versions" }, tables.Select(x => x.Name));
        Assert.All(tables, x => Assert.Equal("procurement", x.Schema));
        Assert.All(tables.SelectMany(x => x.ForeignKeys), x => Assert.Equal(ReferentialAction.Restrict, x.OnDelete));
        Assert.Equal(16, operations.OfType<AddColumnOperation>().Count());
        Assert.DoesNotContain(operations, x => x is DropForeignKeyOperation or DropColumnOperation or DropTableOperation or AlterColumnOperation);
        Assert.All(operations.OfType<AddColumnOperation>(), x => Assert.Contains(x.Table, new[] { "supplier_bids", "supplier_bid_lines" }));
    }

    [Fact]
    public void First_Down_operation_locks_before_dropping_and_vetoes_rows_typed_values_and_retained_markers()
    {
        var operations = Invoke("Down"); var guard = Assert.IsType<SqlOperation>(operations[0]);
        foreach (var table in new[] { "sourcing_manual_contacts", "sourcing_manual_quote_versions", "supplier_bids",
            "supplier_bid_lines", "audit_events", "outbox_events" }) Assert.Contains("procurement." + table, guard.Sql);
        Assert.Contains("IN ACCESS EXCLUSIVE MODE", guard.Sql); Assert.Contains("ERRCODE='55000'", guard.Sql);
        Assert.Contains("is_active IS DISTINCT FROM TRUE", guard.Sql); Assert.Contains("principal_capture_version<>0", guard.Sql);
        foreach (var field in new[] { "supplier_quote_reference", "quote_date", "offer_validity_kind", "valid_until",
            "supplier_offered_payment_code", "supplier_offered_payment_text", "warranty_text", "delivery_terms_text",
            "delivery_date", "discount_kind", "discount_value", "tax_treatment", "supplier_offered_tax_code" })
            Assert.Contains(field, guard.Sql);
        foreach (var marker in new[] { "SourcingManualContact", "SourcingManualQuoteVersion", "OutsideContactRecorded",
            "QuotationDraftCreate", "QuotationDraftRevise", "QuotationDraftCancel", "sourcing.QuotationDraftCancel.v3" })
            Assert.Contains(marker, guard.Sql);
        Assert.DoesNotContain("source_channel=", guard.Sql); Assert.DoesNotContain("unit_price", guard.Sql);
        Assert.DoesNotContain("correction_reason", guard.Sql); Assert.DoesNotContain("received_at", guard.Sql);
        Assert.True(operations.Skip(1).Any(x => x is DropTableOperation));
    }

    private static IReadOnlyList<MigrationOperation> Invoke(string method)
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(M7aPrincipalManualQuotations).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(new M7aPrincipalManualQuotations(), [builder]);
        return builder.Operations.ToArray();
    }
}
