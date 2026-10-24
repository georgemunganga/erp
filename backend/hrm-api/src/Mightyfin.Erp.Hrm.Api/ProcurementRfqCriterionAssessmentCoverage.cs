using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private static Task<IResult> CommercialCriterionAssessmentCoverage(Guid id, Guid offerId,
        string criterionCode, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequireCriterionAssessment(http, db, false, token);
            if (id == Guid.Empty || offerId == Guid.Empty || criterionCode is not { Length: >= 1 and <= 64 }
                || !criterionCode.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
                throw new Problem("RfqAssessmentCoverageInputInvalid", 422);
            // Current Core read action and branch fence precede any offer or
            // assessment count. The named policy purpose is narrower than sourcing read.
            var (round, actor) = await OfferOpeningTarget(db, core, scope, http, auth,
                id, PrincipalRfqOfferOpeningControls.ReadAction, token);
            var (_, controls) = await OpeningPolicy(db, scope, round, token, currentEffects: false);
            if (!controls.ReaderPrincipalIds.Contains(actor.PrincipalId, StringComparer.Ordinal))
                throw new Problem("RfqAssessmentCoverageReaderDenied", 403);
            var opened = await db.PrincipalRfqOfferOpenings.AsNoTracking()
                .SingleOrDefaultAsync(x => x.OpeningId == id, token)
                ?? throw new Problem("RfqOffersNotOpened", 404);
            var seal = await db.PrincipalRfqCommercialRoundSeals.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == opened.SealId && x.OpeningId == id, token)
                ?? throw new Problem("RfqOfferSealChanged", 409);
            var retained = await LoadRetainedCommercialApproval(db, cases, scope, round, token);
            await RequireCommercialSourceHolds(db, cases, scope,
                retained.Event, retained.Original, actor, token);
            await RequireRetainedOpenedOfferPurpose(db, core, scope, round, opened, seal,
                retained, actor, controls, false, token);
            var offers = await RetainedOpeningOffers(http, db, scope, round, seal, actor,
                PrincipalRfqOfferOpeningControls.ReadAction, token);
            if (PrincipalRfqSealedManualOffers.RetainedSetDigest(retained.Proof,
                    offers.Select(OfferProof).ToArray()) != opened.OfferSetSha256)
                throw new Problem("RfqOpenedOfferSetChanged", 409);
            var offer = offers.SingleOrDefault(x => x.Id == offerId)
                ?? throw new Problem("RfqAssessmentOfferNotOpened", 404);
            var (definition, plan, revision) = await AssessmentPlan(db, round, token);
            var criterion = definition.Criteria.SingleOrDefault(x => x.Code == criterionCode)
                ?? throw new Problem("RfqAssessmentCriterionUnknown", 404);
            // At most 64 named evaluators × 100 immutable revisions for this one
            // opened offer/criterion. Never read another evaluator's score or reason
            // into the response, even though each retained row is fully verified.
            var rows = await db.PrincipalRfqCriterionAssessments.AsNoTracking()
                .Where(x => x.RoundId == id && x.OfferId == offerId
                    && x.CriterionCode == criterionCode && x.TenantId == scope.TenantId
                    && x.LegalEntityId == scope.LegalEntityId && x.BranchId == round.BranchId)
                .OrderBy(x => x.EvaluatorPrincipalId).ThenBy(x => x.Revision)
                .Take(6401).ToArrayAsync(token);
            if (rows.Length > 6400) throw new Problem("RfqAssessmentCoverageBound", 409);
            var assessmentIds = rows.Select(x => x.Id).ToArray();
            var receipts = await db.PrincipalRfqCriterionAssessmentReceipts.AsNoTracking()
                .Where(x => assessmentIds.Contains(x.AssessmentId) && x.RoundId == id && x.OfferId == offerId
                    && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId)
                .ToArrayAsync(token);
            if (receipts.Length != rows.Length || receipts.Select(x => x.AssessmentId).Distinct().Count() != rows.Length)
                throw new Problem("RfqAssessmentCoverageReceiptChanged", 409);
            var byAssessment = receipts.ToDictionary(x => x.AssessmentId);
            foreach (var row in rows)
            {
                if (row.PreparedPlanId != plan.Id || row.EvaluationPlanRevisionId != revision.Id)
                    throw new Problem("RfqAssessmentCoverageSourceChanged", 409);
                PrincipalRfqCriterionAssessmentStorage.Verify(row, round, opened, offer, definition);
                PrincipalRfqCriterionAssessmentStorage.VerifyReceipt(row, byAssessment[row.Id]);
            }
            var coverage = PrincipalRfqAssessmentCoverage.Build(offerId, criterion, rows);
            return new Reply(JsonSerializer.Serialize(new
            {
                roundId = id, unsealingId = opened.Id, offerId, criterionCode,
                openedOfferSetSha256 = opened.OfferSetSha256,
                evaluationDefinitionSha256 = definition.RulesSha256,
                originalOfferSha256 = offer.SnapshotSha256,
                coverage.RequiredObservations, coverage.ObservedObservations, coverage.Slots,
                comparisonAvailable = false, recommendationAvailable = false,
                awardAvailable = false, purchaseOrderAvailable = false,
            }, Json));
        }, ct);
}
