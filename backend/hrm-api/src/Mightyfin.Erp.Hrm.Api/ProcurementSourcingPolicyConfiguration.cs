using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPolicyDraftRoutes
{
    private static async Task<bool> SourcingActorCoversRules(HrmDbContext core, ProcurementRequestScope scope,
        string json, CancellationToken ct)
    {
        if (SourcingPublicationPolicyRules.ValidateDraft(json).Count != 0 || !Guid.TryParse(scope.SubjectId, out var actorId)) return false;
        var assigned = await core.UserBranchAssignments.AsNoTracking().Where(x => x.UserId == actorId && !x.IsArchived)
            .Select(x => x.LocationId).ToArrayAsync(ct);
        if (assigned.Length == 0) return true; // Core organisation-wide semantics, irrespective of shell selection.
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("rules").EnumerateArray().All(rule =>
        {
            var sites = rule.GetProperty("when").GetProperty("branchIds").EnumerateArray().Select(x => x.GetGuid()).ToArray();
            return sites.Length != 0 && sites.All(assigned.Contains);
        });
    }

    private static async Task<IResult?> CheckSourcingCompanyRules(PolicyVersion policy, ProcurementDbContext db,
        HrmDbContext core, ProcurementRequestScope scope, string permission, CancellationToken ct, bool draftSyntaxOnly = false)
    {
        // The existing configuration permission is NOT a publication permission or a finite financial grant.
        if (!await ReceivingConfigurationActor(core, scope, permission, ct))
            return Problem("sourcing-configuration-unavailable", "Your current account cannot configure sourcing rules.", 403);
        var issues = draftSyntaxOnly ? SourcingPublicationPolicyRules.ValidateDraft(policy.RulesJson) : SourcingPublicationPolicyRules.Validate(policy.RulesJson);
        if (issues.Count != 0)
            return Results.Json(new { code = "invalid-sourcing-rules", message = "Correct the sourcing publication rules.", issues }, statusCode: 422);
        using var document = JsonDocument.Parse(policy.RulesJson);
        var root = document.RootElement;
        var rulesSchema = root.GetProperty("schemaVersion").GetInt32();
        var currency = await core.LegalEntities.AsNoTracking().Where(x => x.TenantId == scope.TenantId
            && x.Id == scope.LegalEntityId && !x.IsArchived).Select(x => x.Currency).SingleOrDefaultAsync(ct);
        if (currency is null) return Problem("company-unavailable", "The selected company is unavailable.", 409);
        if (currency != root.GetProperty("currencyCode").GetString())
            return Problem("currency-mismatch", "Use the selected company's currency.", 422);
        // A named reviewer may be invalid too, but the maker's current Core
        // branch coverage is an access decision. Resolve it before looking up
        // the reviewers so a confined maker cannot inspect their eligibility.
        if (!await SourcingActorCoversRules(core, scope, policy.RulesJson, ct))
            return Problem("sourcing-policy-scope", "Company-wide rules require company-wide configuration access.", 403);
        var rules = root.GetProperty("rules").EnumerateArray().ToArray();
        var principalIds = rules.SelectMany(x => x.GetProperty("result").GetProperty("approvalStages").EnumerateArray())
            .SelectMany(x => x.GetProperty("principalIds").EnumerateArray()).Select(x => x.GetString()!)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (principalIds.Length > 100)
            return Problem("sourcing-reviewer-bound", "Use at most 100 distinct named reviewers in a policy version.", 422);
        var retainedSchema = await db.SourcingPolicyIdentityEvidenceRecords.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                && x.PolicyVersionId == policy.Id && x.Stage == "Created")
            .Select(x => (int?)x.SchemaVersion).SingleOrDefaultAsync(ct);
        // Identity 2 may coexist with rules 1 in retained source histories.
        // Preserve that protocol without rewriting evidence.
        if (retainedSchema == 1 && rulesSchema != 1)
            return Problem("sourcing-policy-schema-locked", "Start a new policy version to change the sourcing rules format.", 409);
        var identitySchema = retainedSchema ?? rulesSchema;
        var accounts = new Dictionary<string, ProcurementPrincipalPeople.Person>(StringComparer.Ordinal);
        foreach (var id in principalIds)
        {
            var account = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, id, ct);
            // Schema 1 is retained configuration approval, not an RFQ publication
            // grant. Operational publication independently checks current action.
            if (account is null || identitySchema == 2 && !account.Permissions.Contains("procurement-sourcing-publish"))
                return Problem("sourcing-reviewer-unavailable", "Each named reviewer needs a current authorized Core account in this company.", 422);
            if (identitySchema == 1 && (account.WorkerId is not { } currentWorker
                || (await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, scope.TenantId,
                    scope.LegalEntityId, id, ct))?.WorkerId != currentWorker))
                return Problem("sourcing-reviewer-unavailable", "Each named reviewer needs an active canonical worker for this retained policy.", 422);
            accounts.Add(id, account);
        }
        var knownWorkers = accounts.Values.Select(x => x.CoreLinkedWorkerId).Where(x => x is not null).ToArray();
        if (knownWorkers.Distinct().Count() != knownWorkers.Length)
            return Problem("sourcing-reviewer-unavailable", "Choose distinct current people, without account aliases.", 422);
        var userIds = principalIds.Select(Guid.Parse).Append(Guid.Parse(scope.SubjectId)).Distinct().ToArray();
        var assignments = await core.UserBranchAssignments.AsNoTracking().Where(x => userIds.Contains(x.UserId) && !x.IsArchived)
            .Select(x => new { x.UserId, x.LocationId }).ToArrayAsync(ct);
        var actorSites = assignments.Where(x => x.UserId == Guid.Parse(scope.SubjectId)).Select(x => x.LocationId).ToHashSet();
        foreach (var rule in rules)
        {
            var when = rule.GetProperty("when");
            var sites = when.GetProperty("branchIds").EnumerateArray().Select(x => x.GetGuid()).ToArray();
            // Empty assignments are inherited Core organisation-wide scope, NOT denial.
            // Shell branch/department selection alone is context, not a loss of that organisation-wide authority.
            if (sites.Length == 0 && actorSites.Count != 0 || sites.Any(x => actorSites.Count != 0 && !actorSites.Contains(x)))
                return Problem("sourcing-policy-scope", "Company-wide rules require company-wide configuration access.", 403);
            foreach (var site in sites)
                if (!await core.WorkLocations.AsNoTracking().AnyAsync(x => x.Id == site && x.TenantId == scope.TenantId
                    && x.LegalEntityId == scope.LegalEntityId && !x.IsArchived, ct))
                    return Problem("sourcing-site-unavailable", "Choose active branches in this company.", 422);
            foreach (var category in when.GetProperty("categoryCodes").EnumerateArray())
            {
                var code = category.GetString()!;
                if (!await db.ProcurementCategories.AsNoTracking().AnyAsync(x => x.Code == code && x.Status == "Active", ct))
                    return Problem("sourcing-category-unavailable", "Choose active procurement categories in this company.", 422);
            }
            foreach (var id in rule.GetProperty("result").GetProperty("approvalStages").EnumerateArray()
                .SelectMany(x => x.GetProperty("principalIds").EnumerateArray()).Select(x => x.GetString()!).Distinct(StringComparer.Ordinal))
            {
                var reviewerId = Guid.Parse(id);
                var reviewerSites = assignments.Where(x => x.UserId == reviewerId).Select(x => x.LocationId).ToHashSet();
                if (sites.Length == 0 && reviewerSites.Count != 0 || sites.Any(x => reviewerSites.Count != 0 && !reviewerSites.Contains(x)))
                    return Problem("sourcing-reviewer-scope", "Each named reviewer must have the current branch scope required by its rule.", 422);
            }
        }
        return null;
    }
}
