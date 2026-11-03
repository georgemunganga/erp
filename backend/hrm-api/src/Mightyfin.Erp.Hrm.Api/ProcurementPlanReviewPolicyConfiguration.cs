using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPolicyDraftRoutes
{
    private static async Task<IResult?> CheckPlanReviewCompanyRules(PolicyVersion policy,
        HrmDbContext core, ProcurementRequestScope scope, string permission, CancellationToken ct)
    {
        var rules = PlanReviewPolicyRules.Read(policy.RulesJson);
        if (rules is null)
            return Problem("invalid-plan-review-rules", "Use two named Review and Final stages in the Plan review format.", 422);
        var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct);
        if (actor is null || scope.PrincipalId?.ToString("D") != actor.PrincipalId
            || !actor.Permissions.Contains(permission) || scope.IsExplicitAllBranches || scope.OrgUnitId is not null
            || scope.WorkLocationId is null && (scope.IsConfined || !actor.OrganisationWide)
            || scope.WorkLocationId is { } selected && !actor.OrganisationWide && !actor.AllowedBranches.Contains(selected))
            return Problem("plan-policy-configuration-denied", "Your current account cannot configure Plan review in this scope.", 403);
        var currency = await ProcurementCurrencyDefaults.ForSelectedCompanyAsync(core, scope, ct);
        if (currency is not { Length: 3 } || !currency.All(char.IsAsciiLetterUpper))
            return Problem("currency-unavailable", "The selected company's currency is unavailable.", 409);
        var branches = await core.WorkLocations.AsNoTracking().Where(x => x.TenantId == scope.TenantId
            && x.LegalEntityId == scope.LegalEntityId && !x.IsArchived
            && (scope.WorkLocationId == null || x.Id == scope.WorkLocationId)).Select(x => x.Id).ToArrayAsync(ct);
        var common = branches.ToHashSet();
        var people = new List<ProcurementPrincipalPeople.Person>();
        foreach (var stage in rules.Stages)
        {
            var required = stage.Purpose == "Review" ? PlanReviewCoreWorkflow.ReviewPermission : PlanReviewCoreWorkflow.FinalPermission;
            foreach (var principal in stage.AssigneePrincipalIds)
            {
                var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                    scope.LegalEntityId, principal.ToString("D"), ct);
                if (person is null || !person.Permissions.Contains(required))
                    return Problem("plan-policy-assignee-unavailable", "Choose current named people with the required Plan action.", 422);
                if (!person.OrganisationWide) common.IntersectWith(person.AllowedBranches);
                foreach (var previous in people)
                    if (!ProcurementPrincipalEvidence.Independent(previous.PrincipalId,
                        previous.CoreLinkedWorkerId, person.PrincipalId, person.CoreLinkedWorkerId))
                        return Problem("plan-policy-assignee-conflict", "Plan stages require independent named people.", 422);
                people.Add(person);
            }
        }
        if (common.Count == 0)
            return Problem("plan-policy-branch-unavailable", "The named Plan reviewers must share a current permitted branch in this company.", 422);
        // This proves usable current configuration, never future plan authority.
        // Submit and every decision still recheck the actual plan's branch/people.
        return null;
    }

    private static async Task<IResult> PlanReviewers(string? purpose, string? search, string? principalIds, int? offset,
        int? limit, HttpContext http, [FromServices] HrmDbContext core, [FromServices] ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (purpose is not ("Review" or "Final") || (search?.Trim().Length ?? 0) > 100
            || (offset ?? 0) is < 0 or > 100000 || (limit ?? 25) is < 1 or > 50)
            return Problem("plan-reviewer-query-invalid", "Choose Review or Final and a bounded name search/page.", 422);
        var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct);
        if (actor is null || scope.PrincipalId?.ToString("D") != actor.PrincipalId
            || !actor.Permissions.Contains("procurement-configuration-read")
            || scope.WorkLocationId is { } selected && !actor.OrganisationWide && !actor.AllowedBranches.Contains(selected))
            return Results.Forbid();
        Guid[]? requested = null;
        if (principalIds is not null)
        {
            var values = principalIds.Split(',', StringSplitOptions.TrimEntries);
            if (values.Length is < 1 or > 50 || values.Any(x => !Guid.TryParseExact(x, "D", out var id)
                || id == Guid.Empty || x != id.ToString("D")) || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                return Problem("plan-reviewer-ids-invalid", "Use at most 50 distinct canonical UUIDs separated by commas.", 422);
            requested = values.Select(Guid.Parse).ToArray();
        }
        var permission = purpose == "Review" ? PlanReviewCoreWorkflow.ReviewPermission : PlanReviewCoreWorkflow.FinalPermission;
        var text = (search ?? "").Trim().ToLowerInvariant();
        var query = ProcurementPrincipalPeople.Candidates(core, scope, DateTimeOffset.UtcNow)
            .Where(x => text == "" || x.DisplayName.ToLower().Contains(text));
        if (requested is not null) query = query.Where(x => requested.Contains(x.Id));
        if (!actor.OrganisationWide && scope.WorkLocationId is null)
        {
            var visible = actor.AllowedBranches.ToArray();
            if (scope.IsExplicitAllBranches) visible = visible.Intersect(scope.PermittedWorkLocationIds).ToArray();
            query = query.Where(u => !core.UserBranchAssignments.Any(a => a.TenantId == scope.TenantId
                    && a.UserId == u.Id && !a.IsArchived)
                || core.UserBranchAssignments.Any(a => a.TenantId == scope.TenantId && a.UserId == u.Id
                    && !a.IsArchived && visible.Contains(a.LocationId)));
        }
        // Bound current-account resolution before filtering/counting eligible
        // people; report a typed narrow-search response rather than a false total.
        var candidates = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id)
            .Take(201).Select(x => new { x.Id, x.DisplayName }).ToArrayAsync(ct);
        if (candidates.Length > 200)
            return Problem("plan-reviewer-search-required", "Narrow the name search to at most 200 candidates.", 422);
        var items = new List<object>();
        foreach (var row in candidates)
        {
            var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, row.Id.ToString("D"), ct);
            if (person is not null && person.Permissions.Contains(permission)
                && (scope.WorkLocationId is not { } branch || person.OrganisationWide || person.AllowedBranches.Contains(branch)))
                items.Add(new { principalId = row.Id, row.DisplayName });
        }
        return Results.Ok(new { items = items.Skip(offset ?? 0).Take(limit ?? 25).ToArray(),
            purpose, candidateTotal = items.Count, offset = offset ?? 0,
            limit = limit ?? 25, isAuthority = false });
    }
}
