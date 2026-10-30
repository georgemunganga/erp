using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>B2-only physical proof on the existing serializable execution
/// transaction. The B1 and M6A readiness providers retain their own gates.</summary>
internal static class ProcurementPrincipalInvoiceIssuedParentReadiness
{
    // Genuine B2 native private Up captures, byte-exact pg_get_expr/constraint
    // definitions. No generic expression or CHECK normalization is accepted.
    internal const string MigrationId = "20261028191905_M6bInvoiceIssuedParentBindings";
    internal static readonly IReadOnlyDictionary<string,(string Legacy,string Principal,string Check)> Captured =
        new Dictionary<string,(string,string,string)>(StringComparer.Ordinal)
        {
            ["invoice_match_mapping_versions"] = ("\nCASE\n    WHEN (source_schema_version IS NULL) THEN issued_version_id\n    ELSE NULL::uuid\nEND", "\nCASE\n    WHEN (source_schema_version = 3) THEN issued_version_id\n    ELSE NULL::uuid\nEND", "CHECK (((mapping_revision > 0) AND ((mapping_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((invoice_original_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (identity_schema_version = ANY (ARRAY[1, 2])) AND ((source_schema_version IS NULL) OR ((source_schema_version = 3) AND (identity_schema_version = 2))) AND ((identity_schema_version = 2) OR (actor_worker_id IS NOT NULL)) AND ((actor_worker_id IS NULL) OR (actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND (actor_principal_id IS NOT NULL) AND ((identity_schema_version = 1) OR (((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text)))))"),
            ["invoice_match_exposure_states"] = ("\nCASE\n    WHEN (source_schema_version IS NULL) THEN issued_version_id\n    ELSE NULL::uuid\nEND", "\nCASE\n    WHEN (source_schema_version = 3) THEN issued_version_id\n    ELSE NULL::uuid\nEND", "CHECK (((sequence >= 0) AND ((source_schema_version IS NULL) OR (source_schema_version = 3)) AND ((coverage_state)::text = ANY ((ARRAY['Unknown'::character varying, 'Verified'::character varying, 'Stale'::character varying])::text[])) AND ((currency_code)::text ~ '^[A-Z]{3}$'::text) AND (((coverage_state)::text <> 'Verified'::text) OR ((owner_reference IS NOT NULL) AND (owner_watermark IS NOT NULL) AND (owner_evidence_sha256 IS NOT NULL) AND ((owner_evidence_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (owner_valid_until IS NOT NULL)))))"),
            ["invoice_match_results"] = ("\nCASE\n    WHEN (source_schema_version IS NULL) THEN issued_version_id\n    ELSE NULL::uuid\nEND", "\nCASE\n    WHEN (source_schema_version = 3) THEN issued_version_id\n    ELSE NULL::uuid\nEND", "CHECK ((((source_schema_version IS NULL) OR ((source_schema_version = 3) AND (execution_version = 2) AND (mapping_version_id IS NOT NULL) AND (purchase_order_id IS NOT NULL) AND (issued_version_id IS NOT NULL))) AND ((execution_version = 0) OR ((execution_version = ANY (ARRAY[1, 2])) AND (mapping_version_id IS NOT NULL) AND (purchase_order_id IS NOT NULL) AND (issued_version_id IS NOT NULL) AND (invoice_version > 0) AND (command_key IS NOT NULL) AND ((execution_version = 2) OR (actor_worker_id IS NOT NULL)) AND ((actor_worker_id IS NULL) OR (actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND (actor_principal_id IS NOT NULL) AND ((execution_version = 1) OR (((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text))) AND (invoice_original_sha256 IS NOT NULL) AND ((invoice_original_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (input_fingerprint IS NOT NULL) AND ((input_fingerprint)::text ~ '^[0-9a-f]{64}$'::text) AND (evidence_sha256 IS NOT NULL) AND ((evidence_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (policy_rules_sha256 IS NOT NULL) AND ((policy_rules_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (evidence_json IS NOT NULL) AND (source_watermarks_json IS NOT NULL) AND ((outcome)::text = ANY ((ARRAY['Compared'::character varying, 'Blocked'::character varying])::text[]))))))")
        };

