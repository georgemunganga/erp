using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

public enum InvoiceCurrencySchemaMode { RecordRead, RecordCapture, ExecutionRead }

/// <summary>Exact applied M6D native shape and actual trusted finite connection role.
/// Flags/authorization remain at the owning route. Metadata-only checks may run
/// before its business transaction; any existing transaction is preserved.</summary>
public static class ProcurementInvoiceCurrencySchemaReadiness
{
    // Both genuine Procurement profiles were captured and compared exactly on
    // the private native target. Runtime admission still requires all applied
    // migration IDs and the actual finite configured role's effective privileges.
    public static bool FullProfilePopulated => true;
    public const string CoreMigrationId = CoreCurrencyPrecisionSchemaReadiness.MigrationId;
    public const string ReceiptMigrationId = "20261030042444_M6dInvoiceIntakeReceipts";
    public const string ProcurementMigrationId = "20261030035821_M6dInvoiceAmountCalculations";
    public const string NativeMetadataSha256 = "06c72bd4417ad60f82b66ea0dd8778752439e4ff2d0d1bc45c61bb2752c7d354";
    private const string NativeShapeJson = """
{"procurement.invoice_amount_calculations":{"columns":[{"collation":"-","default":null,"generated":"","identity":"","name":"id","notNull":true,"type":"uuid"},{"collation":"-","default":null,"generated":"","identity":"","name":"supplier_invoice_id","notNull":true,"type":"uuid"},{"collation":"-","default":null,"generated":"","identity":"","name":"schema_version","notNull":true,"type":"integer"},{"collation":"-","default":null,"generated":"","identity":"","name":"core_currency_reference_id","notNull":true,"type":"uuid"},{"collation":"-","default":null,"generated":"","identity":"","name":"core_currency_reference_revision","notNull":true,"type":"integer"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"currency_code","notNull":true,"type":"character varying(3)"},{"collation":"-","default":null,"generated":"","identity":"","name":"minor_units","notNull":true,"type":"integer"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"rounding_mode","notNull":true,"type":"character varying(30)"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"core_source_payload_sha256","notNull":true,"type":"character varying(64)"},{"collation":"-","default":null,"generated":"","identity":"","name":"core_published_at_utc","notNull":true,"type":"timestamp with time zone"},{"collation":"-","default":null,"generated":"","identity":"","name":"observed_at_utc","notNull":true,"type":"timestamp with time zone"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"invoice_original_sha256","notNull":true,"type":"character varying(64)"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"calculation_json","notNull":true,"type":"text"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"calculation_policy_sha256","notNull":true,"type":"character varying(64)"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"calculation_sha256","notNull":true,"type":"character varying(64)"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"tenant_id","notNull":true,"type":"character varying(128)"},{"collation":"-","default":null,"generated":"","identity":"","name":"legal_entity_id","notNull":true,"type":"uuid"},{"collation":"-","default":null,"generated":"","identity":"","name":"created_at","notNull":true,"type":"timestamp with time zone"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"created_by","notNull":true,"type":"character varying(200)"},{"collation":"-","default":null,"generated":"","identity":"","name":"updated_at","notNull":false,"type":"timestamp with time zone"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"updated_by","notNull":false,"type":"character varying(200)"},{"collation":"-","default":null,"generated":"","identity":"","name":"version","notNull":true,"type":"bigint"}],"constraints":[{"deferrable":false,"deferred":false,"definition":"UNIQUE (tenant_id, legal_entity_id, id)","inheritCount":0,"kind":"u","local":true,"name":"AK_invoice_amount_calculations_tenant_id_legal_entity_id_id","noInherit":true,"validated":true},{"deferrable":false,"deferred":false,"definition":"FOREIGN KEY (tenant_id, legal_entity_id, supplier_invoice_id) REFERENCES procurement.supplier_invoices(tenant_id, legal_entity_id, id) ON DELETE RESTRICT","inheritCount":0,"kind":"f","local":true,"name":"FK_invoice_amount_calculations_supplier_invoices_tenant_id_leg~","noInherit":true,"validated":true},{"deferrable":false,"deferred":false,"definition":"PRIMARY KEY (id)","inheritCount":0,"kind":"p","local":true,"name":"PK_invoice_amount_calculations","noInherit":true,"validated":true},{"deferrable":false,"deferred":false,"definition":"CHECK (((schema_version = 2) AND (version = 1) AND (supplier_invoice_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (core_currency_reference_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (core_currency_reference_revision > 0) AND ((currency_code)::text ~ '^[A-Z]{3}$'::text) AND ((minor_units >= 0) AND (minor_units <= 4)) AND ((rounding_mode)::text = ANY ((ARRAY['ToEven'::character varying, 'AwayFromZero'::character varying])::text[])) AND ((core_source_payload_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((invoice_original_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((calculation_policy_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((calculation_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((octet_length(calculation_json) >= 1) AND (octet_length(calculation_json) <= 262144)) AND isfinite(core_published_at_utc) AND isfinite(observed_at_utc) AND (core_published_at_utc <= observed_at_utc) AND (created_at >= observed_at_utc) AND (updated_at IS NULL) AND (updated_by IS NULL)))","inheritCount":0,"kind":"c","local":true,"name":"ck_invoice_amount_calculation","noInherit":false,"validated":true}],"forceRls":false,"indexes":[{"definition":"CREATE UNIQUE INDEX \"AK_invoice_amount_calculations_tenant_id_legal_entity_id_id\" ON procurement.invoice_amount_calculations USING btree (tenant_id, legal_entity_id, id)","immediate":true,"live":true,"name":"AK_invoice_amount_calculations_tenant_id_legal_entity_id_id","ready":true,"valid":true},{"definition":"CREATE UNIQUE INDEX \"PK_invoice_amount_calculations\" ON procurement.invoice_amount_calculations USING btree (id)","immediate":true,"live":true,"name":"PK_invoice_amount_calculations","ready":true,"valid":true},{"definition":"CREATE UNIQUE INDEX ux_invoice_amount_calculation_invoice ON procurement.invoice_amount_calculations USING btree (tenant_id, legal_entity_id, supplier_invoice_id)","immediate":true,"live":true,"name":"ux_invoice_amount_calculation_invoice","ready":true,"valid":true}],"kind":"r","nonInternalTriggers":0,"persistence":"p","rls":false},"procurement.invoice_intake_command_receipts":{"columns":[{"collation":"-","default":null,"generated":"","identity":"","name":"id","notNull":true,"type":"uuid"},{"collation":"-","default":null,"generated":"","identity":"","name":"supplier_invoice_id","notNull":true,"type":"uuid"},{"collation":"-","default":null,"generated":"","identity":"","name":"calculation_id","notNull":true,"type":"uuid"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"operation","notNull":true,"type":"character varying(32)"},{"collation":"-","default":null,"generated":"","identity":"","name":"command_key","notNull":true,"type":"uuid"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"actor_principal_id","notNull":true,"type":"character varying(200)"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"request_sha256","notNull":true,"type":"character varying(64)"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"response_json","notNull":true,"type":"text"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"response_sha256","notNull":true,"type":"character varying(64)"},{"collation":"-","default":null,"generated":"","identity":"","name":"response_status","notNull":true,"type":"integer"},{"collation":"-","default":null,"generated":"","identity":"","name":"completed_at_utc","notNull":true,"type":"timestamp with time zone"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"tenant_id","notNull":true,"type":"character varying(128)"},{"collation":"-","default":null,"generated":"","identity":"","name":"legal_entity_id","notNull":true,"type":"uuid"},{"collation":"-","default":null,"generated":"","identity":"","name":"created_at","notNull":true,"type":"timestamp with time zone"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"created_by","notNull":true,"type":"character varying(200)"},{"collation":"-","default":null,"generated":"","identity":"","name":"updated_at","notNull":false,"type":"timestamp with time zone"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"updated_by","notNull":false,"type":"character varying(200)"},{"collation":"-","default":null,"generated":"","identity":"","name":"version","notNull":true,"type":"bigint"}],"constraints":[{"deferrable":false,"deferred":false,"definition":"UNIQUE (tenant_id, legal_entity_id, id)","inheritCount":0,"kind":"u","local":true,"name":"AK_invoice_intake_command_receipts_tenant_id_legal_entity_id_id","noInherit":true,"validated":true},{"deferrable":false,"deferred":false,"definition":"FOREIGN KEY (tenant_id, legal_entity_id, calculation_id) REFERENCES procurement.invoice_amount_calculations(tenant_id, legal_entity_id, id) ON DELETE RESTRICT","inheritCount":0,"kind":"f","local":true,"name":"FK_invoice_intake_command_receipts_invoice_amount_calculations~","noInherit":true,"validated":true},{"deferrable":false,"deferred":false,"definition":"FOREIGN KEY (tenant_id, legal_entity_id, supplier_invoice_id) REFERENCES procurement.supplier_invoices(tenant_id, legal_entity_id, id) ON DELETE RESTRICT","inheritCount":0,"kind":"f","local":true,"name":"FK_invoice_intake_command_receipts_supplier_invoices_tenant_id~","noInherit":true,"validated":true},{"deferrable":false,"deferred":false,"definition":"PRIMARY KEY (id)","inheritCount":0,"kind":"p","local":true,"name":"PK_invoice_intake_command_receipts","noInherit":true,"validated":true},{"deferrable":false,"deferred":false,"definition":"CHECK ((((operation)::text = 'InvoiceCaptureV2'::text) AND (version = 1) AND (response_status = 201) AND (command_key <> '00000000-0000-0000-0000-000000000000'::uuid) AND (supplier_invoice_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (calculation_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((request_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((response_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((octet_length(response_json) >= 1) AND (octet_length(response_json) <= 262144)) AND isfinite(completed_at_utc) AND (created_at >= completed_at_utc) AND (updated_at IS NULL) AND (updated_by IS NULL)))","inheritCount":0,"kind":"c","local":true,"name":"ck_invoice_intake_receipt_v2","noInherit":false,"validated":true}],"forceRls":false,"indexes":[{"definition":"CREATE UNIQUE INDEX \"AK_invoice_intake_command_receipts_tenant_id_legal_entity_id_id\" ON procurement.invoice_intake_command_receipts USING btree (tenant_id, legal_entity_id, id)","immediate":true,"live":true,"name":"AK_invoice_intake_command_receipts_tenant_id_legal_entity_id_id","ready":true,"valid":true},{"definition":"CREATE INDEX \"IX_invoice_intake_command_receipts_tenant_id_legal_entity_id_s~\" ON procurement.invoice_intake_command_receipts USING btree (tenant_id, legal_entity_id, supplier_invoice_id)","immediate":true,"live":true,"name":"IX_invoice_intake_command_receipts_tenant_id_legal_entity_id_s~","ready":true,"valid":true},{"definition":"CREATE UNIQUE INDEX \"PK_invoice_intake_command_receipts\" ON procurement.invoice_intake_command_receipts USING btree (id)","immediate":true,"live":true,"name":"PK_invoice_intake_command_receipts","ready":true,"valid":true},{"definition":"CREATE UNIQUE INDEX ux_invoice_intake_receipt_actor_key ON procurement.invoice_intake_command_receipts USING btree (tenant_id, legal_entity_id, actor_principal_id, command_key)","immediate":true,"live":true,"name":"ux_invoice_intake_receipt_actor_key","ready":true,"valid":true},{"definition":"CREATE UNIQUE INDEX ux_invoice_intake_receipt_calculation ON procurement.invoice_intake_command_receipts USING btree (tenant_id, legal_entity_id, calculation_id)","immediate":true,"live":true,"name":"ux_invoice_intake_receipt_calculation","ready":true,"valid":true}],"kind":"r","nonInternalTriggers":0,"persistence":"p","rls":false}}
""";
    private const string NativeShapeSql = """
SELECT jsonb_object_agg(n.nspname||'.'||c.relname,jsonb_build_object(
'kind',c.relkind,'persistence',c.relpersistence,'rls',c.relrowsecurity,'forceRls',c.relforcerowsecurity,
'columns',(SELECT jsonb_agg(jsonb_build_object('name',a.attname,'type',format_type(a.atttypid,a.atttypmod),'notNull',a.attnotnull,'generated',a.attgenerated,'identity',a.attidentity,'collation',a.attcollation::regcollation::text,'default',pg_get_expr(d.adbin,d.adrelid)) ORDER BY a.attnum) FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum WHERE a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped),
'constraints',(SELECT jsonb_agg(jsonb_build_object('name',q.conname,'kind',q.contype,'definition',pg_get_constraintdef(q.oid,false),'validated',q.convalidated,'deferrable',q.condeferrable,'deferred',q.condeferred,'local',q.conislocal,'inheritCount',q.coninhcount,'noInherit',q.connoinherit) ORDER BY q.conname) FROM pg_constraint q WHERE q.conrelid=c.oid),
'indexes',(SELECT jsonb_agg(jsonb_build_object('name',i.relname,'definition',pg_get_indexdef(x.indexrelid),'valid',x.indisvalid,'ready',x.indisready,'live',x.indislive,'immediate',x.indimmediate) ORDER BY i.relname) FROM pg_index x JOIN pg_class i ON i.oid=x.indexrelid WHERE x.indrelid=c.oid),
'nonInternalTriggers',(SELECT count(*) FROM pg_trigger WHERE tgrelid=c.oid AND NOT tgisinternal)
)) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.oid=ANY(ARRAY[to_regclass('procurement.invoice_amount_calculations'),to_regclass('procurement.invoice_intake_command_receipts')])
""";

