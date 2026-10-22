using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public sealed class ProcurementInvoiceMatchingExecutionOptions
{
    public bool Enabled { get; set; }
    public bool ExecutionDatabaseConfigured { get; set; }
}
public interface IProcurementInvoiceMatchingSchemaReadiness { Task<bool> IsReadyAsync(CancellationToken ct); }
public sealed class ProcurementInvoiceMatchingSchemaReadiness(IConfiguration configuration,IProcurementScope scope,
    IProcurementReceivingSchemaReadiness receiving) : IProcurementInvoiceMatchingSchemaReadiness
{
    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        var connection=configuration.GetConnectionString("ProcurementApprovalExecution");
        if(string.IsNullOrWhiteSpace(connection) || !await receiving.IsReadyAsync(ct)) return false;
        try
        {
            await using var db=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection,
                p=>p.MigrationsHistoryTable("__procurement_migrations","procurement")).Options,scope);
            if(db.Database.HasPendingModelChanges() || (await db.Database.GetPendingMigrationsAsync(ct)).Any()) return false;
            _=await db.InvoiceMatchMappingVersions.Select(x=>new { x.Id,x.MappingSha256 }).Take(1).ToArrayAsync(ct);
            _=await db.InvoiceMatchAllocations.Select(x=>new { x.Id,x.ReceivingSourceStateId }).Take(1).ToArrayAsync(ct);
            _=await db.InvoiceMatchExposureStates.Select(x=>new { x.Id,x.Sequence }).Take(1).ToArrayAsync(ct);
            _=await db.SupplierInvoices.Select(x=>new { x.Id,x.CurrentMappingVersionId,x.LastMatchResultId,x.ActiveMatchClaimResultId }).Take(1).ToArrayAsync(ct);
            await db.Database.OpenConnectionAsync(ct); await using var command=db.Database.GetDbConnection().CreateCommand();
            command.CommandText="""
                SELECT bool_and(has_table_privilege(current_user, relation, privilege)) FROM (VALUES
                ('procurement.invoice_match_mapping_versions','SELECT,INSERT'),
                ('procurement.invoice_match_allocations','SELECT,INSERT'),
                ('procurement.invoice_match_exposure_states','SELECT,INSERT,UPDATE'),
                ('procurement.invoice_match_results','SELECT,INSERT'),
                ('procurement.invoice_exceptions','SELECT,INSERT,UPDATE'),
                ('procurement.supplier_invoices','SELECT,UPDATE'),
                ('procurement.supplier_invoice_lines','SELECT'),
                ('procurement.commercial_command_receipts','SELECT,INSERT')) AS required(relation,privileges)
                CROSS JOIN LATERAL string_to_table(privileges,',') AS grants(privilege)
                """;
            if (await command.ExecuteScalarAsync(ct) is not true) return false;
            // Recheck the exact migration-owned receiving FKs on THIS matching store.
            // A delegated readiness result (or EF/column compatibility alone) cannot
            // prove the ownership tuples backing accepted evidence are still enforced.
            command.CommandText=ReceivingOwnershipConstraintSql.ManifestQuery;
            if(await command.ExecuteScalarAsync(ct) is not true) return false;
            command.CommandText=InvoiceMatchingOwnershipConstraintSql.ManifestQuery;
            return await command.ExecuteScalarAsync(ct) is true;
        }
        catch(Exception error) when(error is not OperationCanceledException) { return false; }
    }
}
