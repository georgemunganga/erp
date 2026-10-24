using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

public interface IProcurementOperationalPolicyIdentitySchemaReadiness
{
    Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct);
    Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct);
}

/// <summary>Read-only P31 discovery on the caller's actual owning connection and
/// transaction. Schema availability is not current Core identity, permission or
/// historical evidence validity. No alternate store, write probe or backfill.</summary>
public sealed class ProcurementOperationalPolicyIdentitySchemaReadiness : IProcurementOperationalPolicyIdentitySchemaReadiness
{
    internal const string MigrationId = "20261022052824_P21CanonicalGovernedProcurementFoundation";
    // Exact PostgreSQL17 catalog capture from the successful private P31 probe,
    // operational_policy_identity_20261022_042007, captured read-only 2026-10-22.
    // Probe evidence: artifacts/operational-policy-identity/private/20261022_042007/probe.log.
    internal const string CheckDefinition = "CHECK (((schema_version = 1) AND ((policy_key)::text = ANY ((ARRAY['RECEIVING'::character varying, 'INVOICE_MATCHING'::character varying])::text[])) AND (id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (policy_version_number > 0) AND (policy_record_version_at_action > 0) AND (actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (rules_sha256 IS NOT NULL) AND ((rules_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (evidence_sha256 IS NOT NULL) AND ((evidence_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND isfinite(action_at) AND (created_at = action_at) AND ((created_by)::text = (actor_principal_id)::text) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL) AND ((((stage)::text = 'Created'::text) AND (policy_record_version_at_action = 1) AND (created_evidence_id IS NULL) AND (created_evidence_stage IS NULL) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Submitted'::text) AND (policy_record_version_at_action > 1) AND (created_evidence_id IS NOT NULL) AND (created_evidence_id <> id) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Published'::text) AND (policy_record_version_at_action > 2) AND (created_evidence_id IS NOT NULL) AND (created_evidence_id <> id) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NOT NULL) AND (submitted_evidence_id <> id) AND (submitted_evidence_id <> created_evidence_id) AND (submitted_evidence_stage IS NOT NULL) AND ((submitted_evidence_stage)::text = 'Submitted'::text)))))";
    internal static readonly IReadOnlyDictionary<string,string> PartialPredicates = new Dictionary<string,string>
    {
        ["ux_operational_policy_identity_created"] = "((stage)::text = 'Created'::text)",
        ["ux_operational_policy_identity_published"] = "((stage)::text = 'Published'::text)",
    };
    internal static bool ManifestCaptured => CheckDefinition.Length > 0 && PartialPredicates.Count == 2
        && PartialPredicates.ContainsKey("ux_operational_policy_identity_created")
        && PartialPredicates.ContainsKey("ux_operational_policy_identity_published");
    private static readonly HashSet<string> ForeignNames = ["fk_operational_policy_identity_owner", "fk_operational_policy_identity_created", "fk_operational_policy_identity_submitted"];
    private static readonly HashSet<string> UniqueNames = ["ux_operational_policy_identity_revision", "ux_operational_policy_identity_created", "ux_operational_policy_identity_published"];

