using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPolicyDraftRoutes
{
    // Purpose-only projections over existing Core and Procurement records. A
    // selectable person/site is not an approval grant or evidence of delivery.
    internal sealed class ReceivingLookupRow
    {
        public Guid Id { get; init; }
        public string DisplayName { get; init; } = "";
        public string? Code { get; init; }
        public Guid? PrincipalId { get; init; }
    }

    private static async Task<IResult> ReceivingPolicyLookup(string kind, string? search, int? offset, int? limit,
        string? ids, HttpContext http, HrmDbContext core, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (kind is not ("people" or "sites" or "categories"))
            return Problem("receiving-lookup-invalid", "Choose people, locations or buying categories.", 422);
        try
        {
            if (!await ReceivingConfigurationActor(core, scope, "procurement-configuration-read", ct))
                return Problem("receiving-configuration-unavailable", "Your current account cannot select receiving settings.", 403);
            var text = (search ?? "").Trim().ToLowerInvariant();
            var skip = offset ?? 0; var take = limit ?? 25;
            if (text.Length > 100 || text.Any(char.IsControl) || skip is < 0 or > 100000 || take is < 1 or > 50)
                return Problem("receiving-lookup-invalid", "Use a name of up to 100 characters and a valid results page.", 422);
            Guid[]? selected = null;
            if (http.Request.Query.ContainsKey("ids"))
            {
                var values = (ids ?? "").Split(',');
                if ((ids?.Length ?? 0) > 924 || values.Length is < 1 or > 25 || text.Length != 0 || skip != 0 || take != 25
                    || values.Any(value => !Guid.TryParseExact(value, "D", out var id) || id == Guid.Empty || id.ToString("D") != value)
                    || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                    return Problem("receiving-lookup-invalid", "Choose up to 25 distinct saved selections without a name search or another page.", 422);
                selected = values.Select(Guid.Parse).ToArray();
            }
            var query = ReceivingPolicyLookupQuery(kind, core, db, scope, DateTimeOffset.UtcNow);
            if (selected is not null) query = query.Where(x => selected.Contains(x.Id));
            if (text.Length != 0) query = query.Where(x => x.DisplayName.ToLower().Contains(text)
                || x.Code != null && x.Code.ToLower().Contains(text));
            // All visibility and saved-selection filters precede both count and paging.
            var total = await query.CountAsync(ct);
            var items = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Skip(skip).Take(take).ToArrayAsync(ct);
            return Results.Ok(new { kind, companyId = scope.LegalEntityId, items, total, offset = skip, limit = take,
                nextOffset = items.Length > 0 && skip + items.Length < total && skip + items.Length <= 100000
                    ? (int?)(skip + items.Length) : null,
                isAuthority = false, canAcceptDelivery = false });
        }
        catch (Exception error) when (error is NpgsqlException or DbUpdateException or InvalidOperationException)
        {
            return Problem("receiving-lookup-unavailable", "Receiving selections are unavailable. Refresh before choosing a record.", 503);
        }
    }

    internal static IQueryable<ReceivingLookupRow> ReceivingPolicyLookupQuery(string kind,
        HrmDbContext core, ProcurementDbContext db, ProcurementRequestScope scope, DateTimeOffset now)
    {
        IQueryable<ReceivingLookupRow> query;
        if (kind == "people")
        {
            // Reuse the same SQL tenant/company/branch/department and unique
            // active account boundary as existing named authority selectors.
            query = ProcurementAuthorityIdentity.CanonicalPeopleQuery(core, scope, now)
                .Select(person => new ReceivingLookupRow { Id = person.WorkerId!.Value, DisplayName = person.DisplayName, Code = null, PrincipalId = person.Id });
        }
        else if (kind == "sites")
        {
            var actor = Guid.Parse(scope.SubjectId);
            var permitted = core.UserBranchAssignments.AsNoTracking().Where(x => x.TenantId == scope.TenantId
                && x.UserId == actor && !x.IsArchived).Select(x => x.LocationId);
            var sites = core.WorkLocations.AsNoTracking().Where(x => x.TenantId == scope.TenantId
                && x.LegalEntityId == scope.LegalEntityId && !x.IsArchived
                && (scope.WorkLocationId == null || x.Id == scope.WorkLocationId)
                && (!permitted.Any() || permitted.Contains(x.Id)));
            if (scope.IsConfined && scope.WorkLocationId is null) sites = sites.Where(_ => false);
            query = sites.Select(site => new ReceivingLookupRow { Id = site.Id, DisplayName = site.Name, Code = site.Code, PrincipalId = null });
        }
        else if (kind == "categories") query = db.ProcurementCategories.AsNoTracking().Where(x => x.TenantId == scope.TenantId
                && x.LegalEntityId == scope.LegalEntityId && x.Status == "Active")
            .Select(category => new ReceivingLookupRow { Id = category.Id, DisplayName = category.Name, Code = category.Code, PrincipalId = null });
        else throw new ArgumentOutOfRangeException(nameof(kind));
        return query;
    }
}
