using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Read-only physical admission on the actual shared execution transaction.
/// This proves storage, not actor permission, historical completeness, or claim eligibility.</summary>
public sealed class ProcurementInvoiceOwnerBaselineReadiness(IConfiguration configuration)
    : IProcurementInvoiceOwnerBaselineReadiness
{
    public Task<bool> CanReadAsync(ProcurementDbContext db,CancellationToken ct)
        => CheckAsync(db,false,ct);
    public Task<bool> CanRecordAsync(ProcurementDbContext db,CancellationToken ct)
        => CheckAsync(db,true,ct);

    private async Task<bool> CheckAsync(ProcurementDbContext db,bool write,CancellationToken ct)
    {
        if(db is null || !db.Database.IsNpgsql()
            || !ProcurementInvoiceOwnerBaselineSchemaCapture.Populated
            || write && !configuration.GetValue<bool>("Procurement:InvoiceOwnerBaseline:Enabled"))return false;
        try
        {
            if(!ProcurementPrincipalOrderDraftSchemaReadiness.TryExecutionConnection(configuration,out var role)
                || !InvoiceDocumentStoreBinding.SamePhysicalDatabase(
                    configuration.GetConnectionString("ProcurementApprovalExecution"),
                    db.Database.GetDbConnection().ConnectionString)
                || !ProcurementPrincipalGoodsReturnStoreReadiness.OwnsSerializable(
                    db.Database.CurrentTransaction?.GetDbTransaction(),db.Database.GetDbConnection())
                || db.Database.HasPendingModelChanges()
                || (await db.Database.GetPendingMigrationsAsync(ct)).Any()
                || !(await db.Database.GetAppliedMigrationsAsync(ct)).Contains(
                    ProcurementInvoiceOwnerBaselineSchemaCapture.MigrationId,StringComparer.Ordinal))return false;
            await using var command=db.Database.GetDbConnection().CreateCommand();
            command.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();command.CommandTimeout=10;
            async Task<bool> True(string sql){command.CommandText=sql;return await command.ExecuteScalarAsync(ct)is true;}
            if(!await True(PrivilegeSql(write,role)))return false;
            var completeInventoryTables=InvoiceProcurementOwnerTables.Names.Select(x=>"procurement."+x)
                .Concat(ProcurementInvoiceOwnerBaselineSchemaCapture.Tables).Distinct(StringComparer.Ordinal).ToArray();
            if(!await True("SELECT count(*)="+completeInventoryTables.Length
                +" AND coalesce(bool_and(c.relkind='r' AND c.relpersistence='p' AND NOT c.relrowsecurity AND NOT c.relforcerowsecurity),false)"
                +" FROM pg_class c WHERE c.oid=ANY("+Columns(completeInventoryTables)+"::regclass[])"))return false;
            var model=db.GetService<IDesignTimeModel>().Model;
            foreach(var type in new[]{typeof(InvoiceProcurementOwnerBaseline),
                typeof(InvoiceProcurementOwnerBaselineRow),typeof(InvoiceProcurementOwnerCommandReceipt)})
            {
                var entity=model.FindEntityType(type);
                if(entity is null || entity.GetSchema()!="procurement")return false;
                var table=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());
                command.CommandText=SelectAllColumnsSql(entity,table);
                await using(var reader=await command.ExecuteReaderAsync(ct)){}
                if(!await True(ColumnsSql(entity,table)))return false;
                var relation=Literal("procurement."+table.Name)+"::regclass";
                if(!await True("SELECT (SELECT count(*) FROM pg_attribute WHERE attrelid="+relation
                    +" AND attnum>0 AND NOT attisdropped)="+entity.GetProperties().Count()
                    +" AND NOT EXISTS(SELECT 1 FROM pg_attrdef WHERE adrelid="+relation+")"
                    +" AND (SELECT count(*) FROM pg_constraint WHERE conrelid="+relation+")="
                    +(entity.GetKeys().Count()+entity.GetForeignKeys().Count()+entity.GetCheckConstraints().Count())
                    +" AND NOT EXISTS(SELECT 1 FROM pg_trigger WHERE tgrelid="+relation+" AND NOT tgisinternal)"))return false;
                foreach(var key in entity.GetKeys())
                    if(!await True(KeySql(table,key.Properties.Select(x=>x.GetColumnName(table)!).ToArray(),
                        key.GetName()!,key.IsPrimaryKey())))return false;
                foreach(var foreign in entity.GetForeignKeys())
                {
                    var parent=StoreObjectIdentifier.Table(foreign.PrincipalEntityType.GetTableName()!,foreign.PrincipalEntityType.GetSchema());
                    if(!await True(ForeignKeySql(table,parent,foreign.Properties.Select(x=>x.GetColumnName(table)!).ToArray(),
                        foreign.PrincipalKey.Properties.Select(x=>x.GetColumnName(parent)!).ToArray(),foreign.GetConstraintName())))return false;
                }
                foreach(var check in entity.GetCheckConstraints())
                {
                    command.CommandText=CheckSql(table,check.Name!);
                    if(await command.ExecuteScalarAsync(ct)is not string actual
                        || !ProcurementInvoiceOwnerBaselineSchemaCapture.CheckMatches(
                            "procurement."+table.Name+"."+check.Name,actual))return false;
                }
            }
            var tables=ProcurementInvoiceOwnerBaselineSchemaCapture.Tables;
            command.CommandText="SELECT array_agg(i.relname::text ORDER BY i.relname)="
                +Columns(ProcurementInvoiceOwnerBaselineSchemaCapture.IndexDefinitions.Keys.Order(StringComparer.Ordinal).ToArray())
                +" FROM pg_index x JOIN pg_class i ON i.oid=x.indexrelid WHERE x.indrelid=ANY("
                +Columns(tables)+"::regclass[])";
            if(await command.ExecuteScalarAsync(ct)is not true)return false;
            foreach(var pair in ProcurementInvoiceOwnerBaselineSchemaCapture.IndexDefinitions)
            {
                command.CommandText="SELECT pg_get_indexdef(i.indexrelid) FROM pg_index i WHERE i.indexrelid="
                    +Literal("procurement.\""+pair.Key.Replace("\"","\"\"")+"\"")+"::regclass"
                    +" AND i.indisvalid AND i.indisready AND i.indislive AND i.indimmediate"
                    +" AND i.indexprs IS NULL AND i.indpred IS NULL";
                if(await command.ExecuteScalarAsync(ct)is not string actual || actual!=pair.Value)return false;
            }
            foreach(var (table,name) in new[]{("operational_policy_identity_evidence","ck_operational_policy_identity"),
                ("invoice_match_mapping_versions","ck_invoice_mapping_evidence")})
            {
                command.CommandText=CheckSql(StoreObjectIdentifier.Table(table,"procurement"),name);
                if(await command.ExecuteScalarAsync(ct)is not string actual
                    || !ProcurementInvoiceOwnerBaselineSchemaCapture.CheckMatches("procurement."+table+"."+name,actual))return false;
            }
            return true;
        }
        catch(Exception error)when(error is not OperationCanceledException){return false;}
    }

    internal static string PrivilegeSql(bool write,string role)
    {
        var append=ProcurementInvoiceOwnerBaselineSchemaCapture.Tables.Concat(
            ["procurement.audit_events","procurement.outbox_events","core_approval.decisions"]).ToArray();
        var reads=InvoiceProcurementOwnerTables.Names.Select(x=>"procurement."+x).Concat(append)
            .Concat(["procurement.policy_versions","procurement.operational_policy_identity_evidence",
                "procurement.__procurement_migrations","core_approval.cases","core_approval.stages",
                "core_approval.assignments"]).Distinct(StringComparer.Ordinal).ToArray();
        var clauses=new List<string>{"session_user=current_user AND current_user="+Literal(role),
            ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql,
            ProcurementSpendingAuthorityIdentitySchemaReadiness.NoOwnershipSql,
            "NOT has_database_privilege(current_user,current_database(),'CREATE')",
            "NOT has_schema_privilege(current_user,'procurement','CREATE')",
            "NOT has_schema_privilege(current_user,'core_approval','CREATE')",
            "NOT EXISTS(SELECT 1 FROM pg_roles r WHERE (r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls) AND pg_has_role(current_user,r.oid,'SET'))",
            "NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid=ANY("+Columns(reads)+"::regclass[]) AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))"};
        foreach(var table in reads)clauses.Add("has_table_privilege(current_user,"+Literal(table)+",'SELECT')");
        foreach(var table in append)
        {
            clauses.Add("NOT has_any_column_privilege(current_user,"+Literal(table)+",'UPDATE')");
            clauses.Add("NOT has_table_privilege(current_user,"+Literal(table)+",'DELETE,TRUNCATE,TRIGGER')");
            if(write)clauses.Add("has_table_privilege(current_user,"+Literal(table)+",'INSERT')");
        }
        foreach(var table in new[]{"core_approval.cases","core_approval.stages","core_approval.assignments"})
        {
            clauses.Add("NOT has_table_privilege(current_user,"+Literal(table)+",'DELETE,TRUNCATE')");
            if(write)
            {
                clauses.Add("has_table_privilege(current_user,"+Literal(table)+",'INSERT')");
                clauses.Add("has_table_privilege(current_user,"+Literal(table)+",'UPDATE')");
            }
        }
        foreach(var action in new[]{"INSERT","UPDATE","DELETE","TRUNCATE"})
            clauses.Add("NOT has_table_privilege(current_user,'procurement.finance_payment_status_refs',"+Literal(action)+")");
        foreach(var action in new[]{"INSERT","UPDATE"})
            clauses.Add("NOT has_any_column_privilege(current_user,'procurement.finance_payment_status_refs',"+Literal(action)+")");
        return "SELECT "+string.Join(" AND ",clauses);
    }
}
