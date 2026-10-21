using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Unregistered P29 candidate: read-only discovery on the policy command's actual
/// owning connection/transaction. It supplies no identity, publication or configuration-version authority.</summary>
public sealed class ProcurementSourcingPolicyIdentitySchemaReadiness : IProcurementSourcingPolicyIdentitySchemaReadiness
{
    internal const string MigrationId = "20261021084158_P29SourcingPolicyIdentity";
    // Exact actual PostgreSQL17 outputs, procurement-p29-postgres-20261021_0847/
    // p29-checks.json and p29-partial-indexes.json. Never relax grouping/casts/literal case.
    internal const string CheckDefinition = "CHECK (((schema_version = 1) AND (id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (policy_version_number > 0) AND (policy_record_version_at_action > 0) AND (actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (length(btrim((actor_principal_id)::text)) > 0) AND (rules_sha256 IS NOT NULL) AND ((rules_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (evidence_sha256 IS NOT NULL) AND ((evidence_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND isfinite(action_at) AND ((((stage)::text = 'Created'::text) AND (policy_record_version_at_action = 1) AND (created_evidence_id IS NULL) AND (created_evidence_stage IS NULL) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Submitted'::text) AND (created_evidence_id IS NOT NULL) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Published'::text) AND (created_evidence_id IS NOT NULL) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NOT NULL) AND (submitted_evidence_stage IS NOT NULL) AND ((submitted_evidence_stage)::text = 'Submitted'::text)))))";
    internal static readonly IReadOnlyDictionary<string, string> PartialPredicates = new Dictionary<string, string>
    {
        ["ux_source_policy_identity_created"] = "((stage)::text = 'Created'::text)",
        ["ux_source_policy_identity_published"] = "((stage)::text = 'Published'::text)",
    };
    private static readonly HashSet<string> ForeignNames = ["fk_source_policy_identity_owner",
        "fk_source_policy_identity_created", "fk_source_policy_identity_submitted"];

    public async Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct)
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
            if (!await True(PrivilegeSql)) return false;
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
            if (await command.ExecuteScalarAsync(ct) is not string actual || actual != CheckDefinition) return false;
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
}
