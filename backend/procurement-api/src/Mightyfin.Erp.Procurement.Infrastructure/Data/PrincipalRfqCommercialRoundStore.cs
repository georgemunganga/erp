using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>Unregistered, owner-enlisted P32 append primitive. Its caller must
/// authenticate current Core people and actions, reload retained source/policy/
/// case/document graphs, lock source rows and obtain document use in the SAME
/// serializable transaction. This class never opens its own transaction.</summary>
public sealed class PrincipalRfqCommercialRoundStore(ProcurementDbContext db, IProcurementScope scope)
{
    private void Enlisted(Guid branch)
    {
        if (!db.Database.IsNpgsql() || db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("RfqCommercialOwnerTransactionRequired");
        if (scope.OrgUnitId is not null || branch == Guid.Empty
            || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches
            || scope.WorkLocationId is { } selected && selected != branch)
            throw new UnauthorizedAccessException("RfqCommercialOwnerScopeDenied");
    }

    public PrincipalRfqCommercialCommandReceipt StageOpening(
        PrincipalRfqCommercialRoundOpening preparedOpening, PrincipalRfqPreparedPlan prepared,
        SourcingEvent source, PolicyVersion policy, Guid publishedIdentityId,
        RfqPrincipalActor actor, Guid commandKey, string requestSha256)
    {
        Enlisted(prepared.BranchId);
        RequireActor(actor, prepared.BranchId);
        if (commandKey == Guid.Empty || !Hash(requestSha256) || publishedIdentityId == Guid.Empty
            || preparedOpening.Case.Id == Guid.Empty || source.Id != prepared.SourcingEventId
            || source.Version <= 0 || source.Status != "Published"
            || policy.Id != preparedOpening.Case.PolicyVersionId
            || policy.PolicyKey != PrincipalRfqCommercialRoundPolicyRules.PolicyKey
            || preparedOpening.Case.SourceId != prepared.Id
            || preparedOpening.Case.MakerSubjectId != actor.PrincipalId
            || !actor.Permissions.Contains("procurement-sourcing-round-open")
            || preparedOpening.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(preparedOpening.SnapshotJson))
            throw new InvalidOperationException("RfqCommercialOpeningProofChanged");
        var row = new PrincipalRfqCommercialRoundOpeningRecord
        {
            Id = preparedOpening.Case.Id, SourcingEventId = source.Id,
            PreparedPlanId = prepared.Id, CommercialPolicyVersionId = policy.Id,
            CommercialPolicyKey = PrincipalRfqCommercialRoundPolicyRules.PolicyKey,
            PublishedPolicyIdentityId = publishedIdentityId,
            CoreCaseId = preparedOpening.Case.Id, BranchId = prepared.BranchId,
            EventVersion = source.Version, PreparedPlanSha256 = prepared.SnapshotSha256,
            PolicyRulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),
            ActorPrincipalId = actor.PrincipalId, ActorWorkerAliasId = actor.LinkedWorkerId,
            OpensAt = preparedOpening.OpensAt, ClosesAt = preparedOpening.ClosesAt,
            SnapshotJson = preparedOpening.SnapshotJson, SnapshotSha256 = preparedOpening.SnapshotSha256,
        };
        db.AddScoped(row);
        return Receipt("Open", row.Id, row.Id, row.SourcingEventId, row.BranchId,
            actor.PrincipalId, commandKey, requestSha256, 201,
            new { roundId = row.Id, eventId = source.Id, state = "PendingCommercialReview",
                coreCaseId = row.CoreCaseId, snapshotSha256 = row.SnapshotSha256 });
    }

