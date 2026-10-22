using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Current Core account and its existing configured permissions. Employee data is optional.</summary>
public sealed record ProcurementCorePrincipal(Guid Id, Guid? WorkerId, string[] Roles, string[] Actions)
{
    public Claim[] Claims(string tenant) => [new("tenant", tenant), new("sub", Id.ToString("D")),
        .. Roles.Select(role => new Claim(ClaimTypes.Role, role)),
        new(ProcurementPermissions.ConfiguredClaim, "true"),
        .. Actions.Select(action => new Claim(ProcurementPermissions.ActionClaim, action))];
}

public static class ProcurementCorePrincipals
{
    public static async Task<ProcurementCorePrincipal?> CurrentAsync(HrmDbContext db, string tenant,
        string subject, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenant) || !Guid.TryParse(subject, out var id) || id == Guid.Empty) return null;
        var now = DateTimeOffset.UtcNow;
        var account = await db.LocalUsers.AsNoTracking().Where(x => x.TenantId == tenant && x.Id == id
            && x.IsActive && !x.IsArchived && (x.LockedUntil == null || x.LockedUntil <= now))
            .Select(x => new { x.Id, x.WorkerId, x.RolesCsv }).SingleOrDefaultAsync(ct);
        if (account is null) return null;
        var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(db, account.RolesCsv, ct, tenant);
        return new(account.Id, account.WorkerId, LocalIdentityRoutes.ParseRoles(account.RolesCsv), actions);
    }
}
