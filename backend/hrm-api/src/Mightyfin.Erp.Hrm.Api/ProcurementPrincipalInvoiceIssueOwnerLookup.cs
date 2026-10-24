using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalInvoiceDiscrepancyRoutes
{
    internal static bool EligibleIssueOwner(ProcurementPrincipalPeople.Person? person,
        Guid branchId, string currentOwner) => person is not null
        && person.Permissions.Contains("procurement-invoice-read")
        && person.Permissions.Contains("procurement-invoice-match")
        && (person.OrganisationWide || person.AllowedBranches.Contains(branchId))
        && person.PrincipalId != currentOwner;

    // Purpose-only names for an issue's current owner. The Assign command repeats
    // current Core account, action and branch checks inside its write transaction.
    private static async Task<IResult> EligibleOwners(Guid id, Guid issueId, string? q,
        Guid? cursor, int? limit, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        if (!Available(http) || !await http.RequestServices
                .GetRequiredService<IProcurementPrincipalInvoiceSchemaReadiness>().IsReadReadyAsync(ct))
            return Results.Json(new { code = "PrincipalInvoiceExceptionsUnavailable" }, statusCode: 503);
        if (q is { Length: > 100 } || q is not null && q.Any(char.IsControl)
            || cursor == Guid.Empty || limit is < 1 or > 50)
            return Results.Json(new { code = "PrincipalInvoiceIssueOwnerQueryInvalid" }, statusCode: 422);
        return await ProcurementPrincipalInvoiceSourceRoutes.Run(http, scope,
            async (db, unusedCases, core, auth, token) =>
            {
                var ready = http.RequestServices.GetRequiredService<IProcurementPrincipalInvoiceSchemaReadiness>();
                if (!await ready.IsReadReadyAsync(db, token))
                    throw new InvalidOperationException("PrincipalInvoiceExceptionsUnavailable");
                var (invoice, _) = await ProcurementPrincipalInvoiceSourceRoutes.Current(
                    db, core, scope, http, auth, id, token);
                var state = await db.PrincipalInvoiceStates.AsNoTracking().SingleOrDefaultAsync(x =>
                    x.SupplierInvoiceId == id && x.State == "ComparisonBlocked", token)
                    ?? throw new KeyNotFoundException();
                var issue = await db.PrincipalInvoiceDiscrepancies.AsNoTracking().SingleOrDefaultAsync(x =>
                    x.Id == issueId && x.SupplierInvoiceId == id && x.BranchId == invoice.BranchRef
                    && x.MappingVersionId == state.CurrentMappingId
                    && x.OwnerPrincipalId == scope.SubjectId
                    && x.State != "Resolved" && x.State != "CancelledWithReason", token)
                    ?? throw new KeyNotFoundException();
                var search = q?.Trim() ?? "";
                var query = ProcurementPrincipalPeople.Candidates(core, scope,
                    http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(), issue.BranchId);
                if (search.Length != 0)
                {
                    var pattern = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%")
                        .Replace("_", "\\_") + "%";
                    query = query.Where(x => EF.Functions.ILike(x.DisplayName, pattern, "\\"));
                }
                // Scan small sorted batches and stop once the next eligible page
                // is known. No unfiltered directory count or post-page permission
                // filter can disclose denied people. A missing/revoked cursor fails
                // closed, and the Assign command checks current authority again.
                const int chunkSize = 64;
                const int maximumScanned = 2048;
                var size = limit ?? 25;
                var results = new List<(Guid Id, string Name)>();
                var cursorSeen = cursor is null;
                var scanned = 0;
                var exhausted = false;
                while (scanned < maximumScanned)
                {
                    var take = Math.Min(chunkSize, maximumScanned - scanned);
                    var rows = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id)
                        .Skip(scanned).Take(take)
                        .Select(x => new { x.Id, x.DisplayName }).ToArrayAsync(token);
                    if (rows.Length == 0) { exhausted = true; break; }
                    scanned += rows.Length;
                    exhausted = rows.Length < take;
                    foreach (var row in rows)
                    {
                        var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                            scope.LegalEntityId, row.Id.ToString("D"), token);
                        if (!EligibleIssueOwner(person, issue.BranchId, issue.OwnerPrincipalId)) continue;
                        if (!cursorSeen)
                        {
                            if (row.Id == cursor) cursorSeen = true;
                            continue;
                        }
                        results.Add((row.Id, row.DisplayName));
                        if (results.Count > size) break;
                    }
                    if (results.Count > size || exhausted) break;
                }
                if (results.Count <= size && !exhausted && scanned >= maximumScanned)
                    throw new InvalidOperationException("PrincipalInvoiceIssueOwnerLookupTooLarge");
                if (!cursorSeen) throw new InvalidOperationException("PrincipalInvoiceIssueOwnerCursorChanged");
                var page = results.Take(size).ToArray();
                return new
                {
                    invoiceId = id, issueId, issueVersion = issue.Version,
                    items = page.Select(x => new { principalId = x.Id.ToString("D"), displayName = x.Name }).ToArray(),
                    nextCursor = results.Count > size ? page[^1].Id.ToString("D") : null,
                    hasMore = results.Count > size,
                    isAuthority = false, sourceSchemaVersion = 2
                };
            }, ct);
    }
}
