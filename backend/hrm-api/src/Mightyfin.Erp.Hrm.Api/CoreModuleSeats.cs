using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Api;
internal static class CoreModuleSeats {
 // Only current active membership grants consume a seat; historical/revoked versions do not.
 internal static Task<int> CountAsync(HrmDbContext db,string tenant,Guid company,string module,Guid? excluding,int stop,CancellationToken ct) =>
  (from grant in db.ModuleAccessVersions.IgnoreQueryFilters().AsNoTracking()
   join member in db.CompanyMemberships.IgnoreQueryFilters().AsNoTracking()
    on new{grant.TenantId,Id=grant.MembershipId} equals new{member.TenantId,member.Id}
   where grant.TenantId==tenant && grant.LegalEntityId==company && grant.ModuleCode==module && !grant.IsArchived && grant.State=="granted"
    && member.LegalEntityId==company && member.IsActive && !member.IsArchived && grant.MembershipRevision==member.Revision
    && (excluding==null || member.Id!=excluding)
    && !db.ModuleAccessVersions.IgnoreQueryFilters().Any(newer=>newer.TenantId==tenant && newer.LegalEntityId==company && newer.ModuleCode==module && newer.MembershipId==member.Id && newer.Revision>grant.Revision)
   select member.Id).Distinct().Take(stop).CountAsync(ct);
}