    public PrincipalRfqCommercialCommandReceipt StageTerminal(
        PrincipalRfqCommercialRoundOpeningRecord opening,
        PrincipalRfqCommercialRoundTerminal terminal, Guid coreAssignmentId,
        RfqPrincipalActor actor, Guid commandKey, string requestSha256, DateTimeOffset decidedAt)
    {
        Enlisted(opening.BranchId);
        RequireActor(actor, opening.BranchId);
        if (commandKey == Guid.Empty || !Hash(requestSha256) || !Hash(terminal.CoreOutcomeSha256)
            || terminal.ActorPrincipalId != actor.PrincipalId
            || !actor.Permissions.Contains(PrincipalRfqCommercialRoundPolicyRules.ApprovalAction)
            || coreAssignmentId == Guid.Empty || terminal.CoreDecisionId == Guid.Empty
            || terminal.State is not ("Approved" or "Returned" or "Rejected")
            || terminal.State == "Approved" && terminal.AcceptingRound is null
            || terminal.State != "Approved" && terminal.AcceptingRound is not null
            || terminal.AcceptingRound is { } round
                && (terminal.State != "Approved" || round.RoundId != opening.Id
                    || round.EventId != opening.SourcingEventId || round.BranchId != opening.BranchId
                    || round.ApprovalEvidenceSha256 != opening.SnapshotSha256))
            throw new InvalidOperationException("RfqCommercialDecisionProofChanged");
        var row = new PrincipalRfqCommercialRoundDecisionRecord
        {
            OpeningId = opening.Id, SourcingEventId = opening.SourcingEventId,
            BranchId = opening.BranchId, CoreCaseId = opening.CoreCaseId,
            CoreDecisionId = terminal.CoreDecisionId, CoreAssignmentId = coreAssignmentId,
            State = terminal.State, ActorPrincipalId = actor.PrincipalId,
            ActorWorkerAliasId = actor.LinkedWorkerId,
            OpeningSha256 = opening.SnapshotSha256,
            CoreOutcomeSha256 = terminal.CoreOutcomeSha256, DecidedAt = decidedAt,
        };
        db.AddScoped(row);
        return Receipt("Decide", row.Id, opening.Id, opening.SourcingEventId,
            opening.BranchId, actor.PrincipalId, commandKey, requestSha256, 200,
            new { roundId = opening.Id, decisionId = row.Id, state = row.State,
                coreDecisionId = row.CoreDecisionId, outcomeSha256 = row.CoreOutcomeSha256 });
    }

    public PrincipalRfqCommercialCommandReceipt StageOffer(
        PrincipalRfqCommercialRoundOpeningRecord opening,
        PrincipalRfqCommercialRoundDecisionRecord approved,
        PrincipalRfqManualOfferSubmission submission, RfqPrincipalActor actor,
        Guid commandKey, string requestSha256)
    {
        Enlisted(opening.BranchId);
        RequireActor(actor, opening.BranchId);
        if (commandKey == Guid.Empty || !Hash(requestSha256) || approved.State != "Approved"
            || !actor.Permissions.Contains(PrincipalRfqSealedManualOffers.SubmitAction)
            || approved.OpeningId != opening.Id || approved.SourcingEventId != opening.SourcingEventId
            || approved.BranchId != opening.BranchId || submission.RoundId != opening.Id
            || approved.OpeningSha256 != opening.SnapshotSha256
            || submission.EventId != opening.SourcingEventId
            || submission.RecorderPrincipalId != actor.PrincipalId
            || submission.RecorderWorkerAliasId != actor.LinkedWorkerId
            || !OpeningAudienceContains(opening.SnapshotJson, submission.SupplierId)
            || submission.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(submission.SnapshotJson))
            throw new InvalidOperationException("RfqCommercialOfferProofChanged");
        var row = new PrincipalRfqCommercialOfferSubmissionRecord
        {
            OpeningId = opening.Id, ApprovedDecisionId = approved.Id,
            SourcingEventId = opening.SourcingEventId, BranchId = opening.BranchId,
            SupplierBidId = submission.BidId, InvitationId = submission.InvitationId,
            SupplierId = submission.SupplierId, DraftRevision = submission.DraftRevision,
            BidVersion = submission.BidVersion,
            OriginalDocumentVersionId = submission.OriginalDocumentVersionId,
            OriginalDocumentSha256 = submission.OriginalDocumentSha256,
            RecorderPrincipalId = actor.PrincipalId,
            RecorderWorkerAliasId = actor.LinkedWorkerId,
            ReportedReceivedAt = submission.ReportedReceivedAt,
            SubmittedAt = submission.SubmittedAt, SnapshotJson = submission.SnapshotJson,
            SnapshotSha256 = submission.SnapshotSha256,
        };
        db.AddScoped(row);
        return Receipt("SubmitOffer", row.Id, opening.Id, opening.SourcingEventId,
            opening.BranchId, actor.PrincipalId, commandKey, requestSha256, 201,
            new { roundId = opening.Id, offerId = row.Id, supplierId = row.SupplierId,
                submittedAt = row.SubmittedAt, snapshotSha256 = row.SnapshotSha256 });
    }

