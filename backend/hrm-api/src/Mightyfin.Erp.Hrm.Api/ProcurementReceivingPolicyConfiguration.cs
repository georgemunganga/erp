using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPolicyDraftRoutes
{
    private static async Task<bool> ReceivingConfigurationActor(HrmDbContext core, ProcurementRequestScope scope, string permission, CancellationToken ct)
    {
        var account = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct);
        if (account is null || await ProcurementAuthorityIdentity.ResolveCanonicalWorkerAccountAsync(core, scope.TenantId, scope.LegalEntityId, account.WorkerId, ct) is null) return false;
        var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(core, account.RolesCsv, ct);
        return LocalIdentityRoutes.ParseRoles(account.RolesCsv).Contains("erp_admin", StringComparer.OrdinalIgnoreCase)
            || actions.Contains("procurement-access", StringComparer.Ordinal) && actions.Contains(permission, StringComparer.Ordinal);
    }
    private static async Task<bool> ReceivingIndependentReviewer(HrmDbContext core, ProcurementRequestScope scope, PolicyVersion policy, CancellationToken ct)
    {
        if (!await ReceivingConfigurationActor(core, scope, "procurement-configuration-approve", ct)) return false;
        var reviewer = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct);
        foreach (var subject in new[] { policy.CreatedBy, policy.ReviewRequestedBy }.Distinct())
        {
            if (string.IsNullOrWhiteSpace(subject)) return false;
            var maker = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, scope.TenantId, scope.LegalEntityId, subject, ct);
            if (maker is null || maker.WorkerId == reviewer!.WorkerId
                || await ProcurementAuthorityIdentity.ResolveCanonicalWorkerAccountAsync(core, scope.TenantId, scope.LegalEntityId, maker.WorkerId, ct) is null) return false;
        }
        return true;
    }
    private static async Task<IResult?> CheckReceivingCompanyRules(PolicyVersion policy, ProcurementDbContext db, HrmDbContext core,
        ProcurementRequestScope scope, string permission, CancellationToken ct)
    {
        if (!await ReceivingConfigurationActor(core, scope, permission, ct)) return Problem("receiving-authority-unavailable", "Your current account cannot configure receiving rules.", 403);
        var issues = ReceivingPolicyRules.Validate(policy.RulesJson);
        if (issues.Count != 0) return Results.Json(new { code = "invalid-receiving-rules", message = "Correct the receiving rules before review.", issues }, statusCode: 422);
        var document = ReceivingPolicyRules.Parse(policy.RulesJson);
        var currency = await core.LegalEntities.AsNoTracking().Where(x => x.TenantId == scope.TenantId && x.Id == scope.LegalEntityId && !x.IsArchived).Select(x => x.Currency).SingleOrDefaultAsync(ct);
        if (currency != document.CurrencyCode) return Problem("currency-mismatch", "Use the selected company's currency.", 422);
        var actorId = Guid.Parse(scope.SubjectId);
        var permittedSites = await core.UserBranchAssignments.AsNoTracking().Where(x => x.UserId == actorId && !x.IsArchived).Select(x => x.LocationId).ToArrayAsync(ct);
        foreach (var rule in document.Rules)
        {
            if (!await db.ProcurementCategories.AnyAsync(x => x.Code == rule.CategoryCode && x.Status == "Active", ct)) return Problem("receiving-category-unavailable", "Choose an active procurement category.", 422);
            foreach (var site in rule.SiteIds)
                if (scope.WorkLocationId is { } selected && site != selected || permittedSites.Length != 0 && !permittedSites.Contains(site)
                    || !await core.WorkLocations.AsNoTracking().AnyAsync(x => x.Id == site && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId && !x.IsArchived, ct))
                    return Problem("receiving-site-unavailable", "Choose current authorized locations in this company.", 422);
            foreach (var worker in rule.InspectorWorkerIds.Concat(rule.ServiceOwnerWorkerIds).Distinct())
                if (await ProcurementAuthorityIdentity.ResolveCanonicalWorkerAccountAsync(core, scope.TenantId, scope.LegalEntityId, worker, ct) is null)
                    return Problem("receiving-worker-unavailable", "Each assigned person needs one active canonical account in this company.", 422);
        }
        return null;
    }
}
