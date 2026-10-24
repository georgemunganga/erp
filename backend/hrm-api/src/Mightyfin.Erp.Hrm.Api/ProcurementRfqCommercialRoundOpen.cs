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
    private sealed record CommercialOpenInput(Guid PreparedPlanId,
        long ExpectedEventVersion, long ExpectedDraftRevision);

    private static Task<IResult> CommercialOpen(Guid id, CommercialOpenInput? input,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequireCommercial(http, db, true, token);
            if (id == Guid.Empty || input is null || input.PreparedPlanId == Guid.Empty
                || input.ExpectedEventVersion < 1 || input.ExpectedDraftRevision < 1)
                throw new Problem("RfqCommercialOpenInputRequired", 422);
            var key = Key(http);
            var requestSha = Fingerprint(new { operation = "OpenCommercialRound",
                eventId = id, input.PreparedPlanId, input.ExpectedEventVersion,
                input.ExpectedDraftRevision });
            var actor = await PrincipalActor(core, scope, http, auth,
                "procurement-sourcing-round-open", null, false, token);
            _ = await PrincipalOwnedEvent(db, core, scope, http, auth, actor,
                "procurement-sourcing-round-open", id, token);
            await LockPrincipalSourcingCommandKey(db, key, token);
            var store = new PrincipalRfqCommercialRoundStore(db, scope);
            if (await store.RecoverAsync(key, "Open", id, requestSha, token) is { } replay)
                return new Reply(replay.ResponseJson, replay.ResponseStatus);

            var source = await LoadCommercialSource(db, cases, core, scope, http, auth,
                id, true, token, "procurement-sourcing-round-open");
            if (source.Event.Status != "Published" || source.Event.ExecutionVersion != 2
                || source.Event.Version != input.ExpectedEventVersion)
                throw new Problem("RfqCommercialPublishedSourceChanged", 409);
            var published = await PrincipalPublicationEvidence(db, cases, source.Event, token);
            var supplierIds = published.Invitations.Select(x => x.SupplierId)
                .Order().ToArray();
            if (supplierIds.Length is < 1 or > 100 || supplierIds.Distinct().Count() != supplierIds.Length)
                throw new Problem("RfqCommercialSupplierAudienceChanged", 409);
            var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            var suppliers = await db.Suppliers.AsNoTracking().Include(x => x.Qualifications)
                .Where(x => supplierIds.Contains(x.Id)).ToArrayAsync(token);
            if (suppliers.Length != supplierIds.Length
                || suppliers.Any(x => !SupplierEligibility.CanReceiveNewOrders(x, now)))
                throw new Problem("RfqCommercialSupplierAudienceUnavailable", 409);
            var draftStore = new PrincipalRfqCommercialDraftStore(db, scope);
            var (head, revision) = await draftStore.LoadAsync(id, token);
            var prepared = await db.PrincipalRfqPreparedPlans.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == input.PreparedPlanId
                    && x.SourcingEventId == id && x.TenantId == scope.TenantId
                    && x.LegalEntityId == scope.LegalEntityId, token)
                ?? throw new Problem("RfqCommercialPreparedPlanNotFound", 404);
            if (head.Revision != input.ExpectedDraftRevision
                || revision.Revision != input.ExpectedDraftRevision
                || prepared.DraftRevision != input.ExpectedDraftRevision
                || prepared.ActorPrincipalId != source.Actor.PrincipalId)
                throw new Problem("RfqCommercialPreparedPlanChanged", 409);
            PrincipalRfqPreparedPlanEvidence.Verify(prepared, head, revision);
            await using (var documents = new DocumentDbContext(
                new DbContextOptionsBuilder<DocumentDbContext>()
                    .UseNpgsql(db.Database.GetDbConnection()).Options, scope))
            {
                await documents.Database.UseTransactionAsync(
                    db.Database.CurrentTransaction!.GetDbTransaction(), token);
                await RequireRetainedCommercialRequirementsAsync(db, documents, scope,
                    prepared, head, revision, token);
            }
            var policy = await db.PolicyVersions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.PolicyKey == PrincipalRfqCommercialRoundPolicyRules.PolicyKey
                && x.State == "Published" && x.EffectiveFrom <= now
                && (x.EffectiveTo == null || now < x.EffectiveTo), token)
                ?? throw new Problem("RfqCommercialPolicyUnavailable", 409);
            var policyIdentity = await ProcurementOperationalPolicyWorkerIdentity
                .VerifyPublicationAsync(db, scope, policy, token);
            var rules = PrincipalRfqCommercialRoundPolicyRules.ResolveEffective(policy,
                scope.TenantId, scope.LegalEntityId, now);
            var reviewerIds = rules.Stages.SelectMany(x => x.PrincipalIds)
                .Distinct(StringComparer.Ordinal).ToArray();
            var reviewers = new List<RfqPrincipalActor>();
            foreach (var principal in reviewerIds)
                reviewers.Add(await CurrentCommercialParticipant(core, scope, principal,
                    prepared.BranchId, PrincipalRfqCommercialRoundPolicyRules.ApprovalAction,
                    token));
            var closer = await CurrentCommercialParticipant(core, scope,
                rules.ClosingOfficerPrincipalId, prepared.BranchId,
                PrincipalRfqSealedManualOffers.CloseAction, token);
            var opening = PrincipalRfqCommercialRoundCoreWorkflow.Open(prepared, head,
                revision, source.Event, source.Original, policy, reviewers, closer,
                supplierIds, now);
            cases.Cases.Add(opening.Case);
            await cases.SaveChangesAsync(token);
            var receipt = store.StageOpening(opening, prepared, source.Event, policy,
                policyIdentity.Published.Id, source.Actor, key, requestSha);
            await db.SaveChangesAsync(token);
            return new Reply(receipt.ResponseJson, receipt.ResponseStatus);
        }, ct);

    private static async Task<RfqPrincipalActor> CurrentCommercialParticipant(
        Mightyfin.Erp.Hrm.Infrastructure.Data.HrmDbContext core,
        ProcurementRequestScope scope, string principal, Guid branch,
        string permission, CancellationToken ct)
    {
        var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, principal, ct)
            ?? throw new Problem("RfqCommercialParticipantUnavailable", 409);
        if (!person.Permissions.Contains(permission)
            || !person.OrganisationWide && !person.AllowedBranches.Contains(branch))
            throw new Problem("RfqCommercialParticipantUnavailable", 409);
        return new(person.PrincipalId, person.CoreLinkedWorkerId, scope.TenantId,
            scope.LegalEntityId, null, person.AllowedBranches,
            person.OrganisationWide, person.Permissions);
    }
}
