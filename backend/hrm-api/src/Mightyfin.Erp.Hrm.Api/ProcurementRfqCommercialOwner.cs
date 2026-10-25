using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

// No MapRoutes, service registration or positive readiness provider in this slice.
public sealed class ProcurementRfqCommercialOptions
{
    public bool Enabled { get; set; }
    public bool OfferOpeningEnabled { get; set; }
    public bool CriterionAssessmentEnabled { get; set; }
    public bool RecommendationDraftEnabled { get; set; }
}
public interface IProcurementRfqCommercialReadiness
{
    Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct);
}
public sealed class UnavailableProcurementRfqCommercialReadiness:IProcurementRfqCommercialReadiness
{public Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct)=>Task.FromResult(false);}

public static partial class ProcurementSourcingExecutionRoutes
{
    private sealed record CommercialSource(RfqPrincipalActor Actor,SourcingEvent Event,SourcingDraftEvidence Evidence,
        IReadOnlyList<SourcingDemandBinding> Bindings,RfqPrincipalDraft Original,IReadOnlyList<ApprovedPurchaseSource> Sources);
    private static async Task RequireCommercial(HttpContext http,ProcurementDbContext db,bool write,CancellationToken ct)
    {
        var options=http.RequestServices.GetService<ProcurementRfqCommercialOptions>();
        var ready=http.RequestServices.GetService<IProcurementRfqCommercialReadiness>();
        if(options is not{Enabled:true}||ready is null||!await ready.IsReadyAsync(db,write,ct))
            throw new Problem("RfqCommercialUnavailable",503);
    }

