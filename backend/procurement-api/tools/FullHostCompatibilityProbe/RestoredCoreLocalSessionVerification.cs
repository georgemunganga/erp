using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Npgsql;

/// <summary>Private existing-handler proof. Owner provisioning/row/audit observation stays
/// outside the app. An HTTP pass cannot assert that append-only audit rows were persisted.</summary>
internal static class RestoredCoreLocalSessionVerification
{
    internal sealed record Credential(string Name,string Password);
    internal sealed record Actor(Guid Id,string Email,string Password,string[] NewPasswords);
    internal sealed record Input(string Database,string Tenant,Guid CompanyId,Guid BranchId,Guid ForeignCompanyId,Guid ForeignBranchId,
        Credential Core,Credential Record,Credential Execution,Actor Granted,Actor Ungranted,string OwnerBaselineSha256);
    private sealed class Tenant(string id):ITenantAccessor { public string GetTenantId()=>id; }
    internal static HrmDbContext ModelContext(string connection,string tenant)=>new(
        new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection,n=>n.MigrationsHistoryTable("__hrm_migrations","hrm")).Options,new Tenant(tenant));
    private static void Need(bool condition,string code){if(!condition)throw new InvalidOperationException(code);}
    private static string Build(string database,Credential c)=>new NpgsqlConnectionStringBuilder
    {
        Host="/private-pg-socket",Port=5432,Database=database,Username=c.Name,Password=c.Password,
        Pooling=true,Multiplexing=false,NoResetOnClose=false,Timeout=10,CommandTimeout=90,
        IncludeErrorDetail=false,LogParameters=false,
    }.ConnectionString;

    internal static async Task DescribeAsync()
    {
        Need(RestoredCoreSessionConnections.Selected,"ExplicitCoreSessionManifestOptInRequired");
        // Model mapping only: does not open this dummy finite connection or grant anything.
        using var core=ModelContext("Host=/private-pg-socket;Database=proc_p30_core_session_20261024_01;Username=p30_core_session_login_01", "p30-core-session-01");
        await WriteNewAsync("/result/core-profile.manifest.private.json",CoreLocalSessionRuntimeProfile.Build(core));
        Console.WriteLine("CORE_SESSION_MAPPED_MANIFEST_ONLY_NOT_PHYSICAL_READINESS");
    }

    internal static async Task RunAsync()
    {
        CoreLocalSessionConfigurationVerification.Verify();
        const string path="/evidence/core-session-input.private.json";
        Need(RestoredCoreSessionConnections.Selected&&File.Exists(path)
            &&(File.GetAttributes(path)&FileAttributes.ReparsePoint)==0
            &&new FileInfo(path).Length<=65_536
            &&File.GetUnixFileMode(path)==(UnixFileMode.UserRead|UnixFileMode.UserWrite),"PrivateCoreSessionInputRequired");
        var input=JsonSerializer.Deserialize<Input>(await File.ReadAllTextAsync(path),new JsonSerializerOptions {UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow})??throw new InvalidOperationException("CoreSessionInputMissing");
        Need(Regex.IsMatch(input.Database,"^proc_p30_core_session_20261024_[0-9]{2}$")
            &&Regex.IsMatch(input.OwnerBaselineSha256,"^[0-9a-f]{64}$")
            &&new[]{input.CompanyId,input.BranchId,input.ForeignCompanyId,input.ForeignBranchId,input.Granted.Id,input.Ungranted.Id}.All(x=>x!=Guid.Empty)
            &&input.CompanyId!=input.ForeignCompanyId&&input.BranchId!=input.ForeignBranchId&&input.Granted.Id!=input.Ungranted.Id,"PrivateCoreSessionScopeInvalid");
        foreach(var actor in new[]{input.Granted,input.Ungranted})
            Need(actor.Email.EndsWith("@core-session.example.test",StringComparison.Ordinal)
                &&Regex.IsMatch(actor.Password,"^[0-9a-f]{64}$")&&actor.NewPasswords.Length==2
                &&actor.NewPasswords.All(p=>Regex.IsMatch(p,"^[0-9a-f]{64}$"))
                &&actor.NewPasswords.Distinct(StringComparer.Ordinal).Count()==2
                &&!actor.NewPasswords.Contains(actor.Password,StringComparer.Ordinal),"ExactPrivateCoreActorCredentialsRequired");
        var coreConnection=Build(input.Database,input.Core);var record=Build(input.Database,input.Record);var execution=Build(input.Database,input.Execution);
        var context=new RestoredCoreSessionConnections(coreConnection,input.Tenant);
        context.RequireSelection("",record,execution,true,true,false,false,false);
        foreach(var (value,kind) in new[]{(coreConnection,"core_session"),(record,"record"),(execution,"execute")})
        {
            var expected=RestoredCoreSessionConnections.Require(value,kind);
            var wrong=new NpgsqlConnectionStringBuilder(value){Pooling=false,Password=expected.Password![..^1]+(expected.Password[^1]=='0'?"1":"0")};
            var denied=false;
            try {await using var bad=new NpgsqlConnection(wrong.ConnectionString);await bad.OpenAsync();}
            catch(PostgresException error) when(error.SqlState=="28P01"){denied=true;}
            Need(denied,"CoreSessionRealPasswordAuthenticationRequired");
            await using var connection=new NpgsqlConnection(value);await connection.OpenAsync();await IdentityAsync(connection,expected.Username!);
        }
        using(var core=ModelContext(coreConnection,input.Tenant))
        {
            var users=await core.LocalUsers.AsNoTracking().Where(x=>x.TenantId==input.Tenant
                &&(x.Id==input.Granted.Id||x.Id==input.Ungranted.Id)).ToArrayAsync();
            Need(users.Length==2&&users.All(x=>x.IsActive&&!x.IsArchived&&x.WorkerId is null&&x.LockedUntil is null
                &&x.FailedLoginCount==0&&!x.MustChangePassword),"FreshOwnedWorkerlessActorsRequired");
            Need(users.Single(x=>x.Id==input.Granted.Id).Email==input.Granted.Email&&users.Single(x=>x.Id==input.Ungranted.Id).Email==input.Ungranted.Email,"StoredCoreActorMismatch");
            Need(await core.LegalEntities.AnyAsync(x=>x.Id==input.CompanyId&&!x.IsArchived)
                &&await core.WorkLocations.AnyAsync(x=>x.Id==input.BranchId&&x.LegalEntityId==input.CompanyId&&!x.IsArchived)
                &&await core.WorkLocations.AnyAsync(x=>x.Id==input.ForeignBranchId&&x.LegalEntityId==input.ForeignCompanyId&&!x.IsArchived),"PreparedCoreCompanyBranchFactsRequired");
            foreach(var actor in users)
            {
                var declared=await core.UserBranchAssignments.Where(x=>x.UserId==actor.Id&&!x.IsArchived).Select(x=>x.LocationId).ToArrayAsync();
                Need(declared.Length==1&&declared[0]==input.BranchId,"PreparedCoreBranchConfinementRequired");
            }
            var granted=await ProcurementCorePrincipals.CurrentAsync(core,input.Tenant,input.Granted.Id.ToString("D"));
            var ungranted=await ProcurementCorePrincipals.CurrentAsync(core,input.Tenant,input.Ungranted.Id.ToString("D"));
            Need(granted is not null&&granted.Actions.SequenceEqual(new[]{"procurement-access"})
                &&ungranted is not null&&ungranted.Actions.Length==0,"PreparedCurrentCoreActionsRequired");
            Need(!await core.LocalSessions.AnyAsync(x=>(x.LocalUserId==input.Granted.Id||x.LocalUserId==input.Ungranted.Id)&&x.RevokedAt==null),"FreshOwnedActorsCannotHaveExistingActiveSessions");
        }
        // Dummy fixture supplies existing host mechanics only. No Bootstrap/Core/Procurement
        // owner method is called; the application receives no owner connection.
        var fixture=new PrincipalApprovalFixture("",false);
        using var host=new PrincipalApprovalHost(fixture,"",record,execution,actualSchema:true,
            normalLocalAuthentication:true,principalExecutionEnabled:false,restoredCore:context);
        host.PrepareEntryPointEnvironment();
        using var client=host.CreateClient(new WebApplicationFactoryClientOptions {BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false,HandleCookies=false});
        await VerifyServiceConnectionsAsync(host,input,context,record,execution);
        var observations=new List<object>();
        async Task<HttpResponseMessage> Send(string prefix,string method,string path,object? body,string? cookie,int expected,Guid actor,Guid? selectedCompany=null,Guid? selectedBranch=null)
        {
            using var request=new HttpRequestMessage(new HttpMethod(method),path);
            if(cookie is not null)request.Headers.Add("Cookie",cookie);
            if(body is not null)request.Content=JsonContent.Create(body);
            if(path.StartsWith("/api/procurement/",StringComparison.Ordinal))
            {request.Headers.Add("X-Shell-Entity",(selectedCompany??input.CompanyId).ToString("D"));request.Headers.Add("X-Shell-Location",(selectedBranch??input.BranchId).ToString("D"));}
            var response=await client.SendAsync(request);
            observations.Add(new {prefix,method,path,actorId=actor,selectedCompany,selectedBranch,status=(int)response.StatusCode});
            Need((int)response.StatusCode==expected,"CoreSessionHttpStatusMismatch");return response;
        }
        static string Cookie(HttpResponseMessage response)=>response.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith("hrm_session=",StringComparison.Ordinal)).Split(';')[0];
        var currentPassword=input.Granted.Password;
        var prefixes=new[]{"/api/hrm/auth","/api/v1/hrm/auth"};
        for(var i=0;i<prefixes.Length;i++)
        {
            var prefix=prefixes[i];var actor=input.Granted.Id;
            using(var denied=await Send(prefix,"POST",prefix+"/login",new {email=input.Granted.Email,password="invalid-not-the-private-password"},null,401,actor)){}
            using var login=await Send(prefix,"POST",prefix+"/login",new {email=input.Granted.Email,password=currentPassword},null,200,actor);
            var cookie=Cookie(login);
            using(var me=await Send(prefix,"GET",prefix+"/me",null,cookie,200,actor))
            {var json=await me.Content.ReadFromJsonAsync<JsonElement>();Need(json.GetProperty("authenticated").GetBoolean()&&json.GetProperty("user").GetProperty("id").GetGuid()==actor&&json.GetProperty("user").GetProperty("workerId").ValueKind==JsonValueKind.Null,"ActualCanonicalWorkerlessSessionRequired");}
            using(var scope=await Send(prefix,"GET","/api/procurement/v1/context",null,cookie,200,actor))
            {var json=await scope.Content.ReadFromJsonAsync<JsonElement>();Need(json.GetProperty("principalId").GetGuid()==actor&&json.GetProperty("legalEntityId").GetGuid()==input.CompanyId&&json.GetProperty("workerId").ValueKind==JsonValueKind.Null,"ActualCoreContextRequired");}
            using(var workspace=await Send(prefix,"GET","/api/core/v1/workspace-context",null,cookie,200,actor))
            {
                var json=await workspace.Content.ReadFromJsonAsync<JsonElement>();
                Need(json.GetProperty("principalId").GetGuid()==actor
                    &&json.GetProperty("companies").GetArrayLength()==1&&json.GetProperty("companies")[0].GetProperty("id").GetGuid()==input.CompanyId
                    &&json.GetProperty("branches").GetArrayLength()==1&&json.GetProperty("branches")[0].GetProperty("id").GetGuid()==input.BranchId,"ActualConfinedCoreWorkspaceRequired");
            }
            using(var foreign=await Send(prefix,"GET","/api/procurement/v1/context",null,cookie,403,actor,input.ForeignCompanyId,input.ForeignBranchId)){}
            using(var admin=await Send(prefix,"GET",prefix+"/users",null,cookie,403,actor)){}
            using(var invalid=await Send(prefix,"POST",prefix+"/change-password",new {currentPassword="invalid-current-password",newPassword=input.Granted.NewPasswords[i]},cookie,400,actor)){}
            using(var changed=await Send(prefix,"POST",prefix+"/change-password",new {currentPassword,newPassword=input.Granted.NewPasswords[i]},cookie,200,actor)){}
            using(var logout=await Send(prefix,"POST",prefix+"/logout",new{},cookie,200,actor)){}
            using(var revoked=await Send(prefix,"GET",prefix+"/me",null,cookie,200,actor))
            {Need(!(await revoked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("authenticated").GetBoolean(),"RevokedSessionMustBeAnonymous");}
            using(var revoked=await Send(prefix,"POST",prefix+"/logout",new{},cookie,401,actor)){}
            using(var old=await Send(prefix,"POST",prefix+"/login",new {email=input.Granted.Email,password=currentPassword},null,401,actor)){}
            currentPassword=input.Granted.NewPasswords[i];
            using(var renewed=await Send(prefix,"POST",prefix+"/login",new {email=input.Granted.Email,password=currentPassword},null,200,actor))
            using(var cleanup=await Send(prefix,"POST",prefix+"/logout",new{},Cookie(renewed),200,actor)){}
            using(var deniedLogin=await Send(prefix,"POST",prefix+"/login",new {email=input.Ungranted.Email,password=input.Ungranted.Password},null,200,input.Ungranted.Id))
            {
                var deniedCookie=Cookie(deniedLogin);
                using(var deniedContext=await Send(prefix,"GET","/api/procurement/v1/context",null,deniedCookie,403,input.Ungranted.Id)){}
                using(var deniedAdmin=await Send(prefix,"GET",prefix+"/users",null,deniedCookie,403,input.Ungranted.Id)){}
                using(var cleanup=await Send(prefix,"POST",prefix+"/logout",new{},deniedCookie,200,input.Ungranted.Id)){}
            }
        }
        using(var core=ModelContext(coreConnection,input.Tenant))
            Need(!await core.LocalSessions.AnyAsync(x=>(x.LocalUserId==input.Granted.Id||x.LocalUserId==input.Ungranted.Id)&&x.RevokedAt==null),"PrivateSessionCleanupMustUseActualLogout");
        await WriteNewAsync("/result/core-session-http.private.json",new {status="PASS_PRIVATE_CORE_SESSION_HTTP_AWAITING_OWNER_AUDIT",input.Database,input.Tenant,
            input.CompanyId,input.BranchId,grantedId=input.Granted.Id,ungrantedId=input.Ungranted.Id,input.OwnerBaselineSha256,observations,
            actualFiniteCoreService=true,actualFiniteRecordService=true,actualFiniteExecutionService=true,
            ownerAuditVerified=false,requiredOwnerAuditFamilies=new[]{"hrm.audit_entries","hrm.privileged_action_events"},
            requiredOwnerUnchanged=new[]{"original-source-rows","other-local-users","roles-and-branches","Procurement-business-and-audit-outbox"},
            generalHrmCrudProven=false,publisherStarted=false,productionTouched=false});
        Console.WriteLine("PASS_PRIVATE_CORE_SESSION_HTTP_AWAITING_OWNER_AUDIT");
    }

    private static async Task VerifyServiceConnectionsAsync(PrincipalApprovalHost host,Input input,RestoredCoreSessionConnections context,string record,string execution)
    {
        context.RequireConfiguration(host.Services.GetRequiredService<IConfiguration>(),record,execution);
        using var scope=host.Services.CreateScope();
        var core=scope.ServiceProvider.GetRequiredService<HrmDbContext>();
        var db=scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        foreach(var (store,kind) in new[]{((DbContext)core,"core_session"),((DbContext)db,"record")})
        {
            var connection=store.Database.GetDbConnection();RestoredCoreSessionConnections.Require(connection.ConnectionString,kind,false);
            await store.Database.OpenConnectionAsync();await IdentityAsync((NpgsqlConnection)connection,"p30_"+kind+"_login_"+input.Database[^2..]);
        }
        var requestScope=scope.ServiceProvider.GetRequiredService<ProcurementRequestScope>();
        requestScope.Set(input.Tenant,input.CompanyId,input.Granted.Id.ToString("D"),input.BranchId,null,false,"private-core-service-connection-proof");
        requestScope.AssignPrincipal(input.Granted.Id);
        var transaction=scope.ServiceProvider.GetRequiredService<ProcurementApprovalTransaction>();
        await transaction.ExecuteAsync(async (owned,cases,ct)=>
        {await IdentityAsync((NpgsqlConnection)owned.Database.GetDbConnection(),input.Execution.Name);await IdentityAsync((NpgsqlConnection)cases.Database.GetDbConnection(),input.Execution.Name);return true;});
    }

    private static async Task IdentityAsync(NpgsqlConnection connection,string role)
    {
        await using var command=new NpgsqlCommand("""
            SELECT current_user=@role AND session_user=@role AND inet_server_addr() IS NULL
            AND EXISTS(SELECT 1 FROM pg_roles r WHERE r.rolname=current_user AND r.rolcanlogin
              AND NOT(r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolreplication OR r.rolbypassrls OR r.rolinherit)
              AND r.rolconnlimit=-1 AND r.rolvaliduntil IS NULL AND r.rolconfig IS NULL)
            AND NOT EXISTS(SELECT 1 FROM pg_auth_members m JOIN pg_roles r ON r.oid=m.member OR r.oid=m.roleid WHERE r.rolname=current_user)
            AND NOT EXISTS(SELECT 1 FROM pg_class c WHERE pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET'))
            AND NOT EXISTS(SELECT 1 FROM pg_namespace n WHERE pg_has_role(current_user,n.nspowner,'MEMBER') OR pg_has_role(current_user,n.nspowner,'SET'))
            AND NOT EXISTS(SELECT 1 FROM pg_database d WHERE pg_has_role(current_user,d.datdba,'MEMBER') OR pg_has_role(current_user,d.datdba,'SET'))
            """,connection);
        command.Parameters.AddWithValue("role",role);Need(await command.ExecuteScalarAsync() is true,"ActualFiniteApplicationServiceIdentityRequired");
    }
    private static async Task WriteNewAsync<T>(string path,T value)
    {
        await using var stream=new FileStream(path,new FileStreamOptions {Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.None,
            UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite});
        await JsonSerializer.SerializeAsync(stream,value);
    }
}
