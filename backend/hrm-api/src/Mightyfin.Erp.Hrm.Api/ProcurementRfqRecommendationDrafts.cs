using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

// Source-only, default-off successor. The P36 owner schema and finite grants
// must be captured before this readiness interface can have a positive provider.
public interface IProcurementRfqRecommendationDraftReadiness
{
    Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct);
}
public sealed class UnavailableProcurementRfqRecommendationDraftReadiness
    : IProcurementRfqRecommendationDraftReadiness
{
    public Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct)
        =>Task.FromResult(false);
}

public static partial class ProcurementSourcingExecutionRoutes
{
    private sealed record RecommendationInput(Guid? DraftId,int ExpectedRevision,
        string? ExpectedSnapshotSha256,PrincipalRfqRecommendationDraft.Selection[]? Lines);

    private static async Task RequireRecommendation(HttpContext http,ProcurementDbContext db,
        bool write,CancellationToken ct)
    {
        if(http.RequestServices.GetService<ProcurementRfqCommercialOptions>() is not
            {Enabled:true,OfferOpeningEnabled:true,RecommendationDraftEnabled:true})
            throw new Problem("RfqRecommendationUnavailable",503);
        await RequireOfferOpening(http,db,false,ct);
        if(http.RequestServices.GetService<IProcurementRfqRecommendationDraftReadiness>() is not { } ready
            || !await ready.IsReadyAsync(db,write,ct))
            throw new Problem("RfqRecommendationUnavailable",503);
    }

