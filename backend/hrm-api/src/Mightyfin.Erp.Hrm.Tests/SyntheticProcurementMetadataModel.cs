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
