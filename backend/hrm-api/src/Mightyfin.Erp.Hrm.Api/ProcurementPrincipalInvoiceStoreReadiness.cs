using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>The P31 owner has no generated migration or PostgreSQL capture yet.
/// Keep both read and write admission false until the genuine owner run fills
/// this exact manifest. Model declarations alone are not runtime proof.</summary>
internal static class ProcurementPrincipalInvoiceSchemaCapture
{
    internal const string MigrationId = "";
    internal static readonly IReadOnlyDictionary<string, string> Checks = new Dictionary<string, string>();
    internal static readonly IReadOnlyDictionary<string, string> OwnershipConstraints = new Dictionary<string, string>();
    internal static readonly IReadOnlySet<string> OwnershipNames = new HashSet<string>(StringComparer.Ordinal);
    internal const string OwnershipManifestSql = "SELECT false";
    internal static bool Populated => !string.IsNullOrWhiteSpace(MigrationId)
        && Checks.Count >= Types.Length && OwnershipConstraints.Count == OwnershipNames.Count
        && OwnershipNames.All(OwnershipConstraints.ContainsKey)
        && OwnershipManifestSql != "SELECT false";

    internal static readonly Type[] Types = [typeof(PrincipalInvoiceState), typeof(PrincipalInvoiceMappingVersion),
        typeof(PrincipalInvoiceMappingLine), typeof(PrincipalInvoiceUnitReviewSource),
        typeof(PrincipalInvoiceUnitReviewAssignment), typeof(PrincipalInvoiceUnitReviewAttestation),
        typeof(PrincipalInvoiceUnitReviewOutcome), typeof(PrincipalInvoiceComparison),
        typeof(PrincipalInvoiceExposureState), typeof(PrincipalInvoiceClaim),
        typeof(PrincipalInvoiceCommandReceipt), typeof(PrincipalInvoiceDiscrepancy),
        typeof(PrincipalInvoiceDiscrepancyAction)];

    internal static readonly ProcurementPrincipalCommercialSchemaInspection.Capture Physical =
        new(MigrationId, Checks, OwnershipConstraints);
}

