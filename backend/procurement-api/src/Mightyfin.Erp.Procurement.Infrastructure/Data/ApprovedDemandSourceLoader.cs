using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public enum ApprovedDemandLoadFailure
{
    SourceNotFound, SourceOutsideScope, SourceNotApproved, ApprovalEvidenceMissing,
    CoreCaseMissing, CoreDecisionMissing, MethodUnavailable,
}
public sealed class ApprovedDemandLoadException(ApprovedDemandLoadFailure failure) : Exception(failure.ToString())
{
    public ApprovedDemandLoadFailure Failure { get; } = failure;
}

/// <summary>One owning EF loader for approved demand and the complete retained allocation ledger.
/// Loading is NOT eligibility/action authority: callers retain current canonical actor/record scope,
/// Finance/catalog owner validation, route-specific rules, sorted source locks and shared transaction.
/// Returned runtime owner facts remain outside all retained PR/PO evidence encoders.</summary>
public sealed record RetainedDemandSourceIdentity(Guid SubmissionId, Guid OutcomeId, Guid MethodId);

public static class ApprovedDemandSourceLoader
{
    public static Task<ApprovedPurchaseSource> LoadAsync(ProcurementDbContext db, ApprovalDbContext core,
        IProcurementScope scope, Guid requestId, Guid lineId, CancellationToken ct = default)
        => LoadCore(db, core, scope, requestId, lineId, null, ct);

    /// <summary>Original owned RFQ binding identity only, never client authority. Historical
    /// loading does not reauthorize a new commitment or require today's PR approval pointer.</summary>
    public static Task<ApprovedPurchaseSource> LoadRetainedAsync(ProcurementDbContext db, ApprovalDbContext core,
        IProcurementScope scope, Guid requestId, Guid lineId, RetainedDemandSourceIdentity retained, CancellationToken ct = default)
    {
        if (retained is null || retained.SubmissionId == Guid.Empty || retained.OutcomeId == Guid.Empty || retained.MethodId == Guid.Empty)
            throw new ApprovedDemandLoadException(ApprovedDemandLoadFailure.ApprovalEvidenceMissing);
        return LoadCore(db, core, scope, requestId, lineId, retained, ct);
    }

    private static async Task<ApprovedPurchaseSource> LoadCore(ProcurementDbContext db, ApprovalDbContext core,
        IProcurementScope scope, Guid requestId, Guid lineId, RetainedDemandSourceIdentity? retained, CancellationToken ct)
    {
        var request = await db.PurchaseRequests.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw new ApprovedDemandLoadException(ApprovedDemandLoadFailure.SourceNotFound);
        if (scope.WorkLocationId is { } selected && request.BranchRef != selected
            || scope.IsConfined && request.BranchRef != scope.WorkLocationId) throw new ApprovedDemandLoadException(ApprovedDemandLoadFailure.SourceOutsideScope);
        var line = request.Lines.SingleOrDefault(x => x.Id == lineId) ?? throw new ApprovedDemandLoadException(ApprovedDemandLoadFailure.SourceNotFound);
        PurchaseRequestApprovalSubmission pin;
        PurchaseRequestApprovalOutcome outcome;
        Guid caseId;
        if (retained is null)
        {
            if (!Guid.TryParse(request.ApprovalInstanceRef, out caseId)) throw new ApprovedDemandLoadException(ApprovedDemandLoadFailure.SourceNotApproved);
            pin = await db.ApprovalSubmissions.SingleOrDefaultAsync(x => x.CoreApprovalCaseId == caseId, ct)
                ?? throw new ApprovedDemandLoadException(ApprovedDemandLoadFailure.ApprovalEvidenceMissing);
            outcome = await db.ApprovalOutcomes.SingleOrDefaultAsync(x => x.SubmissionId == pin.Id && x.Outcome == "Approved", ct)
                ?? throw new ApprovedDemandLoadException(ApprovedDemandLoadFailure.SourceNotApproved);
        }
        else
        {
            pin = await db.ApprovalSubmissions.SingleOrDefaultAsync(x => x.Id == retained.SubmissionId && x.PurchaseRequestId == requestId, ct)
                ?? throw new ApprovedDemandLoadException(ApprovedDemandLoadFailure.ApprovalEvidenceMissing);
            caseId = pin.CoreApprovalCaseId;
            outcome = await db.ApprovalOutcomes.SingleOrDefaultAsync(x => x.Id == retained.OutcomeId && x.SubmissionId == pin.Id && x.Outcome == "Approved", ct)
                ?? throw new ApprovedDemandLoadException(ApprovedDemandLoadFailure.SourceNotApproved);
        }
        var workflow = await core.Cases.SingleOrDefaultAsync(x => x.Id == caseId, ct)
            ?? throw new ApprovedDemandLoadException(ApprovedDemandLoadFailure.CoreCaseMissing);
        var decision = await core.Decisions.SingleOrDefaultAsync(x => x.Id == outcome.CoreDecisionId, ct)
            ?? throw new ApprovedDemandLoadException(ApprovedDemandLoadFailure.CoreDecisionMissing);
        var evaluation = await db.PolicyEvaluations.SingleAsync(x => x.Id == pin.PolicyEvaluationId, ct);
        var policy = await db.PolicyVersions.SingleAsync(x => x.Id == pin.PolicyVersionId, ct);
        var finance = await db.BudgetCheckSnapshots.SingleAsync(x => x.Id == pin.FinanceSnapshotId, ct);
        ProcurementMethod method;
        if (retained is null)
        {
            using var json = JsonDocument.Parse(evaluation.DecisionJson);
            var methodCode = json.RootElement.GetProperty("lines").EnumerateArray().Single(x => x.GetProperty("lineId").GetGuid() == lineId)
                .GetProperty("decision").GetProperty("sourcingMethodCode").GetString();
            method = await db.ProcurementMethods.SingleOrDefaultAsync(x => x.Code == methodCode, ct)
                ?? throw new ApprovedDemandLoadException(ApprovedDemandLoadFailure.MethodUnavailable);
        }
        else method = await db.ProcurementMethods.SingleOrDefaultAsync(x => x.Id == retained.MethodId, ct)
            ?? throw new ApprovedDemandLoadException(ApprovedDemandLoadFailure.MethodUnavailable);
        var allocations = await db.PurchaseRequestAllocations.Where(x => x.PurchaseRequestLineId == lineId).ToListAsync(ct);
        var owners = await LoadTargetOwnersAsync(db, lineId, allocations, ct);
        return new(request, line, pin, outcome, workflow, decision, evaluation, policy, finance, method, allocations, owners);
    }

