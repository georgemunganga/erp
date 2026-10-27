using System.Collections.Frozen;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Physical owning-store proof only. No parent readiness, feature flag,
/// current business permission or policy decision is evaluated here.</summary>
internal static class ProcurementPrincipalOrderGovernancePhysicalSchema
{
    internal const string MigrationId = "20261026201259_M5aPrincipalOrderGovernance";
    internal static readonly IReadOnlyDictionary<Type, string> OwnedTables =
        new Dictionary<Type, string>
        {
            [typeof(PrincipalOrderFundingEvidence)] = "principal_order_funding_evidence",
            [typeof(PrincipalOrderSubmission)] = "principal_order_submissions",
            [typeof(PrincipalOrderAssignmentEvidence)] = "principal_order_assignments",
            [typeof(PrincipalOrderDecisionEvidence)] = "principal_order_decisions",
            [typeof(PrincipalOrderIssuedVersion)] = "principal_order_issued_versions",
            [typeof(PrincipalOrderAcknowledgment)] = "principal_order_acknowledgments",
            [typeof(PrincipalOrderReleaseRequest)] = "principal_order_release_requests",
            [typeof(PrincipalOrderReleaseConfirmation)] = "principal_order_release_confirmations",
        }.ToFrozenDictionary();

    // Populated ONLY from the genuine PostgreSQL capture after the guarded Up.
    // Capture SHA256: a48bdc7bff64636bd4b416540d9fa53fa73ec5df3ffdb3f73b82f9cf86527bc0.
    internal static readonly IReadOnlyDictionary<string, string> CapturedChecks =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["procurement.principal_order_acknowledgments.ck_principal_order_acknowledgment"] = "CHECK (((schema_version = 2) AND ((outcome)::text = ANY ((ARRAY['Accepted'::character varying, 'Rejected'::character varying])::text[])) AND (length(btrim((approved_manual_dispatch_policy_reference)::text)) > 0) AND (observed_at <= recorded_at) AND (length(btrim((channel)::text)) > 0) AND (length(btrim((supplier_reference)::text)) > 0) AND (length(btrim((evidence_reference)::text)) > 0) AND (recorded_by_principal_id IS NOT NULL) AND ((recorded_by_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((recorded_by_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((recorded_by_worker_alias_id IS NULL) OR (recorded_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid))))",
            ["procurement.principal_order_acknowledgments.ck_principal_order_acknowledgments_command"] = "CHECK ((command_key <> '00000000-0000-0000-0000-000000000000'::uuid))",
            ["procurement.principal_order_acknowledgments.ck_principal_order_acknowledgments_result_version"] = "CHECK ((resulting_order_version > 0))",
            ["procurement.principal_order_assignments.ck_principal_order_assignment"] = "CHECK (((schema_version = 2) AND (grant_revision > 0) AND (authority_version > 0) AND ((authority_evidence_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (principal_id IS NOT NULL) AND ((principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((worker_alias_id IS NULL) OR (worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid))))",
            ["procurement.principal_order_decisions.ck_principal_order_decision"] = "CHECK (((schema_version = 2) AND ((action)::text = ANY ((ARRAY['Approve'::character varying, 'Return'::character varying, 'Reject'::character varying])::text[])) AND (actor_principal_id IS NOT NULL) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (((action)::text = 'Approve'::text) OR ((reason IS NOT NULL) AND (length(btrim((reason)::text)) > 0))) AND ((actor_worker_alias_id IS NULL) OR (actor_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid))))",
            ["procurement.principal_order_decisions.ck_principal_order_decisions_command"] = "CHECK ((command_key <> '00000000-0000-0000-0000-000000000000'::uuid))",
            ["procurement.principal_order_draft_receipts.ck_po_principal_receipt"] = "CHECK (((schema_version = 2) AND ((operation)::text = ANY ((ARRAY['Create'::character varying, 'Cancel'::character varying, 'Funding'::character varying, 'Submit'::character varying, 'Decide'::character varying, 'Issue'::character varying, 'Ack'::character varying, 'ReleaseRequest'::character varying, 'ReleaseConfirm'::character varying])::text[])) AND (command_key <> '00000000-0000-0000-0000-000000000000'::uuid) AND (actor_principal_id IS NOT NULL) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((command_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((response_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (result_version > 0) AND ((((operation)::text = ANY ((ARRAY['Create'::character varying, 'Submit'::character varying, 'Issue'::character varying])::text[])) AND (response_status = 201)) OR (((operation)::text = ANY ((ARRAY['Cancel'::character varying, 'Funding'::character varying, 'Decide'::character varying, 'Ack'::character varying, 'ReleaseRequest'::character varying, 'ReleaseConfirm'::character varying])::text[])) AND (response_status = 200)))))",
            ["procurement.principal_order_funding_evidence.ck_principal_order_funding"] = "CHECK (((schema_version = 2) AND (commercial_revision = 1) AND (attested_gross_amount >= (0)::numeric) AND (length(btrim((manual_commitment_reference)::text)) > 0) AND (length(btrim((external_evidence_reference)::text)) > 0) AND (length(btrim((approved_manual_policy_reference)::text)) > 0) AND ((currency_code)::text ~ '^[A-Z]{3}$'::text) AND ((original_direct_source_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((commercial_snapshot_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (recorded_by_principal_id IS NOT NULL) AND ((recorded_by_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((recorded_by_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((buyer_worker_alias_id IS NULL) OR (buyer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((recorded_by_worker_alias_id IS NULL) OR (recorded_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid))))",
            ["procurement.principal_order_funding_evidence.ck_principal_order_funding_evidence_command"] = "CHECK ((command_key <> '00000000-0000-0000-0000-000000000000'::uuid))",
            ["procurement.principal_order_funding_evidence.ck_principal_order_funding_evidence_result_version"] = "CHECK ((resulting_order_version > 0))",
            ["procurement.principal_order_issued_versions.ck_principal_order_issued"] = "CHECK (((schema_version = 2) AND (commercial_revision = 1) AND ((commercial_snapshot_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((original_direct_source_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (length(btrim((manual_commitment_reference)::text)) > 0) AND (authorized_by_principal_id IS NOT NULL) AND ((authorized_by_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((authorized_by_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((authorized_by_worker_alias_id IS NULL) OR (authorized_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid))))",
            ["procurement.principal_order_issued_versions.ck_principal_order_issued_versions_command"] = "CHECK ((command_key <> '00000000-0000-0000-0000-000000000000'::uuid))",
            ["procurement.principal_order_issued_versions.ck_principal_order_issued_versions_result_version"] = "CHECK ((resulting_order_version > 0))",
            ["procurement.principal_order_release_confirmations.ck_principal_order_release_confirmation"] = "CHECK (((schema_version = 2) AND (released_gross_amount >= (0)::numeric) AND ((currency_code)::text ~ '^[A-Z]{3}$'::text) AND (length(btrim((manual_commitment_reference)::text)) > 0) AND (length(btrim((external_release_evidence_reference)::text)) > 0) AND (length(btrim((approved_manual_policy_reference)::text)) > 0) AND (confirmed_by_principal_id IS NOT NULL) AND ((confirmed_by_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((confirmed_by_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((confirmed_by_worker_alias_id IS NULL) OR (confirmed_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid))))",
            ["procurement.principal_order_release_confirmations.ck_principal_order_release_confirmations_command"] = "CHECK ((command_key <> '00000000-0000-0000-0000-000000000000'::uuid))",
            ["procurement.principal_order_release_confirmations.ck_principal_order_release_confirmations_result_version"] = "CHECK ((resulting_order_version > 0))",
            ["procurement.principal_order_release_requests.ck_principal_order_release_request"] = "CHECK (((schema_version = 2) AND ((((original_status)::text = 'Draft'::text) AND (commercial_revision = 0)) OR (((original_status)::text = ANY ((ARRAY['PendingApproval'::character varying, 'Returned'::character varying, 'Rejected'::character varying, 'Approved'::character varying])::text[])) AND (commercial_revision = 1))) AND ((((original_status)::text = 'PendingApproval'::text) AND (cancelled_core_case_id IS NOT NULL) AND (cancelled_core_case_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (core_cancellation_decision_id IS NOT NULL) AND (core_cancellation_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid)) OR (((original_status)::text <> 'PendingApproval'::text) AND (cancelled_core_case_id IS NULL) AND (core_cancellation_decision_id IS NULL))) AND (requested_order_version > 0) AND (length(btrim((reason)::text)) > 0) AND ((original_direct_source_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (requested_by_principal_id IS NOT NULL) AND ((requested_by_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((requested_by_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((requested_by_worker_alias_id IS NULL) OR (requested_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid))))",
            ["procurement.principal_order_release_requests.ck_principal_order_release_requests_command"] = "CHECK ((command_key <> '00000000-0000-0000-0000-000000000000'::uuid))",
            ["procurement.principal_order_submissions.ck_principal_order_submission"] = "CHECK (((schema_version = 2) AND (commercial_revision = 1) AND (submitted_row_version > 1) AND (policy_version_number > 0) AND (line_base_amount > (0)::numeric) AND (gross_amount >= line_base_amount) AND ((currency_code)::text ~ '^[A-Z]{3}$'::text) AND ((original_direct_source_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((commercial_snapshot_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((policy_rules_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (buyer_principal_id IS NOT NULL) AND ((buyer_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((buyer_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((buyer_worker_alias_id IS NULL) OR (buyer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid))))",
            ["procurement.principal_order_submissions.ck_principal_order_submissions_command"] = "CHECK ((command_key <> '00000000-0000-0000-0000-000000000000'::uuid))",
            ["procurement.purchase_orders.ck_po_principal_draft_version"] = "CHECK ((((principal_draft_schema_version = 0) AND (original_direct_source_sha256 IS NULL)) OR ((principal_draft_schema_version = 2) AND (branch_ref IS NOT NULL) AND (buyer_subject_id IS NOT NULL) AND (buyer_subject_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND (buyer_subject_id <> '00000000-0000-0000-0000-000000000000'::text) AND (original_direct_source_sha256 IS NOT NULL) AND ((original_direct_source_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (finance_commitment_ref IS NULL) AND ((((status)::text = ANY ((ARRAY['Draft'::character varying, 'Cancelled'::character varying, 'ReleasePending'::character varying])::text[])) AND (approval_instance_ref IS NULL) AND (submitted_at IS NULL) AND (current_revision = 0)) OR (((status)::text = ANY ((ARRAY['PendingApproval'::character varying, 'Approved'::character varying, 'Returned'::character varying, 'Rejected'::character varying, 'ReleasePending'::character varying, 'Cancelled'::character varying, 'IssuePending'::character varying, 'Issued'::character varying, 'Disputed'::character varying])::text[])) AND (approval_instance_ref IS NOT NULL) AND (submitted_at IS NOT NULL) AND (current_revision = 1))) AND ((((status)::text = 'Issued'::text) AND (issued_at IS NOT NULL)) OR (((status)::text <> 'Issued'::text) AND (issued_at IS NULL))) AND ((((status)::text = 'Cancelled'::text) AND (closed_at IS NOT NULL)) OR (((status)::text <> 'Cancelled'::text) AND (closed_at IS NULL))))))",
            ["procurement.spending_authority_identity_evidence.ck_authority_identity_evidence"] = "CHECK (((schema_version = ANY (ARRAY[1, 2])) AND ((schema_version = 2) OR ((actor_worker_id IS NOT NULL) AND (recipient_worker_id IS NOT NULL))) AND (id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (grant_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((schema_version = 1) OR ((((action)::text = 'PurchaseRequest.Approve'::text) AND ((permission)::text = 'procurement-approval-decide'::text)) OR (((action)::text = 'SourcingEvent.Publish'::text) AND ((permission)::text = 'procurement-sourcing-publish'::text)) OR (((action)::text = 'PurchaseOrder.Approve'::text) AND ((permission)::text = 'procurement-po-approve'::text)))) AND (authority_version > 0) AND (grant_record_version_at_action > 0) AND ((actor_worker_id IS NULL) OR (actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((recipient_worker_id IS NULL) OR (recipient_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND (length(btrim((authority_reference)::text)) > 0) AND (length(btrim((permission)::text)) > 0) AND (length(btrim((action)::text)) > 0) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((recipient_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((recipient_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((configuration_owner_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((configuration_owner_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (business_fingerprint IS NOT NULL) AND ((business_fingerprint)::text ~ '^[0-9a-fA-F]{64}$'::text) AND (evidence_sha256 IS NOT NULL) AND ((evidence_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND isfinite(action_at) AND (created_at = action_at) AND ((created_by)::text = (actor_principal_id)::text) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL) AND ((((stage)::text = 'Created'::text) AND (grant_record_version_at_action = 1) AND (created_evidence_id IS NULL) AND (created_evidence_stage IS NULL) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Submitted'::text) AND (created_evidence_id IS NOT NULL) AND (created_evidence_id <> id) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Activated'::text) AND (created_evidence_id IS NOT NULL) AND (created_evidence_id <> id) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NOT NULL) AND (submitted_evidence_id <> id) AND (submitted_evidence_id <> created_evidence_id) AND (submitted_evidence_stage IS NOT NULL) AND ((submitted_evidence_stage)::text = 'Submitted'::text)))))",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    internal static readonly IReadOnlyDictionary<Type, string> ChangedChecks =
        new Dictionary<Type, string>
        {
            [typeof(PurchaseOrder)] = "ck_po_principal_draft_version",
            [typeof(PrincipalOrderDraftReceipt)] = "ck_po_principal_receipt",
            [typeof(SpendingAuthorityIdentityRecord)] = "ck_authority_identity_evidence",
        }.ToFrozenDictionary();