    private static async Task<(PrincipalRfqCommercialRoundOpeningRecord Round,RfqPrincipalActor Actor,
        PrincipalRfqOfferOpeningRecord Opened,PrincipalRfqCommercialRoundSealRecord Seal,
        PrincipalRfqPreparedPlan Plan,PrincipalRfqCommercialDraftRevision PlanRevision,
        PrincipalRfqCommercialOfferSubmissionRecord[] Offers)> RecommendationSource(
        HttpContext http,ProcurementDbContext db,HrmDbContext core,
        Mightyfin.Erp.Core.Approvals.Infrastructure.ApprovalDbContext cases,
        Microsoft.AspNetCore.Authorization.IAuthorizationService auth,
        ProcurementRequestScope scope,Guid roundId,bool write,CancellationToken ct)
    {
        var (round,actor)=await OfferOpeningTarget(db,core,scope,http,auth,roundId,
            PrincipalRfqRecommendationDraft.Action,ct);
        // The new Draft grant alone cannot reveal offer prices. Both the
        // current purpose action and the frozen named-reader policy are needed.
        _=await PrincipalActor(core,scope,http,auth,
            PrincipalRfqOfferOpeningControls.ReadAction,round.BranchId,true,ct);
        if(write)await LockOfferOpeningSources(db,scope,round,ct);
        var retained=await LoadRetainedCommercialApproval(db,cases,scope,round,ct);
        await RequireCommercialSourceHolds(db,cases,scope,retained.Event,retained.Original,actor,ct);
        var (_,controls)=await OpeningPolicy(db,scope,round,ct,currentEffects:false);
        if(!controls.ReaderPrincipalIds.Contains(actor.PrincipalId,StringComparer.Ordinal))
            throw new Problem("RfqRecommendationReaderDenied",403);
        var opened=await db.PrincipalRfqOfferOpenings.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.OpeningId==roundId,ct)
            ??throw new Problem("RfqOffersNotOpened",409);
        var seal=await db.PrincipalRfqCommercialRoundSeals.AsNoTracking()
            .SingleAsync(x=>x.Id==opened.SealId && x.OpeningId==roundId,ct);
        await RequireRetainedOpenedOfferPurpose(db,core,scope,round,opened,seal,
            retained,actor,controls,true,ct);
        var (definition,plan,revision)=await AssessmentPlan(db,round,ct);
        if(definition.RulesSha256!=plan.EvaluationRulesSha256)
            throw new Problem("RfqRecommendationPlanChanged",409);
        var offers=await RetainedOpeningOffers(http,db,scope,round,seal,actor,
            PrincipalRfqOfferOpeningControls.ReadAction,ct);
        if(PrincipalRfqSealedManualOffers.RetainedSetDigest(retained.Proof,
                offers.Select(OfferProof).ToArray())!=opened.OfferSetSha256)
            throw new Problem("RfqRecommendationOfferSetChanged",409);
        return(round,actor,opened,seal,plan,revision,offers);
    }

    private static PrincipalRfqRecommendationDraft.OfferLine[] RecommendationOfferLines(
        IEnumerable<PrincipalRfqCommercialOfferSubmissionRecord> offers)
    {
        var rows=new List<PrincipalRfqRecommendationDraft.OfferLine>();
        foreach(var offer in offers)
        {
            using var json=JsonDocument.Parse(offer.SnapshotJson);
            var currency=json.RootElement.GetProperty("currencyCode").GetString()
                ??throw new Problem("RfqRecommendationOfferCurrencyChanged",409);
            foreach(var line in json.RootElement.GetProperty("lines").EnumerateArray())
                rows.Add(new(offer.Id,offer.SnapshotSha256,offer.SupplierId,
                    line.GetProperty("SourcingLineId").GetGuid(),
                    line.GetProperty("quantityExact").GetString()!,currency));
        }
        return rows.ToArray();
    }

    private static Task<IResult> CommercialSaveRecommendation(Guid id,RecommendationInput? input,
        HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,async(db,cases,core,auth,token)=>
        {
            await RequireRecommendation(http,db,true,token);
            if(id==Guid.Empty || input?.Lines is null || input.Lines.Length>500
                || input.ExpectedRevision is <0 or >=100
                || input.DraftId is null && (input.ExpectedRevision!=0 || input.ExpectedSnapshotSha256 is not null)
                || input.DraftId is not null && (input.DraftId==Guid.Empty
                    || input.ExpectedRevision==0 || input.ExpectedSnapshotSha256 is not {Length:64}))
                throw new Problem("RfqRecommendationInputInvalid",422);
            var (authorizedRound,authorizedActor)=await OfferOpeningTarget(db,core,scope,http,auth,
                id,PrincipalRfqRecommendationDraft.Action,token);
            _=await PrincipalActor(core,scope,http,auth,
                PrincipalRfqOfferOpeningControls.ReadAction,authorizedRound.BranchId,true,token);
            var key=Key(http);
            var fingerprint=Fingerprint(new{operation="SaveRecommendationDraft",roundId=id,
                input.DraftId,input.ExpectedRevision,input.ExpectedSnapshotSha256,input.Lines});
            await LockPrincipalSourcingCommandKey(db,key,token);
            var prior=await db.PrincipalRfqRecommendationDraftReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.ActorPrincipalId==authorizedActor.PrincipalId && x.CommandKey==key
                    &&x.TenantId==scope.TenantId &&x.LegalEntityId==scope.LegalEntityId,token);
            if(prior is not null)
            {
                if(prior.RoundId!=id || prior.RequestSha256!=fingerprint)
                    throw new Problem("RfqRecommendationCommandConflict",409);
                var priorRevision=await db.PrincipalRfqRecommendationDraftRevisions.AsNoTracking()
                    .SingleAsync(x=>x.Id==prior.RevisionId,token);
                var priorHead=await db.PrincipalRfqRecommendationDrafts.AsNoTracking()
                    .SingleAsync(x=>x.Id==prior.DraftId &&x.MakerPrincipalId==authorizedActor.PrincipalId,token);
                _=await VerifiedRecommendation(db,priorHead,token);
                if(prior.ResponseJson!=PrincipalRfqRecommendationDraft.Response(priorHead,priorRevision)
                    ||prior.ResponseSha256!=ProcurementEvidenceEncoding.Digest(prior.ResponseJson))
                    throw new Problem("RfqRecommendationReceiptChanged",409);
                return new Reply(prior.ResponseJson,201);
            }
            var (round,actor,opened,seal,plan,planRevision,offers)=await RecommendationSource(
                http,db,core,cases,auth,scope,id,true,token);
            var currency=await db.SourcingEvents.AsNoTracking().Where(x=>
                x.Id==round.SourcingEventId &&x.BranchRef==round.BranchId
                &&x.TenantId==scope.TenantId &&x.LegalEntityId==scope.LegalEntityId)
                .Select(x=>x.CurrencyCode).SingleAsync(token);
            if(offers.Select(x=>OfferCurrency(x.SnapshotJson)).Any(x=>x!=currency))
                throw new Problem("RfqRecommendationCurrencyChanged",409);
            var sourceLines=await db.SourcingLines.AsNoTracking()
                .Where(x=>x.SourcingEventId==round.SourcingEventId && x.Status=="Active"
                    &&x.TenantId==scope.TenantId &&x.LegalEntityId==scope.LegalEntityId)
                .Select(x=>new{x.Id,x.Quantity}).ToArrayAsync(token);
            var proposed=PrincipalRfqRecommendationDraft.Validate(input.Lines,
                RecommendationOfferLines(offers),sourceLines.Select(x=>
                    new PrincipalRfqRecommendationDraft.SourceLine(x.Id,
                        x.Quantity.ToString("0.############################",CultureInfo.InvariantCulture))).ToArray(),
                currency);
            if(input.DraftId is null &&await db.PrincipalRfqRecommendationDrafts.AsNoTracking()
                .AnyAsync(x=>x.RoundId==id &&x.MakerPrincipalId==actor.PrincipalId
                    &&x.TenantId==scope.TenantId &&x.LegalEntityId==scope.LegalEntityId,token))
                throw new Problem("RfqRecommendationDraftAlreadyExists",409);
            var head=input.DraftId is null?new PrincipalRfqRecommendationDraftHead
            {
                TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
                RoundId=id,UnsealingId=opened.Id,SealId=seal.Id,SourcingEventId=round.SourcingEventId,
                BranchId=round.BranchId,PreparedPlanId=plan.Id,ApprovedRoundSha256=round.SnapshotSha256,
                OpeningSha256=opened.SnapshotSha256,OfferSetSha256=opened.OfferSetSha256,
                EvaluationPlanRevisionId=planRevision.Id,
                EvaluationDefinitionSha256=plan.EvaluationRulesSha256,
                MakerPrincipalId=actor.PrincipalId,MakerWorkerAliasId=actor.LinkedWorkerId,
            }:(await db.PrincipalRfqRecommendationDrafts.SingleOrDefaultAsync(x=>x.Id==input.DraftId
                && x.RoundId==id && x.TenantId==scope.TenantId && x.LegalEntityId==scope.LegalEntityId
                && x.BranchId==round.BranchId && x.MakerPrincipalId==actor.PrincipalId,token)
                ??throw new Problem("RfqRecommendationDraftNotFound",404));
            if(head.CurrentRevision!=input.ExpectedRevision
                ||input.DraftId is not null && head.CurrentSnapshotSha256!=input.ExpectedSnapshotSha256
                ||head.ApprovedRoundSha256!=round.SnapshotSha256 ||head.OpeningSha256!=opened.SnapshotSha256
                ||head.OfferSetSha256!=seal.SubmissionSetSha256 ||head.EvaluationPlanRevisionId!=planRevision.Id)
                throw new Problem("RfqRecommendationDraftChanged",409);
            var previousId=input.DraftId is null?(Guid?)null:head.CurrentRevisionId;
            var previousSha=input.DraftId is null?null:head.CurrentSnapshotSha256;
            var revision=new PrincipalRfqRecommendationDraftRevision
            {
                TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
                DraftId=head.Id,RoundId=id,UnsealingId=opened.Id,SourcingEventId=round.SourcingEventId,
                BranchId=round.BranchId,Revision=input.ExpectedRevision+1,
                PreviousRevisionId=previousId,PreviousSnapshotSha256=previousSha,
                ActorPrincipalId=actor.PrincipalId,ActorWorkerAliasId=actor.LinkedWorkerId,
                CommandKey=key,RequestSha256=fingerprint,
                RecordedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value,
            };
            revision.SnapshotJson=PrincipalRfqRecommendationDraft.Snapshot(head,revision,proposed);
            revision.SnapshotSha256=ProcurementEvidenceEncoding.Digest(revision.SnapshotJson);
            head.CurrentRevision=revision.Revision;head.CurrentRevisionId=revision.Id;
            head.CurrentSnapshotSha256=revision.SnapshotSha256;
            if(input.DraftId is null)db.AddScoped(head);
            foreach(var value in proposed)db.AddScoped(new PrincipalRfqRecommendationDraftLine
            {
                TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
                DraftId=head.Id,RevisionId=revision.Id,RoundId=id,UnsealingId=opened.Id,
                SourcingEventId=round.SourcingEventId,BranchId=round.BranchId,
                OfferId=value.OfferId,OfferSha256=value.OfferSha256,SupplierId=value.SupplierId,
                SourcingLineId=value.SourcingLineId,QuantityExact=value.QuantityExact,
                ProposedValueExact=value.ProposedValueExact,CurrencyCode=value.CurrencyCode,Rationale=value.Rationale,
            });
            db.AddScoped(revision);
            var response=PrincipalRfqRecommendationDraft.Response(head,revision);
            db.AddScoped(new PrincipalRfqRecommendationDraftReceipt
            {
                TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
                DraftId=head.Id,RevisionId=revision.Id,RoundId=id,
                SourcingEventId=round.SourcingEventId,BranchId=round.BranchId,
                ActorPrincipalId=actor.PrincipalId,CommandKey=key,RequestSha256=fingerprint,
                ResponseJson=response,ResponseSha256=ProcurementEvidenceEncoding.Digest(response),
            });
            db.AddScoped(new AuditEvent{RecordType=nameof(PrincipalRfqRecommendationDraftRevision),
                RecordId=revision.Id,Action="RecordRecommendationDraft",ActorSubjectId=actor.PrincipalId,
                CorrelationId=scope.CorrelationId,
                ChangedFieldsJson=PrincipalRfqRecommendationDraft.AuditPayload(revision)});
            db.AddScoped(new OutboxEvent{EventType=PrincipalRfqRecommendationDraft.EventType,
                AggregateType=nameof(PrincipalRfqRecommendationDraftRevision),AggregateId=revision.Id,
                IdempotencyKey=key.ToString("D"),
                PayloadJson=PrincipalRfqRecommendationDraft.EventPayload(revision)});
            await db.SaveChangesAsync(token);
            return new Reply(response,201);
        },ct);

    private static string? OfferCurrency(string snapshot)
    {
        using var json=JsonDocument.Parse(snapshot);
        return json.RootElement.GetProperty("currencyCode").GetString();
    }

    private static async Task<(PrincipalRfqRecommendationDraftRevision Current,
        PrincipalRfqRecommendationDraftRevision[] History,
        PrincipalRfqRecommendationDraftLine[] CurrentLines)> VerifiedRecommendation(
        ProcurementDbContext db,PrincipalRfqRecommendationDraftHead head,CancellationToken ct)
    {
        var history=await db.PrincipalRfqRecommendationDraftRevisions.AsNoTracking()
            .Where(x=>x.DraftId==head.Id).OrderBy(x=>x.Revision).ToArrayAsync(ct);
        if(history.Length==0 || history.Length>100)
            throw new Problem("RfqRecommendationHistoryChanged",409);
        var current=history[^1];
        PrincipalRfqRecommendationDraft.VerifyChain(head,current,history);
        var lines=await db.PrincipalRfqRecommendationDraftLines.AsNoTracking()
            .Where(x=>x.DraftId==head.Id).ToArrayAsync(ct);
        var receipts=await db.PrincipalRfqRecommendationDraftReceipts.AsNoTracking()
            .Where(x=>x.DraftId==head.Id).ToArrayAsync(ct);
        if(receipts.Length!=history.Length || lines.Length>500*history.Length)
            throw new Problem("RfqRecommendationHistoryChanged",409);
        foreach(var revision in history)
        {
            var proposed=lines.Where(x=>x.RevisionId==revision.Id)
                .OrderBy(x=>x.SourcingLineId).ThenBy(x=>x.OfferId)
                .Select(x=>new PrincipalRfqRecommendationDraft.Proposed(x.OfferId,x.OfferSha256,
                    x.SupplierId,x.SourcingLineId,x.QuantityExact,x.ProposedValueExact,
                    x.CurrencyCode,x.Rationale)).ToArray();
            if(revision.SnapshotJson!=PrincipalRfqRecommendationDraft.Snapshot(head,revision,proposed))
                throw new Problem("RfqRecommendationHistoryChanged",409);
            var ack=receipts.SingleOrDefault(x=>x.RevisionId==revision.Id);
            if(ack is null || ack.ActorPrincipalId!=revision.ActorPrincipalId
                ||ack.CommandKey!=revision.CommandKey ||ack.RequestSha256!=revision.RequestSha256
                ||ack.ResponseJson!=PrincipalRfqRecommendationDraft.Response(head,revision)
                ||ack.ResponseSha256!=ProcurementEvidenceEncoding.Digest(ack.ResponseJson))
                throw new Problem("RfqRecommendationReceiptChanged",409);
        }
        return(current,history,lines.Where(x=>x.RevisionId==current.Id).ToArray());
    }

    private static Task<IResult> CommercialOwnRecommendations(Guid id,int? offset,int? limit,
        HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,async(db,cases,core,auth,token)=>
        {
            await RequireRecommendation(http,db,false,token);
            if(id==Guid.Empty ||offset is <0 or >100000 ||limit is <1 or >25)
                throw new Problem("RfqRecommendationPageInvalid",422);
            var (round,actor,opened,seal,plan,planRevision,_)=await RecommendationSource(
                http,db,core,cases,auth,scope,id,false,token);
            var own=db.PrincipalRfqRecommendationDrafts.AsNoTracking().Where(x=>
                x.RoundId==id &&x.TenantId==scope.TenantId &&x.LegalEntityId==scope.LegalEntityId
                &&x.BranchId==round.BranchId &&x.MakerPrincipalId==actor.PrincipalId);
            var total=await own.CountAsync(token);
            var heads=await own.OrderBy(x=>x.Id).Skip(offset??0).Take(limit??25).ToArrayAsync(token);
            var results=new List<object>();
            foreach(var head in heads)
            {
                if(head.UnsealingId!=opened.Id ||head.SealId!=seal.Id
                    ||head.PreparedPlanId!=plan.Id ||head.EvaluationPlanRevisionId!=planRevision.Id
                    ||head.ApprovedRoundSha256!=round.SnapshotSha256
                    ||head.OpeningSha256!=opened.SnapshotSha256
                    ||head.OfferSetSha256!=seal.SubmissionSetSha256)
                    throw new Problem("RfqRecommendationSourceChanged",409);
                var (current,_,_)=await VerifiedRecommendation(db,head,token);
                results.Add(new{draftId=head.Id,roundId=id,revision=current.Revision,
                    revisionId=current.Id,current.SnapshotSha256,state="Draft",
                    assessmentCoverageComplete=(bool?)null,canSubmit=false,
                    awardCreatedByThisCommand=false,purchaseOrderCreatedByThisCommand=false});
            }
            return new Reply(JsonSerializer.Serialize(new{items=results,total,offset=offset??0,
                limit=limit??25},Json));
        },ct);

    private static Task<IResult> CommercialRecommendationDetail(Guid id,Guid draftId,
        HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,async(db,cases,core,auth,token)=>
        {
            await RequireRecommendation(http,db,false,token);
            var (round,actor,opened,seal,plan,planRevision,_)=await RecommendationSource(
                http,db,core,cases,auth,scope,id,false,token);
            var head=await db.PrincipalRfqRecommendationDrafts.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.Id==draftId &&x.RoundId==id
                    &&x.TenantId==scope.TenantId &&x.LegalEntityId==scope.LegalEntityId
                    &&x.BranchId==round.BranchId &&x.MakerPrincipalId==actor.PrincipalId,token)
                ??throw new Problem("RfqRecommendationDraftNotFound",404);
            if(head.UnsealingId!=opened.Id ||head.SealId!=seal.Id
                ||head.PreparedPlanId!=plan.Id ||head.EvaluationPlanRevisionId!=planRevision.Id
                ||head.ApprovedRoundSha256!=round.SnapshotSha256
                ||head.OpeningSha256!=opened.SnapshotSha256
                ||head.OfferSetSha256!=seal.SubmissionSetSha256)
                throw new Problem("RfqRecommendationSourceChanged",409);
            var (current,history,lines)=await VerifiedRecommendation(db,head,token);
            return new Reply(JsonSerializer.Serialize(new{draftId=head.Id,roundId=id,
                state="Draft",currentRevision=current.Revision,currentRevisionId=current.Id,
                currentSnapshotSha256=current.SnapshotSha256,
                lines=lines.Select(x=>new{x.OfferId,x.SupplierId,x.SourcingLineId,
                    x.QuantityExact,x.ProposedValueExact,x.CurrencyCode,x.Rationale}),
                revisions=history.Select(x=>new{x.Id,x.Revision,x.RecordedAt,x.SnapshotSha256}),
                assessmentCoverageComplete=(bool?)null,canSubmit=false,
                awardCreatedByThisCommand=false,purchaseOrderCreatedByThisCommand=false},Json));
        },ct);

    private static Task<IResult> CommercialRecommendationRecovery(Guid id,Guid key,
        HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,async(db,cases,core,auth,token)=>
        {
            await RequireRecommendation(http,db,false,token);
            var (round,actor)=await OfferOpeningTarget(db,core,scope,http,auth,
                id,PrincipalRfqRecommendationDraft.Action,token);
            _=await PrincipalActor(core,scope,http,auth,
                PrincipalRfqOfferOpeningControls.ReadAction,round.BranchId,true,token);
            var ack=await db.PrincipalRfqRecommendationDraftReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.RoundId==id &&x.BranchId==round.BranchId
                    &&x.TenantId==scope.TenantId &&x.LegalEntityId==scope.LegalEntityId
                    &&x.ActorPrincipalId==actor.PrincipalId &&x.CommandKey==key,token)
                ??throw new Problem("RfqRecommendationCommandNotFound",404);
            var head=await db.PrincipalRfqRecommendationDrafts.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.Id==ack.DraftId &&x.MakerPrincipalId==actor.PrincipalId,token)
                ??throw new Problem("RfqRecommendationCommandNotFound",404);
            if(head.RoundId!=id ||head.BranchId!=round.BranchId)
                throw new Problem("RfqRecommendationCommandNotFound",404);
            _=await VerifiedRecommendation(db,head,token);
            return new Reply(ack.ResponseJson,201);
        },ct);
}
