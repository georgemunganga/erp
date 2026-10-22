using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
namespace Mightyfin.Erp.Hrm.Api;
internal static class ProcurementInvoiceUnitReviewPeople
{
    internal static async Task<InvoiceUnitReviewPolicyRules.Person?> Resolve(HrmDbContext core, string tenant, Guid company, string principal, CancellationToken ct)
    {
        var person = await ProcurementAuthorityIdentity.ResolveCanonicalAccountAsync(core, tenant, company, principal, ct);
        if (person is null || await ProcurementAuthorityIdentity.ResolveCanonicalWorkerAccountAsync(core, tenant, company, person.WorkerId, ct) is not { } worker || worker.PrincipalId != principal) return null;
        var roles = LocalIdentityRoutes.ParseRoles(person.RolesCsv);
        var permissions = roles.Contains("erp_admin", StringComparer.OrdinalIgnoreCase)
            ? ProcurementActionCatalog.BootstrapGrants.Keys.ToHashSet(StringComparer.Ordinal)
            : (await ProcurementPermissions.ResolveConfiguredActionsAsync(core, person.RolesCsv, ct)).ToHashSet(StringComparer.Ordinal);
        var id = Guid.Parse(principal);
        var declared = await core.UserBranchAssignments.AsNoTracking().Where(x => x.UserId == id && !x.IsArchived).Select(x => x.LocationId).Distinct().ToArrayAsync(ct);
        var branches = await core.WorkLocations.AsNoTracking().Where(x => declared.Contains(x.Id) && x.LegalEntityId == company && !x.IsArchived).Select(x => x.Id).ToArrayAsync(ct);
        return new(tenant, company, principal, person.WorkerId, true, permissions, declared.Length == 0, branches.ToHashSet());
    }
}
