using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Source-only admission until the genuine owner migration and exact
/// finite-role capture are installed. No positive provider is registered here.</summary>
public interface IProcurementRfqCriterionAssessmentReadiness
{
    Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct);
}
public sealed class UnavailableProcurementRfqCriterionAssessmentReadiness:IProcurementRfqCriterionAssessmentReadiness
{
    public Task<bool> IsReadyAsync(ProcurementDbContext owner,bool write,CancellationToken ct)=>Task.FromResult(false);
}

public static partial class ProcurementSourcingExecutionRoutes
{
    private sealed record CriterionAssessmentInput(Guid OfferId,string CriterionCode,int ExpectedPreviousRevision,
        string? ScoreExact,bool? Passed,string? Rationale);

    private static async Task RequireCriterionAssessment(HttpContext http,ProcurementDbContext db,
        bool write,CancellationToken ct)
    {
        if(http.RequestServices.GetService<ProcurementRfqCommercialOptions>() is not
            {Enabled:true,OfferOpeningEnabled:true,CriterionAssessmentEnabled:true})
            throw new Problem("RfqCriterionAssessmentUnavailable",503);
        await RequireOfferOpening(http,db,false,ct);
        if(http.RequestServices.GetService<IProcurementRfqCriterionAssessmentReadiness>() is not { } ready
            || !await ready.IsReadyAsync(db,write,ct))
            throw new Problem("RfqCriterionAssessmentUnavailable",503);
    }

    private static async Task<(PrincipalRfqEvaluationPlanDefinition Definition,
        PrincipalRfqPreparedPlan Plan,PrincipalRfqCommercialDraftRevision Revision)> AssessmentPlan(
        ProcurementDbContext db,PrincipalRfqCommercialRoundOpeningRecord round,CancellationToken ct)
    {
        var plan=await db.PrincipalRfqPreparedPlans.AsNoTracking().SingleAsync(x=>x.Id==round.PreparedPlanId,ct);
        var revision=await db.PrincipalRfqCommercialDraftRevisions.AsNoTracking()
            .SingleAsync(x=>x.Id==plan.DraftRevisionId,ct);
        var head=await db.PrincipalRfqCommercialDrafts.AsNoTracking().SingleAsync(x=>x.Id==plan.DraftId,ct);
        var originalAck=await db.PrincipalRfqCommercialDraftReceipts.AsNoTracking()
            .SingleAsync(x=>x.DraftRevisionId==revision.Id,ct);
        PrincipalRfqPreparedPlanEvidence.Verify(plan,head,revision);
        var input=PrincipalRfqCommercialDraft.RestoreHistorical(head,revision,originalAck);
        if(input.EvaluationDefinitionJson is null)
            throw new Problem("RfqEvaluationPlanUnavailable",409);
        var definition=PrincipalRfqEvaluationPlanDefinition.Parse(input.EvaluationDefinitionJson);
        if(plan.EvaluationRulesSha256!=definition.RulesSha256 || plan.SourcingEventId!=round.SourcingEventId
            || plan.BranchId!=round.BranchId || plan.TenantId!=round.TenantId
            || plan.LegalEntityId!=round.LegalEntityId)
            throw new Problem("RfqEvaluationPlanChanged",409);
        return (definition,plan,revision);
    }

