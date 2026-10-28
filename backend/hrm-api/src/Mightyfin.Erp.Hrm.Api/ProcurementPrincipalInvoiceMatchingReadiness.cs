using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Physical admission for the principal invoice path on the actual shared
/// serializable execution transaction. This does not authorize an actor or qualify
/// historical capacity. The Host must separately require the M6A baseline owner
/// readiness using read mode for selectors and write mode for commands.</summary>
internal static class ProcurementPrincipalInvoiceMatchingReadiness
{
    internal const string MigrationId = "20261028184527_M6bPrincipalInvoiceReceivingBindings";
    internal const string ExecutionRole = "erp_proc_execute_finite_v1";

    internal static readonly IReadOnlyDictionary<string,string[]> ParentKeys =
        new Dictionary<string,string[]>(StringComparer.Ordinal)
        {
            ["AK_principal_receiving_sources_tenant_id_legal_entity_id_id_pu~"] =
                ["tenant_id","legal_entity_id","id","purchase_order_id","issued_version_id","purchase_order_line_id"],
            ["AK_principal_receiving_facts_tenant_id_legal_entity_id_id_sour~"] =
                ["tenant_id","legal_entity_id","id","source_state_id"]
        };
    internal static readonly IReadOnlyDictionary<string,(string Parent,string[] From,string[] To)> ForeignKeys =
        new Dictionary<string,(string,string[],string[])>(StringComparer.Ordinal)
        {
            ["fk_invoice_allocation_principal_source_line"] =
                ("principal_receiving_sources",
                    ["tenant_id","legal_entity_id","principal_receiving_source_state_id",
                        "purchase_order_id","issued_version_id","purchase_order_line_id"],
                    ParentKeys["AK_principal_receiving_sources_tenant_id_legal_entity_id_id_pu~"]),
            ["fk_invoice_allocation_principal_fact_source"] =
                ("principal_receiving_facts",
                    ["tenant_id","legal_entity_id","principal_receiving_fact_id",
                        "principal_receiving_source_state_id"],
                    ParentKeys["AK_principal_receiving_facts_tenant_id_legal_entity_id_id_sour~"])
        };
    internal static readonly IReadOnlyDictionary<string,string[]> AllocationIndexes =
        new Dictionary<string,string[]>(StringComparer.Ordinal)
        {
            ["IX_invoice_match_allocations_tenant_id_legal_entity_id_princi~1"] =
                ForeignKeys["fk_invoice_allocation_principal_source_line"].From,
            ["IX_invoice_match_allocations_tenant_id_legal_entity_id_princip~"] =
                ForeignKeys["fk_invoice_allocation_principal_fact_source"].From
        };

    // A native pg_get_constraintdef(oid,false) capture from the reviewed M6B Up
    // must fill this single literal before the principal path can be enabled.
    // Guessing PostgreSQL deparse text or accepting a weakened CHECK is unsafe.
    internal const string CapturedAllocationCheck = "";

