using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSpendingAuthorityRoutes
{
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
        Guid? previousId = null; long? previousVersion = null;
        if (grant.State == "Draft" && grant.ConfigurationOwnerPrincipalId == scope.SubjectId
            && permissions.Contains(SpendingAuthorityConfiguration.ConfigurePermission)) actions.AddRange(["Edit", "Submit"]);
        if (grant.State == "PendingReview" && permissions.Contains(SpendingAuthorityConfiguration.ReviewPermission))
        {
            try
            {
                await ProcurementAuthorityIdentity.RequireIndependentReviewerAsync(core, scope, grant, ct);
                actions.Add("Return");
                var previous = await Scoped(db, scope).AsNoTracking().Where(x => x.Id != grant.Id
                    && x.PrincipalId == grant.PrincipalId && x.Permission == grant.Permission && x.Action == grant.Action
                    && x.AuthorityReference == grant.AuthorityReference && x.State == "Active")
                    .Select(x => new { x.Id, x.Version }).Take(2).ToArrayAsync(ct);
                if (previous.Length == 0) actions.Add("Activate");
                else if (previous.Length == 1)
                { actions.Add("Supersede"); previousId = previous[0].Id; previousVersion = previous[0].Version; }
            }
            catch (InvalidOperationException ex) when (ex.Message == "IndependentAuthorityReviewerRequired") { }
        }
        // Revocation deliberately follows the existing permission policy; no invented maker/checker requirement.
        if (grant.State == "Active" && permissions.Contains(SpendingAuthorityConfiguration.RevokePermission)) actions.Add("Revoke");
        var person = await ProcurementAuthorityIdentity.NamedAccountAsync(core, scope, grant.PrincipalId, ct);
        return View(grant, person?.DisplayName, actions.ToArray(), previousId, previousVersion);
    }

    private static async Task<IResult> Lookups(string kind, string? search, int? offset, int? limit,
        HttpContext http, ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope,
        IAuthorizationService auth, CancellationToken ct)
    {
        try
        {
            if (await Actor(core, scope, http, auth, ReadPermission, ct) is null) return AccessDenied();
            var text = (search ?? "").Trim().ToLowerInvariant();
            if (text.Length > 100) return Invalid("AuthorityLookupSearchTooLong");
            var skip = Math.Clamp(offset ?? 0, 0, 100000); var take = Math.Clamp(limit ?? 25, 1, 50);
            var items = new List<object>(); int scanned;
            if (kind == "people")
            {
                var now = DateTimeOffset.UtcNow;
                // All Core boundaries are applied before pagination. A second active login for
                // one worker is excluded, matching execution's canonical identity requirement.
                var query = core.LocalUsers.AsNoTracking().Where(u => u.TenantId == scope.TenantId
                    && u.IsActive && !u.IsArchived && u.WorkerId != null && (u.LockedUntil == null || u.LockedUntil <= now)
                    && !core.LocalUsers.Any(other => other.TenantId == scope.TenantId && other.IsActive
                        && !other.IsArchived && other.WorkerId == u.WorkerId && other.Id != u.Id)
                    && core.Workers.Any(w => w.TenantId == scope.TenantId && w.Id == u.WorkerId && !w.IsArchived && w.Status == "active"
                        && (scope.WorkLocationId == null || w.LocationId == scope.WorkLocationId)
                        && (scope.OrgUnitId == null || w.OrgUnitId == scope.OrgUnitId)
                        && (w.LocationId != null && core.WorkLocations.Any(b => b.TenantId == scope.TenantId
                                && b.Id == w.LocationId && b.LegalEntityId == scope.LegalEntityId && !b.IsArchived)
                            || w.LocationId == null && w.OrgUnitId != null && core.OrgUnits.Any(d => d.TenantId == scope.TenantId
                                && d.Id == w.OrgUnitId && d.LegalEntityId == scope.LegalEntityId && !d.IsArchived && d.Status == "active"))
                        && (w.OrgUnitId == null || core.OrgUnits.Any(d => d.TenantId == scope.TenantId
                            && d.Id == w.OrgUnitId && d.LegalEntityId == scope.LegalEntityId && !d.IsArchived && d.Status == "active")))
                    && (text == "" || u.DisplayName.ToLower().Contains(text)));
                if (scope.IsConfined && scope.WorkLocationId is null) query = query.Where(_ => false);
                var candidates = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Skip(skip).Take(take)
                    .Select(x => new { x.Id, x.DisplayName, x.WorkerId, x.RolesCsv }).ToArrayAsync(ct);
                scanned = candidates.Length;
                foreach (var candidate in candidates)
                {
                    var roles = LocalIdentityRoutes.ParseRoles(candidate.RolesCsv);
                    var permissions = await ProcurementPermissions.ResolveConfiguredActionsAsync(core, candidate.RolesCsv, ct);
                    if (!roles.Contains("erp_admin", StringComparer.OrdinalIgnoreCase)
                        && (!permissions.Contains("procurement-access") || !permissions.Contains(SpendingAuthorityConfiguration.ApprovalPermission))) continue;
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
                nextOffset = scanned == take ? (int?)(skip + scanned) : null });
        }
        catch (Exception ex) when (Handled(ex)) { return Failure(ex); }
    }
}
