using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPolicyDraftRoutes
{
    private static async Task<IResult?> CheckRoundReviewCompanyRules(PolicyVersion policy,
        Mightyfin.Erp.Procurement.Infrastructure.Data.ProcurementDbContext db,
        HrmDbContext core, ProcurementRequestScope scope, string permission, CancellationToken ct)
    {
        var rules = CommercialRoundReviewPolicyRules.Read(policy.RulesJson);
        if (rules is null)
            return Problem("invalid-round-review-rules", "Set named approvers and explicit round-plan requirements.", 422);
        if (rules.DocumentsRequired)
            return Problem("round-document-check-unavailable", "Required document checks need the Core document owner integration before this policy can be approved.", 422);
        if (!await PlanPolicyConfigurationActor(core, scope, permission, ct))
            return Problem("round-policy-configuration-denied", "Your current account cannot configure company-wide round review.", 403);
        if (!await core.LegalEntities.AsNoTracking().AnyAsync(x => x.TenantId == scope.TenantId
            && x.Id == scope.LegalEntityId && !x.IsArchived, ct))
            return Problem("company-unavailable", "The selected company is unavailable.", 409);
        foreach (var id in rules.MethodIds)
            if (!await db.ProcurementMethods.AsNoTracking().AnyAsync(x => x.Id == id && x.Status == "Active", ct))
                return Problem("round-method-unavailable", "Choose active buying methods for this policy.", 422);
        if (!rules.CategoryCodes.Contains("*"))
            foreach (var code in rules.CategoryCodes)
                if (!await db.ProcurementCategories.AsNoTracking().AnyAsync(x => x.Code == code && x.Status == "Active", ct))
                    return Problem("round-category-unavailable", "Choose active buying categories for this policy.", 422);
        var people = new List<ProcurementPrincipalPeople.Person>();
        foreach (var id in rules.AssigneePrincipalIds)
        {
            var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                scope.LegalEntityId, id.ToString("D"), ct);
            if (person is null || !person.OrganisationWide
                || !person.Permissions.Contains("procurement-round-plan-review"))
                return Problem("round-policy-assignee-unavailable", "Choose current company-wide round reviewers.", 422);
            foreach (var earlier in people)
                if (!ProcurementPrincipalEvidence.Independent(earlier.PrincipalId,
                    earlier.CoreLinkedWorkerId, person.PrincipalId, person.CoreLinkedWorkerId))
                    return Problem("round-policy-assignee-conflict", "Round reviewers must be independent people.", 422);
            people.Add(person);
        }
        return null;
    }
}
