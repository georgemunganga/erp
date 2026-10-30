using System.Data;
using Microsoft.Data.Sqlite;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreCurrencyPrecisionBranchReaderTests
{
    [Fact]
    public async Task Branch_check_uses_caller_serializable_connection_and_exact_tenant_company_branch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var setup = connection.CreateCommand())
        {
            setup.CommandText = "ATTACH DATABASE ':memory:' AS hrm; " +
                "CREATE TABLE hrm.work_locations(tenant_id TEXT NOT NULL,legal_entity_id TEXT NOT NULL," +
                "id TEXT NOT NULL,is_archived INTEGER NOT NULL)";
            await setup.ExecuteNonQueryAsync();
        }
        var company = Guid.NewGuid(); var branch = Guid.NewGuid();
        await using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        await using (var seed = connection.CreateCommand())
        {
            seed.Transaction = transaction;
            seed.CommandText = "INSERT INTO hrm.work_locations VALUES (@tenant,@company,@branch,0)";
            seed.Parameters.AddWithValue("tenant", "tenant-a");
            seed.Parameters.AddWithValue("company", company);
            seed.Parameters.AddWithValue("branch", branch);
            await seed.ExecuteNonQueryAsync();
        }
        var observed = DateTimeOffset.UtcNow;
        Assert.True(await CoreCurrencyPrecisionReader.BranchAvailableForCompanyAsync(connection,
            transaction, "tenant-a", company, branch, observed, default));
        Assert.False(await CoreCurrencyPrecisionReader.BranchAvailableForCompanyAsync(connection,
            transaction, "tenant-b", company, branch, observed, default));
        Assert.False(await CoreCurrencyPrecisionReader.BranchAvailableForCompanyAsync(connection,
            transaction, "tenant-a", Guid.NewGuid(), branch, observed, default));
        Assert.False(await CoreCurrencyPrecisionReader.BranchAvailableForCompanyAsync(connection,
            transaction, "tenant-a", company, Guid.NewGuid(), observed, default));
        await using (var archive = connection.CreateCommand())
        {
            archive.Transaction = transaction;
            archive.CommandText = "UPDATE hrm.work_locations SET is_archived=1 WHERE id=@branch";
            archive.Parameters.AddWithValue("branch", branch);
            await archive.ExecuteNonQueryAsync();
        }
        Assert.False(await CoreCurrencyPrecisionReader.BranchAvailableForCompanyAsync(connection,
            transaction, "tenant-a", company, branch, observed, default));
    }

    [Fact]
    public async Task Branch_check_rejects_missing_or_different_transaction_boundary()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var other = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(); await other.OpenAsync();
        await using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CoreCurrencyPrecisionReader.BranchAvailableForCompanyAsync(other, transaction,
                "tenant-a", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CoreCurrencyPrecisionReader.BranchAvailableForCompanyAsync(connection, transaction,
                "tenant-a", Guid.NewGuid(), Guid.Empty, DateTimeOffset.UtcNow, default));
    }
}
