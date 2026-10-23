using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Validates persisted typed snapshots against their normalized head. This
/// never resolves current actor/policy or turns a client snapshot into owned evidence.</summary>
public static class PrincipalReceivingDraftSnapshot
{
    public static IReadOnlyList<PrincipalReceivingSource> Sources(string kind,PrincipalReceivingDraftRevision revision) => kind switch
    {
        "Goods" => Goods(revision).Lines.Select(x=>x.Source).ToArray(),
        "Service" => [Service(revision).Source],
        _=>throw new InvalidOperationException("PrincipalReceivingDraftKindInvalid")
    };
    public static PrincipalGoodsDraft Goods(PrincipalReceivingDraftRevision revision)
        =>PrincipalReceivingStorageCodec.Decode<PrincipalGoodsDraft>(revision.SnapshotJson,revision.SnapshotSha256);
    public static PrincipalServiceDraft Service(PrincipalReceivingDraftRevision revision)
        =>PrincipalReceivingStorageCodec.Decode<PrincipalServiceDraft>(revision.SnapshotJson,revision.SnapshotSha256);
    public static IReadOnlyList<PrincipalReceivingSource> Validate(PrincipalReceivingDraftHead head,PrincipalReceivingDraftRevision revision)
    {
        if(head.Id!=revision.DraftId || head.Revision!=revision.Revision || head.SnapshotSha256!=revision.SnapshotSha256
            || head.TenantId!=revision.TenantId || head.LegalEntityId!=revision.LegalEntityId)
            throw new InvalidOperationException("PrincipalReceivingDraftSnapshotBindingInvalid");
        PrincipalReceivingPerson creator;string state;Guid id;long version;IReadOnlyList<PrincipalReceivingSource> sources;
        if(head.Kind=="Goods")
        {
            var draft=Goods(revision);PrincipalReceivingDraftLifecycle.VerifyGoods(draft);creator=draft.Creator;state=draft.State;id=draft.Id;version=draft.Version;
            if(draft.Lines is null || draft.Lines.Count is <1 or >100 || draft.SiteId!=head.BranchId
                || draft.PurchaseOrderId!=head.PurchaseOrderId || draft.IssuedVersionId!=head.IssuedVersionId
                || draft.TenantId!=head.TenantId || draft.LegalEntityId!=head.LegalEntityId
                || draft.Lines.Any(x=>x is null || x.Id==Guid.Empty || x.Quantity<=0 || !Quantity(x.Quantity)
                    || x.RejectedQuantity<0 || x.RejectedQuantity>x.Quantity || !Quantity(x.RejectedQuantity))
                || draft.Lines.Select(x=>x.Id).Distinct().Count()!=draft.Lines.Count)
                throw new InvalidOperationException("PrincipalReceivingGoodsSnapshotInvalid");
            sources=draft.Lines.Select(x=>x.Source).ToArray();
        }
        else if(head.Kind=="Service")
        {
            var draft=Service(revision);PrincipalReceivingDraftLifecycle.VerifyService(draft);creator=draft.Creator;state=draft.State;id=draft.Id;version=draft.Version;
            if(draft.Quantity<=0 || !Quantity(draft.Quantity) || string.IsNullOrWhiteSpace(draft.DeliverableReference)
                || draft.DeliverableReference.Length>500 || draft.PeriodTo<draft.PeriodFrom)
                throw new InvalidOperationException("PrincipalReceivingServiceSnapshotInvalid");
            sources=[draft.Source];
        }
        else throw new InvalidOperationException("PrincipalReceivingDraftKindInvalid");
        if(id!=head.Id || version!=head.Revision || state!=head.State || creator is null
            || creator.PrincipalId!=head.CreatorPrincipalId || creator.WorkerAliasId!=head.CreatorWorkerAliasId
            || sources.Select(x=>x.Commercial.PurchaseOrderLineId).Distinct().Count()!=sources.Count)
            throw new InvalidOperationException("PrincipalReceivingDraftSnapshotBindingInvalid");
        foreach(var source in sources)
        {
            PrincipalControlledReceiving.VerifySource(source);var pin=source.Commercial;
            if(pin.TenantId!=head.TenantId || pin.LegalEntityId!=head.LegalEntityId || pin.BranchId!=head.BranchId
                || pin.PurchaseOrderId!=head.PurchaseOrderId || pin.IssuedVersionId!=head.IssuedVersionId
                || pin.Kind.ToString()!=head.Kind)
                throw new InvalidOperationException("PrincipalReceivingDraftSnapshotSourceInvalid");
        }
        return sources;
    }
    public static void Transition(string kind,PrincipalReceivingDraftRevision before,PrincipalReceivingDraftRevision after)
    {
        if(kind=="Goods")
        {
            var previous=Goods(before);var next=Goods(after);
            if(next.Lines.Any(line=>previous.Lines.Any(old=>
                (old.Id==line.Id || old.Source.Commercial.PurchaseOrderLineId==line.Source.Commercial.PurchaseOrderLineId)
                &&(old.Id!=line.Id || old.Source!=line.Source))))
                throw new InvalidOperationException("PrincipalReceivingDraftSourcesImmutable");
            if(next.State!="Draft" && PrincipalReceivingStorageCodec.Encode(previous with{Version=next.Version,State=next.State,CancellationReason=next.CancellationReason})
                !=PrincipalReceivingStorageCodec.Encode(next))
                throw new InvalidOperationException("PrincipalReceivingTerminalDraftChanged");
        }
        else
        {
            var previous=Service(before);var next=Service(after);
            if(previous.Source!=next.Source || next.State!="Draft" && PrincipalReceivingStorageCodec.Encode(previous with{Version=next.Version,State=next.State,CancellationReason=next.CancellationReason})
                !=PrincipalReceivingStorageCodec.Encode(next))
                throw new InvalidOperationException("PrincipalReceivingTerminalDraftChanged");
        }
    }
    private static bool Quantity(decimal value)=>value<=99_999_999_999_999.9999m&&decimal.Round(value,4)==value;
}
