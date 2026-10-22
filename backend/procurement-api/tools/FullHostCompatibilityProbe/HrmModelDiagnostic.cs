using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

/// <summary>Source-model operations only. Never generate/execute DDL or serialize an EF
/// operation wholesale: defaults, seed values, raw SQL and annotations may contain data.</summary>
internal static class HrmModelDiagnostic
{
    internal static async Task<object> DescribeAsync(HrmDbContext db, bool actualPendingModelChanges)
    {
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot;
        // Same owning Npgsql initializer/differ/design model used by the real migrator's
        // pending-model check. Do not compare raw/uninitialized snapshot relational models.
        var source = snapshot is null ? null : db.GetService<IModelRuntimeInitializer>()
            .Initialize(snapshot.Model, designTime: true).GetRelationalModel();
        var target = db.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        var differ = db.GetService<IMigrationsModelDiffer>();
        var pending = differ.HasDifferences(source, target);
        if (pending != actualPendingModelChanges)
            throw new InvalidOperationException("Owner model diagnostic disagrees with the real pending-model guard.");
        var operations = differ.GetDifferences(source, target);
        var catalog = await HrmModelCatalog.ReadAsync(db);
        var compared = operations.Select(operation => new
        {
            sourceOperation = Summary(operation),
            actualCatalog = catalog.Compare(operation, target),
        }).ToArray();
        var history = (await db.Database.GetAppliedMigrationsAsync()).ToHashSet(StringComparer.Ordinal);
        return new
        {
            provider = db.Database.ProviderName,
            snapshotType = snapshot?.GetType().FullName,
            snapshotAvailable = snapshot is not null,
            pendingModelChanges = pending,
            matchesActualPendingModelGuard = true,
            operationCount = operations.Count,
            operationCounts = operations.GroupBy(x => x.GetType().Name).OrderBy(x => x.Key)
                .ToDictionary(x => x.Key, x => x.Count()),
            operations = compared,
            expectedHandwrittenMigrations = new[] { "20261014090000_LoanOpeningBalances", "20261016090000_M55BrandingFont" }
                .Select(id => new { migrationId = id, applied = history.Contains(id) }).ToArray(),
            catalogComparisonScope = "Required model columns/types/nullability, named primary/unique/foreign keys and indexes; no defaults, row data, SQL bodies, sequence state or complete manual invariants proof.",
            valuesAndDefaultsExcluded = true,
            migrationGenerated = false,
        };
    }

    private static Dictionary<string, object?> Summary(MigrationOperation operation)
    {
        var result = new Dictionary<string, object?> { ["kind"] = operation.GetType().Name };
        // Explicit metadata allowlist. Never add DefaultValue, DefaultValueSql, seed
        // Values, annotation values or generic serializer/reflection of all properties.
        foreach (var name in new[] { "Schema", "Table", "Name", "NewSchema", "NewName", "ColumnType", "MaxLength",
            "Precision", "Scale", "IsNullable", "IsUnicode", "IsFixedLength", "PrincipalSchema", "PrincipalTable" })
        {
            var property = operation.GetType().GetProperty(name);
            if (property is null) continue;
            var value = property.GetValue(operation);
            if (value is null or string or int or bool) result[name] = value;
        }
        foreach (var name in new[] { "Columns", "PrincipalColumns" })
        {
            if (operation.GetType().GetProperty(name)?.GetValue(operation) is string[] names)
                result[name] = names;
        }
        if (operation is ColumnOperation column) result["ClrType"] = column.ClrType.FullName;
        if (operation is AlterColumnOperation alter) result["oldColumn"] = Summary(alter.OldColumn);
        if (operation is CreateTableOperation create)
        {
            result["columns"] = create.Columns.Select(Summary).ToArray();
            result["foreignKeys"] = create.ForeignKeys.Select(Summary).ToArray();
            result["primaryKey"] = create.PrimaryKey is null ? null : Summary(create.PrimaryKey);
            result["uniqueConstraints"] = create.UniqueConstraints.Select(Summary).ToArray();
            result["checkConstraints"] = create.CheckConstraints.Select(Summary).ToArray();
        }
        // SQL text itself is never printed. Its hash identifies a source operation only.
        if (operation is SqlOperation sql)
            result["sqlSha256"] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql.Sql)));
        return result;
    }
}
