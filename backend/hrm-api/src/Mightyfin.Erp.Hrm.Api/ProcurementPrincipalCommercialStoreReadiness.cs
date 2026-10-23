using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

internal enum PrincipalCommercialStore { Orders, Publication, Receiving }

internal sealed class ProcurementPrincipalCommercialStoreReadiness(IConfiguration configuration,
    IProcurementScope scope, ProcurementPrincipalApprovalStoreReadiness principalStore)
{
    private static readonly Type[] Dependencies = [typeof(PurchaseRequest), typeof(PurchaseRequestLine),
        typeof(PurchaseRequestAllocation), typeof(Supplier), typeof(SupplierQualification), typeof(CatalogItem),
        typeof(CatalogEntry), typeof(PolicyVersion), typeof(PolicyEvaluation), typeof(BudgetCheckSnapshot),
        typeof(PurchaseRequestApprovalSubmission), typeof(PurchaseRequestApprovalOutcome),
        typeof(PurchaseRequestAssignmentAuthorityEvidence), typeof(ProcurementSpendingAuthorityGrant),
        typeof(ProcurementSpendingAuthorityCategory), typeof(ProcurementMethod), typeof(AuditEvent), typeof(OutboxEvent)];

    internal async Task<bool> ConfiguredAsync(PrincipalCommercialStore kind, CancellationToken ct)
    {
        try
        {
            var connection = configuration.GetConnectionString("ProcurementApprovalExecution");
            if (string.IsNullOrWhiteSpace(connection)) return false;
            await using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql(connection, x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, scope);
            return await CheckAsync(owner, kind, true, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
    }

    internal async Task<bool> CheckAsync(ProcurementDbContext owner, PrincipalCommercialStore kind,
        bool write, CancellationToken ct)
    {
        var types = kind switch
        {
            PrincipalCommercialStore.Orders => ProcurementPrincipalCommercialSchemaCapture.Orders,
            PrincipalCommercialStore.Publication => ProcurementPrincipalCommercialSchemaCapture.Publication,
            _ => ProcurementPrincipalCommercialSchemaCapture.Receiving,
        };
        // Reuse the accepted Core/PR/Finance physical contract. This does not
        // replace current actor, branch, policy, funding or SOD checks in commands.
        if (!(write && kind != PrincipalCommercialStore.Receiving
            ? await principalStore.CanExecuteAsync(owner, ct)
            : await principalStore.IsReadyAsync(owner, ct))) return false;
        var model = owner.Model;
        string Table(Type type)
        {
            var entity = model.FindEntityType(type) ?? throw new InvalidOperationException("CommercialEntityMissing");
            return entity.GetSchema() + "." + entity.GetTableName();
        }
        var inspected = kind == PrincipalCommercialStore.Receiving
            ? types.Concat(ProcurementPrincipalCommercialSchemaCapture.Orders).Distinct().ToArray() : types;
        var owned = types.Select(Table).Distinct(StringComparer.Ordinal).ToArray();
        var reads = Dependencies.Concat(inspected).Select(Table).Distinct(StringComparer.Ordinal).ToArray();
        var mutable = (kind switch
        {
            PrincipalCommercialStore.Orders => new[] { "purchase_orders", "purchase_order_lines" },
            PrincipalCommercialStore.Publication => new[] { "sourcing_events", "sourcing_lines", "sourcing_invitations", "supplier_bids", "supplier_bid_lines" },
            _ => new[] { "principal_receiving_sources", "principal_receiving_drafts", "purchase_order_lines", "purchase_request_lines" },
        }).Select(x => "procurement." + x).ToHashSet(StringComparer.Ordinal);
        var append = owned.Where(x => !mutable.Contains(x))
            .Concat(["procurement.audit_events", "procurement.outbox_events"]).Distinct(StringComparer.Ordinal).ToArray();
        var clauses = reads.Select(x => "has_table_privilege(current_user," + Literal(x) + ",'SELECT')").ToList();
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))");
        clauses.Add("NOT has_schema_privilege(current_user,'procurement','CREATE')");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid=ANY(" + Columns(reads)
            + "::regclass[]) AND pg_has_role(current_user,c.relowner,'MEMBER'))");
        foreach (var table in mutable)
            clauses.Add("NOT has_table_privilege(current_user," + Literal(table) + ",'DELETE,TRUNCATE')");
        foreach (var table in append)
            clauses.Add("NOT has_table_privilege(current_user," + Literal(table) + ",'UPDATE,DELETE,TRUNCATE')");
        if (write)
        {
            foreach (var table in append) clauses.Add("has_table_privilege(current_user," + Literal(table) + ",'INSERT')");
            foreach (var table in mutable)
            {
                clauses.Add("has_table_privilege(current_user," + Literal(table) + ",'UPDATE')");
                if (kind != PrincipalCommercialStore.Receiving || table.StartsWith("procurement.principal_receiving_", StringComparison.Ordinal))
                    clauses.Add("has_table_privilege(current_user," + Literal(table) + ",'INSERT')");
            }
            if (kind != PrincipalCommercialStore.Receiving)
                foreach (var table in new[] { "purchase_requests", "purchase_request_lines", "purchase_request_allocations" })
                    clauses.Add("has_table_privilege(current_user," + Literal("procurement." + table) + ",'UPDATE')");
            if (kind == PrincipalCommercialStore.Orders)
                clauses.Add("has_table_privilege(current_user,'procurement.purchase_request_allocations','INSERT')");
        }
        var overlayNames = kind == PrincipalCommercialStore.Receiving
            ? ProcurementPrincipalCommercialSchemaCapture.ReceivingOwnershipNames
            : new HashSet<string>(StringComparer.Ordinal);
        return await ProcurementPrincipalCommercialSchemaInspection.CheckAsync(owner, inspected,
            "SELECT " + string.Join(" AND ", clauses), overlayNames, ct,
            kind == PrincipalCommercialStore.Receiving ? PrincipalReceivingOwnershipConstraintSql.ManifestQuery : null);
    }
}

