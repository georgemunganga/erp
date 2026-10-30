using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Retained payment schema proof on the actual owning connection. No grants,
/// migrations, policy activation, supplier eligibility or document service is supplied.</summary>
public sealed class ProcurementManualPaymentReadSchemaReadiness : IProcurementManualPaymentReadReadiness
{
    public async Task<bool> CanReadAsync(ProcurementDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql() || db.Database.CurrentTransaction?.GetDbTransaction() is not { } tx
            || tx.IsolationLevel != IsolationLevel.Serializable
            || !ReferenceEquals(tx.Connection, db.Database.GetDbConnection())
            || ProcurementCompiledModelParity.HasPendingModelChanges(db)) return false;
        var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToArray();
        if (!applied.Contains(ProcurementManualPaymentGovernanceReadiness.MigrationId, StringComparer.Ordinal)
            || (await db.Database.GetPendingMigrationsAsync(ct)).Any()) return false;
        var ownerBaseline = applied.Contains(ProcurementInvoiceOwnerBaselineSchemaCapture.MigrationId, StringComparer.Ordinal);
        var expected = ownerBaseline ? ProcurementInvoiceOwnerBaselineSchemaCapture.ManualPaymentCatalogFingerprint
            : ProcurementManualPaymentGovernanceReadiness.ExpectedFingerprint;
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = tx; command.CommandTimeout = 15;
        command.CommandText = ProcurementManualPaymentGovernanceReadiness.FingerprintSql;
        var valid = await command.ExecuteScalarAsync(ct) is string actual && actual == expected;
        if (!valid)
        {
            command.CommandText = ProcurementManualPaymentGovernanceReadiness.FingerprintSqlWithObservedRestoreChecks(ownerBaseline);
            valid = await command.ExecuteScalarAsync(ct) is string restored && restored == expected;
        }
        if (!valid) return false;
        command.CommandText = PrivilegeSql();
        if (await command.ExecuteScalarAsync(ct) is not true) return false;
        command.CommandText = ProcurementManualPaymentGovernanceReadiness.CoreOwnerSql;
        return await command.ExecuteScalarAsync(ct) is true;
    }

    internal static readonly string[] RecordReadTables = [
        "procurement.manual_payment_requests", "procurement.manual_payment_decisions",
        "procurement.manual_payment_receipts", "procurement.manual_payment_workflow_cases",
        "procurement.manual_payment_core_decisions", "procurement.__procurement_migrations",
        "core_approval.cases", "core_approval.decisions", "core_approval.\"__EFMigrationsHistory\"",
        "hrm.__hrm_migrations"
    ];

    // Actual projections used by current Core principal/scope resolution and labels;
    // no passwords, sessions, contact directory, payroll or supplier bank data.
    internal static readonly IReadOnlyDictionary<string, string[]> ReadColumns = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["hrm.local_users"] = ["id", "tenant_id", "is_active", "is_archived", "worker_id", "roles_csv", "locked_until", "display_name"],
        ["hrm.tenant_role_assignments"] = ["tenant_id", "active", "is_archived", "role_key", "permissions_csv"],
        ["hrm.hr_user_branch_assignments"] = ["tenant_id", "user_id", "location_id", "is_archived"],
        ["hrm.legal_entities"] = ["tenant_id", "id", "is_archived", "time_zone_id"],
        ["hrm.work_locations"] = ["tenant_id", "id", "legal_entity_id", "is_archived", "code", "name"],
        ["hrm.workers"] = ["tenant_id", "id", "is_archived", "status", "location_id", "org_unit_id"],
        ["hrm.org_units"] = ["tenant_id", "id", "is_archived", "status", "legal_entity_id"],
        ["procurement.supplier_invoices"] = ["tenant_id", "legal_entity_id", "id", "supplier_id", "branch_ref", "invoice_number", "currency_code", "gross_amount", "invoice_date"],
        ["procurement.suppliers"] = ["tenant_id", "legal_entity_id", "id", "display_name"]
    };

    internal static string PrivilegeSql()
    {
        var clauses = new List<string> {
            "current_setting('transaction_read_only')='on'",
            "NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))",
            "NOT has_schema_privilege(current_user,'procurement','CREATE')",
            "NOT has_schema_privilege(current_user,'core_approval','CREATE')",
            "has_schema_privilege(current_user,'procurement','USAGE')",
            "has_schema_privilege(current_user,'core_approval','USAGE')",
            "has_schema_privilege(current_user,'hrm','USAGE')",
            ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql,
            ProcurementSpendingAuthorityIdentitySchemaReadiness.NoOwnershipSql,
            "NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('procurement','core_approval') AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))"
        };
        foreach (var table in RecordReadTables) clauses.Add("has_table_privilege(current_user," + Literal(table) + ",'SELECT')");
        foreach (var projection in ReadColumns)
            foreach (var column in projection.Value)
                clauses.Add("has_column_privilege(current_user," + Literal(projection.Key) + "," + Literal(column) + ",'SELECT')");
        // Existing finite writer roles remain valid readers; no INSERT/UPDATE grant is
        // required. Retained evidence must still be protected against destructive edits.
        foreach (var table in RecordReadTables)
            clauses.Add("NOT has_table_privilege(current_user," + Literal(table) + ",'DELETE,TRUNCATE,TRIGGER')");
        foreach (var table in new[] { "procurement.manual_payment_decisions", "procurement.manual_payment_receipts",
            "procurement.manual_payment_workflow_cases", "procurement.manual_payment_core_decisions", "core_approval.decisions" })
            clauses.Add("NOT has_any_column_privilege(current_user," + Literal(table) + ",'UPDATE')");
        foreach (var privilege in new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE" })
            clauses.Add("NOT has_table_privilege(current_user,'procurement.finance_payment_status_refs'," + Literal(privilege) + ")");
        foreach (var privilege in new[] { "INSERT", "UPDATE" })
            clauses.Add("NOT has_any_column_privilege(current_user,'procurement.finance_payment_status_refs'," + Literal(privilege) + ")");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname IN ('procurement','core_approval') AND (pg_has_role(current_user,p.proowner,'MEMBER') OR pg_has_role(current_user,p.proowner,'SET')))");
        return "SELECT " + string.Join(" AND ", clauses);
    }
}
