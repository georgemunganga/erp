using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Read-only RFQ store discovery. No DDL, role changes, policy/identity authority or
/// data backfill. CHECK definitions retain exact PostgreSQL17 returned grouping and casts.</summary>
public sealed class ProcurementSourcingDraftSchemaReadiness(IConfiguration configuration, IProcurementScope scope,
    IProcurementFinanceCheckerSchemaReadiness finance) : IProcurementSourcingDraftSchemaReadiness
{
    internal static readonly Type[] OwnedTypes = [typeof(SourcingEvent), typeof(SourcingDraftEvidence),
        typeof(SourcingDemandBinding), typeof(SourcingCommandReceipt)];

    // Exact pg_get_constraintdef(oid,false) from the actual upgraded private PG17 store.
    // Evidence: procurement-p28-postgres-20261021_071011/p28-checks.json; never strip grouping/casts.
    internal static readonly IReadOnlyDictionary<string, string> CheckDefinitions = new Dictionary<string, string>
    {
        ["ck_sourcing_owned_draft"] = "CHECK (((execution_version = ANY (ARRAY[0, 1])) AND (((execution_version = 0) AND (source_evidence_id IS NULL) AND (maker_worker_id IS NULL) AND (method_id IS NULL) AND (method_revision IS NULL) AND (original_draft_fingerprint IS NULL) AND (branch_ref IS NULL)) OR ((execution_version = 1) AND (source_evidence_id IS NOT NULL) AND (maker_worker_id IS NOT NULL) AND (maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (method_id IS NOT NULL) AND (method_revision IS NOT NULL) AND (method_revision > 0) AND (original_draft_fingerprint IS NOT NULL) AND ((original_draft_fingerprint)::text ~ '^[0-9a-f]{64}$'::text) AND ((status)::text = ANY ((ARRAY['Draft'::character varying, 'Cancelled'::character varying])::text[]))))))",
        ["ck_rfq_original_envelope"] = "CHECK (((schema_version = 1) AND ((payload_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((original_draft_fingerprint)::text ~ '^[0-9a-f]{64}$'::text) AND (maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (create_command_key <> '00000000-0000-0000-0000-000000000000'::uuid)))",
        ["ck_rfq_binding_value"] = "CHECK ((((target_type)::text = 'SourcingEvent'::text) AND (quantity > (0)::numeric) AND (unit_estimate >= (0)::numeric) AND (estimated_value = (quantity * unit_estimate)) AND (request_version >= submitted_version) AND (submitted_version > 0) AND (method_revision > 0) AND ((minimum_quote_count >= 0) AND (minimum_quote_count <= 20)) AND (maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (requester_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)))",
        ["ck_rfq_receipt_identity"] = "CHECK ((((operation)::text = ANY ((ARRAY['Create'::character varying, 'Cancel'::character varying])::text[])) AND (command_key <> '00000000-0000-0000-0000-000000000000'::uuid) AND (actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (response_schema_version = 1) AND (result_version > 0) AND ((command_fingerprint)::text ~ '^[0-9a-f]{64}$'::text) AND ((response_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((((operation)::text = 'Create'::text) AND (response_status_code = 201)) OR (((operation)::text = 'Cancel'::text) AND (response_status_code = 200)))))",
    };

    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        try
        {
            var connection = configuration.GetConnectionString("ProcurementApprovalExecution");
            if (string.IsNullOrWhiteSpace(connection)) return false;
            await using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql(connection, x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, scope);
            return await IsReadyAsync(owner, true, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
    }

    public async Task<bool> IsReadyAsync(ProcurementDbContext owner, bool write, CancellationToken ct)
    {
        if (!owner.Database.IsNpgsql()) return false;
        var opened = false;
        try
        {
            if (owner.Database.HasPendingModelChanges() || (await owner.Database.GetPendingMigrationsAsync(ct)).Any()
                || !await finance.IsReadyAsync(owner, false, ct)) return false;
            var connection = owner.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) { await owner.Database.OpenConnectionAsync(ct); opened = true; }
            await using var command = connection.CreateCommand();
            command.Transaction = owner.Database.CurrentTransaction?.GetDbTransaction(); command.CommandTimeout = 10;
            async Task<bool> True(string sql)
            { command.CommandText = sql; return await command.ExecuteScalarAsync(ct) is true; }
            if (!await True(PrivilegeSql(write)) || !await True(SourcingDraftOwnershipConstraintSql.ManifestQuery)) return false;
            var design = owner.GetService<IDesignTimeModel>().Model;
            foreach (var type in OwnedTypes)
            {
                var entity = design.FindEntityType(type)!;
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                // SELECT every original field, not only new columns: finite partial grants must
                // not advertise a feature whose canonical tuple cannot be materialized.
                command.CommandText = "SELECT " + string.Join(',', entity.GetProperties().Select(x => Quote(x.GetColumnName(table)!)))
                    + " FROM " + Quote(table.Schema!) + "." + Quote(table.Name) + " LIMIT 0";
                await using (var result = await command.ExecuteReaderAsync(ct)) { }
                var columnPredicates = new List<string>();
                foreach (var property in entity.GetProperties())
                {
                    columnPredicates.Add("EXISTS(SELECT 1 FROM pg_attribute a WHERE a.attrelid="
                        + Literal(table.Schema + "." + table.Name) + "::regclass AND a.attname="
                        + Literal(property.GetColumnName(table)!) + " AND NOT a.attisdropped AND a.attidentity='' AND a.attgenerated='' AND a.attnotnull="
                        + (property.IsNullable ? "false" : "true") + " AND format_type(a.atttypid,a.atttypmod)="
                        + Literal(property.GetColumnType()!) + ")");
                }
                if (!await True("SELECT " + string.Join(" AND ", columnPredicates))) return false;
                command.CommandText = "SELECT EXISTS(SELECT 1 FROM pg_attribute a JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum WHERE a.attrelid='procurement.sourcing_events'::regclass AND a.attname='execution_version' AND pg_get_expr(d.adbin,d.adrelid)='0')";
                if (await command.ExecuteScalarAsync(ct) is not true) return false;
                foreach (var check in entity.GetCheckConstraints())
                {
                    if (!CheckDefinitions.TryGetValue(check.Name, out var expected)) return false;
                    command.CommandText = "SELECT pg_get_constraintdef(oid,false) FROM pg_constraint WHERE conrelid="
                        + Literal(table.Schema + "." + table.Name) + "::regclass AND conname=" + Literal(check.Name)
                        + " AND contype='c' AND convalidated AND conislocal";
                    if (await command.ExecuteScalarAsync(ct) is not string actual || actual != expected) return false;
                }
                foreach (var foreign in entity.GetForeignKeys())
                {
                    var principal = StoreObjectIdentifier.Table(foreign.PrincipalEntityType.GetTableName()!, foreign.PrincipalEntityType.GetSchema());
                    var childColumns = foreign.Properties.Select(x => x.GetColumnName(table)!).ToArray();
                    var parentColumns = foreign.PrincipalKey.Properties.Select(x => x.GetColumnName(principal)!).ToArray();
                    if (!await True(ForeignKeySql(table, principal, childColumns, parentColumns))) return false;
                }
                foreach (var index in entity.GetIndexes().Where(x => x.IsUnique))
                    if (!await True(UniqueIndexSql(table, index.Properties.Select(x => x.GetColumnName(table)!).ToArray()))) return false;
            }
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
        finally { if (opened) await owner.Database.CloseConnectionAsync(); }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string Literal(string value) => "'" + value.Replace("'", "''") + "'";
    private static string Columns(IEnumerable<string> values) => "ARRAY[" + string.Join(',', values.Select(Literal)) + "]::text[]";
    private static string ForeignKeySql(StoreObjectIdentifier child, StoreObjectIdentifier parent, string[] from, string[] to) =>
        "SELECT EXISTS(SELECT 1 FROM pg_constraint c WHERE c.contype='f' AND c.convalidated AND NOT c.condeferrable AND NOT c.condeferred AND c.confdeltype='r' AND c.confupdtype='a' AND c.confmatchtype='s' AND c.conrelid="
        + Literal(child.Schema + "." + child.Name) + "::regclass AND c.confrelid=" + Literal(parent.Schema + "." + parent.Name)
        + "::regclass AND ARRAY(SELECT a.attname::text FROM unnest(c.conkey) WITH ORDINALITY k(n,p) JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.n ORDER BY k.p)=" + Columns(from)
        + " AND ARRAY(SELECT a.attname::text FROM unnest(c.confkey) WITH ORDINALITY k(n,p) JOIN pg_attribute a ON a.attrelid=c.confrelid AND a.attnum=k.n ORDER BY k.p)=" + Columns(to) + ")";
    private static string UniqueIndexSql(StoreObjectIdentifier table, string[] columns) =>
        "SELECT EXISTS(SELECT 1 FROM pg_index i WHERE i.indrelid=" + Literal(table.Schema + "." + table.Name)
        + "::regclass AND i.indisunique AND i.indisvalid AND i.indimmediate AND i.indexprs IS NULL AND i.indpred IS NULL AND i.indnkeyatts=i.indnatts AND ARRAY(SELECT a.attname::text FROM unnest(i.indkey::smallint[]) WITH ORDINALITY k(n,p) JOIN pg_attribute a ON a.attrelid=i.indrelid AND a.attnum=k.n ORDER BY k.p)=" + Columns(columns) + ")";

    internal static string PrivilegeSql(bool write)
    {
        string[] reads = ["sourcing_events", "sourcing_lines", "sourcing_draft_evidence", "sourcing_demand_bindings", "sourcing_command_receipts",
            "purchase_requests", "purchase_request_lines", "purchase_request_allocations", "purchase_request_approval_submissions",
            "purchase_request_approval_outcomes", "policy_evaluations", "policy_versions", "budget_check_snapshots", "procurement_methods",
            "purchase_orders", "purchase_order_lines", "catalog_items", "catalog_entries", "suppliers", "audit_events", "outbox_events"];
        string[] append = ["sourcing_draft_evidence", "sourcing_demand_bindings", "sourcing_command_receipts", "audit_events", "outbox_events"];
        var clauses = reads.Select(x => "has_table_privilege(current_user," + Literal("procurement." + x) + ",'SELECT')").ToList();
        clauses.AddRange(new[] { "cases", "stages", "assignments", "decisions" }.Select(x =>
            "has_table_privilege(current_user," + Literal("core_approval." + x) + ",'SELECT')"));
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))");
        clauses.Add("NOT has_schema_privilege(current_user,'procurement','CREATE')");
        clauses.AddRange(append.Select(x => "NOT has_table_privilege(current_user," + Literal("procurement." + x) + ",'UPDATE') AND NOT has_table_privilege(current_user," + Literal("procurement." + x) + ",'DELETE')"));
        if (write)
        {
            clauses.AddRange(append.Select(x => "has_table_privilege(current_user," + Literal("procurement." + x) + ",'INSERT')"));
            clauses.AddRange(new[] { "sourcing_events", "sourcing_lines", "purchase_request_allocations" }.Select(x =>
                "has_table_privilege(current_user," + Literal("procurement." + x) + ",'INSERT') AND has_table_privilege(current_user," + Literal("procurement." + x) + ",'UPDATE')"));
            clauses.Add("has_table_privilege(current_user,'procurement.purchase_request_lines','UPDATE')");
            clauses.Add("has_table_privilege(current_user,'procurement.purchase_requests','UPDATE')");
        }
        return "SELECT " + string.Join(" AND ", clauses);
    }
}
