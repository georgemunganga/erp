using System.Net;
using System.Net.Http.Json;
using System.Diagnostics;
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
    internal static string P31DocumentDirectory()
    {
        var path=Environment.GetEnvironmentVariable("P31_HTTP_DOCUMENT_DIRECTORY")??string.Empty;
        var receipt=Environment.GetEnvironmentVariable("P25_HTTP_RESULT")??string.Empty;
        var parent=Path.GetDirectoryName(receipt)??string.Empty;
        Need(Path.IsPathFullyQualified(path) && Directory.Exists(path)
            && Path.IsPathFullyQualified(parent)
            && Path.GetFullPath(path).StartsWith(Path.GetFullPath(parent)+Path.DirectorySeparatorChar,StringComparison.Ordinal)
            && (File.GetAttributes(path)&FileAttributes.ReparsePoint)==0
            && (File.GetUnixFileMode(path)&(UnixFileMode.GroupRead|UnixFileMode.GroupWrite|UnixFileMode.GroupExecute
                |UnixFileMode.OtherRead|UnixFileMode.OtherWrite|UnixFileMode.OtherExecute))==0,
            "P31 requires an existing owner-private Core document directory.");
        return path;
    }
    internal static string P31ScannerSocket()
    {
        var path=Environment.GetEnvironmentVariable("P31_HTTP_SCANNER_SOCKET")??string.Empty;
        Need(Path.IsPathFullyQualified(path) && File.Exists(path)
            && (File.GetAttributes(path)&FileAttributes.ReparsePoint)==0,
            "P31 requires an explicit available private scanner socket.");
        return path;
    }
    internal delegate Task<JsonElement> PrincipalSend(HttpClient client,HttpMethod method,string path,Guid actor,
        object? body=null,HttpStatusCode expected=HttpStatusCode.OK,Guid? commandKey=null);
    internal static void Need(bool condition,string controlledMessage) {if(!condition)throw new PrivatePrincipalProbeFailure(controlledMessage);}
    private static bool PrivateCommercialDatabase(string database)
    {
        var po=Environment.GetEnvironmentVariable("P30_HTTP_VERIFY_PRINCIPAL_PO");
        var publication=Environment.GetEnvironmentVariable("P30_HTTP_VERIFY_RFQ_PUBLICATION");
        var commercial=Environment.GetEnvironmentVariable("P32_HTTP_VERIFY_RFQ_COMMERCIAL_DRAFT");
        var invoice=Environment.GetEnvironmentVariable("P31_HTTP_VERIFY_PRINCIPAL_INVOICE");
        return po=="1" && publication is null
                && (invoice=="1"
                    ? System.Text.RegularExpressions.Regex.IsMatch(database,"^proc_p31_http_[a-z0-9_]{1,40}$")
                    : System.Text.RegularExpressions.Regex.IsMatch(database,"^proc_p30_http_po_[a-z0-9_]{1,40}$"))
            || publication=="1" && po is null
                && Environment.GetEnvironmentVariable("P29_HTTP_VERIFY_PRINCIPAL_RFQ")=="1"
                && Environment.GetEnvironmentVariable("P29_HTTP_SERVE_NORMAL_COOKIE") is null
                && (commercial=="1"
                    ? System.Text.RegularExpressions.Regex.IsMatch(database,"^proc_p32_http_rfq_[a-z0-9_]{1,40}$")
                    : System.Text.RegularExpressions.Regex.IsMatch(database,"^proc_p30_http_rfq_[a-z0-9_]{1,40}$"));
    }

    internal static async Task RunAsync()
    {
        var owner=Environment.GetEnvironmentVariable("P25_HTTP_OWNER_CONNECTION")??throw new InvalidOperationException("Explicit private owner connection required.");
        var connection=new NpgsqlConnectionStringBuilder(owner);
        var database=connection.Database ?? string.Empty;
        Need(connection.Host=="/authority-identity-private-socket"
            && (System.Text.RegularExpressions.Regex.IsMatch(database,"^proc_p25_http_[a-z0-9_]{1,48}$")
                || System.Text.RegularExpressions.Regex.IsMatch(database,"^proc_p30_http_po_[a-z0-9_]{1,40}$")
                || System.Text.RegularExpressions.Regex.IsMatch(database,"^proc_p31_http_[a-z0-9_]{1,40}$")
                || System.Text.RegularExpressions.Regex.IsMatch(database,"^proc_p30_http_rfq_[a-z0-9_]{1,40}$")
                || System.Text.RegularExpressions.Regex.IsMatch(database,"^proc_p32_http_rfq_[a-z0-9_]{1,40}$"))
            && !connection.Multiplexing,"Only bounded fresh private socket database supported.");
        var resultPath=Environment.GetEnvironmentVariable("P25_HTTP_RESULT")??throw new InvalidOperationException("New protected result required.");
        Need(Path.IsPathFullyQualified(resultPath)&&!File.Exists(resultPath)&&Directory.Exists(Path.GetDirectoryName(resultPath)),"New result in existing protected directory required.");
        var poolingFlag=Environment.GetEnvironmentVariable("P25_HTTP_PRODUCTION_POOLING");
        Need(poolingFlag is null or "1","Private production-style pooling opt-in must be exactly1 or absent.");
        Need(poolingFlag is null || !connection.Pooling,
            "Owner seed/DDL connection must stay unpooled in the private pooling profile.");
        var result=new Dictionary<string,object?> { ["status"]="NOT_STARTED",["stage"]="Bootstrap",["productionTouched"]=false,["testAuthenticationOnly"]=true,
            ["realIdentityProviderAuthentication"]=false,["externalFinanceAuthority"]=false,["manualEvidencePositiveWorkflow"]=false,
            ["productionPoolingProfile"]=poolingFlag=="1",["poolIsolationAndResetVerified"]=false };
        var matrixFlag=Environment.GetEnvironmentVariable("P25_HTTP_VERIFY_PRIVILEGE_MATRIX");
        Need(matrixFlag is null or "1","Private privilege matrix opt-in must be exactly1 or absent.");
        var schemaFlag=Environment.GetEnvironmentVariable("P25_HTTP_VERIFY_SCHEMA_NEGATIVES");
        var accountFlag=Environment.GetEnvironmentVariable("P25_HTTP_VERIFY_ACCOUNT_SCOPE_AUTHORITY");
        var aliasFlag=Environment.GetEnvironmentVariable("P25_HTTP_VERIFY_RETAINED_ALIAS");
        var localCookieFlag=Environment.GetEnvironmentVariable("P25_HTTP_SERVE_LOCAL_COOKIE_BROWSER");
        var downFamily=Environment.GetEnvironmentVariable("P25_HTTP_VERIFY_ROW_FAMILY_DOWN");
        var exactBudgetFlag=Environment.GetEnvironmentVariable("P25_HTTP_VERIFY_EXACT_BUDGET_WIRE");
        var legacyWorkerFlag=Environment.GetEnvironmentVariable("P25_HTTP_VERIFY_LEGACY_WORKER_BRIDGE");
        var principalRfqFlag=Environment.GetEnvironmentVariable("P29_HTTP_VERIFY_PRINCIPAL_RFQ");
        var principalPoFlag=Environment.GetEnvironmentVariable("P30_HTTP_VERIFY_PRINCIPAL_PO");
        var receivingFlag=Environment.GetEnvironmentVariable("P30_HTTP_VERIFY_PRINCIPAL_RECEIVING");
        var invoiceFlag=Environment.GetEnvironmentVariable("P31_HTTP_VERIFY_PRINCIPAL_INVOICE");
        var commercialDraftFlag=Environment.GetEnvironmentVariable("P32_HTTP_VERIFY_RFQ_COMMERCIAL_DRAFT");
        var retainedPoCancelFlag=Environment.GetEnvironmentVariable("P30_HTTP_VERIFY_RETAINED_HISTORY_CANCEL");
        Need(receivingFlag is null or "1","P30 receiving opt-in must be exactly1 or absent.");
        Need(receivingFlag is null||principalPoFlag=="1","Receiving proof requires its fresh genuine principal PO profile.");
        Need(invoiceFlag is null or "1","P31 invoice opt-in must be exactly1 or absent.");
        Need(invoiceFlag is null || principalPoFlag=="1" && receivingFlag=="1"
            && System.Text.RegularExpressions.Regex.IsMatch(database,"^proc_p31_http_[a-z0-9_]{1,40}$")
            && retainedPoCancelFlag is null
            && Environment.GetEnvironmentVariable("P29_HTTP_VERIFY_PRINCIPAL_RFQ") is null
            && Environment.GetEnvironmentVariable("P30_HTTP_VERIFY_RFQ_PUBLICATION") is null
            && Environment.GetEnvironmentVariable("P30_HTTP_SERVE_NORMAL_COOKIE") is null
            && Environment.GetEnvironmentVariable("P30_HTTP_SERVE_RECEIVING_NORMAL_COOKIE") is null,
            "P31 needs its own fresh PO and receiving source cohort, without other optional proofs.");
        Need(!database.StartsWith("proc_p31_http_",StringComparison.Ordinal) || invoiceFlag=="1",
            "P31 private database requires its explicit invoice proof profile.");
        if(invoiceFlag=="1") { _=P31DocumentDirectory();_=P31ScannerSocket(); }
        var publicationFlag=Environment.GetEnvironmentVariable("P30_HTTP_VERIFY_RFQ_PUBLICATION");
        var p29CookieFlag=Environment.GetEnvironmentVariable("P29_HTTP_SERVE_NORMAL_COOKIE");
        var p30PoCookieFlag=Environment.GetEnvironmentVariable("P30_HTTP_SERVE_NORMAL_COOKIE");
        var p30ReceivingCookieFlag=Environment.GetEnvironmentVariable("P30_HTTP_SERVE_RECEIVING_NORMAL_COOKIE");
        var p30ServiceCookieFlag=Environment.GetEnvironmentVariable("P30_HTTP_SERVE_SERVICE_NORMAL_COOKIE");
        var p30RfqCookieFlag=Environment.GetEnvironmentVariable("P30_HTTP_SERVE_RFQ_NORMAL_COOKIE");
        Need(principalRfqFlag is null or "1","Private P29 principal RFQ opt-in must be exactly1 or absent.");
        Need(principalPoFlag is null or "1","Private P30 principal PO opt-in must be exactly1 or absent.");
        Need(publicationFlag is null or "1","Private P30 RFQ publication opt-in must be exactly1 or absent.");
        Need(commercialDraftFlag is null or "1","Private P32 commercial Draft opt-in must be exactly1 or absent.");
        Need(commercialDraftFlag is null || publicationFlag=="1" && principalRfqFlag=="1"
            && principalPoFlag is null && receivingFlag is null && invoiceFlag is null
            && database.StartsWith("proc_p32_http_rfq_",StringComparison.Ordinal),
            "P32 Draft proof requires its exclusive fresh P29/P30 RFQ source cohort.");
        Need(!database.StartsWith("proc_p32_http_rfq_",StringComparison.Ordinal)||commercialDraftFlag=="1",
            "P32 private database requires its explicit Draft opt-in.");
        Need(!database.StartsWith("proc_p30_http_rfq_",StringComparison.Ordinal) || publicationFlag=="1",
            "P30 RFQ database requires its explicit publication profile.");
        Need(publicationFlag is null || (database.StartsWith("proc_p30_http_rfq_",StringComparison.Ordinal)
            || commercialDraftFlag=="1" && database.StartsWith("proc_p32_http_rfq_",StringComparison.Ordinal))
            && principalRfqFlag=="1" && principalPoFlag is null && p29CookieFlag is null,
            "P30 RFQ publication cannot combine with PO or cookie profiles.");
        Need(p30RfqCookieFlag is null or "1","P30 RFQ normal-cookie serve opt-in must be exactly1 or absent.");
        Need(p30RfqCookieFlag is null || publicationFlag=="1" && principalRfqFlag=="1"
            && database.StartsWith("proc_p30_http_rfq_",StringComparison.Ordinal)
            && principalPoFlag is null && receivingFlag is null && p29CookieFlag is null
            && p30PoCookieFlag is null && p30ReceivingCookieFlag is null
            && matrixFlag is null && schemaFlag is null && accountFlag is null
            && aliasFlag is null && localCookieFlag is null && downFamily is null
            && exactBudgetFlag is null && legacyWorkerFlag is null,
            "P30 RFQ browser requires its separate fresh publication cohort.");
        Need(!database.StartsWith("proc_p30_http_po_",StringComparison.Ordinal) || principalPoFlag=="1",
            "P30 private database is exclusive to its explicit PO profile.");
        Need(principalPoFlag is null || (database.StartsWith("proc_p30_http_po_",StringComparison.Ordinal)
                && invoiceFlag is null || database.StartsWith("proc_p31_http_",StringComparison.Ordinal)
                && invoiceFlag=="1")
            && principalRfqFlag is null && publicationFlag is null && matrixFlag is null && schemaFlag is null && accountFlag is null
            && aliasFlag is null && localCookieFlag is null && downFamily is null && exactBudgetFlag is null
            && legacyWorkerFlag is null && p29CookieFlag is null
            && Environment.GetEnvironmentVariable("P25_HTTP_VERIFY_RAW_GRAPH") is null,
            "Principal PO needs its own new private database and no other optional cohort.");
        Need(p29CookieFlag is null or "1","P29 normal-cookie serve opt-in must be exactly1 or absent.");
        Need(p30PoCookieFlag is null or "1","P30 PO normal-cookie serve opt-in must be exactly1 or absent.");
        Need(p30ReceivingCookieFlag is null or "1","P30 receiving normal-cookie serve opt-in must be exactly1 or absent.");
        Need(p30ServiceCookieFlag is null or "1","P30 service normal-cookie serve opt-in must be exactly1 or absent.");
        Need(p30ReceivingCookieFlag is null || principalPoFlag=="1" && receivingFlag=="1"
            && database.StartsWith("proc_p30_http_po_",StringComparison.Ordinal)
            && p30PoCookieFlag is null && p30ServiceCookieFlag is null && p29CookieFlag is null && retainedPoCancelFlag is null
            && principalRfqFlag is null && publicationFlag is null,
            "P30 receiving normal-cookie serve needs its separate fresh PO and receiving cohort.");
        Need(p30ServiceCookieFlag is null || principalPoFlag=="1" && receivingFlag=="1"
            && database.StartsWith("proc_p30_http_po_",StringComparison.Ordinal)
            && p30PoCookieFlag is null && p30ReceivingCookieFlag is null && p29CookieFlag is null
            && retainedPoCancelFlag is null && principalRfqFlag is null && publicationFlag is null,
            "P30 service normal-cookie serve needs its exclusive fresh PO and receiving cohort.");
        Need(retainedPoCancelFlag is null or "1","P30 retained-history cancellation opt-in must be exactly1 or absent.");
        Need(retainedPoCancelFlag is null || principalPoFlag=="1"
            && database.StartsWith("proc_p30_http_po_",StringComparison.Ordinal)
            && receivingFlag is null && p30PoCookieFlag is null && p30ReceivingCookieFlag is null && p30ServiceCookieFlag is null && p29CookieFlag is null
            && principalRfqFlag is null && publicationFlag is null,
            "Retained-history cancellation requires a separate fresh private PO API cohort, without cookie or receiving phases.");
        Need(p30PoCookieFlag is null || principalPoFlag=="1" && database.StartsWith("proc_p30_http_po_",StringComparison.Ordinal)
            && p29CookieFlag is null && principalRfqFlag is null && publicationFlag is null && receivingFlag is null
            && p30ReceivingCookieFlag is null && p30ServiceCookieFlag is null,
            "P30 PO normal-cookie serve requires its exclusive fresh private PO profile.");
        Need(p29CookieFlag is null || principalRfqFlag=="1" && publicationFlag is null,
            "P29 normal-cookie serve requires its exclusive genuine principal RFQ profile.");
        Need(principalRfqFlag is null || (database.StartsWith("proc_p25_http_p29_",StringComparison.Ordinal)
                && publicationFlag is null || database.StartsWith("proc_p30_http_rfq_",StringComparison.Ordinal)
                && publicationFlag=="1" || database.StartsWith("proc_p32_http_rfq_",StringComparison.Ordinal)
                && publicationFlag=="1" && commercialDraftFlag=="1") && principalPoFlag is null
            && matrixFlag is null && schemaFlag is null && accountFlag is null && aliasFlag is null
            && localCookieFlag is null && downFamily is null && exactBudgetFlag is null && legacyWorkerFlag is null
            && Environment.GetEnvironmentVariable("P25_HTTP_POSITIVE_MANUAL_EVIDENCE") is null
            && Environment.GetEnvironmentVariable("P25_HTTP_VERIFY_RAW_GRAPH") is null,
            "Principal RFQ needs its own new private database and no other optional cohort.");
        Need(legacyWorkerFlag is null or "1","Private legacy worker bridge opt-in must be exactly1 or absent.");
        Need(legacyWorkerFlag is null || matrixFlag is null && schemaFlag is null && accountFlag is null
            && aliasFlag is null && localCookieFlag is null && downFamily is null && exactBudgetFlag is null
            && Environment.GetEnvironmentVariable("P25_HTTP_POSITIVE_MANUAL_EVIDENCE") is null
            && Environment.GetEnvironmentVariable("P25_HTTP_VERIFY_RAW_GRAPH") is null,
            "Legacy worker bridge requires a separate fresh private cohort without other optional phases.");
        Need(exactBudgetFlag is null or "1","Private exact-budget wire opt-in must be exactly1 or absent.");
        Need(exactBudgetFlag is null || Environment.GetEnvironmentVariable("P25_HTTP_POSITIVE_MANUAL_EVIDENCE")=="1"
            && downFamily is null && matrixFlag is null && schemaFlag is null && accountFlag is null
            && aliasFlag is null && localCookieFlag is null && Environment.GetEnvironmentVariable("P25_HTTP_VERIFY_RAW_GRAPH") is null,
            "Exact-budget wire cohort requires only the successful base positive graph.");
        Need(downFamily is null or "RequestProvenance" or "Authority2" or "Checker2" or "Submission3" or "Assignment2",
            "Unknown private P25 row-family checkpoint.");
        Need(downFamily is null || matrixFlag is null && schemaFlag is null && accountFlag is null
            && aliasFlag is null && localCookieFlag is null && Environment.GetEnvironmentVariable("P25_HTTP_VERIFY_RAW_GRAPH") is null,
            "Row-family Down checkpoint cannot share a different optional cohort.");
        Need(downFamily is null or "RequestProvenance" || Environment.GetEnvironmentVariable("P25_HTTP_POSITIVE_MANUAL_EVIDENCE")=="1",
            "Dependent row-family checkpoint requires the genuine positive manual-evidence route fixture.");
        Need(schemaFlag is null or "1","Private schema negatives opt-in must be exactly1 or absent.");
        Need(accountFlag is null or "1","Private account/authority opt-in must be exactly1 or absent.");
        Need(accountFlag is null || Environment.GetEnvironmentVariable("P25_HTTP_POSITIVE_MANUAL_EVIDENCE")=="1",
            "Account/authority negatives require the genuine private manual-evidence graph.");
        Need(aliasFlag is null or "1","Private retained alias opt-in must be exactly1 or absent.");
        Need(aliasFlag is null || Environment.GetEnvironmentVariable("P25_HTTP_POSITIVE_MANUAL_EVIDENCE")=="1",
            "Retained alias checks require the genuine private manual-evidence graph.");
        Need(localCookieFlag is null or "1","Private normal local-cookie serve opt-in must be exactly1 or absent.");
        Need(localCookieFlag is null || Environment.GetEnvironmentVariable("P25_HTTP_POSITIVE_MANUAL_EVIDENCE")=="1"
            && matrixFlag is null && schemaFlag is null && accountFlag is null && aliasFlag is null
            && Environment.GetEnvironmentVariable("P25_HTTP_VERIFY_RAW_GRAPH") is null,
            "Normal local-cookie serve requires the original genuine base graph without other optional cohorts.");
        result["browserBuyerFixtureProfile"]=localCookieFlag=="1"?"PRIVATE_CONFIG_MAKER_POLICY_EVALUATION":"DISABLED";
        var fixture=new PrincipalApprovalFixture(owner,localCookieFlag=="1",legacyWorkerFlag=="1",principalRfqFlag=="1",principalPoFlag=="1",receivingFlag=="1",invoiceFlag=="1");var stage="Bootstrap";var requests=0;
        using var readinessTiming=new PrincipalApprovalProbeTiming();
        var httpTiming=new List<object>();
        var httpTimingDiscarded=0;
        var rfqCheckpoint=publicationFlag=="1"?new PrincipalApprovalRfqCheckpoint(resultPath):null;
        string? rfqRules=null;
        try
        {
            if(publicationFlag=="1")
            {
                Advance("P30RfqPolicyPreflight");
                rfqRules=BuildP30RfqSourcingRules(fixture.ConfigReviewer);
                PreflightP30RfqSourcingRules(rfqRules);
            }
            Advance("Bootstrap");
            await fixture.BootstrapAsync();
            Advance("FiniteRoleProvision");var roles=await PrincipalApprovalRuntimePrivileges.CreateAsync(owner,fixture,poolingFlag=="1");
            await using var ownerConnection=new NpgsqlConnection(owner);await ownerConnection.OpenAsync();
            foreach(var (text,expected) in new[]{(roles.Record,roles.RecordRole),(roles.Execution,roles.ExecutionRole)})
            {
                await using var runtime=new NpgsqlConnection(text);await runtime.OpenAsync();
                await using var command=new NpgsqlCommand("SELECT current_user=session_user AND current_user=@expected AND NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))",runtime);
                command.Parameters.AddWithValue("expected",expected);Need(await command.ExecuteScalarAsync() is true,"Finite test role is not an actual distinct restricted LOGIN.");
            }
            if(poolingFlag=="1")
            {
                Advance("FiniteRolePoolIsolationAndReset");
                await PrincipalApprovalPoolReset.VerifyAsync(roles);
                result["poolIsolationAndResetVerified"]=true;
            }
            if(legacyWorkerFlag=="1")
            {
                await VerifyLegacyWorkerBridgeAsync(fixture,owner,roles,result,Advance,Send);
                return;
            }
            if(matrixFlag=="1")await VerifyPrivilegeMatrixAsync(fixture,roles,ownerConnection,result,Advance);
            if(schemaFlag=="1")await VerifySchemaNegativesAsync(fixture,roles,ownerConnection,result,Advance);
            Advance("CapturedActualReadRecordExecutionReadiness");
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
            // First host explicitly substitutes an unavailable provider as a
            // negative control; positive hosts use ordinary Program registration.
            Advance("UnavailableProviderNegativeControl");
            await using(var disabled=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,false))
            {
                disabled.PrepareEntryPointEnvironment();using var client=disabled.CreateClient(new(){AllowAutoRedirect=false});
                using var scope=disabled.Services.CreateScope();
                Need(scope.ServiceProvider.GetRequiredService<IProcurementPrincipalApprovalSchemaReadiness>() is UnavailableProcurementPrincipalApprovalSchemaReadiness,"Negative provider control unexpectedly enabled.");
                var baseline=await PrincipalFootprintAsync(ownerConnection);
                var meta=await Send(client,HttpMethod.Get,"/api/procurement/v1/meta",fixture.Maker);Need(!meta.GetProperty("capabilities").GetProperty("principalApprovals").GetBoolean(),"Negative unavailable provider advertised approval.");
                // Physical legacy availability permits the scoped lookup. An absent
                // record remains 404; the real principal-row control follows below.
                await Send(client,HttpMethod.Post,$"/api/procurement/v1/requests/{Guid.NewGuid()}/approval-cases",fixture.Maker,new{expectedRequestVersion=1L},HttpStatusCode.NotFound);
                await ReadAcknowledgementWithoutEffectsAsync(ownerConnection,client,Send,
                    $"/api/procurement/v1/requests/{Guid.NewGuid()}/approval-commands/Open/{Guid.NewGuid()}",fixture.Maker,HttpStatusCode.ServiceUnavailable);
                result["negativeProviderGetAcknowledgementVerified"]=true;
                Need(await PrincipalFootprintAsync(ownerConnection)==baseline,"Unavailable host calls wrote business evidence.");
            }
            Advance("ActualProgramFiniteCreateSubmit");
            await using(var host=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,true))
            {
                host.PrepareEntryPointEnvironment();using var client=host.CreateClient(new(){AllowAutoRedirect=false});
                var options=host.Services.GetRequiredService<ProcurementApprovalExecutionOptions>();
                Need(options.Enabled&&options.PrincipalExecutionEnabled&&options.ExecutionDatabaseConfigured&&options.ManualFinancePolicies.Length==0,"Private host must retain missing manual Finance policy.");
                var created=await Send(client,HttpMethod.Post,"/api/procurement/v1/requests",fixture.Maker,new {purpose="Private P25 current Core-only request",neededBy=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),currencyCode="ZMW",lines=new[]{new{description="No employee prerequisite",unitCode="each",requestedQuantity="2",estimatedUnitPrice="5.1234"}}},HttpStatusCode.Created);
                var id=created.GetProperty("id").GetGuid();var path="/api/procurement/v1/requests/"+id;
                if(downFamily=="RequestProvenance")
                {
                    Advance("DraftRequestProvenanceDownVeto");
                    result["requestId"]=id;
                    result["rowFamilyDown"]=await VerifyRowFamilyDownAsync(ownerConnection,downFamily,fixture.Company,id);
                    result["status"]="PASS_PRIVATE_P25_DEPENDENCY_SHAPED_DOWN_VETO_ONLY";
                    return;
                }
                var submitted=await Send(client,HttpMethod.Post,path+"/submit",fixture.Maker,new {version=created.GetProperty("version").GetInt64()});
                await using(var db=fixture.Procurement())
                {
                    var source=await db.PurchaseRequests.Include(x=>x.Lines).SingleAsync(x=>x.Id==id);
                    Need(source.Status=="Submitted"&&source.RequesterPrincipalId==fixture.Maker&&source.RequesterWorkerId is null&&source.RequesterCoreWorkerId is null
                        &&source.RequesterIdentitySchemaVersion==1&&PurchaseRequestPrincipalIdentity.Valid(source),"Server-owned original principal1/source identity was not persisted.");
                    Need(source.Lines.Count==1&&source.Lines.Single().RequestedQuantity==2m&&source.Lines.Single().EstimatedUnitPrice==5.1234m,"Exact source values changed.");
                    result["requestId"]=id;result["submittedBusinessVersion"]=source.SubmittedVersion;result["currentRequestVersion"]=source.Version;
                }
                Advance("OriginalRequestRawStorage");
                await VerifyRequestRawStorageAsync(ownerConnection,id,PrincipalApprovalFixture.Tenant,fixture.Company,
                    submitted.GetProperty("version").GetInt64());
                result["originalRequestRawStorage"]=PrincipalRawStorageVerification;
                Advance("UnavailableProviderExistingPrincipalControl");
                await VerifyUnavailableExistingPrincipalAsync(fixture,owner,roles,ownerConnection,id,
                    submitted.GetProperty("version").GetInt64(),result,Send);
                Advance("FundingUnavailableNoEffects");var before=await PrincipalFootprintAsync(ownerConnection);
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
            result["currentCoreWorkerlessSourceVerified"]=true;result["negativeProviderControlVerified"]=true;result["fundingUnavailableNoEffectsVerified"]=true;
            result["positiveApprovalGraph"]=false;result["checker2Authority2Submission3Assignment2Acceptance"]="NOT_RUN_NEXT_CHECKPOINT";
            var positive=Environment.GetEnvironmentVariable("P25_HTTP_POSITIVE_MANUAL_EVIDENCE");
            if(positive is not null || principalRfqFlag=="1" || principalPoFlag=="1")
            {
                Need(positive is null or "1","Private manual evidence opt-in must be exactly1.");
                await VerifyPrivateManualApprovalAsync(fixture,owner,roles,ownerConnection,result,Advance,Send,()=>requests++,accountFlag=="1",aliasFlag=="1",downFamily,principalRfqFlag=="1",principalPoFlag=="1");
                if(principalPoFlag=="1")
                {
                    var issued=await VerifyPrincipalOrderAsync(fixture,owner,roles,result,Advance,Send,()=>requests++);
                    if(receivingFlag=="1")await VerifyPrincipalReceivingAsync(fixture,owner,roles,result,Advance,Send,()=>requests++,issued);
                    if(invoiceFlag=="1")await VerifyPrincipalInvoiceAsync(fixture,owner,roles,result,Advance,Send,issued);
                    if(p30ReceivingCookieFlag=="1")
                    {
                        Advance("P30ReceivingGenuineUnconsumedBrowserGoodsSource");
                        PrincipalIssuedFixture browserGoods;
                        await using (var setupHost=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,
                            actualSchema:true,privateManualEvidence:true))
                        {
                            setupHost.PrepareEntryPointEnvironment();
                            using var setupClient=setupHost.CreateClient(new(){AllowAutoRedirect=false});
                            browserGoods=await CreateActualReceivingCookieGoodsSourceAsync(fixture,setupClient,Send,issued.SupplierId);
                        }
                        await ServeP30ReceivingNormalLocalCookieAsync(fixture,owner,roles,ownerConnection,
                            browserGoods,issued,result,Advance);
                    }
                    if(p30ServiceCookieFlag=="1")
                    {
                        Advance("P30ServiceGenuineUnconsumedBrowserSource");
                        PrincipalIssuedFixture browserService;
                        await using (var setupHost=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,
                            actualSchema:true,privateManualEvidence:true))
                        {
                            setupHost.PrepareEntryPointEnvironment();
                            using var setupClient=setupHost.CreateClient(new(){AllowAutoRedirect=false});
                            browserService=await CreateActualReceivingServiceSourceAsync(fixture,setupClient,Send,issued.SupplierId,browser:true);
                        }
                        await ServeP30ServiceNormalLocalCookieAsync(fixture,owner,roles,ownerConnection,
                            browserService,issued,result,Advance);
                    }
                    if(p30PoCookieFlag=="1")
                        await ServeP30PoNormalLocalCookieAsync(fixture,owner,roles,ownerConnection,
                            issued.RequestId,issued.RequestLineId,issued.SupplierId,issued.OrderId,result,Advance);
                }
                if(principalRfqFlag=="1")
                {
                    var p29=await VerifyPrincipalRfqAsync(fixture,owner,roles,result,Advance,Send,()=>requests++);
                    if(publicationFlag=="1")
                        await VerifyP30RfqPublicationAsync(fixture,owner,roles,result,Advance,Send,()=>requests++,rfqRules!);
                    if(p30RfqCookieFlag=="1")
                        await ServeP30RfqNormalLocalCookieAsync(fixture,owner,roles,ownerConnection,result,Advance);
                    if(p29CookieFlag=="1")
                        await ServeP29NormalLocalCookieAsync(fixture,owner,roles,ownerConnection,
                            p29.RequestId,p29.LineId,p29.EventId,result,Advance);
                }
                if(exactBudgetFlag=="1")
                {
                    Need(result.TryGetValue("positiveApprovalGraph",out var graphAccepted) && graphAccepted is true,
                        "The base genuine principal approval graph must pass before exact-budget wire checks.");
                    await VerifyExactBudgetWireAsync(fixture,owner,roles,ownerConnection,result,Advance,Send);
                }
                if(localCookieFlag=="1")
                    await ServeNormalLocalCookieAsync(fixture,owner,roles,ownerConnection,result,Advance);
            }
        }
        catch(Exception error)
        {
            result["status"]="FAILED";result["exceptionType"]=error.GetType().FullName;
            result["failureCode"]=error is PrivatePrincipalProbeFailure known?known.SafeCode:"P25_Unclassified_SeeStageAndTypedCause";
            var cause=error.GetBaseException();result["underlyingExceptionType"]=cause.GetType().FullName;
            result["exceptionTypeChain"]=ExceptionTypes(error);
            if(cause is PostgresException postgres)result["sqlState"]=postgres.SqlState;
            if(PrincipalRawStorageFailure is not null)result["rawStorageFailure"]=PrincipalRawStorageFailure;
            // Never export arbitrary exception messages, SQL, row payloads or connection configuration.
            Environment.ExitCode=2;
        }
        finally
        {
            Checkpoint();
            result["stage"]=stage;result["actualHttpRequests"]=requests;
            result["readinessPhaseTiming"]=readinessTiming.Snapshot();
            result["httpTiming"]=httpTiming;
            result["httpTimingDiscarded"]=httpTimingDiscarded;
            await File.WriteAllTextAsync(resultPath,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
            if(OperatingSystem.IsLinux())File.SetUnixFileMode(resultPath,UnixFileMode.UserRead|UnixFileMode.UserWrite);
        }
        Console.WriteLine(result["status"]);

        void Advance(string next)
        {
            Need(System.Text.RegularExpressions.Regex.IsMatch(next,"^[A-Za-z][A-Za-z0-9]{0,79}$"),"Only controlled private stage names may be logged.");
            stage=next;Console.WriteLine("P25_STAGE "+next);
            Checkpoint();
        }

        void Checkpoint() => rfqCheckpoint?.Write(stage,requests,readinessTiming.Snapshot(),httpTiming.Count,httpTimingDiscarded);

        async Task<JsonElement> Send(HttpClient client,HttpMethod method,string path,Guid actor,object? body=null,HttpStatusCode expected=HttpStatusCode.OK,Guid? commandKey=null)
        {
            using var request=new HttpRequestMessage(method,path);request.Headers.Add("X-Private-P25-Subject",actor.ToString("D"));
            request.Headers.Add("X-Shell-Entity",fixture.Company.ToString("D"));request.Headers.Add("X-Shell-Location",fixture.Branch.ToString("D"));
            if(commandKey is not null)Need(commandKey!=Guid.Empty,"Explicit private command key cannot be empty.");
            if(method==HttpMethod.Post||method==HttpMethod.Put)request.Headers.Add("Idempotency-Key",(commandKey??Guid.NewGuid()).ToString("D"));
            if(body is not null)request.Content=JsonContent.Create(body);
            var start=Stopwatch.GetTimestamp();
            HttpResponseMessage response;
            try { response=await client.SendAsync(request); }
            catch(Exception error)
            {
                RecordHttp(method,path,start,"Exception",null,error.GetType().Name);
                throw;
            }
            using(response)
            {
                requests++;
                RecordHttp(method,path,start,"Response",(int)response.StatusCode,null);
                var text=await response.Content.ReadAsStringAsync();
                if(method==HttpMethod.Get&&path.Contains("/approval-commands/",StringComparison.Ordinal))
                    Need(response.Headers.CacheControl?.NoStore==true,"Every command acknowledgement response must be no-store.");
                if(response.StatusCode!=expected)
                {
                    string? code=null;
                    try {using var problem=JsonDocument.Parse(text);if(problem.RootElement.TryGetProperty("code",out var value)&&value.ValueKind==JsonValueKind.String
                        && value.GetString() is {} candidate && System.Text.RegularExpressions.Regex.IsMatch(candidate,"^[A-Za-z0-9_-]{1,100}$"))code=candidate;} catch(JsonException){}
                    result["failedHttp"]=new {method=method.Method,path,expectedStatus=(int)expected,actualStatus=(int)response.StatusCode,code};
                    throw new PrivatePrincipalProbeFailure("Http_"+method.Method+"_Expected_"+(int)expected+"_Actual_"+(int)response.StatusCode+"_"+(code??"NoSafeProblemCode"));
                }
                return string.IsNullOrWhiteSpace(text)?default:JsonDocument.Parse(text).RootElement.Clone();
            }
        }

        void RecordHttp(HttpMethod method,string path,long start,string outcome,int? status,string? errorType)
        {
            if(httpTiming.Count==256) {httpTimingDiscarded++;Checkpoint();return;}
            var route=path switch
            {
                "/api/procurement/v1/meta"=>"meta",
                _ when path.StartsWith("/api/procurement/v1/configuration/",StringComparison.Ordinal)=>"configuration",
                _ when path.StartsWith("/api/procurement/v1/requests/",StringComparison.Ordinal)=>"requests",
                _ when path.StartsWith("/api/procurement/v1/sourcing/",StringComparison.Ordinal)=>"sourcing",
                _=>"other",
            };
            httpTiming.Add(new {stage,method=method.Method,route,outcome,status,errorType,
                durationMilliseconds=Math.Round(Stopwatch.GetElapsedTime(start).TotalMilliseconds,3)});
            Checkpoint();
        }

        static string[] ExceptionTypes(Exception error)
        {
            var types=new List<string>();
            for(var current=error;current is not null&&types.Count<8;current=current.InnerException)
                types.Add(current.GetType().FullName??current.GetType().Name);
            return types.ToArray();
        }
    }
    // Shared owner-only verifier for this mode and the separately authored raw storage checks.
    // Read-only: hashes every persisted non-system table, metadata, sequences and privilege boundary.
    internal static async Task<string> PrincipalFootprintAsync(NpgsqlConnection owner,bool rowsOnly=false)
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
        if(rowsOnly)
        {
            await transaction.RollbackAsync();
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',parts))));
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

// Controlled tool-generated messages only; arbitrary application/SQL exception text is never accepted here.
internal sealed class PrivatePrincipalProbeFailure(string controlledMessage):Exception("Private P25 verification guard failed.")
{
    internal string SafeCode {get;}=Code(controlledMessage);
    private static string Code(string value)
    {
        var normalized=System.Text.RegularExpressions.Regex.Replace(value,"[^A-Za-z0-9]+","_").Trim('_');
        return "P25_"+normalized[..Math.Min(160,normalized.Length)];
    }
}
