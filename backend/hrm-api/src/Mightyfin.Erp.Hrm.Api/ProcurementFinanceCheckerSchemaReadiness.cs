using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public interface IProcurementFinanceCheckerSchemaReadiness
{
    Task<bool> IsReadyAsync(ProcurementDbContext owner, bool capture, CancellationToken ct);
}

/// <summary>Read-only discovery on the actual caller's owning store/transaction. No configured
/// connection fallback, write probe, identity backfill, or Core ownership duplication.</summary>
public sealed class ProcurementFinanceCheckerSchemaReadiness : IProcurementFinanceCheckerSchemaReadiness
{
    // Proposed literal pg_get_expr outputs. Parent must freeze/verify the actual PostgreSQL
    // generated definitions during migration rehearsal. An unsupported format fails CLOSED.
    internal const string BudgetExpression = "(((checker_identity_schema_version = 0) AND (checked_by_worker_id IS NULL) AND (checker_identity_sha256 IS NULL)) OR ((checker_identity_schema_version = 1) AND (checked_by_worker_id IS NOT NULL) AND (checked_by_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (checker_identity_sha256 IS NOT NULL) AND ((checker_identity_sha256)::text ~ '^[a-f0-9]{64}$'::text)))";
    internal const string SubmissionExpression = "((finance_identity_schema_version = 0) OR ((finance_identity_schema_version = 1) AND (snapshot_schema_version = 2)))";

    internal static bool ExactChecks(string? budget, string? submission) => budget is not null && submission is not null
        && OutsideWhitespace(budget) == OutsideWhitespace(BudgetExpression)
        && OutsideWhitespace(submission) == OutsideWhitespace(SubmissionExpression);

    // Ignore only insignificant whitespace OUTSIDE quoted values. Preserve every group,
    // operator, cast, field and literal byte; never flatten SQL boolean structure.
    private static string OutsideWhitespace(string sql)
    {
        if (sql.Length > 4096) return "";
        var result = new StringBuilder(); var quoted = false;
        for (var i = 0; i < sql.Length; i++)
        {
            var c = sql[i];
            if (c == '\'')
            {
                result.Append(c);
                if (quoted && i + 1 < sql.Length && sql[i + 1] == '\'') { result.Append(sql[++i]); continue; }
                quoted = !quoted;
            }
            else if (quoted || !char.IsWhiteSpace(c)) result.Append(c);
        }
        return quoted ? "" : result.ToString();
    }

    public async Task<bool> IsReadyAsync(ProcurementDbContext owner, bool capture, CancellationToken ct)
    {
        if (!owner.Database.IsNpgsql()) return false;
        var opened = false;
        try
        {
            if (owner.Database.HasPendingModelChanges() || (await owner.Database.GetPendingMigrationsAsync(ct)).Any()) return false;
            var connection = owner.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) { await owner.Database.OpenConnectionAsync(ct); opened = true; }
            await using var command = connection.CreateCommand();
            command.Transaction = owner.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandTimeout = 10;
            var write = command.CreateParameter(); write.ParameterName = "capture"; write.Value = capture; command.Parameters.Add(write);
            command.CommandText = ManifestSql;
            await using var result = await command.ExecuteReaderAsync(ct);
            return await result.ReadAsync(ct) && !result.IsDBNull(0) && result.GetBoolean(0)
                && ExactChecks(result.IsDBNull(1) ? null : result.GetString(1), result.IsDBNull(2) ? null : result.GetString(2));
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
        finally { if (opened) await owner.Database.CloseConnectionAsync(); }
    }

