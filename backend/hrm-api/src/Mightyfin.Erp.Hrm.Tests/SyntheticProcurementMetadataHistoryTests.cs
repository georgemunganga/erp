using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>SQLite-only fixture plumbing; never a PostgreSQL schema compatibility proof.</summary>
public sealed class SyntheticProcurementMetadataHistoryTests
{
    [Theory]
    [InlineData("synthetic-history-only")]
    [InlineData("pending-model")]
    [InlineData("unknown-model")]
    public async Task Normal_provider_history_setup_works_without_weakening_synthetic_model_guards(string scenario)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DbContext>().UseSqlite(connection);
        SyntheticProcurementMetadataModel.Configure(options, scenario);
        await using var fixture = new DbContext(options.Options);
        var differ = fixture.GetService<IMigrationsModelDiffer>();
        Assert.Throws<NotSupportedException>(() => differ.GetDifferences(null, null));

        await SyntheticProcurementMetadataModel.CreateSqliteHistoryAsync(fixture);
        await SyntheticProcurementMetadataModel.CreateSqliteHistoryAsync(fixture);
        var history = fixture.GetService<IHistoryRepository>();
        await fixture.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow("SYNTHETIC_HISTORY_ONLY", "10.0.11")));
        Assert.Equal("SYNTHETIC_HISTORY_ONLY", Assert.Single(await fixture.Database.GetAppliedMigrationsAsync()));
        Assert.Same(connection, fixture.Database.GetDbConnection());
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
        Assert.Throws<NotSupportedException>(() => differ.GetDifferences(null, null));
        if (scenario == "unknown-model")
            Assert.Throws<InvalidOperationException>(() => differ.HasDifferences(null, null));
        else
            Assert.Equal(scenario == "pending-model", differ.HasDifferences(null, null));
        await using var count = connection.CreateCommand();
        count.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
        Assert.Equal(1L, Convert.ToInt64(await count.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task History_helper_rejects_non_Sqlite_provider()
    {
        await using var fixture = new DbContext(new DbContextOptionsBuilder<DbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SyntheticProcurementMetadataModel.CreateSqliteHistoryAsync(fixture));
        Assert.Contains("open SQLite", error.Message);
    }

    [Fact]
    public async Task History_helper_never_opens_a_closed_fixture_connection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var fixture = new DbContext(new DbContextOptionsBuilder<DbContext>().UseSqlite(connection).Options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SyntheticProcurementMetadataModel.CreateSqliteHistoryAsync(fixture));
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
    }
}
