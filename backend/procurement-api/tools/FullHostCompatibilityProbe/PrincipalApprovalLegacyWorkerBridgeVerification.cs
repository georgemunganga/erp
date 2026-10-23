using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

internal static partial class PrincipalApprovalHttpVerification
{
    private static async Task VerifyLegacyWorkerBridgeAsync(PrincipalApprovalFixture fixture,string owner,
        PrincipalApprovalRuntimePrivileges.Connections roles,Dictionary<string,object?> result,
        Action<string> stage,PrincipalSend send)
    {
        stage("LegacySyntheticCurrentCoreWorkers");
        var workers=new Dictionary<Guid,Guid>();
        await using(var core=fixture.Core())
        {
            foreach(var (id,label) in new[] {(fixture.Maker,"maker"),(fixture.Checker,"checker"),(fixture.Approver,"approver")})
            {
                var worker=new Worker {EmployeeNo="P25-LEGACY-"+label,FirstName="Synthetic",
                    LastName="Legacy "+label,Status="active",LocationId=fixture.Branch};
                core.Workers.Add(worker);
                var account=await core.LocalUsers.SingleAsync(x=>x.Id==id);
                Need(account.WorkerId is null,"Legacy fixture Core account already linked to an employee.");
                account.WorkerId=worker.Id;
                if(id==fixture.Approver)account.RolesCsv+=",procurement_manager";
                workers.Add(id,worker.Id);
            }
            await core.SaveChangesAsync();
            Need(await core.Workers.CountAsync()==3 && workers.Values.Distinct().Count()==3,
                "Legacy cohort requires only three distinct synthetic active worker links.");
        }

        // The normal false-option host retains the unavailable PRINCIPAL
        // interface. The same physical P25 checker can authorize original
        // worker1 authority records without selecting the new principal2 path.
        stage("LegacyFalseOptionAuthorityAndOriginalSource");
        Guid grantId,policyId,requestId;long submittedVersion;
        await using(var legacy=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,
            actualSchema:true,privateManualEvidence:true,principalExecutionEnabled:false))
        {
            legacy.PrepareEntryPointEnvironment();using var client=legacy.CreateClient(new(){AllowAutoRedirect=false});
            using(var scope=legacy.Services.CreateScope())
            {
                Need(scope.ServiceProvider.GetRequiredService<IProcurementPrincipalApprovalSchemaReadiness>()
                    is UnavailableProcurementPrincipalApprovalSchemaReadiness,
                    "False option must not expose principal1 readiness.");
                var physical=scope.ServiceProvider.GetRequiredService<ProcurementPrincipalApprovalStoreReadiness>();
                await using var record=fixture.Procurement(roles.Record);
                Need(await physical.CanRecordAsync(record,default),
                    "Actual finite P25 record-role schema must admit retained worker1 configuration.");
            }
            var metadata=await send(client,HttpMethod.Get,"/api/procurement/v1/meta",fixture.Maker);
            var capabilities=metadata.GetProperty("capabilities");
            Need(!capabilities.GetProperty("principalApprovals").GetBoolean(),
                "False principal option advertised the separate principal workflow.");
            Need(capabilities.GetProperty("budgetRecording").GetBoolean(),
                "Physical P25 finite role did not enable retained worker funding recording.");
            // This availability is based on the configured manual evidence policy and
            // finite Core/Procurement schema, not the PR policy published below.
            Need(capabilities.GetProperty("approvals").GetBoolean(),
                "Finite execution role did not enable retained worker approval readiness.");
            const string authorityRoot="/api/procurement/v1/configuration/spending-authorities";
            var authority=await send(client,HttpMethod.Post,authorityRoot,fixture.Maker,new {
                authorityReference="PRIVATE_P25_LEGACY_WORKER_APPROVER",principalId=fixture.Approver.ToString("D"),
                action="PurchaseRequest.Approve",
                draft=new {currencyCode="ZMW",amountBasis="EstimatedLineBase",maximumAmount="100.0000",
                    effectiveFrom=DateTimeOffset.UtcNow.AddMinutes(-1),effectiveUntil=DateTimeOffset.UtcNow.AddHours(1),
                    allBranches=false,branchRef=fixture.Branch,allDepartments=true,departmentRef=(Guid?)null,
                    allCategories=true,categoryCodes=Array.Empty<string>()}},HttpStatusCode.Created);
            grantId=authority.GetProperty("id").GetGuid();var authorityPath=authorityRoot+"/"+grantId;
            var submittedGrant=await send(client,HttpMethod.Post,authorityPath+"/submit",fixture.Maker,
                new {version=authority.GetProperty("version").GetInt64()});
            var active=await send(client,HttpMethod.Post,authorityPath+"/activate",fixture.Checker,
                new {version=submittedGrant.GetProperty("version").GetInt64(),reason="Synthetic independent worker-linked grant review"});
            Need(active.GetProperty("state").GetString()=="Active","Legacy authority1 did not activate through real routes.");

            const string policyRoot="/api/procurement/v1/configuration/policies";
            const string rules="""
                {"schemaVersion":1,"transactionType":"PurchaseRequest","currencyCode":"ZMW","amountBasis":"EstimatedLineBase",
                "rules":[{"code":"PRIVATE_WORKER_PR","priority":0,"when":{"minimumAmountInclusive":null,"maximumAmountExclusive":null,
                "categoryCodes":[],"purchaseTypes":[],"branchIds":[]},"result":{"budgetReservation":"Required","sourcingMethodCode":null,
                "minimumQuoteCount":null,"approvalStages":[{"code":"WORKER_REVIEW","assignee":"ProcurementManager","mode":"One"}]}}]}
                """;
            var effective=DateTimeOffset.UtcNow.AddSeconds(30);
            var policy=await send(client,HttpMethod.Post,policyRoot,fixture.ConfigMaker,
                new {policyKey="PURCHASE_REQUEST",effectiveFrom=effective,effectiveTo=(DateTimeOffset?)null,rulesJson=rules},HttpStatusCode.Created);
            policyId=policy.GetProperty("id").GetGuid();var policyPath=policyRoot+"/"+policyId;
            var review=await send(client,HttpMethod.Post,policyPath+"/submit-review",fixture.ConfigMaker,
                new {version=policy.GetProperty("version").GetInt64(),reason="Synthetic worker workflow policy review"});
            var published=await send(client,HttpMethod.Post,policyPath+"/publish",fixture.ConfigReviewer,
                new {version=review.GetProperty("version").GetInt64(),reason="Independent synthetic worker rule publication"});
            Need(published.GetProperty("state").GetString()=="Published","Worker policy did not publish.");
            var wait=effective-DateTimeOffset.UtcNow;
            Need(wait<=TimeSpan.FromSeconds(30),"Unexpected worker policy activation wait.");
            if(wait>TimeSpan.Zero)await Task.Delay(wait+TimeSpan.FromMilliseconds(50));

            const string categoryRoot="/api/procurement/v1/configuration/categories";
            var category=await send(client,HttpMethod.Post,categoryRoot,fixture.ConfigMaker,
                new {code="PRIVATE_P25",name="Synthetic worker approval category",parentCode=(string?)null,
                    expenseAccountRef=(string?)null},HttpStatusCode.Created);
            var activeCategory=await send(client,HttpMethod.Post,categoryRoot+"/"+category.GetProperty("id").GetGuid()+"/activate",
                fixture.ConfigReviewer,new {version=category.GetProperty("version").GetInt64(),reason="Synthetic category review"});
            Need(activeCategory.GetProperty("status").GetString()=="Active","Worker category activation failed.");

            var created=await send(client,HttpMethod.Post,"/api/procurement/v1/requests",fixture.Maker,new {
                purpose="Private retained worker request",neededBy=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),
                currencyCode="ZMW",lines=new[]{new {description="Worker bridge item",unitCode="each",
                    requestedQuantity="2",estimatedUnitPrice="5.1234",categoryCode="PRIVATE_P25"}}},HttpStatusCode.Created);
            requestId=created.GetProperty("id").GetGuid();
            Need(created.GetProperty("requesterWorkerId").GetGuid()==workers[fixture.Maker],
                "Legacy request did not capture the genuine current maker worker.");
            var source=await send(client,HttpMethod.Post,"/api/procurement/v1/requests/"+requestId+"/submit",fixture.Maker,
                new {version=created.GetProperty("version").GetInt64()});
            submittedVersion=source.GetProperty("submittedVersion").GetInt64();
            await using var stored=fixture.Procurement();
            var original=await stored.PurchaseRequests.SingleAsync(x=>x.Id==requestId);
            Need(original.RequesterIdentitySchemaVersion==0 && original.RequesterIdentitySha256 is null
                && original.RequesterWorkerId==workers[fixture.Maker] && original.RequesterCoreWorkerId is null
                && original.Status=="Submitted","False-option original worker request was adopted or altered.");
        }

        stage("LegacyP25FiniteChecker1AndWorkerApproval");
        await using(var active=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,
            actualSchema:true,privateManualEvidence:true,principalExecutionEnabled:false))
        {
            active.PrepareEntryPointEnvironment();using var client=active.CreateClient(new(){AllowAutoRedirect=false});
            var path="/api/procurement/v1/requests/"+requestId;
            var checkedResult=await send(client,HttpMethod.Post,path+"/budget-checks",fixture.Checker,new {
                requestVersion=submittedVersion,financeCheckRef="PRIVATE-P25-WORKER-CHECK-NOT-EXTERNAL-FINANCE",
                status="Available",checkedAmount="10.2468",currencyCode="ZMW",
                evidenceRef="private-fixture:worker-manual-review",
                decisionReason="Synthetic private worker attestation; no external Finance reservation verified",
                reservationRef="PRIVATE-WORKER-RECORDED-NOT-OWNER-VERIFIED",reservationStatus="Confirmed"},HttpStatusCode.Created);
            Need(checkedResult.GetProperty("checkerIdentitySchemaVersion").GetInt32()==1
                && checkedResult.GetProperty("checkedByWorkerId").GetGuid()==workers[fixture.Checker],
                "Actual P25 budget route must retain independent worker1 checker evidence.");
            var readiness=await send(client,HttpMethod.Get,path+"/approval-readiness",fixture.Maker);
            result["legacyApprovalReadiness"]=readiness.Clone();
            Need(readiness.GetProperty("canOpenCase").GetBoolean(),"Real worker readiness blocked after checker1.");
            var current=await send(client,HttpMethod.Get,path,fixture.Maker);
            var opened=await send(client,HttpMethod.Post,path+"/approval-cases",fixture.Maker,
                new {expectedRequestVersion=current.GetProperty("version").GetInt64()},HttpStatusCode.Created);
            var caseId=opened.GetProperty("caseId").GetGuid();
            var detail=await send(client,HttpMethod.Get,"/api/procurement/v1/approvals/"+caseId,fixture.Approver);
            var assignment=detail.GetProperty("stages").EnumerateArray().Single().GetProperty("assignments").EnumerateArray().Single();
            var assignmentId=assignment.GetProperty("id").GetGuid();
            Need(assignment.GetProperty("principalId").GetString()==fixture.Approver.ToString("D")
                &&assignment.GetProperty("allowedActions").EnumerateArray().Any(x=>x.GetString()=="Approve"),
                "Worker-linked procurement manager was not independently assigned.");
            var decided=await send(client,HttpMethod.Post,
                $"/api/procurement/v1/approvals/{caseId}/assignments/{assignmentId}/decisions",fixture.Approver,
                new {action="Approve",reason="Independent synthetic legacy worker review",
                    expectedCaseVersion=detail.GetProperty("caseVersion").GetInt64(),
                    expectedAssignmentVersion=assignment.GetProperty("version").GetInt64(),
                    expectedRequestVersion=detail.GetProperty("requestVersion").GetInt64()});
            Need(decided.GetProperty("state").GetString()=="Approved"
                &&decided.GetProperty("requestState").GetString()=="Approved",
                "Actual worker-linked Core decision did not approve the request.");
            stage("LegacyIndependentOwnerReload");
            await using var db=fixture.Procurement();await using var core=fixture.Core();await using var approval=fixture.Approvals();
            var request=await db.PurchaseRequests.Include(x=>x.Lines).SingleAsync(x=>x.Id==requestId);
            var finance=await db.BudgetCheckSnapshots.SingleAsync(x=>x.PurchaseRequestId==requestId);
            var pin=await db.ApprovalSubmissions.SingleAsync(x=>x.PurchaseRequestId==requestId);
            var evidence=await db.AssignmentAuthorityEvidence.SingleAsync(x=>x.SubmissionId==pin.Id);
            var grant=await db.SpendingAuthorityGrants.SingleAsync(x=>x.Id==grantId);
            var grantLineage=await db.Set<SpendingAuthorityIdentityRecord>()
                .Where(x=>x.GrantId==grantId).ToArrayAsync();
            var coreCase=await approval.Cases.SingleAsync(x=>x.Id==caseId);
            var decisions=await approval.Decisions.Where(x=>x.CaseId==caseId).ToArrayAsync();
            Need(request.Status=="Approved" && request.RequesterIdentitySchemaVersion==0
                &&request.RequesterIdentitySha256 is null &&request.RequesterWorkerId==workers[fixture.Maker]
                &&request.Lines.Single().ApprovedQuantity==2m &&request.Lines.Single().EstimatedUnitPrice==5.1234m
                &&finance.CheckerIdentitySchemaVersion==1&&finance.CheckedByWorkerId==workers[fixture.Checker]
                &&FinanceCheckerIdentityEvidence.HasValidBinding(finance)
                &&pin.SnapshotSchemaVersion==2&&pin.FinanceIdentitySchemaVersion==1
                &&pin.RequesterWorkerId==workers[fixture.Maker]&&pin.MakerWorkerId==workers[fixture.Maker]
                &&pin.PolicyVersionId==policyId&&pin.FinanceSnapshotId==finance.Id
                &&pin.EstimatedAmount==10.2468m&&PurchaseRequestApprovalPinning.HasRetainedWorkerIdentity(pin)
                &&evidence.IdentitySchemaVersion==1&&evidence.AssigneeWorkerId==workers[fixture.Approver]
                &&evidence.GrantId==grantId&&grant.State=="Active"
                &&grantLineage.Length==3&&grantLineage.All(x=>x.SchemaVersion==1
                    &&x.ActorWorkerId is not null&&x.RecipientWorkerId==workers[fixture.Approver])
                &&grantLineage.Single(x=>x.Stage=="Created").ActorWorkerId==workers[fixture.Maker]
                &&grantLineage.Single(x=>x.Stage=="Activated").ActorWorkerId==workers[fixture.Checker]
                &&coreCase.State=="Approved"&&coreCase.SourceId==requestId
                &&decisions.Length==1&&decisions[0].Action=="Approve"
                &&decisions[0].ActorSubjectId==fixture.Approver.ToString("D")
                &&await core.Workers.CountAsync()==3
                &&await db.ApprovalCommandReceipts.CountAsync(x=>x.PurchaseRequestId==requestId)==2,
                "Owner reload did not retain exact original worker1/checker1/submission2/assignment1/Core decision.");
            Need(!await db.FinanceInvoiceHandoffs.AnyAsync()&&!await db.FinancePaymentStatusRefs.AnyAsync(),
                "Legacy worker bridge must not invent AP or payment facts.");
            result["legacyWorkerBridge"]=new {status="PASS_PRIVATE_P25_WORKER_BRIDGE_ONLY",requestId,grantId,
                checkerId=finance.Id,submissionId=pin.Id,assignmentEvidenceId=evidence.Id,caseId,
                currentCoreWorkerLinks=3,originalWorkerIdentityRetained=true,checker1=true,
                authority1=true,submission2=true,assignment1=true,independentCoreDecision=true,
                externalFinanceAuthority=false};
            result["status"]="PASS_PRIVATE_P25_WORKER_BRIDGE_ONLY";
        }
    }
}
