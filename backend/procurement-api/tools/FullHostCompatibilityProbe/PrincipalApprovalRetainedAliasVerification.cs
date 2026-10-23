using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    private sealed record AliasAccountState(Guid? WorkerId,bool IsActive);
    private sealed record AliasGraphCounts(int Requests,int Checkers,int Submissions,int AssignmentEvidence,int Cases,int Decisions,int Outcomes,int Receipts,int SuccessorEvents);
    private static async Task<AliasGraphCounts> AliasGraphCountsAsync(PrincipalApprovalFixture fixture)
    {
        await using var db=fixture.Procurement();await using var workflow=fixture.Approvals();
        string[] types=["purchase_request.approval_opened.v2","purchase_request.approval_decision_recorded.v2","purchase_request.approved.v2"];
        return new(await db.PurchaseRequests.CountAsync(),await db.BudgetCheckSnapshots.CountAsync(),await db.ApprovalSubmissions.CountAsync(),
            await db.AssignmentAuthorityEvidence.CountAsync(),await workflow.Cases.CountAsync(),await workflow.Decisions.CountAsync(),
            await db.ApprovalOutcomes.CountAsync(),await db.ApprovalCommandReceipts.CountAsync(),await db.OutboxEvents.CountAsync(x=>types.Contains(x.EventType)));
    }
    // Root-owned opt-in hook AFTER first graph/raw proof, BEFORE authority revocation.
    private static async Task VerifyRetainedAliasOffboardingAsync(PrincipalApprovalFixture fixture,NpgsqlConnection owner,
        HttpClient client,PrincipalSend send,Guid grantId,Dictionary<string,object?> result,Action<string> progress)
    {
        await RequirePrivateRawOwnerAsync(owner,PrincipalApprovalFixture.Tenant,fixture.Company);
        Need(new NpgsqlConnectionStringBuilder(owner.ConnectionString).Host=="/authority-identity-private-socket",
            "Alias fixture requires exact private Unix socket.");
        await using(var identity=new NpgsqlCommand("SELECT current_setting('server_version_num')::integer BETWEEN 170000 AND 179999 AND current_setting('listen_addresses')=''",owner))
            Need(await identity.ExecuteScalarAsync() is true,"Alias fixture requires private PostgreSQL17 without TCP.");
        var originalRows=await AliasOriginalRowsAsync(owner);var countsBefore=await AliasGraphCountsAsync(fixture);
        Need(countsBefore==new AliasGraphCounts(2,1,1,1,1,1,1,2,3),
            "Alias hook must follow exactly the original unavailable and approved graphs before authority revocation.");
        await using var core=fixture.Core();
        var actors=new[]{fixture.Maker,fixture.Checker,fixture.Approver};
        var users=await core.LocalUsers.AsNoTracking().Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&actors.Contains(x.Id)).ToArrayAsync();
        Need(users.Length==3&&users.All(x=>x.WorkerId is null&&x.IsActive&&!x.IsArchived&&x.LockedUntil is null),
            "Alias fixture requires the original three private workerless active accounts.");
        var originals=users.ToDictionary(x=>x.Id,x=>new AliasAccountState(x.WorkerId,x.IsActive));
        var originalUserBytes=users.ToDictionary(x=>x.Id,x=>JsonSerializer.Serialize(x));
        var allowed=originals.ToDictionary(x=>x.Key,x=>new HashSet<AliasAccountState>{x.Value});
        await using(var db=fixture.Procurement())
            Need((await db.SpendingAuthorityGrants.AsNoTracking().SingleAsync(x=>x.Id==grantId)).State=="Active",
                "Alias fixture must run before irreversible authority revocation.");
        Need(!await core.Workers.AnyAsync(),"Alias fixture must follow original workerless graph proof, not alter an existing HR worker.");
        progress("RetainedAliasSyntheticWorkerPreparation");
        var worker=new Worker {EmployeeNo="P25_ALIAS_"+Guid.NewGuid().ToString("N"),FirstName="Synthetic",LastName="Private alias only",
            WorkerType="contingent",Status="active",LocationId=fixture.Branch,SubjectId=null};
        core.Workers.Add(worker);await core.SaveChangesAsync();
        var originalWorker=await core.Workers.AsNoTracking().SingleAsync(x=>x.Id==worker.Id&&x.TenantId==PrincipalApprovalFixture.Tenant);
        var originalWorkerBytes=JsonSerializer.Serialize(originalWorker);var workerUnavailableAttempted=false;
        Exception? firstFailure=null;string? failedPhase=null;var phase="LinkMakerBeforeCapture";var secondary=new List<object>();
        var http=0;Guid requestId=Guid.Empty,caseId=Guid.Empty,assignmentId=Guid.Empty,checkerId=Guid.Empty,decisionId=Guid.Empty;
        string? originalIdentityHash=null;var openKey=Guid.NewGuid();var decisionKey=Guid.NewGuid();
        async Task<JsonElement> Call(HttpMethod method,string path,Guid actor,object? body=null,HttpStatusCode expected=HttpStatusCode.OK,Guid? key=null)
        {http++;return await send(client,method,path,actor,body,expected,key);}
        async Task<AliasAccountState> UserState(Guid id)=>await core.LocalUsers.AsNoTracking()
            .Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==id)
            .Select(x=>new AliasAccountState(x.WorkerId,x.IsActive)).SingleAsync();
        async Task ChangeUser(Guid id,AliasAccountState from,AliasAccountState to)
        {
            Need(allowed.ContainsKey(id)&&allowed[id].Contains(from),"Only declared private actor transitions are supported.");
            // Register the intended state before executing: an ambiguous transport
            // failure may have committed it. Restoration still CAS-checks actual bytes.
            allowed[id].Add(to);
            Need(await core.LocalUsers.Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==id
                &&x.WorkerId==from.WorkerId&&x.IsActive==from.IsActive)
                .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.WorkerId,to.WorkerId).SetProperty(x=>x.IsActive,to.IsActive))==1,
                "Exact private Core actor transition failed.");
        }
        async Task RestoreUser(Guid id)
        {
            var actual=await UserState(id);var original=originals[id];
            Need(allowed[id].Contains(actual),"Unexpected concurrent Core actor state must not be overwritten.");
            if(actual!=original)await ChangeUser(id,actual,original);
            var row=await core.LocalUsers.AsNoTracking().SingleAsync(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==id);
            Need(JsonSerializer.Serialize(row)==originalUserBytes[id],"Original complete Core actor row not restored exactly.");
        }
        async Task NoEffect(Func<Task> action)
        {var before=await PrincipalFootprintAsync(owner);await action();Need(await PrincipalFootprintAsync(owner)==before,"Alias negative or detail GET changed full persisted footprint.");}
        async Task WithTemporaryAlias(Guid actor,Func<Task> action)
        {
            var before=await PrincipalFootprintAsync(owner);var from=await UserState(actor);var to=new AliasAccountState(worker.Id,from.IsActive);
            Need(from==originals[actor],"Alias negative must start at the unchanged independent account.");
            Exception? primary=null;var diagnostics=new List<object>();
            try{await ChangeUser(actor,from,to);await action();}
            catch(Exception error){primary=error;}
            finally
            {
                try{await RestoreUser(actor);}catch(Exception error){if(primary is null)primary=error;else diagnostics.Add(new {check="RestoreTemporaryActor",kind=AliasFailureKind(error)});}
                try{Need(await PrincipalFootprintAsync(owner)==before,"Temporary alias restoration changed full original footprint.");}
                catch(Exception error){if(primary is null)primary=error;else diagnostics.Add(new {check="TemporaryAliasFootprint",kind=AliasFailureKind(error)});}
            }
            if(primary is not null)
            {
                result["principalRetainedAliasSecondaryFailure"]=new {stage="TemporaryAlias",actor,kind=AliasFailureKind(primary),secondary=diagnostics};
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
            }
        }
        try
        {
            await ChangeUser(fixture.Maker,originals[fixture.Maker],new(worker.Id,true));
            progress("ActualAliasSourceCapturedBeforeOffboarding");phase="CreateAndSubmitAliasSource";
            var created=await Call(HttpMethod.Post,"/api/procurement/v1/requests",fixture.Maker,new {
                purpose="Private retained original alias and offboarding proof",neededBy=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),currencyCode="ZMW",
                lines=new[]{new {description="Private actual alias source",unitCode="each",requestedQuantity="2",estimatedUnitPrice="5.1234",categoryCode="PRIVATE_P25"}}},HttpStatusCode.Created);
            requestId=created.GetProperty("id").GetGuid();var path="/api/procurement/v1/requests/"+requestId;
            var submitted=await Call(HttpMethod.Post,path+"/submit",fixture.Maker,new {version=created.GetProperty("version").GetInt64()});
            await using(var db=fixture.Procurement())
            {
                var request=await db.PurchaseRequests.AsNoTracking().SingleAsync(x=>x.Id==requestId);
                Need(PurchaseRequestPrincipalIdentity.Valid(request)&&request.RequesterIdentitySchemaVersion==1
                    &&request.RequesterPrincipalId==fixture.Maker&&request.RequesterCoreWorkerId==worker.Id,
                    "Actual create must capture original private Core alias before offboarding.");
                originalIdentityHash=request.RequesterIdentitySha256;
            }
            phase="CaptureIndependentChecker";
            var checkedResult=await Call(HttpMethod.Post,path+"/budget-checks",fixture.Checker,new {
                requestVersion=submitted.GetProperty("version").GetInt64(),financeCheckRef="PRIVATE-ALIAS-MANUAL-NOT-EXTERNAL-FINANCE",
                status="Available",checkedAmount="10.2468",currencyCode="ZMW",evidenceRef="private-fixture:alias-manual-attestation-only",
                decisionReason="Synthetic private recorded manual evidence, no Finance owner",
                reservationRef="PRIVATE-RECORDED-RESERVATION-NOT-OWNER-VERIFIED",reservationStatus="Confirmed"},HttpStatusCode.Created);
            checkerId=checkedResult.GetProperty("id").GetGuid();
            Need(checkedResult.GetProperty("checkerIdentitySchemaVersion").GetInt32()==2&&checkedResult.GetProperty("checkedByWorkerId").ValueKind==JsonValueKind.Null,
                "Alias fixture checker must be actual independent workerless principal2.");
            // Remove current maker linkage AND HR availability; immutable original
            // requester alias must still deny another account's known same person.
            phase="OffboardRetainedWorkerAndClearCurrentMakerLink";
            await ChangeUser(fixture.Maker,new(worker.Id,true),originals[fixture.Maker]);
            workerUnavailableAttempted=true;
            Need(await core.Workers.Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==worker.Id&&x.Status=="active"&&!x.IsArchived)
                .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"terminated").SetProperty(x=>x.IsArchived,true))==1,
                "Exact private worker offboarding transition failed.");
            JsonElement current=default;await NoEffect(async()=>current=await Call(HttpMethod.Get,path,fixture.Maker));
            var openBody=new {expectedRequestVersion=current.GetProperty("version").GetInt64()};
            phase="CheckerKnownAliasDeniedAtOpen";progress("ActualRetainedCheckerAliasDenial");
            await WithTemporaryAlias(fixture.Checker,()=>NoEffect(async()=>
            {
                var denied=await Call(HttpMethod.Post,path+"/approval-cases",fixture.Maker,openBody,HttpStatusCode.Conflict,openKey);
                Need(denied.GetProperty("code").GetString()=="FinanceEvidenceVerifierInvalid","Checker alias must fail exact principal independence guard, not an unrelated prerequisite.");
            }));
            phase="OpenIndependentAliasGraph";
            var opened=await Call(HttpMethod.Post,path+"/approval-cases",fixture.Maker,openBody,HttpStatusCode.Created,openKey);
            caseId=opened.GetProperty("caseId").GetGuid();var detailPath="/api/procurement/v1/approvals/"+caseId;
            JsonElement detail=default;
            await NoEffect(async()=>detail=await Call(HttpMethod.Get,detailPath,fixture.Approver));
            var assignment=detail.GetProperty("stages").EnumerateArray().Single().GetProperty("assignments").EnumerateArray().Single();
            assignmentId=assignment.GetProperty("id").GetGuid();
            Need(assignment.GetProperty("principalId").GetString()==fixture.Approver.ToString("D")
                &&assignment.GetProperty("allowedActions").EnumerateArray().Any(x=>x.GetString()=="Approve"),"Independent actual assignment must be eligible before alias mutation.");
            var decisionBody=new {action="Approve",reason="Independent private principal after original maker offboarding",
                expectedCaseVersion=detail.GetProperty("caseVersion").GetInt64(),expectedAssignmentVersion=assignment.GetProperty("version").GetInt64(),
                expectedRequestVersion=detail.GetProperty("requestVersion").GetInt64()};
            var decisionPath=$"/api/procurement/v1/approvals/{caseId}/assignments/{assignmentId}/decisions";
            phase="AssignedApproverKnownAliasDenied";progress("ActualRetainedAssignedAliasDenial");
            await WithTemporaryAlias(fixture.Approver,async()=>
            {
                await NoEffect(async()=>
                {
                    var aliasDetail=await Call(HttpMethod.Get,detailPath,fixture.Approver);
                    var aliasAssignment=aliasDetail.GetProperty("stages").EnumerateArray().Single().GetProperty("assignments").EnumerateArray().Single();
                    Need(!aliasAssignment.GetProperty("allowedActions").EnumerateArray().Any()
                        &&aliasDetail.GetProperty("blockers").EnumerateArray().Any(x=>x.GetString()=="ApprovalIdentityChanged"),
                        "Alias detail must expose exact identity blocker and no action.");
                });
                await NoEffect(async()=>
                {
                    var denied=await Call(HttpMethod.Post,decisionPath,fixture.Approver,decisionBody,HttpStatusCode.Conflict,decisionKey);
                    Need(denied.GetProperty("code").GetString()=="ApprovalIdentityChanged","Assigned alias must fail exact retained/current independence before authority re-evaluation.");
                });
            });
            phase="OriginalMakerAccountOffboardedIndependentDecision";progress("ActualHistoricalMakerOffboardIndependentDecision");
            await ChangeUser(fixture.Maker,originals[fixture.Maker],new(null,false));
            await NoEffect(async()=>
            {
                var independentDetail=await Call(HttpMethod.Get,detailPath,fixture.Approver);
                var independentAssignment=independentDetail.GetProperty("stages").EnumerateArray().Single().GetProperty("assignments").EnumerateArray().Single();
                Need(independentAssignment.GetProperty("allowedActions").EnumerateArray().Any(x=>x.GetString()=="Approve")
                    &&independentDetail.GetProperty("caseVersion").GetInt64()==decisionBody.expectedCaseVersion
                    &&independentDetail.GetProperty("requestVersion").GetInt64()==decisionBody.expectedRequestVersion
                    &&independentAssignment.GetProperty("version").GetInt64()==decisionBody.expectedAssignmentVersion,
                    "Historical maker inactivity must not revoke independent current principal authority or advance CAS.");
            });
            var decided=await Call(HttpMethod.Post,decisionPath,fixture.Approver,decisionBody,HttpStatusCode.OK,decisionKey);
            decisionId=decided.GetProperty("decisionId").GetGuid();
            Need(decided.GetProperty("state").GetString()=="Approved"&&decided.GetProperty("requestState").GetString()=="Approved",
                "Actual independent approval with historical maker inactive failed.");
            phase="VerifyOwnedAliasGraph";
            await using(var db=fixture.Procurement())
            await using(var approvals=fixture.Approvals())
            {
                var request=await db.PurchaseRequests.AsNoTracking().Include(x=>x.Lines).SingleAsync(x=>x.Id==requestId);
                var pin=await db.ApprovalSubmissions.AsNoTracking().SingleAsync(x=>x.PurchaseRequestId==requestId);
                var assigned=await db.AssignmentAuthorityEvidence.AsNoTracking().SingleAsync(x=>x.SubmissionId==pin.Id);
                var checker=await db.BudgetCheckSnapshots.AsNoTracking().SingleAsync(x=>x.Id==checkerId);
                var outcome=await db.ApprovalOutcomes.AsNoTracking().SingleAsync(x=>x.SubmissionId==pin.Id);
                var decision=await approvals.Decisions.AsNoTracking().SingleAsync(x=>x.CaseId==caseId);
                var actualCase=await approvals.Cases.AsNoTracking().SingleAsync(x=>x.Id==caseId);
                Need(actualCase.SourceId==requestId&&actualCase.SourceVersion==pin.SubmittedBusinessVersion
                    &&actualCase.MakerSubjectId==fixture.Maker.ToString("D")&&actualCase.State=="Approved",
                    "Actual Core case must retain original maker and exact submitted source identity.");
                Need(PurchaseRequestPrincipalIdentity.Valid(request)&&request.RequesterCoreWorkerId==worker.Id&&request.RequesterIdentitySha256==originalIdentityHash
                    &&pin.SnapshotSchemaVersion==3&&pin.RequesterWorkerId==worker.Id&&pin.MakerWorkerId is null
                    &&pin.RequesterIdentitySha256==originalIdentityHash&&pin.ManualEvidenceAuthorityRef==PrivateManualPolicyReference
                    &&pin.FinanceEvidenceMode==PurchaseRequestApprovalPinning.ApprovedManualEvidence,
                    "Immutable original alias/manual evidence source changed after current link or HR offboarding.");
                Need(checker.CheckerIdentitySchemaVersion==2&&checker.CheckedByWorkerId is null&&checker.CheckedBySubjectId==fixture.Checker.ToString("D")
                    &&FinanceCheckerIdentityEvidence.HasValidBinding(checker)&&assigned.IdentitySchemaVersion==2&&assigned.AssigneeWorkerId is null
                    &&assigned.GrantId==grantId&&assigned.CoreAssignmentId==assignmentId,
                    "Independent checker/authority/assignment lineage was rewritten by alias negatives.");
                Need(decision.Id==decisionId&&decision.Action=="Approve"&&decision.ActorSubjectId==fixture.Approver.ToString("D")
                    &&outcome.CoreDecisionId==decision.Id&&outcome.Outcome=="Approved"&&request.Status=="Approved"
                    &&request.Lines.All(x=>x.ApprovalStatus=="Approved"&&x.ApprovedQuantity==x.RequestedQuantity),"Alias fixture final actual decision/outcome/lines differ.");
                var receipts=await db.ApprovalCommandReceipts.AsNoTracking().Where(x=>x.PurchaseRequestId==requestId).ToArrayAsync();
                Need(receipts.Length==2&&receipts.Any(x=>x.Operation=="Open"&&x.CommandKey==openKey&&x.ActorSubjectId==fixture.Maker.ToString("D")&&x.ResponseJson==opened.GetRawText())
                    &&receipts.Any(x=>x.Operation=="Decide"&&x.CommandKey==decisionKey&&x.ActorSubjectId==fixture.Approver.ToString("D")&&x.ResponseJson==decided.GetRawText()),
                    "Denied alias commands persisted receipts or exact successful command acknowledgements changed.");
                string[] expectedEvents=["purchase_request.approval_opened.v2","purchase_request.approval_decision_recorded.v2","purchase_request.approved.v2"];
                var events=await db.OutboxEvents.AsNoTracking().Where(x=>x.AggregateId==requestId&&expectedEvents.Contains(x.EventType)).ToArrayAsync();
                Need(events.Length==3&&events.Select(x=>x.EventType).Distinct().Count()==3&&events.All(x=>x.EventVersion==2)
                    &&!await db.FinanceReleaseRequests.AnyAsync()&&!await db.FinanceReleaseConfirmations.AnyAsync()
                    &&!await db.FinanceInvoiceHandoffs.AnyAsync()&&!await db.FinancePaymentStatusRefs.AnyAsync(),"Alias proof fabricated Finance effects or lost successor events.");
                var evaluation=await db.PolicyEvaluations.AsNoTracking().SingleAsync(x=>x.Id==pin.PolicyEvaluationId);
                var policy=await db.PolicyVersions.AsNoTracking().SingleAsync(x=>x.Id==pin.PolicyVersionId);
                Need(PurchaseRequestApprovalPinning.VerifyBusinessEvidence(pin,request,evaluation,policy,checker,PrincipalApprovalFixture.Tenant,fixture.Company).IsValid,
                    "Actual final alias source business evidence no longer verifies.");
            }
            Need(http==11,"Alias fixture exact additional HTTP count changed.");
        }
        catch(Exception error){firstFailure=error;failedPhase=phase;}
        finally
        {
            async Task Check(string check,Func<Task> action)
            {
                try{await action();}
                catch(Exception error){if(firstFailure is null){firstFailure=error;failedPhase=check;}else secondary.Add(new {check,kind=AliasFailureKind(error)});}
            }
            foreach(var actor in actors)await Check("RestorePrivateCoreActor",()=>RestoreUser(actor));
            await Check("RestoreSyntheticWorker",async()=>
            {
                var actual=await core.Workers.AsNoTracking().SingleAsync(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==worker.Id);
                if(workerUnavailableAttempted&&actual.Status=="terminated"&&actual.IsArchived)
                    Need(await core.Workers.Where(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==worker.Id&&x.Status=="terminated"&&x.IsArchived)
                        .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,originalWorker.Status).SetProperty(x=>x.IsArchived,originalWorker.IsArchived))==1,
                        "Exact synthetic Worker restoration failed.");
                else Need(actual.Status==originalWorker.Status&&actual.IsArchived==originalWorker.IsArchived,"Unexpected Worker state must not be overwritten.");
                Need(JsonSerializer.Serialize(await core.Workers.AsNoTracking().SingleAsync(x=>x.TenantId==PrincipalApprovalFixture.Tenant&&x.Id==worker.Id))==originalWorkerBytes,
                    "Complete newly synthetic Worker row not restored to declared baseline.");
            });
            await Check("PreserveAllPreexistingRows",async()=>
            {
                var after=await AliasOriginalRowsAsync(owner);
                foreach(var (table,hashes) in originalRows)
                    Need(after.TryGetValue(table,out var actual)&&hashes.GroupBy(x=>x).All(g=>actual.Count(x=>x==g.Key)>=g.Count()),
                        "Alias fixture changed a preexisting business Core audit history receipt or evidence row.");
            });
        }
        if(firstFailure is not null)
        {
            result["principalRetainedAliasFailure"]=new {status="FAILED_RETAIN_FIXTURE",failedPhase,kind=AliasFailureKind(firstFailure),secondary,actualHttpCalls=http};
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstFailure).Throw();
        }
        var countsAfter=await AliasGraphCountsAsync(fixture);
        Need(countsAfter==new AliasGraphCounts(3,2,2,2,2,2,2,4,6),"Alias hook retained global graph counts changed unexpectedly.");
        result["principalRetainedAliasOffboardingVerification"]=new {globalGraphCountsAtHookBefore=countsBefore,globalGraphCountsAtHookAfter=countsAfter,status="PASS_PRIVATE_RETAINED_ALIAS_OFFBOARDING_ONLY",actualHttpCalls=http,
            workerId=worker.Id,requestId,caseId,assignmentId,checkerId,decisionId,originalRequestIdentityHash=originalIdentityHash,
            actualCoreLinkCapturedBeforeRequest=true,checkerAliasExactDenial=true,assignedAliasExactDenial=true,aliasPreviewNoActions=true,
            currentMakerLinkCleared=true,retainedAliasSurvivesInactiveArchivedWorker=true,historicalInactiveMakerIndependentApproval=true,
            fullFootprintUnchangedEachDeniedCommandAndDetail=true,temporaryCoreStatesRestoredExactly=true,allPreexistingRowsRetainedExactly=true,
            newSyntheticWorkerRetained=true,newGraphRetained=true,existingGrantNotReactivated=true,privateManualEvidenceOnly=true,
            externalFinanceAuthority=false,nativeIdentityProviderVerified=false,legacyApprovalExecutionVerified=false,productionTouched=false};
    }
    private static string AliasFailureKind(Exception error)=>error switch
    {PrivatePrincipalProbeFailure=>"Verification",PostgresException=>"PostgreSql",NpgsqlException=>"Transport",OperationCanceledException=>"Cancellation",_=>"Unexpected"};
    private static async Task<Dictionary<string,string[]>> AliasOriginalRowsAsync(NpgsqlConnection owner)
    {
        // Preserve exact original row hashes, allowing ONLY additional synthetic
        // records. An update/delete of any original ID removes its original hash.
        var tables=new List<(string Schema,string Table)>();
        await using(var query=new NpgsqlCommand("SELECT n.nspname,c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind IN ('r','p') AND n.nspname NOT LIKE 'pg_%' AND n.nspname<>'information_schema' ORDER BY 1,2",owner))
        await using(var reader=await query.ExecuteReaderAsync())while(await reader.ReadAsync())tables.Add((reader.GetString(0),reader.GetString(1)));
        var rows=new Dictionary<string,string[]>(StringComparer.Ordinal);
        foreach(var (schema,table) in tables)
        {
            var values=new List<string>();
            await using var query=new NpgsqlCommand("SELECT encode(sha256(convert_to(to_jsonb(t)::text,'UTF8')),'hex') FROM "+PrincipalApprovalRuntimePrivileges.Q(schema)+"."+PrincipalApprovalRuntimePrivileges.Q(table)+" t ORDER BY 1",owner);
            await using var reader=await query.ExecuteReaderAsync();while(await reader.ReadAsync())values.Add(reader.GetString(0));
            rows.Add(schema+"."+table,values.ToArray());
        }
        return rows;
    }
}
