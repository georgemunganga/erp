using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Internal publication review only. Supplier delivery is always manual and
/// remains unrecorded until a separate scoped contact command.</summary>
public static partial class ProcurementSourcingExecutionRoutes
{
    public sealed record PrincipalPublicationOpenInput(long ExpectedEventVersion,
        string OriginalDraftFingerprint, Guid[] SupplierIds);
    public sealed record PrincipalPublicationDecisionInput(long ExpectedEventVersion,
        long ExpectedCaseVersion, long ExpectedAssignmentVersion, string Action, string? Reason);

    private static void MapPrincipalPublicationRoutes(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/principal-publication-reviews", PrincipalPublicationOpen)
            .RequireAuthorization("procurement-sourcing-submit");
        group.MapPost("/{id:guid}/principal-publication-reviews/{caseId:guid}/assignments/{assignmentId:guid}/decisions",
            PrincipalPublicationDecide).RequireAuthorization("procurement-sourcing-publish");
        group.MapGet("/principal-publication-commands/{key:guid}", PrincipalPublicationRecover)
            .RequireAuthorization("procurement-access");
        group.MapGet("/principal-publications", PrincipalPublicationList)
            .RequireAuthorization("procurement-access");
        group.MapGet("/principal-publication-reviews/assigned", PrincipalPublicationAssigned)
            .RequireAuthorization("procurement-sourcing-publish");
        group.MapGet("/{id:guid}/principal-publication-suppliers", PrincipalPublicationSuppliers)
            .RequireAuthorization("procurement-sourcing-submit");
        group.MapGet("/{id:guid}/principal-publication", PrincipalPublicationRead)
            .RequireAuthorization("procurement-access");
    }

    private static async Task RequirePrincipalPublication(HttpContext http, ProcurementDbContext db, CancellationToken ct)
    {
        if (!http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled
            || !PrincipalSourceAvailable(http)
            || !HttpMethods.IsGet(http.Request.Method)
                && !await ProcurementPrincipalApprovalCapability.ConfiguredStoreReadyAsync(http.RequestServices, ct)
            || !await http.RequestServices.GetRequiredService<IProcurementPrincipalSourcingPublicationReadiness>()
                .IsReadyAsync(db, !HttpMethods.IsGet(http.Request.Method), ct))
            throw new Problem("PrincipalSourcingPublicationUnavailable", 503);
    }