    internal static async Task<bool> RequireIssuedParentsAsync(ProcurementDbContext db,CancellationToken ct)
    {
        if(db is null || !db.Database.IsNpgsql() || MigrationId.Length==0
            || Captured.Count!=3 || Captured.Values.Any(x=>x.Legacy.Length==0
                || x.Principal.Length==0) || Captured["invoice_match_exposure_states"].Check.Length==0
            || Captured["invoice_match_results"].Check.Length==0
            || !ProcurementPrincipalGoodsReturnStoreReadiness.OwnsSerializable(
                db.Database.CurrentTransaction?.GetDbTransaction(),db.Database.GetDbConnection()))return false;
        try
        {
            if(ProcurementCompiledModelParity.HasPendingModelChanges(db)
                || (await db.Database.GetPendingMigrationsAsync(ct)).Any()
                || !(await db.Database.GetAppliedMigrationsAsync(ct)).Contains(MigrationId,StringComparer.Ordinal))return false;
            var model=db.GetService<IDesignTimeModel>().Model;
            await using var command=db.Database.GetDbConnection().CreateCommand();
            command.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();command.CommandTimeout=10;
            async Task<bool> True(string sql){command.CommandText=sql;return await command.ExecuteScalarAsync(ct)is true;}
            foreach(var type in new[]{typeof(InvoiceMatchMappingVersion),
                typeof(InvoiceMatchExposureState),typeof(InvoiceMatchResult)})
            {
                var entity=model.FindEntityType(type);
                if(entity is null || entity.GetSchema()!="procurement"
                    || !Captured.TryGetValue(entity.GetTableName()!,out var captured))return false;
                var table=StoreObjectIdentifier.Table(entity.GetTableName()!,"procurement");
                var relation=Literal("procurement."+table.Name)+"::regclass";
                command.CommandText=SelectAllColumnsSql(entity,table);
                await using(var reader=await command.ExecuteReaderAsync(ct)){}
                if(!await True("SELECT c.relkind='r' AND c.relpersistence='p' AND NOT c.relrowsecurity"
                    +" AND NOT c.relforcerowsecurity FROM pg_class c WHERE c.oid="+relation))return false;
                if(!await True("SELECT (SELECT count(*) FROM pg_attribute WHERE attrelid="+relation
                    +" AND attnum>0 AND NOT attisdropped)="+entity.GetProperties().Count()
                    +" AND (SELECT count(*) FROM pg_constraint WHERE conrelid="+relation+")="
                    +(entity.GetKeys().Count()+entity.GetForeignKeys().Count()+entity.GetCheckConstraints().Count())
                    +" AND (SELECT count(*) FROM pg_index WHERE indrelid="+relation+")="
                    +(entity.GetKeys().Count()+entity.GetIndexes().Count())
                    +" AND NOT EXISTS(SELECT 1 FROM pg_trigger WHERE tgrelid="+relation
                    +" AND NOT tgisinternal)"))return false;
                foreach(var property in entity.GetProperties())
                {
                    var column=property.GetColumnName(table)!;
                    var expression=property.Name switch
                    {
                        "LegacyIssuedVersionRefId"=>captured.Legacy,
                        "PrincipalIssuedVersionRefId"=>captured.Principal,
                        _=>null
                    };
                    if(!await True(ColumnSql(table,column,property.GetColumnType()!,property.IsNullable,
                        expression)))return false;
                }
                foreach(var key in entity.GetKeys())
                    if(!await True(KeySql(table,key.Properties.Select(x=>x.GetColumnName(table)!).ToArray(),
                        key.GetName()!,key.IsPrimaryKey())))return false;
                foreach(var index in entity.GetIndexes().Where(x=>x.Properties.Any(p=>p.Name is
                    "LegacyIssuedVersionRefId" or "PrincipalIssuedVersionRefId")))
                    if(!await True(IndexSql(table,index.Properties.Select(x=>x.GetColumnName(table)!).ToArray(),
                        index.GetDatabaseName()!,index.IsUnique)))return false;
                var issuedForeigns=entity.GetForeignKeys().Where(x=>x.Properties.Any(p=>p.Name is
                    "LegacyIssuedVersionRefId" or "PrincipalIssuedVersionRefId")).ToArray();
                if(issuedForeigns.Length!=2)return false;
                // The old scoped relations remain part of the admitted shape;
                // a matching count cannot prove their exact parent or tuple.
                foreach(var foreign in entity.GetForeignKeys())
                {
                    var parent=StoreObjectIdentifier.Table(foreign.PrincipalEntityType.GetTableName()!,
                        foreign.PrincipalEntityType.GetSchema());
                    if(!await True(ForeignKeySql(table,parent,
                        foreign.Properties.Select(x=>x.GetColumnName(table)!).ToArray(),
                        foreign.PrincipalKey.Properties.Select(x=>x.GetColumnName(parent)!).ToArray(),
                        foreign.GetConstraintName())))return false;
                }
                foreach(var check in entity.GetCheckConstraints())
                {
                    if(check.Name is "ck_invoice_exposure_state" or "ck_invoice_match_execution")
                    {
                        command.CommandText=CheckSql(table,check.Name!);
                        if(await command.ExecuteScalarAsync(ct)is not string actual
                            || actual!=captured.Check)return false;
                    }
                }
            }
            var principal=model.FindEntityType(typeof(PrincipalOrderIssuedVersion));
            if(principal is null)return false;
            var principalTable=StoreObjectIdentifier.Table(principal.GetTableName()!,principal.GetSchema());
            var binding=principal.GetKeys().SingleOrDefault(x=>x.Properties.Select(p=>p.Name).SequenceEqual(
                new[]{"TenantId","LegalEntityId","Id","PurchaseOrderId"}));
            return binding is not null
                && await True(KeySql(principalTable,binding.Properties.Select(x=>x.GetColumnName(principalTable)!).ToArray(),
                    binding.GetName()!,false))
                && await True(IndexSql(principalTable,binding.Properties.Select(x=>x.GetColumnName(principalTable)!).ToArray(),
                    binding.GetName()!,true));
        }
        catch(Exception error)when(error is not OperationCanceledException){return false;}
    }

    internal static string ColumnSql(StoreObjectIdentifier table,string column,string type,bool nullable,
        string? generatedExpression) =>
        "SELECT EXISTS(SELECT 1 FROM pg_attribute a LEFT JOIN pg_attrdef d"
        +" ON d.adrelid=a.attrelid AND d.adnum=a.attnum WHERE a.attrelid="
        +Literal(table.Schema+"."+table.Name)+"::regclass AND a.attname="+Literal(column)
        +" AND NOT a.attisdropped AND a.attidentity='' AND a.attnotnull="+(nullable?"false":"true")
        +" AND format_type(a.atttypid,a.atttypmod)="+Literal(type)
        +" AND a.attgenerated="+Literal(generatedExpression is null?"":"s")
        +(generatedExpression is null?"":" AND pg_get_expr(d.adbin,d.adrelid)="+Literal(generatedExpression))+")";
}