/// <summary>Finite physical admission for P31 only. The current Core actor,
/// branch, policy, document pins and source history are still rechecked by
/// each command. A read role needs SELECT, while commands additionally need
/// only the owned mutable/append grants and shared Core workflow authority.</summary>
public sealed class ProcurementPrincipalInvoiceStoreReadiness(IConfiguration configuration,
    IProcurementScope scope, ProcurementRequestScope requestScope,
    ProcurementPrincipalApprovalStoreReadiness principalStore,
    IProcurementPrincipalOrderDraftReadiness orders, IProcurementPrincipalReceivingReadiness receiving,
    ProcurementApprovalTransaction transaction, IServiceProvider services)
    : IProcurementPrincipalInvoiceSchemaReadiness
{
    public Task<bool> IsReadyAsync(CancellationToken ct) => ConfiguredAsync(true, ct);
    public Task<bool> IsReadReadyAsync(CancellationToken ct) => ConfiguredAsync(false, ct);
    public Task<bool> IsDownloadReadyAsync(CancellationToken ct) => ConfiguredAsync(false, ct, download: true);
    public Task<bool> IsReadyAsync(ProcurementDbContext db, CancellationToken ct) => CheckAsync(db, true, ct);
    public Task<bool> IsReadReadyAsync(ProcurementDbContext db, CancellationToken ct) => CheckAsync(db, false, ct);
    public Task<bool> IsDownloadReadyAsync(ProcurementDbContext db, CancellationToken ct)
        => CheckAsync(db, false, ct, download: true);

    private async Task<bool> ConfiguredAsync(bool write, CancellationToken ct, bool download = false)
    {
        if (!ProcurementPrincipalInvoiceSchemaCapture.Populated) return false;
        var execution = configuration.GetConnectionString("ProcurementApprovalExecution");
        var documents = configuration.GetConnectionString("CoreDocuments");
        if (string.IsNullOrWhiteSpace(execution)
            || !InvoiceDocumentStoreBinding.SamePhysicalDatabase(documents, execution)
            || services.GetService<CoreDocumentFeature>() is not { Enabled: true }
            || services.GetService<IDocumentSchemaReadiness>() is not { } documentSchema
            || !await documentSchema.IsReadyAsync(ct)) return false;
        try
        {
            return await transaction.ExecuteAsync(async (db, unusedCases, token) =>
                await CheckAsync(db, write, token, download), ct);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
    }

    private async Task<bool> CheckAsync(ProcurementDbContext db, bool write, CancellationToken ct, bool download = false)
    {
        if (!ProcurementPrincipalInvoiceSchemaCapture.Populated || !db.Database.IsNpgsql()
            || db.Database.CurrentTransaction is null
            || !await orders.IsReadyAsync(db, false, ct)
            || !await receiving.IsReadyAsync(db, false, ct)
            || !await principalStore.CanReadWorkflowAsync(db, ct)
            || !await ProcurementInvoiceReviewDocumentReadiness.ReadyAsync(
                db, requestScope, write, false, ct, download))
            return false;
        var physical = db.GetService<IDesignTimeModel>().Model;
        var expectedChecks = ProcurementPrincipalInvoiceSchemaCapture.Types.SelectMany(type =>
        {
            var entity = physical.FindEntityType(type);
            return entity is null ? Enumerable.Empty<string>() : entity.GetCheckConstraints()
                .Select(check => entity.GetSchema() + "." + entity.GetTableName() + "." + check.Name);
        }).ToHashSet(StringComparer.Ordinal);
        if (!expectedChecks.SetEquals(ProcurementPrincipalInvoiceSchemaCapture.Checks.Keys)) return false;
        string Table(Type type)
        {
            var entity = physical.FindEntityType(type) ?? throw new InvalidOperationException("PrincipalInvoiceEntityMissing");
            return entity.GetSchema() + "." + entity.GetTableName();
        }
        var owned = ProcurementPrincipalInvoiceSchemaCapture.Types.Select(Table).ToArray();
        Type[] dependencies = [typeof(SupplierInvoice), typeof(SupplierInvoiceLine), typeof(SupplierCreditNoteRef),
            typeof(InvoiceMatchResult), typeof(InvoiceMatchAllocation), typeof(GoodsReceipt),
            typeof(ServiceAcceptance), typeof(PolicyVersion), typeof(AuditEvent), typeof(OutboxEvent)];
        var reads = owned.Concat(dependencies.Select(Table)).Distinct(StringComparer.Ordinal).ToArray();
        var mutable = new HashSet<string>(StringComparer.Ordinal)
        {
            Table(typeof(PrincipalInvoiceState)), Table(typeof(PrincipalInvoiceExposureState)),
            Table(typeof(PrincipalInvoiceDiscrepancy))
        };
        var append = owned.Where(x => !mutable.Contains(x))
            .Concat([Table(typeof(AuditEvent)), Table(typeof(OutboxEvent))])
            .Distinct(StringComparer.Ordinal).ToArray();
        var clauses = reads.Select(x => "has_table_privilege(current_user," + Literal(x) + ",'SELECT')").ToList();
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))");
        clauses.Add("NOT has_schema_privilege(current_user,'procurement','CREATE')");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid=ANY(" + Columns(reads)
            + "::regclass[]) AND pg_has_role(current_user,c.relowner,'MEMBER'))");
        foreach (var table in owned)
            clauses.Add("NOT has_table_privilege(current_user," + Literal(table) + ",'DELETE,TRUNCATE')");
        foreach (var table in append)
            clauses.Add("NOT has_any_column_privilege(current_user," + Literal(table) + ",'UPDATE')");
        if (write)
        {
            // P31 opens its own Core case. Requiring the P25 purchase-request
            // writer would unnecessarily grant invoice reviewers PR mutations.
            foreach (var table in new[] { "cases", "stages", "assignments" })
            {
                var relation = "core_approval." + table;
                foreach (var grant in new[] { "SELECT", "INSERT", "UPDATE" })
                    clauses.Add("has_table_privilege(current_user," + Literal(relation) + "," + Literal(grant) + ")");
                clauses.Add("NOT has_table_privilege(current_user," + Literal(relation) + ",'DELETE,TRUNCATE')");
            }
            clauses.Add("has_table_privilege(current_user,'core_approval.decisions','SELECT')");
            clauses.Add("has_table_privilege(current_user,'core_approval.decisions','INSERT')");
            clauses.Add("NOT has_any_column_privilege(current_user,'core_approval.decisions','UPDATE')");
            clauses.Add("NOT has_table_privilege(current_user,'core_approval.decisions','DELETE,TRUNCATE')");
            foreach (var table in append)
                clauses.Add("has_table_privilege(current_user," + Literal(table) + ",'INSERT')");
            foreach (var table in mutable)
                clauses.Add("has_table_privilege(current_user," + Literal(table) + ",'UPDATE')");
            // The first mapping inserts one Unknown cursor; later comparisons
            // advance that same row under the owning PO lock.
            foreach (var table in mutable)
                clauses.Add("has_table_privilege(current_user," + Literal(table) + ",'INSERT')");
        }
        return await ProcurementPrincipalCommercialSchemaInspection.CheckAsync(db,
            ProcurementPrincipalInvoiceSchemaCapture.Types,
            "SELECT " + string.Join(" AND ", clauses),
            ProcurementPrincipalInvoiceSchemaCapture.OwnershipNames,
            ProcurementPrincipalInvoiceSchemaCapture.Physical, ct,
            ProcurementPrincipalInvoiceSchemaCapture.OwnershipManifestSql);
    }
}
