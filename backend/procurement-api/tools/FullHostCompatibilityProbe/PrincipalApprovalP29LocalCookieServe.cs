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
    private const string P29CookieManifestPath=LocalCookieEvidenceDirectory+"/p29-local-cookie-fixture.private.json";
    private const string P29CookieCompletionPath=LocalCookieEvidenceDirectory+"/p29-local-cookie-browser-completion.private.json";

    // Future ROOT-owned opt-in hook after the genuine original base43 graph.
    // This owns only a private control plane. A completion file is NOT browser acceptance.
    private static async Task ServeP29NormalLocalCookieAsync(PrincipalApprovalFixture fixture,string owner,
        PrincipalApprovalRuntimePrivileges.Connections roles,NpgsqlConnection ownerConnection,
        Guid approvedRequestId,Guid approvedRequestLineId,Guid cancelledDraftEventId,
        Dictionary<string,object?> result,Action<string> progress,CancellationToken cancellationToken=default)
    {
        progress("P29NormalLocalCookiePrivatePreparation");
        Need(fixture.PrincipalRfqProfile&&result.TryGetValue("principalRfq",out var accepted)&&accepted is not null
            &&result.TryGetValue("principalRfqStorage",out var storage)&&storage is not null
            &&approvedRequestId!=Guid.Empty&&approvedRequestLineId!=Guid.Empty&&cancelledDraftEventId!=Guid.Empty,
            "P29 normal-cookie fixture requires completed actual HTTP and owner-only source proof.");
        await RequirePrivateRawOwnerAsync(ownerConnection,PrincipalApprovalFixture.Tenant,fixture.Company);
        var parsedOwner=new NpgsqlConnectionStringBuilder(owner);
        Need(OperatingSystem.IsLinux()&&parsedOwner.Host=="/authority-identity-private-socket"
            &&parsedOwner.Database==ownerConnection.Database&&parsedOwner.Username=="postgres",
            "Normal cookie fixture requires the exact same private Linux Unix socket owner database.");
        foreach(var finite in new[]{roles.Record,roles.Execution})
        {
            var parsed=new NpgsqlConnectionStringBuilder(finite);
            Need(parsed.Host==parsedOwner.Host&&parsed.Port==parsedOwner.Port&&parsed.Database==parsedOwner.Database,
                "Normal cookie finite contexts must remain on the same actual private store.");
        }
        await using(var identity=new NpgsqlCommand("SELECT current_setting('server_version_num')::integer BETWEEN 170000 AND 179999 AND current_setting('listen_addresses')=''",ownerConnection))
            Need(await identity.ExecuteScalarAsync(cancellationToken) is true,"Normal cookie fixture requires private PostgreSQL17 without TCP.");
        Need(Directory.Exists(LocalCookieEvidenceDirectory)
            &&(File.GetAttributes(LocalCookieEvidenceDirectory)&FileAttributes.ReparsePoint)==0
            &&File.GetUnixFileMode(LocalCookieEvidenceDirectory)==(UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute),
            "Exact private0700 evidence mount required.");
        Need(!File.Exists(P29CookieManifestPath)&&!Directory.Exists(P29CookieManifestPath)
            &&!File.Exists(P29CookieCompletionPath)&&!Directory.Exists(P29CookieCompletionPath),
            "P29 cookie control paths must be new; never overwrite an earlier fixture.");
        await using(var db=fixture.Procurement())
        await using(var workflow=fixture.Approvals())
        {
            Need(await db.PurchaseRequests.CountAsync(cancellationToken)==2
                &&await db.ApprovalSubmissions.CountAsync(cancellationToken)==1
                &&await db.ApprovalCommandReceipts.CountAsync(cancellationToken)==2
                &&await workflow.Decisions.CountAsync(cancellationToken)==1,
                "P29 cookie serve requires the original negative and actual approved graph.");
            Need(await db.SpendingAuthorityGrants.CountAsync(x=>x.State=="Active",cancellationToken)==1,
                "Normal cookie browser prerequisite requires the genuine independently activated finite authority.");
            Need(await db.PurchaseRequests.AnyAsync(x=>x.Id==approvedRequestId&&x.Status=="Approved",cancellationToken)
                &&await db.PurchaseRequestLines.AnyAsync(x=>x.Id==approvedRequestLineId&&x.PurchaseRequestId==approvedRequestId,cancellationToken)
                &&await db.SourcingEvents.AnyAsync(x=>x.Id==cancelledDraftEventId&&x.Status=="Cancelled"&&x.ExecutionVersion==2,cancellationToken)
                &&await db.SourcingDraftEvidenceRecords.AnyAsync(x=>x.SourcingEventId==cancelledDraftEventId&&x.SchemaVersion==2,cancellationToken)
                &&await db.SourcingCommandReceipts.CountAsync(x=>x.SourcingEventId==cancelledDraftEventId,cancellationToken)==2,
                "P29 cookie fixture must bind the genuine approved request and completed principal draft.");
        }
        var credentials=new List<LocalCookieCredential>();
        await using(var core=fixture.Core())
        {
            var subjects=fixture.Subjects;
            var users=await core.LocalUsers.AsNoTracking().Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&subjects.Contains(x.Id)).ToArrayAsync(cancellationToken);
            Need(users.Length==7&&users.All(x=>x.IsActive&&!x.IsArchived&&x.WorkerId is null
                &&x.PasswordHash=="private-no-real-login"&&x.LockedUntil is null&&x.FailedLoginCount==0
                &&x.MustChangePassword&&x.PasswordChangedAt is null&&x.LastLoginAt is null),
                "Only seven original synthetic workerless P29 accounts with unused placeholder credentials may be activated.");
            var activatedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            foreach(var user in users.OrderBy(x=>x.Id))
            {
                // Exact currently owned LocalPasswordHash format; its internal class
                // is not made public. Actual login must verify this production format.
                var password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))+"aA1!";
                var salt=RandomNumberGenerator.GetBytes(16);
                var key=Rfc2898DeriveBytes.Pbkdf2(password,salt,210_000,HashAlgorithmName.SHA256,32);
                var encoded="pbkdf2-sha256$210000$"+Convert.ToBase64String(salt)+"$"+Convert.ToBase64String(key);
                Need(await core.LocalUsers.Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==user.Id
                    &&x.PasswordHash=="private-no-real-login"&&x.IsActive&&!x.IsArchived&&x.WorkerId==null
                    &&x.LockedUntil==null&&x.FailedLoginCount==0&&x.MustChangePassword
                    &&x.PasswordChangedAt==null&&x.LastLoginAt==null)
                    .ExecuteUpdateAsync(set=>set.SetProperty(x=>x.PasswordHash,encoded)
                        .SetProperty(x=>x.MustChangePassword,false)
                        .SetProperty(x=>x.PasswordChangedAt,(DateTimeOffset?)activatedAt),cancellationToken)==1,
                    "Exact new synthetic credential activation failed; retain fixture without retry or overwrite.");
                credentials.Add(new(user.Id,user.Email,password));
            }
        }
        result["p29NormalLocalCookieControlPlane"]=new {status="PREPARING_NOT_BROWSER_ACCEPTANCE",syntheticCredentialsSeeded=credentials.Count,
            syntheticAccountsActivated=credentials.Count,syntheticActivationMode="EXACT_NEW_PLACEHOLDER_CAS_ONLY",
            nativeIdentityProviderVerified=false,localCookieLoginVerified=false,browserAcceptanceVerified=false,productionTouched=false};
        // Separate factory: the completed API setup above used private test auth.
        // Program still owns all normal local authentication/session endpoints.
        await using var host=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,
            actualSchema:true,privateManualEvidence:true,normalLocalAuthentication:true);
        host.PrepareEntryPointEnvironment();
        host.UseKestrel(options=>options.Listen(IPAddress.Loopback,0));
        host.StartServer();
        using(var registration=host.Services.CreateScope())
        {
            var configured=registration.ServiceProvider.GetRequiredService<ProcurementSourcingDraftExecutionOptions>();
            var actual=registration.ServiceProvider.GetRequiredService<IProcurementSourcingDraftSchemaReadiness>();
            Need(configured.Enabled&&configured.PrincipalSourceEnabled&&configured.ExecutionDatabaseConfigured
                &&actual is ProcurementSourcingDraftSchemaReadiness&&await actual.IsReadyAsync(cancellationToken),
                "P29 normal host must use the actual configured finite sourcing store, not a flag-only capability.");
        }
        var schemes=host.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var all=await schemes.GetAllSchemesAsync();
        Need((await schemes.GetDefaultAuthenticateSchemeAsync())?.Name=="local"
            &&(await schemes.GetDefaultChallengeSchemeAsync())?.Name=="local"
            &&all.Any(x=>x.Name=="local"&&x.HandlerType.Name=="LocalAuthenticationHandler")
            &&all.All(x=>x.Name!="private-p25-subject")
            &&host.Services.GetService<PrincipalApprovalSubjects>() is null,
            "Normal cookie host must retain real local authentication without a private subject handler.");
        var addresses=host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.ToArray();
        Need(addresses is {Length:1}&&Uri.TryCreate(addresses[0],UriKind.Absolute,out var address)
            &&address.Scheme=="http"&&address.Host=="127.0.0.1"&&address.Port>0,
            "Only one ephemeral private loopback HTTP listener is supported.");
        var url=addresses![0];
        // Real network Kestrel probe, no WAF/TestServer cookie injection. This is
        // counted separately from the completed setup's actual HTTP count.
        using(var anonymous=new HttpClient(new HttpClientHandler {AllowAutoRedirect=false,UseCookies=false}) {BaseAddress=new Uri(url),Timeout=TimeSpan.FromSeconds(15)})
        using(var request=new HttpRequestMessage(HttpMethod.Get,"/api/procurement/v1/context"))
        {
            request.Headers.Add("X-Private-P25-Subject",fixture.Maker.ToString("D"));
            using var denied=await anonymous.SendAsync(request,cancellationToken);
            Need(denied.StatusCode==HttpStatusCode.Unauthorized,"Private subject header must NOT authenticate the normal cookie host.");
        }
        var challenge=Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var deadline=DateTimeOffset.UtcNow.AddMinutes(15);
        var manifest=new {schemaVersion=2,controlPlaneOnly=true,apiUrl=url,database=ownerConnection.Database,
            tenantId=PrincipalApprovalFixture.Tenant,companyId=fixture.Company,branchId=fixture.Branch,
            buyerPrincipalId=fixture.Buyer,approvedRequestId,approvedRequestLineId,cancelledDraftEventId,
            syntheticCredentials=credentials,challenge,
            syntheticAccountsActivated=7,syntheticActivationMode="EXACT_NEW_PLACEHOLDER_CAS_ONLY",
            mustChangePasswordFrom=true,mustChangePasswordTo=false,passwordChangedAtNormalisedUtc=true,
            completionPath=P29CookieCompletionPath,expiresAt=deadline,
            secureCookieRequiresHttpsProxy=true,apiSetupAuthentication="PRIVATE_SUBJECT_TEST_AUTH_ONLY",
            browserAuthenticationRequired="NORMAL_LOCAL_PASSWORD_AND_SESSION_COOKIE",
            localCookieLoginVerified=false,browserAcceptanceVerified=false,nativeIdentityProviderVerified=false,externalFinanceAuthority=false};
        await using(var stream=new FileStream(P29CookieManifestPath,new FileStreamOptions {
            Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.Read,
            UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite}))
            await JsonSerializer.SerializeAsync(stream,manifest,new JsonSerializerOptions(JsonSerializerDefaults.Web),cancellationToken);
        Need(File.GetUnixFileMode(P29CookieManifestPath)==(UnixFileMode.UserRead|UnixFileMode.UserWrite),"Synthetic credentials manifest must be0600.");
        result["p29NormalLocalCookieControlPlane"]=new {status="LISTENING_CONTROL_PLANE_ONLY",syntheticCredentialsSeeded=7,
            syntheticAccountsActivated=7,syntheticActivationMode="EXACT_NEW_PLACEHOLDER_CAS_ONLY",
            realLocalSchemeSelected=true,privateSubjectHandlerAbsent=true,privateSubjectHeaderRejected401=true,controlPlaneHttpRequests=1,
            loopbackEphemeralListener=true,principalRfqDraftOnly=true,localCookieLoginVerified=false,
            browserAcceptanceVerified=false,ownerCompletionVerified=false,nativeIdentityProviderVerified=false,externalFinanceAuthority=false,productionTouched=false};
        progress("P29NormalLocalCookieAwaitingProtectedBrowserCompletion");
        while(!File.Exists(P29CookieCompletionPath))
        {
            Need(DateTimeOffset.UtcNow<deadline,"Normal cookie private browser completion timed out; retain fixture and evidence.");
            await Task.Delay(TimeSpan.FromMilliseconds(500),cancellationToken);
        }
        Need(DateTimeOffset.UtcNow<=deadline&&new FileInfo(P29CookieCompletionPath).Length is >0 and <=4096
            &&(File.GetAttributes(P29CookieCompletionPath)&FileAttributes.ReparsePoint)==0
            &&File.GetUnixFileMode(P29CookieCompletionPath)==(UnixFileMode.UserRead|UnixFileMode.UserWrite),
            "Browser completion must be a new regular0600 private file.");
        using(var completionStream=new FileStream(P29CookieCompletionPath,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            Need(completionStream.Length is >0 and <=4096,"Bounded browser completion required.");
            using var completion=await JsonDocument.ParseAsync(completionStream,cancellationToken:cancellationToken);
            var fields=completion.RootElement;
            Need(fields.ValueKind==JsonValueKind.Object&&fields.EnumerateObject().Count()==3
                &&fields.EnumerateObject().Select(x=>x.Name).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"schemaVersion","challenge","status"})
                &&fields.GetProperty("schemaVersion").GetInt32()==2&&fields.GetProperty("challenge").GetString()==challenge
                &&fields.GetProperty("status").GetString()=="BrowserFinished",
                "Exact challenged lifecycle completion required; file does not establish browser test acceptance.");
        }
        result["p29NormalLocalCookieControlPlane"]=new {status="CONTROL_PLANE_COMPLETED_BROWSER_NOT_VERIFIED",syntheticCredentialsSeeded=7,
            syntheticAccountsActivated=7,syntheticActivationMode="EXACT_NEW_PLACEHOLDER_CAS_ONLY",
            realLocalSchemeSelected=true,privateSubjectHandlerAbsent=true,privateSubjectHeaderRejected401=true,controlPlaneHttpRequests=1,
            loopbackEphemeralListener=true,protectedLifecycleCompletionReceived=true,completionIsAcceptance=false,principalRfqDraftOnly=true,
            localCookieLoginVerified=false,browserAcceptanceVerified=false,ownerCompletionVerified=false,
            nativeIdentityProviderVerified=false,externalFinanceAuthority=false,productionTouched=false};
        progress("P29NormalLocalCookieControlPlaneClosing");
        // await using disposes factory/Kestrel on success, timeout or any exception.
        // Root must separately bind actual PW exit and independent owning facts.
    }
}
