using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Mightyfin.Erp.Procurement.Infrastructure.Migrations;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class M3bPrincipalOrderDraftMigrationTests
{
    [Fact]
    public void Existing_orders_keep_legacy_schema_version_and_no_source_digest()
    {
        var operations = Invoke("Up");
        var version = Assert.Single(operations.OfType<AddColumnOperation>(), x =>
            x.Table == "purchase_orders" && x.Name == "principal_draft_schema_version");
        Assert.Equal("procurement", version.Schema);
        Assert.Equal(0, version.DefaultValue);
        Assert.False(version.IsNullable);
        var digest = Assert.Single(operations.OfType<AddColumnOperation>(), x =>
            x.Table == "purchase_orders" && x.Name == "original_direct_source_sha256");
        Assert.True(digest.IsNullable);
        Assert.Contains(operations.OfType<CreateTableOperation>(), x =>
            x.Name == "principal_order_source_bindings" && x.Schema == "procurement");
        Assert.Contains(operations.OfType<CreateTableOperation>(), x =>
            x.Name == "principal_order_draft_receipts" && x.Schema == "procurement");
    }

    [Fact]
    public void Downgrade_checks_all_owned_families_before_first_destructive_operation()
    {
        var operations = Invoke("Down");
        var guard = Assert.IsType<SqlOperation>(operations[0]);
        var sql = guard.Sql;
        foreach (var relation in new[] {
            "purchase_orders", "purchase_order_lines", "purchase_request_allocations",
            "principal_order_source_bindings", "principal_order_draft_receipts",
            "audit_events", "outbox_events"
        }) Assert.Contains("procurement." + relation, sql);
        Assert.Contains("principal_draft_schema_version = 2", sql);
        Assert.Contains("original_direct_source_sha256 IS NOT NULL", sql);
        Assert.Contains("PrincipalDraftCreate", sql);
        Assert.Contains("PrincipalDraftCancel", sql);
        Assert.Contains("purchase_order.internal_draft_reserved.v2", sql);
        Assert.Contains("purchase_order.internal_draft_cancelled.v2", sql);
        Assert.Contains("ERRCODE = '55000'", sql);
        Assert.DoesNotContain(operations.Skip(1), x => x is SqlOperation);
        Assert.Contains(operations.Skip(1), x => x is DropTableOperation);
    }

    private static IReadOnlyList<MigrationOperation> Invoke(string method)
    {
        var migration = new M3bPrincipalDirectOrderDraft();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(M3bPrincipalDirectOrderDraft)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations.ToArray();
    }
}
