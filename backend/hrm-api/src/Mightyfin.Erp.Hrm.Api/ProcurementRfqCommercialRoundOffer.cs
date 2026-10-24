using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private sealed record CommercialOfferInput(Guid BidId, long ExpectedBidVersion,
        Guid OriginalDocumentVersionId, DateTimeOffset ReportedReceivedAt);

    private static async Task<IResult> CommercialSubmitOffer(Guid eventId, Guid id,
        CommercialOfferInput? input, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        try
        {
            if (http.RequestServices.GetService<ProcurementRfqCommercialOptions>()
                    is not { Enabled: true })
                throw new Problem("RfqCommercialUnavailable", 503);
            await RequireCommercialDocumentCommandFoundation(http, null, ct);
            if (eventId == Guid.Empty || id == Guid.Empty || input is null || input.BidId == Guid.Empty
                || input.ExpectedBidVersion < 1 || input.OriginalDocumentVersionId == Guid.Empty
                || input.ReportedReceivedAt == default
                || input.ReportedReceivedAt.Offset != TimeSpan.Zero)
                throw new Problem("RfqCommercialOfferInputRequired", 422);
            var key = Key(http);
            var fingerprint = Fingerprint(new { operation = "SubmitOffer",
                eventId, roundId = id, input.BidId, input.ExpectedBidVersion,
                input.OriginalDocumentVersionId, input.ReportedReceivedAt });
            var result = await http.RequestServices.GetRequiredService<ProcurementApprovalTransaction>()
                .ExecuteWithSourcingDocumentsAsync(eventId, async (db, cases, documents,
                    fence, token) =>
                {
                    await RequireCommercialDocumentCommandFoundation(http, db, token);
                    await RequireCommercial(http, db, true, token);
                    await using var core = await ProcurementApprovalExecutionLocks
                        .CoreAsync(db, scope, token);
                    var auth = http.RequestServices.GetRequiredService<IAuthorizationService>();
                    var actor = await PrincipalActor(core, scope, http, auth,
                        PrincipalRfqSealedManualOffers.SubmitAction,
                        null, false, token);
                    var branches = actor.Branches.ToArray();
                    var opening = await db.PrincipalRfqCommercialRoundOpenings.AsNoTracking()
                        .SingleOrDefaultAsync(x => x.Id == id
                            && x.SourcingEventId == eventId
                            && x.TenantId == scope.TenantId
                            && x.LegalEntityId == scope.LegalEntityId
                            && (actor.SelectedBranchId == null
                                || x.BranchId == actor.SelectedBranchId)
                            && (actor.OrganisationWide || branches.Contains(x.BranchId)),
                            token) ?? throw new Problem("RfqCommercialRoundNotFound", 404);
                    actor = await PrincipalActor(core, scope, http, auth,
                        PrincipalRfqSealedManualOffers.SubmitAction,
                        opening.BranchId, true, token);
                    await LockPrincipalSourcingCommandKey(db, key, token);
                    var store = new PrincipalRfqCommercialRoundStore(db, scope);
                    if (await store.RecoverAsync(key, "SubmitOffer",
                            opening.SourcingEventId, fingerprint, token) is { } replay)
                        return new Reply(replay.ResponseJson, replay.ResponseStatus);
                    var requestIds = await db.SourcingDemandBindings.AsNoTracking()
                        .Where(x => x.SourcingEventId == opening.SourcingEventId)
                        .Select(x => x.PurchaseRequestId).Distinct().OrderBy(x => x)
                        .ToArrayAsync(token);
                    foreach (var requestId in requestIds)
                        await ProcurementApprovalExecutionLocks.LockRequestAsync(db,
                            scope, requestId, token);
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        SELECT id FROM procurement.sourcing_events
                        WHERE tenant_id={scope.TenantId}
                            AND legal_entity_id={scope.LegalEntityId}
                            AND id={opening.SourcingEventId} FOR UPDATE
                        """, token);
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        SELECT id FROM procurement.principal_rfq_commercial_round_openings
                        WHERE tenant_id={scope.TenantId}
                            AND legal_entity_id={scope.LegalEntityId}
                            AND id={id} FOR UPDATE
                        """, token);
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        SELECT id FROM procurement.supplier_bids
                        WHERE tenant_id={scope.TenantId}
                            AND legal_entity_id={scope.LegalEntityId}
                            AND id={input.BidId} FOR UPDATE
                        """, token);
                    var retained = await LoadRetainedCommercialApproval(db, cases,
                        scope, opening, token);
                    await RequireCommercialSourceHolds(db, cases, scope,
                        retained.Event, retained.Original, actor, token);
                    var bid = await db.SupplierBids.AsNoTracking()
                        .SingleOrDefaultAsync(x => x.Id == input.BidId
                            && x.SourcingEventId == opening.SourcingEventId
                            && x.SourceChannel == "Manual"
                            && x.SurrogateEnteredBy == actor.PrincipalId,
                            token) ?? throw new Problem("RfqCommercialQuoteNotFound", 404);
                    if (bid.Version != input.ExpectedBidVersion || bid.Status != "Draft"
                        || bid.InvitationId is null || bid.DraftRevision < 1)
                        throw new Problem("RfqCommercialQuoteChanged", 409);
                    var invitation = await db.SourcingInvitations.AsNoTracking()
                        .SingleOrDefaultAsync(x => x.Id == bid.InvitationId
                            && x.SourcingEventId == opening.SourcingEventId
                            && x.SupplierId == bid.SupplierId
                            && x.Status == "ManualContactRecorded", token)
                        ?? throw new Problem("RfqCommercialInvitationChanged", 409);
                    var supplier = await db.Suppliers.AsNoTracking()
                        .Include(x => x.Qualifications)
                        .SingleOrDefaultAsync(x => x.Id == bid.SupplierId, token)
                        ?? throw new Problem("RfqCommercialSupplierUnavailable", 409);
                    var now = ProcurementEvidenceEncoding.DatabaseTimestamp(
                        DateTimeOffset.UtcNow)!.Value;
                    if (!SupplierEligibility.CanReceiveNewOrders(supplier, now))
                        throw new Problem("RfqCommercialSupplierUnavailable", 409);
                    if (await db.PrincipalRfqCommercialOfferSubmissions.AsNoTracking()
                        .AnyAsync(x => x.OpeningId == id
                            && (x.SupplierBidId == bid.Id
                                || x.SupplierId == bid.SupplierId), token))
                        throw new Problem("RfqCommercialOfferAlreadySubmitted", 409);
                    var draftVersion = await db.SourcingManualQuoteVersions.AsNoTracking()
                        .Where(x => x.SupplierBidId == bid.Id)
                        .OrderByDescending(x => x.Revision).FirstOrDefaultAsync(token)
                        ?? throw new Problem("RfqCommercialQuoteEvidenceMissing", 409);
                    var sourceLines = await db.SourcingLines.AsNoTracking()
                        .Where(x => x.SourcingEventId == opening.SourcingEventId)
                        .ToArrayAsync(token);
                    var bidLines = await db.SupplierBidLines.AsNoTracking()
                        .Where(x => x.SupplierBidId == bid.Id).ToArrayAsync(token);
                    var originalVersion = await (from version in documents.Versions.AsNoTracking()
                        join document in documents.Documents.AsNoTracking()
                            on version.DocumentId equals document.Id
                        where version.Id == input.OriginalDocumentVersionId
                            && version.TenantId == scope.TenantId
                            && version.LegalEntityId == scope.LegalEntityId
                            && document.TenantId == scope.TenantId
                            && document.LegalEntityId == scope.LegalEntityId
                            && document.OwnerModule == "procurement"
                            && document.OwnerType == "sourcing_event"
                            && document.OwnerId == opening.SourcingEventId
                            && !document.IsRetired
                            && version.ScanState == DocumentScanState.Available
                        select version).SingleOrDefaultAsync(token)
                        ?? throw new Problem("RfqCommercialOriginalDocumentUnavailable", 409);
                    var submission = PrincipalRfqSealedManualOffers.Submit(retained.Proof,
                        retained.Prepared, retained.Event, invitation, bid,
                        draftVersion, sourceLines, bidLines, actor,
                        originalVersion.Id, originalVersion.Sha256,
                        input.ReportedReceivedAt, now);
                    var use = await StageCommercialOfferOriginalUseAsync(db,
                        documents, fence, scope, retained.Event, bid, actor,
                        key, originalVersion.Id, token);
                    if (use.Sha256 != submission.OriginalDocumentSha256)
                        throw new Problem("RfqCommercialOriginalDocumentChanged", 409);
                    var receipt = store.StageOffer(opening, retained.Decision,
                        submission, actor, key, fingerprint,
                        use.UseSetId, use.SelectionDigest);
                    await db.SaveChangesAsync(token);
                    return new Reply(receipt.ResponseJson, receipt.ResponseStatus);
                }, ct);
            return Results.Content(result.Body, "application/json",
                statusCode: result.Status);
        }
        catch (Exception error) when (error is Problem or InvalidOperationException
            or ArgumentException or UnauthorizedAccessException or KeyNotFoundException
            or JsonException or ApprovedDemandLoadException or DbUpdateException or NpgsqlException
            || ProcurementPostgresFailures.IsStoreFailure(error))
        {
            var status = error is Problem problem ? problem.Status
                : ProcurementPostgresFailures.IsConcurrency(error) ? 409
                : ProcurementPostgresFailures.IsStoreFailure(error) ? 503
                : error is UnauthorizedAccessException ? 403
                : error is KeyNotFoundException ? 404 : 409;
            return Error(error is Problem ? error.Message
                : status == 503 ? "RfqCommercialOfferUnconfirmed"
                    : "RfqCommercialOfferNeedsReview", status);
        }
    }
}
