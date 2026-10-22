using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    internal static void Need(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);}
    internal static async Task RunAsync()
    {
        var owner=Environment.GetEnvironmentVariable("P25_HTTP_OWNER_CONNECTION")??throw new InvalidOperationException("Explicit private owner connection required.");
        var connection=new NpgsqlConnectionStringBuilder(owner);
        Need(connection.Host=="/authority-identity-private-socket" && connection.Database is {} database
            && System.Text.RegularExpressions.Regex.IsMatch(database,"^proc_p25_http_[a-z0-9_]{1,48}$") && !connection.Multiplexing,"Only bounded fresh private socket database supported.");
        var resultPath=Environment.GetEnvironmentVariable("P25_HTTP_RESULT")??throw new InvalidOperationException("New protected result required.");
        Need(Path.IsPathFullyQualified(resultPath)&&!File.Exists(resultPath)&&Directory.Exists(Path.GetDirectoryName(resultPath)),"New result in existing protected directory required.");
        var result=new Dictionary<string,object?> { ["status"]="NOT_STARTED",["stage"]="Bootstrap",["productionTouched"]=false,["testAuthenticationOnly"]=true,
            ["realIdentityProviderAuthentication"]=false,["externalFinanceAuthority"]=false,["manualEvidencePositiveWorkflow"]=false };
        var fixture=new PrincipalApprovalFixture(owner);var stage="Bootstrap";var requests=0;
        try
        {
            await fixture.BootstrapAsync();
            stage="FiniteRoleProvision";var roles=await PrincipalApprovalRuntimePrivileges.CreateAsync(owner,fixture);
            await using var ownerConnection=new NpgsqlConnection(owner);await ownerConnection.OpenAsync();
            foreach(var (text,expected) in new[]{(roles.Record,roles.RecordRole),(roles.Execution,roles.ExecutionRole)})
            {
                await using var runtime=new NpgsqlConnection(text);await runtime.OpenAsync();
                await using var command=new NpgsqlCommand("SELECT current_user=session_user AND current_user=@expected AND NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))",runtime);
                command.Parameters.AddWithValue("expected",expected);Need(await command.ExecuteScalarAsync() is true,"Finite test role is not an actual distinct restricted LOGIN.");
            }
            stage="CapturedActualReadRecordExecutionReadiness";
            await using(var core=fixture.Core())
            {
                var real=new ProcurementPrincipalApprovalStoreReadiness(fixture.Scope,core);
                await using var record=fixture.Procurement(roles.Record);await using var execute=fixture.Procurement(roles.Execution);await using var privileged=fixture.Procurement();
                Need(await real.IsReadyAsync(record,default)&&await real.CanRecordAsync(record,default),"Actual finite record-role readiness unavailable.");
                Need(!await real.CanExecuteAsync(record,default),"Record role must not acquire Core workflow writes.");
                Need(await real.IsReadyAsync(execute,default)&&await real.CanExecuteAsync(execute,default),"Actual finite execution-role readiness unavailable.");
                Need(!await real.CanRecordAsync(execute,default),"Execution role must not satisfy record-only no-Core-DML boundary.");
                Need(!await real.IsReadyAsync(privileged,default),"Owning bootstrap role must never be accepted as finite readiness.");
            }
            // First actual Program host retains normal Unavailable registration.
            stage="UnavailableDefaultRegistration";
            await using(var disabled=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,false))
            {
                disabled.PrepareEntryPointEnvironment();using var client=disabled.CreateClient(new(){AllowAutoRedirect=false});
                using var scope=disabled.Services.CreateScope();
                Need(scope.ServiceProvider.GetRequiredService<IProcurementPrincipalApprovalSchemaReadiness>() is UnavailableProcurementPrincipalApprovalSchemaReadiness,"Default registration unexpectedly enabled.");
                var baseline=await PrincipalFootprintAsync(ownerConnection);
                var meta=await Send(client,HttpMethod.Get,"/api/procurement/v1/meta",fixture.Maker);Need(!meta.GetProperty("capabilities").GetProperty("principalApprovals").GetBoolean(),"Default unavailable provider advertised approval.");
                await Send(client,HttpMethod.Post,$"/api/procurement/v1/requests/{Guid.NewGuid()}/approval-cases",fixture.Maker,new{expectedRequestVersion=1L},HttpStatusCode.ServiceUnavailable);
                Need(await PrincipalFootprintAsync(ownerConnection)==baseline,"Unavailable host calls wrote business evidence.");
            }
            stage="ActualProgramFiniteCreateSubmit";
            await using(var host=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,true))
            {
                host.PrepareEntryPointEnvironment();using var client=host.CreateClient(new(){AllowAutoRedirect=false});
                var options=host.Services.GetRequiredService<ProcurementApprovalExecutionOptions>();
                Need(options.Enabled&&options.PrincipalExecutionEnabled&&options.ExecutionDatabaseConfigured&&options.ManualFinancePolicies.Length==0,"Private host must retain missing manual Finance policy.");
                var created=await Send(client,HttpMethod.Post,"/api/procurement/v1/requests",fixture.Maker,new {purpose="Private P25 current Core-only request",neededBy=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),currencyCode="ZMW",lines=new[]{new{description="No employee prerequisite",unitCode="each",requestedQuantity="2",estimatedUnitPrice="5.1234"}}},HttpStatusCode.Created);
                var id=created.GetProperty("id").GetGuid();var path="/api/procurement/v1/requests/"+id;
                var submitted=await Send(client,HttpMethod.Post,path+"/submit",fixture.Maker,new {version=created.GetProperty("version").GetInt64()});
                await using(var db=fixture.Procurement())
                {
                    var source=await db.PurchaseRequests.Include(x=>x.Lines).SingleAsync(x=>x.Id==id);
                    Need(source.Status=="Submitted"&&source.RequesterPrincipalId==fixture.Maker&&source.RequesterWorkerId is null&&source.RequesterCoreWorkerId is null
                        &&source.RequesterIdentitySchemaVersion==1&&PurchaseRequestPrincipalIdentity.Valid(source),"Server-owned original principal1/source identity was not persisted.");
                    Need(source.Lines.Count==1&&source.Lines.Single().RequestedQuantity==2m&&source.Lines.Single().EstimatedUnitPrice==5.1234m,"Exact source values changed.");
                    result["requestId"]=id;result["submittedBusinessVersion"]=source.SubmittedVersion;result["currentRequestVersion"]=source.Version;
                }
                stage="FundingUnavailableNoEffects";var before=await PrincipalFootprintAsync(ownerConnection);
                var readiness=await Send(client,HttpMethod.Get,path+"/approval-readiness",fixture.Maker);
                Need(!readiness.GetProperty("canOpenCase").GetBoolean()&&readiness.GetProperty("blockers").EnumerateArray().Any(x=>x.GetString()=="PrincipalFinanceCheckerEvidenceRequired"),"Missing Finance/checker evidence must block actual readiness.");
                // This also detects default-record versus configured-execution role mixups after the reviewed preview fix.
                Need(!readiness.GetProperty("blockers").EnumerateArray().Any(x=>x.GetString()=="PrincipalApprovalSchemaUnavailable"),"Readiness preview used wrong/default record role instead of configured execution store.");
                var meta=await Send(client,HttpMethod.Get,"/api/procurement/v1/meta",fixture.Maker);Need(!meta.GetProperty("capabilities").GetProperty("principalApprovals").GetBoolean(),"Absent manual policy advertised configured approval availability.");
                await Send(client,HttpMethod.Post,path+"/approval-cases",fixture.Maker,new{expectedRequestVersion=submitted.GetProperty("version").GetInt64()},HttpStatusCode.Conflict);
                await Send(client,HttpMethod.Post,path+"/approval-cases",fixture.Ungranted,new{expectedRequestVersion=submitted.GetProperty("version").GetInt64()},HttpStatusCode.Forbidden);
                Need(before==await PrincipalFootprintAsync(ownerConnection),"Denied/unavailable approval commands changed persisted facts.");
                await using var proof=fixture.Procurement();await using var cases=fixture.Approvals();
                Need(!await proof.BudgetCheckSnapshots.AnyAsync()&&!await proof.ApprovalSubmissions.AnyAsync()&&!await proof.AssignmentAuthorityEvidence.AnyAsync()&&!await cases.Cases.AnyAsync(),"Unavailable funding left checker/pin/assignment/Core case effects.");
                Need(!await proof.FinanceInvoiceHandoffs.AnyAsync()&&!await proof.FinancePaymentStatusRefs.AnyAsync(),"PR-only denial emitted Finance/AP/payment facts.");
                result["companyId"]=fixture.Company;result["branchId"]=fixture.Branch;result["tenantId"]=PrincipalApprovalFixture.Tenant;result["makerPrincipalId"]=fixture.Maker;
                result["recordRole"]=roles.RecordRole;result["executionRole"]=roles.ExecutionRole;
            }
            result["status"]="PASS_PRIVATE_P25_CREATE_SUBMIT_FUNDING_UNAVAILABLE_ONLY";result["finiteRecordAndExecutionLoginsVerified"]=true;
            result["currentCoreWorkerlessSourceVerified"]=true;result["defaultUnavailableRegistrationVerified"]=true;result["fundingUnavailableNoEffectsVerified"]=true;
            result["positiveApprovalGraph"]=false;result["checker2Authority2Submission3Assignment2Acceptance"]="NOT_RUN_NEXT_CHECKPOINT";
        }
        catch(Exception error) {result["status"]="FAILED";result["exceptionType"]=error.GetType().FullName;Environment.ExitCode=2;}
        finally
        {
            result["stage"]=stage;result["actualHttpRequests"]=requests;
            await File.WriteAllTextAsync(resultPath,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
            if(OperatingSystem.IsLinux())File.SetUnixFileMode(resultPath,UnixFileMode.UserRead|UnixFileMode.UserWrite);
        }
        Console.WriteLine(result["status"]);

        async Task<JsonElement> Send(HttpClient client,HttpMethod method,string path,Guid actor,object? body=null,HttpStatusCode expected=HttpStatusCode.OK)
        {
            using var request=new HttpRequestMessage(method,path);request.Headers.Add("X-Private-P25-Subject",actor.ToString("D"));
            request.Headers.Add("X-Shell-Entity",fixture.Company.ToString("D"));request.Headers.Add("X-Shell-Location",fixture.Branch.ToString("D"));
            if(method==HttpMethod.Post)request.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString("D"));
            if(body is not null)request.Content=JsonContent.Create(body);
            using var response=await client.SendAsync(request);requests++;
            Need(response.StatusCode==expected,"Actual private HTTP status differs: "+(int)expected+"/"+(int)response.StatusCode);
            var text=await response.Content.ReadAsStringAsync();return string.IsNullOrWhiteSpace(text)?default:JsonDocument.Parse(text).RootElement.Clone();
        }
    }
    // Shared owner-only verifier for this mode and the separately authored raw storage checks.
    // Read-only: hashes every persisted non-system table, metadata, sequences and privilege boundary.
    internal static async Task<string> PrincipalFootprintAsync(NpgsqlConnection owner)
    {
        if(owner.State!=System.Data.ConnectionState.Open)await owner.OpenAsync();
        await using var transaction=await owner.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        await using(var readOnly=new NpgsqlCommand("SET TRANSACTION READ ONLY;SET LOCAL TIME ZONE 'UTC';SET LOCAL DateStyle='ISO,YMD';",owner,transaction))await readOnly.ExecuteNonQueryAsync();
        var tables=new List<(string Schema,string Table)>();
        await using(var list=new NpgsqlCommand("SELECT n.nspname,c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind IN ('r','p') AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema' ORDER BY 1,2",owner,transaction))
        await using(var reader=await list.ExecuteReaderAsync())while(await reader.ReadAsync())tables.Add((reader.GetString(0),reader.GetString(1)));
        var parts=new List<string>();
        foreach(var (schema,table) in tables)
        {
            var relation=PrincipalApprovalRuntimePrivileges.Q(schema)+"."+PrincipalApprovalRuntimePrivileges.Q(table);
            await using var values=new NpgsqlCommand("SELECT count(*)::text||':'||encode(sha256(convert_to(coalesce(string_agg(h,'' ORDER BY h),''),'UTF8')),'hex') FROM (SELECT encode(sha256(convert_to(to_jsonb(t)::text,'UTF8')),'hex') h FROM "+relation+" t)q",owner,transaction);
            parts.Add(schema+"."+table+":"+(string)(await values.ExecuteScalarAsync())!);
        }
        const string metadata="""
            SELECT jsonb_build_object(
             'columns',(SELECT jsonb_agg(jsonb_build_array(n.nspname,c.relname,a.attnum,a.attname,format_type(a.atttypid,a.atttypmod),a.attnotnull,pg_get_expr(d.adbin,d.adrelid),a.attidentity,a.attgenerated,a.attacl) ORDER BY n.nspname,c.relname,a.attnum) FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace LEFT JOIN pg_attrdef d ON d.adrelid=c.oid AND d.adnum=a.attnum WHERE a.attnum>0 AND NOT a.attisdropped AND c.relkind IN ('r','p') AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'),
             'constraints',(SELECT jsonb_agg(jsonb_build_array(n.nspname,c.relname,k.conname,pg_get_constraintdef(k.oid),k.convalidated,k.condeferrable,k.condeferred) ORDER BY n.nspname,c.relname,k.conname) FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'),
             'indexes',(SELECT jsonb_agg(jsonb_build_array(n.nspname,c.relname,pg_get_indexdef(i.indexrelid),i.indisvalid,i.indisready) ORDER BY n.nspname,c.relname) FROM pg_index i JOIN pg_class c ON c.oid=i.indexrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema'),
             'sequences',(SELECT jsonb_agg(to_jsonb(s) ORDER BY schemaname,sequencename) FROM pg_sequences s WHERE schemaname NOT LIKE 'pg_%' AND schemaname<>'information_schema'),
             'relationPrivileges',(SELECT jsonb_agg(jsonb_build_array(n.nspname,c.relname,c.relowner,c.relacl) ORDER BY n.nspname,c.relname) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema' AND c.relkind IN ('r','p','v','m','S','f')),
             'schemaPrivileges',(SELECT jsonb_agg(jsonb_build_array(nspname,nspowner,nspacl) ORDER BY nspname) FROM pg_namespace WHERE nspname NOT LIKE 'pg_%' AND nspname<>'information_schema'),
             'defaultPrivileges',(SELECT jsonb_agg(to_jsonb(x) ORDER BY defaclrole,defaclnamespace,defaclobjtype) FROM pg_default_acl x),
             'databasePrivileges',(SELECT jsonb_build_array(datdba,datacl,encoding,datcollate,datctype,datlocprovider,datconnlimit) FROM pg_database WHERE datname=current_database()),
             'roleFlags',(SELECT jsonb_agg(jsonb_build_array(rolname,rolsuper,rolinherit,rolcreaterole,rolcreatedb,rolcanlogin,rolreplication,rolbypassrls,rolconfig) ORDER BY rolname) FROM pg_roles),
             'memberships',(SELECT jsonb_agg(to_jsonb(x) ORDER BY roleid,member,grantor) FROM pg_auth_members x)
            )::text
            """;
        await using(var shape=new NpgsqlCommand(metadata,owner,transaction))parts.Add((string)(await shape.ExecuteScalarAsync())!);
        await transaction.RollbackAsync();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',parts))));
    }
}
