using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Microsoft.AspNetCore.Authorization;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private sealed record CommercialOpenOffersInput(long ExpectedEventVersion,
        Guid ExpectedSealId, string ExpectedOfferSetSha256, string? Reason, string? Reference);

    private static async Task RequireOfferOpening(HttpContext http, ProcurementDbContext db,
        bool write, CancellationToken ct)
    {
        if (http.RequestServices.GetService<ProcurementRfqCommercialOptions>() is not { OfferOpeningEnabled: true })
            throw new Problem("RfqOfferOpeningUnavailable", 503);
        await RequireCommercial(http, db, write, ct);
    }

    private static async Task<(PrincipalRfqCommercialRoundOpeningRecord Round, RfqPrincipalActor Actor)>
        OfferOpeningTarget(ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope,
            HttpContext http, IAuthorizationService auth, Guid id, string action, CancellationToken ct)
    {
        var actor = await PrincipalActor(core, scope, http, auth, action, null, false, ct);
        var branches = actor.Branches.ToArray();
        var round = await db.PrincipalRfqCommercialRoundOpenings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.TenantId == scope.TenantId
                && x.LegalEntityId == scope.LegalEntityId
                && (actor.SelectedBranchId == null || x.BranchId == actor.SelectedBranchId)
                && (actor.OrganisationWide || branches.Contains(x.BranchId)), ct)
            ?? throw new Problem("RfqCommercialRoundNotFound", 404);
        actor = await PrincipalActor(core, scope, http, auth, action, round.BranchId, true, ct);
        return (round, actor);
    }

    private static Task<IResult> CommercialOpenOffers(Guid id, CommercialOpenOffersInput? input,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequireOfferOpening(http, db, true, token);
            if (id == Guid.Empty || input is null || input.ExpectedEventVersion < 1
                || input.ExpectedSealId == Guid.Empty || input.ExpectedOfferSetSha256 is not { Length: 64 }
                || input.Reason?.Length > 1000 || input.Reference?.Length > 200)
                throw new Problem("RfqOfferOpeningInputRequired", 422);
            var (round, actor) = await OfferOpeningTarget(db, core, scope, http, auth,
                id, PrincipalRfqOfferOpeningControls.OpenAction, token);
            var key = Key(http);
            var fingerprint = Fingerprint(new { operation = "OpenOffers", roundId = id,
                input.ExpectedEventVersion, input.ExpectedSealId,
                input.ExpectedOfferSetSha256, input.Reason, input.Reference });
            await LockPrincipalSourcingCommandKey(db, key, token);
            var store = new PrincipalRfqOfferOpeningStore(db, scope);
            // Original actor/action/scope precede recovery; current commercial
            // eligibility/policy must not erase a completed original ack.
            if (await store.RecoverAsync(id, key, fingerprint, token) is { } replay)
                return new Reply(replay.ResponseJson, replay.ResponseStatus);
            await LockOfferOpeningSources(db, scope, round, token);
            var retained = await LoadRetainedCommercialApproval(db, cases, scope, round, token);
            if (retained.Event.Version != input.ExpectedEventVersion)
                throw new Problem("RfqCommercialSourceChanged", 409);
            await RequireCommercialSourceHolds(db, cases, scope,
                retained.Event, retained.Original, actor, token);
            if (await db.PrincipalRfqOfferOpenings.AsNoTracking().AnyAsync(x => x.OpeningId == id, token))
                throw new Problem("RfqOffersAlreadyOpened", 409);
            var (policy, controls) = await OpeningPolicy(db, scope, round, token);
            var seal = await db.PrincipalRfqCommercialRoundSeals.AsNoTracking()
                .SingleOrDefaultAsync(x => x.OpeningId == id, token)
                ?? throw new Problem("RfqOfferSealRequired", 409);
            if (seal.Id != input.ExpectedSealId || seal.SubmissionSetSha256 != input.ExpectedOfferSetSha256)
                throw new Problem("RfqOfferSealChanged", 409);
            if (controls.OfficerPrincipalId != actor.PrincipalId)
                throw new Problem("RfqOfferOpeningOfficerDenied", 403);
            var recorders = await OpeningRecorders(db, round.Id, token);
            var (conflicts, aliases) = await OpeningConflicts(db, core, scope,
                retained, recorders, actor, controls, token);
            if (!controls.AllowClosingOfficer)
                conflicts = conflicts.Append(new RfqCommercialConflictParticipant(
                    round.ClosingOfficerPrincipalId, round.ClosingOfficerWorkerAliasId)).ToArray();
            PrincipalRfqOfferOpening.RequireActor(retained.Proof, controls, actor, aliases,
                PrincipalRfqOfferOpeningControls.OpenAction, true);
            PrincipalRfqOfferOpening.RequireIndependent(actor, conflicts, aliases);
            var offers = await RetainedOpeningOffers(http, db, scope, round, seal, actor,
                PrincipalRfqOfferOpeningControls.OpenAction, token);
            var proof = PrincipalRfqOfferOpening.Open(retained.Proof, round.SnapshotJson,
                seal, policy, offers.Select(OfferProof).ToArray(), actor, conflicts, aliases,
                ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value,
                input.Reason, input.Reference);
            var receipt = store.Stage(proof, round, seal, actor, key, fingerprint);
            await db.SaveChangesAsync(token);
            return new Reply(receipt.ResponseJson, receipt.ResponseStatus);
        }, ct);

    private static async Task LockOfferOpeningSources(ProcurementDbContext db,
        ProcurementRequestScope scope, PrincipalRfqCommercialRoundOpeningRecord round, CancellationToken ct)
    {
        var requests = await db.SourcingDemandBindings.AsNoTracking()
            .Where(x => x.SourcingEventId == round.SourcingEventId)
            .Select(x => x.PurchaseRequestId).Distinct().OrderBy(x => x).ToArrayAsync(ct);
        foreach (var request in requests)
            await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, request, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT id FROM procurement.sourcing_events WHERE tenant_id={scope.TenantId}
              AND legal_entity_id={scope.LegalEntityId} AND id={round.SourcingEventId} FOR UPDATE
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT id FROM procurement.principal_rfq_commercial_round_openings WHERE tenant_id={scope.TenantId}
              AND legal_entity_id={scope.LegalEntityId} AND id={round.Id} FOR UPDATE
            """, ct);
    }

    private static async Task<(PolicyVersion Policy, PrincipalRfqOfferOpeningControls Controls)>
        OpeningPolicy(ProcurementDbContext db, ProcurementRequestScope scope,
            PrincipalRfqCommercialRoundOpeningRecord round, CancellationToken ct, bool currentEffects = true)
    {
        var policy = await db.PolicyVersions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == round.CommercialPolicyVersionId && x.PolicyKey == PrincipalRfqCommercialRoundPolicyRules.PolicyKey, ct)
            ?? throw new Problem("RfqOfferOpeningPolicyUnavailable", 409);
        var lineage = await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db, scope, policy, ct,
            currentEffects ? OperationalPolicyPublicationMode.CurrentPublished : OperationalPolicyPublicationMode.RetainedHistorical);
        var rules = currentEffects ? PrincipalRfqCommercialRoundPolicyRules.ResolveEffective(policy,
            scope.TenantId, scope.LegalEntityId, ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value)
            : PrincipalRfqCommercialRoundPolicyRules.Parse(policy.RulesJson);
        if (lineage.Published.Id != round.PublishedPolicyIdentityId
            || rules.RulesSha256 != round.PolicyRulesSha256 || rules.OfferOpening is null
            || !PrincipalRfqOfferOpeningControls.MatchesRoundSnapshot(round.SnapshotJson, rules))
            throw new Problem("RfqOfferOpeningPolicyUnavailable", 409);
        var prepared = await db.PrincipalRfqPreparedPlans.AsNoTracking().SingleAsync(x => x.Id == round.PreparedPlanId, ct);
        var head = await db.PrincipalRfqCommercialDrafts.AsNoTracking().SingleAsync(x => x.Id == prepared.DraftId, ct);
        var revision = await db.PrincipalRfqCommercialDraftRevisions.AsNoTracking().SingleAsync(x => x.Id == prepared.DraftRevisionId, ct);
        PrincipalRfqPreparedPlanEvidence.Verify(prepared, head, revision);
        var definition = PrincipalRfqCommercialDraft.Restore(head, revision).EvaluationDefinitionJson;
        if (definition is null) throw new Problem("RfqCommercialEvaluatorPlanUnavailable", 409);
        var evaluators = PrincipalRfqEvaluationPlanDefinition.Parse(definition).Criteria
            .SelectMany(x => x.EvaluatorPrincipalIds).ToHashSet(StringComparer.Ordinal);
        if (rules.OfferOpening.ReaderPrincipalIds.Any(x => !evaluators.Contains(x)))
            throw new Problem("RfqOpenedOfferReaderNotAssigned", 409);
        return (policy, rules.OfferOpening);
    }

    private static async Task<PrincipalRfqCommercialOfferSubmissionRecord[]> RetainedOpeningOffers(
        HttpContext http, ProcurementDbContext db, ProcurementRequestScope scope,
        PrincipalRfqCommercialRoundOpeningRecord round, PrincipalRfqCommercialRoundSealRecord seal,
        RfqPrincipalActor actor, string action, CancellationToken ct)
    {
        var close = await db.PrincipalRfqCommercialCommandReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SealTargetId == seal.Id && x.Operation == "Close"
                && x.OpeningId == round.Id && x.ActorPrincipalId == seal.OfficerPrincipalId, ct)
            ?? throw new Problem("RfqOfferSealReceiptChanged", 409);
        RequireOpeningOriginalReceipt(close, round, seal.Id, "Close", 200);
        using (var json = JsonDocument.Parse(close.ResponseJson))
        {
            var result = json.RootElement.GetProperty("result");
            if (result.GetProperty("roundId").GetGuid() != round.Id
                || result.GetProperty("state").GetString() != "Closed"
                || result.GetProperty("sealId").GetGuid() != seal.Id
                || result.GetProperty("offerCount").GetInt32() != seal.SubmissionCount
                || result.GetProperty("submissionSetSha256").GetString() != seal.SubmissionSetSha256)
                throw new Problem("RfqOfferSealReceiptChanged", 409);
        }
        // Caller has established current action, target branch and named policy
        // purpose BEFORE this count or any offer payload is loaded.
        var count = await db.PrincipalRfqCommercialOfferSubmissions.CountAsync(x => x.OpeningId == round.Id, ct);
        if (count > 500 || count != seal.SubmissionCount) throw new Problem("RfqOfferSetChanged", 409);
        var offers = await db.PrincipalRfqCommercialOfferSubmissions.AsNoTracking()
            .Where(x => x.OpeningId == round.Id).OrderBy(x => x.SupplierBidId).ToArrayAsync(ct);
        if (offers.Length == 0) return offers;
        var configuration = http.RequestServices.GetRequiredService<IConfiguration>();
        if (http.RequestServices.GetService<CoreDocumentFeature>() is not { Enabled: true }
            || !InvoiceDocumentStoreBinding.SamePhysicalDatabase(configuration.GetConnectionString("CoreDocuments"),
                db.Database.GetDbConnection().ConnectionString)
            || !await ProcurementInvoiceReviewDocumentReadiness.ReadyAsync(db, scope,
                write: false, retire: false, ct))
            throw new Problem("RfqOfferOpeningDocumentsUnavailable", 503);
        await using var documents = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>()
            .UseNpgsql(db.Database.GetDbConnection()).Options, scope);
        await documents.Database.UseTransactionAsync(db.Database.CurrentTransaction!.GetDbTransaction(), ct);
        foreach (var offer in offers)
        {
            var receipt = await db.PrincipalRfqCommercialCommandReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.OfferTargetId == offer.Id && x.Operation == "SubmitOffer"
                    && x.OpeningId == round.Id && x.ActorPrincipalId == offer.RecorderPrincipalId, ct)
                ?? throw new Problem("RfqOfferOriginalReceiptChanged", 409);
            RequireOpeningOriginalReceipt(receipt, round, offer.Id, "SubmitOffer", 201);
            using (var json = JsonDocument.Parse(receipt.ResponseJson))
            {
                var result = json.RootElement.GetProperty("result");
                if (result.GetProperty("roundId").GetGuid() != round.Id
                    || result.GetProperty("offerId").GetGuid() != offer.Id
                    || result.GetProperty("supplierId").GetGuid() != offer.SupplierId
                    || result.GetProperty("submittedAt").GetDateTimeOffset() != offer.SubmittedAt
                    || result.GetProperty("snapshotSha256").GetString() != offer.SnapshotSha256)
                    throw new Problem("RfqOfferOriginalReceiptChanged", 409);
            }
            if (offer.ApprovedDecisionId != seal.ApprovedDecisionId || offer.BranchId != round.BranchId
                || offer.TenantId != scope.TenantId || offer.LegalEntityId != scope.LegalEntityId)
                throw new Problem("RfqOfferOriginalChanged", 409);
            await RequireRetainedOfferOriginalUseAsync(documents, scope, offer, receipt, actor, ct, action);
        }
        return offers;
    }

    private static void RequireOpeningOriginalReceipt(PrincipalRfqCommercialCommandReceipt receipt,
        PrincipalRfqCommercialRoundOpeningRecord round, Guid target, string operation, int status)
    {
        if (receipt.SchemaVersion != 1 || receipt.OpeningId != round.Id
            || receipt.SourcingEventId != round.SourcingEventId || receipt.BranchId != round.BranchId
            || receipt.TenantId != round.TenantId || receipt.LegalEntityId != round.LegalEntityId
            || receipt.Operation != operation || receipt.TargetId != target || receipt.CommandKey == Guid.Empty
            || receipt.ResponseStatus != status || receipt.ResponseSha256 != ProcurementEvidenceEncoding.Digest(receipt.ResponseJson))
            throw new Problem("RfqOfferOriginalReceiptChanged", 409);
        _ = ProcurementEvidenceEncoding.PolicyJsonDigest(receipt.ResponseJson);
        using var json = JsonDocument.Parse(receipt.ResponseJson);
        var root = json.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("operation").GetString() != operation
            || root.GetProperty("commandKey").GetGuid() != receipt.CommandKey
            || root.GetProperty("openingId").GetGuid() != round.Id || root.GetProperty("eventId").GetGuid() != round.SourcingEventId
            || root.GetProperty("branchId").GetGuid() != round.BranchId || root.GetProperty("targetId").GetGuid() != target
            || root.GetProperty("responseStatus").GetInt32() != status)
            throw new Problem("RfqOfferOriginalReceiptChanged", 409);
    }

    private static PrincipalRfqManualOfferSubmission OfferProof(PrincipalRfqCommercialOfferSubmissionRecord x)
        => new(x.OpeningId, x.SourcingEventId, x.SupplierBidId, x.InvitationId, x.SupplierId,
            x.DraftRevision, x.BidVersion, DraftSnapshotSha(x.SnapshotJson), x.OriginalDocumentVersionId,
            x.OriginalDocumentSha256, x.RecorderPrincipalId, x.RecorderWorkerAliasId,
            x.ReportedReceivedAt, x.SubmittedAt, x.SnapshotJson, x.SnapshotSha256);

    private static async Task<RfqCommercialConflictParticipant[]> OpeningRecorders(
        ProcurementDbContext db, Guid roundId, CancellationToken ct)
    {
        // Bounded identity-only metadata, after named purpose authorization;
        // no sealed prices or document body is read to construct SOD facts.
        var people = await db.PrincipalRfqCommercialOfferSubmissions.AsNoTracking()
            .Where(x => x.OpeningId == roundId).OrderBy(x => x.Id).Take(501)
            .Select(x => new { x.RecorderPrincipalId, x.RecorderWorkerAliasId }).ToArrayAsync(ct);
        if (people.Length > 500) throw new Problem("RfqOfferSetBound", 409);
        return people.Select(x => new RfqCommercialConflictParticipant(
            x.RecorderPrincipalId, x.RecorderWorkerAliasId)).ToArray();
    }

    private static async Task<(RfqCommercialConflictParticipant[] Conflicts, Dictionary<string, Guid?> Aliases)>
        OpeningConflicts(ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope,
            RetainedCommercialApproval retained, IReadOnlyList<RfqCommercialConflictParticipant> recorders,
            RfqPrincipalActor actor, PrincipalRfqOfferOpeningControls controls, CancellationToken ct)
    {
        // Always server-derived; no request supplies participants or worker facts.
        var people = retained.Original.Lines.SelectMany(x => new[] {
                new RfqCommercialConflictParticipant(x.MakerPrincipalId, x.MakerWorkerAlias),
                new RfqCommercialConflictParticipant(x.RequesterPrincipalId, x.RequesterWorkerAlias) })
            .Append(new(retained.Original.MakerPrincipalId, retained.Original.MakerWorkerAlias))
            .Append(new(retained.Opening.ActorPrincipalId, retained.Opening.ActorWorkerAliasId)).ToList();
        using var snapshot = JsonDocument.Parse(retained.Opening.SnapshotJson);
        foreach (var assignment in snapshot.RootElement.GetProperty("stages").EnumerateArray()
            .SelectMany(x => x.GetProperty("assignments").EnumerateArray()))
            people.Add(new(assignment.GetProperty("AssigneeSubjectId").GetString()!,
                assignment.GetProperty("WorkerAliasId").ValueKind == JsonValueKind.Null
                    ? null : assignment.GetProperty("WorkerAliasId").GetGuid()));
        var votes = await db.PrincipalRfqCommercialRoundVotes.AsNoTracking()
            .Where(x => x.OpeningId == retained.Opening.Id).ToArrayAsync(ct);
        people.AddRange(votes.Select(x => new RfqCommercialConflictParticipant(x.ActorPrincipalId, x.ActorWorkerAliasId)));
        people.AddRange(recorders);
        var principals = people.Select(x => x.PrincipalId).Append(actor.PrincipalId)
            .Append(retained.Opening.ClosingOfficerPrincipalId).Append(controls.OfficerPrincipalId)
            .Concat(controls.ReaderPrincipalIds).Distinct(StringComparer.Ordinal).ToArray();
        if (principals.Any(x => !ProcurementPrincipalEvidence.Canonical(x)))
            throw new Problem("RfqOpeningParticipantChanged", 409);
        var ids = principals.Select(x => Guid.ParseExact(x, "D")).ToArray();
        var aliases = await core.LocalUsers.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && ids.Contains(x.Id))
            .Select(x => new { x.Id, x.WorkerId }).ToDictionaryAsync(x => x.Id.ToString("D"), x => x.WorkerId, ct);
        if (aliases.Count != ids.Length || aliases.Values.Any(x => !ProcurementPrincipalEvidence.OptionalWorker(x)))
            throw new Problem("RfqOpeningAliasesUnavailable", 409);
        return (people.Distinct().ToArray(), aliases);
    }
}
