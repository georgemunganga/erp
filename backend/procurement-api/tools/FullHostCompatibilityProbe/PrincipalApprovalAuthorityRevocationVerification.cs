using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    // Hook LAST, after the first approved graph's owner/GET/replay/raw proof. This
    // intentionally retains a second genuine pending graph and a revoked authority;
    // revocation is not reversible and is never undone with owning SQL.
    internal static async Task VerifyAuthorityRevocationDenialAsync(PrincipalApprovalFixture fixture,NpgsqlConnection owner,
        HttpClient client,PrincipalSend send,Guid originalRequestId,Guid originalOpenKey,JsonElement originalOpened,
        Guid grantId,Dictionary<string,object?> result)
    {
        await RequirePrivateRawOwnerAsync(owner,PrincipalApprovalFixture.Tenant,fixture.Company);
        Need(grantId!=Guid.Empty&&originalRequestId!=Guid.Empty&&originalOpenKey!=Guid.Empty,"Real retained authority and original command required.");
        var created=await send(client,HttpMethod.Post,"/api/procurement/v1/requests",fixture.Maker,new {
            purpose="Private current authority revocation negative",neededBy=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),currencyCode="ZMW",
            lines=new[]{new {description="Real second pending source",unitCode="each",requestedQuantity="2",estimatedUnitPrice="5.1234",categoryCode="PRIVATE_P25"}}},HttpStatusCode.Created);
        var requestId=created.GetProperty("id").GetGuid();var path="/api/procurement/v1/requests/"+requestId;
        var submitted=await send(client,HttpMethod.Post,path+"/submit",fixture.Maker,new {version=created.GetProperty("version").GetInt64()},HttpStatusCode.OK);
        await send(client,HttpMethod.Post,path+"/budget-checks",fixture.Checker,new {
            requestVersion=submitted.GetProperty("version").GetInt64(),financeCheckRef="PRIVATE-P25-REVOCATION-MANUAL-NOT-EXTERNAL-FINANCE",
            status="Available",checkedAmount="10.2468",currencyCode="ZMW",evidenceRef="private-fixture:manual-revocation-prerequisite",
            decisionReason="Private recorded manual attestation only; no Finance owner",
            reservationRef="PRIVATE-RECORDED-RESERVATION-NOT-OWNER-VERIFIED",reservationStatus="Confirmed"},HttpStatusCode.Created);
        var current=await send(client,HttpMethod.Get,path,fixture.Maker);
        var opened=await send(client,HttpMethod.Post,path+"/approval-cases",fixture.Maker,new {
            expectedRequestVersion=current.GetProperty("version").GetInt64()},HttpStatusCode.Created);
        var caseId=opened.GetProperty("caseId").GetGuid();
        var detail=await send(client,HttpMethod.Get,"/api/procurement/v1/approvals/"+caseId,fixture.Approver);
        var assignment=detail.GetProperty("stages").EnumerateArray().Single().GetProperty("assignments").EnumerateArray().Single();
        Need(assignment.GetProperty("principalId").GetString()==fixture.Approver.ToString("D")
            &&assignment.GetProperty("allowedActions").EnumerateArray().Any(x=>x.GetString()=="Approve"),"Second genuine pending assignment must be eligible before revocation.");
        var assignmentId=assignment.GetProperty("id").GetGuid();
        await using(var db=fixture.Procurement())
        {
            var pin=await db.ApprovalSubmissions.AsNoTracking().SingleAsync(x=>x.PurchaseRequestId==requestId);
            var bound=await db.AssignmentAuthorityEvidence.AsNoTracking().SingleAsync(x=>x.SubmissionId==pin.Id);
            Need(bound.GrantId==grantId&&bound.CoreAssignmentId==assignmentId,"Second pending graph must use the actual authority being revoked.");
        }
        var authorityRoot="/api/procurement/v1/configuration/spending-authorities/"+grantId;
        var grant=await send(client,HttpMethod.Get,authorityRoot,fixture.ConfigReviewer);
        Need(grant.GetProperty("state").GetString()=="Active","Only actual active authority may enter the revocation lifecycle.");
        // A single explicit private Core action enables the real public command.
        // All other role bytes are retained; the action is restored even on failure.
        await WithPrivateRevocationActionAsync(fixture,async()=> {
            var revoked=await send(client,HttpMethod.Post,authorityRoot+"/revoke",fixture.ConfigReviewer,new {
                version=grant.GetProperty("version").GetInt64(),reason="Private actual authority revocation negative"},HttpStatusCode.OK);
            Need(revoked.GetProperty("state").GetString()=="Revoked","Actual public authority revocation did not persist.");
        });
        // Retained acknowledgement must not demand current spending authority.
        await VerifyActualAcknowledgementAsync(fixture,owner,client,send,originalRequestId,"Open",originalOpenKey,fixture.Maker,originalOpened);
        var before=await PrincipalFootprintAsync(owner);
        var denial=await send(client,HttpMethod.Post,$"/api/procurement/v1/approvals/{caseId}/assignments/{assignmentId}/decisions",fixture.Approver,new {
            action="Approve",reason="Must fail after actual authority revocation",expectedCaseVersion=detail.GetProperty("caseVersion").GetInt64(),
            expectedAssignmentVersion=assignment.GetProperty("version").GetInt64(),expectedRequestVersion=detail.GetProperty("requestVersion").GetInt64()},HttpStatusCode.Conflict);
        // ValidateAssignmentAsync -> ResolveAsync has no eligible active authority;
        // Failure maps this exact InvalidOperationException code to409, not403.
        Need(denial.GetProperty("code").GetString()=="ApprovalAssigneeAuthorityUnavailable","Wrong authority-revocation denial; another guard must not satisfy this test.");
        Need(await PrincipalFootprintAsync(owner)==before,"Denied new decision after authority revocation changed persisted facts.");
        await using(var db=fixture.Procurement())
        await using(var workflow=fixture.Approvals())
        {
            Need((await db.SpendingAuthorityGrants.AsNoTracking().SingleAsync(x=>x.Id==grantId)).State=="Revoked",
                "Revoked authority must remain revoked, never reactivated with owning writes.");
            var retained=await db.PurchaseRequests.AsNoTracking().SingleAsync(x=>x.Id==requestId);
            Need(retained.Status=="Submitted"&&retained.ApprovalState=="Pending"
                &&!await db.ApprovalOutcomes.AnyAsync(x=>x.PurchaseRequestId==requestId)
                &&!await workflow.Decisions.AnyAsync(x=>x.CaseId==caseId)
                &&await db.ApprovalCommandReceipts.CountAsync(x=>x.PurchaseRequestId==requestId)==1,
                "Authority denial must retain exactly the second pending graph's original Open receipt and no decision/outcome.");
        }
        result["principalAuthorityRevocationVerification"]=new {
            status="PASS_PRIVATE_ACTUAL_AUTHORITY_REVOCATION_ONLY",historicalOriginalOpenAcknowledgement200=true,
            actualNewDecision409AuthorityUnavailable=true,deniedDecisionFullFootprintUnchanged=true,
            temporaryCoreRevokeActionRestoredExactly=true,secondPendingGraphRetained=true,revokedGrantRemainsRevoked=true,
            secondRequestId=requestId,secondCaseId=caseId,secondAssignmentId=assignmentId,revokedGrantId=grantId,
            secondGraphUsesRecordedPrivateManualEvidence=true,knownAliasOrLegacyExecutionVerified=false,
            nativeIdentityProviderVerified=false,externalFinanceAuthority=false};
    }

    private static async Task WithPrivateRevocationActionAsync(PrincipalApprovalFixture fixture,Func<Task> command)
    {
        await using var core=fixture.Core();
        var user=await core.LocalUsers.AsNoTracking().SingleAsync(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==fixture.ConfigReviewer);
        const string key="p25_config_reviewer";var permission=SpendingAuthorityConfiguration.RevokePermission;
        Need(user.RolesCsv.Split(',').Contains(key,StringComparer.Ordinal),"Actual independent reviewer role required.");
        var role=await core.TenantRoleAssignments.AsNoTracking().SingleAsync(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.RoleKey==key&&x.Active&&!x.IsArchived);
        var original=role.PermissionsCsv;Need(!original.Split(',').Contains(permission,StringComparer.Ordinal),"Revocation action must be a new explicit private fixture prerequisite.");
        var extended=original+","+permission;var before=JsonSerializer.Serialize(role);
        try
        {
            Need(await core.TenantRoleAssignments.Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==role.Id&&x.PermissionsCsv==original)
                .ExecuteUpdateAsync(set=>set.SetProperty(x=>x.PermissionsCsv,extended))==1,"Exact private Core revoke action grant failed.");
            await command();
        }
        finally
        {
            var actual=await core.TenantRoleAssignments.AsNoTracking().SingleAsync(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==role.Id);
            if(actual.PermissionsCsv==extended)
                Need(await core.TenantRoleAssignments.Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==role.Id&&x.PermissionsCsv==extended)
                    .ExecuteUpdateAsync(set=>set.SetProperty(x=>x.PermissionsCsv,original))==1,"Original private reviewer role could not be restored.");
            else Need(actual.PermissionsCsv==original,"Unexpected concurrent reviewer grant must not be overwritten.");
            Need(JsonSerializer.Serialize(await core.TenantRoleAssignments.AsNoTracking().SingleAsync(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==role.Id))==before,
                "Temporary public-revocation prerequisite changed other Core role fields.");
        }
    }
}
