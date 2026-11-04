using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private sealed record RoundSubmitInput(long ExpectedEventVersion, long ExpectedPlanVersion,
        Guid SubmittedRevisionId, string ContentSha256, Guid PolicyVersionId, long PolicyVersionNumber);
    private sealed record RoundDecisionInput(Guid SubmissionId, Guid AssignmentId, long ExpectedEventVersion,
        long ExpectedPlanVersion, long ExpectedCaseVersion, long ExpectedAssignmentVersion,
        string Action, string? Reason);

    private static Task<IResult> RoundSubmit(Guid id, JsonElement body, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
        => RunRound(http, scope, async (db, cases, core, auth, token) =>
        {
            var input = RoundInput<RoundSubmitInput>(body);
            if (id == Guid.Empty || input.ExpectedEventVersion < 1 || input.ExpectedPlanVersion < 1
                || input.ExpectedPlanVersion == long.MaxValue || input.SubmittedRevisionId == Guid.Empty
                || input.PolicyVersionId == Guid.Empty || input.PolicyVersionNumber < 1
                || input.ContentSha256 is not { Length: 64 }
                || input.ContentSha256.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
                throw new Problem("RoundReviewInputInvalid", 422);
            var key = RoundReviewKey(http); var hash = RoundHash("Submit", id, input);
            await LockPrincipalSourcingCommandKey(db, key, token);
            var source = await RoundSource(db, core, scope, http, auth,
                CommercialRoundReviewCoreWorkflow.SubmitPermission, id, token);
            RoundWriteContext(http, scope, source);
            if (await RoundReplay(db, scope, id, key, "Submit", hash, token) is { } replay) return replay;
            await RequireRoundReviewRecord(http, db, token);
            await RoundLockSource(db, scope, id, token); await db.Entry(source).ReloadAsync(token);
            if (source.Status != "Draft" || source.Version != input.ExpectedEventVersion)
                throw new Problem("RoundPlanSourceChanged", 409);
            await RequireRoundUntouched(db, id, token);
            _ = await RoundVerifyHeldSource(db, cases, scope, source, token);
            var graph = await RoundGraph(db, source, null, tracked: true, token);
            if (graph.Plan.Version != input.ExpectedPlanVersion || graph.Plan.Status != "Draft"
                || graph.Revision.Id != input.SubmittedRevisionId
                || graph.Revision.ContentSha256 != input.ContentSha256)
                throw new Problem("RoundPlanVersionChanged", 409);
            if (await db.CommercialRoundReviewSubmissions.AnyAsync(x => x.PlanId == graph.Plan.Id
                && !db.CommercialRoundReviewOutcomes.Any(y => y.SubmissionId == x.Id
                    && y.ResultStatus != "InReview"), token))
                throw new Problem("RoundPlanReviewAlreadyOpen", 409);
            var sourceCategories = await db.SourcingLines.AsNoTracking()
                .Where(x => x.SourcingEventId == id && x.Status == "Active")
                .Select(x => x.CategoryCode).ToArrayAsync(token);
            if (source.MethodId is null || sourceCategories.Length == 0
                || sourceCategories.Any(string.IsNullOrWhiteSpace))
                throw new Problem("RoundReviewSourceClassificationUnavailable", 409);
            var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            var policies = await db.PolicyVersions.AsNoTracking().Where(x =>
                    x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.PolicyKey == CommercialRoundReviewPolicyRules.PolicyKey && x.State == "Published"
                    && x.EffectiveFrom <= now && (x.EffectiveTo == null || x.EffectiveTo > now))
                .ToArrayAsync(token);
            var applicable = policies.Select(policy => (policy, rules: CommercialRoundReviewPolicyRules.Read(policy.RulesJson)))
                .Where(x => x.rules is { } rules && rules.MethodIds.Contains(source.MethodId.Value)
                    && sourceCategories.All(category => rules.CategoryCodes.Contains("*")
                        || rules.CategoryCodes.Contains(category!))).ToArray();
            if (applicable.Length != 1 || applicable[0].policy.Id != input.PolicyVersionId
                || applicable[0].policy.PolicyVersionNumber != input.PolicyVersionNumber)
                throw new Problem("RoundPlanReviewPolicyUnavailable", 409);
            var (policy, rules) = applicable[0];
            if (rules is null) throw new Problem("RoundPlanReviewPolicyUnavailable", 409);
            _ = await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db, scope, policy, token);
            var draft = CommercialRoundPlanDraft.Restore(graph.Revision, graph.Criteria, graph.Participants);
            if (rules.DocumentsRequired)
                throw new Problem("RoundReviewDocumentVerificationUnavailable", 503);
            if (!RoundReviewComplete(draft, rules)) throw new Problem("RoundPlanIncomplete", 422);
            await RoundParticipants(core, scope, graph.Plan.BranchId, draft, token);
            var maker = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, token);
            if (maker is null || !maker.Permissions.Contains(CommercialRoundReviewCoreWorkflow.SubmitPermission))
                throw new UnauthorizedAccessException("Round submit authority changed.");
            var assignees = new List<ProcurementPrincipalPeople.Person>();
            foreach (var principalId in rules.AssigneePrincipalIds)
            {
                var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                    scope.LegalEntityId, principalId.ToString("D"), token);
                if (person is null || !person.Permissions.Contains(CommercialRoundReviewCoreWorkflow.ReviewPermission)
                    || !person.OrganisationWide && !person.AllowedBranches.Contains(graph.Plan.BranchId)
                    || !await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                        maker.PrincipalId, maker.CoreLinkedWorkerId, person.PrincipalId, person.CoreLinkedWorkerId, token))
                    throw new Problem("RoundReviewerUnavailable", 409);
                if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                    source.OwnerSubjectId, source.MakerWorkerId, person.PrincipalId, person.CoreLinkedWorkerId, token))
                    throw new Problem("RoundReviewerUnavailable", 409);
                foreach (var earlier in assignees)
                    if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                        earlier.PrincipalId, earlier.CoreLinkedWorkerId,
                        person.PrincipalId, person.CoreLinkedWorkerId, token))
                        throw new Problem("RoundReviewerConflict", 409);
                assignees.Add(person);
            }
            var submissionId = Guid.CreateVersion7(); var caseId = Guid.CreateVersion7();
            var change = CommercialRoundPlanDraft.Transition(graph.Plan, graph.Revision, graph.Criteria,
                graph.Participants, new(scope.SubjectId, maker.CoreLinkedWorkerId), "Submit", "InReview", null,
                key, hash, now, new(submissionId, caseId, null, null));
            var submission = new CommercialRoundReviewSubmission
            {
                Id = submissionId,
                TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                PlanId = graph.Plan.Id, SourcingEventId = id, BranchId = graph.Plan.BranchId,
                SubmittedRevisionId = change.Revision.Id, SubmittedPlanVersion = change.Revision.ResultVersion,
                SubmittedContentSha256 = change.Revision.ContentSha256,
                PolicyVersionId = policy.Id, PolicyVersionNumber = policy.PolicyVersionNumber,
                PolicyRulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
                CoreCaseId = caseId, MakerPrincipalId = maker.PrincipalId,
                MakerWorkerAlias = maker.CoreLinkedWorkerId, SubmittedAt = now,
            };
            db.StageCommercialRoundPlan(change);
            var approvalCase = CommercialRoundReviewCoreWorkflow.Open(graph.Plan, change.Revision,
                submission, policy, rules, RoundPrincipal(maker, scope),
                assignees.Select(x => RoundPrincipal(x, scope)).ToArray(), now);
            db.AddScoped(submission);
            cases.AddCase(approvalCase); RoundRecord(db, scope, change);
            await db.SaveChangesAsync(token);
            return new(change.Receipt.ResponseJson, change.Receipt.ResponseStatusCode);
        }, ct, CommercialRoundReviewCoreWorkflow.SubmitPermission);

    private static Task<IResult> RoundDecide(Guid id, JsonElement body, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
        => RunRound(http, scope, async (db, cases, core, auth, token) =>
        {
            var input = RoundInput<RoundDecisionInput>(body);
            var reason = input.Reason?.Trim();
            if (id == Guid.Empty || input.SubmissionId == Guid.Empty || input.AssignmentId == Guid.Empty
                || input.ExpectedEventVersion < 1 || input.ExpectedPlanVersion < 1
                || input.ExpectedPlanVersion == long.MaxValue || input.ExpectedCaseVersion < 1
                || input.ExpectedAssignmentVersion < 1 || input.Action is not ("Approve" or "Return" or "Reject")
                || input.Action != "Approve" && string.IsNullOrWhiteSpace(reason)
                || reason?.Length > 2000 || input.Action == "Approve" && reason is not null)
                throw new Problem("RoundReviewInputInvalid", 422);
            var key = RoundReviewKey(http); var hash = RoundHash("Decide", id,
                input with { Reason = reason });
            await LockPrincipalSourcingCommandKey(db, key, token);
            var source = await RoundSource(db, core, scope, http, auth,
                CommercialRoundReviewCoreWorkflow.ReviewPermission, id, token);
            RoundWriteContext(http, scope, source);
            if (await RoundReplay(db, scope, id, key, input.Action, hash, token) is { } replay) return replay;
            await RequireRoundReviewRecord(http, db, token);
            await RoundLockSource(db, scope, id, token); await db.Entry(source).ReloadAsync(token);
            if (source.Status != "Draft" || source.Version != input.ExpectedEventVersion)
                throw new Problem("RoundPlanSourceChanged", 409);
            await RequireRoundUntouched(db, id, token);
            _ = await RoundVerifyHeldSource(db, cases, scope, source, token);
            var graph = await RoundGraph(db, source, null, tracked: true, token);
            if (graph.Plan.Status != "InReview" || graph.Plan.Version != input.ExpectedPlanVersion)
                throw new Problem("RoundPlanVersionChanged", 409);
            var submission = await db.CommercialRoundReviewSubmissions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == input.SubmissionId && x.PlanId == graph.Plan.Id && x.BranchId == graph.Plan.BranchId, token)
                ?? throw new Problem("RoundReviewNotFound", 404);
            var frozen = await RoundGraph(db, source, submission.SubmittedPlanVersion, tracked: false, token);
            if (frozen.Revision.Id != submission.SubmittedRevisionId
                || frozen.Revision.Operation != "Submit"
                || frozen.Revision.ContentSha256 != submission.SubmittedContentSha256
                || graph.Revision.ContentSha256 != submission.SubmittedContentSha256)
                throw new Problem("RoundReviewSnapshotInvalid", 409);
            var approvalCase = await cases.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
                .SingleOrDefaultAsync(x => x.Id == submission.CoreCaseId
                    && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.SourceModule == "procurement" && x.SourceType == CommercialRoundReviewCoreWorkflow.SourceType
                    && x.SourceId == graph.Plan.Id, token)
                ?? throw new Problem("RoundReviewCaseMissing", 409);
            var assignment = approvalCase.Stages.SelectMany(x => x.Assignments)
                .SingleOrDefault(x => x.Id == input.AssignmentId && x.AssigneeSubjectId == scope.SubjectId)
                ?? throw new Problem("RoundReviewTaskNotFound", 404);
            var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                scope.LegalEntityId, scope.SubjectId, token)
                ?? throw new UnauthorizedAccessException("Current reviewer unavailable.");
            if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                    submission.MakerPrincipalId, submission.MakerWorkerAlias,
                    actor.PrincipalId, actor.CoreLinkedWorkerId, token)
                || !await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                    source.OwnerSubjectId, source.MakerWorkerId,
                    actor.PrincipalId, actor.CoreLinkedWorkerId, token))
                throw new UnauthorizedAccessException("Independent reviewer required.");
            if (approvalCase.Version != input.ExpectedCaseVersion
                || assignment.Version != input.ExpectedAssignmentVersion)
                throw new Problem("RoundReviewVersionChanged", 409);
            var policy = await db.PolicyVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == submission.PolicyVersionId, token);
            if (policy is null || policy.PolicyKey != CommercialRoundReviewPolicyRules.PolicyKey
                || policy.PolicyVersionNumber != submission.PolicyVersionNumber
                || ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson) != submission.PolicyRulesSha256)
                throw new Problem("RoundReviewPolicyChanged", 409);
            _ = await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db, scope, policy, token,
                OperationalPolicyPublicationMode.RetainedHistorical);
            var decisions = await cases.Decisions.Where(x => x.CaseId == approvalCase.Id).ToListAsync(token);
            var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            var (decision, status) = CommercialRoundReviewCoreWorkflow.Decide(graph.Plan, submission,
                approvalCase, assignment, RoundPrincipal(actor, scope), input.Action, key.ToString("D"), reason,
                decisions, now);
            cases.Decisions.Add(decision);
            var change = CommercialRoundPlanDraft.Transition(graph.Plan, graph.Revision, graph.Criteria,
                graph.Participants, new(scope.SubjectId, actor.CoreLinkedWorkerId), input.Action, status,
                decision.Reason, key, hash, now,
                new(submission.Id, approvalCase.Id, assignment.Id, decision.Id));
            var outcome = new CommercialRoundReviewOutcome
            {
                TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                SourcingEventId = id, BranchId = graph.Plan.BranchId, PlanId = graph.Plan.Id,
                SubmissionId = submission.Id, CoreCaseId = approvalCase.Id,
                CoreStageId = decision.StageId, CoreAssignmentId = decision.AssignmentId,
                CoreDecisionId = decision.Id, ResultRevisionId = change.Revision.Id,
                ResultPlanVersion = change.Revision.ResultVersion, ResultStatus = status,
                Action = input.Action, ActorPrincipalId = actor.PrincipalId,
                ActorWorkerAlias = actor.CoreLinkedWorkerId, Reason = decision.Reason, RecordedAt = now,
            };
            db.StageCommercialRoundPlan(change); db.AddScoped(outcome);
            RoundRecord(db, scope, change);
            await db.SaveChangesAsync(token);
            return new(change.Receipt.ResponseJson, change.Receipt.ResponseStatusCode);
        }, ct, CommercialRoundReviewCoreWorkflow.ReviewPermission);

    private static PlanReviewPrincipal RoundPrincipal(ProcurementPrincipalPeople.Person person,
        ProcurementRequestScope scope) => new(Guid.ParseExact(person.PrincipalId, "D"),
        person.CoreLinkedWorkerId, scope.TenantId, scope.LegalEntityId,
        person.OrganisationWide, person.AllowedBranches, person.Permissions);

    private static bool RoundReviewComplete(RoundPlanDraftInput draft,
        CommercialRoundReviewPolicyRules.Definition rules) =>
        !string.IsNullOrWhiteSpace(draft.Requirements)
        && !string.IsNullOrWhiteSpace(draft.ProposedPolicyReference)
        && draft.OpensAt is not null && draft.ClosesAt is not null
        && draft.Criteria.Count >= rules.MinimumCriteria && draft.Participants.Count > 0
        && rules.RequiredCriterionStages.All(stage => draft.Criteria.Any(x => x.Stage == stage));

    private static Guid RoundReviewKey(HttpContext http)
    {
        if (http.Request.Headers["X-Idempotency-Key"].Count != 0
            || http.Request.Headers["Idempotency-Key"].Count != 1
            || !Guid.TryParseExact(http.Request.Headers["Idempotency-Key"].ToString(), "D", out var key)
            || key == Guid.Empty) throw new Problem("RoundReviewCommandKeyRequired", 422);
        return key;
    }

    private static async Task RequireRoundReviewRecord(HttpContext http, ProcurementDbContext db, CancellationToken ct)
    {
        var options = http.RequestServices.GetRequiredService<ProcurementCommercialRoundOptions>();
        var readiness = http.RequestServices.GetRequiredService<IProcurementCommercialRoundReviewReadiness>();
        if (!options.ReviewEnabled || !options.RecordEnabled || !readiness.HasCapturedContract
            || !await readiness.ReadyAsync(db, true, ct))
            throw new Problem("RoundPlanReviewUnavailable", 503);
    }
}
