using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

public interface IProcurementSpendingAuthorityIdentitySchemaReadiness
{
    Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct);
    Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct);
}

/// <summary>Read-only P30 discovery on the caller's actual owning connection and
/// transaction. Schema availability is not current Core identity, permission or
/// historical evidence validity. No alternate store, write probe or backfill.</summary>
public sealed class ProcurementSpendingAuthorityIdentitySchemaReadiness : IProcurementSpendingAuthorityIdentitySchemaReadiness
{
    internal const string MigrationId = "20261022052824_P21CanonicalGovernedProcurementFoundation";
    // Exact PostgreSQL17 catalog capture, read-only against the rehearsed disposable
    // authority_identity_20261021_120345 database, 2026-10-22. Original outputs retained
    // under artifacts/authority-identity/readiness-manifest-20261022/.
    internal const string CheckDefinition = "CHECK (((schema_version = 1) AND (id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (grant_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (authority_version > 0) AND (grant_record_version_at_action > 0) AND (actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (recipient_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (length(btrim((authority_reference)::text)) > 0) AND (length(btrim((permission)::text)) > 0) AND (length(btrim((action)::text)) > 0) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((recipient_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((recipient_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((configuration_owner_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((configuration_owner_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (business_fingerprint IS NOT NULL) AND ((business_fingerprint)::text ~ '^[0-9a-fA-F]{64}$'::text) AND (evidence_sha256 IS NOT NULL) AND ((evidence_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND isfinite(action_at) AND (created_at = action_at) AND ((created_by)::text = (actor_principal_id)::text) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL) AND ((((stage)::text = 'Created'::text) AND (grant_record_version_at_action = 1) AND (created_evidence_id IS NULL) AND (created_evidence_stage IS NULL) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Submitted'::text) AND (created_evidence_id IS NOT NULL) AND (created_evidence_id <> id) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Activated'::text) AND (created_evidence_id IS NOT NULL) AND (created_evidence_id <> id) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NOT NULL) AND (submitted_evidence_id <> id) AND (submitted_evidence_id <> created_evidence_id) AND (submitted_evidence_stage IS NOT NULL) AND ((submitted_evidence_stage)::text = 'Submitted'::text)))))";
    internal static readonly IReadOnlyDictionary<string,string> PartialPredicates = new Dictionary<string,string>
    {
        ["ux_authority_identity_origin"] = "((stage)::text = 'Created'::text)",
        ["ux_authority_identity_activation"] = "((stage)::text = 'Activated'::text)",
    };
    private static readonly HashSet<string> ForeignNames = ["fk_authority_identity_grant", "fk_authority_identity_created", "fk_authority_identity_submitted"];
    private static readonly HashSet<string> UniqueNames = ["ux_authority_identity_action_revision", "ux_authority_identity_origin", "ux_authority_identity_activation"];

