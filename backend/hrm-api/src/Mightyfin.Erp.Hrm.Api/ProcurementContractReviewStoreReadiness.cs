using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

internal enum ContractReviewStorePurpose { Read, PolicyWrite, ReviewWrite }

// Physical admission only. Existing callbacks resolve the current Core actor,
// branch, named participants and policy again; catalog presence grants nothing.
public sealed class ProcurementContractReviewStoreReadiness(ProcurementContractReviewOptions options,
    IConfiguration configuration, IServiceProvider services) : IProcurementContractReviewSchemaReadiness
{
    internal static readonly Type[] Dependencies = [typeof(PolicyVersion), typeof(SupplierContract),
        typeof(SupplierContractVersion), typeof(ContractRate), typeof(Supplier), typeof(AuditEvent)];
    public Task<bool> IsReadReadyAsync(ProcurementDbContext owner, CancellationToken ct) =>
        CheckAsync(owner, ContractReviewStorePurpose.Read, ct);
    public Task<bool> CanRecordPolicyAsync(ProcurementDbContext owner, CancellationToken ct) =>
        CheckAsync(owner, ContractReviewStorePurpose.PolicyWrite, ct);
    public Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct) =>
        CheckAsync(owner, ContractReviewStorePurpose.ReviewWrite, ct);

    // Availability uses the configured execution identity, never policy Record
    // privileges. Each action still checks its current actor and owned record.
    public async Task<bool> IsConfiguredAsync(CancellationToken ct)
    {
        if (!options.Enabled || !options.ExecutionDatabaseConfigured
            || services.GetService<ProcurementApprovalExecutionOptions>() is not
                { Enabled: true, PrincipalExecutionEnabled: true, ExecutionDatabaseConfigured: true }) return false;
        try
        {
            return await services.GetRequiredService<ProcurementApprovalTransaction>().ExecuteAsync(
                async (owner, cases, token) => await CanRecordAsync(owner, token), ct);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
    }

    private async Task<bool> CheckAsync(ProcurementDbContext owner, ContractReviewStorePurpose purpose, CancellationToken ct)
    {
        if (!options.Enabled || !options.ExecutionDatabaseConfigured || !ProcurementContractReviewSchemaCapture.Populated
            || owner is null || !owner.Database.IsNpgsql()) return false;
        IDbContextTransaction? ownedRead = null;
        try
        {
            // Comparison is physical-store identity only; policy Record and review
            // Execution retain their different SQL identities and privilege contracts.
            if (!InvoiceDocumentStoreBinding.SamePhysicalDatabase(
                configuration.GetConnectionString("ProcurementApprovalExecution"), owner.Database.GetDbConnection().ConnectionString)) return false;
            if (owner.Database.CurrentTransaction is null)
            {
                if (purpose != ContractReviewStorePurpose.Read) return false;
                ownedRead = await owner.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                await owner.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
            }
            var transaction = owner.Database.CurrentTransaction?.GetDbTransaction();
            if (transaction is null || transaction.IsolationLevel != IsolationLevel.Serializable
                || !ReferenceEquals(transaction.Connection, owner.Database.GetDbConnection())) return false;
            var principal = services.GetRequiredService<ProcurementPrincipalApprovalStoreReadiness>();
            if (!(purpose == ContractReviewStorePurpose.PolicyWrite
                ? await principal.IsReadyAsync(owner, ct)
                : await principal.CanReadWorkflowAsync(owner, ct))) return false;
            var types = ProcurementContractReviewSchemaCapture.Types.Concat(Dependencies).ToArray();
            string Table(Type type)
            {
                var entity = owner.Model.FindEntityType(type) ?? throw new InvalidOperationException("ContractReviewEntityMissing");
                return entity.GetSchema() + "." + entity.GetTableName();
            }
            var reads = types.Select(Table).Distinct(StringComparer.Ordinal).ToArray();
            return await ProcurementPrincipalCommercialSchemaInspection.CheckAsync(owner, types,
                PrivilegeSql(purpose, reads), ProcurementContractReviewSchemaCapture.ForeignNames.ToHashSet(StringComparer.Ordinal),
                ProcurementContractReviewSchemaCapture.Physical, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
        finally { if (ownedRead is not null) await ownedRead.DisposeAsync(); }
    }

    internal static string PrivilegeSql(ContractReviewStorePurpose purpose, IReadOnlyList<string> reads)
    {
        var clauses = reads.Select(x => "has_table_privilege(current_user," + Literal(x) + ",'SELECT')").ToList();
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))");
        clauses.Add("NOT has_schema_privilege(current_user,'procurement','CREATE')");
        if (purpose != ContractReviewStorePurpose.PolicyWrite)
            clauses.Add("(" + ProcurementInvoiceUnitReviewSchemaReadiness.CoreReadBoundarySql[7..] + ")");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid=ANY(" + Columns(reads.ToArray())
            + "::regclass[]) AND pg_has_role(current_user,c.relowner,'MEMBER'))");
        foreach (var table in ProcurementContractReviewSchemaCapture.TableNames.Append("procurement.audit_events"))
        {
            clauses.Add("NOT has_any_column_privilege(current_user," + Literal(table) + ",'UPDATE')");
            clauses.Add("NOT has_table_privilege(current_user," + Literal(table) + ",'DELETE,TRUNCATE')");
        }
        void Require(string table, string operation) => clauses.Add("has_table_privilege(current_user," + Literal(table) + ",'" + operation + "')");
        if (purpose != ContractReviewStorePurpose.Read) Require("procurement.audit_events", "INSERT");
        if (purpose == ContractReviewStorePurpose.PolicyWrite)
        {
            Require("procurement.policy_versions", "INSERT");
            Require("procurement.policy_versions", "UPDATE");
            Require("procurement.contract_review_policy_identity", "INSERT");
            clauses.Add("(" + ProcurementPrincipalApprovalStoreReadiness.NoCoreWorkflowDmlSql[7..] + ")");
        }
        if (purpose == ContractReviewStorePurpose.ReviewWrite)
        {
            foreach (var table in ProcurementContractReviewSchemaCapture.TableNames.Where(x => x != "procurement.contract_review_policy_identity")) Require(table, "INSERT");
            Require("procurement.supplier_contracts", "UPDATE");
            Require("procurement.supplier_contract_versions", "UPDATE");
            foreach (var table in new[] { "cases", "stages", "assignments" })
            {
                Require("core_approval." + table, "SELECT");
                Require("core_approval." + table, "INSERT");
                Require("core_approval." + table, "UPDATE");
                clauses.Add("NOT has_table_privilege(current_user," + Literal("core_approval." + table) + ",'DELETE,TRUNCATE')");
            }
            clauses.Add("(" + ProcurementInvoiceUnitReviewSchemaReadiness.CoreDecisionPrivilegesSql[7..] + ")");
            clauses.Add("(" + ProcurementPrincipalApprovalStoreReadiness.NoOtherCoreWorkflowDmlSql[7..] + ")");
        }
        return "SELECT " + string.Join(" AND ", clauses);
    }
}
