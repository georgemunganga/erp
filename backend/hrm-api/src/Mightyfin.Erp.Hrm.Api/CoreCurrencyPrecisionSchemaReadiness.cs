using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

public interface ICoreCurrencyPrecisionSchemaReadiness
{
    Task<bool> IsReadyAsync(bool write, CancellationToken ct);
}

/// <summary>Core-owned schema/finite-role admission. The production implementation
/// always requires PostgreSQL; HTTP tests may explicitly replace the interface.</summary>
public sealed class CoreCurrencyPrecisionSchemaReadiness(HrmDbContext db, IConfiguration configuration)
    : ICoreCurrencyPrecisionSchemaReadiness
{
    public const string MigrationId = "20261030035815_M6dCoreCurrencyPrecision";
    public const string NativeMetadataSha256 = "d9b71fc5050e92aac3e405d250500b374a5ae18748a0b6b12eabb9a2dab7003d";
    private const string NativeShapeJson = """
{"hrm.currency_precision_command_receipts":{"columns":[{"collation":"-","default":null,"generated":"","identity":"","name":"id","notNull":true,"type":"uuid"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"operation","notNull":true,"type":"character varying(10)"},{"collation":"-","default":null,"generated":"","identity":"","name":"command_key","notNull":true,"type":"uuid"},{"collation":"-","default":null,"generated":"","identity":"","name":"actor_principal_id","notNull":true,"type":"uuid"},{"collation":"-","default":null,"generated":"","identity":"","name":"company_id","notNull":true,"type":"uuid"},{"collation":"-","default":null,"generated":"","identity":"","name":"version_id","notNull":true,"type":"uuid"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"request_sha256","notNull":true,"type":"character varying(64)"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"response_json","notNull":true,"type":"text"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"response_sha256","notNull":true,"type":"character varying(64)"},{"collation":"-","default":null,"generated":"","identity":"","name":"response_status","notNull":true,"type":"integer"},{"collation":"-","default":null,"generated":"","identity":"","name":"completed_at_utc","notNull":true,"type":"timestamp with time zone"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"tenant_id","notNull":true,"type":"text"},{"collation":"-","default":null,"generated":"","identity":"","name":"created_at","notNull":true,"type":"timestamp with time zone"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"created_by","notNull":true,"type":"text"},{"collation":"-","default":null,"generated":"","identity":"","name":"updated_at","notNull":false,"type":"timestamp with time zone"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"updated_by","notNull":false,"type":"text"},{"collation":"-","default":null,"generated":"","identity":"","name":"is_archived","notNull":true,"type":"boolean"}],"constraints":[{"deferrable":false,"deferred":false,"definition":"FOREIGN KEY (tenant_id, version_id) REFERENCES hrm.currency_precision_versions(tenant_id, id) ON DELETE RESTRICT","inheritCount":0,"kind":"f","local":true,"name":"FK_currency_precision_command_receipts_currency_precision_vers~","noInherit":true,"validated":true},{"deferrable":false,"deferred":false,"definition":"PRIMARY KEY (id)","inheritCount":0,"kind":"p","local":true,"name":"PK_currency_precision_command_receipts","noInherit":true,"validated":true},{"deferrable":false,"deferred":false,"definition":"CHECK ((((operation)::text = ANY ((ARRAY['Publish'::character varying, 'Retire'::character varying])::text[])) AND (command_key <> '00000000-0000-0000-0000-000000000000'::uuid) AND (response_status = ANY (ARRAY[200, 201])) AND ((request_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((response_sha256)::text ~ '^[0-9a-f]{64}$'::text)))","inheritCount":0,"kind":"c","local":true,"name":"ck_core_currency_precision_receipt","noInherit":false,"validated":true}],"forceRls":false,"indexes":[{"definition":"CREATE UNIQUE INDEX \"IX_currency_precision_command_receipts_tenant_id_actor_princip~\" ON hrm.currency_precision_command_receipts USING btree (tenant_id, actor_principal_id, operation, command_key)","immediate":true,"live":true,"name":"IX_currency_precision_command_receipts_tenant_id_actor_princip~","ready":true,"valid":true},{"definition":"CREATE INDEX \"IX_currency_precision_command_receipts_tenant_id_version_id\" ON hrm.currency_precision_command_receipts USING btree (tenant_id, version_id)","immediate":true,"live":true,"name":"IX_currency_precision_command_receipts_tenant_id_version_id","ready":true,"valid":true},{"definition":"CREATE UNIQUE INDEX \"PK_currency_precision_command_receipts\" ON hrm.currency_precision_command_receipts USING btree (id)","immediate":true,"live":true,"name":"PK_currency_precision_command_receipts","ready":true,"valid":true}],"kind":"r","nonInternalTriggers":0,"persistence":"p","rls":false},"hrm.currency_precision_versions":{"columns":[{"collation":"-","default":null,"generated":"","identity":"","name":"id","notNull":true,"type":"uuid"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"currency_code","notNull":true,"type":"character varying(3)"},{"collation":"-","default":null,"generated":"","identity":"","name":"revision","notNull":true,"type":"integer"},{"collation":"-","default":null,"generated":"","identity":"","name":"minor_units","notNull":true,"type":"integer"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"rounding","notNull":true,"type":"character varying(20)"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"source_reference","notNull":true,"type":"character varying(200)"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"publication_reason","notNull":true,"type":"character varying(500)"},{"collation":"-","default":null,"generated":"","identity":"","name":"published_at_utc","notNull":true,"type":"timestamp with time zone"},{"collation":"-","default":null,"generated":"","identity":"","name":"retired_at_utc","notNull":false,"type":"timestamp with time zone"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"retirement_reason","notNull":false,"type":"character varying(500)"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"payload_sha256","notNull":true,"type":"character varying(64)"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"tenant_id","notNull":true,"type":"text"},{"collation":"-","default":null,"generated":"","identity":"","name":"created_at","notNull":true,"type":"timestamp with time zone"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"created_by","notNull":true,"type":"text"},{"collation":"-","default":null,"generated":"","identity":"","name":"updated_at","notNull":false,"type":"timestamp with time zone"},{"collation":"\"default\"","default":null,"generated":"","identity":"","name":"updated_by","notNull":false,"type":"text"},{"collation":"-","default":null,"generated":"","identity":"","name":"is_archived","notNull":true,"type":"boolean"}],"constraints":[{"deferrable":false,"deferred":false,"definition":"UNIQUE (tenant_id, id)","inheritCount":0,"kind":"u","local":true,"name":"AK_currency_precision_versions_tenant_id_id","noInherit":true,"validated":true},{"deferrable":false,"deferred":false,"definition":"PRIMARY KEY (id)","inheritCount":0,"kind":"p","local":true,"name":"PK_currency_precision_versions","noInherit":true,"validated":true},{"deferrable":false,"deferred":false,"definition":"CHECK (((revision > 0) AND ((minor_units >= 0) AND (minor_units <= 4)) AND ((rounding)::text = ANY ((ARRAY['ToEven'::character varying, 'AwayFromZero'::character varying])::text[])) AND ((currency_code)::text ~ '^[A-Z]{3}$'::text) AND ((payload_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((retired_at_utc IS NULL) OR (retired_at_utc >= published_at_utc))))","inheritCount":0,"kind":"c","local":true,"name":"ck_core_currency_precision_version","noInherit":false,"validated":true}],"forceRls":false,"indexes":[{"definition":"CREATE UNIQUE INDEX \"AK_currency_precision_versions_tenant_id_id\" ON hrm.currency_precision_versions USING btree (tenant_id, id)","immediate":true,"live":true,"name":"AK_currency_precision_versions_tenant_id_id","ready":true,"valid":true},{"definition":"CREATE UNIQUE INDEX \"IX_currency_precision_versions_tenant_id_currency_code\" ON hrm.currency_precision_versions USING btree (tenant_id, currency_code) WHERE (retired_at_utc IS NULL)","immediate":true,"live":true,"name":"IX_currency_precision_versions_tenant_id_currency_code","ready":true,"valid":true},{"definition":"CREATE UNIQUE INDEX \"IX_currency_precision_versions_tenant_id_currency_code_revision\" ON hrm.currency_precision_versions USING btree (tenant_id, currency_code, revision)","immediate":true,"live":true,"name":"IX_currency_precision_versions_tenant_id_currency_code_revision","ready":true,"valid":true},{"definition":"CREATE UNIQUE INDEX \"PK_currency_precision_versions\" ON hrm.currency_precision_versions USING btree (id)","immediate":true,"live":true,"name":"PK_currency_precision_versions","ready":true,"valid":true}],"kind":"r","nonInternalTriggers":0,"persistence":"p","rls":false}}
""";
    private const string NativeShapeSql = """
SELECT jsonb_object_agg(n.nspname||'.'||c.relname,jsonb_build_object(
'kind',c.relkind,'persistence',c.relpersistence,'rls',c.relrowsecurity,'forceRls',c.relforcerowsecurity,
'columns',(SELECT jsonb_agg(jsonb_build_object('name',a.attname,'type',format_type(a.atttypid,a.atttypmod),'notNull',a.attnotnull,'generated',a.attgenerated,'identity',a.attidentity,'collation',a.attcollation::regcollation::text,'default',pg_get_expr(d.adbin,d.adrelid)) ORDER BY a.attnum) FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum WHERE a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped),
'constraints',(SELECT jsonb_agg(jsonb_build_object('name',q.conname,'kind',q.contype,'definition',pg_get_constraintdef(q.oid,false),'validated',q.convalidated,'deferrable',q.condeferrable,'deferred',q.condeferred,'local',q.conislocal,'inheritCount',q.coninhcount,'noInherit',q.connoinherit) ORDER BY q.conname) FROM pg_constraint q WHERE q.conrelid=c.oid),
'indexes',(SELECT jsonb_agg(jsonb_build_object('name',i.relname,'definition',pg_get_indexdef(x.indexrelid),'valid',x.indisvalid,'ready',x.indisready,'live',x.indislive,'immediate',x.indimmediate) ORDER BY i.relname) FROM pg_index x JOIN pg_class i ON i.oid=x.indexrelid WHERE x.indrelid=c.oid),
'nonInternalTriggers',(SELECT count(*) FROM pg_trigger WHERE tgrelid=c.oid AND NOT tgisinternal)
)) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.oid=ANY(ARRAY[to_regclass('hrm.currency_precision_versions'),to_regclass('hrm.currency_precision_command_receipts')])
""";
    public Task<bool> IsReadyAsync(bool write, CancellationToken ct)
        => IsReadyAsync(db, configuration, write, ct);

    public static async Task<bool> IsReadyAsync(HrmDbContext context, IConfiguration config,
        bool write, CancellationToken ct)
    {
        if (context is null || !context.Database.IsNpgsql()) return false;
        var connection = context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        try
        {
            var configured = config.GetConnectionString("Hrm");
            if (string.IsNullOrWhiteSpace(configured)) return false;
            var expected = new NpgsqlConnectionStringBuilder(configured);
            var actual = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
            if (string.IsNullOrWhiteSpace(expected.Username) || expected.Username != actual.Username
                || !string.Equals(expected.Host, actual.Host, StringComparison.OrdinalIgnoreCase)
                || expected.Port != actual.Port || expected.Database != actual.Database) return false;
            if (openedHere) await connection.OpenAsync(ct);
            var transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            if (!await NativeShapeReadyAsync(connection, transaction, ct)) return false;
            await using var command = connection.CreateCommand();
            command.Transaction = transaction; command.CommandTimeout = 15;
            command.CommandText = PrivilegeSql(write, expected.Username);
            if (await command.ExecuteScalarAsync(ct) is not true) return false;
            command.CommandText = "SELECT EXISTS(SELECT 1 FROM hrm.__hrm_migrations WHERE \"MigrationId\"="
                + Literal(MigrationId) + ")";
            return await command.ExecuteScalarAsync(ct) is true;
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
        finally { if (openedHere && connection.State != ConnectionState.Closed) await connection.CloseAsync(); }
    }

    public static async Task<bool> NativeShapeReadyAsync(DbConnection connection,
        DbTransaction? transaction, CancellationToken ct)
    {
        if (connection.State != ConnectionState.Open || transaction is not null
            && !ReferenceEquals(transaction.Connection, connection)) return false;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction; command.CommandTimeout = 15;
        command.CommandText = "SELECT (" + NativeShapeSql + ")=" + Literal(NativeShapeJson) + "::jsonb";
        return await command.ExecuteScalarAsync(ct) is true;
    }

    internal static string PrivilegeSql(bool write, string role)
    {
        const string versions = "hrm.currency_precision_versions";
        const string receipts = "hrm.currency_precision_command_receipts";
        var clauses = new List<string>
        {
            "session_user=current_user AND current_user=" + Literal(role),
            "NOT EXISTS(SELECT 1 FROM pg_roles r WHERE r.rolname IN ('pg_read_all_data','pg_write_all_data') AND (pg_has_role(current_user,r.oid,'MEMBER') OR pg_has_role(current_user,r.oid,'SET')))",
            "NOT has_database_privilege(current_user,current_database(),'CREATE')",
            "NOT EXISTS(SELECT 1 FROM pg_namespace n WHERE has_schema_privilege(current_user,n.oid,'CREATE'))",
            "NOT EXISTS(SELECT 1 FROM pg_roles r WHERE (r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls) AND pg_has_role(current_user,r.oid,'SET'))",
            "has_column_privilege(current_user,'hrm.__hrm_migrations','MigrationId','SELECT')",
            "NOT has_table_privilege(current_user,'hrm.currency_precision_versions','UPDATE')",
            "NOT has_any_column_privilege(current_user,'hrm.currency_precision_command_receipts','UPDATE')",
            "NOT EXISTS(SELECT 1 FROM pg_attribute a WHERE a.attrelid='hrm.currency_precision_versions'::regclass AND a.attnum>0 AND NOT a.attisdropped AND a.attname<>ALL(ARRAY['retired_at_utc','retirement_reason','updated_at','updated_by']) AND has_column_privilege(current_user,a.attrelid,a.attnum,'UPDATE'))"
        };
        foreach (var table in new[] { versions, receipts })
        {
            clauses.Add("has_table_privilege(current_user," + Literal(table) + ",'SELECT')");
            clauses.Add("NOT has_table_privilege(current_user," + Literal(table) + ",'DELETE,TRUNCATE,TRIGGER')");
            clauses.Add("NOT has_any_column_privilege(current_user," + Literal(table) + ",'REFERENCES')");
            clauses.Add("NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid=" + Literal(table)
                + "::regclass AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))");
            if (write) clauses.Add("has_table_privilege(current_user," + Literal(table) + ",'INSERT')");
        }
        if (write)
            foreach (var column in new[] { "retired_at_utc", "retirement_reason", "updated_at", "updated_by" })
                clauses.Add("has_column_privilege(current_user," + Literal(versions) + "," + Literal(column) + ",'UPDATE')");
        return "SELECT " + string.Join(" AND ", clauses);
    }
    private static string Literal(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
