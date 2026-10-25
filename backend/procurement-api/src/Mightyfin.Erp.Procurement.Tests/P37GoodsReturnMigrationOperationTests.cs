using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Mightyfin.Erp.Procurement.Infrastructure.Migrations;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class P37GoodsReturnMigrationOperationTests
{
    [Fact]
    public void Genuine_up_installs_owner_overlay_after_all_three_generated_tables()
    {
        var operations=new P37PrincipalGoodsReturnDrafts().UpOperations;
        var created=operations.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(new[]{"principal_goods_return_drafts","principal_goods_return_revisions",
            "principal_goods_return_receipts"}.Order(),created.Select(x=>x.Name).Order());
        Assert.Equal(8,created.Sum(x=>x.ForeignKeys.Count));
        var sql=Assert.IsType<SqlOperation>(operations.Last());
        Assert.Equal(P37PrincipalGoodsReturnOwnershipSql.AfterUpSql,sql.Sql);
        Assert.Single(operations.OfType<SqlOperation>());
        Assert.False(sql.SuppressTransaction);
    }

    [Fact]
    public void First_down_veto_precedes_owner_teardown_and_every_generated_drop()
    {
        var operations=new P37PrincipalGoodsReturnDrafts().DownOperations;
        Assert.Equal(5,operations.Count);
        var veto=Assert.IsType<SqlOperation>(operations[0]);
        Assert.Equal(P37PrincipalGoodsReturnRollbackGuards.BeforeDownSql,veto.Sql);
        Assert.False(veto.SuppressTransaction);
        Assert.Equal(P37PrincipalGoodsReturnOwnershipSql.BeforeDropSql,
            Assert.IsType<SqlOperation>(operations[1]).Sql);
        Assert.All(operations.Skip(2),x=>Assert.IsType<DropTableOperation>(x));
        Assert.Contains("ERRCODE = '55000'",veto.Sql,StringComparison.Ordinal);
        Assert.Contains("procurement.audit_events",veto.Sql,StringComparison.Ordinal);
        Assert.Contains("procurement.outbox_events",veto.Sql,StringComparison.Ordinal);
    }
}
