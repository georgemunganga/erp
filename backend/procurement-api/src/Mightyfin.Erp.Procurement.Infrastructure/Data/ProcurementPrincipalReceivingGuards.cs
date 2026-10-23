using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private void GuardPrincipalReceiving()
    {
        foreach(var entry in ChangeTracker.Entries<ProcurementRecord>().Where(x=>x.Entity is PrincipalReceivingSourceState
            or PrincipalReceivingFact or PrincipalReceivingDraftHead or PrincipalReceivingDraftRevision or PrincipalReceivingCommandReceipt))
        {
            if(entry.State is EntityState.Unchanged or EntityState.Detached)continue;
            if(entry.Entity.TenantId!=scope.TenantId || entry.Entity.LegalEntityId!=scope.LegalEntityId)
                throw new InvalidOperationException("PrincipalReceivingScopeMismatch");
            if(entry.State==EntityState.Deleted || entry.State==EntityState.Modified && entry.Entity is
                PrincipalReceivingFact or PrincipalReceivingDraftRevision or PrincipalReceivingCommandReceipt)
                throw new InvalidOperationException("PrincipalReceivingEvidenceAppendOnly");
            if(entry.State==EntityState.Modified)
            {
                var mutable=entry.Entity is PrincipalReceivingSourceState
                    ?new[]{nameof(PrincipalReceivingSourceState.Sequence),nameof(PrincipalReceivingSourceState.AcceptedQuantity),nameof(PrincipalReceivingSourceState.AcceptedNetAmount)}
                    :new[]{nameof(PrincipalReceivingDraftHead.Revision),nameof(PrincipalReceivingDraftHead.State),nameof(PrincipalReceivingDraftHead.SnapshotSha256)};
                if(entry.Properties.Any(p=>p.IsModified && !mutable.Contains(p.Metadata.Name)
                    &&p.Metadata.Name is not(nameof(ProcurementRecord.Version) or nameof(ProcurementRecord.UpdatedAt) or nameof(ProcurementRecord.UpdatedBy))))
                    throw new InvalidOperationException("PrincipalReceivingOriginalPinsImmutable");
            }
        }
        var addedReceipts=ChangeTracker.Entries<PrincipalReceivingCommandReceipt>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity).ToArray();
        var addedFacts=ChangeTracker.Entries<PrincipalReceivingFact>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity).ToArray();
        foreach(var row in addedReceipts)
        {
            var head=PrincipalReceivingDraftHeads.Local.SingleOrDefault(x=>x.Id==row.DraftId);
            if(head is null || row.SchemaVersion!=2 || row.ActorPrincipalId!=scope.SubjectId || row.BranchId!=head.BranchId
                || row.CommandKey==Guid.Empty || row.RequestSha256 is not {Length:64}
                || row.ResponseSha256!=ProcurementEvidenceEncoding.Digest(row.ResponseJson)
                || row.ResponseStatus is not(200 or 201) || row.ResultRevision!=head.Revision
                || row.Operation is not("Create" or "Revise" or "Cancel" or "Record" or "Inspect" or "Accept" or "Certify"))
                throw new InvalidOperationException("PrincipalReceivingReceiptInvalid");
            if(row.Operation is "Create" or "Revise" or "Cancel" or "Record" && head.CreatorPrincipalId!=scope.SubjectId)
                throw new InvalidOperationException("PrincipalReceivingDraftOwnerRequired");
            if(row.Operation=="Create" && (Entry(head).State!=EntityState.Added || head.Revision!=1 || head.State!="Draft")
                || row.Operation=="Revise" && head.State!="Draft"
                || row.Operation=="Cancel" && head.State!="Cancelled"
                || row.Operation=="Record" && head.State!="Recorded"
                || row.Operation=="Certify" && head.State!="Certified")
                throw new InvalidOperationException("PrincipalReceivingReceiptStateInvalid");
            if(row.Operation is "Record" or "Inspect" or "Accept" or "Certify" && !addedFacts.Any(x=>x.DraftId==head.Id&&x.CommandKey==row.CommandKey))
                throw new InvalidOperationException("PrincipalReceivingReceiptFactRequired");
        }
        foreach(var headEntry in ChangeTracker.Entries<PrincipalReceivingDraftHead>().Where(x=>x.State is EntityState.Added or EntityState.Modified))
        {
            var head=headEntry.Entity;
            var revisions=ChangeTracker.Entries<PrincipalReceivingDraftRevision>().Where(x=>x.State==EntityState.Added&&x.Entity.DraftId==head.Id).Select(x=>x.Entity).ToArray();
            if(head.SchemaVersion!=2 || revisions.Length!=1 || revisions[0].Revision!=head.Revision
                || revisions[0].SnapshotSha256!=head.SnapshotSha256 || revisions[0].ActorPrincipalId!=scope.SubjectId
                || revisions[0].SchemaVersion!=2 || revisions[0].SnapshotSha256!=ProcurementEvidenceEncoding.Digest(revisions[0].SnapshotJson)
                || !addedReceipts.Any(x=>x.DraftId==head.Id&&x.CommandKey==revisions[0].CommandKey))
                throw new InvalidOperationException("PrincipalReceivingExactRevisionReceiptRequired");
            var sources=PrincipalReceivingDraftSnapshot.Validate(head,revisions[0]);
            foreach(var source in sources)
                if(!PrincipalReceivingSourceStates.Local.Any(x=>PrincipalReceivingStorageCodec.Source(x)==source))
                    throw new InvalidOperationException("PrincipalReceivingDraftSourceRequired");
            if(headEntry.State==EntityState.Modified)
            {
                var prior=headEntry.OriginalValues.GetValue<long>(nameof(PrincipalReceivingDraftHead.Revision));
                var before=headEntry.OriginalValues.GetValue<string>(nameof(PrincipalReceivingDraftHead.State));
                if(head.Revision!=checked(prior+1) || before!="Draft"
                    || head.State is not("Draft" or "Recorded" or "Certified" or "Cancelled"))
                    throw new InvalidOperationException("PrincipalReceivingDraftTransitionInvalid");
                var old=PrincipalReceivingDraftRevisions.Local.SingleOrDefault(x=>x.DraftId==head.Id&&x.Revision==prior)
                    ??throw new InvalidOperationException("PrincipalReceivingPriorRevisionRequired");
                // No source replacement, even if the caller forges matching new header IDs.
                PrincipalReceivingDraftSnapshot.Transition(head.Kind,old,revisions[0]);
            }
        }
        foreach(var row in ChangeTracker.Entries<PrincipalReceivingDraftRevision>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity))
            if(!ChangeTracker.Entries<PrincipalReceivingDraftHead>().Any(x=>x.Entity.Id==row.DraftId&&x.State is EntityState.Added or EntityState.Modified))
                throw new InvalidOperationException("PrincipalReceivingRevisionHeadRequired");
        foreach(var row in addedFacts)
        {
            var source=PrincipalReceivingSourceStates.Local.SingleOrDefault(x=>x.Id==row.SourceStateId);
            if(source is null || Entry(source).State is not(EntityState.Added or EntityState.Modified)
                || row.ActorPrincipalId!=scope.SubjectId || !addedReceipts.Any(x=>x.DraftId==row.DraftId&&x.CommandKey==row.CommandKey
                    && x.Operation==(row.Kind=="Observation"?"Record":row.Kind=="Inspection"?"Inspect":PrincipalReceivingStorageCodec.Source(source).Commercial.Kind==ReceivingKind.Service?"Certify":"Accept")))
                throw new InvalidOperationException("PrincipalReceivingFactCommandRequired");
        }
        foreach(var entry in ChangeTracker.Entries<PrincipalReceivingSourceState>().Where(x=>x.State is EntityState.Added or EntityState.Modified))
        {
            var state=entry.Entity;var source=PrincipalReceivingStorageCodec.Source(state);
            var facts=PrincipalReceivingFacts.Local.Where(x=>x.SourceStateId==state.Id).ToArray();
            _=PrincipalReceivingStorageCodec.Ledger(state,facts,PolicyVersions.Local.ToArray());
            var previous=entry.State==EntityState.Added?0:entry.OriginalValues.GetValue<long>(nameof(PrincipalReceivingSourceState.Sequence));
            var added=addedFacts.Where(x=>x.SourceStateId==state.Id).ToArray();
            if(state.Sequence!=checked(previous+added.Length) || entry.State==EntityState.Modified&&added.Length==0)
                throw new InvalidOperationException("PrincipalReceivingSourceAdvanceRequiresFacts");
            if(entry.State==EntityState.Added && !ChangeTracker.Entries<PrincipalReceivingDraftHead>().Any(x=>x.State is EntityState.Added or EntityState.Modified
                &&x.Entity.PurchaseOrderId==state.PurchaseOrderId&&x.Entity.IssuedVersionId==state.IssuedVersionId))
                throw new InvalidOperationException("PrincipalReceivingNewSourceDraftRequired");
            // API verifies full retained Core/source evidence; normalized parents must
            // still be loaded and agree before any new graph is persisted.
            var order=PurchaseOrders.Local.SingleOrDefault(x=>x.Id==state.PurchaseOrderId);
            var line=PurchaseOrderLines.Local.SingleOrDefault(x=>x.Id==state.PurchaseOrderLineId);
            if(order is null || line is null || order.PrincipalDraftSchemaVersion!=2 || order.BranchRef!=state.BranchId
                || line.PurchaseOrderId!=order.Id || line.PurchaseRequestLineId!=state.PurchaseRequestLineId
                || source.Commercial.AuthorizedQuantity!=line.Quantity || source.Commercial.NetUnitPrice!=line.UnitPrice)
                throw new InvalidOperationException("PrincipalReceivingOwnedSourceRequired");
            var issued=PrincipalOrderIssuedVersions.Local.SingleOrDefault(x=>x.Id==state.IssuedVersionId);
            var submission=PrincipalOrderSubmissions.Local.SingleOrDefault(x=>x.Id==state.SubmissionId);
            var funding=PrincipalOrderFundingEvidence.Local.SingleOrDefault(x=>x.Id==state.FundingEvidenceId);
            var ack=PrincipalOrderAcknowledgments.Local.SingleOrDefault(x=>x.Id==state.AcknowledgmentId);
            var binding=PrincipalOrderSourceBindings.Local.SingleOrDefault(x=>x.PurchaseOrderId==state.PurchaseOrderId&&x.PurchaseOrderLineId==state.PurchaseOrderLineId);
            if(issued is null || submission is null || funding is null || ack is null || binding is null
                || issued.PurchaseOrderId!=order.Id || issued.SubmissionId!=submission.Id || issued.FundingEvidenceId!=funding.Id
                || submission.PurchaseOrderId!=order.Id || submission.FundingEvidenceId!=funding.Id || funding.PurchaseOrderId!=order.Id
                || ack.PurchaseOrderId!=order.Id || ack.IssuedVersionId!=issued.Id || ack.Outcome!="Accepted"
                || source.SupplierAcknowledgedAt!=ack.ObservedAt || source.IssuedAuthorizedAt!=issued.AuthorizedAt
                || source.CoreApprovalCaseId!=issued.CoreApprovalCaseId || submission.CoreApprovalCaseId!=issued.CoreApprovalCaseId
                || source.OriginalDirectSourceSha256!=issued.OriginalDirectSourceSha256
                || source.Commercial.IssuedSnapshotSha256!=issued.CommercialSnapshotSha256
                || source.Buyer.PrincipalId!=submission.BuyerPrincipalId || source.Buyer.WorkerAliasId!=submission.BuyerWorkerAliasId
                || source.Requester.PrincipalId!=binding.RequesterPrincipalId.ToString("D") || source.Requester.WorkerAliasId!=binding.RequesterWorkerAliasId
                || binding.PurchaseRequestLineId!=state.PurchaseRequestLineId)
                throw new InvalidOperationException("PrincipalReceivingOwnedSourceParentsRequired");
            var delta=ExactDecimalArithmetic.Sum(added.Where(x=>x.Kind=="Acceptance").Select(x=>x.Quantity));
            if(delta>0)
            {
                var request=PurchaseRequestLines.Local.SingleOrDefault(x=>x.Id==state.PurchaseRequestLineId)
                    ??throw new InvalidOperationException("PrincipalReceivingRequestLineRequired");
                var lineEntry=Entry(line);var requestEntry=Entry(request);
                var expectedPo=ExactDecimalArithmetic.Add(lineEntry.OriginalValues.GetValue<decimal>(nameof(PurchaseOrderLine.AcceptedQuantity)),
                    ExactDecimalArithmetic.Sum(PrincipalReceivingAcceptanceDeltas(line.Id,false)));
                var expectedPr=ExactDecimalArithmetic.Add(requestEntry.OriginalValues.GetValue<decimal>(nameof(PurchaseRequestLine.AcceptedQuantity)),
                    ExactDecimalArithmetic.Sum(PrincipalReceivingAcceptanceDeltas(request.Id,true)));
                if(lineEntry.State!=EntityState.Modified || requestEntry.State!=EntityState.Modified
                    || line.AcceptedQuantity!=expectedPo || request.AcceptedQuantity!=expectedPr || expectedPr>request.ApprovedQuantity)
                    throw new InvalidOperationException("PrincipalReceivingAcceptanceCountersRequired");
            }
        }
    }
    private IEnumerable<decimal> PrincipalReceivingAcceptanceDeltas(Guid lineId,bool request)
    {
        foreach(var entry in ChangeTracker.Entries<PrincipalReceivingFact>().Where(x=>x.State==EntityState.Added&&x.Entity.Kind=="Acceptance"))
        {
            var source=PrincipalReceivingSourceStates.Local.SingleOrDefault(x=>x.Id==entry.Entity.SourceStateId)
                ??throw new InvalidOperationException("PrincipalReceivingSourceRequired");
            if((request?source.PurchaseRequestLineId:source.PurchaseOrderLineId)==lineId)yield return entry.Entity.Quantity;
        }
    }

}
