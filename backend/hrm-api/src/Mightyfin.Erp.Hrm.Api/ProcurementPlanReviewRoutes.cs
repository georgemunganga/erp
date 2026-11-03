using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Governed Plan commands use the existing Core case transaction. Nothing
/// here grants budget authority or converts a forecast into a purchase request.</summary>
public static class ProcurementPlanReviewRoutes
{
    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/plans");
        group.MapPost("/{id:guid}/review-submissions", Submit)
            .RequireAuthorization("procurement-plan-submit");
        group.MapPost("/review-submissions/{submissionId:guid}/assignments/{assignmentId:guid}/decisions", Decide)
            .RequireAuthorization("procurement-access");
        group.MapPost("/review-submissions/{submissionId:guid}/withdrawals", Withdraw)
            .RequireAuthorization(PlanReviewCoreWorkflow.WithdrawPermission);
    }

    private static async Task<IResult> Submit(Guid id, SubmitInput input, HttpContext http,
        ProcurementRequestScope scope, ProcurementPlanReviewOptions options,
        IProcurementPlanReviewReadiness readiness, ProcurementApprovalTransaction transaction,
        CancellationToken ct)
    {
        if (!options.Enabled || !options.ExecutionDatabaseConfigured || !await readiness.IsReadyAsync(ct))
            return Problem("plan-review-unavailable", "Plan review is not ready in this workspace.", 503);
        if (scope.IsExplicitAllBranches || scope.WorkLocationId is not { } branch
            || scope.PrincipalId is null) return Problem("branch-required", "Choose a permitted branch.", 400);
        if (!TryKey(http, out var key)) return Problem("command-key-required",
            "Send one valid Idempotency-Key for safe recovery.", 422);
        var fingerprint = Hash(new { operation = "Submit", id, actor = scope.SubjectId, branch,
            input.ExpectedPlanVersion });
        try
        {
            var result = await transaction.ExecuteAsync(async (db, workflow, token) =>
            {
                await Lock(db, scope, id, token);
                var plan = await db.ProcurementPlans.Include(x => x.Lines)
                    .SingleOrDefaultAsync(x => x.Id == id && x.OwnerSubjectId == scope.SubjectId
                        && x.BranchRef == branch, token);
                if (plan is null) return new Reply(404, Json(new { code = "not-found", message = "Plan not found." }));
                var previous = await db.PlanReviewCommandReceipts.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.CommandKey == key, token);
                if (previous is not null)
                    return previous.ProcurementPlanId == id && previous.Operation == "Submit"
                        && previous.ActorSubjectId == scope.SubjectId && previous.RequestSha256 == fingerprint
                        ? new Reply(previous.ResponseStatus, previous.ResponseJson)
                        : new Reply(409, Json(new { code = "command-key-conflict", message = "That key belongs to another action." }));
                if (plan.Version != input.ExpectedPlanVersion)
                    return new Reply(409, Json(new { code = "version-conflict", message = "Refresh the plan before submitting." }));
                if (await db.PlanReviewSubmissions.AnyAsync(x => x.ProcurementPlanId == id && x.State == "Pending", token))
                    return new Reply(409, Json(new { code = "plan-already-submitted", message = "A plan review is already open." }));
                _ = PlanReviewLifecycle.Submit(plan.Status, plan.Lines.Any(x => x.Status == "Active"), true);
                var now = DateTimeOffset.UtcNow;
                var policies = await db.PolicyVersions.Where(x => x.PolicyKey == PlanReviewPolicyRules.PolicyKey
                        && x.State == "Published" && x.EffectiveFrom <= now
                        && (x.EffectiveTo == null || x.EffectiveTo > now))
                    .OrderByDescending(x => x.PolicyVersionNumber).Take(2).ToArrayAsync(token);
                if (policies.Length != 1 || PlanReviewPolicyRules.Read(policies[0].RulesJson) is not { } definition)
                    return new Reply(409, Json(new { code = "plan-policy-unavailable",
                        message = "A single approved plan review policy is required." }));
                var policy = policies[0];
                _ = await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db, scope, policy, token);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                var currentMaker = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                    scope.LegalEntityId, scope.SubjectId, token);
                if (currentMaker is null || !currentMaker.Permissions.Contains("procurement-plan-submit"))
                    return new Reply(403, Json(new { code = "plan-submission-denied", message = "Plan submission is unavailable." }));
                var assignees = new List<ProcurementPrincipalPeople.Person>();
                foreach (var principalId in definition.Stages.SelectMany(x => x.AssigneePrincipalIds))
                {
                    var current = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                        scope.LegalEntityId, principalId.ToString("D"), token);
                    if (current is null) return new Reply(409, Json(new { code = "plan-assignee-unavailable",
                        message = "A named approver is unavailable." }));
                    assignees.Add(current);
                }
                var people = new[] { currentMaker }.Concat(assignees).ToArray();
                for (var left = 0; left < people.Length; left++)
                    for (var right = left + 1; right < people.Length; right++)
                        if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                            people[left].PrincipalId, people[left].CoreLinkedWorkerId,
                            people[right].PrincipalId, people[right].CoreLinkedWorkerId, token))
                            return new Reply(409, Json(new { code = "plan-assignee-conflict",
                                message = "The plan requires independent people for each approval stage." }));
                static PlanReviewPrincipal Convert(ProcurementPrincipalPeople.Person person,
                    ProcurementRequestScope context) => new(Guid.ParseExact(person.PrincipalId, "D"),
                        person.CoreLinkedWorkerId, context.TenantId,
                        context.LegalEntityId, person.OrganisationWide, person.AllowedBranches,
                        person.Permissions);
                var previousState = ProcurementDraftAudit.Snapshot(new { plan.Status, plan.Version });
                plan.Status = "Submitted";
                plan.StatusReason = null;
                var submittedVersion = checked(plan.Version + 1);
                var snapshot = PlanReviewSnapshot.Create(plan, submittedVersion);
                var submission = new PlanReviewSubmission
                {
                    ProcurementPlanId = plan.Id, BranchRef = branch, SubmittedPlanVersion = submittedVersion,
                    CoreApprovalCaseId = Guid.CreateVersion7(), PolicyVersionId = policy.Id,
                    PolicyVersionNumber = policy.PolicyVersionNumber,
                    PolicyRulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
                    MakerSubjectId = scope.SubjectId,
                    MakerCoreLinkedWorkerId = currentMaker.CoreLinkedWorkerId,
                    CurrencyCode = plan.CurrencyCode,
                    EstimatedAmount = snapshot.EstimatedAmount, SnapshotJson = snapshot.Json,
                    SnapshotSha256 = snapshot.Sha256, SubmittedAt = now,
                };
                db.AddScoped(submission);
                var approvalCase = PlanReviewCoreWorkflow.Open(plan, submission, policy, definition,
                    Convert(currentMaker, scope), assignees.Select(x => Convert(x, scope)).ToArray(), now);
                workflow.AddCase(approvalCase);
                plan.ApprovalInstanceRef = approvalCase.Id.ToString("D");
                ProcurementDraftAudit.Add(db, scope, "ProcurementPlan", plan.Id, "PlanSubmitted",
                    previousState, new { plan.Status, submittedVersion, submission.Id, approvalCase.Id,
                        snapshot.Sha256, policy.Id, policy.PolicyVersionNumber });
                db.AddScoped(new OutboxEvent
                {
                    AggregateType = "ProcurementPlan", AggregateId = plan.Id,
                    EventType = "procurement.plan.submitted.v1", IdempotencyKey = key.ToString("D"),
                    PayloadJson = Json(new { planId = plan.Id, submissionId = submission.Id,
                        caseId = approvalCase.Id, submittedVersion, branchRef = branch,
                        currencyCode = plan.CurrencyCode, estimatedAmount = snapshot.EstimatedAmount,
                        policyVersionId = policy.Id, policy.PolicyVersionNumber }),
                });
                var response = Json(new { planId = plan.Id, planVersion = submittedVersion,
                    submissionId = submission.Id, caseId = approvalCase.Id, status = plan.Status });
                db.AddScoped(new PlanReviewCommandReceipt
                {
                    ProcurementPlanId = plan.Id, CommandKey = key, Operation = "Submit",
                    ActorSubjectId = scope.SubjectId, RequestSha256 = fingerprint,
                    ResponseJson = response, ResponseStatus = 201,
                });
                return new Reply(201, response);
            }, ct);
            return Results.Content(result.Body, "application/json", statusCode: result.Status);
        }
        catch (OperationCanceledException) { throw; }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh the plan before submitting.", 409); }
        catch (InvalidOperationException) { return Problem("plan-review-conflict", "Refresh the plan or review its approval settings.", 409); }
        catch (Exception) { return Problem("plan-submission-unconfirmed", "Check the original command key before retrying.", 503); }
    }

    private static async Task<IResult> Decide(Guid submissionId, Guid assignmentId, DecideInput input,
        HttpContext http, ProcurementRequestScope scope, ProcurementPlanReviewOptions options,
        IProcurementPlanReviewReadiness readiness, ProcurementApprovalTransaction transaction,
        CancellationToken ct)
    {
        if (!options.Enabled || !options.ExecutionDatabaseConfigured || !await readiness.IsReadyAsync(ct))
            return Problem("plan-review-unavailable", "Plan review is not ready in this workspace.", 503);
        if (scope.IsExplicitAllBranches || scope.WorkLocationId is not { } branch || scope.PrincipalId is null)
            return Problem("branch-required", "Choose a permitted branch.", 400);
        if (!TryKey(http, out var key)) return Problem("command-key-required",
            "Send one valid Idempotency-Key for safe recovery.", 422);
        var reason = input.Reason?.Trim();
        if (input.Action is not ("Approve" or "Return" or "Reject")
            || input.Action != "Approve" && string.IsNullOrWhiteSpace(reason)
            || reason?.Length > 2000)
            return Problem("decision-invalid", "Choose a decision and include a reason when returning or rejecting.", 422);
        var fingerprint = Hash(new { operation = "Decide", submissionId, assignmentId,
            actor = scope.SubjectId, branch, input.Action, reason, input.ExpectedPlanVersion,
            input.ExpectedCaseVersion, input.ExpectedAssignmentVersion });
        try
        {
            var result = await transaction.ExecuteAsync(async (db, workflow, token) =>
            {
                var submission = await db.PlanReviewSubmissions.SingleOrDefaultAsync(x => x.Id == submissionId, token);
                if (submission is null || submission.BranchRef != branch)
                    return new Reply(404, Json(new { code = "not-found", message = "Review not found." }));
                await Lock(db, scope, submission.ProcurementPlanId, token);
                var plan = await db.ProcurementPlans.Include(x => x.Lines)
                    .SingleOrDefaultAsync(x => x.Id == submission.ProcurementPlanId && x.BranchRef == branch, token);
                if (plan is null) return new Reply(404, Json(new { code = "not-found", message = "Plan not found." }));
                var approvalCase = await workflow.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
                    .SingleOrDefaultAsync(x => x.Id == submission.CoreApprovalCaseId
                        && x.SourceModule == "procurement" && x.SourceType == PlanReviewCoreWorkflow.SourceType
                        && x.SourceId == plan.Id, token);
                var assignment = approvalCase?.Stages.SelectMany(x => x.Assignments)
                    .SingleOrDefault(x => x.Id == assignmentId && x.AssigneeSubjectId == scope.SubjectId);
                if (assignment is null || approvalCase is null || assignment.DelegatedFromSubjectId is not null)
                    return new Reply(404, Json(new { code = "not-found", message = "Review task not found." }));
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                var currentActor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                    scope.LegalEntityId, scope.SubjectId, token);
                if (currentActor is null
                    || !currentActor.Permissions.Contains(assignment.PermissionKey)
                    || !await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                        plan.OwnerSubjectId, submission.MakerCoreLinkedWorkerId,
                        currentActor.PrincipalId, currentActor.CoreLinkedWorkerId, token))
                    return new Reply(403, Json(new { code = "plan-review-denied", message = "This approval is unavailable." }));
                var previous = await db.PlanReviewCommandReceipts.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.CommandKey == key, token);
                if (previous is not null)
                    return previous.ProcurementPlanId == plan.Id && previous.Operation == "Decide"
                        && previous.ActorSubjectId == scope.SubjectId && previous.RequestSha256 == fingerprint
                        ? new Reply(previous.ResponseStatus, previous.ResponseJson)
                        : new Reply(409, Json(new { code = "command-key-conflict", message = "That key belongs to another action." }));
                if (plan.Version != input.ExpectedPlanVersion || approvalCase.Version != input.ExpectedCaseVersion
                    || assignment.Version != input.ExpectedAssignmentVersion)
                    return new Reply(409, Json(new { code = "version-conflict", message = "Refresh this approval before deciding." }));
                var now = DateTimeOffset.UtcNow;
                var policy = await db.PolicyVersions.SingleOrDefaultAsync(x => x.Id == submission.PolicyVersionId, token);
                if (!PlanReviewPinnedPolicy.Matches(policy, submission))
                    return new Reply(409, Json(new { code = "plan-policy-changed", message = "The submitted plan policy needs review." }));
                // This is retained provenance for an already-open pinned case. Current
                // account, action, branch, SoD and assignment checks still govern each vote.
                _ = await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db, scope, policy!, token,
                    OperationalPolicyPublicationMode.RetainedHistorical);
                static PlanReviewPrincipal Convert(ProcurementPrincipalPeople.Person person,
                    ProcurementRequestScope context) => new(Guid.ParseExact(person.PrincipalId, "D"),
                        person.CoreLinkedWorkerId, context.TenantId,
                        context.LegalEntityId, person.OrganisationWide, person.AllowedBranches,
                        person.Permissions);
                var beforeStatus = plan.Status;
                var decisions = await workflow.Decisions.Where(x => x.CaseId == approvalCase.Id).ToListAsync(token);
                if (input.Action == "Approve" && approvalCase.Stages.Single(x => x.Id == assignment.StageId).Code == "Final")
                {
                    var earlierVotes = decisions.Where(x => x.Action == "Approve").ToArray();
                    var earlierIds = earlierVotes.Select(x => x.Id).ToArray();
                    var earlierReceipts = await db.PlanReviewCommandReceipts.AsNoTracking()
                        .Where(x => x.ProcurementPlanId == plan.Id && x.Operation == "Decide"
                            && x.DecisionId.HasValue && earlierIds.Contains(x.DecisionId.Value))
                        .ToArrayAsync(token);
                    if (earlierReceipts.Length != earlierVotes.Length)
                        return new Reply(409, Json(new { code = "plan-review-evidence-missing",
                            message = "Review evidence must be checked before final approval." }));
                    foreach (var earlier in earlierVotes)
                    {
                        var evidence = earlierReceipts.SingleOrDefault(x => x.DecisionId == earlier.Id
                            && x.ActorSubjectId == earlier.ActorSubjectId);
                        if (evidence is null)
                            return new Reply(409, Json(new { code = "plan-review-evidence-missing",
                                message = "Review evidence must be checked before final approval." }));
                        if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                            earlier.ActorSubjectId, evidence.ActorCoreLinkedWorkerId,
                            currentActor.PrincipalId,
                            currentActor.CoreLinkedWorkerId, token))
                            return new Reply(403, Json(new { code = "plan-review-conflict",
                                message = "Final approval requires a different person from the reviewer." }));
                    }
                }
                var decision = PlanReviewCoreWorkflow.Decide(plan, submission, approvalCase, assignment,
                    new(Guid.ParseExact(plan.OwnerSubjectId, "D"), submission.MakerCoreLinkedWorkerId, scope.TenantId,
                        scope.LegalEntityId, false, new HashSet<Guid>(), new HashSet<string>()),
                    Convert(currentActor, scope), input.Action,
                    key.ToString("D"), reason, decisions, now);
                workflow.Decisions.Add(decision);
                var resultingPlanVersion = plan.Version + (plan.Status == beforeStatus ? 0 : 1);
                ProcurementDraftAudit.Add(db, scope, "ProcurementPlan", plan.Id, "PlanDecisionRecorded",
                    new { status = beforeStatus, plan.Version },
                    new { plan.Status, resultingPlanVersion, decision.Id, assignmentId,
                        caseId = approvalCase.Id, submissionId, input.Action, reason });
                db.AddScoped(new OutboxEvent
                {
                    AggregateType = "ProcurementPlan", AggregateId = plan.Id,
                    EventType = plan.Status switch
                    {
                        "Active" => "procurement.plan.approved.v1",
                        "Returned" => "procurement.plan.returned.v1",
                        "Rejected" => "procurement.plan.rejected.v1",
                        "Reviewed" when beforeStatus != "Reviewed" => "procurement.plan.reviewed.v1",
                        _ => "procurement.plan.decision_recorded.v1",
                    },
                    IdempotencyKey = key.ToString("D"),
                    PayloadJson = Json(new { planId = plan.Id, submissionId, caseId = approvalCase.Id,
                        assignmentId, decisionId = decision.Id, action = input.Action, status = plan.Status,
                        planVersion = resultingPlanVersion }),
                });
                var response = Json(new { planId = plan.Id, submissionId, caseId = approvalCase.Id,
                    assignmentId, decisionId = decision.Id, status = plan.Status,
                    roundState = submission.State, planVersion = resultingPlanVersion,
                    caseVersion = approvalCase.Version + 1, assignmentVersion = assignment.Version + 1 });
                db.AddScoped(new PlanReviewCommandReceipt
                {
                    ProcurementPlanId = plan.Id, CommandKey = key, Operation = "Decide",
                    DecisionId = decision.Id, ActorCoreLinkedWorkerId = currentActor.CoreLinkedWorkerId,
                    ActorSubjectId = scope.SubjectId, RequestSha256 = fingerprint,
                    ResponseJson = response, ResponseStatus = 200,
                });
                return new Reply(200, response);
            }, ct);
            return Results.Content(result.Body, "application/json", statusCode: result.Status);
        }
        catch (OperationCanceledException) { throw; }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh this approval before deciding.", 409); }
        catch (InvalidOperationException) { return Problem("plan-review-conflict", "Refresh the approval or review its policy.", 409); }
        catch (Exception) { return Problem("plan-decision-unconfirmed", "Check the original command key before retrying.", 503); }
    }

    private static async Task<IResult> Withdraw(Guid submissionId, WithdrawInput input, HttpContext http,
        ProcurementRequestScope scope, ProcurementPlanReviewOptions options,
        IProcurementPlanReviewReadiness readiness, ProcurementApprovalTransaction transaction,
        CancellationToken ct)
    {
        if (!options.Enabled || !options.ExecutionDatabaseConfigured || !await readiness.IsReadyAsync(ct))
            return Problem("plan-review-unavailable", "Plan review is not ready in this workspace.", 503);
        if (scope.IsExplicitAllBranches || scope.WorkLocationId is not { } branch || scope.PrincipalId is null)
            return Problem("branch-required", "Choose a permitted branch.", 400);
        if (!TryKey(http, out var key)) return Problem("command-key-required",
            "Send one valid Idempotency-Key for safe recovery.", 422);
        var reason = input.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
            return Problem("reason-required", "Give a withdrawal reason of up to 2,000 characters.", 422);
        var fingerprint = Hash(new { operation = "Withdraw", submissionId, actor = scope.SubjectId,
            branch, reason, input.ExpectedPlanVersion, input.ExpectedCaseVersion });
        try
        {
            var result = await transaction.ExecuteAsync(async (db, workflow, token) =>
            {
                var submission = await db.PlanReviewSubmissions.SingleOrDefaultAsync(x => x.Id == submissionId, token);
                if (submission is null || submission.BranchRef != branch)
                    return new Reply(404, Json(new { code = "not-found", message = "Review not found." }));
                await Lock(db, scope, submission.ProcurementPlanId, token);
                var plan = await db.ProcurementPlans.Include(x => x.Lines)
                    .SingleOrDefaultAsync(x => x.Id == submission.ProcurementPlanId
                        && x.BranchRef == branch && x.OwnerSubjectId == scope.SubjectId, token);
                if (plan is null) return new Reply(404, Json(new { code = "not-found", message = "Plan not found." }));
                var approvalCase = await workflow.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
                    .SingleOrDefaultAsync(x => x.Id == submission.CoreApprovalCaseId
                        && x.SourceModule == "procurement" && x.SourceType == PlanReviewCoreWorkflow.SourceType
                        && x.SourceId == plan.Id, token);
                if (approvalCase is null) return new Reply(409, Json(new { code = "review-missing", message = "Plan review is unavailable." }));
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                var currentMaker = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                    scope.LegalEntityId, scope.SubjectId, token);
                if (currentMaker is null || !currentMaker.Permissions.Contains(PlanReviewCoreWorkflow.WithdrawPermission))
                    return new Reply(403, Json(new { code = "plan-withdrawal-denied", message = "Withdrawal is unavailable." }));
                var previous = await db.PlanReviewCommandReceipts.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.CommandKey == key, token);
                if (previous is not null)
                    return previous.ProcurementPlanId == plan.Id && previous.Operation == "Withdraw"
                        && previous.ActorSubjectId == scope.SubjectId && previous.RequestSha256 == fingerprint
                        ? new Reply(previous.ResponseStatus, previous.ResponseJson)
                        : new Reply(409, Json(new { code = "command-key-conflict", message = "That key belongs to another action." }));
                if (plan.Version != input.ExpectedPlanVersion || approvalCase.Version != input.ExpectedCaseVersion)
                    return new Reply(409, Json(new { code = "version-conflict", message = "Refresh this plan before withdrawing." }));
                var active = approvalCase.Stages.SingleOrDefault(x => x.State == "Active")?
                    .Assignments.FirstOrDefault(x => x.State == "Pending");
                if (active is null) return new Reply(409, Json(new { code = "plan-review-closed", message = "The review has already ended." }));
                var now = DateTimeOffset.UtcNow;
                var decisions = await workflow.Decisions.Where(x => x.CaseId == approvalCase.Id).ToListAsync(token);
                var decision = PlanReviewCoreWorkflow.Withdraw(plan, submission, approvalCase, active,
                    new(Guid.ParseExact(currentMaker.PrincipalId, "D"),
                        currentMaker.CoreLinkedWorkerId, scope.TenantId,
                        scope.LegalEntityId, currentMaker.OrganisationWide, currentMaker.AllowedBranches,
                        currentMaker.Permissions), key.ToString("D"), reason, decisions, now);
                workflow.Decisions.Add(decision);
                var planVersion = checked(plan.Version + 1);
                ProcurementDraftAudit.Add(db, scope, "ProcurementPlan", plan.Id, "PlanWithdrawn",
                    new { status = "SubmittedOrReviewed", plan.Version },
                    new { plan.Status, planVersion, submissionId, caseId = approvalCase.Id,
                        decisionId = decision.Id, reason });
                db.AddScoped(new OutboxEvent
                {
                    AggregateType = "ProcurementPlan", AggregateId = plan.Id,
                    EventType = "procurement.plan.withdrawn.v1", IdempotencyKey = key.ToString("D"),
                    PayloadJson = Json(new { planId = plan.Id, submissionId, caseId = approvalCase.Id,
                        decisionId = decision.Id, status = plan.Status, planVersion }),
                });
                var response = Json(new { planId = plan.Id, submissionId, caseId = approvalCase.Id,
                    decisionId = decision.Id, status = plan.Status, roundState = submission.State,
                    planVersion, caseVersion = approvalCase.Version + 1 });
                db.AddScoped(new PlanReviewCommandReceipt
                {
                    ProcurementPlanId = plan.Id, CommandKey = key, Operation = "Withdraw",
                    DecisionId = decision.Id, ActorCoreLinkedWorkerId = currentMaker.CoreLinkedWorkerId,
                    ActorSubjectId = scope.SubjectId, RequestSha256 = fingerprint,
                    ResponseJson = response, ResponseStatus = 200,
                });
                return new Reply(200, response);
            }, ct);
            return Results.Content(result.Body, "application/json", statusCode: result.Status);
        }
        catch (OperationCanceledException) { throw; }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh this plan before withdrawing.", 409); }
        catch (InvalidOperationException) { return Problem("plan-review-conflict", "Refresh the plan before withdrawing.", 409); }
        catch (Exception) { return Problem("plan-withdrawal-unconfirmed", "Check the original command key before retrying.", 503); }
    }

    private static Task Lock(ProcurementDbContext db, ProcurementRequestScope scope, Guid id, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.procurement_plans WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE", ct);
    private static string Hash(object value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json(value))));
    internal static bool TryKey(HttpContext http, out Guid key)
    {
        key = Guid.Empty;
        return http.Request.Headers["X-Idempotency-Key"].Count == 0
            && http.Request.Headers["Idempotency-Key"].Count == 1
            && Guid.TryParseExact(http.Request.Headers["Idempotency-Key"].ToString(), "D", out key)
            && key != Guid.Empty;
    }
    private static string Json(object value) => JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private static IResult Problem(string code, string message, int status) => Results.Json(new { code, message }, statusCode: status);
    private sealed record Reply(int Status, string Body);
    public sealed record SubmitInput(long ExpectedPlanVersion);
    public sealed record DecideInput(string Action, string? Reason, long ExpectedPlanVersion,
        long ExpectedCaseVersion, long ExpectedAssignmentVersion);
    public sealed record WithdrawInput(long ExpectedPlanVersion, long ExpectedCaseVersion, string? Reason);
}
