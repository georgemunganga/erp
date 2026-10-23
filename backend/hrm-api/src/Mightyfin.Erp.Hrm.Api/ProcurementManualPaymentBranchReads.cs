using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementManualPaymentRoutes
{
    // Core owns branches. A payment-only user can choose an explicit transaction
    // branch under an All branches read context without HRM administrator access.
    private static async Task<IResult> BranchLookup(HttpContext http, ProcurementRequestScope scope,
        ProcurementApprovalTransaction transaction, IAuthorizationService authorization,
        CancellationToken ct)
        => await Run(http, scope, transaction, authorization, async (_, _, core, actor, assignedBranches, token) =>
        {
            var canDraft = false;
            foreach (var action in new[] { "procurement-payment-create", "procurement-payment-edit", "procurement-payment-submit" })
                if (actor.Permissions.Contains(action)
                    && (await authorization.AuthorizeAsync(http.User, null, action)).Succeeded)
                { canDraft = true; break; }
            if (!canDraft || scope.CoreLinkedWorkerId != actor.CoreLinkedWorkerId)
                throw new UnauthorizedAccessException("ManualPaymentDraftActionRequired");
        if (scope.OrgUnitId is not null || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches)
                throw new UnauthorizedAccessException("ManualPaymentCurrentScopeRequired");

            var query = core.WorkLocations.AsNoTracking().Where(x => x.TenantId == scope.TenantId
                && x.LegalEntityId == scope.LegalEntityId && !x.IsArchived);
            if (scope.WorkLocationId is { } selected) query = query.Where(x => x.Id == selected);
            if (!actor.OrganisationWide)
            {
                var allowed = actor.AllowedBranches.Intersect(assignedBranches).ToArray();
                if (allowed.Length == 0) throw new UnauthorizedAccessException("ManualPaymentCurrentScopeRequired");
                query = query.Where(x => allowed.Contains(x.Id));
            }
            var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Code).ThenBy(x => x.Id)
                .Select(x => new { x.Id, x.Code, x.Name }).ToArrayAsync(token);
            return new { items };
        }, ct);
}
