using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private static Task<IResult> CommercialOpenedOffers(Guid id, int? offset, int? limit,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequireOfferOpening(http, db, false, token);
            if (id == Guid.Empty || offset is < 0 or > 100000 || limit is < 1 or > 25)
                throw new Problem("RfqOpenedOfferPageInvalid", 422);
            var (round, actor) = await OfferOpeningTarget(db, core, scope, http, auth,
                id, PrincipalRfqOfferOpeningControls.ReadAction, token);
            var (policy, controls) = await OpeningPolicy(db, scope, round, token, currentEffects: false);
            if (!controls.ReaderPrincipalIds.Contains(actor.PrincipalId, StringComparer.Ordinal))
                throw new Problem("RfqOpenedOfferReaderDenied", 403);
            var opened = await db.PrincipalRfqOfferOpenings.AsNoTracking()
                .SingleOrDefaultAsync(x => x.OpeningId == id, token)
                ?? throw new Problem("RfqOpenedOffersUnavailable", 404);
            var seal = await db.PrincipalRfqCommercialRoundSeals.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == opened.SealId && x.OpeningId == id, token)
                ?? throw new Problem("RfqOfferSealChanged", 409);
            var original = await db.PrincipalRfqOfferOpeningReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.UnsealingId == opened.Id, token)
                ?? throw new Problem("RfqOfferOpeningReceiptChanged", 409);
            PrincipalRfqOfferOpeningStorage.Verify(opened, round, seal);
            PrincipalRfqOfferOpeningStorage.VerifyReceipt(opened, original);
            var retained = await LoadRetainedCommercialApproval(db, cases, scope, round, token);
            await RequireCommercialSourceHolds(db, cases, scope,
                retained.Event, retained.Original, actor, token);
            var recorders = await OpeningRecorders(db, id, token);
            var (conflicts, aliases) = await OpeningConflicts(db, core, scope,
                retained, recorders, actor, controls, token);
            var closer = new RfqCommercialConflictParticipant(round.ClosingOfficerPrincipalId,
                round.ClosingOfficerWorkerAliasId);
            var openerConflicts = controls.AllowClosingOfficer ? conflicts : conflicts.Append(closer).ToArray();
            // Old officer need not retain today's grant. Its retained AND current
            // aliases still cannot become an original maker/reviewer/recorder.
            PrincipalRfqOfferOpening.RequireRetainedIndependence(
                new(opened.OfficerPrincipalId, opened.OfficerWorkerAliasId), openerConflicts, aliases);
            PrincipalRfqOfferOpening.RequireActor(retained.Proof, controls, actor, aliases,
                PrincipalRfqOfferOpeningControls.ReadAction, false);
            PrincipalRfqOfferOpening.RequireIndependent(actor, conflicts.Append(closer).ToArray(), aliases);
            var offers = await RetainedOpeningOffers(http, db, scope, round, seal, actor,
                PrincipalRfqOfferOpeningControls.ReadAction, token);
            if (PrincipalRfqSealedManualOffers.RetainedSetDigest(retained.Proof,
                    offers.Select(OfferProof).ToArray()) != opened.OfferSetSha256)
                throw new Problem("RfqOpenedOfferSetChanged", 409);
            var page = offers.Skip(offset ?? 0).Take(limit ?? 25).Select(x =>
            {
                using var snapshot = JsonDocument.Parse(x.SnapshotJson);
                var body = snapshot.RootElement;
                return new { offerId = x.Id, x.SupplierId, x.SupplierBidId,
                    x.DraftRevision, x.SubmittedAt, x.SnapshotSha256,
                    originalDocumentVersionId = x.OriginalDocumentVersionId,
                    originalDocumentSha256 = x.OriginalDocumentSha256,
                    currencyCode = body.GetProperty("currencyCode").GetString(),
                    lines = body.GetProperty("lines").Clone() };
            }).ToArray();
            return new Reply(JsonSerializer.Serialize(new
            {
                roundId = id, unsealingId = opened.Id, state = "OffersOpened",
                items = page, total = offers.Length, offset = offset ?? 0, limit = limit ?? 25,
                evaluationAvailable = false, awardAvailable = false, purchaseOrderAvailable = false,
            }, Json));
        }, ct);

    private static Task<IResult> CommercialOfferOpeningRecovery(Guid id, Guid key,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequireOfferOpening(http, db, false, token);
            _ = await OfferOpeningTarget(db, core, scope, http, auth,
                id, PrincipalRfqOfferOpeningControls.OpenAction, token);
            var receipt = await new PrincipalRfqOfferOpeningStore(db, scope)
                .RecoverAsync(id, key, null, token)
                ?? throw new Problem("RfqOfferOpeningCommandNotFound", 404);
            return new Reply(receipt.ResponseJson, receipt.ResponseStatus);
        }, ct);
}
