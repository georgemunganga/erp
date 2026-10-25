using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private void GuardRfqRecommendationDraft()
    {
        var heads=ChangeTracker.Entries<PrincipalRfqRecommendationDraftHead>().ToArray();
        var revisions=ChangeTracker.Entries<PrincipalRfqRecommendationDraftRevision>().ToArray();
        var lines=ChangeTracker.Entries<PrincipalRfqRecommendationDraftLine>().ToArray();
        var receipts=ChangeTracker.Entries<PrincipalRfqRecommendationDraftReceipt>().ToArray();

        if(heads.Any(x=>x.State is EntityState.Deleted)
            || revisions.Any(x=>x.State is not(EntityState.Added or EntityState.Unchanged))
            || lines.Any(x=>x.State is not(EntityState.Added or EntityState.Unchanged))
            || receipts.Any(x=>x.State is not(EntityState.Added or EntityState.Unchanged)))
            throw new InvalidOperationException("RfqRecommendationHistoryImmutable");
        var added=revisions.Where(x=>x.State==EntityState.Added).Select(x=>x.Entity).ToArray();
        var addedLines=lines.Where(x=>x.State==EntityState.Added).Select(x=>x.Entity).ToArray();
        var acks=receipts.Where(x=>x.State==EntityState.Added).Select(x=>x.Entity).ToArray();
        var audits=ChangeTracker.Entries<AuditEvent>().Where(x=>x.State==EntityState.Added)
            .Select(x=>x.Entity).Where(x=>x.RecordType==nameof(PrincipalRfqRecommendationDraftRevision)).ToArray();
        var events=ChangeTracker.Entries<OutboxEvent>().Where(x=>x.State==EntityState.Added)
            .Select(x=>x.Entity).Where(x=>x.EventType==PrincipalRfqRecommendationDraft.EventType).ToArray();
        if(added.Length!=acks.Length || added.Length!=audits.Length || added.Length!=events.Length
            || addedLines.Any(x=>added.Count(r=>r.Id==x.RevisionId)!=1))
            throw new InvalidOperationException("RfqRecommendationSameSaveEffectsRequired");
        foreach(var entry in heads.Where(x=>x.State==EntityState.Modified))
        {
            var modified=entry.Properties.Where(x=>x.IsModified).Select(x=>x.Metadata.Name).ToHashSet();
            if(!modified.IsSubsetOf([nameof(PrincipalRfqRecommendationDraftHead.CurrentRevision),
                    nameof(PrincipalRfqRecommendationDraftHead.CurrentRevisionId),
                    nameof(PrincipalRfqRecommendationDraftHead.CurrentSnapshotSha256),
                    nameof(ProcurementRecord.UpdatedAt),nameof(ProcurementRecord.UpdatedBy),
                    nameof(ProcurementRecord.Version)]))
                throw new InvalidOperationException("RfqRecommendationHeaderImmutable");
        }
        foreach(var row in added)
        {
            var head=heads.SingleOrDefault(x=>x.Entity.Id==row.DraftId)?.Entity
                ??throw new InvalidOperationException("RfqRecommendationHeadMissing");
            var headEntry=heads.Single(x=>x.Entity.Id==head.Id);
            if(row.ActorPrincipalId!=scope.SubjectId || row.ActorWorkerAliasId!=scope.CoreLinkedWorkerId
                || head.MakerPrincipalId!=scope.SubjectId
                || head.TenantId!=scope.TenantId || head.LegalEntityId!=scope.LegalEntityId
                || row.TenantId!=scope.TenantId || row.LegalEntityId!=scope.LegalEntityId
                || row.RoundId!=head.RoundId || row.UnsealingId!=head.UnsealingId
                || row.SourcingEventId!=head.SourcingEventId || row.BranchId!=head.BranchId
                || head.CurrentRevision!=row.Revision || head.CurrentRevisionId!=row.Id
                || head.CurrentSnapshotSha256!=row.SnapshotSha256
                || acks.Count(x=>x.RevisionId==row.Id)!=1)
                throw new InvalidOperationException("RfqRecommendationRevisionBindingChanged");
            if(headEntry.State==EntityState.Added)
            {
                if(head.MakerWorkerAliasId!=scope.CoreLinkedWorkerId
                    ||row.Revision!=1 || row.PreviousRevisionId is not null || row.PreviousSnapshotSha256 is not null)
                    throw new InvalidOperationException("RfqRecommendationFirstRevisionInvalid");
            }
            else
            {
                var prior=headEntry.OriginalValues;
                if(row.Revision!=prior.GetValue<int>(nameof(PrincipalRfqRecommendationDraftHead.CurrentRevision))+1
                    || row.PreviousRevisionId!=prior.GetValue<Guid>(nameof(PrincipalRfqRecommendationDraftHead.CurrentRevisionId))
                    || row.PreviousSnapshotSha256!=prior.GetValue<string>(nameof(PrincipalRfqRecommendationDraftHead.CurrentSnapshotSha256)))
                    throw new InvalidOperationException("RfqRecommendationRevisionCasInvalid");
            }
            var proposed=addedLines.Where(x=>x.RevisionId==row.Id)
                .Select(x=>new PrincipalRfqRecommendationDraft.Proposed(x.OfferId,x.OfferSha256,
                    x.SupplierId,x.SourcingLineId,x.QuantityExact,x.ProposedValueExact,
                    x.CurrencyCode,x.Rationale))
                .OrderBy(x=>x.SourcingLineId).ThenBy(x=>x.OfferId).ToArray();
            if(row.RecordedAt==default || row.RecordedAt.Offset!=TimeSpan.Zero
                || row.RecordedAt.UtcTicks%10!=0
                || row.SnapshotJson!=PrincipalRfqRecommendationDraft.Snapshot(head,row,proposed)
                || row.SnapshotSha256!=ProcurementEvidenceEncoding.Digest(row.SnapshotJson))
                throw new InvalidOperationException("RfqRecommendationSnapshotChanged");
            var ack=acks.Single(x=>x.RevisionId==row.Id);
            if(ack.DraftId!=head.Id || ack.RoundId!=head.RoundId || ack.UnsealingId!=head.UnsealingId
                || ack.SourcingEventId!=head.SourcingEventId || ack.BranchId!=head.BranchId
                || ack.ActorPrincipalId!=row.ActorPrincipalId || ack.CommandKey!=row.CommandKey
                || ack.RequestSha256!=row.RequestSha256
                || ack.ResponseJson!=PrincipalRfqRecommendationDraft.Response(head,row)
                || ack.ResponseSha256!=ProcurementEvidenceEncoding.Digest(ack.ResponseJson)
                || audits.Count(x=>x.RecordId==row.Id && x.ActorSubjectId==scope.SubjectId
                    && x.Action=="RecordRecommendationDraft"
                    && x.ChangedFieldsJson==PrincipalRfqRecommendationDraft.AuditPayload(row))!=1
                || events.Count(x=>x.AggregateId==row.Id
                    && x.AggregateType==nameof(PrincipalRfqRecommendationDraftRevision)
                    && x.IdempotencyKey==row.CommandKey.ToString("D")
                    && x.PayloadJson==PrincipalRfqRecommendationDraft.EventPayload(row)
                    && x.State=="Pending" && x.Attempts==0 && x.PublishedAt is null)!=1)
                throw new InvalidOperationException("RfqRecommendationReceiptOrEffectsChanged");
        }
        if(heads.Any(x=>(x.State is EntityState.Added or EntityState.Modified)
                && added.Count(r=>r.DraftId==x.Entity.Id)!=1))
            throw new InvalidOperationException("RfqRecommendationHeaderWithoutRevision");
    }
}