    private static Task<IResult> CommercialRecordCriterionAssessment(Guid id,CriterionAssessmentInput? input,
        HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        => Run(http,scope,async(db,cases,core,auth,token)=>
        {
            await RequireCriterionAssessment(http,db,true,token);
            if(id==Guid.Empty || input is null || input.OfferId==Guid.Empty
                || input.CriterionCode is null or {Length:>64}
                || input.ExpectedPreviousRevision is <0 or >=100
                || input.Rationale is null or {Length:>2000})
                throw new Problem("RfqCriterionAssessmentInputRequired",422);
            var (round,actor)=await OfferOpeningTarget(db,core,scope,http,auth,id,
                PrincipalRfqCriterionAssessmentStorage.Action,token);
            var key=Key(http);
            var fingerprint=Fingerprint(new {operation="RecordCriterionAssessment",roundId=id,
                input.OfferId,input.CriterionCode,input.ExpectedPreviousRevision,
                input.ScoreExact,input.Passed,input.Rationale});
            await LockPrincipalSourcingCommandKey(db,key,token);
            var priorAck=await db.PrincipalRfqCriterionAssessmentReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.CommandKey==key && x.ActorPrincipalId==actor.PrincipalId,token);
            if(priorAck is not null)
            {
                if(priorAck.RoundId!=id || priorAck.RequestSha256!=fingerprint)
                    throw new Problem("RfqCriterionAssessmentCommandConflict",409);
                var priorRow=await db.PrincipalRfqCriterionAssessments.AsNoTracking()
                    .SingleAsync(x=>x.Id==priorAck.AssessmentId,token);
                PrincipalRfqCriterionAssessmentStorage.VerifyReceipt(priorRow,priorAck);
                return new Reply(priorAck.ResponseJson,201);
            }
            await LockOfferOpeningSources(db,scope,round,token);
            var retained=await LoadRetainedCommercialApproval(db,cases,scope,round,token);
            await RequireCommercialSourceHolds(db,cases,scope,retained.Event,retained.Original,actor,token);
            var (_,controls)=await OpeningPolicy(db,scope,round,token,currentEffects:false);
            if(!controls.ReaderPrincipalIds.Contains(actor.PrincipalId,StringComparer.Ordinal))
                throw new Problem("RfqCriterionAssessmentReaderDenied",403);
            var opened=await db.PrincipalRfqOfferOpenings.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.OpeningId==id,token)
                ?? throw new Problem("RfqOffersNotOpened",409);
            var seal=await db.PrincipalRfqCommercialRoundSeals.AsNoTracking()
                .SingleAsync(x=>x.Id==opened.SealId && x.OpeningId==id,token);
            await RequireRetainedOpenedOfferPurpose(db,core,scope,round,opened,seal,
                retained,actor,controls,true,token);
            var (definition,plan,revision)=await AssessmentPlan(db,round,token);
            var offer=(await RetainedOpeningOffers(http,db,scope,round,seal,actor,
                PrincipalRfqCriterionAssessmentStorage.Action,token))
                .SingleOrDefault(x=>x.Id==input.OfferId)
                ?? throw new Problem("RfqOfferNotInOpenedSet",404);
            var value=PrincipalRfqCriterionAssessmentRules.Validate(definition,
                input.CriterionCode,actor.PrincipalId,checked(input.ExpectedPreviousRevision+1),
                input.ScoreExact,input.Passed,input.Rationale,
                offer.OriginalDocumentVersionId,offer.OriginalDocumentSha256);
            var latest=await db.PrincipalRfqCriterionAssessments.AsNoTracking()
                .Where(x=>x.RoundId==id && x.OfferId==offer.Id
                    && x.CriterionCode==value.CriterionCode && x.EvaluatorPrincipalId==actor.PrincipalId)
                .OrderByDescending(x=>x.Revision).ThenByDescending(x=>x.Id)
                .FirstOrDefaultAsync(token);
            if((latest?.Revision??0)!=input.ExpectedPreviousRevision)
                throw new Problem("RfqCriterionAssessmentVersionChanged",409);
            var row=new PrincipalRfqCriterionAssessmentRecord
            {
                TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
                RoundId=id,UnsealingId=opened.Id,OfferId=offer.Id,EventId=round.SourcingEventId,
                BranchId=round.BranchId,PreparedPlanId=plan.Id,EvaluationPlanRevisionId=revision.Id,
                EvaluationDefinitionSha256=definition.RulesSha256,
                ApprovedRoundSha256=round.SnapshotSha256,OpeningSha256=opened.SnapshotSha256,
                OfferSha256=offer.SnapshotSha256,CriterionCode=value.CriterionCode,Kind=value.Kind,
                EvaluatorPrincipalId=actor.PrincipalId,EvaluatorWorkerAliasId=actor.LinkedWorkerId,
                Revision=value.Revision,PreviousAssessmentId=latest?.Id,PreviousRevision=latest?.Revision,
                Score=value.Score,Passed=value.Passed,Rationale=value.Rationale,
                OriginalDocumentVersionId=value.OriginalDocumentVersionId,
                OriginalDocumentSha256=value.OriginalDocumentSha256,
                CommandKey=key,RequestSha256=fingerprint,
                RecordedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value,
            };
            row.SnapshotJson=PrincipalRfqCriterionAssessmentStorage.Snapshot(row);
            row.SnapshotSha256=ProcurementEvidenceEncoding.Digest(row.SnapshotJson);
            PrincipalRfqCriterionAssessmentStorage.Verify(row,round,opened,offer,definition);
            var response=PrincipalRfqCriterionAssessmentStorage.Response(row);
            var ack=new PrincipalRfqCriterionAssessmentReceipt
            {
                TenantId=scope.TenantId,LegalEntityId=scope.LegalEntityId,
                AssessmentId=row.Id,RoundId=id,UnsealingId=opened.Id,OfferId=offer.Id,
                EventId=round.SourcingEventId,BranchId=round.BranchId,ActorPrincipalId=actor.PrincipalId,
                CommandKey=key,RequestSha256=fingerprint,ResponseJson=response,
                ResponseSha256=ProcurementEvidenceEncoding.Digest(response),
            };
            db.AddScoped(row);db.AddScoped(ack);
            db.AddScoped(new AuditEvent {RecordType=nameof(PrincipalRfqCriterionAssessmentRecord),
                RecordId=row.Id,Action="RecordCriterionAssessment",ActorSubjectId=actor.PrincipalId,
                CorrelationId=scope.CorrelationId,
                ChangedFieldsJson=PrincipalRfqCriterionAssessmentStorage.AuditPayload(row)});
            db.AddScoped(new OutboxEvent {EventType=PrincipalRfqCriterionAssessmentStorage.EventType,
                AggregateType=nameof(PrincipalRfqCriterionAssessmentRecord),AggregateId=row.Id,
                IdempotencyKey=key.ToString("D"),PayloadJson=PrincipalRfqCriterionAssessmentStorage.EventPayload(row)});
            await db.SaveChangesAsync(token);
            return new Reply(response,201);
        },ct);

    private static Task<IResult> CommercialOwnCriterionAssessments(Guid id,int? offset,int? limit,
        HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        => Run(http,scope,async(db,cases,core,auth,token)=>
        {
            await RequireCriterionAssessment(http,db,false,token);
            if(id==Guid.Empty || offset is <0 or >100000 || limit is <1 or >50)
                throw new Problem("RfqCriterionAssessmentPageInvalid",422);
            var (round,actor)=await OfferOpeningTarget(db,core,scope,http,auth,id,
                PrincipalRfqCriterionAssessmentStorage.Action,token);
            var (_,controls)=await OpeningPolicy(db,scope,round,token,currentEffects:false);
            if(!controls.ReaderPrincipalIds.Contains(actor.PrincipalId,StringComparer.Ordinal))
                throw new Problem("RfqCriterionAssessmentReaderDenied",403);
            var opened=await db.PrincipalRfqOfferOpenings.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.OpeningId==id,token)
                ?? throw new Problem("RfqOffersNotOpened",404);
            var seal=await db.PrincipalRfqCommercialRoundSeals.AsNoTracking()
                .SingleAsync(x=>x.Id==opened.SealId && x.OpeningId==id,token);
            var retained=await LoadRetainedCommercialApproval(db,cases,scope,round,token);
            await RequireCommercialSourceHolds(db,cases,scope,retained.Event,retained.Original,actor,token);
            await RequireRetainedOpenedOfferPurpose(db,core,scope,round,opened,seal,
                retained,actor,controls,true,token);
            var (definition,plan,revision)=await AssessmentPlan(db,round,token);
            var offers=(await RetainedOpeningOffers(http,db,scope,round,seal,actor,
                PrincipalRfqCriterionAssessmentStorage.Action,token)).ToDictionary(x=>x.Id);
            var own=db.PrincipalRfqCriterionAssessments.AsNoTracking().Where(x=>x.RoundId==id
                && x.TenantId==scope.TenantId && x.LegalEntityId==scope.LegalEntityId
                && x.BranchId==round.BranchId && x.EvaluatorPrincipalId==actor.PrincipalId);
            var total=await own.CountAsync(token);
            var rows=await own.OrderBy(x=>x.OfferId).ThenBy(x=>x.CriterionCode)
                .ThenBy(x=>x.Revision).Skip(offset??0).Take(limit??25).ToArrayAsync(token);
            foreach(var row in rows)
            {
                if(row.PreparedPlanId!=plan.Id || row.EvaluationPlanRevisionId!=revision.Id
                    || !offers.TryGetValue(row.OfferId,out var offer))
                    throw new Problem("RfqAssessmentHistoryChanged",409);
                PrincipalRfqCriterionAssessmentStorage.Verify(row,round,opened,offer,definition);
                var ack=await db.PrincipalRfqCriterionAssessmentReceipts.AsNoTracking()
                    .SingleAsync(x=>x.AssessmentId==row.Id,token);
                PrincipalRfqCriterionAssessmentStorage.VerifyReceipt(row,ack);
            }
            return new Reply(JsonSerializer.Serialize(new {items=rows.Select(x=>new
                {assessmentId=x.Id,x.RoundId,x.OfferId,x.CriterionCode,x.Kind,x.Revision,
                    scoreExact=x.Score?.ToString("0.####",CultureInfo.InvariantCulture),
                    x.Passed,x.Rationale,x.RecordedAt,x.SnapshotSha256}),
                total,offset=offset??0,limit=limit??25,evaluationComplete=false,
                awardMade=false},Json));
        },ct);

    private static Task<IResult> CommercialCriterionAssessmentRecovery(Guid id,Guid key,
        HttpContext http,ProcurementRequestScope scope,CancellationToken ct)
        => Run(http,scope,async(db,cases,core,auth,token)=>
        {
            await RequireCriterionAssessment(http,db,false,token);
            _=await OfferOpeningTarget(db,core,scope,http,auth,id,
                PrincipalRfqCriterionAssessmentStorage.Action,token);
            var ack=await db.PrincipalRfqCriterionAssessmentReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x=>x.CommandKey==key && x.RoundId==id
                    && x.ActorPrincipalId==scope.SubjectId,token)
                ?? throw new Problem("RfqCriterionAssessmentCommandNotFound",404);
            var row=await db.PrincipalRfqCriterionAssessments.AsNoTracking()
                .SingleAsync(x=>x.Id==ack.AssessmentId,token);
            PrincipalRfqCriterionAssessmentStorage.VerifyReceipt(row,ack);
            return new Reply(ack.ResponseJson,201);
        },ct);
}
