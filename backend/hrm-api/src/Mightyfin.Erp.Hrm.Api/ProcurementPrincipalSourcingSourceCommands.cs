using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private static Task<IResult> PrincipalCreate(CreateInput input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
    {
        if(!PrincipalSourceAvailable(http))return Task.FromResult(Error("PrincipalSourcingSourceUnavailable",503));
        var eventId=Guid.CreateVersion7();
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var key=Key(http);var title=Normalize(input.Title);
            if(title.Length is <1 or >200 || input.Sources is not {Length: >0 and <=200}
                || input.Sources.Any(x=>x.PurchaseRequestId==Guid.Empty || x.PurchaseRequestLineId==Guid.Empty
                    || x.ExpectedRequestVersion<1 || x.Quantity<=0 || !ExactDecimalJsonCodec.IsRepresentable(x.Quantity,18,4))
                || input.Sources.Select(x=>x.PurchaseRequestLineId).Distinct().Count()!=input.Sources.Length)
                throw new Problem("SourcingSourceSelectionInvalid",422);
            var actor=await PrincipalActor(core,scope,http,auth,"procurement-sourcing-convert",null,false,token);
            var fingerprint=Fingerprint(new{schemaVersion=2,operation="Create",title,
                sources=input.Sources.OrderBy(x=>x.PurchaseRequestLineId).Select(x=>new{x.PurchaseRequestId,
                    x.PurchaseRequestLineId,x.ExpectedRequestVersion,quantity=ProcurementEvidenceEncoding.Decimal(x.Quantity)}).ToArray()});
            if(await PrincipalReplay(db,core,scope,http,auth,actor,"procurement-sourcing-convert","Create",key,fingerprint,null,token) is { } prior)
                return prior;
            foreach(var requestId in input.Sources.Select(x=>x.PurchaseRequestId).Distinct().Order())
                await ProcurementApprovalExecutionLocks.LockRequestAsync(db,scope,requestId,token);
            var loaded=new List<RfqPrincipalSelection>();var now=DateTimeOffset.UtcNow;
            foreach(var selected in input.Sources.OrderBy(x=>x.PurchaseRequestLineId))
            {
                var(source,_)=await PrincipalEligible(db,cases,core,scope,http,auth,
                    selected.PurchaseRequestId,selected.PurchaseRequestLineId,now,token);
                loaded.Add(new(source,selected.ExpectedRequestVersion,selected.Quantity));
            }
            var plan=GovernedPrincipalRfqDraft.Create(actor,eventId,key,title,loaded,now);
            var(payload,sha)=GovernedPrincipalRfqRestoration.Capture(plan.Draft,key);
            var first=plan.Draft.Lines[0];
            var evidence=new SourcingDraftEvidence{SchemaVersion=2,SourcingEventId=eventId,
                BranchRef=plan.Draft.BranchId,MakerSubjectId=actor.PrincipalId,MakerPrincipalId=actor.PrincipalId,
                MakerWorkerId=actor.LinkedWorkerId,CreateCommandKey=key,
                OriginalDraftFingerprint=plan.Draft.OriginalFingerprint,PayloadJson=payload,PayloadSha256=sha};
            var ev=new SourcingEvent{Id=eventId,ExecutionVersion=2,Number="RF-"+eventId.ToString("N"),
                Title=plan.Draft.Title,Method=first.MethodCode,MethodId=first.MethodId,MethodRevision=first.MethodRevision,
                BranchRef=plan.Draft.BranchId,CurrencyCode=plan.Draft.CurrencyCode,OwnerSubjectId=actor.PrincipalId,
                MakerWorkerId=actor.LinkedWorkerId,SourceEvidenceId=evidence.Id,
                OriginalDraftFingerprint=plan.Draft.OriginalFingerprint};
            db.AddScoped(ev);db.AddScoped(evidence);
            var number=0;
            foreach(var pin in plan.Draft.Lines)
            {
                var source=loaded.Single(x=>x.Source.Line.Id==pin.LineId).Source;
                var effect=plan.Effects.Single(x=>x.AllocationId==pin.AllocationId);
                db.AddScoped(new SourcingLine{Id=pin.EventLineId,SourcingEventId=ev.Id,LineNumber=++number,
                    PurchaseRequestLineId=pin.LineId,Description=source.Line.Description,UnitCode=pin.UnitCode,
                    CategoryCode=pin.CategoryCode,NeededBy=source.Line.NeededBy,Quantity=pin.Quantity});
                db.AddScoped(new PurchaseRequestAllocation{Id=pin.AllocationId,PurchaseRequestId=pin.RequestId,
                    PurchaseRequestLineId=pin.LineId,TargetType="SourcingEvent",TargetId=ev.Id,Method=pin.MethodCode,
                    Status="Reserved",Quantity=pin.Quantity,EstimatedValue=pin.EstimatedValue,
                    CurrencyCode=ev.CurrencyCode,DecidedBy=actor.PrincipalId,DecidedAt=now,IdempotencyKey=effect.EffectKey});
                db.AddScoped(PrincipalBinding(ev.Id,evidence.Id,pin));
                source.Line.AllocatedQuantity=effect.ResultingAllocatedQuantity;source.Line.Route="PendingSourcing";
                source.Request.UpdatedAt=now;
            }
            PrincipalRecord(db,scope,ev,"DraftReserved",key,new{lineIds=plan.Draft.Lines.Select(x=>x.LineId).ToArray()});
            return await PrincipalSave(db,scope,ev,"Create",key,fingerprint,token);
        },ct);
    }

    private static Task<IResult> PrincipalCancel(Guid id,CancelInput input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
    {
        if(!PrincipalSourceAvailable(http))return Task.FromResult(Error("PrincipalSourcingSourceUnavailable",503));
        return Run(http,scope,async(db,cases,core,auth,token)=>
        {
            var key=Key(http);var reason=Normalize(input.Reason);
            if(input.ExpectedEventVersion<1 || reason.Length is <1 or >2000
                || input.OriginalDraftFingerprint is not {Length:64})
                throw new Problem("SourcingCancellationInvalid",422);
            var actor=await PrincipalActor(core,scope,http,auth,"procurement-sourcing-cancel",null,false,token);
            var fingerprint=Fingerprint(new{schemaVersion=2,operation="Cancel",id,input.ExpectedEventVersion,
                input.OriginalDraftFingerprint,reason});
            if(await PrincipalReplay(db,core,scope,http,auth,actor,"procurement-sourcing-cancel","Cancel",key,fingerprint,id,token) is { } prior)
                return prior;
            var initial=await PrincipalOwnedEvent(db,core,scope,http,auth,actor,"procurement-sourcing-cancel",id,token);
            var selected=await db.SourcingDemandBindings.AsNoTracking().Where(x=>x.SourcingEventId==id)
                .Select(x=>x.PurchaseRequestId).Distinct().OrderBy(x=>x).ToArrayAsync(token);
            if(selected.Length is <1 or >200)throw new Problem("SourcingOriginalEvidenceUnavailable",409);
            foreach(var requestId in selected)await ProcurementApprovalExecutionLocks.LockRequestAsync(db,scope,requestId,token);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.sourcing_events WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE",token);
            await db.Entry(initial).ReloadAsync(token);
            var ev=await PrincipalOwnedEvent(db,core,scope,http,auth,actor,"procurement-sourcing-cancel",id,token);
            if(ev.Status!="Draft" || ev.Version!=input.ExpectedEventVersion
                || ev.OriginalDraftFingerprint!=input.OriginalDraftFingerprint)
                throw new Problem("SourcingDraftChanged",409);
            var evidence=await db.SourcingDraftEvidenceRecords.SingleAsync(x=>x.Id==ev.SourceEvidenceId,token);
            var bindings=await db.SourcingDemandBindings.Where(x=>x.SourcingEventId==id)
                .OrderBy(x=>x.PurchaseRequestLineId).ToArrayAsync(token);
            var lines=await db.SourcingLines.Where(x=>x.SourcingEventId==id).ToArrayAsync(token);
            if(bindings.Length is <1 or >200 || lines.Length!=bindings.Length
                || !SameRequestSet(selected,bindings.Select(x=>x.PurchaseRequestId)))
                throw new Problem("SourcingSourceSetChanged",409);
            var draft=GovernedPrincipalRfqRestoration.RestoreVerified(evidence,ev,bindings);
            var sources=new List<ApprovedPurchaseSource>();
            foreach(var binding in bindings)
            {
                var source=await ApprovedDemandSourceLoader.LoadRetainedAsync(db,cases,scope,binding.PurchaseRequestId,
                    binding.PurchaseRequestLineId,new(binding.SubmissionId,binding.ApprovalOutcomeId,binding.MethodId),token);
                _=await PrincipalActor(core,scope,http,auth,"procurement-sourcing-cancel",source.Request.BranchRef,true,token);
                var line=lines.SingleOrDefault(x=>x.Id==binding.SourcingLineId)
                    ??throw new Problem("SourcingOriginalEvidenceUnavailable",409);
                if(line.PurchaseRequestLineId!=binding.PurchaseRequestLineId || line.Quantity!=binding.Quantity
                    || line.UnitCode!=binding.UnitCode || line.CategoryCode!=binding.CategoryCode || line.Status!="Active")
                    throw new Problem("SourcingOriginalEvidenceUnavailable",409);
                sources.Add(source);
            }
            GovernedPrincipalRfqRestoration.VerifyHistorical(draft,sources);
            var plan=GovernedPrincipalRfqDraft.Cancel(actor,draft,key,input.OriginalDraftFingerprint,reason,sources);
            foreach(var effect in plan.Effects)
            {
                var source=sources.Single(x=>x.Line.Id==effect.RequestLineId);
                var allocation=source.Allocations.Single(x=>x.Id==effect.AllocationId);
                if(allocation.Version!=effect.ExpectedVersion || source.Request.Version!=effect.ExpectedRequestVersion)
                    throw new Problem("SourcingReservationChanged",409);
                allocation.Status="Released";allocation.Reason=reason;
                source.Line.AllocatedQuantity=effect.ResultingAllocatedQuantity;source.Request.UpdatedAt=DateTimeOffset.UtcNow;
            }
            ev.Status="Cancelled";ev.CancellationReason=reason;
            foreach(var line in lines){line.Status="Cancelled";line.StatusReason="Quote request cancelled; see the event reason.";}
            PrincipalRecord(db,scope,ev,"DraftCancelled",key,new{reason});
            return await PrincipalSave(db,scope,ev,"Cancel",key,fingerprint,token);
        },ct);
    }

    private static void PrincipalRecord(ProcurementDbContext db,ProcurementRequestScope scope,SourcingEvent ev,
        string action,Guid key,object fields)
    {
        db.AddScoped(new AuditEvent{RecordType="SourcingEvent",RecordId=ev.Id,Action=action,
            ActorSubjectId=scope.SubjectId,ChangedFieldsJson=JsonSerializer.Serialize(new{
                executionVersion=2,sourceEvidenceId=ev.SourceEvidenceId,
                originalDraftFingerprint=ev.OriginalDraftFingerprint,commandKey=key,changes=fields},Json),
            CorrelationId=scope.CorrelationId});
        db.AddScoped(new OutboxEvent{AggregateType="SourcingEvent",AggregateId=ev.Id,
            EventType=action=="DraftReserved"?"sourcing.draft_reserved.v2":"sourcing.draft_cancelled.v2",
            IdempotencyKey=$"rfq.{action}.{key:N}.v2",PayloadJson=JsonSerializer.Serialize(new{
                eventId=ev.Id,commandKey=key,internallyPublished=false,supplierDeliveryConfirmed=false,
                financeReservationChanged=false},Json)});
    }

    // PostgreSQL UUID ordering and .NET Guid ordering need not agree. This is
    // an identity-set check after the request rows have been locked, not an
    // ordering rule for a multi-request cancellation.
    internal static bool SameRequestSet(IReadOnlyCollection<Guid> selected,IEnumerable<Guid> boundRequestIds)
    {
        var expected=selected.ToHashSet();
        var actual=boundRequestIds.ToHashSet();
        return selected.Count==expected.Count && actual.Count==expected.Count && actual.SetEquals(expected);
    }
}
