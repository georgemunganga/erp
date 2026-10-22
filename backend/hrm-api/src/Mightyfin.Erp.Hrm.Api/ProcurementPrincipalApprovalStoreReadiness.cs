using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Unregistered source candidate. Genuine successor generation and actual PostgreSQL
/// capture must populate the reviewed constants before this provider can ever return true.</summary>
public sealed class ProcurementPrincipalApprovalStoreReadiness(IApprovalScope coreScope,HrmDbContext coreIdentity):IProcurementPrincipalApprovalSchemaReadiness
{
    internal const string MigrationId="";
    internal static readonly IReadOnlyDictionary<string,string> CheckDefinitions=new Dictionary<string,string>();
    internal static readonly IReadOnlyDictionary<string,string> PartialPredicates=new Dictionary<string,string>();
    internal static bool ManifestCaptured=>MigrationId.Length>0 && CheckDefinitions.Count>0 && PartialPredicates.Count>0;
    private static readonly Type[] SourceTypes=[typeof(PurchaseRequest),typeof(PurchaseRequestLine),typeof(BudgetCheckSnapshot),
        typeof(PolicyVersion),typeof(PolicyEvaluation),typeof(PurchaseRequestApprovalSubmission),typeof(PurchaseRequestAssignmentAuthorityEvidence),
        typeof(ProcurementSpendingAuthorityGrant),typeof(ProcurementSpendingAuthorityCategory),typeof(SpendingAuthorityIdentityRecord),
        typeof(PurchaseRequestApprovalOutcome),typeof(PurchaseRequestFinanceReleaseRequest),typeof(PurchaseRequestFinanceReleaseConfirmation),
        typeof(ProcurementApprovalCommandReceipt),typeof(AuditEvent),typeof(OutboxEvent)];
    private static readonly string[] AppendTables=["budget_check_snapshots","spending_authority_identity_evidence","purchase_request_approval_submissions",
        "assignment_authority_evidence","purchase_request_approval_outcomes","purchase_request_finance_release_requests","purchase_request_finance_release_confirmations",
        "approval_command_receipts","audit_events","outbox_events"];
    // Only fields read by the existing current-account/grant/branch and optional
    // HR-manager queries. Catalog validation may inspect model column metadata;
    // the actual finite role never needs credential, pay or personal-field SELECT.
    internal static readonly IReadOnlyDictionary<Type,string[]> CoreIdentityReadProperties=new Dictionary<Type,string[]>
    {
        [typeof(LegalEntity)]=[nameof(LegalEntity.Id),nameof(LegalEntity.TenantId),nameof(LegalEntity.IsArchived)],
        [typeof(WorkLocation)]=[nameof(WorkLocation.Id),nameof(WorkLocation.TenantId),nameof(WorkLocation.LegalEntityId),nameof(WorkLocation.IsArchived)],
        [typeof(OrgUnit)]=[nameof(OrgUnit.Id),nameof(OrgUnit.TenantId),nameof(OrgUnit.LegalEntityId),nameof(OrgUnit.IsArchived),nameof(OrgUnit.Status)],
        [typeof(Worker)]=[nameof(Worker.Id),nameof(Worker.TenantId),nameof(Worker.IsArchived),nameof(Worker.Status),nameof(Worker.LocationId),nameof(Worker.OrgUnitId),nameof(Worker.ManagerId)],
        [typeof(LocalUser)]=[nameof(LocalUser.Id),nameof(LocalUser.TenantId),nameof(LocalUser.WorkerId),nameof(LocalUser.RolesCsv),nameof(LocalUser.LockedUntil),nameof(LocalUser.IsActive),nameof(LocalUser.IsArchived)],
        [typeof(HrUserBranchAssignment)]=[nameof(HrUserBranchAssignment.TenantId),nameof(HrUserBranchAssignment.UserId),nameof(HrUserBranchAssignment.LocationId),nameof(HrUserBranchAssignment.IsArchived)],
        [typeof(TenantRoleAssignment)]=[nameof(TenantRoleAssignment.TenantId),nameof(TenantRoleAssignment.Active),nameof(TenantRoleAssignment.IsArchived),nameof(TenantRoleAssignment.RoleKey),nameof(TenantRoleAssignment.PermissionsCsv)],
    };
    private static readonly Type[] CoreTypes=[typeof(ApprovalCase),typeof(ApprovalStage),typeof(ApprovalAssignment),typeof(ApprovalDecision)];
    public Task<bool> IsReadyAsync(ProcurementDbContext owner,CancellationToken ct)=>Check(owner,0,ct);
    public Task<bool> CanRecordAsync(ProcurementDbContext owner,CancellationToken ct)=>Check(owner,1,ct);
    public Task<bool> CanExecuteAsync(ProcurementDbContext owner,CancellationToken ct)=>Check(owner,2,ct);

