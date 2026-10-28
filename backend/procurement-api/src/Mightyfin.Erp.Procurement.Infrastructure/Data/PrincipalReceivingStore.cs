using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>Owned enlisted persistence primitive, deliberately not DI-registered. Caller
/// resolves canonical Core authority and retained-issued2 proof before invoking this store.
/// Save/commit/audit/outbox orchestration belongs to the owning application transaction.</summary>
public sealed class PrincipalReceivingStore(ProcurementDbContext db)
{
    public async Task<PrincipalReceivingSourceState> SourceAsync(PrincipalReceivingSource source,CancellationToken ct)
    {
        Enlisted();PrincipalControlledReceiving.VerifySource(source);var pin=source.Commercial;
        // Load normalized immutable parent evidence on the actual owner context;
        // these are relational consistency checks, not substitutes for the API's
        // retained Core case and original-source verifier.
        var order=await db.PurchaseOrders.SingleAsync(x=>x.Id==pin.PurchaseOrderId,ct);
        var issued=await db.PrincipalOrderIssuedVersions.SingleAsync(x=>x.Id==pin.IssuedVersionId,ct);
        var submission=await db.PrincipalOrderSubmissions.SingleAsync(x=>x.Id==source.SubmissionId,ct);
        var funding=await db.PrincipalOrderFundingEvidence.SingleAsync(x=>x.Id==source.FundingEvidenceId,ct);
        var ack=await db.PrincipalOrderAcknowledgments.SingleAsync(x=>x.Id==source.SupplierAcknowledgmentId,ct);
        if(order.PrincipalDraftSchemaVersion!=2 || order.BranchRef!=pin.BranchId
            || issued.PurchaseOrderId!=order.Id || issued.SubmissionId!=submission.Id || issued.FundingEvidenceId!=funding.Id
            || submission.PurchaseOrderId!=order.Id || submission.FundingEvidenceId!=funding.Id || funding.PurchaseOrderId!=order.Id
            || ack.PurchaseOrderId!=order.Id || ack.IssuedVersionId!=issued.Id || ack.Outcome!="Accepted"
            || issued.CoreApprovalCaseId!=source.CoreApprovalCaseId || issued.CommercialSnapshotSha256!=pin.IssuedSnapshotSha256
            || issued.OriginalDirectSourceSha256!=source.OriginalDirectSourceSha256 || issued.AuthorizedAt!=source.IssuedAuthorizedAt
            || ack.ObservedAt!=source.SupplierAcknowledgedAt || submission.BuyerPrincipalId!=source.Buyer.PrincipalId
            || submission.BuyerWorkerAliasId!=source.Buyer.WorkerAliasId)
            throw new InvalidOperationException("PrincipalReceivingOwnedSourceRequired");
        await db.PrincipalOrderSourceBindings.Where(x=>x.Id==source.SourceBindingId&&x.PurchaseOrderId==pin.PurchaseOrderId&&x.PurchaseOrderLineId==pin.PurchaseOrderLineId).LoadAsync(ct);
        await db.PurchaseRequestLines.Where(x=>x.Id==pin.PurchaseRequestLineId).LoadAsync(ct);
        await db.PurchaseOrderLines.Where(x=>x.Id==pin.PurchaseOrderLineId&&x.PurchaseOrderId==pin.PurchaseOrderId).LoadAsync(ct);
        var existing=await db.PrincipalReceivingSourceStates.SingleOrDefaultAsync(x=>x.IssuedVersionId==pin.IssuedVersionId&&x.PurchaseOrderLineId==pin.PurchaseOrderLineId,ct);
        if(existing is not null)
        {
            if(PrincipalReceivingStorageCodec.Source(existing)!=source)throw new InvalidOperationException("PrincipalReceivingOriginalSourceChanged");
            return existing;
        }
        // Never adopt pre-existing receiving/accepted counters as a fabricated empty
        // ledger. Reconciliation must be explicit before a first successor use.
        var line=await db.PurchaseOrderLines.SingleAsync(x=>x.Id==pin.PurchaseOrderLineId&&x.PurchaseOrderId==pin.PurchaseOrderId,ct);
        if(line.AcceptedQuantity!=0 || await HasLegacyHistoryAsync(line.Id,ct))
            throw new InvalidOperationException("PrincipalReceivingEarlierHistoryUnknown");
        var json=PrincipalReceivingStorageCodec.Encode(source);
        var state=new PrincipalReceivingSourceState{TenantId=pin.TenantId,LegalEntityId=pin.LegalEntityId,
            BranchId=pin.BranchId!.Value,PurchaseOrderId=pin.PurchaseOrderId,PurchaseOrderLineId=pin.PurchaseOrderLineId,
            PurchaseRequestLineId=pin.PurchaseRequestLineId,IssuedVersionId=pin.IssuedVersionId,SubmissionId=source.SubmissionId,
            FundingEvidenceId=source.FundingEvidenceId,AcknowledgmentId=source.SupplierAcknowledgmentId,
            SourceBindingId=source.SourceBindingId,OriginalSourceSha256=source.OriginalDirectSourceSha256,IssuedSnapshotSha256=pin.IssuedSnapshotSha256,
            CategoryCode=pin.CategoryCode,PurchaseType=pin.PurchaseType,Kind=pin.Kind.ToString(),CurrencyCode=pin.CurrencyCode,
            SourceJson=json,SourceSha256=ProcurementEvidenceEncoding.Digest(json)};
        db.AddScoped(state);return state;
    }
    public async Task<PrincipalReceivingLedger> LedgerAsync(PrincipalReceivingSourceState state,CancellationToken ct)
    {
        Enlisted();
        if(await HasLegacyHistoryAsync(state.PurchaseOrderLineId,ct))throw new InvalidOperationException("PrincipalReceivingEarlierHistoryUnknown");
        await db.PrincipalReceivingFacts.Where(x=>x.SourceStateId==state.Id).LoadAsync(ct);
        var rows=db.PrincipalReceivingFacts.Local.Where(x=>x.SourceStateId==state.Id).ToArray();
        var ids=rows.Select(x=>x.PolicyVersionId).Distinct().ToArray();
        await db.PolicyVersions.Where(x=>ids.Contains(x.Id)).LoadAsync(ct);
        var keys=rows.Select(x=>x.CommandKey).Distinct().ToArray();
        var receipts=await db.PrincipalReceivingCommandReceipts.AsNoTracking().Where(x=>keys.Contains(x.CommandKey)).ToArrayAsync(ct);
        foreach(var fact in rows)
        {
            // Newly added facts are not historical: their same-save receipt is
            // mandatory at the final Save guard. Never waive a persisted fact.
            var receipt=receipts.SingleOrDefault(x=>x.CommandKey==fact.CommandKey)
                ??db.PrincipalReceivingCommandReceipts.Local.SingleOrDefault(x=>x.CommandKey==fact.CommandKey);
            if(receipt is null&&db.Entry(fact).State==EntityState.Added)continue;
            PrincipalReceivingStoredReceipt.Verify(fact,receipt,PrincipalReceivingStorageCodec.Source(state));
        }
        return PrincipalReceivingStorageCodec.Ledger(state,rows,db.PolicyVersions.Local.Where(x=>ids.Contains(x.Id)).ToArray());
    }
    public async Task<PrincipalReceivingDraftHead> HeadAsync(Guid id,CancellationToken ct)
    {
        Enlisted();var head=await db.PrincipalReceivingDraftHeads.SingleAsync(x=>x.Id==id,ct);
        await db.PrincipalReceivingDraftRevisions.Where(x=>x.DraftId==id&&x.Revision==head.Revision).LoadAsync(ct);
        var revision=db.PrincipalReceivingDraftRevisions.Local.Single(x=>x.DraftId==id&&x.Revision==head.Revision);
        await db.PrincipalReceivingDraftLineBindings.Where(x=>x.DraftRevisionId==revision.Id).LoadAsync(ct);
        return head;
    }
    public PrincipalReceivingDraftHead Stage(PrincipalGoodsDraft draft,Guid key,string actor)
        =>Stage(draft.Id,draft.Version,draft.TenantId,draft.LegalEntityId,draft.SiteId,draft.PurchaseOrderId,draft.IssuedVersionId,
            "Goods",draft.Creator,draft.State,PrincipalReceivingStorageCodec.Encode(draft),key,actor);
    public PrincipalReceivingDraftHead Stage(PrincipalServiceDraft draft,Guid key,string actor)
        =>Stage(draft.Id,draft.Version,draft.Source.Commercial.TenantId,draft.Source.Commercial.LegalEntityId,
            draft.Source.Commercial.BranchId!.Value,draft.Source.Commercial.PurchaseOrderId,draft.Source.Commercial.IssuedVersionId,
            "Service",draft.Creator,draft.State,PrincipalReceivingStorageCodec.Encode(draft),key,actor);
    private PrincipalReceivingDraftHead Stage(Guid id,long revision,string tenant,Guid company,Guid branch,Guid order,Guid issued,
        string kind,PrincipalReceivingPerson creator,string state,string json,Guid key,string actor)
    {
        Enlisted();if(key==Guid.Empty)throw new InvalidOperationException("PrincipalReceivingCommandKeyRequired");
        var head=db.PrincipalReceivingDraftHeads.Local.SingleOrDefault(x=>x.Id==id);
        var hash=ProcurementEvidenceEncoding.Digest(json);
        if(head is null)
        {
            if(revision!=1 || state!="Draft")throw new InvalidOperationException("PrincipalReceivingInitialDraftRequired");
            head=new(){Id=id,TenantId=tenant,LegalEntityId=company,BranchId=branch,PurchaseOrderId=order,IssuedVersionId=issued,
                Kind=kind,CreatorPrincipalId=creator.PrincipalId,CreatorWorkerAliasId=creator.WorkerAliasId,SnapshotSha256=hash};
            db.AddScoped(head);
        }
        else
        {
            if(head.Revision+1!=revision || head.State!="Draft")throw new InvalidOperationException("PrincipalReceivingDraftVersionChanged");
            head.Revision=revision;head.State=state;head.SnapshotSha256=hash;
        }
        var row=new PrincipalReceivingDraftRevision{TenantId=tenant,LegalEntityId=company,DraftId=id,Revision=revision,
            CommandKey=key,ActorPrincipalId=actor,SnapshotJson=json,SnapshotSha256=hash};
        var sources=PrincipalReceivingDraftSnapshot.Validate(head,row);db.AddScoped(row);
        foreach(var source in sources)
        {
            var sourceState=db.PrincipalReceivingSourceStates.Local.Single(x=>PrincipalReceivingStorageCodec.Source(x)==source);
            var goods=kind=="Goods"?PrincipalReceivingDraftSnapshot.Goods(row):null;
            var goodsLine=goods?.Lines.Single(x=>x.Source==source);
            var service=kind=="Service"?PrincipalReceivingDraftSnapshot.Service(row):null;
            db.AddScoped(new PrincipalReceivingDraftLineBinding{TenantId=tenant,LegalEntityId=company,DraftId=id,DraftRevisionId=row.Id,
                LineId=goodsLine?.Id??id,SourceStateId=sourceState.Id,BranchId=branch,PurchaseOrderId=order,IssuedVersionId=issued,
                PurchaseOrderLineId=source.Commercial.PurchaseOrderLineId,Quantity=goodsLine?.Quantity??service!.Quantity,
                RejectedQuantity=goodsLine?.RejectedQuantity??0});
        }
        return head;
    }
    public Task AppendAsync(PrincipalReceivingSourceState state,PrincipalGoodsObservation fact,CancellationToken ct)
        =>AppendAsync(state,fact.Id,fact.ReceiptId,"Observation",fact.Sequence,fact.CommandKey,fact.Receiver,fact.PolicyVersionId,fact.ReceivedQuantity,0,PrincipalReceivingStorageCodec.Encode(fact),ct);
    public Task AppendAsync(PrincipalReceivingSourceState state,Guid draftId,PrincipalGoodsInspection fact,CancellationToken ct)
        =>AppendAsync(state,fact.Id,draftId,"Inspection",fact.Sequence,fact.CommandKey,fact.Inspector,fact.PolicyVersionId,fact.QualifiedQuantity,0,PrincipalReceivingStorageCodec.Encode(fact),ct);
    public Task AppendAsync(PrincipalReceivingSourceState state,Guid draftId,PrincipalReceivingAcceptance fact,CancellationToken ct)
        =>AppendAsync(state,fact.Id,draftId,"Acceptance",fact.Sequence,fact.CommandKey,fact.Actor,fact.PolicyVersionId,fact.Quantity,fact.NetAmount,PrincipalReceivingStorageCodec.Encode(fact),ct);
    private async Task AppendAsync(PrincipalReceivingSourceState state,Guid id,Guid draft,string kind,long sequence,Guid key,
        PrincipalReceivingPerson actor,Guid policy,decimal quantity,decimal amount,string json,CancellationToken ct)
    {
        Enlisted();await ReconcileCountersAsync(state.PurchaseRequestLineId,ct);if(sequence!=state.Sequence+1)throw new InvalidOperationException("PrincipalReceivingSourceVersionChanged");
        var head=db.PrincipalReceivingDraftHeads.Local.Single(x=>x.Id==draft);
        var revision=db.PrincipalReceivingDraftRevisions.Local.Single(x=>x.DraftId==draft&&x.Revision==head.Revision);
        var lineBinding=db.PrincipalReceivingDraftLineBindings.Local.Single(x=>x.DraftRevisionId==revision.Id&&x.SourceStateId==state.Id);
        var row=new PrincipalReceivingFact{Id=id,DraftLineBindingId=lineBinding.Id,TenantId=state.TenantId,LegalEntityId=state.LegalEntityId,SourceStateId=state.Id,
            DraftId=draft,BranchId=state.BranchId,Kind=kind,Operation=kind=="Observation"?"Record":kind=="Inspection"?"Inspect":state.Kind=="Service"?"Certify":"Accept",Sequence=sequence,CommandKey=key,ActorPrincipalId=actor.PrincipalId,
            ActorWorkerAliasId=actor.WorkerAliasId,PolicyVersionId=policy,Quantity=quantity,NetAmount=amount,PayloadJson=json,
            PayloadSha256=ProcurementEvidenceEncoding.Digest(json)};
        db.AddScoped(row);state.Sequence=sequence;
        if(kind=="Acceptance")
        {
            state.AcceptedQuantity=ExactDecimalArithmetic.Add(state.AcceptedQuantity,quantity);
            state.AcceptedNetAmount=ExactDecimalArithmetic.Add(state.AcceptedNetAmount,amount);
            var line=db.PurchaseOrderLines.Local.Single(x=>x.Id==state.PurchaseOrderLineId);
            var request=db.PurchaseRequestLines.Local.Single(x=>x.Id==state.PurchaseRequestLineId);
            var next=ExactDecimalArithmetic.Add(request.AcceptedQuantity,quantity);
            if(next>request.ApprovedQuantity)throw new InvalidOperationException("PrincipalReceivingApprovedRequestCapacityExceeded");
            line.AcceptedQuantity=ExactDecimalArithmetic.Add(line.AcceptedQuantity,quantity);request.AcceptedQuantity=next;
        }
        // Revalidation includes all persisted facts, not merely this new delta.
        _=PrincipalReceivingStorageCodec.Ledger(state,db.PrincipalReceivingFacts.Local.Where(x=>x.SourceStateId==state.Id).ToArray(),db.PolicyVersions.Local.ToArray());
    }
    public PrincipalReceivingCommandReceipt Complete(PrincipalReceivingDraftHead head,string operation,Guid key,string actor,
        string fingerprint,string response,int status)
    {
        Enlisted();var row=new PrincipalReceivingCommandReceipt{TenantId=head.TenantId,LegalEntityId=head.LegalEntityId,
            DraftId=head.Id,BranchId=head.BranchId,Operation=operation,CommandKey=key,ActorPrincipalId=actor,
            RequestSha256=fingerprint,ResponseJson=response,ResponseSha256=ProcurementEvidenceEncoding.Digest(response),
            ResponseStatus=status,ResultRevision=head.Revision};db.AddScoped(row);return row;
    }
    /// <summary>Caller reauthorizes exact actor/action and retained record scope first.
    /// This read never repeats current commercial/policy eligibility or writes.</summary>
    public async Task<PrincipalReceivingCommandReceipt?> RecoverAsync(Guid key,string actor,string operation,
        string? fingerprint,CancellationToken ct)
    {
        var receipt=await db.PrincipalReceivingCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x=>x.CommandKey==key&&x.ActorPrincipalId==actor&&x.Operation==operation,ct);
        if(receipt is null)return null;
        if(fingerprint is not null&&fingerprint!=receipt.RequestSha256
            || receipt.ResponseSha256!=ProcurementEvidenceEncoding.Digest(receipt.ResponseJson))
            throw new InvalidOperationException("PrincipalReceivingCommandKeyReused");
        return receipt;
    }
    /// <summary>Complete successor aggregation. Mixed/unexplained legacy rows are
    /// unavailable, never adopted as zero; owner reconciliation is a separate contract.</summary>
    public async Task ReconcileCountersAsync(Guid requestLineId,CancellationToken ct)
    {
        Enlisted();
        var request=await db.PurchaseRequestLines.SingleAsync(x=>x.Id==requestLineId,ct);
        var lines=await db.PurchaseOrderLines.Where(x=>x.PurchaseRequestLineId==requestLineId).ToArrayAsync(ct);
        await db.PrincipalReceivingSourceStates.Where(x=>x.PurchaseRequestLineId==requestLineId).LoadAsync(ct);
        var states=db.PrincipalReceivingSourceStates.Local.Where(x=>x.PurchaseRequestLineId==requestLineId).ToArray();
        var ledgers=new List<PrincipalReceivingLedger>();
        foreach(var state in states)
        {
            if(!lines.Any(line=>line.Id==state.PurchaseOrderLineId&&line.PurchaseOrderId==state.PurchaseOrderId))
                throw new InvalidOperationException("PrincipalReceivingAggregateSourceUnknown");
            ledgers.Add(await LedgerAsync(state,ct));
        }
        foreach(var line in lines)
            if(await HasLegacyHistoryAsync(line.Id,ct))throw new InvalidOperationException("PrincipalReceivingEarlierHistoryUnknown");
        PrincipalReceivingCounterReconciliation.Verify(request,lines,ledgers);
    }
    public async Task<bool> HasLegacyHistoryAsync(Guid lineId,CancellationToken ct)
        =>await db.ReceivingSourceStates.AnyAsync(x=>x.PurchaseOrderLineId==lineId,ct)
            ||await db.GoodsReceiptLines.AnyAsync(x=>x.PurchaseOrderLineId==lineId,ct)
            ||await db.ServiceAcceptances.AnyAsync(x=>x.PurchaseOrderLineId==lineId,ct);
    private void Enlisted()
    {
        if(db.Database.IsRelational()&&db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("PrincipalReceivingOwnedTransactionRequired");
    }
}
