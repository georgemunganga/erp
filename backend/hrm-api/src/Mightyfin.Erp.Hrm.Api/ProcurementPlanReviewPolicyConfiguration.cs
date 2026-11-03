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
            || !actor.Permissions.Contains(permission) || !actor.OrganisationWide)
            return Problem("plan-policy-configuration-denied", "Your current account cannot configure Plan review in this scope.", 403);
        var currency = await ProcurementCurrencyDefaults.ForSelectedCompanyAsync(core, scope, ct);
        if (currency is not { Length: 3 } || !currency.All(char.IsAsciiLetterUpper))
            return Problem("currency-unavailable", "The selected company's currency is unavailable.", 409);
        var people = new List<ProcurementPrincipalPeople.Person>();
        foreach (var stage in rules.Stages)
        {
            var required = stage.Purpose == "Review" ? PlanReviewCoreWorkflow.ReviewPermission : PlanReviewCoreWorkflow.FinalPermission;
            foreach (var principal in stage.AssigneePrincipalIds)
            {
                var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                    scope.LegalEntityId, principal.ToString("D"), ct);
                if (person is null || !person.OrganisationWide || !person.Permissions.Contains(required))
                    return Problem("plan-policy-assignee-unavailable", "Choose current company-wide people with the required Plan action.", 422);
                foreach (var previous in people)
                    if (!ProcurementPrincipalEvidence.Independent(previous.PrincipalId,
                        previous.CoreLinkedWorkerId, person.PrincipalId, person.CoreLinkedWorkerId))
                        return Problem("plan-policy-assignee-conflict", "Plan stages require independent named people.", 422);
                people.Add(person);
            }
        }
        // Schema2 has no branch conditions: both configuration and named
        // stage coverage must be company-wide, as in other global policy families.
        // Actual plans still recheck their own branch/current people at use.
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
            || !actor.Permissions.Contains("procurement-configuration-read") || !actor.OrganisationWide)
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
        // Global configuration authority comes from current Core grants;
        // shell branch/department selection must not narrow that directory.
        // The shared Candidates defaults remain unchanged for other consumers.
        var lookupScope = new ProcurementRequestScope();
        lookupScope.Set(scope.TenantId, scope.LegalEntityId, scope.SubjectId, null, null, false, "global-plan-policy-lookup");
        var query = ProcurementPrincipalPeople.Candidates(core, lookupScope, DateTimeOffset.UtcNow)
            .Where(x => text == "" || x.DisplayName.ToLower().Contains(text));
        if (requested is not null) query = query.Where(x => requested.Contains(x.Id));
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
            if (person is not null && person.OrganisationWide && person.Permissions.Contains(permission))
                items.Add(new { principalId = row.Id, row.DisplayName });
        }
        return Results.Ok(new { items = items.Skip(offset ?? 0).Take(limit ?? 25).ToArray(),
            purpose, candidateTotal = items.Count, offset = offset ?? 0,
            limit = limit ?? 25, isAuthority = false });
    }
}
