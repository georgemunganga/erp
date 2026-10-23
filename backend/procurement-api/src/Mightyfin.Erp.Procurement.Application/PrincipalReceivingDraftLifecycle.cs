using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Explicit pure draft transitions over server-resolved source pins. Existing
/// serializer, store, current-policy/Core checks, transaction and durable receipts remain owning.
/// No detached snapshot supplied by a client is authority.</summary>
public static class PrincipalReceivingDraftLifecycle
{
    public sealed record LineInput(Guid? Id,PrincipalReceivingSource Source,decimal Quantity,decimal RejectedQuantity);
    public sealed record GoodsRecordResult(PrincipalGoodsDraft Draft,IReadOnlyList<PrincipalGoodsObservation> Observations);
    public sealed record ServiceRecordResult(PrincipalServiceDraft Draft,PrincipalReceivingAcceptance Acceptance);

    public static PrincipalGoodsDraft CreateGoods(PrincipalReceivingActor actor,IReadOnlyList<LineInput> lines,
        DateTimeOffset observedAt,string? deliveryNote,string? notes)
    {
        var normalized=GoodsLines(lines,null);var source=normalized[0].Source;Scope(actor,source,"procurement-receipt-create");
        Need(observedAt!=default,"PrincipalReceivingObservedDateRequired");
        return new(Guid.CreateVersion7(),1,source.Commercial.TenantId,source.Commercial.LegalEntityId,
            source.Commercial.PurchaseOrderId,source.Commercial.IssuedVersionId,source.Commercial.BranchId!.Value,
            new(actor.PrincipalId,actor.WorkerAliasId),Time(observedAt),Clean(deliveryNote,200),Clean(notes,2000),"Draft",normalized);
    }
    public static PrincipalGoodsDraft ReviseGoods(PrincipalGoodsDraft draft,PrincipalReceivingActor actor,long version,
        IReadOnlyList<LineInput> lines,DateTimeOffset observedAt,string? deliveryNote,string? notes)
    {
        VerifyGoods(draft);Own(draft.Creator,actor,draft.Lines[0].Source,"procurement-receipt-edit");
        Need(draft.State=="Draft"&&draft.Version==version&&observedAt!=default,"PrincipalReceivingDraftChanged");
        var normalized=GoodsLines(lines,draft);
        return draft with{Version=Next(version),Lines=normalized,ObservedAt=Time(observedAt),DeliveryNoteReference=Clean(deliveryNote,200),Notes=Clean(notes,2000)};
    }
    public static PrincipalGoodsDraft CancelGoods(PrincipalGoodsDraft draft,PrincipalReceivingActor actor,long version,string reason)
    {
        VerifyGoods(draft);Own(draft.Creator,actor,draft.Lines[0].Source,"procurement-receipt-edit");
        Need(draft.State=="Draft"&&draft.Version==version&&!string.IsNullOrWhiteSpace(reason)&&reason.Trim().Length<=1000,"PrincipalReceivingDraftChanged");
        return draft with{Version=Next(version),State="Cancelled",CancellationReason=reason.Trim()};
    }
    public static GoodsRecordResult RecordGoods(PrincipalGoodsDraft draft,PrincipalReceivingActor actor,long version,
        IReadOnlyDictionary<Guid,PrincipalReceivingLedger> ledgers,IReadOnlyDictionary<Guid,PolicyVersion> policies,IReadOnlyDictionary<Guid,long> expectedSequences,
        Guid commandKey,string evidence,DateTimeOffset at)
    {
        VerifyGoods(draft);Own(draft.Creator,actor,draft.Lines[0].Source,ControlledReceiving.RecordPermission);
        Need(draft.State=="Draft"&&draft.Version==version&&!string.IsNullOrWhiteSpace(draft.DeliveryNoteReference)
            &&ledgers.Count==draft.Lines.Count&&policies.Count==draft.Lines.Count&&expectedSequences.Count==draft.Lines.Count,"PrincipalReceivingDraftChanged");
        var result=new List<PrincipalGoodsObservation>(draft.Lines.Count);
        foreach(var line in draft.Lines)
        {
            Need(ledgers.TryGetValue(line.Source.Commercial.PurchaseOrderLineId,out var ledger)&&ledger.Source==line.Source
                &&policies.TryGetValue(line.Source.Commercial.PurchaseOrderLineId,out _)&&expectedSequences.ContainsKey(line.Source.Commercial.PurchaseOrderLineId),"PrincipalReceivingDraftSourceChanged");
            result.Add(PrincipalControlledReceiving.Record(ledger!,policies[line.Source.Commercial.PurchaseOrderLineId],actor,
                draft.Id,line.Id,commandKey,expectedSequences[line.Source.Commercial.PurchaseOrderLineId],line.Quantity,line.RejectedQuantity,draft.SiteId,
                draft.DeliveryNoteReference!,evidence,draft.ObservedAt,at));
        }
        return new(draft with{Version=Next(version),State="Recorded"},result);
    }
    public static PrincipalServiceDraft CreateService(PrincipalReceivingActor actor,PrincipalReceivingSource source,
        string deliverable,DateOnly from,DateOnly to,decimal quantity,string? notes)
    {
        ServiceFields(source,deliverable,from,to,quantity);Scope(actor,source,"procurement-receipt-create");
        return new(Guid.CreateVersion7(),1,source,new(actor.PrincipalId,actor.WorkerAliasId),"Draft",deliverable.Trim(),from,to,quantity,Clean(notes,2000));
    }
    public static PrincipalServiceDraft ReviseService(PrincipalServiceDraft draft,PrincipalReceivingActor actor,long version,
        string deliverable,DateOnly from,DateOnly to,decimal quantity,string? notes)
    {
        VerifyService(draft);Own(draft.Creator,actor,draft.Source,"procurement-receipt-edit");
        Need(draft.State=="Draft"&&draft.Version==version,"PrincipalReceivingDraftChanged");ServiceFields(draft.Source,deliverable,from,to,quantity);
        return draft with{Version=Next(version),DeliverableReference=deliverable.Trim(),PeriodFrom=from,PeriodTo=to,Quantity=quantity,Notes=Clean(notes,2000)};
    }
    public static PrincipalServiceDraft CancelService(PrincipalServiceDraft draft,PrincipalReceivingActor actor,long version,string reason)
    {
        VerifyService(draft);Own(draft.Creator,actor,draft.Source,"procurement-receipt-edit");
        Need(draft.State=="Draft"&&draft.Version==version&&!string.IsNullOrWhiteSpace(reason)&&reason.Trim().Length<=1000,"PrincipalReceivingDraftChanged");
        return draft with{Version=Next(version),State="Cancelled",CancellationReason=reason.Trim()};
    }
    public static ServiceRecordResult CertifyService(PrincipalServiceDraft draft,PrincipalReceivingActor actor,long version,
        PrincipalReceivingLedger ledger,PolicyVersion policy,long expectedSequence,Guid key,string evidence,DateTimeOffset at)
    {
        VerifyService(draft);Need(draft.State=="Draft"&&draft.Version==version&&draft.Source==ledger.Source,"PrincipalReceivingDraftChanged");
        var accepted=PrincipalControlledReceiving.CertifyService(ledger,policy,actor,draft.Id,key,expectedSequence,
            draft.Quantity,draft.DeliverableReference,draft.PeriodFrom,draft.PeriodTo,evidence,at);
        return new(draft with{Version=Next(version),State="Certified"},accepted);
    }
    public static void VerifyGoods(PrincipalGoodsDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);Need(draft.Id!=Guid.Empty&&draft.Version>0&&draft.State is "Draft" or "Recorded" or "Cancelled"
            &&Person(draft.Creator)&&draft.Lines is {Count:>0 and <=100}&&draft.ObservedAt!=default,"PrincipalReceivingDraftInvalid");
        Need(draft.State=="Cancelled"?!string.IsNullOrWhiteSpace(draft.CancellationReason)&&draft.CancellationReason.Length<=1000:draft.CancellationReason is null,"PrincipalReceivingCancellationReasonInvalid");
        var lines=GoodsLines(draft.Lines.Select(line=>new LineInput(line.Id,line.Source,line.Quantity,line.RejectedQuantity)).ToArray(),draft);
        var source=lines[0].Source.Commercial;
        Need(draft.TenantId==source.TenantId&&draft.LegalEntityId==source.LegalEntityId&&draft.PurchaseOrderId==source.PurchaseOrderId
            &&draft.IssuedVersionId==source.IssuedVersionId&&draft.SiteId==source.BranchId,"PrincipalReceivingDraftSourceChanged");
    }
    public static void VerifyService(PrincipalServiceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);Need(draft.Id!=Guid.Empty&&draft.Version>0&&draft.State is "Draft" or "Certified" or "Cancelled"&&Person(draft.Creator),"PrincipalReceivingDraftInvalid");
        Need(draft.State=="Cancelled"?!string.IsNullOrWhiteSpace(draft.CancellationReason)&&draft.CancellationReason.Length<=1000:draft.CancellationReason is null,"PrincipalReceivingCancellationReasonInvalid");
        ServiceFields(draft.Source,draft.DeliverableReference,draft.PeriodFrom,draft.PeriodTo,draft.Quantity);
    }
    private static PrincipalGoodsDraftLine[] GoodsLines(IReadOnlyList<LineInput> lines,PrincipalGoodsDraft? previous)
    {
        Need(lines is {Count:>0 and <=100},"PrincipalReceivingDraftLinesRequired");var result=new List<PrincipalGoodsDraftLine>(lines.Count);
        var first=lines[0].Source;PrincipalControlledReceiving.VerifySource(first);
        foreach(var line in lines)
        {
            PrincipalControlledReceiving.VerifySource(line.Source);var p=line.Source.Commercial;
            Need(p.Kind==ReceivingKind.Goods&&p.TenantId==first.Commercial.TenantId&&p.LegalEntityId==first.Commercial.LegalEntityId
                &&p.PurchaseOrderId==first.Commercial.PurchaseOrderId&&p.IssuedVersionId==first.Commercial.IssuedVersionId&&p.BranchId==first.Commercial.BranchId
                &&line.Quantity>0&&Quantity(line.Quantity)&&line.RejectedQuantity>=0&&line.RejectedQuantity<=line.Quantity&&Quantity(line.RejectedQuantity),"PrincipalReceivingDraftLineInvalid");
            var old=previous?.Lines.SingleOrDefault(value=>value.Source.Commercial.PurchaseOrderLineId==p.PurchaseOrderLineId);
            Need(previous is null||(old is null||line.Source==old.Source)&&(line.Id is null || old is not null&&line.Id==old.Id),"PrincipalReceivingDraftLineIdentityChanged");
            if(previous is not null)Need(previous.PurchaseOrderId==p.PurchaseOrderId&&previous.IssuedVersionId==p.IssuedVersionId&&previous.SiteId==p.BranchId,"PrincipalReceivingDraftSourceChanged");
            result.Add(new(previous is null?Guid.CreateVersion7():line.Id??old?.Id??Guid.CreateVersion7(),line.Source,line.Quantity,line.RejectedQuantity));
        }
        Need(result.Select(value=>value.Id).Distinct().Count()==result.Count&&result.Select(value=>value.Source.Commercial.PurchaseOrderLineId).Distinct().Count()==result.Count,"PrincipalReceivingDuplicateDraftLine");return result.ToArray();
    }
    private static void ServiceFields(PrincipalReceivingSource source,string deliverable,DateOnly from,DateOnly to,decimal quantity)
    {
        PrincipalControlledReceiving.VerifySource(source);Need(source.Commercial.Kind==ReceivingKind.Service&&!string.IsNullOrWhiteSpace(deliverable)&&deliverable.Trim().Length<=200
            &&from!=default&&to>=from&&quantity>0&&Quantity(quantity),"PrincipalReceivingServiceDraftInvalid");
    }
    private static void Own(PrincipalReceivingPerson owner,PrincipalReceivingActor actor,PrincipalReceivingSource source,string permission)
    {Scope(actor,source,permission);Need(owner.PrincipalId==actor.PrincipalId,"PrincipalReceivingDraftOwnerRequired");}
    private static void Scope(PrincipalReceivingActor actor,PrincipalReceivingSource source,string permission)
        =>Need(ProcurementPrincipalEvidence.Canonical(actor.PrincipalId)&&ProcurementPrincipalEvidence.OptionalWorker(actor.WorkerAliasId)
            &&actor.TenantId==source.Commercial.TenantId&&actor.LegalEntityId==source.Commercial.LegalEntityId
            &&source.Commercial.BranchId is {} branch&&actor.AllowedBranches.Contains(branch)&&actor.Permissions.Contains(permission),"PrincipalReceivingActorDenied");
    private static bool Person(PrincipalReceivingPerson value)=>ProcurementPrincipalEvidence.Canonical(value.PrincipalId)&&ProcurementPrincipalEvidence.OptionalWorker(value.WorkerAliasId);
    private static bool Quantity(decimal value)=>value<=99_999_999_999_999.9999m&&decimal.Round(value,4)==value;
    private static string? Clean(string? value,int maximum){Need(value is null||value.Trim().Length<=maximum,"PrincipalReceivingDraftTextTooLong");return string.IsNullOrWhiteSpace(value)?null:value.Trim();}
    private static DateTimeOffset Time(DateTimeOffset value)=>ProcurementEvidenceEncoding.DatabaseTimestamp(value)!.Value;
    private static long Next(long value)=>checked(value+1);
    private static void Need(bool value,string code){if(!value)throw new InvalidOperationException(code);}
}
