using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Procurement.Domain;
using Npgsql;

internal static partial class PrincipalApprovalHttpVerification
{
    private const string PoRoot="/api/procurement/v1/orders";
    private static async Task VerifyPrincipalOrderAsync(PrincipalApprovalFixture fixture,string owner,
        PrincipalApprovalRuntimePrivileges.Connections roles,Dictionary<string,object?> result,
        Action<string> stage,PrincipalSend send,Action countRequest)
    {
        Need(fixture.PrincipalPoProfile && result.TryGetValue("positiveApprovalGraph",out var approved)
            && approved is true && result["positiveApprovalRequestId"] is Guid,
            "P30 requires its genuinely approved direct-purchase principal source.");
        var requestId=(Guid)result["positiveApprovalRequestId"]!;
        stage("P30RealFiniteReadinessOnly");
        await using var host=new PrincipalApprovalHost(fixture,owner,roles.Record,roles.Execution,
            actualSchema:true,privateManualEvidence:true);
        host.PrepareEntryPointEnvironment();using var client=host.CreateClient(new(){AllowAutoRedirect=false});
        using(var registration=host.Services.CreateScope())
        {
            var readiness=registration.ServiceProvider.GetRequiredService<IProcurementPrincipalOrderDraftReadiness>();
            Need(readiness is not UnavailableProcurementPrincipalOrderDraftReadiness
                && await readiness.IsReadyAsync(default),
                "P30 real owning migration, finite grants and normal host readiness are required; no positive stub.");
        }
        var metadata=await send(client,HttpMethod.Get,"/api/procurement/v1/meta",fixture.Buyer);
        Need(metadata.GetProperty("capabilities").GetProperty("principalDirectOrderDrafts").GetBoolean(),
            "P30 genuine principal PO draft capability is unavailable.");
        // A supplier is proposed and independently approved by the real vendor routes.
        stage("P30IndependentSupplierMasterActivation");
        const string vendorRoot="/api/procurement/v1/vendors";
        var proposed=await send(client,HttpMethod.Post,vendorRoot,fixture.ConfigMaker,new {
            legalName="Private P30 order supplier",displayName="Private P30 supplier",countryCode="ZM",
            currencyCode="ZMW",categoryCode="PRIVATE_P25",isLocal=true,portalAccessRequested=false,
            primaryContact=new{name="Private P30 contact",isPrimary=true}
        },HttpStatusCode.Created);
        var supplierId=proposed.GetProperty("id").GetGuid();
        var submittedVendor=await send(client,HttpMethod.Post,$"{vendorRoot}/{supplierId}/submit",fixture.ConfigMaker,
            new{version=proposed.GetProperty("version").GetInt64(),businessReason="Private P30 direct purchase source"});
        var activatedVendor=await send(client,HttpMethod.Post,$"{vendorRoot}/{supplierId}/approve",fixture.ConfigReviewer,
            new{version=submittedVendor.GetProperty("version").GetInt64(),reason="Independent private supplier review",duplicatesReviewed=true});
        Need(activatedVendor.GetProperty("status").GetString()=="Active"
            && activatedVendor.GetProperty("isOrderable").GetBoolean(),"Real supplier approval did not activate buying eligibility.");

        stage("P30IndependentOrderAuthorityAndPolicy");
        const string authorities="/api/procurement/v1/configuration/spending-authorities";
        var grant=await send(client,HttpMethod.Post,authorities,fixture.ConfigMaker,new{
            authorityReference="PRIVATE_P30_PO_CORE_APPROVER",principalId=fixture.PoApprover.ToString("D"),
            action="PurchaseOrder.Approve",draft=new{currencyCode="ZMW",amountBasis="ApprovedLineBase",
                maximumAmount="100.0000",effectiveFrom=DateTimeOffset.UtcNow.AddMinutes(-1),
                effectiveUntil=DateTimeOffset.UtcNow.AddHours(1),allBranches=false,branchRef=fixture.Branch,
                allDepartments=true,departmentRef=(Guid?)null,allCategories=true,categoryCodes=Array.Empty<string>()}
        },HttpStatusCode.Created);
        var authorityPath=authorities+"/"+grant.GetProperty("id").GetGuid();
        var submittedGrant=await send(client,HttpMethod.Post,authorityPath+"/submit",fixture.ConfigMaker,
            new{version=grant.GetProperty("version").GetInt64()});
        var activeGrant=await send(client,HttpMethod.Post,authorityPath+"/activate",fixture.ConfigReviewer,
            new{version=submittedGrant.GetProperty("version").GetInt64(),reason="Private independent PO grant review"});
        Need(activeGrant.GetProperty("state").GetString()=="Active","Actual PO-specific spending grant was not activated.");
        const string policyRoot="/api/procurement/v1/configuration/policies";
        const string poRules="""
            {"schemaVersion":1,"transactionType":"PurchaseOrder","currencyCode":"ZMW","amountBasis":"ApprovedLineBase",
             "rules":[{"code":"PRIVATE_P30_DIRECT","priority":0,
               "when":{"minimumAmountInclusive":null,"maximumAmountExclusive":null,"categoryCodes":[],"purchaseTypes":[],"branchIds":[]},
               "result":{"budgetReservation":"Required","sourcingMethodCode":"DIRECT","minimumQuoteCount":null,
                 "approvalStages":[{"code":"PRIVATE_PO_REVIEW","assignee":"ProcurementManager","mode":"One"}]}}]}
            """;
        var effectiveFrom=DateTimeOffset.UtcNow.AddSeconds(30);
        var policy=await send(client,HttpMethod.Post,policyRoot,fixture.ConfigMaker,new{
            policyKey="PURCHASE_ORDER",effectiveFrom,effectiveTo=(DateTimeOffset?)null,rulesJson=poRules},HttpStatusCode.Created);
        var policyPath=policyRoot+"/"+policy.GetProperty("id").GetGuid();
        var review=await send(client,HttpMethod.Post,policyPath+"/submit-review",fixture.ConfigMaker,
            new{version=policy.GetProperty("version").GetInt64(),reason="Private PO rule review"});
        var published=await send(client,HttpMethod.Post,policyPath+"/publish",fixture.ConfigReviewer,
            new{version=review.GetProperty("version").GetInt64(),reason="Independent private PO policy review"});
        Need(published.GetProperty("state").GetString()=="Published","PO rule was not published by independent review.");
        var remaining=effectiveFrom-DateTimeOffset.UtcNow;
        Need(remaining<=TimeSpan.FromSeconds(30),"Unexpected private PO policy activation wait.");
        if(remaining>TimeSpan.Zero)await Task.Delay(remaining+TimeSpan.FromMilliseconds(50));

        stage("P30CurrentApprovedDirectSourceAndSupplierPurposeLookup");
        var sourcePage=await send(client,HttpMethod.Get,PoRoot+"/approved-principal-direct-sources?page=1&pageSize=25",fixture.Buyer);
        var source=sourcePage.GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("purchaseRequestId").GetGuid()==requestId);
        Need(source.GetProperty("canSelect").GetBoolean()
            && source.GetProperty("availableQuantity").GetString()=="2"
            && source.GetProperty("approvedUnitEstimate").GetString()=="5.1234",
            "Approved direct source was not selectable with exact retained amount.");
        var lineId=source.GetProperty("purchaseRequestLineId").GetGuid();
        var suppliers=await send(client,HttpMethod.Get,PoRoot+"/principal-direct-suppliers?page=1&pageSize=25",fixture.Buyer);
        Need(suppliers.GetProperty("previewOnly").GetBoolean()
            && suppliers.GetProperty("items").EnumerateArray().Any(x=>x.GetProperty("id").GetGuid()==supplierId),
            "Purpose-only approved supplier was absent.");
        await send(client,HttpMethod.Get,PoRoot+"/approved-principal-direct-sources",fixture.Ungranted,null,HttpStatusCode.Forbidden);
        await send(client,HttpMethod.Get,PoRoot+"/principal-direct-suppliers",fixture.Ungranted,null,HttpStatusCode.Forbidden);
        var foreignBranch=Guid.NewGuid();
        await using(var core=fixture.Core())
        {
            core.WorkLocations.Add(new WorkLocation{Id=foreignBranch,Code="P30_FOREIGN",Name="Unassigned private branch",
                LegalEntityId=fixture.Company});
            await core.SaveChangesAsync();
        }
        using(var wrongScope=new HttpRequestMessage(HttpMethod.Get,PoRoot+"/approved-principal-direct-sources"))
        {
            wrongScope.Headers.Add("X-Private-P25-Subject",fixture.Buyer.ToString("D"));
            wrongScope.Headers.Add("X-Shell-Entity",fixture.Company.ToString("D"));
            wrongScope.Headers.Add("X-Shell-Location",foreignBranch.ToString("D"));
            countRequest();
            using var denied=await client.SendAsync(wrongScope);
            Need(denied.StatusCode==HttpStatusCode.Forbidden,"An unassigned current Core branch exposed principal PO source candidates.");
        }

