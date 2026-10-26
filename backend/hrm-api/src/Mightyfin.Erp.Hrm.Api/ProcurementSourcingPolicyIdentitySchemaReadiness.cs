using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Normal-host M4 readiness: read-only discovery on the policy command's actual
/// owning connection/transaction. It supplies no identity, publication or configuration-version authority.</summary>
public sealed class ProcurementSourcingPolicyIdentitySchemaReadiness : IProcurementSourcingPolicyIdentitySchemaReadiness
{
    // The mixed held P30 migration is deliberately absent. This exact M4 ID
    // and its physical CHECK are captured from genuine private PostgreSQL.
    internal const string MigrationId = M4PrincipalRfqPhysicalChecks.MigrationId;
    internal static string CheckDefinition => M4PrincipalRfqPhysicalChecks.Native["ck_source_policy_identity"];
    internal static readonly IReadOnlyDictionary<string, string> PartialPredicates = new Dictionary<string, string>
    {
        ["ux_source_policy_identity_created"] = "((stage)::text = 'Created'::text)",
        ["ux_source_policy_identity_published"] = "((stage)::text = 'Published'::text)",
    };
    private static readonly HashSet<string> ForeignNames = ["fk_source_policy_identity_owner",
        "fk_source_policy_identity_created", "fk_source_policy_identity_submitted"];

