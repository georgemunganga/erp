using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Actual P28 catalog and finite business-role inspection. No Finance readiness,
/// employee prerequisite, auto-migration, grant or approved policy is supplied here.</summary>
public sealed class ProcurementManualPaymentGovernanceReadiness(
    string executionConnection, ProcurementRequestScope scope,
    ILogger<ProcurementManualPaymentGovernanceReadiness> logger) : IProcurementManualPaymentGovernanceReadiness
{
    internal const string MigrationId = "20261023153226_P28ManualPaymentGovernance";
    // Verified equal on fresh down/up and retained native P27→P28 PostgreSQL rehearsals.
    // Physical column ordinals are not a named-column API contract; their attributes are.
    internal const string ExpectedFingerprint = "a5e05163346be79ccaee5aa11093e377a9d0a5f86046311759698ca72435e29b";
    // Reuse the accepted P25 identity projection, adding only the Core labels and
    // company clock read by the actual payment pickers/detail/date provider.
    // Model metadata is not a table SELECT grant; credentials are never projected.
    internal static readonly IReadOnlyDictionary<Type, string[]> CoreReadProperties =
        ProcurementPrincipalApprovalStoreReadiness.CoreIdentityReadProperties.ToDictionary(
            pair => pair.Key, pair => pair.Value.Concat(pair.Key == typeof(LocalUser)
                ? new[] { nameof(LocalUser.DisplayName) }
                : pair.Key == typeof(WorkLocation)
                    ? new[] { nameof(WorkLocation.Code), nameof(WorkLocation.Name) }
                    : pair.Key == typeof(LegalEntity)
                        ? new[] { nameof(LegalEntity.TimeZoneId) } : Array.Empty<string>())
                .Distinct(StringComparer.Ordinal).ToArray());
    private sealed record Tenant(string Value) : ITenantAccessor
    { public string GetTenantId() => Value; }
    internal const string OperationalCheckDefinition = "CHECK (((schema_version = ANY (ARRAY[1, 2])) AND ((policy_key)::text = ANY ((ARRAY['RECEIVING'::character varying, 'INVOICE_MATCHING'::character varying, 'INVOICE_UNIT_REVIEW'::character varying, 'MANUAL_PAYMENT_REVIEW'::character varying])::text[])) AND (((policy_key)::text <> 'MANUAL_PAYMENT_REVIEW'::text) OR (schema_version = 2)) AND (id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (policy_version_number > 0) AND (policy_record_version_at_action > 0) AND ((actor_worker_id IS NULL) OR (actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((schema_version = 2) OR (actor_worker_id IS NOT NULL)) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (rules_sha256 IS NOT NULL) AND ((rules_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (evidence_sha256 IS NOT NULL) AND ((evidence_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND isfinite(action_at) AND (created_at = action_at) AND ((created_by)::text = (actor_principal_id)::text) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL) AND ((((stage)::text = 'Created'::text) AND (policy_record_version_at_action = 1) AND (created_evidence_id IS NULL) AND (created_evidence_stage IS NULL) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Submitted'::text) AND (policy_record_version_at_action > 1) AND (created_evidence_id IS NOT NULL) AND (created_evidence_id <> id) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Published'::text) AND (policy_record_version_at_action > 2) AND (created_evidence_id IS NOT NULL) AND (created_evidence_id <> id) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NOT NULL) AND (submitted_evidence_id <> id) AND (submitted_evidence_id <> created_evidence_id) AND (submitted_evidence_stage IS NOT NULL) AND ((submitted_evidence_stage)::text = 'Submitted'::text)))))";

    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(executionConnection)) return false;
        try
        {
            await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql(executionConnection, x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, scope);
            if (db.Database.HasPendingModelChanges()) return false;
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            await db.Database.ExecuteSqlRawAsync("SET LOCAL search_path=pg_catalog; SET LOCAL transaction_read_only=on; SET LOCAL statement_timeout='5s'", ct);
            if ((await db.Database.GetPendingMigrationsAsync(ct)).Any()
                || !(await db.Database.GetAppliedMigrationsAsync(ct)).Contains(MigrationId, StringComparer.Ordinal)) return false;
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction(); command.CommandTimeout = 10;
            // Metadata only: all privilege/catalog SQL remains on the original
            // execution connection and its existing read-only transaction.
            using var core = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>()
                .UseNpgsql(db.Database.GetDbConnection()).Options, new Tenant(scope.TenantId));
            command.CommandText = PrivilegeSql(core.Model);
            if (await command.ExecuteScalarAsync(ct) is not true) return false;
            command.CommandText = CoreOwnerSql;
            if (await command.ExecuteScalarAsync(ct) is not true) return false;
            command.CommandText = FingerprintSql;
            return await command.ExecuteScalarAsync(ct) is string actual && actual == ExpectedFingerprint;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            // Never log connection strings, query bodies, identity data or bank references.
            logger.LogWarning("Payment governance inspection unavailable ({ErrorType}).", error.GetType().Name);
            return false;
        }
    }

    internal static string PrivilegeSql(IModel coreModel)
    {
        var clauses = new List<string> {
            "NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))",
            "NOT has_schema_privilege(current_user,'procurement','CREATE')",
            "NOT has_schema_privilege(current_user,'core_approval','CREATE')",
            "NOT has_schema_privilege(current_user,'hrm','CREATE')",
            "has_schema_privilege(current_user,'hrm','USAGE')",
            ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql,
            ProcurementSpendingAuthorityIdentitySchemaReadiness.NoOwnershipSql,
            "NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('procurement','core_approval') AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))"
        };
        foreach (var table in new[] { "manual_payment_decisions", "manual_payment_receipts", "manual_payment_workflow_cases",
            "manual_payment_core_decisions", "manual_payment_date_observations", "operational_policy_identity_evidence", "audit_events", "outbox_events" })
            clauses.Add(AppendOnly("procurement." + table));
        clauses.Add(AppendOnly("core_approval.decisions"));
        foreach (var table in new[] { "procurement.manual_payment_requests", "procurement.policy_versions",
            "core_approval.cases", "core_approval.stages", "core_approval.assignments" })
            clauses.Add("has_table_privilege(current_user,'" + table + "','SELECT')"
                + " AND has_table_privilege(current_user,'" + table + "','INSERT')"
                + " AND has_table_privilege(current_user,'" + table + "','UPDATE')"
                + " AND NOT has_table_privilege(current_user,'" + table + "','DELETE')"
                + " AND NOT has_table_privilege(current_user,'" + table + "','TRUNCATE')");
        foreach (var table in new[] { "procurement.supplier_invoices", "procurement.supplier_credit_note_refs",
            "procurement.finance_payment_status_refs", "procurement.__procurement_migrations",
            "core_approval.\"__EFMigrationsHistory\"", "hrm.__hrm_migrations" })
            clauses.Add("has_table_privilege(current_user,'" + table + "','SELECT')");
        clauses.AddRange(CoreReadClauses(coreModel));
        // PostgreSQL invoice SELECT FOR UPDATE needs UPDATE on at least one column.
        clauses.Add("has_any_column_privilege(current_user,'procurement.supplier_invoices','UPDATE')");
        foreach (var action in new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE" })
            clauses.Add("NOT has_table_privilege(current_user,'procurement.finance_payment_status_refs','" + action + "')");
        foreach (var action in new[] { "INSERT", "UPDATE" })
            clauses.Add("NOT has_any_column_privilege(current_user,'procurement.finance_payment_status_refs','" + action + "')");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname IN ('procurement','core_approval') AND (pg_has_role(current_user,p.proowner,'MEMBER') OR pg_has_role(current_user,p.proowner,'SET')))");
        return "SELECT " + string.Join(" AND ", clauses);
    }

    internal static IReadOnlyList<string> CoreReadClauses(IModel model)
    {
        var clauses = new List<string>();
        foreach (var (type, properties) in CoreReadProperties)
        {
            var entity = model.FindEntityType(type)
                ?? throw new InvalidOperationException("PaymentCoreReadEntityMissing");
            if (entity.GetSchema() != "hrm" || entity.GetTableName() is not { } name)
                throw new InvalidOperationException("PaymentCoreReadOwnerUnsupported");
            var table = StoreObjectIdentifier.Table(name, "hrm");
            foreach (var property in properties)
            {
                var column = entity.FindProperty(property)?.GetColumnName(table)
                    ?? throw new InvalidOperationException("PaymentCoreReadPropertyMissing");
                clauses.Add("has_column_privilege(current_user,"
                    + ProcurementSchemaInspectionSql.Literal("hrm." + name) + ","
                    + ProcurementSchemaInspectionSql.Literal(column) + ",'SELECT')");
            }
        }
        return clauses;
    }

    private static string AppendOnly(string table) =>
        "has_table_privilege(current_user,'" + table + "','SELECT') AND has_table_privilege(current_user,'" + table + "','INSERT')"
        + " AND NOT has_table_privilege(current_user,'" + table + "','UPDATE')"
        + " AND NOT has_any_column_privilege(current_user,'" + table + "','UPDATE')"
        + " AND NOT has_table_privilege(current_user,'" + table + "','DELETE')"
        + " AND NOT has_table_privilege(current_user,'" + table + "','TRUNCATE')"
        + " AND NOT has_table_privilege(current_user,'" + table + "','TRIGGER')";

    private const string CoreOwnerSql = """
        SELECT EXISTS(SELECT 1 FROM core_approval."__EFMigrationsHistory" WHERE "MigrationId"='20261019183218_P01CoreApprovalFoundation')
        AND EXISTS(SELECT 1 FROM hrm.__hrm_migrations WHERE "MigrationId"='20261023130726_CoreCompanyTimeZone')
        AND EXISTS(SELECT 1 FROM pg_attribute a WHERE a.attrelid='hrm.legal_entities'::regclass
          AND a.attname='time_zone_id' AND NOT a.attisdropped AND a.attnotnull AND format_type(a.atttypid,a.atttypmod)='character varying(100)')
        AND has_column_privilege(current_user,'hrm.legal_entities','time_zone_id','SELECT')
        """;

    internal const string FingerprintSql = """
        WITH wanted(schema_name,table_name) AS (VALUES
         ('procurement','manual_payment_requests'),('procurement','manual_payment_decisions'),('procurement','manual_payment_receipts'),
         ('procurement','manual_payment_workflow_cases'),('procurement','manual_payment_core_decisions'),('procurement','manual_payment_date_observations'),
         ('procurement','policy_versions'),('procurement','operational_policy_identity_evidence'),('procurement','supplier_invoices'),
         ('procurement','supplier_credit_note_refs'),('procurement','finance_payment_status_refs'),('procurement','audit_events'),('procurement','outbox_events'),
         ('core_approval','cases'),('core_approval','stages'),('core_approval','assignments'),('core_approval','decisions')),
         relations AS (SELECT n.nspname,c.relname,c.oid,c.relkind,c.relrowsecurity,c.relforcerowsecurity
         FROM wanted w JOIN pg_namespace n ON n.nspname=w.schema_name JOIN pg_class c ON c.relnamespace=n.oid AND c.relname=w.table_name),
         manifest AS (SELECT jsonb_build_object(
         'relations',(SELECT jsonb_agg(jsonb_build_array(nspname,relname,relkind,relrowsecurity,relforcerowsecurity) ORDER BY nspname,relname) FROM relations),
         'columns',(SELECT jsonb_agg(jsonb_build_array(r.nspname,r.relname,a.attname,pg_catalog.format_type(a.atttypid,a.atttypmod),a.attnotnull,a.attidentity,a.attgenerated,pg_get_expr(d.adbin,d.adrelid)) ORDER BY r.nspname,r.relname,a.attname)
         FROM relations r JOIN pg_attribute a ON a.attrelid=r.oid AND a.attnum>0 AND NOT a.attisdropped LEFT JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum),
         'constraints',(SELECT jsonb_agg(jsonb_build_array(r.nspname,r.relname,c.conname,c.contype,c.convalidated,c.condeferrable,c.condeferred,pg_get_constraintdef(c.oid)) ORDER BY r.nspname,r.relname,c.conname)
         FROM relations r JOIN pg_constraint c ON c.conrelid=r.oid),
         'indexes',(SELECT jsonb_agg(jsonb_build_array(r.nspname,r.relname,c.relname,i.indisvalid,i.indisready,i.indisunique,pg_get_indexdef(i.indexrelid)) ORDER BY r.nspname,r.relname,c.relname)
         FROM relations r JOIN pg_index i ON i.indrelid=r.oid JOIN pg_class c ON c.oid=i.indexrelid),
         'triggers',(SELECT jsonb_agg(jsonb_build_array(r.nspname,r.relname,t.tgname,t.tgenabled,t.tgdeferrable,t.tginitdeferred,pg_get_triggerdef(t.oid)) ORDER BY r.nspname,r.relname,t.tgname)
         FROM relations r JOIN pg_trigger t ON t.tgrelid=r.oid AND NOT t.tgisinternal),
         'functions',(SELECT jsonb_agg(jsonb_build_array(p.proname,pg_get_function_identity_arguments(p.oid),pg_get_functiondef(p.oid)) ORDER BY p.proname,pg_get_function_identity_arguments(p.oid))
         FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='procurement' AND p.proname IN
         ('reject_manual_payment_evidence_mutation','guard_manual_payment_request','require_manual_payment_command_bundle','require_manual_payment_evidence_bundle',
         'guard_manual_payment_title','require_manual_payment_governance_companions','manual_payment_policy_digest','require_manual_payment_core_case','require_manual_payment_core_decision','require_manual_payment_company_date'))
         ) AS value)
         SELECT encode(sha256(convert_to(value::text,'UTF8')),'hex') FROM manifest;
        """;
}
