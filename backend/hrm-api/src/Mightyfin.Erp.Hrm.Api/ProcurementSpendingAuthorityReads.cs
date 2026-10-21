using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSpendingAuthorityRoutes
{
    private static bool TryReadContract(string? action, out ProcurementSpendingAction contract)
    {
        var requested = action ?? SpendingAuthorityConfiguration.ApprovalAction;
        if (requested is "PurchaseRequest.Approve" or "PurchaseOrder.Approve" or "SourcingEvent.Publish")
        { contract = ProcurementSpendingActions.Resolve(requested); return true; }
        contract = ProcurementSpendingActions.PurchaseRequest; return false;
    }

    private static async Task<HashSet<string>> ReadActions(HrmDbContext core, ProcurementRequestScope scope,
        HttpContext http, IAuthorizationService auth, CancellationToken ct)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var permission in new[] { SpendingAuthorityConfiguration.ConfigurePermission,
            SpendingAuthorityConfiguration.ReviewPermission, SpendingAuthorityConfiguration.RevokePermission })
            if (await Actor(core, scope, http, auth, permission, ct) is not null) result.Add(permission);
        return result;
    }

    private static async Task<object> ReadView(ProcurementSpendingAuthorityGrant grant, ProcurementDbContext db,
        HrmDbContext core, ProcurementRequestScope scope, HashSet<string> permissions, CancellationToken ct)
    {
        var actions = new List<string>();
        var blockers = new List<object>();
        Guid? previousId = null; long? previousVersion = null;
        var actor = new SpendingAuthorityConfigurationActor(scope.TenantId, scope.LegalEntityId, scope.SubjectId, true, permissions);
        var subject = await ProcurementAuthorityIdentity.SubjectAsync(core, scope, grant.PrincipalId, ct);
        var now = DateTimeOffset.UtcNow;
        bool Eligible(string action, Action validate)
        {
            try { validate(); return true; }
            catch (InvalidOperationException ex) { blockers.Add(new { action, code = ex.Message }); return false; }
        }
        async Task<bool> References(string action)
        {
            var issue = subject is null ? "AuthoritySubjectUnavailable" : await ProcurementAuthorityIdentity.ReferenceIssueAsync(core,
                db, scope, ProcurementAuthorityIdentity.Draft(grant), true, ct);
            if (issue is null) return true;
            blockers.Add(new { action, code = issue }); return false;
        }
        if (grant.State == "Draft" && grant.ConfigurationOwnerPrincipalId == scope.SubjectId
            && permissions.Contains(SpendingAuthorityConfiguration.ConfigurePermission))
        {
            actions.Add("Edit");
            if (await References("Submit") && Eligible("Submit", () => SpendingAuthorityConfiguration.AssertCanSubmit(
                grant, grant.Version, actor, subject!, now))) actions.Add("Submit");
        }
        if (grant.State == "PendingReview" && permissions.Contains(SpendingAuthorityConfiguration.ReviewPermission))
        {
            try
            {
                await ProcurementAuthorityIdentity.RequireIndependentReviewerAsync(core, scope, grant, ct);
                if (Eligible("Return", () => SpendingAuthorityConfiguration.AssertCanReturn(grant, grant.Version, actor))) actions.Add("Return");
                // A complete internal family is required even when a predecessor is outside
                // caller scope. Never publish from a filtered family or expose hidden IDs.
                var family = await Load(db, grant, ct);
                var current = family.Single(x => x.Id == grant.Id);
                var predecessors = family.Where(x => x.Id != grant.Id && x.State == "Active").ToArray();
                if (predecessors.Length == 0)
                {
                    if (await References("Activate") && Eligible("Activate", () => SpendingAuthorityConfiguration.AssertCanActivate(
                        current, grant.Version, actor, subject!, now, family))) actions.Add("Activate");
                }
                else if (predecessors.Length == 1 && InScope(predecessors[0], scope))
                {
                    var previous = predecessors[0];
                    if (await References("Supersede") && Eligible("Supersede", () => SpendingAuthorityConfiguration.AssertCanSupersede(
                        current, grant.Version, previous, previous.Version, actor, subject!, now, family)))
                    { actions.Add("Supersede"); previousId = previous.Id; previousVersion = previous.Version; }
                }
                else blockers.Add(new { action = "Activate", code = "AuthorityActionUnavailableInCurrentScope" });
            }
            catch (InvalidOperationException ex) when (ex.Message == "IndependentAuthorityReviewerRequired") { }
        }
        // Revocation deliberately follows the existing permission policy; no invented maker/checker requirement.
        if (grant.State == "Active" && permissions.Contains(SpendingAuthorityConfiguration.RevokePermission)
            && Eligible("Revoke", () => SpendingAuthorityConfiguration.AssertCanRevoke(grant, grant.Version, actor))) actions.Add("Revoke");
        var person = await ProcurementAuthorityIdentity.NamedAccountAsync(core, scope, grant.PrincipalId, ct);
        return View(grant, person?.DisplayName, actions.ToArray(), previousId, previousVersion, blockers.ToArray());
    }

    private static async Task<IResult> Lookups(string kind, string? action, string? search, int? offset, int? limit,
        HttpContext http, ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope,
        IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            if (await Actor(core, scope, http, auth, ReadPermission, ct) is null) return AccessDenied();
            if (!TryReadContract(action, out var contract)) return Invalid("AuthorityActionUnsupported");
            var text = (search ?? "").Trim().ToLowerInvariant();
            if (text.Length > 100) return Invalid("AuthorityLookupSearchTooLong");
            var skip = Math.Clamp(offset ?? 0, 0, 100000); var take = Math.Clamp(limit ?? 25, 1, 50);
            var items = new List<object>(); int scanned;
            if (kind == "people")
            {
                var now = DateTimeOffset.UtcNow;
                // All Core boundaries are applied before pagination. A second active login for
                // one worker is excluded, matching execution's canonical identity requirement.
                var query = ProcurementAuthorityIdentity.CanonicalPeopleQuery(core, scope, now)
                    .Where(u => text == "" || u.DisplayName.ToLower().Contains(text));
                var candidates = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Skip(skip).Take(take)
                    .Select(x => new { x.Id, x.DisplayName, x.WorkerId, x.RolesCsv }).ToArrayAsync(ct);
                scanned = candidates.Length;
                foreach (var candidate in candidates)
                {
                    var roles = LocalIdentityRoutes.ParseRoles(candidate.RolesCsv);
                    var permissions = await ProcurementPermissions.ResolveConfiguredActionsAsync(core, candidate.RolesCsv, ct);
                    if (!roles.Contains("erp_admin", StringComparer.OrdinalIgnoreCase)
                        && (!permissions.Contains("procurement-access") || !permissions.Contains(contract.Permission))) continue;
                    if (await ProcurementAuthorityIdentity.NamedAccountAsync(core, scope, candidate.Id.ToString("D"), ct) is null) continue;
                    items.Add(new { principalId = candidate.Id.ToString("D"), candidate.DisplayName, candidate.WorkerId });
                }
            }
            else if (kind == "branches")
            {
                var query = core.WorkLocations.AsNoTracking().Where(x => x.TenantId == scope.TenantId
                    && x.LegalEntityId == scope.LegalEntityId && !x.IsArchived
                    && (scope.WorkLocationId == null || x.Id == scope.WorkLocationId)
                    && (text == "" || x.Name.ToLower().Contains(text) || x.Code.ToLower().Contains(text)));
                if (scope.IsConfined && scope.WorkLocationId is null) query = query.Where(_ => false);
                var rows = await query.OrderBy(x => x.Code).ThenBy(x => x.Id).Skip(skip).Take(take)
                    .Select(x => new { x.Id, x.Code, x.Name }).ToArrayAsync(ct);
                scanned = rows.Length; items.AddRange(rows);
            }
            else if (kind == "departments")
            {
                var date = DateOnly.FromDateTime(DateTime.UtcNow);
                var query = core.OrgUnits.AsNoTracking().Where(x => x.TenantId == scope.TenantId
                    && x.LegalEntityId == scope.LegalEntityId && !x.IsArchived && x.Status == "active"
                    && x.EffectiveFrom <= date && (x.EffectiveTo == null || date <= x.EffectiveTo)
                    && (scope.OrgUnitId == null || x.Id == scope.OrgUnitId)
                    && (text == "" || x.Name.ToLower().Contains(text) || x.Code.ToLower().Contains(text)));
                if (scope.IsConfined && scope.WorkLocationId is null) query = query.Where(_ => false);
                var rows = await query.OrderBy(x => x.Code).ThenBy(x => x.Id).Skip(skip).Take(take)
                    .Select(x => new { x.Id, x.Code, x.Name }).ToArrayAsync(ct);
                scanned = rows.Length; items.AddRange(rows);
            }
            else if (kind == "categories")
            {
                var rows = await db.ProcurementCategories.AsNoTracking().Where(x => x.Status == "Active"
                    && (text == "" || x.Name.ToLower().Contains(text) || x.Code.ToLower().Contains(text)))
                    .OrderBy(x => x.Code).ThenBy(x => x.Id).Skip(skip).Take(take)
                    .Select(x => new { x.Id, x.Code, x.Name }).ToArrayAsync(ct);
                scanned = rows.Length; items.AddRange(rows);
            }
            else return Invalid("AuthorityLookupKindInvalid");
            // Eligibility is rechecked after the bounded Core page. An empty eligible page
            // can have a nextOffset; never misrepresent raw candidates as a total of eligible people.
            return Results.Ok(new { items, offset = skip, limit = take,
                nextOffset = scanned == take && skip + scanned <= 100000 ? (int?)(skip + scanned) : null });
        }
        catch (Exception ex) when (Handled(ex)) { return Failure(ex); }
    }
}
