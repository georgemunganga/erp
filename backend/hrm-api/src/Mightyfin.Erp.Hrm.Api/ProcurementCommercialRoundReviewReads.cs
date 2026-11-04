using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private static Task<IResult> RoundSubmitContext(Guid id, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
        => RunRound(http, scope, async (db, cases, core, auth, token) =>
        {
            if (scope.IsExplicitAllBranches || scope.WorkLocationId is null
                || http.Request.Headers["X-Shell-Branch-Scope"].Any(x =>
                    string.Equals(x, "all", StringComparison.OrdinalIgnoreCase)))
                throw new UnauthorizedAccessException("Select the round's current branch.");
            var source = await RoundSource(db, core, scope, http, auth,
                CommercialRoundReviewCoreWorkflow.SubmitPermission, id, token);
            if (scope.WorkLocationId != source.BranchRef)
                throw new UnauthorizedAccessException("Select the round's current branch.");
            var graph = await RoundGraph(db, source, null, tracked: false, token);
            var hints = await RoundActions(http, db, cases, core, scope, auth,
                source, graph.Plan, true, token);
            return new(JsonSerializer.Serialize(new { eventId = id, eventVersion = source.Version,
                planId = graph.Plan.Id, planVersion = graph.Plan.Version, status = graph.Plan.Status,
                revisionId = graph.Revision.Id, contentSha256 = graph.Revision.ContentSha256,
                allowedActions = hints.Actions, applicableReviewPolicy = hints.Policy }, RoundJson));
        }, ct, CommercialRoundReviewCoreWorkflow.SubmitPermission);

    private static Task<IResult> RoundReviewTasks(Guid? after, int? limit, HttpContext http, ProcurementRequestScope scope,
        CancellationToken ct) => RunRound(http, scope, async (db, cases, core, auth, token) =>
    {
        await RequireRoundReviewRead(http, db, token);
        var size=limit??25;
        if (size is < 1 or > 50 || after==Guid.Empty) throw new Problem("RoundReviewTaskPageInvalid", 422);
        var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, scope.SubjectId, token)
            ?? throw new UnauthorizedAccessException("Current reviewer unavailable.");
        if (!actor.Permissions.Contains(CommercialRoundReviewCoreWorkflow.ReviewPermission))
            throw new UnauthorizedAccessException("Current reviewer action unavailable.");
        var query = cases.Assignments.AsNoTracking()
            .Where(x => x.AssigneeSubjectId == scope.SubjectId
                && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                && x.PermissionKey == CommercialRoundReviewCoreWorkflow.ReviewPermission
                && x.State == "Pending");
        if (after is { } cursor)
            query=query.Where(x=>EF.Functions.GreaterThan(ValueTuple.Create(x.Id),ValueTuple.Create(cursor)));
        var pending = await query.OrderBy(x => x.Id).Take(201).ToArrayAsync(token);
        var caseIds = pending.Select(x => x.CaseId).Distinct().ToArray();
        var activeCases = await cases.Cases.AsNoTracking()
            .Where(x => caseIds.Contains(x.Id) && x.State == "Pending")
            .ToArrayAsync(token);
        var activeStages = await cases.Stages.AsNoTracking()
            .Where(x => caseIds.Contains(x.CaseId) && x.State == "Active" && x.Code == "RoundPlanReview")
            .ToArrayAsync(token);
        var rounds = await db.CommercialRoundReviewSubmissions.AsNoTracking()
            .Where(x => caseIds.Contains(x.CoreCaseId)).ToArrayAsync(token);
        var planIds = rounds.Select(x => x.PlanId).Distinct().ToArray();
        var newest = await db.CommercialRoundReviewSubmissions.AsNoTracking()
            .Where(x => planIds.Contains(x.PlanId))
            .GroupBy(x => x.PlanId)
            .Select(group => new { PlanId = group.Key, Version = group.Max(x => x.SubmittedPlanVersion) })
            .ToArrayAsync(token);
        var items = new List<object>();
        Guid? scanned=null;
        foreach (var task in pending.Take(200))
        {
            scanned=task.Id;
            var round = rounds.SingleOrDefault(x => x.CoreCaseId == task.CaseId);
            if (round is null || round.TenantId != scope.TenantId || round.LegalEntityId != scope.LegalEntityId
                || !actor.OrganisationWide && !actor.AllowedBranches.Contains(round.BranchId)
                || !scope.IsExplicitAllBranches && scope.WorkLocationId != round.BranchId) continue;
            var plan = await db.CommercialRoundPlans.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == round.PlanId && x.SourcingEventId == round.SourcingEventId
                && x.BranchId == round.BranchId, token);
            var approvalCase = activeCases.SingleOrDefault(x => x.Id == task.CaseId);
            var stage = activeStages.SingleOrDefault(x => x.Id == task.StageId);
            var latest = newest.SingleOrDefault(x => x.PlanId == round.PlanId);
            if (plan is null || approvalCase is null || stage is null || latest is null
                || !CommercialRoundReviewCoreWorkflow.PendingTask(approvalCase, stage,
                    task, round, plan, latest.Version)) continue;
            var eventVersion = await QuotationOwnedSources(db).AsNoTracking()
                .Where(x => x.Id == round.SourcingEventId && x.BranchRef == round.BranchId)
                .Select(x => x.Version).SingleOrDefaultAsync(token);
            if (eventVersion < 1) continue;
            items.Add(new { eventId = round.SourcingEventId, planId = plan.Id,
                submissionId = round.Id, caseId = round.CoreCaseId,
                assignmentId = task.Id, submittedVersion = round.SubmittedPlanVersion,
                currentPlanVersion = plan.Version, eventVersion,
                contentSha256 = round.SubmittedContentSha256,
                assignmentVersion = task.Version });
            if(items.Count==size) break;
        }
        var nextCursor=scanned is { } last && pending.Any(x=>x.Id.CompareTo(last)>0) ? scanned : null;
        return new(JsonSerializer.Serialize(new { items, nextCursor }, RoundJson));
    }, ct, CommercialRoundReviewCoreWorkflow.ReviewPermission);

    private static Task<IResult> RoundReviewDetail(Guid id, Guid submissionId,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunRound(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequireRoundReviewRead(http, db, token);
            var source = await RoundSource(db, core, scope, http, auth,
                CommercialRoundReviewCoreWorkflow.ReviewPermission, id, token);
            var submission = await db.CommercialRoundReviewSubmissions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == submissionId && x.SourcingEventId == id
                    && x.BranchId == source.BranchRef, token)
                ?? throw new Problem("RoundReviewNotFound", 404);
            var approvalCase = await cases.Cases.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == submission.CoreCaseId
                    && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.SourceModule == "procurement"
                    && x.SourceType == CommercialRoundReviewCoreWorkflow.SourceType
                    && x.SourceId == submission.PlanId
                    && x.SourceVersion == submission.SubmittedPlanVersion, token)
                ?? throw new Problem("RoundReviewCaseMissing", 409);
            var assignment = await cases.Assignments.AsNoTracking().SingleOrDefaultAsync(x =>
                x.CaseId == approvalCase.Id && x.AssigneeSubjectId == scope.SubjectId
                && x.PermissionKey == CommercialRoundReviewCoreWorkflow.ReviewPermission, token)
                ?? throw new Problem("RoundReviewTaskNotFound", 404);
            var submitted = await RoundGraph(db, source, submission.SubmittedPlanVersion, tracked: false, token);
            if (submitted.Plan.Id != submission.PlanId
                || submitted.Revision.Id != submission.SubmittedRevisionId
                || submitted.Revision.Operation != "Submit"
                || submitted.Revision.ContentSha256 != submission.SubmittedContentSha256)
                throw new Problem("RoundReviewSnapshotInvalid", 409);
            var current = await db.CommercialRoundPlans.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == submission.PlanId && x.SourcingEventId == id && x.BranchId == submission.BranchId, token)
                ?? throw new Problem("RoundReviewNotFound", 404);
            var stage = await cases.Stages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assignment.StageId
                && x.CaseId == approvalCase.Id, token);
            var latestVersion = await db.CommercialRoundReviewSubmissions.AsNoTracking()
                .Where(x => x.PlanId == current.Id).MaxAsync(x => x.SubmittedPlanVersion, token);
            var currentRound = stage is not null
                && CommercialRoundReviewCoreWorkflow.PendingTask(approvalCase, stage, assignment,
                    submission, current, latestVersion);
            var allowedActions = Array.Empty<string>();
            if (currentRound && source.Status == "Draft" && !scope.IsExplicitAllBranches
                && scope.WorkLocationId == current.BranchId
                && !http.Request.Headers["X-Shell-Branch-Scope"].Any(x => string.Equals(x, "all", StringComparison.OrdinalIgnoreCase)))
            {
                var options = http.RequestServices.GetRequiredService<ProcurementCommercialRoundOptions>();
                var readiness = http.RequestServices.GetRequiredService<IProcurementCommercialRoundReviewReadiness>();
                if (options.ReviewEnabled && options.RecordEnabled && readiness.HasCapturedContract)
                {
                    try
                    {
                        var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                            scope.LegalEntityId, scope.SubjectId, token);
                        var material = await RoundLatestMaterialAuthor(db, current.Id,
                            submission.SubmittedPlanVersion, token);
                        var independent = actor is not null
                            && await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                                submission.MakerPrincipalId, submission.MakerWorkerAlias,
                                actor.PrincipalId, actor.CoreLinkedWorkerId, token)
                            && await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                                current.MakerPrincipalId, submitted.Enrollment.EnrolledByWorkerAlias,
                                actor.PrincipalId, actor.CoreLinkedWorkerId, token)
                            && await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                                material.PrincipalId, material.WorkerAlias,
                                actor.PrincipalId, actor.CoreLinkedWorkerId, token)
                            && await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                                source.OwnerSubjectId, source.MakerWorkerId,
                                actor.PrincipalId, actor.CoreLinkedWorkerId, token);
                        if (independent && await readiness.ReadyAsync(db, true, token)
                            && !await RoundExposedSourceIds(db).ContainsAsync(id, token))
                        {
                            _ = await RoundVerifyHeldSource(db, cases, scope, source, token);
                            var currentHash = await db.CommercialRoundPlanRevisions.AsNoTracking()
                                .Where(x => x.PlanId == current.Id && x.ResultVersion == current.Version)
                                .Select(x => x.ContentSha256).SingleOrDefaultAsync(token);
                            if (currentHash == submission.SubmittedContentSha256)
                            {
                                var policy = await db.PolicyVersions.AsNoTracking()
                                    .SingleOrDefaultAsync(x => x.Id == submission.PolicyVersionId, token);
                                var rules = policy is null ? null : CommercialRoundReviewPolicyRules.Read(policy.RulesJson);
                                if (rules is not null && policy!.PolicyVersionNumber == submission.PolicyVersionNumber
                                    && ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson) == submission.PolicyRulesSha256)
                                {
                                    _ = await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db,
                                        scope, policy, token, OperationalPolicyPublicationMode.RetainedHistorical);
                                    allowedActions = ["Return", "Reject"];
                                    var draft = CommercialRoundPlanDraft.Restore(submitted.Revision,
                                        submitted.Criteria, submitted.Participants);
                                    if (RoundReviewComplete(draft, rules, DateTimeOffset.UtcNow))
                                    {
                                        await RoundParticipants(core, scope, current.BranchId, draft, token);
                                        allowedActions = ["Approve", "Return", "Reject"];
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception error) when (error is not OperationCanceledException)
                    { allowedActions = []; }
                }
            }
            using var parsed = JsonDocument.Parse(submitted.Revision.ContentJson);
            var snapshot = parsed.RootElement.Clone();
            var response = JsonSerializer.Serialize(new { eventId = id, planId = submission.PlanId,
                eventHeader = new { source.Number, source.Title, source.Method,
                    source.CurrencyCode, branchId = source.BranchRef },
                submissionId = submission.Id, submittedRevisionId = submission.SubmittedRevisionId,
                submittedVersion = submission.SubmittedPlanVersion,
                currentPlanVersion = current.Version, currentStatus = current.Status,
                eventVersion = source.Version, currentRound, allowedActions,
                contentSha256 = submission.SubmittedContentSha256,
                submittedSnapshot = snapshot, caseId = approvalCase.Id,
                caseVersion = approvalCase.Version, caseState = approvalCase.State,
                assignmentId = assignment.Id, assignmentVersion = assignment.Version,
                assignmentState = assignment.State, policyVersionId = submission.PolicyVersionId,
                policyVersionNumber = submission.PolicyVersionNumber }, RoundJson);
            return new(response);
        }, ct, CommercialRoundReviewCoreWorkflow.ReviewPermission);

    private static Task<IResult> RoundReviewRecover(Guid id, Guid key,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunRound(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequireRoundReviewRead(http, db, token);
            if (key == Guid.Empty) throw new Problem("RoundReviewNotFound", 404);
            var receipt = await db.CommercialRoundPlanReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
                x.ActorPrincipalId == scope.SubjectId && x.CommandKey == key
                && x.SourcingEventId == id
                && (x.Operation == "Submit" || x.Operation == "Approve"
                    || x.Operation == "Return" || x.Operation == "Reject"), token)
                ?? throw new Problem("RoundReviewNotFound", 404);
            var permission = receipt.Operation == "Submit"
                ? CommercialRoundReviewCoreWorkflow.SubmitPermission
                : CommercialRoundReviewCoreWorkflow.ReviewPermission;
            var source = await RoundSource(db, core, scope, http, auth, permission, id, token);
            if (receipt.BranchId != source.BranchRef) throw new Problem("RoundReviewNotFound", 404);
            var replay = await RoundReplay(db, scope, id, key, receipt.Operation,
                receipt.RequestSha256, token) ?? throw new Problem("RoundReviewNotFound", 404);
            return replay;
        }, ct, "procurement-access");

    private static async Task RequireRoundReviewRead(HttpContext http, ProcurementDbContext db, CancellationToken ct)
    {
        var readiness = http.RequestServices.GetRequiredService<IProcurementCommercialRoundReviewReadiness>();
        if (!readiness.HasCapturedContract || !await readiness.ReadyAsync(db, false, ct))
            throw new Problem("RoundReviewHistoryUnavailable", 503);
    }
}
