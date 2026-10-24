using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private sealed record CommercialCloseInput(long ExpectedEventVersion,
        string ExpectedOpeningSha256, int ExpectedOfferCount);

    private static Task<IResult> CommercialClose(Guid id, CommercialCloseInput? input,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequireCommercial(http, db, true, token);
            if (id == Guid.Empty || input is null || input.ExpectedEventVersion < 1
                || input.ExpectedOfferCount is < 0 or > 500
                || input.ExpectedOpeningSha256 is not { Length: 64 })
                throw new Problem("RfqCommercialCloseInputRequired", 422);
            var key = Key(http);
            var fingerprint = Fingerprint(new { operation = "CloseCommercialRound",
                roundId = id, input.ExpectedEventVersion,
                input.ExpectedOpeningSha256, input.ExpectedOfferCount });
            var actor = await PrincipalActor(core, scope, http, auth,
                PrincipalRfqSealedManualOffers.CloseAction, null, false, token);
            var branches = actor.Branches.ToArray();
            var opening = await db.PrincipalRfqCommercialRoundOpenings.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == id && x.TenantId == scope.TenantId
                    && x.LegalEntityId == scope.LegalEntityId
                    && (actor.SelectedBranchId == null || x.BranchId == actor.SelectedBranchId)
                    && (actor.OrganisationWide || branches.Contains(x.BranchId)), token)
                ?? throw new Problem("RfqCommercialRoundNotFound", 404);
            actor = await PrincipalActor(core, scope, http, auth,
                PrincipalRfqSealedManualOffers.CloseAction, opening.BranchId, true, token);
            await LockPrincipalSourcingCommandKey(db, key, token);
            var store = new PrincipalRfqCommercialRoundStore(db, scope);
            if (await store.RecoverAsync(key, "Close", opening.SourcingEventId,
                    fingerprint, token) is { } replay)
                return new Reply(replay.ResponseJson, replay.ResponseStatus);
            var requestIds = await db.SourcingDemandBindings.AsNoTracking()
                .Where(x => x.SourcingEventId == opening.SourcingEventId)
                .Select(x => x.PurchaseRequestId).Distinct().OrderBy(x => x)
                .ToArrayAsync(token);
            foreach (var requestId in requestIds)
                await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope,
                    requestId, token);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT id FROM procurement.sourcing_events
                WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId}
                    AND id={opening.SourcingEventId} FOR UPDATE
                """, token);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT id FROM procurement.principal_rfq_commercial_round_openings
                WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId}
                    AND id={id} FOR UPDATE
                """, token);
            if (opening.SnapshotSha256 != input.ExpectedOpeningSha256)
                throw new Problem("RfqCommercialRoundChanged", 409);
            var retained = await LoadRetainedCommercialApproval(db, cases,
                scope, opening, token);
            if (retained.Event.Version != input.ExpectedEventVersion)
                throw new Problem("RfqCommercialSourceChanged", 409);
            await RequireCommercialSourceHolds(db, cases, scope,
                retained.Event, retained.Original, actor, token);
            if (await db.PrincipalRfqCommercialRoundSeals.AsNoTracking()
                .AnyAsync(x => x.OpeningId == id, token))
                throw new Problem("RfqCommercialRoundAlreadyClosed", 409);
            var offers = await db.PrincipalRfqCommercialOfferSubmissions.AsNoTracking()
                .Where(x => x.OpeningId == id).OrderBy(x => x.SupplierBidId)
                .ToArrayAsync(token);
            if (offers.Length != input.ExpectedOfferCount)
                throw new Problem("RfqCommercialOfferSetChanged", 409);
            await using (var documents = new DocumentDbContext(
                new DbContextOptionsBuilder<DocumentDbContext>()
                    .UseNpgsql(db.Database.GetDbConnection()).Options, scope))
            {
                await documents.Database.UseTransactionAsync(
                    db.Database.CurrentTransaction!.GetDbTransaction(), token);
                foreach (var offer in offers)
                {
                    var originalReceipt = await db.PrincipalRfqCommercialCommandReceipts
                        .AsNoTracking().SingleOrDefaultAsync(x => x.OfferTargetId == offer.Id
                            && x.Operation == "SubmitOffer" && x.OpeningId == id
                            && x.ActorPrincipalId == offer.RecorderPrincipalId, token)
                        ?? throw new Problem("RfqCommercialOfferReceiptChanged", 409);
                    if (originalReceipt.ResponseSha256 != ProcurementEvidenceEncoding
                            .Digest(originalReceipt.ResponseJson))
                        throw new Problem("RfqCommercialOfferReceiptChanged", 409);
                    await RequireRetainedOfferOriginalUseAsync(documents, scope,
                        offer, originalReceipt, actor, token);
                }
            }
            var submissions = offers.Select(x => new PrincipalRfqManualOfferSubmission(
                x.OpeningId, x.SourcingEventId, x.SupplierBidId, x.InvitationId,
                x.SupplierId, x.DraftRevision, x.BidVersion,
                DraftSnapshotSha(x.SnapshotJson), x.OriginalDocumentVersionId,
                x.OriginalDocumentSha256, x.RecorderPrincipalId,
                x.RecorderWorkerAliasId, x.ReportedReceivedAt, x.SubmittedAt,
                x.SnapshotJson, x.SnapshotSha256)).ToArray();
            var now = ProcurementEvidenceEncoding.DatabaseTimestamp(
                DateTimeOffset.UtcNow)!.Value;
            var seal = PrincipalRfqSealedManualOffers.Close(retained.Proof,
                submissions, actor, now);
            var receipt = await store.StageCloseAsync(opening, retained.Decision,
                seal, actor, key, fingerprint, token);
            await db.SaveChangesAsync(token);
            return new Reply(receipt.ResponseJson, receipt.ResponseStatus);
        }, ct);

    private static string DraftSnapshotSha(string snapshot)
    {
        using var document = System.Text.Json.JsonDocument.Parse(snapshot);
        return document.RootElement.GetProperty("draftSnapshotSha256").GetString()
            ?? throw new Problem("RfqCommercialOfferChanged", 409);
    }
}
