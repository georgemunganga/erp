using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Consumes existing Core identity/organization records. No Procurement account master.</summary>
internal static class ProcurementAuthorityIdentity
{
    /// <summary>Shared Core-owned active canonical people boundary. No role/action eligibility is implied.
    /// Worker home and selected data scope match existing authority lookup semantics, before paging.</summary>
    internal static IQueryable<Mightyfin.Erp.Hrm.Domain.Entities.LocalUser> CanonicalPeopleQuery(HrmDbContext core,
        ProcurementRequestScope scope, DateTimeOffset now)
    {
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
                    );
        if (scope.IsConfined && scope.WorkLocationId is null) query = query.Where(_ => false);
        return query;
    }

    internal sealed record NamedAccount(CanonicalAccount Account, string DisplayName);

    internal static async Task<NamedAccount?> NamedAccountAsync(HrmDbContext core,
        ProcurementRequestScope scope, string principalId, CancellationToken ct)
    {
        var account = await ResolveCanonicalAccountAsync(core, scope.TenantId, scope.LegalEntityId, principalId, ct);
        if (account is null) return null;
        var id = Guid.Parse(account.PrincipalId);
        var name = await core.LocalUsers.AsNoTracking().Where(x => x.TenantId == scope.TenantId && x.Id == id)
            .Select(x => x.DisplayName).SingleAsync(ct);
        return new(account, name);
    }
    public static async Task<SpendingAuthorityConfigurationActor?> ActorAsync(HrmDbContext core,
        ProcurementRequestScope scope, ClaimsPrincipal principal, IAuthorizationService authorization,
        string permission, CancellationToken ct)
    {
        if (!(await authorization.AuthorizeAsync(principal, permission)).Succeeded) return null;
        var account = await ResolveCanonicalAccountAsync(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, ct);
        if (account is null || scope.WorkerId != account.WorkerId) return null;
        // Local identity middleware refreshes claims, but configuration must also reject
        // currently removed action grants rather than trusting a previously issued claim.
        var roles = LocalIdentityRoutes.ParseRoles(account.RolesCsv);
        var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(core, account.RolesCsv, ct);
        if (!roles.Contains("erp_admin", StringComparer.OrdinalIgnoreCase)
            && (!actions.Contains("procurement-access", StringComparer.Ordinal)
                || !actions.Contains(permission, StringComparer.Ordinal))) return null;
        return new(scope.TenantId, scope.LegalEntityId, account.PrincipalId, true,
            new HashSet<string>([permission], StringComparer.Ordinal));
    }

    public static async Task<SpendingAuthoritySubject?> SubjectAsync(HrmDbContext core,
        ProcurementRequestScope scope, string principalId, CancellationToken ct)
    {
        var account = await ResolveCanonicalAccountAsync(core, scope.TenantId, scope.LegalEntityId, principalId, ct);
        return account is null ? null : new(scope.TenantId, scope.LegalEntityId, account.PrincipalId, true, true);
    }

    internal sealed record CanonicalAccount(string PrincipalId, Guid WorkerId,
        Guid? BranchId, Guid? DepartmentId, string RolesCsv);

    internal static async Task<CanonicalAccount?> ResolveCanonicalAccountAsync(HrmDbContext core,
        string tenantId, Guid legalEntityId, string principalId, CancellationToken ct)
    {
        if (!Guid.TryParse(principalId, out var id) || principalId != id.ToString("D")) return null;
        var account = await core.LocalUsers.AsNoTracking().Where(x => x.TenantId == tenantId
            && x.Id == id && !x.IsArchived && x.IsActive)
            .Select(x => new { x.WorkerId, x.LockedUntil, x.RolesCsv }).SingleOrDefaultAsync(ct);
        if (account is null || account.WorkerId is null
            || account.LockedUntil is { } lockedUntil && lockedUntil > DateTimeOffset.UtcNow) return null;
        var home = await ProcurementWorkforce.ResolveActiveWorkerHome(core, tenantId, principalId, ct);
        if (home?.LegalEntityId != legalEntityId) return null;
        return new(principalId, home.WorkerId, home.LocationId, home.OrgUnitId, account.RolesCsv);
    }

    internal static async Task<CanonicalAccount?> ResolveCanonicalWorkerAccountAsync(HrmDbContext core,
        string tenantId, Guid legalEntityId, Guid workerId, CancellationToken ct)
    {
        var accounts = await core.LocalUsers.AsNoTracking().Where(x => x.TenantId == tenantId
            && x.WorkerId == workerId && x.IsActive && !x.IsArchived).Select(x => x.Id).Take(2).ToArrayAsync(ct);
        return accounts.Length == 1 ? await ResolveCanonicalAccountAsync(core, tenantId,
            legalEntityId, accounts[0].ToString("D"), ct) : null;
    }

    public static async Task<string?> ReferenceIssueAsync(HrmDbContext core, ProcurementDbContext db,
        ProcurementRequestScope scope, SpendingAuthorityDraft input, bool complete, CancellationToken ct)
    {
        if (input.BranchRef is { } branch && !await core.WorkLocations.AsNoTracking().AnyAsync(x =>
            x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId && x.Id == branch && !x.IsArchived, ct))
            return "AuthorityBranchUnavailable";
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        if (input.DepartmentRef is { } department && !await core.OrgUnits.AsNoTracking().AnyAsync(x =>
            x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId && x.Id == department
            && !x.IsArchived && x.Status == "active" && x.EffectiveFrom <= date
            && (x.EffectiveTo == null || date <= x.EffectiveTo), ct)) return "AuthorityDepartmentUnavailable";
        foreach (var code in input.CategoryCodes ?? [])
            if (!await db.ProcurementCategories.AsNoTracking().AnyAsync(x => x.Code == code
                && (!complete || x.Status == "Active"), ct)) return "AuthorityCategoryUnavailable";
        if (input.CurrencyCode is { Length: > 0 } currency)
        {
            var companyCurrency = await ProcurementCurrencyDefaults.ForSelectedCompanyAsync(core, scope, ct);
            // No cross-currency authority until a configured currency/FX contract is available.
            if (!string.Equals(currency, companyCurrency, StringComparison.OrdinalIgnoreCase))
                return "AuthorityCompanyCurrencyRequired";
        }
        return null;
    }

    public static IQueryable<ProcurementSpendingAuthorityGrant> InScope(
        IQueryable<ProcurementSpendingAuthorityGrant> query, ProcurementRequestScope scope)
    {
        if (scope.WorkLocationId is { } branch) query = query.Where(x => !x.AllBranches && x.BranchRef == branch);
        if (scope.OrgUnitId is { } department) query = query.Where(x => !x.AllDepartments && x.DepartmentRef == department);
        if (scope.IsConfined && scope.WorkLocationId is null) query = query.Where(_ => false);
        return query;
    }

    public static bool InputInScope(SpendingAuthorityDraft input, ProcurementRequestScope scope)
        => (!scope.WorkLocationId.HasValue || !input.AllBranches && input.BranchRef == scope.WorkLocationId)
            && (!scope.OrgUnitId.HasValue || !input.AllDepartments && input.DepartmentRef == scope.OrgUnitId)
            && (!scope.IsConfined || scope.WorkLocationId.HasValue);

    public static SpendingAuthorityDraft Draft(ProcurementSpendingAuthorityGrant value)
        => new(value.CurrencyCode, value.AmountBasis, value.MaximumAmount, value.EffectiveFrom,
            value.EffectiveUntil, value.AllBranches, value.BranchRef, value.AllDepartments,
            value.DepartmentRef, value.AllCategories, value.Categories.Select(x => x.CategoryCode).ToArray());
}
