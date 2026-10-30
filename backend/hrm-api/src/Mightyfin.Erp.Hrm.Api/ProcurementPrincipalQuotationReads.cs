using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private static Task<IResult> QuotationSources(HttpContext http, ProcurementRequestScope scope, CancellationToken ct) =>
        RunQuotation(http, scope, async (db, cases, core, auth, token) =>
        {
            var actor = await PrincipalActor(core, scope, http, auth, "procurement-quotation-read", null, false, token);
            var branches = actor.Branches.ToArray();
            var query = QuotationOwnedSources(db).AsNoTracking().Where(x => x.Status == "Published" || x.Status == "Cancelled");
            if (scope.WorkLocationId is { } branch) query = query.Where(x => x.BranchRef == branch);
            if (!actor.OrganisationWide) query = query.Where(x => x.BranchRef != null && branches.Contains(x.BranchRef.Value));
            var sources = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(101).ToArrayAsync(token);
            var result = new List<object>();
            foreach (var source in sources.Take(100))
            {
                var retained = await QuotationRetainedSource(db, cases, scope, source, token);
                if (string.IsNullOrWhiteSpace(source.Number) || source.Number.Length > 80)
                    throw new Problem("QuotationSourceReferenceUnavailable", 409);
                result.Add(new { id = source.Id, number = source.Number, title = source.Title,
                    status = source.Status, version = source.Version, branchId = source.BranchRef,
                    currency = source.CurrencyCode, invitationCount = retained.Invitations.Count,
                    detailUrl = $"/api/procurement/v1/sourcing/{source.Id:D}/quotation-drafts" });
            }
            return new(JsonSerializer.Serialize(new { items = result, hasMore = sources.Length > 100 }, Json));
        }, ct);

    private static Task<IResult> QuotationList(Guid id, HttpContext http, ProcurementRequestScope scope, CancellationToken ct) =>
        RunQuotation(http, scope, async (db, cases, core, auth, token) =>
        {
            var source = await QuotationSource(db, core, scope, http, auth, "procurement-quotation-read", id, token);
            var retained = await QuotationRetainedSource(db, cases, scope, source, token);
            var suppliers = retained.Invitations.Select(x => x.SupplierId).ToArray();
            var labels = await db.Suppliers.AsNoTracking().Where(x => suppliers.Contains(x.Id))
                .Select(x => new { x.Id, x.Number, x.DisplayName }).ToArrayAsync(token);
            if (labels.Length != suppliers.Length || labels.Any(x => string.IsNullOrWhiteSpace(x.Number) || string.IsNullOrWhiteSpace(x.DisplayName))
                || string.IsNullOrWhiteSpace(source.Number)) throw new Problem("QuotationSourceReferenceUnavailable", 409);
            var bids = await db.SupplierBids.AsNoTracking().Where(x => x.SourcingEventId == id && x.PrincipalCaptureVersion == 2)
                .OrderBy(x => x.Id).Take(101).ToArrayAsync(token);
            if (bids.Length > 100) throw new Problem("QuotationHistoryNeedsReview", 409);
            var contacts = await db.SourcingManualContacts.AsNoTracking().Where(x => x.SourcingEventId == id).ToArrayAsync(token);
            var readied = await http.RequestServices.GetRequiredService<IProcurementManualQuotationReadiness>().CanRecordAsync(db, token);
            var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, token);
            var canCapture = readied && source.Status == "Published" && scope.WorkLocationId == source.BranchRef
                && !scope.IsExplicitAllBranches && http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled
                && person is not null && person.Permissions.Contains("procurement-quotation-capture")
                && (await auth.AuthorizeAsync(http.User, "procurement-quotation-capture")).Succeeded;
            var canContact = readied && source.Status == "Published" && scope.WorkLocationId == source.BranchRef
                && !scope.IsExplicitAllBranches && http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled
                && person is not null && person.Permissions.Contains("procurement-sourcing-contact")
                && (await auth.AuthorizeAsync(http.User, "procurement-sourcing-contact")).Succeeded;
            if ((canCapture || canContact) && !await QuotationCurrentSourceAvailable(db, cases, source, token))
            { canCapture = false; canContact = false; }
            var eligible = new HashSet<Guid>();
            if (canCapture || canContact)
            {
                var eligibility = await db.Suppliers.AsNoTracking().Include(x => x.Qualifications)
                    .Where(x => suppliers.Contains(x.Id)).ToArrayAsync(token);
                eligible = eligibility.Where(x => SupplierEligibility.CanReceiveNewOrders(x, DateTimeOffset.UtcNow))
                    .Select(x => x.Id).ToHashSet();
            }
            var invitations = retained.Invitations.Select(x => new { id = x.Id, supplierId = x.SupplierId,
                supplierNumber = labels.Single(s => s.Id == x.SupplierId).Number,
                supplierName = labels.Single(s => s.Id == x.SupplierId).DisplayName, status = x.Status, version = x.Version,
                manualContactId = contacts.SingleOrDefault(c => c.InvitationId == x.Id)?.Id,
                allowedActions = x.Status == "ReadyForManualDelivery" && canContact && eligible.Contains(x.SupplierId) ? new[] { "RecordOutsideContact" }
                    : x.Status == "ManualContactRecorded" && canCapture && eligible.Contains(x.SupplierId) && !bids.Any(b => b.InvitationId == x.Id)
                        ? new[] { "CreateDraft" } : Array.Empty<string>() }).ToArray();
            var items = new List<object>();
            foreach (var bid in bids)
            {
                var owned = await LoadQuotationHistory(db, bid, token);
                items.Add(new { id = bid.Id, supplierId = bid.SupplierId,
                    supplierName = labels.Single(x => x.Id == bid.SupplierId).DisplayName,
                    supplierQuoteReference = bid.SupplierQuoteReference, status = bid.Status, version = bid.Version,
                    lastRecordedAt = owned.Latest.RecordedAt });
            }
            return new(JsonSerializer.Serialize(new { source = new { id = source.Id, number = source.Number,
                title = source.Title, status = source.Status, version = source.Version, branchId = source.BranchRef,
                currency = source.CurrencyCode }, invitations, items }, Json));
        }, ct);

    private static Task<IResult> QuotationRead(Guid id, Guid bidId, HttpContext http, ProcurementRequestScope scope, CancellationToken ct) =>
        RunQuotation(http, scope, async (db, cases, core, auth, token) =>
        {
            var source = await QuotationSource(db, core, scope, http, auth, "procurement-quotation-read", id, token);
            _ = await QuotationRetainedSource(db, cases, scope, source, token);
            var bid = await db.SupplierBids.SingleOrDefaultAsync(x => x.Id == bidId && x.SourcingEventId == id
                && x.PrincipalCaptureVersion == 2, token) ?? throw new Problem("QuotationNotFound", 404);
            var loaded = await LoadQuotationHistory(db, bid, token);
            var supplier = await db.Suppliers.AsNoTracking().Where(x => x.Id == bid.SupplierId)
                .Select(x => new { x.Id, x.Number, x.DisplayName }).SingleOrDefaultAsync(token)
                ?? throw new Problem("QuotationSourceReferenceUnavailable", 409);
            if (string.IsNullOrWhiteSpace(source.Number) || string.IsNullOrWhiteSpace(supplier.Number)
                || string.IsNullOrWhiteSpace(supplier.DisplayName)) throw new Problem("QuotationSourceReferenceUnavailable", 409);
            var canRecord = await http.RequestServices.GetRequiredService<IProcurementManualQuotationReadiness>().CanRecordAsync(db, token);
            var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId, scope.LegalEntityId, scope.SubjectId, token);
            var actions = new List<string>();
            if (canRecord && bid.Status == "Draft" && scope.WorkLocationId == source.BranchRef && !scope.IsExplicitAllBranches && person is not null)
            {
                if (person.Permissions.Contains("procurement-quotation-cancel")
                    && (await auth.AuthorizeAsync(http.User, "procurement-quotation-cancel")).Succeeded) actions.Add("CancelDraft");
                if (source.Status == "Published" && http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled
                    && person.Permissions.Contains("procurement-quotation-capture")
                    && (await auth.AuthorizeAsync(http.User, "procurement-quotation-capture")).Succeeded)
                {
                    var currentSource = await QuotationCurrentSourceAvailable(db, cases, source, token);
                    var eligibility = await db.Suppliers.AsNoTracking().Include(x => x.Qualifications)
                        .SingleOrDefaultAsync(x => x.Id == bid.SupplierId, token);
                    if (currentSource && eligibility is not null && SupplierEligibility.CanReceiveNewOrders(eligibility, DateTimeOffset.UtcNow))
                        actions.Add("ReviseDraft");
                }
            }
            return new(JsonSerializer.Serialize(new { id = bid.Id, sourceId = source.Id, sourceNumber = source.Number,
                sourceVersion = source.Version, invitationId = bid.InvitationId,
                invitationVersion = (await db.SourcingInvitations.AsNoTracking().SingleAsync(x => x.Id == bid.InvitationId, token)).Version,
                supplierId = supplier.Id, supplierNumber = supplier.Number, supplierName = supplier.DisplayName,
                status = bid.Status, version = bid.Version, allowedActions = actions,
                snapshot = JsonSerializer.Deserialize<JsonElement>(loaded.Latest.SnapshotJson),
                history = loaded.Summaries, historyTotal = loaded.TotalVersions,
                historyHasMore = loaded.TotalVersions > loaded.Summaries.Length,
                oldestShownVersion = loaded.Summaries.Length == 0 ? (long?)null : loaded.Summaries[0].Version }, Json));
        }, ct);

    private static async Task<bool> QuotationCurrentSourceAvailable(ProcurementDbContext db,
        Mightyfin.Erp.Core.Approvals.Infrastructure.ApprovalDbContext cases, SourcingEvent source, CancellationToken ct)
    {
        try { _ = await PrincipalPublicationEvidence(db, cases, source, ct); return true; }
        catch (Problem error) when (error.Status == 409 && error.Message == "SourcingPublicationEvidenceNeedsReview")
        { return false; }
        catch (InvalidOperationException error) when (error.Message is "SourcingPublishedDemandHoldChanged"
            or "ManualContactSourceChanged" or "ManualContactEvidenceRequired" or "ManualContactInvitationChanged")
        { return false; }
        // Permission, cancellation, provider and database failures are not an
        // eligibility result and must propagate through the owning error path.
    }

    private sealed record QuotationVersionSummary(Guid Id, long Version, string Operation,
        DateTimeOffset RecordedAt, string ActorPrincipalId, string SnapshotSha256);
    private sealed record QuotationHistory(SourcingManualContact Contact, SourcingManualQuoteVersion Latest,
        QuotationVersionSummary[] Summaries, long TotalVersions, SupplierBidLine[] Lines);
    private static async Task<QuotationHistory> LoadQuotationHistory(ProcurementDbContext db, SupplierBid bid, CancellationToken ct)
    {
        var contact = await db.SourcingManualContacts.AsNoTracking().SingleOrDefaultAsync(x => x.InvitationId == bid.InvitationId, ct)
            ?? throw new Problem("QuotationHistoryNeedsReview", 409);
        var summaries = new Queue<QuotationVersionSummary>();
        SourcingManualQuoteVersion? latest = null; Guid? previousId = null;
        DateTimeOffset? previousAt = null; long count = 0;
        // Keyset batches of ONE full owned row bound the eight-MiB snapshot
        // and request bodies. Materialization closes the Npgsql reader before
        // verification (including any future real Core pin-store work).
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (System.Diagnostics.Stopwatch.GetElapsedTime(started) > TimeSpan.FromSeconds(10))
                throw new Problem("QuotationHistoryReadBudgetExceeded", 503);
            var batch = await db.SourcingManualQuoteVersions.AsNoTracking()
                .Where(x => x.SupplierBidId == bid.Id && x.BidVersion > count)
                .OrderBy(x => x.BidVersion).Take(1).ToArrayAsync(ct);
            if (batch.Length == 0) break;
            var row = batch[0];
            count = checked(count + 1);
            if (row.SnapshotSchemaVersion != 3 || row.BidVersion != count || row.PreviousVersionId != previousId
                || previousAt is { } at && row.RecordedAt < at)
                throw new Problem("QuotationHistoryNeedsReview", 409);
            var restored = PrincipalQuotationStorage.Restore(row.SnapshotJson);
            PrincipalManualQuotationEvidence.VerifyQuotation(row, restored.Bid, restored.Lines, contact);
            summaries.Enqueue(Summary(row)); if (summaries.Count > 50) _ = summaries.Dequeue();
            previousId = row.Id; previousAt = row.RecordedAt; latest = row;
        }
        if (latest is null || latest.BidVersion != bid.Version) throw new Problem("QuotationHistoryNeedsReview", 409);
        var lines = await db.SupplierBidLines.Where(x => x.SupplierBidId == bid.Id).ToArrayAsync(ct);
        PrincipalManualQuotationEvidence.VerifyQuotation(latest, bid, lines.Where(x => x.IsActive).ToArray(), contact);
        return new(contact, latest, summaries.ToArray(), count, lines);
    }
    private static QuotationVersionSummary Summary(SourcingManualQuoteVersion row) =>
        new(row.Id, row.BidVersion, row.Operation, row.RecordedAt, row.ActorPrincipalId, row.SnapshotSha256);

    private static Task<IResult> QuotationVersionRead(Guid id, Guid bidId, long version,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct) =>
        RunQuotation(http, scope, async (db, cases, core, auth, token) =>
        {
            var source = await QuotationSource(db, core, scope, http, auth, "procurement-quotation-read", id, token);
            _ = await QuotationRetainedSource(db, cases, scope, source, token);
            if (version < 1 || !await db.SupplierBids.AsNoTracking().AnyAsync(x => x.Id == bidId
                && x.SourcingEventId == id && x.PrincipalCaptureVersion == 2, token))
                throw new Problem("QuotationNotFound", 404);
            var row = await db.SourcingManualQuoteVersions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.SupplierBidId == bidId && x.BidVersion == version && x.SourcingEventId == id, token)
                ?? throw new Problem("QuotationNotFound", 404);
            var contact = await db.SourcingManualContacts.AsNoTracking().SingleAsync(x => x.Id == row.ManualContactId, token);
            var restored = PrincipalQuotationStorage.Restore(row.SnapshotJson);
            PrincipalManualQuotationEvidence.VerifyQuotation(row, restored.Bid, restored.Lines, contact);
            if (row.PreviousVersionId is { } previous)
            {
                var prior = await db.SourcingManualQuoteVersions.AsNoTracking().Where(x => x.Id == previous && x.SupplierBidId == bidId)
                    .Select(x => new { x.BidVersion, x.RecordedAt }).SingleOrDefaultAsync(token);
                if (prior is null || prior.BidVersion != version - 1 || prior.RecordedAt > row.RecordedAt)
                    throw new Problem("QuotationHistoryNeedsReview", 409);
            }
            return new(JsonSerializer.Serialize(new { id = bidId, sourceId = id, sourceNumber = source.Number,
                version, recordedAt = row.RecordedAt, actorPrincipalId = row.ActorPrincipalId,
                snapshot = JsonSerializer.Deserialize<JsonElement>(row.SnapshotJson), snapshotSha256 = row.SnapshotSha256 }, Json));
        }, ct);

    private static Task<IResult> QuotationRecover(Guid key, HttpContext http, ProcurementRequestScope scope, CancellationToken ct) =>
        RunQuotation(http, scope, async (db, cases, core, auth, token) =>
        {
            _ = await PrincipalActor(core, scope, http, auth, "procurement-quotation-read", null, false, token);
            var version = await db.SourcingManualQuoteVersions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.CommandKey == key && x.ActorPrincipalId == scope.SubjectId, token);
            if (version is not null)
            {
                var source = await QuotationSource(db, core, scope, http, auth, "procurement-quotation-read", version.SourcingEventId, token);
                _ = await QuotationRetainedSource(db, cases, scope, source, token);
                var contact = await db.SourcingManualContacts.AsNoTracking().SingleAsync(x => x.Id == version.ManualContactId, token);
                var restored = PrincipalQuotationStorage.Restore(version.SnapshotJson);
                return new(PrincipalManualQuotationEvidence.RecoverQuotation(version, restored.Bid, restored.Lines,
                    contact, scope.SubjectId, key, version.CommandSha256), version.ResponseStatusCode);
            }
            var recorded = await db.SourcingManualContacts.AsNoTracking().SingleOrDefaultAsync(x =>
                x.CommandKey == key && x.ActorPrincipalId == scope.SubjectId, token)
                ?? throw new Problem("QuotationCommandNotFound", 404);
            var parent = await QuotationSource(db, core, scope, http, auth, "procurement-quotation-read", recorded.SourcingEventId, token);
            _ = await QuotationRetainedSource(db, cases, scope, parent, token);
            return new(PrincipalManualQuotationEvidence.RecoverContact(recorded, scope.SubjectId, key, recorded.CommandSha256), recorded.ResponseStatusCode);
        }, ct);
}
