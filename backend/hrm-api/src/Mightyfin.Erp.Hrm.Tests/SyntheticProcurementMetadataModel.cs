using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>
/// Only for metadata fixtures containing synthetic migration-history rows and no business tables.
/// SQLite cannot establish compatibility with the owning Npgsql snapshot. These explicit doubles
/// isolate metadata control flow; PostgreSQL schema/manifest verification is a separate gate.
/// </summary>
internal static class SyntheticProcurementMetadataModel
{
    public static void Configure(DbContextOptionsBuilder options, string scenario)
    {
        if (scenario == "pending-model") options.ReplaceService<IMigrationsModelDiffer, Pending>();
        else if (scenario == "unknown-model") options.ReplaceService<IMigrationsModelDiffer, Unknown>();
        else options.ReplaceService<IMigrationsModelDiffer, Unchanged>();
    }

    /// <summary>
    /// Generate only SQLite's history DDL with its normal provider services. The fixture's
    /// synthetic differ must remain incapable of generating migration operations.
    /// The caller owns the already-open connection; no business schema is created.
    /// </summary>
    public static async Task CreateSqliteHistoryAsync(DbContext fixture, CancellationToken ct = default)
    {
        if (!fixture.Database.IsSqlite()
            || fixture.Database.GetDbConnection() is not SqliteConnection connection
            || connection.State != ConnectionState.Open)
            throw new InvalidOperationException("Synthetic metadata history setup requires an existing open SQLite fixture connection.");

        // Do not copy fixture options: those intentionally replace IMigrationsModelDiffer.
        // UseSqlite(DbConnection) leaves connection ownership with the caller.
        var options = new DbContextOptionsBuilder<DbContext>().UseSqlite(connection).Options;
        await using var normalProvider = new DbContext(options);
        var history = normalProvider.GetService<IHistoryRepository>();
        var ddl = history.GetCreateIfNotExistsScript();
        await fixture.Database.ExecuteSqlRawAsync(ddl, ct);
    }

    public class Unchanged : IMigrationsModelDiffer
    {
        public virtual bool HasDifferences(IRelationalModel? source, IRelationalModel? target) => false;
        public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
            => throw new NotSupportedException("Synthetic metadata fixture cannot generate migrations.");
    }
    public sealed class Pending : Unchanged
    {
        public override bool HasDifferences(IRelationalModel? source, IRelationalModel? target) => true;
    }
    public sealed class Unknown : Unchanged
    {
        public override bool HasDifferences(IRelationalModel? source, IRelationalModel? target)
            => throw new InvalidOperationException("Synthetic model comparison unavailable.");
    }
}
