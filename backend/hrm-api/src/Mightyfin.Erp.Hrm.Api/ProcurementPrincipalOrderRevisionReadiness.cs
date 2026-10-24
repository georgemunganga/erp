using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

public sealed class ProcurementPrincipalOrderRevisionReadiness(
    PrincipalOrderRevisionOptions options, IServiceProvider services) : IPrincipalOrderRevisionReadiness
{
    // Metadata uses the existing execution transaction/context. It must never
    // inspect the unrelated Record connection and advertise execution readiness.
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        if (!options.Enabled || !ProcurementPrincipalOrderRevisionSchemaCapture.Populated
            || services.GetService<ProcurementApprovalExecutionOptions>() is not
                { Enabled: true, PrincipalExecutionEnabled: true, ExecutionDatabaseConfigured: true }) return false;
        try
        {
            return await services.GetRequiredService<ProcurementApprovalTransaction>().ExecuteAsync(
                async (owner, unusedCases, token) => await IsReadyAsync(owner, token), ct);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
    }

    public async Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct)
    {
        if (!options.Enabled || !ProcurementPrincipalOrderRevisionSchemaCapture.Populated
            || owner is null || !owner.Database.IsNpgsql()
            || owner.Database.CurrentTransaction is not { } transaction
            || transaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.Serializable
            || !ReferenceEquals(transaction.GetDbTransaction().Connection, owner.Database.GetDbConnection())) return false;
        try
        {
            if (!await services.GetRequiredService<IProcurementPrincipalOrderDraftReadiness>()
                .IsReadyAsync(owner, true, ct)
                || !await ProcurementPrincipalOrderDraftHistory.InstalledAsync(owner, ct)) return false;
            return await ProcurementPrincipalCommercialSchemaInspection.CheckAsync(owner,
                ProcurementPrincipalOrderRevisionSchemaCapture.Types,
                "SELECT " + string.Join(" AND ", HistoryPrivilegeClauses(true)),
                new HashSet<string>(StringComparer.Ordinal), ct);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
    }

    internal static IReadOnlyList<string> HistoryPrivilegeClauses(bool insert)
    {
        var clauses = new List<string>
        {
            "NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))",
            "NOT has_schema_privilege(current_user,'procurement','CREATE')"
        };
        foreach (var table in ProcurementPrincipalOrderRevisionSchemaCapture.Tables)
        {
            var quoted = Literal(table);
            clauses.Add("has_table_privilege(current_user," + quoted + ",'SELECT')");
            clauses.Add("NOT has_any_column_privilege(current_user," + quoted + ",'UPDATE')");
            clauses.Add("NOT has_table_privilege(current_user," + quoted + ",'DELETE,TRUNCATE')");
            clauses.Add("NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid=" + quoted
                + "::regclass AND pg_has_role(current_user,c.relowner,'MEMBER'))");
            if (insert) clauses.Add("has_table_privilege(current_user," + quoted + ",'INSERT')");
        }
        return clauses;
    }
}