    // Document-writing commands use the existing sourcing/document coordinator
    // directly. They still require the same P29/P30 owner and Finance-checker
    // prerequisites that Run enforces for non-document commands.
    private static async Task RequireCommercialDocumentCommandFoundation(
        HttpContext http, ProcurementDbContext? db, CancellationToken ct)
    {
        var options = http.RequestServices.GetRequiredService<ProcurementSourcingDraftExecutionOptions>();
        var readiness = http.RequestServices
            .GetRequiredService<IProcurementSourcingDraftSchemaReadiness>();
        if (!options.Enabled || !options.ExecutionDatabaseConfigured
            || db is null && !await readiness.IsReadyAsync(ct)
            || db is not null && !await readiness.IsReadyAsync(db, true, ct))
            throw new Problem("SourcingDraftExecutionUnavailable", 503);
        if (db is not null && !await ProcurementFinanceCheckerSchemaGuard.ReadyAsync(
                db, http.RequestServices, false, ct))
            throw new Problem("SourcingFinanceCheckerSchemaUnavailable", 503);
    }
    private static void RequireCommercialTransaction(ProcurementDbContext db,HrmDbContext core)
    {
        if(!db.Database.IsNpgsql()||db.Database.CurrentTransaction is null||core.Database.CurrentTransaction is null
            ||!ReferenceEquals(db.Database.GetDbConnection(),core.Database.GetDbConnection())
            ||!ReferenceEquals(db.Database.CurrentTransaction.GetDbTransaction(),core.Database.CurrentTransaction.GetDbTransaction()))
            throw new InvalidOperationException("RfqCommercialOwningCoreTransactionRequired");
    }
    private static async Task<CommercialSource> LoadCommercialSource(ProcurementDbContext db,ApprovalDbContext cases,HrmDbContext core,
        ProcurementRequestScope scope,HttpContext http,IAuthorizationService auth,Guid id,bool locks,CancellationToken ct,
        string permission = PrincipalRfqCommercialDraft.Permission)
    {
        RequireCommercialTransaction(db,core);
        var actor=await PrincipalActor(core,scope,http,auth,permission,null,false,ct);
        // Authorize exact current branch/owner before discovering source IDs/rows.
        var initial=await PrincipalOwned(db,actor).AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)
            ??throw new Problem("SourcingNotFound",404);
        actor=await PrincipalActor(core,scope,http,auth,permission,initial.BranchRef,true,ct);
        if(locks)
        {
            var ids=await db.SourcingDemandBindings.AsNoTracking().Where(x=>x.SourcingEventId==id)
                .Select(x=>x.PurchaseRequestId).Distinct().OrderBy(x=>x).ToArrayAsync(ct);
            foreach(var request in ids)await ProcurementApprovalExecutionLocks.LockRequestAsync(db,scope,request,ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.sourcing_events WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={id} FOR UPDATE",ct);
        }
        var ev=await PrincipalOwnedEvent(db,core,scope,http,auth,actor,permission,id,ct);
        actor=await PrincipalActor(core,scope,http,auth,permission,ev.BranchRef,true,ct);
        var evidence=await db.SourcingDraftEvidenceRecords.SingleAsync(x=>x.Id==ev.SourceEvidenceId,ct);
        var bindings=await db.SourcingDemandBindings.Where(x=>x.SourcingEventId==id).OrderBy(x=>x.SourcingLineId).ToArrayAsync(ct);
        var original=GovernedPrincipalRfqRestoration.RestoreVerified(evidence,ev,bindings);
        var targetScope=PrincipalRfqTargetBranchScope.Bind(scope,actor,ev.BranchRef!.Value);
        var sources=new List<ApprovedPurchaseSource>();
        foreach(var pin in original.Lines.OrderBy(x=>x.RequestId).ThenBy(x=>x.LineId))
        {
            var source=await ApprovedDemandSourceLoader.LoadRetainedAsync(db,cases,targetScope,pin.RequestId,pin.LineId,new(pin.SubmissionId,pin.OutcomeId,pin.MethodId),ct);
            _=await PrincipalActor(core,scope,http,auth,permission,source.Request.BranchRef,true,ct);
            _=PurchaseDemandAllocationBalance.Evaluate(source.Request,source.Line,source.Allocations,source.AllocationOwners??[],scope.TenantId,scope.LegalEntityId);
            var hold=source.Allocations.SingleOrDefault(x=>x.Id==pin.AllocationId&&x.TargetType=="SourcingEvent"&&x.TargetId==id);
            if(hold is null||(source.AllocationOwners??[]).Count(x=>x.TargetType=="SourcingEvent"&&x.TargetId==id&&x.TargetLineId==pin.EventLineId&&x.PurchaseRequestLineId==pin.LineId)!=1
                ||hold.Quantity!=pin.Quantity||hold.EstimatedValue!=pin.EstimatedValue
                ||locks&&hold.Status!="Reserved")throw new Problem("RfqCommercialSourceHoldChanged",409);
            sources.Add(source);
        }
        // Historical original proof, not today's catalog/method/supplier eligibility.
        GovernedPrincipalRfqRestoration.VerifyHistorical(original,sources);
        return new(actor,ev,evidence,bindings,original,sources.AsReadOnly());
    }