    public Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct) => CheckAsync(owner, false, ct);
    public Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct) => CheckAsync(owner, true, ct);

    private static async Task<bool> CheckAsync(ProcurementDbContext owner, bool capture, CancellationToken ct)
    {
        if (!owner.Database.IsNpgsql()) return false;
        var opened = false;
        try
        {
            if (ProcurementCompiledModelParity.HasPendingModelChanges(owner) || (await owner.Database.GetPendingMigrationsAsync(ct)).Any()) return false;
            if (!(await owner.Database.GetAppliedMigrationsAsync(ct)).Contains(MigrationId, StringComparer.Ordinal)) return false;
            var connection = owner.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) { await owner.Database.OpenConnectionAsync(ct); opened = true; }
            await using var command = connection.CreateCommand();
            command.Transaction = owner.Database.CurrentTransaction?.GetDbTransaction(); command.CommandTimeout = 10;
            async Task<bool> True(string sql)
            { command.CommandText = sql; return await command.ExecuteScalarAsync(ct) is true; }
            if (!await True(PrivilegeSql(capture))) return false;
            var model = owner.GetService<IDesignTimeModel>().Model;
            var identity = model.FindEntityType(typeof(SpendingAuthorityIdentityRecord));
            var grant = model.FindEntityType(typeof(ProcurementSpendingAuthorityGrant));
            var category = model.FindEntityType(typeof(ProcurementSpendingAuthorityCategory));
            if (identity is null || grant is null || category is null) return false;
            var table = StoreObjectIdentifier.Table(identity.GetTableName()!, identity.GetSchema());
            if (table.Schema != "procurement" || table.Name != "spending_authority_identity_evidence"
                || grant.GetSchema() != "procurement" || grant.GetTableName() != "spending_authority_grants"
                || category.GetSchema() != "procurement" || category.GetTableName() != "spending_authority_categories") return false;
            foreach (var entity in new[] { identity, grant, category })
            {
                var mapped = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                command.CommandText = SelectAllColumnsSql(entity, mapped);
                await using (var rows = await command.ExecuteReaderAsync(ct)) { }
                if (!await True(ColumnsSql(entity, mapped))) return false;
            }
            if (!await True("SELECT NOT EXISTS(SELECT 1 FROM pg_attrdef WHERE adrelid='procurement.spending_authority_identity_evidence'::regclass)")) return false;
            var checks = identity.GetCheckConstraints().ToArray();
            if (checks.Length != 1 || checks[0].Name != "ck_authority_identity_evidence") return false;
            command.CommandText = CheckSql(table, checks[0].Name!);
            if (await command.ExecuteScalarAsync(ct) is not string actual || actual != CheckDefinition) return false;
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
        string[] append = ["spending_authority_identity_evidence", "audit_events", "outbox_events"];
        var clauses = append.Select(table =>
            "has_table_privilege(current_user," + Literal("procurement." + table) + ",'SELECT')"
            + (capture ? " AND has_table_privilege(current_user," + Literal("procurement." + table) + ",'INSERT')" : "")
            + " AND NOT has_table_privilege(current_user," + Literal("procurement." + table) + ",'UPDATE')"
            + " AND NOT has_any_column_privilege(current_user," + Literal("procurement." + table) + ",'UPDATE')"
            + " AND NOT has_table_privilege(current_user," + Literal("procurement." + table) + ",'DELETE')"
            + " AND NOT has_table_privilege(current_user," + Literal("procurement." + table) + ",'TRUNCATE')").ToList();
        clauses.Add("has_table_privilege(current_user,'procurement.spending_authority_grants','SELECT')"
            + (capture ? " AND has_table_privilege(current_user,'procurement.spending_authority_grants','INSERT') AND has_table_privilege(current_user,'procurement.spending_authority_grants','UPDATE')" : "")
            + " AND NOT has_table_privilege(current_user,'procurement.spending_authority_grants','DELETE') AND NOT has_table_privilege(current_user,'procurement.spending_authority_grants','TRUNCATE')");
        // Existing draft-category replacement legitimately deletes these children;
        // append-only action evidence is never given the same privilege.
        clauses.Add("has_table_privilege(current_user,'procurement.spending_authority_categories','SELECT')"
            + (capture ? " AND has_table_privilege(current_user,'procurement.spending_authority_categories','INSERT') AND has_table_privilege(current_user,'procurement.spending_authority_categories','DELETE')" : "")
            + " AND NOT has_table_privilege(current_user,'procurement.spending_authority_categories','UPDATE') AND NOT has_any_column_privilege(current_user,'procurement.spending_authority_categories','UPDATE') AND NOT has_table_privilege(current_user,'procurement.spending_authority_categories','TRUNCATE')");
        clauses.Add("has_table_privilege(current_user,'procurement.__procurement_migrations','SELECT')");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))");
        clauses.Add("NOT has_schema_privilege(current_user,'procurement','CREATE')");
        clauses.Add(NoCoreIdentityWritesSql);
        clauses.Add(NoOwnershipSql);
        return "SELECT " + string.Join(" AND ", clauses);
    }

    // Revoking ordinary DML ACLs does not remove an owner's ability to ALTER,
    // DROP or restore grants. Reject owning-role membership as well as SET ROLE
    // access, including the database/schema owners, on this actual connection.
    internal const string NoOwnershipSql = """
        NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
          WHERE ((n.nspname='procurement' AND c.relname IN (
            'spending_authority_grants','spending_authority_categories',
            'spending_authority_identity_evidence','audit_events','outbox_events','__procurement_migrations'))
            OR n.nspname='hrm') AND (
              pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))
        AND NOT EXISTS(SELECT 1 FROM pg_namespace n WHERE n.nspname IN ('procurement','hrm') AND (
          pg_has_role(current_user,n.nspowner,'MEMBER') OR pg_has_role(current_user,n.nspowner,'SET')))
        AND NOT EXISTS(SELECT 1 FROM pg_database d WHERE d.datname=current_database() AND (
          pg_has_role(current_user,d.datdba,'MEMBER') OR pg_has_role(current_user,d.datdba,'SET')))
        AND NOT EXISTS(SELECT 1 FROM pg_roles r WHERE (r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls)
          AND pg_has_role(current_user,r.oid,'SET'))
        """;

    // Core authentication/organisation facts remain read-only. Legitimate engine
    // writes in core_approval are separate and must not be forbidden for consumers.
    internal const string NoCoreIdentityWritesSql = """
        NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
          WHERE n.nspname='hrm' AND c.relkind IN ('r','p','v','f') AND (
            has_table_privilege(current_user,c.oid,'INSERT') OR has_any_column_privilege(current_user,c.oid,'INSERT')
            OR has_table_privilege(current_user,c.oid,'UPDATE') OR has_any_column_privilege(current_user,c.oid,'UPDATE')
            OR has_table_privilege(current_user,c.oid,'DELETE') OR has_table_privilege(current_user,c.oid,'TRUNCATE')))
        AND NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname='hrm' AND has_schema_privilege(current_user,oid,'CREATE'))
        """;
}
