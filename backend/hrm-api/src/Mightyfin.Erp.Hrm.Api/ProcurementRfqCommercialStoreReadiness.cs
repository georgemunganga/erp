using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Finite, owning-transaction P32 owner admission for Draft and
/// commercial evidence. Core Documents is checked at actual document-use
/// purposes; an empty Draft does not use that store. Current Core permissions
/// and commercial policy are checked by routes and shared workflow.</summary>
public sealed class ProcurementRfqCommercialStoreReadiness(
    ProcurementRfqCommercialOptions options, IConfiguration configuration,
    IServiceProvider services,
    ProcurementPrincipalApprovalStoreReadiness principalStore,
    IProcurementSourcingDraftSchemaReadiness sourceDrafts,
    IProcurementPrincipalSourcingPublicationReadiness publication)
    : IProcurementRfqCommercialReadiness
{
    private static readonly Type[] Dependencies =
    [
        typeof(SourcingEvent), typeof(SourcingLine), typeof(SourcingInvitation),
        typeof(SupplierBid), typeof(SupplierBidLine), typeof(SourcingDraftEvidence),
        typeof(SourcingDemandBinding), typeof(PolicyVersion),
        typeof(OperationalPolicyIdentityRecord), typeof(Supplier),
        typeof(PurchaseRequest), typeof(PurchaseRequestLine),
        typeof(PurchaseRequestAllocation), typeof(AuditEvent), typeof(OutboxEvent)
    ];
    private static readonly string[] CoreCaseTables =
        ["core_approval.cases", "core_approval.stages", "core_approval.assignments"];

    public async Task<bool> IsReadyAsync(ProcurementDbContext owner, bool write, CancellationToken ct)
    {
        if (!options.Enabled || !ProcurementRfqCommercialSchemaCapture.Populated
            || owner is null || !owner.Database.IsNpgsql()
            || owner.Database.CurrentTransaction is not { } transaction
            || transaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable
            || !ReferenceEquals(transaction.GetDbTransaction().Connection, owner.Database.GetDbConnection())
            || services.GetService<ProcurementApprovalExecutionOptions>() is not
                { Enabled: true, PrincipalExecutionEnabled: true, ExecutionDatabaseConfigured: true })
            return false;
        try
        {
            var execution = configuration.GetConnectionString("ProcurementApprovalExecution");
            if (string.IsNullOrWhiteSpace(execution)
                || !InvoiceDocumentStoreBinding.SamePhysicalDatabase(
                    execution, owner.Database.GetDbConnection().ConnectionString)
                || !await sourceDrafts.IsReadyAsync(owner, false, ct)
                // Commercial review reads immutable P30 publication evidence.
                // It does not require an evaluator to hold P30 publish/PR-write rights.
                || !await publication.IsReadyAsync(owner, false, ct)
                || !await principalStore.CanReadWorkflowAsync(owner, ct)
                || services.GetService<IProcurementOperationalPolicyIdentitySchemaReadiness>() is not { } identity
                || !await identity.IsReadyAsync(owner, ct)) return false;

            var design = owner.GetService<IDesignTimeModel>().Model;
            var expectedTables = new HashSet<string>(StringComparer.Ordinal);
            var expectedChecks = new HashSet<string>(StringComparer.Ordinal);
            foreach (var type in ProcurementRfqCommercialSchemaCapture.Types)
            {
                var entity = design.FindEntityType(type);
                if (entity is null || entity.GetSchema() != "procurement"
                    || entity.GetTableName() is not { } name) return false;
                expectedTables.Add("procurement." + name);
                foreach (var check in entity.GetCheckConstraints())
                    expectedChecks.Add("procurement." + name + "." + check.Name);
            }
            if (!MatchesModelCatalog(expectedTables, expectedChecks))
                return false;
            var reads = ProcurementRfqCommercialSchemaCapture.Types.Concat(Dependencies)
                .Select(type => design.FindEntityType(type))
                .ToArray();
            if (reads.Any(x => x is null || x.GetSchema() != "procurement" || x.GetTableName() is null))
                return false;
            var tables = reads.Select(x => "procurement." + x!.GetTableName())
                .Distinct(StringComparer.Ordinal).ToArray();
            var privileges = PrivilegeSql(tables, write);
            return await ProcurementPrincipalCommercialSchemaInspection.CheckAsync(owner,
                ProcurementRfqCommercialSchemaCapture.Types, privileges,
                ProcurementRfqCommercialSchemaCapture.OwnershipNames,
                ProcurementRfqCommercialSchemaCapture.Physical, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
    }

    internal static bool MatchesModelCatalog(IReadOnlySet<string> tables, IReadOnlySet<string> checks)
        => tables.Count == ProcurementRfqCommercialSchemaCapture.TableNames.Count
           && checks.Count == ProcurementRfqCommercialSchemaCapture.Checks.Count
           && tables.All(ProcurementRfqCommercialSchemaCapture.TableNames.Contains)
           && checks.All(ProcurementRfqCommercialSchemaCapture.Checks.ContainsKey);

    internal static string PrivilegeSql(IReadOnlyList<string> readTables, bool write)
    {
        var owned = ProcurementRfqCommercialSchemaCapture.TableNames.Order(StringComparer.Ordinal).ToArray();
        var append = owned.Where(x => x != "procurement.rfq_commercial_drafts")
            .Concat(["procurement.audit_events", "procurement.outbox_events"])
            .Distinct(StringComparer.Ordinal).ToArray();
        var clauses = readTables.Select(x => "has_table_privilege(current_user," + Literal(x) + ",'SELECT')")
            .ToList();
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND "
            + "(rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))");
        clauses.Add("NOT has_schema_privilege(current_user,'procurement','CREATE')");
        clauses.Add("NOT has_schema_privilege(current_user,'core_approval','CREATE')");
        foreach (var relation in readTables.Concat(CoreCaseTables).Append("core_approval.decisions")
                     .Distinct(StringComparer.Ordinal))
        {
            clauses.Add("NOT has_table_privilege(current_user," + Literal(relation) + ",'DELETE,TRUNCATE')");
            clauses.Add("NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid=" + Literal(relation)
                + "::regclass AND (pg_has_role(current_user,c.relowner,'MEMBER')"
                + " OR pg_has_role(current_user,c.relowner,'SET')))");
        }
        foreach (var relation in append.Append("core_approval.decisions"))
            clauses.Add("NOT has_any_column_privilege(current_user," + Literal(relation) + ",'UPDATE')");
        foreach (var relation in CoreCaseTables.Append("core_approval.decisions"))
            clauses.Add("has_table_privilege(current_user," + Literal(relation) + ",'SELECT')");
        if (write)
        {
            foreach (var relation in owned.Concat(["procurement.audit_events", "procurement.outbox_events"])
                .Concat(CoreCaseTables).Append("core_approval.decisions"))
                clauses.Add("has_table_privilege(current_user," + Literal(relation) + ",'INSERT')");
            clauses.Add("has_table_privilege(current_user,'procurement.rfq_commercial_drafts','UPDATE')");
            foreach (var relation in CoreCaseTables)
                clauses.Add("has_table_privilege(current_user," + Literal(relation) + ",'UPDATE')");
        }
        return "SELECT " + string.Join(" AND ", clauses);
    }
}
