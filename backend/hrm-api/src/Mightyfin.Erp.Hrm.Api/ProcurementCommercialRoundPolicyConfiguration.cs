using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPolicyDraftRoutes
{
    private static async Task<IResult?> CommercialPolicyActor(
        string policyKey, HrmDbContext core, ProcurementRequestScope scope,
        string action, CancellationToken ct)
    {
        if (policyKey != PrincipalRfqCommercialRoundPolicyRules.PolicyKey)
            return null;
        var actor = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, scope.SubjectId, ct);
        if (actor is null || scope.PrincipalId is not { } principal
            || actor.PrincipalId != principal.ToString("D")
            || !actor.Permissions.Contains(action))
            return Problem("commercial-policy-action-denied",
                "Your current account cannot change these supplier-response controls.", 403);
        return null;
    }

    private static async Task<IResult?> CheckCommercialCompanyRules(
        PolicyVersion policy, HrmDbContext core,
        ProcurementRequestScope scope, string genericPermission,
        CancellationToken ct)
    {
        var action = genericPermission == "procurement-configuration-approve"
            ? PrincipalRfqCommercialRoundPolicyRules.ApprovalAction
            : PrincipalRfqCommercialRoundPolicyRules.RequestAction;
        if (await CommercialPolicyActor(policy.PolicyKey, core, scope, action, ct) is { } denied)
            return denied;
        var issues = PrincipalRfqCommercialRoundPolicyRules.Validate(policy.RulesJson);
        if (issues.Count != 0)
            return Results.Json(new { code = "invalid-commercial-round-rules", issues },
                statusCode: 422);
        if (!await core.LegalEntities.AsNoTracking().AnyAsync(x =>
                x.TenantId == scope.TenantId && x.Id == scope.LegalEntityId
                && !x.IsArchived, ct))
            return Problem("company-unavailable", "The selected ERP company is unavailable.", 409);
        var rules = PrincipalRfqCommercialRoundPolicyRules.Parse(policy.RulesJson);
        var reviewers = rules.Stages.SelectMany(x => x.PrincipalIds)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (reviewers.Contains(rules.ClosingOfficerPrincipalId, StringComparer.Ordinal))
            return Problem("commercial-round-person-conflict",
                "Choose a closing officer who is separate from the reviewers.", 422);
        if (!await CommercialPolicyPeopleAvailable(core, scope, rules, ct))
            return Problem("commercial-round-person-unavailable",
                "Choose current independent officers and named readers with the required company access.", 422);
        return null;
    }
    internal static async Task<bool> CommercialPolicyPeopleAvailable(HrmDbContext core,
        ProcurementRequestScope scope, PrincipalRfqCommercialRoundPolicy rules, CancellationToken ct)
    {
        if (rules.OfferOpening is not null && scope.OrgUnitId is not null) return false;
        var required = rules.Stages.SelectMany(x => x.PrincipalIds).Select(x =>
            (Id: x, Action: PrincipalRfqCommercialRoundPolicyRules.ApprovalAction))
            .Append((rules.ClosingOfficerPrincipalId, PrincipalRfqSealedManualOffers.CloseAction)).ToList();
        if (rules.OfferOpening is { } opening)
        {
            required.Add((opening.OfficerPrincipalId, PrincipalRfqOfferOpeningControls.OpenAction));
            required.AddRange(opening.ReaderPrincipalIds.Select(x => (x, PrincipalRfqOfferOpeningControls.ReadAction)));
        }
        var knownWorkers = new HashSet<Guid>();
        foreach (var group in required.GroupBy(x => x.Id, StringComparer.Ordinal))
        {
            var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                scope.LegalEntityId, group.Key, ct);
            if (person is null || group.Any(x => !person.Permissions.Contains(x.Action))
                || rules.OfferOpening is not null && scope.WorkLocationId is { } branch
                    && !person.OrganisationWide && !person.AllowedBranches.Contains(branch))
                return false;
            var principalId = Guid.ParseExact(group.Key, "D");
            var alias = await core.LocalUsers.AsNoTracking().Where(x => x.TenantId == scope.TenantId && x.Id == principalId)
                .Select(x => x.WorkerId).SingleAsync(ct);
            if (!ProcurementPrincipalEvidence.OptionalWorker(alias)
                || alias is { } worker && !knownWorkers.Add(worker)) return false;
        }
        return true;
    }

}
