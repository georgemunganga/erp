using System.Text.Json;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Own-department procurement forecasts. These drafts do not reserve budget or authorize buying.</summary>
public static class ProcurementPlanRoutes
{
    public static void MapRoutes(WebApplication app)
    {
        var group = app.MapGroup("/api/procurement/v1/plans");
        group.MapGet("", List).RequireAuthorization("procurement-plan-read");
        group.MapGet("/{id:guid}", Get).RequireAuthorization("procurement-plan-read");
        group.MapPost("", Create).Accepts<ProcurementPlanInput>("application/json").RequireAuthorization("procurement-plan-create");
        group.MapPut("/{id:guid}", Update).Accepts<UpdatePlan>("application/json").RequireAuthorization("procurement-plan-edit");
        group.MapPost("/{id:guid}/cancel", Cancel).RequireAuthorization("procurement-plan-edit");
        group.MapPost("/{id:guid}/claim-scope", ClaimScope).RequireAuthorization("procurement-access");
    }

    private static async Task<IResult> List(ProcurementDbContext db, ProcurementRequestScope scope,
        IProcurementPlanReviewReadiness readiness,
        ProcurementPlanReviewOptions options, HttpContext http, IAuthorizationService authorization,
        ProcurementApprovalTransaction transaction,
        string? status, int? page, int? pageSize, CancellationToken ct)
    {
        if (!await readiness.CanReadPlanAsync(db, ct)) return PlanStoreUnavailable();
        var number = Math.Clamp(page ?? 1, 1, 100000);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var branches = scope.PermittedWorkLocationIds;
        var selected = scope.WorkLocationId;
        var all = scope.IsExplicitAllBranches;
        var query = db.ProcurementPlans.AsNoTracking().Where(x => x.OwnerSubjectId == scope.SubjectId
            && (x.BranchRef == null || all && x.BranchRef.HasValue && branches.Contains(x.BranchRef.Value)
                || !all && selected.HasValue && x.BranchRef == selected));
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((number - 1) * size).Take(size)
            .Select(x => new { x.Id, x.Number, x.FinancialPeriodRef, x.DepartmentRef, x.BranchRef,
                x.CurrencyCode, x.Status, x.RevisionNumber, x.Version, x.CreatedAt,
                LineCount = db.ProcurementPlanLines.Count(line => line.ProcurementPlanId == x.Id && line.Status == "Active") })
            .ToListAsync(ct);
        var actions = await ActionContext(transaction, scope, options, readiness, http, authorization, ct);
        return Results.Ok(new { items = items.Select(x => new
            {
                x.Id, x.Number, x.FinancialPeriodRef, x.DepartmentRef, x.BranchRef,
                x.CurrencyCode, x.Status, x.RevisionNumber, x.Version, x.CreatedAt, x.LineCount,
                allowedActions = AllowedActions(x.Status, x.BranchRef, scope, actions),
            }).ToArray(), total, page = number, pageSize = size });
    }

