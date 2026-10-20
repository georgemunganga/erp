using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementApprovalExecutionRoutes
{
    private static async Task<IResult> OpeningQueue(int? limit, int? offset, HttpContext http,
        ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope,
        ProcurementApprovalExecutionOptions options, IAuthorizationService authorization, CancellationToken ct)
    {
        try
        {
            Enabled(options); _ = await Actor(core, scope, http, authorization, OpenPermission, ct);
            var branches = await core.UserBranchAssignments.AsNoTracking().Where(x => x.UserId == Guid.Parse(scope.SubjectId)
                && !x.IsArchived).Select(x => x.LocationId).ToArrayAsync(ct);
            var query = ScopedRequests(db, scope).AsNoTracking().Where(x => x.Status == "Submitted" && x.ApprovalInstanceRef == null
                && db.PolicyEvaluations.Any(p => p.PurchaseRequestId == x.Id && p.RequestVersion == x.SubmittedVersion && p.Outcome == "Matched")
                && db.BudgetCheckSnapshots.Where(f => f.PurchaseRequestId == x.Id && f.RequestVersion == x.SubmittedVersion)
                    .OrderByDescending(f => f.CheckedAt).ThenByDescending(f => f.Id).Take(1)
                    .Any(f => f.Status == "Available" && f.ReservationStatus == "Confirmed"));
            if (branches.Length != 0) query = query.Where(x => x.BranchRef.HasValue && branches.Contains(x.BranchRef.Value));
            if (scope.IsConfined && branches.Length == 0) query = query.Where(_ => false);
            var size = Math.Clamp(limit ?? 50, 1, 100); var skip = Math.Clamp(offset ?? 0, 0, 100000);
            var count = await query.CountAsync(ct);
            var records = await query.OrderBy(x => x.SubmittedAt).ThenBy(x => x.Id).Skip(skip).Take(size).Select(x => new
            {
                requestId = x.Id, requestNumber = x.Number, purpose = x.Purpose, currencyCode = x.CurrencyCode,
                amount = x.Lines.Where(l => l.ApprovalStatus != "Cancelled").Sum(l => l.RequestedQuantity * l.EstimatedUnitPrice),
                requestVersion = x.Version, submittedAt = x.SubmittedAt,
            }).ToArrayAsync(ct);
            return Results.Ok(new { records, count, limit = size, offset = skip });
        }
        catch (Exception ex) when (Handled(ex)) { return Failure(ex); }
    }

    private static async Task<IResult> Queue(string? state, int? limit, int? offset, HttpContext http,
        ProcurementDbContext db, ApprovalDbContext workflow, HrmDbContext core, ProcurementRequestScope scope,
        ProcurementApprovalExecutionOptions options, IAuthorizationService authorization, CancellationToken ct)
    {
        try
        {
            Enabled(options); _ = await Actor(core, scope, http, authorization, ReadPermission, ct);
            if (state is not (null or "Pending")) throw Invalid("ApprovalQueueStateUnsupported");
            var size = Math.Clamp(limit ?? 50, 1, 100); var skip = Math.Clamp(offset ?? 0, 0, 100000);
            var branches = await core.UserBranchAssignments.AsNoTracking().Where(x => x.UserId == Guid.Parse(scope.SubjectId)
                && !x.IsArchived).Select(x => x.LocationId).ToArrayAsync(ct);
            var query = workflow.Database.SqlQuery<ApprovalQueueRow>($"""
                SELECT c."Id" AS "CaseId",c."Version" AS "CaseVersion",c."State" AS "State",c."OpenedAt" AS "OpenedAt",
                       r.id AS "RequestId",r.number AS "RequestNumber",r.purpose AS "Purpose",r.version AS "RequestVersion",
                       r.branch_ref AS "BranchRef",r.department_ref AS "DepartmentRef",
                       p.currency_code AS "CurrencyCode",p.estimated_amount AS "Amount",p.amount_basis AS "AmountBasis"
                FROM core_approval.cases c
                JOIN procurement.purchase_requests r ON r.id=c."SourceId" AND r.tenant_id=c."TenantId" AND r.legal_entity_id=c."LegalEntityId"
                JOIN procurement.purchase_request_approval_submissions p ON p.core_approval_case_id=c."Id" AND p.tenant_id=r.tenant_id AND p.legal_entity_id=r.legal_entity_id
                WHERE c."TenantId"={scope.TenantId} AND c."LegalEntityId"={scope.LegalEntityId}
                  AND c."SourceModule"='procurement' AND c."SourceType"='purchase_request' AND c."State"='Pending'
                  AND r.status='Submitted' AND r.approval_instance_ref=c."Id"::text AND r.submitted_version=c."SourceVersion"
                  AND EXISTS (SELECT 1 FROM core_approval.stages s JOIN core_approval.assignments a
                    ON a."StageId"=s."Id" AND a."CaseId"=c."Id" AND a."TenantId"=c."TenantId" AND a."LegalEntityId"=c."LegalEntityId"
                    WHERE s."CaseId"=c."Id" AND s."TenantId"=c."TenantId" AND s."LegalEntityId"=c."LegalEntityId"
                      AND s."State"='Active' AND a."AssigneeSubjectId"={scope.SubjectId} AND a."State"='Pending' AND a."DelegatedFromSubjectId" IS NULL)
                """);
            if (scope.WorkLocationId is { } branch) query = query.Where(x => x.BranchRef == branch);
            if (scope.OrgUnitId is { } department) query = query.Where(x => x.DepartmentRef == department);
            if (branches.Length != 0) query = query.Where(x => x.BranchRef.HasValue && branches.Contains(x.BranchRef.Value));
            if (scope.IsConfined && branches.Length == 0) query = query.Where(_ => false);
            var count = await query.CountAsync(ct);
            var page = await query.OrderBy(x => x.OpenedAt).ThenBy(x => x.CaseId).Skip(skip).Take(size).ToArrayAsync(ct);
            var ids = page.Select(x => x.CaseId).ToArray();
            var stages = await workflow.Stages.AsNoTracking().Include(x => x.Assignments)
                .Where(x => ids.Contains(x.CaseId) && x.State == "Active").ToListAsync(ct);
            var records = page.Select(x =>
            {
                var stage = stages.Single(s => s.CaseId == x.CaseId);
                return new { x.CaseId, x.RequestId, x.RequestNumber, x.Purpose, x.CurrencyCode, x.Amount, x.AmountBasis,
                    x.State, x.RequestVersion, x.CaseVersion, x.OpenedAt,
                    stage = new { stage.Id, stage.Code, stage.State },
                    assignments = stage.Assignments.Where(a => a.AssigneeSubjectId == scope.SubjectId && a.State == "Pending")
                        .Select(a => new { a.Id, a.Version, a.State }).ToArray() };
            }).ToArray();
            return Results.Ok(new { records, count, limit = size, offset = skip });
        }
        catch (Exception ex) when (Handled(ex)) { return Failure(ex); }
    }

    private static async Task<IResult> Detail(Guid caseId, HttpContext http, ProcurementDbContext db,
        ApprovalDbContext workflow, HrmDbContext core, ProcurementRequestScope scope,
        ProcurementApprovalExecutionOptions options, IAuthorizationService authorization, CancellationToken ct)
    {
        try
        {
            Enabled(options); _ = await Actor(core, scope, http, authorization, ReadPermission, ct);
            var approvalCase = await Case(workflow, caseId, ct);
            if (!approvalCase.Stages.SelectMany(x => x.Assignments).Any(x => x.AssigneeSubjectId == scope.SubjectId)) throw Missing();
            var request = await Request(db, scope, approvalCase.SourceId, ct);
            await RecordScope(core, scope, request, ct);
            var pin = await db.ApprovalSubmissions.AsNoTracking().SingleOrDefaultAsync(x => x.CoreApprovalCaseId == caseId, ct)
                ?? throw Conflict("ApprovalSubmissionMissing");
            var decisions = await workflow.Decisions.AsNoTracking().Where(x => x.CaseId == caseId).OrderBy(x => x.DecidedAt).ToListAsync(ct);
            var blockers = new List<string>();
            var allowed = new List<string>();
            if (approvalCase.State == "Pending")
            {
                try
                {
                    _ = await Actor(core, scope, http, authorization, DecidePermission, ct);
                    var assignment = approvalCase.Stages.Where(x => x.State == "Active").SelectMany(x => x.Assignments)
                        .SingleOrDefault(x => x.AssigneeSubjectId == scope.SubjectId && x.State == "Pending" && x.DelegatedFromSubjectId == null);
                    if (assignment is not null)
                    {
                        var authority = await db.AssignmentAuthorityEvidence.AsNoTracking()
                            .SingleOrDefaultAsync(x => x.CoreAssignmentId == assignment.Id && x.SubmissionId == pin.Id, ct)
                            ?? throw Conflict("ApprovalAssignmentEvidenceMissing");
                        await ProcurementApprovalAssigneeResolver.ValidateAssignmentAsync(request, authority, core, db, scope, DateTimeOffset.UtcNow, ct);
                        var evidence = await OriginalEvidence(db, pin, ct);
                        var validation = PurchaseRequestApprovalPinning.Verify(pin, request, evidence.Evaluation,
                            evidence.Policy, evidence.Finance, scope.TenantId, scope.LegalEntityId);
                        if (!validation.IsValid) throw Conflict("ApprovalPinnedEvidenceChanged");
                        allowed.AddRange(["Reject", "Return"]);
                        var current = await Evidence(db, request, pin, ct);
                        var mode = await ProcurementApprovalFinanceEligibility.CheckAsync(request, current.Finance, options, core, scope, DateTimeOffset.UtcNow, ct);
                        if (mode.Mode != pin.FinanceEvidenceMode || mode.Reference != pin.ManualEvidenceAuthorityRef)
                            throw Conflict("ApprovalFinanceAuthorityChanged");
                        var assignmentPins = await db.AssignmentAuthorityEvidence.AsNoTracking()
                            .Where(x => x.SubmissionId == pin.Id).ToListAsync(ct);
                        var caseAssignments = approvalCase.Stages.SelectMany(x => x.Assignments).ToArray();
                        if (assignmentPins.Count != caseAssignments.Length || caseAssignments.Any(x =>
                            !assignmentPins.Any(p => p.CoreAssignmentId == x.Id && p.PrincipalId == x.AssigneeSubjectId
                                && p.CoreStageId == x.StageId))) throw Conflict("ApprovalAssignmentSetChanged");
                        foreach (var assignmentPin in assignmentPins)
                            await ProcurementApprovalAssigneeResolver.ValidateAssignmentAsync(request, assignmentPin,
                                core, db, scope, DateTimeOffset.UtcNow, ct);
                        allowed.Insert(0, "Approve");
                    }
                }
                catch (ApprovalHttpError error) { blockers.Add(error.Code); }
                catch (InvalidOperationException error) { blockers.Add(SafeCode(error)); }
            }
            return Results.Ok(new
            {
                caseId, requestId = request.Id, requestNumber = request.Number, purpose = request.Purpose,
                currencyCode = pin.CurrencyCode, amount = pin.EstimatedAmount, amountBasis = pin.AmountBasis,
                state = approvalCase.State, requestState = request.Status, requestVersion = request.Version,
                caseVersion = approvalCase.Version, submittedVersion = pin.SubmittedBusinessVersion,
                openedAt = approvalCase.OpenedAt, completedAt = approvalCase.CompletedAt,
                requestSnapshot = JsonSerializer.Deserialize<JsonElement>(pin.RequestSnapshotJson),
                stages = approvalCase.Stages.OrderBy(x => x.Sequence).Select(s => new
                {
                    s.Id, s.Sequence, s.Code, s.State, s.Mode, s.RequiredApprovals,
                    assignments = s.Assignments.Select(a => new
                    {
                        a.Id, principalId = a.AssigneeSubjectId, a.State, a.Version,
                        isMine = a.AssigneeSubjectId == scope.SubjectId,
                        allowedActions = s.State == "Active" && a.State == "Pending"
                            && a.AssigneeSubjectId == scope.SubjectId ? allowed.ToArray() : [],
                    }).ToArray(),
                }).ToArray(),
                decisions = decisions.Select(DecisionView).ToArray(), blockers,
            });
        }
        catch (Exception ex) when (Handled(ex)) { return Failure(ex); }
    }

    private static async Task<IResult> History(Guid id, HttpContext http, ProcurementDbContext db,
        ApprovalDbContext workflow, HrmDbContext core, ProcurementRequestScope scope,
        ProcurementApprovalExecutionOptions options, IAuthorizationService authorization, CancellationToken ct)
    {
        try
        {
            Enabled(options);
            _ = await Actor(core, scope, http, authorization, "procurement-request-read", ct);
            var request = await Request(db, scope, id, ct); await RecordScope(core, scope, request, ct);
            if (request.CreatedBy != scope.SubjectId
                && !(await authorization.AuthorizeAsync(http.User, OpenPermission)).Succeeded) throw Missing();
            if (request.CreatedBy != scope.SubjectId) _ = await Actor(core, scope, http, authorization, OpenPermission, ct);
            var pins = await db.ApprovalSubmissions.AsNoTracking().Where(x => x.PurchaseRequestId == id).OrderBy(x => x.SubmittedBusinessVersion).ToListAsync(ct);
            var ids = pins.Select(x => x.CoreApprovalCaseId).ToArray();
            var cases = await workflow.Cases.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            var decisions = await workflow.Decisions.AsNoTracking().Where(x => ids.Contains(x.CaseId)).OrderBy(x => x.DecidedAt).ToListAsync(ct);
            var releaseRequests = await db.FinanceReleaseRequests.AsNoTracking().Where(x => x.PurchaseRequestId == id).ToListAsync(ct);
            var releaseIds = releaseRequests.Select(x => x.Id).ToArray();
            var confirmations = await db.FinanceReleaseConfirmations.AsNoTracking().Where(x => releaseIds.Contains(x.ReleaseRequestId)).ToListAsync(ct);
            return Results.Ok(new { requestId = id, records = pins.Where(x => cases.ContainsKey(x.CoreApprovalCaseId)).Select(p =>
            {
                var c = cases[p.CoreApprovalCaseId];
                var release = releaseRequests.SingleOrDefault(x => x.SubmissionId == p.Id);
                var confirmation = release is null ? null : confirmations.SingleOrDefault(x => x.ReleaseRequestId == release.Id);
                return new { caseId = c.Id, submittedVersion = p.SubmittedBusinessVersion, state = c.State,
                    openedAt = c.OpenedAt, completedAt = c.CompletedAt,
                    financeRelease = release is null ? null : new { releaseRequestId = release.Id,
                        state = confirmation is null ? "Pending" : "Confirmed", confirmedAt = confirmation?.FinanceConfirmedAt },
                    decisions = decisions.Where(d => d.CaseId == c.Id).Select(DecisionView).ToArray() };
            }).ToArray() });
        }
        catch (Exception ex) when (Handled(ex)) { return Failure(ex); }
    }

    private static object DecisionView(ApprovalDecision d) => new
        { d.Id, d.AssignmentId, d.ActorSubjectId, d.Action, d.Reason, d.DecidedAt };
    private static object Response(PurchaseRequest r, ApprovalCase c) => new
    {
        caseId = c.Id, requestId = r.Id, submittedVersion = r.SubmittedVersion, requestVersion = r.Version,
        caseVersion = c.Version, state = c.State, requestState = r.Status, approvalState = r.ApprovalState,
    };
    private static IQueryable<PurchaseRequest> ScopedRequests(ProcurementDbContext db, ProcurementRequestScope scope)
    {
        var query = db.PurchaseRequests.AsQueryable();
        if (scope.WorkLocationId is { } branch) query = query.Where(x => x.BranchRef == branch);
        if (scope.OrgUnitId is { } department) query = query.Where(x => x.DepartmentRef == department);
        if (scope.IsConfined && scope.WorkLocationId is null) query = query.Where(_ => false);
        return query;
    }
    private static async Task<PurchaseRequest> Request(ProcurementDbContext db, ProcurementRequestScope scope, Guid id, CancellationToken ct)
        => await ScopedRequests(db, scope).Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing();
    private static async Task<ApprovalCase> Case(ApprovalDbContext db, Guid id, CancellationToken ct)
        => await db.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
            .SingleOrDefaultAsync(x => x.Id == id && x.SourceModule == "procurement" && x.SourceType == "purchase_request", ct) ?? throw Missing();
    private static async Task<SpendingAuthorityConfigurationActor> Actor(HrmDbContext core, ProcurementRequestScope scope,
        HttpContext http, IAuthorizationService auth, string permission, CancellationToken ct)
        => await ProcurementAuthorityIdentity.ActorAsync(core, scope, http.User, auth, permission, ct)
            ?? throw new ApprovalHttpError(403, "ApprovalPermissionUnavailable", "Your current company account does not have this permission.");
    private static async Task RecordScope(HrmDbContext core, ProcurementRequestScope scope, PurchaseRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(scope.SubjectId, out var id)) throw Missing();
        var branches = await core.UserBranchAssignments.AsNoTracking().Where(x => x.UserId == id && !x.IsArchived).Select(x => x.LocationId).ToArrayAsync(ct);
        if (branches.Length != 0 && (request.BranchRef is null || !branches.Contains(request.BranchRef.Value))) throw Missing();
    }
    private sealed record EvidenceSet(PolicyEvaluation Evaluation, PolicyVersion Policy, BudgetCheckSnapshot Finance);
    private static async Task<EvidenceSet> OriginalEvidence(ProcurementDbContext db,
        PurchaseRequestApprovalSubmission pin, CancellationToken ct)
        => new(await db.PolicyEvaluations.SingleAsync(x => x.Id == pin.PolicyEvaluationId, ct),
            await db.PolicyVersions.SingleAsync(x => x.Id == pin.PolicyVersionId, ct),
            await db.BudgetCheckSnapshots.SingleAsync(x => x.Id == pin.FinanceSnapshotId, ct));
    private static async Task<EvidenceSet> Evidence(ProcurementDbContext db, PurchaseRequest request,
        PurchaseRequestApprovalSubmission? pin, CancellationToken ct)
    {
        var version = pin?.SubmittedBusinessVersion ?? request.SubmittedVersion;
        var evaluation = await db.PolicyEvaluations.SingleOrDefaultAsync(x => x.PurchaseRequestId == request.Id && x.RequestVersion == version, ct)
            ?? throw Conflict("ApprovalPolicyEvaluationMissing");
        var policy = await db.PolicyVersions.SingleOrDefaultAsync(x => x.Id == evaluation.PolicyVersionId, ct)
            ?? throw Conflict("ApprovalPolicyMissing");
        var finance = await db.BudgetCheckSnapshots.Where(x => x.PurchaseRequestId == request.Id && x.RequestVersion == version)
            .OrderByDescending(x => x.CheckedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct)
            ?? throw Conflict("ApprovalFinanceEvidenceMissing");
        if (pin is not null && finance.Id != pin.FinanceSnapshotId) throw Conflict("ApprovalFinanceEvidenceChanged");
        return new(evaluation, policy, finance);
    }
    private static async Task<ProcurementApprovalCommandReceipt?> Receipt(ProcurementDbContext db, string operation,
        Guid key, string actor, string fingerprint, Guid requestId, Guid? assignmentId, CancellationToken ct)
    {
        var receipt = await db.ApprovalCommandReceipts.SingleOrDefaultAsync(x => x.Operation == operation && x.CommandKey == key, ct);
        if (receipt is not null && (receipt.ActorSubjectId != actor || receipt.CommandFingerprint != fingerprint
            || receipt.PurchaseRequestId != requestId || receipt.CoreAssignmentId != assignmentId)) throw Conflict("ApprovalCommandKeyConflict");
        return receipt;
    }
    private static void AddReceipt(ProcurementDbContext db, ProcurementRequestScope scope, string operation,
        Guid key, string fingerprint, Guid requestId, Guid caseId, Guid? assignmentId, string response, DateTimeOffset now)
        => db.AddScoped(new ProcurementApprovalCommandReceipt
        {
            Operation = operation, CommandKey = key, ActorSubjectId = scope.SubjectId, PurchaseRequestId = requestId,
            CoreApprovalCaseId = caseId, CoreAssignmentId = assignmentId, CommandFingerprint = fingerprint,
            ResponseJson = response, CompletedAt = now,
        });
    private static void Audit(ProcurementDbContext db, ProcurementRequestScope scope, Guid id, string action, object evidence)
        => db.AddScoped(new AuditEvent { RecordType = "PurchaseRequest", RecordId = id, Action = action,
            ActorSubjectId = scope.SubjectId, ChangedFieldsJson = JsonSerializer.Serialize(evidence, Json) });
    private static OutboxEvent Event(PurchaseRequest request, string key, string type, object payload) => new()
    {
        TenantId = request.TenantId, LegalEntityId = request.LegalEntityId, AggregateType = "purchase_request",
        AggregateId = request.Id, EventType = type, IdempotencyKey = key, PayloadJson = JsonSerializer.Serialize(payload, Json),
    };
    private static Guid CommandKey(HttpContext http)
        => http.Request.Headers["Idempotency-Key"].Count == 1
            && Guid.TryParse(http.Request.Headers["Idempotency-Key"], out var key) && key != Guid.Empty
                ? key : throw Invalid("ApprovalIdempotencyKeyRequired");
    private static string? NormalizeReason(string? value)
    { var reason = string.Join(" ", (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)); return reason.Length == 0 ? null : reason; }
    private static string Hash(object payload) => ProcurementApprovalAssigneeResolver.Hash(JsonSerializer.Serialize(payload, Json));
    private static void Enabled(ProcurementApprovalExecutionOptions options)
    {
        if (!options.Enabled) throw new ApprovalHttpError(503, "ApprovalsNotEnabled", "Purchase approvals are not enabled yet. Your draft requests remain available.");
        if (!options.ExecutionDatabaseConfigured) throw new ApprovalHttpError(503, "ApprovalExecutionDatabaseNotConfigured",
            "Purchase approvals need their database access configured before they can start.");
    }
    private static ApprovalHttpError Missing() => new(404, "ApprovalNotFound", "Approval or request not found.");
    private static ApprovalHttpError Conflict(string code) => new(409, code, "The request, funding or approval changed. Refresh it and resolve the listed check before trying again.");
    private static ApprovalHttpError Invalid(string code) => new(422, code, "Check the approval details and try again.");
    private static string SafeCode(InvalidOperationException error) => error.Message.Length <= 100
        && error.Message.All(x => char.IsAsciiLetterOrDigit(x)) ? error.Message : "ApprovalPrerequisiteUnavailable";
    private static bool Handled(Exception ex) => ex is ApprovalHttpError or InvalidOperationException or ArgumentException
        or UnauthorizedAccessException or DbUpdateException or NpgsqlException;
    private static IResult Failure(Exception error)
    {
        if (error is ApprovalHttpError known) return Results.Json(new ApiError(known.Code, known.Message, []), statusCode: known.Status);
        var pg = ProcurementPostgresFailures.Postgres(error);
        if (pg?.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn
            || error is NpgsqlException && pg is null)
            return Results.Json(new ApiError("ApprovalServiceUnavailable", "Purchase approvals are temporarily unavailable. Try again shortly.", []), statusCode: 503);
        if (pg?.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.UniqueViolation)
            return Results.Json(new ApiError("ApprovalConcurrentChange", "The request or approval changed. Refresh before trying again.", []), statusCode: 409);
        if (error is UnauthorizedAccessException) return Results.Json(new ApiError("ApprovalAccessDenied", "You cannot act on this approval.", []), statusCode: 403);
        if (error is ArgumentException) return Results.Json(new ApiError("ApprovalInputOrEvidenceInvalid", "Check the request and its approval evidence.", []), statusCode: 409);
        return Results.Json(new ApiError(error is InvalidOperationException operation ? SafeCode(operation) : "ApprovalConcurrentChange",
            "The request, funding or approval changed. Refresh before trying again.", []), statusCode: 409);
    }
    public sealed class ApprovalQueueRow
    {
        public Guid CaseId { get; set; }
        public long CaseVersion { get; set; }
        public string State { get; set; } = "";
        public DateTimeOffset OpenedAt { get; set; }
        public Guid RequestId { get; set; }
        public string RequestNumber { get; set; } = "";
        public string Purpose { get; set; } = "";
        public long RequestVersion { get; set; }
        public Guid? BranchRef { get; set; }
        public Guid? DepartmentRef { get; set; }
        public string CurrencyCode { get; set; } = "";
        public decimal Amount { get; set; }
        public string AmountBasis { get; set; } = "";
    }
    private sealed class ApprovalHttpError(int status, string code, string message) : Exception(message)
    { public int Status { get; } = status; public string Code { get; } = code; }
}
