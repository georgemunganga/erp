using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalInvoiceDiscrepancyRoutes
{
    internal static bool EligibleIssueOwner(ProcurementPrincipalPeople.Person? person,
        Guid branchId, string currentOwner) => person is not null
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
                // Filter current action and branch authority before returning a page
                // or a count. This bounded directory cannot expose unrelated users.
                var rows = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id)
                    .Take(2049).Select(x => new { x.Id, x.DisplayName }).ToArrayAsync(token);
                if (rows.Length > 2048)
                    throw new InvalidOperationException("PrincipalInvoiceIssueOwnerLookupTooLarge");
                var eligible = new List<(Guid Id, string Name)>();
                foreach (var row in rows)
                {
                    var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                        scope.LegalEntityId, row.Id.ToString("D"), token);
                    if (!EligibleIssueOwner(person, issue.BranchId, issue.OwnerPrincipalId)) continue;
                    eligible.Add((row.Id, row.DisplayName));
                }
                var start = 0;
                if (cursor is { } after)
                {
                    var position = eligible.FindIndex(x => x.Id == after);
                    if (position < 0) throw new InvalidOperationException("PrincipalInvoiceIssueOwnerCursorChanged");
                    start = position + 1;
                }
                var size = limit ?? 25;
                var page = eligible.Skip(start).Take(size).ToArray();
                return new
                {
                    invoiceId = id, issueId, issueVersion = issue.Version,
                    items = page.Select(x => new { principalId = x.Id.ToString("D"), displayName = x.Name }).ToArray(),
                    nextCursor = start + page.Length < eligible.Count ? page[^1].Id.ToString("D") : null,
                    hasMore = start + page.Length < eligible.Count,
                    isAuthority = false, sourceSchemaVersion = 2
                };
            }, ct);
    }
}
