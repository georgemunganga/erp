using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Domain;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    // Opt-in NEW fixture only. Root owns the shared entry hook; ordinary runs do not call this.
    private static async Task VerifySchemaNegativesAsync(PrincipalApprovalFixture fixture,
        PrincipalApprovalRuntimePrivileges.Connections roles,NpgsqlConnection owner,Dictionary<string,object?> result,Action<string> progress)
    {
        progress("SchemaNegativePreparation");
        Need(owner.Database.StartsWith("proc_p25_http_",StringComparison.Ordinal)
            &&new NpgsqlConnectionStringBuilder(owner.ConnectionString).Host=="/authority-identity-private-socket",
            "Schema negatives require the fresh private fixture database.");
        await using(var identity=new NpgsqlCommand("SELECT current_user='postgres' AND session_user='postgres' AND system_identifier::text='7698678713374847009' AND current_setting('server_version_num')::integer BETWEEN 170000 AND 179999 AND current_setting('listen_addresses')='' FROM pg_control_system()",owner))
            Need(await identity.ExecuteScalarAsync() is true,"Exact private owner cluster required before schema negatives.");
        Need(System.Text.RegularExpressions.Regex.IsMatch(roles.RecordRole,"^p25_record_[0-9a-f]{16}$")
            &&System.Text.RegularExpressions.Regex.IsMatch(roles.ExecutionRole,"^p25_execute_[0-9a-f]{16}$")
            &&roles.RecordRole[11..]==roles.ExecutionRole[12..],"Only newly provisioned finite fixture roles are supported.");
        await using var proc=fixture.Procurement();var model=proc.GetService<IDesignTimeModel>().Model;
        IEntityType Entity(Type type)=>model.FindEntityType(type)??throw new PrivatePrincipalProbeFailure("Schema negative owning model missing");
        static PrivilegeRelation Relation(IEntityType entity)=>new(entity.GetSchema()!,entity.GetTableName()!);
        static string Q(string name)=>PrincipalApprovalRuntimePrivileges.Q(name);
        var receiptEntity=Entity(typeof(ProcurementApprovalCommandReceipt));var receipt=Relation(receiptEntity);
        var requestEntity=Entity(typeof(PurchaseRequest));var request=Relation(requestEntity);
        var authorityEntity=Entity(typeof(SpendingAuthorityIdentityRecord));var authority=Relation(authorityEntity);
        Need(new[]{receipt,request,authority}.All(x=>x.Schema=="procurement"),"Only exact owning Procurement relations may be altered.");
        // Refuse an already-used fixture. This hook precedes ALL HTTP/business graph writes.
        foreach(var relation in new[]{receipt,request,authority})
        {
            await using var empty=new NpgsqlCommand("SELECT NOT EXISTS(SELECT 1 FROM "+relation.Sql+")",owner);
            Need(await empty.ExecuteScalarAsync() is true,"Schema negative hook requires unused fresh fixture tables.");
        }
        var pk=receiptEntity.FindPrimaryKey()??throw new PrivatePrincipalProbeFailure("Receipt primary key missing");
        var foreign=receiptEntity.GetForeignKeys().Single(x=>x.PrincipalEntityType.ClrType==typeof(PurchaseRequest));
        var check=requestEntity.GetCheckConstraints().Single(x=>x.Name=="ck_pr_original_principal_identity");
        var partial=authorityEntity.GetIndexes().Single(x=>x.IsUnique&&x.GetDatabaseName()=="ux_authority_identity_origin"&&x.GetFilter() is not null);
        var migration=proc.Database.GetMigrations().Single(x=>x.EndsWith("_P25CorePrincipalRequestApproval",StringComparison.Ordinal));
        var history=new PrivilegeRelation("procurement","__procurement_migrations");
        var constraints=new[]{(Name:"MissingLeafPrimaryKey",Relation:receipt,Constraint:pk.GetName()!,Kind:"p"),
            (Name:"MissingScopedForeignKey",Relation:receipt,Constraint:foreign.GetConstraintName()!,Kind:"f"),
            (Name:"MissingPrincipalCheck",Relation:request,Constraint:check.Name!,Kind:"c")};
        var definitions=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var item in constraints)
        {
            await using var command=new NpgsqlCommand("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid=to_regclass(@relation) AND conname=@name AND contype::text=@kind AND convalidated",owner);
            command.Parameters.AddWithValue("relation",item.Relation.Sql);command.Parameters.AddWithValue("name",item.Constraint);command.Parameters.AddWithValue("kind",item.Kind);
            definitions.Add(item.Name,await command.ExecuteScalarAsync() as string??throw new PrivatePrincipalProbeFailure("Exact validated model constraint missing"));
        }
        // Never CASCADE. Even an unexpected incoming FK vetoes preparation before any DROP.
        async Task LeafHasNoIncomingForeignKeys()
        {
            await using var incoming=new NpgsqlCommand("SELECT NOT EXISTS(SELECT 1 FROM pg_constraint WHERE contype='f' AND confrelid=to_regclass(@relation))",owner);
            incoming.Parameters.AddWithValue("relation",receipt.Sql);Need(await incoming.ExecuteScalarAsync() is true,"Leaf primary key has incoming foreign keys; abandon fixture without dropping dependencies.");
        }
        await LeafHasNoIncomingForeignKeys();
        string indexDefinition;
        await using(var command=new NpgsqlCommand("SELECT pg_get_indexdef(i.indexrelid) FROM pg_index i JOIN pg_class c ON c.oid=i.indexrelid WHERE i.indrelid=to_regclass(@relation) AND c.relname=@name AND i.indisunique AND i.indisvalid AND i.indisready AND i.indpred IS NOT NULL AND NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conindid=i.indexrelid)",owner))
        {
            command.Parameters.AddWithValue("relation",authority.Sql);command.Parameters.AddWithValue("name",partial.GetDatabaseName()!);
            indexDefinition=await command.ExecuteScalarAsync() as string??throw new PrivatePrincipalProbeFailure("Exact valid model partial unique index missing");
        }
        string productVersion;
        await using(var command=new NpgsqlCommand("SELECT \"ProductVersion\" FROM "+history.Sql+" WHERE \"MigrationId\"=@migration",owner))
        {command.Parameters.AddWithValue("migration",migration);productVersion=await command.ExecuteScalarAsync() as string??throw new PrivatePrincipalProbeFailure("Actual P25 history row missing");}
        var recordBaseline=new PrivilegeState(true,true,false);var executionBaseline=new PrivilegeState(true,false,true);
        async Task Baselines()
        {
            Need(await PrivilegeReadinessAsync(fixture,roles.Record,roles.RecordRole)==recordBaseline,"Record LOGIN must be ready before and after each schema defect.");
            Need(await PrivilegeReadinessAsync(fixture,roles.Execution,roles.ExecutionRole)==executionBaseline,"Execution LOGIN must be ready before and after each schema defect.");
        }
        // Prove the SELECT grant did not change and actually read from each defect's
        // relation using the SAME actual LOGIN connection as the public provider calls.
        async Task<PrivilegeState> ReadWithSelect(string connection,string role,PrivilegeRelation relation)
        {
            await using var finite=fixture.Procurement(connection);await finite.Database.OpenConnectionAsync();
            var connectionOwner=(NpgsqlConnection)finite.Database.GetDbConnection();
            await using(var login=new NpgsqlCommand("SELECT current_user=session_user AND current_user=@role AND has_table_privilege(current_user,@relation,'SELECT') AND NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))",connectionOwner))
            {login.Parameters.AddWithValue("role",role);login.Parameters.AddWithValue("relation",relation.Sql);Need(await login.ExecuteScalarAsync() is true,"Actual finite LOGIN must retain SELECT during schema defect.");}
            await using(var select=new NpgsqlCommand("SELECT * FROM "+relation.Sql+" LIMIT 0",connectionOwner))await using(var reader=await select.ExecuteReaderAsync()){}
            await using var core=fixture.Core();var provider=new Mightyfin.Erp.Hrm.Api.ProcurementPrincipalApprovalStoreReadiness(fixture.Scope,core);
            return new(await provider.IsReadyAsync(finite,default),await provider.CanRecordAsync(finite,default),await provider.CanExecuteAsync(finite,default));
        }
        async Task Execute(string statement)
        {await using var command=new NpgsqlCommand(statement,owner);await command.ExecuteNonQueryAsync();}
        var completed=new List<object>();
        async Task Verify(string name,PrivilegeRelation relation,Func<Task> remove,Func<Task> absent,Func<Task> restore)
        {
            progress("SchemaNegative"+name);await Baselines();
            var before=await PrincipalFootprintAsync(owner);var acl=await PrivilegeAclAsync(owner,relation);
            Need(await ReadWithSelect(roles.Record,roles.RecordRole,relation)==recordBaseline
                &&await ReadWithSelect(roles.Execution,roles.ExecutionRole,relation)==executionBaseline,"Non-vacuous schema baseline required on both finite LOGIN connections.");
            var removeCompleted=false;var phase="Remove";Exception? firstFailure=null;string? firstFailurePhase=null;
            var secondary=new List<object>();
            static string FailureKind(Exception error)=>error switch
            {
                PrivatePrincipalProbeFailure=>"Verification",
                PostgresException=>"PostgreSql",
                NpgsqlException=>"Transport",
                OperationCanceledException=>"Cancellation",
                _=>"Unexpected"
            };
            try
            {
                await remove();removeCompleted=true;phase="AssertAbsent";await absent();
                phase="FiniteReadiness";
                Need(await ReadWithSelect(roles.Record,roles.RecordRole,relation)==new PrivilegeState(false,false,false)
                    &&await ReadWithSelect(roles.Execution,roles.ExecutionRole,relation)==new PrivilegeState(false,false,false),
                    "Actual provider must reject missing schema independently of retained SELECT.");
                phase="DefectRawAcl";
                Need(await PrivilegeAclAsync(owner,relation)==acl,"Schema defect changed table or column ACLs.");
            }
            catch(Exception error){firstFailure=error;firstFailurePhase=phase;}
            finally
            {
                async Task Check(string check,Func<Task> action)
                {
                    try{await action();}
                    catch(Exception error)
                    {
                        if(firstFailure is null){firstFailure=error;firstFailurePhase=check;}
                        else secondary.Add(new {check,kind=FailureKind(error)});
                    }
                }
                // Only a successfully completed removal authorizes its inverse.
                // A transport error can leave commit status unknown: retain failure,
                // inspect the safe read-only checks below, and NEVER guess a repair.
                if(removeCompleted)await Check("Restore",restore);
                await Check("RestoredRawAcl",async()=>
                    Need(await PrivilegeAclAsync(owner,relation)==acl,"Schema restore changed raw table or column ACLs."));
                await Check("RestoredLogicalFootprint",async()=>
                    Need(await PrincipalFootprintAsync(owner)==before,"Schema negative did not restore complete logical data/catalog/privilege footprint."));
                await Check("RestoredRoleBaselines",Baselines);
            }
            if(firstFailure is not null)
            {
                result["principalApprovalSchemaNegativeFailure"]=new {status="FAILED_RETAIN_FIXTURE",name,removeCompleted,
                    restorationAttempted=removeCompleted,firstFailurePhase,firstFailureKind=FailureKind(firstFailure),secondary,
                    noGuessedRepair=true,productionTouched=false};
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstFailure).Throw();
            }
            completed.Add(new {name,actualFiniteLogins=true,selectStillEffective=true,affectedReadAndWriteReadinessFalse=true,
                logicalFootprintRestoredExactly=true,rawAclRestoredExactly=true,bothRoleBaselinesRestored=true});
            result["principalApprovalSchemaNegativeVerification"]=new {status="IN_PROGRESS",completedCases=completed.Count,cases=completed};
        }
        foreach(var item in constraints)
        {
            var captured=item;
            await Verify(item.Name,item.Relation,async()=>
            {
                if(captured.Kind=="p")await LeafHasNoIncomingForeignKeys();
                await Execute("ALTER TABLE "+captured.Relation.Sql+" DROP CONSTRAINT "+Q(captured.Constraint));
            },async()=>
            {
                await using var missing=new NpgsqlCommand("SELECT NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conrelid=to_regclass(@relation) AND conname=@name)",owner);
                missing.Parameters.AddWithValue("relation",captured.Relation.Sql);missing.Parameters.AddWithValue("name",captured.Constraint);
                Need(await missing.ExecuteScalarAsync() is true,"Schema constraint defect not actually applied.");
            },()=>Execute("ALTER TABLE "+captured.Relation.Sql+" ADD CONSTRAINT "+Q(captured.Constraint)+" "+definitions[captured.Name]));
        }
        await Verify("MissingPartialUniqueIndex",authority,
            ()=>Execute("DROP INDEX "+Q(authority.Schema)+"."+Q(partial.GetDatabaseName()!)),async()=>
            {
                await using var missing=new NpgsqlCommand("SELECT to_regclass(@name) IS NULL",owner);
                missing.Parameters.AddWithValue("name",Q(authority.Schema)+"."+Q(partial.GetDatabaseName()!));
                Need(await missing.ExecuteScalarAsync() is true,"Partial index defect not actually applied.");
            },()=>Execute(indexDefinition));
        await Verify("MissingP25History",history,async()=>
            {
                await using var remove=new NpgsqlCommand("DELETE FROM "+history.Sql+" WHERE \"MigrationId\"=@migration AND \"ProductVersion\"=@version",owner);
                remove.Parameters.AddWithValue("migration",migration);remove.Parameters.AddWithValue("version",productVersion);
                Need(await remove.ExecuteNonQueryAsync()==1,"Exactly one actual P25 history row must be removed.");
            },async()=>
            {
                await using var missing=new NpgsqlCommand("SELECT NOT EXISTS(SELECT 1 FROM "+history.Sql+" WHERE \"MigrationId\"=@migration)",owner);
                missing.Parameters.AddWithValue("migration",migration);Need(await missing.ExecuteScalarAsync() is true,"P25 history defect not actually applied.");
            },async()=>
            {
                await using var restore=new NpgsqlCommand("INSERT INTO "+history.Sql+" (\"MigrationId\",\"ProductVersion\") VALUES (@migration,@version)",owner);
                restore.Parameters.AddWithValue("migration",migration);restore.Parameters.AddWithValue("version",productVersion);
                Need(await restore.ExecuteNonQueryAsync()==1,"Exactly one captured actual P25 history row must be restored.");
            });
        result["principalApprovalSchemaNegativeVerification"]=new {status="PASS_PRIVATE_ACTUAL_LOGIN_SCHEMA_NEGATIVES_ONLY",caseCount=5,cases=completed,
            trustedDefinitions=definitions,partialIndexDefinition=indexDefinition,migrationId=migration,
            actualLoginNotSetRole=true,ownerConnectionSchemaCommandsOnly=true,noOperationalDml=true,
            selectRetainedEveryCase=true,fullLogicalFootprintRestoredEveryCase=true,rawAclRestoredEveryCase=true,bothRoleBaselinesRestoredEveryCase=true,
            physicalOidsRestored=false,productionTouched=false,externalFinanceAuthority=false,nativeIdentityProviderVerified=false};
    }
}