    /// <summary>Runtime target facts for an already loaded complete ledger, not proof that an
    /// arbitrary provided ledger is complete. No target query runs for an absent target type.</summary>
    public static async Task<IReadOnlyList<PurchaseDemandAllocationOwner>> LoadTargetOwnersAsync(
        ProcurementDbContext db, Guid lineId, IReadOnlyList<PurchaseRequestAllocation> allocations,
        CancellationToken ct = default)
    {
        // Complete target ownership, including inactive retained lines. Never infer ownership from a UUID/string.
        var orderTargets = allocations.Where(x => x.TargetType == "PurchaseOrder" && x.TargetId != null).Select(x => x.TargetId!.Value).Distinct().ToArray();
        var sourcingTargets = allocations.Where(x => x.TargetType == "SourcingEvent" && x.TargetId != null).Select(x => x.TargetId!.Value).Distinct().ToArray();
        var owners = new List<PurchaseDemandAllocationOwner>();
        if (orderTargets.Length != 0)
        {
            var owned = await (from target in db.PurchaseOrders.AsNoTracking()
                join targetLine in db.PurchaseOrderLines.AsNoTracking() on target.Id equals targetLine.PurchaseOrderId
                where orderTargets.Contains(target.Id) && targetLine.PurchaseRequestLineId == lineId
                select new { Target = new PurchaseOrder { Id = target.Id, TenantId = target.TenantId, LegalEntityId = target.LegalEntityId },
                    Line = new PurchaseOrderLine { Id = targetLine.Id, TenantId = targetLine.TenantId, LegalEntityId = targetLine.LegalEntityId,
                        PurchaseOrderId = targetLine.PurchaseOrderId, PurchaseRequestLineId = targetLine.PurchaseRequestLineId } }).ToArrayAsync(ct);
            owners.AddRange(owned.Select(x => PurchaseDemandAllocationOwner.FromOrder(x.Target, x.Line)));
        }
        if (sourcingTargets.Length != 0)
        {
            var owned = await (from target in db.SourcingEvents.AsNoTracking()
                join targetLine in db.SourcingLines.AsNoTracking() on target.Id equals targetLine.SourcingEventId
                where sourcingTargets.Contains(target.Id) && targetLine.PurchaseRequestLineId == lineId
                select new { Target = new SourcingEvent { Id = target.Id, TenantId = target.TenantId, LegalEntityId = target.LegalEntityId },
                    Line = new SourcingLine { Id = targetLine.Id, TenantId = targetLine.TenantId, LegalEntityId = targetLine.LegalEntityId,
                        SourcingEventId = targetLine.SourcingEventId, PurchaseRequestLineId = targetLine.PurchaseRequestLineId } }).ToArrayAsync(ct);
            owners.AddRange(owned.Select(x => PurchaseDemandAllocationOwner.FromSourcing(x.Target, x.Line)));
        }
        return owners;
    }
}
