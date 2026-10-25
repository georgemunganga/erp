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
    private const string P35CookieManifestPath=LocalCookieEvidenceDirectory+"/p35-contract-review-cookie-fixture.private.json";
    private const string P35CookieCompletionPath=LocalCookieEvidenceDirectory+"/p35-contract-review-cookie-completion.private.json";

    // Tool-only control plane. A separate browser and read-only owner verifier decide acceptance.
    private static async Task ServeP35ContractReviewNormalLocalCookieAsync(PrincipalApprovalFixture fixture,
        string owner,PrincipalApprovalRuntimePrivileges.Connections roles,NpgsqlConnection ownerConnection,
        P35ContractReviewEvidence native,Dictionary<string,object?> result,Action<string> progress,
        CancellationToken cancellationToken=default)
    {
        progress("P35ContractReviewNormalCookiePrivatePreparation");
        Need(fixture.PrincipalPoProfile&&fixture.PrincipalContractReviewProfile
            &&result.TryGetValue("principalContractReview",out var accepted)&&accepted is not null
            &&native.ContractId!=Guid.Empty&&native.SupplierId!=Guid.Empty&&native.PolicyId!=Guid.Empty
            &&native.FirstSubmissionId!=Guid.Empty&&native.SecondSubmissionId!=Guid.Empty,
            "P35 cookie requires its completed native internal-review graph.");
        await RequirePrivateRawOwnerAsync(ownerConnection,PrincipalApprovalFixture.Tenant,fixture.Company);
        var parsedOwner=new NpgsqlConnectionStringBuilder(owner);
        Need(OperatingSystem.IsLinux()&&parsedOwner.Host=="/authority-identity-private-socket"
            &&parsedOwner.Database==ownerConnection.Database&&parsedOwner.Username=="postgres"
            &&System.Text.RegularExpressions.Regex.IsMatch(ownerConnection.Database,
                "^proc_p35_http_contract_review_[a-z0-9_]{1,24}$"),
            "P35 cookie requires the exact fresh private Unix-socket owner database.");
        foreach(var finite in new[]{roles.Record,roles.Execution})
        {
            var parsed=new NpgsqlConnectionStringBuilder(finite);
            Need(parsed.Host==parsedOwner.Host&&parsed.Port==parsedOwner.Port&&parsed.Database==parsedOwner.Database,
                "P35 normal-cookie finite contexts must remain on the same private store.");
        }
        await using(var identity=new NpgsqlCommand(
            "SELECT current_setting('server_version_num')::integer BETWEEN 170000 AND 179999 AND current_setting('listen_addresses')=''",
            ownerConnection))
            Need(await identity.ExecuteScalarAsync(cancellationToken) is true,
                "Private PostgreSQL17 without TCP required.");
        Need(Directory.Exists(LocalCookieEvidenceDirectory)
            &&(File.GetAttributes(LocalCookieEvidenceDirectory)&FileAttributes.ReparsePoint)==0
            &&File.GetUnixFileMode(LocalCookieEvidenceDirectory)==(UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute),
            "Exact private0700 evidence mount required.");
        Need(!File.Exists(P35CookieManifestPath)&&!Directory.Exists(P35CookieManifestPath)
            &&!File.Exists(P35CookieCompletionPath)&&!Directory.Exists(P35CookieCompletionPath),
            "P35 fixture and completion paths must be new.");
        await using(var db=fixture.Procurement())
        {
            var contract=await db.SupplierContracts.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==native.ContractId,cancellationToken);
            var submissions=await db.ContractReviewSubmissions.AsNoTracking()
                .Where(x=>x.ContractId==native.ContractId).OrderBy(x=>x.SubmissionNumber).ToArrayAsync(cancellationToken);
            var votes=await db.ContractReviewVotes.AsNoTracking()
                .Where(x=>x.ContractId==native.ContractId).ToArrayAsync(cancellationToken);
            var receipts=await db.ContractReviewCommandReceipts.AsNoTracking()
                .Where(x=>x.ContractId==native.ContractId).ToArrayAsync(cancellationToken);
            Need(contract is not null&&contract.SupplierId==native.SupplierId&&contract.Status=="Approved"
                &&submissions.Length==2&&submissions[0].Id==native.FirstSubmissionId
                &&submissions[1].Id==native.SecondSubmissionId&&votes.Length==2&&receipts.Length==5
                &&receipts.Any(x=>x.CommandKey==native.FirstSubmitKey)
                &&receipts.Any(x=>x.CommandKey==native.CorrectionKey)
                &&receipts.Any(x=>x.CommandKey==native.SecondSubmitKey),
                "P35 browser must start from the exact retained approved-internal native graph.");
        }
        var credentials=new List<LocalCookieCredential>();
        await using(var core=fixture.Core())
        {
            var subjects=fixture.Subjects;
            Need(subjects.Length==10&&subjects.Distinct().Count()==10,
                "P35 native profile must retain exactly ten synthetic Core subjects.");
            var users=await core.LocalUsers.AsNoTracking()
                .Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&subjects.Contains(x.Id))
                .ToArrayAsync(cancellationToken);
            Need(users.Length==10&&users.All(x=>x.IsActive&&!x.IsArchived&&x.WorkerId is null
                &&x.PasswordHash=="private-no-real-login"&&x.LockedUntil is null&&x.FailedLoginCount==0
                &&x.MustChangePassword&&x.PasswordChangedAt is null&&x.LastLoginAt is null),
                "Only ten original unused synthetic workerless accounts may be activated.");
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
                    "Exact P35 synthetic credential activation failed; do not overwrite or retry.");
                credentials.Add(new(user.Id,user.Email,password));
            }
        }
        result["p35ContractReviewNormalCookieControlPlane"]=new {status="PREPARING_NOT_BROWSER_ACCEPTANCE",
            syntheticCredentialsSeeded=credentials.Count,syntheticAccountsActivated=credentials.Count,
            browserAcceptanceVerified=false,productionTouched=false};
        await using var host=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,
            actualSchema:true,privateManualEvidence:true,normalLocalAuthentication:true);
        host.PrepareEntryPointEnvironment();host.UseKestrel(options=>options.Listen(IPAddress.Loopback,0));host.StartServer();
        using(var registration=host.Services.CreateScope())
        {
            var configured=registration.ServiceProvider.GetRequiredService<ProcurementContractReviewOptions>();
            var actual=registration.ServiceProvider.GetRequiredService<IProcurementContractReviewSchemaReadiness>();
            Need(configured.Enabled&&configured.ExecutionDatabaseConfigured
                &&actual is ProcurementContractReviewStoreReadiness,
                "P35 normal host must register concrete review readiness; authenticated HTTP verifies scoped admission.");
        }
        var schemes=host.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var all=await schemes.GetAllSchemesAsync();
        Need((await schemes.GetDefaultAuthenticateSchemeAsync())?.Name=="local"
            &&(await schemes.GetDefaultChallengeSchemeAsync())?.Name=="local"
            &&all.Any(x=>x.Name=="local"&&x.HandlerType.Name=="LocalAuthenticationHandler")
            &&all.All(x=>x.Name!="private-p25-subject")
            &&host.Services.GetService<PrincipalApprovalSubjects>() is null,
            "Normal P35 cookie host must keep real local authentication without a private subject handler.");
        var addresses=host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.ToArray();
        Need(addresses is {Length:1}&&Uri.TryCreate(addresses[0],UriKind.Absolute,out var address)
            &&address.Scheme=="http"&&address.Host=="127.0.0.1"&&address.Port>0,
            "Only one ephemeral private loopback listener is supported.");
        var url=addresses![0];
        using(var anonymous=new HttpClient(new HttpClientHandler {AllowAutoRedirect=false,UseCookies=false})
            {BaseAddress=new Uri(url),Timeout=TimeSpan.FromSeconds(15)})
        using(var request=new HttpRequestMessage(HttpMethod.Get,"/api/procurement/v1/context"))
        {
            request.Headers.Add("X-Private-P25-Subject",fixture.Buyer.ToString("D"));
            using var denied=await anonymous.SendAsync(request,cancellationToken);
            Need(denied.StatusCode==HttpStatusCode.Unauthorized,
                "Private subject header must not authenticate normal P35 cookie host.");
        }
        var challenge=Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var deadline=DateTimeOffset.UtcNow.AddMinutes(15);
        var manifest=new {schemaVersion=1,controlPlaneOnly=true,apiUrl=url,database=ownerConnection.Database,
            tenantId=PrincipalApprovalFixture.Tenant,companyId=fixture.Company,branchId=fixture.Branch,
            buyerPrincipalId=fixture.Buyer,reviewerPrincipalId=fixture.Approver,
            configMakerPrincipalId=fixture.ConfigMaker,configReviewerPrincipalId=fixture.ConfigReviewer,
            supplierId=native.SupplierId,nativeContractId=native.ContractId,policyId=native.PolicyId,
            firstSubmissionId=native.FirstSubmissionId,secondSubmissionId=native.SecondSubmissionId,
            firstSubmitKey=native.FirstSubmitKey,correctionKey=native.CorrectionKey,
            secondSubmitKey=native.SecondSubmitKey,syntheticCredentials=credentials,challenge,
            syntheticAccountsActivated=10,syntheticActivationMode="EXACT_NEW_PLACEHOLDER_CAS_ONLY",
            completionPath=P35CookieCompletionPath,expiresAt=deadline,secureCookieRequiresHttpsProxy=true,
            apiSetupAuthentication="PRIVATE_SUBJECT_TEST_AUTH_ONLY",
            browserAuthenticationRequired="NORMAL_LOCAL_PASSWORD_AND_SESSION_COOKIE",
            localCookieLoginVerified=false,browserAcceptanceVerified=false,ownerCompletionVerified=false,
            signatureAuthority=false,contractActivated=false,financePosting=false,productionTouched=false};
        await using(var stream=new FileStream(P35CookieManifestPath,new FileStreamOptions{
            Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.Read,
            UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite}))
            await JsonSerializer.SerializeAsync(stream,manifest,new JsonSerializerOptions(JsonSerializerDefaults.Web),cancellationToken);
        Need(File.GetUnixFileMode(P35CookieManifestPath)==(UnixFileMode.UserRead|UnixFileMode.UserWrite),
            "P35 credential manifest must be0600.");
        result["p35ContractReviewNormalCookieControlPlane"]=new {status="LISTENING_CONTROL_PLANE_ONLY",
            syntheticCredentialsSeeded=10,syntheticAccountsActivated=10,realLocalSchemeSelected=true,
            privateSubjectHandlerAbsent=true,privateSubjectHeaderRejected401=true,controlPlaneHttpRequests=1,
            loopbackEphemeralListener=true,browserAcceptanceVerified=false,ownerCompletionVerified=false,
            productionTouched=false};
        progress("P35ContractReviewNormalCookieAwaitingProtectedBrowserCompletion");
        while(!File.Exists(P35CookieCompletionPath))
        {
            Need(DateTimeOffset.UtcNow<deadline,"P35 private browser completion timed out; retain fixture and evidence.");
            await Task.Delay(TimeSpan.FromMilliseconds(500),cancellationToken);
        }
        Need(DateTimeOffset.UtcNow<=deadline&&new FileInfo(P35CookieCompletionPath).Length is >0 and <=4096
            &&(File.GetAttributes(P35CookieCompletionPath)&FileAttributes.ReparsePoint)==0
            &&File.GetUnixFileMode(P35CookieCompletionPath)==(UnixFileMode.UserRead|UnixFileMode.UserWrite),
            "P35 browser completion must be a new regular0600 private file.");
        using(var stream=new FileStream(P35CookieCompletionPath,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            Need(stream.Length is >0 and <=4096,"Bounded P35 browser completion required.");
            using var completion=await JsonDocument.ParseAsync(stream,cancellationToken:cancellationToken);
            var fields=completion.RootElement;
            Need(fields.ValueKind==JsonValueKind.Object&&fields.EnumerateObject().Count()==3
                &&fields.EnumerateObject().Select(x=>x.Name).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"schemaVersion","challenge","status"})
                &&fields.GetProperty("schemaVersion").GetInt32()==1&&fields.GetProperty("challenge").GetString()==challenge
                &&fields.GetProperty("status").GetString()=="BrowserFinished",
                "Exact challenged P35 lifecycle completion required; it does not establish browser acceptance.");
        }
        result["p35ContractReviewNormalCookieControlPlane"]=new {status="CONTROL_PLANE_COMPLETED_BROWSER_NOT_VERIFIED",
            syntheticCredentialsSeeded=10,syntheticAccountsActivated=10,realLocalSchemeSelected=true,
            privateSubjectHandlerAbsent=true,privateSubjectHeaderRejected401=true,controlPlaneHttpRequests=1,
            protectedLifecycleCompletionReceived=true,completionIsAcceptance=false,
            browserAcceptanceVerified=false,ownerCompletionVerified=false,productionTouched=false};
    }
}
