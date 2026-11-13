using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Hrm.Application;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Core-owned self-service selector, separate from organisation administration.
/// Reads the existing account, company and branch masters; never creates grants.</summary>
public static class CoreWorkspaceContextRoutes
{
    public const string Policy = "core-workspace-context";

    public static void AddPolicy(AuthorizationOptions options)
        => options.AddPolicy(Policy, policy => policy.RequireAuthenticatedUser().RequireClaim("tenant"));

    public static void MapRoutes(WebApplication app)
        => app.MapGet("/api/core/v1/workspace-context", Read).RequireAuthorization(Policy).WithMetadata(new ProductWorkspaceRequestPolicy());

    private static async Task<IResult> Read(HttpContext http, HrmDbContext core, TimeProvider clock, IConfiguration config,
        CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if(ProductRequestAuthentication.Enabled(config) || http.User.Identity?.AuthenticationType==ProductRequestAuthentication.Scheme)
            return await ReadProduct(http,core,ct);
        var tenant = http.User.FindFirstValue("tenant");
        if (string.IsNullOrWhiteSpace(tenant)
            || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var principal))
            return Results.Forbid();
        var now = clock.GetUtcNow();
        var account = await core.LocalUsers.AsNoTracking().Where(x => x.TenantId == tenant && x.Id == principal
                && x.IsActive && !x.IsArchived)
            .Select(x => new { x.LockedUntil, x.RolesCsv }).SingleOrDefaultAsync(ct);
        if (account is null || account.LockedUntil is { } until && until > now)
            return Results.Forbid();

        // Retain declared assignments even if their branch has become inactive:
        // losing every usable assignment must not turn a confined user into an
        // organisation-wide one. No assignments follows the existing Core scope contract.
        var declared = await core.UserBranchAssignments.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.UserId == principal && !x.IsArchived)
            .Select(x => x.LocationId).Distinct().ToArrayAsync(ct);
        var organisationWide = declared.Length == 0;
        var assignedRoles = LocalIdentityRoutes.ParseRoles(account.RolesCsv);
        var rolePermissions = await core.TenantRoleAssignments.AsNoTracking()
            .Where(x => x.TenantId == tenant && assignedRoles.Contains(x.RoleKey) && x.Active && !x.IsArchived)
            .Select(x => x.PermissionsCsv).ToArrayAsync(ct);
        var grants = rolePermissions.SelectMany(x => LocalIdentityRoutes.ParseRoles(x)).ToHashSet(StringComparer.Ordinal);
        var coreActions = CoreCurrencyPrecisionPermissions.All.Where(action =>
            CoreCurrencyPrecisionPermissions.Allows(true, true, !organisationWide,
                grants.Contains(CoreCurrencyPrecisionPermissions.Read), grants.Contains(CoreCurrencyPrecisionPermissions.Configure),
                configure: action == CoreCurrencyPrecisionPermissions.Configure)).ToArray();
        var activeCompanies = core.LegalEntities.AsNoTracking()
            .Where(x => x.TenantId == tenant && !x.IsArchived);
        var branches = core.WorkLocations.AsNoTracking()
            .Where(x => x.TenantId == tenant && !x.IsArchived
                && activeCompanies.Any(company => company.Id == x.LegalEntityId));
        var companies = activeCompanies;
        if (!organisationWide)
        {
            branches = branches.Where(x => declared.Contains(x.Id));
            companies = companies.Where(company => branches.Any(branch => branch.LegalEntityId == company.Id));
        }
        var companyChoices = await companies.OrderByDescending(x => x.IsDefault).ThenBy(x => x.RegisteredName)
            .ThenBy(x => x.Id).Select(x => new { x.Id, x.Code, x.RegisteredName, x.Currency, x.IsDefault }).ToArrayAsync(ct);
        var branchChoices = await branches.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Code, x.Name, x.LegalEntityId, x.Type }).ToArrayAsync(ct);
        return Results.Ok(new { schemaVersion = 1, tenantId = tenant, principalId = principal,
            organisationWide, companies = companyChoices, branches = branchChoices, coreActions });
    }
    private static async Task<IResult> ReadProduct(HttpContext http,HrmDbContext core,CancellationToken ct)
    {
        var scope=ProductRequestContext.Current(http);
        var token = ProductErpSessionFlow.ReadCookie(http);
        if(scope is null || token is null)return Results.Forbid();
        var tenant=scope.TenantId;var company=scope.CompanyId;var selected=scope.LocationIds.ToArray();
        var companies=await core.LegalEntities.AsNoTracking().Where(x=>x.TenantId==tenant && x.Id==company && !x.IsArchived)
            .Select(x=>new{x.Id,x.Code,x.RegisteredName,x.Currency,x.IsDefault}).ToArrayAsync(ct);
        if(companies.Length!=1)return Results.Forbid();
        var branches=core.WorkLocations.AsNoTracking().Where(x=>x.TenantId==tenant && x.LegalEntityId==company && !x.IsArchived);
        if(!scope.AllCompanyLocations)branches=branches.Where(x=>selected.Contains(x.Id));
        var choices=await branches.OrderBy(x=>x.Name).ThenBy(x=>x.Id).Select(x=>new{x.Id,x.Code,x.Name,x.LegalEntityId,x.Type}).ToArrayAsync(ct);
        if(!scope.AllCompanyLocations && choices.Length!=selected.Length)return Results.Forbid();
        var final=await ProductRequestContext.RecheckAsync(http,ct);
        if(final!=ModuleAccessStatus.Authorized)return final==ModuleAccessStatus.Unavailable?Results.StatusCode(503):Results.Forbid();
        // Product context never consults legacy RolesCsv, branch assignments or
        // role-to-Core-action bootstrap. Company-wide means this company only.
        var fullHrm = ProductHrmWorkspace.Enabled(http.RequestServices.GetRequiredService<IConfiguration>())
            && await ProductHrmWorkspace.CoversCompanyAsync(http, core, ct);
        return Results.Ok(new{schemaVersion=1,tenantId=tenant,principalId=scope.PrincipalId,contextKind="product",selectedModule=scope.Module,
            hrmWorkspace=fullHrm,moduleRoles=scope.Roles,
            selectedCompanyId=company,organisationWide=scope.AllCompanyLocations,companies,branches=choices,coreActions=Array.Empty<string>(),
            sessionContext = ProductRequestAuthentication.SessionContext(token),
            workerDirectory = scope.Module == "hrm" && scope.Roles.Any(role => role is "hr_ops" or "hr_admin" or "payroll" or "manager"),
            workerArchive = scope.Module == "hrm" && scope.Roles.Any(role => role is "hr_ops" or "hr_admin")});
    }

}