    public async Task<PrincipalRfqCommercialCommandReceipt> StageCloseAsync(
        PrincipalRfqCommercialRoundOpeningRecord opening,
        PrincipalRfqCommercialRoundDecisionRecord approved,
        PrincipalRfqRoundSealProof seal, RfqPrincipalActor actor,
        Guid commandKey, string requestSha256, CancellationToken ct)
    {
        Enlisted(opening.BranchId);
        RequireActor(actor, opening.BranchId);
        if (commandKey == Guid.Empty || !Hash(requestSha256) || approved.State != "Approved"
            || !actor.Permissions.Contains(PrincipalRfqSealedManualOffers.CloseAction)
            || approved.OpeningId != opening.Id || approved.SourcingEventId != opening.SourcingEventId
            || approved.BranchId != opening.BranchId || seal.RoundId != opening.Id
            || approved.OpeningSha256 != opening.SnapshotSha256
            || seal.EventId != opening.SourcingEventId
            || seal.ActorPrincipalId != actor.PrincipalId
            || seal.ActorWorkerAliasId != actor.LinkedWorkerId
            || seal.ClosedAt < opening.ClosesAt || !Hash(seal.SubmissionSetSha256))
            throw new InvalidOperationException("RfqCommercialSealProofChanged");
        var retainedOffers = await db.PrincipalRfqCommercialOfferSubmissions.AsNoTracking()
            .Where(x => x.OpeningId == opening.Id).OrderBy(x => x.SupplierBidId).ToArrayAsync(ct);
        var exactSet = JsonSerializer.Serialize(retainedOffers.Select(x => new
        { BidId = x.SupplierBidId, x.SupplierId, x.SnapshotSha256 }));
        if (retainedOffers.Length != seal.SubmissionCount
            || retainedOffers.Any(x => x.ApprovedDecisionId != approved.Id
                || x.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(x.SnapshotJson))
            || seal.SubmissionSetSha256 != ProcurementEvidenceEncoding.Digest(exactSet))
            throw new InvalidOperationException("RfqCommercialSubmissionSetChanged");
        var row = new PrincipalRfqCommercialRoundSealRecord
        {
            OpeningId = opening.Id, ApprovedDecisionId = approved.Id,
            SourcingEventId = opening.SourcingEventId, BranchId = opening.BranchId,
            OfficerPrincipalId = actor.PrincipalId, OfficerWorkerAliasId = actor.LinkedWorkerId,
            ClosedAt = seal.ClosedAt, SubmissionCount = seal.SubmissionCount,
            SubmissionSetSha256 = seal.SubmissionSetSha256,
        };
        db.AddScoped(row);
        return Receipt("Close", row.Id, opening.Id, opening.SourcingEventId,
            opening.BranchId, actor.PrincipalId, commandKey, requestSha256, 200,
            new { roundId = opening.Id, sealId = row.Id, state = "Closed",
                offerCount = row.SubmissionCount, submissionSetSha256 = row.SubmissionSetSha256 });
    }

