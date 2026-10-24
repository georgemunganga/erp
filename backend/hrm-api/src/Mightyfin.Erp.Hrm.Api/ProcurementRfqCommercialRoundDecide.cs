using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private sealed record CommercialDecisionInput(long ExpectedEventVersion,
        long ExpectedCaseVersion, long ExpectedAssignmentVersion,
        string Action, string? Reason);

    private static Task<IResult> CommercialDecide(Guid id, Guid assignmentId,
        CommercialDecisionInput? input, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequireCommercial(http, db, true, token);
            if (id == Guid.Empty || assignmentId == Guid.Empty || input is null
                || input.ExpectedEventVersion < 1 || input.ExpectedCaseVersion < 1
                || input.ExpectedAssignmentVersion < 1
                || input.Action is not ("Approve" or "Return" or "Reject"))
                throw new Problem("RfqCommercialDecisionInputRequired", 422);
            var reason = Normalize(input.Reason);
            if (reason.Length > 2000 || input.Action != "Approve" && reason.Length == 0)
                throw new Problem("RfqCommercialDecisionReasonRequired", 422);
            var key = Key(http);
            var fingerprint = Fingerprint(new { operation = "Decide", roundId = id,
                assignmentId, input.ExpectedEventVersion, input.ExpectedCaseVersion,
                input.ExpectedAssignmentVersion, input.Action, reason });
            var actor = await PrincipalActor(core, scope, http, auth,
                PrincipalRfqCommercialRoundPolicyRules.ApprovalAction, null, false, token);
            var branches = actor.Branches.ToArray();
            var opening = await db.PrincipalRfqCommercialRoundOpenings.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == id && x.TenantId == scope.TenantId
                    && x.LegalEntityId == scope.LegalEntityId
                    && (actor.SelectedBranchId == null || x.BranchId == actor.SelectedBranchId)
                    && (actor.OrganisationWide || branches.Contains(x.BranchId)), token)
                ?? throw new Problem("RfqCommercialRoundNotFound", 404);
            actor = await PrincipalActor(core, scope, http, auth,
                PrincipalRfqCommercialRoundPolicyRules.ApprovalAction,
                opening.BranchId, true, token);
            await LockPrincipalSourcingCommandKey(db, key, token);
            var store = new PrincipalRfqCommercialRoundStore(db, scope);
            if (await store.RecoverAsync(key, "Decide", opening.SourcingEventId,
                    fingerprint, token) is { } replay)
                return new Reply(replay.ResponseJson, replay.ResponseStatus);
            var requestIds = await db.SourcingDemandBindings.AsNoTracking()
                .Where(x => x.SourcingEventId == opening.SourcingEventId)
                .Select(x => x.PurchaseRequestId).Distinct().OrderBy(x => x)
                .ToArrayAsync(token);
            foreach (var requestId in requestIds)
                await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope,
                    requestId, token);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT id FROM procurement.principal_rfq_commercial_round_openings
                WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId}
                    AND id={id} FOR UPDATE
                """, token);
            await cases.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT "Id" FROM core_approval.cases
                WHERE "TenantId"={scope.TenantId} AND "LegalEntityId"={scope.LegalEntityId}
                    AND "Id"={opening.CoreCaseId} FOR UPDATE
                """, token);
            var workflow = await cases.Cases.Include(x => x.Stages)
                .ThenInclude(x => x.Assignments)
                .SingleOrDefaultAsync(x => x.Id == opening.CoreCaseId
                    && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId,
                    token)
                ?? throw new Problem("RfqCommercialReviewNotFound", 404);
            var assignment = workflow.Stages.SelectMany(x => x.Assignments)
                .SingleOrDefault(x => x.Id == assignmentId
                    && x.AssigneeSubjectId == actor.PrincipalId
                    && x.PermissionKey == PrincipalRfqCommercialRoundPolicyRules.ApprovalAction
                    && x.DelegatedFromSubjectId is null
                    && x.State == "Pending")
                ?? throw new Problem("RfqCommercialReviewNotFound", 404);
            if (workflow.State != "Pending" || workflow.Version != input.ExpectedCaseVersion
                || assignment.Version != input.ExpectedAssignmentVersion
                || workflow.SourceModule != "procurement"
                || workflow.SourceType != PrincipalRfqCommercialRoundCoreWorkflow.SourceType
                || workflow.SourceId != opening.PreparedPlanId
                || workflow.MakerSubjectId != opening.ActorPrincipalId
                || workflow.PolicyVersionId != opening.CommercialPolicyVersionId
                || workflow.Id != opening.Id
                || opening.EventVersion != input.ExpectedEventVersion)
                throw new Problem("RfqCommercialReviewChanged", 409);
            var source = await db.SourcingEvents.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == opening.SourcingEventId && x.TenantId == scope.TenantId
                && x.LegalEntityId == scope.LegalEntityId && x.BranchRef == opening.BranchId,
                token) ?? throw new Problem("RfqCommercialRoundSourceChanged", 409);
            if (source.Status != "Published" || source.ExecutionVersion != 2
                || source.Version != opening.EventVersion)
                throw new Problem("RfqCommercialRoundSourceChanged", 409);
            _ = await PrincipalPublicationEvidence(db, cases, source, token);
            var prepared = await db.PrincipalRfqPreparedPlans.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == opening.PreparedPlanId
                    && x.SourcingEventId == source.Id && x.BranchId == opening.BranchId,
                    token) ?? throw new Problem("RfqCommercialPreparedPlanChanged", 409);
            var head = await db.PrincipalRfqCommercialDrafts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == prepared.DraftId
                    && x.SourcingEventId == source.Id, token)
                ?? throw new Problem("RfqCommercialPreparedPlanChanged", 409);
            var revision = await db.PrincipalRfqCommercialDraftRevisions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == prepared.DraftRevisionId
                    && x.DraftId == head.Id, token)
                ?? throw new Problem("RfqCommercialPreparedPlanChanged", 409);
            PrincipalRfqPreparedPlanEvidence.Verify(prepared, head, revision);
            if (prepared.SnapshotSha256 != opening.PreparedPlanSha256
                || prepared.DraftRevision != workflow.SourceVersion
                || opening.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(opening.SnapshotJson))
                throw new Problem("RfqCommercialPreparedPlanChanged", 409);
            await using (var documents = new DocumentDbContext(
                new DbContextOptionsBuilder<DocumentDbContext>()
                    .UseNpgsql(db.Database.GetDbConnection()).Options, scope))
            {
                await documents.Database.UseTransactionAsync(
                    db.Database.CurrentTransaction!.GetDbTransaction(), token);
                await RequireRetainedCommercialRequirementsAsync(db, documents, scope,
                    prepared, head, revision, token, currentPurposeAuthorized: true);
            }
            var policy = await db.PolicyVersions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == opening.CommercialPolicyVersionId
                    && x.PolicyKey == PrincipalRfqCommercialRoundPolicyRules.PolicyKey,
                    token) ?? throw new Problem("RfqCommercialPolicyChanged", 409);
            var identity = await ProcurementOperationalPolicyWorkerIdentity
                .VerifyPublicationAsync(db, scope, policy, token,
                    OperationalPolicyPublicationMode.RetainedHistorical);
            if (identity.Published.Id != opening.PublishedPolicyIdentityId
                || ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
                    != opening.PolicyRulesSha256)
                throw new Problem("RfqCommercialPolicyChanged", 409);
            var original = await RequireCommercialReviewerIndependence(db, core, scope, source,
                actor, token);
            await RequireCommercialSourceHolds(db, cases, scope, source,
                original, actor, token);
            var decisions = await cases.Decisions.Where(x => x.CaseId == workflow.Id)
                .OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).ToListAsync(token);
            await RequireCommercialPriorVotes(db, opening, decisions, token);
            var before = decisions.Count;
            var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            var result = ApprovalEngine.Decide(workflow, assignment,
                new ApprovalActor(actor.PrincipalId, scope.TenantId, scope.LegalEntityId,
                    new HashSet<string> { PrincipalRfqCommercialRoundPolicyRules.ApprovalAction }),
                input.Action, key.ToString("D"), reason.Length == 0 ? null : reason,
                decisions, now);
            if (!result.Recorded || decisions.Count != before + 1)
                throw new Problem("RfqCommercialCoreDecisionNotRecorded", 409);
            var currentDecision = decisions[^1];
            cases.Decisions.Add(currentDecision);
            PrincipalRfqCommercialRoundTerminal? terminal = null;
            if (workflow.State is "Approved" or "Returned" or "Rejected")
            {
                using var json = JsonDocument.Parse(opening.SnapshotJson);
                var pinnedSuppliers = json.RootElement.GetProperty("supplierIds")
                    .EnumerateArray().Select(x => x.GetGuid()).ToArray();
                var retained = new PrincipalRfqCommercialRoundOpening(workflow,
                    opening.SnapshotJson, opening.SnapshotSha256, pinnedSuppliers,
                    opening.OpensAt, opening.ClosesAt,
                    opening.ClosingOfficerPrincipalId,
                    opening.ClosingOfficerWorkerAliasId);
                terminal = PrincipalRfqCommercialRoundCoreWorkflow.Finish(retained,
                    prepared, source, policy, workflow, decisions,
                    currentDecision, actor, now);
            }
            await cases.SaveChangesAsync(token);
            var receipt = store.StageVote(opening, workflow, currentDecision,
                assignment, terminal, actor, key, fingerprint);
            await db.SaveChangesAsync(token);
            return new Reply(receipt.ResponseJson, receipt.ResponseStatus);
        }, ct);

    private static async Task<RfqPrincipalDraft> RequireCommercialReviewerIndependence(
        ProcurementDbContext db, Mightyfin.Erp.Hrm.Infrastructure.Data.HrmDbContext core,
        ProcurementRequestScope scope, SourcingEvent source, RfqPrincipalActor reviewer,
        CancellationToken ct)
    {
        var evidence = await db.SourcingDraftEvidenceRecords.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == source.SourceEvidenceId, ct)
            ?? throw new Problem("RfqCommercialSourceEvidenceChanged", 409);
        var bindings = await db.SourcingDemandBindings.AsNoTracking()
            .Where(x => x.SourcingEventId == source.Id).ToArrayAsync(ct);
        var original = GovernedPrincipalRfqRestoration.RestoreVerified(evidence,
            source, bindings);
        await RequireCommercialCurrentIndependence(core, scope, original,
            [reviewer], ct);
        return original;
    }

    private static async Task RequireCommercialSourceHolds(ProcurementDbContext db,
        Mightyfin.Erp.Core.Approvals.Infrastructure.ApprovalDbContext cases,
        ProcurementRequestScope scope, SourcingEvent source,
        RfqPrincipalDraft original, RfqPrincipalActor actor, CancellationToken ct)
    {
        if (source.BranchRef is not { } branch)
            throw new Problem("RfqCommercialSourceHoldChanged", 409);
        var targetScope = PrincipalRfqTargetBranchScope.Bind(scope, actor, branch);
        foreach (var pin in original.Lines.OrderBy(x => x.RequestId)
            .ThenBy(x => x.LineId))
        {
            var current = await ApprovedDemandSourceLoader.LoadRetainedAsync(db, cases,
                targetScope, pin.RequestId, pin.LineId,
                new(pin.SubmissionId, pin.OutcomeId, pin.MethodId), ct);
            _ = PurchaseDemandAllocationBalance.Evaluate(current.Request,
                current.Line, current.Allocations, current.AllocationOwners ?? [],
                scope.TenantId, scope.LegalEntityId);
            var hold = current.Allocations.SingleOrDefault(x => x.Id == pin.AllocationId
                && x.TargetType == "SourcingEvent" && x.TargetId == source.Id);
            if (hold is null || hold.Status != "Reserved"
                || hold.Quantity != pin.Quantity || hold.EstimatedValue != pin.EstimatedValue
                || (current.AllocationOwners ?? []).Count(x =>
                    x.TargetType == "SourcingEvent" && x.TargetId == source.Id
                    && x.TargetLineId == pin.EventLineId
                    && x.PurchaseRequestLineId == pin.LineId) != 1)
                throw new Problem("RfqCommercialSourceHoldChanged", 409);
        }
    }

    private static async Task RequireCommercialPriorVotes(ProcurementDbContext db,
        PrincipalRfqCommercialRoundOpeningRecord opening,
        IReadOnlyList<ApprovalDecision> decisions, CancellationToken ct)
    {
        var votes = await db.PrincipalRfqCommercialRoundVotes.AsNoTracking()
            .Where(x => x.OpeningId == opening.Id).ToArrayAsync(ct);
        var receipts = await db.PrincipalRfqCommercialCommandReceipts.AsNoTracking()
            .Where(x => x.OpeningId == opening.Id && x.Operation == "Decide")
            .ToArrayAsync(ct);
        if (votes.Length != decisions.Count || receipts.Length != decisions.Count
            || votes.Select(x => x.CoreDecisionId).Distinct().Count() != votes.Length
            || receipts.Select(x => x.VoteTargetId).Distinct().Count() != receipts.Length)
            throw new Problem("RfqCommercialVoteHistoryChanged", 409);
        foreach (var decision in decisions)
        {
            var vote = votes.SingleOrDefault(x => x.CoreDecisionId == decision.Id);
            var receipt = vote is null ? null : receipts.SingleOrDefault(x => x.VoteTargetId == vote.Id);
            if (vote is null || receipt is null
                || vote.CoreCaseId != opening.CoreCaseId
                || vote.CoreAssignmentId != decision.AssignmentId
                || vote.ActorPrincipalId != decision.ActorSubjectId
                || vote.Action != decision.Action
                || vote.DecidedAt != ProcurementEvidenceEncoding.DatabaseTimestamp(decision.DecidedAt)
                || receipt.ActorPrincipalId != decision.ActorSubjectId
                || receipt.CommandKey.ToString("D") != decision.IdempotencyKey
                || receipt.ResponseSha256 != ProcurementEvidenceEncoding.Digest(receipt.ResponseJson))
                throw new Problem("RfqCommercialVoteHistoryChanged", 409);
        }
    }
}
