using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Procurement.Application;
using Npgsql;
using NpgsqlTypes;

internal static partial class PrincipalApprovalHttpVerification
{
    // Hook before the original pending assignment's terminal decision. No worker
    // rows or aliases are created: these are current Core account/scope negatives.
    internal static async Task VerifyCurrentAccountAndScopeDenialsAsync(PrincipalApprovalFixture fixture,
        NpgsqlConnection owner,HttpClient client,PrincipalSend send,Guid caseId,Guid assignmentId,
        object pendingDecision,Guid commandKey,Action countExtraHttp,Dictionary<string,object?> result)
    {
        await RequirePrivateRawOwnerAsync(owner,PrincipalApprovalFixture.Tenant,fixture.Company);
        Need(caseId!=Guid.Empty&&assignmentId!=Guid.Empty&&commandKey!=Guid.Empty,"Actual pending command required.");
        var path=$"/api/procurement/v1/approvals/{caseId}/assignments/{assignmentId}/decisions";
        var faults=new (string Field,NpgsqlDbType Type,object Value)[] {
            ("is_active",NpgsqlDbType.Boolean,false),
            ("locked_until",NpgsqlDbType.TimestampTz,ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow.AddHours(1))!.Value),
            ("is_archived",NpgsqlDbType.Boolean,true),
            ("tenant_id",NpgsqlDbType.Text,"p25-private-negative-foreign-tenant")};
        foreach(var fault in faults)
            await WithExactAccountFaultAsync(owner,fixture.Approver,fault.Field,fault.Type,fault.Value,async()=> {
                var baseline=await PrincipalFootprintAsync(owner);
                await send(client,HttpMethod.Post,path,fixture.Approver,pendingDecision,HttpStatusCode.Forbidden,commandKey);
                Need(await PrincipalFootprintAsync(owner)==baseline,"Ineligible current account decision changed persisted facts.");
            });

        // Constructor-only owning fixture records: real foreign entity and a real
        // unassigned branch. Never synthesize scope by editing the request or grant.
        Guid otherCompany,otherBranch;
        await using(var core=fixture.Core())
        {
            var company=new LegalEntity {Code="P25_DENIED_SCOPE",RegisteredName="Private denied entity",Currency="ZMW"};
            var branch=new WorkLocation {Code="P25_DENIED_BRANCH",Name="Private unassigned branch",LegalEntityId=fixture.Company};
            core.LegalEntities.Add(company);core.WorkLocations.Add(branch);await core.SaveChangesAsync();
            otherCompany=company.Id;otherBranch=branch.Id;
        }
        foreach(var selected in new[]{(Company:otherCompany,Branch:fixture.Branch),(Company:fixture.Company,Branch:otherBranch)})
        {
            var baseline=await PrincipalFootprintAsync(owner);
            using var request=new HttpRequestMessage(HttpMethod.Post,path);
            request.Headers.Add("X-Private-P25-Subject",fixture.Approver.ToString("D"));
            request.Headers.Add("X-Shell-Entity",selected.Company.ToString("D"));
            request.Headers.Add("X-Shell-Location",selected.Branch.ToString("D"));
            request.Headers.Add("Idempotency-Key",commandKey.ToString("D"));request.Content=JsonContent.Create(pendingDecision);
            using var response=await client.SendAsync(request);countExtraHttp();
            Need(response.StatusCode==HttpStatusCode.Forbidden,"Foreign selected company/branch did not deny the pending command.");
            Need(await PrincipalFootprintAsync(owner)==baseline,"Foreign scope denial changed persisted facts.");
        }
        result["principalAccountScopeNegatives"]=new {
            status="PASS_PRIVATE_CURRENT_CORE_ACCOUNT_SCOPE_DENIALS_ONLY",cases=6,
            actualInactiveLockedArchivedForeignTenantDenied=true,actualForeignCompanyAndUnassignedBranchDenied=true,
            actualPendingDecisionUsed=true,currentCoreFieldsRestoredExactly=true,fullFootprintUnchangedEveryDeniedCommand=true,
            constructorOnlyForeignCoreEntityAndBranchRetained=true,extraHttpCallsOutsideSend=2,
            knownAliasOrLegacyExecutionVerified=false,nativeIdentityProviderVerified=false,externalFinanceAuthority=false};
    }

    private static async Task WithExactAccountFaultAsync(NpgsqlConnection owner,Guid actor,string field,
        NpgsqlDbType type,object changed,Func<Task> deniedAction)
    {
        Need(field is "is_active" or "locked_until" or "is_archived" or "tenant_id","Fixed Core fixture field required.");
        var before=await PrincipalFootprintAsync(owner);
        var changedTenant=field=="tenant_id"?(string)changed:PrincipalApprovalFixture.Tenant;
        async Task<(object Value,string Tenant)> Read()
        {
            await using var read=new NpgsqlCommand($"SELECT {field},tenant_id FROM hrm.local_users WHERE id=@actor AND tenant_id IN (@originalTenant,@changedTenant)",owner);
            read.Parameters.AddWithValue("actor",actor);read.Parameters.AddWithValue("originalTenant",PrincipalApprovalFixture.Tenant);read.Parameters.AddWithValue("changedTenant",changedTenant);
            await using var rows=await read.ExecuteReaderAsync();Need(await rows.ReadAsync(),"Exact known Core fixture account missing.");
            var value=rows.IsDBNull(0)?DBNull.Value:type==NpgsqlDbType.TimestampTz?(object)rows.GetFieldValue<DateTimeOffset>(0):rows.GetValue(0);var tenant=rows.GetString(1);Need(!await rows.ReadAsync(),"Core fixture identity ambiguous.");return(value,tenant);
        }
        async Task Change(object expected,object next,string expectedTenant)
        {
            await using var write=new NpgsqlCommand($"UPDATE hrm.local_users SET {field}=@next WHERE id=@actor AND tenant_id=@tenant AND {field} IS NOT DISTINCT FROM @expected",owner);
            write.Parameters.AddWithValue("actor",actor);write.Parameters.AddWithValue("tenant",expectedTenant);
            write.Parameters.AddWithValue("expected",type,expected);write.Parameters.AddWithValue("next",type,next);
            Need(await write.ExecuteNonQueryAsync()==1,"Exact Core fixture field CAS failed.");
        }
        var original=await Read();Need(original.Tenant==PrincipalApprovalFixture.Tenant&&!Equals(original.Value,changed),"Original eligible account field required.");
        try {await Change(original.Value,changed,original.Tenant);await deniedAction();}
        finally
        {
            var actual=await Read();
            if(Equals(actual.Value,changed)&&actual.Tenant==changedTenant)await Change(changed,original.Value,changedTenant);
            else Need(Equals(actual.Value,original.Value)&&actual.Tenant==original.Tenant,"Unexpected concurrent Core fixture change must not be overwritten.");
            Need(await PrincipalFootprintAsync(owner)==before,"Account restoration or denied decision changed original full footprint.");
        }
    }
}
