using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Company governance only. This result never authenticates a session or grants module actions.</summary>
internal sealed record CoreCompanyAccessDecision(Guid CompanyId, Guid PrincipalId, string TenantId, string Role)
{
    internal bool MayManageTeam => Role is CoreCompanyRoles.Owner or CoreCompanyRoles.Administrator;
    internal bool MayManageOwners => Role == CoreCompanyRoles.Owner;
}

/// <summary>Exact verified identity + active explicit membership + current purchased slot.</summary>
internal sealed class CoreCompanyAccess(HrmDbContext db, TimeProvider clock)
{
    internal async Task<CoreCompanyAccessDecision?> ReadAsync(string issuer, string subject, Guid linkId,
        Guid companyId, CommercialLicenseLease license, CancellationToken cancellation = default)
    {
        if (issuer != ProductCommercialLicense.Issuer || !Regex.IsMatch(subject, "^[1-9][0-9]{0,18}\\z")
            || linkId == Guid.Empty || companyId == Guid.Empty || !license.IsCurrent) return null;
        // Tenant filter bypass is confined to an exact verified issuer/subject/link.
        // Company/payer/email supplied by a caller can never establish membership.
        var row = await (from link in db.ExternalIdentityLinks.IgnoreQueryFilters().AsNoTracking()
            join user in db.LocalUsers.IgnoreQueryFilters().AsNoTracking()
                on new { link.TenantId, Id = link.LocalUserId } equals new { user.TenantId, user.Id }
            join member in db.CompanyMemberships.IgnoreQueryFilters().AsNoTracking()
                on new { user.TenantId, UserId = user.Id } equals new { member.TenantId, UserId = member.LocalUserId }
            join company in db.LegalEntities.IgnoreQueryFilters().AsNoTracking()
                on new { member.TenantId, Id = member.LegalEntityId } equals new { company.TenantId, company.Id }
            join binding in db.WorkspaceBindings.IgnoreQueryFilters().AsNoTracking()
                on new { company.TenantId, CompanyId = company.Id } equals new { binding.TenantId, CompanyId = binding.LegalEntityId }
            where link.Id == linkId && link.Issuer == issuer && link.Subject == subject
                && link.IsActive && !link.IsArchived && user.IsActive && !user.IsArchived
                && user.CredentialMode == LocalCredentialModes.Federated
                && member.LegalEntityId == companyId && member.IsActive && !member.IsArchived
                && !company.IsArchived && !binding.IsArchived && binding.Issuer == issuer
            select new { company.Id, PrincipalId = user.Id, member.TenantId, member.Role, user.LockedUntil,
                binding.EnrollmentId, binding.Slot }).SingleOrDefaultAsync(cancellation);
        if (row is null || !CoreCompanyRoles.IsValid(row.Role) || row.EnrollmentId != license.EnrollmentId
            || row.LockedUntil is { } until && until > clock.GetUtcNow()
            || !license.IsCurrent || !license.AllowsWorkspace(row.Slot)) return null;
        return new(row.Id, row.PrincipalId, row.TenantId, row.Role);
    }
}
