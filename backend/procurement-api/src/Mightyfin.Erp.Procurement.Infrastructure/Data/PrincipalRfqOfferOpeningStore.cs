using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>Unregistered same-owner primitive. The application must first resolve
/// current Core authority/aliases and actual retained Core/source/document proofs.
/// This opens no connection/transaction and grants no module purpose.</summary>
public sealed class PrincipalRfqOfferOpeningStore(ProcurementDbContext db, IProcurementScope scope)
{
    private void EnlistedScope()
    {
        if (!db.Database.IsNpgsql() || db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("RfqOfferOpeningOwnerTransactionRequired");
        if (scope.OrgUnitId is not null || !ProcurementPrincipalEvidence.Canonical(scope.SubjectId)
            || scope.LegalEntityId == Guid.Empty || string.IsNullOrWhiteSpace(scope.TenantId)
            || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches)
            throw new UnauthorizedAccessException("RfqOfferOpeningOwnerScopeDenied");
    }

    private void Enlisted(Guid branch)
    {
        EnlistedScope();
        if (branch == Guid.Empty
            || scope.WorkLocationId is { } selected && selected != branch
            || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches)
            throw new UnauthorizedAccessException("RfqOfferOpeningOwnerScopeDenied");
    }

    public PrincipalRfqOfferOpeningReceipt Stage(PrincipalRfqOpenedOffersProof proof,
        PrincipalRfqCommercialRoundOpeningRecord round, PrincipalRfqCommercialRoundSealRecord seal,
        RfqPrincipalActor actor, Guid key, string requestSha256)
    {
        Enlisted(round.BranchId);
        if (actor.PrincipalId != scope.SubjectId || actor.TenantId != scope.TenantId
            || actor.CompanyId != scope.LegalEntityId || actor.PrincipalId != proof.OfficerPrincipalId
            || actor.LinkedWorkerId != proof.OfficerWorkerAliasId
            || !actor.Permissions.Contains(PrincipalRfqOfferOpeningControls.OpenAction)
            || actor.SelectedBranchId is { } selected && selected != round.BranchId
            || !actor.OrganisationWide && !actor.Branches.Contains(round.BranchId))
            throw new UnauthorizedAccessException("RfqOfferOpeningActorDenied");
        var row = new PrincipalRfqOfferOpeningRecord
        {
            OpeningId = proof.RoundId, SealId = proof.SealId, SourcingEventId = proof.EventId,
            BranchId = proof.BranchId, PolicyVersionId = proof.PolicyVersionId,
            PublishedPolicyIdentityId = round.PublishedPolicyIdentityId,
            PolicyRulesSha256 = proof.PolicyRulesSha256, ApprovedRoundSha256 = round.SnapshotSha256,
            OfficerPrincipalId = actor.PrincipalId, OfficerWorkerAliasId = actor.LinkedWorkerId,
            CommandKey = key, RequestSha256 = requestSha256, OpenedAt = proof.OpenedAt,
            OfferCount = proof.OfferCount, OfferSetSha256 = proof.OfferSetSha256,
            Reason = proof.Reason, Reference = proof.Reference,
            SnapshotJson = proof.SnapshotJson, SnapshotSha256 = proof.SnapshotSha256,
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
        };
        PrincipalRfqOfferOpeningStorage.Verify(row, round, seal);
        var response = PrincipalRfqOfferOpeningStorage.Response(row);
        var receipt = new PrincipalRfqOfferOpeningReceipt
        {
            UnsealingId = row.Id, OpeningId = row.OpeningId, SourcingEventId = row.SourcingEventId,
            BranchId = row.BranchId, ActorPrincipalId = actor.PrincipalId, CommandKey = key,
            RequestSha256 = requestSha256, ResponseJson = response,
            ResponseSha256 = ProcurementEvidenceEncoding.Digest(response),
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
        };
        db.AddScoped(row); db.AddScoped(receipt);
        db.AddScoped(new AuditEvent { RecordType = nameof(PrincipalRfqOfferOpeningRecord), RecordId = row.Id,
            Action = "OpenOffers", ActorSubjectId = actor.PrincipalId, CorrelationId = scope.CorrelationId,
            ChangedFieldsJson = PrincipalRfqOfferOpeningStorage.AuditPayload(row) });
        db.AddScoped(new OutboxEvent { AggregateType = "RfqCommercialRound", AggregateId = row.OpeningId,
            EventType = "sourcing.commercial_offers_opened.v1", IdempotencyKey = key.ToString("D"),
            PayloadJson = PrincipalRfqOfferOpeningStorage.EventPayload(row) });
        return receipt;
    }

    public async Task<PrincipalRfqOfferOpeningReceipt?> RecoverAsync(Guid roundId, Guid key,
        string? requestSha256, CancellationToken ct)
    {
        EnlistedScope();
        if (roundId == Guid.Empty || key == Guid.Empty)
            throw new ArgumentException("RfqOfferOpeningCommandRequired");
        var receipt = await db.PrincipalRfqOfferOpeningReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CommandKey == key && x.ActorPrincipalId == scope.SubjectId, ct);
        if (receipt is null) return null;
        Enlisted(receipt.BranchId);
        if (receipt.OpeningId != roundId || scope.WorkLocationId is { } selected && receipt.BranchId != selected
            || scope.OrgUnitId is not null || scope.IsConfined && scope.WorkLocationId is null && !scope.IsExplicitAllBranches
            || requestSha256 is not null && receipt.RequestSha256 != requestSha256)
            throw new InvalidOperationException("RfqOfferOpeningCommandConflict");
        var row = await db.PrincipalRfqOfferOpenings.AsNoTracking().SingleAsync(x => x.Id == receipt.UnsealingId, ct);
        var round = await db.PrincipalRfqCommercialRoundOpenings.AsNoTracking().SingleAsync(x => x.Id == roundId, ct);
        var seal = await db.PrincipalRfqCommercialRoundSeals.AsNoTracking().SingleAsync(x => x.Id == row.SealId, ct);
        PrincipalRfqOfferOpeningStorage.Verify(row, round, seal);
        PrincipalRfqOfferOpeningStorage.VerifyReceipt(row, receipt);
        return receipt;
    }
}
