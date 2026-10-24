using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Held contract review commands. MapRoutes is deliberately not called
/// until P35 schema, finite grants and actual Core transaction proof are captured.</summary>
public static partial class ProcurementContractReviewRoutes
{
    private sealed class Problem(string code, int status) : Exception(code) { internal int Status => status; }
    private sealed record Reply(string Body, int Status);
    public sealed record SubmitInput(long ExpectedContractVersion, Guid ReviewBranchId);
    public sealed record DecisionInput(long ExpectedContractVersion, long ExpectedCaseVersion,
        long ExpectedAssignmentVersion, string Action, string? Reason);
    public sealed record CorrectionInput(long ExpectedContractVersion, string Reason);

    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/contracts");
        group.MapPost("/{id:guid}/internal-review/submit", Submit)
            .RequireAuthorization("procurement-contract-submit");
        group.MapPost("/{id:guid}/internal-review/assignments/{assignmentId:guid}/decide", Decide)
            .RequireAuthorization("procurement-contract-review");
        group.MapPost("/{id:guid}/internal-review/correct", Correct)
            .RequireAuthorization("procurement-contract-submit");
        group.MapGet("/internal-review/assigned", Assigned)
            .RequireAuthorization("procurement-contract-review-read");
        group.MapGet("/{id:guid}/internal-review/assignments/{assignmentId:guid}", ReviewDetail)
            .RequireAuthorization("procurement-contract-review-read");
        group.MapGet("/internal-review/commands/{key:guid}", Recover)
            .RequireAuthorization("procurement-access");
    }

    private static async Task<IResult> Run(HttpContext http, ProcurementRequestScope scope,
        Func<ProcurementDbContext, ApprovalDbContext, HrmDbContext, CancellationToken, Task<Reply>> work,
        CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        try
        {
            var option = http.RequestServices.GetService<ProcurementContractReviewOptions>();
            var readiness = http.RequestServices.GetService<IProcurementContractReviewSchemaReadiness>();
            var approvals = http.RequestServices.GetService<ProcurementApprovalExecutionOptions>();
            if (option is not { Enabled: true, ExecutionDatabaseConfigured: true } || readiness is null
                || approvals is not { Enabled: true, PrincipalExecutionEnabled: true, ExecutionDatabaseConfigured: true }
                || scope.OrgUnitId is not null)
                throw new Problem("ContractReviewUnavailable", 503);
            var result = await http.RequestServices.GetRequiredService<ProcurementApprovalTransaction>()
                .ExecuteAsync(async (db, cases, token) =>
                {
                    if (!(HttpMethods.IsGet(http.Request.Method)
                        ? await readiness.IsReadReadyAsync(db, token)
                        : await readiness.CanRecordAsync(db, token)))
                        throw new Problem("ContractReviewUnavailable", 503);
                    await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                    return await work(db, cases, core, token);
                }, ct);
            return Results.Content(result.Body, "application/json", statusCode: result.Status);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is Problem or InvalidOperationException or UnauthorizedAccessException
            or DbUpdateException or JsonException || ProcurementPostgresFailures.IsStoreFailure(error))
        {
            var status = error is Problem p ? p.Status : error is UnauthorizedAccessException ? 403
                : ProcurementPostgresFailures.IsConcurrency(error) ? 409
                : ProcurementPostgresFailures.IsStoreFailure(error) ? 503 : 409;
            return Results.Json(new { code = error is Problem ? error.Message
                    : status == 503 ? "ContractReviewSaveUnconfirmed" : "ContractReviewSourceChanged",
                message = status == 503
                    ? "The review command could not be confirmed. Keep its key and inspect the original result."
                    : "Refresh the contract and check its terms, company and current review access." }, statusCode: status);
        }
    }

    private static Guid Key(HttpContext http) => Guid.TryParse(http.Request.Headers["Idempotency-Key"], out var key)
        && key != Guid.Empty ? key : throw new Problem("ContractReviewCommandKeyRequired", 400);

    private static async Task<ProcurementPrincipalPeople.Person> Actor(HrmDbContext core,
        ProcurementRequestScope scope, string action, Guid branchId, CancellationToken ct)
    {
        if (branchId == Guid.Empty || scope.PrincipalId?.ToString("D") != scope.SubjectId
            || scope.OrgUnitId is not null || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches
            || scope.WorkLocationId is { } selected && selected != branchId)
            throw new Problem("ContractReviewScopeDenied", 403);
        var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, scope.SubjectId, ct);
        if (person is null || person.CoreLinkedWorkerId != scope.CoreLinkedWorkerId
            || !person.Permissions.Contains(action)
            || !person.OrganisationWide && !person.AllowedBranches.Contains(branchId)
            || !await core.WorkLocations.AsNoTracking().AnyAsync(x => x.TenantId == scope.TenantId
                && x.LegalEntityId == scope.LegalEntityId && x.Id == branchId && !x.IsArchived, ct))
            throw new Problem("ContractReviewScopeDenied", 403);
        return person;
    }

    private static async Task<Reply?> Replay(ProcurementDbContext db, HrmDbContext core,
        ProcurementRequestScope scope, string operation, Guid key, string fingerprint,
        Guid? targetId, CancellationToken ct)
    {
        var rows = await db.ContractReviewCommandReceipts.AsNoTracking()
            .Where(x => x.ActorPrincipalId == scope.SubjectId && x.CommandKey == key)
            .Take(2).ToArrayAsync(ct);
        if (rows.Length == 0) return null;
        if (rows.Length != 1) throw new Problem("ContractReviewReceiptAmbiguous", 409);
        var receipt = rows[0];
        _ = await Actor(core, scope, receipt.Operation is "Submit" or "Correction"
            ? "procurement-contract-submit" : "procurement-contract-review", receipt.ReviewBranchId, ct);
        ContractReviewReceiptProof.Verify(receipt);
        if (receipt.Operation != operation || receipt.CommandSha256 != fingerprint
            || targetId is { } requested && receipt.ContractId != requested)
            throw new Problem("ContractReviewCommandKeyConflict", 409);
        return new(receipt.ResponseJson, receipt.ResponseStatus);
    }

    private static Task<IResult> Submit(Guid id, SubmitInput input, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        return Run(http, scope, async (db, cases, core, token) =>
        {
            var key = Key(http);
            if (input is null || input.ExpectedContractVersion < 1 || input.ReviewBranchId == Guid.Empty)
                throw new Problem("ContractReviewSubmitInputInvalid", 422);
            var fingerprint = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
            { schemaVersion = 1, operation = "Submit", id, actor = scope.SubjectId, input }));
            if (await Replay(db, core, scope, "Submit", key, fingerprint, id, token) is { } prior)
                return prior;
            var maker = await Actor(core, scope, "procurement-contract-submit", input.ReviewBranchId, token);
            var contract = await db.SupplierContracts.SingleOrDefaultAsync(x => x.Id == id
                && x.OwnerSubjectId == scope.SubjectId, token)
                ?? throw new Problem("ContractReviewContractNotFound", 404);
            if (contract.Status != "Draft" || contract.Version != input.ExpectedContractVersion)
                throw new Problem("ContractReviewDraftChanged", 409);
            var version = await db.SupplierContractVersions.SingleOrDefaultAsync(x => x.SupplierContractId == id
                && x.RevisionNumber == 1, token) ?? throw new Problem("ContractReviewVersionMissing", 409);
            var rates = await db.ContractRates.AsNoTracking().Where(x => x.SupplierContractVersionId == version.Id)
                .ToArrayAsync(token);
            if (!await db.Suppliers.AsNoTracking().AnyAsync(x => x.Id == contract.SupplierId
                && (x.Status == "Active" || x.Status == "Approved") && x.IsOrderable, token))
                throw new Problem("ContractReviewSupplierUnavailable", 409);
            var now = DateTimeOffset.UtcNow;
            var policies = await db.PolicyVersions.AsNoTracking().Where(x => x.PolicyKey == ProcurementContractReviewPolicyRules.PolicyKey
                && x.State == "Published" && x.EffectiveFrom <= now && (x.EffectiveTo == null || x.EffectiveTo > now))
                .Take(2).ToArrayAsync(token);
            if (policies.Length != 1) throw new Problem("ContractReviewPublishedPolicyRequired", 409);
            var policy = policies[0];
            await ProcurementPolicyDraftRoutes.ContractPolicyPublished(db, scope, policy, token);
            var rules = ProcurementContractReviewPolicyRules.Parse(policy.RulesJson);
            var named = new List<ContractReviewCurrentReviewer>();
            foreach (var principal in rules.Stages.SelectMany(x => x.PrincipalIds))
            {
                var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                    scope.LegalEntityId, principal, token);
                if (person is null) throw new Problem("ContractReviewReviewerUnavailable", 409);
                named.Add(new(person.PrincipalId, person.CoreLinkedWorkerId, person.OrganisationWide,
                    person.AllowedBranches, person.Permissions));
            }
            var count = await db.ContractReviewSubmissions.CountAsync(x => x.ContractId == id, token);
            var opened = ContractReviewCoreWorkflow.Open(contract, version, rates, policy,
                input.ReviewBranchId, maker.PrincipalId, maker.CoreLinkedWorkerId, named, checked(count + 1), now);
            contract.Status = "PendingReview"; version.Status = "PendingReview";
            version.ApprovalInstanceRef = opened.Case.Id.ToString("D");
            cases.AddCase(opened.Case);
            db.AddScoped(opened.Submission);
            var at = opened.Submission.SubmittedAt;
            var receipt = new ContractReviewCommandReceipt
            {
                TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                ContractId = id, SubmissionId = opened.Submission.Id, ReviewBranchId = input.ReviewBranchId,
                Operation = "Submit", CommandKey = key, ActorPrincipalId = scope.SubjectId,
                CommandSha256 = fingerprint, ResponseStatus = 201,
                ResultContractVersion = checked(contract.Version + 1), CompletedAt = at,
                CreatedAt = at, CreatedBy = scope.SubjectId,
            };
            receipt.ResponseJson = ContractReviewReceiptProof.Response(receipt, "PendingReview");
            receipt.ResponseSha256 = ProcurementEvidenceEncoding.Digest(receipt.ResponseJson);
            db.AddScoped(receipt);
            await cases.SaveChangesAsync(token);
            await db.SaveChangesAsync(token);
            return new Reply(receipt.ResponseJson, 201);
        }, ct);
    }

    private static Task<IResult> Recover(Guid key, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct) => Run(http, scope, async (db, cases, core, token) =>
    {
        var row = await db.ContractReviewCommandReceipts.AsNoTracking()
            .Where(x => x.ActorPrincipalId == scope.SubjectId && x.CommandKey == key)
            .Select(x => new { x.Operation }).SingleOrDefaultAsync(token);
        if (row is null) throw new Problem("ContractReviewReceiptNotFound", 404);
        return await Replay(db, core, scope, row.Operation, key,
            await db.ContractReviewCommandReceipts.AsNoTracking().Where(x => x.ActorPrincipalId == scope.SubjectId
                && x.CommandKey == key).Select(x => x.CommandSha256).SingleAsync(token), null, token)
            ?? throw new Problem("ContractReviewReceiptNotFound", 404);
    }, ct);

    private static Task<IResult> Correct(Guid id, CorrectionInput input, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, token) =>
        {
            var key = Key(http);
            var reason = input?.Reason?.Trim();
            if (input is null || input.ExpectedContractVersion < 1
                || string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
                throw new Problem("ContractReviewCorrectionInputInvalid", 422);
            var fingerprint = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
            { schemaVersion = 1, operation = "Correction", id, actor = scope.SubjectId,
                input.ExpectedContractVersion, reason }));
            if (await Replay(db, core, scope, "Correction", key, fingerprint, id, token) is { } prior)
                return prior;
            var contract = await db.SupplierContracts.SingleOrDefaultAsync(x => x.Id == id
                && x.OwnerSubjectId == scope.SubjectId, token)
                ?? throw new Problem("ContractReviewContractNotFound", 404);
            if (contract.Status != "Returned" || contract.Version != input.ExpectedContractVersion)
                throw new Problem("ContractReviewReturnedChanged", 409);
            var source = await db.ContractReviewSubmissions.AsNoTracking()
                .Where(x => x.ContractId == id).OrderByDescending(x => x.SubmissionNumber)
                .FirstOrDefaultAsync(token) ?? throw new Problem("ContractReviewSubmissionMissing", 409);
            var maker = await Actor(core, scope, "procurement-contract-submit", source.ReviewBranchId, token);
            if (maker.PrincipalId != source.MakerPrincipalId || maker.CoreLinkedWorkerId != source.MakerWorkerAliasId
                || await db.ContractReviewCorrections.AnyAsync(x => x.SubmissionId == source.Id, token))
                throw new Problem("ContractReviewCorrectionAlreadyRecorded", 409);
            var version = await db.SupplierContractVersions.SingleOrDefaultAsync(x => x.Id == source.ContractVersionId
                && x.SupplierContractId == id, token) ?? throw new Problem("ContractReviewVersionMissing", 409);
            var rates = await db.ContractRates.AsNoTracking().Where(x => x.SupplierContractVersionId == version.Id)
                .ToArrayAsync(token);
            ContractReviewTerms.VerifyCurrentReturned(source, contract, version, rates);
            var policy = await db.PolicyVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == source.PolicyVersionId, token)
                ?? throw new Problem("ContractReviewPolicyMissing", 409);
            await ProcurementPolicyDraftRoutes.ContractPolicyPublished(db, scope, policy, token,
                OperationalPolicyPublicationMode.RetainedHistorical);
            var workflow = await cases.Cases.AsNoTracking().Include(x => x.Stages).ThenInclude(x => x.Assignments)
                .SingleOrDefaultAsync(x => x.Id == source.CoreCaseId, token)
                ?? throw new Problem("ContractReviewCaseMissing", 409);
            var pins = ContractReviewAssignmentPins.Verify(source, workflow, policy);
            var decisions = await cases.Decisions.AsNoTracking().Where(x => x.CaseId == workflow.Id)
                .ToArrayAsync(token);
            ContractReviewCoreHistory.VerifyReturned(source, workflow, pins, decisions);
            var votes = await db.ContractReviewVotes.AsNoTracking().Where(x => x.SubmissionId == source.Id)
                .ToArrayAsync(token);
            var priorReceipts = await db.ContractReviewCommandReceipts.AsNoTracking()
                .Where(x => x.SubmissionId == source.Id).ToArrayAsync(token);
            ContractReviewRetainedVotes.Verify(source, decisions, votes, priorReceipts);
            var at = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            var correction = new ContractReviewCorrection
            {
                TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                ContractId = id, ContractVersionId = version.Id, SubmissionId = source.Id,
                ReviewBranchId = source.ReviewBranchId, CoreCaseId = source.CoreCaseId,
                FromContractVersion = contract.Version, ToContractVersion = checked(contract.Version + 1),
                ActorPrincipalId = maker.PrincipalId, ActorWorkerAliasId = maker.CoreLinkedWorkerId,
                Reason = reason, CorrectedAt = at, CreatedAt = at, CreatedBy = scope.SubjectId,
            };
            contract.Status = "Draft"; version.Status = "Draft"; version.ApprovalInstanceRef = null;
            db.AddScoped(correction);
            var receipt = new ContractReviewCommandReceipt
            {
                TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                ContractId = id, SubmissionId = source.Id, ReviewBranchId = source.ReviewBranchId,
                CorrectionId = correction.Id, Operation = "Correction", CommandKey = key,
                ActorPrincipalId = scope.SubjectId, CommandSha256 = fingerprint,
                ResponseStatus = 200, ResultContractVersion = correction.ToContractVersion,
                CompletedAt = at, CreatedAt = at, CreatedBy = scope.SubjectId,
            };
            receipt.ResponseJson = ContractReviewReceiptProof.Response(receipt, "Draft");
            receipt.ResponseSha256 = ProcurementEvidenceEncoding.Digest(receipt.ResponseJson);
            db.AddScoped(receipt);
            await db.SaveChangesAsync(token);
            return new Reply(receipt.ResponseJson, 200);
        }, ct);

    private static Task<IResult> Assigned(int? page, int? pageSize, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, token) =>
        {
            if (scope.PrincipalId?.ToString("D") != scope.SubjectId || scope.OrgUnitId is not null
                || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches)
                throw new Problem("ContractReviewScopeDenied", 403);
            var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                scope.LegalEntityId, scope.SubjectId, token);
            if (person is null || person.CoreLinkedWorkerId != scope.CoreLinkedWorkerId
                || !person.Permissions.Contains("procurement-contract-review-read"))
                throw new Problem("ContractReviewScopeDenied", 403);
            var number = Math.Clamp(page ?? 1, 1, 100000);
            var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var allowed = person.AllowedBranches.ToArray();
            var branches = await core.WorkLocations.AsNoTracking().Where(x => x.TenantId == scope.TenantId
                && x.LegalEntityId == scope.LegalEntityId && !x.IsArchived
                && (person.OrganisationWide || allowed.Contains(x.Id))
                && (scope.WorkLocationId == null || x.Id == scope.WorkLocationId))
                .Select(x => x.Id).ToArrayAsync(token);
            if (branches.Length == 0) return new Reply(JsonSerializer.Serialize(new
                { items = Array.Empty<object>(), total = 0, page = number, pageSize = size }), 200);
            // The own Core assignment, active stage, company and current allowed
            // branches are all applied in PostgreSQL before count or pagination.
            var visible = db.Database.SqlQuery<AssignedReviewRow>($"""
                SELECT s.id AS "Id",s.contract_id AS "ContractId",s.core_case_id AS "CoreCaseId",
                    a."Id" AS "AssignmentId",s.review_branch_id AS "ReviewBranchId",
                    s.submitted_at AS "SubmittedAt",s.submission_number AS "SubmissionNumber",
                    contract.number AS "Number",contract.title AS "Title",
                    contract.currency_code AS "CurrencyCode",contract.version AS "Version"
                FROM procurement.contract_review_submissions AS s
                JOIN procurement.supplier_contracts AS contract ON contract.id=s.contract_id
                    AND contract.tenant_id=s.tenant_id AND contract.legal_entity_id=s.legal_entity_id
                    AND contract.status='PendingReview'
                JOIN core_approval.cases AS c ON c."Id"=s.core_case_id
                    AND c."TenantId"=s.tenant_id AND c."LegalEntityId"=s.legal_entity_id
                    AND c."SourceModule"='procurement' AND c."SourceType"='SupplierContractReview'
                    AND c."SourceId"=s.id AND c."State"='Pending'
                JOIN core_approval.assignments AS a ON a."CaseId"=c."Id"
                    AND a."TenantId"=c."TenantId" AND a."LegalEntityId"=c."LegalEntityId"
                    AND a."AssigneeSubjectId"={scope.SubjectId} AND a."State"='Pending'
                    AND a."PermissionKey"='procurement-contract-review'
                JOIN core_approval.stages AS st ON st."Id"=a."StageId"
                    AND st."CaseId"=c."Id" AND st."TenantId"=c."TenantId"
                    AND st."LegalEntityId"=c."LegalEntityId" AND st."State"='Active'
                WHERE s.tenant_id={scope.TenantId} AND s.legal_entity_id={scope.LegalEntityId}
                    AND s.review_branch_id=ANY({branches})
                """);
            var total = await visible.CountAsync(token);
            var items = await visible.OrderByDescending(x => x.SubmittedAt).ThenByDescending(x => x.Id).ThenBy(x => x.AssignmentId)
                .Skip((number - 1) * size).Take(size).ToArrayAsync(token);
            return new Reply(JsonSerializer.Serialize(new { items, total, page = number, pageSize = size }), 200);
        }, ct);

    // Unmapped query projection, not a new persisted entity. Each work item is
    // the actor's exact Core assignment; submission IDs are never action IDs.
    public sealed class AssignedReviewRow
    {
        public Guid Id { get; set; }
        public Guid ContractId { get; set; }
        public Guid CoreCaseId { get; set; }
        public Guid AssignmentId { get; set; }
        public Guid ReviewBranchId { get; set; }
        public DateTimeOffset SubmittedAt { get; set; }
        public int SubmissionNumber { get; set; }
        public string Number { get; set; } = "";
        public string Title { get; set; } = "";
        public string CurrencyCode { get; set; } = "";
        public long Version { get; set; }
    }

    private static Task<IResult> ReviewDetail(Guid id, Guid assignmentId, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, token) =>
        {
            var own = await cases.Assignments.AsNoTracking().Where(x => x.Id == assignmentId
                && x.AssigneeSubjectId == scope.SubjectId && x.State == "Pending")
                .Select(x => new { x.CaseId, x.StageId, x.Version }).SingleOrDefaultAsync(token);
            if (own is null) throw new Problem("ContractReviewAssignmentNotFound", 404);
            var source = await db.ContractReviewSubmissions.AsNoTracking().SingleOrDefaultAsync(x => x.ContractId == id
                && x.CoreCaseId == own.CaseId, token) ?? throw new Problem("ContractReviewAssignmentNotFound", 404);
            try { _ = await Actor(core, scope, "procurement-contract-review-read", source.ReviewBranchId, token); }
            catch (Problem) { throw new Problem("ContractReviewAssignmentNotFound", 404); }
            var contract = await db.SupplierContracts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id
                && x.Status == "PendingReview", token) ?? throw new Problem("ContractReviewAssignmentNotFound", 404);
            var policy = await db.PolicyVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == source.PolicyVersionId, token)
                ?? throw new Problem("ContractReviewSourceChanged", 409);
            await ProcurementPolicyDraftRoutes.ContractPolicyPublished(db, scope, policy, token,
                OperationalPolicyPublicationMode.RetainedHistorical);
            var workflow = await cases.Cases.AsNoTracking().Include(x => x.Stages).ThenInclude(x => x.Assignments)
                .SingleOrDefaultAsync(x => x.Id == own.CaseId && x.State == "Pending", token)
                ?? throw new Problem("ContractReviewAssignmentNotFound", 404);
            var pins = ContractReviewAssignmentPins.Verify(source, workflow, policy);
            if (!pins.Any(x => x.AssignmentId == assignmentId && x.PrincipalId == scope.SubjectId)
                || !workflow.Stages.Any(x => x.Id == own.StageId && x.State == "Active"))
                throw new Problem("ContractReviewAssignmentNotFound", 404);
            var decisions = await cases.Decisions.AsNoTracking().Where(x => x.CaseId == workflow.Id).ToArrayAsync(token);
            ContractReviewCoreHistory.VerifyPending(source, workflow, pins, decisions);
            using var termsDocument = JsonDocument.Parse(source.TermsJson);
            return new Reply(JsonSerializer.Serialize(new
            {
                source.Id, source.ContractId, source.CoreCaseId, source.ReviewBranchId,
                source.SubmissionNumber, source.SubmittedAt, source.TermsSha256,
                terms = termsDocument.RootElement.Clone(),
                contract.Number, contract.Title, contract.SupplierId, contract.CurrencyCode,
                contractVersion = contract.Version, caseVersion = workflow.Version,
                assignmentVersion = own.Version, assignmentId,
            }), 200);
        }, ct);

    private static Task<IResult> Decide(Guid id, Guid assignmentId, DecisionInput input,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, token) =>
        {
            var key = Key(http);
            var reason = string.IsNullOrWhiteSpace(input?.Reason) ? null : input.Reason.Trim();
            if (input is null || input.ExpectedContractVersion < 1 || input.ExpectedCaseVersion < 1
                || input.ExpectedAssignmentVersion < 1 || input.Action is not ("Approve" or "Return" or "Reject")
                || input.Action != "Approve" && reason is null || reason?.Length > 2000)
                throw new Problem("ContractReviewDecisionInputInvalid", 422);
            var fingerprint = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
            { schemaVersion = 1, operation = "Decide", id, assignmentId, actor = scope.SubjectId,
                input.ExpectedContractVersion, input.ExpectedCaseVersion,
                input.ExpectedAssignmentVersion, input.Action, reason }));
            if (await Replay(db, core, scope, "Decide", key, fingerprint, id, token) is { } prior)
                return prior;
            var ownedAssignment = await cases.Assignments.AsNoTracking().Where(x => x.Id == assignmentId
                && x.AssigneeSubjectId == scope.SubjectId).Select(x => new { x.CaseId }).SingleOrDefaultAsync(token);
            if (ownedAssignment is null) throw new Problem("ContractReviewAssignmentNotFound", 404);
            var submission = await db.ContractReviewSubmissions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ContractId == id && x.CoreCaseId == ownedAssignment.CaseId, token)
                ?? throw new Problem("ContractReviewAssignmentNotFound", 404);
            ProcurementPrincipalPeople.Person actor;
            try { actor = await Actor(core, scope, "procurement-contract-review", submission.ReviewBranchId, token); }
            catch (Problem) { throw new Problem("ContractReviewAssignmentNotFound", 404); }
            var contract = await db.SupplierContracts.SingleOrDefaultAsync(x => x.Id == id, token)
                ?? throw new Problem("ContractReviewAssignmentNotFound", 404);
            var version = await db.SupplierContractVersions.SingleOrDefaultAsync(x => x.Id == submission.ContractVersionId
                && x.SupplierContractId == id, token) ?? throw new Problem("ContractReviewSourceChanged", 409);
            if (contract.Status != "PendingReview" || version.Status != "PendingReview"
                || contract.Version != input.ExpectedContractVersion)
                throw new Problem("ContractReviewSourceChanged", 409);
            var rates = await db.ContractRates.AsNoTracking().Where(x => x.SupplierContractVersionId == version.Id)
                .ToArrayAsync(token);
            ContractReviewTerms.VerifyCurrentPending(submission, contract, version, rates);
            var policy = await db.PolicyVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == submission.PolicyVersionId, token)
                ?? throw new Problem("ContractReviewPolicyMissing", 409);
            await ProcurementPolicyDraftRoutes.ContractPolicyPublished(db, scope, policy, token,
                OperationalPolicyPublicationMode.RetainedHistorical);
            var workflow = await cases.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
                .SingleOrDefaultAsync(x => x.Id == submission.CoreCaseId, token)
                ?? throw new Problem("ContractReviewCaseMissing", 409);
            var pins = ContractReviewAssignmentPins.Verify(submission, workflow, policy);
            var retainedActor = pins.SingleOrDefault(x => x.AssignmentId == assignmentId
                && x.PrincipalId == scope.SubjectId);
            if (retainedActor is null || retainedActor.WorkerAliasId != actor.CoreLinkedWorkerId)
                throw new Problem("ContractReviewReviewerBindingChanged", 409);
            if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                    submission.MakerPrincipalId, submission.MakerWorkerAliasId,
                    actor.PrincipalId, actor.CoreLinkedWorkerId, token))
                throw new Problem("ContractReviewIndependentReviewerRequired", 403);
            foreach (var peer in pins.Where(x => x.PrincipalId != actor.PrincipalId))
                if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                        peer.PrincipalId, peer.WorkerAliasId,
                        actor.PrincipalId, actor.CoreLinkedWorkerId, token))
                    throw new Problem("ContractReviewIndependentReviewerRequired", 403);
            var assignment = workflow.Stages.SelectMany(x => x.Assignments)
                .SingleOrDefault(x => x.Id == assignmentId)
                ?? throw new Problem("ContractReviewAssignmentNotFound", 404);
            if (workflow.Version != input.ExpectedCaseVersion || assignment.Version != input.ExpectedAssignmentVersion)
                throw new Problem("ContractReviewDecisionVersionChanged", 409);
            var decisions = await cases.Decisions.Where(x => x.CaseId == workflow.Id).ToListAsync(token);
            ContractReviewCoreHistory.VerifyPending(submission, workflow, pins, decisions);
            var previousCount = decisions.Count;
            var at = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            ApprovalEngine.Decide(workflow, assignment,
                new ApprovalActor(actor.PrincipalId, scope.TenantId, scope.LegalEntityId, actor.Permissions),
                input.Action, key.ToString("D"), reason, decisions, at);
            if (decisions.Count != previousCount + 1)
                throw new Problem("ContractReviewDecisionReceiptMissing", 409);
            var coreDecision = decisions.Single(x => x.IdempotencyKey == key.ToString("D"));
            cases.Decisions.Add(coreDecision);
            var vote = new ContractReviewVote { TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                SubmissionId = submission.Id, ContractId = id, ReviewBranchId = submission.ReviewBranchId,
                CoreCaseId = workflow.Id, CoreStageId = assignment.StageId,
                CoreAssignmentId = assignment.Id, CoreDecisionId = coreDecision.Id,
                ActorPrincipalId = actor.PrincipalId, ActorWorkerAliasId = actor.CoreLinkedWorkerId,
                Action = input.Action, DecidedAt = at, CreatedAt = at, CreatedBy = actor.PrincipalId };
            db.AddScoped(vote);
            if (workflow.State is "Approved" or "Returned" or "Rejected")
            { contract.Status = workflow.State; version.Status = workflow.State; }
            var receipt = new ContractReviewCommandReceipt
            {
                TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                ContractId = id, SubmissionId = submission.Id, ReviewBranchId = submission.ReviewBranchId,
                VoteId = vote.Id, Operation = "Decide", CommandKey = key,
                ActorPrincipalId = actor.PrincipalId, CommandSha256 = fingerprint,
                ResponseStatus = 200, ResultContractVersion = workflow.State == "Pending"
                    ? contract.Version : checked(contract.Version + 1),
                CompletedAt = at, CreatedAt = at, CreatedBy = actor.PrincipalId,
            };
            receipt.ResponseJson = ContractReviewReceiptProof.Response(receipt, contract.Status);
            receipt.ResponseSha256 = ProcurementEvidenceEncoding.Digest(receipt.ResponseJson);
            db.AddScoped(receipt);
            await cases.SaveChangesAsync(token);
            await db.SaveChangesAsync(token);
            return new Reply(receipt.ResponseJson, 200);
        }, ct);
}
