using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record ProcurementApprovalEffects(PurchaseRequestApprovalOutcome Outcome,
    PurchaseRequestFinanceReleaseRequest? ReleaseRequest, IReadOnlyList<OutboxEvent> Events, bool IsReplay);
public sealed record ProcurementFinanceReleaseEffects(PurchaseRequestFinanceReleaseConfirmation Confirmation,
    IReadOnlyList<OutboxEvent> Events, bool IsReplay);

/// <summary>Returned only by a server-side authoritative resolver; never bind this record from an HTTP body.</summary>
public sealed record VerifiedFinanceRelease(string TenantId, Guid LegalEntityId, string ReservationRef,
    decimal ReleasedAmount, string CurrencyCode, string Status, string EvidenceRef, string EvidenceSource,
    string VerifiedBySubjectId, DateTimeOffset ConfirmedAt);
public interface IPurchaseRequestFinanceReleaseAuthority
{
    Task<VerifiedFinanceRelease?> ResolveAsync(PurchaseRequestFinanceReleaseRequest request,
        string evidenceReference, CancellationToken ct);
}

/// <summary>
/// Source effects only. The coordinator must load complete scoped records, enforce current identity,
/// permissions, spending authority and Finance eligibility, lock source/Core/evidence, and persist all
/// returned records/audit/outbox in the SAME transaction. A Core status supplied by a client is not authority.
/// </summary>
public static class PurchaseRequestApprovalOutcomes
{
    public static ProcurementApprovalEffects Apply(PurchaseRequest request, PurchaseRequestApprovalSubmission pin,
        ApprovalCase approvalCase, ApprovalDecision decision, PolicyEvaluation evaluation, PolicyVersion policy,
        BudgetCheckSnapshot pinnedFinance, string tenant, Guid entity, DateTimeOffset now,
        PurchaseRequestApprovalOutcome? existing = null, PurchaseRequestFinanceReleaseRequest? release = null)
    {
        RequireCase(request, pin, approvalCase, decision, tenant, entity);
        var validation = existing is null
            ? PurchaseRequestApprovalPinning.Verify(pin, request, evaluation, policy, pinnedFinance, tenant, entity)
            : PurchaseRequestApprovalPinning.VerifyBusinessEvidence(pin, request, evaluation, policy, pinnedFinance, tenant, entity);
        if (!validation.IsValid) throw new InvalidOperationException("Approval evidence changed: " + string.Join(",", validation.Blockers));
        var effectKey = $"procurement.pr.approval.{approvalCase.Id:N}.{approvalCase.State.ToLowerInvariant()}.v1";
        if (existing is not null)
        {
            if (!Scoped(existing, tenant, entity) || existing.SubmissionId != pin.Id
                || existing.PurchaseRequestId != request.Id || existing.SubmittedBusinessVersion != pin.SubmittedBusinessVersion
                || existing.CoreApprovalCaseId != approvalCase.Id || existing.CoreDecisionId != decision.Id
                || existing.DecisionCommandKey != decision.IdempotencyKey || existing.Outcome != approvalCase.State
                || existing.EffectKey != effectKey || existing.ActorSubjectId != decision.ActorSubjectId)
                throw new InvalidOperationException("The approval outcome does not match this exact decision.");
            if (approvalCase.State == "Approved")
            {
                if (release is not null || request.Lines.Where(Active).Any(x =>
                    x.ApprovalStatus != "Approved" || x.ApprovedQuantity != x.RequestedQuantity))
                    throw new InvalidOperationException("Approved source effects changed.");
            }
            else
            {
                RequireRelease(release, existing, pin, request, pinnedFinance, tenant, entity);
                var expectedSourceState = approvalCase.State == "Returned"
                    ? request.Status is "ReturnPendingFinanceRelease" or "ReturnedForChanges"
                    : request.Status == "Rejected";
                if (!expectedSourceState || request.Lines.Where(Active).Any(x =>
                        x.ApprovedQuantity != 0 || x.ApprovalStatus != approvalCase.State))
                    throw new InvalidOperationException("Non-orderable source effects changed.");
            }
            return new(existing, release, [], true);
        }
        if (release is not null || now == default || now < decision.DecidedAt)
            throw new InvalidOperationException("A new approval outcome requires a valid application time and no prior release request.");
        var outcome = new PurchaseRequestApprovalOutcome
        {
            TenantId = tenant, LegalEntityId = entity, SubmissionId = pin.Id, PurchaseRequestId = request.Id,
            SubmittedBusinessVersion = pin.SubmittedBusinessVersion, CoreApprovalCaseId = approvalCase.Id,
            CoreDecisionId = decision.Id, DecisionCommandKey = decision.IdempotencyKey,
            Outcome = approvalCase.State, EffectKey = effectKey, ActorSubjectId = decision.ActorSubjectId, AppliedAt = now,
        };
        request.ApprovalState = approvalCase.State;
        if (approvalCase.State == "Approved")
        {
            request.Status = "Approved";
            foreach (var line in request.Lines.Where(Active))
            {
                line.ApprovedQuantity = line.RequestedQuantity;
                line.ApprovalStatus = "Approved";
            }
        }
        else
        {
            request.Status = approvalCase.State == "Returned" ? "ReturnPendingFinanceRelease" : "Rejected";
            foreach (var line in request.Lines.Where(Active)) line.ApprovalStatus = approvalCase.State;
            release = new PurchaseRequestFinanceReleaseRequest
            {
                TenantId = tenant, LegalEntityId = entity, ApprovalOutcomeId = outcome.Id, SubmissionId = pin.Id,
                PurchaseRequestId = request.Id, SubmittedBusinessVersion = pin.SubmittedBusinessVersion,
                CoreApprovalCaseId = approvalCase.Id, FinanceSnapshotId = pinnedFinance.Id, BranchRef = request.BranchRef,
                ReservationRef = pinnedFinance.ReservationRef!, ReservedAmount = pinnedFinance.CheckedAmount,
                CurrencyCode = pinnedFinance.CurrencyCode, EffectKey = $"procurement.pr.release.{pin.Id:N}.v1",
                RequestedBySubjectId = decision.ActorSubjectId, RequestedAt = now,
            };
        }
        var events = new List<OutboxEvent>
        {
            Event(request, effectKey, $"purchase_request.{approvalCase.State.ToLowerInvariant()}.v1", new
            { request.Id, pin.SubmittedBusinessVersion, caseId = approvalCase.Id, outcomeId = outcome.Id, request.Status }),
        };
        if (release is not null) events.Add(Event(request, release.EffectKey, "procurement.finance_release.requested.v1",
            new { releaseRequestId = release.Id, request.Id, pin.SubmittedBusinessVersion }));
        return new(outcome, release, events, false);
    }

