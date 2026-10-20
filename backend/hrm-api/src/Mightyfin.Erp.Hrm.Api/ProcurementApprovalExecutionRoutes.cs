using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementApprovalExecutionRoutes
{
    public const string OpenPermission = "procurement-approval-open";
    public const string ReadPermission = "procurement-approval-read";
    public const string DecidePermission = "procurement-approval-decide";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapRoutes(WebApplication app)
    {
        app.MapPost("/api/procurement/v1/requests/{id:guid}/approval-cases", Open).RequireAuthorization(OpenPermission);
        app.MapGet("/api/procurement/v1/approvals", Queue).RequireAuthorization(ReadPermission);
        app.MapGet("/api/procurement/v1/requests/approval-opening-queue", OpeningQueue).RequireAuthorization(OpenPermission);
        app.MapGet("/api/procurement/v1/approvals/{caseId:guid}", Detail).RequireAuthorization(ReadPermission);
        app.MapPost("/api/procurement/v1/approvals/{caseId:guid}/assignments/{assignmentId:guid}/decisions", Decide)
            .RequireAuthorization(DecidePermission);
        app.MapGet("/api/procurement/v1/requests/{id:guid}/approval-history", History).RequireAuthorization("procurement-request-read");
        app.MapGet("/api/procurement/v1/requests/finance-release-queue", FinanceReleaseQueue)
            .RequireAuthorization("procurement-budget-check");
        app.MapPost("/api/procurement/v1/requests/{id:guid}/finance-release-confirmations", ConfirmFinanceRelease)
            .RequireAuthorization("procurement-budget-check");
    }

    private static async Task<IResult> Open(Guid id, OpenInput input, HttpContext http,
        ProcurementRequestScope scope, ProcurementApprovalExecutionOptions options,
        ProcurementApprovalTransaction transaction, IAuthorizationService authorization, CancellationToken ct)
    {
        try
        {
            Enabled(options); var key = CommandKey(http); var fingerprint = Hash(new
                { operation = "Open", id, actor = scope.SubjectId, input.ExpectedRequestVersion });
            var reply = await transaction.ExecuteAsync(async (db, workflow, token) =>
            {
                await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, id, token);
                var request = await Request(db, scope, id, token);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                _ = await Actor(core, scope, http, authorization, OpenPermission, token);
                await RecordScope(core, scope, request, token);
                var prior = await Receipt(db, "Open", key, scope.SubjectId, fingerprint, id, null, token);
                if (prior is not null) return new Reply(201, prior.ResponseJson);
                if (request.Version != input.ExpectedRequestVersion) throw Conflict("ApprovalSourceChanged");
                var evidence = await Evidence(db, request, null, token);
                var now = DateTimeOffset.UtcNow;
                var financeMode = await ProcurementApprovalFinanceEligibility.CheckAsync(request, evidence.Finance,
                    options, core, db, scope, now, token);
                var plan = await ProcurementApprovalAssigneeResolver.ResolveAsync(request, evidence.Evaluation,
                    core, db, scope, now, token);
                var makerIdentity = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, scope.TenantId,
                    scope.LegalEntityId, request.CreatedBy, token) ?? throw Conflict("ApprovalMakerIdentityUnavailable");
                var requesterIdentity = await ProcurementAuthorityIdentity.ResolveCanonicalWorkerAccountAsync(core, scope.TenantId,
                    scope.LegalEntityId, request.RequesterWorkerId, token) ?? throw Conflict("ApprovalRequesterIdentityUnavailable");
                var approvalCase = new ApprovalCase
                {
                    TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                    SourceModule = "procurement", SourceType = "purchase_request", SourceId = request.Id,
                    SourceVersion = request.SubmittedVersion ?? 0, PolicyVersionId = evidence.Policy.Id,
                    PolicyVersionNumber = evidence.Policy.PolicyVersionNumber, MakerSubjectId = request.CreatedBy,
                    OpenedAt = now,
                };
                var pin = PurchaseRequestApprovalPinning.Pin(request, evidence.Evaluation, evidence.Policy,
                    evidence.Finance, scope.TenantId, scope.LegalEntityId, approvalCase.Id, key,
                    financeMode.Mode, financeMode.Reference, now, makerIdentity.WorkerId, requesterIdentity.WorkerId);
                var sequence = 0;
                foreach (var resolved in plan)
                {
                    var stage = new ApprovalStage
                    {
                        TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId, CaseId = approvalCase.Id,
                        Sequence = ++sequence, Code = resolved.Route.Code,
                        Mode = resolved.Route.Mode == "One" ? "Any" : "All",
                        RequiredApprovals = resolved.Route.Mode == "One" ? 1 : resolved.Assignees.Count,
                        State = sequence == 1 ? "Active" : "Waiting",
                    };
                    foreach (var candidate in resolved.Assignees)
                    {
                        var assignment = new ApprovalAssignment
                        {
                            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                            CaseId = approvalCase.Id, StageId = stage.Id, AssigneeSubjectId = candidate.PrincipalId,
                            PermissionKey = DecidePermission,
                        };
                        stage.Assignments.Add(assignment);
                        var json = JsonSerializer.Serialize(new PurchaseRequestAssignmentIdentityEvidence(candidate.WorkerId, candidate.Authority));
                        db.AddScoped(new PurchaseRequestAssignmentAuthorityEvidence
                        {
                            SubmissionId = pin.Id, CoreApprovalCaseId = approvalCase.Id, CoreStageId = stage.Id,
                            CoreAssignmentId = assignment.Id, PrincipalId = candidate.PrincipalId,
                            AssigneeWorkerId = candidate.WorkerId, AssigneeKind = resolved.Route.Assignee, GrantId = candidate.Authority.GrantId,
                            GrantRevision = candidate.Authority.GrantRevision, AuthorityVersion = candidate.Authority.AuthorityVersion,
                            EvidenceJson = json, EvidenceSha256 = ProcurementApprovalAssigneeResolver.Hash(json),
                        });
                    }
                    approvalCase.Stages.Add(stage);
                }
                workflow.AddCase(approvalCase);
                db.AddScoped(pin);
                request.ApprovalInstanceRef = approvalCase.Id.ToString("D"); request.ApprovalState = "Pending";
                db.AddScoped(Event(request, $"procurement.pr.approval_opened.{pin.Id:N}.v1", "purchase_request.approval_opened.v1",
                    new { requestId = id, caseId = approvalCase.Id, pin.SubmittedBusinessVersion }));
                Audit(db, scope, request.Id, "ApprovalCaseOpened", new { caseId = approvalCase.Id, pin.Id, pin.SubmittedBusinessVersion });
                await workflow.SaveChangesAsync(token); await db.SaveChangesAsync(token);
                var response = JsonSerializer.Serialize(Response(request, approvalCase), Json);
                AddReceipt(db, scope, "Open", key, fingerprint, request.Id, approvalCase.Id, null, response, now);
                return new Reply(201, response);
            }, ct);
            return Results.Content(reply.Json, "application/json", statusCode: reply.Status);
        }
        catch (Exception ex) when (Handled(ex)) { return Failure(ex); }
    }

    private static async Task<IResult> Decide(Guid caseId, Guid assignmentId, DecideInput input,
        HttpContext http, ProcurementRequestScope scope, ProcurementApprovalExecutionOptions options,
        ProcurementApprovalTransaction transaction, ApprovalDbContext reads,
        IAuthorizationService authorization, CancellationToken ct)
    {
        try
        {
            Enabled(options); var key = CommandKey(http);
            var reason = NormalizeReason(input.Reason);
            if (input.Action is not ("Approve" or "Reject" or "Return") || reason?.Length > 2000
                || input.Action != "Approve" && reason is null) throw Invalid("ApprovalDecisionInvalid");
            var fingerprint = Hash(new { operation = "Decide", caseId, assignmentId, actor = scope.SubjectId,
                input.Action, reason, input.ExpectedCaseVersion, input.ExpectedAssignmentVersion, input.ExpectedRequestVersion });
            var source = await reads.Cases.AsNoTracking().Where(x => x.Id == caseId && x.SourceModule == "procurement"
                && x.SourceType == "purchase_request").Select(x => (Guid?)x.SourceId).SingleOrDefaultAsync(ct);
            if (source is null) throw Missing();
            var reply = await transaction.ExecuteAsync(async (db, workflow, token) =>
            {
                await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, source.Value, token);
                var request = await Request(db, scope, source.Value, token);
                await workflow.Database.ExecuteSqlInterpolatedAsync($"""SELECT "Id" FROM core_approval.cases WHERE "TenantId"={scope.TenantId} AND "LegalEntityId"={scope.LegalEntityId} AND "Id"={caseId} FOR UPDATE""", token);
                var approvalCase = await Case(workflow, caseId, token);
                await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                _ = await Actor(core, scope, http, authorization, DecidePermission, token);
                await RecordScope(core, scope, request, token);
                var assignment = approvalCase.Stages.SelectMany(x => x.Assignments).SingleOrDefault(x => x.Id == assignmentId);
                if (assignment is null || assignment.AssigneeSubjectId != scope.SubjectId
                    || assignment.DelegatedFromSubjectId is not null || assignment.PermissionKey != DecidePermission) throw Missing();
                var pin = await db.ApprovalSubmissions.SingleOrDefaultAsync(x => x.CoreApprovalCaseId == caseId, token)
                    ?? throw Conflict("ApprovalSubmissionMissing");
                var authority = await db.AssignmentAuthorityEvidence.SingleOrDefaultAsync(x => x.CoreAssignmentId == assignmentId
                    && x.SubmissionId == pin.Id && x.CoreApprovalCaseId == caseId, token) ?? throw Conflict("ApprovalAssignmentEvidenceMissing");
                if (authority.PrincipalId != scope.SubjectId || authority.CoreStageId != assignment.StageId) throw Missing();
                var now = DateTimeOffset.UtcNow;
                await ProcurementApprovalAssigneeResolver.ValidateAssignmentAsync(request, authority, core, db, scope, now, token);
                var prior = await Receipt(db, "Decide", key, scope.SubjectId, fingerprint, request.Id, assignmentId, token);
                if (prior is not null) return new Reply(200, prior.ResponseJson);
                if (request.Version != input.ExpectedRequestVersion || approvalCase.Version != input.ExpectedCaseVersion
                    || assignment.Version != input.ExpectedAssignmentVersion) throw Conflict("ApprovalVersionsChanged");
                var evidence = input.Action == "Approve" ? await Evidence(db, request, pin, token)
                    : await OriginalEvidence(db, pin, token);
                var currentFinance = await db.BudgetCheckSnapshots.AsNoTracking().Where(x => x.PurchaseRequestId == request.Id
                    && x.RequestVersion == pin.SubmittedBusinessVersion).OrderByDescending(x => x.CheckedAt)
                    .ThenByDescending(x => x.Id).FirstOrDefaultAsync(token);
                var check = PurchaseRequestApprovalPinning.Verify(pin, request, evidence.Evaluation,
                    evidence.Policy, evidence.Finance, scope.TenantId, scope.LegalEntityId);
                if (!check.IsValid) throw Conflict("ApprovalPinnedEvidenceChanged");
                if (input.Action == "Approve")
                {
                    var financeMode = await ProcurementApprovalFinanceEligibility.CheckAsync(request, evidence.Finance, options, core, db, scope, now, token);
                    if (financeMode.Mode != pin.FinanceEvidenceMode || financeMode.Reference != pin.ManualEvidenceAuthorityRef)
                        throw Conflict("ApprovalFinanceAuthorityChanged");
                }
                var allPins = await db.AssignmentAuthorityEvidence.Where(x => x.SubmissionId == pin.Id).ToListAsync(token);
                var allAssignments = approvalCase.Stages.SelectMany(x => x.Assignments).ToArray();
                if (allPins.Count != allAssignments.Length || allAssignments.Any(x => !allPins.Any(p => p.CoreAssignmentId == x.Id
                    && p.PrincipalId == x.AssigneeSubjectId && p.CoreStageId == x.StageId))) throw Conflict("ApprovalAssignmentSetChanged");
                if (input.Action == "Approve")
                    foreach (var assignedPin in allPins)
                        await ProcurementApprovalAssigneeResolver.ValidateAssignmentAsync(request, assignedPin, core, db, scope, now, token);
                var decisions = await workflow.Decisions.Where(x => x.CaseId == caseId).ToListAsync(token);
                var result = ApprovalEngine.Decide(approvalCase, assignment,
                    new(scope.SubjectId, scope.TenantId, scope.LegalEntityId, new HashSet<string>([DecidePermission])),
                    input.Action, key.ToString("D"), reason, decisions, now);
                if (!result.Recorded) throw Conflict("ApprovalCommandReceiptMissing");
                var decision = decisions[^1]; workflow.Decisions.Add(decision);
                if (approvalCase.State is "Approved" or "Rejected" or "Returned")
                {
                    var effects = PurchaseRequestApprovalOutcomes.Apply(request, pin, approvalCase, decision,
                        evidence.Evaluation, evidence.Policy, evidence.Finance, scope.TenantId, scope.LegalEntityId, now);
                    db.AddScoped(effects.Outcome);
                    if (effects.ReleaseRequest is not null) db.AddScoped(effects.ReleaseRequest);
                    foreach (var message in effects.Events) db.AddScoped(message);
                }
                db.AddScoped(Event(request, $"procurement.pr.decision.{decision.Id:N}.v1", "purchase_request.approval_decision_recorded.v1",
                    new { requestId = request.Id, caseId, assignmentId, decisionId = decision.Id, action = input.Action }));
                Audit(db, scope, request.Id, "ApprovalDecisionRecorded", new
                    { caseId, assignmentId, decisionId = decision.Id, input.Action, reason, caseState = approvalCase.State,
                        currentFinanceSnapshotId = currentFinance?.Id, currentFinanceStatus = currentFinance?.Status,
                        currentReservationStatus = currentFinance?.ReservationStatus,
                        pinnedFinanceSnapshotId = pin.FinanceSnapshotId });
                await workflow.SaveChangesAsync(token); await db.SaveChangesAsync(token);
                var response = JsonSerializer.Serialize(new
                {
                    caseId = approvalCase.Id, requestId = request.Id, submittedVersion = request.SubmittedVersion,
                    requestVersion = request.Version, caseVersion = approvalCase.Version, state = approvalCase.State,
                    requestState = request.Status, approvalState = request.ApprovalState,
                    assignmentId = assignment.Id, assignmentVersion = assignment.Version, decisionId = decision.Id,
                }, Json);
                AddReceipt(db, scope, "Decide", key, fingerprint, request.Id, caseId, assignmentId, response, now);
                return new Reply(200, response);
            }, ct);
            return Results.Content(reply.Json, "application/json", statusCode: reply.Status);
        }
        catch (Exception ex) when (Handled(ex)) { return Failure(ex); }
    }

    public sealed record OpenInput(long ExpectedRequestVersion);
    public sealed record DecideInput(string Action, string? Reason, long ExpectedCaseVersion,
        long ExpectedAssignmentVersion, long ExpectedRequestVersion);
    private sealed record Reply(int Status, string Json);
}
