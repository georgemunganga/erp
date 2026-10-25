using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

// Source-only, default-off successor. The registered provider remains closed
// until the genuine P36 physical catalog and finite grants are captured.
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

// Metadata availability, not record authorization. The route still verifies
// its named reader, current aliases and retained source on every request.
internal static class ProcurementRfqRecommendationCapability
{
    internal static async Task<bool> ReadyAsync(IServiceProvider services,
        ProcurementDbContext owner,ProcurementRequestScope scope,HttpContext http,
        bool pending,CancellationToken ct)
    {
        if(pending || services.GetService<ProcurementRfqCommercialOptions>() is not
            {Enabled:true,OfferOpeningEnabled:true,RecommendationDraftEnabled:true}
            || services.GetService<IProcurementRfqRecommendationDraftReadiness>() is not { } ready)
            return false;
        var person=await ProcurementPrincipalPeople.Resolve(
            services.GetRequiredService<HrmDbContext>(),scope.TenantId,
            scope.LegalEntityId,scope.SubjectId,ct);
        if(person is null || !person.Permissions.Contains(PrincipalRfqRecommendationDraft.Action)
            || !person.Permissions.Contains(PrincipalRfqOfferOpeningControls.ReadAction)
            || !(scope.WorkLocationId is { } branch
                ? person.OrganisationWide || person.AllowedBranches.Contains(branch)
                : person.OrganisationWide || person.AllowedBranches.Count>0)) return false;
        var auth=services.GetRequiredService<IAuthorizationService>();
        if(!(await auth.AuthorizeAsync(http.User,PrincipalRfqRecommendationDraft.Action)).Succeeded
            || !(await auth.AuthorizeAsync(http.User,PrincipalRfqOfferOpeningControls.ReadAction)).Succeeded)
            return false;
        // The physical providers require the same owning serializable context
        // as a real read. No schema or privilege result is cached in metadata.
        try
        {
            await using var transaction=await owner.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,ct);
            var available=await ready.IsReadyAsync(owner,false,ct);
            await transaction.RollbackAsync(ct);
            return available;
        }
        catch(Exception error) when(error is not OperationCanceledException) {return false;}
    }
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
        PrincipalRfqCommercialOfferSubmissionRecord[] Offers,RfqPrincipalDraft Original)> RecommendationSource(
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
        RequireRecommendationNamedReader(controls,actor.PrincipalId);
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
        return(round,actor,opened,seal,plan,revision,offers,retained.Original);
    }

    internal static void RequireRecommendationNamedReader(
        PrincipalRfqOfferOpeningControls controls,string principalId)
    {
        if(!controls.ReaderPrincipalIds.Contains(principalId,StringComparer.Ordinal))
            throw new Problem("RfqRecommendationReaderDenied",403);
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

    // Projection accepts only fields supplied by the verified schema-2 RFQ
    // envelope and retained offer set. It never consults mutable supplier data.
    internal static string RecommendationSourceBody(Guid roundId,Guid unsealingId,
        string verifiedTitle,IReadOnlyList<Guid> frozenLineIds,
        IReadOnlyList<PrincipalRfqCommercialOfferSubmissionRecord> offers)
    {
        var labels=frozenLineIds.Select((lineId,index)=>new
            {LineId=lineId,Label=$"Line {index+1}"})
            .ToDictionary(x=>x.LineId,x=>x.Label);
        var lines=RecommendationOfferLines(offers);
        if(lines.Any(x=>!labels.ContainsKey(x.SourcingLineId)))
            throw new Problem("RfqRecommendationSourceChanged",409);
        var items=offers.Select((offer,index)=>new
        {
            offerId=offer.Id,offerSha256=offer.SnapshotSha256,
            offerLabel=$"Offer {index+1}",supplierLabel=(string?)null,
            lines=lines.Where(x=>x.OfferId==offer.Id).Select(x=>new
            {sourceLineId=x.SourcingLineId,lineLabel=labels[x.SourcingLineId],
                offeredQuantityExact=x.OfferedQuantityExact,x.CurrencyCode})
        }).ToArray();
        return JsonSerializer.Serialize(new
        {roundId,unsealingId,rfqTitle=verifiedTitle,
            sourceLines=frozenLineIds.Select((lineId,index)=>new
            {sourceLineId=lineId,lineLabel=$"Line {index+1}"}),
            offers=items,canSubmit=false,awardAvailable=false,
            purchaseOrderAvailable=false},Json);
    }

    // Every source and offer passes current named-reader, retained receipt,
    // alias and sealed-set checks before the projection sees any record.
    private static Task<IResult> CommercialRecommendationSource(Guid id,
        HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,async(db,cases,core,auth,token)=>
        {
            await RequireRecommendation(http,db,false,token);
            if(id==Guid.Empty)throw new Problem("RfqRecommendationSourceNotFound",404);
            var (_,_,opened,_,_,_,offers,original)=await RecommendationSource(
                http,db,core,cases,auth,scope,id,false,token);
            return new Reply(RecommendationSourceBody(id,opened.Id,original.Title,
                original.Lines.Select(x=>x.EventLineId).ToArray(),offers));
        },ct);

    private static Task<IResult> CommercialSaveRecommendation(Guid id,RecommendationInput? input,
        HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        =>Run(http,scope,async(db,cases,core,auth,token)=>
        {
            await RequireRecommendation(http,db,true,token);
            if(id==Guid.Empty || input?.Lines is null || input.Lines.Length>500 || input.Lines.Any(x=>x is null)
                || input.ExpectedRevision is <0 or >=100
                || input.DraftId is null && (input.ExpectedRevision!=0 || input.ExpectedSnapshotSha256 is not null)
                || input.DraftId is not null && (input.DraftId==Guid.Empty
                    || input.ExpectedRevision==0 || input.ExpectedSnapshotSha256 is not {Length:64}))
                throw new Problem("RfqRecommendationInputInvalid",422);
            // Replay is a current purpose read too: no receipt lookup or response
            // precedes named-reader, retained opening and current alias admission.
            var (round,actor,opened,seal,plan,planRevision,offers,_)=await RecommendationSource(
                http,db,core,cases,auth,scope,id,true,token);
            var key=Key(http);
            var fingerprint=Fingerprint(new{operation="SaveRecommendationDraft",roundId=id,
                input.DraftId,input.ExpectedRevision,input.ExpectedSnapshotSha256,input.Lines});
            await LockPrincipalSourcingCommandKey(db,key,token);
            var prior=await db.PrincipalRfqRecommendationDraftReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.ActorPrincipalId==actor.PrincipalId && x.CommandKey==key
                    &&x.TenantId==scope.TenantId &&x.LegalEntityId==scope.LegalEntityId,token);
            if(prior is not null)
            {
                if(prior.RoundId!=id || prior.RequestSha256!=fingerprint)
                    throw new Problem("RfqRecommendationCommandConflict",409);
                var priorRevision=await db.PrincipalRfqRecommendationDraftRevisions.AsNoTracking()
                    .SingleAsync(x=>x.Id==prior.RevisionId,token);
                var priorHead=await db.PrincipalRfqRecommendationDrafts.AsNoTracking()
                    .SingleAsync(x=>x.Id==prior.DraftId &&x.MakerPrincipalId==actor.PrincipalId,token);
                _=await VerifiedRecommendation(db,priorHead,token);
                if(prior.ResponseJson!=PrincipalRfqRecommendationDraft.Response(priorHead,priorRevision)
                    ||prior.ResponseSha256!=ProcurementEvidenceEncoding.Digest(prior.ResponseJson))
                    throw new Problem("RfqRecommendationReceiptChanged",409);
                return new Reply(prior.ResponseJson,201);
            }
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
            if(input.DraftId is not null)
                _=await VerifiedRecommendation(db,head,token);
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
                DraftId=head.Id,RevisionId=revision.Id,RoundId=id,UnsealingId=opened.Id,
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
        if(lines.Any(x=>x.DraftId!=head.Id ||!history.Any(r=>r.Id==x.RevisionId)
            ||x.RoundId!=head.RoundId ||x.UnsealingId!=head.UnsealingId
            ||x.SourcingEventId!=head.SourcingEventId ||x.BranchId!=head.BranchId))
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
            if(ack is null || ack.UnsealingId!=head.UnsealingId
                || ack.RoundId!=head.RoundId || ack.SourcingEventId!=head.SourcingEventId
                || ack.BranchId!=head.BranchId || ack.ActorPrincipalId!=revision.ActorPrincipalId
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
            var (round,actor,opened,seal,plan,planRevision,_,_)=await RecommendationSource(
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
            var (round,actor,opened,seal,plan,planRevision,_,_)=await RecommendationSource(
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
            var (round,actor,_,_,_,_,_,_)=await RecommendationSource(
                http,db,core,cases,auth,scope,id,false,token);
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
