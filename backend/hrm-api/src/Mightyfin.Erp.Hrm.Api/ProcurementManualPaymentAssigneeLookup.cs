using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementManualPaymentRoutes
{
    // This is a current Core directory projection, not an assignment or an approval.
    // The command checks all named people and their relationships again at write time.
    private static async Task<IResult> AssigneeLookup(string? role, Guid? branchId,
        string? q, Guid? cursor, int? limit, string? recorderPrincipalId,
        HttpContext http, ProcurementRequestScope scope, ProcurementApprovalTransaction transaction,
        IAuthorizationService authorization, CancellationToken ct)
        => await Run(http, scope, transaction, authorization, async (_, _, core, actor, _, token) =>
        {
            if (role is not ("approve" or "record" or "confirm") || branchId is not { } branch
                || branch == Guid.Empty || cursor == Guid.Empty || limit is < 1 or > 50
                || q is { Length: > 100 } || q is not null && q.Any(char.IsControl))
                throw new ArgumentException("ManualPaymentAssigneeQueryInvalid");
            var canEditDraft = false;
            foreach (var action in new[] { "procurement-payment-create", "procurement-payment-edit", "procurement-payment-submit" })
                if (actor.Permissions.Contains(action)
                    && (await authorization.AuthorizeAsync(http.User, null, action)).Succeeded)
                { canEditDraft = true; break; }
            if (scope.PrincipalId?.ToString("D") != actor.PrincipalId
                || scope.CoreLinkedWorkerId != actor.CoreLinkedWorkerId || !canEditDraft)
                throw new UnauthorizedAccessException("ManualPaymentDraftActionRequired");
            // The branch is a transaction input, never inferred from an AllBranches read.
            if (!await core.WorkLocations.AsNoTracking().AnyAsync(x => x.TenantId == scope.TenantId
                    && x.LegalEntityId == scope.LegalEntityId && x.Id == branch && !x.IsArchived, token))
                throw new KeyNotFoundException();
            _ = await ProcurementExecutionRecordScope.Branches(core, scope, branch, true, token);
            if (!actor.OrganisationWide && !actor.AllowedBranches.Contains(branch))
                throw new KeyNotFoundException();

            ProcurementPrincipalPeople.Person? recorder = null;
            if (role == "confirm")
            {
                if (!ProcurementPrincipalEvidence.Canonical(recorderPrincipalId))
                    throw new ArgumentException("ManualPaymentRecorderRequired");
                recorder = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                    scope.LegalEntityId, recorderPrincipalId!, token);
                if (recorder is null || !recorder.Permissions.Contains("procurement-payment-record")
                    || !recorder.OrganisationWide && !recorder.AllowedBranches.Contains(branch))
                    throw new ArgumentException("ManualPaymentRecorderUnavailable");
            }
            else if (recorderPrincipalId is not null)
                throw new ArgumentException("ManualPaymentRecorderUnexpected");

            var permission = role switch
            {
                "approve" => "procurement-payment-approve",
                "record" => "procurement-payment-record",
                _ => "procurement-payment-confirm"
            };
            var search = q?.Trim() ?? "";
            var now = http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            var query = ProcurementPrincipalPeople.Candidates(core, scope, now, branch);
            if (search.Length > 0)
            {
                var pattern = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%")
                    .Replace("_", "\\_") + "%";
                query = query.Where(x => EF.Functions.ILike(x.DisplayName, pattern, "\\"));
            }
            // Scan in small bounded chunks. Apply current action, branch and person
            // independence before cursor paging; never return an unfiltered total.
            const int chunkSize = 64;
            const int maximumScanned = 2048;
            var pageSize = limit ?? 25;
            var results = new List<(Guid Id, string Name)>();
            var cursorSeen = cursor is null;
            var scanned = 0;
            while (scanned < maximumScanned)
            {
                var batch = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id)
                    .Skip(scanned).Take(chunkSize).Select(x => new { x.Id, x.DisplayName })
                    .ToArrayAsync(token);
                if (batch.Length == 0) break;
                scanned += batch.Length;
                foreach (var row in batch)
                {
                    var candidate = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                        scope.LegalEntityId, row.Id.ToString("D"), token);
                    if (candidate is null || !candidate.Permissions.Contains(permission)
                        || !candidate.OrganisationWide && !candidate.AllowedBranches.Contains(branch)) continue;
                    if (role == "approve" && !await ProcurementPrincipalPeople.IndependentNow(core,
                            scope.TenantId, actor.PrincipalId, actor.CoreLinkedWorkerId,
                            candidate.PrincipalId, candidate.CoreLinkedWorkerId, token)) continue;
                    if (role == "confirm" && !await ProcurementPrincipalPeople.IndependentNow(core,
                            scope.TenantId, recorder!.PrincipalId, recorder.CoreLinkedWorkerId,
                            candidate.PrincipalId, candidate.CoreLinkedWorkerId, token)) continue;
                    if (!cursorSeen)
                    {
                        if (row.Id == cursor) cursorSeen = true;
                        continue;
                    }
                    results.Add((row.Id, row.DisplayName));
                    if (results.Count > pageSize) break;
                }
                if (results.Count > pageSize || batch.Length < chunkSize) break;
            }
            if (!cursorSeen) throw new ReadProblem("ManualPaymentAssigneeCursorChanged", 409);
            if (results.Count <= pageSize && scanned >= maximumScanned)
                throw new ReadProblem("ManualPaymentAssigneeLookupTooLarge", 503);
            var page = results.Take(pageSize).ToArray();
            return new
            {
                items = page.Select(x => new { principalId = x.Id.ToString("D"), displayName = x.Name }).ToArray(),
                nextCursor = results.Count > pageSize ? page[^1].Id.ToString("D") : null,
                hasMore = results.Count > pageSize
            };
        }, ct);
}
