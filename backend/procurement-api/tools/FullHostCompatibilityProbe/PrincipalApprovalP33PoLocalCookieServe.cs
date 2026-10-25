using System.Net;
using System.Net.Http.Json;
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
    private const string P33PoCookieManifestPath=LocalCookieEvidenceDirectory+"/p33-po-local-cookie-fixture.private.json";
    private const string P33PoCookieCompletionPath=LocalCookieEvidenceDirectory+"/p33-po-local-cookie-browser-completion.private.json";

    // This is a private transport control plane, not browser or owning-store acceptance.
    private static async Task ServeP33PoNormalLocalCookieAsync(PrincipalApprovalFixture fixture,string owner,
        PrincipalApprovalRuntimePrivileges.Connections roles,NpgsqlConnection ownerConnection,
        Guid approvedRequestId,Guid approvedRequestLineId,Guid supplierId,Guid issuedOrderId,
        Guid cancelledNativeEditOrderId,
        Dictionary<string,object?> result,Action<string> progress,CancellationToken cancellationToken=default)
    {
        progress("P33PoNormalCookiePrivatePreparation");
        Need(fixture.PrincipalPoProfile&&result.TryGetValue("principalOrderRevision",out var accepted)&&accepted is not null
            &&approvedRequestId!=Guid.Empty&&approvedRequestLineId!=Guid.Empty&&supplierId!=Guid.Empty
            &&issuedOrderId!=Guid.Empty&&cancelledNativeEditOrderId!=Guid.Empty,
            "P33 PO cookie fixture requires completed real P30 and P33 owner reload proof.");
        await RequirePrivateRawOwnerAsync(ownerConnection,PrincipalApprovalFixture.Tenant,fixture.Company);
        var parsedOwner=new NpgsqlConnectionStringBuilder(owner);
        Need(OperatingSystem.IsLinux()&&parsedOwner.Host=="/authority-identity-private-socket"
            &&parsedOwner.Database==ownerConnection.Database&&parsedOwner.Username=="postgres"
            &&System.Text.RegularExpressions.Regex.IsMatch(ownerConnection.Database,"^proc_p33_http_po_cookie_[a-z0-9_]{1,32}$"),
            "P33 PO cookie fixture requires the exact fresh private Unix-socket owner database.");
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
        Need(!File.Exists(P33PoCookieManifestPath)&&!Directory.Exists(P33PoCookieManifestPath)
            &&!File.Exists(P33PoCookieCompletionPath)&&!Directory.Exists(P33PoCookieCompletionPath),
            "P33 PO cookie fixture and completion paths must be new.");
        await using(var db=fixture.Procurement())
        {
            var line=await db.PurchaseRequestLines.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==approvedRequestLineId
                &&x.PurchaseRequestId==approvedRequestId,cancellationToken);
            Need(await db.PurchaseRequests.AnyAsync(x=>x.Id==approvedRequestId&&x.Status=="Approved",cancellationToken)
                &&line is not null&&line.ApprovedQuantity==2m
                &&line.AllocatedQuantity==0m&&line.OrderedQuantity==1m&&line.CancelledQuantity==0m
                &&line.ApprovedQuantity-line.AllocatedQuantity-line.OrderedQuantity-line.CancelledQuantity==1m
                &&await db.Suppliers.AnyAsync(x=>x.Id==supplierId&&x.Status=="Active",cancellationToken)
                &&await db.PurchaseOrders.AnyAsync(x=>x.Id==issuedOrderId&&x.Status=="Issued",cancellationToken)
                &&await db.PurchaseOrders.CountAsync(cancellationToken)==4
                &&await db.PurchaseOrders.AnyAsync(x=>x.Id==cancelledNativeEditOrderId&&x.Status=="Cancelled",cancellationToken)
                &&await db.PrincipalOrderDraftCommercialRevisions.CountAsync(x=>x.PurchaseOrderId==cancelledNativeEditOrderId,cancellationToken)==1,
                "P33 browser must start with real approved remaining source, active supplier and completed internal issued order.");
        }
        var credentials=new List<LocalCookieCredential>();
        await using(var core=fixture.Core())
        {
            var subjects=fixture.Subjects;
            var users=await core.LocalUsers.AsNoTracking().Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&subjects.Contains(x.Id)).ToArrayAsync(cancellationToken);
            Need(users.Length==10&&users.All(x=>x.IsActive&&!x.IsArchived&&x.WorkerId is null
                &&x.PasswordHash=="private-no-real-login"&&x.LockedUntil is null&&x.FailedLoginCount==0
                &&x.MustChangePassword&&x.PasswordChangedAt is null&&x.LastLoginAt is null),
                "Only ten original synthetic workerless PO accounts with unused placeholder credentials may be activated.");
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
                    "Exact new synthetic PO credential activation failed; do not overwrite or retry.");
                credentials.Add(new(user.Id,user.Email,password));
            }
        }
        result["p33PoNormalLocalCookieControlPlane"]=new {status="PREPARING_NOT_BROWSER_ACCEPTANCE",
            syntheticCredentialsSeeded=credentials.Count,syntheticAccountsActivated=credentials.Count,
            browserAcceptanceVerified=false,productionTouched=false};
        await using var host=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,
            actualSchema:true,privateManualEvidence:true,normalLocalAuthentication:true);
        host.PrepareEntryPointEnvironment();host.UseKestrel(options=>options.Listen(IPAddress.Loopback,0));host.StartServer();
        using(var registration=host.Services.CreateScope())
        {
            var configured=registration.ServiceProvider.GetRequiredService<ProcurementPrincipalOrderDraftOptions>();
            var actual=registration.ServiceProvider.GetRequiredService<IProcurementPrincipalOrderDraftReadiness>();
            Need(configured.Enabled&&configured.ExecutionDatabaseConfigured
                &&actual is ProcurementPrincipalOrderDraftReadiness&&await actual.IsReadyAsync(cancellationToken),
                "P33 PO normal host must use actual finite owned readiness, not a flag-only capability.");
            var edit=registration.ServiceProvider.GetRequiredService<PrincipalOrderRevisionOptions>();
            var editReady=registration.ServiceProvider.GetRequiredService<IPrincipalOrderRevisionReadiness>();
            // Revision readiness composes ProcurementApprovalTransaction, which requires
            // the scope established by authenticated ProcurementScopeMiddleware. A bare
            // DI scope cannot exercise it and is always an invalid negative control.
            Need(edit.Enabled&&editReady is ProcurementPrincipalOrderRevisionReadiness,
                "P33 normal host must register captured finite revision readiness.");
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
        var buyerCredential=credentials.Single(x=>x.PrincipalId==fixture.Buyer);
        // The product session cookie is Secure. This private control-plane
        // diagnostic replays that exact server-issued cookie only to the already
        // verified HTTP loopback listener; browser cookie transport is proven
        // independently through the HTTPS proxy.
        using(var preflight=new HttpClient(new HttpClientHandler {AllowAutoRedirect=false,UseCookies=false})
            {BaseAddress=new Uri(url),Timeout=TimeSpan.FromSeconds(90)})
        {
            using(var login=await preflight.PostAsJsonAsync("/api/hrm/auth/login",
                new {email=buyerCredential.Email,password=buyerCredential.Password},cancellationToken))
            {
                Need(login.StatusCode==HttpStatusCode.OK,"P33 normal preflight requires genuine local login.");
                Need(login.Headers.TryGetValues("Set-Cookie",out var setCookies),
                    "P33 preflight requires a server-issued local session cookie.");
                var sessions=setCookies!.Where(x=>x.StartsWith("hrm_session=",StringComparison.Ordinal)).ToArray();
                Need(sessions.Length==1,"P33 preflight requires exactly one genuine local session cookie.");
                var cookieValue=sessions[0]["hrm_session=".Length..];
                var separator=cookieValue.IndexOf(';');
                Need(separator>0,"P33 preflight requires bounded local cookie attributes.");
                var token=cookieValue[..separator];
                Need(System.Text.RegularExpressions.Regex.IsMatch(token,"^[A-Za-z0-9_-]{43}$"),
                    "P33 preflight received an invalid local session token.");
                preflight.DefaultRequestHeaders.Add("Cookie","hrm_session="+token);
            }
            try
            {
                using(var contextRequest=new HttpRequestMessage(HttpMethod.Get,"/api/procurement/v1/context"))
                {
                    contextRequest.Headers.Add("X-Shell-Entity",fixture.Company.ToString("D"));
                    contextRequest.Headers.Add("X-Shell-Location",fixture.Branch.ToString("D"));
                    using var context=await preflight.SendAsync(contextRequest,cancellationToken);
                    Need(context.StatusCode==HttpStatusCode.OK,"P33 preflight requires authenticated selected Core scope.");
                    using var scoped=await JsonDocument.ParseAsync(await context.Content.ReadAsStreamAsync(cancellationToken),
                        cancellationToken:cancellationToken);
                    Need(scoped.RootElement.GetProperty("tenantId").GetString()==PrincipalApprovalFixture.Tenant
                        &&scoped.RootElement.GetProperty("legalEntityId").GetGuid()==fixture.Company
                        &&scoped.RootElement.GetProperty("principalId").GetGuid()==fixture.Buyer
                        &&scoped.RootElement.GetProperty("workerId").ValueKind==JsonValueKind.Null,
                        "P33 preflight must retain the actual workerless buyer Core scope.");
                }
                using(var metadataRequest=new HttpRequestMessage(HttpMethod.Get,"/api/procurement/v1/meta"))
                {
                    metadataRequest.Headers.Add("X-Shell-Entity",fixture.Company.ToString("D"));
                    metadataRequest.Headers.Add("X-Shell-Location",fixture.Branch.ToString("D"));
                    using var metadata=await preflight.SendAsync(metadataRequest,cancellationToken);
                    Need(metadata.StatusCode==HttpStatusCode.OK,"P33 preflight requires actual scoped metadata.");
                    using var body=await JsonDocument.ParseAsync(await metadata.Content.ReadAsStreamAsync(cancellationToken),
                        cancellationToken:cancellationToken);
                    Need(body.RootElement.GetProperty("capabilities").GetProperty("principalDirectOrderDrafts").GetBoolean()
                        &&body.RootElement.GetProperty("capabilities").GetProperty("principalOrderDraftRevision").GetBoolean(),
                        "P33 normal host must use captured finite revision readiness under authenticated Core scope.");
                }
            }
            finally
            {
                using var logout=await preflight.PostAsync("/api/hrm/auth/logout",null,cancellationToken);
                Need(logout.StatusCode==HttpStatusCode.OK,"P33 preflight local session must be revoked before browser baseline.");
            }
            using var signedOut=await preflight.GetAsync("/api/hrm/auth/me",cancellationToken);
            Need(signedOut.StatusCode==HttpStatusCode.OK,"P33 preflight signed-out identity check failed.");
            using(var identity=await JsonDocument.ParseAsync(await signedOut.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken:cancellationToken))
                Need(identity.RootElement.GetProperty("authenticated").GetBoolean()==false,
                    "P33 preflight session must no longer authenticate before browser baseline.");
            using(var revokedRequest=new HttpRequestMessage(HttpMethod.Get,"/api/procurement/v1/context"))
            {
                revokedRequest.Headers.Add("X-Shell-Entity",fixture.Company.ToString("D"));
                revokedRequest.Headers.Add("X-Shell-Location",fixture.Branch.ToString("D"));
                using var revoked=await preflight.SendAsync(revokedRequest,cancellationToken);
                Need(revoked.StatusCode==HttpStatusCode.Unauthorized,
                    "P33 preflight cookie must not authorize Procurement after logout.");
            }
        }
        using(var anonymous=new HttpClient(new HttpClientHandler {AllowAutoRedirect=false,UseCookies=false}) {BaseAddress=new Uri(url),Timeout=TimeSpan.FromSeconds(15)})
        using(var request=new HttpRequestMessage(HttpMethod.Get,"/api/procurement/v1/context"))
        {
            request.Headers.Add("X-Private-P25-Subject",fixture.Buyer.ToString("D"));
            using var denied=await anonymous.SendAsync(request,cancellationToken);
            Need(denied.StatusCode==HttpStatusCode.Unauthorized,"Private subject header must not authenticate normal cookie host.");
        }
        var challenge=Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var deadline=DateTimeOffset.UtcNow.AddMinutes(15);
        var manifest=new {schemaVersion=4,controlPlaneOnly=true,apiUrl=url,database=ownerConnection.Database,
            tenantId=PrincipalApprovalFixture.Tenant,companyId=fixture.Company,branchId=fixture.Branch,
            buyerPrincipalId=fixture.Buyer,approvedRequestId,approvedRequestLineId,supplierId,issuedOrderId,
            cancelledNativeEditOrderId,
            syntheticCredentials=credentials,challenge,syntheticAccountsActivated=10,
            syntheticActivationMode="EXACT_NEW_PLACEHOLDER_CAS_ONLY",completionPath=P33PoCookieCompletionPath,
            expiresAt=deadline,secureCookieRequiresHttpsProxy=true,
            apiSetupAuthentication="PRIVATE_SUBJECT_TEST_AUTH_ONLY",
            browserAuthenticationRequired="NORMAL_LOCAL_PASSWORD_AND_SESSION_COOKIE",
            localCookieLoginVerified=false,browserAcceptanceVerified=false,
            nativeIdentityProviderVerified=false,externalFinanceAuthority=false};
        await using(var stream=new FileStream(P33PoCookieManifestPath,new FileStreamOptions{
            Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.Read,
            UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite}))
            await JsonSerializer.SerializeAsync(stream,manifest,new JsonSerializerOptions(JsonSerializerDefaults.Web),cancellationToken);
        Need(File.GetUnixFileMode(P33PoCookieManifestPath)==(UnixFileMode.UserRead|UnixFileMode.UserWrite),"P33 credential manifest must be0600.");
        result["p33PoNormalLocalCookieControlPlane"]=new {status="LISTENING_CONTROL_PLANE_ONLY",
            syntheticCredentialsSeeded=10,syntheticAccountsActivated=10,realLocalSchemeSelected=true,
            privateSubjectHandlerAbsent=true,privateSubjectHeaderRejected401=true,controlPlaneHttpRequests=7,
            authenticatedScopedRevisionReadinessVerified=true,preflightLocalSessionRevoked=true,
            loopbackEphemeralListener=true,browserAcceptanceVerified=false,ownerCompletionVerified=false,
            productionTouched=false};
        progress("P33PoNormalCookieAwaitingProtectedBrowserCompletion");
        while(!File.Exists(P33PoCookieCompletionPath))
        {
            Need(DateTimeOffset.UtcNow<deadline,"P33 private browser completion timed out; retain fixture and evidence.");
            await Task.Delay(TimeSpan.FromMilliseconds(500),cancellationToken);
        }
        Need(DateTimeOffset.UtcNow<=deadline&&new FileInfo(P33PoCookieCompletionPath).Length is >0 and <=4096
            &&(File.GetAttributes(P33PoCookieCompletionPath)&FileAttributes.ReparsePoint)==0
            &&File.GetUnixFileMode(P33PoCookieCompletionPath)==(UnixFileMode.UserRead|UnixFileMode.UserWrite),
            "P33 browser completion must be new regular0600 private file.");
        using(var stream=new FileStream(P33PoCookieCompletionPath,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            Need(stream.Length is >0 and <=4096,"Bounded P33 browser completion required.");
            using var completion=await JsonDocument.ParseAsync(stream,cancellationToken:cancellationToken);
            var fields=completion.RootElement;
            Need(fields.ValueKind==JsonValueKind.Object&&fields.EnumerateObject().Count()==3
                &&fields.EnumerateObject().Select(x=>x.Name).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"schemaVersion","challenge","status"})
                &&fields.GetProperty("schemaVersion").GetInt32()==4&&fields.GetProperty("challenge").GetString()==challenge
                &&fields.GetProperty("status").GetString()=="BrowserFinished",
                "Exact challenged PO lifecycle completion required; it does not establish browser acceptance.");
        }
        result["p33PoNormalLocalCookieControlPlane"]=new {status="CONTROL_PLANE_COMPLETED_BROWSER_NOT_VERIFIED",
            syntheticCredentialsSeeded=10,syntheticAccountsActivated=10,realLocalSchemeSelected=true,
            privateSubjectHandlerAbsent=true,privateSubjectHeaderRejected401=true,controlPlaneHttpRequests=7,
            authenticatedScopedRevisionReadinessVerified=true,preflightLocalSessionRevoked=true,
            protectedLifecycleCompletionReceived=true,completionIsAcceptance=false,
            browserAcceptanceVerified=false,ownerCompletionVerified=false,productionTouched=false};
    }
}