    public Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct) =>
        CheckAsync(owner, true, ct);

    public Task<bool> IsReadReadyAsync(ProcurementDbContext owner, CancellationToken ct) =>
        CheckAsync(owner, false, ct);

    private async Task<bool> CheckAsync(ProcurementDbContext owner, bool write, CancellationToken ct)
    {
        if (!owner.Database.IsNpgsql()) return false;
        var opened = false;
        try
        {
            if (owner.Database.HasPendingModelChanges() || (await owner.Database.GetPendingMigrationsAsync(ct)).Any()) return false;
            var applied = await owner.Database.GetAppliedMigrationsAsync(ct);
            if (!applied.Contains(MigrationId, StringComparer.Ordinal)) return false;
            var connection = owner.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) { await owner.Database.OpenConnectionAsync(ct); opened = true; }
            await using var command = connection.CreateCommand();
            command.Transaction = owner.Database.CurrentTransaction?.GetDbTransaction(); command.CommandTimeout = 10;
            async Task<bool> True(string sql)
            { command.CommandText = sql; return await command.ExecuteScalarAsync(ct) is true; }
            if (!await True(write ? PrivilegeSql : ReadPrivilegeSql)) return false;
            var model = owner.GetService<IDesignTimeModel>().Model;
            var identity = model.FindEntityType(typeof(SourcingPolicyIdentityEvidence));
            var policy = model.FindEntityType(typeof(PolicyVersion));
            if (identity is null || policy is null) return false;
            var table = StoreObjectIdentifier.Table(identity.GetTableName()!, identity.GetSchema());
            if (table.Schema != "procurement" || table.Name != "sourcing_policy_identity_evidence") return false;
            // Materialize no rows, but require every retained field to be readable. No alternate store.
            foreach (var entity in new[] { identity, policy })
            {
                var ownedTable = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                command.CommandText = SelectAllColumnsSql(entity, ownedTable);
                await using (var result = await command.ExecuteReaderAsync(ct)) { }
                if (!await True(ColumnsSql(entity, ownedTable))) return false;
            }
            if (!await True("SELECT NOT EXISTS(SELECT 1 FROM pg_attrdef WHERE adrelid='procurement.sourcing_policy_identity_evidence'::regclass)")) return false;
            var checks = identity.GetCheckConstraints().ToArray();
            if (checks.Length != 1 || checks[0].Name != "ck_source_policy_identity") return false;
            command.CommandText = CheckSql(table, checks[0].Name);
            if (await command.ExecuteScalarAsync(ct) is not string actual || !M4PrincipalRfqPhysicalChecks.Matches("ck_source_policy_identity", actual)) return false;
            var foreigns = identity.GetForeignKeys().ToArray();
            if (foreigns.Length != ForeignNames.Count || !foreigns.Select(x => x.GetConstraintName()!).ToHashSet(StringComparer.Ordinal).SetEquals(ForeignNames)) return false;
            foreach (var foreign in foreigns)
            {
                var parent = StoreObjectIdentifier.Table(foreign.PrincipalEntityType.GetTableName()!, foreign.PrincipalEntityType.GetSchema());
                if (!await True(ForeignKeySql(table, parent,
                    foreign.Properties.Select(x => x.GetColumnName(table)!).ToArray(),
                    foreign.PrincipalKey.Properties.Select(x => x.GetColumnName(parent)!).ToArray(), foreign.GetConstraintName()))) return false;
                // Exact named stable principal key, including the policy version's existing key.
                if (!await True(KeySql(parent, foreign.PrincipalKey.Properties.Select(x => x.GetColumnName(parent)!).ToArray(),
                    foreign.PrincipalKey.GetName()!, foreign.PrincipalKey.IsPrimaryKey()))) return false;
            }
            foreach (var key in identity.GetKeys())
                if (!await True(KeySql(table, key.Properties.Select(x => x.GetColumnName(table)!).ToArray(), key.GetName()!, key.IsPrimaryKey()))) return false;
            var unique = identity.GetIndexes().Where(x => x.IsUnique).ToArray();
            if (unique.Length != 3 || !unique.Select(x => x.GetDatabaseName()).ToHashSet(StringComparer.Ordinal)
                .SetEquals(["ux_source_policy_identity_revision", "ux_source_policy_identity_created", "ux_source_policy_identity_published"])) return false;
            foreach (var index in unique)
            {
                var name = index.GetDatabaseName()!;
                var predicate = PartialPredicates.GetValueOrDefault(name);
                if ((index.GetFilter() is null) != (predicate is null)) return false;
                if (!await True(UniqueIndexSql(table, index.Properties.Select(x => x.GetColumnName(table)!).ToArray(), name, predicate))) return false;
            }
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
        finally { if (opened) await owner.Database.CloseConnectionAsync(); }
    }

    internal static string PrivilegeSql
    {
        get
        {
            string[] append = ["sourcing_policy_identity_evidence", "audit_events", "outbox_events"];
            var clauses = append.Select(table => "has_table_privilege(current_user," + Literal("procurement." + table) + ",'SELECT') AND has_table_privilege(current_user," + Literal("procurement." + table) + ",'INSERT') AND NOT has_table_privilege(current_user," + Literal("procurement." + table) + ",'UPDATE') AND NOT has_table_privilege(current_user," + Literal("procurement." + table) + ",'DELETE') AND NOT has_any_column_privilege(current_user," + Literal("procurement." + table) + ",'UPDATE') AND NOT has_table_privilege(current_user," + Literal("procurement." + table) + ",'TRUNCATE')").ToList();
            clauses.Add("has_table_privilege(current_user,'procurement.policy_versions','SELECT') AND has_table_privilege(current_user,'procurement.policy_versions','INSERT') AND has_table_privilege(current_user,'procurement.policy_versions','UPDATE') AND NOT has_table_privilege(current_user,'procurement.policy_versions','DELETE') AND NOT has_table_privilege(current_user,'procurement.policy_versions','TRUNCATE')");
            clauses.Add("has_table_privilege(current_user,'procurement.__procurement_migrations','SELECT')");
            clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))");
            clauses.Add("NOT has_schema_privilege(current_user,'procurement','CREATE')");
            return "SELECT " + string.Join(" AND ", clauses);
        }
    }

    // The Execution role reads policy identity while opening/deciding an RFQ. Its
    // own append-only audit/outbox rights remain required; policy authoring stays
    // on the Record connection and is explicitly absent here.
    internal static string ReadPrivilegeSql
    {
        get
        {
            string[] append = ["audit_events", "outbox_events"];
            var clauses = append.Select(table => "has_table_privilege(current_user," + Literal("procurement." + table)
                + ",'SELECT') AND has_table_privilege(current_user," + Literal("procurement." + table)
                + ",'INSERT') AND NOT has_any_column_privilege(current_user," + Literal("procurement." + table)
                + ",'UPDATE') AND NOT has_table_privilege(current_user," + Literal("procurement." + table)
                + ",'DELETE') AND NOT has_table_privilege(current_user," + Literal("procurement." + table)
                + ",'TRUNCATE')").ToList();
            clauses.Add("has_table_privilege(current_user,'procurement.sourcing_policy_identity_evidence','SELECT')"
                + " AND NOT has_any_column_privilege(current_user,'procurement.sourcing_policy_identity_evidence','INSERT')"
                + " AND NOT has_any_column_privilege(current_user,'procurement.sourcing_policy_identity_evidence','UPDATE')"
                + " AND NOT has_table_privilege(current_user,'procurement.sourcing_policy_identity_evidence','DELETE')"
                + " AND NOT has_table_privilege(current_user,'procurement.sourcing_policy_identity_evidence','TRUNCATE')");
            clauses.Add("has_table_privilege(current_user,'procurement.policy_versions','SELECT')"
                + " AND NOT has_any_column_privilege(current_user,'procurement.policy_versions','INSERT')"
                + " AND NOT has_any_column_privilege(current_user,'procurement.policy_versions','UPDATE')"
                + " AND NOT has_table_privilege(current_user,'procurement.policy_versions','DELETE')"
                + " AND NOT has_table_privilege(current_user,'procurement.policy_versions','TRUNCATE')");
            clauses.Add("has_table_privilege(current_user,'procurement.__procurement_migrations','SELECT')");
            clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))");
            clauses.Add("NOT has_schema_privilege(current_user,'procurement','CREATE')");
            return "SELECT " + string.Join(" AND ", clauses);
        }
    }
}
