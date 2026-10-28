using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Own M5d physical admission, not business permission. Held expected
/// definitions require fresh native M5d rehearsal. Never opens an owner fallback.</summary>
public sealed class ProcurementPrincipalGoodsReturnStoreReadiness(
    ProcurementPrincipalGoodsReturnOptions options, IConfiguration configuration,
    IProcurementPrincipalReceivingReadiness receiving, IServiceProvider services)
    : IProcurementPrincipalGoodsReturnReadiness
{
    public async Task<bool> IsConfiguredAsync(CancellationToken ct)
    {
        if (!options.ExecutionDatabaseConfigured
            || !ProcurementPrincipalGoodsReturnSchemaCapture.Populated) return false;
        try
        {
            return await services.GetRequiredService<ProcurementApprovalTransaction>().ExecuteAsync(
                async (owner,_,token)=>await IsReadyAsync(owner,false,token),ct);
        }
        catch(Exception error) when(error is not OperationCanceledException) { return false; }
    }

    internal static bool OwnsSerializable(DbTransaction? transaction,DbConnection connection)
        => transaction is not null && transaction.IsolationLevel==IsolationLevel.Serializable
            && ReferenceEquals(transaction.Connection,connection);

    internal static Task<bool> SourceReadReadyAsync(IProcurementPrincipalReceivingReadiness source,
        ProcurementDbContext owner,CancellationToken ct) => source.IsReadyAsync(owner,false,ct);

    public async Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct)
    {
        if(!options.ExecutionDatabaseConfigured
            || !ProcurementPrincipalGoodsReturnSchemaCapture.Populated
            || owner is null || !owner.Database.IsNpgsql()) return false;
        try
        {
            if(write && !options.Enabled) return false;
            if(!ProcurementPrincipalOrderDraftSchemaReadiness.TryExecutionConnection(configuration,out var executionRole)) return false;
            if(!OwnsSerializable(owner.Database.CurrentTransaction?.GetDbTransaction(),
                    owner.Database.GetDbConnection())
                || !InvoiceDocumentStoreBinding.SamePhysicalDatabase(
                    configuration.GetConnectionString("ProcurementApprovalExecution"),
                    owner.Database.GetDbConnection().ConnectionString)
                || !await SourceReadReadyAsync(receiving,owner,ct)) return false;
            if(!await CheckPhysicalAsync(owner,write,executionRole,ct)) return false;
            // These include all generated PK/AK/business/FK indexes and the
            // unmodeled current-revision owner index. Unknown extras fail closed.
            var connection=owner.Database.GetDbConnection();
            await using var command=connection.CreateCommand();
            command.Transaction=owner.Database.CurrentTransaction!.GetDbTransaction();
            command.CommandTimeout=10;
            command.CommandText="SELECT array_agg(i.relname::text ORDER BY i.relname)="
                +Columns(ProcurementPrincipalGoodsReturnSchemaCapture.IndexNames.Order(StringComparer.Ordinal).ToArray())
                +" FROM pg_index x JOIN pg_class i ON i.oid=x.indexrelid WHERE x.indrelid=ANY("
                +Columns(ProcurementPrincipalGoodsReturnSchemaCapture.Tables)+"::regclass[])";
            if(await command.ExecuteScalarAsync(ct) is not true) return false;
            foreach(var pair in ProcurementPrincipalGoodsReturnSchemaCapture.IndexDefinitions)
            {
                command.CommandText="SELECT pg_get_indexdef(i.indexrelid) FROM pg_index i WHERE i.indexrelid="
                    +Literal("procurement.\""+pair.Key.Replace("\"","\"\"")+"\"")+"::regclass"
                    +" AND i.indisvalid AND i.indisready AND i.indislive AND i.indimmediate"
                    +" AND i.indexprs IS NULL AND i.indpred IS NULL";
                if(await command.ExecuteScalarAsync(ct) is not string actual || actual!=pair.Value) return false;
            }
            return true;
        }
        catch(Exception error) when(error is not OperationCanceledException) { return false; }
    }

    private static async Task<bool> CheckPhysicalAsync(ProcurementDbContext owner,bool write,string role,CancellationToken ct)
    {
        if(owner.Database.HasPendingModelChanges() || (await owner.Database.GetPendingMigrationsAsync(ct)).Any()
            || !(await owner.Database.GetAppliedMigrationsAsync(ct)).Contains(ProcurementPrincipalGoodsReturnSchemaCapture.MigrationId,StringComparer.Ordinal))return false;
        var model=owner.GetService<IDesignTimeModel>().Model;
        await using var command=owner.Database.GetDbConnection().CreateCommand();
        command.Transaction=owner.Database.CurrentTransaction!.GetDbTransaction();command.CommandTimeout=10;
        async Task<bool> True(string sql){command.CommandText=sql;return await command.ExecuteScalarAsync(ct)is true;}
        if(!await True(PrivilegeSql(write,role)))return false;
        foreach(var type in ProcurementPrincipalGoodsReturnSchemaCapture.Types)
        {
            var entity=model.FindEntityType(type);if(entity is null || entity.GetSchema()!="procurement")return false;
            var table=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());
            command.CommandText=SelectAllColumnsSql(entity,table);await using(var reader=await command.ExecuteReaderAsync(ct)){}
            if(!await True(ColumnsSql(entity,table)))return false;
            var extra=table.Name=="principal_goods_return_revisions"?1:0;
            var fks=table.Name=="principal_goods_return_drafts"?4:0;
            if(!await True("SELECT (SELECT count(*)FROM pg_attribute WHERE attrelid="+Literal("procurement."+table.Name)+"::regclass AND attnum>0 AND NOT attisdropped)="+entity.GetProperties().Count()
                +" AND NOT EXISTS(SELECT 1 FROM pg_attrdef WHERE adrelid="+Literal("procurement."+table.Name)+"::regclass)"
                +" AND (SELECT count(*)FROM pg_constraint WHERE conrelid="+Literal("procurement."+table.Name)+"::regclass AND contype IN('p','u','f','c'))="+(entity.GetKeys().Count()+entity.GetForeignKeys().Count()+entity.GetCheckConstraints().Count()+extra+fks)))return false;
            foreach(var key in entity.GetKeys())if(!await True(KeySql(table,key.Properties.Select(x=>x.GetColumnName(table)!).ToArray(),key.GetName()!,key.IsPrimaryKey())))return false;
            foreach(var check in entity.GetCheckConstraints())
            {
                command.CommandText=CheckSql(table,check.Name);
                if(!ProcurementPrincipalGoodsReturnSchemaCapture.Checks.TryGetValue("procurement."+table.Name+"."+check.Name,out var expected)
                    || await command.ExecuteScalarAsync(ct)is not string actual || actual!=expected)return false;
            }
        }
        foreach(var pair in ProcurementPrincipalGoodsReturnSchemaCapture.ForeignDefinitions)
        {
            var name=pair.Key.Split('.');command.CommandText="SELECT pg_get_constraintdef(oid,false)FROM pg_constraint WHERE conrelid="+Literal(name[0]+"."+name[1])+"::regclass AND conname="+Literal(name[2])+" AND contype='f' AND convalidated AND conislocal";
            if(await command.ExecuteScalarAsync(ct)is not string actual || actual!=pair.Value)return false;
        }
        foreach(var pair in ProcurementPrincipalGoodsReturnSchemaCapture.OverlayKeys)
        {
            var name=pair.Key.Split('.');command.CommandText="SELECT pg_get_constraintdef(oid,false)FROM pg_constraint WHERE conrelid="+Literal(name[0]+"."+name[1])+"::regclass AND conname="+Literal(name[2])+" AND contype='u' AND convalidated AND conislocal AND NOT condeferrable AND NOT condeferred";
            if(await command.ExecuteScalarAsync(ct)is not string actual || actual!=pair.Value)return false;
        }
        return await True(OwnershipManifestSql);
    }

    internal static readonly string[] MutableHeadColumns =
        ["revision","state","current_revision_id","version","updated_at","updated_by"];
    internal static string PrivilegeSql(bool write,string? expectedRole=null)
    {
        var tables=ProcurementPrincipalGoodsReturnSchemaCapture.Tables;
        var reads=tables.Concat(["procurement.audit_events","procurement.outbox_events"]).ToArray();
        var clauses=reads.Select(t=>"has_table_privilege(current_user,"+Literal(t)+",'SELECT')").ToList();
        if(expectedRole is not null)clauses.Add("session_user=current_user AND current_user="+Literal(expectedRole));
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles r WHERE (r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls) AND pg_has_role(current_user,r.oid,'SET'))");
        clauses.Add("NOT has_database_privilege(current_user,current_database(),'CREATE')");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_database d WHERE d.datname=current_database() AND (pg_has_role(current_user,d.datdba,'MEMBER') OR pg_has_role(current_user,d.datdba,'SET')))");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))");
        clauses.Add("NOT has_schema_privilege(current_user,'procurement','CREATE')");
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid=ANY("+Columns(reads)
            +"::regclass[]) AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))");
        foreach(var table in reads)
        {
            clauses.Add("NOT has_table_privilege(current_user,"+Literal(table)+",'DELETE,TRUNCATE,TRIGGER')");
            if(table!="procurement.principal_goods_return_drafts")
                clauses.Add("NOT has_any_column_privilege(current_user,"+Literal(table)+",'UPDATE')");
        }
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_attribute a WHERE a.attrelid='procurement.principal_goods_return_drafts'::regclass"
            +" AND a.attnum>0 AND NOT a.attisdropped AND NOT(a.attname::text=ANY("+Columns(MutableHeadColumns)
            +")) AND has_column_privilege(current_user,a.attrelid,a.attnum,'UPDATE'))");
        if(write)
        {
            foreach(var table in reads)
                clauses.Add("has_table_privilege(current_user,"+Literal(table)+",'INSERT')");
            foreach(var column in MutableHeadColumns)
                clauses.Add("has_column_privilege(current_user,'procurement.principal_goods_return_drafts',"
                    +Literal(column)+",'UPDATE')");
        }
        // P30 source, PO/PR counters and Core cases are inspected by the owning
        // read provider. This contract never requires their INSERT/UPDATE rights.
        return "SELECT "+string.Join(" AND ",clauses);
    }

    internal const string OwnershipManifestSql = """
        WITH expected(name,parent,childcols,parentcols,deferred) AS (VALUES
          ('fk_pgret_exact_source','procurement.principal_receiving_sources',
           ARRAY['tenant_id','legal_entity_id','source_state_id','branch_id','purchase_order_id','purchase_order_line_id','issued_version_id'],
           ARRAY['tenant_id','legal_entity_id','id','branch_id','purchase_order_id','purchase_order_line_id','issued_version_id'],false),
          ('fk_pgret_exact_fact','procurement.principal_receiving_facts',
           ARRAY['tenant_id','legal_entity_id','acceptance_fact_id','source_state_id','receipt_id','branch_id'],
           ARRAY['tenant_id','legal_entity_id','id','source_state_id','draft_id','branch_id'],false),
          ('fk_pgret_exact_receipt','procurement.principal_receiving_drafts',
           ARRAY['tenant_id','legal_entity_id','receipt_id','branch_id','purchase_order_id','issued_version_id'],
           ARRAY['tenant_id','legal_entity_id','id','branch_id','purchase_order_id','issued_version_id'],false),
          ('fk_pgret_current_revision','procurement.principal_goods_return_revisions',
           ARRAY['tenant_id','legal_entity_id','id','current_revision_id','revision','state'],
           ARRAY['tenant_id','legal_entity_id','draft_id','id','revision','state'],true))
        SELECT count(*)=4 AND coalesce(bool_and(
          c.oid IS NOT NULL AND c.contype='f' AND c.convalidated AND c.conislocal
          AND c.condeferrable=e.deferred AND c.condeferred=e.deferred
          AND c.confdeltype='a' AND c.confupdtype='a' AND c.confmatchtype='s'
          AND i.indisunique AND i.indisvalid AND i.indisready AND i.indislive AND i.indimmediate
          AND ARRAY(SELECT a.attname::text FROM unnest(c.conkey) WITH ORDINALITY k(n,p)
            JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.n ORDER BY k.p)=e.childcols
          AND ARRAY(SELECT a.attname::text FROM unnest(c.confkey) WITH ORDINALITY k(n,p)
            JOIN pg_attribute a ON a.attrelid=c.confrelid AND a.attnum=k.n ORDER BY k.p)=e.parentcols),false)
        FROM expected e LEFT JOIN pg_constraint c
          ON c.conrelid='procurement.principal_goods_return_drafts'::regclass AND c.conname=e.name
          AND c.confrelid=to_regclass(e.parent)
        LEFT JOIN pg_index i ON i.indexrelid=c.conindid;
        """;
}