    internal const string ManifestSql = """
        WITH expected(table_name, column_name, data_type, not_null, default_value) AS (VALUES
          ('budget_check_snapshots','checker_identity_schema_version','integer',true,'0'),
          ('budget_check_snapshots','checked_by_worker_id','uuid',false,NULL),
          ('budget_check_snapshots','checker_identity_sha256','character varying(64)',false,NULL),
          ('purchase_request_approval_submissions','finance_identity_schema_version','integer',true,'0')
        ), columns_ready AS (
          SELECT count(a.attname)=4 AND bool_and(
            format_type(a.atttypid,a.atttypmod)=e.data_type AND a.attnotnull=e.not_null
            AND a.attidentity='' AND a.attgenerated='' AND NOT a.attisdropped
            AND pg_get_expr(d.adbin,d.adrelid) IS NOT DISTINCT FROM e.default_value
            AND has_column_privilege(current_user,c.oid,a.attnum,'SELECT')
            AND (NOT @capture OR e.table_name <> 'budget_check_snapshots'
                 OR has_column_privilege(current_user,c.oid,a.attnum,'INSERT'))
          ) AS ready
          FROM expected e
          LEFT JOIN pg_namespace n ON n.nspname='procurement'
          LEFT JOIN pg_class c ON c.relnamespace=n.oid AND c.relname=e.table_name AND c.relkind='r'
          LEFT JOIN pg_attribute a ON a.attrelid=c.oid AND a.attname=e.column_name AND a.attnum>0
          LEFT JOIN pg_attrdef d ON d.adrelid=c.oid AND d.adnum=a.attnum
        )
        SELECT (SELECT ready FROM columns_ready)
          AND has_table_privilege(current_user,to_regclass('procurement.budget_check_snapshots'),'SELECT')
          AND (NOT @capture OR (has_table_privilege(current_user,to_regclass('procurement.budget_check_snapshots'),'INSERT')
              AND has_table_privilege(current_user,to_regclass('procurement.audit_events'),'INSERT')))
          AND EXISTS (
            SELECT 1 FROM pg_constraint f
            WHERE f.contype='f' AND f.convalidated AND NOT f.condeferrable AND NOT f.condeferred
              AND f.conrelid=to_regclass('procurement.purchase_request_approval_submissions')
              AND f.confrelid=to_regclass('procurement.budget_check_snapshots')
              AND f.confupdtype='a' AND f.confdeltype='r' AND f.confmatchtype='s'
              AND ARRAY(SELECT a.attname::text FROM unnest(f.conkey) WITH ORDINALITY k(num,ord)
                        JOIN pg_attribute a ON a.attrelid=f.conrelid AND a.attnum=k.num ORDER BY k.ord)
                  = ARRAY['tenant_id','legal_entity_id','finance_snapshot_id','purchase_request_id','submitted_business_version']
              AND ARRAY(SELECT a.attname::text FROM unnest(f.confkey) WITH ORDINALITY k(num,ord)
                        JOIN pg_attribute a ON a.attrelid=f.confrelid AND a.attnum=k.num ORDER BY k.ord)
                  = ARRAY['tenant_id','legal_entity_id','id','purchase_request_id','request_version']
          ),
          (SELECT pg_get_expr(c.conbin,c.conrelid) FROM pg_constraint c
            WHERE c.conrelid=to_regclass('procurement.budget_check_snapshots')
              AND c.conname='ck_budget_checker_identity' AND c.contype='c' AND c.convalidated AND c.conislocal),
          (SELECT pg_get_expr(c.conbin,c.conrelid) FROM pg_constraint c
            WHERE c.conrelid=to_regclass('procurement.purchase_request_approval_submissions')
              AND c.conname='ck_pr_finance_identity_version' AND c.contype='c' AND c.convalidated AND c.conislocal)
        """;
}

internal static class ProcurementFinanceCheckerSchemaGuard
{
    internal static async Task<bool> ReadyAsync(ProcurementDbContext owner, IServiceProvider services,
        bool capture, CancellationToken ct)
    {
        if(await (services.GetService<IProcurementFinanceCheckerSchemaReadiness>()
            ?? new ProcurementFinanceCheckerSchemaReadiness()).IsReadyAsync(owner,capture,ct))return true;
        return await ProcurementLegacyP25SchemaGuard.ReadyAsync(owner,services,capture,ct);
    }

    internal static IResult Unavailable() => Results.Problem(statusCode: 503,
        title: "Funding evidence is temporarily unavailable. Contact your administrator to complete the database setup.",
        extensions: new Dictionary<string, object?> { ["code"] = "FinanceCheckerSchemaUnavailable" });
}