    private async Task<bool> Check(ProcurementDbContext owner,int mode,CancellationToken ct)
    {
        if(!ManifestCaptured || !owner.Database.IsNpgsql())return false;
        var opened=false;
        try
        {
            if(owner.Database.HasPendingModelChanges() || (await owner.Database.GetPendingMigrationsAsync(ct)).Any()
                || !(await owner.Database.GetAppliedMigrationsAsync(ct)).Contains(MigrationId,StringComparer.Ordinal))return false;
            if(owner.Database.GetDbConnection().State!=ConnectionState.Open){await owner.Database.OpenConnectionAsync(ct);opened=true;}
            await using var command=owner.Database.GetDbConnection().CreateCommand();
            command.Transaction=owner.Database.CurrentTransaction?.GetDbTransaction();command.CommandTimeout=10;
            async Task<bool> True(string sql){command.CommandText=sql;return await command.ExecuteScalarAsync(ct) is true;}
            var model=owner.GetService<IDesignTimeModel>().Model;
            var entities=SourceTypes.Select(t=>model.FindEntityType(t)).ToArray();
            if(entities.Any(x=>x is null))return false;
            var expectedChecks=entities.SelectMany(x=>x!.GetCheckConstraints().Select(c=>x.GetSchema()+"."+x.GetTableName()+"."+c.Name)).ToHashSet(StringComparer.Ordinal);
            var expectedPredicates=entities.SelectMany(x=>x!.GetIndexes().Where(i=>i.IsUnique && i.GetFilter() is not null)
                .Select(i=>x.GetSchema()+"."+x.GetTableName()+"."+i.GetDatabaseName())).ToHashSet(StringComparer.Ordinal);
            if(!expectedChecks.SetEquals(CheckDefinitions.Keys) || !expectedPredicates.SetEquals(PartialPredicates.Keys))return false;
            foreach(var entity in entities)
            {
                if(entity!.GetSchema()!="procurement" || !await Inspect(entity,true))return false;
            }
            // Metadata comes from the existing Core owner model; reads/privilege
            // discovery still use ONLY the original Procurement connection/transaction.
            var identityModel=coreIdentity.GetService<IDesignTimeModel>().Model;
            foreach(var type in CoreIdentityReadProperties.Keys)
            {
                var entity=identityModel.FindEntityType(type);
                if(entity is null || entity.GetSchema()!="hrm")return false;
                var table=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());
                command.CommandText=CoreIdentitySelectSql(entity);await using(var reader=await command.ExecuteReaderAsync(ct)){}
                if(!await True(ColumnsSql(entity,table)))return false;
            }
            var relations=entities.Select(x=>x!.GetTableName()!).Append("__procurement_migrations").Distinct().ToArray();
            if(!await True("SELECT NOT has_schema_privilege(current_user,'procurement','CREATE')")
                || !await True("SELECT "+ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql)
                || !await True("SELECT "+ProcurementSpendingAuthorityIdentitySchemaReadiness.NoOwnershipSql)
                || !await True(NoOwnersSql(relations)) || !await True(CoreOwnershipSql))return false;
            foreach(var table in AppendTables)if(!await True(AppendSql(table,false)))return false;
            if(!await True("SELECT NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='procurement' AND c.relname::text=ANY("
                +Columns(relations)+") AND has_table_privilege(current_user,c.oid,'TRUNCATE'))"))return false;
            if(!await True(DefaultSql("purchase_requests","requester_identity_schema_version","0"))
                || !await True(DefaultSql("budget_check_snapshots","checker_identity_schema_version","0"))
                || !await True(DefaultSql("purchase_request_approval_submissions","finance_identity_schema_version","0"))
                || !await True(DefaultSql("assignment_authority_evidence","identity_schema_version","1")))return false;
            if(mode==1)
            {
                if(!await True(NoCoreWorkflowDmlSql))return false;
                foreach(var table in new[]{"purchase_requests","purchase_request_lines","spending_authority_grants"})
                    if(!await True(RequiredSql("procurement."+table,"SELECT","INSERT","UPDATE")))return false;
                if(!await True(RequiredSql("procurement.spending_authority_categories","SELECT","INSERT","DELETE")))return false;
                foreach(var table in new[]{"budget_check_snapshots","spending_authority_identity_evidence","audit_events","outbox_events"})
                    if(!await True(AppendSql(table,true)))return false;
            }
            if(mode==2)
            {
                // Use the real shared Core model solely as metadata. All catalog commands
                // remain on the original owner's connection/current transaction.
                await using var core=new ApprovalDbContext(new DbContextOptionsBuilder<ApprovalDbContext>()
                    .UseNpgsql(owner.Database.GetDbConnection()).Options,coreScope);
                var coreModel=core.GetService<IDesignTimeModel>().Model;
                foreach(var type in CoreTypes)
                {
                    var entity=coreModel.FindEntityType(type);
                    if(entity is null || entity.GetSchema()!="core_approval" || !await Inspect(entity,false))return false;
                }
                if(!await True("SELECT EXISTS(SELECT 1 FROM core_approval.\"__EFMigrationsHistory\" WHERE \"MigrationId\"='20261019183218_P01CoreApprovalFoundation')"))return false;
                foreach(var table in new[]{"cases","stages","assignments"})
                    if(!await True(RequiredSql("core_approval."+table,"SELECT","INSERT","UPDATE")
                        +" AND NOT has_table_privilege(current_user,"+Literal("core_approval."+table)+",'DELETE,TRUNCATE')"))return false;
                if(!await True(ProcurementInvoiceUnitReviewSchemaReadiness.CoreDecisionPrivilegesSql)
                    || !await True(NoOtherCoreWorkflowDmlSql))return false;
                foreach(var table in new[]{"purchase_requests","purchase_request_lines"})
                    if(!await True(RequiredSql("procurement."+table,"SELECT","UPDATE")))return false;
                foreach(var table in AppendTables.Where(x=>x is not ("budget_check_snapshots" or "spending_authority_identity_evidence")))
                    if(!await True(AppendSql(table,true)))return false;
            }
            return true;

            async Task<bool> Inspect(IEntityType entity,bool procurement)
            {
                var table=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());
                command.CommandText=SelectAllColumnsSql(entity,table);await using(var reader=await command.ExecuteReaderAsync(ct)){}
                if(!await True(ColumnsSql(entity,table)))return false;
                foreach(var check in entity.GetCheckConstraints())
                {
                    if(!procurement || !CheckDefinitions.TryGetValue(table.Schema+"."+table.Name+"."+check.Name,out var expected))return false;
                    command.CommandText=CheckSql(table,check.Name!);
                    if(await command.ExecuteScalarAsync(ct) is not string actual || actual!=expected)return false;
                }
                foreach(var key in entity.GetKeys())
                    if(!await True(KeySql(table,key.Properties.Select(p=>p.GetColumnName(table)!).ToArray(),key.GetName()!,key.IsPrimaryKey())))return false;
                foreach(var foreign in entity.GetForeignKeys())
                {
                    var parent=StoreObjectIdentifier.Table(foreign.PrincipalEntityType.GetTableName()!,foreign.PrincipalEntityType.GetSchema());
                    if(foreign.DeleteBehavior!=DeleteBehavior.Restrict
                        || !await True(ForeignKeySql(table,parent,foreign.Properties.Select(p=>p.GetColumnName(table)!).ToArray(),
                            foreign.PrincipalKey.Properties.Select(p=>p.GetColumnName(parent)!).ToArray(),foreign.GetConstraintName()))
                        || !await True(KeySql(parent,foreign.PrincipalKey.Properties.Select(p=>p.GetColumnName(parent)!).ToArray(),
                            foreign.PrincipalKey.GetName()!,foreign.PrincipalKey.IsPrimaryKey())))return false;
                }
                foreach(var index in entity.GetIndexes().Where(x=>x.IsUnique))
                {
                    string? predicate=null;
                    if(index.GetFilter() is not null && (!procurement || !PartialPredicates.TryGetValue(table.Schema+"."+table.Name+"."+index.GetDatabaseName(),out predicate)))return false;
                    if(!await True(UniqueIndexSql(table,index.Properties.Select(p=>p.GetColumnName(table)!).ToArray(),index.GetDatabaseName(),predicate)))return false;
                }
                return true;
            }
        }
        catch(Exception error) when(error is not OperationCanceledException){return false;}
        finally{if(opened)await owner.Database.CloseConnectionAsync();}
    }

    internal static string CoreIdentitySelectSql(IEntityType entity)
    {
        var table=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());
        if(!CoreIdentityReadProperties.TryGetValue(entity.ClrType,out var names) || table.Schema!="hrm")
            throw new InvalidOperationException("PrincipalApprovalCoreReadProjectionUnsupported");
        return "SELECT "+string.Join(',',names.Select(name=>Quote((entity.FindProperty(name)
            ?? throw new InvalidOperationException("PrincipalApprovalCoreReadPropertyMissing")).GetColumnName(table)!)))
            +" FROM "+Quote(table.Schema)+"."+Quote(table.Name)+" LIMIT 0";
    }

    internal static string RequiredSql(string relation,params string[] privileges)=>"SELECT "+string.Join(" AND ",privileges.Select(p=>
        "has_table_privilege(current_user,"+Literal(relation)+","+Literal(p)+")"));
    internal static string AppendSql(string table,bool insert)=>RequiredSql("procurement."+table,insert?new[]{"SELECT","INSERT"}:new[]{"SELECT"})
        +" AND NOT has_any_column_privilege(current_user,"+Literal("procurement."+table)+",'UPDATE')"
        +" AND NOT has_table_privilege(current_user,"+Literal("procurement."+table)+",'DELETE,TRUNCATE')";
    private static string DefaultSql(string table,string column,string value)=>"SELECT EXISTS(SELECT 1 FROM pg_attrdef d JOIN pg_attribute a ON a.attrelid=d.adrelid AND a.attnum=d.adnum WHERE d.adrelid="
        +Literal("procurement."+table)+"::regclass AND a.attname="+Literal(column)+" AND pg_get_expr(d.adbin,d.adrelid)="+Literal(value)+")";
    internal static string NoOwnersSql(string[] tables)=>"SELECT NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='procurement' AND c.relname::text=ANY("+Columns(tables)
        +") AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))";
    internal const string CoreOwnershipSql="SELECT NOT EXISTS(SELECT 1 FROM pg_namespace n WHERE n.nspname='core_approval' AND (has_schema_privilege(current_user,n.oid,'CREATE') OR pg_has_role(current_user,n.nspowner,'MEMBER') OR pg_has_role(current_user,n.nspowner,'SET'))) AND NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='core_approval' AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))";
    internal const string NoCoreWorkflowDmlSql="SELECT NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='core_approval' AND c.relkind IN ('r','p','v','f') AND (has_any_column_privilege(current_user,c.oid,'INSERT') OR has_any_column_privilege(current_user,c.oid,'UPDATE') OR has_table_privilege(current_user,c.oid,'DELETE,TRUNCATE')))";
    internal const string NoOtherCoreWorkflowDmlSql="SELECT NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='core_approval' AND c.relkind IN ('r','p','v','f') AND c.relname NOT IN ('cases','stages','assignments','decisions') AND (has_any_column_privilege(current_user,c.oid,'INSERT') OR has_any_column_privilege(current_user,c.oid,'UPDATE') OR has_table_privilege(current_user,c.oid,'DELETE,TRUNCATE')))";

}
