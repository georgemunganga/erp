using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Mightyfin.Erp.Procurement.Infrastructure.Migrations;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class M5aOrderMigrationSafetyTests
{
    [Fact]
    public void Downgrade_checks_and_locks_every_new_evidence_table_before_any_ddl()
    {
        var migration = new M5aPrincipalOrderGovernance();
        var created = migration.UpOperations.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(8, created.Length);
        var first = Assert.IsType<SqlOperation>(migration.DownOperations[0]);
        Assert.False(first.SuppressTransaction);
        var beforeCheck = first.Sql[..first.Sql.IndexOf("DO $", StringComparison.Ordinal)];
        foreach (var table in created)
        {
            Assert.Contains(table.Schema + "." + table.Name, beforeCheck);
            Assert.Contains("EXISTS (SELECT 1 FROM " + table.Schema + "." + table.Name + ")", first.Sql);
        }
        Assert.Contains("IN ACCESS EXCLUSIVE MODE", beforeCheck);
        Assert.Contains("ERRCODE='55000'", first.Sql);
        Assert.Contains(migration.DownOperations.Skip(1), x => x is DropTableOperation);
    }

}