    public async Task<PrincipalRfqCommercialCommandReceipt?> RecoverAsync(
        Guid commandKey, string operation, Guid eventId, string? requestSha256,
        CancellationToken ct)
    {
        if (commandKey == Guid.Empty || operation is not ("Open" or "Decide" or "SubmitOffer" or "Close"))
            throw new ArgumentException("RfqCommercialCommandRequired");
        if (!db.Database.IsNpgsql() || db.Database.CurrentTransaction is null
            || scope.OrgUnitId is not null
            || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches)
            throw new UnauthorizedAccessException("RfqCommercialOwnerScopeDenied");
        var row = await db.PrincipalRfqCommercialCommandReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CommandKey == commandKey && x.ActorPrincipalId == scope.SubjectId
                && x.SourcingEventId == eventId
                && (scope.WorkLocationId == null || x.BranchId == scope.WorkLocationId), ct);
        if (row is null) return null;
        Enlisted(row.BranchId);
        if (row.Operation != operation || requestSha256 is not null && row.RequestSha256 != requestSha256)
            throw new InvalidOperationException("RfqCommercialCommandConflict");
        if (row.ResponseSha256 != ProcurementEvidenceEncoding.Digest(row.ResponseJson))
            throw new InvalidOperationException("RfqCommercialReceiptChanged");
        return row;
    }

    private void RequireActor(RfqPrincipalActor actor, Guid branch)
    {
        if (actor.TenantId != scope.TenantId || actor.CompanyId != scope.LegalEntityId
            || actor.PrincipalId != scope.SubjectId || actor.SelectedBranchId is { } selected && selected != branch
            || !actor.OrganisationWide && !actor.Branches.Contains(branch))
            throw new UnauthorizedAccessException("RfqCommercialCurrentActorRequired");
    }

    private PrincipalRfqCommercialCommandReceipt Receipt(string operation, Guid targetId,
        Guid openingId, Guid eventId, Guid branch, string actor, Guid commandKey,
        string requestSha256, int status, object response)
    {
        var json = JsonSerializer.Serialize(response);
        var row = new PrincipalRfqCommercialCommandReceipt
        {
            OpeningId = openingId, SourcingEventId = eventId, BranchId = branch,
            Operation = operation, TargetId = targetId, CommandKey = commandKey,
            OpeningTargetId = operation == "Open" ? targetId : null,
            DecisionTargetId = operation == "Decide" ? targetId : null,
            OfferTargetId = operation == "SubmitOffer" ? targetId : null,
            SealTargetId = operation == "Close" ? targetId : null,
            ActorPrincipalId = actor, RequestSha256 = requestSha256,
            ResponseJson = json, ResponseSha256 = ProcurementEvidenceEncoding.Digest(json),
            ResponseStatus = status, CompletedAt = DateTimeOffset.UtcNow,
        };
        db.AddScoped(row);
        db.AddScoped(new AuditEvent
        {
            RecordType = "RfqCommercialRound", RecordId = openingId, Action = operation,
            ActorSubjectId = actor, CorrelationId = scope.CorrelationId,
            ChangedFieldsJson = JsonSerializer.Serialize(new
            { eventId, branch, targetId, commandKey, row.ResponseSha256,
                supplierSendPerformed = false, awardMade = false }),
        });
        db.AddScoped(new OutboxEvent
        {
            AggregateType = "RfqCommercialRound", AggregateId = openingId,
            EventType = "sourcing.commercial_round_recorded.v1",
            IdempotencyKey = $"rfq.commercial.{commandKey:N}.v1",
            PayloadJson = JsonSerializer.Serialize(new
            { eventId, operation, targetId, supplierSendPerformed = false,
                awardMade = false, financeReservationChanged = false }),
        });
        return row;
    }

    private static bool Hash(string? value) => value is { Length: 64 }
        && value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool OpeningAudienceContains(string snapshotJson, Guid supplierId)
    {
        try
        {
            using var document = JsonDocument.Parse(snapshotJson);
            return document.RootElement.GetProperty("supplierIds").EnumerateArray()
                .Any(x => x.GetGuid() == supplierId);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException) { return false; }
    }
}
