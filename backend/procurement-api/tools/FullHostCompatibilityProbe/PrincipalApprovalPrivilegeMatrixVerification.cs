using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Procurement.Domain;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    private sealed record PrivilegeState(bool Read,bool Record,bool Execute);
    private sealed record PrivilegeRelation(string Schema,string Table,string? Column=null)
    { internal string Sql=>PrincipalApprovalRuntimePrivileges.Q(Schema)+"."+PrincipalApprovalRuntimePrivileges.Q(Table); }
    private sealed record PrivilegeFault(string Name,bool Execution,PrivilegeRelation Relation,string Permission,bool Remove,bool ReadAfter);

    private static async Task VerifyPrivilegeMatrixAsync(PrincipalApprovalFixture fixture,
        PrincipalApprovalRuntimePrivileges.Connections roles,NpgsqlConnection owner,Dictionary<string,object?> result,Action<string> progress)
    {
        progress("PrivilegeAclBaselinePreparation");
        Need(owner.Database.StartsWith("proc_p25_http_",StringComparison.Ordinal)
            &&new NpgsqlConnectionStringBuilder(owner.ConnectionString).Host=="/authority-identity-private-socket",
            "Privilege matrix is confined to the freshly bootstrapped private fixture database.");
        await using(var identity=new NpgsqlCommand("SELECT current_user='postgres' AND session_user='postgres' AND system_identifier::text='7698678713374847009' AND current_setting('server_version_num')::integer BETWEEN 170000 AND 179999 AND current_setting('listen_addresses')='' FROM pg_control_system()",owner))
            Need(await identity.ExecuteScalarAsync() is true,"Exact private bootstrap owner/cluster required before fixture ACL preparation.");
        Need(System.Text.RegularExpressions.Regex.IsMatch(roles.RecordRole,"^p25_record_[0-9a-f]{16}$")
            &&System.Text.RegularExpressions.Regex.IsMatch(roles.ExecutionRole,"^p25_execute_[0-9a-f]{16}$")
            &&roles.RecordRole[11..]==roles.ExecutionRole[12..],"Only newly provisioned paired private finite roles are supported.");
        await using var proc=fixture.Procurement();await using var identityCore=fixture.Core();await using var workflow=fixture.Approvals();
        var procModel=proc.GetService<IDesignTimeModel>().Model;var identityModel=identityCore.GetService<IDesignTimeModel>().Model;
        var coreModel=workflow.GetService<IDesignTimeModel>().Model;
        static PrivilegeRelation Rel(IModel model,Type type,string? property=null)
        {
            var entity=model.FindEntityType(type)??throw new PrivatePrincipalProbeFailure("Privilege owning model type missing");
            var table=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());
            string? column=null;
            if(property is not null)column=(entity.FindProperty(property)
                ??throw new PrivatePrincipalProbeFailure("Privilege owning column missing")).GetColumnName(table)
                ??throw new PrivatePrincipalProbeFailure("Privilege mapped column missing");
            return new(table.Schema!,table.Name,column);
        }
        var request=Rel(procModel,typeof(PurchaseRequest));var grant=Rel(procModel,typeof(ProcurementSpendingAuthorityGrant));
        var budget=Rel(procModel,typeof(BudgetCheckSnapshot));var budgetHash=Rel(procModel,typeof(BudgetCheckSnapshot),nameof(BudgetCheckSnapshot.CheckerIdentitySha256));
        var assignmentHash=Rel(procModel,typeof(PurchaseRequestAssignmentAuthorityEvidence),nameof(PurchaseRequestAssignmentAuthorityEvidence.EvidenceSha256));
        var audit=Rel(procModel,typeof(AuditEvent));var outbox=Rel(procModel,typeof(OutboxEvent));
        var cases=Rel(coreModel,typeof(ApprovalCase));var assignments=Rel(coreModel,typeof(ApprovalAssignment));
        var decisions=Rel(coreModel,typeof(ApprovalDecision));var decisionAction=Rel(coreModel,typeof(ApprovalDecision),nameof(ApprovalDecision.Action));
        var delegation=Rel(coreModel,typeof(Mightyfin.Erp.Core.Approvals.Domain.ApprovalDelegation));
        var rolePermission=Rel(identityModel,typeof(TenantRoleAssignment),nameof(TenantRoleAssignment.PermissionsCsv));
        var userWorker=Rel(identityModel,typeof(LocalUser),nameof(LocalUser.WorkerId));
        var dataBefore=await PrincipalFootprintAsync(owner,rowsOnly:true);var globalBefore=await PrivilegeGlobalBoundaryAsync(owner);
        var prep=new List<object>();
        // DECLARED NEW-FIXTURE SETUP BEFORE the matrix baseline. Never equate a
        // NULL ACL with {} after a temporary GRANT/REVOKE. Owner already owns these
        // objects; explicit owner SELECT introduces no finite-role capability.
        foreach(var column in new[]{budgetHash,assignmentHash,decisionAction,rolePermission})
        {
            var before=await PrivilegeAclAsync(owner,column);
            await PrivilegeChangeAsync(owner,column,"SELECT","postgres",remove:false);
            await using var exists=new NpgsqlCommand("SELECT a.attacl IS NOT NULL FROM pg_attribute a WHERE a.attrelid=to_regclass(@relation) AND a.attname=@column AND NOT a.attisdropped",owner);
            exists.Parameters.AddWithValue("relation",column.Sql);exists.Parameters.AddWithValue("column",column.Column!);
            Need(await exists.ExecuteScalarAsync() is true,"Owner column ACL baseline was not materialized; do not normalize or proceed.");
            prep.Add(new {relation=column.Sql,column=column.Column,beforeAcl=before,afterAcl=await PrivilegeAclAsync(owner,column)});
        }
        var delegationBefore=await PrivilegeAclAsync(owner,delegation);
        await PrivilegeChangeAsync(owner,delegation,"SELECT","postgres",remove:false);
        await using(var exists=new NpgsqlCommand("SELECT relacl IS NOT NULL FROM pg_class WHERE oid=to_regclass(@relation)",owner))
        {exists.Parameters.AddWithValue("relation",delegation.Sql);Need(await exists.ExecuteScalarAsync() is true,"Owner table ACL baseline was not materialized; private fixture requires review.");}
        prep.Add(new {relation=delegation.Sql,beforeAcl=delegationBefore,afterAcl=await PrivilegeAclAsync(owner,delegation)});
        // A revoked column-only SELECT entry must be LAST before binding the
        // baseline, so regrant restores exact raw order. No extra rights are added.
        await PrivilegeChangeAsync(owner,rolePermission,"SELECT",roles.RecordRole,remove:true);
        await PrivilegeChangeAsync(owner,rolePermission,"SELECT",roles.RecordRole,remove:false);
        prep.Add(new {relation=rolePermission.Sql,column=rolePermission.Column,recordEntryPreparedLast=true,acl=await PrivilegeAclAsync(owner,rolePermission)});
        Need(await PrincipalFootprintAsync(owner,rowsOnly:true)==dataBefore&&await PrivilegeGlobalBoundaryAsync(owner)==globalBefore,
            "Declared owner ACL setup changed business rows/global roles/memberships/default ACLs.");
        result["principalApprovalPrivilegeAclPreparation"]=new {status="DECLARED_NEW_FIXTURE_OWNER_ACL_BASELINE",steps=prep,
            businessRowsUnchanged=true,globalRolesMembershipsDefaultAclsUnchanged=true,acceptedDatabasesChanged=false};
        PrivilegeFault[] faults=[
            new("RecordMissingRequestSelect",false,request,"SELECT",true,false),
            new("RecordMissingCorePermissionSelect",false,rolePermission,"SELECT",true,false),
            new("RecordMissingCheckerInsert",false,budget,"INSERT",true,true),
            new("RecordMissingAuthorityUpdate",false,grant,"UPDATE",true,true),
            new("ExecutionMissingCaseSelect",true,cases,"SELECT",true,true),
            new("ExecutionMissingDecisionInsert",true,decisions,"INSERT",true,true),
            new("ExecutionMissingAssignmentUpdate",true,assignments,"UPDATE",true,true),
            new("ExecutionMissingRequestUpdate",true,request,"UPDATE",true,true),
            new("RecordExtraCheckerColumnUpdate",false,budgetHash,"UPDATE",false,false),
            new("ExecutionExtraAssignmentColumnUpdate",true,assignmentHash,"UPDATE",false,false),
            new("RecordExtraAuditTruncate",false,audit,"TRUNCATE",false,false),
            new("ExecutionExtraOutboxTruncate",true,outbox,"TRUNCATE",false,false),
            new("RecordExtraCoreCaseInsert",false,cases,"INSERT",false,true),
            new("ExecutionExtraDecisionColumnUpdate",true,decisionAction,"UPDATE",false,true),
            new("ExecutionExtraDecisionDelete",true,decisions,"DELETE",false,true),
            new("ExecutionExtraCaseTruncate",true,cases,"TRUNCATE",false,true),
            new("ExecutionExtraDelegationInsert",true,delegation,"INSERT",false,true),
            new("ExecutionExtraIdentityColumnUpdate",true,userWorker,"UPDATE",false,false)];
        var recordBaseline=new PrivilegeState(true,true,false);var executionBaseline=new PrivilegeState(true,false,true);
        async Task Baselines()
        {
            Need(await PrivilegeReadinessAsync(fixture,roles.Record,roles.RecordRole)==recordBaseline,"Actual record LOGIN baseline unavailable.");
            Need(await PrivilegeReadinessAsync(fixture,roles.Execution,roles.ExecutionRole)==executionBaseline,"Actual execution LOGIN baseline unavailable.");
        }
        await Baselines();var completed=new List<object>();
        foreach(var fault in faults)
        {
            progress("PrivilegeMatrix"+fault.Name);
            var connection=fault.Execution?roles.Execution:roles.Record;var role=fault.Execution?roles.ExecutionRole:roles.RecordRole;
            var before=await PrincipalFootprintAsync(owner);var acl=await PrivilegeAclAsync(owner,fault.Relation);
            var original=await PrivilegeReadinessAsync(fixture,connection,role,fault);
            Need(original.EffectivePrivilege==fault.Remove,"Target effective privilege does not match declared non-vacuous baseline.");
            try
            {
                // Autocommit owner ACL change MUST be visible to the distinct LOGIN;
                // no SET ROLE or uncommitted owner-transaction simulation is used.
                await PrivilegeChangeAsync(owner,fault.Relation,fault.Permission,role,fault.Remove);
                var observed=await PrivilegeReadinessAsync(fixture,connection,role,fault);
                Need(observed.EffectivePrivilege==!fault.Remove,"Effective privilege did not change on the same finite provider connection.");
                Need(observed.State==new PrivilegeState(fault.ReadAfter,false,false),"Actual finite provider did not fail closed in the affected access mode.");
                var control=await PrivilegeReadinessAsync(fixture,fault.Execution?roles.Record:roles.Execution,fault.Execution?roles.RecordRole:roles.ExecutionRole);
                Need(control==(fault.Execution?recordBaseline:executionBaseline),"Privilege negative changed the other finite role baseline.");
            }
            finally
            {
                await PrivilegeChangeAsync(owner,fault.Relation,fault.Permission,role,!fault.Remove);
                Need(await PrivilegeAclAsync(owner,fault.Relation)==acl,"Raw table/column ACL representation was not restored exactly.");
                Need(await PrincipalFootprintAsync(owner)==before,"Privilege negative did not restore the entire original footprint.");
                await Baselines();
            }
            completed.Add(new {name=fault.Name,login=fault.Execution?"Execution":"Record",effectivePrivilegeChanged=true,
                affectedWriteReadinessFalse=true,readReadiness=fault.ReadAfter,rawAclRestoredExactly=true,fullFootprintRestoredExactly=true,bothRoleBaselinesRestored=true});
            result["principalApprovalPrivilegeMatrixVerification"]=new {status="IN_PROGRESS",completedCases=completed.Count,cases=completed};
        }
        result["principalApprovalPrivilegeMatrixVerification"]=new {status="PASS_PRIVATE_ACTUAL_LOGIN_PRIVILEGE_MATRIX_ONLY",caseCount=18,cases=completed,
            actualLoginNotSetRole=true,ownerConnectionAclCommandsOnly=true,noOperationalDml=true,fullFootprintRestoredEveryCase=true,rawAclRestoredEveryCase=true,
            bothRoleBaselinesRestoredEveryCase=true,productionTouched=false,externalFinanceAuthority=false,nativeIdentityProviderVerified=false};
    }

    private static async Task<PrivilegeState> PrivilegeReadinessAsync(PrincipalApprovalFixture fixture,string connection,string role)
        =>(await PrivilegeReadinessAsync(fixture,connection,role,null)).State;
    private static async Task<(PrivilegeState State,bool? EffectivePrivilege)> PrivilegeReadinessAsync(
        PrincipalApprovalFixture fixture,string connection,string role,PrivilegeFault? fault)
    {
        await using var db=fixture.Procurement(connection);await db.Database.OpenConnectionAsync();
        var finite=(NpgsqlConnection)db.Database.GetDbConnection();
        await using(var identity=new NpgsqlCommand("SELECT current_user=session_user AND current_user=@role AND NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))",finite))
        {identity.Parameters.AddWithValue("role",role);Need(await identity.ExecuteScalarAsync() is true,"Provider must run on the actual finite LOGIN, never owner SET ROLE.");}
        bool? effective=null;
        if(fault is not null)
        {
            var sql=fault.Relation.Column is null?"SELECT has_table_privilege(current_user,@relation,@permission)":"SELECT has_column_privilege(current_user,@relation,@column,@permission)";
            await using var privilege=new NpgsqlCommand(sql,finite);privilege.Parameters.AddWithValue("relation",fault.Relation.Sql);
            privilege.Parameters.AddWithValue("permission",fault.Permission);if(fault.Relation.Column is not null)privilege.Parameters.AddWithValue("column",fault.Relation.Column);
            effective=await privilege.ExecuteScalarAsync() is true;
        }
        await using var core=fixture.Core();var actual=new ProcurementPrincipalApprovalStoreReadiness(fixture.Scope,core);
        // All three public methods use THIS already-open finite connection.
        return(new(await actual.IsReadyAsync(db,default),await actual.CanRecordAsync(db,default),await actual.CanExecuteAsync(db,default)),effective);
    }
    private static async Task PrivilegeChangeAsync(NpgsqlConnection owner,PrivilegeRelation relation,string permission,string role,bool remove)
    {
        Need(permission is "SELECT" or "INSERT" or "UPDATE" or "DELETE" or "TRUNCATE","Unsupported private matrix privilege.");
        var columns=relation.Column is null?"":" ("+PrincipalApprovalRuntimePrivileges.Q(relation.Column)+")";
        await using var command=new NpgsqlCommand((remove?"REVOKE ":"GRANT ")+permission+columns+" ON TABLE "+relation.Sql
            +(remove?" FROM ":" TO ")+PrincipalApprovalRuntimePrivileges.Q(role),owner);await command.ExecuteNonQueryAsync();
    }
    private static async Task<string> PrivilegeAclAsync(NpgsqlConnection owner,PrivilegeRelation relation)
    {
        await using var command=new NpgsqlCommand("SELECT jsonb_build_object('table',c.relacl::text,'columns',(SELECT jsonb_agg(jsonb_build_array(a.attnum,a.attname,a.attacl::text) ORDER BY a.attnum) FROM pg_attribute a WHERE a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped))::text FROM pg_class c WHERE c.oid=to_regclass(@relation)",owner);
        command.Parameters.AddWithValue("relation",relation.Sql);return await command.ExecuteScalarAsync() as string??throw new PrivatePrincipalProbeFailure("Actual privilege relation missing");
    }
    private static async Task<string> PrivilegeGlobalBoundaryAsync(NpgsqlConnection owner)
    {
        await using var command=new NpgsqlCommand("SELECT jsonb_build_object('roles',(SELECT jsonb_agg(encode(sha256(convert_to(to_jsonb(r)::text,'UTF8')),'hex') ORDER BY rolname) FROM pg_authid r),'memberships',(SELECT jsonb_agg(to_jsonb(m) ORDER BY roleid,member,grantor) FROM pg_auth_members m),'defaults',(SELECT jsonb_agg(to_jsonb(d) ORDER BY defaclrole,defaclnamespace,defaclobjtype) FROM pg_default_acl d))::text",owner);
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
