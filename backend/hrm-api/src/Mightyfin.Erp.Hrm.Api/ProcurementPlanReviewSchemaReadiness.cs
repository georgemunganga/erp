using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Actual read-only catalog/finite-role inspection. The new physical CHECK
/// fingerprints and migration ID remain absent until a genuine post-B1 migration
/// and private PostgreSQL capture; configuration alone cannot admit commands.</summary>
public sealed class ProcurementPlanReviewSchemaReadiness(IConfiguration configuration,
    IProcurementScope scope, IProcurementOperationalPolicyIdentitySchemaReadiness policyIdentity)
    : IProcurementPlanReviewReadiness
{
    internal const string RequiredMigrationId = "";
    internal static readonly IReadOnlyDictionary<string, string> CheckDefinitions =
        new Dictionary<string, string>();
    internal static bool ManifestCaptured => RequiredMigrationId.Length > 0
        && CheckDefinitions.Keys.ToHashSet(StringComparer.Ordinal)
            .SetEquals(new[] { "ck_plan_review_submission", "ck_plan_review_receipt" });

    public async Task<bool> CanReadPlanAsync(ProcurementDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) return false;
        var opened = false;
        try
        {
            if (ProcurementCompiledModelParity.HasPendingModelChanges(db)
                || (await db.Database.GetPendingMigrationsAsync(ct)).Any()) return false;
            var model = db.GetService<IDesignTimeModel>().Model;
            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) { await db.Database.OpenConnectionAsync(ct); opened = true; }
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandTimeout = 10;
            foreach (var type in new[] { typeof(ProcurementPlan), typeof(ProcurementPlanLine) })
            {
                var entity = model.FindEntityType(type);
                if (entity is null || entity.GetSchema() != "procurement") return false;
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                command.CommandText = SelectAllColumnsSql(entity, table);
                await using (var result = await command.ExecuteReaderAsync(ct)) { }
                command.CommandText = ColumnsSql(entity, table);
                if (await command.ExecuteScalarAsync(ct) is not true) return false;
            }
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
        finally { if (opened) await db.Database.CloseConnectionAsync(); }
    }

    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        var execution = configuration.GetConnectionString("ProcurementApprovalExecution");
        if (string.IsNullOrWhiteSpace(execution)) return false;
        try
        {
            await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql(execution, x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, scope);
            return await CheckReviewAsync(db, readOnly: false, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
    }

    public Task<bool> CanReadReviewAsync(ProcurementDbContext db, CancellationToken ct)
        => CheckReviewAsync(db, readOnly: true, ct);

    private async Task<bool> CheckReviewAsync(ProcurementDbContext db, bool readOnly, CancellationToken ct)
    {
        if (!ManifestCaptured || !await CanReadPlanAsync(db, ct)
            || !(await db.Database.GetAppliedMigrationsAsync(ct)).Contains(RequiredMigrationId, StringComparer.Ordinal)
            || !await policyIdentity.IsReadyAsync(db, ct)) return false;
        var opened = false;
        try
        {
                var connection = db.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open) { await db.Database.OpenConnectionAsync(ct); opened = true; }
                await using var command = connection.CreateCommand();
                command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandTimeout = 10;
                command.CommandText = readOnly ? ReadPrivilegeSql : PrivilegeSql;
                if (await command.ExecuteScalarAsync(ct) is not true) return false;
                var design = db.GetService<IDesignTimeModel>().Model;
                foreach (var type in new[] { typeof(PlanReviewSubmission), typeof(PlanReviewCommandReceipt) })
                {
                    var entity = design.FindEntityType(type);
                    if (entity is null || entity.GetSchema() != "procurement") return false;
                    var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                    command.CommandText = SelectAllColumnsSql(entity, table);
                    await using (var rows = await command.ExecuteReaderAsync(ct)) { }
                    command.CommandText = ColumnsSql(entity, table);
                    if (await command.ExecuteScalarAsync(ct) is not true) return false;
                    var checks = entity.GetCheckConstraints().ToArray();
                    if (checks.Length != 1) return false;
                    foreach (var check in checks)
                    {
                        if (!CheckDefinitions.TryGetValue(check.Name!, out var expected)) return false;
                        command.CommandText = CheckSql(table, check.Name!);
                        if (await command.ExecuteScalarAsync(ct) is not string actual || actual != expected) return false;
                    }
                    foreach (var index in entity.GetIndexes().Where(x => x.IsUnique))
                        if (!await True(command, UniqueIndexSql(table,
                            index.Properties.Select(x => x.GetColumnName(table)!).ToArray()), ct)) return false;
                    foreach (var foreign in entity.GetForeignKeys())
                    {
                        var principal = StoreObjectIdentifier.Table(foreign.PrincipalEntityType.GetTableName()!,
                            foreign.PrincipalEntityType.GetSchema());
                        if (!await True(command, ForeignKeySql(table, principal,
                            foreign.Properties.Select(x => x.GetColumnName(table)!).ToArray(),
                            foreign.PrincipalKey.Properties.Select(x => x.GetColumnName(principal)!).ToArray()), ct)) return false;
                    }
                }
                return true;
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
        finally { if (opened) await db.Database.CloseConnectionAsync(); }
    }

    private static async Task<bool> True(System.Data.Common.DbCommand command, string sql, CancellationToken ct)
    { command.CommandText = sql; return await command.ExecuteScalarAsync(ct) is true; }

    internal static string ReadPrivilegeSql => "SELECT " + string.Join(" AND ", new[]
    {
        "current_setting('transaction_read_only')='on'",
        "NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))",
        ProcurementSpendingAuthorityIdentitySchemaReadiness.NoOwnershipSql,
        ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql,
        ProcurementCoreReadProjectionGuard.Sql,
        SubmissionImmutableColumnsSql,
        "has_schema_privilege(current_user,'hrm','USAGE')",
        "NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('procurement','core_approval') AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))"
    }.Concat(new[] { "procurement.procurement_plans", "procurement.procurement_plan_lines",
        "procurement.plan_review_submissions", "procurement.plan_review_command_receipts",
        "procurement.policy_versions", "procurement.operational_policy_identity_evidence",
        "core_approval.cases", "core_approval.stages", "core_approval.assignments",
        "core_approval.decisions" }.Select(x =>
            "has_table_privilege(current_user," + Literal(x) + ",'SELECT')"))
      .Concat(new[] { "procurement.plan_review_command_receipts", "core_approval.decisions" }
        .Select(x => "NOT has_any_column_privilege(current_user," + Literal(x) + ",'UPDATE')"))
      .Concat(new[] { "procurement.plan_review_submissions", "procurement.plan_review_command_receipts",
        "core_approval.cases", "core_approval.stages", "core_approval.assignments", "core_approval.decisions" }
        .Select(x => "NOT has_table_privilege(current_user," + Literal(x) + ",'DELETE,TRUNCATE,TRIGGER')"))
      .Concat(CoreColumns.SelectMany(projection => projection.Value.Select(column =>
          "has_column_privilege(current_user," + Literal(projection.Key) + ","
              + Literal(column) + ",'SELECT')"))));

    internal static string PrivilegeSql => "SELECT " + string.Join(" AND ", new[]
    {
        "NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))",
        ProcurementSpendingAuthorityIdentitySchemaReadiness.NoOwnershipSql,
        ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql,
        ProcurementCoreReadProjectionGuard.Sql,
        SubmissionImmutableColumnsSql,
        "NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE ((n.nspname='procurement' AND c.relname IN ('procurement_plans','procurement_plan_lines','plan_review_submissions','plan_review_command_receipts','policy_versions','operational_policy_identity_evidence')) OR n.nspname='core_approval') AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))",
        "has_schema_privilege(current_user,'hrm','USAGE')",
    }.Concat(new[] { "procurement.procurement_plans", "procurement.procurement_plan_lines",
            "procurement.plan_review_submissions", "procurement.plan_review_command_receipts",
            "procurement.policy_versions", "procurement.operational_policy_identity_evidence",
            "core_approval.cases", "core_approval.stages", "core_approval.assignments",
            "core_approval.decisions" }
        .Select(x => "has_table_privilege(current_user," + Literal(x) + ",'SELECT')"))
      .Concat(new[] { "procurement.procurement_plans",
            "core_approval.cases", "core_approval.stages", "core_approval.assignments" }
        .Select(x => "has_table_privilege(current_user," + Literal(x) + ",'UPDATE')"))
      .Concat(SubmissionUpdateColumns.Select(column =>
          "has_column_privilege(current_user,'procurement.plan_review_submissions',"
              + Literal(column) + ",'UPDATE')"))
      .Concat(new[] { "procurement.plan_review_submissions", "procurement.plan_review_command_receipts",
            "procurement.audit_events", "procurement.outbox_events", "core_approval.cases",
            "core_approval.stages", "core_approval.assignments", "core_approval.decisions" }
        .Select(x => "has_table_privilege(current_user," + Literal(x) + ",'INSERT')"))
      .Concat(new[] { "procurement.plan_review_command_receipts", "procurement.audit_events",
            "procurement.outbox_events", "core_approval.decisions" }
        .Select(x => "NOT has_any_column_privilege(current_user," + Literal(x) + ",'UPDATE')"))
      .Concat(new[] { "procurement.procurement_plans", "procurement.procurement_plan_lines",
            "procurement.plan_review_submissions", "procurement.plan_review_command_receipts",
            "procurement.audit_events", "procurement.outbox_events", "core_approval.cases",
            "core_approval.stages", "core_approval.assignments", "core_approval.decisions" }
        .Select(x => "NOT has_table_privilege(current_user," + Literal(x) + ",'DELETE,TRUNCATE,TRIGGER')"))
      .Concat(new[] { "procurement.budget_check_snapshots", "procurement.finance_invoice_handoffs",
            "procurement.finance_payment_status_refs" }
        .SelectMany(x => new[]
        {
            "NOT has_table_privilege(current_user," + Literal(x) + ",'INSERT,UPDATE,DELETE,TRUNCATE,TRIGGER')",
            "NOT has_any_column_privilege(current_user," + Literal(x) + ",'INSERT,UPDATE')",
        }))
      .Concat(CoreColumns.SelectMany(projection => projection.Value.Select(column =>
          "has_column_privilege(current_user," + Literal(projection.Key) + ","
              + Literal(column) + ",'SELECT')"))));

    // Tracked terminal transitions change State/CompletedAt; the shared DbContext
    // stamps UpdatedAt/UpdatedBy/Version. No source, policy, maker or snapshot
    // column needs UPDATE, including for INSERT or the parent plan's row lock.
    internal static readonly IReadOnlyList<string> SubmissionUpdateColumns = Array.AsReadOnly(new[]
    {
        "state", "completed_at", "updated_at", "updated_by", "version",
    });

    // Inspect effective privileges, not just direct ACL rows: a table-level,
    // PUBLIC or inherited UPDATE on any current/future sealed column fails.
    // Apply to readers too, without requiring any transition UPDATE authority.
    internal static readonly string SubmissionImmutableColumnsSql =
        "NOT EXISTS(SELECT 1 FROM pg_attribute a "
        + "WHERE a.attrelid='procurement.plan_review_submissions'::regclass "
        + "AND a.attnum>0 AND NOT a.attisdropped "
        + "AND a.attname NOT IN (" + string.Join(",", SubmissionUpdateColumns.Select(Literal)) + ") "
        + "AND has_column_privilege(current_user,a.attrelid,a.attnum,'UPDATE'))";

    // Exact projection used by current Core people and optional worker alias
    // resolution. No password, session, payroll or unrelated HR fields.
    internal static readonly IReadOnlyDictionary<string, string[]> CoreColumns =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["hrm.local_users"] = ["id", "tenant_id", "is_active", "is_archived", "worker_id", "roles_csv", "locked_until"],
            ["hrm.tenant_role_assignments"] = ["tenant_id", "active", "is_archived", "role_key", "permissions_csv"],
            ["hrm.hr_user_branch_assignments"] = ["tenant_id", "user_id", "location_id", "is_archived"],
            ["hrm.legal_entities"] = ["tenant_id", "id", "is_archived"],
            ["hrm.work_locations"] = ["tenant_id", "id", "legal_entity_id", "is_archived"],
            ["hrm.workers"] = ["tenant_id", "id", "is_archived", "status", "location_id", "org_unit_id"],
            ["hrm.org_units"] = ["tenant_id", "id", "is_archived", "status", "legal_entity_id"],
        };
}