    private static async Task<IResult> Get(Guid id, ProcurementDbContext db, ProcurementRequestScope scope,
        IProcurementPlanReviewReadiness readiness,
        ProcurementPlanReviewOptions options, HttpContext http, IAuthorizationService authorization,
        ProcurementApprovalTransaction transaction,
        CancellationToken ct)
    {
        if (!await readiness.CanReadPlanAsync(db, ct)) return PlanStoreUnavailable();
        var plan = await db.ProcurementPlans.AsNoTracking().Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id && x.OwnerSubjectId == scope.SubjectId, ct);
        if (plan is not null && !VisibleBranch(plan, scope)) plan = null;
        if (plan is null) return Problem("not-found", "Plan not found.", 404);
        var actions = await ActionContext(transaction, scope, options, readiness, http, authorization, ct);
        return Results.Ok(Detail(plan, AllowedActions(plan.Status, plan.BranchRef, scope, actions)));
    }

    private static async Task<IResult> Create(JsonElement body, ProcurementDbContext db, HrmDbContext core,
        IProcurementPlanReviewReadiness readiness,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        if (!await readiness.CanReadPlanAsync(db, ct)) return PlanStoreUnavailable();
        var parsed=ProcurementOriginalValueInput.Read<ProcurementPlanInput>(body);
        if(parsed.Error is not null) return parsed.Error;
        var input=parsed.Value!;
        if (scope.IsExplicitAllBranches || scope.WorkLocationId is null)
            return Problem("branch-required", "Choose a permitted branch before creating a plan.", 400);
        var coreCurrency = await ProcurementCurrencyDefaults.ForSelectedCompanyAsync(core, scope, ct);
        if (coreCurrency is null) return Problem("currency-unavailable", "The company's currency is unavailable.", 503);
        if (!string.IsNullOrWhiteSpace(input.CurrencyCode)
            && !string.Equals(input.CurrencyCode.Trim(), coreCurrency, StringComparison.OrdinalIgnoreCase))
            return Problem("currency-context-changed", "Refresh the company currency before saving this plan.", 409);
        input = input with { CurrencyCode = coreCurrency };
        var issue = ProcurementPlanRules.Validate(input);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        if (input.Lines!.Any(x => x.Id.HasValue))
            return Problem("validation-failed", "New plan lines cannot carry an existing line ID.", 422);
        var referenceIssue = await ValidateReferences(input.Lines!, db, ct);
        if (referenceIssue is not null) return Problem("validation-failed", referenceIssue, 422);
        var plan = new ProcurementPlan
        {
            Number = "PLAN-" + Guid.CreateVersion7().ToString("N"),
            BranchRef = scope.WorkLocationId,
            FinancialPeriodRef = input.FinancialPeriodRef!.Trim(),
            CurrencyCode = input.CurrencyCode!.Trim().ToUpperInvariant(),
            FundingSourceRef = Clean(input.FundingSourceRef),
            DepartmentRef = scope.OrgUnitId,
            OwnerSubjectId = scope.SubjectId,
            Status = "Draft",
        };
        for (var i = 0; i < input.Lines!.Count; i++)
            plan.Lines.Add(NewLine(plan.Id, i + 1, input.Lines[i]));
        db.AddScoped(plan);
        ProcurementDraftAudit.Add(db, scope, "ProcurementPlan", plan.Id,
            "DraftCreated", null, Detail(plan));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/procurement/v1/plans/{plan.Id}", Detail(plan));
    }

    private static async Task<IResult> Update(Guid id, JsonElement body, ProcurementDbContext db,
        IProcurementPlanReviewReadiness readiness,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        if (!await readiness.CanReadPlanAsync(db, ct)) return PlanStoreUnavailable();
        if (scope.IsExplicitAllBranches || scope.WorkLocationId is null)
            return Problem("branch-required", "Choose a permitted branch before changing a plan.", 400);
        var parsed=ProcurementOriginalValueInput.Read<UpdatePlan>(body);
        if(parsed.Error is not null) return parsed.Error;
        var input=parsed.Value!;
        var plan = await db.ProcurementPlans.Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id && x.OwnerSubjectId == scope.SubjectId, ct);
        if (plan is null) return Problem("not-found", "Plan not found.", 404);
        if (!VisibleBranch(plan, scope)) return Problem("not-found", "Plan not found.", 404);
        if (plan.Status is not ("Draft" or "Returned"))
            return Problem("plan-locked", "Only a draft or returned plan can be changed.", 409);
        if (plan.Version != input.Version) return Problem("version-conflict", "Refresh this plan before saving changes.", 409);
        if (!string.IsNullOrWhiteSpace(input.Draft.CurrencyCode)
            && !string.Equals(input.Draft.CurrencyCode.Trim(), plan.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            return Problem("currency-locked", "The saved forecast currency cannot be changed with an edit.", 409);
        var draft = input.Draft with { CurrencyCode = plan.CurrencyCode };
        var issue = ProcurementPlanRules.Validate(draft);
        if (issue is not null) return Problem("validation-failed", issue, 422);
        var lines = draft.Lines!;
        var referenceIssue = await ValidateReferences(lines, db, ct);
        if (referenceIssue is not null) return Problem("validation-failed", referenceIssue, 422);
        var existing = plan.Lines.ToDictionary(x => x.Id);
        if (lines.Any(x => x.Id.HasValue && (!existing.ContainsKey(x.Id.Value) || existing[x.Id.Value].Status != "Active")))
            return Problem("validation-failed", "A planned need is unavailable or belongs to another plan.", 422);
        var before = ProcurementDraftAudit.Snapshot(Detail(plan));

        plan.FinancialPeriodRef = draft.FinancialPeriodRef!.Trim();
        plan.FundingSourceRef = Clean(draft.FundingSourceRef);
        plan.UpdatedAt = DateTimeOffset.UtcNow; // conflict token also changes for line-only edits
        var included = lines.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();
        foreach (var line in plan.Lines.Where(x => x.Status == "Active" && !included.Contains(x.Id)))
        {
            line.Status = "Cancelled";
            line.StatusReason = "Removed from draft plan";
        }
        var nextLine = plan.Lines.Count == 0 ? 1 : plan.Lines.Max(x => x.LineNumber) + 1;
        foreach (var incoming in lines)
        {
            if (incoming.Id is Guid lineId)
                Apply(existing[lineId], incoming);
            else
            {
                var line = NewLine(plan.Id, nextLine++, incoming);
                db.AddScoped(line);
            }
        }
        ProcurementDraftAudit.Add(db, scope, "ProcurementPlan", plan.Id,
            "DraftUpdated", before, Detail(plan));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh this plan before saving changes.", 409); }
        return Results.Ok(Detail(plan));
    }

    private static async Task<IResult> Cancel(Guid id, CancelPlan input, ProcurementDbContext db,
        IProcurementPlanReviewReadiness readiness,
        ProcurementRequestScope scope, ProcurementPlanReviewOptions options,
        ProcurementApprovalTransaction transaction, CancellationToken ct)
    {
        if (!await readiness.CanReadPlanAsync(db, ct)) return PlanStoreUnavailable();
        if (scope.IsExplicitAllBranches || scope.WorkLocationId is null)
            return Problem("branch-required", "Choose a permitted branch before cancelling a plan.", 400);
        var plan = await db.ProcurementPlans.Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id && x.OwnerSubjectId == scope.SubjectId, ct);
        if (plan is null) return Problem("not-found", "Plan not found.", 404);
        if (!VisibleBranch(plan, scope)) return Problem("not-found", "Plan not found.", 404);
        if (plan.Status is not ("Draft" or "Returned"))
            return Problem("plan-locked", "Only a draft or returned plan can be cancelled here.", 409);
        if (plan.Version != input.Version) return Problem("version-conflict", "Refresh this plan before cancelling it.", 409);
        if (plan.Status == "Returned")
        {
            try
            {
                var rounds = await db.PlanReviewSubmissions.AsNoTracking()
                    .Where(x => x.ProcurementPlanId == plan.Id)
                    .OrderByDescending(x => x.SubmittedPlanVersion).ToArrayAsync(ct);
                if (!ReturnedReviewClosed(plan, rounds))
                    return Problem("plan-review-conflict", "Refresh the returned review before cancelling this plan.", 409);
                if (!options.ExecutionDatabaseConfigured) return PlanStoreUnavailable();
                var caseReturned = await transaction.ExecuteAsync(async (records, core, token) =>
                    await core.Cases.AsNoTracking().AnyAsync(x => x.Id == rounds[0].CoreApprovalCaseId
                        && x.SourceModule == "procurement" && x.SourceType == PlanReviewCoreWorkflow.SourceType
                        && x.SourceId == plan.Id && x.State == "Returned", token), ct);
                if (!caseReturned)
                    return Problem("plan-review-conflict", "Refresh the returned review before cancelling this plan.", 409);
            }
            catch (OperationCanceledException) { throw; }
            catch (DbException) { return PlanStoreUnavailable(); }
            catch (InvalidOperationException) { return PlanStoreUnavailable(); }
        }
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000)
            return Problem("validation-failed", "Give a cancellation reason of up to 1,000 characters.", 422);
        var before = ProcurementDraftAudit.Snapshot(Detail(plan));
        plan.Status = "Cancelled";
        plan.StatusReason = input.Reason.Trim();
        foreach (var line in plan.Lines.Where(x => x.Status == "Active"))
        {
            line.Status = "Cancelled";
            line.StatusReason = "Plan cancelled: " + input.Reason.Trim();
        }
        ProcurementDraftAudit.Add(db, scope, "ProcurementPlan", plan.Id,
            "DraftCancelled", before, Detail(plan));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Problem("version-conflict", "Refresh this plan before cancelling it.", 409); }
        return Results.Ok(Detail(plan));
    }

    private static async Task<IResult> ClaimScope(Guid id, ClaimPlanScope input, HttpContext http,
        IAuthorizationService authorization, ProcurementPlanReviewOptions options,
        IProcurementPlanReviewReadiness readiness,
        ProcurementDbContext db, ProcurementRequestScope scope,
        CancellationToken ct)
    {
        if (!options.Enabled || !options.ExecutionDatabaseConfigured || !await readiness.IsReadyAsync(ct))
            return Problem("plan-review-unavailable", "Plan review is not ready in this workspace.", 503);
        if (scope.IsExplicitAllBranches || scope.WorkLocationId is not { } branch || scope.PrincipalId is null)
            return Problem("branch-required", "Choose a permitted branch to claim this plan.", 400);
        if (!ProcurementPlanReviewRoutes.TryKey(http, out var key))
            return Problem("command-key-required", "Send one valid Idempotency-Key for safe recovery.", 422);
        var reason = input.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 ||
            string.IsNullOrWhiteSpace(input.ConfirmStoredCurrencyCode))
            return Problem("validation-failed", "Confirm the saved currency and give a claim reason.", 422);
        var plan = await db.ProcurementPlans.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (plan is null) return Problem("not-found", "Plan not found.", 404);
        var owner = plan.OwnerSubjectId == scope.SubjectId;
        var permission = owner ? "procurement-plan-edit" : "procurement-plan-claim-scope-all";
        if (!(await authorization.AuthorizeAsync(http.User, permission)).Succeeded)
            return Problem("not-found", "Plan not found.", 404);
        if (plan.BranchRef.HasValue && plan.BranchRef != branch)
            return Problem("not-found", "Plan not found.", 404);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { id, actor = scope.SubjectId, input.Version,
                currency = input.ConfirmStoredCurrencyCode.Trim().ToUpperInvariant(), reason, branch })))).ToLowerInvariant();
        var prior = await db.PlanReviewCommandReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CommandKey == key, ct);
        if (prior is not null)
            return prior.ProcurementPlanId == id && prior.Operation == "ClaimScope"
                && prior.ActorSubjectId == scope.SubjectId && prior.RequestSha256 == fingerprint
                ? Results.Content(prior.ResponseJson, "application/json", statusCode: prior.ResponseStatus)
                : Problem("command-key-conflict", "This command key belongs to another action.", 409);
        if (plan.BranchRef.HasValue || plan.Status is not ("Draft" or "Returned"))
            return Problem("plan-scope-locked", "Only a legacy unclaimed draft can be assigned to a branch.", 409);
        if (plan.Version != input.Version)
            return Problem("version-conflict", "Refresh this plan before claiming its branch.", 409);
        if (!string.Equals(plan.CurrencyCode, input.ConfirmStoredCurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase))
            return Problem("currency-confirmation-mismatch", "Confirm the plan's saved currency without changing its estimates.", 409);
        var before = ProcurementDraftAudit.Snapshot(Detail(plan));
        plan.BranchRef = branch;
        plan.UpdatedAt = DateTimeOffset.UtcNow;
        ProcurementDraftAudit.Add(db, scope, "ProcurementPlan", plan.Id, "ScopeClaimed", before, Detail(plan));
        var response = ClaimScopeResponse(plan.Id, branch, plan.CurrencyCode, checked(input.Version + 1));
        db.AddScoped(new PlanReviewCommandReceipt { ProcurementPlanId = id, CommandKey = key,
            Operation = "ClaimScope", ActorSubjectId = scope.SubjectId, RequestSha256 = fingerprint,
            ResponseJson = response, ResponseStatus = 200 });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { return Problem("version-conflict", "Refresh this plan before claiming its branch.", 409); }
        catch (DbUpdateException)
        { return Problem("claim-unconfirmed", "Check this plan and the original command key before retrying.", 503); }
        return Results.Content(response, "application/json", statusCode: 200);
    }

    private static async Task<string?> ValidateReferences(IReadOnlyList<ProcurementPlanLineInput> lines,
        ProcurementDbContext db, CancellationToken ct)
    {
        var ids = lines.Where(x => x.CatalogItemId.HasValue).Select(x => x.CatalogItemId!.Value).Distinct().ToArray();
        if (ids.Length > 0 && await db.CatalogItems.AsNoTracking().CountAsync(x => ids.Contains(x.Id), ct) != ids.Length)
            return "One or more catalog items are unavailable in this company.";
        return null;
    }

    private static ProcurementPlanLine NewLine(Guid planId, int number, ProcurementPlanLineInput input)
    {
        var line = new ProcurementPlanLine { ProcurementPlanId = planId, LineNumber = number };
        Apply(line, input);
        return line;
    }

    private static void Apply(ProcurementPlanLine line, ProcurementPlanLineInput input)
    {
        line.CatalogItemId = input.CatalogItemId;
        line.Description = input.Description!.Trim();
        line.CategoryCode = Clean(input.CategoryCode);
        line.UnitCode = input.UnitCode!.Trim();
        line.EstimatedQuantity = input.EstimatedQuantity;
        line.EstimatedAmount = input.EstimatedAmount;
        line.ExpectedOn = input.ExpectedOn;
        line.FundingSourceRef = Clean(input.FundingSourceRef);
    }

    private static object Detail(ProcurementPlan plan, IReadOnlyList<string>? allowedActions = null) => new
    {
        plan.Id, plan.Number, plan.FinancialPeriodRef, plan.DepartmentRef, plan.BranchRef,
        plan.CurrencyCode, plan.Status, plan.StatusReason, plan.OwnerSubjectId,
        plan.RevisionNumber, plan.FundingSourceRef, plan.Version, plan.CreatedAt, plan.UpdatedAt,
        allowedActions = allowedActions ?? [],
        Lines = plan.Lines.OrderBy(x => x.LineNumber).Select(x => new
        {
            x.Id, x.LineNumber, x.CatalogItemId, x.Description, x.CategoryCode, x.UnitCode,
            x.EstimatedQuantity, x.EstimatedAmount, x.ExpectedOn, x.FundingSourceRef,
            x.Status, x.StatusReason, x.Version,
        }),
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    internal static string ClaimScopeResponse(Guid id, Guid branchRef, string currencyCode, long version)
        => JsonSerializer.Serialize(new { id, branchRef, currencyCode, version, scopeClaimed = true },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    internal static bool ReturnedReviewClosed(ProcurementPlan plan, IReadOnlyList<PlanReviewSubmission> rounds)
    {
        var latest = rounds.OrderByDescending(x => x.SubmittedPlanVersion).FirstOrDefault();
        return plan.Status == "Returned" && latest is { State: "Returned", CompletedAt: not null }
            && plan.ApprovalInstanceRef == latest.CoreApprovalCaseId.ToString("D")
            && !rounds.Any(x => x.State == "Pending");
    }
    private sealed record PlanActionContext(bool Edit, bool Claim, bool Submit, bool Withdraw,
        bool WriteReady, bool PolicyReady);

    private static async Task<PlanActionContext> ActionContext(ProcurementApprovalTransaction transaction,
        ProcurementRequestScope scope, ProcurementPlanReviewOptions options,
        IProcurementPlanReviewReadiness readiness, HttpContext http,
        IAuthorizationService authorization, CancellationToken ct)
    {
        var edit = (await authorization.AuthorizeAsync(http.User, "procurement-plan-edit")).Succeeded;
        var claim = (await authorization.AuthorizeAsync(http.User, "procurement-plan-claim-scope-all")).Succeeded;
        var submit = (await authorization.AuthorizeAsync(http.User, "procurement-plan-submit")).Succeeded;
        var withdraw = (await authorization.AuthorizeAsync(http.User, "procurement-plan-withdraw")).Succeeded;
        var writeReady = options.Enabled && options.ExecutionDatabaseConfigured
            && (edit || submit || withdraw || claim) && await readiness.IsReadyAsync(ct);
        var policyReady = false;
        if (writeReady && submit)
        {
            try
            {
                policyReady = await transaction.ExecuteAsync(async (db, approvalContext, token) =>
                {
                    var now = DateTimeOffset.UtcNow;
                    var policies = await db.PolicyVersions.AsNoTracking()
                        .Where(x => x.PolicyKey == PlanReviewPolicyRules.PolicyKey && x.State == "Published"
                            && x.EffectiveFrom <= now && (x.EffectiveTo == null || x.EffectiveTo > now))
                        .Take(2).ToArrayAsync(token);
                    if (policies.Length != 1 || PlanReviewPolicyRules.Read(policies[0].RulesJson) is null)
                        return false;
                    _ = await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db,
                        scope, policies[0], token);
                    return true;
                }, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { policyReady = false; }
        }
        return new(edit, claim, submit, withdraw, writeReady, policyReady);
    }

    private static string[] AllowedActions(string status, Guid? branch,
        ProcurementRequestScope scope, PlanActionContext actor)
    {
        var selected = !scope.IsExplicitAllBranches && scope.WorkLocationId.HasValue;
        var actions = new List<string>();
        if (selected && branch == scope.WorkLocationId && actor.Edit && (status is "Draft" or "Returned"))
            actions.Add("Edit");
        if (selected && branch == scope.WorkLocationId && actor.Edit && (status is "Draft" or "Returned"))
            actions.Add("Cancel");
        if (selected && branch is null && (status is "Draft" or "Returned")
            && actor.WriteReady && (actor.Edit || actor.Claim)) actions.Add("ClaimScope");
        if (selected && branch == scope.WorkLocationId && (status is "Draft" or "Returned")
            && actor.WriteReady && actor.Submit && actor.PolicyReady) actions.Add("Submit");
        if (selected && branch == scope.WorkLocationId && (status is "Submitted" or "Reviewed")
            && actor.WriteReady && actor.Withdraw) actions.Add("Withdraw");
        return actions.ToArray();
    }
    private static bool VisibleBranch(ProcurementPlan plan, ProcurementRequestScope scope) =>
        plan.BranchRef is null || scope.IsExplicitAllBranches
            ? plan.BranchRef is null || scope.PermittedWorkLocationIds.Contains(plan.BranchRef.Value)
            : scope.WorkLocationId.HasValue && plan.BranchRef == scope.WorkLocationId;
    private static IResult Problem(string code, string message, int status) =>
        Results.Json(new { code, message }, statusCode: status);
    private static IResult PlanStoreUnavailable() => Problem("plan-store-unavailable",
        "Planning is being updated. Try again after the workspace is ready.", 503);

    public sealed record UpdatePlan(long Version, ProcurementPlanInput Draft);
    public sealed record CancelPlan(long Version, string? Reason);
    public sealed record ClaimPlanScope(long Version, string? ConfirmStoredCurrencyCode, string? Reason);
}
