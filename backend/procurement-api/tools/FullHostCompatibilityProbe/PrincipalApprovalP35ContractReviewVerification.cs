using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    private sealed record P35ContractReviewEvidence(Guid ContractId,Guid SupplierId,Guid PolicyId,
        Guid FirstSubmissionId,Guid SecondSubmissionId,Guid FirstSubmitKey,Guid CorrectionKey,Guid SecondSubmitKey);
    // Exclusive private native cohort. All positive policy/contract/approval facts
    // are produced through actual HTTP and the real shared Core approval engine.
    private static async Task<P35ContractReviewEvidence> VerifyPrincipalContractReviewAsync(PrincipalApprovalFixture fixture,string owner,
        PrincipalApprovalRuntimePrivileges.Connections roles,Dictionary<string,object?> result,
        Action<string> stage,PrincipalSend send,Guid supplierId)
    {
        Need(fixture.PrincipalPoProfile&&fixture.PrincipalContractReviewProfile&&supplierId!=Guid.Empty,
            "P35 requires its genuine supplier and separate principal PO source cohort.");
        await using var host=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,true,true);
        host.PrepareEntryPointEnvironment();using var client=host.CreateClient(new(){AllowAutoRedirect=false});
        using(var registration=host.Services.CreateScope())
        {
            var requestScope=registration.ServiceProvider.GetRequiredService<ProcurementRequestScope>();
            requestScope.Set(PrincipalApprovalFixture.Tenant,fixture.Company,fixture.Buyer.ToString("D"),fixture.Branch,null,true,"p35-native-readiness");
            requestScope.AssignPrincipal(fixture.Buyer);
            var options=registration.ServiceProvider.GetRequiredService<ProcurementContractReviewOptions>();
            var ready=registration.ServiceProvider.GetRequiredService<IProcurementContractReviewSchemaReadiness>();
            Need(options.Enabled&&options.ExecutionDatabaseConfigured&&ready is ProcurementContractReviewStoreReadiness
                &&await ready.IsConfiguredAsync(default),"P35 requires actual captured finite review admission.");
            await using var record=fixture.Procurement(roles.Record);
            await using var recordTransaction=await record.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            Need(await ready.CanRecordPolicyAsync(record,default)&&!await ready.CanRecordAsync(record,default),
                "P35 Record policy privileges must not satisfy Core review writes.");
            await using var execution=fixture.Procurement(roles.Execution);
            await using var executionTransaction=await execution.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            Need(await ready.CanRecordAsync(execution,default)&&!await ready.CanRecordPolicyAsync(execution,default),
                "P35 Execution review privileges must not acquire policy configuration authority.");
        }
        var meta=await send(client,HttpMethod.Get,"/api/procurement/v1/meta",fixture.Buyer);
        Need(meta.GetProperty("capabilities").GetProperty("contractInternalReview").GetBoolean(),
            "P35 normal metadata must advertise only actual captured configured availability.");
        stage("P35ActualTypedPolicyLifecycle");
        const string policies="/api/procurement/v1/configuration/policies";
        var rules=JsonSerializer.Serialize(new {schemaVersion=1,transactionType="SupplierContractReview",
            stages=new[]{new {code="INTERNAL",mode="All",principalIds=new[]{fixture.Approver.ToString("D")}}}});
        Need(ProcurementContractReviewPolicyRules.Validate(rules,draft:false).Count==0,
            "P35 native rules must satisfy the actual typed contract before creation.");
        var policyEffective=ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow.AddSeconds(30))!.Value;
        var policy=await send(client,HttpMethod.Post,policies,fixture.ConfigMaker,new {policyKey="SUPPLIER_CONTRACT_REVIEW",
            effectiveFrom=policyEffective,effectiveTo=(DateTimeOffset?)null,rulesJson=rules},HttpStatusCode.Created);
        var policyId=policy.GetProperty("id").GetGuid();
        var pendingPolicy=await send(client,HttpMethod.Post,$"{policies}/{policyId}/submit-review",fixture.ConfigMaker,
            new {version=policy.GetProperty("version").GetInt64(),reason="Private independently reviewed internal contract policy"});
        await send(client,HttpMethod.Post,$"{policies}/{policyId}/publish",fixture.ConfigMaker,
            new {version=pendingPolicy.GetProperty("version").GetInt64(),reason="Maker must not publish own policy"},HttpStatusCode.Forbidden);
        var published=await send(client,HttpMethod.Post,$"{policies}/{policyId}/publish",fixture.ConfigReviewer,
            new {version=pendingPolicy.GetProperty("version").GetInt64(),reason="Independent private contract policy publication"});
        Need(published.GetProperty("state").GetString()=="Published","Actual typed contract policy was not published.");
        var policyWait=policyEffective-DateTimeOffset.UtcNow;
        Need(policyWait<=TimeSpan.FromSeconds(30),"Unexpected private contract policy activation wait.");
        if(policyWait>TimeSpan.Zero)await Task.Delay(policyWait+TimeSpan.FromMilliseconds(50));
        const string contracts="/api/procurement/v1/contracts";
        object Draft(string title)=>new {supplierId,title,contractType="Framework",currencyCode="ZMW",ceilingAmount="100.0001",
            effectiveFrom=DateOnly.FromDateTime(DateTime.UtcNow),expiresOn=DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),renewalNoticeOn=(DateOnly?)null};
        stage("P35ActualContractAndRateDraft");
        var created=await send(client,HttpMethod.Post,contracts,fixture.Buyer,Draft("Private internal review terms"),HttpStatusCode.Created);
        var id=created.GetProperty("id").GetGuid();var path=$"{contracts}/{id}";
        var rate=await send(client,HttpMethod.Post,path+"/rates",fixture.Buyer,
            new {contractVersion=created.GetProperty("version").GetInt64(),rateVersion=(long?)null,
                rate=new {catalogItemId=(Guid?)null,description="Private exact service rate",unitCode="each",unitPrice="5.1234",maximumQuantity="2.0001"}},HttpStatusCode.Created);
        var rateId=rate.GetProperty("rate").GetProperty("id").GetGuid();
        var version=rate.GetProperty("contractVersion").GetInt64();
        var originalRateVersion=rate.GetProperty("rate").GetProperty("version").GetInt64();
        var retained=await send(client,HttpMethod.Get,path,fixture.Buyer);
        Need(retained.GetProperty("version").GetInt64()==version&&retained.GetProperty("ceilingAmountExact").GetString()=="100.0001",
            "Real contract reload changed exact amount or current CAS.");
        await using var observer=new NpgsqlConnection(owner);await observer.OpenAsync();
        var deniedBefore=await PrincipalFootprintAsync(observer);
        await send(client,HttpMethod.Post,path+"/internal-review/submit",fixture.Ungranted,
            new ProcurementContractReviewRoutes.SubmitInput(version,fixture.Branch),HttpStatusCode.Forbidden);
        await send(client,HttpMethod.Post,path+"/internal-review/submit",fixture.Buyer,
            new ProcurementContractReviewRoutes.SubmitInput(version,Guid.NewGuid()),HttpStatusCode.Forbidden);
        Need(deniedBefore==await PrincipalFootprintAsync(observer),"P35 permission/branch denials changed persisted facts.");
        stage("P35SubmitAssignedReturnAndOriginalRecovery");
        var firstSubmitKey=Guid.NewGuid();var firstSubmitInput=new ProcurementContractReviewRoutes.SubmitInput(version,fixture.Branch);
        var first=await send(client,HttpMethod.Post,path+"/internal-review/submit",fixture.Buyer,firstSubmitInput,HttpStatusCode.Created,firstSubmitKey);
        Need(first.GetProperty("contractStatus").GetString()=="PendingReview","Contract did not enter actual pending Core review.");
        var firstReplay=await send(client,HttpMethod.Post,path+"/internal-review/submit",fixture.Buyer,firstSubmitInput,HttpStatusCode.Created,firstSubmitKey);
        var firstRecovery=await send(client,HttpMethod.Get,$"{contracts}/internal-review/commands/{firstSubmitKey}",fixture.Buyer,null,HttpStatusCode.Created);
        Need(firstReplay.GetRawText()==first.GetRawText()&&firstRecovery.GetRawText()==first.GetRawText(),
            "P35 Submit replay/GET changed original acknowledgement.");
        var returned=await Decide("Return","Private terms require correction",first.GetProperty("contractVersion").GetInt64());
        Need(returned.GetProperty("contractStatus").GetString()=="Returned","Actual Core Return did not return contract.");
        var correctionKey=Guid.NewGuid();var correctionInput=new ProcurementContractReviewRoutes.CorrectionInput(
            returned.GetProperty("contractVersion").GetInt64(),"Apply independently requested terms correction");
        var corrected=await send(client,HttpMethod.Post,path+"/internal-review/correct",fixture.Buyer,correctionInput,HttpStatusCode.OK,correctionKey);
        var correctionRecovery=await send(client,HttpMethod.Get,$"{contracts}/internal-review/commands/{correctionKey}",fixture.Buyer);
        Need(corrected.GetProperty("contractStatus").GetString()=="Draft"&&correctionRecovery.GetRawText()==corrected.GetRawText(),
            "Returned terms did not reopen through original actor-owned correction.");
        stage("P35DraftReviseResubmitIndependentApprove");
        var revised=await send(client,HttpMethod.Put,path,fixture.Buyer,new {version=corrected.GetProperty("contractVersion").GetInt64(),draft=Draft("Private corrected internal terms")});
        var editedRate=await send(client,HttpMethod.Put,path+$"/rates/{rateId}",fixture.Buyer,
            new {contractVersion=revised.GetProperty("version").GetInt64(),rateVersion=originalRateVersion,
                rate=new {catalogItemId=(Guid?)null,description="Private corrected exact service rate",unitCode="each",unitPrice="5.1001",maximumQuantity="2.0001"}});
        var secondSubmitKey=Guid.NewGuid();var second=await send(client,HttpMethod.Post,path+"/internal-review/submit",fixture.Buyer,
            new ProcurementContractReviewRoutes.SubmitInput(editedRate.GetProperty("contractVersion").GetInt64(),fixture.Branch),HttpStatusCode.Created,secondSubmitKey);
        var approved=await Decide("Approve",null,second.GetProperty("contractVersion").GetInt64());
        Need(approved.GetProperty("contractStatus").GetString()=="Approved","Actual independent Core vote did not approve internal terms.");
        var final=await send(client,HttpMethod.Get,path,fixture.Buyer);
        Need(final.GetProperty("status").GetString()=="Approved"&&final.GetProperty("version").GetInt64()==approved.GetProperty("contractVersion").GetInt64(),
            "Fresh contract API reload disagrees with retained approval acknowledgement.");
        stage("P35IndependentOwnerGraphReload");
        await using(var db=fixture.Procurement())
        await using(var coreCases=fixture.Approvals())
        {
            var submissions=await db.ContractReviewSubmissions.AsNoTracking().Where(x=>x.ContractId==id).OrderBy(x=>x.SubmissionNumber).ToArrayAsync();
            var votes=await db.ContractReviewVotes.AsNoTracking().Where(x=>x.ContractId==id).ToArrayAsync();
            var corrections=await db.ContractReviewCorrections.AsNoTracking().Where(x=>x.ContractId==id).ToArrayAsync();
            var receipts=await db.ContractReviewCommandReceipts.AsNoTracking().Where(x=>x.ContractId==id).ToArrayAsync();
            var versions=await db.SupplierContractVersions.AsNoTracking().Where(x=>x.SupplierContractId==id).ToArrayAsync();
            var cases=submissions.Select(x=>x.CoreCaseId).ToArray();
            Need(submissions.Length==2&&submissions[0].SubmissionNumber==1&&submissions[1].SubmissionNumber==2
                &&submissions.All(x=>x.MakerPrincipalId==fixture.Buyer.ToString("D")&&x.MakerWorkerAliasId is null&&x.ReviewBranchId==fixture.Branch)
                &&votes.Length==2&&votes.All(x=>x.ActorPrincipalId==fixture.Approver.ToString("D")&&x.ActorWorkerAliasId is null)
                &&votes.Select(x=>x.Action).Order().SequenceEqual(new[]{"Approve","Return"})
                &&corrections.Length==1&&receipts.Length==5&&versions.Length==1&&versions[0].Status=="Approved"
                &&await coreCases.Cases.AsNoTracking().CountAsync(x=>cases.Contains(x.Id))==2
                &&await coreCases.Decisions.AsNoTracking().CountAsync(x=>cases.Contains(x.CaseId))==2
                &&await db.ContractReviewPolicyIdentityRecords.CountAsync(x=>x.PolicyVersionId==policyId)==3
                &&await db.ContractRates.CountAsync(x=>x.Id==rateId&&x.UnitPrice==5.1001m&&x.MaximumQuantity==2.0001m)==1
                &&!await db.FinanceInvoiceHandoffs.AnyAsync()&&!await db.FinancePaymentStatusRefs.AnyAsync(),
                "P35 original source, actual Core votes, exact rate, receipts or no-Finance boundary changed.");
            foreach(var receipt in receipts)ContractReviewReceiptProof.Verify(receipt);
            var retainedPolicy=await db.PolicyVersions.AsNoTracking().SingleAsync(x=>x.Id==policyId);
            foreach(var submission in submissions)
            {
                ContractReviewTerms.VerifyRetainedSnapshot(submission);
                var workflow=await coreCases.Cases.AsNoTracking().Include(x=>x.Stages).ThenInclude(x=>x.Assignments)
                    .SingleAsync(x=>x.Id==submission.CoreCaseId);
                var pins=ContractReviewAssignmentPins.Verify(submission,workflow,retainedPolicy);
                var decisions=await coreCases.Decisions.AsNoTracking().Where(x=>x.CaseId==workflow.Id).ToArrayAsync();
                ContractReviewRetainedVotes.Verify(submission,pins,decisions,
                    votes.Where(x=>x.SubmissionId==submission.Id).ToArray(),
                    receipts.Where(x=>x.SubmissionId==submission.Id&&x.Operation is "Submit" or "Decide").ToArray());
            }
            // Correction is a separate original command, not a Core vote receipt.
            // Verify it independently against the completed retained Return and
            // original submitted terms; never omit it from total/effect proof.
            var originalSubmission=submissions[0];var laterSubmission=submissions[1];
            var correction=corrections.Single();
            var correctionReceipt=receipts.Single(x=>x.Operation=="Correction");
            var returnedCase=await coreCases.Cases.AsNoTracking().Include(x=>x.Stages).ThenInclude(x=>x.Assignments)
                .SingleAsync(x=>x.Id==originalSubmission.CoreCaseId);
            var returnedPins=ContractReviewAssignmentPins.Verify(originalSubmission,returnedCase,retainedPolicy);
            var returnDecisions=await coreCases.Decisions.AsNoTracking().Where(x=>x.CaseId==returnedCase.Id).ToArrayAsync();
            ContractReviewCoreHistory.VerifyReturned(originalSubmission,returnedCase,returnedPins,returnDecisions);
            var correctionHash=ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
                {schemaVersion=1,operation="Correction",id,actor=fixture.Buyer.ToString("D"),
                    correctionInput.ExpectedContractVersion,reason=correctionInput.Reason.Trim()}));
            Need(correction.ContractId==id&&correction.ContractVersionId==originalSubmission.ContractVersionId
                &&correction.ContractVersionId==laterSubmission.ContractVersionId
                &&correction.SubmissionId==originalSubmission.Id&&correction.CoreCaseId==returnedCase.Id
                &&correction.TenantId==originalSubmission.TenantId&&correction.LegalEntityId==originalSubmission.LegalEntityId
                &&correction.ReviewBranchId==originalSubmission.ReviewBranchId
                &&correction.ActorPrincipalId==originalSubmission.MakerPrincipalId
                &&correction.ActorWorkerAliasId==originalSubmission.MakerWorkerAliasId
                &&correction.FromContractVersion==originalSubmission.ContractBusinessVersion+2
                &&correction.ToContractVersion==correction.FromContractVersion+1
                &&correction.ToContractVersion<laterSubmission.ContractBusinessVersion
                &&correction.Reason==correctionInput.Reason.Trim()
                &&correction.CorrectedAt>=returnedCase.CompletedAt!.Value
                &&correction.CorrectedAt<=laterSubmission.SubmittedAt
                &&correctionReceipt.CorrectionId==correction.Id&&correctionReceipt.VoteId is null
                &&correctionReceipt.SubmissionId==originalSubmission.Id&&correctionReceipt.ContractId==id
                &&correctionReceipt.ReviewBranchId==correction.ReviewBranchId
                &&correctionReceipt.ActorPrincipalId==correction.ActorPrincipalId
                &&correctionReceipt.CommandKey==correctionKey&&correctionReceipt.CommandSha256==correctionHash
                &&correctionReceipt.ResultContractVersion==correction.ToContractVersion
                &&correctionReceipt.CompletedAt==correction.CorrectedAt
                &&correctionReceipt.ResponseJson==ContractReviewReceiptProof.Response(correctionReceipt,"Draft"),
                "P35 correction lost original Return, terms ownership, CAS, actor or acknowledgement binding.");
            using var oldTerms=JsonDocument.Parse(originalSubmission.TermsJson);
            using var newTerms=JsonDocument.Parse(laterSubmission.TermsJson);
            Need(oldTerms.RootElement.GetProperty("Title").GetString()=="Private internal review terms"
                &&newTerms.RootElement.GetProperty("Title").GetString()=="Private corrected internal terms"
                &&oldTerms.RootElement.GetProperty("rates").EnumerateArray().Single().GetProperty("unitPriceExact").GetString()=="5.1234"
                &&newTerms.RootElement.GetProperty("rates").EnumerateArray().Single().GetProperty("unitPriceExact").GetString()=="5.1001",
                "P35 correction must retain original exact terms independently of revised current terms.");
        }
        result["principalContractReview"]=new {status="PASS_PRIVATE_P35_INTERNAL_CONTRACT_REVIEW_ONLY",contractId=id,policyId,
            supplierId,firstSubmitKey,correctionKey,secondSubmitKey,actualCoreReturnAndApprove=true,
            retainedSubmissions=2,retainedVotes=2,retainedCorrections=1,retainedReceipts=5,
            workerRequired=false,signatureAuthority=false,contractActivated=false,financePosting=false,
            normalCookieAuthenticationVerified=false};
        result["status"]="PASS_PRIVATE_P35_INTERNAL_CONTRACT_REVIEW_ONLY";
        return new P35ContractReviewEvidence(id,supplierId,policyId,
            first.GetProperty("SubmissionId").GetGuid(),second.GetProperty("SubmissionId").GetGuid(),
            firstSubmitKey,correctionKey,secondSubmitKey);

        async Task<JsonElement> Decide(string action,string? reason,long contractVersion)
        {
            var queue=await send(client,HttpMethod.Get,$"{contracts}/internal-review/assigned",fixture.Approver);
            var item=queue.GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("ContractId").GetGuid()==id);
            var assignment=item.GetProperty("AssignmentId").GetGuid();
            var reviewPath=path+$"/internal-review/assignments/{assignment}";
            var detail=await send(client,HttpMethod.Get,reviewPath,fixture.Approver);
            Need(detail.GetProperty("contractVersion").GetInt64()==contractVersion,"Assigned details have stale contract CAS.");
            var body=new ProcurementContractReviewRoutes.DecisionInput(contractVersion,detail.GetProperty("caseVersion").GetInt64(),
                detail.GetProperty("assignmentVersion").GetInt64(),action,reason);
            var before=await PrincipalFootprintAsync(observer);
            await send(client,HttpMethod.Get,reviewPath,fixture.Buyer,null,HttpStatusCode.NotFound);
            await send(client,HttpMethod.Post,reviewPath+"/decide",fixture.Buyer,body,HttpStatusCode.NotFound,Guid.NewGuid());
            Need(before==await PrincipalFootprintAsync(observer),"Unassigned maker decision changed actual review facts.");
            var key=Guid.NewGuid();var reply=await send(client,HttpMethod.Post,reviewPath+"/decide",fixture.Approver,body,HttpStatusCode.OK,key);
            var replay=await send(client,HttpMethod.Post,reviewPath+"/decide",fixture.Approver,body,HttpStatusCode.OK,key);
            var recovery=await send(client,HttpMethod.Get,$"{contracts}/internal-review/commands/{key}",fixture.Approver);
            Need(replay.GetRawText()==reply.GetRawText()&&recovery.GetRawText()==reply.GetRawText(),"Original decision replay or GET result changed.");
            return reply;
        }
    }
}
