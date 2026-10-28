using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;
namespace Mightyfin.Erp.Hrm.Api;
public sealed class ProcurementInvoiceUnitReviewOptions
{
    public bool Enabled { get; set; }
    public bool ManualAttestationEnabled { get; set; }
    public bool ExecutionDatabaseConfigured { get; set; }
}
public interface IProcurementInvoiceUnitReviewSchemaReadiness
{
    Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct);
    Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct);
}
public sealed class ProcurementInvoiceUnitReviewSchemaReadiness : IProcurementInvoiceUnitReviewSchemaReadiness
{
    // Actual PG17 private capture1235, genuine P23 SQL b75973c7...
    // Seven CHECK capture SHA852061b7fbc71a789ed74b7700e7982378dca7f23865ae7951c969f7f123f423.
    // Catalog pins alone grant no authority: finite privileges/current Core scope remain mandatory.
    internal const string MigrationId = "20261022112145_P23CorePrincipalInvoiceUnitReview";
    internal const string OperationalCheckDefinition = "CHECK (((schema_version = ANY (ARRAY[1, 2])) AND ((policy_key)::text = ANY ((ARRAY['RECEIVING'::character varying, 'INVOICE_MATCHING'::character varying, 'INVOICE_UNIT_REVIEW'::character varying])::text[])) AND (id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (policy_version_number > 0) AND (policy_record_version_at_action > 0) AND ((actor_worker_id IS NULL) OR (actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((schema_version = 2) OR (actor_worker_id IS NOT NULL)) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (rules_sha256 IS NOT NULL) AND ((rules_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (evidence_sha256 IS NOT NULL) AND ((evidence_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND isfinite(action_at) AND (created_at = action_at) AND ((created_by)::text = (actor_principal_id)::text) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL) AND ((((stage)::text = 'Created'::text) AND (policy_record_version_at_action = 1) AND (created_evidence_id IS NULL) AND (created_evidence_stage IS NULL) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Submitted'::text) AND (policy_record_version_at_action > 1) AND (created_evidence_id IS NOT NULL) AND (created_evidence_id <> id) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Published'::text) AND (policy_record_version_at_action > 2) AND (created_evidence_id IS NOT NULL) AND (created_evidence_id <> id) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NOT NULL) AND (submitted_evidence_id <> id) AND (submitted_evidence_id <> created_evidence_id) AND (submitted_evidence_stage IS NOT NULL) AND ((submitted_evidence_stage)::text = 'Submitted'::text)))))";
    internal static readonly IReadOnlyDictionary<string, string> CheckDefinitions = new Dictionary<string, string>
    {
        ["ck_inv_unit_source"] = "CHECK (((invoice_version_at_open > 0) AND (mapping_revision > 0) AND (matching_policy_id <> review_policy_id) AND (core_case_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((maker_worker_id IS NULL) OR (maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((maker_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((maker_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (source_sha256 IS NOT NULL) AND ((source_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (original_sha256 IS NOT NULL) AND ((original_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (mapping_sha256 IS NOT NULL) AND ((mapping_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((octet_length(source_json) >= 1) AND (octet_length(source_json) <= 262144)) AND ((octet_length(issued_json) >= 1) AND (octet_length(issued_json) <= 262144)) AND isfinite(opened_at)))",
        ["ck_inv_unit_assignment"] = "CHECK (((core_assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((worker_id IS NULL) OR (worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text)))",
        ["ck_inv_unit_outcome"] = "CHECK ((((state)::text = ANY ((ARRAY['Verified'::character varying, 'Returned'::character varying, 'Rejected'::character varying])::text[])) AND (evidence_schema_version = 3) AND (final_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((reviewer_worker_id IS NULL) OR (reviewer_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((reviewer_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((reviewer_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (evidence_sha256 IS NOT NULL) AND ((evidence_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((octet_length(evidence_json) >= 1) AND (octet_length(evidence_json) <= 524288)) AND isfinite(completed_at)))",
        ["ck_inv_unit_receipt"] = "CHECK ((((operation)::text = ANY ((ARRAY['Open'::character varying, 'Decision'::character varying])::text[])) AND (command_key <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((actor_worker_id IS NULL) OR (actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (fingerprint IS NOT NULL) AND ((fingerprint)::text ~ '^[0-9a-f]{64}$'::text) AND ((octet_length(response_json) >= 1) AND (octet_length(response_json) <= 262144)) AND ((octet_length(request_json) >= 1) AND (octet_length(request_json) <= 65536)) AND isfinite(completed_at)))",
    };
    // Exact PostgreSQL-rendered P23 CHECK definitions for the two altered legacy
    // tables. No model SQL string or EF migration history alone grants readiness.
    internal static readonly IReadOnlyDictionary<string, string> PrincipalCheckDefinitions = new Dictionary<string, string>
    {
        ["ck_invoice_mapping_evidence"] = "CHECK (((mapping_revision > 0) AND ((mapping_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((invoice_original_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (identity_schema_version = ANY (ARRAY[1, 2])) AND ((identity_schema_version = 2) OR (actor_worker_id IS NOT NULL)) AND ((actor_worker_id IS NULL) OR (actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND (actor_principal_id IS NOT NULL) AND ((identity_schema_version = 1) OR (((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text)))))",
        ["ck_invoice_match_execution"] = "CHECK (((execution_version = 0) OR ((execution_version = ANY (ARRAY[1, 2])) AND (mapping_version_id IS NOT NULL) AND (purchase_order_id IS NOT NULL) AND (issued_version_id IS NOT NULL) AND (invoice_version > 0) AND (command_key IS NOT NULL) AND ((execution_version = 2) OR (actor_worker_id IS NOT NULL)) AND ((actor_worker_id IS NULL) OR (actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND (actor_principal_id IS NOT NULL) AND ((execution_version = 1) OR (((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text))) AND (invoice_original_sha256 IS NOT NULL) AND ((invoice_original_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (input_fingerprint IS NOT NULL) AND ((input_fingerprint)::text ~ '^[0-9a-f]{64}$'::text) AND (evidence_sha256 IS NOT NULL) AND ((evidence_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (policy_rules_sha256 IS NOT NULL) AND ((policy_rules_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (evidence_json IS NOT NULL) AND (source_watermarks_json IS NOT NULL) AND ((outcome)::text = ANY ((ARRAY['Compared'::character varying, 'Blocked'::character varying])::text[])))))",
    };
    private static readonly Type[] Types = [typeof(InvoiceUnitReviewSource), typeof(InvoiceUnitReviewAssignmentEvidence), typeof(InvoiceUnitReviewOutcome), typeof(InvoiceUnitReviewCommandReceipt)];
    internal static bool ManifestCaptured => MigrationId.Length > 0 && OperationalCheckDefinition.Length > 0 && CheckDefinitions.Count == 4 && PrincipalCheckDefinitions.Count == 2;
    public Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct) => Check(owner, false, ct);
    public Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct) => Check(owner, true, ct);
    internal const string CoreDecisionPrivilegesSql = "SELECT has_table_privilege(current_user,'core_approval.decisions','SELECT') AND has_table_privilege(current_user,'core_approval.decisions','INSERT') AND NOT has_any_column_privilege(current_user,'core_approval.decisions','UPDATE') AND NOT has_table_privilege(current_user,'core_approval.decisions','DELETE,TRUNCATE')";
    internal const string CoreReadBoundarySql = "SELECT has_table_privilege(current_user,'core_approval.decisions','SELECT') AND NOT has_any_column_privilege(current_user,'core_approval.decisions','UPDATE') AND NOT has_table_privilege(current_user,'core_approval.decisions','DELETE,TRUNCATE') AND NOT has_schema_privilege(current_user,'core_approval','CREATE') AND NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='core_approval' AND c.relname IN ('cases','stages','assignments','decisions') AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))";
    internal static string AppendPrivilegesSql(string relation, bool write)
        => $"SELECT NOT has_any_column_privilege(current_user,{Literal(relation)},'UPDATE') AND NOT has_table_privilege(current_user,{Literal(relation)},'DELETE,TRUNCATE') AND NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid={Literal(relation)}::regclass AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))"
            + (write ? $" AND has_table_privilege(current_user,{Literal(relation)},'INSERT')" : "");
    internal static bool IssuedParentColumnsCovered(Type type, bool issuedParentsInstalled) =>
        issuedParentsInstalled && (type == typeof(InvoiceMatchMappingVersion) || type == typeof(InvoiceMatchResult));

    private static async Task<bool> Check(ProcurementDbContext owner, bool write, CancellationToken ct)
    {
        if (!ManifestCaptured || !owner.Database.IsNpgsql()) return false;
        var opened = false;
        try
        {
            if (owner.Database.HasPendingModelChanges() || (await owner.Database.GetPendingMigrationsAsync(ct)).Any()
                || !(await owner.Database.GetAppliedMigrationsAsync(ct)).Contains(MigrationId, StringComparer.Ordinal)) return false;
            if (owner.Database.GetDbConnection().State != ConnectionState.Open) { await owner.Database.OpenConnectionAsync(ct); opened = true; }
            await using var command = owner.Database.GetDbConnection().CreateCommand(); command.Transaction = owner.Database.CurrentTransaction?.GetDbTransaction(); command.CommandTimeout = 10;
            async Task<bool> True(string sql) { command.CommandText = sql; return await command.ExecuteScalarAsync(ct) is true; }
            var applied = (await owner.Database.GetAppliedMigrationsAsync(ct)).ToArray();
            var manualInstalled = ProcurementInvoiceManualUnitReviewSchemaCapture.MigrationId.Length > 0
                && applied.Contains(ProcurementInvoiceManualUnitReviewSchemaCapture.MigrationId, StringComparer.Ordinal);
            var model = owner.GetService<IDesignTimeModel>().Model;
            foreach (var type in Types)
            {
                var entity = model.FindEntityType(type); if (entity is null) return false;
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                if (table.Schema != "procurement") return false;
                command.CommandText = SelectAllColumnsSql(entity, table); await using (var reader = await command.ExecuteReaderAsync(ct)) { }
                if (!await True(ColumnsSql(entity, table))) return false;
                var checks = entity.GetCheckConstraints().ToArray();
                if (checks.Length != 1 || !CheckDefinitions.TryGetValue(checks[0].Name!, out var expected)) return false;
                command.CommandText = CheckSql(table, checks[0].Name!);
                if (await command.ExecuteScalarAsync(ct) is not string actual
                    || (manualInstalled && checks[0].Name == "ck_inv_unit_outcome"
                        ? !ProcurementInvoiceManualUnitReviewSchemaCapture.MatchesOutcome(actual) : actual != expected)) return false;
                foreach (var foreign in entity.GetForeignKeys())
                {
                    var parent = StoreObjectIdentifier.Table(foreign.PrincipalEntityType.GetTableName()!, foreign.PrincipalEntityType.GetSchema());
                    if (!await True(ForeignKeySql(table, parent, foreign.Properties.Select(x => x.GetColumnName(table)!).ToArray(), foreign.PrincipalKey.Properties.Select(x => x.GetColumnName(parent)!).ToArray(), foreign.GetConstraintName()))) return false;
                    if (!await True(KeySql(parent, foreign.PrincipalKey.Properties.Select(x => x.GetColumnName(parent)!).ToArray(), foreign.PrincipalKey.GetName()!, foreign.PrincipalKey.IsPrimaryKey()))) return false;
                }
                foreach (var key in entity.GetKeys()) if (!await True(KeySql(table, key.Properties.Select(x => x.GetColumnName(table)!).ToArray(), key.GetName()!, key.IsPrimaryKey()))) return false;
                foreach (var index in entity.GetIndexes().Where(x => x.IsUnique))
                    if (index.GetFilter() is not null || !await True(UniqueIndexSql(table, index.Properties.Select(x => x.GetColumnName(table)!).ToArray(), index.GetDatabaseName()!, null))) return false;
                var relation = "procurement." + table.Name;
                if (!await True($"SELECT has_table_privilege(current_user,'{relation}','SELECT')")) return false;
                // Eligibility reads trust retained append-only facts too: an overprivileged
                // reader cannot use evidence it could have rewritten itself.
                if (!await True(AppendPrivilegesSql(relation, write))) return false;
            }
            var ownerBaselineInstalled = applied.Contains(ProcurementInvoiceOwnerBaselineSchemaCapture.MigrationId,StringComparer.Ordinal);
            var issuedParentsInstalled = ProcurementPrincipalInvoiceIssuedParentReadiness.MigrationId.Length > 0
                && applied.Contains(ProcurementPrincipalInvoiceIssuedParentReadiness.MigrationId, StringComparer.Ordinal);
            // The full B2 proof owns the six generated pins, their exact CASE
            // expressions, scoped parent FKs and both changed CHECK definitions.
            if (issuedParentsInstalled
                && !await ProcurementPrincipalInvoiceIssuedParentReadiness.RequireIssuedParentsAsync(owner, ct)) return false;
            foreach (var (type, name) in new[] { (typeof(InvoiceMatchMappingVersion), "ck_invoice_mapping_evidence"), (typeof(InvoiceMatchResult), "ck_invoice_match_execution") })
            {
                var entity = model.FindEntityType(type); if (entity is null || !PrincipalCheckDefinitions.TryGetValue(name, out var expected)) return false;
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                if (table.Schema != "procurement"
                    || (!IssuedParentColumnsCovered(type, issuedParentsInstalled)
                        && !await True(ColumnsSql(entity, table)))) return false;
                // The B2 result CHECK was proved above from its exact native
                // capture. Mapping keeps the existing M6A CHECK proof below.
                if (issuedParentsInstalled && type == typeof(InvoiceMatchResult)) continue;
                command.CommandText = CheckSql(table, name);
                if (await command.ExecuteScalarAsync(ct) is not string actual) return false;
                if (name == "ck_invoice_mapping_evidence" && ownerBaselineInstalled)
                {
                    if(!ProcurementInvoiceOwnerBaselineSchemaCapture.CheckMatches("procurement.invoice_match_mapping_versions.ck_invoice_mapping_evidence",actual))return false;
                }
                else if(actual != expected)return false;
            }
            command.CommandText = CheckSql(StoreObjectIdentifier.Table("operational_policy_identity_evidence", "procurement"), "ck_operational_policy_identity");
            var paymentInstalled = applied.Contains(ProcurementManualPaymentGovernanceReadiness.MigrationId, StringComparer.Ordinal);
            var expectedOperational = ownerBaselineInstalled ? ProcurementInvoiceOwnerBaselineSchemaCapture.OperationalCheckDefinition
                : paymentInstalled ? ProcurementManualPaymentGovernanceReadiness.OperationalCheckDefinition : OperationalCheckDefinition;
            if (await command.ExecuteScalarAsync(ct) is not string operational
                || (ownerBaselineInstalled
                    ? !ProcurementInvoiceOwnerBaselineSchemaCapture.CheckMatches("procurement.operational_policy_identity_evidence.ck_operational_policy_identity",operational)
                    : operational != expectedOperational && !ProcurementRestoreCheckAlternatives.Matches(
                        "procurement.operational_policy_identity_evidence.ck_operational_policy_identity",expectedOperational,operational))) return false;
            if (!await True("SELECT " + ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql)
                || !await True("SELECT " + ProcurementSpendingAuthorityIdentitySchemaReadiness.NoOwnershipSql)
                || !await True(CoreReadBoundarySql)) return false;
            if (write)
            {
                foreach (var table in new[] { "cases", "stages", "assignments" })
                    if (!await True($"SELECT has_table_privilege(current_user,'core_approval.{table}','SELECT') AND has_table_privilege(current_user,'core_approval.{table}','INSERT') AND has_table_privilege(current_user,'core_approval.{table}','UPDATE') AND NOT has_table_privilege(current_user,'core_approval.{table}','DELETE,TRUNCATE')")) return false;
                if (!await True(CoreDecisionPrivilegesSql)) return false;
                foreach (var table in new[] { "audit_events", "outbox_events" })
                    if (!await True($"SELECT has_table_privilege(current_user,'procurement.{table}','SELECT') AND has_table_privilege(current_user,'procurement.{table}','INSERT') AND NOT has_any_column_privilege(current_user,'procurement.{table}','UPDATE') AND NOT has_table_privilege(current_user,'procurement.{table}','DELETE,TRUNCATE')")) return false;
            }
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
        finally { if (opened) await owner.Database.CloseConnectionAsync(); }
    }
}
