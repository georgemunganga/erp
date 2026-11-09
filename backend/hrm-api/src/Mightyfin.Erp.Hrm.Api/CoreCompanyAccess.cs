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
        if (!license.IsCurrent) return null;
        var row = await FindCandidateAsync(issuer, subject, linkId, companyId, cancellation);
        if (row is null || row.EnrollmentId != license.EnrollmentId
            || !license.IsCurrent || !license.AllowsWorkspace(row.Slot)) return null;
        return new(row.CompanyId, row.PrincipalId, row.TenantId, row.Role);
    }

    // Internal discovery only. A candidate is never an authorization decision;
    // licensed reads and mutations must call ReadAsync with a fresh lease.
    internal async Task<CoreCompanyCandidate?> FindCandidateAsync(string issuer, string subject,
        Guid linkId, Guid companyId, CancellationToken cancellation = default)
    {
        if (!ValidIdentity(issuer, subject) || linkId == Guid.Empty || companyId == Guid.Empty) return null;
        var row = await Candidates(issuer, subject).SingleOrDefaultAsync(
            x => x.LinkId == linkId && x.CompanyId == companyId, cancellation);
        return row is not null && Usable(row) ? row : null;
    }

    internal async Task<(CoreCompanyCandidate[] Rows, string? Next)> DirectoryAsync(string issuer, string subject,
        Guid? after, CancellationToken cancellation = default)
        => await DirectoryCoreAsync(issuer,subject,after,true,cancellation);

    internal Task<(CoreCompanyCandidate[] Rows,string? Next)> MemberDirectoryAsync(string issuer,string subject,
        Guid? after,CancellationToken cancellation=default)=>DirectoryCoreAsync(issuer,subject,after,false,cancellation);

    private async Task<(CoreCompanyCandidate[] Rows,string? Next)> DirectoryCoreAsync(string issuer,string subject,
        Guid? after,bool governanceOnly,CancellationToken cancellation)
    {
        if (!ValidIdentity(issuer, subject)) return ([], null);
        var query = Candidates(issuer, subject);
        if(governanceOnly)query=query.Where(x=>x.Role==CoreCompanyRoles.Owner || x.Role==CoreCompanyRoles.Administrator);
        if (after is { } cursor)
        {
            var text = cursor.ToString("D");
            query = query.Where(x => string.Compare(x.MembershipId.ToString().ToLower(), text) > 0);
        }
        var scanned = await query.OrderBy(x => x.MembershipId.ToString().ToLower()).Take(21).ToArrayAsync(cancellation);
        var page = scanned.Take(20).ToArray();
        // LockedUntil is checked after materialization for SQLite compatibility.
        // The cursor advances over scanned rows, including unavailable accounts.
        return (page.Where(Usable).ToArray(), scanned.Length > 20 ? page[^1].MembershipId.ToString("D") : null);
    }

    private bool Usable(CoreCompanyCandidate row) => CoreCompanyRoles.IsValid(row.Role)
        && (row.LockedUntil is null || row.LockedUntil <= clock.GetUtcNow());
    private static bool ValidIdentity(string issuer, string subject) => issuer == ProductCommercialLicense.Issuer
        && Regex.IsMatch(subject, "^[1-9][0-9]{0,18}\\z");

    private IQueryable<CoreCompanyCandidate> Candidates(string issuer, string subject)
    {
        // Tenant filter bypass is confined to an exact verified issuer/subject/link.
        // Company/payer/email supplied by a caller can never establish membership.
        return from link in db.ExternalIdentityLinks.IgnoreQueryFilters().AsNoTracking()
            join user in db.LocalUsers.IgnoreQueryFilters().AsNoTracking()
                on new { link.TenantId, Id = link.LocalUserId } equals new { user.TenantId, user.Id }
            join member in db.CompanyMemberships.IgnoreQueryFilters().AsNoTracking()
                on new { user.TenantId, UserId = user.Id } equals new { member.TenantId, UserId = member.LocalUserId }
            join company in db.LegalEntities.IgnoreQueryFilters().AsNoTracking()
                on new { member.TenantId, Id = member.LegalEntityId } equals new { company.TenantId, company.Id }
            join binding in db.WorkspaceBindings.IgnoreQueryFilters().AsNoTracking()
                on new { company.TenantId, CompanyId = company.Id } equals new { binding.TenantId, CompanyId = binding.LegalEntityId }
            where link.Issuer == issuer && link.Subject == subject
                && link.IsActive && !link.IsArchived && user.IsActive && !user.IsArchived
                && user.CredentialMode == LocalCredentialModes.Federated
                && member.IsActive && !member.IsArchived
                && !company.IsArchived && !binding.IsArchived && binding.Issuer == issuer
            select new CoreCompanyCandidate { CompanyId = company.Id, CompanyName = company.RegisteredName,
                PrincipalId = user.Id, TenantId = member.TenantId, Role = member.Role, LockedUntil = user.LockedUntil,
                EnrollmentId = binding.EnrollmentId, Slot = binding.Slot, LinkId = link.Id, MembershipId = member.Id };
    }
}

internal sealed record CoreCompanyCandidate
{
    public Guid CompanyId { get; init; }
    public string CompanyName { get; init; } = "";
    public Guid PrincipalId { get; init; }
    public string TenantId { get; init; } = "";
    public string Role { get; init; } = "";
    public DateTimeOffset? LockedUntil { get; init; }
    public string EnrollmentId { get; init; } = "";
    public int Slot { get; init; }
    public Guid LinkId { get; init; }
    public Guid MembershipId { get; init; }
}
