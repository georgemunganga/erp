using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Permissioned plan task/history views; Core case and Procurement plan are
/// read in the same transaction, with branch filtering before paging.</summary>
public static class ProcurementPlanReviewReads
{
    internal static async Task<bool> ReadCapability(ProcurementRequestScope scope, HttpContext http,
        bool pending, CancellationToken ct)
    {
        var services = http.RequestServices;
        var options = services.GetService<ProcurementPlanReviewOptions>();
        var readiness = services.GetService<IProcurementPlanReviewReadiness>();
        var transaction = services.GetService<ProcurementApprovalTransaction>();
        var authorization = services.GetService<IAuthorizationService>();
        if (pending || options is not { ExecutionDatabaseConfigured: true }
            || readiness is null || transaction is null || authorization is null
            || scope.OrgUnitId is not null
            || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches)
            return false;
        var ownerRead = (await authorization.AuthorizeAsync(http.User, "procurement-plan-read")).Succeeded;
        var assignedRead = (await authorization.AuthorizeAsync(http.User, "procurement-plan-review-read")).Succeeded;
        if (!ownerRead && !assignedRead) return false;
        try
        {
            return await ReadAsync(transaction, readiness, async (db, _, token) =>
            {
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                    scope.LegalEntityId, scope.SubjectId, token);
                return actor is not null && (ownerRead && actor.Permissions.Contains("procurement-plan-read")
                    || assignedRead && actor.Permissions.Contains("procurement-plan-review-read"))
                    && (scope.IsExplicitAllBranches || scope.WorkLocationId is { } selected
                        && (actor.OrganisationWide || actor.AllowedBranches.Contains(selected)));
            }, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is ReviewReadUnavailable or DbException
            or InvalidOperationException or UnauthorizedAccessException) { return false; }
    }

    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/plans");
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (OperationCanceledException) { throw; }
            catch (UnauthorizedAccessException)
            { return Results.Json(new { code = "plan-review-read-denied" }, statusCode: 403); }
            catch (Exception error) when (error is ReviewReadUnavailable or DbException or InvalidOperationException
                or DbUpdateException)
            { return Unavailable(); }
        });
        group.MapGet("/review-tasks", Tasks).RequireAuthorization("procurement-plan-review-read");
        group.MapGet("/{id:guid}/review-history", History).RequireAuthorization("procurement-plan-read");
        group.MapGet("/review-submissions/{submissionId:guid}", Detail).RequireAuthorization("procurement-access");
        group.MapGet("/commands/{commandKey:guid}", Command).RequireAuthorization("procurement-access");
    }

    private static async Task<IResult> Tasks(ProcurementRequestScope scope,
        ProcurementPlanReviewOptions options, IProcurementPlanReviewReadiness readiness,
        ProcurementApprovalTransaction transaction, int? page, int? pageSize, CancellationToken ct)
    {
        if (!options.ExecutionDatabaseConfigured) return Unavailable();
        var writeReady = options.Enabled && await readiness.IsReadyAsync(ct);
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var result = await ReadAsync(transaction, readiness, async (db, core, token) =>
        {
            await using var people = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
            var current = await ProcurementPrincipalPeople.Resolve(people, scope.TenantId,
                scope.LegalEntityId, scope.SubjectId, token);
            if (current is null || !current.Permissions.Contains("procurement-plan-review-read"))
                return new { items = Array.Empty<object>(), total = 0, page = number,
                    pageSize = size, capped = false };
            var pending = await (from assignment in core.Assignments.AsNoTracking()
                join stage in core.Stages.AsNoTracking() on assignment.StageId equals stage.Id
                join approvalCase in core.Cases.AsNoTracking() on assignment.CaseId equals approvalCase.Id
                where assignment.AssigneeSubjectId == scope.SubjectId && assignment.State == "Pending"
                    && stage.State == "Active" && approvalCase.State == "Pending"
                    && approvalCase.TenantId == scope.TenantId
                    && approvalCase.LegalEntityId == scope.LegalEntityId
                    && approvalCase.SourceModule == "procurement"
                    && approvalCase.SourceType == PlanReviewCoreWorkflow.SourceType
                orderby approvalCase.OpenedAt descending, approvalCase.Id descending
                select new { planId = approvalCase.SourceId, caseId = approvalCase.Id,
                    caseVersion = approvalCase.Version, approvalCase.SourceVersion,
                    approvalCase.TenantId, approvalCase.LegalEntityId,
                    approvalCase.PolicyVersionId, approvalCase.PolicyVersionNumber,
                    stage.Code, assignmentId = assignment.Id,
                    assignmentVersion = assignment.Version }).Take(1000).ToArrayAsync(token);
            var ids = pending.Select(x => x.planId).Distinct().ToArray();
            var plans = await db.ProcurementPlans.AsNoTracking().Where(x => ids.Contains(x.Id)
                && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                && (scope.IsExplicitAllBranches && x.BranchRef.HasValue
                        && scope.PermittedWorkLocationIds.Contains(x.BranchRef.Value)
                    || !scope.IsExplicitAllBranches && x.BranchRef == scope.WorkLocationId))
                .Select(x => new { x.Id, x.Number, x.BranchRef, x.Status, x.CurrencyCode,
                    x.OwnerSubjectId, x.ApprovalInstanceRef, x.Version }).ToArrayAsync(token);
            var byId = plans.ToDictionary(x => x.Id);
            var eligible = pending.Where(x => byId.ContainsKey(x.planId)
                    && current.Permissions.Contains(x.Code == "Review"
                        ? PlanReviewCoreWorkflow.ReviewPermission : PlanReviewCoreWorkflow.FinalPermission)
                    && (current.OrganisationWide || byId[x.planId].BranchRef is { } allowed
                        && current.AllowedBranches.Contains(allowed))).ToArray();
            var caseIds = eligible.Select(x => x.caseId).Distinct().ToArray();
            var rounds = await db.PlanReviewSubmissions.AsNoTracking()
                .Where(x => caseIds.Contains(x.CoreApprovalCaseId)).ToArrayAsync(token);
            var roundByCase = rounds.GroupBy(x => x.CoreApprovalCaseId)
                .ToDictionary(x => x.Key, x => x.ToArray());
            var linked = eligible.Select(x =>
            {
                if (!roundByCase.TryGetValue(x.caseId, out var candidates) || candidates.Length != 1)
                    throw new ReviewReadUnavailable();
                var plan = byId[x.planId];
                var submissionId = PendingTaskSubmissionId(candidates[0], x.planId, x.caseId,
                    x.SourceVersion, x.TenantId, x.LegalEntityId, plan.BranchRef,
                    plan.OwnerSubjectId, plan.Status, plan.ApprovalInstanceRef,
                    x.PolicyVersionId, x.PolicyVersionNumber);
                if (submissionId == Guid.Empty) throw new ReviewReadUnavailable();
                return new { task = x, submissionId };
            }).ToArray();
            var items = linked.Select(x => new { x.task.planId, x.task.caseId,
                    x.submissionId, x.task.caseVersion, x.task.Code, x.task.assignmentId,
                    x.task.assignmentVersion, plan = byId[x.task.planId],
                    allowedActions = writeReady ? new[] { "Approve", "Return", "Reject" } : Array.Empty<string>() }).ToArray();
            return new { items = items.Skip((number - 1) * size).Take(size)
                    .Select(x => (object)x).ToArray(),
                total = items.Length, page = number, pageSize = size,
                capped = pending.Length == 1000 };
        }, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> History(Guid id, ProcurementRequestScope scope,
        ProcurementPlanReviewOptions options, IProcurementPlanReviewReadiness readiness,
        ProcurementApprovalTransaction transaction, CancellationToken ct)
    {
        if (!options.ExecutionDatabaseConfigured) return Unavailable();
        var result = await ReadAsync(transaction, readiness, async (db, core, token) =>
        {
            var plan = await db.ProcurementPlans.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == id && x.OwnerSubjectId == scope.SubjectId, token);
            if (plan is null || !BranchVisible(plan.BranchRef, scope)) return null;
            var rounds = await db.PlanReviewSubmissions.AsNoTracking()
                .Where(x => x.ProcurementPlanId == id).OrderBy(x => x.SubmittedAt)
                .Select(x => new { x.Id, x.CoreApprovalCaseId, x.SubmittedPlanVersion,
                    x.PolicyVersionId, x.PolicyVersionNumber, x.State, x.SubmittedAt, x.CompletedAt,
                    x.SnapshotSha256, x.CurrencyCode, x.EstimatedAmount })
                .ToArrayAsync(token);
            var caseIds = rounds.Select(x => x.CoreApprovalCaseId).ToArray();
            var decisions = await core.Decisions.AsNoTracking().Where(x => caseIds.Contains(x.CaseId))
                .OrderBy(x => x.DecidedAt).ThenBy(x => x.Id)
                .Select(x => new { x.Id, x.CaseId, x.StageId, x.AssignmentId,
                    x.ActorSubjectId, x.Action, x.Reason, x.DecidedAt })
                .ToArrayAsync(token);
            return new { planId = plan.Id, planVersion = plan.Version, planStatus = plan.Status,
                rounds, decisions };
        }, ct);
        return result is null ? Missing() : Results.Ok(result);
    }

    private static async Task<IResult> Detail(Guid submissionId, HttpContext http,
        IAuthorizationService authorization, ProcurementRequestScope scope,
        ProcurementPlanReviewOptions options, IProcurementPlanReviewReadiness readiness,
        ProcurementApprovalTransaction transaction, CancellationToken ct)
    {
        if (!options.ExecutionDatabaseConfigured) return Unavailable();
        var writeReady = options.Enabled && await readiness.IsReadyAsync(ct);
        var result = await ReadAsync(transaction, readiness, async (db, core, token) =>
        {
            var round = await db.PlanReviewSubmissions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == submissionId, token);
            if (round is null) return null;
            var plan = await db.ProcurementPlans.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == round.ProcurementPlanId, token);
            if (plan is null || !BranchVisible(plan.BranchRef, scope)) return null;
            var approvalCase = await core.Cases.AsNoTracking().Include(x => x.Stages)
                .ThenInclude(x => x.Assignments)
                .SingleOrDefaultAsync(x => x.Id == round.CoreApprovalCaseId
                    && x.SourceType == PlanReviewCoreWorkflow.SourceType && x.SourceId == plan.Id, token);
            if (approvalCase is null) return null;
            var assigned = approvalCase.Stages.SelectMany(x => x.Assignments)
                .Any(x => x.AssigneeSubjectId == scope.SubjectId);
            if (plan.OwnerSubjectId != scope.SubjectId && !assigned) return null;
            var permission = plan.OwnerSubjectId == scope.SubjectId
                ? "procurement-plan-read" : "procurement-plan-review-read";
            if (!(await authorization.AuthorizeAsync(http.User, permission)).Succeeded) return null;
            var canWithdraw = writeReady && plan.OwnerSubjectId == scope.SubjectId
                && (plan.Status is "Submitted" or "Reviewed")
                && (await authorization.AuthorizeAsync(http.User,
                    PlanReviewCoreWorkflow.WithdrawPermission)).Succeeded;
            if (!PlanReviewSnapshot.TryReadRetained(round, out var submittedSnapshot))
                return new DetailRead(503, new { code = "plan-review-evidence-unavailable",
                    message = "The submitted plan evidence could not be verified." });
            var decisions = await core.Decisions.AsNoTracking().Where(x => x.CaseId == approvalCase.Id)
                .OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).ToArrayAsync(token);
            return new DetailRead(200, new { planId = plan.Id, planNumber = plan.Number, planStatus = plan.Status,
                planVersion = plan.Version, submissionId = round.Id, round.State,
                round.SubmittedPlanVersion, round.CurrencyCode, round.EstimatedAmount,
                submittedSnapshot, round.SnapshotSha256,
                allowedActions = canWithdraw ? new[] { "Withdraw" } : Array.Empty<string>(),
                round.SubmittedAt, round.CompletedAt, caseId = approvalCase.Id,
                caseState = approvalCase.State, caseVersion = approvalCase.Version,
                stages = approvalCase.Stages.OrderBy(x => x.Sequence).Select(x => new
                {
                    x.Id, x.Code, x.Mode, x.RequiredApprovals, x.State,
                    assignments = x.Assignments.Select(a => new { a.Id, a.AssigneeSubjectId,
                        a.State, a.Version,
                        allowedActions = writeReady && x.State == "Active" && a.State == "Pending"
                            && a.AssigneeSubjectId == scope.SubjectId
                            && (x.Code == "Review" && http.User.HasClaim(ProcurementPermissions.ActionClaim,
                                    PlanReviewCoreWorkflow.ReviewPermission)
                                || x.Code == "Final" && http.User.HasClaim(ProcurementPermissions.ActionClaim,
                                    PlanReviewCoreWorkflow.FinalPermission))
                                ? new[] { "Approve", "Return", "Reject" } : Array.Empty<string>() }).ToArray(),
                }).ToArray(),
                decisions = decisions.Select(x => new { x.Id, x.StageId, x.AssignmentId,
                    x.ActorSubjectId, x.Action, x.Reason, x.DecidedAt }).ToArray() });
        }, ct);
        return result is null ? Missing() : Results.Json(result.Payload, statusCode: result.Status);
    }

    private static async Task<IResult> Command(Guid commandKey, HttpContext http,
        IAuthorizationService authorization, ProcurementRequestScope scope,
        ProcurementPlanReviewOptions options, IProcurementPlanReviewReadiness readiness,
        ProcurementApprovalTransaction transaction, CancellationToken ct)
    {
        if (!options.ExecutionDatabaseConfigured) return Unavailable();
        var result = await ReadAsync(transaction, readiness, async (db, _, token) =>
        {
            var receipt = await db.PlanReviewCommandReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CommandKey == commandKey
                    && x.ActorSubjectId == scope.SubjectId, token);
            if (receipt is null) return null;
            var plan = await db.ProcurementPlans.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == receipt.ProcurementPlanId, token);
            if (plan is null || !BranchVisible(plan.BranchRef, scope)) return null;
            var ownerRead = plan.OwnerSubjectId == scope.SubjectId
                && (await authorization.AuthorizeAsync(http.User, "procurement-plan-read")).Succeeded;
            var actorRead = (await authorization.AuthorizeAsync(http.User,
                "procurement-plan-review-read")).Succeeded;
            if (!ownerRead && !actorRead) return null;
            return new { receipt.ResponseStatus, receipt.ResponseJson };
        }, ct);
        return result is null ? Missing()
            : Results.Content(result.ResponseJson, "application/json", statusCode: result.ResponseStatus);
    }

    private static bool BranchVisible(Guid? branch, ProcurementRequestScope scope) => branch is { } id
        && (scope.IsExplicitAllBranches ? scope.PermittedWorkLocationIds.Contains(id)
            : scope.WorkLocationId == id);
    internal static Guid PendingTaskSubmissionId(PlanReviewSubmission round, Guid planId,
        Guid caseId, long sourceVersion, string tenantId, Guid legalEntityId, Guid? branch,
        string ownerSubjectId, string planStatus, string? activeCaseRef,
        Guid policyVersionId, int policyVersionNumber)
        => round.Id != Guid.Empty && round.Id != caseId
            && round.CoreApprovalCaseId == caseId && round.ProcurementPlanId == planId
            && round.SubmittedPlanVersion == sourceVersion && round.TenantId == tenantId
            && round.LegalEntityId == legalEntityId && branch is { } claimed
            && round.BranchRef == claimed && round.MakerSubjectId == ownerSubjectId
            && round.PolicyVersionId == policyVersionId
            && round.PolicyVersionNumber == policyVersionNumber
            && round.State == "Pending" && round.CompletedAt is null
            && (planStatus is "Submitted" or "Reviewed")
            && activeCaseRef == caseId.ToString("D")
            && PlanReviewSnapshot.TryReadRetained(round, out _)
                ? round.Id : Guid.Empty;
    private static Task<TResult> ReadAsync<TResult>(ProcurementApprovalTransaction transaction,
        IProcurementPlanReviewReadiness readiness,
        Func<ProcurementDbContext, ApprovalDbContext, CancellationToken, Task<TResult>> read,
        CancellationToken ct) => transaction.ExecuteAsync(async (db, core, token) =>
        {
            await db.Database.ExecuteSqlRawAsync("SET LOCAL transaction_read_only=on", token);
            if (!await readiness.CanReadReviewAsync(db, token)) throw new ReviewReadUnavailable();
            return await read(db, core, token);
        }, ct);
    private sealed class ReviewReadUnavailable : Exception { }
    private static IResult Unavailable() => Results.Json(new { code = "plan-review-unavailable",
        message = "Plan review is not ready in this workspace." }, statusCode: 503);
    private static IResult Missing() => Results.Json(new { code = "not-found",
        message = "Plan review not found." }, statusCode: 404);
    private sealed record DetailRead(int Status, object Payload);
}