    private static async Task<IReadOnlyList<RfqPrincipalActor>> ResolveCommercialEvaluators(CommercialSource source,
        PrincipalRfqEvaluationPlanDefinition definition,HrmDbContext core,ProcurementRequestScope scope,CancellationToken ct)
    {
        var result=new List<RfqPrincipalActor>();
        var participants=source.Original.Lines.SelectMany(x=>new[]{(x.MakerPrincipalId,x.MakerWorkerAlias),(x.RequesterPrincipalId,x.RequesterWorkerAlias)})
            .Append((source.Original.MakerPrincipalId,source.Original.MakerWorkerAlias)).Distinct().ToArray();
        var names=definition.Criteria.SelectMany(x=>x.EvaluatorPrincipalIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if(names.Length is <1 or >100)throw new Problem("RfqCommercialEvaluatorSetBound",409);
        var allIds=participants.Select(x=>Guid.ParseExact(x.Item1,"D")).Concat(names.Select(x=>Guid.ParseExact(x,"D"))).Distinct().ToArray();
        // One bounded owning Core query, including archived historical makers for
        // denial-only aliases. Explicit tenant confinement survives IgnoreFilters.
        var aliases=await core.LocalUsers.IgnoreQueryFilters().AsNoTracking().Where(x=>x.TenantId==scope.TenantId&&allIds.Contains(x.Id))
            .Select(x=>new{x.Id,x.WorkerId}).ToDictionaryAsync(x=>x.Id,x=>x.WorkerId,ct);
        foreach(var principal in names)
        {
            var person=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,principal,ct);
            if(person is null||!person.Permissions.Contains(PrincipalRfqPrepublicationPlan.EvaluatorPermission)
                ||!person.OrganisationWide&&!person.AllowedBranches.Contains(source.Event.BranchRef!.Value))
                throw new Problem("RfqCommercialEvaluatorUnavailable",409);
            // IndependentNow reads CURRENT account aliases for BOTH sides, including
            // historical/offboarded makers. A retained null is never a bypass.
            foreach(var (maker,alias) in participants)
                if(!ProcurementPrincipalPeople.IndependentWithCurrentAliases(maker,alias,person.PrincipalId,person.CoreLinkedWorkerId,aliases))
                    throw new Problem("RfqCommercialEvaluatorNotIndependent",409);
            foreach(var other in result)
                if(!ProcurementPrincipalPeople.IndependentWithCurrentAliases(other.PrincipalId,other.LinkedWorkerId,person.PrincipalId,person.CoreLinkedWorkerId,aliases))
                    throw new Problem("RfqCommercialEvaluatorAliasConflict",409);
            result.Add(new(person.PrincipalId,person.CoreLinkedWorkerId,scope.TenantId,scope.LegalEntityId,scope.WorkLocationId,
                person.AllowedBranches,person.OrganisationWide,person.Permissions));
        }
        return result.AsReadOnly();
    }

    private sealed record CommercialSaveInput(long? ExpectedRevision,PrincipalRfqCommercialDraftInput Draft);
    private static Task<IResult> CommercialSave(Guid id,CommercialSaveInput input,HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,async(db,cases,core,auth,token)=>
        {
            await RequireCommercial(http,db,true,token);
            if(input is null||input.Draft is null||id==Guid.Empty||input.ExpectedRevision is <1)throw new Problem("RfqCommercialInputRequired",422);
            var key=Key(http);await LockPrincipalSourcingCommandKey(db,key,token);
            var actor=await PrincipalActor(core,scope,http,auth,PrincipalRfqCommercialDraft.Permission,null,false,token);
            _=await PrincipalOwnedEvent(db,core,scope,http,auth,actor,PrincipalRfqCommercialDraft.Permission,id,token);
            var operation=input.ExpectedRevision is null?"Create":"Revise";
            var fingerprint=Fingerprint(new{operation,eventId=id,input.ExpectedRevision,input.Draft});
            var store=new PrincipalRfqCommercialDraftStore(db,scope);
            if(await store.RecoverAsync(id,key,operation,fingerprint,token) is {} original)return new Reply(original.ResponseJson,original.ResponseStatus);
            var source=await LoadCommercialSource(db,cases,core,scope,http,auth,id,true,token);
            PrincipalRfqCommercialDraftReceipt receipt;
            if(input.ExpectedRevision is null)receipt=store.Create(source.Actor,key,fingerprint,source.Event,source.Evidence,source.Bindings,input.Draft);
            else
            {
                var current=await store.LoadAsync(id,token);
                receipt=store.Revise(source.Actor,key,fingerprint,current.Head,current.Revision,input.ExpectedRevision.Value,
                    source.Event,source.Evidence,source.Bindings,input.Draft);
            }
            // Original source counters/event remain untouched. Same actual owner
            // transaction commits Draft + revision + receipt + audit + outbox only.
            await db.SaveChangesAsync(token);return new Reply(receipt.ResponseJson,receipt.ResponseStatus);
        },ct);
}
