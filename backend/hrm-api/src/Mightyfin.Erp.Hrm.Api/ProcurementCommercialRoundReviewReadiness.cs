using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public interface IProcurementCommercialRoundReviewReadiness
{
    bool HasCapturedContract { get; }
    Task<bool> ReadyAsync(ProcurementDbContext db, bool record, CancellationToken ct);
}

/// <summary>Genuine successor schema and finite-role profile. Root captures and
/// pins the generated migration/catalog after source integration; until then all
/// review commands are unavailable. Existing B1 draft readiness is independent.</summary>
public sealed class ProcurementCommercialRoundReviewReadiness(
    IConfiguration configuration, IProcurementCommercialRoundReadiness draft) : IProcurementCommercialRoundReviewReadiness
{
    // Filled only from the generated successor migration and native catalog proof.
    internal const string MigrationId = "";
    internal const string Fingerprint = "";
    public bool HasCapturedContract => MigrationId.Length > 15 && Fingerprint.Length == 64
        && Fingerprint.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public async Task<bool> ReadyAsync(ProcurementDbContext db, bool record, CancellationToken ct)
    {
        if (!HasCapturedContract || !db.Database.IsNpgsql()
            || db.Database.CurrentTransaction?.GetDbTransaction() is not { } tx
            || tx.IsolationLevel != IsolationLevel.Serializable
            || !ReferenceEquals(tx.Connection, db.Database.GetDbConnection())
            || !await draft.ReadyAsync(db, record, ct)
            || !ProcurementManualQuotationSchemaReadiness.TrustedExecutionRole(configuration,
                db.Database.GetConnectionString()!, out var role)
            || !(await db.Database.GetAppliedMigrationsAsync(ct)).Contains(MigrationId, StringComparer.Ordinal)) return false;
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = tx; command.CommandTimeout = 15;
        command.CommandText = FingerprintSql;
        if (await command.ExecuteScalarAsync(ct) is not string actual || actual != Fingerprint) return false;
        command.CommandText = PrivilegeSql(role, record);
        return await command.ExecuteScalarAsync(ct) is true;
    }

    private static string Literal(string text) => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";
    internal static string PrivilegeSql(string role, bool record)
    {
        var requirements = new List<string>
        {
            "session_user=current_user", "current_user=" + Literal(role),
            ProcurementSpendingAuthorityIdentitySchemaReadiness.NoOwnershipSql,
            ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql,
            "NOT has_schema_privilege(current_user,'procurement','CREATE')",
            "NOT has_schema_privilege(current_user,'core_approval','CREATE')",
        };
        foreach (var table in new[] { "commercial_round_review_submissions", "commercial_round_review_outcomes" })
        {
            var name = Literal("procurement." + table);
            requirements.Add("has_table_privilege(current_user," + name + ",'SELECT')");
            requirements.Add("NOT has_table_privilege(current_user," + name + ",'UPDATE,DELETE,TRUNCATE,TRIGGER,REFERENCES')");
            requirements.Add("NOT has_any_column_privilege(current_user," + name + ",'UPDATE,REFERENCES')");
            requirements.Add("NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid=" + name
                + "::regclass AND (pg_has_role(current_user,c.relowner,'SET') OR pg_has_role(current_user,c.relowner,'MEMBER')))");
            if (record) requirements.Add("has_table_privilege(current_user," + name + ",'INSERT')");
        }
        if (record)
        {
            requirements.Add("current_setting('transaction_read_only')='off' AND NOT pg_is_in_recovery()");
            requirements.Add("has_table_privilege(current_user,'core_approval.cases','INSERT')");
            requirements.Add("has_table_privilege(current_user,'core_approval.stages','INSERT')");
            requirements.Add("has_table_privilege(current_user,'core_approval.assignments','INSERT')");
            requirements.Add("has_table_privilege(current_user,'core_approval.decisions','INSERT')");
            foreach (var table in new[] { "cases", "stages", "assignments" })
                requirements.Add("has_table_privilege(current_user," + Literal("core_approval." + table) + ",'UPDATE')");
        }
        return "SELECT " + string.Join(" AND ", requirements);
    }

    // Exact catalog capture includes the new tables, existing result-state checks,
    // approved policy key CHECK and both owner functions. No startup inference.
    internal const string FingerprintSql = """
WITH wanted(name) AS (VALUES ('commercial_round_review_submissions'),('commercial_round_review_outcomes'),
 ('commercial_round_plans'),('commercial_round_plan_revisions'),('commercial_round_plan_receipts'),
 ('operational_policy_identity_evidence')),
r AS (SELECT c.oid,c.relname,c.relkind,c.relrowsecurity,c.relforcerowsecurity,
 pg_get_userbyid(c.relowner) owner_name FROM wanted w JOIN pg_class c ON c.relname=w.name
 JOIN pg_namespace n ON n.oid=c.relnamespace AND n.nspname='procurement')
SELECT encode(sha256(convert_to(jsonb_build_object(
 'relations',(SELECT jsonb_agg(jsonb_build_array(relname,relkind,relrowsecurity,relforcerowsecurity,owner_name) ORDER BY relname) FROM r),
 'columns',(SELECT jsonb_agg(jsonb_build_array(r.relname,a.attname,format_type(a.atttypid,a.atttypmod),a.attnotnull,a.attidentity,a.attgenerated,pg_get_expr(d.adbin,d.adrelid)) ORDER BY r.relname,a.attname) FROM r JOIN pg_attribute a ON a.attrelid=r.oid AND a.attnum>0 AND NOT a.attisdropped LEFT JOIN pg_attrdef d ON d.adrelid=r.oid AND d.adnum=a.attnum),
 'constraints',(SELECT jsonb_agg(jsonb_build_array(r.relname,c.conname,c.contype,c.convalidated,c.condeferrable,c.condeferred,pg_get_constraintdef(c.oid)) ORDER BY r.relname,c.conname) FROM r JOIN pg_constraint c ON c.conrelid=r.oid),
 'indexes',(SELECT jsonb_agg(jsonb_build_array(r.relname,i.indisvalid,i.indisready,i.indislive,i.indimmediate,pg_get_indexdef(i.indexrelid)) ORDER BY r.relname,pg_get_indexdef(i.indexrelid)) FROM r JOIN pg_index i ON i.indrelid=r.oid),
 'triggers',(SELECT jsonb_agg(jsonb_build_array(c.relname,t.tgname,t.tgenabled,pg_get_triggerdef(t.oid)) ORDER BY c.relname,t.tgname) FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid WHERE c.oid IN (SELECT oid FROM r) AND NOT t.tgisinternal),
 'functions',(SELECT jsonb_agg(jsonb_build_array(p.proname,pg_get_userbyid(p.proowner),p.prosecdef,pg_get_functiondef(p.oid)) ORDER BY p.proname) FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='procurement' AND p.proname IN ('commercial_round_complete','commercial_round_plan_update'))
)::text,'UTF8')),'hex')
""";
}
