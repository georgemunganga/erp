using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Core-owned self-service selector, separate from organisation administration.
/// Reads the existing account, company and branch masters; never creates grants.</summary>
public static class CoreWorkspaceContextRoutes
{
    public const string Policy = "core-workspace-context";

    public static void AddPolicy(AuthorizationOptions options)
        => options.AddPolicy(Policy, policy => policy.RequireAuthenticatedUser().RequireClaim("tenant"));

    public static void MapRoutes(WebApplication app)
        => app.MapGet("/api/core/v1/workspace-context", Read).RequireAuthorization(Policy);

    private static async Task<IResult> Read(HttpContext http, HrmDbContext core, TimeProvider clock,
        CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        var tenant = http.User.FindFirstValue("tenant");
        if (string.IsNullOrWhiteSpace(tenant)
            || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var principal))
            return Results.Forbid();
        var now = clock.GetUtcNow();
        var account = await core.LocalUsers.AsNoTracking().Where(x => x.TenantId == tenant && x.Id == principal
                && x.IsActive && !x.IsArchived)
            .Select(x => new { x.LockedUntil }).SingleOrDefaultAsync(ct);
        if (account is null || account.LockedUntil is { } until && until > now)
            return Results.Forbid();

        // Retain declared assignments even if their branch has become inactive:
        // losing every usable assignment must not turn a confined user into an
        // organisation-wide one. No assignments follows the existing Core scope contract.
        var declared = await core.UserBranchAssignments.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.UserId == principal && !x.IsArchived)
            .Select(x => x.LocationId).Distinct().ToArrayAsync(ct);
        var organisationWide = declared.Length == 0;
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
            .Select(x => new { x.Id, x.Code, x.Name, x.LegalEntityId }).ToArrayAsync(ct);
        return Results.Ok(new { schemaVersion = 1, tenantId = tenant, principalId = principal,
            organisationWide, companies = companyChoices, branches = branchChoices });
    }
}
