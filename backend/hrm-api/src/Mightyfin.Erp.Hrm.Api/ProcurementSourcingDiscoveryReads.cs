using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    // Shared current eligibility: picker previews and Create use the same owner checks.
    // No source locks or transitions here; Create owns locks and always revalidates.
    private static async Task<(ApprovedPurchaseSource Source, CompetitiveSourceAvailability Available)> EligibleSource(
        ProcurementDbContext db, ApprovalDbContext cases, HrmDbContext core, ProcurementRequestScope scope,
        HttpContext http, IAuthorizationService auth, Guid requestId, Guid lineId, DateTimeOffset now, CancellationToken ct)
    {
        var source = await ApprovedDemandSourceLoader.LoadAsync(db, cases, scope, requestId, lineId, ct);
        _ = await Actor(core, scope, http, auth, "procurement-sourcing-convert", source.Request.BranchRef, true, ct);
        if (await ProcurementCatalogClassificationBinding.StoredIssue(db, new[] { source.Line }, ct) is not null)
            throw new Problem("SourcingCatalogSourceUnavailable", 409);
        var finance = await ProcurementApprovalFinanceEligibility.CheckAsync(source.Request, source.Finance,
            http.RequestServices.GetRequiredService<ProcurementApprovalExecutionOptions>(), core, db, scope, now, ct);
        if (source.Submission.FinanceEvidenceMode != finance.Mode
            || source.Submission.ManualEvidenceAuthorityRef != finance.Reference)
            throw new Problem("SourcingFinanceAuthorityChanged", 409);
        return (source, CompetitiveSourcingSource.Check(source, scope.TenantId, scope.LegalEntityId, now));
    }

    private static Task<IResult> ApprovedSources(int? page, int? pageSize, string? q,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            var paging = ProcurementSourcingDiscoveryQueries.Paging(page, pageSize, q);
            var actor = await Actor(core, scope, http, auth, "procurement-sourcing-convert", null, false, token);
            var lines = ProcurementSourcingDiscoveryQueries.Candidates(db, scope);
            var requests = ProcurementSourcingDiscoveryQueries.ScopedRequests(db.PurchaseRequests.AsNoTracking(), actor);
            var candidates = from line in lines join request in requests on line.PurchaseRequestId equals request.Id
                             select new { line.Id, line.PurchaseRequestId, request.Number, line.LineNumber, line.Description };
            if (paging.Search.Length > 0) candidates = candidates.Where(x => EF.Functions.ILike(x.Number, paging.Pattern, "\\")
                || EF.Functions.ILike(x.Description, paging.Pattern, "\\"));
            var count = await candidates.CountAsync(token);
            var rows = await candidates.OrderBy(x => x.Number).ThenBy(x => x.PurchaseRequestId).ThenBy(x => x.LineNumber)
                .ThenBy(x => x.Id).Skip(paging.Offset).Take(paging.Size).ToArrayAsync(token);
            var items = new List<object>(); var now = DateTimeOffset.UtcNow;
            foreach (var row in rows)
            {
                try
                {
                    var (source, available) = await EligibleSource(db, cases, core, scope, http, auth,
                        row.PurchaseRequestId, row.Id, now, token);
                    items.Add(new { purchaseRequestId = row.PurchaseRequestId, purchaseRequestLineId = row.Id,
                        requestNumber = row.Number, source.Line.Description, source.Line.UnitCode, source.Line.CategoryCode,
                        expectedRequestVersion = source.Request.Version, sourceLineVersion = source.Line.Version,
                        availableQuantity = ProcurementEvidenceEncoding.Decimal(available.AvailableQuantity),
                        approvedUnitEstimate = ProcurementEvidenceEncoding.Decimal(available.ApprovedUnitPrice),
                        available.CurrencyCode, available.MinimumQuoteCount, available.SourceSubmissionId, available.SourceCaseId,
                        methodId = available.MethodId, methodRevision = available.MethodRevision,
                        state = available.AvailableQuantity > 0 ? "Available" : "FullyAllocated",
                        canSelect = available.AvailableQuantity > 0, canPublish = false });
                }
                catch (Exception error) when (IsDiscoveryEvidenceFailure(error))
                {
                    // Never expose owner errors, policy payloads or monetary facts from a failed proof.
                    items.Add(new { purchaseRequestId = row.PurchaseRequestId, purchaseRequestLineId = row.Id,
                        requestNumber = row.Number, state = "Unavailable", blocker = "SourcingSourceNeedsReview", canSelect = false, canPublish = false });
                }
            }
            ReadOnly(db, cases, core);
            return new Reply(JsonSerializer.Serialize(new { items, candidateTotal = count, page = paging.Number,
                pageSize = paging.Size, eligibilityMeaning = "CurrentPreviewOnly", canPublish = false }, Json));
        }, ct);

    private static Task<IResult> OwnedDrafts(int? page, int? pageSize, string? q, string? state,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            var paging = ProcurementSourcingDiscoveryQueries.Paging(page, pageSize, q);
            if (state is not (null or "Draft" or "Cancelled")) throw new ArgumentException("SourcingDiscoveryStateInvalid");
            var (actor, _) = await ReadActor(core, scope, http, auth, token);
            var query = Owned(db, actor).AsNoTracking().Where(x => x.Status == "Draft" || x.Status == "Cancelled");
            query = ProcurementSourcingDiscoveryQueries.ScopedDrafts(query, actor);
            if (state is not null) query = query.Where(x => x.Status == state);
            if (paging.Search.Length > 0) query = query.Where(x => EF.Functions.ILike(x.Number, paging.Pattern, "\\")
                || EF.Functions.ILike(x.Title, paging.Pattern, "\\"));
            var count = await query.CountAsync(token);
            var rows = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
                .Skip(paging.Offset).Take(paging.Size).ToArrayAsync(token);
            var items = new List<object>();
            foreach (var ev in rows)
            {
                try
                {
                    // Reuse detail's retained integrity proof. Current commercial eligibility is
                    // deliberately not consulted for owned historical drafts/cancellations.
                    _ = await ReadOwned(db, ev, token);
                    items.Add(new { ev.Id, ev.Number, ev.Title, ev.CurrencyCode, ev.Status, ev.Version,
                        originalDraftFingerprint = ev.OriginalDraftFingerprint, state = "VerifiedRetainedDraft", canPublish = false });
                }
                catch (Exception error) when (IsDiscoveryEvidenceFailure(error))
                { items.Add(new { ev.Id, ev.Number, state = "Unavailable", blocker = "SourcingOriginalEvidenceUnavailable", canPublish = false }); }
            }
            ReadOnly(db, cases, core);
            return new Reply(JsonSerializer.Serialize(new { items, total = count, page = paging.Number,
                pageSize = paging.Size, canPublish = false }, Json));
        }, ct);

    internal static bool IsDiscoveryEvidenceFailure(Exception error)
        => !ProcurementPostgresFailures.IsStoreFailure(error)
            && error is ApprovedDemandLoadException or InvalidOperationException or ArgumentException
                or UnauthorizedAccessException or KeyNotFoundException or JsonException or OverflowException or Problem;

    private static void ReadOnly(ProcurementDbContext db, ApprovalDbContext cases, HrmDbContext core)
    {
        if (db.ChangeTracker.HasChanges() || cases.ChangeTracker.HasChanges() || core.ChangeTracker.HasChanges())
            throw new InvalidOperationException("SourcingDiscoveryMustNotWrite");
    }
}
