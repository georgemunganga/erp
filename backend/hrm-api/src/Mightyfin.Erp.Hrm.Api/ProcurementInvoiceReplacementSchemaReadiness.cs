using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;
using Npgsql;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Physical/finite-role admission on the actual caller transaction.
/// Genuine E schema capture exists; finite-grant positive proof and registration
/// are separate release gates. This class is deliberately NOT registered yet.
/// No actor, source eligibility, policy, or Finance authority is inferred here.</summary>
public sealed class ProcurementInvoiceReplacementSchemaReadiness(IConfiguration configuration)
    : IProcurementInvoiceReplacementSchemaReadiness
{
    public Task<bool> CanReadAsync(ProcurementDbContext db, CancellationToken ct) => CheckAsync(db, false, ct);
    public Task<bool> CanRecordAsync(ProcurementDbContext db, CancellationToken ct) => CheckAsync(db, true, ct);

    private async Task<bool> CheckAsync(ProcurementDbContext db, bool write, CancellationToken ct)
    {
        if (db is null || !db.Database.IsNpgsql()) return false;
        var connection = db.Database.GetDbConnection();
        var transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        if (transaction is null || transaction.IsolationLevel != IsolationLevel.Serializable
            || !ReferenceEquals(transaction.Connection, connection)
            || !TrustedRole(configuration, connection.ConnectionString, write, out var role)) return false;
        // Safe catalog lookup returns false when this additive schema is absent.
        // Unexpected database/provider errors propagate to the existing API error
        // handling; they are not silently converted into business ineligibility.
        if (!await ProcurementInvoiceReplacementNativeShape.ReadyAsync(connection, transaction, ct)
            || db.Database.HasPendingModelChanges()
            || (await db.Database.GetPendingMigrationsAsync(ct)).Any()) return false;
        var applied = await db.Database.GetAppliedMigrationsAsync(ct);
        if (!applied.Contains(ProcurementInvoiceReplacementNativeShape.MigrationId, StringComparer.Ordinal)) return false;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;command.CommandTimeout = 15;
        command.CommandText = PrivilegeSql(write, role);
        return await command.ExecuteScalarAsync(ct) is true;
    }

    internal static bool TrustedRole(IConfiguration configuration, string actualConnection, bool write, out string role)
    {
        role = "";
        try
        {
            var actual = new NpgsqlConnectionStringBuilder(actualConnection);
            foreach (var key in write ? new[] { "Procurement" } : new[] { "Procurement", "ProcurementApprovalExecution" })
            {
                var trusted = configuration.GetConnectionString(key);
                if (string.IsNullOrWhiteSpace(trusted)
                    || !InvoiceDocumentStoreBinding.SamePhysicalDatabase(trusted, actualConnection)) continue;
                var expected = new NpgsqlConnectionStringBuilder(trusted);
                if (!string.IsNullOrWhiteSpace(expected.Username) && expected.Username == actual.Username)
                { role = expected.Username;return true; }
            }
        }
        catch (ArgumentException) { }
        return false;
    }

    // Existing history permission contracts are reused rather than broadening a
    // role or pretending that SELECT on just the two new tables proves readiness.
    internal static string PrivilegeSql(bool write, string role)
    {
        var clauses = new List<string> {
            ProcurementInvoiceOwnerBaselineReadiness.PrivilegeSql(false, role)[7..],
            ProcurementInvoiceCurrencySchemaReadiness.PrivilegeSql(InvoiceCurrencySchemaMode.RecordRead, role)[7..],
            "has_table_privilege(current_user,'procurement.commercial_command_receipts','SELECT')"
        };
        foreach (var table in ProcurementInvoiceReplacementNativeShape.Tables)
        {
            clauses.Add("has_table_privilege(current_user," + Literal(table) + ",'SELECT')");
            clauses.Add("NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid=" + Literal(table)
                + "::regclass AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))");
            clauses.Add("NOT has_any_column_privilege(current_user," + Literal(table) + ",'UPDATE')");
            clauses.Add("NOT has_any_column_privilege(current_user," + Literal(table) + ",'REFERENCES')");
            clauses.Add("NOT has_table_privilege(current_user," + Literal(table) + ",'DELETE,TRUNCATE,TRIGGER')");
            if (write) clauses.Add("has_table_privilege(current_user," + Literal(table) + ",'INSERT')");
        }
        if (write)
        {
            clauses.Add("current_setting('transaction_read_only')='off' AND NOT pg_is_in_recovery()");
            clauses.Add(ProcurementPrincipalOrderDraftSchemaReadiness.PrivilegeSql(false, role)[7..]);
            foreach (var table in WriteAdditionalReads)
                clauses.Add("has_table_privilege(current_user," + Literal(table) + ",'SELECT')");
            foreach (var table in new[] { "supplier_invoices", "supplier_invoice_lines", "invoice_amount_calculations",
                "invoice_intake_command_receipts", "audit_events", "outbox_events" })
                clauses.Add("has_table_privilege(current_user," + Literal("procurement." + table) + ",'INSERT')");
            // PostgreSQL SELECT FOR UPDATE requires UPDATE on at least one
            // column. This route locks these rows but does not edit their values.
            foreach (var table in new[] { "purchase_requests", "purchase_orders", "supplier_invoices", "invoice_match_exposure_states" })
                clauses.Add("has_any_column_privilege(current_user," + Literal("procurement." + table) + ",'UPDATE')");
        }
        return "SELECT " + string.Join(" AND ", clauses);
    }

    internal static readonly string[] WriteAdditionalReads = [
        "procurement.principal_order_submissions", "procurement.principal_order_issued_versions",
        "procurement.principal_order_funding_evidence", "procurement.principal_order_acknowledgments",
        "procurement.principal_order_assignments", "procurement.principal_order_decisions",
        "procurement.principal_receiving_sources", "procurement.principal_receiving_facts",
        "procurement.principal_receiving_revisions", "procurement.principal_receiving_receipts",
        "procurement.principal_receiving_line_bindings", "procurement.policy_evaluations",
        "procurement.sourcing_events", "procurement.sourcing_lines"
    ];
}
