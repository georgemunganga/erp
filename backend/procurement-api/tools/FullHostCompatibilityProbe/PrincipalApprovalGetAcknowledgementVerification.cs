using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    private static async Task<JsonElement> ReadAcknowledgementWithoutEffectsAsync(NpgsqlConnection owner,HttpClient client,
        PrincipalSend send,string path,Guid actor,HttpStatusCode status=HttpStatusCode.OK)
    {
        var before=await PrincipalFootprintAsync(owner);
        var response=await send(client,HttpMethod.Get,path,actor,null,status);
        Need(await PrincipalFootprintAsync(owner)==before,"GET command acknowledgement changed full store footprint.");
        return response;
    }
    private static string AcknowledgementPath(Guid request,string operation,Guid key)
        =>$"/api/procurement/v1/requests/{request}/approval-commands/{operation}/{key}";

    private static async Task VerifyActualAcknowledgementAsync(PrincipalApprovalFixture fixture,NpgsqlConnection owner,
        HttpClient client,PrincipalSend send,Guid requestId,string operation,Guid key,Guid actor,JsonElement original)
    {
        // Independently pin the actual stored actor/scope/key before the HTTP read;
        // a URL or historical case status is not a command acknowledgement.
        await using var db=fixture.Procurement();
        var receipt=await db.ApprovalCommandReceipts.AsNoTracking().SingleAsync(x=>x.PurchaseRequestId==requestId
            &&x.Operation==operation&&x.CommandKey==key);
        var source=await db.PurchaseRequests.AsNoTracking().SingleAsync(x=>x.Id==requestId);
        Need(receipt.ActorSubjectId==actor.ToString("D")&&receipt.TenantId==PrincipalApprovalFixture.Tenant
            &&receipt.LegalEntityId==fixture.Company&&source.BranchRef==fixture.Branch
            &&receipt.ResponseJson==original.GetRawText(),"Acknowledgement fixture is not the actual original actor/scope/response tuple.");
        var response=await ReadAcknowledgementWithoutEffectsAsync(owner,client,send,AcknowledgementPath(requestId,operation,key),actor);
        string[] fields=["acknowledgementSchemaVersion","acknowledgement","operation","commandKey","requestId","caseId","assignmentId","completedAt","result"];
        var names=response.EnumerateObject().Select(x=>x.Name).ToArray();
        Need(names.Length==fields.Length&&names.Distinct(StringComparer.Ordinal).Count()==fields.Length
            &&!names.Except(fields,StringComparer.Ordinal).Any(),"GET acknowledgement envelope differs from explicit typed contract.");
        Need(response.GetProperty("acknowledgementSchemaVersion").GetInt32()==1&&response.GetProperty("acknowledgement").GetString()=="Completed"
            &&response.GetProperty("operation").GetString()==operation&&response.GetProperty("commandKey").GetGuid()==key
            &&response.GetProperty("requestId").GetGuid()==requestId&&response.GetProperty("caseId").GetGuid()==receipt.CoreApprovalCaseId
            &&response.GetProperty("completedAt").GetDateTimeOffset()==receipt.CompletedAt,"Actual GET acknowledgement changed original command binding.");
        Need(response.GetProperty("result").GetRawText()==original.GetRawText(),"GET acknowledgement did not return exact original typed response.");
        if(operation=="Open")
            Need(response.GetProperty("assignmentId").ValueKind==JsonValueKind.Null&&response.GetProperty("result").EnumerateObject().Count()==8,
                "Open acknowledgement must preserve exactly8 original result fields and no assignment.");
        else
            Need(response.GetProperty("result").EnumerateObject().Count()==11
                &&response.GetProperty("assignmentId").GetGuid()==original.GetProperty("assignmentId").GetGuid()
                &&response.GetProperty("result").GetProperty("decisionId").GetGuid()==original.GetProperty("decisionId").GetGuid(),
                "Decide acknowledgement lost its actual11-field assignment/decision binding.");
    }

    private static async Task VerifyHistoricalAcknowledgementsAsync(PrincipalApprovalFixture fixture,NpgsqlConnection owner,
        HttpClient client,PrincipalSend send,ProcurementApprovalExecutionOptions options,Guid requestId,Guid otherScopedRequestId,
        Guid openKey,Guid decideKey,JsonElement opened,JsonElement decided,Dictionary<string,object?> result)
    {
        await VerifyActualAcknowledgementAsync(fixture,owner,client,send,requestId,"Open",openKey,fixture.Maker,opened);
        await VerifyActualAcknowledgementAsync(fixture,owner,client,send,requestId,"Decide",decideKey,fixture.Approver,decided);
        // Both404 cases remain unknown outcomes to a caller. This tool never retries
        // POST or invents a new UUID as a result of absence.
        await ReadAcknowledgementWithoutEffectsAsync(owner,client,send,AcknowledgementPath(requestId,"Open",Guid.NewGuid()),fixture.Maker,HttpStatusCode.NotFound);
        await ReadAcknowledgementWithoutEffectsAsync(owner,client,send,AcknowledgementPath(otherScopedRequestId,"Open",openKey),fixture.Maker,HttpStatusCode.NotFound);
        await VerifyRevokedCurrentGrantReplayAsync(fixture,owner,client,send,fixture.Maker,"p25_request_maker","procurement-approval-open",
            AcknowledgementPath(requestId,"Open",openKey),openKey,null,HttpMethod.Get);
        await VerifyActualAcknowledgementAsync(fixture,owner,client,send,requestId,"Open",openKey,fixture.Maker,opened);
        await VerifyRevokedCurrentGrantReplayAsync(fixture,owner,client,send,fixture.Approver,"p25_custom_approver","procurement-approval-decide",
            AcknowledgementPath(requestId,"Decide",decideKey),decideKey,null,HttpMethod.Get);
        await VerifyActualAcknowledgementAsync(fixture,owner,client,send,requestId,"Decide",decideKey,fixture.Approver,decided);
        // PRIVATE HOST OPTIONS ONLY. The existing actual registered read provider
        // and record connection stay authoritative; no production configuration changes.
        var policies=options.ManualFinancePolicies;var executionConfigured=options.ExecutionDatabaseConfigured;
        try
        {
            options.ManualFinancePolicies=[];options.ExecutionDatabaseConfigured=false;
            Need(options.ManualFinancePolicies.Length==0&&!options.ExecutionDatabaseConfigured,"Private historical-read options were not cleared.");
            await VerifyActualAcknowledgementAsync(fixture,owner,client,send,requestId,"Open",openKey,fixture.Maker,opened);
            await VerifyActualAcknowledgementAsync(fixture,owner,client,send,requestId,"Decide",decideKey,fixture.Approver,decided);
        }
        finally
        {
            options.ManualFinancePolicies=policies;options.ExecutionDatabaseConfigured=executionConfigured;
            Need(ReferenceEquals(options.ManualFinancePolicies,policies)&&options.ExecutionDatabaseConfigured==executionConfigured,
                "Original private host options were not restored exactly.");
        }
        Need(result.GetValueOrDefault("defaultUnavailableGetAcknowledgementVerified") is true,"Default unavailable GET phase was not verified.");
        result["principalApprovalGetAcknowledgementVerification"]=new {
            status="PASS_PRIVATE_ACTOR_OWNED_GET_ACKNOWLEDGEMENTS_ONLY",actualGets=12,
            originalOpenBeforeAndAfterTerminalExact=true,originalDecideElevenFieldsExact=true,actualReceiptActorScopeKeyBound=true,
            missingKeyAndWrongRequest404NoEffects=true,absenceProvesNonCommit=false,
            currentOperationGrantRevocation403Restoration=true,defaultUnavailable503=true,fullFootprintUnchangedEveryGet=true,noStoreEveryGet=true,
            noManualPolicyOrExecutionConfiguredOptionRequired=true,privateHostOptionsOnly=true,actualExecutionConnectionRemoved=false,
            exactTwoReceiptsOneDecisionThreeSuccessorEventsStillRequired=true,
            nativeIdentityProviderVerified=false,externalFinanceAuthority=false,legacyApprovalExecutionVerified=false,wholeProcurementVerified=false};
    }
}