    private static Task<IResult> PrincipalPublicationOpen(Guid id, PrincipalPublicationOpenInput? input,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
    {
        if (!http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled)
            return Task.FromResult(Error("PrincipalSourcingPublicationUnavailable", 503));
        return Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequirePrincipalPublication(http, db, token);
            var key = Key(http);
            await LockPrincipalSourcingCommandKey(db, key, token);
            if (input is null || id == Guid.Empty || input.ExpectedEventVersion < 1
                || input.OriginalDraftFingerprint is not { Length: 64 }
                || input.SupplierIds is not { Length: > 0 and <= 100 }
                || input.SupplierIds.Any(x => x == Guid.Empty)
                || input.SupplierIds.Distinct().Count() != input.SupplierIds.Length)
                throw new Problem("SourcingPublicationInputInvalid", 422);
            var actor = await PrincipalActor(core, scope, http, auth, "procurement-sourcing-submit", null, false, token);
            var fingerprint = Fingerprint(new { schemaVersion = 2, operation = "OpenPublicationReview", id,
                input.ExpectedEventVersion, input.OriginalDraftFingerprint,
                supplierIds = input.SupplierIds.Order().ToArray() });
            if (await PrincipalPublicationReplay(db, core, scope, http, auth, actor,
                    "procurement-sourcing-submit", key, fingerprint, id, token) is { } replay)
                return replay;
            await RequirePrincipalSourcingKeyUnused(db, key, token);
            var requestIds = await db.SourcingDemandBindings.AsNoTracking().Where(x => x.SourcingEventId == id)
                .Select(x => x.PurchaseRequestId).Distinct().OrderBy(x => x).ToArrayAsync(token);
            if (requestIds.Length is < 1 or > 200) throw new Problem("SourcingOriginalEvidenceUnavailable", 409);
            foreach (var requestId in requestIds)
                await ProcurementApprovalExecutionLocks.LockRequestAsync(db, scope, requestId, token);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.sourcing_events WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE", token);
            var ev = await PrincipalOwnedEvent(db, core, scope, http, auth, actor,
                "procurement-sourcing-submit", id, token);
            if (ev.Status != "Draft" || ev.Version != input.ExpectedEventVersion
                || ev.OriginalDraftFingerprint != input.OriginalDraftFingerprint)
                throw new Problem("SourcingDraftChanged", 409);
            var sourceEnvelope = await db.SourcingDraftEvidenceRecords.SingleAsync(x => x.Id == ev.SourceEvidenceId, token);
            var bindings = await db.SourcingDemandBindings.AsNoTracking().Where(x => x.SourcingEventId == id)
                .OrderBy(x => x.SourcingLineId).ToArrayAsync(token);
            var draft = GovernedPrincipalRfqRestoration.RestoreVerified(sourceEnvelope, ev, bindings);
            var now = DateTimeOffset.UtcNow;
            var currentClassifications = new List<PolicyScenario>();
            foreach (var pin in draft.Lines.OrderBy(x => x.EventLineId))
            {
                var source = await ApprovedDemandSourceLoader.LoadAsync(db, cases, scope,
                    pin.RequestId, pin.LineId, token);
                var current = CompetitiveSourcingSource.Check(source, scope.TenantId, scope.LegalEntityId, now);
                var hold = source.Allocations.SingleOrDefault(x => x.Id == pin.AllocationId
                    && x.TargetType == "SourcingEvent" && x.TargetId == ev.Id && x.Status == "Reserved");
                if (source.Request.BranchRef != ev.BranchRef || source.Request.CurrencyCode != ev.CurrencyCode
                    || source.Line.CategoryCode != pin.CategoryCode || source.Line.EstimatedUnitPrice != pin.UnitEstimate
                    || current.MethodId != pin.MethodId || current.MethodRevision != pin.MethodRevision
                    || hold is null || hold.Quantity != pin.Quantity || hold.EstimatedValue != pin.EstimatedValue)
                    throw new Problem("SourcingSourceChanged", 409);
                if (await ProcurementCatalogClassificationBinding.StoredIssue(db, [source.Line], token) is not null)
                    throw new Problem("SourcingCatalogSourceUnavailable", 409);
                var finance = await ProcurementApprovalFinanceEligibility.CheckPrincipalAsync(source.Request, source.Finance,
                    http.RequestServices.GetRequiredService<ProcurementApprovalExecutionOptions>(), core, db, scope, now, token);
                if (finance.Mode != source.Submission.FinanceEvidenceMode
                    || finance.Reference != source.Submission.ManualEvidenceAuthorityRef)
                    throw new Problem("SourcingFinanceAuthorityChanged", 409);
                currentClassifications.Add(new(ev.CurrencyCode, pin.EstimatedValue,
                    source.Line.CategoryCode, source.Line.PurchaseType, ev.BranchRef));
            }
            var policy = await db.PolicyVersions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.PolicyKey == SourcingPublicationPolicyRules.PolicyKey && x.State == "Published"
                && x.EffectiveFrom <= now && (x.EffectiveTo == null || x.EffectiveTo > now), token)
                ?? throw new Problem("SourcingPolicyUnavailable", 409);
            var policyIdentity = await db.SourcingPolicyIdentityEvidenceRecords.AsNoTracking()
                .Where(x => x.PolicyVersionId == policy.Id).ToArrayAsync(token);
            var created = policyIdentity.SingleOrDefault(x => x.Stage == "Created")
                ?? throw new Problem("SourcingPolicyIdentityUnavailable", 409);
            var published = policyIdentity.SingleOrDefault(x => x.Stage == "Published")
                ?? throw new Problem("SourcingPolicyIdentityUnavailable", 409);
            var submitted = policyIdentity.SingleOrDefault(x => x.Id == published.SubmittedEvidenceId
                && x.Stage == "Submitted")
                ?? throw new Problem("SourcingPolicyIdentityUnavailable", 409);
            SourcingPolicyIdentity.VerifyPublication(policy, created, submitted, published);
            if (created.SchemaVersion != 2) throw new Problem("PrincipalSourcingPolicyRequired", 409);
            var suppliers = await db.Suppliers.Include(x => x.Qualifications)
                .Where(x => input.SupplierIds.Contains(x.Id)).ToArrayAsync(token);
            if (suppliers.Length != input.SupplierIds.Length)
                throw new Problem("SourcingSupplierUnavailable", 409);
            var previews = currentClassifications.Select(x => SourcingPublicationPolicyRules.Evaluate(policy.RulesJson, x)).ToArray();
            if (previews.Any(x => x.Status != "Match" || x.Controls is null))
                throw new Problem("SourcingPolicyNoMatch", 409);
            var shortlist = PrincipalRfqPublicationPreparation.Prepare(draft, previews[0].Controls!, suppliers, now);
            var names = previews.SelectMany(x => x.Stages).SelectMany(x => x.PrincipalIds)
                .Distinct(StringComparer.Ordinal).ToArray();
            if (names.Length is < 1 or > 100) throw new Problem("SourcingReviewerSetInvalid", 409);
            var reviewers = new List<RfqPrincipalActor>();
            foreach (var name in names)
            {
                var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
                    scope.LegalEntityId, name, token);
                if (person is null || !person.Permissions.Contains(PrincipalRfqPublicationCoreWorkflow.Permission)
                    || !person.OrganisationWide && (ev.BranchRef is not { } branch
                        || !person.AllowedBranches.Contains(branch)))
                    throw new Problem("SourcingReviewerUnavailable", 409);
                if (!await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                        draft.MakerPrincipalId, draft.MakerWorkerAlias,
                        person.PrincipalId, person.CoreLinkedWorkerId, token)
                    || draft.Lines.Any(line => !ProcurementPrincipalEvidence.Independent(person.PrincipalId,
                        person.CoreLinkedWorkerId, line.RequesterPrincipalId, line.RequesterWorkerAlias)))
                    throw new Problem("SourcingReviewerNotIndependent", 409);
                reviewers.Add(new(person.PrincipalId, person.CoreLinkedWorkerId, scope.TenantId,
                    scope.LegalEntityId, null, person.AllowedBranches, person.OrganisationWide,
                    person.Permissions));
            }
            var opening = PrincipalRfqPublicationCoreWorkflow.Open(draft, ev.Version, policy,
                created, submitted, published, currentClassifications, reviewers, shortlist, now);
            cases.AddCase(opening.Case);
            await cases.SaveChangesAsync(token);
            ev.Status = "PublicationReview";
            ev.ApprovalInstanceRef = opening.Case.Id.ToString("D");
            var at = ProcurementEvidenceEncoding.DatabaseTimestamp(now)!.Value;
            var row = new SourcingPublicationOpening
            {
                SourcingEventId = ev.Id, EventVersionAtOpening = checked(ev.Version + 1),
                PolicyVersionId = policy.Id, PolicyVersionNumber = policy.PolicyVersionNumber,
                PolicyRulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
                PublishedPolicyEvidenceId = published.Id, CoreApprovalCaseId = opening.Case.Id,
                MakerPrincipalId = actor.PrincipalId, MakerCoreLinkedWorkerId = actor.LinkedWorkerId,
                OriginalDraftFingerprint = draft.OriginalFingerprint,
                SnapshotJson = opening.SnapshotJson, SnapshotSha256 = opening.SnapshotSha256, OpenedAt = at,
            };
            db.AddScoped(row);
            var body = JsonSerializer.Serialize(new { eventId = ev.Id, status = ev.Status,
                version = checked(ev.Version + 1), publicationReviewId = row.Id,
                coreCaseId = opening.Case.Id, coreCaseVersion = opening.Case.Version,
                supplierDeliveryState = "NotStarted", financeReservationChanged = false,
                commandKey = key }, Json);
            db.AddScoped(new SourcingPublicationReceipt
            {
                SourcingEventId = ev.Id, OpeningId = row.Id, Operation = "Open", CommandKey = key,
                ActorPrincipalId = actor.PrincipalId, ActorCoreLinkedWorkerId = actor.LinkedWorkerId,
                CommandSha256 = fingerprint, ResponseJson = body,
                ResponseSha256 = ProcurementEvidenceEncoding.Digest(body), ResponseStatusCode = 201,
                ResultVersion = checked(ev.Version + 1), CompletedAt = at,
            });
            db.AddScoped(new AuditEvent { RecordType = "SourcingEvent", RecordId = ev.Id,
                Action = "PublicationReviewOpened", ActorSubjectId = actor.PrincipalId,
                ChangedFieldsJson = JsonSerializer.Serialize(new { openingId = row.Id,
                    coreCaseId = opening.Case.Id, policyVersionId = policy.Id, snapshotSha256 = opening.SnapshotSha256 }, Json),
                CorrelationId = scope.CorrelationId });
            db.AddScoped(new OutboxEvent { AggregateType = "SourcingEvent", AggregateId = ev.Id,
                EventType = "sourcing.publication_review_opened.v2",
                IdempotencyKey = $"rfq.publication.review.{key:N}.v2",
                PayloadJson = JsonSerializer.Serialize(new { eventId = ev.Id, openingId = row.Id,
                    coreCaseId = opening.Case.Id, internallyPublished = false,
                    supplierDeliveryConfirmed = false, financeReservationChanged = false }, Json) });
            await db.SaveChangesAsync(token);
            return new Reply(body, 201);
        }, ct);
    }

    private static async Task<Reply?> PrincipalPublicationReplay(ProcurementDbContext db,
        HrmDbContext core, ProcurementRequestScope scope, HttpContext http,
        IAuthorizationService auth, RfqPrincipalActor actor, string permission,
        Guid key, string fingerprint, Guid target, CancellationToken ct)
    {
        var receipt = await db.SourcingPublicationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CommandKey == key, ct);
        if (receipt is null) return null;
        if (receipt.ActorPrincipalId != actor.PrincipalId || receipt.ActorCoreLinkedWorkerId != actor.LinkedWorkerId)
            throw new Problem("SourcingReceiptNotFound", 404);
        _ = await PrincipalOwnedEvent(db, core, scope, http, auth, actor, permission,
            receipt.SourcingEventId, ct);
        if (receipt.Operation != "Open" || receipt.SourcingEventId != target
            || receipt.CommandSha256 != fingerprint)
            throw new Problem("SourcingCommandKeyConflict", 409);
        if (receipt.ResponseSha256 != ProcurementEvidenceEncoding.Digest(receipt.ResponseJson)
            || receipt.ResponseStatusCode != 201)
            throw new Problem("SourcingReceiptNeedsReview", 409);
        return new Reply(receipt.ResponseJson, receipt.ResponseStatusCode);
    }

    private static Task<IResult> PrincipalPublicationRecover(Guid key, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        if (!http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled)
            return Task.FromResult(Error("PrincipalSourcingPublicationUnavailable", 503));
        return Run(http, scope, async (db, unusedCases, core, auth, token) =>
        {
            await RequirePrincipalPublication(http, db, token);
            if (key == Guid.Empty) throw new Problem("SourcingCommandKeyRequired", 400);
            var receipt = await db.SourcingPublicationReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CommandKey == key, token)
                ?? throw new Problem("SourcingReceiptNotFound", 404);
            if (receipt.ActorPrincipalId != scope.SubjectId
                || receipt.ActorCoreLinkedWorkerId != scope.CoreLinkedWorkerId)
                throw new Problem("SourcingReceiptNotFound", 404);
            var permission = receipt.Operation == "Open" ? "procurement-sourcing-submit"
                : receipt.Operation == "Decide" ? PrincipalRfqPublicationCoreWorkflow.Permission
                : throw new Problem("SourcingReceiptNeedsReview", 409);
            var ev = await db.SourcingEvents.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == receipt.SourcingEventId && x.ExecutionVersion == 2, token)
                ?? throw new Problem("SourcingReceiptNotFound", 404);
            _ = await PrincipalActor(core, scope, http, auth, permission, ev.BranchRef, true, token);
            if (receipt.Operation == "Open" && ev.OwnerSubjectId != scope.SubjectId)
                throw new Problem("SourcingReceiptNotFound", 404);
            if (receipt.ResponseSha256 != ProcurementEvidenceEncoding.Digest(receipt.ResponseJson))
                throw new Problem("SourcingReceiptNeedsReview", 409);
            return new Reply(receipt.ResponseJson, receipt.ResponseStatusCode);
        }, ct);
    }

    private static Task<IResult> PrincipalPublicationDecide(Guid id, Guid caseId, Guid assignmentId,
        PrincipalPublicationDecisionInput? input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
    {
        if (!http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled)
            return Task.FromResult(Error("PrincipalSourcingPublicationUnavailable", 503));
        return Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequirePrincipalPublication(http, db, token);
            var key = Key(http);
            await LockPrincipalSourcingCommandKey(db, key, token);
            if (input is null) throw new Problem("SourcingPublicationDecisionInvalid", 422);
            var reason = Normalize(input.Reason);
            if (id == Guid.Empty || caseId == Guid.Empty || assignmentId == Guid.Empty
                || input.ExpectedEventVersion < 1 || input.ExpectedCaseVersion < 1
                || input.ExpectedAssignmentVersion < 1 || input.Action is not ("Approve" or "Return" or "Reject")
                || reason.Length > 2000 || input.Action != "Approve" && reason.Length == 0)
                throw new Problem("SourcingPublicationDecisionInvalid", 422);
            var actor = await PrincipalActor(core, scope, http, auth,
                PrincipalRfqPublicationCoreWorkflow.Permission, null, false, token);
            var fingerprint = Fingerprint(new { schemaVersion = 2, operation = "DecidePublicationReview",
                id, caseId, assignmentId, input.ExpectedEventVersion, input.ExpectedCaseVersion,
                input.ExpectedAssignmentVersion, input.Action, reason });
            var previous = await db.SourcingPublicationReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CommandKey == key, token);
            if (previous is not null)
            {
                if (previous.ActorPrincipalId != actor.PrincipalId
                    || previous.ActorCoreLinkedWorkerId != actor.LinkedWorkerId)
                    throw new Problem("SourcingReceiptNotFound", 404);
                var retained = await db.SourcingEvents.AsNoTracking().SingleOrDefaultAsync(x =>
                    x.Id == previous.SourcingEventId && x.ExecutionVersion == 2, token)
                    ?? throw new Problem("SourcingReceiptNotFound", 404);
                _ = await PrincipalActor(core, scope, http, auth,
                    PrincipalRfqPublicationCoreWorkflow.Permission, retained.BranchRef, true, token);
                if (previous.Operation != "Decide" || previous.SourcingEventId != id
                    || previous.CommandSha256 != fingerprint || previous.DecisionId is null)
                    throw new Problem("SourcingCommandKeyConflict", 409);
                if (previous.ResponseSha256 != ProcurementEvidenceEncoding.Digest(previous.ResponseJson)
                    || previous.ResponseStatusCode != 200)
                    throw new Problem("SourcingReceiptNeedsReview", 409);
                return new Reply(previous.ResponseJson, 200);
            }
            await RequirePrincipalSourcingKeyUnused(db, key, token);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.sourcing_events WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE", token);
            await cases.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM core_approval.cases WHERE \"TenantId\"={scope.TenantId} AND \"LegalEntityId\"={scope.LegalEntityId} AND \"Id\"={caseId} FOR UPDATE", token);
            var ev = await db.SourcingEvents.SingleOrDefaultAsync(x => x.Id == id && x.ExecutionVersion == 2, token)
                ?? throw new Problem("SourcingNotFound", 404);
            _ = await PrincipalActor(core, scope, http, auth,
                PrincipalRfqPublicationCoreWorkflow.Permission, ev.BranchRef, true, token);
            if (ev.Status != "PublicationReview" || ev.Version != input.ExpectedEventVersion
                || ev.ApprovalInstanceRef != caseId.ToString("D"))
                throw new Problem("SourcingPublicationReviewChanged", 409);
            var opening = await db.SourcingPublicationOpenings.AsNoTracking()
                .SingleOrDefaultAsync(x => x.SourcingEventId == id && x.CoreApprovalCaseId == caseId, token)
                ?? throw new Problem("SourcingPublicationOpeningMissing", 409);
            if (opening.MakerPrincipalId == actor.PrincipalId
                || !await ProcurementPrincipalPeople.IndependentNow(core, scope.TenantId,
                    opening.MakerPrincipalId, opening.MakerCoreLinkedWorkerId,
                    actor.PrincipalId, actor.LinkedWorkerId, token))
                throw new Problem("SourcingReviewerNotIndependent", 403);
            var approval = await cases.Cases.Include(x => x.Stages).ThenInclude(x => x.Assignments)
                .SingleOrDefaultAsync(x => x.Id == caseId && x.SourceModule == "procurement"
                    && x.SourceType == PrincipalRfqPublicationCoreWorkflow.SourceType
                    && x.SourceId == id, token)
                ?? throw new Problem("SourcingCoreReviewMissing", 409);
            var assignment = approval.Stages.SelectMany(x => x.Assignments)
                .SingleOrDefault(x => x.Id == assignmentId && x.AssigneeSubjectId == actor.PrincipalId
                    && x.PermissionKey == PrincipalRfqPublicationCoreWorkflow.Permission
                    && x.DelegatedFromSubjectId is null)
                ?? throw new Problem("SourcingAssignmentUnavailable", 404);
            if (approval.Version != input.ExpectedCaseVersion
                || assignment.Version != input.ExpectedAssignmentVersion
                || approval.PolicyVersionId != opening.PolicyVersionId
                || approval.PolicyVersionNumber != opening.PolicyVersionNumber
                || approval.SourceVersion != opening.EventVersionAtOpening - 1
                || approval.MakerSubjectId != opening.MakerPrincipalId
                || opening.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(opening.SnapshotJson))
                throw new Problem("SourcingCoreReviewChanged", 409);
            var original = await db.SourcingDraftEvidenceRecords.SingleAsync(x => x.Id == ev.SourceEvidenceId, token);
            var bindings = await db.SourcingDemandBindings.AsNoTracking().Where(x => x.SourcingEventId == id)
                .OrderBy(x => x.SourcingLineId).ToArrayAsync(token);
            var draft = GovernedPrincipalRfqRestoration.RestoreVerified(original, ev, bindings);
            if (draft.Lines.Any(line => !ProcurementPrincipalEvidence.Independent(actor.PrincipalId,
                    actor.LinkedWorkerId, line.MakerPrincipalId, line.MakerWorkerAlias)
                    || !ProcurementPrincipalEvidence.Independent(actor.PrincipalId,
                        actor.LinkedWorkerId, line.RequesterPrincipalId, line.RequesterWorkerAlias)))
                throw new Problem("SourcingReviewerNotIndependent", 403);
            var decisions = await cases.Decisions.Where(x => x.CaseId == caseId).ToListAsync(token);
            var now = DateTimeOffset.UtcNow;
            var result = ApprovalEngine.Decide(approval, assignment,
                new(actor.PrincipalId, scope.TenantId, scope.LegalEntityId,
                    new HashSet<string> { PrincipalRfqPublicationCoreWorkflow.Permission }),
                input.Action, key.ToString("D"), reason.Length == 0 ? null : reason, decisions, now);
            if (!result.Recorded) throw new Problem("SourcingCoreDecisionNotRecorded", 409);
            var coreDecision = decisions[^1];
            cases.Decisions.Add(coreDecision);
            var final = approval.State is "Approved" or "Returned" or "Rejected";
            if (approval.State == "Approved")
            {
                foreach (var pin in draft.Lines)
                {
                    var source = await ApprovedDemandSourceLoader.LoadAsync(db, cases, scope,
                        pin.RequestId, pin.LineId, token);
                    var available = CompetitiveSourcingSource.Check(source, scope.TenantId, scope.LegalEntityId, now);
                    var hold = source.Allocations.SingleOrDefault(x => x.Id == pin.AllocationId
                        && x.TargetType == "SourcingEvent" && x.TargetId == ev.Id && x.Status == "Reserved");
                    if (hold is null || hold.Quantity != pin.Quantity || hold.EstimatedValue != pin.EstimatedValue
                        || source.Request.BranchRef != ev.BranchRef || source.Request.CurrencyCode != ev.CurrencyCode
                        || source.Line.CategoryCode != pin.CategoryCode || source.Line.EstimatedUnitPrice != pin.UnitEstimate
                        || available.MethodId != pin.MethodId || available.MethodRevision != pin.MethodRevision)
                        throw new Problem("SourcingSourceChanged", 409);
                    var finance = await ProcurementApprovalFinanceEligibility.CheckPrincipalAsync(source.Request,
                        source.Finance, http.RequestServices.GetRequiredService<ProcurementApprovalExecutionOptions>(),
                        core, db, scope, now, token);
                    if (finance.Mode != source.Submission.FinanceEvidenceMode
                        || finance.Reference != source.Submission.ManualEvidenceAuthorityRef)
                        throw new Problem("SourcingFinanceAuthorityChanged", 409);
                }
                using var snapshot = JsonDocument.Parse(opening.SnapshotJson);
                var ids = snapshot.RootElement.GetProperty("supplierIds").EnumerateArray()
                    .Select(x => x.GetGuid()).ToArray();
                if (ids.Length is < 1 or > 100 || ids.Distinct().Count() != ids.Length)
                    throw new Problem("SourcingShortlistChanged", 409);
                var suppliers = await db.Suppliers.Include(x => x.Qualifications)
                    .Where(x => ids.Contains(x.Id)).ToArrayAsync(token);
                if (suppliers.Length != ids.Length || suppliers.Any(x => !SupplierEligibility.CanReceiveNewOrders(x, now)))
                    throw new Problem("SourcingSupplierUnavailable", 409);
                foreach (var supplier in suppliers)
                    db.AddScoped(new SourcingInvitation { SourcingEventId = id, SupplierId = supplier.Id,
                        Status = "ReadyForManualDelivery", SentAt = null, ViewedAt = null, DeliveryRef = null });
                ev.Status = "Published";
                ev.OpensAt = ProcurementEvidenceEncoding.DatabaseTimestamp(now);
            }
            else if (approval.State is "Returned" or "Rejected")
            {
                ev.Status = "Draft"; ev.OpensAt = null;
            }
            await cases.SaveChangesAsync(token);
            var at = ProcurementEvidenceEncoding.DatabaseTimestamp(now)!.Value;
            var link = new SourcingPublicationDecision
            {
                SourcingEventId = id, OpeningId = opening.Id, CoreApprovalCaseId = caseId,
                CoreApprovalDecisionId = coreDecision.Id, CoreAssignmentId = assignment.Id,
                Action = input.Action, CaseState = approval.State,
                ActorPrincipalId = actor.PrincipalId, ActorCoreLinkedWorkerId = actor.LinkedWorkerId,
                EventVersionAtDecision = checked(ev.Version + (final ? 1 : 0)), DecidedAt = at,
            };
            db.AddScoped(link);
            var body = JsonSerializer.Serialize(new { eventId = id, status = ev.Status,
                version = link.EventVersionAtDecision, publicationReviewId = opening.Id,
                coreCaseId = caseId, coreCaseVersion = approval.Version,
                coreDecisionId = coreDecision.Id, caseState = approval.State,
                supplierDeliveryState = ev.Status == "Published" ? "ReadyForManualDelivery" : "NotStarted",
                financeReservationChanged = false, commandKey = key }, Json);
            db.AddScoped(new SourcingPublicationReceipt
            {
                SourcingEventId = id, OpeningId = opening.Id, DecisionId = link.Id,
                Operation = "Decide", CommandKey = key, ActorPrincipalId = actor.PrincipalId,
                ActorCoreLinkedWorkerId = actor.LinkedWorkerId,
                CommandSha256 = fingerprint, ResponseJson = body,
                ResponseSha256 = ProcurementEvidenceEncoding.Digest(body), ResponseStatusCode = 200,
                ResultVersion = link.EventVersionAtDecision, CompletedAt = at,
            });
            db.AddScoped(new AuditEvent { RecordType = "SourcingEvent", RecordId = id,
                Action = "PublicationReviewDecisionRecorded", ActorSubjectId = actor.PrincipalId,
                ChangedFieldsJson = JsonSerializer.Serialize(new { openingId = opening.Id,
                    coreCaseId = caseId, coreDecisionId = coreDecision.Id, input.Action,
                    caseState = approval.State, supplierDeliveryConfirmed = false }, Json),
                CorrelationId = scope.CorrelationId });
            db.AddScoped(new OutboxEvent { AggregateType = "SourcingEvent", AggregateId = id,
                EventType = approval.State == "Approved" ? "sourcing.internally_published.v2"
                    : "sourcing.publication_review_decision.v2",
                IdempotencyKey = $"rfq.publication.decision.{key:N}.v2",
                PayloadJson = JsonSerializer.Serialize(new { eventId = id, openingId = opening.Id,
                    coreCaseId = caseId, coreDecisionId = coreDecision.Id,
                    caseState = approval.State, internallyPublished = approval.State == "Approved",
                    supplierDeliveryConfirmed = false, financeReservationChanged = false }, Json) });
            await db.SaveChangesAsync(token);
            return new Reply(body);
        }, ct);
    }
}
