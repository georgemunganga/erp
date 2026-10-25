using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private void GuardPrincipalGoodsReturns()
    {
        var heads = ChangeTracker.Entries<PrincipalGoodsReturnDraft>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified).ToArray();
        var revisions = ChangeTracker.Entries<PrincipalGoodsReturnRevision>()
            .Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        var receipts = ChangeTracker.Entries<PrincipalGoodsReturnCommandReceipt>()
            .Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        foreach (var entry in ChangeTracker.Entries<ProcurementRecord>().Where(x => x.Entity is
            PrincipalGoodsReturnDraft or PrincipalGoodsReturnRevision or PrincipalGoodsReturnCommandReceipt))
        {
            if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified && entry.Entity is not PrincipalGoodsReturnDraft)
                throw new InvalidOperationException("ReturnDraftEvidenceAppendOnly");
            if (entry.State is EntityState.Added or EntityState.Modified &&
                (entry.Entity.TenantId != scope.TenantId || entry.Entity.LegalEntityId != scope.LegalEntityId))
                throw new InvalidOperationException("ReturnDraftScopeChanged");
            if (entry.State == EntityState.Modified && entry.Entity is PrincipalGoodsReturnDraft &&
                entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not
                    (nameof(PrincipalGoodsReturnDraft.Revision) or nameof(PrincipalGoodsReturnDraft.State) or
                     nameof(PrincipalGoodsReturnDraft.CurrentRevisionId) or nameof(ProcurementRecord.Version) or
                     nameof(ProcurementRecord.UpdatedAt) or nameof(ProcurementRecord.UpdatedBy))))
                throw new InvalidOperationException("ReturnDraftOriginalPinsImmutable");
        }
        foreach (var entry in heads)
        {
            var head = entry.Entity;
            var revision = revisions.SingleOrDefault(x => x.DraftId == head.Id);
            var receipt = receipts.SingleOrDefault(x => x.DraftId == head.Id);
            if (head.SchemaVersion != 1 || head.CreatorPrincipalId != scope.SubjectId || head.AcceptedFactQuantity <= 0 ||
                head.SourceSha256 is not { Length: 64 } || head.AcceptanceSha256 is not { Length: 64 } ||
                revision is null || receipt is null || revision.Id != head.CurrentRevisionId ||
                revision.Revision != head.Revision || revision.State != head.State ||
                revision.ActorPrincipalId != scope.SubjectId || receipt.ActorPrincipalId != scope.SubjectId ||
                revision.CommandKey != receipt.CommandKey || receipt.ResultRevision != head.Revision ||
                receipt.BranchId != head.BranchId || receipt.RequestSha256 !=
                    ProcurementEvidenceEncoding.PolicyJsonDigest(receipt.RequestJson) ||
                !PrincipalGoodsReturnRules.MatchesReceipt(head, revision, receipt) ||
                receipt.ResponseJson != PrincipalGoodsReturnRules.ResponseJson(head, revision) ||
                receipt.ResponseSha256 != ProcurementEvidenceEncoding.Digest(receipt.ResponseJson) ||
                revision.Quantity <= 0 || revision.Quantity > head.AcceptedFactQuantity ||
                string.IsNullOrWhiteSpace(revision.Reason) || revision.Reason.Length > 2000 ||
                revision.ProposalSha256 != PrincipalGoodsReturnRules.Fingerprint(head.Id, revision.Revision,
                    revision.Quantity, revision.Reason, revision.EvidenceReference, revision.State))
                throw new InvalidOperationException("ReturnDraftExactRevisionReceiptRequired");
            if (ChangeTracker.Entries<OutboxEvent>().Count(x => x.State == EntityState.Added &&
                x.Entity.AggregateType == "PrincipalGoodsReturnDraft" && x.Entity.AggregateId == head.Id &&
                x.Entity.State == "Pending" && x.Entity.Attempts == 0 && x.Entity.PublishedAt == null &&
                x.Entity.EventType == $"procurement.goods_return.draft_{receipt.Operation.ToLowerInvariant()}.v1" &&
                x.Entity.IdempotencyKey == $"goods-return.draft.{receipt.CommandKey:N}.v1" &&
                x.Entity.PayloadJson == PrincipalGoodsReturnRules.OutboxPayloadJson(head.Id, receipt.Operation,
                    receipt.CommandKey, head.Revision, scope.SubjectId)) != 1)
                throw new InvalidOperationException("ReturnDraftInternalEventRequired");
            var source = PrincipalReceivingSourceStates.Local.SingleOrDefault(x => x.Id == head.SourceStateId);
            var fact = PrincipalReceivingFacts.Local.SingleOrDefault(x => x.Id == head.AcceptanceFactId);
            if (source is null || fact is null || source.Kind != "Goods" || fact.Kind != "Acceptance" || fact.Operation != "Accept" ||
                source.BranchId != head.BranchId || source.SourceSha256 != head.SourceSha256 ||
                source.PurchaseOrderId != head.PurchaseOrderId || source.PurchaseOrderLineId != head.PurchaseOrderLineId ||
                source.IssuedVersionId != head.IssuedVersionId || fact.SourceStateId != source.Id ||
                fact.DraftId != head.ReceiptId || fact.PayloadSha256 != head.AcceptanceSha256 ||
                fact.Quantity != head.AcceptedFactQuantity)
                throw new InvalidOperationException("ReturnDraftAcceptedGoodsSourceRequired");
            if (entry.State == EntityState.Added)
            {
                if (head.Revision != 1 || head.State != "Draft" || receipt.Operation != "Create" || receipt.ResponseStatus != 201)
                    throw new InvalidOperationException("ReturnDraftInitialStateRequired");
            }
            else if (entry.OriginalValues.GetValue<long>(nameof(PrincipalGoodsReturnDraft.Revision)) + 1 != head.Revision ||
                entry.OriginalValues.GetValue<string>(nameof(PrincipalGoodsReturnDraft.State)) != "Draft" ||
                receipt.Operation != (head.State == "Cancelled" ? "Cancel" : "Revise") || receipt.ResponseStatus != 200)
                throw new InvalidOperationException("ReturnDraftTransitionInvalid");
            if (entry.State == EntityState.Modified && head.State == "Cancelled")
            {
                var prior = PrincipalGoodsReturnRevisions.Local.SingleOrDefault(x => x.DraftId == head.Id &&
                    x.Revision == head.Revision - 1);
                if (prior is null || !PrincipalGoodsReturnRules.CancellationPreservesPrior(prior, revision))
                    throw new InvalidOperationException("ReturnDraftCancellationChangedProposal");
            }
        }
        if (revisions.Any(x => !heads.Any(h => h.Entity.Id == x.DraftId)) ||
            receipts.Any(x => !heads.Any(h => h.Entity.Id == x.DraftId)))
            throw new InvalidOperationException("ReturnDraftOrphanEvidence");
        foreach (var eventRow in ChangeTracker.Entries<OutboxEvent>().Where(x => x.State == EntityState.Added &&
            x.Entity.AggregateType == "PrincipalGoodsReturnDraft").Select(x => x.Entity))
        {
            var receipt = receipts.SingleOrDefault(r => r.DraftId == eventRow.AggregateId &&
                eventRow.IdempotencyKey == $"goods-return.draft.{r.CommandKey:N}.v1");
            if (receipt is null || eventRow.State != "Pending" || eventRow.Attempts != 0 ||
                eventRow.PublishedAt is not null || eventRow.EventType !=
                $"procurement.goods_return.draft_{receipt.Operation.ToLowerInvariant()}.v1" ||
                eventRow.PayloadJson != PrincipalGoodsReturnRules.OutboxPayloadJson(receipt.DraftId,
                    receipt.Operation, receipt.CommandKey, receipt.ResultRevision, receipt.ActorPrincipalId))
                throw new InvalidOperationException("ReturnDraftOrphanEvent");
        }
    }
}
