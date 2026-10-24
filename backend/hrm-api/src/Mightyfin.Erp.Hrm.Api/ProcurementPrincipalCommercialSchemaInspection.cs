using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Shared physical inspection for the three new commercial capabilities.
/// Each caller supplies its own owned entities and finite privilege contract.
/// Does not grant record access, run DDL or substitute for current Core authorization.</summary>
internal static class ProcurementPrincipalCommercialSchemaInspection
{
    internal sealed record Capture(string MigrationId, IReadOnlyDictionary<string, string> Checks,
        IReadOnlyDictionary<string, string> OwnershipConstraints,
        IReadOnlyDictionary<string, string>? ParentKeys = null);

    private static readonly Capture P30 = new(ProcurementPrincipalCommercialSchemaCapture.MigrationId,
        ProcurementPrincipalCommercialSchemaCapture.Checks,
        ProcurementPrincipalCommercialSchemaCapture.OwnershipConstraints);

    internal static bool TryResolveIndexPredicate(Type entityType, string? indexName,
        string? modelPredicate, out string? predicate)
    {
        predicate = null;
        if (entityType == typeof(SourcingPolicyIdentityEvidence) && indexName is not null
            && ProcurementSourcingPolicyIdentitySchemaReadiness.PartialPredicates.TryGetValue(indexName, out var captured))
        {
            var expectedModel = indexName switch
            {
                "ux_source_policy_identity_created" => "stage = 'Created'",
                "ux_source_policy_identity_published" => "stage = 'Published'",
                _ => null,
            };
            if (expectedModel is null || modelPredicate != expectedModel) return false;
            predicate = captured;
            return true;
        }
        if (entityType == typeof(ContractReviewPolicyIdentityRecord) && indexName is not null
            && ProcurementContractReviewSchemaCapture.ModelPredicates.TryGetValue(indexName, out var expectedModel))
        {
            if (expectedModel != modelPredicate
                || !ProcurementContractReviewSchemaCapture.PartialPredicates.TryGetValue(indexName, out var captured)) return false;
            predicate = captured;
            return true;
        }
        // PostgreSQL's canonical expression is an owning-schema contract.
        // Unknown filtered indexes need their own reviewed capture.
        return modelPredicate is null;
    }

