using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

/// <summary>Core-owned private auth/session grant CONTRACT, not a grant executor or auth store.
/// The real Core model supplies column names. Nothing here registers a runtime provider.</summary>
internal static class CoreLocalSessionRuntimeProfile
{
    internal sealed record Relation(string Schema,string Table,string[] Select,string[] Insert,string[] Update);
    internal sealed record Manifest(int SchemaVersion,string Owner,string Scope,Relation[] Relations,
        bool PhysicalSchemaVerified=false,bool RuntimeReady=false);

    internal static Manifest Build(HrmDbContext core)
    {
        var model=core.GetService<IDesignTimeModel>().Model;
        var rows=new Dictionary<Type,(string[]? Read,string[]? Insert,string[]? Update)>
        {
            // Existing LocalIdentity queries materialize these complete mapped entities.
            [typeof(LocalUser)]=(null,[],["FailedLoginCount","LockedUntil","LastLoginAt","PasswordHash","MustChangePassword","PasswordChangedAt"]),
            [typeof(LocalSession)]=(null,null,["LastSeenAt","RevokedAt"]),
            [typeof(TenantRoleAssignment)]=(null,[],[]),
            [typeof(AuditEntry)]=([],null,[]),
            [typeof(PrivilegedActionEvent)]=([],null,[]),
            [typeof(LegalEntity)]=(["Id","TenantId","IsArchived","Code","RegisteredName","Currency","IsDefault"],[],[]),
            [typeof(WorkLocation)]=(["Id","TenantId","IsArchived","LegalEntityId","Code","Name"],[],[]),
            [typeof(OrgUnit)]=(["Id","TenantId","IsArchived","LegalEntityId","Code","Name","Status","EffectiveFrom","EffectiveTo"],[],[]),
            [typeof(HrUserBranchAssignment)]=(["TenantId","UserId","LocationId","IsArchived"],[],[]),
            [typeof(Worker)]=(["Id","TenantId","IsArchived","Status","LocationId","OrgUnitId","ManagerId"],[],[]),
        };
        var result=new List<Relation>();
        foreach(var (type,operations) in rows)
        {
            var entity=model.FindEntityType(type)??throw new InvalidOperationException("CoreSessionOwningModelMissing");
            if(entity.GetSchema()!="hrm"||entity.GetTableName() is not {} table)
                throw new InvalidOperationException("CoreSessionOwningSchemaUnsupported");
            var store=StoreObjectIdentifier.Table(table,"hrm");
            string[] Columns(string[]? names)=> (names is null?entity.GetProperties():names.Select(name=>
                    entity.FindProperty(name)??throw new InvalidOperationException("CoreSessionOwningPropertyMissing")))
                .Select(p=>p.GetColumnName(store)??throw new InvalidOperationException("CoreSessionOwningColumnMissing"))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            result.Add(new("hrm",table,Columns(operations.Read),Columns(operations.Insert),Columns(operations.Update)));
        }
        // Existing CoreSchemaStartup pending-history check. This is not DDL authority.
        result.Add(new("hrm","__hrm_migrations",["MigrationId","ProductVersion"],[],[]));
        return new(1,"Core","PRIVATE_LOCAL_SESSION_AND_CURRENT_CORE_READS_ONLY",
            result.OrderBy(r=>r.Table,StringComparer.Ordinal).ToArray());
    }
}
