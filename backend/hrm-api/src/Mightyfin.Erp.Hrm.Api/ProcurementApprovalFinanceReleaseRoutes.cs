using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementApprovalExecutionRoutes
{
    private static async Task<IResult> FinanceReleaseQueue(int? limit, int? offset, HttpContext http,
        ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope,
        ProcurementApprovalExecutionOptions options, IAuthorizationService authorization, CancellationToken ct)
    {
        try
        {
            Enabled(options); _ = await Actor(core, scope, http, authorization, "procurement-budget-check", ct);
            var branches = await core.UserBranchAssignments.AsNoTracking().Where(x => x.UserId == Guid.Parse(scope.SubjectId)
                && !x.IsArchived).Select(x => x.LocationId).ToArrayAsync(ct);
            var requests = ScopedRequests(db, scope);
            if (branches.Length != 0) requests = requests.Where(x => x.BranchRef.HasValue && branches.Contains(x.BranchRef.Value));
            if (scope.IsConfined && branches.Length == 0) requests = requests.Where(_ => false);
            var query = db.FinanceReleaseRequests.AsNoTracking()
                .Where(x => !db.FinanceReleaseConfirmations.Any(c => c.ReleaseRequestId == x.Id))
                .Join(requests, r => r.PurchaseRequestId, p => p.Id, (r, p) => new
                {
                    requestId = p.Id, requestNumber = p.Number, requestVersion = p.Version, requestState = p.Status,
                    releaseRequestId = r.Id, reservationReference = r.ReservationRef, reservedAmount = r.ReservedAmount,
                    currencyCode = r.CurrencyCode, requestedAt = r.RequestedAt,
                });
            var size = Math.Clamp(limit ?? 50, 1, 100); var skip = Math.Clamp(offset ?? 0, 0, 100000);
            var count = await query.CountAsync(ct);
            var records = await query.OrderBy(x => x.requestedAt).ThenBy(x => x.releaseRequestId).Skip(skip).Take(size).ToArrayAsync(ct);
            return Results.Ok(new { records, count, limit = size, offset = skip });
        }
        catch (Exception ex) when (Handled(ex)) { return Failure(ex); }
    }

    private static async Task<IResult> ConfirmFinanceRelease(Guid id, FinanceReleaseInput input,
        HttpContext http, ProcurementRequestScope scope, ProcurementApprovalExecutionOptions options,
        ProcurementApprovalTransaction transaction, IAuthorizationService authorization, CancellationToken ct)
    {
        try
        {
            Enabled(options); var key = CommandKey(http); var reason = NormalizeReason(input.Reason);
            if (reason is null || reason.Length > 2000 || string.IsNullOrWhiteSpace(input.EvidenceReference)
                || input.EvidenceReference.Trim().Length > 500 || input.ReleasedAmount <= 0
                || decimal.Round(input.ReleasedAmount, 4) != input.ReleasedAmount) throw Invalid("FinanceReleaseInputInvalid");
            var fingerprint = Hash(new { operation = "Release", id, actor = scope.SubjectId,
                input.ExpectedRequestVersion, input.ReservationReference, input.ReleasedAmount,
                input.CurrencyCode, evidenceReference = input.EvidenceReference.Trim(), input.ConfirmedAt, reason });
            var reply = await transaction.ExecuteAsync(async (db, workflow, token) =>
            {
                await RequireCheckerSchema(db, http, token);
                await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, id, token);
                var request = await Request(db, scope, id, token);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                _ = await Actor(core, scope, http, authorization, "procurement-budget-check", token);
                await RecordScope(core, scope, request, token);
                if (scope.SubjectId == request.CreatedBy) throw new ApprovalHttpError(403,
                    "IndependentFinanceVerifierRequired", "Another Finance officer must verify this release.");
                var verifier = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core,
                    scope.TenantId, scope.LegalEntityId, scope.SubjectId, token)
                    ?? throw new ApprovalHttpError(403, "IndependentFinanceVerifierRequired", "An active Finance officer is required.");
                var prior = await Receipt(db, "Release", key, scope.SubjectId, fingerprint, id, null, token);
                if (prior is null && request.Version != input.ExpectedRequestVersion) throw Conflict("ApprovalSourceChanged");
                var caseId = prior?.CoreApprovalCaseId ?? (Guid.TryParse(request.ApprovalInstanceRef, out var currentCase)
                    ? currentCase : throw Conflict("ApprovalSubmissionMissing"));
                var pin = await db.ApprovalSubmissions.SingleAsync(x => x.PurchaseRequestId == id
                    && x.CoreApprovalCaseId == caseId && (prior != null || x.SubmittedBusinessVersion == request.SubmittedVersion), token);
                var approvalCase = await Case(workflow, caseId, token);
                var outcome = await db.ApprovalOutcomes.SingleOrDefaultAsync(x => x.SubmissionId == pin.Id, token)
                    ?? throw Conflict("ApprovalOutcomeMissing");
                var decision = await workflow.Decisions.SingleAsync(x => x.Id == outcome.CoreDecisionId, token);
                if (!PurchaseRequestApprovalPinning.HasRetainedWorkerIdentity(pin)
                    || pin.RequesterWorkerId != request.RequesterWorkerId)
                    throw Conflict("ApprovalHistoricalWorkerEvidenceMissing");
                var terminalIdentity = await db.AssignmentAuthorityEvidence.AsNoTracking().SingleOrDefaultAsync(x =>
                    x.SubmissionId == pin.Id && x.CoreApprovalCaseId == caseId && x.CoreAssignmentId == decision.AssignmentId, token);
                var retainedTerminal = terminalIdentity is null ? null : PurchaseRequestAssignmentIdentity.ReadVerified(terminalIdentity);
                if (terminalIdentity is null || retainedTerminal is null || terminalIdentity.PrincipalId != decision.ActorSubjectId)
                    throw Conflict("ApprovalHistoricalWorkerEvidenceMissing");
                if (decision.ActorSubjectId == scope.SubjectId || verifier.WorkerId == pin.MakerWorkerId
                    || verifier.WorkerId == pin.RequesterWorkerId || verifier.WorkerId == retainedTerminal.AssigneeWorkerId)
                    throw new ApprovalHttpError(403, "IndependentFinanceVerifierRequired", "Another Finance officer must verify this release.");
                if (prior is not null) return new Reply(200, prior.ResponseJson);
                var release = await db.FinanceReleaseRequests.SingleOrDefaultAsync(x => x.ApprovalOutcomeId == outcome.Id, token)
                    ?? throw Conflict("FinanceReleaseRequestMissing");
                var evaluation = await db.PolicyEvaluations.SingleAsync(x => x.Id == pin.PolicyEvaluationId, token);
                var policy = await db.PolicyVersions.SingleAsync(x => x.Id == pin.PolicyVersionId, token);
                var finance = await db.BudgetCheckSnapshots.SingleAsync(x => x.Id == pin.FinanceSnapshotId, token);
                var now = DateTimeOffset.UtcNow;
                var policies = options.ManualFinancePolicies.Where(x => x.Enabled && x.LegalEntityId == scope.LegalEntityId
                    && x.EffectiveFrom != default && x.EffectiveFrom <= now
                    && (x.EffectiveUntil is null || now < x.EffectiveUntil)).ToArray();
                if (policies.Length != 1 || string.IsNullOrWhiteSpace(policies[0].ApprovedPolicyReference)
                    || pin.FinanceEvidenceMode != PurchaseRequestApprovalPinning.ApprovedManualEvidence
                    || pin.ManualEvidenceAuthorityRef != policies[0].ApprovedPolicyReference)
                    throw Conflict("FinanceReleaseManualAuthorityNotConfigured");
                if (input.ReservationReference != release.ReservationRef || input.ReleasedAmount != release.ReservedAmount
                    || input.CurrencyCode != release.CurrencyCode || input.ConfirmedAt < release.RequestedAt
                    || input.ConfirmedAt > now) throw Invalid("FinanceReleaseEvidenceMismatch");
                var attestation = new VerifiedFinanceRelease(scope.TenantId, scope.LegalEntityId,
                    release.ReservationRef, input.ReleasedAmount, input.CurrencyCode, "Released",
                    input.EvidenceReference.Trim(), "ControlledManual:" + policies[0].ApprovedPolicyReference,
                    scope.SubjectId, input.ConfirmedAt);
                var existing = await db.FinanceReleaseConfirmations.SingleOrDefaultAsync(x => x.ReleaseRequestId == release.Id, token);
                var effects = await PurchaseRequestApprovalOutcomes.ResolveReleaseAsync(request, pin, approvalCase,
                    decision, outcome, release, evaluation, policy, finance, scope.TenantId, scope.LegalEntityId,
                    attestation.EvidenceRef, new ControlledManualRelease(attestation), now, existing, token);
                if (!effects.IsReplay)
                {
                    db.AddScoped(effects.Confirmation);
                    foreach (var message in effects.Events) db.AddScoped(message);
                    Audit(db, scope, id, "FinanceReleaseEvidenceRecorded", new
                    {
                        releaseRequestId = release.Id, confirmationId = effects.Confirmation.Id,
                        mode = PurchaseRequestApprovalPinning.ApprovedManualEvidence,
                        policyReference = policies[0].ApprovedPolicyReference, reason,
                    });
                }
                await db.SaveChangesAsync(token);
                var response = JsonSerializer.Serialize(new
                {
                    requestId = id, requestVersion = request.Version, requestState = request.Status,
                    releaseRequestId = release.Id, confirmationId = effects.Confirmation.Id,
                    financeReleaseState = "Confirmed", evidenceMode = PurchaseRequestApprovalPinning.ApprovedManualEvidence,
                }, Json);
                AddReceipt(db, scope, "Release", key, fingerprint, id, caseId, null, response, now);
                return new Reply(200, response);
            }, ct);
            return Results.Content(reply.Json, "application/json", statusCode: reply.Status);
        }
        catch (Exception ex) when (Handled(ex)) { return Failure(ex); }
    }

    // A controlled manual attestation, explicitly distinct from an external Finance API response.
    private sealed class ControlledManualRelease(VerifiedFinanceRelease evidence) : IPurchaseRequestFinanceReleaseAuthority
    {
        public Task<VerifiedFinanceRelease?> ResolveAsync(PurchaseRequestFinanceReleaseRequest request,
            string evidenceReference, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<VerifiedFinanceRelease?>(request.TenantId == evidence.TenantId
                && request.LegalEntityId == evidence.LegalEntityId && request.ReservationRef == evidence.ReservationRef
                && evidence.EvidenceRef == evidenceReference ? evidence : null);
        }
    }
    public sealed record FinanceReleaseInput(long ExpectedRequestVersion, string ReservationReference,
        decimal ReleasedAmount, string CurrencyCode, string EvidenceReference, DateTimeOffset ConfirmedAt, string? Reason);
}
