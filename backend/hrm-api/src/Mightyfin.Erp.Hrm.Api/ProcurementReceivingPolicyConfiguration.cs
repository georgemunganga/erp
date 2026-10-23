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
        var principal = await ProcurementCorePrincipals.CurrentAsync(core, scope.TenantId, scope.SubjectId, ct);
        return principal is not null && scope.PrincipalId == principal.Id
            && ProcurementPermissions.Allows(permission, principal.Claims(scope.TenantId));
    }
    private static async Task<IResult?> CheckReceivingCompanyRules(PolicyVersion policy, ProcurementDbContext db, HrmDbContext core,
        ProcurementRequestScope scope, string permission, CancellationToken ct)
    {
        if (!await ReceivingConfigurationActor(core, scope, permission, ct)) return Problem("receiving-authority-unavailable", "Your current account cannot configure receiving rules.", 403);
        if(PrincipalReceivingPolicyRules.IsDocument(policy.RulesJson))
            return await CheckPrincipalReceivingCompanyRules(policy,db,core,scope,ct);
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
    private static async Task<IResult?> CheckPrincipalReceivingCompanyRules(PolicyVersion policy,ProcurementDbContext db,
        HrmDbContext core,ProcurementRequestScope scope,CancellationToken ct)
    {
        var issues=PrincipalReceivingPolicyRules.Validate(policy.RulesJson);
        if(issues.Count!=0)return Results.Json(new{code="invalid-receiving-rules",message="Correct the receiving rules before review.",issues},statusCode:422);
        if(scope.OrgUnitId is not null)return Problem("receiving-scope-unsupported","Department-scoped receiving rules are unavailable.",422);
        var document=PrincipalReceivingPolicyRules.Parse(policy.RulesJson);
        var maker=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,scope.SubjectId,ct);
        if(maker is null || maker.CoreLinkedWorkerId!=scope.CoreLinkedWorkerId)return Problem("receiving-authority-unavailable","Your current account cannot configure receiving rules.",403);
        var currency=await core.LegalEntities.AsNoTracking().Where(x=>x.TenantId==scope.TenantId&&x.Id==scope.LegalEntityId&&!x.IsArchived).Select(x=>x.Currency).SingleOrDefaultAsync(ct);
        if(currency!=document.CurrencyCode)return Problem("currency-mismatch","Use the selected company's currency.",422);
        foreach(var rule in document.Rules)
        {
            if(!await db.ProcurementCategories.AnyAsync(x=>x.Code==rule.CategoryCode&&x.Status=="Active",ct))
                return Problem("receiving-category-unavailable","Choose an active procurement category.",422);
            foreach(var site in rule.SiteIds)
                if(scope.WorkLocationId is {} selected&&selected!=site || !maker.OrganisationWide&&!maker.AllowedBranches.Contains(site)
                    || !await core.WorkLocations.AsNoTracking().AnyAsync(x=>x.Id==site&&x.TenantId==scope.TenantId&&x.LegalEntityId==scope.LegalEntityId&&!x.IsArchived,ct))
                    return Problem("receiving-site-unavailable","Choose current authorized locations in this company.",422);
            foreach(var principal in rule.InspectorPrincipalIds.Concat(rule.AcceptorPrincipalIds).Concat(rule.ServiceOwnerPrincipalIds).Distinct())
            {
                // Names are configured Core accounts, not employee IDs or grants.
                // Current action eligibility is re-evaluated when each actor acts.
                var person=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,principal,ct);
                if(person is null || !person.OrganisationWide&&rule.SiteIds.Any(site=>!person.AllowedBranches.Contains(site)))
                    return Problem("receiving-person-unavailable","Choose current Core accounts within every configured site.",422);
            }
        }
        return null;
    }

}
