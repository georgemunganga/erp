using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Read-only owning-store proof, independent of feature flags and business
/// authorization. M5b native migration rehearsal must verify these expected definitions.</summary>
internal static class ProcurementPrincipalReceivingPhysicalSchema
{
    internal const string MigrationId = "20261028160000_AddPrincipalReceivingExecution";
    internal static readonly IReadOnlyDictionary<Type, string> OwnedTables = new Dictionary<Type, string>
    {
        [typeof(PrincipalReceivingSourceState)] = "principal_receiving_sources",
        [typeof(PrincipalReceivingFact)] = "principal_receiving_facts",
        [typeof(PrincipalReceivingDraftHead)] = "principal_receiving_drafts",
        [typeof(PrincipalReceivingDraftRevision)] = "principal_receiving_revisions",
        [typeof(PrincipalReceivingCommandReceipt)] = "principal_receiving_receipts",
        [typeof(PrincipalReceivingDraftLineBinding)] = "principal_receiving_line_bindings",
    };
    // Exact receiving-only definitions retained from the held PostgreSQL physical
    // contract; no claim of a fresh M5b capture is made before native rehearsal.
    internal static readonly IReadOnlyDictionary<string, string> ExpectedChecks = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["procurement.principal_receiving_drafts.ck_prcv_draft"] = "CHECK (((schema_version = 2) AND (revision > 0) AND ((kind)::text = ANY ((ARRAY['Goods'::character varying, 'Service'::character varying])::text[])) AND ((state)::text = ANY ((ARRAY['Draft'::character varying, 'Recorded'::character varying, 'Certified'::character varying, 'Cancelled'::character varying])::text[])) AND (((kind)::text = 'Goods'::text) OR ((state)::text <> 'Recorded'::text)) AND (((kind)::text = 'Service'::text) OR ((state)::text <> 'Certified'::text)) AND ((snapshot_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((creator_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((creator_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text)))",
        ["procurement.principal_receiving_facts.ck_prcv_fact"] = "CHECK (((schema_version = 2) AND (sequence > 0) AND ((kind)::text = ANY ((ARRAY['Observation'::character varying, 'Inspection'::character varying, 'Acceptance'::character varying])::text[])) AND ((((kind)::text = 'Observation'::text) AND ((operation)::text = 'Record'::text)) OR (((kind)::text = 'Inspection'::text) AND ((operation)::text = 'Inspect'::text)) OR (((kind)::text = 'Acceptance'::text) AND ((operation)::text = ANY ((ARRAY['Accept'::character varying, 'Certify'::character varying])::text[])))) AND (quantity >= (0)::numeric) AND (net_amount >= (0)::numeric) AND (((kind)::text = 'Inspection'::text) OR (quantity > (0)::numeric)) AND (((kind)::text = 'Acceptance'::text) OR (net_amount = (0)::numeric)) AND ((payload_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND (command_key <> '00000000-0000-0000-0000-000000000000'::uuid)))",
        ["procurement.principal_receiving_line_bindings.ck_prcv_line"] = "CHECK (((schema_version = 2) AND (quantity > (0)::numeric) AND (rejected_quantity >= (0)::numeric) AND (rejected_quantity <= quantity) AND (line_id <> '00000000-0000-0000-0000-000000000000'::uuid)))",
        ["procurement.principal_receiving_receipts.ck_prcv_receipt"] = "CHECK (((schema_version = 2) AND ((operation)::text = ANY ((ARRAY['Create'::character varying, 'Revise'::character varying, 'Cancel'::character varying, 'Record'::character varying, 'Inspect'::character varying, 'Accept'::character varying, 'Certify'::character varying])::text[])) AND (response_status = ANY (ARRAY[200, 201])) AND (result_revision > 0) AND ((request_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((response_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND (command_key <> '00000000-0000-0000-0000-000000000000'::uuid)))",
        ["procurement.principal_receiving_revisions.ck_prcv_revision"] = "CHECK (((schema_version = 2) AND (revision > 0) AND ((snapshot_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND (command_key <> '00000000-0000-0000-0000-000000000000'::uuid)))",
        ["procurement.principal_receiving_sources.ck_prcv_source"] = "CHECK (((schema_version = 2) AND ((kind)::text = ANY ((ARRAY['Goods'::character varying, 'Service'::character varying])::text[])) AND (length((category_code)::text) > 0) AND (length((purchase_type)::text) > 0) AND ((currency_code)::text ~ '^[A-Z]{3}$'::text) AND ((original_source_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((issued_snapshot_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (sequence >= 0) AND (accepted_quantity >= (0)::numeric) AND (accepted_net_amount >= (0)::numeric) AND ((source_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (branch_id <> '00000000-0000-0000-0000-000000000000'::uuid)))",
    };
    private static readonly IReadOnlyDictionary<string, int> OverlayCounts = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["principal_receiving_sources"] = 4, ["principal_receiving_facts"] = 2,
        ["principal_receiving_drafts"] = 1, ["principal_receiving_revisions"] = 0,
        ["principal_receiving_receipts"] = 1, ["principal_receiving_line_bindings"] = 3,
    };

    internal static async Task<bool> InspectAsync(ProcurementDbContext owner, bool write, string role, CancellationToken ct)
    {
        if (!owner.Database.IsNpgsql() || string.IsNullOrWhiteSpace(role) || ExpectedChecks.Count != 6) return false;
        var opened = false;
        try
        {
            if (ProcurementCompiledModelParity.HasPendingModelChanges(owner) || (await owner.Database.GetPendingMigrationsAsync(ct)).Any()
                || !(await owner.Database.GetAppliedMigrationsAsync(ct)).Contains(MigrationId, StringComparer.Ordinal)) return false;
            var applied=await owner.Database.GetAppliedMigrationsAsync(ct);
            var returnsApplied=applied.Contains(
                ProcurementPrincipalGoodsReturnSchemaCapture.MigrationId,StringComparer.Ordinal);
            var invoiceBindingsApplied=applied.Contains(
                ProcurementPrincipalInvoiceMatchingReadiness.MigrationId,StringComparer.Ordinal);
            var model = owner.GetService<IDesignTimeModel>().Model;
            var connection = owner.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) { await owner.Database.OpenConnectionAsync(ct); opened = true; }
            await using var command = connection.CreateCommand();
            command.Transaction = owner.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandTimeout = 10;
            async Task<bool> True(string sql) { command.CommandText = sql; return await command.ExecuteScalarAsync(ct) is true; }
            if (!await True(PrivilegeSql(write, role))) return false;
            var checkedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (type, name) in OwnedTables)
            {
                var entity = model.FindEntityType(type);
                if (entity is null || entity.GetSchema() != "procurement" || entity.GetTableName() != name) return false;
                var table = StoreObjectIdentifier.Table(name, "procurement");
                command.CommandText = SelectAllColumnsSql(entity, table);
                await using (var reader = await command.ExecuteReaderAsync(ct)) { }
                if (!await True(ColumnsSql(entity, table)) || !await True(ExactShapeSql(entity, table,returnsApplied))) return false;
                if(returnsApplied && ReturnOwnerKeys.TryGetValue(name,out var returnKey)
                    && !await True(KeySql(table,returnKey.Columns,returnKey.Name,false)))return false;
                foreach (var key in entity.GetKeys())
                    if (!await True(KeySql(table, key.Properties.Select(x => x.GetColumnName(table)!).ToArray(), key.GetName()!, key.IsPrimaryKey()))) return false;
                foreach (var foreign in entity.GetForeignKeys())
                {
                    var parent = StoreObjectIdentifier.Table(foreign.PrincipalEntityType.GetTableName()!, foreign.PrincipalEntityType.GetSchema());
                    if (!await True(ForeignKeySql(table, parent, foreign.Properties.Select(x => x.GetColumnName(table)!).ToArray(),
                        foreign.PrincipalKey.Properties.Select(x => x.GetColumnName(parent)!).ToArray(), foreign.GetConstraintName()))) return false;
                }
                foreach (var index in entity.GetIndexes())
                    if (index.GetFilter() is not null || !await True(IndexSql(table, index.Properties.Select(x => x.GetColumnName(table)!).ToArray(), index.GetDatabaseName()!, index.IsUnique))) return false;
                foreach (var check in entity.GetCheckConstraints())
                {
                    var qualified = "procurement." + name + "." + check.Name;
                    if (!checkedNames.Add(qualified) || !ExpectedChecks.TryGetValue(qualified, out var expected)) return false;
                    command.CommandText = CheckSql(table, check.Name);
                    if (await command.ExecuteScalarAsync(ct) is not string actual
                        || !MatchesCapturedCheck(qualified, expected, actual)) return false;
                }
            }
            // Verify the five extra parent unique indexes as physical indexes too;
            // the ownership manifest proves all eleven exact ordered FK bindings.
            foreach (var type in ProcurementPrincipalReceivingParentIndexes.ParentTypes)
            {
                var entity = model.FindEntityType(type);
                if (entity is null) return false;
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                var indexes = entity.GetIndexes().Where(ProcurementPrincipalReceivingParentIndexes.IsOwned).ToArray();
                if (indexes.Length != ProcurementPrincipalReceivingParentIndexes.ExpectedCount(type)) return false;
                foreach (var index in indexes)
                    if (!await True(IndexSql(table, index.Properties.Select(x => x.GetColumnName(table)!).ToArray(), index.GetDatabaseName()!, true))) return false;
            }
            // The M6B nullable invoice FKs add two real parent AK/index pairs.
            // GetKeys() above already includes them in exact counts; do not add
            // synthetic overlay counts. Verify the named tuples if B is applied.
            if(invoiceBindingsApplied)
                foreach(var (name,columns) in ProcurementPrincipalInvoiceMatchingReadiness.ParentKeys)
                {
                    var parent=StoreObjectIdentifier.Table(name.Contains("sources",StringComparison.Ordinal)
                        ?"principal_receiving_sources":"principal_receiving_facts","procurement");
                    if(!await True(KeySql(parent,columns,name,false))
                        || !await True(IndexSql(parent,columns,name,true)))return false;
                }
            return checkedNames.SetEquals(ExpectedChecks.Keys) && await True(PrincipalReceivingOwnershipConstraintSql.ManifestQuery);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
        finally { if (opened) await owner.Database.CloseConnectionAsync(); }
    }

    internal static bool MatchesCapturedCheck(string qualifiedName, string expected, string actual) =>
        ExpectedChecks.TryGetValue(qualifiedName, out var captured)
        && string.Equals(expected, captured, StringComparison.Ordinal)
        && (string.Equals(actual, captured, StringComparison.Ordinal)
            || ProcurementRestoreCheckAlternatives.Matches(qualifiedName, captured, actual));

    internal static readonly IReadOnlyDictionary<string,(string Name,string[] Columns)> ReturnOwnerKeys =
        new Dictionary<string,(string,string[])>(StringComparer.Ordinal)
        {
            ["principal_receiving_sources"]=("ak_pgret_source_owner",["tenant_id","legal_entity_id","id","branch_id","purchase_order_id","purchase_order_line_id","issued_version_id"]),
            ["principal_receiving_facts"]=("ak_pgret_fact_owner",["tenant_id","legal_entity_id","id","source_state_id","draft_id","branch_id"]),
            ["principal_receiving_drafts"]=("ak_pgret_receipt_owner",["tenant_id","legal_entity_id","id","branch_id","purchase_order_id","issued_version_id"]),
        };

    internal static string ExactShapeSql(IEntityType entity, StoreObjectIdentifier table,bool returnsApplied=false)
    {
        var relation = Literal(table.Schema + "." + table.Name) + "::regclass";
        var successorKey=returnsApplied && ReturnOwnerKeys.ContainsKey(table.Name)?1:0;
        return "SELECT (SELECT count(*) FROM pg_attribute WHERE attrelid=" + relation + " AND attnum>0 AND NOT attisdropped)=" + entity.GetProperties().Count()
            + " AND NOT EXISTS(SELECT 1 FROM pg_attrdef WHERE adrelid=" + relation + ")"
            + " AND (SELECT count(*) FROM pg_constraint WHERE conrelid=" + relation + " AND contype IN ('p','u','f','c'))="
            + (entity.GetKeys().Count() + entity.GetForeignKeys().Count() + entity.GetCheckConstraints().Count() + OverlayCounts[table.Name]+successorKey)
            + " AND (SELECT count(*) FROM pg_index WHERE indrelid=" + relation + ")=" + (entity.GetKeys().Count() + entity.GetIndexes().Count()+successorKey);
    }

    internal static string PrivilegeSql(bool write, string role)
    {
        string[] mutable = ["principal_receiving_sources", "principal_receiving_drafts", "purchase_order_lines", "purchase_request_lines"];
        var append = OwnedTables.Values.Except(mutable, StringComparer.Ordinal).Concat(["audit_events", "outbox_events"]).ToArray();
        var reads = OwnedTables.Values.Concat(["receiving_source_states", "goods_receipt_lines", "service_acceptances", "policy_versions", "purchase_order_lines", "purchase_request_lines", "__procurement_migrations"]).Distinct(StringComparer.Ordinal).ToArray();
        var clauses = reads.Select(x => "has_table_privilege(current_user," + Literal("procurement." + x) + ",'SELECT')").ToList();
        clauses.Add("session_user=current_user AND current_user=" + Literal(role));
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles r WHERE (r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls) AND (r.rolname=current_user OR pg_has_role(current_user,r.oid,'SET')))");
        clauses.Add("NOT has_database_privilege(current_user,current_database(),'CREATE')");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_database d WHERE d.datname=current_database() AND (pg_has_role(current_user,d.datdba,'MEMBER') OR pg_has_role(current_user,d.datdba,'SET')))");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_namespace n WHERE n.nspname IN ('procurement','core_approval') AND (has_schema_privilege(current_user,n.oid,'CREATE') OR pg_has_role(current_user,n.nspowner,'MEMBER') OR pg_has_role(current_user,n.nspowner,'SET')))");
        foreach (var name in reads)
        {
            var relation = Literal("procurement." + name);
            clauses.Add("NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid=" + relation + "::regclass AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))");
        }
        foreach (var name in append.Concat(mutable))
        {
            var relation = Literal("procurement." + name);
            foreach (var privilege in new[] { "DELETE", "TRUNCATE", "TRIGGER" })
                clauses.Add("NOT has_table_privilege(current_user," + relation + "," + Literal(privilege) + ")");
            if (append.Contains(name, StringComparer.Ordinal))
            {
                clauses.Add("NOT has_any_column_privilege(current_user," + relation + ",'UPDATE')");
                if (write) clauses.Add("has_table_privilege(current_user," + relation + ",'INSERT')");
            }
            else if (write)
            {
                if (name.StartsWith("principal_receiving_", StringComparison.Ordinal))
                    clauses.Add("has_table_privilege(current_user," + relation + ",'INSERT')");
                string[] columns = name switch
                {
                    "principal_receiving_sources" => ["sequence", "accepted_quantity", "accepted_net_amount", "version", "updated_at", "updated_by"],
                    "principal_receiving_drafts" => ["revision", "state", "snapshot_sha256", "version", "updated_at", "updated_by"],
                    _ => ["accepted_quantity", "version", "updated_at", "updated_by"],
                };
                foreach (var column in columns)
                    clauses.Add("has_column_privilege(current_user," + relation + "," + Literal(column) + ",'UPDATE')");
            }
        }
        return "SELECT " + string.Join(" AND ", clauses);
    }
}