        async Task<(JsonElement Created,JsonElement Detail,Guid Key)> CreateDraftAsync()
        {
            var fresh=await send(client,HttpMethod.Get,PoRoot+"/approved-principal-direct-sources?page=1&pageSize=25",fixture.Buyer);
            var selected=fresh.GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("purchaseRequestLineId").GetGuid()==lineId);
            var body=new {draft=new{supplierId,currencyCode="ZMW",shipToRef="Private branch",billToRef="Private company",
                paymentTermRef="Net 30",expectedDeliveryOn=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14),
                lines=new[]{new{id=(Guid?)null,description="Exact Core principal source",unitCode="each",
                    quantity=1.0000m,unitPrice=5.1234m,taxAmount=0m,costCenterRef=(string?)null,
                    projectRef=(string?)null,expectedDeliveryOn=(DateOnly?)null}}},
                sources=new[]{new{draftLineIndex=0,purchaseRequestId=requestId,purchaseRequestLineId=lineId,
                    expectedRequestVersion=selected.GetProperty("expectedRequestVersion").GetInt64()}}};
            var key=Guid.NewGuid();
            await send(client,HttpMethod.Post,PoRoot+"/from-approved-principal-direct-sources",fixture.Ungranted,
                body,HttpStatusCode.Forbidden,Guid.NewGuid());
            var created=await send(client,HttpMethod.Post,PoRoot+"/from-approved-principal-direct-sources",fixture.Buyer,
                body,HttpStatusCode.Created,key);
            var orderId=created.GetProperty("orderId").GetGuid();
            var replay=await send(client,HttpMethod.Post,PoRoot+"/from-approved-principal-direct-sources",fixture.Buyer,
                body,HttpStatusCode.Created,key);
            Need(replay.GetRawText()==created.GetRawText(),"Historical create replay changed its recorded response.");
            await send(client,HttpMethod.Post,PoRoot+"/from-approved-principal-direct-sources",fixture.Buyer,
                new{body.draft,sources=new[]{new{draftLineIndex=0,purchaseRequestId=requestId,
                    purchaseRequestLineId=lineId,expectedRequestVersion=selected.GetProperty("expectedRequestVersion").GetInt64()+1}}},
                HttpStatusCode.Conflict,key);
            var recovered=await send(client,HttpMethod.Get,PoRoot+"/principal-direct-command-receipts/Create/"+key,fixture.Buyer);
            Need(recovered.GetProperty("responseJson").GetString()==created.GetRawText(),"Actor-owned lost Create recovery changed bytes.");
            await send(client,HttpMethod.Get,PoRoot+"/principal-direct-command-receipts/Create/"+key,
                fixture.Ungranted,null,HttpStatusCode.Forbidden);
            var detail=await send(client,HttpMethod.Get,$"{PoRoot}/{orderId}/principal-direct-draft",fixture.Buyer);
            Need(detail.GetProperty("status").GetString()=="Draft"&&detail.GetProperty("supplierId").GetGuid()==supplierId
                &&detail.GetProperty("lines").EnumerateArray().Single().GetProperty("quantity").GetString()=="1"
                &&detail.GetProperty("lines").EnumerateArray().Single().GetProperty("allocationStatus").GetString()=="Reserved",
                "Principal PO draft did not retain its exact source and reservation.");
            return (created,detail,key);
        }

        stage("P30DraftCancelReleaseAndNoEffects");
        var first=await CreateDraftAsync();var firstId=first.Created.GetProperty("orderId").GetGuid();
        var cancelKey=Guid.NewGuid();var cancelBody=new{expectedOrderVersion=first.Detail.GetProperty("version").GetInt64(),
            originalDirectSourceSha256=first.Detail.GetProperty("originalDirectSourceSha256").GetString(),reason="Private unused draft"};
        var cancelled=await send(client,HttpMethod.Post,$"{PoRoot}/{firstId}/principal-direct-cancel",fixture.Buyer,
            cancelBody,HttpStatusCode.OK,cancelKey);
        Need(cancelled.GetProperty("status").GetString()=="Cancelled","Unfunded draft was not cancelled.");
        var firstRecovery=await send(client,HttpMethod.Get,PoRoot+"/principal-direct-command-receipts/Cancel/"+cancelKey,fixture.Buyer);
        Need(firstRecovery.GetProperty("responseJson").GetString()==cancelled.GetRawText(),"Cancel recovery changed bytes.");

        async Task<JsonElement> FundAsync(Guid orderId,JsonElement detail,string suffix)
        {
            var finance=await send(client,HttpMethod.Get,$"{PoRoot}/{orderId}/principal-finance-detail",fixture.PoFinance);
            Need(finance.GetProperty("grossAmountExact").GetString()=="5.1234"
                && finance.GetProperty("sourceReservationReferences").GetArrayLength()==1,
                "Independent Finance-purpose read omitted exact retained commitment inputs.");
            var references=finance.GetProperty("sourceReservationReferences").EnumerateArray().Select(x=>x.GetString()!).ToArray();
            var now=DateTimeOffset.UtcNow;
            return await send(client,HttpMethod.Post,$"{PoRoot}/{orderId}/principal-funding",fixture.PoFinance,new{
                expectedOrderVersion=detail.GetProperty("version").GetInt64(),manualCommitmentReference="PRIVATE_P30_"+suffix,
                externalEvidenceReference="PRIVATE_P30_EXTERNAL_"+suffix,currencyCode="ZMW",attestedGrossAmount=5.1234m,
                sourceReservationReferences=references,observedAt=now},HttpStatusCode.OK,Guid.NewGuid());
        }
        var second=await CreateDraftAsync();var secondId=second.Created.GetProperty("orderId").GetGuid();
        var funded=await FundAsync(secondId,second.Detail,"RELEASE");
        var releaseKey=Guid.NewGuid();var release=await send(client,HttpMethod.Post,$"{PoRoot}/{secondId}/principal-release-request",fixture.Buyer,
            new{expectedOrderVersion=funded.GetProperty("version").GetInt64(),
                originalDirectSourceSha256=second.Detail.GetProperty("originalDirectSourceSha256").GetString(),
                reason="Private external commitment release required"},HttpStatusCode.OK,releaseKey);
        Need(release.GetProperty("status").GetString()=="ReleasePending","Funded draft bypassed pending external release.");
        var releaseDetail=await send(client,HttpMethod.Get,$"{PoRoot}/{secondId}/principal-direct-draft",fixture.Buyer);
        var releaseFinance=await send(client,HttpMethod.Get,$"{PoRoot}/{secondId}/principal-finance-detail",fixture.ConfigReviewer);
        var confirmed=await send(client,HttpMethod.Post,$"{PoRoot}/{secondId}/principal-release-confirmation",fixture.ConfigReviewer,new{
            expectedOrderVersion=release.GetProperty("version").GetInt64(),
            releaseRequestId=releaseDetail.GetProperty("release").GetProperty("id").GetGuid(),
            externalReleaseEvidenceReference="PRIVATE_P30_RELEASED_EXTERNALLY",currencyCode="ZMW",
            releasedGrossAmount=5.1234m,
            restoredSourceReservationReferences=releaseFinance.GetProperty("sourceReservationReferences").EnumerateArray().Select(x=>x.GetString()!).ToArray(),
            confirmedAt=DateTimeOffset.UtcNow},HttpStatusCode.OK,Guid.NewGuid());
        Need(confirmed.GetProperty("status").GetString()=="Cancelled","External release did not precede funded cancellation.");

        stage("P30CurrentFundingScopeAndIndependentCoreApproval");
        var third=await CreateDraftAsync();var orderId=third.Created.GetProperty("orderId").GetGuid();
        var orderPath=$"{PoRoot}/{orderId}";
        var funding=await FundAsync(orderId,third.Detail,"ISSUE");
        await using(var core=fixture.Core())
        {
            var link=await core.UserBranchAssignments.SingleAsync(x=>x.UserId==fixture.PoFinance&&x.LocationId==fixture.Branch);
            link.IsArchived=true;await core.SaveChangesAsync();
        }
        await send(client,HttpMethod.Post,orderPath+"/principal-submit",fixture.Buyer,
            new{expectedOrderVersion=funding.GetProperty("version").GetInt64()},HttpStatusCode.Conflict,Guid.NewGuid());
        await using(var core=fixture.Core())
        {
            var link=await core.UserBranchAssignments.IgnoreQueryFilters().SingleAsync(x=>x.UserId==fixture.PoFinance&&x.LocationId==fixture.Branch);
            link.IsArchived=false;await core.SaveChangesAsync();
        }
        var submitted=await send(client,HttpMethod.Post,orderPath+"/principal-submit",fixture.Buyer,
            new{expectedOrderVersion=funding.GetProperty("version").GetInt64()},HttpStatusCode.Created,Guid.NewGuid());
        Need(submitted.GetProperty("status").GetString()=="PendingApproval","Real Core PO approval did not open.");
        var queue=await send(client,HttpMethod.Get,PoRoot+"/principal-assigned-reviews?page=1&pageSize=25",fixture.PoApprover);
        var task=queue.GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("orderId").GetGuid()==orderId);
        var assignmentId=task.GetProperty("assignmentId").GetGuid();
        var reviewDetail=await send(client,HttpMethod.Get,$"{orderPath}/principal-review/{assignmentId}",fixture.PoApprover);
        Need(reviewDetail.GetProperty("status").GetString()=="PendingApproval","Assigned reviewer cannot inspect pinned PO facts.");
        var decision=await send(client,HttpMethod.Post,$"{orderPath}/principal-decisions/{assignmentId}",fixture.PoApprover,new{
            action="Approve",reason=(string?)null,expectedOrderVersion=task.GetProperty("version").GetInt64(),
            expectedCaseVersion=task.GetProperty("caseVersion").GetInt64(),
            expectedAssignmentVersion=task.GetProperty("assignmentVersion").GetInt64()},HttpStatusCode.OK,Guid.NewGuid());
        Need(decision.GetProperty("status").GetString()=="Approved","Independent Core PO decision was not retained.");

        stage("P30InternalVersionAndObservedSupplierResponse");
        var work=await send(client,HttpMethod.Get,PoRoot+"/principal-fulfilment-work?view=issue&page=1&pageSize=25",fixture.Buyer);
        Need(work.GetProperty("items").EnumerateArray().Any(x=>x.GetProperty("id").GetGuid()==orderId),
            "Authorized internal issue worklist did not contain the approved order.");
        var issued=await send(client,HttpMethod.Post,orderPath+"/principal-issue",fixture.Buyer,
            new{expectedOrderVersion=decision.GetProperty("version").GetInt64()},HttpStatusCode.Created,Guid.NewGuid());
        Need(issued.GetProperty("status").GetString()=="IssuePending"&&!issued.GetProperty("supplierSendPerformed").GetBoolean(),
            "Internal issue falsely implied supplier dispatch.");
        var ackView=await send(client,HttpMethod.Get,orderPath+"/principal-fulfilment-detail?view=ack",fixture.PoAck);
        var issuedId=ackView.GetProperty("issuedVersionId").GetGuid();
        var ack=await send(client,HttpMethod.Post,orderPath+"/principal-acknowledgment",fixture.PoAck,new{
            expectedOrderVersion=issued.GetProperty("version").GetInt64(),issuedVersionId=issuedId,
            outcome="Accepted",channel="ManualRecord",supplierReference="PRIVATE_P30_SUPPLIER_ACK",
            evidenceReference="PRIVATE_P30_SUPPLIER_RESPONSE",observedAt=DateTimeOffset.UtcNow},HttpStatusCode.OK,Guid.NewGuid());
        Need(ack.GetProperty("status").GetString()=="Issued"&&!ack.GetProperty("supplierSendPerformed").GetBoolean(),
            "Observed manual supplier response was not distinguished from a send.");

        stage("P30IndependentOwnerReloadNoInventedEffects");
        await using var db=fixture.Procurement();await using var cases=fixture.Approvals();await using var coreProof=fixture.Core();
        var orders=await db.PurchaseOrders.Where(x=>new[]{firstId,secondId,orderId}.Contains(x.Id)).ToArrayAsync();
        var allocations=await db.PurchaseRequestAllocations.Where(x=>x.PurchaseRequestLineId==lineId&&x.TargetType=="PurchaseOrder").ToArrayAsync();
        var original=await db.PurchaseRequestLines.SingleAsync(x=>x.Id==lineId);
        var receipts=await db.PrincipalOrderDraftReceipts.Where(x=>new[]{firstId,secondId,orderId}.Contains(x.PurchaseOrderId)).ToArrayAsync();
        var submission=await db.PrincipalOrderSubmissions.SingleAsync(x=>x.PurchaseOrderId==orderId);
        var caseRow=await cases.Cases.SingleAsync(x=>x.Id==submission.CoreApprovalCaseId);
        Need(orders.Length==3&&orders.Count(x=>x.Status=="Cancelled")==2&&orders.Single(x=>x.Id==orderId).Status=="Issued"
            &&allocations.Length==3&&allocations.Count(x=>x.Status=="Released")==2&&allocations.Count(x=>x.Status=="Ordered")==1
            &&original.AllocatedQuantity==1m&&caseRow.State=="Approved"&&await db.PrincipalOrderFundingEvidence.CountAsync()==2
            &&await db.PrincipalOrderReleaseRequests.CountAsync()==1&&await db.PrincipalOrderReleaseConfirmations.CountAsync()==1
            &&await db.PrincipalOrderIssuedVersions.CountAsync()==1&&await db.PrincipalOrderAcknowledgments.CountAsync()==1
            &&receipts.Count(x=>x.Operation=="Create")==3&&receipts.Select(x=>x.CommandKey).Distinct().Count()==receipts.Length
            &&!await coreProof.Workers.AnyAsync()&&!await db.FinanceInvoiceHandoffs.AnyAsync()&&!await db.FinancePaymentStatusRefs.AnyAsync(),
            "Reload did not retain exact cancelled/released/approved PO graph without Finance or employees.");
        result["principalOrder"]=new{status="PASS_PRIVATE_P30_PRINCIPAL_PO_INTERNAL_AND_MANUAL_EVIDENCE_ONLY",
            requestId,lineId,issuedOrderId=orderId,firstCancelledOrderId=firstId,secondReleasedOrderId=secondId,
            sourceAllocationRestoredBeforeIssue=true,independentCoreApproval=true,
            currentRecorderScopeRevocationDenied=true,actorOwnedCreateRecovery=true,
            externalFinancePosting=false,supplierSendPerformed=false,paymentExecuted=false};
        result["status"]="PASS_PRIVATE_P30_PRINCIPAL_PO_INTERNAL_AND_MANUAL_EVIDENCE_ONLY";
    }
}