    public static async Task<ProcurementFinanceReleaseEffects> ResolveReleaseAsync(PurchaseRequest request,
        PurchaseRequestApprovalSubmission pin, ApprovalCase approvalCase, ApprovalDecision decision,
        PurchaseRequestApprovalOutcome outcome, PurchaseRequestFinanceReleaseRequest release,
        PolicyEvaluation evaluation, PolicyVersion policy, BudgetCheckSnapshot pinnedFinance,
        string tenant, Guid entity, string evidenceReference, IPurchaseRequestFinanceReleaseAuthority authority,
        DateTimeOffset now, PurchaseRequestFinanceReleaseConfirmation? existing = null, CancellationToken ct = default)
    {
        // Reuse exact outcome/case/business evidence checks without reapplying any source effect.
        _ = Apply(request, pin, approvalCase, decision, evaluation, policy, pinnedFinance, tenant, entity, now, outcome, release);
        if (approvalCase.State is not ("Returned" or "Rejected")) throw new InvalidOperationException("This outcome needs no Finance release.");
        evidenceReference = evidenceReference?.Trim() ?? string.Empty;
        if (evidenceReference.Length is < 1 or > 500) throw new ArgumentException("An authoritative release evidence reference is required.");
        var evidence = await authority.ResolveAsync(release, evidenceReference, ct);
        if (evidence is null || evidence.TenantId != tenant || evidence.LegalEntityId != entity
            || evidence.ReservationRef != release.ReservationRef || evidence.CurrencyCode != release.CurrencyCode
            || evidence.ReleasedAmount != release.ReservedAmount || evidence.Status != "Released"
            || evidence.EvidenceRef != evidenceReference || string.IsNullOrWhiteSpace(evidence.EvidenceSource)
            || string.IsNullOrWhiteSpace(evidence.VerifiedBySubjectId)
            || evidence.ConfirmedAt < pinnedFinance.CheckedAt || evidence.ConfirmedAt > now)
            throw new InvalidOperationException("Finance has not authoritatively confirmed this reservation's full release.");
        var key = $"procurement.pr.release_confirmed.{release.Id:N}.v1";
        if (existing is not null)
        {
            if (!Scoped(existing, tenant, entity) || existing.ReleaseRequestId != release.Id || existing.EffectKey != key
                || existing.ReservationRef != evidence.ReservationRef || existing.ReleasedAmount != evidence.ReleasedAmount
                || existing.CurrencyCode != evidence.CurrencyCode || existing.EvidenceRef != evidence.EvidenceRef
                || existing.EvidenceSource != evidence.EvidenceSource || existing.VerifiedBySubjectId != evidence.VerifiedBySubjectId
                || existing.FinanceConfirmedAt != evidence.ConfirmedAt)
                throw new InvalidOperationException("Release confirmation replay does not match the recorded evidence.");
            return new(existing, [], true);
        }
        if (request.Status != (approvalCase.State == "Returned" ? "ReturnPendingFinanceRelease" : "Rejected"))
            throw new InvalidOperationException("The source is no longer awaiting this release.");
        var confirmation = new PurchaseRequestFinanceReleaseConfirmation
        {
            TenantId = tenant, LegalEntityId = entity, ReleaseRequestId = release.Id,
            ReservationRef = evidence.ReservationRef, ReleasedAmount = evidence.ReleasedAmount,
            CurrencyCode = evidence.CurrencyCode, EvidenceRef = evidence.EvidenceRef, EvidenceSource = evidence.EvidenceSource,
            VerifiedBySubjectId = evidence.VerifiedBySubjectId, FinanceConfirmedAt = evidence.ConfirmedAt,
            RecordedAt = now, EffectKey = key,
        };
        if (approvalCase.State == "Returned")
        {
            request.Status = "ReturnedForChanges";
            request.ReturnedAt = now;
            request.ReturnedBySubjectId = decision.ActorSubjectId;
            request.ReturnReason = decision.Reason;
        }
        return new(confirmation, [Event(request, key, "procurement.finance_release.confirmed.v1",
            new { request.Id, releaseRequestId = release.Id, confirmationId = confirmation.Id, request.Status })], false);
    }

