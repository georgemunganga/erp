using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;

internal static partial class PrincipalApprovalHttpVerification
{
    private const string P30ServiceCookieManifestPath=LocalCookieEvidenceDirectory+"/p30-service-local-cookie-fixture.private.json";
    private const string P30ServiceCookieCompletionPath=LocalCookieEvidenceDirectory+"/p30-service-local-cookie-browser-completion.private.json";

    // A second, separate private control plane. Native HTTP already certified two
    // halves on another genuine service PO; this one remains unconsumed for the UI.
    private static async Task ServeP30ServiceNormalLocalCookieAsync(PrincipalApprovalFixture fixture,string owner,
        PrincipalApprovalRuntimePrivileges.Connections roles,NpgsqlConnection ownerConnection,
        PrincipalIssuedFixture browserService,PrincipalIssuedFixture nativeGoods,
        Dictionary<string,object?> result,Action<string> progress,CancellationToken cancellationToken=default)
    {
        progress("P30ServiceNormalCookiePrivatePreparation");
        Need(fixture.PrincipalReceivingProfile&&result.TryGetValue("principalReceiving",out var accepted)&&accepted is not null
            &&browserService.RequestId!=Guid.Empty&&browserService.RequestLineId!=Guid.Empty
            &&browserService.SupplierId!=Guid.Empty&&browserService.OrderId!=Guid.Empty,
            "Service cookie fixture requires completed native receiving and a separate genuine source.");
        await RequirePrivateRawOwnerAsync(ownerConnection,PrincipalApprovalFixture.Tenant,fixture.Company);
        var parsedOwner=new NpgsqlConnectionStringBuilder(owner);
        Need(OperatingSystem.IsLinux()&&parsedOwner.Host=="/authority-identity-private-socket"
            &&parsedOwner.Database==ownerConnection.Database&&parsedOwner.Username=="postgres"
            &&System.Text.RegularExpressions.Regex.IsMatch(ownerConnection.Database,"^proc_p30_http_po_[a-z0-9_]{1,40}$"),
            "Service cookie fixture requires the exact fresh private Unix-socket owner database.");
        foreach(var finite in new[]{roles.Record,roles.Execution})
        {
            var parsed=new NpgsqlConnectionStringBuilder(finite);
            Need(parsed.Host==parsedOwner.Host&&parsed.Port==parsedOwner.Port&&parsed.Database==parsedOwner.Database,
                "Normal-cookie finite contexts must remain on the same private store.");
        }
        await using(var identity=new NpgsqlCommand("SELECT current_setting('server_version_num')::integer BETWEEN 170000 AND 179999 AND current_setting('listen_addresses')=''",ownerConnection))
            Need(await identity.ExecuteScalarAsync(cancellationToken) is true,"Private PostgreSQL17 without TCP required.");
        Need(Directory.Exists(LocalCookieEvidenceDirectory)
            &&(File.GetAttributes(LocalCookieEvidenceDirectory)&FileAttributes.ReparsePoint)==0
            &&File.GetUnixFileMode(LocalCookieEvidenceDirectory)==(UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute),
            "Exact private0700 evidence mount required.");
        Need(!File.Exists(P30ServiceCookieManifestPath)&&!Directory.Exists(P30ServiceCookieManifestPath)
            &&!File.Exists(P30ServiceCookieCompletionPath)&&!Directory.Exists(P30ServiceCookieCompletionPath),
            "Service cookie fixture and completion paths must be new.");
        Guid nativeServiceOrderId;
        await using(var db=fixture.Procurement())
        {
            var line=await db.PurchaseRequestLines.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==browserService.RequestLineId
                &&x.PurchaseRequestId==browserService.RequestId,cancellationToken);
            var poLine=await db.PurchaseOrderLines.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==browserService.OrderLineId
                &&x.PurchaseOrderId==browserService.OrderId,cancellationToken);
            var existing=await db.PrincipalReceivingSourceStates.AsNoTracking().Where(x=>x.Kind=="Service")
                .Select(x=>x.PurchaseOrderId).Distinct().ToArrayAsync(cancellationToken);
            Need(existing.Length==1&&existing[0]!=browserService.OrderId,"Native service history must exist on a separate source.");
            nativeServiceOrderId=existing[0];
            Need(await db.PurchaseRequests.AnyAsync(x=>x.Id==browserService.RequestId&&x.Status=="Approved",cancellationToken)
                &&line is not null&&line.PurchaseType=="SERVICE"&&line.ApprovedQuantity==1m
                &&line.AllocatedQuantity==0m&&line.OrderedQuantity==1m&&line.AcceptedQuantity==0m
                &&await db.Suppliers.AnyAsync(x=>x.Id==browserService.SupplierId&&x.Status=="Active",cancellationToken)
                &&await db.PurchaseOrders.AnyAsync(x=>x.Id==browserService.OrderId&&x.Status=="Issued",cancellationToken)
                &&poLine is not null&&poLine.Quantity==1m&&poLine.AcceptedQuantity==0m
                &&!await db.PrincipalReceivingSourceStates.AnyAsync(x=>x.PurchaseOrderLineId==browserService.OrderLineId,cancellationToken),
                "Browser must start with unconsumed genuine issued/manual-ack service source.");
        }
        var credentials=new List<LocalCookieCredential>();
        await using(var core=fixture.Core())
        {
            var subjects=fixture.Subjects;
            var users=await core.LocalUsers.AsNoTracking().Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&subjects.Contains(x.Id)).ToArrayAsync(cancellationToken);
            Need(users.Length==16&&users.All(x=>x.IsActive&&!x.IsArchived&&x.WorkerId is null
                &&x.PasswordHash=="private-no-real-login"&&x.LockedUntil is null&&x.FailedLoginCount==0
                &&x.MustChangePassword&&x.PasswordChangedAt is null&&x.LastLoginAt is null),
                "Only sixteen original synthetic workerless receiving accounts with unused placeholder credentials may be activated.");
            var activatedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            foreach(var user in users.OrderBy(x=>x.Id))
            {
                var password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))+"aA1!";
                var salt=RandomNumberGenerator.GetBytes(16);
                var key=Rfc2898DeriveBytes.Pbkdf2(password,salt,210_000,HashAlgorithmName.SHA256,32);
                var encoded="pbkdf2-sha256$210000$"+Convert.ToBase64String(salt)+"$"+Convert.ToBase64String(key);
                Need(await core.LocalUsers.Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==user.Id
                    &&x.PasswordHash=="private-no-real-login"&&x.IsActive&&!x.IsArchived&&x.WorkerId==null
                    &&x.LockedUntil==null&&x.FailedLoginCount==0&&x.MustChangePassword&&x.PasswordChangedAt==null&&x.LastLoginAt==null)
                    .ExecuteUpdateAsync(set=>set.SetProperty(x=>x.PasswordHash,encoded)
                        .SetProperty(x=>x.MustChangePassword,false)
                        .SetProperty(x=>x.PasswordChangedAt,(DateTimeOffset?)activatedAt),cancellationToken)==1,
                    "Exact new synthetic service credential activation failed; do not overwrite or retry.");
                credentials.Add(new(user.Id,user.Email,password));
            }
        }
        result["p30ServiceNormalLocalCookieControlPlane"]=new {status="PREPARING_NOT_BROWSER_ACCEPTANCE",
            syntheticCredentialsSeeded=credentials.Count,syntheticAccountsActivated=credentials.Count,
            browserAcceptanceVerified=false,productionTouched=false};
        await using var host=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,
            actualSchema:true,privateManualEvidence:true,normalLocalAuthentication:true);
        host.PrepareEntryPointEnvironment();host.UseKestrel(options=>options.Listen(IPAddress.Loopback,0));host.StartServer();
        using(var registration=host.Services.CreateScope())
        {
            var configured=registration.ServiceProvider.GetRequiredService<ProcurementPrincipalReceivingOptions>();
            var actual=registration.ServiceProvider.GetRequiredService<IProcurementPrincipalReceivingReadiness>();
            Need(configured.Enabled&&configured.ExecutionDatabaseConfigured
                &&actual is ProcurementPrincipalReceivingReadiness&&await actual.IsReadyAsync(cancellationToken),
                "Service normal host must use actual finite owned readiness, not a flag-only capability.");
        }
        var schemes=host.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var all=await schemes.GetAllSchemesAsync();
        Need((await schemes.GetDefaultAuthenticateSchemeAsync())?.Name=="local"
            &&(await schemes.GetDefaultChallengeSchemeAsync())?.Name=="local"
            &&all.Any(x=>x.Name=="local"&&x.HandlerType.Name=="LocalAuthenticationHandler")
            &&all.All(x=>x.Name!="private-p25-subject")
            &&host.Services.GetService<PrincipalApprovalSubjects>() is null,
            "Normal cookie host must keep real local authentication without a private subject handler.");
        var addresses=host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.ToArray();
        Need(addresses is {Length:1}&&Uri.TryCreate(addresses[0],UriKind.Absolute,out var address)
            &&address.Scheme=="http"&&address.Host=="127.0.0.1"&&address.Port>0,
            "Only one ephemeral private loopback HTTP listener is supported.");
        var url=addresses![0];
        using(var anonymous=new HttpClient(new HttpClientHandler {AllowAutoRedirect=false,UseCookies=false}) {BaseAddress=new Uri(url),Timeout=TimeSpan.FromSeconds(15)})
        using(var request=new HttpRequestMessage(HttpMethod.Get,"/api/procurement/v1/context"))
        {
            request.Headers.Add("X-Private-P25-Subject",fixture.Receiver.ToString("D"));
            using var denied=await anonymous.SendAsync(request,cancellationToken);
            Need(denied.StatusCode==HttpStatusCode.Unauthorized,"Private subject header must not authenticate normal cookie host.");
        }
        var challenge=Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var deadline=DateTimeOffset.UtcNow.AddMinutes(15);
        var manifest=new {schemaVersion=3,controlPlaneOnly=true,apiUrl=url,database=ownerConnection.Database,
            tenantId=PrincipalApprovalFixture.Tenant,companyId=fixture.Company,branchId=fixture.Branch,
            receiverPrincipalId=fixture.Receiver,serviceOwnerPrincipalId=fixture.ServiceOwner,
            approvedRequestId=browserService.RequestId,approvedRequestLineId=browserService.RequestLineId,
            supplierId=browserService.SupplierId,issuedOrderId=browserService.OrderId,issuedVersionId=browserService.IssuedId,
            issuedOrderLineId=browserService.OrderLineId,nativeGoodsOrderId=nativeGoods.OrderId,nativeServiceOrderId,
            syntheticCredentials=credentials,challenge,syntheticAccountsActivated=16,
            syntheticActivationMode="EXACT_NEW_PLACEHOLDER_CAS_ONLY",completionPath=P30ServiceCookieCompletionPath,
            expiresAt=deadline,secureCookieRequiresHttpsProxy=true,
            apiSetupAuthentication="PRIVATE_SUBJECT_TEST_AUTH_ONLY",
            browserAuthenticationRequired="NORMAL_LOCAL_PASSWORD_AND_SESSION_COOKIE",
            localCookieLoginVerified=false,browserAcceptanceVerified=false,
            nativeIdentityProviderVerified=false,externalFinanceAuthority=false};
        await using(var stream=new FileStream(P30ServiceCookieManifestPath,new FileStreamOptions{
            Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.Read,
            UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite}))
            await JsonSerializer.SerializeAsync(stream,manifest,new JsonSerializerOptions(JsonSerializerDefaults.Web),cancellationToken);
        Need(File.GetUnixFileMode(P30ServiceCookieManifestPath)==(UnixFileMode.UserRead|UnixFileMode.UserWrite),"Service credential manifest must be0600.");
        result["p30ServiceNormalLocalCookieControlPlane"]=new {status="LISTENING_CONTROL_PLANE_ONLY",
            syntheticCredentialsSeeded=16,syntheticAccountsActivated=16,realLocalSchemeSelected=true,
            privateSubjectHandlerAbsent=true,privateSubjectHeaderRejected401=true,controlPlaneHttpRequests=1,
            loopbackEphemeralListener=true,browserAcceptanceVerified=false,ownerCompletionVerified=false,
            productionTouched=false};
        progress("P30ServiceNormalCookieAwaitingProtectedBrowserCompletion");
        while(!File.Exists(P30ServiceCookieCompletionPath))
        {
            Need(DateTimeOffset.UtcNow<deadline,"Service private browser completion timed out; retain fixture and evidence.");
            await Task.Delay(TimeSpan.FromMilliseconds(500),cancellationToken);
        }
        Need(DateTimeOffset.UtcNow<=deadline&&new FileInfo(P30ServiceCookieCompletionPath).Length is >0 and <=4096
            &&(File.GetAttributes(P30ServiceCookieCompletionPath)&FileAttributes.ReparsePoint)==0
            &&File.GetUnixFileMode(P30ServiceCookieCompletionPath)==(UnixFileMode.UserRead|UnixFileMode.UserWrite),
            "Service browser completion must be new regular0600 private file.");
        using(var stream=new FileStream(P30ServiceCookieCompletionPath,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            Need(stream.Length is >0 and <=4096,"Bounded service browser completion required.");
            using var completion=await JsonDocument.ParseAsync(stream,cancellationToken:cancellationToken);
            var fields=completion.RootElement;
            Need(fields.ValueKind==JsonValueKind.Object&&fields.EnumerateObject().Count()==3
                &&fields.EnumerateObject().Select(x=>x.Name).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"schemaVersion","challenge","status"})
                &&fields.GetProperty("schemaVersion").GetInt32()==3&&fields.GetProperty("challenge").GetString()==challenge
                &&fields.GetProperty("status").GetString()=="BrowserFinished",
                "Exact challenged service lifecycle completion required; it does not establish browser acceptance.");
        }
        result["p30ServiceNormalLocalCookieControlPlane"]=new {status="CONTROL_PLANE_COMPLETED_BROWSER_NOT_VERIFIED",
            syntheticCredentialsSeeded=16,syntheticAccountsActivated=16,realLocalSchemeSelected=true,
            privateSubjectHandlerAbsent=true,privateSubjectHeaderRejected401=true,controlPlaneHttpRequests=1,
            protectedLifecycleCompletionReceived=true,completionIsAcceptance=false,
            browserAcceptanceVerified=false,ownerCompletionVerified=false,productionTouched=false};
    }
}