    public Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct) => CheckAsync(owner, false, ct);
    public Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct) => CheckAsync(owner, true, ct);

    private static async Task<bool> CheckAsync(ProcurementDbContext owner, bool capture, CancellationToken ct)
    {
        if (!ManifestCaptured || !owner.Database.IsNpgsql()) return false;
        var opened = false;
        try
        {
            if (owner.Database.HasPendingModelChanges()) return false;
            var pending = (await owner.Database.GetPendingMigrationsAsync(ct)).ToArray();
            var unitMigration = ProcurementInvoiceUnitReviewSchemaReadiness.MigrationId;
            // Keep the exact established two-key contract usable before the additive review
            // migration; no other pending migration/schema broadening is tolerated.
            if (pending.Any(x => unitMigration.Length == 0 || x != unitMigration)) return false;
            var applied = (await owner.Database.GetAppliedMigrationsAsync(ct)).ToArray();
            if (!applied.Contains(MigrationId, StringComparer.Ordinal)) return false;
            var connection = owner.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) { await owner.Database.OpenConnectionAsync(ct); opened = true; }
            await using var command = connection.CreateCommand();
            command.Transaction = owner.Database.CurrentTransaction?.GetDbTransaction(); command.CommandTimeout = 10;
            async Task<bool> True(string sql)
            { command.CommandText = sql; return await command.ExecuteScalarAsync(ct) is true; }
            if (!await True(PrivilegeSql(capture))) return false;
            var model = owner.GetService<IDesignTimeModel>().Model;
            var identity = model.FindEntityType(typeof(OperationalPolicyIdentityRecord));
            var policy = model.FindEntityType(typeof(PolicyVersion));
            if (identity is null || policy is null) return false;
            var table = StoreObjectIdentifier.Table(identity.GetTableName()!, identity.GetSchema());
            if (table.Schema != "procurement" || table.Name != "operational_policy_identity_evidence"
                || policy.GetSchema() != "procurement" || policy.GetTableName() != "policy_versions") return false;
            foreach (var entity in new[] { identity, policy })
            {
                var mapped = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                command.CommandText = SelectAllColumnsSql(entity, mapped);
                await using (var rows = await command.ExecuteReaderAsync(ct)) { }
                if (!await True(ColumnsSql(entity, mapped))) return false;
            }
            if (!await True("SELECT NOT EXISTS(SELECT 1 FROM pg_attrdef WHERE adrelid='procurement.operational_policy_identity_evidence'::regclass)")) return false;
            var checks = identity.GetCheckConstraints().ToArray();
            if (checks.Length != 1 || checks[0].Name != "ck_operational_policy_identity") return false;
            command.CommandText = CheckSql(table, checks[0].Name!);
            if (await command.ExecuteScalarAsync(ct) is not string actual) return false;
            var unitInstalled = unitMigration.Length > 0 && applied.Contains(unitMigration, StringComparer.Ordinal);
            var expectedCheck = ProcurementP32OperationalPolicyCatalog.SelectCheck(applied);
            if (unitInstalled && !ProcurementInvoiceUnitReviewSchemaReadiness.ManifestCaptured
                || expectedCheck is null || actual != expectedCheck) return false;
            var foreigns = identity.GetForeignKeys().ToArray();
            if (foreigns.Length != ForeignNames.Count || !foreigns.Select(x => x.GetConstraintName()!).ToHashSet(StringComparer.Ordinal).SetEquals(ForeignNames)) return false;
            foreach (var foreign in foreigns)
            {
                var parent = StoreObjectIdentifier.Table(foreign.PrincipalEntityType.GetTableName()!, foreign.PrincipalEntityType.GetSchema());
                if (!await True(ForeignKeySql(table, parent,
                    foreign.Properties.Select(x => x.GetColumnName(table)!).ToArray(),
                    foreign.PrincipalKey.Properties.Select(x => x.GetColumnName(parent)!).ToArray(), foreign.GetConstraintName()))) return false;
                if (!await True(KeySql(parent, foreign.PrincipalKey.Properties.Select(x => x.GetColumnName(parent)!).ToArray(),
                    foreign.PrincipalKey.GetName()!, foreign.PrincipalKey.IsPrimaryKey()))) return false;
            }
            foreach (var key in identity.GetKeys())
                if (!await True(KeySql(table, key.Properties.Select(x => x.GetColumnName(table)!).ToArray(), key.GetName()!, key.IsPrimaryKey()))) return false;
            var unique = identity.GetIndexes().Where(x => x.IsUnique).ToArray();
            if (unique.Length != UniqueNames.Count || !unique.Select(x => x.GetDatabaseName()!).ToHashSet(StringComparer.Ordinal).SetEquals(UniqueNames)) return false;
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

    internal static string PrivilegeSql(bool capture)
    {
        string[] append = ["operational_policy_identity_evidence", "audit_events", "outbox_events"];
        var clauses = append.Select(table =>
            "has_table_privilege(current_user," + Literal("procurement." + table) + ",'SELECT')"
            + (capture ? " AND has_table_privilege(current_user," + Literal("procurement." + table) + ",'INSERT')" : "")
            + " AND NOT has_table_privilege(current_user," + Literal("procurement." + table) + ",'UPDATE')"
            + " AND NOT has_any_column_privilege(current_user," + Literal("procurement." + table) + ",'UPDATE')"
            + " AND NOT has_table_privilege(current_user," + Literal("procurement." + table) + ",'DELETE')"
            + " AND NOT has_table_privilege(current_user," + Literal("procurement." + table) + ",'TRUNCATE')").ToList();
        clauses.Add("has_table_privilege(current_user,'procurement.policy_versions','SELECT')"
            + (capture ? " AND has_table_privilege(current_user,'procurement.policy_versions','INSERT') AND has_table_privilege(current_user,'procurement.policy_versions','UPDATE')" : "")
            + " AND NOT has_table_privilege(current_user,'procurement.policy_versions','DELETE') AND NOT has_table_privilege(current_user,'procurement.policy_versions','TRUNCATE')");
        clauses.Add("has_table_privilege(current_user,'procurement.__procurement_migrations','SELECT')");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))");
        clauses.Add("NOT has_schema_privilege(current_user,'procurement','CREATE')");
        clauses.Add(ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql);
        clauses.Add(ProcurementSpendingAuthorityIdentitySchemaReadiness.NoOwnershipSql);
        clauses.Add(NoOperationalPolicyOwnershipSql);
        return "SELECT " + string.Join(" AND ", clauses);
    }

    // Shared ownership guard covers audit/outbox, Core facts, schemas, database and
    // assumable elevated roles. Add the two owners specific to this policy boundary.
    internal const string NoOperationalPolicyOwnershipSql = """
        NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
          WHERE n.nspname='procurement' AND c.relname IN ('policy_versions','operational_policy_identity_evidence') AND (
            pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))
        """;
}