    internal static async Task<bool> CheckAsync(ProcurementDbContext owner, Type[] types,
        string privilegeSql, IReadOnlySet<string> requiredOwnershipConstraints, CancellationToken ct, string? ownershipManifestSql = null)
    {
        try
        {
            if (!owner.Database.IsNpgsql()) return false;
            var orderHistory = types.Any(type => type == typeof(PurchaseOrder)
                || type == typeof(PurchaseOrderLine) || type == typeof(PrincipalOrderSourceBinding)
                || type == typeof(PrincipalOrderDraftReceipt)
                || ProcurementPrincipalOrderRevisionSchemaCapture.Types.Contains(type));
            var p33Applied = orderHistory && (await owner.Database.GetAppliedMigrationsAsync(ct))
                .Contains(ProcurementPrincipalOrderRevisionSchemaCapture.MigrationId, StringComparer.Ordinal);
            var capture = orderHistory ? ProcurementPrincipalOrderRevisionSchemaCapture.Select(p33Applied) : P30;
            return capture is not null && await CheckAsync(owner, types, privilegeSql,
                requiredOwnershipConstraints, capture, ct, ownershipManifestSql);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
    }

    internal static async Task<bool> CheckAsync(ProcurementDbContext owner, Type[] types,
        string privilegeSql, IReadOnlySet<string> requiredOwnershipConstraints, Capture capture,
        CancellationToken ct, string? ownershipManifestSql = null)
    {
        if (!owner.Database.IsNpgsql() || string.IsNullOrWhiteSpace(capture.MigrationId)
            || capture.Checks.Count == 0) return false;
        var opened = false;
        try
        {
            if (owner.Database.HasPendingModelChanges()
                || (await owner.Database.GetPendingMigrationsAsync(ct)).Any()
                || !(await owner.Database.GetAppliedMigrationsAsync(ct)).Contains(
                    capture.MigrationId, StringComparer.Ordinal)) return false;
            var connection = owner.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) { await owner.Database.OpenConnectionAsync(ct); opened = true; }
            await using var command = connection.CreateCommand();
            command.Transaction = owner.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandTimeout = 10;
            async Task<bool> True(string sql)
            {
                command.CommandText = sql;
                return await command.ExecuteScalarAsync(ct) is true;
            }
            if (!await True(privilegeSql)) return false;
            var model = owner.GetService<IDesignTimeModel>().Model;
            foreach (var type in types)
            {
                var entity = model.FindEntityType(type);
                if (entity is null) return false;
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                command.CommandText = SelectAllColumnsSql(entity, table);
                await using (var reader = await command.ExecuteReaderAsync(ct)) { }
                if (!await True(ColumnsSql(entity, table))) return false;
                foreach (var key in entity.GetKeys())
                    if (!await True(KeySql(table, key.Properties.Select(x => x.GetColumnName(table)!).ToArray(),
                        key.GetName()!, key.IsPrimaryKey()))) return false;
                foreach (var check in entity.GetCheckConstraints())
                {
                    if (!capture.Checks.TryGetValue(
                        table.Schema + "." + table.Name + "." + check.Name, out var expected)) return false;
                    command.CommandText = CheckSql(table, check.Name);
                    if (await command.ExecuteScalarAsync(ct) is not string actual || actual != expected) return false;
                }
                foreach (var foreign in entity.GetForeignKeys())
                {
                    var parent = StoreObjectIdentifier.Table(foreign.PrincipalEntityType.GetTableName()!,
                        foreign.PrincipalEntityType.GetSchema());
                    if (!await True(ForeignKeySql(table, parent,
                        foreign.Properties.Select(x => x.GetColumnName(table)!).ToArray(),
                        foreign.PrincipalKey.Properties.Select(x => x.GetColumnName(parent)!).ToArray(),
                        foreign.GetConstraintName()))) return false;
                }
                foreach (var index in entity.GetIndexes().Where(x => x.IsUnique))
                {
                    if (!TryResolveIndexPredicate(type, index.GetDatabaseName(), index.GetFilter(), out var predicate))
                        return false;
                    if (!await True(UniqueIndexSql(table, index.Properties.Select(x => x.GetColumnName(table)!).ToArray(),
                        index.GetDatabaseName(), predicate))) return false;
                }
            }
            if (capture.ParentKeys is { } parentKeys)
                foreach (var pair in parentKeys)
                {
                    var parts = pair.Key.Split('.');
                    if (parts.Length != 3) return false;
                    command.CommandText = "SELECT pg_get_constraintdef(oid,false) FROM pg_constraint WHERE conrelid="
                        + Literal(parts[0] + "." + parts[1]) + "::regclass AND conname=" + Literal(parts[2])
                        + " AND contype='u' AND convalidated AND conislocal AND NOT condeferrable AND NOT condeferred";
                    if (await command.ExecuteScalarAsync(ct) is not string actual || actual != pair.Value) return false;
                }
            foreach (var fullName in requiredOwnershipConstraints)
            {
                var parts = fullName.Split('.');
                if (parts.Length != 3 || !capture.OwnershipConstraints
                    .TryGetValue(fullName, out var expected)) return false;
                command.CommandText = "SELECT pg_get_constraintdef(oid,false) FROM pg_constraint WHERE conrelid="
                    + Literal(parts[0] + "." + parts[1]) + "::regclass AND conname=" + Literal(parts[2])
                    + " AND contype='f' AND convalidated AND conislocal";
                if (await command.ExecuteScalarAsync(ct) is not string actual || actual != expected) return false;
            }
            return ownershipManifestSql is null || await True(ownershipManifestSql);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
        finally { if (opened) await owner.Database.CloseConnectionAsync(); }
    }
}
