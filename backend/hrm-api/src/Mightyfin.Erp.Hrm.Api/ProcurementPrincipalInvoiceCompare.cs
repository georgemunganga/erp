using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementInvoiceMatchingRoutes
{
    // Hook after mapping lookup, before the legacy Target/Replay/Verify branch.
    // No detached/default owner provider participates in this source3 path.
    private static async Task<string> ComparePrincipal(Guid id, CompareInput input,
        HttpContext http, ProcurementRequestScope scope, ProcurementDbContext db,
        ApprovalDbContext cases, HrmDbContext core, IAuthorizationService auth,
        InvoiceMatchMappingVersion mapping, CancellationToken token)
    {
        await RequirePrincipalMatching(http, scope, db, cases, true, token);
        if (mapping.SourceSchemaVersion != 3 || mapping.SupplierInvoiceId != id
            || mapping.TenantId != scope.TenantId || mapping.LegalEntityId != scope.LegalEntityId)
            throw new MatchingProblem("PrincipalInvoiceMappingRequired",409);
        if (input.Reason?.Length > 2000) throw new MatchingProblem("InvoiceComparisonReasonTooLong",422);
        var transaction=db.Database.CurrentTransaction?.GetDbTransaction();
        if (transaction is null || transaction.IsolationLevel!=IsolationLevel.Serializable
            || !ReferenceEquals(transaction,core.Database.CurrentTransaction?.GetDbTransaction())
            || !ReferenceEquals(db.Database.GetDbConnection(),core.Database.GetDbConnection()))
            throw new MatchingProblem("PrincipalInvoiceSharedTransactionRequired",503);
        var invoice=await Invoice(db,scope,id,token);
        var worker=await Actor(core,scope,http,auth,"procurement-invoice-match",invoice.BranchRef,token);
        _=await Target(db,core,scope,http,auth,"procurement-invoice-match",invoice,mapping.PurchaseOrderId,token);
        var key=Key(http);
        var fingerprint=PrincipalCompareFingerprint(id,input);
        // A durable original result remains historical. Current actor/record access
        // is checked above; old policy windows and current capacity are not replayed.
        if (await ReplayPrincipalCompare(db,scope,key,fingerprint,id,mapping.PurchaseOrderId,token) is { } recovered)
            return recovered;

        var allowed=await PrincipalMatchingBranches(core,scope,token);
        // Preserve upstream PR -> PO -> exposure -> invoice ordering. No invoice or
        // exposure row lock is taken before the retained issued owner acquires its locks.
        var issuedWitness=await ProcurementPrincipalInvoiceIssuedWitness.LoadAsync(
            db,cases,scope,allowed,mapping.PurchaseOrderId,mapping.IssuedVersionId,token);
        var issued=await ProcurementPrincipalInvoiceIssuedWitness.LoadReviewIssuedOrderAsync(
            db,cases,scope,allowed,mapping.PurchaseOrderId,mapping.IssuedVersionId,token);
        var exposure=await db.InvoiceMatchExposureStates.SingleOrDefaultAsync(x=>
            x.PurchaseOrderId==mapping.PurchaseOrderId && x.IssuedVersionId==mapping.IssuedVersionId,token);
        if (exposure is null)
        {
            exposure=new InvoiceMatchExposureState{PurchaseOrderId=mapping.PurchaseOrderId,
                IssuedVersionId=mapping.IssuedVersionId,SupplierId=invoice.SupplierId,CurrencyCode=invoice.CurrencyCode,
                SourceSchemaVersion=3};
            db.AddScoped(exposure);
        }
        else
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.invoice_match_exposure_states WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={exposure.Id} FOR UPDATE",token);
            await db.Entry(exposure).ReloadAsync(token);
            if(exposure.SourceSchemaVersion!=3)throw new MatchingProblem("PrincipalInvoiceExposureReconciliationRequired",409);
        }
        invoice=await Invoice(db,scope,id,token,true);
        await db.Entry(invoice).ReloadAsync(token);
        if (invoice.Version!=input.Version || invoice.CurrentMappingVersionId!=mapping.Id
            || invoice.MappingRevision!=mapping.MappingRevision)
            throw new MatchingProblem("InvoiceComparisonVersionChanged",409);
        if (invoice.ActiveMatchClaimResultId is not null)
            throw new MatchingProblem("InvoiceExistingClaimRequiresOwnerResolution",409);
        var originals=await db.SupplierInvoiceLines.Where(x=>x.SupplierInvoiceId==id && x.IsActive)
            .OrderBy(x=>x.LineNumber).ToArrayAsync(token);
        _=PrincipalInvoiceMappingEvidence.VerifyStored(mapping,invoice,originals);
        var reviewMapping=InvoiceMappingReviewSourceDispatch.Verify(mapping,invoice,originals);
        var now=Now();
        var policies=await db.PolicyVersions.Where(x=>x.PolicyKey==InvoiceMatchingPolicyRules.PolicyKey
            && x.State=="Published" && x.EffectiveFrom<=now && (x.EffectiveTo==null || now<x.EffectiveTo))
            .Take(2).ToArrayAsync(token);
        if (policies.Length!=1) throw new MatchingProblem("InvoicePublishedMatchingPolicyUnavailable",409);
        var policy=policies[0];
        await ProcurementOperationalPolicyCurrentPublication.RequireAsync(db,scope,http.RequestServices,policy,token);
        var policySha=ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson);
        var hashes=await DocumentHashes(db,scope,http,id,reviewMapping,token);
        var reviewContext=new InvoiceMappingReviewContext(scope.TenantId,scope.LegalEntityId,invoice.BranchRef,
            scope.SubjectId,worker,policy.Id,policySha,now,hashes);
        var review=await ProcurementInvoiceUnitReviewRoutes.RetainedReview(db,cases,scope,http,
            invoice,originals,mapping,reviewContext,token);
        if (review is null || !PrincipalInvoiceMatchingBridge.ReviewCovers3(mapping,invoice,originals,review,reviewContext))
            throw new MatchingProblem("PrincipalInvoiceOriginalUnitReviewRequired",409);
        var approvedBaseline=await ApprovedPrincipalInvoiceBaseline(db,cases,scope,now,token);
        var ownerContext=new InvoiceExposureReadContext(scope.TenantId,scope.LegalEntityId,invoice.BranchRef,
            issued.Id,issued.RevisionId,invoice.SupplierId,invoice.CurrencyCode,policySha,now);
        var owner=await db.BuildPrincipalInvoiceOwnerLeaseAsync(ownerContext,issuedWitness,approvedBaseline,exposure,token);
        // The factory itself checks this current graph while building its lease.
        // Reload the typed evidence in the SAME snapshot for CompleteHistory/Claims;
        // do not replace it with a client completeness flag or detached owner result.
        var current=await db.ReadPrincipalInvoiceCurrentClaimsAsync(issuedWitness,token);
        var locals=current.ActiveClaims.Select(x=>new InvoiceActiveLocalClaim(x.Allocation,x.Invoice)).ToArray();
        var history=InvoiceMatchExecutionEvidence.CompleteHistory(owner,ownerContext,exposure,locals);
        var projected=PrincipalInvoiceMatchingBridge.Project(mapping,invoice,originals,review,reviewContext,current.RetainedSources);
        var accepted=PrincipalInvoiceMatchingBridge.ProjectAcceptances(current.RetainedSources,scope.TenantId,scope.LegalEntityId);
        var duplicates=await db.SupplierInvoices.Where(x=>x.SupplierId==invoice.SupplierId
            && x.NormalizedInvoiceNumber==invoice.NormalizedInvoiceNumber && x.CurrencyCode==invoice.CurrencyCode)
            .Select(x=>new InvoiceDuplicateIdentity(x.Id,x.TenantId,x.LegalEntityId,x.SupplierId,x.NormalizedInvoiceNumber,x.CurrencyCode))
            .ToArrayAsync(token);
        var duplicateWatermark=Fingerprint(new{schemaVersion=3,identities=duplicates.OrderBy(x=>x.InvoiceId).ToArray()});
        var command=new InvoiceMatchCommand(key,input.Version,now,input.Reason?.Trim());
        var comparisonInput=new InvoiceMatchingInput(projected,issued,accepted,InvoiceMatchingPolicyRules.Project(policy),
            history,new(duplicateWatermark,true,duplicates));
        var comparison=InvoiceMatchingFoundation.Evaluate(command,scope,comparisonInput);
        var result=new InvoiceMatchResult{SupplierInvoiceId=id,MatchType=comparison.Lines.Count==0?"Unresolved":
                string.Join("+",comparison.Lines.Select(x=>x.Method.ToString()).Distinct().Order()),
            Outcome=comparison.Outcome,PolicyVersionId=policy.Id,MatchedAt=now,ExecutionVersion=2,SourceSchemaVersion=3,MappingVersionId=mapping.Id,
            PurchaseOrderId=mapping.PurchaseOrderId,IssuedVersionId=mapping.IssuedVersionId,InvoiceVersion=invoice.Version,
            InvoiceOriginalSha256=mapping.InvoiceOriginalSha256,ActorPrincipalId=scope.SubjectId,ActorWorkerId=worker,
            CommandKey=key,InputFingerprint=comparison.InputFingerprint,EvidenceJson=comparison.EvidenceJson,
            EvidenceSha256=comparison.EvidenceSha256,PolicyRulesSha256=policySha,
            SourceWatermarksJson=InvoiceMatchExecutionEvidence.SourceWatermarks(comparisonInput,mapping,review)};
        db.AddScoped(result);
        exposure.CoverageState="Verified";exposure.OwnerReference=owner.OwnerReference;
        exposure.OwnerWatermark=owner.Watermark;exposure.OwnerEvidenceSha256=owner.EvidenceSha256;
        exposure.OwnerValidUntil=owner.ValidUntil;
        if (comparison.ComparisonPassed)
        {
            var claims=PrincipalInvoiceMatchingBridge.Claims(comparison,comparisonInput,result,exposure,owner,now,
                invoice,originals,mapping,review,reviewContext,command,scope,current.ActiveClaims,policy,current.RetainedSources);
            foreach(var claim in claims)db.AddScoped(claim);
            exposure.Sequence=checked(exposure.Sequence+1);invoice.ActiveMatchClaimResultId=result.Id;
        }
        foreach(var issue in comparison.Discrepancies)
            db.AddScoped(new InvoiceException{SupplierInvoiceId=id,MatchResultId=result.Id,SupplierInvoiceLineId=issue.InvoiceLineId,
                Type=issue.Code,Status="Open",OwnerSubjectId=scope.SubjectId,OwnerWorkerId=worker,
                DiscrepancyKey=Fingerprint(new{schemaVersion=3,invoiceId=id,issue.InvoiceLineId,issue.Code}),
                Reason="Review the original invoice and authoritative source evidence."});
        invoice.LastMatchResultId=result.Id;invoice.MatchRevision=checked(invoice.MatchRevision+1);
        invoice.MatchingState=comparison.ComparisonPassed?"ComparisonPassed":"ComparisonBlocked";
        db.AddScoped(new AuditEvent{RecordType="SupplierInvoice",RecordId=id,Action="PrincipalInvoiceCompared",
            ActorSubjectId=scope.SubjectId,CorrelationId=key.ToString("D"),ChangedFieldsJson=JsonSerializer.Serialize(new
            {schemaVersion=3,sourceSchemaVersion=3,mappingVersionId=mapping.Id,resultId=result.Id,commandKey=key,
                outcome=result.Outcome,hasCapacityClaim=invoice.ActiveMatchClaimResultId==result.Id,canPostToFinance=false},Json)});
        db.AddScoped(new OutboxEvent{AggregateType="SupplierInvoice",AggregateId=id,EventType="supplier_invoice.compared.v3",
            EventVersion=3,IdempotencyKey=key.ToString("D"),PayloadJson=JsonSerializer.Serialize(new
            {schemaVersion=3,sourceSchemaVersion=3,invoiceId=id,resultId=result.Id,outcome=result.Outcome,
                executionVersion=2,principalEvidenceVersion=2,canPostToFinance=false},Json)});
        await db.SaveChangesAsync(token);
        var response=JsonSerializer.Serialize(new{invoiceId=id,commandKey=key,commandPermission="procurement-invoice-match",
            commandSchemaVersion=3,sourceSchemaVersion=3,recordedHistoricalResult=true,isCurrentApproval=false,canPostToFinance=false,
            result=new{result.Id,result.Outcome,invoice.Version,invoice.MatchingState,
                hasCapacityClaim=invoice.ActiveMatchClaimResultId==result.Id,canPostToFinance=false,
                discrepancies=comparison.Discrepancies,sourceBlockers=Array.Empty<string>()}},Json);
        db.AddScoped(new ProcurementCommercialCommandReceipt{PurchaseOrderId=mapping.PurchaseOrderId,
            ActorPrincipalId=scope.SubjectId,Operation="InvoiceCompare",CommandKey=key,Fingerprint=fingerprint,
            ResponseJson=response,CompletedAt=Now()});
        return response;
    }

    internal static string PrincipalCompareFingerprint(Guid invoiceId,CompareInput input)
        => Fingerprint(new{schemaVersion=3,sourceSchemaVersion=3,id=invoiceId,input});

    private static async Task<string?> ReplayPrincipalCompare(ProcurementDbContext db,ProcurementRequestScope scope,
        Guid key,string fingerprint,Guid invoiceId,Guid orderId,CancellationToken token)
    {
        var receipt=await db.CommercialCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x=>
            x.Operation=="InvoiceCompare" && x.CommandKey==key && x.ActorPrincipalId==scope.SubjectId,token);
        if(receipt is null)return null;
        using var body=JsonDocument.Parse(receipt.ResponseJson);
        if(receipt.Fingerprint!=fingerprint || receipt.PurchaseOrderId!=orderId
            || body.RootElement.GetProperty("invoiceId").GetGuid()!=invoiceId
            || body.RootElement.GetProperty("sourceSchemaVersion").GetInt32()!=3
            || body.RootElement.GetProperty("commandSchemaVersion").GetInt32()!=3)
            throw new MatchingProblem("InvoiceMatchingCommandKeyConflict",409);
        return receipt.ResponseJson;
    }

    private static async Task<InvoiceProcurementApprovedBaselineWitness> ApprovedPrincipalInvoiceBaseline(
        ProcurementDbContext db,ApprovalDbContext cases,ProcurementRequestScope scope,DateTimeOffset at,CancellationToken token)
    {
        var baselines=await db.InvoiceProcurementOwnerBaselines.AsNoTracking().Where(x=>x.CapturedAtUtc<=at)
            .OrderBy(x=>x.Id).Take(101).ToArrayAsync(token);
        if(baselines.Length>100)throw new MatchingProblem("InvoiceApprovedBaselineReconciliationRequired",409);
        var ids=baselines.Select(x=>x.CoreCaseId).ToArray();
        var workflows=await cases.Cases.Where(x=>ids.Contains(x.Id) && x.TenantId==scope.TenantId
            && x.LegalEntityId==scope.LegalEntityId && x.State=="Approved"
            && x.SourceModule=="procurement" && x.SourceType==InvoiceProcurementBaselineEvidence.SourceType)
            .Include(x=>x.Stages).ThenInclude(x=>x.Assignments).ToArrayAsync(token);
        Guid caseId;
        try{caseId=SelectSingleApprovedBaselineCase(baselines,workflows,scope.TenantId,scope.LegalEntityId,at);}
        catch(InvalidOperationException){throw new MatchingProblem("InvoiceApprovedBaselineReconciliationRequired",409);}
        var workflow=workflows.Single(x=>x.Id==caseId);
        var baseline=baselines.Single(x=>x.CoreCaseId==caseId);
        var receipts=await db.InvoiceProcurementOwnerCommandReceipts.AsNoTracking().Where(x=>
            x.BaselineId==baseline.Id && x.Operation=="Decide").Take(2).ToArrayAsync(token);
        if(receipts.Length!=1)throw new MatchingProblem("InvoiceApprovedBaselineReceiptUnavailable",409);
        var receipt=receipts[0];
        var recorded=InvoiceProcurementOwnerReceiptEvidence.Verify(receipt,baseline);
        if(recorded.CoreCaseState!="Approved" || recorded.CoreCaseId!=workflow.Id
            || recorded.CoreCaseVersion!=workflow.Version || recorded.CoreDecisionId is null)
            throw new MatchingProblem("InvoiceApprovedBaselineReceiptUnavailable",409);
        var decisions=await cases.Decisions.Where(x=>x.CaseId==workflow.Id).ToArrayAsync(token);
        // These are historical, immutable signer aliases; a current account lookup
        // cannot rewrite a previously independent Core baseline approval.
        return new(workflow,decisions,receipt.ActorPrincipalId,receipt.ActorWorkerAliasId);
    }

    internal static Guid SelectSingleApprovedBaselineCase(IReadOnlyList<InvoiceProcurementOwnerBaseline> baselines,
        IReadOnlyList<ApprovalCase> workflows,string tenant,Guid company,DateTimeOffset at)
    {
        var candidates=(from baseline in baselines from workflow in workflows
            where baseline.TenantId==tenant && baseline.LegalEntityId==company && baseline.Id!=Guid.Empty
                && baseline.CapturedAtUtc!=default && baseline.CapturedAtUtc<=at
                && workflow.Id==baseline.CoreCaseId && workflow.TenantId==tenant && workflow.LegalEntityId==company
                && workflow.State=="Approved" && workflow.SourceModule=="procurement"
                && workflow.SourceType==InvoiceProcurementBaselineEvidence.SourceType
                && workflow.SourceId==baseline.Id && workflow.SourceVersion==1
                && workflow.CompletedAt is { } completed && completed<=at
            select workflow.Id).ToArray();
        if(candidates.Length!=1)throw new InvalidOperationException("InvoiceApprovedBaselineReconciliationRequired");
        return candidates[0];
    }
}
