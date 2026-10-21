using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPolicyDraftRoutes
{
    private static async Task<IResult> SourcingReviewers(string? search, int? offset, int? limit, string? principalIds,
        HttpContext http, HrmDbContext core, ProcurementDbContext db, ProcurementRequestScope scope, CancellationToken ct)
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
            Guid[]? selected = null;
            if (http.Request.Query.ContainsKey("principalIds"))
            {
                // Resolve only retained selections, never fetch all people to fill off-page names.
                // Malformed targeting must not fall back to an unrestricted name lookup.
                var requested = principalIds ?? "";
                var values = requested.Split(',');
                if (requested.Length > 924 || values.Length is < 1 or > 25 || text.Length != 0
                    || (offset ?? 0) != 0 || (limit ?? 25) != 25
                    || values.Any(value => !Guid.TryParseExact(value, "D", out var id)
                        || id == Guid.Empty || id.ToString("D") != value)
                    || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                    return Problem("sourcing-reviewer-selection-invalid", "Choose up to 25 distinct saved reviewers without a name search or another page.", 422);
                selected = values.Select(Guid.Parse).ToArray();
            }
            // Unlike spending-authority people lookup, there is no financial-action eligibility
            // filter. Every identity/data boundary is in SQL before BOTH count and pagination.
            var query = ProcurementAuthorityIdentity.CanonicalPeopleQuery(core, scope, DateTimeOffset.UtcNow)
                .Where(x => text == "" || x.DisplayName.ToLower().Contains(text));
            if (selected is not null) query = query.Where(x => selected.Contains(x.Id));
            var total = await query.CountAsync(ct);
            var page = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Skip(skip).Take(take)
                .Select(x => new { principalId = x.Id.ToString(), x.DisplayName, x.WorkerId }).ToArrayAsync(ct);
            var configurationVersion = 0;
            try
            {
                var readiness = http.RequestServices.GetService<IProcurementSourcingPolicyIdentitySchemaReadiness>();
                if (readiness is not null && await readiness.IsReadyAsync(db, ct)) configurationVersion = 1;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // People remain scoped/readable if Procurement schema discovery fails.
                // This marker confers no authority; mutation routes repeat the owning-store guard.
            }
            return Results.Ok(new { items = page, total, offset = skip, limit = take,
                configurationVersion,
                nextOffset = page.Length > 0 && skip + page.Length < total && skip + page.Length <= 100000 ? (int?)(skip + page.Length) : null,
                isAuthority = false });
        }
        catch (Exception error) when (error is NpgsqlException or DbUpdateException or InvalidOperationException)
        {
            return Problem("sourcing-reviewer-store-unavailable", "Reviewer details are unavailable. Refresh before selecting someone.", 503);
        }
    }
}
