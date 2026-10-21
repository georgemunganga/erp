using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPolicyDraftRoutes
{
    private static async Task<IResult> SourcingReviewers(string? search, int? offset, int? limit,
        HttpContext http, HrmDbContext core, ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        try
        {
            if (!await ReceivingConfigurationActor(core, scope, "procurement-configuration-read", ct))
                return Problem("sourcing-configuration-unavailable", "Your current account cannot select sourcing reviewers.", 403);
            var text = (search ?? "").Trim().ToLowerInvariant();
            if (text.Length > 100)
                return Problem("sourcing-reviewer-search-invalid", "Search by a name of up to 100 characters.", 422);
            var skip = Math.Clamp(offset ?? 0, 0, 100000);
            var take = Math.Clamp(limit ?? 25, 1, 50);
            // Unlike spending-authority people lookup, there is no financial-action eligibility
            // filter. Every identity/data boundary is in SQL before BOTH count and pagination.
            var query = ProcurementAuthorityIdentity.CanonicalPeopleQuery(core, scope, DateTimeOffset.UtcNow)
                .Where(x => text == "" || x.DisplayName.ToLower().Contains(text));
            var total = await query.CountAsync(ct);
            var page = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Skip(skip).Take(take)
                .Select(x => new { principalId = x.Id.ToString(), x.DisplayName, x.WorkerId }).ToArrayAsync(ct);
            return Results.Ok(new { items = page, total, offset = skip, limit = take,
                nextOffset = page.Length > 0 && skip + page.Length < total && skip + page.Length <= 100000 ? (int?)(skip + page.Length) : null,
                isAuthority = false });
        }
        catch (Exception error) when (error is NpgsqlException or DbUpdateException or InvalidOperationException)
        {
            return Problem("sourcing-reviewer-store-unavailable", "Reviewer details are unavailable. Refresh before selecting someone.", 503);
        }
    }
}