    private static bool Active(PurchaseRequestLine line) => line.ApprovalStatus != "Cancelled";
    private static bool Scoped(ProcurementRecord row, string tenant, Guid entity) => row.TenantId == tenant && row.LegalEntityId == entity;
    private static void RequireCase(PurchaseRequest request, PurchaseRequestApprovalSubmission pin,
        ApprovalCase approvalCase, ApprovalDecision decision, string tenant, Guid entity)
    {
        if (approvalCase.TenantId != tenant || approvalCase.LegalEntityId != entity || decision.TenantId != tenant
            || decision.LegalEntityId != entity || approvalCase.Id != pin.CoreApprovalCaseId
            || approvalCase.SourceModule != "procurement" || approvalCase.SourceType != "purchase_request"
            || approvalCase.SourceId != request.Id || approvalCase.SourceVersion != pin.SubmittedBusinessVersion
            || approvalCase.PolicyVersionId != pin.PolicyVersionId || approvalCase.PolicyVersionNumber != pin.PolicyVersionNumber
            || approvalCase.MakerSubjectId != pin.MakerSubjectId || decision.Id == Guid.Empty
            || decision.CaseId != approvalCase.Id || approvalCase.CompletedAt != decision.DecidedAt
            || decision.DecidedAt < pin.PinnedAt
            || approvalCase.State is not ("Approved" or "Rejected" or "Returned")
            || decision.Action != (approvalCase.State == "Approved" ? "Approve" : approvalCase.State == "Rejected" ? "Reject" : "Return")
            || string.IsNullOrWhiteSpace(decision.IdempotencyKey) || decision.IdempotencyKey.Length > 150
            || string.IsNullOrWhiteSpace(decision.ActorSubjectId) || decision.ActorSubjectId == pin.MakerSubjectId
            || approvalCase.State == "Approved" && approvalCase.CompletionKey != decision.IdempotencyKey)
            throw new InvalidOperationException("The completed Core case/decision does not match the pinned Procurement source.");
        var stage = approvalCase.Stages.SingleOrDefault(x => x.Id == decision.StageId);
        var assignment = stage?.Assignments.SingleOrDefault(x => x.Id == decision.AssignmentId);
        if (stage is null || stage.TenantId != tenant || stage.LegalEntityId != entity
            || stage.CaseId != approvalCase.Id || stage.State != approvalCase.State
            || assignment is null || assignment.TenantId != tenant || assignment.LegalEntityId != entity
            || assignment.CaseId != approvalCase.Id || assignment.StageId != stage.Id
            || assignment.AssigneeSubjectId != decision.ActorSubjectId || assignment.State != approvalCase.State)
            throw new InvalidOperationException("The terminal decision does not belong to the completed assigned stage.");
    }
    private static void RequireRelease(PurchaseRequestFinanceReleaseRequest? release, PurchaseRequestApprovalOutcome outcome,
        PurchaseRequestApprovalSubmission pin, PurchaseRequest request, BudgetCheckSnapshot finance, string tenant, Guid entity)
    {
        if (release is null || !Scoped(release, tenant, entity) || release.ApprovalOutcomeId != outcome.Id
            || release.SubmissionId != pin.Id || release.PurchaseRequestId != request.Id
            || release.SubmittedBusinessVersion != pin.SubmittedBusinessVersion || release.CoreApprovalCaseId != pin.CoreApprovalCaseId
            || release.BranchRef != request.BranchRef || release.RequestedBySubjectId != outcome.ActorSubjectId
            || release.RequestedAt != outcome.AppliedAt
            || release.FinanceSnapshotId != finance.Id || release.ReservationRef != finance.ReservationRef
            || release.ReservedAmount != finance.CheckedAmount || release.CurrencyCode != finance.CurrencyCode
            || release.EffectKey != $"procurement.pr.release.{pin.Id:N}.v1")
            throw new InvalidOperationException("Finance release request does not match the pinned reservation/outcome.");
    }
    private static OutboxEvent Event(PurchaseRequest request, string key, string type, object payload) => new()
    {
        TenantId = request.TenantId, LegalEntityId = request.LegalEntityId, EventType = type,
        AggregateType = "purchase_request", AggregateId = request.Id, IdempotencyKey = key,
        PayloadJson = JsonSerializer.Serialize(payload),
    };
}
