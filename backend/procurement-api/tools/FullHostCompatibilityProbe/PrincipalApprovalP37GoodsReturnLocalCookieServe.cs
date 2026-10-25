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

internal static partial class PrincipalApprovalHttpVerification
{
    private const string P37ReturnCookieManifestPath=LocalCookieEvidenceDirectory+"/p37-return-local-cookie-fixture.private.json";
    private const string P37ReturnCookieCompletionPath=LocalCookieEvidenceDirectory+"/p37-return-local-cookie-browser-completion.private.json";

    // Probe-only transport. Browser and independent owner evidence must be checked separately.
    private static async Task ServeP37GoodsReturnNormalLocalCookieAsync(PrincipalApprovalFixture fixture,string owner,
        PrincipalApprovalRuntimePrivileges.Connections roles,NpgsqlConnection ownerConnection,
        P37GoodsReturnEvidence native,Dictionary<string,object?> result,Action<string> progress,
        CancellationToken cancellationToken=default)
    {
        progress("P37ReturnNormalCookiePrivatePreparation");
        Need(fixture.PrincipalGoodsReturnProfile&&fixture.PrincipalReceivingProfile&&fixture.PrincipalPoProfile
            &&result.TryGetValue("principalGoodsReturn",out var accepted)&&accepted is not null
            &&native.AcceptanceFactId!=Guid.Empty&&native.NativeDraftId!=Guid.Empty&&native.ReceiptId!=Guid.Empty,
            "P37 cookie fixture requires completed native return and a real accepted-Goods fact.");
        await RequirePrivateRawOwnerAsync(ownerConnection,PrincipalApprovalFixture.Tenant,fixture.Company);
        var parsedOwner=new NpgsqlConnectionStringBuilder(owner);
        Need(OperatingSystem.IsLinux()&&parsedOwner.Host=="/authority-identity-private-socket"
            &&parsedOwner.Database==ownerConnection.Database&&parsedOwner.Username=="postgres"
            &&System.Text.RegularExpressions.Regex.IsMatch(ownerConnection.Database,"^proc_p37_http_return_[a-z0-9_]{1,30}$"),
            "P37 cookie fixture requires the exact fresh private Unix-socket owner database.");
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
        Need(!File.Exists(P37ReturnCookieManifestPath)&&!Directory.Exists(P37ReturnCookieManifestPath)
            &&!File.Exists(P37ReturnCookieCompletionPath)&&!Directory.Exists(P37ReturnCookieCompletionPath),
            "P37 fixture and completion paths must be new.");
        await using(var db=fixture.Procurement())
        {
            var fact=await db.PrincipalReceivingFacts.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==native.AcceptanceFactId,cancellationToken);
            var state=fact is null?null:await db.PrincipalReceivingSourceStates.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.Id==fact.SourceStateId,cancellationToken);
            var nativeDraft=await db.PrincipalGoodsReturnDrafts.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.Id==native.NativeDraftId,cancellationToken);
            Need(fact is not null&&state is not null&&nativeDraft is not null
                &&fact.Kind=="Acceptance"&&fact.Operation=="Accept"&&fact.Quantity==0.9999m
                &&fact.DraftId==native.ReceiptId&&fact.BranchId==fixture.Branch
                &&state.Kind=="Goods"&&state.BranchId==fixture.Branch
                &&nativeDraft.AcceptanceFactId==fact.Id&&nativeDraft.ReceiptId==native.ReceiptId
                &&nativeDraft.CreatorPrincipalId==fixture.Receiver.ToString("D")
                &&nativeDraft.State=="Cancelled"&&nativeDraft.Revision==3
                &&await db.PrincipalGoodsReturnDrafts.CountAsync(cancellationToken)==1
                &&await db.PrincipalGoodsReturnRevisions.CountAsync(cancellationToken)==3
                &&await db.PrincipalGoodsReturnCommandReceipts.CountAsync(cancellationToken)==3,
                "P37 browser must start from the exact retained Goods acceptance and completed native draft.");
        }
        var credentials=new List<LocalCookieCredential>();
        await using(var core=fixture.Core())
        {
            var subjects=fixture.Subjects;
            var users=await core.LocalUsers.AsNoTracking().Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&subjects.Contains(x.Id))
                .ToArrayAsync(cancellationToken);
            Need(users.Length==16&&users.All(x=>x.IsActive&&!x.IsArchived&&x.WorkerId is null
                &&x.PasswordHash=="private-no-real-login"&&x.LockedUntil is null&&x.FailedLoginCount==0
                &&x.MustChangePassword&&x.PasswordChangedAt is null&&x.LastLoginAt is null),
                "Only sixteen original synthetic workerless accounts with unused placeholder credentials may be activated.");
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
                    "Exact new synthetic credential activation failed; do not overwrite or retry.");
                credentials.Add(new(user.Id,user.Email,password));
            }
        }
        result["p37ReturnNormalLocalCookieControlPlane"]=new {status="PREPARING_NOT_BROWSER_ACCEPTANCE",
            syntheticCredentialsSeeded=credentials.Count,syntheticAccountsActivated=credentials.Count,
            browserAcceptanceVerified=false,productionTouched=false};
        await using var host=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,
            actualSchema:true,privateManualEvidence:true,normalLocalAuthentication:true);
        host.PrepareEntryPointEnvironment();host.UseKestrel(options=>options.Listen(IPAddress.Loopback,0));host.StartServer();
        using(var registration=host.Services.CreateScope())
        {
            var configured=registration.ServiceProvider.GetRequiredService<ProcurementPrincipalGoodsReturnOptions>();
            var actual=registration.ServiceProvider.GetRequiredService<IProcurementPrincipalGoodsReturnReadiness>();
            // This readiness requires request-scoped Core/branch evidence; do not call it from a root DI scope.
            Need(configured.Enabled&&configured.ExecutionDatabaseConfigured
                &&actual is ProcurementPrincipalGoodsReturnStoreReadiness,
                "P37 normal host must register its concrete owner readiness; authenticated HTTP verifies scoped admission.");
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
        using(var anonymous=new HttpClient(new HttpClientHandler {AllowAutoRedirect=false,UseCookies=false})
            {BaseAddress=new Uri(url),Timeout=TimeSpan.FromSeconds(15)})
        using(var request=new HttpRequestMessage(HttpMethod.Get,"/api/procurement/v1/context"))
        {
            request.Headers.Add("X-Private-P25-Subject",fixture.Receiver.ToString("D"));
            using var denied=await anonymous.SendAsync(request,cancellationToken);
            Need(denied.StatusCode==HttpStatusCode.Unauthorized,
                "Private subject header must not authenticate normal cookie host.");
        }
        var challenge=Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var deadline=DateTimeOffset.UtcNow.AddMinutes(15);
        var manifest=new {schemaVersion=4,controlPlaneOnly=true,apiUrl=url,database=ownerConnection.Database,
            tenantId=PrincipalApprovalFixture.Tenant,companyId=fixture.Company,branchId=fixture.Branch,
            receiverPrincipalId=fixture.Receiver,acceptanceFactId=native.AcceptanceFactId,
            receiptId=native.ReceiptId,nativeDraftId=native.NativeDraftId,
            acceptedFactQuantityExact="0.9999",syntheticCredentials=credentials,challenge,
            syntheticAccountsActivated=16,syntheticActivationMode="EXACT_NEW_PLACEHOLDER_CAS_ONLY",
            completionPath=P37ReturnCookieCompletionPath,expiresAt=deadline,secureCookieRequiresHttpsProxy=true,
            apiSetupAuthentication="PRIVATE_SUBJECT_TEST_AUTH_ONLY",
            browserAuthenticationRequired="NORMAL_LOCAL_PASSWORD_AND_SESSION_COOKIE",
            localCookieLoginVerified=false,browserAcceptanceVerified=false,ownerCompletionVerified=false,
            nativeIdentityProviderVerified=false,externalFinanceAuthority=false,productionTouched=false};
        await using(var stream=new FileStream(P37ReturnCookieManifestPath,new FileStreamOptions{
            Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.Read,
            UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite}))
            await JsonSerializer.SerializeAsync(stream,manifest,new JsonSerializerOptions(JsonSerializerDefaults.Web),cancellationToken);
        Need(File.GetUnixFileMode(P37ReturnCookieManifestPath)==(UnixFileMode.UserRead|UnixFileMode.UserWrite),
            "P37 credential manifest must be0600.");
        result["p37ReturnNormalLocalCookieControlPlane"]=new {status="LISTENING_CONTROL_PLANE_ONLY",
            syntheticCredentialsSeeded=16,syntheticAccountsActivated=16,realLocalSchemeSelected=true,
            privateSubjectHandlerAbsent=true,privateSubjectHeaderRejected401=true,controlPlaneHttpRequests=1,
            loopbackEphemeralListener=true,browserAcceptanceVerified=false,ownerCompletionVerified=false,
            productionTouched=false};
        progress("P37ReturnNormalCookieAwaitingProtectedBrowserCompletion");
        while(!File.Exists(P37ReturnCookieCompletionPath))
        {
            Need(DateTimeOffset.UtcNow<deadline,"P37 private browser completion timed out; retain fixture and evidence.");
            await Task.Delay(TimeSpan.FromMilliseconds(500),cancellationToken);
        }
        Need(DateTimeOffset.UtcNow<=deadline&&new FileInfo(P37ReturnCookieCompletionPath).Length is >0 and <=4096
            &&(File.GetAttributes(P37ReturnCookieCompletionPath)&FileAttributes.ReparsePoint)==0
            &&File.GetUnixFileMode(P37ReturnCookieCompletionPath)==(UnixFileMode.UserRead|UnixFileMode.UserWrite),
            "P37 browser completion must be new regular0600 private file.");
        using(var stream=new FileStream(P37ReturnCookieCompletionPath,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            Need(stream.Length is >0 and <=4096,"Bounded P37 browser completion required.");
            using var completion=await JsonDocument.ParseAsync(stream,cancellationToken:cancellationToken);
            var fields=completion.RootElement;
            Need(fields.ValueKind==JsonValueKind.Object&&fields.EnumerateObject().Count()==3
                &&fields.EnumerateObject().Select(x=>x.Name).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"schemaVersion","challenge","status"})
                &&fields.GetProperty("schemaVersion").GetInt32()==4&&fields.GetProperty("challenge").GetString()==challenge
                &&fields.GetProperty("status").GetString()=="BrowserFinished",
                "Exact challenged P37 lifecycle completion required; it does not establish browser acceptance.");
        }
        result["p37ReturnNormalLocalCookieControlPlane"]=new {status="CONTROL_PLANE_COMPLETED_BROWSER_NOT_VERIFIED",
            syntheticCredentialsSeeded=16,syntheticAccountsActivated=16,realLocalSchemeSelected=true,
            privateSubjectHandlerAbsent=true,privateSubjectHeaderRejected401=true,controlPlaneHttpRequests=1,
            protectedLifecycleCompletionReceived=true,completionIsAcceptance=false,
            browserAcceptanceVerified=false,ownerCompletionVerified=false,productionTouched=false};
    }
}