    public static async Task<bool> IsReadyAsync(ProcurementDbContext db, IConfiguration configuration,
        InvoiceCurrencySchemaMode mode, CancellationToken ct)
    {
        if (db is null || !db.Database.IsNpgsql() || !Enum.IsDefined(mode) || !FullProfilePopulated) return false;
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        try
        {
            var trusted = configuration.GetConnectionString(mode == InvoiceCurrencySchemaMode.ExecutionRead
                ? "ProcurementApprovalExecution" : "Procurement");
            if (string.IsNullOrWhiteSpace(trusted)
                || !InvoiceDocumentStoreBinding.SamePhysicalDatabase(trusted, connection.ConnectionString)) return false;
            var expected = new NpgsqlConnectionStringBuilder(trusted);
            var actual = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
            if (string.IsNullOrWhiteSpace(expected.Username) || actual.Username != expected.Username) return false;
            if (openedHere) await connection.OpenAsync(ct);
            var transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction; command.CommandTimeout = 15;
            async Task<bool> True(string sql)
            { command.CommandText = sql; return await command.ExecuteScalarAsync(ct) is true; }
            if (!await True(PrivilegeSql(mode, expected.Username))) return false;
            if (!await NativeShapeReadyAsync(connection, transaction, false, ct)) return false;
            if (ProcurementCompiledModelParity.HasPendingModelChanges(db) || (await db.Database.GetPendingMigrationsAsync(ct)).Any()) return false;
            return await True("SELECT EXISTS(SELECT 1 FROM hrm.__hrm_migrations WHERE \"MigrationId\"="
                + Literal(CoreMigrationId) + ") AND EXISTS(SELECT 1 FROM procurement.__procurement_migrations WHERE \"MigrationId\"="
                + Literal(ProcurementMigrationId) + ") AND EXISTS(SELECT 1 FROM procurement.__procurement_migrations WHERE \"MigrationId\"="
                + Literal(ReceiptMigrationId) + ")");
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
        finally { if (openedHere && connection.State != ConnectionState.Closed) await connection.CloseAsync(); }
    }

    /// <summary>Shared exact shape proof for a future Core producer readiness gate.
    /// This method grants no write capability and checks no actor authority.</summary>
    public static async Task<bool> NativeShapeReadyAsync(DbConnection connection, DbTransaction? transaction,
        bool coreOnly, CancellationToken ct)
    {
        if (connection.State != ConnectionState.Open || transaction is not null
            && !ReferenceEquals(transaction.Connection, connection)) return false;
        if (!await CoreCurrencyPrecisionSchemaReadiness.NativeShapeReadyAsync(connection, transaction, ct)) return false;
        if (coreOnly) return true;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction; command.CommandTimeout = 15;
        command.CommandText = "SELECT (" + NativeShapeSql + ")=" + Literal(NativeShapeJson) + "::jsonb";
        return await command.ExecuteScalarAsync(ct) is true;
    }

    internal static string PrivilegeSql(InvoiceCurrencySchemaMode mode, string role)
    {
        var child = "procurement.invoice_amount_calculations";
        var receipt = "procurement.invoice_intake_command_receipts";
        var coreTables = new[] { "hrm.currency_precision_versions", "hrm.currency_precision_command_receipts" };
        var clauses = new List<string>
        {
            "session_user=current_user AND current_user=" + Literal(role),
            "NOT EXISTS(SELECT 1 FROM pg_roles r WHERE r.rolname IN ('pg_read_all_data','pg_write_all_data') AND (pg_has_role(current_user,r.oid,'MEMBER') OR pg_has_role(current_user,r.oid,'SET')))",
            "NOT has_database_privilege(current_user,current_database(),'CREATE')",
            "NOT has_schema_privilege(current_user,'procurement','CREATE')",
            "NOT has_schema_privilege(current_user,'hrm','CREATE')",
            "NOT EXISTS(SELECT 1 FROM pg_roles r WHERE (r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls) AND pg_has_role(current_user,r.oid,'SET'))",
            "has_table_privilege(current_user,'procurement.invoice_amount_calculations','SELECT')",
            "has_table_privilege(current_user,'procurement.invoice_intake_command_receipts','SELECT')",
            "has_table_privilege(current_user,'hrm.currency_precision_versions','SELECT')",
            "has_column_privilege(current_user,'hrm.__hrm_migrations','MigrationId','SELECT')",
            "has_column_privilege(current_user,'procurement.__procurement_migrations','MigrationId','SELECT')"
        };
        foreach (var table in coreTables.Append(child).Append(receipt))
        {
            clauses.Add("NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid=" + Literal(table)
                + "::regclass AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))");
            clauses.Add("NOT has_any_column_privilege(current_user," + Literal(table) + ",'UPDATE')");
            clauses.Add("NOT has_any_column_privilege(current_user," + Literal(table) + ",'REFERENCES')");
            clauses.Add("NOT has_table_privilege(current_user," + Literal(table) + ",'DELETE,TRUNCATE,TRIGGER')");
        }
        foreach (var table in coreTables)
            clauses.Add("NOT has_any_column_privilege(current_user," + Literal(table) + ",'INSERT')");
        if (mode == InvoiceCurrencySchemaMode.RecordCapture)
        {
            clauses.Add("has_table_privilege(current_user," + Literal(child) + ",'INSERT')");
            clauses.Add("has_table_privilege(current_user," + Literal(receipt) + ",'INSERT')");
            foreach (var column in new[] { "id", "tenant_id", "currency", "is_archived" })
                clauses.Add("has_column_privilege(current_user,'hrm.legal_entities'," + Literal(column) + ",'SELECT')");
        }
        else if (mode == InvoiceCurrencySchemaMode.ExecutionRead)
        {
            clauses.Add("NOT has_any_column_privilege(current_user," + Literal(child) + ",'INSERT')");
            clauses.Add("NOT has_any_column_privilege(current_user," + Literal(receipt) + ",'INSERT')");
        }
        return "SELECT " + string.Join(" AND ", clauses);
    }

    private static string Literal(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
