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
        var participants = reviewers.Append(rules.ClosingOfficerPrincipalId).ToArray();
        var knownWorkers = new HashSet<Guid>();
        foreach (var id in participants)
        {
            var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                scope.LegalEntityId, id, ct);
            var required = id == rules.ClosingOfficerPrincipalId
                ? PrincipalRfqSealedManualOffers.CloseAction
                : PrincipalRfqCommercialRoundPolicyRules.ApprovalAction;
            if (person is null || !person.Permissions.Contains(required))
                return Problem("commercial-round-person-unavailable",
                    "Choose current named commercial reviewers and a closing officer for this company.", 422);
            if (person.CoreLinkedWorkerId is { } worker && !knownWorkers.Add(worker))
                return Problem("commercial-round-person-conflict",
                    "Choose separate current accounts for commercial review and closing.", 422);
        }
        return null;
    }
}
