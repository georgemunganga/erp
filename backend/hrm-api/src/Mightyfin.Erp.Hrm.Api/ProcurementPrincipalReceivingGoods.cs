using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalReceivingRoutes
{
    private static Task<IResult> CreateGoods(ProcurementReceivingRoutes.ReceiptDraft input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,"procurement-receipt-create",async(db,cases,_,actor,token)=>
        {
            if(input is null)throw new Problem("ReceivingInputRequired",422);
            var key=Key(http);var hash=Fingerprint(new{operation="Create",kind="Goods",input});
            if(await Replay(db,actor,"Create",key,hash,token) is {} replay)return replay;
            if(input.Lines is not{Length:>0 and <=100})throw new Problem("ReceivingLinesRequired",422);
            var lines=new List<PrincipalReceivingDraftLifecycle.LineInput>();var store=new PrincipalReceivingStore(db);
            foreach(var line in input.Lines)
            {
                if(line is null)throw new Problem("ReceivingLineRequired",422);
                var source=await NewSource(db,cases,scope,http,actor,input.PurchaseOrderId,input.IssuedVersionId,line.PurchaseOrderLineId,token);
                _=await store.SourceAsync(source,token);
                lines.Add(new(null,source,Quantity(line.ReceivedQuantity),Quantity(line.RejectedQuantity)));
            }
            var draft=PrincipalReceivingDraftLifecycle.CreateGoods(actor,lines,input.ReceivedAt,input.DeliveryNoteRef,input.Notes);
            if(draft.SiteId!=input.SiteId)throw new Problem("ReceivingSiteChanged",422);
            var head=store.Stage(draft,key,actor.PrincipalId);return Complete(db,head,"Create",key,actor.PrincipalId,hash,201);
        },ct);
    private static Task<IResult> ReviseGoods(Guid id,ProcurementReceivingRoutes.VersionedReceipt input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,"procurement-receipt-edit",async(db,cases,_,actor,token)=>
        {
            if(input is null)throw new Problem("ReceivingInputRequired",422);
            var key=Key(http);var hash=Fingerprint(new{operation="Revise",id,input});
            if(await Replay(db,actor,"Revise",key,hash,token) is {} replay)return replay;
            var head=await Head(db,actor,id,"Goods",true,token);Version(head,input.Version);
            var previous=PrincipalReceivingDraftSnapshot.Goods(Revision(db,head));var draft=input.Draft;
            if(draft is null || draft.PurchaseOrderId!=head.PurchaseOrderId || draft.IssuedVersionId!=head.IssuedVersionId
                || draft.SiteId!=head.BranchId || draft.Lines is not{Length:>0 and <=100})throw new Problem("ReceivingSourceChanged",422);
            var lines=new List<PrincipalReceivingDraftLifecycle.LineInput>();var store=new PrincipalReceivingStore(db);
            foreach(var line in draft.Lines)
            {
                if(line is null)throw new Problem("ReceivingLineRequired",422);
                var retained=previous.Lines.SingleOrDefault(x=>x.Source.Commercial.PurchaseOrderLineId==line.PurchaseOrderLineId);
                PrincipalReceivingSource source;
                if(retained is not null){source=retained.Source;_=await StoredSource(db,cases,scope,actor,source,true,token);}
                else{source=await NewSource(db,cases,scope,http,actor,draft.PurchaseOrderId,draft.IssuedVersionId,line.PurchaseOrderLineId,token);_=await store.SourceAsync(source,token);}
                lines.Add(new(line.Id,source,Quantity(line.ReceivedQuantity),Quantity(line.RejectedQuantity)));
            }
            var revised=PrincipalReceivingDraftLifecycle.ReviseGoods(previous,actor,input.Version,lines,draft.ReceivedAt,draft.DeliveryNoteRef,draft.Notes);
            store.Stage(revised,key,actor.PrincipalId);return Complete(db,head,"Revise",key,actor.PrincipalId,hash);
        },ct);
    private static Task<IResult> CancelGoods(Guid id,ProcurementReceivingRoutes.CancelDraft input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,"procurement-receipt-edit",async(db,cases,_,actor,token)=>
        {
            if(input is null)throw new Problem("ReceivingInputRequired",422);
            var key=Key(http);var hash=Fingerprint(new{operation="Cancel",id,input});
            if(await Replay(db,actor,"Cancel",key,hash,token) is {} replay)return replay;
            var head=await Head(db,actor,id,"Goods",true,token);Version(head,input.Version);
            var draft=PrincipalReceivingDraftSnapshot.Goods(Revision(db,head));
            foreach(var line in draft.Lines) _=await StoredSource(db,cases,scope,actor,line.Source,true,token);
            new PrincipalReceivingStore(db).Stage(PrincipalReceivingDraftLifecycle.CancelGoods(draft,actor,input.Version,input.Reason),key,actor.PrincipalId);
            return Complete(db,head,"Cancel",key,actor.PrincipalId,hash);
        },ct);
    private static Task<IResult> GoodsDetail(Guid id,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,"procurement-receipt-view",async(db,cases,_,actor,token)=>
        {
            var head=await Head(db,actor,id,"Goods",false,token);
            foreach(var line in PrincipalReceivingDraftSnapshot.Goods(Revision(db,head)).Lines)
                _=await StoredSource(db,cases,scope,actor,line.Source,false,token);
            return new Reply(200,System.Text.Json.JsonSerializer.Serialize(View(db,head),Json));
        },ct);
    private static Task<IResult> Record(Guid id,ProcurementReceivingRoutes.RecordReceipt input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,ControlledReceiving.RecordPermission,async(db,cases,_,actor,token)=>
        {
            if(input is null)throw new Problem("ReceivingInputRequired",422);
            var key=Key(http);var hash=Fingerprint(new{operation="Record",id,input});
            if(await Replay(db,actor,"Record",key,hash,token) is {} replay)return replay;
            var head=await Head(db,actor,id,"Goods",true,token);Version(head,input.Version);
            var draft=PrincipalReceivingDraftSnapshot.Goods(Revision(db,head));
            var states=new Dictionary<Guid,PrincipalReceivingSourceState>();var ledgers=new Dictionary<Guid,PrincipalReceivingLedger>();var policies=new Dictionary<Guid,PolicyVersion>();
            foreach(var line in draft.Lines.OrderBy(x=>x.Source.Commercial.PurchaseOrderLineId))
            {
                var loaded=await StoredSource(db,cases,scope,actor,line.Source,true,token);var lineId=line.Source.Commercial.PurchaseOrderLineId;
                states.Add(lineId,loaded.State);ledgers.Add(lineId,loaded.Ledger);
                policies.Add(lineId,(await ProcurementPrincipalReceivingPolicy.CurrentAsync(db,scope,http.RequestServices,line.Source,token)).Record);
            }
            var result=PrincipalReceivingDraftLifecycle.RecordGoods(draft,actor,input.Version,ledgers,policies,
                ledgers.ToDictionary(x=>x.Key,x=>x.Value.Sequence),key,input.EvidenceReference,DateTimeOffset.UtcNow);
            var store=new PrincipalReceivingStore(db);store.Stage(result.Draft,key,actor.PrincipalId);
            foreach(var fact in result.Observations)store.Append(states[fact.Source.Commercial.PurchaseOrderLineId],fact);
            return Complete(db,head,"Record",key,actor.PrincipalId,hash);
        },ct);
    private static Task<IResult> Inspect(Guid id,Guid lineId,ProcurementReceivingRoutes.ReceivingAction input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,ControlledReceiving.InspectPermission,async(db,cases,core,actor,token)=>
        {
            if(input is null)throw new Problem("ReceivingInputRequired",422);
            var key=Key(http);var hash=Fingerprint(new{operation="Inspect",id,lineId,input});
            if(await Replay(db,actor,"Inspect",key,hash,token) is {} replay)return replay;
            var head=await Head(db,actor,id,"Goods",false,token);Version(head,input.Version);
            if(head.State!="Recorded")throw new Problem("ReceivingPhysicalObservationRequired",409);
            var line=PrincipalReceivingDraftSnapshot.Goods(Revision(db,head)).Lines.SingleOrDefault(x=>x.Id==lineId)??throw new Problem("ReceivingLineNotFound",404);
            var loaded=await StoredSource(db,cases,scope,actor,line.Source,true,token);
            var observed=loaded.Ledger.Observations.SingleOrDefault(x=>x.ReceiptId==id&&x.ReceiptLineId==lineId)??throw new Problem("ReceivingPhysicalObservationRequired",409);
            var policy=await ProcurementPrincipalReceivingPolicy.CurrentAsync(db,scope,http.RequestServices,line.Source,token);
            if(policy.Rule.RequireIndependentInspector)await ProcurementPrincipalReceivingActor.RequireIndependentAsync(core,actor,[observed.Receiver],token);
            var fact=PrincipalControlledReceiving.Inspect(loaded.Ledger,policy.Record,actor,observed.Id,key,input.SourceVersion,Quantity(input.Quantity),input.EvidenceReference,DateTimeOffset.UtcNow);
            new PrincipalReceivingStore(db).Append(loaded.State,id,fact);return Complete(db,head,"Inspect",key,actor.PrincipalId,hash);
        },ct);
    private static Task<IResult> Accept(Guid id,Guid lineId,ProcurementReceivingRoutes.ReceivingAction input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,ControlledReceiving.AcceptPermission,async(db,cases,core,actor,token)=>
        {
            if(input is null)throw new Problem("ReceivingInputRequired",422);
            var key=Key(http);var hash=Fingerprint(new{operation="Accept",id,lineId,input});
            if(await Replay(db,actor,"Accept",key,hash,token) is {} replay)return replay;
            var head=await Head(db,actor,id,"Goods",false,token);Version(head,input.Version);
            if(head.State!="Recorded")throw new Problem("ReceivingPhysicalObservationRequired",409);
            var line=PrincipalReceivingDraftSnapshot.Goods(Revision(db,head)).Lines.SingleOrDefault(x=>x.Id==lineId)??throw new Problem("ReceivingLineNotFound",404);
            var loaded=await StoredSource(db,cases,scope,actor,line.Source,true,token);
            var observed=loaded.Ledger.Observations.SingleOrDefault(x=>x.ReceiptId==id&&x.ReceiptLineId==lineId)??throw new Problem("ReceivingPhysicalObservationRequired",409);
            var policy=await ProcurementPrincipalReceivingPolicy.CurrentAsync(db,scope,http.RequestServices,line.Source,token);
            var denied=new List<PrincipalReceivingPerson>();if(!policy.Rule.AllowBuyerCertification)denied.Add(line.Source.Buyer);
            if(!policy.Rule.AllowRequesterCertification)denied.Add(line.Source.Requester);if(!policy.Rule.AllowReceiverCertification)denied.Add(observed.Receiver);
            await ProcurementPrincipalReceivingActor.RequireIndependentAsync(core,actor,denied,token);
            var fact=PrincipalControlledReceiving.AcceptGoods(loaded.Ledger,policy.Record,actor,observed.Id,key,input.SourceVersion,Quantity(input.Quantity),input.EvidenceReference,DateTimeOffset.UtcNow);
            new PrincipalReceivingStore(db).Append(loaded.State,id,fact);return Complete(db,head,"Accept",key,actor.PrincipalId,hash);
        },ct);
}
