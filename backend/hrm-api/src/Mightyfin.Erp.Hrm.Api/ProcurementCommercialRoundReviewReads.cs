using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
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
            items.Add(new { eventId = round.SourcingEventId, planId = plan.Id,
                submissionId = round.Id, caseId = round.CoreCaseId,
                assignmentId = task.Id, submittedVersion = round.SubmittedPlanVersion,
                currentPlanVersion = plan.Version, contentSha256 = round.SubmittedContentSha256,
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
            using var parsed = JsonDocument.Parse(submitted.Revision.ContentJson);
            var snapshot = parsed.RootElement.Clone();
            var response = JsonSerializer.Serialize(new { eventId = id, planId = submission.PlanId,
                submissionId = submission.Id, submittedRevisionId = submission.SubmittedRevisionId,
                submittedVersion = submission.SubmittedPlanVersion,
                currentPlanVersion = current.Version, currentStatus = current.Status,
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
