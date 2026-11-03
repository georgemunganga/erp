using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;
using Npgsql;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Quotation READ and RECORD admission on the owning enlisted connection.
/// No workflow flag, active supplier, current employee or application permission
/// is inferred here. Those decisions remain in the application handlers.
/// Not registered until the actual finite-role/native contract is accepted.</summary>
public sealed class ProcurementManualQuotationSchemaReadiness(IConfiguration configuration)
    : IProcurementManualQuotationReadiness
{
    public bool HasCapturedContract => true;
    public Task<bool> CanReadAsync(ProcurementDbContext owner, CancellationToken ct) => CheckAsync(owner, false, ct);
    public Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct) => CheckAsync(owner, true, ct);

    private async Task<bool> CheckAsync(ProcurementDbContext owner, bool write, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (owner is null || !owner.Database.IsNpgsql()) return false;
        var connection = owner.Database.GetDbConnection();
        var transaction = owner.Database.CurrentTransaction?.GetDbTransaction();
        if (transaction is null || transaction.IsolationLevel != IsolationLevel.Serializable
            || !ReferenceEquals(transaction.Connection, connection)
            || !TrustedExecutionRole(configuration, connection.ConnectionString, out var role)) return false;
        if (!await ProcurementManualQuotationNativeShape.ReadyAsync(connection, transaction, ct)
            || ProcurementCompiledModelParity.HasPendingModelChanges(owner)
            || (await owner.Database.GetPendingMigrationsAsync(ct)).Any()
            || !(await owner.Database.GetAppliedMigrationsAsync(ct)).Contains(
                ProcurementManualQuotationNativeShape.MigrationId, StringComparer.Ordinal)) return false;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction; command.CommandTimeout = 15;
        command.CommandText = PrivilegeSql(write, role);
        return await command.ExecuteScalarAsync(ct) is true;
    }

    internal static bool TrustedExecutionRole(IConfiguration configuration, string actualConnection, out string role)
    {
        role = "";
        try
        {
            var execution = configuration.GetConnectionString("ProcurementApprovalExecution");
            var record = configuration.GetConnectionString("Procurement");
            if (string.IsNullOrWhiteSpace(execution) || string.IsNullOrWhiteSpace(record)
                || !InvoiceDocumentStoreBinding.SamePhysicalDatabase(execution, actualConnection)
                || !InvoiceDocumentStoreBinding.SamePhysicalDatabase(record, execution)) return false;
            var expected = new NpgsqlConnectionStringBuilder(execution);
            var actual = new NpgsqlConnectionStringBuilder(actualConnection);
            var recorder = new NpgsqlConnectionStringBuilder(record);
            if (string.IsNullOrWhiteSpace(expected.Username) || string.IsNullOrWhiteSpace(recorder.Username)
                || expected.Username != actual.Username || recorder.Username == expected.Username) return false;
            role = expected.Username; return true;
        }
        catch (Exception error) when (error is ArgumentException or FormatException) { return false; }
    }

    internal static readonly string[] ReadTables = [
        "__procurement_migrations", "sourcing_events", "sourcing_lines", "sourcing_invitations",
        "sourcing_draft_evidence", "sourcing_demand_bindings", "sourcing_command_receipts",
        "sourcing_publication_openings", "sourcing_publication_decisions", "sourcing_publication_receipts",
        "sourcing_policy_identity_evidence", "sourcing_manual_contacts", "sourcing_manual_quote_versions",
        "supplier_bids", "supplier_bid_lines", "suppliers", "supplier_qualifications",
        "purchase_requests", "purchase_request_lines", "purchase_request_allocations",
        "purchase_request_approval_submissions", "purchase_request_approval_outcomes",
        "policy_versions", "policy_evaluations", "budget_check_snapshots", "procurement_methods",
        "purchase_orders", "purchase_order_lines", "audit_events", "outbox_events"
    ];
    internal static readonly string[] AppendTables = [
        "sourcing_manual_contacts", "sourcing_manual_quote_versions", "audit_events", "outbox_events"
    ];
    internal static string PrivilegeSql(bool write, string role)
    {
        var clauses = new List<string> {
            "session_user=current_user AND current_user=" + Literal(role),
            ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql,
            "NOT has_database_privilege(current_user,current_database(),'CREATE')",
            "NOT has_schema_privilege(current_user,'procurement','CREATE')",
            "NOT has_schema_privilege(current_user,'core_approval','CREATE')",
            "NOT EXISTS(SELECT 1 FROM pg_roles r WHERE (r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls) AND pg_has_role(current_user,r.oid,'SET'))"
        };
        foreach (var table in ReadTables.Select(x => "procurement." + x)
            .Concat(["core_approval.cases", "core_approval.stages", "core_approval.assignments", "core_approval.decisions"]))
        {
            var name = Literal(table);
            clauses.Add("has_table_privilege(current_user," + name + ",'SELECT')");
            clauses.Add("NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid=" + name
                + "::regclass AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))");
        }
        foreach (var projection in ProcurementInvoiceReplacementSchemaReadiness.CoreReadColumns)
            foreach (var column in projection.Value)
                clauses.Add("has_column_privilege(current_user," + Literal(projection.Key) + "," + Literal(column) + ",'SELECT')");
        foreach (var table in AppendTables)
        {
            var name = Literal("procurement." + table);
            clauses.Add("NOT has_any_column_privilege(current_user," + name + ",'UPDATE')");
            clauses.Add("NOT has_any_column_privilege(current_user," + name + ",'REFERENCES')");
            clauses.Add("NOT has_table_privilege(current_user," + name + ",'DELETE,TRUNCATE,TRIGGER')");
            if (write) clauses.Add("has_table_privilege(current_user," + name + ",'INSERT')");
        }
        foreach (var table in new[] { "supplier_bids", "supplier_bid_lines", "sourcing_invitations" })
            clauses.Add("NOT has_table_privilege(current_user," + Literal("procurement." + table) + ",'DELETE,TRUNCATE,TRIGGER')");
        foreach (var action in new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE", "TRIGGER", "REFERENCES" })
            clauses.Add("NOT has_table_privilege(current_user,'procurement.finance_payment_status_refs'," + Literal(action) + ")");
        foreach (var action in new[] { "INSERT", "UPDATE", "REFERENCES" })
            clauses.Add("NOT has_any_column_privilege(current_user,'procurement.finance_payment_status_refs'," + Literal(action) + ")");
        if (write)
        {
            clauses.Add("current_setting('transaction_read_only')='off' AND NOT pg_is_in_recovery()");
            foreach (var table in new[] { "supplier_bids", "supplier_bid_lines" })
                {
                    clauses.Add("has_table_privilege(current_user," + Literal("procurement." + table) + ",'INSERT')");
                    clauses.Add("has_table_privilege(current_user," + Literal("procurement." + table) + ",'UPDATE')");
                }
            foreach (var table in new[] { "sourcing_events", "sourcing_invitations" })
                clauses.Add("has_any_column_privilege(current_user," + Literal("procurement." + table) + ",'UPDATE')");
        }
        return "SELECT " + string.Join(" AND ", clauses);
    }
}