public sealed class ProcurementPrincipalOrderDraftReadiness(IConfiguration configuration,
    IProcurementScope scope, ProcurementPrincipalApprovalStoreReadiness principalStore) : IProcurementPrincipalOrderDraftReadiness
{
    private readonly ProcurementPrincipalCommercialStoreReadiness store = new(configuration, scope, principalStore);
    public Task<bool> IsReadyAsync(CancellationToken ct) => store.ConfiguredAsync(PrincipalCommercialStore.Orders, ct);
    public Task<bool> IsReadyAsync(ProcurementDbContext owner, bool write, CancellationToken ct) =>
        store.CheckAsync(owner, PrincipalCommercialStore.Orders, write, ct);
}

public sealed class ProcurementPrincipalSourcingPublicationReadiness(IConfiguration configuration,
    IProcurementScope scope, ProcurementPrincipalApprovalStoreReadiness principalStore) : IProcurementPrincipalSourcingPublicationReadiness
{
    private readonly ProcurementPrincipalCommercialStoreReadiness store = new(configuration, scope, principalStore);
    public Task<bool> IsReadyAsync(CancellationToken ct) => store.ConfiguredAsync(PrincipalCommercialStore.Publication, ct);
    public Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct) =>
        store.CheckAsync(owner, PrincipalCommercialStore.Publication, true, ct);
    public Task<bool> IsReadyAsync(ProcurementDbContext owner, bool write, CancellationToken ct) =>
        store.CheckAsync(owner, PrincipalCommercialStore.Publication, write, ct);
}

public sealed class ProcurementPrincipalReceivingReadiness(IConfiguration configuration,
    IProcurementScope scope, ProcurementPrincipalApprovalStoreReadiness principalStore) : IProcurementPrincipalReceivingReadiness
{
    private readonly ProcurementPrincipalCommercialStoreReadiness store = new(configuration, scope, principalStore);
    public Task<bool> IsReadyAsync(CancellationToken ct) => store.ConfiguredAsync(PrincipalCommercialStore.Receiving, ct);
    public Task<bool> IsReadyAsync(ProcurementDbContext owner, bool write, CancellationToken ct) =>
        store.CheckAsync(owner, PrincipalCommercialStore.Receiving, write, ct);
}
