using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    internal const string PrivateManualPolicyReference="PRIVATE-P25-MANUAL-EVIDENCE-NOT-EXTERNAL-FINANCE";
    private static async Task VerifyPrivateManualApprovalAsync(PrincipalApprovalFixture fixture,string owner,
        PrincipalApprovalRuntimePrivileges.Connections roles,NpgsqlConnection ownerConnection,Dictionary<string,object?> result,
        Action<string> stage,PrincipalSend send)
    {
        // Separate opt-in phase AFTER the real default-off and Finance-unavailable denials.
        // Only existing manual-evidence configuration is enabled; no Finance owner is substituted.
        var negativeRequestId=(Guid)result["requestId"]!;
        result["negativeOnlyRequestId"]=negativeRequestId;
        async Task<string> NegativeSourceAsync()
        {
            await using var before=fixture.Procurement();
            var source=await before.PurchaseRequests.AsNoTracking().SingleAsync(x=>x.Id==negativeRequestId);
            var lines=await before.PurchaseRequestLines.AsNoTracking().Where(x=>x.PurchaseRequestId==negativeRequestId).OrderBy(x=>x.Id).ToArrayAsync();
            return JsonSerializer.Serialize(new {source,lines});
        }
        var retainedNegativeSource=await NegativeSourceAsync();
        stage("PrivateManualEvidenceHost");
        await using var host=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,true,true);
        host.PrepareEntryPointEnvironment();using var client=host.CreateClient(new(){AllowAutoRedirect=false});
        var options=host.Services.GetRequiredService<ProcurementApprovalExecutionOptions>();
        Need(options.ManualFinancePolicies.Length==1&&options.HasCurrentManualEvidencePolicy(fixture.Company,DateTimeOffset.UtcNow)
            &&options.ManualFinancePolicies.Single().ApprovedPolicyReference==PrivateManualPolicyReference,"Explicit private manual evidence policy missing.");
        var metadata=await send(client,HttpMethod.Get,"/api/procurement/v1/meta",fixture.Maker,null,HttpStatusCode.OK);
        Need(metadata.GetProperty("capabilities").GetProperty("principalApprovals").GetBoolean(),"Actual configured finite execution capability unavailable.");

        stage("ActualIndependentAuthorityConfiguration");
        const string authorityRoot="/api/procurement/v1/configuration/spending-authorities";
        var authority=await send(client,HttpMethod.Post,authorityRoot,fixture.ConfigMaker,new {
            authorityReference="PRIVATE_P25_CUSTOM_CORE_APPROVER",principalId=fixture.Approver.ToString("D"),action="PurchaseRequest.Approve",
            draft=new {currencyCode="ZMW",amountBasis="EstimatedLineBase",maximumAmount="100.0000",effectiveFrom=DateTimeOffset.UtcNow.AddMinutes(-1),
                effectiveUntil=DateTimeOffset.UtcNow.AddHours(1),allBranches=false,branchRef=fixture.Branch,allDepartments=true,departmentRef=(Guid?)null,
                allCategories=true,categoryCodes=Array.Empty<string>()}},HttpStatusCode.Created);
        var grantId=authority.GetProperty("id").GetGuid();var authorityPath=authorityRoot+"/"+grantId;
        var submittedAuthority=await send(client,HttpMethod.Post,authorityPath+"/submit",fixture.ConfigMaker,
            new {version=authority.GetProperty("version").GetInt64()},HttpStatusCode.OK);
        var activated=await send(client,HttpMethod.Post,authorityPath+"/activate",fixture.ConfigReviewer,
            new {version=submittedAuthority.GetProperty("version").GetInt64(),reason="Private independent finite Core authority review"},HttpStatusCode.OK);
        Need(activated.GetProperty("state").GetString()=="Active"&&activated.GetProperty("principalId").GetString()==fixture.Approver.ToString("D"),"Actual independent authority activation failed.");

        stage("ActualPolicyCreateSubmitPublish");
        const string policyRoot="/api/procurement/v1/configuration/policies";
        const string rules="""
            {"schemaVersion":1,"transactionType":"PurchaseRequest","currencyCode":"ZMW","amountBasis":"EstimatedLineBase",
            "rules":[{"code":"PRIVATE_CORE_PR","priority":0,"when":{"minimumAmountInclusive":null,"maximumAmountExclusive":null,
            "categoryCodes":[],"purchaseTypes":[],"branchIds":[]},"result":{"budgetReservation":"Required","sourcingMethodCode":null,
            "minimumQuoteCount":null,"approvalStages":[{"code":"CUSTOM_CORE_REVIEW","assignee":"ProcurementManager","mode":"One"}]}}]}
            """;
        // Production publication requires a future start. Wait for that actual instant;
        // never rewrite a published policy or fabricate submission/evaluation timestamps.
        var effectiveFrom=DateTimeOffset.UtcNow.AddSeconds(30);
        var policy=await send(client,HttpMethod.Post,policyRoot,fixture.ConfigMaker,new {policyKey="PURCHASE_REQUEST",effectiveFrom,
            effectiveTo=(DateTimeOffset?)null,rulesJson=rules},HttpStatusCode.Created);
        var policyId=policy.GetProperty("id").GetGuid();var policyPath=policyRoot+"/"+policyId;
        var policySubmitted=await send(client,HttpMethod.Post,policyPath+"/submit-review",fixture.ConfigMaker,
            new {version=policy.GetProperty("version").GetInt64(),reason="Private named Core approval policy review"},HttpStatusCode.OK);
        var published=await send(client,HttpMethod.Post,policyPath+"/publish",fixture.ConfigReviewer,
            new {version=policySubmitted.GetProperty("version").GetInt64(),reason="Private independent rule coverage approved"},HttpStatusCode.OK);
        Need(published.GetProperty("state").GetString()=="Published","Actual policy publication failed.");
        var remaining=effectiveFrom-DateTimeOffset.UtcNow;
        Need(remaining<=TimeSpan.FromSeconds(30),"Unexpected private policy activation wait.");
        if(remaining>TimeSpan.Zero)await Task.Delay(remaining+TimeSpan.FromMilliseconds(50));

        // Draft CRUD permits uncategorized demand, while spending authority requires
        // classified lines. Satisfy that real prerequisite through independent API
        // activation; do not weaken the authority evaluator or seed an active row.
        stage("ActualIndependentCategoryActivation");
        const string categoryRoot="/api/procurement/v1/configuration/categories";
        const string categoryCode="PRIVATE_P25";
        var category=await send(client,HttpMethod.Post,categoryRoot,fixture.ConfigMaker,
            new {code=categoryCode,name="Private approval test category",parentCode=(string?)null,expenseAccountRef=(string?)null},HttpStatusCode.Created);
        var activeCategory=await send(client,HttpMethod.Post,categoryRoot+"/"+category.GetProperty("id").GetGuid()+"/activate",fixture.ConfigReviewer,
            new {version=category.GetProperty("version").GetInt64(),reason="Private independent category review"},HttpStatusCode.OK);
        Need(activeCategory.GetProperty("status").GetString()=="Active","Actual independent category activation failed.");

        stage("ActualPrincipalSourceAndChecker2");
        var created=await send(client,HttpMethod.Post,"/api/procurement/v1/requests",fixture.Maker,new {
            purpose="Private manual evidence principal approval",neededBy=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),currencyCode="ZMW",
            lines=new[]{new {description="Exact Core principal source",unitCode="each",requestedQuantity="2",estimatedUnitPrice="5.1234",categoryCode}}},HttpStatusCode.Created);
        var requestId=created.GetProperty("id").GetGuid();var path="/api/procurement/v1/requests/"+requestId;
        var submitted=await send(client,HttpMethod.Post,path+"/submit",fixture.Maker,new {version=created.GetProperty("version").GetInt64()},HttpStatusCode.OK);
        var checker=await send(client,HttpMethod.Post,path+"/budget-checks",fixture.Checker,new {
            requestVersion=submitted.GetProperty("version").GetInt64(),financeCheckRef="PRIVATE-P25-MANUAL-CHECK-NOT-ERP-FINANCE",
            status="Available",checkedAmount="10.2468",currencyCode="ZMW",evidenceRef="private-fixture:human-recorded-manual-evidence-only",
            decisionReason="Synthetic private manual attestation; no external Finance reservation verified",
            reservationRef="PRIVATE-RECORDED-RESERVATION-NOT-OWNER-VERIFIED",reservationStatus="Confirmed"},HttpStatusCode.Created);
        var checkerId=checker.GetProperty("id").GetGuid();
        Need(checker.GetProperty("checkerIdentitySchemaVersion").GetInt32()==2&&checker.GetProperty("checkedByWorkerId").ValueKind==JsonValueKind.Null,
            "Actual checker2 must retain named workerless Core identity.");
        var readiness=await send(client,HttpMethod.Get,path+"/approval-readiness",fixture.Maker,null,HttpStatusCode.OK);
        result["positiveReadinessBlockers"]=readiness.GetProperty("blockers").EnumerateArray()
            .Select(x=>x.ValueKind==JsonValueKind.String?x.GetString():null)
            .Select(x=>x is {Length:>0 and <=100}&&x.All(char.IsAsciiLetterOrDigit)?x:"UnrecognizedSafeBlocker")
            .Take(32).ToArray();
        Need(readiness.GetProperty("canOpenCase").GetBoolean(),"Actual manual-evidence principal readiness blocked.");
        // Read the actual current CAS independently from retained submitted business identity.
        var current=await send(client,HttpMethod.Get,path,fixture.Maker,null,HttpStatusCode.OK);
        stage("ActualSubmission3Assignment2Open");
        var openVersion=current.GetProperty("version").GetInt64();var openKey=Guid.NewGuid();
        var openBody=new {expectedRequestVersion=openVersion};
        var opened=await send(client,HttpMethod.Post,path+"/approval-cases",fixture.Maker,openBody,HttpStatusCode.Created,openKey);
        var caseId=opened.GetProperty("caseId").GetGuid();
        stage("OpenExactReplayConflictAndCurrentGrantDenial");
        await VerifyExactReplayAndConflictAsync(ownerConnection,client,send,path+"/approval-cases",fixture.Maker,
            openKey,openBody,new {expectedRequestVersion=checked(openVersion+1)},opened,HttpStatusCode.Created);
        await VerifyRevokedCurrentGrantReplayAsync(fixture,ownerConnection,client,send,fixture.Maker,"p25_request_maker",
            "procurement-approval-open",path+"/approval-cases",openKey,openBody);
        stage("ActualAssignedDecisionPreview");
        var previewBaseline=await PrincipalFootprintAsync(ownerConnection);
        var detail=await send(client,HttpMethod.Get,"/api/procurement/v1/approvals/"+caseId,fixture.Approver,null,HttpStatusCode.OK);
        Need(await PrincipalFootprintAsync(ownerConnection)==previewBaseline,"Assigned approval preview changed persisted facts.");
        result["assignedDecisionPreviewBlockers"]=detail.GetProperty("blockers").EnumerateArray()
            .Select(x=>x.ValueKind==JsonValueKind.String?x.GetString():null)
            .Select(x=>x is {Length:>0 and <=100}&&x.All(char.IsAsciiLetterOrDigit)?x:"UnrecognizedSafeBlocker")
            .Take(32).ToArray();
        var assignment=detail.GetProperty("stages").EnumerateArray().Single().GetProperty("assignments").EnumerateArray().Single();
        var assignmentId=assignment.GetProperty("id").GetGuid();
        Need(assignment.GetProperty("principalId").GetString()==fixture.Approver.ToString("D")
            &&assignment.GetProperty("allowedActions").EnumerateArray().Any(x=>x.GetString()=="Approve"),"Actual custom-role finite assignee cannot approve.");
        stage("ActualIndependentCoreDecisionAndOutcome");
        var decideKey=Guid.NewGuid();var decisionPath=$"/api/procurement/v1/approvals/{caseId}/assignments/{assignmentId}/decisions";
        var decisionBody=new {
            action="Approve",reason="Private independent named Core review under explicit manual evidence policy",
            expectedCaseVersion=detail.GetProperty("caseVersion").GetInt64(),expectedAssignmentVersion=assignment.GetProperty("version").GetInt64(),
            expectedRequestVersion=detail.GetProperty("requestVersion").GetInt64()};
        var decided=await send(client,HttpMethod.Post,decisionPath,fixture.Approver,decisionBody,HttpStatusCode.OK,decideKey);
        Need(decided.GetProperty("state").GetString()=="Approved"&&decided.GetProperty("requestState").GetString()=="Approved","Actual independent final approval failed.");
        stage("DecisionExactReplayConflictAndCurrentGrantDenial");
        await VerifyExactReplayAndConflictAsync(ownerConnection,client,send,decisionPath,fixture.Approver,decideKey,
            decisionBody,decisionBody with {reason="Changed body must not recover original command"},decided,HttpStatusCode.OK);
        await VerifyRevokedCurrentGrantReplayAsync(fixture,ownerConnection,client,send,fixture.Approver,"p25_custom_approver",
            "procurement-approval-decide",decisionPath,decideKey,decisionBody);
        result["principalApprovalReplayVerification"]=new {
            status="PASS_PRIVATE_EXACT_POST_REPLAY_AND_CURRENT_GRANT_DENIAL_ONLY",cases=6,
            sameKeyBodyOpenResponseExact=true,sameKeyBodyTerminalDecisionResponseExact=true,changedBodiesConflict=true,
            currentOpenGrantRevocationDenied=true,currentDecisionGrantRevocationDenied=true,
            fullFootprintUnchangedEachCommand=true,currentCoreGrantsRestoredExactly=true,
            commandKeys=new {open=openKey,decide=decideKey},getUuidReceiptRecoveryAvailable=false,
            historicalReadsAreCommandAcknowledgements=false,legacyApprovalExecutionVerified=false,externalFinanceAuthority=false};

        stage("IndependentOwnedGraphReload");
        await using var db=fixture.Procurement();await using var workflow=fixture.Approvals();await using var core=fixture.Core();
        var request=await db.PurchaseRequests.Include(x=>x.Lines).SingleAsync(x=>x.Id==requestId);
        var finance=await db.BudgetCheckSnapshots.SingleAsync(x=>x.Id==checkerId);
        var pin=await db.ApprovalSubmissions.SingleAsync(x=>x.PurchaseRequestId==requestId);
        var assigned=await db.AssignmentAuthorityEvidence.SingleAsync(x=>x.SubmissionId==pin.Id);
        var grantEvidence=await db.Set<SpendingAuthorityIdentityRecord>().Where(x=>x.GrantId==grantId).ToListAsync();
        var origin=grantEvidence.Single(x=>x.Stage=="Created");
        var outcome=await db.Set<PurchaseRequestApprovalOutcome>().SingleAsync(x=>x.SubmissionId==pin.Id);
        var decision=await workflow.Decisions.SingleAsync(x=>x.CaseId==caseId);
        Need(!await core.Workers.AnyAsync()&&request.RequesterPrincipalId==fixture.Maker&&request.RequesterWorkerId is null&&request.RequesterCoreWorkerId is null,
            "Approval introduced an employee prerequisite or changed original identity.");
        Need(finance.CheckerIdentitySchemaVersion==2&&finance.CheckedBySubjectId==fixture.Checker.ToString("D")&&finance.CheckedByWorkerId is null
            &&FinanceCheckerIdentityEvidence.HasValidBinding(finance),"Retained checker2 binding invalid.");
        Need(grantEvidence.Count==3&&grantEvidence.All(x=>x.SchemaVersion==2&&x.RecipientPrincipalId==fixture.Approver.ToString("D")&&x.RecipientWorkerId is null&&x.ActorWorkerId is null)
            &&origin.ActorPrincipalId==fixture.ConfigMaker.ToString("D")&&grantEvidence.Single(x=>x.Stage=="Activated").ActorPrincipalId==fixture.ConfigReviewer.ToString("D"),"Actual authority2 lineage changed.");
        Need(pin.SnapshotSchemaVersion==3&&pin.FinanceIdentitySchemaVersion==2&&pin.FinanceSnapshotId==checkerId&&pin.PolicyVersionId==policyId
            &&pin.FinanceEvidenceMode==PurchaseRequestApprovalPinning.ApprovedManualEvidence&&pin.ManualEvidenceAuthorityRef==PrivateManualPolicyReference
            &&pin.RequesterPrincipalId==fixture.Maker&&pin.MakerWorkerId is null&&pin.RequesterWorkerId is null,"Actual PR3 pin changed source/manual evidence identity.");
        Need(assigned.IdentitySchemaVersion==2&&assigned.CoreAssignmentId==assignmentId&&assigned.PrincipalId==fixture.Approver.ToString("D")
            &&assigned.AssigneeWorkerId is null&&assigned.GrantId==grantId,"Actual assignment2 binding invalid.");
        Need(decision.ActorSubjectId==fixture.Approver.ToString("D")&&decision.Action=="Approve"&&outcome.CoreDecisionId==decision.Id
            &&outcome.Outcome=="Approved"&&outcome.ActorSubjectId==decision.ActorSubjectId&&request.Status=="Approved"
            &&request.Lines.All(x=>x.ApprovalStatus=="Approved"&&x.ApprovedQuantity==x.RequestedQuantity),"Actual Core decision/source outcome not exact.");
        var eventTypes=new[]{"purchase_request.approval_opened.v2","purchase_request.approval_decision_recorded.v2","purchase_request.approved.v2"};
        var events=await db.OutboxEvents.Where(x=>x.AggregateId==requestId&&eventTypes.Contains(x.EventType)).ToListAsync();
        Need(events.Count==3&&events.Select(x=>x.EventType).Distinct().Count()==3&&events.All(x=>x.EventVersion==2),"Principal approval v2 event type/version incomplete.");
        foreach(var message in events)
        {
            using var json=JsonDocument.Parse(message.PayloadJson);var payload=json.RootElement;
            Need(payload.GetProperty("principalApprovalSchemaVersion").GetInt32()==1&&payload.GetProperty("submissionSchemaVersion").GetInt32()==3
                &&payload.GetProperty("submissionId").GetGuid()==pin.Id,"Principal event durable marker does not match actual pin.");
        }
        Need(await db.ApprovalCommandReceipts.CountAsync(x=>x.PurchaseRequestId==requestId)==2
            &&!await db.FinanceReleaseRequests.AnyAsync()&&!await db.FinanceReleaseConfirmations.AnyAsync()
            &&!await db.FinanceInvoiceHandoffs.AnyAsync()&&!await db.FinancePaymentStatusRefs.AnyAsync(),"Approval duplicated receipts or invented Finance release/AP/payment facts.");
        Need(!await core.LocalUsers.AnyAsync(x=>x.RolesCsv=="procurement_manager"),"Private successor test accidentally depends on literal legacy role label.");
        var evaluation=await db.PolicyEvaluations.SingleAsync(x=>x.Id==pin.PolicyEvaluationId);
        var retainedPolicy=await db.PolicyVersions.SingleAsync(x=>x.Id==policyId);
        Need(PurchaseRequestApprovalPinning.VerifyBusinessEvidence(pin,request,evaluation,retainedPolicy,finance,
            PrincipalApprovalFixture.Tenant,fixture.Company).IsValid,"Final retained PR3 business evidence hash verification failed.");
        Need(await NegativeSourceAsync()==retainedNegativeSource,"Positive phase changed original Finance-unavailable source.");
        var graph=new PrincipalApprovalStorageGraph(requestId,checkerId,origin.Id,pin.Id,assigned.Id,PrincipalApprovalFixture.Tenant,
            fixture.Company,pin.SubmittedBusinessVersion,request.Version);
        result["principalApprovalStorageGraph"]=graph;result["positiveApprovalRequestId"]=requestId;result["caseId"]=caseId;
        result["manualEvidencePositiveWorkflow"]=true;result["positiveApprovalGraph"]=true;
        result["checker2Authority2Submission3Assignment2Acceptance"]="ACTUAL_HTTP_GRAPH_AND_INDEPENDENT_OWNER_RELOAD";
        result["manualEvidenceAuthorityReference"]=PrivateManualPolicyReference;result["externalFinanceAuthority"]=false;
        result["status"]="PASS_PRIVATE_P25_MANUAL_PRINCIPAL_APPROVAL_ONLY";
        result["currentGraphRawStorage"]="NOT_RUN";
        if(Environment.GetEnvironmentVariable("P25_HTTP_VERIFY_RAW_GRAPH") is {} raw)
        {
            Need(raw=="1","Private raw graph opt-in must be exactly1.");
            stage("OwnerCurrentGraphRawStorage");
            result["currentGraphRawStorage"]="IN_PROGRESS_NOT_ACCEPTED";
            await VerifyRawStorageAsync(ownerConnection,graph);
            result["currentGraphRawStorage"]=PrincipalRawStorageVerification;
        }
        // Owner-only raw probes do not establish Finance authority or independently
        // isolate the five dependent domain graph families.
    }
}
