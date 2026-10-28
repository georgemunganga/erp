using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Hrm.Api.Routing;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Read-only admission on the actual P30 execution connection. No
/// configuration switch, migration row, or owner login alone can enable P30.</summary>
public sealed class ProcurementPrincipalOrderDraftSchemaReadiness(
    IConfiguration configuration, IProcurementScope scope,
    ProcurementPrincipalApprovalStoreReadiness p25,
    IProcurementSourcingDraftSchemaReadiness p29) : IProcurementPrincipalOrderDraftReadiness
{
    internal const string MigrationId = "20261026074403_M3bPrincipalDirectOrderDraft";
    internal static readonly Type[] OwnedTypes = [typeof(PrincipalOrderSourceBinding), typeof(PrincipalOrderDraftReceipt)];
    internal static readonly string[] NewCheckNames =
        ["ck_po_principal_draft_version", "ck_po_principal_binding", "ck_po_principal_receipt"];

    // Captured from the genuine private PostgreSQL 17 Up/empty Down/Up rehearsal.
    // Compare full physical definitions; do not infer them from the EF source SQL.
    internal static readonly IReadOnlyDictionary<string, string> CapturedCheckDefinitions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ck_po_principal_draft_version"] = """
CHECK ((((principal_draft_schema_version = 0) AND (original_direct_source_sha256 IS NULL)) OR ((principal_draft_schema_version = 2) AND (branch_ref IS NOT NULL) AND (buyer_subject_id IS NOT NULL) AND (buyer_subject_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND (buyer_subject_id <> '00000000-0000-0000-0000-000000000000'::text) AND (original_direct_source_sha256 IS NOT NULL) AND ((original_direct_source_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((status)::text = ANY ((ARRAY['Draft'::character varying, 'Cancelled'::character varying])::text[])) AND (finance_commitment_ref IS NULL) AND (approval_instance_ref IS NULL) AND (submitted_at IS NULL) AND (issued_at IS NULL))))
""",
            ["ck_po_principal_binding"] = """
CHECK (((schema_version = 2) AND (quantity > (0)::numeric) AND (unit_estimate >= (0)::numeric) AND (proposed_unit_price >= (0)::numeric) AND (estimated_value = (quantity * proposed_unit_price)) AND (request_version >= submitted_business_version) AND (submitted_business_version > 0) AND (method_revision > 0) AND (buyer_principal_id IS NOT NULL) AND ((buyer_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((buyer_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (requester_principal_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((buyer_worker_alias_id IS NULL) OR (buyer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((requester_worker_alias_id IS NULL) OR (requester_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((original_direct_source_sha256)::text ~ '^[0-9a-f]{64}$'::text)))
""",
            ["ck_po_principal_receipt"] = """
CHECK (((schema_version = 2) AND ((operation)::text = ANY ((ARRAY['Create'::character varying, 'Cancel'::character varying])::text[])) AND (command_key <> '00000000-0000-0000-0000-000000000000'::uuid) AND (actor_principal_id IS NOT NULL) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((command_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((response_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (result_version > 0) AND ((((operation)::text = 'Create'::text) AND (response_status = 201)) OR (((operation)::text = 'Cancel'::text) AND (response_status = 200)))))
""",
        };
    internal static bool ManifestCaptured => CapturedCheckDefinitions.Count == NewCheckNames.Length
        && NewCheckNames.All(CapturedCheckDefinitions.ContainsKey);

    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        if (!ManifestCaptured || !ConnectionBoundary(out _)) return false;
        try
        {
            var execution = configuration.GetConnectionString("ProcurementApprovalExecution")!;
            await using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql(execution, x => x.MigrationsHistoryTable("__procurement_migrations", "procurement"))
                .Options, scope);
            return await IsReadyAsync(owner, false, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
    }

    public async Task<bool> IsReadyAsync(ProcurementDbContext owner, bool write, CancellationToken ct)
    {
        if (!ManifestCaptured || !ConnectionBoundary(out var executionRole) || !owner.Database.IsNpgsql()) return false;
        var opened = false;
        try
        {
            if (owner.Database.HasPendingModelChanges()
                || (await owner.Database.GetPendingMigrationsAsync(ct)).Any()
                || !(await owner.Database.GetAppliedMigrationsAsync(ct)).Contains(MigrationId, StringComparer.Ordinal)
                || !await p25.CanExecuteAsync(owner, ct)
                || !await p29.IsReadyAsync(owner, false, ct)) return false;

            var connection = owner.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) { await owner.Database.OpenConnectionAsync(ct); opened = true; }
            await using var command = connection.CreateCommand();
            command.Transaction = owner.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandTimeout = 10;
            async Task<bool> True(string sql)
            { command.CommandText = sql; return await command.ExecuteScalarAsync(ct) is true; }

            if (!await True(PrivilegeSql(write, executionRole)) || !await True(VersionDefaultSql)) return false;
            var model = owner.GetService<IDesignTimeModel>().Model;
            var order = model.FindEntityType(typeof(PurchaseOrder));
            if (order is null || order.GetSchema() != "procurement") return false;
            var owned = OwnedTypes.Select(type => model.FindEntityType(type)).ToArray();
            if (owned.Any(x => x is null)
                || owned.Sum(x => x!.GetKeys().Count()) != 4 // 2 PK + 2 scoped AK
                || owned.Sum(x => x!.GetForeignKeys().Count()) != 9
                || owned.Sum(x => x!.GetIndexes().Count(index => !ProcurementPrincipalReceivingParentIndexes.IsOwned(index))) != 10) return false;
            var orderTable = StoreObjectIdentifier.Table(order.GetTableName()!, order.GetSchema());
            command.CommandText = "SELECT principal_draft_schema_version,original_direct_source_sha256 FROM "
                + Quote(orderTable.Schema!) + "." + Quote(orderTable.Name) + " LIMIT 0";
            await using (var reader = await command.ExecuteReaderAsync(ct)) { }
            var checks = new HashSet<string>(StringComparer.Ordinal);
            foreach (var type in OwnedTypes)
            {
                var entity = model.FindEntityType(type);
                if (entity is null || entity.GetSchema() != "procurement") return false;
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                command.CommandText = SelectAllColumnsSql(entity, table);
                await using (var reader = await command.ExecuteReaderAsync(ct)) { }
                if (!await True(ColumnsSql(entity, table))) return false;
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
                        foreign.GetConstraintName()))) return false;
                }
                foreach (var index in entity.GetIndexes())
                    if (index.GetFilter() is not null || !await True(IndexSql(table,
                        index.Properties.Select(x => x.GetColumnName(table)!).ToArray(),
                        index.GetDatabaseName()!, index.IsUnique))) return false;
                foreach (var check in entity.GetCheckConstraints())
                    checks.Add(check.Name);
            }
            if (!order.GetCheckConstraints().Any(x => x.Name == "ck_po_principal_draft_version")) return false;
            checks.Add("ck_po_principal_draft_version");
            if (!checks.SetEquals(NewCheckNames) || !checks.SetEquals(CapturedCheckDefinitions.Keys)) return false;
            var usesGovernanceShape = false;
            foreach (var (name, expected) in CapturedCheckDefinitions)
            {
                var table = name == "ck_po_principal_draft_version" ? orderTable
                    : StoreObjectIdentifier.Table(name == "ck_po_principal_binding"
                        ? "principal_order_source_bindings" : "principal_order_draft_receipts", "procurement");
                command.CommandText = CheckSql(table, name);
                if (await command.ExecuteScalarAsync(ct) is not string actual) return false;
                if (actual == expected) continue;
                if (name is not ("ck_po_principal_draft_version" or "ck_po_principal_receipt")
                    || !ProcurementPrincipalOrderGovernancePhysicalSchema.MatchesCapturedCheck(
                        table.Schema + "." + table.Name + "." + name, actual)) return false;
                usesGovernanceShape = true;
            }
            // The old cancellation path reads retained funding evidence. Do not
            // admit the broader constraint text if the owning M5 store is absent
            // or the current execution role cannot safely read it.
            return !usesGovernanceShape || await ProcurementPrincipalOrderGovernancePhysicalSchema
                .InspectAsync(owner, false, executionRole, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
        finally { if (opened) await owner.Database.CloseConnectionAsync(); }
    }

    internal static string PrivilegeSql(bool write, string expectedRole)
    {
        string[] reads = ["purchase_orders", "purchase_order_lines", "purchase_request_allocations",
            "purchase_requests", "purchase_request_lines", "purchase_request_approval_submissions",
            "purchase_request_approval_outcomes", "budget_check_snapshots", "procurement_methods",
            "suppliers", "supplier_qualifications", "catalog_items", "catalog_entries",
            "principal_order_source_bindings", "principal_order_draft_receipts", "audit_events", "outbox_events"];
        string[] append = ["principal_order_source_bindings", "principal_order_draft_receipts",
            "audit_events", "outbox_events"];
        var clauses = reads.Select(x => "has_table_privilege(current_user," + Literal("procurement." + x) + ",'SELECT')").ToList();
        clauses.Add("session_user=current_user");
        clauses.Add("current_user=" + Literal(expectedRole));
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND "
            + "(rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))");
        clauses.Add("NOT has_schema_privilege(current_user,'procurement','CREATE')");
        clauses.Add("NOT has_schema_privilege(current_user,'core_approval','CREATE')");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_namespace n WHERE n.nspname IN ('procurement','core_approval') "
            + "AND (pg_has_role(current_user,n.nspowner,'MEMBER') OR pg_has_role(current_user,n.nspowner,'SET')))");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace "
            + "WHERE n.nspname='procurement' AND c.relname IN "
            + "('purchase_orders','purchase_order_lines','purchase_request_allocations',"
            + "'principal_order_source_bindings','principal_order_draft_receipts','audit_events','outbox_events') "
            + "AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))");
        clauses.Add("has_column_privilege(current_user,'hrm.work_locations','name','SELECT')");
        foreach (var table in append)
        {
            clauses.Add("NOT has_any_column_privilege(current_user," + Literal("procurement." + table) + ",'UPDATE')");
            clauses.Add("NOT has_table_privilege(current_user," + Literal("procurement." + table) + ",'DELETE')");
            clauses.Add("NOT has_table_privilege(current_user," + Literal("procurement." + table) + ",'TRUNCATE')");
            clauses.Add("NOT has_table_privilege(current_user," + Literal("procurement." + table) + ",'TRIGGER')");
            if (write) clauses.Add("has_table_privilege(current_user," + Literal("procurement." + table) + ",'INSERT')");
        }
        if (write)
        {
            foreach (var table in new[] { "purchase_orders", "purchase_order_lines", "purchase_request_allocations" })
            {
                clauses.Add("has_table_privilege(current_user," + Literal("procurement." + table) + ",'INSERT')");
                clauses.Add("has_table_privilege(current_user," + Literal("procurement." + table) + ",'UPDATE')");
            }
            foreach (var table in new[] { "purchase_requests", "purchase_request_lines" })
                clauses.Add("has_table_privilege(current_user," + Literal("procurement." + table) + ",'UPDATE')");
        }
        return "SELECT " + string.Join(" AND ", clauses);
    }

    internal const string VersionDefaultSql = """
        SELECT EXISTS(SELECT 1 FROM pg_attribute a JOIN pg_attrdef d
          ON d.adrelid=a.attrelid AND d.adnum=a.attnum
          WHERE a.attrelid='procurement.purchase_orders'::regclass
            AND a.attname='principal_draft_schema_version' AND NOT a.attisdropped
            AND a.attnotnull AND pg_get_expr(d.adbin,d.adrelid)='0')
          AND EXISTS(SELECT 1 FROM pg_attribute a
          WHERE a.attrelid='procurement.purchase_orders'::regclass
            AND a.attname='original_direct_source_sha256' AND NOT a.attisdropped
            AND NOT a.attnotnull AND format_type(a.atttypid,a.atttypmod)='character varying(64)')
        """;

    private bool ConnectionBoundary(out string executionRole) =>
        TryExecutionConnection(configuration, out executionRole);

    internal static bool TryExecutionConnection(IConfiguration configuration, out string executionRole)
    {
        executionRole = string.Empty;
        var record = configuration.GetConnectionString("Procurement");
        var execution = configuration.GetConnectionString("ProcurementApprovalExecution");
        if (string.IsNullOrWhiteSpace(record) || string.IsNullOrWhiteSpace(execution)
            || !ProcurementMasterImportRoutes.SameDatabaseEndpoints(record, false, null, 0,
                execution, false, null, 0)) return false;
        try
        {
            static NpgsqlConnectionStringBuilder Settings(string value) => new(
                value.StartsWith("postgresql", StringComparison.OrdinalIgnoreCase) && value.Contains('?')
                    ? NpgsqlConnectionStringNormalizer.Normalize(value) : value);
            var r = Settings(record);
            var e = Settings(execution);
            if (string.IsNullOrWhiteSpace(r.Username) || string.IsNullOrWhiteSpace(e.Username)
                || string.Equals(r.Username, e.Username, StringComparison.Ordinal)) return false;
            executionRole = e.Username;
            return true;
        }
        catch (Exception error) when (error is ArgumentException or FormatException) { return false; }
    }
}
