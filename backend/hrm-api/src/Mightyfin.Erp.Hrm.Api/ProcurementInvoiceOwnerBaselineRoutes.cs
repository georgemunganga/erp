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

/// <summary>M6-specific physical read/write proof supplied by the owning
/// readiness implementation after native schema acceptance.</summary>
public interface IProcurementInvoiceOwnerBaselineReadiness
{
    Task<bool> CanReadAsync(ProcurementDbContext db, CancellationToken ct);
    Task<bool> CanRecordAsync(ProcurementDbContext db, CancellationToken ct);
}

/// <summary>Captures an immutable company invoice inventory for independent
/// Core review. Physical readiness and a disabled-by-default write flag guard
/// this baseline-only route set; it does not qualify invoice claims. A company
/// selection uses an ordinary company-wide Core principal context; an explicit
/// All-branches shell context is never treated as authority to write.</summary>
public static class ProcurementInvoiceOwnerBaselineRoutes
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed class BaselineProblem(string code, int status) : Exception(code)
    {
        internal int Status => status;
    }

    public sealed record DecisionInput(long ExpectedCaseVersion, long ExpectedAssignmentVersion,
        string Action, string? Reason);

    internal static bool WriteContextAllowed(bool confined,bool explicitAll,bool rawAll)
        => !confined && !explicitAll && !rawAll;

    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/invoice-owner-baselines");
        group.MapPost("/", Open).RequireAuthorization(InvoiceProcurementBaselineEvidence.OpenPermission);
        group.MapGet("/{id:guid}", Detail).RequireAuthorization(InvoiceProcurementBaselineEvidence.ReadPermission);
        group.MapGet("/commands/{key:guid}", Recover).RequireAuthorization("procurement-access");
        group.MapPost("/{id:guid}/assignments/{assignmentId:guid}/decisions", Decide)
            .RequireAuthorization(InvoiceProcurementBaselineEvidence.ReviewPermission);
    }

    private static async Task<IResult> Run(HttpContext http, ProcurementRequestScope scope, bool write,
        Func<ProcurementDbContext, ApprovalDbContext, HrmDbContext, IAuthorizationService,
            CancellationToken, Task<IResult>> work, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        try
        {
            if(write && !WriteContextAllowed(scope.IsConfined,scope.IsExplicitAllBranches,
                    http.Request.Headers.ContainsKey("X-Shell-Branch-Scope")))
                throw new BaselineProblem("InvoiceOwnerSelectedCompanyContextRequired",422);
            if (write && !http.RequestServices.GetRequiredService<IConfiguration>()
                    .GetValue<bool>("Procurement:InvoiceOwnerBaseline:Enabled"))
                throw new BaselineProblem("InvoiceOwnerBaselineUnavailable", 503);
            return await http.RequestServices.GetRequiredService<ProcurementApprovalTransaction>()
                .ExecuteAsync(async (db, cases, token) =>
                {
                    var readiness = http.RequestServices.GetService<IProcurementInvoiceOwnerBaselineReadiness>();
                    if (readiness is null || !(write
                            ? await readiness.CanRecordAsync(db, token)
                            : await readiness.CanReadAsync(db, token)))
                        throw new BaselineProblem("InvoiceOwnerBaselineSchemaUnavailable", 503);
                    await using var core = await ProcurementApprovalExecutionLocks.CoreAsync(db, scope, token);
                    return await work(db, cases, core,
                        http.RequestServices.GetRequiredService<IAuthorizationService>(), token);
                }, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is BaselineProblem or InvalidOperationException
            or ArgumentException or UnauthorizedAccessException or KeyNotFoundException
            or JsonException or System.Data.Common.DbException or DbUpdateException)
        {
            var status = error is BaselineProblem problem ? problem.Status
                : error is OperationalPolicyPublicationProblem publication ? publication.Status
                : error is UnauthorizedAccessException ? 403
                : error is KeyNotFoundException ? 404
                : ProcurementPostgresFailures.IsConcurrency(error) ? 409
                : ProcurementPostgresFailures.IsStoreFailure(error) ? 503 : 409;
            return Results.Json(new
            {
                code = error is BaselineProblem or OperationalPolicyPublicationProblem
                    ? error.Message : "InvoiceOwnerBaselineEvidenceNeedsReview",
                message = "Refresh the company invoice history, Core reviewer and current access before continuing. No invoice claim or Finance posting was created."
            }, statusCode: status);
        }
    }

    private static async Task<ProcurementPrincipalPeople.Person> Actor(HrmDbContext core,
        ProcurementRequestScope scope, HttpContext http, IAuthorizationService auth,
        string permission, CancellationToken ct)
    {
        if (scope.IsConfined
            || !(await auth.AuthorizeAsync(http.User, permission)).Succeeded)
            throw new UnauthorizedAccessException();
        var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, scope.SubjectId, ct);
        if (person is null || !person.OrganisationWide
            || scope.PrincipalId?.ToString("D") != person.PrincipalId
            || !person.Permissions.Contains(permission))
            throw new UnauthorizedAccessException();
        return person;
    }

    private static InvoiceProcurementBaselineReviewPolicyRules.Person Reviewer(
        ProcurementPrincipalPeople.Person person, ProcurementRequestScope scope) =>
        new(scope.TenantId, scope.LegalEntityId, person.PrincipalId,
            person.CoreLinkedWorkerId, true, person.OrganisationWide, person.Permissions);

    private static Guid Key(HttpContext http) =>
        Guid.TryParseExact(http.Request.Headers["Idempotency-Key"], "D", out var key)
            && key != Guid.Empty ? key
            : throw new BaselineProblem("InvoiceOwnerCommandKeyRequired", 400);

    private static DateTimeOffset Now() =>
        ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;

    internal static string Fingerprint(string operation, Guid key, Guid? baselineId,
        Guid? assignmentId, DecisionInput? input) => ProcurementEvidenceEncoding.PolicyJsonDigest(
        JsonSerializer.Serialize(new { schemaVersion = 1, operation, key, baselineId, assignmentId, input }, Json));

    private static async Task<PolicyVersion> CurrentPolicy(ProcurementDbContext db,
        ProcurementRequestScope scope, DateTimeOffset at, CancellationToken ct)
    {
        var rows = await db.PolicyVersions.Where(x =>
            x.PolicyKey == InvoiceProcurementBaselineReviewPolicyRules.PolicyKey
            && x.State == "Published" && x.EffectiveFrom <= at
            && (x.EffectiveTo == null || at < x.EffectiveTo)).Take(2).ToArrayAsync(ct);
        if (rows.Length != 1 || rows[0].TenantId != scope.TenantId
            || rows[0].LegalEntityId != scope.LegalEntityId)
            throw new BaselineProblem("InvoiceOwnerCurrentPolicyUnavailable", 409);
        return rows[0];
    }

    private static async Task<ApprovalCase> Workflow(ApprovalDbContext cases,
        InvoiceProcurementOwnerBaseline baseline, CancellationToken ct) =>
        await cases.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
            .SingleOrDefaultAsync(x => x.Id == baseline.CoreCaseId
                && x.SourceModule == "procurement"
                && x.SourceType == InvoiceProcurementBaselineEvidence.SourceType
                && x.SourceId == baseline.Id && x.SourceVersion == 1, ct)
            ?? throw new KeyNotFoundException();

    private static async Task VerifyStored(ProcurementDbContext db,
        InvoiceProcurementOwnerBaseline baseline, CancellationToken ct)
    {
        var rows = await db.InvoiceProcurementOwnerBaselineRows.AsNoTracking()
            .Where(x => x.BaselineId == baseline.Id).ToArrayAsync(ct);
        _ = InvoiceProcurementStoredBaselineEvidence.VerifyStored(baseline, rows);
    }

    private static Task<IResult> Open(HttpContext http, ProcurementRequestScope scope,
        CancellationToken ct) => Run(http, scope, true, async (db, cases, core, auth, token) =>
    {
        var actor = await Actor(core, scope, http, auth,
            InvoiceProcurementBaselineEvidence.OpenPermission, token);
        var key = Key(http);
        var requestSha = Fingerprint("Open", key, null, null, null);
        if (await db.RecoverInvoiceOwnerReceiptAsync(key, "Open", requestSha, token) is { } prior)
        {
            var stored = await db.InvoiceProcurementOwnerBaselines.AsNoTracking()
                .SingleAsync(x => x.Id == prior.BaselineId, token);
            await VerifyStored(db, stored, token);
            return Results.Content(prior.ResponseJson, "application/json", statusCode: 201);
        }
        var at = Now();
        var policy = await CurrentPolicy(db, scope, at, token);
        await ProcurementOperationalPolicyCurrentPublication.RequireAsync(
            db, scope, http.RequestServices, policy, token);
        var reviewerId = InvoiceProcurementBaselineReviewPolicyRules.Parse(policy.RulesJson)
            .ReviewerPrincipalId;
        var reviewer = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, reviewerId, token)
            ?? throw new BaselineProblem("InvoiceOwnerReviewerUnavailable", 409);
        if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                actor.PrincipalId, actor.CoreLinkedWorkerId, reviewer.PrincipalId,
                reviewer.CoreLinkedWorkerId, token))
            throw new BaselineProblem("InvoiceOwnerIndependentReviewerRequired", 409);
        var baselineId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var workflow = InvoiceProcurementBaselineOpeningPlan.Prepare(baselineId, caseId,
            policy, scope.TenantId, scope.LegalEntityId, actor.PrincipalId,
            actor.CoreLinkedWorkerId, Reviewer(reviewer, scope), at);
        var aliases = new Dictionary<string, Guid?>
            { [reviewer.PrincipalId] = reviewer.CoreLinkedWorkerId };
        cases.AddCase(workflow);
        var baseline = await db.CaptureAndStageInvoiceOwnerBaselineAsync(
            baselineId, workflow, aliases, at, token);
        await cases.SaveChangesAsync(token);
        var response = new InvoiceProcurementOwnerReceiptEvidence.Response(1,
            baseline.Id, caseId, null, "Open", key, workflow.Version,
            workflow.State, baseline.InventorySetSha256);
        var receipt = await db.StageInvoiceOwnerReceiptAsync(response, requestSha, at, token);
        return Results.Content(receipt.ResponseJson, "application/json", statusCode: 201);
    }, ct);

    private static Task<IResult> Decide(Guid id, Guid assignmentId, DecisionInput input,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct) =>
        Run(http, scope, true, async (db, cases, core, auth, token) =>
    {
        var actor = await Actor(core, scope, http, auth,
            InvoiceProcurementBaselineEvidence.ReviewPermission, token);
        var key = Key(http);
        var requestSha = Fingerprint("Decide", key, id, assignmentId, input);
        if (await db.RecoverInvoiceOwnerReceiptAsync(key, "Decide", requestSha, token) is { } prior)
        {
            if (prior.BaselineId != id) throw new BaselineProblem("InvoiceOwnerCommandKeyConflict", 409);
            var stored = await db.InvoiceProcurementOwnerBaselines.AsNoTracking()
                .SingleAsync(x => x.Id == id, token);
            await VerifyStored(db, stored, token);
            return Results.Content(prior.ResponseJson, "application/json");
        }
        var baseline = await db.InvoiceProcurementOwnerBaselines
            .SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new KeyNotFoundException();
        await VerifyStored(db, baseline, token);
        if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                baseline.MakerPrincipalId, baseline.MakerWorkerAliasId,
                actor.PrincipalId, actor.CoreLinkedWorkerId, token))
            throw new UnauthorizedAccessException();
        var workflow = await Workflow(cases, baseline, token);
        var assignment = workflow.Stages.SelectMany(x => x.Assignments)
            .SingleOrDefault(x => x.Id == assignmentId)
            ?? throw new KeyNotFoundException();
        var at = Now();
        PolicyVersion? policy = null;
        if (input.Action == "Approve")
        {
            policy = await CurrentPolicy(db, scope, at, token);
            await ProcurementOperationalPolicyCurrentPublication.RequireAsync(
                db, scope, http.RequestServices, policy, token);
        }
        var aliases = new Dictionary<string, Guid?>
            { [actor.PrincipalId] = actor.CoreLinkedWorkerId };
        var decisions = (await cases.Decisions.Where(x => x.CaseId == workflow.Id)
            .ToArrayAsync(token)).ToList();
        var previousIds = decisions.Select(x => x.Id).ToHashSet();
        var decision = InvoiceProcurementBaselineDecision.Apply(baseline, workflow, assignment,
            aliases, Reviewer(actor, scope), policy,
            new(key, input.ExpectedCaseVersion, input.ExpectedAssignmentVersion,
                input.Action, input.Reason, at), decisions);
        if (!decision.Recorded) throw new BaselineProblem("InvoiceOwnerDecisionNotRecorded", 409);
        var vote = decisions.Single(x => !previousIds.Contains(x.Id));
        cases.Decisions.Add(vote);
        await cases.SaveChangesAsync(token);
        if (input.Action == "Approve")
        {
            var approvalAudit = new AuditEvent
            {
                TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
                RecordType = InvoiceProcurementBaselineEvidence.SourceType,
                RecordId = baseline.Id, Action = "InvoiceOwnerBaselineApproved",
                ActorSubjectId = actor.PrincipalId,
                ChangedFieldsJson = InvoiceProcurementBaselineEvidence.DecisionAuditJson(baseline, vote),
                CorrelationId = workflow.CompletionKey!, CreatedAt = at,
                CreatedBy = actor.PrincipalId
            };
            InvoiceProcurementBaselineEvidence.VerifyCoreReview(baseline, workflow,
                decisions, approvalAudit, actor.PrincipalId, actor.CoreLinkedWorkerId, at);
            db.AddScoped(approvalAudit);
        }
        var response = new InvoiceProcurementOwnerReceiptEvidence.Response(1,
            baseline.Id, workflow.Id, vote.Id, "Decide", key, workflow.Version,
            workflow.State, baseline.InventorySetSha256);
        var receipt = await db.StageInvoiceOwnerReceiptAsync(response, requestSha, at, token);
        return Results.Content(receipt.ResponseJson, "application/json");
    }, ct);

    private static Task<IResult> Detail(Guid id, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct) =>
        Run(http, scope, false, async (db, cases, core, auth, token) =>
    {
        var actor = await Actor(core, scope, http, auth,
            InvoiceProcurementBaselineEvidence.ReadPermission, token);
        var baseline = await db.InvoiceProcurementOwnerBaselines.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new KeyNotFoundException();
        await VerifyStored(db, baseline, token);
        var workflow = await Workflow(cases, baseline, token);
        if (baseline.MakerPrincipalId != actor.PrincipalId
            && !workflow.Stages.SelectMany(x => x.Assignments)
                .Any(x => x.AssigneeSubjectId == actor.PrincipalId))
            throw new KeyNotFoundException();
        return Results.Json(new
        {
            baselineId = baseline.Id, baseline.CoreCaseId, baseline.CatalogManifestSha256,
            baseline.InventorySetSha256, baseline.InventoryRowCount,
            coreCaseVersion = workflow.Version, coreCaseState = workflow.State,
            assignments = workflow.Stages.SelectMany(x => x.Assignments)
                .Where(x => x.AssigneeSubjectId == actor.PrincipalId)
                .Select(x => new { x.Id, x.Version, x.State }).ToArray(),
            recordedHistoricalResult = true, qualifiedExposure = false,
            canPostToFinance = false
        });
    }, ct);

    private static Task<IResult> Recover(Guid key, string operation, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct) =>
        Run(http, scope, false, async (db, _, core, auth, token) =>
    {
        await Actor(core, scope, http, auth,
            InvoiceProcurementBaselineEvidence.ReadPermission, token);
        var receipt = await db.RecoverInvoiceOwnerReceiptAsync(key, operation, null, token)
            ?? throw new KeyNotFoundException();
        var baseline = await db.InvoiceProcurementOwnerBaselines.AsNoTracking()
            .SingleAsync(x => x.Id == receipt.BaselineId, token);
        await VerifyStored(db, baseline, token);
        return Results.Content(receipt.ResponseJson, "application/json");
    }, ct);
}