    internal static async Task<bool> IsReadyAsync(ProcurementDbContext db,
        ApprovalDbContext cases,bool write,CancellationToken ct)
    {
        if(db is null || cases is null || !db.Database.IsNpgsql() || !cases.Database.IsNpgsql()
            || CapturedAllocationCheck.Length==0
            || !ProcurementInvoiceOwnerBaselineSchemaCapture.Populated
            || !ProcurementPrincipalGoodsReturnStoreReadiness.OwnsSerializable(
                db.Database.CurrentTransaction?.GetDbTransaction(),db.Database.GetDbConnection())
            || !ReferenceEquals(db.Database.GetDbConnection(),cases.Database.GetDbConnection())
            || !ReferenceEquals(db.Database.CurrentTransaction!.GetDbTransaction(),
                cases.Database.CurrentTransaction?.GetDbTransaction()))
            return false;
        try
        {
            if(db.Database.HasPendingModelChanges() || (await db.Database.GetPendingMigrationsAsync(ct)).Any()
                || !(await db.Database.GetAppliedMigrationsAsync(ct)).Contains(MigrationId,StringComparer.Ordinal)
                || !await ProcurementPrincipalReceivingPhysicalSchema.InspectAsync(
                    db,false,ExecutionRole,ct))
                return false;
            var model=db.GetService<IDesignTimeModel>().Model;
            var allocation=model.FindEntityType(typeof(InvoiceMatchAllocation));
            if(allocation is null || allocation.GetSchema()!="procurement"
                || allocation.GetTableName()!="invoice_match_allocations"
                || allocation.GetProperties().Count(x=>x.Name is
                    nameof(InvoiceMatchAllocation.PrincipalReceivingSourceStateId)
                    or nameof(InvoiceMatchAllocation.PrincipalReceivingFactId))!=2)
                return false;
            var table=StoreObjectIdentifier.Table("invoice_match_allocations","procurement");
            await using var command=db.Database.GetDbConnection().CreateCommand();
            command.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
            command.CommandTimeout=10;
            async Task<bool> True(string sql){command.CommandText=sql;return await command.ExecuteScalarAsync(ct)is true;}
            command.CommandText=SelectAllColumnsSql(allocation,table);
            await using(var reader=await command.ExecuteReaderAsync(ct)){}
            if(!await True(ColumnsSql(allocation,table))
                || !await True(PrivilegeSql(write))
                || !await True("SELECT (SELECT count(*) FROM pg_attribute WHERE attrelid='procurement.invoice_match_allocations'::regclass AND attnum>0 AND NOT attisdropped)="
                    +allocation.GetProperties().Count()
                    +" AND NOT EXISTS(SELECT 1 FROM pg_attrdef WHERE adrelid='procurement.invoice_match_allocations'::regclass)"))
                return false;
            foreach(var (name,columns) in ParentKeys)
            {
                var parent=StoreObjectIdentifier.Table(name.Contains("sources",StringComparison.Ordinal)
                    ?"principal_receiving_sources":"principal_receiving_facts","procurement");
                if(!await True(KeySql(parent,columns,name,false))
                    || !await True(IndexSql(parent,columns,name,true)))
                    return false;
            }
            foreach(var (name,entry) in ForeignKeys)
                if(!await True(ForeignKeySql(table,
                    StoreObjectIdentifier.Table(entry.Parent,"procurement"),entry.From,entry.To,name)))
                    return false;
            foreach(var (name,columns) in AllocationIndexes)
                if(!await True(IndexSql(table,columns,name,false)))return false;
            command.CommandText=CheckSql(table,"ck_invoice_match_allocation");
            return await command.ExecuteScalarAsync(ct) is string actual
                && actual==CapturedAllocationCheck;
        }
        catch(Exception error) when(error is not OperationCanceledException){return false;}
    }

    internal static string PrivilegeSql(bool write)
    {
        var required=new List<string>
        {
            ProcurementInvoiceOwnerBaselineReadiness.PrivilegeSql(write,ExecutionRole)[7..],
            "has_table_privilege(current_user,'procurement.commercial_command_receipts','SELECT')"
        };
        foreach(var table in new[]{"invoice_match_allocations","invoice_match_mapping_versions",
            "invoice_match_results","commercial_command_receipts"})
        {
            required.Add("NOT has_table_privilege(current_user,"+Literal("procurement."+table)+",'DELETE,TRUNCATE,TRIGGER')");
            required.Add("NOT has_any_column_privilege(current_user,"+Literal("procurement."+table)+",'UPDATE')");
        }
        if(write)
        {
            foreach(var table in new[]{"invoice_match_mapping_versions","invoice_match_allocations",
                "invoice_match_results","invoice_exceptions","commercial_command_receipts"})
                required.Add("has_table_privilege(current_user,"+Literal("procurement."+table)+",'INSERT')");
            foreach(var table in new[]{"invoice_match_exposure_states","supplier_invoices",
                "invoice_exceptions"})
                required.Add("has_table_privilege(current_user,"+Literal("procurement."+table)+",'UPDATE')");
        }
        return "SELECT "+string.Join(" AND ",required);
    }
}
