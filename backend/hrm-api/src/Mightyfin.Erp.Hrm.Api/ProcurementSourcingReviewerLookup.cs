using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPolicyDraftRoutes
{
    private static async Task<IResult> SourcingReviewers(string? search, int? offset, int? limit, string? principalIds,
        int? schemaVersion,
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
            if (schemaVersion is not (null or 1 or 2))
                return Problem("sourcing-reviewer-schema-invalid", "Choose sourcing rule schema 1 or 2.", 422);
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
            int total;
            int pageLength;
            object page;
            if (schemaVersion == 2)
            {
                // Core company/branch scope is in SQL before materialization. Resolve
                // each bounded candidate against current stored roles; an HR worker
                // link is optional and never substitutes for the explicit grant.
                var query = ProcurementPrincipalPeople.Candidates(core, scope, DateTimeOffset.UtcNow)
                    .Where(x => text == "" || x.DisplayName.ToLower().Contains(text));
                if (selected is not null) query = query.Where(x => selected.Contains(x.Id));
                var candidates = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id)
                    .Select(x => new { x.Id, x.DisplayName }).Take(501).ToArrayAsync(ct);
                if (candidates.Length > 500)
                    return Problem("sourcing-reviewer-search-too-broad", "Narrow the reviewer search before selecting a person.", 422);
                var eligible = new List<(Guid Id, string DisplayName, Guid? WorkerId)>();
                foreach (var candidate in candidates)
                {
                    var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                        scope.LegalEntityId, candidate.Id.ToString("D"), ct);
                    if (person is not null && person.Permissions.Contains("procurement-sourcing-publish")
                        && (scope.WorkLocationId is { } branch
                            ? person.OrganisationWide || person.AllowedBranches.Contains(branch)
                            : person.OrganisationWide || person.AllowedBranches.Count > 0))
                        eligible.Add((candidate.Id, candidate.DisplayName, person.CoreLinkedWorkerId));
                }
                total = eligible.Count;
                var result = eligible.Skip(skip).Take(take).Select(x => new
                    { principalId = x.Id.ToString("D"), x.DisplayName, x.WorkerId }).ToArray();
                page = result;
                pageLength = result.Length;
            }
            else
            {
                // Schema 1 retains the worker-backed people contract exactly.
                var query = ProcurementAuthorityIdentity.CanonicalPeopleQuery(core, scope, DateTimeOffset.UtcNow)
                    .Where(x => text == "" || x.DisplayName.ToLower().Contains(text));
                if (selected is not null) query = query.Where(x => selected.Contains(x.Id));
                total = await query.CountAsync(ct);
                var result = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Skip(skip).Take(take)
                    .Select(x => new { principalId = x.Id.ToString(), x.DisplayName, x.WorkerId }).ToArrayAsync(ct);
                page = result;
                pageLength = result.Length;
            }
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
                supportedSourcingRuleSchemas = SourcingPublicationPolicyRules.SupportedSchemaVersions,
                nextOffset = pageLength > 0 && skip + pageLength < total && skip + pageLength <= 100000 ? (int?)(skip + pageLength) : null,
                isAuthority = false });
        }
        catch (Exception error) when (error is NpgsqlException or DbUpdateException or InvalidOperationException)
        {
            return Problem("sourcing-reviewer-store-unavailable", "Reviewer details are unavailable. Refresh before selecting someone.", 503);
        }
    }
}