    internal static bool MatchesCapturedCheck(string qualifiedName, string actual) =>
        CapturedChecks.TryGetValue(qualifiedName, out var captured)
        && !string.IsNullOrEmpty(captured) && string.Equals(captured, actual, StringComparison.Ordinal);

    internal static async Task<bool> InspectAsync(ProcurementDbContext owner, bool write,
        string expectedExecutionRole, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(expectedExecutionRole) || CapturedChecks.Count != 22
            || !owner.Database.IsNpgsql()) return false;
        var opened = false;
        try
        {
            if (owner.Database.HasPendingModelChanges()
                || (await owner.Database.GetPendingMigrationsAsync(ct)).Any()
                || !(await owner.Database.GetAppliedMigrationsAsync(ct)).Contains(MigrationId, StringComparer.Ordinal))
                return false;
            var model = owner.GetService<IDesignTimeModel>().Model;
            var entities = OwnedTables.Keys.Select(model.FindEntityType).ToArray();
            if (entities.Any(x => x is null)
                || entities.Sum(x => x!.GetKeys().Count()) != 16
                || entities.Sum(x => x!.GetForeignKeys().Count()) != 17
                || entities.Sum(x => x!.GetIndexes().Count()) != 27
                || entities.Sum(x => x!.GetCheckConstraints().Count()) != 19) return false;
            var connection = owner.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            { await owner.Database.OpenConnectionAsync(ct); opened = true; }
            await using var command = connection.CreateCommand();
            command.Transaction = owner.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandTimeout = 10;
            async Task<bool> True(string sql)
            { command.CommandText = sql; return await command.ExecuteScalarAsync(ct) is true; }
            if (!await True(PrivilegeSql(write, expectedExecutionRole))) return false;
            var checkNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entity in entities.Select(x => x!))
            {
                if (entity.GetSchema() != "procurement"
                    || entity.GetTableName() != OwnedTables[entity.ClrType]) return false;
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                command.CommandText = SelectAllColumnsSql(entity, table);
                await using (var reader = await command.ExecuteReaderAsync(ct)) { }
                if (!await True(ColumnsSql(entity, table))
                    || !await True(ExactTableShapeSql(entity, table))) return false;
                foreach (var key in entity.GetKeys())
                    if (!await True(KeySql(table,
                        key.Properties.Select(x => x.GetColumnName(table)!).ToArray(),
                        key.GetName()!, key.IsPrimaryKey()))) return false;
                foreach (var foreign in entity.GetForeignKeys())
                {
                    var parent = StoreObjectIdentifier.Table(foreign.PrincipalEntityType.GetTableName()!,
                        foreign.PrincipalEntityType.GetSchema());
                    if (!await True(ForeignKeySql(table, parent,
                        foreign.Properties.Select(x => x.GetColumnName(table)!).ToArray(),
                        foreign.PrincipalKey.Properties.Select(x => x.GetColumnName(parent)!).ToArray(),
                        foreign.GetConstraintName()))
                        || !await True(KeySql(parent,
                            foreign.PrincipalKey.Properties.Select(x => x.GetColumnName(parent)!).ToArray(),
                            foreign.PrincipalKey.GetName()!, foreign.PrincipalKey.IsPrimaryKey()))) return false;
                }
                foreach (var index in entity.GetIndexes())
                    if (index.GetFilter() is not null || !await True(IndexSql(table,
                        index.Properties.Select(x => x.GetColumnName(table)!).ToArray(),
                        index.GetDatabaseName()!, index.IsUnique))) return false;
                foreach (var check in entity.GetCheckConstraints())
                {
                    var name = table.Schema + "." + table.Name + "." + check.Name;
                    if (!checkNames.Add(name)) return false;
                    command.CommandText = CheckSql(table, check.Name);
                    if (await command.ExecuteScalarAsync(ct) is not string actual
                        || !MatchesCapturedCheck(name, actual)) return false;
                }
            }
            foreach (var (type, checkName) in ChangedChecks)
            {
                var entity = model.FindEntityType(type);
                if (entity is null || entity.GetSchema() != "procurement"
                    || !entity.GetCheckConstraints().Any(x => x.Name == checkName)) return false;
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                var name = table.Schema + "." + table.Name + "." + checkName;
                if (!checkNames.Add(name)) return false;
                command.CommandText = CheckSql(table, checkName);
                if (await command.ExecuteScalarAsync(ct) is not string actual
                    || !MatchesCapturedCheck(name, actual)) return false;
                if (type == typeof(PrincipalOrderDraftReceipt))
                {
                    var operation = entity.FindProperty(nameof(PrincipalOrderDraftReceipt.Operation));
                    if (operation?.GetColumnType() != "character varying(20)"
                        || operation.GetMaxLength() != 20
                        || !await True(ColumnsSql(entity, table))) return false;
                }
            }
            return checkNames.SetEquals(CapturedChecks.Keys);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
        finally { if (opened) await owner.Database.CloseConnectionAsync(); }
    }

    internal static string ExactTableShapeSql(IEntityType entity, StoreObjectIdentifier table)
    {
        var relation = Literal(table.Schema + "." + table.Name) + "::regclass";
        return "SELECT (SELECT count(*) FROM pg_attribute WHERE attrelid=" + relation
            + " AND attnum>0 AND NOT attisdropped)=" + entity.GetProperties().Count()
            + " AND NOT EXISTS(SELECT 1 FROM pg_attrdef WHERE adrelid=" + relation + ")"
            + " AND (SELECT count(*) FROM pg_constraint WHERE conrelid=" + relation
            + " AND contype IN ('p','u','f','c'))="
            + (entity.GetKeys().Count() + entity.GetForeignKeys().Count() + entity.GetCheckConstraints().Count())
            + " AND (SELECT count(*) FROM pg_index WHERE indrelid=" + relation + ")="
            + (entity.GetKeys().Count() + entity.GetIndexes().Count());
    }

    internal static string PrivilegeSql(bool write, string expectedExecutionRole)
    {
        var append = OwnedTables.Values.Order(StringComparer.Ordinal).ToArray();
        var reads = append.Concat(new[] { "purchase_orders", "principal_order_draft_receipts",
            "spending_authority_identity_evidence", "__procurement_migrations" });
        var clauses = reads.Select(x => "has_table_privilege(current_user," + Literal("procurement." + x)
            + ",'SELECT')").ToList();
        clauses.Add("session_user=current_user AND current_user=" + Literal(expectedExecutionRole));
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND "
            + "(rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles r WHERE "
            + "(r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls) "
            + "AND pg_has_role(current_user,r.oid,'SET'))");
        clauses.Add("NOT has_database_privilege(current_user,current_database(),'CREATE')");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_database d WHERE d.datname=current_database() AND "
            + "(pg_has_role(current_user,d.datdba,'MEMBER') OR pg_has_role(current_user,d.datdba,'SET')))");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_namespace n WHERE n.nspname IN "
            + "('procurement','core_approval') AND (has_schema_privilege(current_user,n.oid,'CREATE') "
            + "OR pg_has_role(current_user,n.nspowner,'MEMBER') OR pg_has_role(current_user,n.nspowner,'SET')))");
        foreach (var table in append)
        {
            var relation = Literal("procurement." + table);
            clauses.Add("NOT has_any_column_privilege(current_user," + relation + ",'UPDATE')");
            foreach (var privilege in new[] { "DELETE", "TRUNCATE", "TRIGGER" })
                clauses.Add("NOT has_table_privilege(current_user," + relation + "," + Literal(privilege) + ")");
            clauses.Add("NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid=" + relation + "::regclass "
                + "AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))");
            if (write) clauses.Add("has_table_privilege(current_user," + relation + ",'INSERT')");
        }
        return "SELECT " + string.Join(" AND ", clauses);
    }
}
