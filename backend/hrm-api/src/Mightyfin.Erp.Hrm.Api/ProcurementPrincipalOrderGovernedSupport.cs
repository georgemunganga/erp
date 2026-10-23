using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalOrderDraftRoutes
{
    private sealed record GovernedOrder(PurchaseOrder Order,PurchaseOrderLine[] Lines,
        PrincipalOrderSourceBinding[] Bindings,PurchaseOrderSourceLine[] Sources,Supplier Supplier);
    private static void GovernedEnabled(HttpContext http)
    {
        var options=http.RequestServices.GetRequiredService<ProcurementOrderExecutionOptions>();
        if(!options.PrincipalExecutionEnabled || !options.ExecutionDatabaseConfigured)
            throw new Problem("PrincipalOrderGovernanceUnavailable",503);
    }
    private static async Task<GovernedOrder> LoadGoverned(ProcurementDbContext db,ApprovalDbContext cases,
        HrmDbContext core,ProcurementRequestScope scope,HttpContext http,IAuthorizationService auth,
        string action,Guid id,CancellationToken ct,bool lockSources=true)
    {
        var requestIds=await db.PrincipalOrderSourceBindings.AsNoTracking().Where(x=>x.PurchaseOrderId==id)
            .Select(x=>x.PurchaseRequestId).Distinct().OrderBy(x=>x).ToArrayAsync(ct);
        if(requestIds.Length is <1 or >100)throw new Problem("OrderOriginalSourceUnavailable",409);
        if(lockSources)
        {
            foreach(var requestId in requestIds)await ProcurementApprovalExecutionLocks.LockRequestAsync(db,scope,requestId,ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.purchase_orders WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE",ct);
        }
        var order=await db.PurchaseOrders.SingleOrDefaultAsync(x=>x.Id==id&&x.PrincipalDraftSchemaVersion==2,ct)
            ??throw new Problem("OrderNotFound",404);
        _=await Actor(core,scope,http,auth,action,order.BranchRef,true,ct);
        var lines=await db.PurchaseOrderLines.Where(x=>x.PurchaseOrderId==id&&x.IsActive)
            .OrderBy(x=>x.LineNumber).ToArrayAsync(ct);
        var bindings=await db.PrincipalOrderSourceBindings.Where(x=>x.PurchaseOrderId==id).ToArrayAsync(ct);
        PrincipalOrderDraftPins.Verify(order,lines,bindings);
        if(bindings.Length is <1 or >100 || !requestIds.ToHashSet().SetEquals(bindings.Select(x=>x.PurchaseRequestId)))
            throw new Problem("OrderOriginalSourceUnavailable",409);
        var result=new List<PurchaseOrderSourceLine>();
        foreach(var binding in bindings)
        {
            if(binding.BranchId!=order.BranchRef)throw new Problem("OrderOriginalSourceUnavailable",409);
            var source=await ApprovedDemandSourceLoader.LoadRetainedAsync(db,cases,
                new SourceBranchScope(scope,binding.BranchId),binding.PurchaseRequestId,binding.PurchaseRequestLineId,
                new RetainedDemandSourceIdentity(binding.SubmissionId,binding.ApprovalOutcomeId,binding.MethodId),ct);
            var line=lines.SingleOrDefault(x=>x.Id==binding.PurchaseOrderLineId);
            var allocation=source.Allocations.SingleOrDefault(x=>x.Id==binding.AllocationId);
            if(line is null || allocation is null || allocation.TargetId!=id || allocation.TargetType!="PurchaseOrder"
                || allocation.Quantity!=binding.Quantity || allocation.EstimatedValue!=binding.EstimatedValue
                || allocation.CurrencyCode!=binding.CurrencyCode || source.Request.BranchRef!=binding.BranchId
                || source.Submission.SnapshotSchemaVersion!=3 || source.Submission.FinanceIdentitySchemaVersion!=2
                || source.Submission.RequestSnapshotSha256!=binding.RequestSnapshotSha256
                || source.Submission.PolicyDecisionSha256!=binding.PolicyDecisionSha256
                || source.Submission.PolicyRulesSha256!=binding.PolicyRulesSha256
                || source.Submission.FinanceSnapshotSha256!=binding.FinanceSnapshotSha256
                || source.Submission.PrincipalIdentitySha256!=binding.PrincipalIdentitySha256
                || source.Outcome.Id!=binding.ApprovalOutcomeId || source.CoreCase.Id!=binding.CoreApprovalCaseId
                || source.FinalDecision.Id!=binding.CoreDecisionId || source.Finance.Id!=binding.FinanceSnapshotId
                || source.Method.Version!=binding.MethodRevision)
                throw new Problem("OrderOriginalSourceUnavailable",409);
            _=PurchaseDemandAllocationBalance.Evaluate(source.Request,source.Line,source.Allocations,
                source.AllocationOwners??[],scope.TenantId,scope.LegalEntityId);
            result.Add(new(line,source,allocation));
        }
        var supplier=await db.Suppliers.Include(x=>x.Qualifications).SingleOrDefaultAsync(x=>x.Id==order.SupplierId,ct)
            ??throw new Problem("OrderSupplierUnavailable",409);
        return new(order,lines,bindings,result.ToArray(),supplier);
    }
    private static async Task CurrentPositiveSources(ProcurementDbContext db,HrmDbContext core,
        ProcurementRequestScope scope,HttpContext http,GovernedOrder load,DateTimeOffset now,CancellationToken ct)
    {
        if(!SupplierEligibility.CanReceiveNewOrders(load.Supplier,now))throw new Problem("OrderSupplierUnavailable",409);
        foreach(var source in load.Sources)
        {
            _=PurchaseOrderSourceAllocation.Check(source.Source,scope.TenantId,scope.LegalEntityId,now);
            var finance=await ProcurementApprovalFinanceEligibility.CheckPrincipalAsync(source.Source.Request,
                source.Source.Finance,http.RequestServices.GetRequiredService<ProcurementApprovalExecutionOptions>(),
                core,db,scope,now,ct);
            if(source.Source.Submission.FinanceEvidenceMode!=finance.Mode
                || source.Source.Submission.ManualEvidenceAuthorityRef!=finance.Reference)
                throw new Problem("OrderFinanceAuthorityChanged",409);
        }
        if(await ProcurementCatalogClassificationBinding.StoredIssue(db,load.Sources.Select(x=>x.Source.Line).ToArray(),ct) is not null
            || await ProcurementCatalogClassificationBinding.CommercialIssue(db,load.Order.SupplierId,
                load.Order.CurrencyCode,load.Sources.Select(x=>(x.Source.Line,x.OrderLine)).ToArray(),ct) is not null)
            throw new Problem("OrderCatalogPriceChanged",409);
    }
    private static ProcurementManualFinancePolicy ManualPolicy(HttpContext http,Guid company,DateTimeOffset now)
    {
        var options=http.RequestServices.GetRequiredService<ProcurementOrderExecutionOptions>();
        var policies=options.ManualCommitmentPolicies.Where(x=>x.Enabled&&x.LegalEntityId==company
            &&x.EffectiveFrom!=default&&x.EffectiveFrom<=now&&(x.EffectiveUntil is null||now<x.EffectiveUntil)
            &&!string.IsNullOrWhiteSpace(x.ApprovedPolicyReference)
            &&(x.EvidenceMaximumAgeMinutes is null or >0)).ToArray();
        return policies.Length==1?policies[0]:throw new Problem("OrderApprovedFinancePolicyRequired",409);
    }
    internal static bool RecorderCanActInBranch(ProcurementPrincipalPeople.Person recorder,Guid? branch)
        => branch is { } id && id!=Guid.Empty
            && (recorder.OrganisationWide || recorder.AllowedBranches.Contains(id));
    private static async Task<PrincipalOrderFundingEvidence> CurrentFunding(ProcurementDbContext db,
        HrmDbContext core,ProcurementRequestScope scope,HttpContext http,GovernedOrder load,int revision,
        DateTimeOffset now,CancellationToken ct)
    {
        var funding=await db.PrincipalOrderFundingEvidence.SingleOrDefaultAsync(x=>x.PurchaseOrderId==load.Order.Id
            &&x.CommercialRevision==revision,ct)??throw new Problem("OrderManualCommitmentRequired",409);
        var policy=ManualPolicy(http,scope.LegalEntityId,now);
        var recorder=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,
            funding.RecordedByPrincipalId,ct);
        var commercial=PrincipalOrderCommercialSnapshot.Capture(load.Order,load.Lines,load.Supplier,revision);
        var totals=PurchaseOrderCommercialWorkflow.Totals(load.Lines);
        var expected=load.Sources.Select(x=>x.Source.Finance.ReservationRef).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        if(recorder is null || !recorder.Permissions.Contains("procurement-po-finance-confirm")
            || !RecorderCanActInBranch(recorder,load.Order.BranchRef)
            || recorder.CoreLinkedWorkerId!=funding.RecordedByWorkerAliasId
            || funding.ApprovedManualPolicyReference!=policy.ApprovedPolicyReference
            || funding.CurrencyCode!=load.Order.CurrencyCode || funding.AttestedGrossAmount!=totals.Gross
            || funding.CommercialSnapshotJson!=commercial
            || funding.CommercialSnapshotSha256!=ProcurementEvidenceEncoding.Digest(commercial)
            || funding.OriginalDirectSourceSha256!=load.Order.OriginalDirectSourceSha256
            || funding.ObservedAt>now || policy.EvidenceMaximumAgeMinutes is { } minutes
                && funding.ObservedAt<now.AddMinutes(-minutes)
            || !expected.SequenceEqual(System.Text.Json.JsonSerializer.Deserialize<string[]>(funding.SourceReservationsJson)
                ??[]))throw new Problem("OrderManualCommitmentChanged",409);
        foreach(var binding in load.Bindings)
            if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,binding.BuyerPrincipalId,
                binding.BuyerWorkerAliasId,recorder.PrincipalId,recorder.CoreLinkedWorkerId,ct)
                || !await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,
                    binding.RequesterPrincipalId.ToString("D"),binding.RequesterWorkerAliasId,
                    recorder.PrincipalId,recorder.CoreLinkedWorkerId,ct))
                throw new Problem("OrderManualCommitmentSegregationChanged",409);
        foreach(var source in load.Sources)
            if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,
                source.Source.Finance.CheckedBySubjectId,source.Source.Finance.CheckedByWorkerId,
                recorder.PrincipalId,recorder.CoreLinkedWorkerId,ct))
                throw new Problem("OrderManualCommitmentSegregationChanged",409);
        return funding;
    }
}
