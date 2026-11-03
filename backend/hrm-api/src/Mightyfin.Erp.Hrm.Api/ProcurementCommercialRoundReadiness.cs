using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

public sealed class ProcurementCommercialRoundOptions
{
    public bool ReadEnabled { get; set; }
    public bool RecordEnabled { get; set; }
}
public interface IProcurementCommercialRoundReadiness
{
    bool HasCapturedContract { get; }
    Task<bool> ReadyAsync(ProcurementDbContext db, bool record, CancellationToken ct);
    Task<bool> BarrierReadyAsync(ProcurementDbContext db, CancellationToken ct);
}
/// <summary>Genuine current-model EF migration and private native capture adopted
/// together with the exact capture query. This is schema evidence, not feature
/// activation or a substitute for current finite-role and scoped authorization.</summary>
internal static class CommercialRoundNativeContract
{
    internal const string MigrationId = "20261103163108_M7b1CommercialRoundDraftPlans";
    internal const string Fingerprint = "24e096b8ea495da24c7eab1ea2b0bd1fda9cc54754d2d2b5f9ca923cd743133b";
    internal static bool IsCaptured(string? migrationId,string? fingerprint) => migrationId is { Length: > 15 }
        && fingerprint is { Length: 64 } && fingerprint.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
public sealed class ProcurementCommercialRoundReadiness(IConfiguration configuration) : IProcurementCommercialRoundReadiness
{
    public bool HasCapturedContract => CommercialRoundNativeContract.IsCaptured(CommercialRoundNativeContract.MigrationId,CommercialRoundNativeContract.Fingerprint);
    public Task<bool> ReadyAsync(ProcurementDbContext db, bool record, CancellationToken ct) => CheckAsync(db, record, false, ct);
    public Task<bool> BarrierReadyAsync(ProcurementDbContext db, CancellationToken ct) => CheckAsync(db, false, true, ct);
    private async Task<bool> CheckAsync(ProcurementDbContext db, bool record, bool barrierOnly, CancellationToken ct)
    {
        if (!HasCapturedContract || !db.Database.IsNpgsql() || db.Database.CurrentTransaction?.GetDbTransaction() is not { } tx
            || tx.IsolationLevel != IsolationLevel.Serializable || !ReferenceEquals(tx.Connection, db.Database.GetDbConnection())
            || !ProcurementManualQuotationSchemaReadiness.TrustedExecutionRole(configuration, db.Database.GetConnectionString()!, out var role)
            || ProcurementCompiledModelParity.HasPendingModelChanges(db) || (await db.Database.GetPendingMigrationsAsync(ct)).Any()
            || !(await db.Database.GetAppliedMigrationsAsync(ct)).Contains(CommercialRoundNativeContract.MigrationId, StringComparer.Ordinal)
            || await CommercialRoundPlanSchema.PresenceAsync(db, ct) != CommercialRoundSchemaPresence.Complete) return false;
        await using var command = db.Database.GetDbConnection().CreateCommand(); command.Transaction = tx; command.CommandTimeout = 15;
        command.CommandText = FingerprintSql;
        if (await command.ExecuteScalarAsync(ct) is not string actual || actual != CommercialRoundNativeContract.Fingerprint) return false;
        command.CommandText = barrierOnly ? BarrierPrivilegeSql(role) : PrivilegeSql(record, role);
        return await command.ExecuteScalarAsync(ct) is true;
    }
    // Minimal M4 trigger/Host barrier projection. No criteria, team, content or
    // principal details are exposed by merely retaining M4 publication/cancel.
    internal static readonly IReadOnlyDictionary<string, string[]> BarrierColumns = new Dictionary<string, string[]> {
        ["commercial_round_enrollments"] = ["tenant_id","legal_entity_id","sourcing_event_id"],
        ["commercial_round_plans"] = ["tenant_id","legal_entity_id","id","sourcing_event_id","status","version"],
        ["commercial_round_plan_revisions"] = ["tenant_id","legal_entity_id","plan_id","id","result_version","result_status","operation"],
        ["commercial_round_plan_receipts"] = ["tenant_id","legal_entity_id","plan_id","revision_id","result_version","operation"]
    };
    internal static string BarrierPrivilegeSql(string role) => "SELECT session_user=current_user AND current_user=" + Literal(role)
        + " AND " + ProcurementSpendingAuthorityIdentitySchemaReadiness.NoOwnershipSql + " AND "
        + string.Join(" AND ", BarrierColumns.SelectMany(x=>x.Value.Select(column=>
            "has_column_privilege(current_user,"+Literal("procurement."+x.Key)+","+Literal(column)+",'SELECT')")))
        + " AND " + string.Join(" AND ", CommercialRoundPlanSchema.Tables.Select(table => "NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid="
            + Literal("procurement."+table)+"::regclass AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))") )
        + " AND NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='procurement' AND p.proname IN ('commercial_round_immutable','commercial_round_enroll_guard','commercial_round_event_barrier','commercial_round_complete','commercial_round_plan_update') AND (pg_has_role(current_user,p.proowner,'SET') OR pg_has_role(current_user,p.proowner,'MEMBER')))";
    internal static readonly string[] SourceReadTables = [ "sourcing_events", "sourcing_lines", "sourcing_draft_evidence", "sourcing_demand_bindings",
        "__procurement_migrations", "purchase_requests", "purchase_request_lines", "purchase_request_allocations",
        "purchase_request_approval_submissions", "purchase_request_approval_outcomes", "policy_versions", "policy_evaluations",
        "budget_check_snapshots", "procurement_methods" ];
    internal static readonly string[] ExistenceOnlyTables = [ "sourcing_publication_openings", "sourcing_publication_decisions",
        "sourcing_publication_receipts", "sourcing_invitations", "sourcing_manual_contacts", "sourcing_manual_quote_versions", "supplier_bids", "sourcing_awards" ];
    internal static string PrivilegeSql(bool record, string role)
    {
        var clauses = new List<string> { "session_user=current_user AND current_user=" + Literal(role),
            ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql,
            ProcurementSpendingAuthorityIdentitySchemaReadiness.NoOwnershipSql,
            "NOT has_schema_privilege(current_user,'procurement','CREATE')",
            "NOT has_schema_privilege(current_user,'core_approval','CREATE')" };
        foreach (var table in SourceReadTables.Concat(CommercialRoundPlanSchema.Tables))
        {
            var name=Literal("procurement."+table);
            clauses.Add("has_table_privilege(current_user," + name + ",'SELECT')");
            clauses.Add("NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid="+name+"::regclass AND (pg_has_role(current_user,c.relowner,'SET') OR pg_has_role(current_user,c.relowner,'MEMBER')))");
        }
        foreach (var projection in new Dictionary<string, string[]> {
            ["purchase_orders"] = ["tenant_id","legal_entity_id","id"],
            ["purchase_order_lines"] = ["tenant_id","legal_entity_id","id","purchase_order_id","purchase_request_line_id"] })
            foreach (var column in projection.Value)
                clauses.Add("has_column_privilege(current_user," + Literal("procurement." + projection.Key) + "," + Literal(column) + ",'SELECT')");
        foreach (var table in new[] { "cases", "stages", "assignments", "decisions" })
            clauses.Add("has_table_privilege(current_user," + Literal("core_approval." + table) + ",'SELECT')");
        foreach (var table in ExistenceOnlyTables)
            foreach (var column in new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" })
                clauses.Add("has_column_privilege(current_user," + Literal("procurement." + table) + "," + Literal(column) + ",'SELECT')");
        foreach (var projection in ProcurementInvoiceReplacementSchemaReadiness.CoreReadColumns)
            foreach (var column in projection.Value)
                clauses.Add("has_column_privilege(current_user," + Literal(projection.Key) + "," + Literal(column) + ",'SELECT')");
        clauses.Add("has_column_privilege(current_user,'hrm.local_users','display_name','SELECT')");
        clauses.Add("has_column_privilege(current_user,'hrm.legal_entities','time_zone_id','SELECT')");
        foreach (var table in CommercialRoundPlanSchema.Tables)
        {
            var name = Literal("procurement." + table);
            clauses.Add("NOT has_table_privilege(current_user," + name + ",'DELETE,TRUNCATE,TRIGGER,REFERENCES')");
            clauses.Add("NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid=" + name + "::regclass AND (pg_has_role(current_user,c.relowner,'SET') OR pg_has_role(current_user,c.relowner,'MEMBER')))");
            clauses.Add("NOT has_any_column_privilege(current_user," + name + ",'REFERENCES')");
            if (table != "commercial_round_plans") clauses.Add("NOT has_any_column_privilege(current_user," + name + ",'UPDATE')");
            if (record) clauses.Add("has_table_privilege(current_user," + name + ",'INSERT')");
        }
        if (record)
        {
            clauses.Add("current_setting('transaction_read_only')='off' AND NOT pg_is_in_recovery()");
            clauses.Add("has_table_privilege(current_user,'procurement.commercial_round_plans','UPDATE')");
            clauses.Add("has_any_column_privilege(current_user,'procurement.sourcing_events','UPDATE')");
            clauses.Add("has_any_column_privilege(current_user,'procurement.purchase_requests','UPDATE')");
            foreach (var table in new[] { "audit_events", "outbox_events" }) clauses.Add("has_table_privilege(current_user,'procurement." + table + "','INSERT')");
        }
        foreach (var permission in new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE" })
            clauses.Add("NOT has_table_privilege(current_user,'procurement.finance_payment_status_refs'," + Literal(permission) + ")");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname IN ('procurement','core_approval') AND (pg_has_role(current_user,p.proowner,'SET') OR pg_has_role(current_user,p.proowner,'MEMBER')))");
        return "SELECT " + string.Join(" AND ", clauses);
    }
    // Actual generated catalog capture, including rollback-critical source-event
    // trigger and all named owner functions. No guessed constraint text/hash.
    internal static readonly string FingerprintSql = """
WITH wanted(name) AS (VALUES ('commercial_round_enrollments'),('commercial_round_plans'),('commercial_round_plan_revisions'),('commercial_round_criteria'),('commercial_round_participants'),('commercial_round_plan_receipts')),
r AS (SELECT c.oid,c.relname,c.relkind,c.relrowsecurity,c.relforcerowsecurity,pg_get_userbyid(c.relowner) AS owner_name FROM wanted w JOIN pg_class c ON c.relname=w.name JOIN pg_namespace n ON n.oid=c.relnamespace AND n.nspname='procurement')
SELECT encode(sha256(convert_to(jsonb_build_object(
 'relations',(SELECT jsonb_agg(jsonb_build_array(relname,relkind,relrowsecurity,relforcerowsecurity,owner_name) ORDER BY relname) FROM r),
 'columns',(SELECT jsonb_agg(jsonb_build_array(r.relname,a.attname,format_type(a.atttypid,a.atttypmod),a.attnotnull,a.attidentity,a.attgenerated,pg_get_expr(d.adbin,d.adrelid)) ORDER BY r.relname,a.attname) FROM r JOIN pg_attribute a ON a.attrelid=r.oid AND a.attnum>0 AND NOT a.attisdropped LEFT JOIN pg_attrdef d ON d.adrelid=r.oid AND d.adnum=a.attnum),
 'constraints',(SELECT jsonb_agg(jsonb_build_array(r.relname,c.conname,c.contype,c.convalidated,c.condeferrable,c.condeferred,pg_get_constraintdef(c.oid)) ORDER BY r.relname,c.conname) FROM r JOIN pg_constraint c ON c.conrelid=r.oid),
 'indexes',(SELECT jsonb_agg(jsonb_build_array(r.relname,i.indisvalid,i.indisready,i.indislive,i.indimmediate,pg_get_indexdef(i.indexrelid)) ORDER BY r.relname,pg_get_indexdef(i.indexrelid)) FROM r JOIN pg_index i ON i.indrelid=r.oid),
 'triggers',(SELECT jsonb_agg(jsonb_build_array(c.relname,t.tgname,t.tgenabled,pg_get_triggerdef(t.oid)) ORDER BY c.relname,t.tgname) FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='procurement' AND NOT t.tgisinternal AND (c.oid IN(SELECT oid FROM r) OR t.tgname='commercial_round_event_barrier')),
 'originalSourceKey',(SELECT jsonb_agg(jsonb_build_array(c.conname,c.contype,(SELECT jsonb_agg(a.attname ORDER BY k.ord) FROM unnest(c.conkey) WITH ORDINALITY AS k(num,ord) JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.num),c.convalidated,c.condeferrable,c.condeferred,pg_get_constraintdef(c.oid),ic.relname,i.indisvalid,i.indisready,i.indislive,i.indisunique,i.indimmediate,pg_get_indexdef(i.indexrelid)) ORDER BY c.conname) FROM pg_constraint c JOIN pg_class pc ON pc.oid=c.conrelid JOIN pg_namespace pn ON pn.oid=pc.relnamespace JOIN pg_index i ON i.indexrelid=c.conindid JOIN pg_class ic ON ic.oid=i.indexrelid WHERE pn.nspname='procurement' AND pc.relname='sourcing_draft_evidence' AND c.conname='ak_round_original_source'),
 'functions',(SELECT jsonb_agg(jsonb_build_array(p.proname,pg_get_userbyid(p.proowner),p.prosecdef,pg_get_functiondef(p.oid)) ORDER BY p.proname) FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='procurement' AND p.proname IN ('commercial_round_immutable','commercial_round_enroll_guard','commercial_round_event_barrier','commercial_round_complete','commercial_round_plan_update'))
)::text,'UTF8')),'hex')
""";
}
