using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalReceivingRoutes
{
    private static Task<IResult> CreateService(ProcurementReceivingRoutes.ServiceDraft input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,"procurement-receipt-create",async(db,cases,unusedCore,actor,token)=>
        {
            if(input is null)throw new Problem("ReceivingInputRequired",422);
            var key=Key(http);var hash=Fingerprint(new{operation="Create",kind="Service",input});
            if(await Replay(db,actor,"Create",key,hash,token) is {} replay)return replay;
            var source=await NewSource(db,cases,scope,http,actor,input.PurchaseOrderId,input.IssuedVersionId,input.PurchaseOrderLineId,token);
            var store=new PrincipalReceivingStore(db);_=await store.SourceAsync(source,token);
            await ReconcileRequest(db,cases,scope,actor,source.Commercial.PurchaseRequestLineId,token);
            var draft=PrincipalReceivingDraftLifecycle.CreateService(actor,source,input.DeliverableReference,input.PeriodFrom,input.PeriodTo,Quantity(input.Quantity),input.Notes);
            var head=store.Stage(draft,key,actor.PrincipalId);return Complete(db,head,"Create",key,actor.PrincipalId,hash,201);
        },ct);
    private static Task<IResult> ReviseService(Guid id,ProcurementReceivingRoutes.VersionedService input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,"procurement-receipt-edit",async(db,cases,unusedCore,actor,token)=>
        {
            if(input is null)throw new Problem("ReceivingInputRequired",422);
            var key=Key(http);var hash=Fingerprint(new{operation="Revise",id,input});
            if(await Replay(db,actor,"Revise",key,hash,token) is {} replay)return replay;
            var head=await Head(db,actor,id,"Service",true,token);Version(head,input.Version);
            var draft=PrincipalReceivingDraftSnapshot.Service(Revision(db,head));var next=input.Draft;
            if(next is null || next.PurchaseOrderId!=head.PurchaseOrderId || next.IssuedVersionId!=head.IssuedVersionId
                || next.PurchaseOrderLineId!=draft.Source.Commercial.PurchaseOrderLineId)throw new Problem("ReceivingSourceChanged",422);
            _=await StoredSource(db,cases,scope,actor,draft.Source,true,token);
            var revised=PrincipalReceivingDraftLifecycle.ReviseService(draft,actor,input.Version,next.DeliverableReference,next.PeriodFrom,next.PeriodTo,Quantity(next.Quantity),next.Notes);
            new PrincipalReceivingStore(db).Stage(revised,key,actor.PrincipalId);return Complete(db,head,"Revise",key,actor.PrincipalId,hash);
        },ct);
    private static Task<IResult> CancelService(Guid id,ProcurementReceivingRoutes.CancelDraft input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,"procurement-receipt-edit",async(db,cases,unusedCore,actor,token)=>
        {
            if(input is null)throw new Problem("ReceivingInputRequired",422);
            var key=Key(http);var hash=Fingerprint(new{operation="Cancel",id,input});
            if(await Replay(db,actor,"Cancel",key,hash,token) is {} replay)return replay;
            var head=await Head(db,actor,id,"Service",true,token);Version(head,input.Version);
            var draft=PrincipalReceivingDraftSnapshot.Service(Revision(db,head));_=await StoredSource(db,cases,scope,actor,draft.Source,true,token);
            new PrincipalReceivingStore(db).Stage(PrincipalReceivingDraftLifecycle.CancelService(draft,actor,input.Version,input.Reason),key,actor.PrincipalId);
            return Complete(db,head,"Cancel",key,actor.PrincipalId,hash);
        },ct);
    private static Task<IResult> ServiceDetail(Guid id,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,"procurement-receipt-view",async(db,cases,core,actor,token)=>
        {
            var head=await Head(db,actor,id,"Service",false,token);
            _=await StoredSource(db,cases,scope,actor,PrincipalReceivingDraftSnapshot.Service(Revision(db,head)).Source,false,token);
            return new Reply(200,System.Text.Json.JsonSerializer.Serialize(await DetailView(db,core,scope,http,head,actor,token),Json));
        },ct);
    private static Task<IResult> Certify(Guid id,ProcurementReceivingRoutes.ReceivingAction input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,ControlledReceiving.CertifyPermission,async(db,cases,core,actor,token)=>
        {
            if(input is null)throw new Problem("ReceivingInputRequired",422);
            var key=Key(http);var hash=Fingerprint(new{operation="Certify",id,input});
            if(await Replay(db,actor,"Certify",key,hash,token) is {} replay)return replay;
            var head=await Head(db,actor,id,"Service",false,token);Version(head,input.Version);
            var draft=PrincipalReceivingDraftSnapshot.Service(Revision(db,head));
            if(Quantity(input.Quantity)!=draft.Quantity)throw new Problem("ReceivingServiceQuantityChanged",409);
            var loaded=await StoredSource(db,cases,scope,actor,draft.Source,true,token);
            var policy=await ProcurementPrincipalReceivingPolicy.CurrentAsync(db,scope,http.RequestServices,draft.Source,token);
            var denied=new List<PrincipalReceivingPerson>();if(!policy.Rule.AllowBuyerCertification)denied.Add(draft.Source.Buyer);
            if(!policy.Rule.AllowRequesterCertification)denied.Add(draft.Source.Requester);
            await ProcurementPrincipalReceivingActor.RequireIndependentAsync(core,actor,denied,token);
            var companyDate=await ProcurementPrincipalReceivingBusinessDate.ObserveAsync(core,
                scope.TenantId,scope.LegalEntityId,http.RequestServices.GetRequiredService<TimeProvider>(),token);
            var result=PrincipalReceivingDraftLifecycle.CertifyService(draft,actor,input.Version,loaded.Ledger,
                policy.Record,input.SourceVersion,key,input.EvidenceReference,companyDate.ObservedAtUtc,companyDate);
            var store=new PrincipalReceivingStore(db);store.Stage(result.Draft,key,actor.PrincipalId);await store.AppendAsync(loaded.State,id,result.Acceptance,token);
            return Complete(db,head,"Certify",key,actor.PrincipalId,hash);
        },ct);
}
